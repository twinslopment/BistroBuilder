using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala la semántica operacional real de cocina, pass y barra.
/// Mantiene intactas las autoridades de producción, servicio y navegación.
/// </summary>
public static class BistroBuilderBBSISPhase2BInstaller
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ContractFolder =
        "Assets/Resources/BistroBuilder/Spatial/Contracts";
    private const string StationCatalogPath =
        "Assets/Data/Kitchen/BB_Kitchen_Station_Catalog.asset";

    [MenuItem("Bistro Builder/BBSIS/Fase 2B/Instalar")]
    public static void Install()
    {
        BistroBuilderBBSISPhase2AInstaller.Install();
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena canónica BBSIS 2B.");

        BistroBuilderKitchenStationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderKitchenStationCatalog>(
                StationCatalogPath);
        string catalogError = string.Empty;
        if (catalog == null ||
            !catalog.TryValidate(out catalogError))
            throw new InvalidOperationException(
                "Catálogo de estaciones inválido: " +
                catalogError);

        KitchenSystem kitchen =
            UnityEngine.Object.FindFirstObjectByType<KitchenSystem>();
        if (kitchen == null || kitchen.PickupPoint == null)
            throw new InvalidOperationException(
                "BBSIS 2B necesita KitchenSystem y PickupPoint.");

        BistroBuilderSpatialContractDefinition kitchenContract =
            BuildKitchenContract(kitchen, catalog);
        if (!BistroBuilderOperationalSpatialBindingUtility.BindKitchen(
                kitchen,
                kitchenContract,
                ResolveKitchenSubjectId(kitchen)))
            throw new InvalidOperationException(
                "No pudo vincularse la cocina real.");

        BistroBuilderSpatialContractDefinition barContract =
            BuildBarContract();
        BistroBuilderBarServiceSpot[] spots =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderBarServiceSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        if (spots.Length == 0)
            throw new InvalidOperationException(
                "BBSIS 2B necesita plazas reales de barra.");
        Array.Sort(spots, (left, right) =>
            string.CompareOrdinal(
                left != null ? left.BarSpotId : string.Empty,
                right != null ? right.BarSpotId : string.Empty));
        for (int i = 0; i < spots.Length; i++)
        {
            BistroBuilderBarServiceSpot spot = spots[i];
            if (spot == null)
                continue;
            RemoveMissingScripts(spot.gameObject);
            if (!BistroBuilderOperationalSpatialBindingUtility.BindBarSpot(
                    spot,
                    barContract,
                    "spatial.bar." + spot.BarSpotId))
                throw new InvalidOperationException(
                    "No pudo vincularse " + spot.BarSpotId + ".");
            MarkSpatialDirty(spot.gameObject);
        }

        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException(
                "BBSIS 2B necesita GameSystems.");
        BistroBuilderOperationalSpatialCoordinator coordinator =
            GetOrAdd<BistroBuilderOperationalSpatialCoordinator>(
                systems);
        coordinator.ConfigureForEditor(
            systems.GetComponent<
                BistroBuilderSpatialInteractionService>(),
            kitchen.GetComponent<
                BistroBuilderKitchenSpatialAdapter>(),
            systems.GetComponent<
                BistroBuilderAdvancedKitchenService>(),
            systems.GetComponent<
                BistroBuilderBarServiceRegistry>(),
            systems.GetComponent<
                BistroBuilderBarServiceSystem>());

        MarkSpatialDirty(kitchen.gameObject);
        MarkSpatialDirty(systems);
        BistroBuilderSpatialInteractionService spatial =
            systems.GetComponent<
                BistroBuilderSpatialInteractionService>();
        spatial?.RebuildSubjects();
        systems.GetComponent<
            BistroBuilderSpatialAssessmentService>()?
            .EvaluateCurrentLayout();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2B/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderBBSISPhase2BValidator.Run();
        BistroBuilderBBSISPhase2BSelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 2B - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static BistroBuilderSpatialContractDefinition
        BuildKitchenContract(
            KitchenSystem kitchen,
            BistroBuilderKitchenStationCatalog catalog)
    {
        BistroBuilderSpatialContractDefinition contract =
            GetOrCreateContract("Kitchen_Operational");
        contract.ConfigureForEditor(
            "spatial.contract.kitchen.operational",
            "work.kitchen",
            BistroBuilderAdaptiveSpatialProxyMode.Layered,
            new[]
            {
                "kitchen.operational",
                "work.station",
                "work.edge",
                "transfer.pass",
                "service.kitchen"
            });
        contract.SetContractVersionForEditor(3);
        contract.ClearSemanticGeometryForEditor();

        Vector3 passLocal = kitchen.transform.InverseTransformPoint(
            kitchen.PickupPoint.position);
        contract.AddPortForEditor(
            new BistroBuilderSpatialPortDefinition
            {
                portId =
                    BistroBuilderKitchenSpatialAdapter.PassPortId,
                kind = BistroBuilderSpatialPortKind.Transfer,
                localPosition = passLocal,
                localForward = Vector3.back,
                radius = 0.36f,
                conflictMode =
                    BistroBuilderSpatialConflictMode.Block
            });

        int totalSlots = 0;
        for (int i = 0; i < catalog.Stations.Count; i++)
            if (catalog.Stations[i] != null)
                totalSlots += Mathf.Max(
                    1, catalog.Stations[i].baseCapacity);
        int columns = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Sqrt(totalSlots)), 2, 5);
        const float spacingX = 0.68f;
        const float spacingZ = 0.72f;
        int globalSlot = 0;

        for (int stationIndex = 0;
             stationIndex < catalog.Stations.Count;
             stationIndex++)
        {
            BistroBuilderKitchenStationDefinition station =
                catalog.Stations[stationIndex];
            if (station == null)
                continue;
            int capacity = Mathf.Max(1, station.baseCapacity);
            Vector3 first = Vector3.zero;
            Vector3 last = Vector3.zero;
            for (int slot = 0; slot < capacity; slot++)
            {
                int column = globalSlot % columns;
                int row = globalSlot / columns;
                float centeredX =
                    (column - (columns - 1) * 0.5f) * spacingX;
                Vector3 local = passLocal +
                    new Vector3(
                        centeredX,
                        0f,
                        0.72f + row * spacingZ);
                if (slot == 0)
                    first = local;
                last = local;
                contract.AddPortForEditor(
                    new BistroBuilderSpatialPortDefinition
                    {
                        portId = "work." +
                            station.stationId + "." + slot,
                        kind =
                            BistroBuilderSpatialPortKind.Work,
                        localPosition = local,
                        localForward = Vector3.back,
                        radius = 0.28f,
                        conflictMode =
                            BistroBuilderSpatialConflictMode.Block
                    });
                globalSlot++;
            }

            contract.AddWorkEdgeForEditor(
                new BistroBuilderSpatialWorkEdgeDefinition
                {
                    edgeId = "edge." + station.stationId,
                    localStart =
                        first + Vector3.left * 0.3f,
                    localEnd =
                        last + Vector3.right * 0.3f,
                    serviceDepth = 0.58f,
                    conflictMode =
                        BistroBuilderSpatialConflictMode.Reservable
                });
        }

        EditorUtility.SetDirty(contract);
        return contract;
    }

    private static BistroBuilderSpatialContractDefinition
        BuildBarContract()
    {
        BistroBuilderSpatialContractDefinition contract =
            GetOrCreateContract("Bar_Service_Spot");
        contract.ConfigureForEditor(
            "spatial.contract.bar.service_spot",
            "work.bar",
            BistroBuilderAdaptiveSpatialProxyMode.Layered,
            new[]
            {
                "bar.operational",
                "service.bar",
                "transfer.bar",
                "work.edge"
            });
        contract.SetContractVersionForEditor(3);
        contract.ClearSemanticGeometryForEditor();
        contract.AddPortForEditor(
            new BistroBuilderSpatialPortDefinition
            {
                portId =
                    BistroBuilderBarSpatialAdapter.CustomerPortId,
                kind = BistroBuilderSpatialPortKind.SeatBay,
                radius = 0.26f,
                conflictMode =
                    BistroBuilderSpatialConflictMode.Block
            });
        contract.AddPortForEditor(
            new BistroBuilderSpatialPortDefinition
            {
                portId =
                    BistroBuilderBarSpatialAdapter.ServicePortId,
                kind = BistroBuilderSpatialPortKind.Service,
                localPosition = Vector3.back * 0.55f,
                localForward = Vector3.forward,
                radius = 0.3f,
                conflictMode =
                    BistroBuilderSpatialConflictMode.Reservable
            });
        contract.AddPortForEditor(
            new BistroBuilderSpatialPortDefinition
            {
                portId =
                    BistroBuilderBarSpatialAdapter.TransferPortId,
                kind = BistroBuilderSpatialPortKind.Transfer,
                localPosition = Vector3.back * 0.2f,
                localForward = Vector3.forward,
                radius = 0.24f,
                conflictMode =
                    BistroBuilderSpatialConflictMode.Degrade
            });
        contract.AddWorkEdgeForEditor(
            new BistroBuilderSpatialWorkEdgeDefinition
            {
                edgeId = "edge.bar.service",
                localStart = Vector3.left * 0.35f,
                localEnd = Vector3.right * 0.35f,
                serviceDepth = 0.55f,
                conflictMode =
                    BistroBuilderSpatialConflictMode.Reservable
            });
        EditorUtility.SetDirty(contract);
        return contract;
    }

    private static string ResolveKitchenSubjectId(
        KitchenSystem kitchen)
    {
        BistroBuilderSpatialSubject existing =
            kitchen.GetComponent<BistroBuilderSpatialSubject>();
        if (existing != null &&
            !string.IsNullOrWhiteSpace(existing.SubjectId))
            return existing.SubjectId;
        return "spatial.kitchen." + kitchen.KitchenId;
    }

    private static BistroBuilderSpatialContractDefinition
        GetOrCreateContract(string suffix)
    {
        string path = ContractFolder +
            "/BB_SpatialContract_" + suffix + ".asset";
        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderSpatialContractDefinition>(path);
        if (contract != null)
            return contract;
        contract = ScriptableObject.CreateInstance<
            BistroBuilderSpatialContractDefinition>();
        AssetDatabase.CreateAsset(contract, path);
        return contract;
    }

    private static T GetOrAdd<T>(
        GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null
            ? component
            : gameObject.AddComponent<T>();
    }

    private static void RemoveMissingScripts(
        GameObject gameObject)
    {
        if (gameObject != null &&
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                gameObject) > 0)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                gameObject);
    }

    private static void MarkSpatialDirty(
        GameObject gameObject)
    {
        if (gameObject == null)
            return;
        Component[] components =
            gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null)
                EditorUtility.SetDirty(components[i]);
        EditorUtility.SetDirty(gameObject);
    }
}
