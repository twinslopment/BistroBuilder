using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMarketing7CValidationResult
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
        "=== BISTRO BUILDER — 7C / VALIDACIÓN PERSISTENCIA ===\n" +
        string.Join("\n", lines) +
        "\nResultado: " + Passed + " OK / " + Errors + " errores.";
}

public static class BistroBuilderMarketing7CValidator
{
    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        BistroBuilderMarketing7CValidationResult result = ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors > 0)
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }
        Debug.Log(report);
    }

    [MenuItem("Tools/Bistro Builder/Marketing/7C - Validar", false, 7311)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMarketing7CValidationResult result = ValidateCurrentScene();
        string report = result.BuildReport();
        if (result.Errors == 0) Debug.Log(report);
        else Debug.LogError(report);
    }

    public static BistroBuilderMarketing7CValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderMarketing7CValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] gameSystems = FindNamedObjects(scene, "GameSystems");
        result.Check(
            scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Existe una escena activa guardada.",
            "No existe una escena activa guardada.");
        result.Check(
            gameSystems.Length == 1,
            "Existe exactamente un GameSystems canónico.",
            "Se esperaba un GameSystems; hay " + gameSystems.Length + ".");

        BistroBuilderMarketingSaveSectionProvider[] providers =
            FindSceneComponents<BistroBuilderMarketingSaveSectionProvider>(scene);
        result.Check(
            providers.Length == 1,
            "Existe exactamente un proveedor marketing.state.",
            "Se esperaba un proveedor marketing.state; hay " + providers.Length + ".");

        if (providers.Length == 1 && gameSystems.Length == 1)
        {
            result.Check(
                providers[0].gameObject == gameSystems[0],
                "marketing.state vive en GameSystems.",
                "marketing.state no vive en GameSystems.");
            result.Check(
                providers[0].ValidateConfiguration(out _),
                "El proveedor 7C valida todas sus dependencias.",
                "El proveedor 7C tiene dependencias inválidas.");
            result.Check(
                providers[0].PrepareOrder < 9000 &&
                providers[0].FinalizeOrder > 11200,
                "7C respeta el orden seguro de reconstrucción de service.runtime y Reservas.",
                "El orden de fases de 7C puede generar efectos durante la carga.");
        }

        BistroBuilderSaveGameService[] saves =
            FindSceneComponents<BistroBuilderSaveGameService>(scene);
        result.Check(
            saves.Length == 1,
            "Existe un único SaveGameService.",
            "Se esperaba exactamente un SaveGameService.");
        if (saves.Length == 1)
        {
            saves[0].RefreshExtensions();
            result.Check(
                saves[0].HasProvider(BistroBuilderMarketingSaveSectionProvider.StableSectionId),
                "SaveGame descubre marketing.state por su contrato universal.",
                "SaveGame no descubre marketing.state.");
        }

        result.Check(
            HasAcquisitionField(typeof(BistroBuilderCustomerArrivalPlanSaveRecord)),
            "service.runtime persiste atribución en llegadas pendientes.",
            "Las llegadas pendientes no conservan atribución de captación.");
        result.Check(
            HasAcquisitionField(typeof(BistroBuilderCustomerGroupSaveRecord)),
            "service.runtime persiste atribución en grupos materializados.",
            "Los CustomerGroup activos no conservan atribución de captación.");

        BistroBuilderMarketing7BValidationResult sevenB =
            BistroBuilderMarketing7BValidator.ValidateCurrentScene();
        result.Check(
            sevenB.Errors == 0,
            "7C conserva todos los gates estructurales de 7B.",
            "7C rompió un gate estructural de 7B.");
        return result;
    }

    private static bool HasAcquisitionField(Type type)
    {
        FieldInfo field = type.GetField(
            "acquisition",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null &&
            field.FieldType == typeof(BistroBuilderCustomerAcquisitionProfile);
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
