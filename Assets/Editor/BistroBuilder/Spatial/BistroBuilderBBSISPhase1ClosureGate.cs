using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderBBSISPhase1ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string BbsisPlayReport = "BBSISPhase1PlayModeReport.txt";
    private const string NavigationPlayReport = "Navigation17PlayModeReport.txt";
    private const string NavigationSaveLoadReport = "Navigation17SaveLoadReport.txt";
    private const string ClosureReport = "BBSISPhase1ClosureReport.txt";

    private static readonly string[] RuntimeEvidenceSources =
    {
        "Assets/Scripts/Application/Spatial/BistroBuilderSpatialInteractionService.cs",
        "Assets/Scripts/Application/Spatial/BistroBuilderAdaptiveSpatialProxy.cs",
        "Assets/Scripts/Application/Spatial/BistroBuilderSpatialSubject.cs",
        "Assets/Scripts/Domain/Spatial/BistroBuilderSpatialContractDefinition.cs",
        "Assets/Scripts/Domain/Spatial/BistroBuilderSpatialFamilyCatalog.cs",
        "Assets/Scripts/Application/Navigation/BistroBuilderNavigationService.cs",
        "Assets/Scripts/Application/Navigation/BistroBuilderDynamicCirculationEnvelope.cs"
    };

    [MenuItem("Bistro Builder/BBSIS/Fase 1/Closure gate")]
    private static void RunFromMenu()
    {
        try { Debug.Log(Run()); }
        catch (Exception exception) { Debug.LogError(exception.Message); }
    }

    public static void RunFromCommandLine()
    {
        try
        {
            string report = Run();
            File.WriteAllText(Path.GetFullPath(ClosureReport), report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.GetFullPath(ClosureReport),
                "=== BISTRO BUILDER - BBSIS FASE 1 / CLOSURE GATE ===\n[FAIL] " +
                exception.Message);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena canónica para BBSIS Fase 1.");

        BistroBuilderBBSISPhase1Validator.Run();
        int validationPassed = BistroBuilderBBSISPhase1Validator.LastPassed;
        int validationFailed = BistroBuilderBBSISPhase1Validator.LastFailed;

        BistroBuilderBBSISPhase1SelfTest.Run();
        int selfPassed = BistroBuilderBBSISPhase1SelfTest.LastPassed;
        int selfFailed = BistroBuilderBBSISPhase1SelfTest.LastFailed;

        DateTime evidenceFloor = GetRuntimeEvidenceFloorUtc();
        int reportPassed = 0;
        var reportChecks = new StringBuilder();
        CheckFreshPassReport(
            BbsisPlayReport, "Play Mode real BBSIS", evidenceFloor,
            ref reportPassed, reportChecks);
        CheckFreshPassReport(
            NavigationPlayReport, "Regresión real Navegación 17", evidenceFloor,
            ref reportPassed, reportChecks);
        CheckFreshPassReport(
            NavigationSaveLoadReport, "Save/Load real 17 + 368EF", evidenceFloor,
            ref reportPassed, reportChecks);

        if (validationFailed > 0 || selfFailed > 0 || reportPassed != 3)
            throw new InvalidOperationException("Closure gate BBSIS Fase 1 fallido.");

        int accumulated = validationPassed + selfPassed + reportPassed;
        return "=== BISTRO BUILDER - BBSIS FASE 1 / CLOSURE GATE ===\n" +
               "[PASS] Fase 1 instalada y validada.\n" +
               BistroBuilderBBSISPhase1Validator.LastReport + "\n" +
               BistroBuilderBBSISPhase1SelfTest.LastReport + "\n" +
               reportChecks +
               "Resultado acumulado: " + accumulated + " OK / 0 fallos.";
    }

    private static DateTime GetRuntimeEvidenceFloorUtc()
    {
        DateTime latest = DateTime.MinValue;
        for (int i = 0; i < RuntimeEvidenceSources.Length; i++)
        {
            string path = Path.GetFullPath(RuntimeEvidenceSources[i]);
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "Falta fuente runtime BBSIS: " + RuntimeEvidenceSources[i]);
            DateTime modified = File.GetLastWriteTimeUtc(path);
            if (modified > latest) latest = modified;
        }
        return latest;
    }

    private static void CheckFreshPassReport(
        string relativePath,
        string label,
        DateTime evidenceFloorUtc,
        ref int passed,
        StringBuilder report)
    {
        string path = Path.GetFullPath(relativePath);
        bool exists = File.Exists(path);
        bool pass = exists && File.ReadAllText(path).IndexOf(
            "[PASS]", StringComparison.Ordinal) >= 0;
        bool fresh = exists && File.GetLastWriteTimeUtc(path) >= evidenceFloorUtc;
        bool ok = pass && fresh;
        report.AppendLine((ok ? "OK - " : "FAIL - ") + label);
        if (!ok)
        {
            string reason = !exists ? "sin informe" :
                !pass ? "informe sin PASS" : "evidencia anterior al runtime actual";
            throw new InvalidOperationException(label + ": " + reason + ".");
        }
        passed++;
    }
}
