using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest estático de los gates de endurecimiento 4D añadidos antes de 4E.
/// No necesita Play Mode ni modifica escenas guardadas.
/// </summary>
public static class BistroBuilderStaff4DHardeningSelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    private const string EligibilityBatchPath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffEligibilityBatch.cs";

    private const string DomainSessionModelsPath =
        "Assets/Scripts/Domain/Staff/BistroBuilderStaffSessionModels.cs";

    private const string ApplicationSessionViewsPath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionViews.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest endurecimiento",
        false,
        3233)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D endurecimiento",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest de endurecimiento 4D ha fallado.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4D HARDENING / AUTOTEST ===");

        GameObject waiterObject = null;
        try
        {
            waiterObject = new GameObject("4D_Hardening_Test_Waiter");
            Waiter waiter = waiterObject.AddComponent<Waiter>();

            var session = new BistroBuilderStaffSessionSnapshot
            {
                schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
                revision = 1L,
                active = true,
                sessionId = BistroBuilderStaffSessionIdUtility.CreateNew(),
                dayIndex = 1,
                bindings = new List<BistroBuilderStaffSessionBindingRecord>
                {
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = BistroBuilderEmployeeIdUtility.CreateNew(),
                        waiterId = waiter.WaiterId,
                        handledTableIds = new List<int>()
                    }
                }
            };

            var waiters = new Dictionary<int, Waiter>
            {
                { waiter.WaiterId, waiter }
            };

            Check(
                waiter.CurrentState == WaiterState.Idle && waiter.IsAvailable,
                "El agente de prueba parte libre y elegible.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out string idleError),
                "El preflight acepta una sesión cuyos agentes están libres. " +
                idleError,
                ref passed, ref failed, log);

            waiter.SetState(WaiterState.TakingOrder);
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out _),
                "El preflight bloquea consolidación mientras un agente trabaja.",
                ref passed, ref failed, log);

            waiter.SetState(WaiterState.Idle);
            Check(
                waiter.TrySetStaffServiceEligibility(false) &&
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out _),
                "El preflight rechaza un binding cuya elegibilidad se perdió.",
                ref passed, ref failed, log);

            Check(
                waiter.TrySetStaffServiceEligibility(true),
                "La elegibilidad del agente de prueba puede restaurarse.",
                ref passed, ref failed, log);

            var missing = new Dictionary<int, Waiter>();
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    missing,
                    out _),
                "El preflight rechaza WaiterId inexistentes.",
                ref passed, ref failed, log);

            var duplicateSession = session.DeepClone();
            duplicateSession.bindings.Add(
                duplicateSession.bindings[0].DeepClone());
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    duplicateSession,
                    waiters,
                    out _),
                "El preflight rechaza bindings duplicados.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffEligibilityBatch.TryApply(
                    new[] { waiter },
                    false,
                    out string batchDisableError) &&
                !waiter.IsStaffServiceEligible,
                "El lote transaccional puede desactivar agentes libres. " +
                batchDisableError,
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffEligibilityBatch.TryApply(
                    new[] { waiter },
                    true,
                    out string batchEnableError) &&
                waiter.IsStaffServiceEligible,
                "El lote transaccional restaura elegibilidad. " +
                batchEnableError,
                ref passed, ref failed, log);

            string source = ReadSource(SessionServicePath);
            string batchSource = ReadSource(EligibilityBatchPath);

            int eligibilityMethodIndex = source.IndexOf(
                "private bool TrySetAllWaitersEligible",
                StringComparison.Ordinal);
            int eligibilityBatchIndex = source.IndexOf(
                "BistroBuilderStaffEligibilityBatch.TryApply(",
                eligibilityMethodIndex >= 0 ? eligibilityMethodIndex : 0,
                StringComparison.Ordinal);
            int nextEligibilityMethodIndex = source.IndexOf(
                "private Waiter[] BuildOrderedWaiterArray",
                eligibilityMethodIndex >= 0 ? eligibilityMethodIndex : 0,
                StringComparison.Ordinal);

            Check(
                eligibilityMethodIndex >= 0 &&
                eligibilityBatchIndex > eligibilityMethodIndex &&
                (nextEligibilityMethodIndex < 0 ||
                 eligibilityBatchIndex < nextEligibilityMethodIndex),
                "TrySetAllWaitersEligible delega realmente en el lote " +
                "transaccional de elegibilidad.",
                ref passed, ref failed, log);

            Check(
                batchSource.Contains(
                    "IEnumerable<KeyValuePair<Waiter, bool>> targets"),
                "El batch 4D soporta un plan mixto atómico por Waiter.",
                ref passed, ref failed, log);

            int restoreMethodIndex = source.IndexOf(
                "public bool TryRestoreSessionSnapshot",
                StringComparison.Ordinal);
            int resumeMethodIndex = source.IndexOf(
                "public bool TryResumeAfterRuntimeLoad",
                restoreMethodIndex >= 0 ? restoreMethodIndex : 0,
                StringComparison.Ordinal);
            string restoreBody = Slice(
                source,
                restoreMethodIndex,
                resumeMethodIndex);

            Check(
                restoreMethodIndex >= 0 &&
                restoreBody.Contains(
                    "BistroBuilderStaffSessionRestorePreflight.TryValidate(") &&
                restoreBody.Contains(
                    "TryApplyEligibilityForSnapshot(candidate, out error)") &&
                !restoreBody.Contains("TrySetAllWaitersEligible(false") &&
                !restoreBody.Contains("boundWaiters") &&
                !restoreBody.Contains("TrySetAllWaitersEligible(true"),
                "TryRestoreSessionSnapshot preflighta y aplica un único plan " +
                "mixto, sin activación por fases ni recuperación global.",
                ref passed, ref failed, log);

            int rehydrateMethodIndex = source.IndexOf(
                "private bool TryRehydrateRuntimeFromCurrentState",
                StringComparison.Ordinal);
            int applyPlanMethodIndex = source.IndexOf(
                "private bool TryApplyEligibilityForSnapshot",
                rehydrateMethodIndex >= 0 ? rehydrateMethodIndex : 0,
                StringComparison.Ordinal);
            string rehydrateBody = Slice(
                source,
                rehydrateMethodIndex,
                applyPlanMethodIndex);

            Check(
                rehydrateMethodIndex >= 0 &&
                rehydrateBody.Contains(
                    "BistroBuilderStaffSessionRestorePreflight.TryValidate(") &&
                rehydrateBody.Contains(
                    "TryApplyEligibilityForSnapshot(sessionState, out error)") &&
                !rehydrateBody.Contains("TrySetAllWaitersEligible(false") &&
                !rehydrateBody.Contains("boundWaiters"),
                "TryRehydrateRuntimeFromCurrentState construye primero el " +
                "runtime y usa después un único plan de elegibilidad atómico.",
                ref passed, ref failed, log);

            int finalizeMethodIndex = source.IndexOf(
                "public bool TryFinalizeClosedSession",
                StringComparison.Ordinal);
            int closePreflightIndex = source.IndexOf(
                "BistroBuilderStaffSessionClosePreflight.TryValidate(",
                finalizeMethodIndex >= 0 ? finalizeMethodIndex : 0,
                StringComparison.Ordinal);
            int finalizeObservedIndex = source.IndexOf(
                "FinalizeObservedWorkCycle(",
                finalizeMethodIndex >= 0 ? finalizeMethodIndex : 0,
                StringComparison.Ordinal);
            int applyResultIndex = source.IndexOf(
                "developmentService.TryApplyServiceResult(",
                finalizeMethodIndex >= 0 ? finalizeMethodIndex : 0,
                StringComparison.Ordinal);

            Check(
                finalizeMethodIndex >= 0 &&
                closePreflightIndex > finalizeMethodIndex &&
                finalizeObservedIndex > closePreflightIndex &&
                applyResultIndex > closePreflightIndex,
                "TryFinalizeClosedSession ejecuta el preflight antes de " +
                "consolidar ciclos o publicar XP/rendimiento.",
                ref passed, ref failed, log);

            string domainSource = ReadSource(DomainSessionModelsPath);
            string applicationViewsSource = ReadSource(ApplicationSessionViewsPath);

            bool domainOwnsOnlyPersistedSessionModels =
                !domainSource.Contains(
                    "class BistroBuilderEmployeeSessionAssignmentView") &&
                !domainSource.Contains(
                    "class BistroBuilderStaffCoverageSnapshot") &&
                !domainSource.Contains("WaiterState");
            Check(
                domainOwnsOnlyPersistedSessionModels,
                "Domain no vuelve a declarar vistas Application ni depende de WaiterState.",
                ref passed, ref failed, log);

            bool applicationOwnsSessionViews =
                applicationViewsSource.Contains(
                    "class BistroBuilderEmployeeSessionAssignmentView") &&
                applicationViewsSource.Contains(
                    "class BistroBuilderStaffCoverageSnapshot");
            Check(
                applicationOwnsSessionViews,
                "Application conserva la propiedad única de las vistas consultivas 4D.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        finally
        {
            if (waiterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(waiterObject);
            }
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Slice(string source, int start, int end)
    {
        if (string.IsNullOrEmpty(source) || start < 0)
        {
            return string.Empty;
        }
        int safeEnd = end > start && end <= source.Length
            ? end
            : source.Length;
        return source.Substring(start, safeEnd - start);
    }

    private static string ReadSource(string assetPath)
    {
        string absolutePath = Path.GetFullPath(assetPath);
        return File.Exists(absolutePath)
            ? File.ReadAllText(absolutePath)
            : string.Empty;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + text);
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
