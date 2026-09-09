using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4D que protege el orden de commit al cerrar una sesión real.
///
/// El cierre solo puede comprometer rendimiento después de validar que el
/// servicio está Closed y superar el preflight completo. A su vez, la sesión
/// y la elegibilidad operativa no pueden desmontarse antes de terminar todos
/// los commits de Personal. Este gate no sustituye la prueba runtime.
/// </summary>
public static class BistroBuilderStaff4DCloseCommitOrderingSelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Gate orden commit cierre",
        false,
        3236)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D / Orden de commit de cierre",
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
        log.AppendLine("=== BISTRO BUILDER — 4D / ORDEN COMMIT CIERRE ===");

        string source = ReadSource(SessionServicePath);
        Check(
            !string.IsNullOrWhiteSpace(source),
            "Existe la autoridad de sesión 4D.",
            ref passed, ref failed, log);

        int methodIndex = source.IndexOf("public bool TryFinalizeClosedSession(out string error)");
        int closedGuardIndex = source.IndexOf(
            "if (!restaurantServiceStateService.IsClosed)",
            methodIndex >= 0 ? methodIndex : 0);
        int closePreflightIndex = source.IndexOf(
            "BistroBuilderStaffSessionClosePreflight.TryValidate(",
            methodIndex >= 0 ? methodIndex : 0);
        int resultCommitIndex = source.IndexOf(
            "developmentService.TryApplyServiceResult(",
            methodIndex >= 0 ? methodIndex : 0);
        int eligibilityCommitIndex = source.IndexOf(
            "TrySetAllWaitersEligible(false, out error)",
            methodIndex >= 0 ? methodIndex : 0);
        int sessionClearIndex = source.IndexOf(
            "sessionState = BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();",
            methodIndex >= 0 ? methodIndex : 0);
        int bindingsClearIndex = source.IndexOf(
            "bindingsByEmployeeId.Clear();",
            sessionClearIndex >= 0 ? sessionClearIndex : 0);
        int releaseEventIndex = source.IndexOf(
            "EmployeeReleasedFromService?.Invoke(",
            sessionClearIndex >= 0 ? sessionClearIndex : 0);
        int sessionEndedIndex = source.IndexOf(
            "SessionEnded?.Invoke(endedSessionId);",
            sessionClearIndex >= 0 ? sessionClearIndex : 0);

        Check(
            methodIndex >= 0 && closedGuardIndex > methodIndex &&
            closePreflightIndex > closedGuardIndex,
            "4D exige servicio Closed antes del preflight de cierre.",
            ref passed, ref failed, log);

        int purePreflightIndex = source.IndexOf(
            "BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(",
            methodIndex >= 0 ? methodIndex : 0);
        Check(
            closePreflightIndex >= 0 && purePreflightIndex > closePreflightIndex &&
            resultCommitIndex > purePreflightIndex,
            "El preflight topológico/runtime y el preflight puro 4C preceden al primer commit real.",
            ref passed, ref failed, log);

        Check(
            resultCommitIndex >= 0 && eligibilityCommitIndex > resultCommitIndex &&
            sessionClearIndex > eligibilityCommitIndex,
            "La elegibilidad y la sesión no se desmontan antes de aplicar los resultados.",
            ref passed, ref failed, log);

        Check(
            sessionClearIndex >= 0 && bindingsClearIndex > sessionClearIndex &&
            releaseEventIndex > bindingsClearIndex && sessionEndedIndex > releaseEventIndex,
            "El estado se vuelve inactivo antes de publicar liberaciones y SessionEnded.",
            ref passed, ref failed, log);

        Check(
            source.Contains("BuildServiceResultOperationId(") &&
            source.Contains("pendingResults") &&
            source.Contains("serviceCompleted = true"),
            "Los resultados de cierre conservan operationId estable e idempotente.",
            ref passed, ref failed, log);

        Check(
            !source.Contains("new WaiterTaskCoordinator") &&
            !source.Contains("new BistroBuilderStaffService") &&
            !source.Contains("new BistroBuilderStaffDevelopmentService"),
            "El cierre no introduce autoridades operativas o de Personal paralelas.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        log.AppendLine(
            "Este gate protege el orden estático del cierre; compilación, Play Mode " +
            "y Queen Test real siguen siendo obligatorios.");
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
