using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5E — Gate estático de frontera Presentation para Horarios.
/// </summary>
public static class BistroBuilderStaff5EStaticSelfTest
{
    private const string FacadePath =
        "Assets/Scripts/Presentation/Staff/Scheduling/BistroBuilderStaffSchedulePlayerFacade.cs";
    private const string ScreenPath =
        "Assets/Scripts/Presentation/Staff/Scheduling/BistroBuilderStaffSchedulePlayerScreen.cs";

    [MenuItem("Tools/Bistro Builder/Personal/5E - Autotest UI horarios", false, 3278)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5E", passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        string facade = Read(FacadePath);
        string screen = Read(ScreenPath);

        Check(facade.Contains("BistroBuilderStaffScheduleService") &&
              facade.Contains("TrySetScheduled(") &&
              facade.Contains("TryAutoFillMinimumWaiters(") &&
              facade.Contains("TryCopyServicePlan("),
            "La fachada delega todos los comandos en ScheduleService.",
            ref passed, ref failed, log);
        Check(!facade.Contains("BistroBuilderSaveGameService") &&
              !facade.Contains("WaiterTaskCoordinator") &&
              !facade.Contains("BistroBuilderFinance") &&
              !facade.Contains("FinanceLedger"),
            "Presentation no accede a Save, tareas ni Finanzas.",
            ref passed, ref failed, log);
        Check(screen.Contains("BistroBuilderStaffSchedulePlayerFacade") &&
              !screen.Contains("BistroBuilderStaffScheduleService") &&
              !screen.Contains("BistroBuilderStaffService"),
            "La pantalla solo conoce su fachada Presentation.",
            ref passed, ref failed, log);
        Check(screen.Contains("coverage.isSufficient") &&
              screen.Contains("projectedSalaryCents"),
            "La UI muestra suficiencia y coste salarial proyectado.",
            ref passed, ref failed, log);

        Check(screen.Contains("emptyStateText") &&
              screen.Contains("snapshot.employees.Count > 0"),
            "La UI contempla explicitamente una plantilla todavia vacia.",
            ref passed, ref failed, log);
        Check(screen.Contains("UpdateMealButtonState") &&
              screen.Contains("coverageText.color"),
            "La UI diferencia servicio activo y suficiencia de cobertura.",
            ref passed, ref failed, log);
        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Read(string path)
    {
        string full = Path.GetFullPath(path);
        return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
    }

    private static void Check(bool condition, string text, ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
