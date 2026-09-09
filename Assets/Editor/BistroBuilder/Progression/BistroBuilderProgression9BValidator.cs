using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderProgression9BValidationResult
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
        "=== BISTRO BUILDER — 9B / COMPRA Y PERSISTENCIA ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderProgression9BValidator
{
    [MenuItem("Tools/Bistro Builder/Progression/9B - Validar", false, 9011)]
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

    public static BistroBuilderProgression9BValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderProgression9BValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path) && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa, guardada o está Dirty.");

        GameObject gameSystems = FindUniqueGameSystems(scene);
        result.Check(gameSystems != null,
            "Existe un único GameSystems canónico.",
            "GameSystems no es único.");
        var services = FindSceneComponents<BistroBuilderUpgradeService>(scene);
        var providers = FindSceneComponents<BistroBuilderUpgradeSaveSectionProvider>(scene);
        var saves = FindSceneComponents<BistroBuilderSaveGameService>(scene);

        result.Check(services.Length == 1 && gameSystems != null &&
            services[0].gameObject == gameSystems,
            "UpgradeService sigue siendo autoridad única en GameSystems.",
            "UpgradeService no es único o ha salido de GameSystems.");
        result.Check(providers.Length == 1 && gameSystems != null &&
            providers[0].gameObject == gameSystems,
            "El proveedor progression.upgrades es único en GameSystems.",
            "El proveedor de mejoras falta, está duplicado o mal ubicado.");
        result.Check(saves.Length == 1,
            "SaveGameService conserva autoridad única.",
            "SaveGameService no es único.");

        if (services.Length == 1)
            result.Check(services[0].ValidateConfiguration(out _),
                "UpgradeService valida catálogo, estado y autoridades.",
                "UpgradeService no valida su configuración.");
        if (providers.Length == 1)
            result.Check(providers[0].ValidateConfiguration(out _),
                "El proveedor de persistencia valida sus dependencias.",
                "El proveedor de persistencia no valida.");
        if (saves.Length == 1)
        {
            saves[0].RefreshExtensions();
            result.Check(saves[0].HasProvider(
                    BistroBuilderUpgradeSaveSectionProvider.StableSectionId),
                "SaveGame descubre progression.upgrades.",
                "SaveGame no descubre progression.upgrades.");
        }

        result.Check(BistroBuilderUpgradeSaveSectionProvider.StableSectionId ==
                "progression.upgrades" &&
            BistroBuilderUpgradeSaveSectionProvider.StableSectionVersion == 1,
            "La sección de guardado usa identidad y versión estables.",
            "La identidad o versión de progression.upgrades cambió.");

        bool financeCompatible =
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.dining_room") &&
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.kitchen") &&
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.terrace") &&
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.bar") &&
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.infrastructure") &&
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.ambience_identity");
        result.Check(financeCompatible,
            "Las seis categorías de mejora usan Finanzas 3F sin autoridad paralela.",
            "Alguna categoría de mejora no está admitida por Finanzas 3F.");

        BistroBuilderProgression9AValidationResult previous =
            BistroBuilderProgression9AValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "La fundación 9A permanece estructuralmente verde.",
            "9B rompe algún gate estructural de 9A.");
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
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
