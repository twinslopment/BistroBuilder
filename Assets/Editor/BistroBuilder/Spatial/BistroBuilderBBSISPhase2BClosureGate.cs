using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderBBSISPhase2BClosureGate
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ClosureReport =
        "BBSISPhase2BClosureReport.txt";

    private static readonly string[] EvidencePaths =
    {
        "BBSISPhase2BPlayModeReport.txt",
        "AdvancedKitchen12PlayModeReport.txt",
        "AdvancedKitchen12SaveLoadReport.txt",
        "Navigation17PlayModeReport.txt",
        "Navigation17SaveLoadReport.txt",
        "BBSISPhase2AClosureReport.txt"
    };

    private static readonly string[] EvidenceLabels =
    {
        "Play Mode real BBSIS 2B",
        "Regresión real Cocina avanzada 12",
        "Save/Load real Cocina avanzada 12",
        "Regresión real Navegación 17",
        "Save/Load real 17 + 368EF",
        "Regresión acumulativa BBSIS 2A"
    };

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2B/Closure gate")]
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
                "No pudo abrirse la escena BBSIS 2B.");

        BistroBuilderBBSISPhase2BValidator.Run();
        int validationPassed =
            BistroBuilderBBSISPhase2BValidator.LastPassed;
        int validationFailed =
            BistroBuilderBBSISPhase2BValidator.LastFailed;
        BistroBuilderBBSISPhase2BSelfTest.Run();
        int selfPassed =
            BistroBuilderBBSISPhase2BSelfTest.LastPassed;
        int selfFailed =
            BistroBuilderBBSISPhase2BSelfTest.LastFailed;

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
                "Closure gate BBSIS 2B fallido.");

        int accumulated =
            validationPassed +
            selfPassed +
            evidencePassed;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2B / " +
            "CLOSURE GATE ===\n" +
            "[PASS] Fase 2B cerrada con evidencia real.\n" +
            BistroBuilderBBSISPhase2BValidator.LastReport +
            "\n" +
            BistroBuilderBBSISPhase2BSelfTest.LastReport +
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
                "Falta evidencia PASS actual para " +
                label + ".");
        passed++;
    }
}
