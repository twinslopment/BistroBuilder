using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4D — Gate estático de elegibilidad durante restore/rehidratación.
///
/// Impide volver a una restauración por fases. El snapshot completo debe
/// convertirse primero en un plan mixto Waiter→elegibilidad y aplicarse como
/// una única transacción mediante BistroBuilderStaffEligibilityBatch.
/// </summary>
public static class BistroBuilderStaff4DRestoreEligibilitySelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest restore elegibilidad",
        false,
        3235)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D restore elegibilidad",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El gate 4D de restore de elegibilidad ha fallado.");
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
        log.AppendLine(
            "=== BISTRO BUILDER — 4D RESTORE ELIGIBILITY / AUTOTEST ===");

        string absolutePath = Path.GetFullPath(SessionServicePath);
        string source = File.Exists(absolutePath)
            ? File.ReadAllText(absolutePath)
            : string.Empty;

        Check(
            !string.IsNullOrWhiteSpace(source),
            "Existe BistroBuilderStaffSessionService.cs.",
            ref passed,
            ref failed,
            log);

        string restore = ExtractMethod(
            source,
            "public bool TryRestoreSessionSnapshot",
            "public bool TryResumeAfterRuntimeLoad");
        string rehydrate = ExtractMethod(
            source,
            "private bool TryRehydrateRuntimeFromCurrentState",
            "private bool TryApplyEligibilityForSnapshot");
        string eligibilityPlan = ExtractMethod(
            source,
            "private bool TryApplyEligibilityForSnapshot",
            "private void RebuildBindingDictionariesFromState");

        Check(
            restore.Contains(
                "BistroBuilderStaffSessionRestorePreflight.TryValidate(") &&
            restore.Contains(
                "TryApplyEligibilityForSnapshot(candidate, out error)"),
            "TryRestoreSessionSnapshot preflighta y delega en el plan " +
            "transaccional completo.",
            ref passed,
            ref failed,
            log);

        Check(
            !restore.Contains("waiter.TrySetStaffServiceEligibility") &&
            !restore.Contains("TrySetAllWaitersEligible(") &&
            !restore.Contains("boundWaiters"),
            "TryRestoreSessionSnapshot no aplica elegibilidad por fases ni " +
            "usa fallback global.",
            ref passed,
            ref failed,
            log);

        Check(
            rehydrate.Contains(
                "BistroBuilderStaffSessionRestorePreflight.TryValidate(") &&
            rehydrate.Contains(
                "TryApplyEligibilityForSnapshot(sessionState, out error)"),
            "TryRehydrateRuntimeFromCurrentState valida primero y aplica " +
            "después el mismo plan atómico.",
            ref passed,
            ref failed,
            log);

        Check(
            !rehydrate.Contains("waiter.TrySetStaffServiceEligibility") &&
            !rehydrate.Contains("TrySetAllWaitersEligible(") &&
            !rehydrate.Contains("boundWaiters"),
            "La rehidratación no activa bindings individualmente.",
            ref passed,
            ref failed,
            log);

        Check(
            eligibilityPlan.Contains("eligibilityPlanBuffer.Clear()") &&
            eligibilityPlan.Contains("new KeyValuePair<Waiter, bool>(") &&
            eligibilityPlan.Contains(
                "snapshot.active && boundWaiterIds.Contains(pair.Key)") &&
            eligibilityPlan.Contains(
                "BistroBuilderStaffEligibilityBatch.TryApply("),
            "TryApplyEligibilityForSnapshot construye un objetivo explícito " +
            "para cada Waiter y aplica un único batch mixto.",
            ref passed,
            ref failed,
            log);

        log.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string ExtractMethod(
        string source,
        string startMarker,
        string endMarker)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = source.Length;
        }
        return source.Substring(start, end - start);
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
