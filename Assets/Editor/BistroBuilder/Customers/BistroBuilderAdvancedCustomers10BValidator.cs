using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10BValidationResult
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
        "=== BISTRO BUILDER — 10B / SATISFACCIÓN INDIVIDUAL ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10BValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10B - Validar", false, 10011)]
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
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderAdvancedCustomers10BValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10BValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa/guardada.");

        var profiles = FindSceneComponents<BistroBuilderAdvancedCustomerProfileService>(scene);
        var tracking = FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        result.Check(profiles.Length == 1 && tracking.Length == 1,
            "Perfil avanzado y Experience Tracking mantienen autoridad única.",
            "Algún servicio 10A/8B falta o está duplicado.");

        if (tracking.Length == 1 && profiles.Length == 1)
        {
            result.Check(tracking[0].ValidateConfiguration(out _),
                "Experience Tracking valida también la dependencia avanzada.",
                "Experience Tracking no valida tras 10B.");
            result.Check(ReferenceEquals(
                    tracking[0].AdvancedCustomerProfileService, profiles[0]),
                "Satisfacción individual alimenta la autoridad 8B existente.",
                "Tracking no está enlazado al perfil avanzado canónico.");
        }

        result.Check(FindSceneComponents<BistroBuilderReputationService>(scene).Length == 1,
            "Reputación sigue siendo autoridad única del resultado agregado.",
            "Reputación falta o está duplicada.");
        result.Check(FindSceneComponents<BistroBuilderGuestRelationsService>(scene).Length == 1,
            "GuestRelations conserva autoridad única de retorno/habituales.",
            "GuestRelations falta o está duplicado.");

        BistroBuilderAdvancedCustomers10AValidationResult previous =
            BistroBuilderAdvancedCustomers10AValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10A permanece estructuralmente verde.",
            "10B introduce una regresión sobre 10A.");
        return result;
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
