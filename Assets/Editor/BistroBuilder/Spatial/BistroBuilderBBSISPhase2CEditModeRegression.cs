using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ejecuta la regresión real de la fábrica universal de colocables.
/// </summary>
public static class BistroBuilderBBSISPhase2CEditModeRegression
{
    private const string ReportPath =
        "BBSISPhase2CEditModeRegressionReport.txt";

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2C/Regresión fábrica colocables")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Debug.Log(Run());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
    private static string Run()
    {
        BistroBuilderPlaceablePipelineSelfTestResult result =
            BistroBuilderPlaceablePipelineSelfTest.Run();
        bool ok = result != null &&
                  result.Succeeded &&
                  result.CleanupSucceeded &&
                  result.FailedChecks.Count == 0;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2C / " +
            "REGRESIÓN MODO EDICIÓN ===\n" +
            (ok ? "[PASS] " : "[FAIL] ") +
            (result != null
                ? result.BuildSummary()
                : "El autotest no devolvió resultado.");
        File.WriteAllText(
            Path.GetFullPath(ReportPath),
            report);
        if (!ok)
            throw new InvalidOperationException(report);
        return report;
    }
}
