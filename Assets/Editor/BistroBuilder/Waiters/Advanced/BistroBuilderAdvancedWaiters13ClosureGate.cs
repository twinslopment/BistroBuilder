using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAdvancedWaiters13ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Waiters/13 - Closure gate", false, 13004)]
    private static void RunFromMenu()
    {
        try
        {
            string report = Run();
            Debug.Log(report);
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message);
        }
    }

    public static void RunFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);
    }

    private static string Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("No pudo abrirse la escena canonica del Bloque 13.");

        BistroBuilderAdvancedWaiters13ValidationResult validation =
            BistroBuilderAdvancedWaiters13Validator.ValidateCurrentScene();
        bool selfOk = BistroBuilderAdvancedWaiters13SelfTest.Run(
            out int passed,
            out int failed,
            out string selfReport
        );

        if (validation.Errors > 0 || !selfOk || failed > 0)
        {
            throw new InvalidOperationException(
                "Closure gate 13 fallido.\n" +
                validation.BuildReport() + "\n" + selfReport
            );
        }

        int accumulated = validation.Passed + passed;
        return "=== BISTRO BUILDER - BLOQUE 13 / CLOSURE GATE ===\n" +
               validation.BuildReport() + "\n" + selfReport + "\n" +
               "Resultado acumulado: " + accumulated + " OK / 0 fallos.";
    }
}
