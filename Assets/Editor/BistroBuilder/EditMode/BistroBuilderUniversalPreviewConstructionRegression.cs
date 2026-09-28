using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Regresión determinista del preview universal de construcción.
/// No simula ratón, no usa reflection y no toca estado privado.
/// La entrada de ratón tiene su propia regresión consolidada en el proyecto.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderUniversalPreviewConstructionRegression
{
    private const string Armed =
        "BB.UniversalPreviewV3.Construction";

    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    private static BistroBuilderConstructionAuthoringRuntimeTool tool;
    private static BistroBuilderEditRuntimeCoordinator coordinator;
    private static RestaurantEditInteractionController editController;
    private static BistroBuilderUniversalPreviewService preview;

    private static int baselineWalls;
    private static int stage;
    private static int lastFrame;
    private static double next;
    private static double deadline;
    private static string failure;

    static BistroBuilderUniversalPreviewConstructionRegression()
    {
        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Universal Preview V3/Construction",
        false,
        52061)]
    public static void Run()
    {
        SessionState.SetBool(
            Armed,
            true);

        SessionState.SetBool(
            Armed + ".Pass",
            false);

        EditorSceneManager.OpenScene(
            ScenePath);

        EditorApplication.isPlaying =
            true;
    }

    public static void RunBatch()
    {
        Run();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (!SessionState.GetBool(
                Armed,
                false))
        {
            return;
        }

        if (state ==
            PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            lastFrame = -1;
            failure = null;

            next =
                EditorApplication.timeSinceStartup + 3d;

            deadline =
                EditorApplication.timeSinceStartup + 90d;

            EditorApplication.update +=
                Tick;

            Application.logMessageReceived +=
                HandleLog;

            return;
        }

        if (state !=
            PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.update -=
            Tick;

        Application.logMessageReceived -=
            HandleLog;

        bool passed =
            SessionState.GetBool(
                Armed + ".Pass",
                false);

        SessionState.SetBool(
            Armed,
            false);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(
                passed ? 0 : 1);
        }
    }

    private static void HandleLog(
        string message,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Exception ||
            type == LogType.Assert)
        {
            failure =
                message +
                "\n" +
                stackTrace;
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < next ||
            Time.frameCount == lastFrame)
        {
            return;
        }

        next =
            EditorApplication.timeSinceStartup + 0.15d;

        lastFrame =
            Time.frameCount;

        try
        {
            if (failure != null)
            {
                throw new InvalidOperationException(
                    failure);
            }

            if (EditorApplication.timeSinceStartup >
                deadline)
            {
                throw new TimeoutException(
                    "Construction preview regression timed out.");
            }

            switch (stage)
            {
                case 0:
                    ResolveDependencies();

                    if (!DependenciesReady())
                        return;

                    UnityEngine.Object
                        .FindFirstObjectByType<
                            BistroBuilderNewGameOpeningPlayerScreen>()
                        ?.Hide();

                    Check(
                        editController.TryEnterEditMode(),
                        "No se pudo entrar en modo edición.");

                    break;

                case 1:
                    Check(
                        coordinator.HasSession ||
                        !tool.HasDraftSession,
                        "Estado de Draft Session incoherente.");

                    Check(
                        tool.TryPreviewRoomAtPlanPoints(
                            new Vector2(30f, 30f),
                            new Vector2(36f, 34f),
                            "zone.dining",
                            out string previewError),
                        "No se pudo crear la preview de habitación: " +
                        previewError);

                    Check(
                        coordinator.HasSession,
                        "La preview no inicializó Draft Session.");

                    baselineWalls =
                        coordinator.Session.Draft.walls.Count;

                    break;

                case 2:
                    Check(
                        coordinator.Session.Draft.walls.Count ==
                            baselineWalls,
                        "La preview modificó el borrador antes de confirmar.");

                    AssertConstructionPreview();

                    Check(
                        tool.TryCancelDraft(
                            out string cancelError),
                        "No se pudo cancelar el borrador de preview: " +
                        cancelError);

                    break;

                case 3:
                    Check(
                        !coordinator.HasSession,
                        "Cancelar no cerró la Draft Session.");

                    Check(
                        !preview.Current.IsVisible,
                        "La preview siguió visible tras cancelar.");

                    tool.SetMode(
                        BistroBuilderConstructionRuntimeMode.Furniture);

                    editController.TryExitEditMode(
                        true);

                    Finish(
                        true,
                        "preview aislada / 4 segmentos / " +
                        "4 volúmenes / dimensiones configuradas / " +
                        "cancelación limpia");

                    return;
            }

            stage++;
        }
        catch (Exception error)
        {
            Finish(
                false,
                "Stage " +
                stage +
                ": " +
                error);
        }
    }

    private static void ResolveDependencies()
    {
        tool =
            UnityEngine.Object
                .FindFirstObjectByType<
                    BistroBuilderConstructionAuthoringRuntimeTool>();

        coordinator =
            UnityEngine.Object
                .FindFirstObjectByType<
                    BistroBuilderEditRuntimeCoordinator>();

        editController =
            UnityEngine.Object
                .FindFirstObjectByType<
                    RestaurantEditInteractionController>();

        preview =
            BistroBuilderUniversalPreviewService
                .GetOrCreate();
    }

    private static bool DependenciesReady()
    {
        return tool != null &&
               coordinator != null &&
               editController != null &&
               preview != null &&
               BistroBuilderConstructionPlayerPanel.Instance != null &&
               BistroBuilderConstructionPlayerPanel.Instance.IsReady &&
               UnityEngine.Object.FindFirstObjectByType<
                   BistroBuilderUniversalPreviewRenderer>() != null;
    }

    private static void AssertConstructionPreview()
    {
        BistroBuilderUniversalPreviewState state =
            preview.Current;

        Check(
            state.IsVisible,
            "La preview de construcción no está visible.");

        Check(
            state.OwnerId ==
                BistroBuilderUniversalPreviewService
                    .ConstructionOwner,
            "La preview activa no pertenece a Construction.");

        Check(
            state.Domain ==
                BistroBuilderPreviewDomain.Room,
            "La preview no está tipada como Room.");

        Check(
            state.Validity ==
                BistroBuilderPreviewValidity.Valid,
            "La habitación de regresión no está marcada válida.");

        Check(
            state.CandidateSegments.Count == 8,
            "La habitación no publicó cuatro segmentos.");

        Check(
            state.Volumes.Count == 4,
            "La habitación no publicó cuatro volúmenes.");

        for (int i = 0;
             i < state.Volumes.Count;
             i++)
        {
            BistroBuilderPreviewBox box =
                state.Volumes[i];

            Check(
                Mathf.Abs(
                    box.Size.y -
                    tool.WallHeight) <= 0.001f,
                "El volumen no conserva la altura configurada.");

            Check(
                Mathf.Abs(
                    box.Size.z -
                    tool.WallThickness) <= 0.001f,
                "El volumen no conserva el grosor configurado.");
        }
    }

    private static void Finish(
        bool passed,
        string message)
    {
        EditorApplication.update -=
            Tick;

        Application.logMessageReceived -=
            HandleLog;

        if (tool != null)
        {
            if (tool.HasDraftSession)
            {
                tool.TryCancelDraft(
                    out _);
            }

            tool.SetMode(
                BistroBuilderConstructionRuntimeMode.Furniture);
        }

        if (editController != null)
        {
            editController.TryExitEditMode(
                true);
        }

        Directory.CreateDirectory(
            "Logs");

        string report =
            (passed ? "PASS " : "FAIL ") +
            message;

        File.WriteAllText(
            "Logs/UniversalPreviewConstructionRegression.txt",
            report);

        Debug.Log(
            "BB_UNIVERSAL_PREVIEW_CONSTRUCTION_" +
            report);

        SessionState.SetBool(
            Armed + ".Pass",
            passed);

        EditorApplication.isPlaying =
            false;
    }

    private static void Check(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
