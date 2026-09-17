using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BistroBuilderPlaytestBuild
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string RelativeOutput = "Builds/Windows/BistroBuilder_Playtest/BistroBuilder.exe";
    private const string FullscreenLauncherName = "Launch_Fullscreen.cmd";

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
        string outputDirectory = Path.GetDirectoryName(output) ?? ".";
        Directory.CreateDirectory(outputDirectory);

        FullScreenMode previousFullscreenMode = PlayerSettings.fullScreenMode;
        bool previousNativeResolution = PlayerSettings.defaultIsNativeResolution;

        try
        {
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;

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

            WriteBuildInfo(outputDirectory, summary);
            WriteFullscreenLauncher(outputDirectory);

            Debug.Log(
                "BB_PLAYTEST_BUILD_PASS|" + output +
                "|SIZE=" + summary.totalSize +
                "|WARNINGS=" + summary.totalWarnings +
                "|FULLSCREEN=FullScreenWindow|NATIVE_RESOLUTION=true");
        }
        finally
        {
            PlayerSettings.fullScreenMode = previousFullscreenMode;
            PlayerSettings.defaultIsNativeResolution = previousNativeResolution;
        }
    }

    private static void WriteBuildInfo(string outputDirectory, BuildSummary summary)
    {
        string infoPath = Path.Combine(outputDirectory, "BUILD_INFO.txt");
        File.WriteAllText(infoPath,
            "Bistro Builder - Windows Playtest\n" +
            "Scene: " + ScenePath + "\n" +
            "Unity: " + Application.unityVersion + "\n" +
            "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
            "Size: " + summary.totalSize + " bytes\n" +
            "Display: FullScreenWindow\n" +
            "Native resolution: true\n" +
            "Launcher: " + FullscreenLauncherName + "\n");
    }

    private static void WriteFullscreenLauncher(string outputDirectory)
    {
        string launcherPath = Path.Combine(outputDirectory, FullscreenLauncherName);
        File.WriteAllText(launcherPath,
            "@echo off\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            "\"%~dp0BistroBuilder.exe\" -screen-fullscreen 1\r\n");
    }
}
