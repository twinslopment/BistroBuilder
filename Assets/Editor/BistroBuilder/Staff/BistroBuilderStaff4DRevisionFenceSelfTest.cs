using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4D que protege la valla de revisión del estado canónico de
/// Personal. Los resultados de servicio nunca deben comprometerse sobre un
/// staff.state obsoleto: StaffService debe aceptar únicamente candidate con
/// revision = state.revision + 1 y rechazar candidatos incoherentes antes de
/// sustituir el snapshot vivo.
/// </summary>
public static class BistroBuilderStaff4DRevisionFenceSelfTest
{
    private const string StaffServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffService.cs";
    private const string DevelopmentServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffDevelopmentService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Gate valla de revision staff.state",
        false,
        3236)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D / Valla de revision",
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
        log.AppendLine("=== BISTRO BUILDER — 4D / VALLA REVISION STAFF.STATE ===");

        string staffSource = ReadSource(StaffServicePath);
        string developmentSource = ReadSource(DevelopmentServicePath);

        Check(
            !string.IsNullOrWhiteSpace(staffSource) &&
            !string.IsNullOrWhiteSpace(developmentSource),
            "Existen las autoridades canónicas StaffService y DevelopmentService.",
            ref passed, ref failed, log);

        Check(
            staffSource.Contains("expectedRevision = checked(state.revision + 1L)") &&
            staffSource.Contains("candidate.revision != expectedRevision"),
            "StaffService exige exactamente la siguiente revisión y rechaza snapshots obsoletos.",
            ref passed, ref failed, log);

        int revisionCheckIndex = staffSource.IndexOf("candidate.revision != expectedRevision");
        int stateCommitIndex = staffSource.IndexOf("state = candidate.DeepClone();");
        Check(
            revisionCheckIndex >= 0 &&
            stateCommitIndex > revisionCheckIndex,
            "La valla de revisión se comprueba antes de sustituir staff.state.",
            ref passed, ref failed, log);

        Check(
            staffSource.Contains("TryValidateExtendedSnapshot(candidate, out error)") &&
            staffSource.Contains("committedEmployee.revision != updatedEmployee.revision"),
            "El commit valida snapshot completo y coherencia de revisión del empleado actualizado.",
            ref passed, ref failed, log);

        Check(
            developmentSource.Contains("BistroBuilderStaffSnapshot snapshot = staffService.CreateSnapshot();") &&
            developmentSource.Contains("staffService.TryCommitDomainMutation(candidate, updated, out error)"),
            "4C calcula desde snapshot canónico y delega el commit a StaffService.",
            ref passed, ref failed, log);

        Check(
            !developmentSource.Contains("staffService.TryRestoreSnapshot(candidate") &&
            !developmentSource.Contains("new BistroBuilderStaffService") &&
            !developmentSource.Contains("new WaiterTaskCoordinator"),
            "El desarrollo no usa Save/Load como commit ni crea autoridades paralelas.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        log.AppendLine(
            "Este gate protege la concurrencia optimista del dominio; no sustituye " +
            "compilación, Play Mode ni el Queen Test real.");
        report = log.ToString();
        return failed == 0;
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
