using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate acumulativo no destructivo del Bloque 9.
/// Revalida 9A-9E sin reescribir la escena y deja 9F para Play Mode real.
/// </summary>
public static class BistroBuilderProgressionBlock9ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Progression/Bloque 9 - Gate acumulativo", false, 9090)]
    private static void RunFromMenu()
    {
        try { Debug.Log(Run()); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static void RunFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);
    }

    private static string Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int passed = 0;
        int failed = 0;

        var v9A = BistroBuilderProgression9AValidator.ValidateCurrentScene();
        var v9B = BistroBuilderProgression9BValidator.ValidateCurrentScene();
        var v9C = BistroBuilderProgression9CValidator.ValidateCurrentScene();
        var v9D = BistroBuilderProgression9DValidator.ValidateCurrentScene();
        var v9E = BistroBuilderProgression9EValidator.ValidateCurrentScene();
        AccumulateValidation("9A", v9A.Passed, v9A.Errors, ref passed, ref failed);
        AccumulateValidation("9B", v9B.Passed, v9B.Errors, ref passed, ref failed);
        AccumulateValidation("9C", v9C.Passed, v9C.Errors, ref passed, ref failed);
        AccumulateValidation("9D", v9D.Passed, v9D.Errors, ref passed, ref failed);
        AccumulateValidation("9E", v9E.Passed, v9E.Errors, ref passed, ref failed);

        RunSelfTest("9A", BistroBuilderProgression9ASelfTest.Run, ref passed, ref failed);
        RunSelfTest("9B", BistroBuilderProgression9BSelfTest.Run, ref passed, ref failed);
        RunSelfTest("9C", BistroBuilderProgression9CSelfTest.Run, ref passed, ref failed);
        RunSelfTest("9D", BistroBuilderProgression9DSelfTest.Run, ref passed, ref failed);
        RunSelfTest("9E", BistroBuilderProgression9ESelfTest.Run, ref passed, ref failed);

        if (failed > 0)
            throw new InvalidOperationException("Bloque 9 no supera el gate acumulativo: " +
                passed + " OK / " + failed + " fallos.");
        return "=== BISTRO BUILDER — BLOQUE 9 / GATE ACUMULATIVO ===\n" +
            "Resultado: " + passed + " OK / 0 fallos.\n" +
            "9A-9E estructurales y puros permanecen verdes.";
    }

    private delegate bool SelfTestRunner(out int passed, out int failed, out string report);

    private static void RunSelfTest(string id, SelfTestRunner runner,
        ref int totalPassed, ref int totalFailed)
    {
        bool ok = runner(out int passed, out int failed, out string report);
        Debug.Log(report);
        totalPassed += passed;
        totalFailed += failed;
        if (!ok && failed == 0) totalFailed++;
        Debug.Log(id + " autotest acumulado: " + passed + " OK / " + failed + " fallos.");
    }

    private static void AccumulateValidation(string id, int passed, int errors,
        ref int totalPassed, ref int totalFailed)
    {
        totalPassed += passed;
        totalFailed += errors;
        Debug.Log(id + " validación acumulada: " + passed + " OK / " + errors + " errores.");
    }
}