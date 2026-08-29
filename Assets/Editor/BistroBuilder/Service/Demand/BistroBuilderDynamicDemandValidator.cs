using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderDynamicDemandValidationResult
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
        "=== BISTRO BUILDER — DEMANDA BASE DINÁMICA VALIDACIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

/// <summary>Gate estructural del motor universal de demanda base.</summary>
public static class BistroBuilderDynamicDemandValidator
{
    [MenuItem("Tools/Bistro Builder/Service/Demanda dinámica - Validar", false, 8302)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderDynamicDemandValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderDynamicDemandValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Escena principal activa y guardada.", "No existe escena principal guardada.");

        GameObject[] hosts = FindNamed(scene, "GameSystems");
        result.Check(hosts.Length == 1,
            "Existe un único GameSystems canónico.", "GameSystems falta o está duplicado.");

        BistroBuilderDynamicDemandService[] dynamic =
            FindScene<BistroBuilderDynamicDemandService>(scene);
        result.Check(dynamic.Length == 1 && hosts.Length == 1 && dynamic[0].gameObject == hosts[0],
            "DynamicDemandService es único y vive en GameSystems.",
            "DynamicDemandService falta, está duplicado o mal ubicado.");
        if (dynamic.Length == 1)
            result.Check(dynamic[0].ValidateConfiguration(out _),
                "La demanda dinámica valida todas sus autoridades.",
                "La demanda dinámica tiene dependencias inválidas.");

        BistroBuilderMarketingDemandIntegrationService[] marketing =
            FindScene<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(marketing.Length == 1 && marketing[0].ValidateConfiguration(out _),
            "Marketing compone sobre la demanda dinámica.",
            "Marketing no valida con la nueva demanda base.");

        CustomerGroupSpawner[] spawners = FindScene<CustomerGroupSpawner>(scene);
        result.Check(spawners.Length == 1,
            "Existe un único materializador de CustomerGroup.",
            "CustomerGroupSpawner falta o está duplicado.");

        result.Check(HasField(typeof(BistroBuilderCustomerDemandPlan), "arrivalDelaySeconds") &&
                     HasField(typeof(BistroBuilderCustomerArrivalPlanSaveRecord),
                         "delayBeforeArrivalSeconds"),
            "Plan y service.runtime conservan cadencia variable.",
            "La cadencia dinámica no forma parte del contrato persistible.");

        result.Check(HasField(typeof(BistroBuilderMarketingDemandIntegrationService),
                         "dynamicDemandService"),
            "Marketing referencia explícitamente la autoridad de demanda base.",
            "Marketing sigue dependiendo exclusivamente del baseline provisional.");

        result.Check(HasNoMarketingField(typeof(BistroBuilderDynamicDemandService)) &&
                     HasNoMarketingField(typeof(BistroBuilderDynamicDemandEngine)),
            "La demanda base es independiente de tipos Marketing.",
            "La autoridad base depende directamente de Marketing.");

        result.Check(typeof(BistroBuilderDynamicDemandEngine).IsAbstract &&
                     typeof(BistroBuilderDynamicDemandEngine).IsSealed,
            "El motor de cálculo es puro y estático.",
            "El motor de demanda no conserva el contrato puro esperado.");

        BistroBuilderReputationBlock8ValidationResult reputation =
            BistroBuilderReputationBlock8Validator.ValidateCurrentScene();
        result.Check(reputation.Errors == 0,
            "Bloque 8 Reputación permanece estructuralmente verde.",
            "La demanda dinámica ha introducido una regresión en Reputación.");

        BistroBuilderMarketingPlayerUiValidationResult marketingUi =
            BistroBuilderMarketingPlayerUiValidator.ValidateCurrentScene();
        result.Check(marketingUi.Errors == 0,
            "Bloque 7 Marketing permanece estructuralmente verde.",
            "La demanda dinámica ha introducido una regresión en Marketing.");
        return result;
    }

    private static bool HasField(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic) != null;

    private static bool HasNoMarketingField(Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                             BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].FieldType.Name.IndexOf("Marketing", StringComparison.Ordinal) >= 0)
                return false;
        return true;
    }

    private static GameObject[] FindNamed(Scene scene, string name)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == name) list.Add(t.gameObject);
        return list.ToArray();
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) list.Add(found[i]);
        }
        return list.ToArray();
    }
}
