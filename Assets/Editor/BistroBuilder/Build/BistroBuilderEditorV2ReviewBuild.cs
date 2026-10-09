using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Isolated one-scene Windows review build of Editor V2, not a release build.</summary>
public static class BistroBuilderEditorV2ReviewBuild
{
    private const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string Folder = "Builds/Windows/EditorV2_VisibleFix_20261009";
    private const string Exe = "BistroBuilder_EditorV2_VISIBLE.exe";
    private const string ReportFile = "EditorV2_VisibleFix_Build_Report.txt";

    [MenuItem("Bistro Builder/QA/Editor V2/Windows Review Build")]
    public static void BuildFromMenu() => Build();

    public static void BuildFromCommandLine() => Build();

    private static void Build()
    {
        string project = Path.GetFullPath(".");
        string destination = Path.Combine(project, Folder);
        string executable = Path.Combine(destination, Exe);
        Directory.CreateDirectory(destination);
        string reportPath = Path.Combine(project, ReportFile);

        if (!File.Exists(Path.Combine(project, Scene)))
            throw new FileNotFoundException("Falta la escena de pruebas", Scene);
        var originalMode = PlayerSettings.fullScreenMode;
        bool originalNative = PlayerSettings.defaultIsNativeResolution;
        int originalWidth = PlayerSettings.defaultScreenWidth;
        int originalHeight = PlayerSettings.defaultScreenHeight;

        try
        {
            // A small window is appropriate for UX evaluation at 1280x720.
            // Players can switch to 1920x1080 for the large-screen comparison.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string result = "BUILD_RESULT=" + summary.result + "\n" +
                "SCENE=" + Scene + "\nUNITY=" + Application.unityVersion +
                "\nTARGET=StandaloneWindows64\nOUTPUT=" + executable +
                "\nTOTAL_BYTES=" + summary.totalSize +
                "\nERRORS=" + summary.totalErrors +
                "\nWARNINGS=" + summary.totalWarnings +
                "\nUTC=" + DateTime.UtcNow.ToString("O") + "\n";
            File.WriteAllText(reportPath, result);
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(result);

            File.WriteAllText(Path.Combine(destination, "LEEME_EDITOR_V2.txt"),
                "BISTRO BUILDER - EDITOR V2 - BUILD CORREGIDA (VISIBILIDAD)\n" +
                "Esta build CORRIGE la superposicion del modo edicion antiguo. NO es una version final.\n\n" +
                "1. Ejecuta " + Exe + ".\n" +
                "2. Entra en Modo Edicion desde la interfaz del juego.\n" +
                "3. Abre Colocar para revisar Galeria Viva, Destacado y Relacionados.\n" +
                "4. Selecciona muebles para revisar el inspector y Principal/Conjunto.\n" +
                "5. Comprueba la interfaz a 1280x720 y 1920x1080.\n" +
                "Snapping, Vistas y Halo de Huella definitivo aun no estan cerrados.\n" +
                "Aplica/Descarta con cuidado: solo son pruebas de este proyecto.\n" +
                "Esta carpeta debe permanecer completa junto al EXE.\n\n" +
                result);
            File.WriteAllText(Path.Combine(destination, "ARRANCAR_1280x720.cmd"),
                "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0" + Exe +
                "\" -screen-width 1280 -screen-height 720 -screen-fullscreen 0\r\n");
            File.WriteAllText(Path.Combine(destination, "ARRANCAR_1920x1080.cmd"),
                "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0" + Exe +
                "\" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0\r\n");

            Debug.Log("[EditorV2 REVIEW BUILD] PASS\n" + result);
        }
        catch (Exception ex)
        {
            File.WriteAllText(reportPath,
                "BUILD_RESULT=FAILED\n" + ex + "\n");
            Debug.LogError("[EditorV2 REVIEW BUILD] FAIL " + ex);
            throw;
        }
        finally
        {
            PlayerSettings.fullScreenMode = originalMode;
            PlayerSettings.defaultIsNativeResolution = originalNative;
            PlayerSettings.defaultScreenWidth = originalWidth;
            PlayerSettings.defaultScreenHeight = originalHeight;
        }
    }
}
