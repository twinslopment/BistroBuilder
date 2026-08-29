using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketingGuestRelationsValidationResult
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
        "=== BISTRO BUILDER — MARKETING / GUEST RELATIONS VALIDACIÓN ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}
public static class BistroBuilderMarketingGuestRelationsValidator
{
    [MenuItem("Tools/Bistro Builder/Marketing/Guest Relations - Validar", false, 7251)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketingGuestRelationsValidationResult result =
            ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketingGuestRelationsValidationResult result =
            ValidateCurrentScene();
        if (result.Errors > 0)
            throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderMarketingGuestRelationsValidationResult
        ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketingGuestRelationsValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(
            scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");
        result.Check(
            hosts.Length == 1,
            "Existe un único GameSystems canónico.",
            "GameSystems es inexistente o está duplicado.");

        BistroBuilderGuestRelationsService[] relations =
            FindSceneComponents<BistroBuilderGuestRelationsService>(scene);
        result.Check(
            relations.Length == 1 && hosts.Length == 1 &&
            relations[0].gameObject == hosts[0],
            "GuestRelations es único y vive en GameSystems.",
            "GuestRelations falta, está duplicado o fuera de GameSystems.");
        if (relations.Length == 1)
            result.Check(
                relations[0].ValidateConfiguration(out _),
                "La autoridad de relaciones valida sus dependencias.",
                "GuestRelations tiene dependencias inválidas.");

        BistroBuilderMarketingGuestRelationsBridge[] bridges =
            FindSceneComponents<BistroBuilderMarketingGuestRelationsBridge>(scene);
        result.Check(
            bridges.Length == 1 && hosts.Length == 1 &&
            bridges[0].gameObject == hosts[0],
            "Existe un único puente Marketing → GuestRelations.",
            "El puente Marketing → GuestRelations es inválido.");
        if (bridges.Length == 1)
            result.Check(
                bridges[0].ValidateConfiguration(out _),
                "El puente de reputación valida correctamente.",
                "El puente de reputación tiene dependencias inválidas.");

        BistroBuilderGuestRelationsSaveSectionProvider[] providers =
            FindSceneComponents<BistroBuilderGuestRelationsSaveSectionProvider>(scene);
        result.Check(
            providers.Length == 1 && hosts.Length == 1 &&
            providers[0].gameObject == hosts[0],
            "guest_relations.state tiene un único proveedor persistente.",
            "El proveedor guest_relations.state es inválido.");
        if (providers.Length == 1)
            result.Check(
                providers[0].ValidateConfiguration(out _),
                "guest_relations.state valida sus dependencias.",
                "guest_relations.state no valida.");

        BistroBuilderSaveGameService[] saves =
            FindSceneComponents<BistroBuilderSaveGameService>(scene);
        result.Check(
            saves.Length == 1,
            "Existe un único SaveGameService.",
            "SaveGameService falta o está duplicado.");
        if (saves.Length == 1)
        {
            saves[0].RefreshExtensions();
            result.Check(
                saves[0].HasProvider(
                    BistroBuilderGuestRelationsSaveSectionProvider.StableSectionId),
                "SaveGame descubre guest_relations.state.",
                "SaveGame no descubre guest_relations.state.");
        }

        BistroBuilderMarketingDemandIntegrationService[] demand =
            FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(
            demand.Length == 1 && demand[0].ValidateConfiguration(out _),
            "La integración de demanda consume GuestRelations sin duplicar autoridad.",
            "La integración de demanda quedó inválida.");

        result.Check(
            HasNoMarketingField(typeof(BistroBuilderGuestRelationsService)),
            "GuestRelations permanece independiente de tipos Marketing.",
            "GuestRelations contiene una dependencia directa de Marketing.");
        result.Check(
            HasNoMarketingField(typeof(CustomerGroupSpawner)),
            "CustomerGroupSpawner permanece desacoplado de Marketing.",
            "CustomerGroupSpawner contiene una dependencia directa de Marketing.");

        BistroBuilderMarketing7BValidationResult sevenB =
            BistroBuilderMarketing7BValidator.ValidateCurrentScene();
        result.Check(
            sevenB.Errors == 0,
            "ReservationDemand/7B permanece estructuralmente verde.",
            "GuestRelations rompió 7B.");

        BistroBuilderMarketing7CValidationResult sevenC =
            BistroBuilderMarketing7CValidator.ValidateCurrentScene();
        result.Check(
            sevenC.Errors == 0,
            "La persistencia 7C permanece estructuralmente verde.",
            "GuestRelations rompió 7C.");

        BistroBuilderMarketingOperationalPressureValidationResult pressure =
            BistroBuilderMarketingOperationalPressureValidator.ValidateCurrentScene();
        result.Check(
            pressure.Errors == 0,
            "OperationalPressure permanece estructuralmente verde.",
            "GuestRelations rompió OperationalPressure.");

        return result;
    }

    private static bool HasNoMarketingField(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
            if (fields[index].FieldType.Name.Contains("Marketing"))
                return false;
        return true;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var result = new List<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
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
