using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class BistroBuilderNavigation17Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Bistro Builder/17 Navegacion/Instalar bloque 17")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null) throw new InvalidOperationException("Falta GameSystems.");

        BistroBuilderNavigationService navigation =
            Ensure<BistroBuilderNavigationService>(systems);
        Ensure<BistroBuilderNavigationEditIntegration>(systems);
        NavMeshSurface surface = Ensure<NavMeshSurface>(systems);
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        ConfigureAreas();
        InstallSeatEnvelopesInScene();

        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        surface.BuildNavMesh();
        navigation.RebuildNavigationTopology();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Bistro Builder/17 Navegacion/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderNavigation17Validator.Run();
        BistroBuilderNavigation17SelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BLOQUE 17 - INSTALACION PASS");
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

    private static void ConfigureAreas()
    {
        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RestaurantArea area in areas)
        {
            if (area == null) continue;
            BistroBuilderNavigationAccessZone zone =
                area.GetComponent<BistroBuilderNavigationAccessZone>();
            if (zone == null) zone = Undo.AddComponent<BistroBuilderNavigationAccessZone>(area.gameObject);

            BistroBuilderNavigationAgentMask allowed = BistroBuilderNavigationAgentMask.All;
            string id = area.AreaId ?? string.Empty;
            if (id.IndexOf("kitchen", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                allowed = BistroBuilderNavigationAgentMask.Waiter |
                          BistroBuilderNavigationAgentMask.Staff |
                          BistroBuilderNavigationAgentMask.Delivery;
            }
            zone.ConfigureForEditor(area, allowed, 1f);
            EditorUtility.SetDirty(zone);
        }
    }

    private static void InstallSeatEnvelopesInScene()
    {
        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RestaurantSeat seat in seats)
        {
            if (seat == null) continue;
            if (seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() == null)
                Undo.AddComponent<BistroBuilderSeatCirculationEnvelope>(seat.gameObject);
            EditorUtility.SetDirty(seat.gameObject);
        }
    }

    private static void InstallSeatEnvelopesInPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            RestaurantSeat[] seats = root.GetComponentsInChildren<RestaurantSeat>(true);
            foreach (RestaurantSeat seat in seats)
            {
                if (seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() != null) continue;
                seat.gameObject.AddComponent<BistroBuilderSeatCirculationEnvelope>();
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
