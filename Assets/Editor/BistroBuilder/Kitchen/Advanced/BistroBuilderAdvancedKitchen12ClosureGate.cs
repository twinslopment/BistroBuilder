using System;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderAdvancedKitchen12ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BistroBuilderAdvancedKitchen12ValidationResult validation =
            BistroBuilderAdvancedKitchen12Validator.ValidateCurrentScene();
        bool selfOk = BistroBuilderAdvancedKitchen12SelfTest.Run(
            out int selfPassed,
            out int selfFailed,
            out string selfReport);

        string report = "=== BISTRO BUILDER — BLOQUE 12 / CLOSURE GATE ===\n" +
            validation.BuildReport() + "\n" + selfReport +
            "\nResultado acumulado: " +
            (validation.Passed + selfPassed) + " OK / " +
            (validation.Errors + selfFailed) + " fallos.";
        if (validation.Errors != 0 || !selfOk)
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }
}
