using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4D — Gate estático de seguridad para PrepareForRuntimeLoad.
///
/// Verifica que la preparación de Save/Load no comprometa suspensión,
/// tracking ni diccionarios runtime antes de haber validado el índice de
/// Waiter y completado el batch transaccional de elegibilidad.
/// </summary>
public static class BistroBuilderStaff4DPrepareForLoadSelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest PrepareForLoad seguro",
        false,
        3234)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D PrepareForLoad",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4D PREPARE FOR LOAD / AUTOTEST ===");

        string absolutePath = Path.GetFullPath(SessionServicePath);
        string source = File.Exists(absolutePath)
            ? File.ReadAllText(absolutePath)
            : string.Empty;

        int prepareIndex = source.IndexOf(
            "public bool PrepareForRuntimeLoad",
            StringComparison.Ordinal);
        int restoreIndex = source.IndexOf(
            "public bool TryRestoreSessionSnapshot",
            prepareIndex >= 0 ? prepareIndex : 0,
            StringComparison.Ordinal);
        string body = Slice(source, prepareIndex, restoreIndex);

        Check(
            prepareIndex >= 0 && !string.IsNullOrEmpty(body),
            "Existe PrepareForRuntimeLoad y puede inspeccionarse.",
            ref passed,
            ref failed,
            log);

        int refreshIndex = body.IndexOf(
            "RefreshWaiterIndex(out error)",
            StringComparison.Ordinal);
        int eligibilityIndex = body.IndexOf(
            "TrySetAllWaitersEligible(false, out error)",
            StringComparison.Ordinal);
        int suspensionIndex = body.IndexOf(
            "suspendedForRuntimeLoad = true",
            StringComparison.Ordinal);
        int trackingIndex = body.IndexOf(
            "ClearObservedTaskTracking()",
            StringComparison.Ordinal);
        int employeeClearIndex = body.IndexOf(
            "bindingsByEmployeeId.Clear()",
            StringComparison.Ordinal);
        int waiterClearIndex = body.IndexOf(
            "bindingsByWaiterId.Clear()",
            StringComparison.Ordinal);

        Check(
            refreshIndex >= 0 &&
            eligibilityIndex > refreshIndex,
            "El índice operativo se valida antes del batch de elegibilidad.",
            ref passed,
            ref failed,
            log);

        Check(
            eligibilityIndex >= 0 &&
            suspensionIndex > eligibilityIndex &&
            trackingIndex > eligibilityIndex &&
            employeeClearIndex > eligibilityIndex &&
            waiterClearIndex > eligibilityIndex,
            "Suspensión, tracking y bindings solo se comprometen después del batch.",
            ref passed,
            ref failed,
            log);

        string beforeEligibility = eligibilityIndex > 0
            ? body.Substring(0, eligibilityIndex)
            : body;
        Check(
            !beforeEligibility.Contains("suspendedForRuntimeLoad = true") &&
            !beforeEligibility.Contains("ClearObservedTaskTracking()") &&
            !beforeEligibility.Contains("bindingsByEmployeeId.Clear()") &&
            !beforeEligibility.Contains("bindingsByWaiterId.Clear()"),
            "Un fallo de preflight/batch no puede dejar 4D suspendido ni vaciar runtime.",
            ref passed,
            ref failed,
            log);

        Check(
            body.Contains("error = string.Empty;") &&
            body.Contains("return true;"),
            "La preparación confirma explícitamente el commit exitoso.",
            ref passed,
            ref failed,
            log);

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
