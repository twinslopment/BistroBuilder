using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10DValidationResult
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
        "=== BISTRO BUILDER — 10D / MESA, VIP Y NECESIDADES ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10DValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10D - Validar", false, 10031)]
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

    public static BistroBuilderAdvancedCustomers10DValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10DValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded &&
            scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa o guardada.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var services = FindSceneComponents<BistroBuilderAdvancedCustomerSeatingPreferenceService>(scene);
        var profiles = FindSceneComponents<BistroBuilderAdvancedCustomerProfileService>(scene);
        var history = FindSceneComponents<BistroBuilderAdvancedCustomerHistoryService>(scene);
        var assignment = FindSceneComponents<TableAssignmentSystem>(scene);
        var registry = FindSceneComponents<RestaurantTableRegistry>(scene);
        result.Check(services.Length == 1 && host != null &&
            services[0].gameObject == host,
            "SeatingPreferenceService es único y vive en GameSystems.",
            "SeatingPreferenceService falta, está duplicado o mal ubicado.");
        result.Check(profiles.Length == 1 && history.Length == 1 &&
            assignment.Length == 1 && registry.Length == 1,
            "Perfiles, historial, asignación y registro de mesas mantienen autoridad única.",
            "Alguna autoridad consumida por 10D falta o está duplicada.");

        if (services.Length == 1)
        {
            result.Check(services[0].ValidateConfiguration(out _),
                "SeatingPreferenceService valida todas sus dependencias.",
                "SeatingPreferenceService no valida.");
            if (profiles.Length == 1 && history.Length == 1 &&
                assignment.Length == 1 && registry.Length == 1)
            {
                result.Check(ReferenceEquals(services[0].ProfileService, profiles[0]) &&
                    ReferenceEquals(services[0].HistoryService, history[0]) &&
                    ReferenceEquals(services[0].TableAssignmentSystem, assignment[0]) &&
                    ReferenceEquals(services[0].TableRegistry, registry[0]),
                    "10D consume exclusivamente las autoridades canónicas.",
                    "10D no referencia las autoridades canónicas.");
            }
        }

        result.Check(typeof(TableAssignmentSystem).GetEvent("CustomerGroupRegistered") != null,
            "TableAssignment expone el hook previo a asignación sin delegar autoridad.",
            "Falta el hook síncrono de registro previo a asignación.");

        BistroBuilderAdvancedCustomers10CValidationResult previous =
            BistroBuilderAdvancedCustomers10CValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10C permanece estructuralmente verde.",
            "10D introduce una regresión sobre 10C.");
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
