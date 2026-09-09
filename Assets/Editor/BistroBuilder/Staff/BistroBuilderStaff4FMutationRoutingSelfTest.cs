using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4F — Gate estático de enrutado de mutaciones de la pantalla jugable.
/// Protege una frontera que la reflexión de campos no puede detectar: una
/// regresión futura podría localizar servicios canónicos dinámicamente desde
/// Presentation sin almacenarlos en campos y el gate 4F clásico no lo vería.
/// </summary>
public static class BistroBuilderStaff4FMutationRoutingSelfTest
{
    private const string ScreenPath =
        "Assets/Scripts/Presentation/Staff/BistroBuilderStaffPlayerScreen.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Autotest enrutado mutaciones UI",
        false,
        3254)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4F Enrutado",
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
        log.AppendLine("=== BISTRO BUILDER — 4F MUTATION ROUTING / STATIC GATE ===");

        string screen = Read(ScreenPath);
        Check(
            !string.IsNullOrWhiteSpace(screen),
            "Existe el código fuente canónico de StaffPlayerScreen.",
            ref passed, ref failed, log);

        Check(
            screen.Contains("facade.TryHireCandidate(") &&
            screen.Contains("facade.TryDismissEmployee(") &&
            screen.Contains("facade.TrySetAvailability(") &&
            screen.Contains("facade.TryRefreshCandidates("),
            "Las mutaciones de plantilla y mercado se enrutan por StaffPlayerFacade.",
            ref passed, ref failed, log);

        Check(
            screen.Contains("PendingConfirmation.Hire") &&
            screen.Contains("PendingConfirmation.Dismiss") &&
            screen.Contains("ConfirmPendingAction()") &&
            screen.Contains("CancelConfirmation()"),
            "Contratación y despido conservan confirmación explícita de Presentation.",
            ref passed, ref failed, log);

        Check(
            !ContainsAny(
                screen,
                "FindObjectOfType<BistroBuilderStaffService",
                "FindFirstObjectByType<BistroBuilderStaffService",
                "FindAnyObjectByType<BistroBuilderStaffService",
                "GetComponent<BistroBuilderStaffService",
                "FindObjectOfType<BistroBuilderStaffRecruitmentService",
                "FindFirstObjectByType<BistroBuilderStaffRecruitmentService",
                "FindAnyObjectByType<BistroBuilderStaffRecruitmentService",
                "GetComponent<BistroBuilderStaffRecruitmentService",
                "FindObjectOfType<BistroBuilderStaffDevelopmentService",
                "FindFirstObjectByType<BistroBuilderStaffDevelopmentService",
                "FindAnyObjectByType<BistroBuilderStaffDevelopmentService",
                "GetComponent<BistroBuilderStaffDevelopmentService",
                "FindObjectOfType<BistroBuilderStaffSessionService",
                "FindFirstObjectByType<BistroBuilderStaffSessionService",
                "FindAnyObjectByType<BistroBuilderStaffSessionService",
                "GetComponent<BistroBuilderStaffSessionService"),
            "La pantalla no puede saltarse la fachada localizando autoridades de Personal dinámicamente.",
            ref passed, ref failed, log);

        Check(
            !ContainsAny(
                screen,
                "BistroBuilderSaveGameService",
                "WaiterTaskCoordinator",
                "FinanceService",
                "FinanceLedger"),
            "La pantalla no conoce Save, coordinador operativo ni Finanzas.",
            ref passed, ref failed, log);

        Check(
            !ContainsAny(
                screen,
                ".HireCandidate(",
                ".DismissEmployee(",
                ".SetAvailability(",
                ".RefreshCandidates("),
            "No se introducen rutas de mutación paralelas sin contrato Try de fachada.",
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

    private static bool ContainsAny(string source, params string[] tokens)
    {
        if (string.IsNullOrEmpty(source) || tokens == null)
        {
            return false;
        }

        for (int index = 0; index < tokens.Length; index++)
        {
            if (!string.IsNullOrEmpty(tokens[index]) &&
                source.IndexOf(tokens[index], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
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
