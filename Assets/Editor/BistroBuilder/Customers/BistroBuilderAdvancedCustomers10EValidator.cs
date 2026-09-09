using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10EValidationResult
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
        "=== BISTRO BUILDER — 10E / FIDELIDAD Y RECOMENDACIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10EValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    [MenuItem("Tools/Bistro Builder/Customers/10E - Validar", false, 10041)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var result = ValidateCurrentScene();
        if (result.Errors > 0)
            throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderAdvancedCustomers10EValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10EValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded &&
            scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa o guardada.");
        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var advocacy = FindSceneComponents<BistroBuilderAdvancedCustomerAdvocacyService>(scene);
        var history = FindSceneComponents<BistroBuilderAdvancedCustomerHistoryService>(scene);
        var tracking = FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        var marketing = FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);

        result.Check(advocacy.Length == 1 && host != null &&
            advocacy[0].gameObject == host,
            "AdvocacyService es único y vive en GameSystems.",
            "AdvocacyService falta, está duplicado o mal ubicado.");
        result.Check(history.Length == 1 && tracking.Length == 1 && marketing.Length == 1,
            "Historial, Experience Tracking y Marketing mantienen autoridad única.",
            "Alguna autoridad consumida por 10E falta o está duplicada.");

        if (advocacy.Length == 1)
            result.Check(advocacy[0].ValidateConfiguration(out _),
                "AdvocacyService valida sus autoridades canónicas.",
                "AdvocacyService no valida.");
        if (marketing.Length == 1)
        {
            result.Check(marketing[0].ValidateConfiguration(out _),
                "Marketing Demand Integration sigue validando.",
                "Marketing Demand Integration no valida tras 10E.");
            result.Check(marketing[0].HasReturnCohortPriorityProvider,
                "Marketing detecta el proveedor opcional de prioridad de retorno.",
                "Marketing no detecta AdvocacyService como prioridad de retorno.");
        }

        BistroBuilderAdvancedCustomers10DValidationResult previous =
            BistroBuilderAdvancedCustomers10DValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10D permanece estructuralmente verde.",
            "10E introduce una regresión sobre 10D.");
        return result;
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
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
