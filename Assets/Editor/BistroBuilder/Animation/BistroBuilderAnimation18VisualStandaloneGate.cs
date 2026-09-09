using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAnimation18VisualStandaloneGate
{
    private const string ScenePath = "Assets/Scenes/__BB18_VisualProbe__.unity";
    private const string BuildFolder = "Logs/BB18VisualRuntime";
    private const string HumanoidPath =
        "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
    private const string CatalogPath =
        "Assets/Data/Animation/BistroBuilderMotionCatalog.asset";
    private const string ChairPath =
        "Assets/Art/Blender/Placeables/Furniture/Chairs/BB_Chair_Master_001/Prefabs/PF_BB_Chair_Master_001_Functional_Olive.prefab";

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Visual Humanoid standalone gate")]
    public static void Build()
    {
        string absoluteBuildFolder = Path.GetFullPath(BuildFolder);
        if (Directory.Exists(absoluteBuildFolder)) Directory.Delete(absoluteBuildFolder, true);
        Directory.CreateDirectory(absoluteBuildFolder);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            AssetDatabase.DeleteAsset(ScenePath);
        GameObject humanoid = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidPath);
        BistroBuilderMotionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(CatalogPath);
        GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairPath);
        if (humanoid == null || catalog == null || chairPrefab == null)
            throw new InvalidOperationException("Faltan assets del fixture visual BB18.");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject systems = new GameObject("GameSystems");
        systems.AddComponent<BistroBuilderNavigationService>();
        BistroBuilderInteractionPresentationService interactions =
            systems.AddComponent<BistroBuilderInteractionPresentationService>();
        interactions.ConfigureForEditor(catalog);

        GameObject chair = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab, scene);
        chair.name = "BB18_RealChair";
        chair.transform.position = Vector3.zero;
        RestaurantSeat seat = chair.GetComponent<RestaurantSeat>();
        if (seat == null) throw new InvalidOperationException("La silla real no contiene RestaurantSeat.");

        GameObject table = new GameObject("BB18_TopologyTable");
        RestaurantTableSeatingConfiguration tableConfig =
            table.AddComponent<RestaurantTableSeatingConfiguration>();
        seat.ApplyTopology(tableConfig, 0, RestaurantSeatTopologyStatus.Associated,
            "BB18 visual fixture");

        Transform startPoint = new GameObject("StartPoint").transform;
        startPoint.position = new Vector3(0f, 0f, -3.0f);
        startPoint.rotation = Quaternion.identity;

        Transform transferApproach = new GameObject("TransferApproach").transform;
        transferApproach.position = new Vector3(2.0f, 0f, -0.18f);
        transferApproach.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        Transform sourceSocket = new GameObject("SourceSocket").transform;
        sourceSocket.position = new Vector3(2.0f, 0.82f, 0.05f);
        GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prop.name = "BB18_PlateProp";
        prop.transform.SetParent(sourceSocket, false);
        prop.transform.localScale = new Vector3(0.28f, 0.04f, 0.28f);
        BistroBuilderTransferableVisual transferable =
            prop.AddComponent<BistroBuilderTransferableVisual>();
        Transform plateGrip = new GameObject("RightGrip").transform;
        plateGrip.SetParent(prop.transform, false);
        plateGrip.localPosition = new Vector3(0.25f, 0f, -0.05f);
        BistroBuilderCarryableDescriptor plateDescriptor =
            prop.AddComponent<BistroBuilderCarryableDescriptor>();
        plateDescriptor.ConfigureForEditor(
            BistroBuilderCarryMode.Plate, plateGrip, null, Vector3.zero, Vector3.zero);

        Transform placeApproach = new GameObject("PlaceApproach").transform;
        placeApproach.position = new Vector3(1.25f, 0f, 0.40f);
        placeApproach.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        Transform placeSocket = new GameObject("PlaceSocket").transform;
        placeSocket.position = new Vector3(1.25f, 0.82f, 0.20f);

        Transform boxApproach = new GameObject("BoxApproach").transform;
        boxApproach.position = new Vector3(2.85f, 0f, -0.18f);
        boxApproach.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Transform boxSourceSocket = new GameObject("BoxSourceSocket").transform;
        boxSourceSocket.position = new Vector3(2.85f, 0.88f, 0.08f);
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "BB18_BoxProp";
        box.transform.SetParent(boxSourceSocket, false);
        box.transform.localScale = new Vector3(0.50f, 0.28f, 0.34f);
        BistroBuilderTransferableVisual boxTransferable =
            box.AddComponent<BistroBuilderTransferableVisual>();
        Transform boxRightGrip = new GameObject("RightGrip").transform;
        boxRightGrip.SetParent(box.transform, false);
        boxRightGrip.localPosition = new Vector3(0.35f, 0f, -0.12f);
        Transform boxLeftGrip = new GameObject("LeftGrip").transform;
        boxLeftGrip.SetParent(box.transform, false);
        boxLeftGrip.localPosition = new Vector3(-0.35f, 0f, -0.12f);
        BistroBuilderCarryableDescriptor boxDescriptor =
            box.AddComponent<BistroBuilderCarryableDescriptor>();
        boxDescriptor.ConfigureForEditor(
            BistroBuilderCarryMode.BoxTwoHand, boxRightGrip, boxLeftGrip,
            Vector3.zero, Vector3.zero);
        GameObject cameraObject = new GameObject("EvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(4.2f, 3.1f, -5.2f);
        cameraObject.transform.LookAt(new Vector3(0.7f, 0.85f, -0.5f));
        camera.fieldOfView = 48f;
        camera.clearFlags = CameraClearFlags.Skybox;

        GameObject lightObject = new GameObject("KeyLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);

        GameObject probeObject = new GameObject("__BB18_VisualRuntimeProbe__");
        BistroBuilderAnimation18VisualRuntimeProbe probe =
            probeObject.AddComponent<BistroBuilderAnimation18VisualRuntimeProbe>();
        probe.ConfigureForEditor(
            true,
            humanoid,
            catalog,
            seat,
            startPoint,
            transferApproach,
            sourceSocket,
            transferable,
            placeApproach,
            placeSocket,
            boxApproach,
            boxSourceSocket,
            boxTransferable,
            camera);

        EditorUtility.SetDirty(probe);
        EditorUtility.SetDirty(interactions);
        EditorUtility.SetDirty(seat);
        if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
            throw new InvalidOperationException("No se pudo guardar la escena visual BB18.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string executablePath = Path.Combine(absoluteBuildFolder, "BB18VisualProbe.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                "Visual standalone build falló: " + report.summary.result +
                "; errors=" + report.summary.totalErrors +
                "; warnings=" + report.summary.totalWarnings + ".");

        Debug.Log("BB18_VISUAL_STANDALONE_BUILD_PASS|" + executablePath +
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
            Debug.Log("BB18_VISUAL_STANDALONE_CLEANUP_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
