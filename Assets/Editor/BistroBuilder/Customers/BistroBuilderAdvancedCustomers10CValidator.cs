using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10CValidationResult
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
        "=== BISTRO BUILDER — 10C / HISTORIAL, HABITUALES Y VIP ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10CValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10C - Validar", false, 10021)]
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

    public static BistroBuilderAdvancedCustomers10CValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10CValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa o guardada.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var history = FindSceneComponents<BistroBuilderAdvancedCustomerHistoryService>(scene);
        var provider = FindSceneComponents<BistroBuilderAdvancedCustomerHistorySaveSectionProvider>(scene);
        var tracking = FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        var relations = FindSceneComponents<BistroBuilderGuestRelationsService>(scene);
        var save = FindSceneComponents<BistroBuilderSaveGameService>(scene);

        result.Check(history.Length == 1 && host != null && history[0].gameObject == host,
            "AdvancedCustomerHistoryService es único y vive en GameSystems.",
            "El historial avanzado falta, está duplicado o mal ubicado.");
        result.Check(provider.Length == 1 && host != null && provider[0].gameObject == host,
            "advanced_customers.state tiene un proveedor único en GameSystems.",
            "El proveedor persistente 10C falta, está duplicado o mal ubicado.");
        result.Check(tracking.Length == 1 && relations.Length == 1 && save.Length == 1,
            "Tracking, GuestRelations y SaveGame conservan autoridad única.",
            "Alguna autoridad consumida por 10C falta o está duplicada.");

        if (history.Length == 1 && tracking.Length == 1 && relations.Length == 1)
        {
            result.Check(history[0].ValidateConfiguration(out _),
                "El historial avanzado valida su estado y dependencias.",
                "AdvancedCustomerHistoryService no valida.");
            result.Check(ReferenceEquals(history[0].ExperienceTrackingService, tracking[0]) &&
                         ReferenceEquals(history[0].GuestRelationsService, relations[0]),
                "10C consume Tracking y GuestRelations sin duplicar responsabilidades.",
                "10C no referencia las autoridades canónicas.");
        }

        if (provider.Length == 1)
            result.Check(provider[0].ValidateConfiguration(out _),
                "El proveedor advanced_customers.state valida correctamente.",
                "El proveedor persistent 10C no valida.");
        if (save.Length == 1)
        {
            save[0].RefreshExtensions();
            result.Check(save[0].HasProvider(
                    BistroBuilderAdvancedCustomerHistorySaveSectionProvider.StableSectionId),
                "SaveGame descubre advanced_customers.state.",
                "SaveGame no descubre la sección 10C.");
        }

        BistroBuilderAdvancedCustomers10BValidationResult previous =
            BistroBuilderAdvancedCustomers10BValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10B permanece estructuralmente verde.",
            "10C introduce una regresión sobre 10B.");

        return result;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
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
