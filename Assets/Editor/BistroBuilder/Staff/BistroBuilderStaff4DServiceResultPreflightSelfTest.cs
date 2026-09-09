using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4D que protege el cierre de sesión frente a aplicaciones
/// parciales deterministas de XP/rendimiento. Antes del primer commit real,
/// toda la secuencia debe simularse sobre una copia de staff.state usando el
/// motor puro 4C y los mismos operationId idempotentes que el commit real.
/// </summary>
public static class BistroBuilderStaff4DServiceResultPreflightSelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Gate preflight resultados servicio",
        false,
        3235)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D / Preflight de resultados",
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
        log.AppendLine("=== BISTRO BUILDER — 4D / PREFLIGHT RESULTADOS ===");

        string source = ReadSource(SessionServicePath);
        Check(
            !string.IsNullOrWhiteSpace(source),
            "Existe la autoridad de sesión 4D.",
            ref passed, ref failed, log);

        Check(
            source.Contains("pendingResults") &&
            source.Contains("BuildServiceResultOperationId(") &&
            source.Contains("BistroBuilderEmployeeServicePerformanceReport"),
            "4D construye primero el lote completo con operationId estable.",
            ref passed, ref failed, log);

        Check(
            source.Contains("BistroBuilderStaffSnapshot preflightState = staffService.CreateSnapshot();") &&
            source.Contains("BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance("),
            "El lote se simula sobre una copia de staff.state mediante el motor puro 4C.",
            ref passed, ref failed, log);

        int preflightIndex = source.IndexOf(
            "BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(");
        int commitIndex = source.IndexOf(
            "developmentService.TryApplyServiceResult(");
        Check(
            preflightIndex >= 0 && commitIndex > preflightIndex,
            "Ningún resultado real se compromete antes de superar el preflight completo.",
            ref passed, ref failed, log);

        Check(
            source.Contains("preflightState = nextPreflightState;") &&
            source.Contains("developmentService.DevelopmentProfile") &&
            source.Contains("staffService.RoleCatalog"),
            "La simulación encadena exactamente el estado, perfil y catálogo canónicos.",
            ref passed, ref failed, log);

        Check(
            !source.Contains("staffService.TryRestoreSnapshot(preflightState") &&
            !source.Contains("new BistroBuilderStaffService") &&
            !source.Contains("new BistroBuilderStaffDevelopmentService"),
            "El hardening no crea autoridades paralelas ni usa Restore como commit ordinario.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        log.AppendLine(
            "Este gate reduce fallos parciales deterministas, pero no sustituye " +
            "compilación, Play Mode ni Queen Test real.");
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
