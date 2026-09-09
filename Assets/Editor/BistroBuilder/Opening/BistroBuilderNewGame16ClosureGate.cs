using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderNewGame16ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Opening/16 - Closure Gate", false, 16004)]
    private static void RunFromMenu()
    {
        bool ok = Run(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder - Closure 16", report, "Aceptar");
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!Run(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out string report)
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BistroBuilderNewGame16ValidationResult validation =
            BistroBuilderNewGame16Validator.ValidateCurrentScene();
        bool selfOk = BistroBuilderNewGame16SelfTest.Run(
            out int passed, out int failed, out string selfReport);
        int totalOk = validation.Passed + passed;
        int totalFail = validation.Errors + failed;
        report = validation.BuildReport() + "\n" + selfReport +
                 "\nResultado acumulado: " + totalOk + " OK / " + totalFail + " fallos.";
        return validation.Errors == 0 && selfOk && failed == 0;
    }
}
