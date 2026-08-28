using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketingTargetDemandValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }

    public void Check(bool condition, string ok, string fail)
    {
        if (condition)
        {
            Passed++;
            lines.Add("[OK] " + ok);
        }
        else
        {
            Errors++;
            lines.Add("[ERROR] " + fail);
        }
    }

    public string BuildReport() =>
        "=== BISTRO BUILDER — MARKETING / TARGET DEMAND VALIDACIÓN ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketingTargetDemandValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/TargetDemand - Validar",
        false,
        7221)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketingTargetDemandValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report); else Debug.LogError(report);
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketingTargetDemandValidationResult result =
            ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors > 0)
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static BistroBuilderMarketingTargetDemandValidationResult
        ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketingTargetDemandValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(
            scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");

        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(
            hosts.Length == 1,
            "Existe exactamente un GameSystems canónico.",
            "Se esperaba un GameSystems; hay " + hosts.Length + ".");

        var providers = FindSceneComponents<
            BistroBuilderMarketingMenuSelectionWeightProvider>(scene);
        result.Check(
            providers.Length == 1,
            "Existe un único proveedor TargetDemand → selección.",
            "Se esperaba un proveedor TargetDemand; hay " +
                providers.Length + ".");

        if (providers.Length == 1)
        {
            result.Check(
                hosts.Length == 1 && providers[0].gameObject == hosts[0],
                "El proveedor vive en GameSystems.",
                "El proveedor no vive en GameSystems.");
            result.Check(
                providers[0].ValidateConfiguration(out _),
                "El proveedor valida sus dependencias reales.",
                "El proveedor tiene dependencias inválidas.");
        }

        BistroBuilderMenuSelectionService[] selections =
            FindSceneComponents<BistroBuilderMenuSelectionService>(scene);
        result.Check(
            selections.Length == 1,
            "Existe una única autoridad 2.1D de selección.",
            "Se esperaba un MenuSelectionService; hay " +
                selections.Length + ".");
        result.Check(
            HasNoMarketingField(typeof(BistroBuilderMenuSelectionService)),
            "2.1D permanece desacoplado de tipos Marketing.",
            "MenuSelectionService contiene una dependencia directa de Marketing.");

        BistroBuilderMarketingService[] marketing =
            FindSceneComponents<BistroBuilderMarketingService>(scene);
        result.Check(
            marketing.Length == 1 && marketing[0].ValidateConfiguration(out _),
            "MarketingService conserva configuración válida.",
            "MarketingService falta, está duplicado o es inválido.");

        BistroBuilderMarketingDemandIntegrationService[] demand =
            FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(
            demand.Length == 1 && demand[0].ValidateConfiguration(out _),
            "La demanda 7B reconoce el portfolio activo.",
            "La integración de demanda no reconoce el portfolio de cartas.");

        BistroBuilderMarketing7CValidationResult sevenC =
            BistroBuilderMarketing7CValidator.ValidateCurrentScene();
        result.Check(
            sevenC.Errors == 0,
            "El gate estructural 7C permanece verde.",
            "TargetDemand rompió la validación 7C.");

        BistroBuilderSignatureDish21DValidationResult menu21D =
            BistroBuilderSignatureDish21DValidator.ValidateCurrentProject();
        result.Check(
            menu21D.ErrorCount == 0,
            "El validador histórico 2.1D permanece sin errores.",
            "La extensión TargetDemand rompió un gate 2.1D.\n" +
            menu21D.BuildReport());

        return result;
    }

    private static bool HasNoMarketingField(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
        {
            if (fields[index].FieldType.Name.Contains("Marketing"))
                return false;
        }
        return true;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var result = new List<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null &&
                string.Equals(transform.name, name, StringComparison.Ordinal))
                result.Add(transform.gameObject);
        }
        return result.ToArray();
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < found.Length; index++)
                if (found[index] != null) result.Add(found[index]);
        }
        return result.ToArray();
    }
}

// Fin del validador TargetDemand.
