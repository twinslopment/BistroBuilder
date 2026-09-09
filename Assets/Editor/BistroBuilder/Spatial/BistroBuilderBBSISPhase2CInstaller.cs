using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala la integración espacial BBSIS con el Modo Edición.
/// </summary>
public static class BistroBuilderBBSISPhase2CInstaller
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Bistro Builder/BBSIS/Fase 2C/Instalar")]
    public static void Install()
    {
        BistroBuilderBBSISPhase2BInstaller.Install();
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena canónica BBSIS 2C.");

        PrimeSeatingTopologyForEditor();

        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException(
                "BBSIS 2C necesita GameSystems.");
        BistroBuilderSpatialInteractionService spatial =
            Require<BistroBuilderSpatialInteractionService>(systems);
        BistroBuilderSpatialAssessmentService layoutAssessment =
            Require<BistroBuilderSpatialAssessmentService>(systems);
        BistroBuilderSpatialRuntimeBinder binder =
            Require<BistroBuilderSpatialRuntimeBinder>(systems);

        BistroBuilderSpatialPlacementAssessmentService candidateAssessment =
            GetOrAdd<BistroBuilderSpatialPlacementAssessmentService>(
                systems);
        candidateAssessment.ConfigureForEditor(spatial);

        RestaurantPlacementConstraintService constraintService =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementConstraintService>();
        if (constraintService == null)
            throw new InvalidOperationException(
                "BBSIS 2C necesita PlacementConstraintService.");

        BistroBuilderSpatialPlacementConstraintRule rule =
            GetOrAdd<BistroBuilderSpatialPlacementConstraintRule>(
                constraintService.gameObject);
        rule.ConfigureForEditor(candidateAssessment);
        constraintService.RefreshRules();

        RestaurantPlacementTransactionService transactions =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementTransactionService>();
        RestaurantPlacementHistoryService history =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementHistoryService>();
        RestaurantPlaceableRegistry registry =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableRegistry>();
        RestaurantPlaceableLifecycleService lifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableLifecycleService>();
        if (transactions == null || history == null ||
            registry == null || lifecycle == null)
            throw new InvalidOperationException(
                "BBSIS 2C necesita el ciclo completo del Modo Edición.");

        BistroBuilderSpatialEditModeIntegration integration =
            GetOrAdd<BistroBuilderSpatialEditModeIntegration>(systems);
        integration.ConfigureForEditor(
            spatial,
            layoutAssessment,
            candidateAssessment,
            binder,
            transactions,
            history,
            registry,
            lifecycle);
        integration.RefreshNow();

        MarkDirty(systems);
        MarkDirty(constraintService.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2C/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderBBSISPhase2CValidator.Run();
        BistroBuilderBBSISPhase2CSelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 2C - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void PrimeSeatingTopologyForEditor()
    {
        RestaurantTableRegistry tableRegistry =
            UnityEngine.Object.FindFirstObjectByType<RestaurantTableRegistry>();
        RestaurantSeatRegistry seatRegistry =
            UnityEngine.Object.FindFirstObjectByType<RestaurantSeatRegistry>();
        RestaurantSeatingTopologyService topology =
            UnityEngine.Object.FindFirstObjectByType<RestaurantSeatingTopologyService>();
        if (tableRegistry == null || seatRegistry == null || topology == null)
            throw new InvalidOperationException(
                "BBSIS 2C necesita registros y topología de seating.");

        RestaurantTable[] tables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < tables.Length; i++)
            if (tables[i] != null)
                tableRegistry.RegisterTable(tables[i]);

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < seats.Length; i++)
            if (seats[i] != null)
                seatRegistry.RegisterSeat(seats[i]);

        topology.RebuildImmediately();
    }
    private static T Require<T>(GameObject source)
        where T : Component
    {
        T component = source.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException(
                source.name + " necesita " + typeof(T).Name + ".");
        return component;
    }
    private static T GetOrAdd<T>(GameObject source)
        where T : Component
    {
        T component = source.GetComponent<T>();
        return component != null
            ? component
            : source.AddComponent<T>();
    }

    private static void MarkDirty(GameObject source)
    {
        if (source == null)
            return;
        Component[] components = source.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null)
                EditorUtility.SetDirty(components[i]);
        EditorUtility.SetDirty(source);
    }
}
