using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderBBSISPhase2CClosureGate
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ClosureReport =
        "BBSISPhase2CClosureReport.txt";

    private static readonly string[] EvidencePaths =
    {
        "BBSISPhase2CPlayModeReport.txt",
        "BBSISPhase2CEditModeRegressionReport.txt",
        "Navigation17PlayModeReport.txt",
        "Navigation17SaveLoadReport.txt",
        "BBSISPhase2BClosureReport.txt"
    };

    private static readonly string[] EvidenceLabels =
    {
        "Play Mode real BBSIS 2C",
        "Regresion real Modo Edicion",
        "Regresion real Navegacion 17",
        "Save/Load real 17 + 368EF",        "Regresion acumulativa BBSIS 2B"
    };

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2C/Closure gate")]
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
            ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena BBSIS 2C.");

        BistroBuilderBBSISPhase2CValidator.Run();
        int validationPassed =
            BistroBuilderBBSISPhase2CValidator.LastPassed;
        int validationFailed =
            BistroBuilderBBSISPhase2CValidator.LastFailed;
        BistroBuilderBBSISPhase2CSelfTest.Run();
        int selfPassed =
            BistroBuilderBBSISPhase2CSelfTest.LastPassed;
        int selfFailed =
            BistroBuilderBBSISPhase2CSelfTest.LastFailed;

        int evidencePassed = 0;
        StringBuilder evidence = new StringBuilder();
        for (int i = 0; i < EvidencePaths.Length; i++)
            CheckReport(
                EvidencePaths[i],
                EvidenceLabels[i],
                ref evidencePassed,
                evidence);

        if (validationFailed > 0 ||            selfFailed > 0 ||
            evidencePassed != EvidencePaths.Length)
            throw new InvalidOperationException(
                "Closure gate BBSIS 2C fallido.");

        int accumulated =
            validationPassed +
            selfPassed +
            evidencePassed;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2C / " +
            "CLOSURE GATE ===\n" +
            "[PASS] Fase 2C cerrada con evidencia real.\n" +
            BistroBuilderBBSISPhase2CValidator.LastReport +
            "\n" +
            BistroBuilderBBSISPhase2CSelfTest.LastReport +
            "\n" +
            evidence +
            "Resultado acumulado: " +
            accumulated + " OK / 0 fallos.";
        File.WriteAllText(
            Path.GetFullPath(ClosureReport), report);
        return report;
    }

    private static void CheckReport(
        string relativePath,
        string label,        ref int passed,
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
                "Falta evidencia PASS actual para " +
                label + ".");
        passed++;
    }
}
