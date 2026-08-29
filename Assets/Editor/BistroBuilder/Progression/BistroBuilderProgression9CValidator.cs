using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderProgression9CValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    public void Check(bool condition, string ok, string fail)
    {
        if (condition) { Passed++; lines.Add("[OK] " + ok); }
        else { Errors++; lines.Add("[ERROR] " + fail); }
    }
    public string BuildReport() =>
        "=== BISTRO BUILDER — 9C / HITOS Y EVOLUCIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderProgression9CValidator
{
    [MenuItem("Tools/Bistro Builder/Progression/9C - Validar", false, 9021)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderProgression9CValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderProgression9CValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path) && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa/guardada.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems no es único.");

        var milestoneServices = FindSceneComponents<BistroBuilderProgressionMilestoneService>(scene);
        result.Check(milestoneServices.Length == 1 && host != null &&
            milestoneServices[0].gameObject == host,
            "MilestoneService es único y vive en GameSystems.",
            "MilestoneService falta, está duplicado o mal ubicado.");
        if (milestoneServices.Length == 1)
        {
            var service = milestoneServices[0];
            result.Check(service.ValidateConfiguration(out _),
                "MilestoneService valida todas sus autoridades canónicas.",
                "MilestoneService no valida su configuración.");
            result.Check(service.MilestoneCatalog != null &&
                service.MilestoneCatalog.Milestones.Count == 4,
                "El catálogo V1 contiene cuatro hitos secuenciales.",
                "El catálogo de hitos V1 falta o no contiene cuatro hitos.");
        }

        result.Check(FindSceneComponents<BistroBuilderGeneralGameStateService>(scene).Length == 1,
            "GeneralGameState sigue siendo autoridad única de stage/level.",
            "La autoridad global de progresión no es única.");
        result.Check(FindSceneComponents<BistroBuilderFinancialResultsService>(scene).Length == 1,
            "Resultados financieros 3G siguen siendo autoridad única de rendimiento.",
            "Resultados financieros 3G no son únicos.");
        result.Check(FindSceneComponents<BistroBuilderReputationService>(scene).Length == 1,
            "Reputación sigue siendo autoridad única de calidad percibida.",
            "Reputación no es única.");
        result.Check(FindSceneComponents<BistroBuilderUpgradeService>(scene).Length == 1,
            "UpgradeService sigue siendo autoridad única de mejoras adquiridas.",
            "UpgradeService no es único.");

        BistroBuilderProgression9BValidationResult previous =
            BistroBuilderProgression9BValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "9A/9B permanecen estructuralmente verdes.",
            "9C rompe algún gate anterior del Bloque 9.");
        return result;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == "GameSystems")
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
