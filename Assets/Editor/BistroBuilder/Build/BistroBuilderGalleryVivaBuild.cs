using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BistroBuilderGalleryVivaBuild
{
    public static void Build()
    {
        const string folder = "Builds/Windows/EditorV2_GaleriaViva";
        Directory.CreateDirectory(folder);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Prototype_Restaurant.unity" },
            locationPathName = folder + "/BistroBuilder_EditorV2_GaleriaViva.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        var summary = result.summary;
        string report = "BUILD=" + summary.result + "\nERRORS=" + summary.totalErrors +
            "\nWARNINGS=" + summary.totalWarnings + "\nUNITY=" + Application.unityVersion + "\nUTC=" + DateTime.UtcNow.ToString("O");
        File.WriteAllText(folder + "/BUILD_REPORT.txt", report);
        if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report);
        File.WriteAllText(folder + "/LEEME.txt", "EDITOR V2 · GALERÍA VIVA NATIVA\n\n" +
            "Revisión R3 del 11/10/2026: composición V2, sin paneles superpuestos ni interfaz antigua visible.\n" +
            "Entra en Edición y abre Colocar. Usa Buscar, Favoritos, Recientes, filtros, orden y categorías.\n" +
            "Las tarjetas presentan los artículos y precios reales publicados; las ocho mesas de la maqueta no forman parte del catálogo actual.\n" +
            "Con selección de mobiliario, una tarjeta prepara la sustitución B10. Sin selección inicia la colocación provisional.\n" +
            "No se ha integrado esta revisión en la rama principal.\n\n" + report);
        File.WriteAllText(folder + "/ABRIR_EDITOR_V2_R3_PANTALLA_COMPLETA.cmd",
            "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0BistroBuilder_EditorV2_GaleriaViva.exe\" -bb-gallery-viva-review -bb-editor-v2-fullscreen -screen-fullscreen 1 -window-mode borderless\r\n");
        foreach (int width in new[] { 1280, 1672, 1920, 3840 })
            File.WriteAllText(folder + "/ABRIR_GALERIA_VIVA_" + width + ".cmd",
                "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0BistroBuilder_EditorV2_GaleriaViva.exe\" -bb-gallery-viva-review -screen-fullscreen 0 -screen-width " + width + " -screen-height " + (width == 1280 ? 720 : width == 1672 ? 941 : width == 3840 ? 2160 : 1080) + "\r\n");
    }
}
