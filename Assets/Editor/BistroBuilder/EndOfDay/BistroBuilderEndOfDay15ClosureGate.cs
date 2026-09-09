using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderEndOfDay15ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/End Of Day/15 - Closure gate", false, 15004)]
    private static void RunFromMenu()
    {
        try { Debug.Log(Run()); }
        catch (Exception exception) { Debug.LogError(exception.Message); }
    }

    public static void RunFromCommandLine() => Debug.Log(Run());

    private static string Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("No pudo abrirse la escena canonica del Bloque 15.");

        BistroBuilderEndOfDay15ValidationResult validation =
            BistroBuilderEndOfDay15Validator.ValidateCurrentScene();
        bool selfOk = BistroBuilderEndOfDay15SelfTest.Run(
            out int passed, out int failed, out string selfReport);
        if (validation.Errors > 0 || !selfOk || failed > 0)
            throw new InvalidOperationException("Closure gate 15 fallido.\n" +
                validation.BuildReport() + "\n" + selfReport);

        return "=== BISTRO BUILDER - BLOQUE 15 / CLOSURE GATE ===\n" +
               validation.BuildReport() + "\n" + selfReport + "\n" +
               "Resultado acumulado: " + (validation.Passed + passed) + " OK / 0 fallos.";
    }
}
