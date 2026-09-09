using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderBBSISPhase2AClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string PlayReport = "BBSISPhase2APlayModeReport.txt";
    private const string NavigationReport = "Navigation17PlayModeReport.txt";
    private const string SaveLoadReport = "Navigation17SaveLoadReport.txt";
    private const string ClosureReport = "BBSISPhase2AClosureReport.txt";

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/Closure gate")]
    private static void RunFromMenu()
    {
        try { Debug.Log(Run()); }
        catch (Exception exception) { Debug.LogError(exception.Message); }
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
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("No pudo abrirse la escena canónica BBSIS 2A.");

        BistroBuilderBBSISPhase2AValidator.Run();
        int validationPassed = BistroBuilderBBSISPhase2AValidator.LastPassed;
        int validationFailed = BistroBuilderBBSISPhase2AValidator.LastFailed;
        BistroBuilderBBSISPhase2ASelfTest.Run();
        int selfPassed = BistroBuilderBBSISPhase2ASelfTest.LastPassed;
        int selfFailed = BistroBuilderBBSISPhase2ASelfTest.LastFailed;

        int reportPassed = 0;
        StringBuilder evidence = new StringBuilder();
        CheckReport(PlayReport, "Play Mode real BBSIS 2A", ref reportPassed, evidence);
        CheckReport(NavigationReport, "Regresión real Navegación 17", ref reportPassed, evidence);
        CheckReport(SaveLoadReport, "Save/Load real 17 + 368EF", ref reportPassed, evidence);

        if (validationFailed > 0 || selfFailed > 0 || reportPassed != 3)
            throw new InvalidOperationException("Closure gate BBSIS 2A fallido.");
        int accumulated = validationPassed + selfPassed + reportPassed;
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2A / CLOSURE GATE ===\n" +
            "[PASS] Fase 2A cerrada con evidencia real.\n" +
            BistroBuilderBBSISPhase2AValidator.LastReport + "\n" +
            BistroBuilderBBSISPhase2ASelfTest.LastReport + "\n" +
            evidence +
            "Resultado acumulado: " + accumulated + " OK / 0 fallos.";
        File.WriteAllText(Path.GetFullPath(ClosureReport), report);
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
                  File.ReadAllText(path).IndexOf("[PASS]", StringComparison.Ordinal) >= 0;
        report.AppendLine((ok ? "OK - " : "FAIL - ") + label);
        if (!ok)
            throw new InvalidOperationException("Falta evidencia PASS actual para " + label + ".");
        passed++;
    }
}
