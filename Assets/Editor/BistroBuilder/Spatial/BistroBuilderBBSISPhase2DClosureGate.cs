using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cierre acumulativo de BBSIS 2D: movilidad, carga y regresiones.
/// </summary>
public static class BistroBuilderBBSISPhase2DClosureGate
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ClosureReport =
        "BBSISPhase2DClosureReport.txt";

    private static readonly string[] EvidencePaths =
    {
        "BBSISPhase2DPlayModeReport.txt",
        "BBSISPhase2CClosureReport.txt",
        "Navigation17PlayModeReport.txt",
        "Navigation17SaveLoadReport.txt"
    };
    private static readonly string[] EvidenceLabels =
    {
        "Play Mode real BBSIS 2D",
        "Regresion acumulativa BBSIS 2C",
        "Regresion real Navegacion 17",
        "Save/Load real 17 + 368EF"
    };

    [MenuItem("Bistro Builder/BBSIS/Fase 2D/Closure gate")]
    private static void RunFromMenu()
    {
        try
        {
            Debug.Log(Run());
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message);
        }
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
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena BBSIS 2D.");

        BistroBuilderBBSISPhase2DValidator.Run();
        int validationPassed =
            BistroBuilderBBSISPhase2DValidator.LastPassed;
        int validationFailed =
            BistroBuilderBBSISPhase2DValidator.LastFailed;

        BistroBuilderBBSISPhase2DSelfTest.Run();
        int selfPassed =
            BistroBuilderBBSISPhase2DSelfTest.LastPassed;
        int selfFailed =
            BistroBuilderBBSISPhase2DSelfTest.LastFailed;

        int evidencePassed = 0;
        StringBuilder evidence = new StringBuilder();
        for (int i = 0; i < EvidencePaths.Length; i++)
            CheckReport(
                EvidencePaths[i],
                EvidenceLabels[i],
                ref evidencePassed,
                evidence);

        if (validationFailed > 0 ||
            selfFailed > 0 ||
            evidencePassed != EvidencePaths.Length)
            throw new InvalidOperationException(
                "Closure gate BBSIS 2D fallido.");

        int accumulated =
            validationPassed + selfPassed + evidencePassed;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2D / " +
            "CLOSURE GATE ===\n" +
            "[PASS] Fase 2D cerrada con evidencia real.\n" +
            BistroBuilderBBSISPhase2DValidator.LastReport +
            "\n" +
            BistroBuilderBBSISPhase2DSelfTest.LastReport +
            "\n" +
            evidence +
            "Resultado acumulado: " + accumulated +
            " OK / 0 fallos.";
        File.WriteAllText(
            Path.GetFullPath(ClosureReport),
            report);
        return report;
    }

    private static void CheckReport(
        string relativePath,
        string label,
        ref int passed,
        StringBuilder report)
    {
        string path = Path.GetFullPath(relativePath);
        bool ok = File.Exists(path) &&
            File.ReadAllText(path).IndexOf(
                "[PASS]",
                StringComparison.Ordinal) >= 0;
        report.AppendLine(
            (ok ? "OK - " : "FAIL - ") + label);
        if (!ok)
            throw new InvalidOperationException(
                "Falta evidencia PASS actual para " + label + ".");
        passed++;
    }
}
