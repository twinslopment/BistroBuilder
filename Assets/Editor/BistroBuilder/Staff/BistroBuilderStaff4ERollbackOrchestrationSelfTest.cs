using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4E — Gate estático de orquestación de rollback Save/Load.
///
/// Protege que una carga fallida no deje Personal en un estado suspendido o
/// parcialmente reconstruido: el SaveGame canónico debe capturar rollback,
/// repetir Prepare/Apply/Finalize sobre ese snapshot y mantener a
/// staff.session.runtime dentro del mismo contrato de fases.
/// </summary>
public static class BistroBuilderStaff4ERollbackOrchestrationSelfTest
{
    private const string SaveGamePath =
        "Assets/Scripts/Application/Persistence/BistroBuilderSaveGameService.cs";
    private const string StaffSessionProviderPath =
        "Assets/Scripts/Application/Persistence/Staff/BistroBuilderStaffSessionSaveSectionProvider.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4E v2 - Autotest rollback SaveLoad",
        false,
        3245)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E Rollback",
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
        log.AppendLine("=== BISTRO BUILDER — 4E ROLLBACK ORCHESTRATION ===");

        string save = Read(SaveGamePath);
        string staff = Read(StaffSessionProviderPath);

        string loadRoutine = Slice(
            save,
            "private IEnumerator LoadRoutine",
            "private IEnumerator DeleteRoutine");

        Check(
            loadRoutine.Contains("CaptureAllProviders(") &&
            loadRoutine.Contains("ConvertCapturedToLoaded(rollbackCapture.Sections)"),
            "SaveGame captura un snapshot de rollback antes de mutar el mundo.",
            ref passed, ref failed, log);

        int targetPrepare = loadRoutine.IndexOf(
            "yield return PrepareAllProviders(loadContext)",
            StringComparison.Ordinal);
        int targetApply = loadRoutine.IndexOf(
            "yield return ApplyAllProviders(\n                targetSections",
            StringComparison.Ordinal);
        int targetFinalize = loadRoutine.IndexOf(
            "FinalizeAllProviders(loadContext)",
            StringComparison.Ordinal);
        Check(
            targetPrepare >= 0 && targetApply > targetPrepare &&
            targetFinalize > targetApply,
            "La carga objetivo respeta Prepare -> Apply -> Finalize.",
            ref passed, ref failed, log);

        int rollbackContext = loadRoutine.IndexOf(
            "BistroBuilderSaveLoadContext rollbackContext",
            StringComparison.Ordinal);
        int rollbackPrepare = loadRoutine.IndexOf(
            "yield return PrepareAllProviders(rollbackContext)",
            StringComparison.Ordinal);
        int rollbackApply = loadRoutine.IndexOf(
            "rollbackSections,\n                    rollbackContext",
            StringComparison.Ordinal);
        int rollbackFinalize = loadRoutine.IndexOf(
            "FinalizeAllProviders(rollbackContext)",
            StringComparison.Ordinal);
        Check(
            rollbackContext >= 0 && rollbackPrepare > rollbackContext &&
            rollbackApply > rollbackPrepare && rollbackFinalize > rollbackApply,
            "El rollback reutiliza el mismo pipeline Prepare -> Apply -> Finalize.",
            ref passed, ref failed, log);

        Check(
            loadRoutine.Contains("new BistroBuilderSaveLoadContext(\n                    slotIndex,\n                    true") &&
            loadRoutine.Contains("El estado anterior fue restaurado correctamente."),
            "El contexto de rollback queda marcado explícitamente y reporta restauración completa.",
            ref passed, ref failed, log);

        Check(
            staff.Contains("public bool IsRequired => false;") &&
            staff.Contains("public int PrepareOrder => 8950;") &&
            staff.Contains("public int ApplyOrder => 550;") &&
            staff.Contains("public int FinalizeOrder => 10950;"),
            "staff.session.runtime conserva opcionalidad y órdenes de fase estables durante rollback.",
            ref passed, ref failed, log);

        string prepare = Slice(
            staff,
            "public IEnumerator PrepareForLoad",
            "public IEnumerator ApplyState");
        string apply = Slice(
            staff,
            "public IEnumerator ApplyState",
            "public void FinalizeLoad");
        string finalize = Slice(
            staff,
            "public void FinalizeLoad",
            "private void CacheDependencies");

        Check(
            prepare.Contains("PrepareForRuntimeLoad(out error)") &&
            apply.Contains("TryRestoreSessionSnapshot(") &&
            finalize.Contains("TryResumeAfterRuntimeLoad(out string error)"),
            "4E suspende, restaura y reanuda 4D únicamente mediante sus contratos públicos.",
            ref passed, ref failed, log);

        Check(
            !staff.Contains("TryRestoreSnapshot(") &&
            !staff.Contains("WaiterTaskCoordinator") &&
            !staff.Contains("new Waiter"),
            "El provider de Personal no suplanta staff.state ni la autoridad operativa durante rollback.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Read(string assetPath)
    {
        string path = Path.GetFullPath(assetPath);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end <= start) end = source.Length;
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
        }
        else
        {
            failed++;
            log.AppendLine("[FALLO] " + text);
        }
    }
}
