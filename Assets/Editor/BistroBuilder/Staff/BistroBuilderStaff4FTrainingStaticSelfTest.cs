using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4F — Gate estático de frontera arquitectónica para la formación jugable.
/// Comprueba que Presentation delega en la fachada y que el instalador solo
/// materializa UI a partir del perfil canónico 4C.
/// </summary>
public static class BistroBuilderStaff4FTrainingStaticSelfTest
{
    private const string PanelPath =
        "Assets/Scripts/Presentation/Staff/BistroBuilderStaffPlayerTrainingPanel.cs";
    private const string InstallerPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4FTrainingInstaller.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Autotest formación UI",
        false,
        3253)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4F Formación",
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
        log.AppendLine("=== BISTRO BUILDER — 4F TRAINING UI / STATIC GATE ===");

        string panel = Read(PanelPath);
        string installer = Read(InstallerPath);

        Check(
            panel.Contains("BistroBuilderStaffPlayerFacade") &&
            panel.Contains("BistroBuilderStaffPlayerScreen") &&
            panel.Contains("facade.TryTrainEmployee("),
            "La formación jugable delega siempre el comando en StaffPlayerFacade.",
            ref passed, ref failed, log);

        Check(
            !panel.Contains("BistroBuilderStaffDevelopmentService") &&
            !panel.Contains("BistroBuilderSaveGameService") &&
            !panel.Contains("WaiterTaskCoordinator") &&
            !panel.Contains("FinanceService") &&
            !panel.Contains("FinanceLedger"),
            "TrainingPanel no accede directamente a Desarrollo, Save, tareas ni Finanzas.",
            ref passed, ref failed, log);

        Check(
            panel.Contains("screen.SelectedEmployeeId") &&
            panel.Contains("screen.Refresh()"),
            "La UI usa la selección Presentation y refresca proyecciones tras entrenar.",
            ref passed, ref failed, log);

        Check(
            installer.Contains(
                "Assets/Resources/BistroBuilder/Staff/StaffDevelopmentProfile.asset") &&
            installer.Contains("AssetDatabase.LoadAssetAtPath<BistroBuilderStaffDevelopmentProfile>") &&
            installer.Contains("profile.Trainings"),
            "El instalador deriva las opciones visibles del perfil canónico 4C.",
            ref passed, ref failed, log);

        Check(
            installer.Contains("BistroBuilderStaffPlayerTrainingPanel") &&
            !installer.Contains("Undo.AddComponent<BistroBuilderStaffDevelopmentService>") &&
            !installer.Contains("Undo.AddComponent<BistroBuilderSaveGameService>") &&
            !installer.Contains("Undo.AddComponent<WaiterTaskCoordinator>"),
            "El instalador añade solo Presentation y no duplica autoridades.",
            ref passed, ref failed, log);

        Check(
            installer.Contains("File.WriteAllBytes(absoluteScenePath, backup)") &&
            installer.Contains("EditorSceneManager.OpenScene"),
            "La instalación conserva rollback binario de escena ante fallo.",
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
