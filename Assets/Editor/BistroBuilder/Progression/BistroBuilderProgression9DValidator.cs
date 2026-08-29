using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderProgression9DValidationResult
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
        "=== BISTRO BUILDER — 9D / EFECTOS JUGABLES ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderProgression9DValidator
{
    [MenuItem("Tools/Bistro Builder/Progression/9D - Validar", false, 9031)]
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

    public static BistroBuilderProgression9DValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderProgression9DValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena no está activa o permanece Dirty.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems no es único.");

        var effects = FindSceneComponents<BistroBuilderUpgradeEffectsService>(scene);
        result.Check(effects.Length == 1 && host != null && effects[0].gameObject == host,
            "UpgradeEffectsService es único y vive en GameSystems.",
            "UpgradeEffectsService falta, está duplicado o mal ubicado.");
        if (effects.Length == 1)
            result.Check(effects[0].ValidateConfiguration(out _),
                "UpgradeEffectsService valida su autoridad de mejoras.",
                "UpgradeEffectsService no valida su configuración.");

        var tracking = FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        result.Check(tracking.Length == 1 && tracking[0].ValidateConfiguration(out _),
            "Reputación 8B-8D consume efectos de mejoras sin duplicar autoridad.",
            "ExperienceTracking no integra correctamente 9D.");

        var execution = FindSceneComponents<BistroBuilderOrderLineExecutionService>(scene);
        result.Check(execution.Length == 1 && execution[0].ValidateConfiguration(out _),
            "Cocina 367D acepta el proveedor de mejoras junto a Marketing.",
            "OrderLineExecution no acepta la composición de proveedores 9D.");

        var upgrades = FindSceneComponents<BistroBuilderUpgradeService>(scene);
        bool catalogOk = upgrades.Length == 1 && upgrades[0].UpgradeCatalog != null &&
            upgrades[0].UpgradeCatalog.Count == 18;
        int withEffects = 0;
        if (catalogOk)
        {
            var definitions = upgrades[0].UpgradeCatalog.Upgrades;
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i]?.effects != null && definitions[i].effects.Count > 0)
                    withEffects++;
        }
        result.Check(catalogOk && withEffects == 18,
            "Las 18 mejoras canónicas declaran efectos jugables.",
            "El catálogo no contiene efectos para las 18 mejoras.");

        BistroBuilderProgression9CValidationResult previous =
            BistroBuilderProgression9CValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "9A-9C permanecen estructuralmente verdes.",
            "9D rompe un gate anterior del Bloque 9.");
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
