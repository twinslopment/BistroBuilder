using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalación idempotente del coordinador lógico v1.
/// No crea ni modifica geometría BBSIS.
/// </summary>
public static class BistroBuilderInteractionV1Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Instalar")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException("Interaction v1: falta GameSystems.");

        EnsureSingleAuthority<BistroBuilderInteractionService>();
        BistroBuilderInteractionBbsisBridge bridge =
            Ensure<BistroBuilderInteractionBbsisBridge>(systems);
        BistroBuilderInteractionService service =
            Ensure<BistroBuilderInteractionService>(systems);
        BistroBuilderInteractionSaveSectionProvider saveProvider =
            Ensure<BistroBuilderInteractionSaveSectionProvider>(systems);
        BistroBuilderSeatingReservationCoordinator seating =
            Ensure<BistroBuilderSeatingReservationCoordinator>(systems);
        BistroBuilderWaiterTaskClaimCoordinator waiterClaims =
            Ensure<BistroBuilderWaiterTaskClaimCoordinator>(systems);
        BistroBuilderKitchenInteractionCoordinator kitchenInteraction =
            Ensure<BistroBuilderKitchenInteractionCoordinator>(systems);
        BistroBuilderDishCustodyCoordinator dishCustody =
            Ensure<BistroBuilderDishCustodyCoordinator>(systems);
        RestaurantTableRegistry tableRegistry =
            UnityEngine.Object.FindFirstObjectByType<RestaurantTableRegistry>();

        service.ConfigureForEditor(bridge);
        seating.ConfigureForEditor(service, tableRegistry);
        waiterClaims.ConfigureForEditor(service);
        kitchenInteraction.ConfigureForEditor(service);
        dishCustody.ConfigureForEditor(service);
        service.RebuildTargets();
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(service);
        EditorUtility.SetDirty(saveProvider);
        EditorUtility.SetDirty(seating);
        EditorUtility.SetDirty(waiterClaims);
        EditorUtility.SetDirty(kitchenInteraction);
        EditorUtility.SetDirty(dishCustody);
        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderInteractionV1Validator.Run();
        BistroBuilderInteractionV1SelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BB INTERACTION & RESERVATION v1 - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null) component = Undo.AddComponent<T>(target);
        return component;
    }

    private static void EnsureSingleAuthority<T>() where T : Component
    {
        T[] existing = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (existing.Length > 1)
            throw new InvalidOperationException(
                "Interaction v1: autoridad duplicada " + typeof(T).Name + ".");
    }
}
