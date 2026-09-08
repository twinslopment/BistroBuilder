using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10FValidationResult
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
        "=== BISTRO BUILDER — 10F / COMPORTAMIENTO EN SERVICIO ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10FValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    [MenuItem("Tools/Bistro Builder/Customers/10F - Validar", false, 10051)]
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

    public static BistroBuilderAdvancedCustomers10FValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10FValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa o guardada.");
        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");
        var behavior = FindSceneComponents<BistroBuilderAdvancedCustomerBehaviorService>(scene);
        var tracking = FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        var profiles = FindSceneComponents<BistroBuilderAdvancedCustomerProfileService>(scene);
        var assignment = FindSceneComponents<TableAssignmentSystem>(scene);
        result.Check(behavior.Length == 1 && host != null && behavior[0].gameObject == host,
            "BehaviorService es único y vive en GameSystems.",
            "BehaviorService falta, está duplicado o mal ubicado.");
        result.Check(tracking.Length == 1 && profiles.Length == 1 && assignment.Length == 1,
            "Tracking, perfiles y TableAssignment mantienen autoridad única.",
            "Alguna autoridad consumida por 10F falta o está duplicada.");
        if (behavior.Length == 1)
            result.Check(behavior[0].ValidateConfiguration(out _),
                "BehaviorService valida sus dependencias canónicas.",
                "BehaviorService no valida.");

        result.Check(typeof(BistroBuilderCustomerExperienceTrackingService)
                .GetMethod("TryGetRuntimeVisit") != null,
            "Experience Tracking expone lectura segura sin duplicar cronómetros.",
            "Falta el contrato de lectura del runtime de espera.");

        BistroBuilderAdvancedCustomers10EValidationResult previous =
            BistroBuilderAdvancedCustomers10EValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10E permanece estructuralmente verde.",
            "10F introduce una regresión sobre 10E.");
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
