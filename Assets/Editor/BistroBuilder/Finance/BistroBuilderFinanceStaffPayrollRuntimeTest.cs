using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prueba reversible de la integración Personal/Horarios -> nómina 3E -> caja 3A.
/// Abre y cierra un servicio real sin dejar cambios en Finance ni Staff.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderFinanceStaffPayrollRuntimeTest
{
    private const string ArmedKey = "BB.Finance.StaffPayroll.Armed";
    private const string CliKey = "BB.Finance.StaffPayroll.Cli";
    private const string ReportPath = "Block3FinanceStaffPayrollRuntime.txt";
    private const double StartupTimeout = 25d;

    private static double deadline;
    private static int settleFrames;
    private static int capturedErrors;

    static BistroBuilderFinanceStaffPayrollRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    [MenuItem("Tools/Bistro Builder/Finanzas/3E - Prueba nómina Staff real", false, 3111)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        Begin(true);
    }

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlaying || SessionState.GetBool(ArmedKey, false))
        {
            if (commandLine) EditorApplication.Exit(1);
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.SetBool(CliKey, commandLine);
        capturedErrors = 0;
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            deadline = EditorApplication.timeSinceStartup + StartupTimeout;
            settleFrames = 5;
            Application.logMessageReceived += HandleLog;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Update;
            Application.logMessageReceived -= HandleLog;
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            bool cli = SessionState.GetBool(CliKey, false);
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetBool(CliKey, false);
            if (cli)
            {
                string report = File.Exists(Path.GetFullPath(ReportPath))
                    ? File.ReadAllText(Path.GetFullPath(ReportPath))
                    : string.Empty;
                EditorApplication.Exit(report.Contains("[PASS]") ? 0 : 1);
            }
        }
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        if (settleFrames > 0)
        {
            settleFrames--;
            return;
        }

        if (TryResolve(out _, out _, out _, out _, out _))
        {
            EditorApplication.update -= Update;
            Execute();
            return;
        }

        if (EditorApplication.timeSinceStartup >= deadline)
        {
            EditorApplication.update -= Update;
            Complete(false, "No se inicializaron las autoridades de nómina dentro del tiempo esperado.");
        }
    }

    private static void Execute()
    {
        if (!TryResolve(
                out BistroBuilderFinanceService finance,
                out BistroBuilderStaffService staff,
                out BistroBuilderStaffSessionService session,
                out BistroBuilderStaffPayrollFinanceBridge payroll,
                out RestaurantServiceStateService serviceState))
        {
            Complete(false, "No se resolvieron las autoridades requeridas.");
            return;
        }

        string error = string.Empty;
        if (!serviceState.IsClosed ||
            !payroll.ValidateConfiguration(out error))
        {
            Complete(false, "La base de la prueba no está Closed/configurada. " + error);
            return;
        }

        BistroBuilderFinanceSnapshot financeBaseline = finance.CreateSnapshot();
        BistroBuilderStaffSnapshot staffBaseline = staff.CreateSnapshot();
        BistroBuilderStaffSessionSnapshot sessionBaseline = session.CreateSessionSnapshot();
        if (financeBaseline == null || staffBaseline == null)
        {
            Complete(false, "No se pudieron capturar snapshots de rollback.");
            return;
        }

        int baselineTransactions = finance.TransactionCount;
        long baselineCash = finance.CurrentBalanceCents;

        if (!TryFindPayrollWaiter(staff, staff.CreateSnapshot(), out _) &&
            !session.TryEnsureSessionStarted(out string bootstrapError))
        {
            Restore(finance, financeBaseline, staff, staffBaseline);
            Complete(false, "No se pudo preparar Personal para proyectar nómina. " + bootstrapError);
            return;
        }

        BistroBuilderStaffScheduleService schedule =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffScheduleService>();
        BistroBuilderOperatingExpenseService operating =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderOperatingExpenseService>();
        BistroBuilderFinancingService financing =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancingService>();
        string projectionError = string.Empty;
        if (schedule == null || operating == null || financing == null ||
            !TryValidateSalaryProjection(
                staff,
                schedule,
                operating,
                financing,
                out projectionError))
        {
            if (sessionBaseline != null) session.TryRestoreSessionSnapshot(sessionBaseline, out _);
            Restore(finance, financeBaseline, staff, staffBaseline);
            Complete(false,
                "La proyección salarial Staff -> 3E -> 3I no quedó integrada. " +
                projectionError);
            return;
        }


        if (!serviceState.TryOpenService() || !session.HasActiveSession)
        {
            Restore(finance, financeBaseline, staff, staffBaseline);
            Complete(false, "No se pudo abrir una sesión real de Personal.");
            return;
        }

        if (!payroll.TryRefreshActiveSession(out error) ||
            payroll.ActiveEmployeeCount <= 0 ||
            payroll.ActivePayrollCents <= 0L)
        {
            serviceState.TryCloseServiceImmediately();
            Restore(finance, financeBaseline, staff, staffBaseline);
            Complete(false, "La sesión real no produjo una nómina positiva. " + error);
            return;
        }

        long expectedPayroll = payroll.ActivePayrollCents;
        int expectedEmployees = payroll.ActiveEmployeeCount;
        string sessionId = payroll.ActiveSessionId;

        if (!serviceState.TryBeginClosing() ||
            !serviceState.TryCompleteClosing() ||
            session.HasActiveSession)
        {
            serviceState.TryCloseServiceImmediately();
            Restore(finance, financeBaseline, staff, staffBaseline);
            Complete(false, "El cierre real no finalizó la sesión de Personal.");
            return;
        }

        BistroBuilderFinanceSnapshot after = finance.CreateSnapshot();
        BistroBuilderFinanceTransactionRecord payrollTx = FindNewPayroll(
            after,
            financeBaseline.nextTransactionSequence);

        bool payrollValid = payrollTx != null &&
            payrollTx.kind == BistroBuilderFinanceTransactionKind.Debit &&
            payrollTx.categoryId == BistroBuilderOperatingExpensePolicy.PayrollCategoryId &&
            payrollTx.sourceSystemId == BistroBuilderOperatingExpensePolicy.PayrollSourceSystemId &&
            payrollTx.amountCents == expectedPayroll &&
            finance.TransactionCount == baselineTransactions + 1 &&
            finance.CurrentBalanceCents == baselineCash - expectedPayroll;

        bool restored = Restore(finance, financeBaseline, staff, staffBaseline) &&
            serviceState.IsClosed &&
            !session.HasActiveSession &&
            SnapshotsEqual(financeBaseline, finance.CreateSnapshot()) &&
            SnapshotsEqual(staffBaseline, staff.CreateSnapshot());

        if (!payrollValid)
        {
            Complete(false,
                "La sesión terminó, pero 3E no publicó exactamente una nómina canónica.");
            return;
        }
        if (!restored)
        {
            Complete(false,
                "La nómina fue correcta, pero el rollback Finance/Staff no quedó exacto.");
            return;
        }
        if (capturedErrors != 0)
        {
            Complete(false,
                "Se observaron " + capturedErrors + " Error/Exception/Assert.");
            return;
        }

        Complete(true,
            "sesión " + sessionId + " · " + expectedEmployees +
            " empleado(s) · nómina " + expectedPayroll +
            " céntimos · débito 3E/3A exacto · rollback íntegro.");
    }

    private static bool TryValidateSalaryProjection(
        BistroBuilderStaffService staff,
        BistroBuilderStaffScheduleService schedule,
        BistroBuilderOperatingExpenseService operating,
        BistroBuilderFinancingService financing,
        out string error)
    {
        error = string.Empty;
        BistroBuilderStaffScheduleSnapshot baseline = schedule.CreateSnapshot();
        if (baseline == null || !schedule.EnsureReady(out error))
        {
            return false;
        }

        BistroBuilderStaffSnapshot staffSnapshot = staff.CreateSnapshot();
        if (!TryFindPayrollWaiter(staff, staffSnapshot, out BistroBuilderEmployeeRecord employee))
        {
            error = "No existe un camarero activo/disponible con salario positivo para probar la proyección.";
            return false;
        }

        int today = schedule.CurrentDayIndex;
        int targetDay = today + 1;
        var ids = new[] { employee.employeeId };

        if (!operating.TryCalculateRecurringObligationsCents(
                today, today + financing.LiquidityHorizonDays,
                out long operatingBefore, out error) ||
            !financing.TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition liquidityBefore, out error))
        {
            return false;
        }

        bool restored = false;
        try
        {
            if (!schedule.TryReplaceServiceAssignments(
                    targetDay,
                    BistroBuilderMealServiceAvailability.Lunch,
                    ids,
                    out error))
            {
                return false;
            }

            BistroBuilderStaffPayrollFinanceBridge bridge =
                operating.StaffPayrollFinanceBridge;
            if (bridge == null ||
                !bridge.TryCalculateScheduledPayrollObligationsCents(
                    today, today + financing.LiquidityHorizonDays,
                    out long payrollAfter, out int serviceCount, out error) ||
                payrollAfter <= 0L || serviceCount <= 0)
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "El puente salarial no proyectó la asignación recién creada.";
                return false;
            }

            if (!operating.TryCalculateRecurringObligationsCents(
                    today, today + financing.LiquidityHorizonDays,
                    out long operatingAfter, out error) ||
                !financing.TryGetLiquidityPosition(
                    out BistroBuilderLiquidityPosition liquidityAfter, out error))
            {
                return false;
            }

            long operatingDelta = checked(operatingAfter - operatingBefore);
            long liquidityObligationDelta = checked(
                liquidityAfter.recurringOperatingObligationsWithinHorizonCents -
                liquidityBefore.recurringOperatingObligationsWithinHorizonCents);
            long projectedLiquidityDelta = checked(
                liquidityBefore.projectedLiquidityAfterHorizonObligationsCents -
                liquidityAfter.projectedLiquidityAfterHorizonObligationsCents);

            if (operatingDelta != liquidityObligationDelta ||
                liquidityObligationDelta != projectedLiquidityDelta ||
                operatingDelta <= 0L)
            {
                error = "3I no absorbió exactamente el incremento salarial de 3E.";
                return false;
            }

            if (!schedule.TryRestoreSnapshot(baseline, out string restoreSuccessError) ||
                !string.Equals(
                    JsonUtility.ToJson(baseline),
                    JsonUtility.ToJson(schedule.CreateSnapshot()),
                    StringComparison.Ordinal))
            {
                error = "La proyección fue correcta, pero staff.schedule no restauró exactamente. " +
                    restoreSuccessError;
                return false;
            }
            restored = true;
            return true;
        }
        catch (OverflowException)
        {
            error = "La prueba de proyección salarial desbordó el rango monetario.";
            return false;
        }
        finally
        {
            restored = schedule.TryRestoreSnapshot(baseline, out string restoreError);
            if (!restored && string.IsNullOrWhiteSpace(error))
                error = "No se pudo restaurar staff.schedule: " + restoreError;
        }
    }

    private static bool TryFindPayrollWaiter(
        BistroBuilderStaffService staff,
        BistroBuilderStaffSnapshot snapshot,
        out BistroBuilderEmployeeRecord employee)
    {
        employee = null;
        if (snapshot == null || snapshot.employees == null) return false;
        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord candidate = snapshot.employees[index];
            if (candidate == null ||
                candidate.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                candidate.availability != BistroBuilderEmployeeAvailability.Available ||
                candidate.salaryCentsPerService <= 0L ||
                !staff.TryGetRoleDefinition(
                    candidate.roleId, out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
            {
                continue;
            }

            employee = candidate;
            return true;
        }
        return false;
    }
    private static BistroBuilderFinanceTransactionRecord FindNewPayroll(
        BistroBuilderFinanceSnapshot snapshot,
        long firstSequence)
    {
        if (snapshot == null || snapshot.transactions == null) return null;
        for (int index = 0; index < snapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord tx = snapshot.transactions[index];
            if (tx != null && tx.sequence >= firstSequence &&
                tx.categoryId == BistroBuilderOperatingExpensePolicy.PayrollCategoryId)
            {
                return tx;
            }
        }
        return null;
    }

    private static bool Restore(
        BistroBuilderFinanceService finance,
        BistroBuilderFinanceSnapshot financeBaseline,
        BistroBuilderStaffService staff,
        BistroBuilderStaffSnapshot staffBaseline)
    {
        bool financeOk = finance != null && financeBaseline != null &&
            finance.TryRestoreSnapshot(financeBaseline, out _);
        bool staffOk = staff != null && staffBaseline != null &&
            staff.TryRestoreSnapshot(staffBaseline, out _);
        return financeOk && staffOk;
    }

    private static bool SnapshotsEqual(
        BistroBuilderFinanceSnapshot left,
        BistroBuilderFinanceSnapshot right) =>
        left != null && right != null &&
        string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);

    private static bool SnapshotsEqual(
        BistroBuilderStaffSnapshot left,
        BistroBuilderStaffSnapshot right) =>
        left != null && right != null &&
        string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);

    private static bool TryResolve(
        out BistroBuilderFinanceService finance,
        out BistroBuilderStaffService staff,
        out BistroBuilderStaffSessionService session,
        out BistroBuilderStaffPayrollFinanceBridge payroll,
        out RestaurantServiceStateService serviceState)
    {
        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        staff = UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffService>();
        session = UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffSessionService>();
        payroll = UnityEngine.Object.FindFirstObjectByType<BistroBuilderStaffPayrollFinanceBridge>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();

        return finance != null && finance.IsInitialized &&
            staff != null && staff.IsInitialized &&
            session != null && payroll != null && serviceState != null;
    }

    private static void HandleLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors++;
        }
    }

    private static void Complete(bool success, string message)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= HandleLog;

        string report =
            "=== BISTRO BUILDER — 3E / NÓMINA STAFF REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
