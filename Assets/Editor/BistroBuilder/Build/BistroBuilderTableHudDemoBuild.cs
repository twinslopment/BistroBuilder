using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BistroBuilderTableHudDemoBuild
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    private const string RelativeOutput =
        "Builds/Windows/BistroBuilder_TableHudDemo/BistroBuilder_TableHudDemo.exe";

    public static void BuildFromCommandLine()
    {
        string output = Path.GetFullPath(RelativeOutput);
        string directory = Path.GetDirectoryName(output) ?? ".";
        Directory.CreateDirectory(directory);

        FullScreenMode previousMode = PlayerSettings.fullScreenMode;
        bool previousNative = PlayerSettings.defaultIsNativeResolution;

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

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Build demo fallida: " + report.summary.result);
            }

            string launcher = Path.Combine(
                directory,
                "Launch_TableHudDemo.cmd"
            );

            File.WriteAllText(
                launcher,
                "@echo off\r\n" +
                "start \"\" \"BistroBuilder_TableHudDemo.exe\" " +
                BistroBuilderTableHudDemoBootstrap.CommandLineFlag +
                " -screen-fullscreen 1\r\n"
            );

            File.WriteAllText(
                Path.Combine(directory, "DEMO_INFO.txt"),
                "Bistro Builder - Table HUD Demo\n" +
                "Incluye una mesa ocupada con clientes y comanda.\n" +
                "Haz un clic sobre la mesa ocupada para probar el HUD.\n" +
                "Flag: " +
                BistroBuilderTableHudDemoBootstrap.CommandLineFlag + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "UTC: " + DateTime.UtcNow.ToString("O") + "\n"
            );
            Debug.Log(
                "BB_TABLE_HUD_DEMO_BUILD_PASS|" +
                output +
                "|SIZE=" + report.summary.totalSize +
                "|WARNINGS=" + report.summary.totalWarnings
            );
        }
        finally
        {
            PlayerSettings.fullScreenMode = previousMode;
            PlayerSettings.defaultIsNativeResolution = previousNative;
        }
    }
}
