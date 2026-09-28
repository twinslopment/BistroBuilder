using System;
using System.IO;
using UnityEditor;

/// <summary>
/// Ejecuta las dos regresiones independientes del Universal Preview V4.
/// La suite no mezcla mobiliario y construcción dentro de la misma sesión:
/// cada bloque abre la escena canónica desde cero.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderUniversalPreviewRegressionSuite
{
    private const string Armed =
        "BB.UniversalPreviewV4.Suite";

    private const string Phase =
        Armed + ".Phase";

    private const string FurniturePass =
        "BB.UniversalPreviewV3.Furniture.Pass";

    private const string ConstructionPass =
        "BB.UniversalPreviewV3.Construction.Pass";

    static BistroBuilderUniversalPreviewRegressionSuite()
    {
        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Universal Preview V4/Run All",
        false,
        52059)]
    public static void RunAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "BB Universal Preview V4",
                "Sal de Play Mode antes de iniciar la suite.",
                "Aceptar");

            return;
        }

        SessionState.SetBool(
            Armed,
            true);

        SessionState.SetInt(
            Phase,
            1);

        SessionState.SetBool(
            FurniturePass,
            false);

        SessionState.SetBool(
            ConstructionPass,
            false);

        Directory.CreateDirectory(
            "Logs");

        BistroBuilderUniversalPreviewFurnitureRegression
            .Run();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Armed, false) ||
            state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        /*
         * Se difiere la evaluación un editor tick para asegurar que
         * la regresión que acaba de terminar haya persistido primero
         * su SessionState PASS/FAIL.
         */
        EditorApplication.delayCall -=
            ContinueSuite;

        EditorApplication.delayCall +=
            ContinueSuite;
    }

    private static void ContinueSuite()
    {
        if (!SessionState.GetBool(Armed, false))
            return;

        int phase =
            SessionState.GetInt(
                Phase,
                0);

        if (phase == 1)
        {
            if (!SessionState.GetBool(
                    FurniturePass,
                    false))
            {
                Finish(
                    false,
                    "Furniture regression FAILED. " +
                    "Revisa Logs/UniversalPreviewFurnitureRegression.txt");

                return;
            }

            SessionState.SetInt(
                Phase,
                2);

            BistroBuilderUniversalPreviewConstructionRegression
                .Run();

            return;
        }

        if (phase == 2)
        {
            if (!SessionState.GetBool(
                    ConstructionPass,
                    false))
            {
                Finish(
                    false,
                    "Construction regression FAILED. " +
                    "Revisa Logs/UniversalPreviewConstructionRegression.txt");

                return;
            }

            Finish(
                true,
                "Furniture PASS + Construction PASS");

            return;
        }

        Finish(
            false,
            "Estado interno de suite inválido.");
    }

    private static void Finish(
        bool passed,
        string message)
    {
        SessionState.SetBool(
            Armed,
            false);

        SessionState.SetInt(
            Phase,
            0);

        string report =
            (passed ? "PASS " : "FAIL ") +
            message;

        Directory.CreateDirectory(
            "Logs");

        File.WriteAllText(
            "Logs/UniversalPreviewRegressionSuite.txt",
            report);

        UnityEngine.Debug.Log(
            "BB_UNIVERSAL_PREVIEW_SUITE_" +
            report);

        EditorUtility.DisplayDialog(
            "BB Universal Preview V4",
            report,
            "Aceptar");
    }
}
