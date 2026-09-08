using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye un player temporal de desarrollo para validar BB18
/// con el game loop real, fuera de las limitaciones de batch Play Mode.
/// </summary>
public static class BistroBuilderAnimation18StandaloneGate
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string TempScenePath =
        "Assets/Scenes/__BB18_StandaloneProbe__.unity";
    private const string BuildFolder =
        "Logs/BB18StandaloneRuntime";
    private const string ExecutableName =
        "BB18Probe.exe";

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Standalone runtime gate")]
    public static void Build()
    {
        string absoluteBuildFolder = Path.GetFullPath(BuildFolder);
        // Gate reproducible: nunca reutiliza un player parcial/incremental anterior.
        if (Directory.Exists(absoluteBuildFolder))
            Directory.Delete(absoluteBuildFolder, true);
        Directory.CreateDirectory(absoluteBuildFolder);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TempScenePath) != null)
            AssetDatabase.DeleteAsset(TempScenePath);

        // El runtime gate usa una escena mínima y determinista con las clases
        // reales de producción. La escena completa ya está cubierta por el
        // validador estructural; así aislamos el game loop de ruido ajeno.
        const string tempProfilePath = "Assets/__BB18_StandaloneSeatProfile.asset";
        if (AssetDatabase.LoadAssetAtPath<RestaurantSeatUseProfileDefinition>(tempProfilePath) != null)
            AssetDatabase.DeleteAsset(tempProfilePath);
        RestaurantSeatUseProfileDefinition seatProfile =
            ScriptableObject.CreateInstance<RestaurantSeatUseProfileDefinition>();
        seatProfile.name = "__BB18_StandaloneSeatProfile__";
        AssetDatabase.CreateAsset(seatProfile, tempProfilePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(tempProfilePath, ImportAssetOptions.ForceSynchronousImport);
        seatProfile = AssetDatabase.LoadAssetAtPath<RestaurantSeatUseProfileDefinition>(tempProfilePath);
        if (seatProfile == null)
            throw new InvalidOperationException("No se pudo persistir el SeatUseProfile temporal BB18.");

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        GameObject systems = new GameObject("GameSystems");
        systems.AddComponent<BistroBuilderInteractionPresentationService>();

        GameObject seatRoot = new GameObject("__BB18_TestSeat__");
        Transform associationPoint = new GameObject("AssociationPoint").transform;
        associationPoint.SetParent(seatRoot.transform, false);
        Transform motionRoot = new GameObject("OperationalMotionRoot").transform;
        motionRoot.SetParent(seatRoot.transform, false);
        Transform seatPoint = new GameObject("SeatPoint").transform;
        seatPoint.SetParent(motionRoot, false);
        Transform approachPoint = new GameObject("CustomerApproachPoint").transform;
        approachPoint.SetParent(seatRoot.transform, false);
        approachPoint.localPosition = new Vector3(0f, 0f, -0.35f);

        RestaurantSeat testSeat = seatRoot.AddComponent<RestaurantSeat>();
        testSeat.ConfigureForEditor(
            seatProfile,
            associationPoint,
            motionRoot,
            seatPoint,
            approachPoint);
        EditorUtility.SetDirty(testSeat);

        GameObject tableRoot = new GameObject("__BB18_TestTable__");
        RestaurantTableSeatingConfiguration tableConfiguration =
            tableRoot.AddComponent<RestaurantTableSeatingConfiguration>();
        testSeat.ApplyTopology(
            tableConfiguration,
            0,
            RestaurantSeatTopologyStatus.Associated,
            "BB18 standalone deterministic topology");

        GameObject probeObject = new GameObject("__BB18_StandaloneRuntimeProbe__");
        BistroBuilderAnimation18StandaloneRuntimeProbe probe =
            probeObject.AddComponent<BistroBuilderAnimation18StandaloneRuntimeProbe>();
        probe.ConfigureForEditor(true);

        if (!EditorSceneManager.SaveScene(scene, TempScenePath, true))
            throw new InvalidOperationException(
                "No se pudo crear la escena temporal standalone BB18.");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene(TempScenePath, OpenSceneMode.Single);
        RestaurantSeat persistedSeat = UnityEngine.Object.FindFirstObjectByType<RestaurantSeat>();
        if (persistedSeat == null || persistedSeat.UseProfile == null)
            throw new InvalidOperationException("El SeatUseProfile no sobrevivio al authoring del gate BB18.");
        if (!persistedSeat.ValidateConfiguration(out string seatError))
            throw new InvalidOperationException("Seat sintetico BB18 invalido: " + seatError);

        string executablePath = Path.Combine(
            absoluteBuildFolder,
            ExecutableName);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { TempScenePath },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Standalone BB18 fallo: " + summary.result +
                "; errores=" + summary.totalErrors +
                "; warnings=" + summary.totalWarnings + ".");
        }

        Debug.Log(
            "BB18_STANDALONE_BUILD_PASS|" + executablePath +
            "|SIZE=" + summary.totalSize);
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TempScenePath) != null)
                AssetDatabase.DeleteAsset(TempScenePath);
            const string tempProfilePath = "Assets/__BB18_StandaloneSeatProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<RestaurantSeatUseProfileDefinition>(tempProfilePath) != null)
                AssetDatabase.DeleteAsset(tempProfilePath);
            const string tempPlayScenePath = "Assets/Scenes/__BB18_PlayModeProbe__.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(tempPlayScenePath) != null)
                AssetDatabase.DeleteAsset(tempPlayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BB18_STANDALONE_CLEANUP_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
