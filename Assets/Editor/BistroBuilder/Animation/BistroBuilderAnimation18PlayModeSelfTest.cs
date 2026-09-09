using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lanzador del probe Play Mode real de BB18.
/// El probe runtime finaliza el propio proceso con código 0/1.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderAnimation18PlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ReportPath =
        "Animation18PlayModeReport.txt";
    private const string TempScenePath =
        "Assets/Scenes/__BB18_PlayModeProbe__.unity";

    static BistroBuilderAnimation18PlayModeSelfTest()
    {
        EditorApplication.update -= PumpBatchPlayMode;
        EditorApplication.update += PumpBatchPlayMode;
    }

    private static void PumpBatchPlayMode()
    {
        if (!EditorApplication.isPlaying) return;
        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(active.path, TempScenePath, StringComparison.Ordinal))
            return;
        EditorApplication.QueuePlayerLoopUpdate();
    }

    [MenuItem("Bistro Builder/18 Animacion e interacciones/PlayMode real")]
    private static void RunFromMenu()
    {
        Begin();
    }

    public static void RunFromCommandLine()
    {
        Begin();
    }

    private static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El PlayMode BB18 ya esta ejecutandose.");

        string absoluteReport = Path.GetFullPath(ReportPath);
        if (File.Exists(absoluteReport))
            File.Delete(absoluteReport);

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        GameObject probeObject =
            new GameObject("__BB18_PlayModeProbe__");
        BistroBuilderAnimation18RuntimePlayProbe probe =
            probeObject.AddComponent<
                BistroBuilderAnimation18RuntimePlayProbe>();
        probe.ConfigureForEditor(true);

        if (File.Exists(Path.GetFullPath(TempScenePath)))
            AssetDatabase.DeleteAsset(TempScenePath);

        // El probe se serializa únicamente en una copia temporal para que
        // sobreviva al domain reload de Play Mode sin tocar la escena real.
        if (!EditorSceneManager.SaveScene(scene, TempScenePath, true))
            throw new InvalidOperationException(
                "No se pudo crear la escena temporal del PlayMode BB18.");
        EditorSceneManager.OpenScene(
            TempScenePath,
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
