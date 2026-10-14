using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Standalone Windows review: native game UI, direct V2 entry, no browser host.</summary>
public static class BistroBuilderEditorV2ReferenceR6Build
{
    public static void Build()
    {
        const string folder="Builds/Windows/EditorV2_Native_R6";
        Directory.CreateDirectory(folder);
        string originalName=PlayerSettings.productName;
        FullScreenMode originalMode=PlayerSettings.fullScreenMode;
        BuildReport result;
        try {
            PlayerSettings.productName="BistroBuilder Editor V2 R6";
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Scenes/Prototype_Restaurant.unity"},
                locationPathName=folder+"/BistroBuilder_EditorV2_R6.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
            });
        } finally {
            PlayerSettings.productName=originalName;
            PlayerSettings.fullScreenMode=originalMode;
        }
        var summary=result.summary;
        string report="BUILD="+summary.result+"\nERRORS="+summary.totalErrors+"\nWARNINGS="+summary.totalWarnings+
            "\nUNITY="+Application.unityVersion+"\nUTC="+DateTime.UtcNow.ToString("O");
        File.WriteAllText(folder+"/BUILD_REPORT.txt",report);
        if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(report);
        File.WriteAllText(folder+"/LEEME.txt","EDITOR V2 R6 · WINDOWS NATIVO\n\n"+
            "Abre BistroBuilder_EditorV2_R6.exe. Entra directamente en V2 a pantalla completa. F11 alterna ventana.\n"+
            "Paneles nativos Unity/TMP, iconos vectoriales SDF, filtros conectados al catálogo real.\n"+
            "Búsqueda, categorías, favoritos, recientes, precio, ubicación, orden y detalles funcionan sobre las definiciones publicadas.\n"+
            "Los productos y variantes disponibles siguen siendo los del proyecto; la maqueta incluye productos aún no publicados.\n\n"+report);
        File.WriteAllText(folder+"/ABRIR_EDITOR_V2_R6_PANTALLA_COMPLETA.cmd",
            "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0BistroBuilder_EditorV2_R6.exe\" -bb-editor-v2-fullscreen\r\n");
        File.WriteAllText(folder+"/ABRIR_EDITOR_V2_R6_VENTANA.cmd",
            "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0BistroBuilder_EditorV2_R6.exe\" -bb-editor-v2-windowed -screen-fullscreen 0 -screen-width 1672 -screen-height 941\r\n");
    }
}
