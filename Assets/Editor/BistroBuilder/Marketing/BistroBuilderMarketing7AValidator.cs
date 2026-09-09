using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketing7AValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }

    public void Check(bool condition, string success, string failure)
    {
        if (condition)
        {
            Passed++;
            lines.Add("[OK] " + success);
        }
        else
        {
            Errors++;
            lines.Add("[ERROR] " + failure);
        }
    }

    public string BuildReport() =>
        "=== BISTRO BUILDER — 7A / VALIDACIÓN MARKETING ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

/// <summary>Validador estructural de 7A sobre proyecto y escena canónica.</summary>
public static class BistroBuilderMarketing7AValidator
{
    [MenuItem("Tools/Bistro Builder/Marketing/7A - Validar", false, 701)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketing7AValidationResult result = ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report);
        else Debug.LogError(report);
    }

    public static BistroBuilderMarketing7AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketing7AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(
            scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");

        GameObject[] gameSystems = FindNamedObjects(scene, "GameSystems");
        result.Check(
            gameSystems.Length == 1,
            "Existe exactamente un GameSystems canónico.",
            "Se esperaba exactamente un GameSystems; hay " + gameSystems.Length + ".");

        BistroBuilderMarketingService[] marketing =
            FindSceneComponents<BistroBuilderMarketingService>(scene);
        result.Check(
            marketing.Length == 1,
            "Existe exactamente un MarketingService.",
            "Se esperaba un MarketingService; hay " + marketing.Length + ".");

        if (marketing.Length == 1 && gameSystems.Length == 1)
        {
            result.Check(
                marketing[0].gameObject == gameSystems[0],
                "MarketingService vive en GameSystems.",
                "MarketingService no vive en GameSystems.");
            result.Check(
                marketing[0].ValidateConfiguration(out _),
                "MarketingService valida sus dependencias.",
                "MarketingService tiene dependencias inválidas.");
        }

        BistroBuilderDiscretionaryFinanceService[] finance =
            FindSceneComponents<BistroBuilderDiscretionaryFinanceService>(scene);
        result.Check(
            finance.Length == 1,
            "Existe la autoridad financiera discrecional de 3F.",
            "7A requiere exactamente un DiscretionaryFinanceService.");

        BistroBuilderGeneralGameStateService[] general =
            FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
        result.Check(
            general.Length == 1,
            "Existe la autoridad de calendario/progresión.",
            "7A requiere exactamente un GeneralGameStateService.");

        BistroBuilderMarketingCampaignCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMarketingCampaignCatalog>(
                BistroBuilderMarketing7APaths.CatalogAsset);
        result.Check(
            catalog != null,
            "Existe el catálogo canónico de Marketing.",
            "No existe el catálogo canónico de Marketing.");
        if (catalog != null)
        {
            result.Check(
                catalog.ValidateConfiguration(out _),
                "El catálogo cumple el contrato universal.",
                "El catálogo contiene definiciones inválidas.");
            result.Check(
                catalog.Count == 35,
                "El catálogo contiene 35 campañas.",
                "El catálogo debe contener 35 campañas; contiene " + catalog.Count + ".");
            result.Check(
                HasFivePerFamily(catalog.Campaigns),
                "Las 7 familias contienen 5 campañas cada una.",
                "La distribución del catálogo no es 7 x 5.");
        }

        result.Check(
            BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "expense.marketing.validation"),
            "Finanzas acepta expense.marketing.* sin acoplarse al contenido.",
            "El contrato financiero no acepta Marketing.");

        return result;
    }

    private static bool HasFivePerFamily(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions)
    {
        var counts = new Dictionary<BistroBuilderMarketingCampaignType, int>();
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderMarketingCampaignDefinition definition = definitions[i];
            if (definition == null) return false;
            counts.TryGetValue(definition.type, out int count);
            counts[definition.type] = count + 1;
        }
        Array values = Enum.GetValues(typeof(BistroBuilderMarketingCampaignType));
        if (counts.Count != values.Length) return false;
        foreach (BistroBuilderMarketingCampaignType type in values)
            if (!counts.TryGetValue(type, out int count) || count != 5)
                return false;
        return true;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var result = new List<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded)
            return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null &&
                string.Equals(transform.name, name, StringComparison.Ordinal))
                result.Add(transform.gameObject);
        return result.ToArray();
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded)
            return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
