using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderProgression9AValidationResult
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
        "=== BISTRO BUILDER — 9A / VALIDACIÓN MEJORAS Y PROGRESIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

/// <summary>Gate estructural de 9A y regresión acumulativa sobre Bloque 8.</summary>
public static class BistroBuilderProgression9AValidator
{
    [MenuItem("Tools/Bistro Builder/Progression/9A - Validar", false, 9001)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderProgression9AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderProgression9AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path) && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa, guardada o está Dirty.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var services = FindSceneComponents<BistroBuilderUpgradeService>(scene);
        result.Check(services.Length == 1 && host != null && services[0].gameObject == host,
            "UpgradeService es único y vive en GameSystems.",
            "UpgradeService falta, está duplicado o mal ubicado.");

        var finance = FindSceneComponents<BistroBuilderDiscretionaryFinanceService>(scene);
        var general = FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
        var reputation = FindSceneComponents<BistroBuilderReputationService>(scene);
        result.Check(finance.Length == 1 && general.Length == 1 && reputation.Length == 1,
            "Finanzas, estado general y Reputación conservan autoridad única.",
            "Alguna autoridad consumida por 9A falta o está duplicada.");

        if (services.Length == 1)
        {
            BistroBuilderUpgradeService service = services[0];
            result.Check(service.ValidateConfiguration(out _),
                "UpgradeService valida catálogo, dependencias y estado.",
                "UpgradeService no valida su configuración.");
            result.Check(finance.Length == 1 && ReferenceEquals(service.DiscretionaryFinanceService, finance[0]) &&
                         general.Length == 1 && ReferenceEquals(service.GeneralGameStateService, general[0]) &&
                         reputation.Length == 1 && ReferenceEquals(service.ReputationService, reputation[0]),
                "9A consume autoridades existentes sin duplicarlas.",
                "UpgradeService no referencia las autoridades canónicas.");

            BistroBuilderUpgradeCatalog catalog = service.UpgradeCatalog;
            result.Check(catalog != null && catalog.Count == 18 && catalog.ValidateConfiguration(out _),
                "El catálogo canónico contiene 18 mejoras válidas.",
                "El catálogo falta, no contiene 18 mejoras o es inválido.");
            if (catalog != null)
            {
                var counts = new int[6];
                var definitions = catalog.Upgrades;
                for (int i = 0; i < definitions.Count; i++)
                    if (definitions[i] != null) counts[(int)definitions[i].category]++;
                bool balanced = true;
                for (int i = 0; i < counts.Length; i++) balanced &= counts[i] == 3;
                result.Check(balanced,
                    "Las seis categorías V1 están representadas de forma data-driven.",
                    "El catálogo no representa correctamente las seis categorías V1.");

                bool terraceGuarded = AllCategoryRequireCapability(
                    definitions, BistroBuilderUpgradeCategory.Terrace, "facility.terrace");
                bool barGuarded = AllCategoryRequireCapability(
                    definitions, BistroBuilderUpgradeCategory.Bar, "facility.bar");
                result.Check(terraceGuarded && barGuarded,
                    "Terraza y Barra respetan capacidades reales del local.",
                    "Hay mejoras físicas que ignoran la compatibilidad del local.");
            }

            var capabilities = new List<string>();
            service.CopyLocalCapabilities(capabilities);
            result.Check(capabilities.Contains("restaurant.base") &&
                         capabilities.Contains("facility.dining_room") &&
                         capabilities.Contains("facility.kitchen"),
                "El local declara capacidades base explícitas.",
                "Faltan capacidades base del local para progresión.");
        }

        result.Check(BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "investment.improvement.progression"),
            "Finanzas 3F admite inversiones de mejoras sin nueva autoridad monetaria.",
            "Finanzas no admite la categoría de inversión de mejoras.");

        BistroBuilderReputationBlock8ValidationResult previous =
            BistroBuilderReputationBlock8Validator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "Bloque 8 Reputación permanece estructuralmente verde.",
            "9A ha introducido una regresión estructural en Bloque 8.");

        return result;
    }

    private static bool AllCategoryRequireCapability(
        IReadOnlyList<BistroBuilderUpgradeDefinition> definitions,
        BistroBuilderUpgradeCategory category,
        string capability)
    {
        bool found = false;
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null || definition.category != category) continue;
            found = true;
            bool has = false;
            if (definition.requiredCapabilityIds != null)
                for (int p = 0; p < definition.requiredCapabilityIds.Count; p++)
                    has |= BistroBuilderProgressionEngine.NormalizeId(
                        definition.requiredCapabilityIds[p]) == capability;
            if (!has) return false;
        }
        return found;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
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