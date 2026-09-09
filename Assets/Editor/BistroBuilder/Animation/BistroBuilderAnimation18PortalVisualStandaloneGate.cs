using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye una escena Development determinista para el cierre visual BB18.4.
/// </summary>
public static class BistroBuilderAnimation18PortalVisualStandaloneGate
{
    private const string ScenePath = "Assets/Scenes/__BB18_PortalVisualProbe__.unity";
    private const string BuildFolder = "Logs/BB18PortalRuntime";
    private const string HumanoidPath =
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
    private const string CatalogPath =
        "Assets/Data/Animation/BistroBuilderMotionCatalog.asset";
    private const string DoorContractPath =
        "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Door_Standard.asset";

    [MenuItem("Bistro Builder/18 Animacion e interacciones/BB18.4 Portal visual standalone gate")]
    public static void Build()
    {
        string absoluteBuildFolder = Path.GetFullPath(BuildFolder);
        if (Directory.Exists(absoluteBuildFolder))
            Directory.Delete(absoluteBuildFolder, true);
        Directory.CreateDirectory(absoluteBuildFolder);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            AssetDatabase.DeleteAsset(ScenePath);

        GameObject humanoid = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidPath);
        BistroBuilderMotionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(CatalogPath);
        BistroBuilderSpatialContractDefinition doorContract =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(DoorContractPath);
        if (humanoid == null || catalog == null || doorContract == null)
            throw new InvalidOperationException("Faltan assets para el fixture BB18.4.");

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        GameObject systems = new GameObject("GameSystems");
        systems.AddComponent<BistroBuilderSpatialInteractionService>();
        systems.AddComponent<BistroBuilderNavigationService>();
        BistroBuilderInteractionPresentationService interactions =
            systems.AddComponent<BistroBuilderInteractionPresentationService>();
        interactions.ConfigureForEditor(catalog);

        GameObject doorRoot = new GameObject("BB18_PortalDoor");
        doorRoot.transform.position = Vector3.zero;
        NavMeshObstacle obstacle = doorRoot.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0.45f, 1f, 0f);
        obstacle.size = new Vector3(0.9f, 2f, 0.12f);
        obstacle.carving = true;
        BistroBuilderDoorCirculationEnvelope envelope =
            doorRoot.AddComponent<BistroBuilderDoorCirculationEnvelope>();
        envelope.ConfigureSweepForAngle(90f);

        Transform hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(doorRoot.transform, false);
        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "DoorLeaf";
        leaf.transform.SetParent(hinge, false);
        leaf.transform.localPosition = new Vector3(0.45f, 1f, 0f);
        leaf.transform.localScale = new Vector3(0.9f, 2f, 0.10f);
        Collider leafCollider = leaf.GetComponent<Collider>();
        if (leafCollider != null) UnityEngine.Object.DestroyImmediate(leafCollider);

        Transform handleA = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        handleA.name = "Handle_A";
        handleA.SetParent(hinge, false);
        handleA.localPosition = new Vector3(0.78f, 1.05f, -0.10f);
        handleA.localScale = Vector3.one * 0.08f;
        Collider handleACollider = handleA.GetComponent<Collider>();
        if (handleACollider != null) UnityEngine.Object.DestroyImmediate(handleACollider);

        Transform handleB = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        handleB.name = "Handle_B";
        handleB.SetParent(hinge, false);
        handleB.localPosition = new Vector3(0.78f, 1.05f, 0.10f);
        handleB.localScale = Vector3.one * 0.08f;
        Collider handleBCollider = handleB.GetComponent<Collider>();
        if (handleBCollider != null) UnityEngine.Object.DestroyImmediate(handleBCollider);

        BistroBuilderNavigableDoor door =
            doorRoot.AddComponent<BistroBuilderNavigableDoor>();
        SerializedObject doorSerialized = new SerializedObject(door);
        doorSerialized.FindProperty("movingLeaf").objectReferenceValue = hinge;
        doorSerialized.FindProperty("startsOpen").boolValue = false;
        doorSerialized.FindProperty("openAngle").floatValue = 90f;
        doorSerialized.FindProperty("motionDuration").floatValue = 0.75f;
        doorSerialized.FindProperty("navMeshObstacle").objectReferenceValue = obstacle;
        doorSerialized.ApplyModifiedPropertiesWithoutUndo();

        doorContract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(DoorContractPath);
        if (doorContract == null)
            throw new InvalidOperationException("El Spatial Contract de puerta se perdiÃ³ al cambiar de escena.");
        if (!BistroBuilderSpatialBindingUtility.BindDoor(
                door,
                doorContract,
                "bb18.portal.fixture"))
            throw new InvalidOperationException("BBSIS no pudo vincular la puerta BB18.4.");

        Transform approachA = CreateFrame(
            "Approach_A", doorRoot.transform, new Vector3(0.72f, 0f, -0.72f), Vector3.forward);
        Transform approachB = CreateFrame(
            "Approach_B", doorRoot.transform, new Vector3(0.72f, 0f, 0.72f), Vector3.back);
        Transform throughA = CreateFrame(
            "Through_A", doorRoot.transform, new Vector3(0.45f, 0f, -1.25f), Vector3.forward);
        Transform throughB = CreateFrame(
            "Through_B", doorRoot.transform, new Vector3(0.45f, 0f, 1.25f), Vector3.back);

        BistroBuilderPortalAnimationDescriptor descriptor =
            doorRoot.AddComponent<BistroBuilderPortalAnimationDescriptor>();
        descriptor.ConfigureForEditor(
            door,
            approachA,
            approachB,
            throughA,
            throughB,
            handleA,
            handleB,
            null);
        if (!descriptor.ValidateConfiguration(out string descriptorError))
            throw new InvalidOperationException(descriptorError);

        CreateWall("Wall_Left", new Vector3(-1.30f, 1f, 0f),
            new Vector3(2.60f, 2f, 0.16f));
        CreateWall("Wall_Right", new Vector3(2.00f, 1f, 0f),
            new Vector3(2.20f, 2f, 0.16f));
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

        GameObject cameraObject = new GameObject("EvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(4.6f, 3.2f, -5.4f);
        cameraObject.transform.LookAt(new Vector3(0.45f, 1f, 0f));
        camera.fieldOfView = 46f;
        camera.clearFlags = CameraClearFlags.Skybox;

        GameObject lightObject = new GameObject("KeyLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        GameObject probeObject = new GameObject("__BB18_PortalVisualRuntimeProbe__");
        BistroBuilderAnimation18PortalVisualRuntimeProbe probe =
            probeObject.AddComponent<BistroBuilderAnimation18PortalVisualRuntimeProbe>();
        probe.ConfigureForEditor(true, humanoid, catalog, descriptor, camera);
        EditorUtility.SetDirty(probe);
        EditorUtility.SetDirty(descriptor);
        EditorUtility.SetDirty(interactions);
        EditorUtility.SetDirty(door);

        if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
            throw new InvalidOperationException("No se pudo guardar la escena BB18.4.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Gate de integridad: reabrir desde disco antes de construir evita arrastrar
        // una escena temporal que Unity no pueda deserializar limpiamente.
        Scene reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!reopened.IsValid() || !reopened.isLoaded)
            throw new InvalidOperationException("La escena BB18.4 no supera el reload gate.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string executablePath = Path.Combine(
            absoluteBuildFolder,
            "BB18PortalVisualProbe.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.CleanBuildCache
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                "BB18.4 standalone build fallÃƒÆ’Ã‚Â³: " + report.summary.result +
                "; errors=" + report.summary.totalErrors +
                "; warnings=" + report.summary.totalWarnings + ".");

        Debug.Log(
            "BB18_PORTAL_VISUAL_BUILD_PASS|" + executablePath +
            "|SIZE=" + report.summary.totalSize);
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            Build();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void CleanupFromCommandLine()
    {
        try
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BB18_PORTAL_VISUAL_CLEANUP_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Transform CreateFrame(
        string frameName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localForward)
    {
        Transform frame = new GameObject(frameName).transform;
        frame.SetParent(parent, false);
        frame.localPosition = localPosition;
        frame.localRotation = Quaternion.LookRotation(localForward, Vector3.up);
        return frame;
    }

    private static void CreateWall(
        string wallName,
        Vector3 position,
        Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }
}
