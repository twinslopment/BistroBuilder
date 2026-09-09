using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10AValidationResult
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
        "=== BISTRO BUILDER — 10A / CLIENTES AVANZADOS ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10AValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10A - Validar", false, 10001)]
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

    public static BistroBuilderAdvancedCustomers10AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded &&
            scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa, guardada o está Dirty.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var services = FindSceneComponents<BistroBuilderAdvancedCustomerProfileService>(scene);
        result.Check(services.Length == 1 && host != null &&
            services[0].gameObject == host,
            "AdvancedCustomerProfileService es único y vive en GameSystems.",
            "El servicio de perfiles avanzados falta, está duplicado o mal ubicado.");

        var general = FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
        var tables = FindSceneComponents<TableAssignmentSystem>(scene);
        result.Check(general.Length == 1 && tables.Length == 1,
            "Estado general y TableAssignmentSystem conservan autoridad única.",
            "Alguna autoridad consumida por 10A falta o está duplicada.");

        if (services.Length == 1)
        {
            BistroBuilderAdvancedCustomerProfileService service = services[0];
            result.Check(service.ValidateConfiguration(out _),
                "El servicio valida catálogo y dependencias canónicas.",
                "El servicio avanzado no valida su configuración.");
            result.Check(service.ProfileCatalog != null &&
                service.ProfileCatalog.Count == 10 &&
                service.ProfileCatalog.ValidateConfiguration(out _),
                "El catálogo canónico contiene 10 arquetipos válidos.",
                "El catálogo avanzado falta, tiene cardinalidad incorrecta o es inválido.");
            result.Check(general.Length == 1 &&
                ReferenceEquals(service.GeneralGameStateService, general[0]) &&
                tables.Length == 1 &&
                ReferenceEquals(service.TableAssignmentSystem, tables[0]),
                "10A consume autoridades existentes sin duplicarlas.",
                "10A no referencia las autoridades canónicas de servicio.");
        }

        result.Check(
            FindSceneComponents<BistroBuilderGuestRelationsService>(scene).Length == 1 &&
            FindSceneComponents<BistroBuilderReputationService>(scene).Length == 1 &&
            FindSceneComponents<BistroBuilderMarketingService>(scene).Length == 1,
            "GuestRelations, Reputación y Marketing mantienen sus autoridades únicas.",
            "10A coincide con una autoridad previa ausente o duplicada.");

        result.Check(
            FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene).Length == 1,
            "La satisfacción existente sigue perteneciendo a Experience Tracking/Reputación.",
            "La autoridad actual de experiencia de cliente no es única.");

        BistroBuilderProgression9EValidationResult previous =
            BistroBuilderProgression9EValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "Bloque 9 permanece estructuralmente verde.",
            "10A introduce una regresión estructural sobre el Bloque 9.");

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
