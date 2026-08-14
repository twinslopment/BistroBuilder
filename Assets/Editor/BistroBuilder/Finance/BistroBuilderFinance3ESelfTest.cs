using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3ESelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3E - Autotest",
        false,
        3042)]
    public static void RunFromMenu()
    {
        bool ok = Run(
            out int passed,
            out int failed,
            out string report);

        Debug.Log(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 3E",
            "Autotest: " + passed + " OK / " +
            failed + " fallos.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3E — Autotest fallido.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        int capturedErrors = 0;
        var builder = new StringBuilder();

        Application.LogCallback logHandler =
            (condition, stackTrace, type) =>
            {
                if (type == LogType.Error ||
                    type == LogType.Exception ||
                    type == LogType.Assert)
                {
                    capturedErrors++;
                }
            };

        Application.logMessageReceived += logHandler;

        try
        {
            RunPolicyTests(
                ref passed,
                ref failed,
                builder);

            RunServiceTests(
                ref passed,
                ref failed,
                builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine(
                "[ERROR] Excepción inesperada: " +
                exception.GetType().Name + " - " +
                exception.Message);
        }
        finally
        {
            Application.logMessageReceived -= logHandler;
        }

        Check(
            capturedErrors == 0,
            "Console sin Error/Exception/Assert durante el autotest.",
            ref passed,
            ref failed,
            builder);

        builder.Insert(
            0,
            "3E — AUTOTEST GASTOS OPERATIVOS Y NÓMINAS\n" +
            "Correctos: " + passed +
            "  Fallos: " + failed +
            "  Error/Exception/Assert: " +
            capturedErrors + "\n\n");

        report = builder.ToString();
        return failed == 0;
    }

    private static void RunPolicyTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        bool validationOk =
            BistroBuilderFinance3EValidator.ValidateCurrentScene(
                out _,
                out int validationErrors,
                out _);

        Check(
            validationOk && validationErrors == 0,
            "La instalación 3E supera el validador estructural.",
            ref passed,
            ref failed,
            builder);

        var expense =
            new BistroBuilderRecurringExpenseDefinition(
                "utilities_test",
                "Suministros de prueba",
                "expense.operating.utilities",
                2500L,
                7,
                7);

        Check(
            expense.TryValidate(out _),
            "Una definición recurrente válida es aceptada.",
            ref passed,
            ref failed,
            builder);

        Check(
            !expense.IsDueOnDay(6),
            "El gasto no vence antes de su primer día.",
            ref passed,
            ref failed,
            builder);

        Check(
            expense.IsDueOnDay(7),
            "El gasto vence en su primer día.",
            ref passed,
            ref failed,
            builder);

        Check(
            expense.IsDueOnDay(14),
            "La recurrencia vuelve a vencer en su intervalo.",
            ref passed,
            ref failed,
            builder);

        Check(
            !expense.IsDueOnDay(8),
            "Un día intermedio no produce gasto.",
            ref passed,
            ref failed,
            builder);

        Check(
            BistroBuilderOperatingExpensePolicy.TryGetNextDueDay(
                expense,
                8,
                out int nextDue) &&
            nextDue == 14,
            "El siguiente vencimiento se calcula sin iterar días.",
            ref passed,
            ref failed,
            builder);

        Check(
            BistroBuilderOperatingExpensePolicy
                .TryBuildOperatingTransactionRequest(
                    expense,
                    7,
                    480,
                    out BistroBuilderFinanceTransactionRequest
                        operatingRequest,
                    out _),
            "El vencimiento construye una operación financiera válida.",
            ref passed,
            ref failed,
            builder);

        Check(
            operatingRequest != null &&
            operatingRequest.kind ==
                BistroBuilderFinanceTransactionKind.Debit &&
            operatingRequest.amountCents == 2500L &&
            operatingRequest.categoryId ==
                "expense.operating.utilities" &&
            operatingRequest.dayIndex == 7,
            "El débito operativo conserva importe, categoría y fecha.",
            ref passed,
            ref failed,
            builder);

        string expectedOperationId =
            BistroBuilderOperatingExpensePolicy
                .BuildOperatingOperationId(
                    "utilities_test",
                    7);

        Check(
            operatingRequest != null &&
            operatingRequest.operationId ==
                expectedOperationId,
            "El OperationId recurrente es determinista por gasto y día.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderOperatingExpenseProfile validProfile =
            ScriptableObject.CreateInstance<
                BistroBuilderOperatingExpenseProfile>();

        validProfile.ConfigureForEditor(
            "profile_test",
            new List<BistroBuilderRecurringExpenseDefinition>
            {
                expense
            });

        Check(
            validProfile.TryValidate(out _),
            "Un perfil con identidades únicas es válido.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderOperatingExpenseProfile duplicateProfile =
            ScriptableObject.CreateInstance<
                BistroBuilderOperatingExpenseProfile>();

        duplicateProfile.ConfigureForEditor(
            "profile_duplicate",
            new List<BistroBuilderRecurringExpenseDefinition>
            {
                expense,
                new BistroBuilderRecurringExpenseDefinition(
                    "utilities_test",
                    "Duplicado",
                    "expense.operating.other",
                    100L,
                    1,
                    1)
            });

        Check(
            !duplicateProfile.TryValidate(out _),
            "ExpenseId duplicado se rechaza.",
            ref passed,
            ref failed,
            builder);

        var badCategory =
            new BistroBuilderRecurringExpenseDefinition(
                "bad_category",
                "Categoría incorrecta",
                "expense.marketing",
                100L,
                1,
                1);

        Check(
            !badCategory.TryValidate(out _),
            "3E no acepta categorías fuera de expense.operating.",
            ref passed,
            ref failed,
            builder);

        var payroll = ValidPayroll();

        Check(
            BistroBuilderOperatingExpensePolicy
                .TryValidatePayrollBatch(
                    payroll,
                    out _),
            "Una nómina resumida válida es aceptada.",
            ref passed,
            ref failed,
            builder);

        Check(
            BistroBuilderOperatingExpensePolicy
                .TryBuildPayrollTransactionRequest(
                    payroll,
                    8,
                    510,
                    out BistroBuilderFinanceTransactionRequest
                        payrollRequest,
                    out _),
            "El contrato de nómina construye una operación válida.",
            ref passed,
            ref failed,
            builder);

        Check(
            payrollRequest != null &&
            payrollRequest.kind ==
                BistroBuilderFinanceTransactionKind.Debit &&
            payrollRequest.sourceSystemId ==
                BistroBuilderOperatingExpensePolicy
                    .PayrollSourceSystemId &&
            payrollRequest.categoryId ==
                BistroBuilderOperatingExpensePolicy
                    .PayrollCategoryId &&
            payrollRequest.amountCents == payroll.totalCents,
            "La nómina se publica como gasto de Personal, no como roster financiero.",
            ref passed,
            ref failed,
            builder);

        Check(
            payrollRequest != null &&
            payrollRequest.operationId ==
                BistroBuilderOperatingExpensePolicy
                    .BuildPayrollOperationId(
                        payroll.payrollRunId),
            "PayrollRunId produce un OperationId determinista.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderPayrollBatchRequest badPeriod =
            ValidPayroll();
        badPeriod.periodStartDayIndex = 8;
        badPeriod.periodEndDayIndex = 7;

        Check(
            !BistroBuilderOperatingExpensePolicy
                .TryValidatePayrollBatch(
                    badPeriod,
                    out _),
            "Un periodo de nómina invertido se rechaza.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderPayrollBatchRequest noEmployees =
            ValidPayroll();
        noEmployees.employeeCount = 0;

        Check(
            !BistroBuilderOperatingExpensePolicy
                .TryValidatePayrollBatch(
                    noEmployees,
                    out _),
            "Una nómina sin personas se rechaza.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderPayrollBatchRequest zeroPayroll =
            ValidPayroll();
        zeroPayroll.totalCents = 0L;

        Check(
            !BistroBuilderOperatingExpensePolicy
                .TryValidatePayrollBatch(
                    zeroPayroll,
                    out _),
            "Una nómina de importe cero se rechaza.",
            ref passed,
            ref failed,
            builder);

        UnityEngine.Object.DestroyImmediate(validProfile);
        UnityEngine.Object.DestroyImmediate(duplicateProfile);
    }

    private static void RunServiceTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject testObject =
            new GameObject("BB_3E_OperatingExpenseSelfTest");

        BistroBuilderOperatingExpenseProfile profile =
            ScriptableObject.CreateInstance<
                BistroBuilderOperatingExpenseProfile>();

        try
        {
            testObject.AddComponent<GameClock>();
            BistroBuilderGeneralGameStateService generalState =
                testObject.AddComponent<
                    BistroBuilderGeneralGameStateService>();
            testObject.AddComponent<BistroBuilderSaveGameService>();
            BistroBuilderFinanceService finance =
                testObject.AddComponent<BistroBuilderFinanceService>();

            profile.ConfigureForEditor(
                "profile_service_test",
                new List<BistroBuilderRecurringExpenseDefinition>
                {
                    new BistroBuilderRecurringExpenseDefinition(
                        "daily_test",
                        "Gasto diario de prueba",
                        "expense.operating.test",
                        1000L,
                        1,
                        1)
                });

            BistroBuilderOperatingExpenseService service =
                testObject.AddComponent<
                    BistroBuilderOperatingExpenseService>();

            SetReference(
                service,
                "expenseProfile",
                profile);

            Check(
                finance.TryInitializeFresh(out _) &&
                service.ValidateConfiguration(out _),
                "El servicio 3E temporal comparte autoridades válidas.",
                ref passed,
                ref failed,
                builder);

            long openingBalance = finance.CurrentBalanceCents;

            Check(
                service.TryProcessCurrentDay(
                    out int postedCount,
                    out _) &&
                postedCount == 1,
                "El gasto vencido se publica exactamente una vez.",
                ref passed,
                ref failed,
                builder);

            Check(
                finance.CurrentBalanceCents ==
                    openingBalance - 1000L &&
                finance.TransactionCount == 1,
                "El gasto recurrente reduce la caja y usa finance.runtime.",
                ref passed,
                ref failed,
                builder);

            Check(
                service.TryProcessCurrentDay(
                    out int replayedCount,
                    out _) &&
                replayedCount == 0 &&
                finance.TransactionCount == 1,
                "Reprocesar el mismo día es idempotente.",
                ref passed,
                ref failed,
                builder);

            BistroBuilderPayrollBatchRequest payroll =
                ValidPayroll();

            Check(
                service.TryPostPayrollBatch(
                    payroll,
                    out _,
                    out bool payrollReplayed,
                    out _) &&
                !payrollReplayed &&
                finance.TransactionCount == 2,
                "Una nómina nueva produce un único débito.",
                ref passed,
                ref failed,
                builder);

            Check(
                service.TryPostPayrollBatch(
                    payroll,
                    out _,
                    out bool secondPayrollReplay,
                    out _) &&
                secondPayrollReplay &&
                finance.TransactionCount == 2,
                "Reintentar la misma nómina no duplica el pago.",
                ref passed,
                ref failed,
                builder);

            BistroBuilderPayrollBatchRequest conflict =
                ValidPayroll();
            conflict.totalCents++;

            Check(
                !service.TryPostPayrollBatch(
                    conflict,
                    out _,
                    out _,
                    out _) &&
                finance.TransactionCount == 2,
                "El mismo PayrollRunId con otro importe se rechaza.",
                ref passed,
                ref failed,
                builder);

            BistroBuilderFinanceSnapshot snapshot =
                finance.CreateSnapshot();

            Check(
                snapshot != null &&
                BistroBuilderFinanceEngine.TryValidateSnapshot(
                    snapshot,
                    out _) &&
                generalState.DayIndex == 1,
                "Ledger final válido y calendario de la prueba intacto.",
                ref passed,
                ref failed,
                builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static BistroBuilderPayrollBatchRequest ValidPayroll()
    {
        return new BistroBuilderPayrollBatchRequest
        {
            payrollRunId = "payroll_test_0001",
            periodStartDayIndex = 1,
            periodEndDayIndex = 7,
            employeeCount = 3,
            totalCents = 43210L
        };
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe " + fieldName +
                " en " + target.GetType().Name + ".");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }
}
