using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderAdvancedCustomers10GValidationResult
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
        "=== BISTRO BUILDER — 10G / FICHA INDIVIDUAL ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderAdvancedCustomers10GValidator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string PrefabPath = "Assets/Prefabs/Customers/CustomerGroupPrefab.prefab";

    [MenuItem("Tools/Bistro Builder/Customers/10G - Validar", false, 10061)]
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

    public static BistroBuilderAdvancedCustomers10GValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderAdvancedCustomers10GValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded &&
            scene.path == ScenePath && !scene.isDirty,
            "Escena principal activa y guardada.",
            "La escena principal no está activa o guardada.");

        GameObject host = FindUniqueGameSystems(scene);
        result.Check(host != null,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        var inspections = FindSceneComponents<
            BistroBuilderAdvancedCustomerInspectionService>(scene);
        var controllers = FindSceneComponents<
            BistroBuilderAdvancedCustomerInspectionController>(scene);
        result.Check(inspections.Length == 1 && host != null &&
            inspections[0].gameObject == host,
            "InspectionService es único y vive en GameSystems.",
            "InspectionService falta, está duplicado o mal ubicado.");
        result.Check(controllers.Length == 1 && host != null &&
            controllers[0].gameObject == host,
            "InspectionController es único y vive en GameSystems.",
            "InspectionController falta, está duplicado o mal ubicado.");
        if (inspections.Length == 1)
            result.Check(inspections[0].ValidateConfiguration(out _),
                "InspectionService valida sus autoridades canónicas.",
                "InspectionService no valida su configuración.");
        if (controllers.Length == 1)
            result.Check(controllers[0].ValidateConfiguration(out _),
                "InspectionController valida cámara, HUD y servicio.",
                "InspectionController no valida cámara/HUD/servicio.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var visualGroups = prefab != null
            ? prefab.GetComponents<BistroBuilderAdvancedCustomerMemberVisualGroup>()
            : Array.Empty<BistroBuilderAdvancedCustomerMemberVisualGroup>();
        result.Check(prefab != null && visualGroups.Length == 1,
            "CustomerGroupPrefab contiene un único materializador visual individual.",
            "El prefab de clientes no contiene exactamente un visualizador 10G.");
        if (visualGroups.Length == 1)
            result.Check(visualGroups[0].ValidateConfiguration(out _),
                "El materializador visual del prefab valida CustomerGroup y renderer.",
                "El materializador visual del prefab no valida.");

        result.Check(typeof(BistroBuilderAdvancedCustomerInspectionController)
                .GetMethod("TryInspectAtScreenPoint") != null &&
            typeof(BistroBuilderAdvancedCustomerMemberHitTarget) != null,
            "La selección individual expone raycast real y destino por miembro.",
            "Falta el contrato de selección individual por click.");

        BistroBuilderAdvancedCustomers10FValidationResult previous =
            BistroBuilderAdvancedCustomers10FValidator.ValidateCurrentScene();
        result.Check(previous.Errors == 0,
            "10F permanece estructuralmente verde.",
            "10G introduce una regresión sobre 10F.");
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
