using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4E — Gate estático de aislamiento durante Finalize de Save/Load.
///
/// Personal debe rehidratar su binding mientras service.runtime mantiene
/// activo el scope global de restauración. Solo después service.runtime puede
/// reanudar WaiterTaskCoordinator y el resto del mundo operativo.
/// </summary>
public static class BistroBuilderStaff4EFinalizeIsolationSelfTest
{
    private const string StaffProviderPath =
        "Assets/Scripts/Application/Persistence/Staff/BistroBuilderStaffSessionSaveSectionProvider.cs";
    private const string ServiceProviderPath =
        "Assets/Scripts/Application/Persistence/Service/BistroBuilderActiveServiceSaveSectionProvider.cs";
    private const string SaveGamePath =
        "Assets/Scripts/Application/Persistence/BistroBuilderSaveGameService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4E v2 - Autotest aislamiento Finalize",
        false,
        3244)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E Finalize",
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
        log.AppendLine("=== BISTRO BUILDER — 4E FINALIZE ISOLATION ===");

        string staff = Read(StaffProviderPath);
        string service = Read(ServiceProviderPath);
        string save = Read(SaveGamePath);

        int staffOrder = ExtractOrder(staff, "public int FinalizeOrder =>");
        int serviceOrder = ExtractOrder(service, "public int FinalizeOrder =>");

        Check(
            staffOrder > 10000 && serviceOrder > staffOrder,
            "staff.session.runtime finaliza después de game.general y antes de service.runtime.",
            ref passed, ref failed, log);

        string serviceFinalize = Slice(
            service,
            "public void FinalizeLoad",
            "private bool CaptureCustomers");
        int releaseScope = serviceFinalize.IndexOf(
            "BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false",
            StringComparison.Ordinal);
        int resumeCoordinator = serviceFinalize.IndexOf(
            "waiterTaskCoordinator.ResumeAfterRuntimeLoad()",
            StringComparison.Ordinal);
        Check(
            releaseScope >= 0 && resumeCoordinator > releaseScope,
            "service.runtime conserva la autoridad de reanudar WaiterTaskCoordinator tras quitar el scope.",
            ref passed, ref failed, log);

        string staffFinalize = Slice(
            staff,
            "public void FinalizeLoad",
            "private void CacheDependencies");
        Check(
            staffFinalize.Contains("TryResumeAfterRuntimeLoad(out string error)") &&
            !staffFinalize.Contains("WaiterTaskCoordinator") &&
            !staffFinalize.Contains("ResumeAfterRuntimeLoad()"),
            "Personal solo rehidrata su binding y no reanuda la autoridad operativa.",
            ref passed, ref failed, log);

        string finalizeAll = Slice(
            save,
            "private void FinalizeAllProviders",
            "private IEnumerator RunProviderRoutineSafely");
        Check(
            finalizeAll.Contains("if (context.HasFailed)") &&
            finalizeAll.Contains("return;"),
            "SaveGame detiene Finalize tras el primer fallo antes de reanudar proveedores posteriores.",
            ref passed, ref failed, log);

        Check(
            service.Contains("BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = true") &&
            serviceFinalize.Contains("BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false"),
            "service.runtime abre y cierra explícitamente el scope global de restauración.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static int ExtractOrder(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return -1;
        int start = markerIndex + marker.Length;
        int end = source.IndexOf(';', start);
        if (end <= start) return -1;
        return int.TryParse(source.Substring(start, end - start).Trim(), out int value)
            ? value
            : -1;
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
