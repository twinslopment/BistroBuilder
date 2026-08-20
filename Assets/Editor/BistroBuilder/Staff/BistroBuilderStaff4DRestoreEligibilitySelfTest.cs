using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4D — Gate estático de elegibilidad durante restore/rehidratación.
///
/// Impide que Save/Load vuelva a activar bindings Waiter uno a uno o que un
/// fallo restaure globalmente agentes no ligados. La activación debe pasar por
/// BistroBuilderStaffEligibilityBatch sobre el subconjunto ligado.
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
            "private void RebuildBindingDictionariesFromState");

        Check(
            restore.Contains("var boundWaiters = new List<Waiter>") &&
            restore.Contains("BistroBuilderStaffEligibilityBatch.TryApply(") &&
            restore.Contains("boundWaiters,") &&
            restore.Contains("true,"),
            "TryRestoreSessionSnapshot activa solo el subconjunto ligado " +
            "mediante el lote transaccional.",
            ref passed,
            ref failed,
            log);

        Check(
            !restore.Contains("waiter.TrySetStaffServiceEligibility(true)") &&
            !restore.Contains("TrySetAllWaitersEligible(true"),
            "TryRestoreSessionSnapshot no contiene activación individual ni " +
            "fallback global de elegibilidad.",
            ref passed,
            ref failed,
            log);

        Check(
            rehydrate.Contains("var boundWaiters = new List<Waiter>") &&
            rehydrate.Contains("BistroBuilderStaffEligibilityBatch.TryApply(") &&
            rehydrate.Contains("boundWaiters,") &&
            rehydrate.Contains("true,"),
            "TryRehydrateRuntimeFromCurrentState activa los bindings como " +
            "un único lote transaccional.",
            ref passed,
            ref failed,
            log);

        Check(
            !rehydrate.Contains("waiter.TrySetStaffServiceEligibility(true)") &&
            !rehydrate.Contains("TrySetAllWaitersEligible(true"),
            "La rehidratación no reactiva Waiter individualmente ni abre " +
            "agentes no ligados ante un fallo.",
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
