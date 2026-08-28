using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketing7BValidationResult
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
        "=== BISTRO BUILDER — 7B / VALIDACIÓN DEMANDA ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketing7BValidator
{
    [MenuItem("Tools/Bistro Builder/Marketing/7B - Validar", false, 7211)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketing7BValidationResult result = ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report);
        else Debug.LogError(report);
    }

    public static BistroBuilderMarketing7BValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketing7BValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(
            scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");

        GameObject[] gameSystems = FindNamedObjects(scene, "GameSystems");
        result.Check(
            gameSystems.Length == 1,
            "Existe exactamente un GameSystems canónico.",
            "Se esperaba un GameSystems; hay " + gameSystems.Length + ".");

        BistroBuilderMarketingDemandIntegrationService[] integrations =
            FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(
            integrations.Length == 1,
            "Existe exactamente una integración de demanda 7B.",
            "Se esperaba una integración 7B; hay " + integrations.Length + ".");

        if (integrations.Length == 1 && gameSystems.Length == 1)
        {
            result.Check(
                integrations[0].gameObject == gameSystems[0],
                "La integración 7B vive en GameSystems.",
                "La integración 7B no vive en GameSystems.");
            result.Check(
                integrations[0].ValidateConfiguration(out _),
                "La integración 7B valida todas sus dependencias.",
                "La integración 7B tiene dependencias inválidas.");
            result.Check(
                integrations[0].TryBuildProjection(out var projection, out _) &&
                projection != null && projection.baselineWalkInGroups > 0,
                "7B construye una proyección válida sobre la escena real.",
                "7B no puede proyectar la demanda de la escena real.");
        }

        CustomerGroupSpawner[] spawners =
            FindSceneComponents<CustomerGroupSpawner>(scene);
        result.Check(
            spawners.Length == 1 && spawners[0].BaselineGroupCount > 0,
            "Existe un único flujo canónico de walk-ins con demanda base.",
            "El flujo canónico de walk-ins es inexistente, duplicado o inválido.");

        BistroBuilderMarketingService[] marketing =
            FindSceneComponents<BistroBuilderMarketingService>(scene);
        result.Check(
            marketing.Length == 1,
            "MarketingService sigue siendo autoridad única de campañas.",
            "Se esperaba exactamente un MarketingService.");

        BistroBuilderReservationService[] reservations =
            FindSceneComponents<BistroBuilderReservationService>(scene);
        result.Check(
            reservations.Length == 1,
            "Reservas conserva una única autoridad persistente.",
            "Se esperaba exactamente un ReservationService.");

        BistroBuilderReservationAvailabilityService[] availability =
            FindSceneComponents<BistroBuilderReservationAvailabilityService>(scene);
        result.Check(
            availability.Length == 1,
            "La capacidad de reservas sigue en ReservationAvailabilityService.",
            "Se esperaba un único ReservationAvailabilityService.");

        result.Check(
            CustomerSpawnerHasNoMarketingField(),
            "CustomerGroupSpawner permanece desacoplado de Marketing.",
            "CustomerGroupSpawner contiene una dependencia directa de Marketing.");
        result.Check(
            AcquisitionTagHasNoMarketingField(),
            "El perfil de captación es un contrato genérico del servicio.",
            "La etiqueta de captación contiene autoridad de Marketing.");

        BistroBuilderMarketing7AValidationResult sevenA =
            BistroBuilderMarketing7AValidator.ValidateCurrentScene();
        result.Check(
            sevenA.Errors == 0,
            "La instalación 7B conserva todos los gates estructurales de 7A.",
            "La instalación 7B rompió un gate estructural de 7A.");

        return result;
    }

    private static bool CustomerSpawnerHasNoMarketingField()
    {
        return HasNoMarketingField(typeof(CustomerGroupSpawner));
    }

    private static bool AcquisitionTagHasNoMarketingField()
    {
        return HasNoMarketingField(typeof(BistroBuilderCustomerAcquisitionTag));
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
            for (int index = 0; index < found.Length; index++)
                if (found[index] != null) result.Add(found[index]);
        }
        return result.ToArray();
    }
}
