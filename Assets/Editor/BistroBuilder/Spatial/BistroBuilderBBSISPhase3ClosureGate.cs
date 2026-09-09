using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cierre acumulativo BBSIS Fase 3: hardening y regresiones reales.
/// </summary>
public static class BistroBuilderBBSISPhase3ClosureGate
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ClosureReport =
        "BBSISPhase3ClosureReport.txt";

    private static readonly string[] EvidencePaths =
    {
        "BBSISPhase3PlayModeReport.txt",
        "BBSISPhase2DPlayModeReport.txt",
        "BBSISPhase2CClosureReport.txt",
        "Navigation17PlayModeReport.txt",
        "Navigation17SaveLoadReport.txt"
    };
    private static readonly string[] EvidenceLabels =
    {
        "Play Mode real BBSIS Fase 3",
        "Regresion BBSIS Fase 2D",
        "Regresion acumulativa BBSIS Fase 2C",
        "Regresion real Navegacion 17",
        "Save/Load real 17 + 368EF"
    };

    [MenuItem("Bistro Builder/BBSIS/Fase 3/Closure gate")]
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
                "No pudo abrirse la escena BBSIS Fase 3.");

        BistroBuilderBBSISPhase3Validator.Run();
        int validationPassed =
            BistroBuilderBBSISPhase3Validator.LastPassed;
        int validationFailed =
            BistroBuilderBBSISPhase3Validator.LastFailed;

        BistroBuilderBBSISPhase3SelfTest.Run();
        int selfPassed =
            BistroBuilderBBSISPhase3SelfTest.LastPassed;
        int selfFailed =
            BistroBuilderBBSISPhase3SelfTest.LastFailed;

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
                "Closure gate BBSIS Fase 3 fallido.");

        int accumulated =
            validationPassed + selfPassed + evidencePassed;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 3 / CLOSURE GATE ===\n" +
            "[PASS] Fase 3 cerrada con evidencia real.\n" +
            BistroBuilderBBSISPhase3Validator.LastReport +
            "\n" +
            BistroBuilderBBSISPhase3SelfTest.LastReport +
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
