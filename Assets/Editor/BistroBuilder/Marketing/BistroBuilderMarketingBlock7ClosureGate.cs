using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gate acumulativo de cierre del Bloque 7 — Marketing.
/// Encadena la validación estructural definitiva y todos los autotests puros.
/// Los PlayMode reales se ejecutan aparte para conservar fixtures aislados.
/// </summary>
public static class BistroBuilderMarketingBlock7ClosureGate
{
    [MenuItem("Tools/Bistro Builder/Marketing/Bloque 7 - Gate de cierre", false, 7270)]
    private static void RunFromMenu()
    {
        bool success = Run(out string report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — Bloque 7", report, "Aceptar");
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        if (!Run(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }
    public static bool Run(out string report)
    {
        var lines = new List<string>();
        int totalPassed = 0;
        int totalFailed = 0;

        Scene scene = SceneManager.GetActiveScene();
        bool sceneOk = scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path);
        Add(sceneOk, "Escena principal activa y guardada.",
            "No existe una escena principal activa válida.",
            lines, ref totalPassed, ref totalFailed);

        BistroBuilderMarketingPlayerUiValidationResult structure =
            BistroBuilderMarketingPlayerUiValidator.ValidateCurrentScene();
        Add(structure.Errors == 0,
            "Cadena estructural completa verde: UI + GuestRelations + " +
            "OperationalPressure + AverageTicket + TargetDemand + 7C.",
            "La cadena estructural acumulativa contiene " + structure.Errors +
            " error(es).",
            lines, ref totalPassed, ref totalFailed);

        RunSelfTest("7A Fundación", BistroBuilderMarketing7ASelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("7B Demanda", BistroBuilderMarketing7BSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("7C Persistencia", BistroBuilderMarketing7CSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("TargetDemand", BistroBuilderMarketingTargetDemandSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("AverageTicket", BistroBuilderMarketingAverageTicketSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("OperationalPressure",
            BistroBuilderMarketingOperationalPressureSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("GuestRelations",
            BistroBuilderMarketingGuestRelationsSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);
        RunSelfTest("UI jugable", BistroBuilderMarketingPlayerUiSelfTest.Run,
            lines, ref totalPassed, ref totalFailed);

        report =
            "=== BISTRO BUILDER — BLOQUE 7 / GATE DE CIERRE ===\n" +
            string.Join("\n", lines) +
            "\nResultado acumulado: " + totalPassed + " OK / " +
            totalFailed + " fallos.";
        return totalFailed == 0;
    }

    private delegate bool SelfTest(
        out int passed,
        out int failed,
        out string report);

    private static void RunSelfTest(
        string name,
        SelfTest test,
        List<string> lines,
        ref int totalPassed,
        ref int totalFailed)
    {
        bool success = test(out int passed, out int failed, out _);
        totalPassed += passed;
        totalFailed += failed;
        lines.Add((success ? "[OK] " : "[ERROR] ") + name +
            ": " + passed + " OK / " + failed + " fallos.");
    }

    private static void Add(
        bool condition,
        string ok,
        string fail,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + ok);
        }
        else
        {
            failed++;
            lines.Add("[ERROR] " + fail);
        }
    }
}
