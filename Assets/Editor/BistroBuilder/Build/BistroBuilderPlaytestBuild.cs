using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BistroBuilderPlaytestBuild
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string RelativeOutput = "Builds/Windows/BistroBuilder_Edicion/BistroBuilder.exe";

    [MenuItem("Tools/Bistro Builder/Build/Windows Playtest", false, 50000)]
    public static void BuildWindowsPlaytestFromMenu()
    {
        BuildWindowsPlaytest();
    }

    public static void BuildWindowsPlaytestFromCommandLine()
    {
        BuildWindowsPlaytest();
    }

    private static void BuildWindowsPlaytest()
    {
        if (!File.Exists(ScenePath))
            throw new FileNotFoundException("No existe la escena canónica de playtest.", ScenePath);

        string output = Path.GetFullPath(RelativeOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Build Windows fallida: " + summary.result +
                ", errores=" + summary.totalErrors +
                ", warnings=" + summary.totalWarnings);
        }

        string infoPath = Path.Combine(
            Path.GetDirectoryName(output) ?? ".",
            "BUILD_INFO.txt");
        File.WriteAllText(infoPath,
            "Bistro Builder - Windows Playtest\n" +
            "Scene: " + ScenePath + "\n" +
            "Unity: " + Application.unityVersion + "\n" +
            "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
            "Size: " + summary.totalSize + " bytes\n");

        // Explicit arguments override the resolution mode remembered by a windowed playtest.
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output) ?? ".", "Jugar_Pantalla_Completa.cmd"),
            "@echo off\r\n" +
            "start \"Bistro Builder\" /D \"%~dp0\" \"%~dp0BistroBuilder.exe\" -screen-fullscreen 1 -window-mode borderless\r\n");

        Debug.Log(
            "BB_PLAYTEST_BUILD_PASS|" + output +
            "|SIZE=" + summary.totalSize +
            "|WARNINGS=" + summary.totalWarnings);
    }
}
