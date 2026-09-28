using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Regresión determinista del preview de mobiliario.
/// Usa únicamente APIs públicas del modo edición y un obstáculo temporal
/// registrado en el mismo sistema de validación que usa el juego.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderUniversalPreviewFurnitureRegression
{
    private const string Armed = "BB.UniversalPreviewV3.Furniture";
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    private static RestaurantEditInteractionController controller;
    private static RestaurantPlacementValidationService validation;
    private static RestaurantPlaceableRegistry placeables;
    private static RestaurantPlacementObstacleRegistry obstacles;
    private static BistroBuilderUniversalPreviewService preview;

    private static RestaurantPlaceableObject target;
    private static RestaurantAreaMember targetMember;
    private static GameObject temporaryObstacleRoot;
    private static RestaurantPlacementObstacle temporaryObstacle;

    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static Vector3 originalScale;
    private static Transform originalParent;
    private static int originalSiblingIndex;
    private static RestaurantArea originalArea;
    private static MeshRenderer[] targetRenderers = Array.Empty<MeshRenderer>();
    private static int initiallyEnabledMeshRenderers;

    private static int stage;
    private static int lastFrame;
    private static double next;
    private static double deadline;
    private static string failure;

    static BistroBuilderUniversalPreviewFurnitureRegression()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Validation/Universal Preview V3/Furniture",
        false,
        52060)]
    public static void Run()
    {
        SessionState.SetBool(Armed, true);
        SessionState.SetBool(Armed + ".Pass", false);
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    public static void RunBatch()
    {
        Run();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Armed, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            lastFrame = -1;
            failure = null;
            next = EditorApplication.timeSinceStartup + 3d;
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
            Application.logMessageReceived += HandleLog;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.update -= Tick;
        Application.logMessageReceived -= HandleLog;

        bool passed =
            SessionState.GetBool(
                Armed + ".Pass",
                false);

        SessionState.SetBool(Armed, false);

        if (Application.isBatchMode)
            EditorApplication.Exit(passed ? 0 : 1);
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
                message + "\n" + stackTrace;
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
                throw new InvalidOperationException(failure);

            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException(
                    "Furniture preview regression timed out.");

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
                        controller.TryEnterEditMode(),
                        "No se pudo entrar en modo edición.");

                    SelectTarget();
                    CaptureOriginalState();

                    Check(
                        controller.TrySelectPlaceable(target),
                        "No se pudo seleccionar el colocable.");

                    Check(
                        CountEnabledMeshRenderers() ==
                            initiallyEnabledMeshRenderers,
                        "Seleccionar alteró la representación visual.");

                    Check(
                        controller.TryBeginMoveSelected(),
                        "No se pudo iniciar el movimiento.");

                    Check(
                        controller.HasActivePlacement,
                        "Move no abrió una transacción.");

                    Check(
                        CountEnabledMeshRenderers() == 0,
                        "El proxy no ocultó todas las MeshRenderer fuente.");

                    break;

                case 1:
                    Check(
                        controller.TryPreviewActivePlacementAtWorldPose(
                            originalPosition,
                            originalRotation,
                            out RestaurantPlacementValidationResult
                                validResult,
                            out RestaurantPlacementTransactionFailureReason
                                validFailure),
                        "No se pudo publicar la pose válida: " +
                        validFailure + ".");

                    Check(
                        validResult.IsValid,
                        "La pose original no es válida.");

                    AssertValidUniversalPreview();

                    CreateAndRegisterBlockingObstacle();
                    break;

                case 2:
                    Check(
                        controller.TryPreviewActivePlacementAtWorldPose(
                            originalPosition,
                            originalRotation,
                            out RestaurantPlacementValidationResult
                                invalidResult,
                            out RestaurantPlacementTransactionFailureReason
                                invalidFailure),
                        "No se pudo publicar la pose conflictiva: " +
                        invalidFailure + ".");

                    Check(
                        !invalidResult.IsValid,
                        "El obstáculo registrado no invalidó la pose.");

                    Check(
                        invalidResult.ConflictingObstacle ==
                            temporaryObstacle,
                        "El validador no identificó el obstáculo temporal.");

                    AssertInvalidUniversalPreview();

                    RemoveTemporaryObstacle();

                    Check(
                        controller.CancelActivePlacement(),
                        "No se pudo cancelar la colocación.");

                    break;

                case 3:
                    AssertOriginalStateRestored();

                    Check(
                        CountEnabledMeshRenderers() ==
                            initiallyEnabledMeshRenderers,
                        "Cancelar no restauró las MeshRenderer fuente.");

                    Check(
                        !preview.Current.IsVisible,
                        "La preview quedó visible después de cancelar.");

                    controller.TryExitEditMode(true);

                    Finish(
                        true,
                        "selección aislada / proxy / pose válida / " +
                        "obstáculo real / conflicto localizado / " +
                        "cancelación exacta");

                    return;
            }

            stage++;
        }
        catch (Exception error)
        {
            Finish(
                false,
                "Stage " + stage + ": " + error);
        }
    }

    private static void ResolveDependencies()
    {
        controller =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantEditInteractionController>();

        validation =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementValidationService>();

        placeables =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableRegistry>();

        obstacles =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementObstacleRegistry>();

        preview =
            BistroBuilderUniversalPreviewService.GetOrCreate();
    }

    private static bool DependenciesReady()
    {
        return controller != null &&
               validation != null &&
               placeables != null &&
               placeables.RegisteredPlaceableCount > 0 &&
               obstacles != null &&
               preview != null &&
               UnityEngine.Object.FindFirstObjectByType<
                   BistroBuilderUniversalPreviewRenderer>() != null &&
               UnityEngine.Object.FindFirstObjectByType<
                   BistroBuilderFurniturePreviewProxyRenderer>() != null;
    }

    private static void SelectTarget()
    {
        target = null;
        targetMember = null;

        foreach (RestaurantPlaceableObject candidate
                 in placeables.RegisteredPlaceables)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                !candidate.TryGetComponent(
                    out RestaurantEditableObject editable) ||
                !candidate.TryGetComponent(
                    out RestaurantAreaMember member) ||
                !candidate.TryGetComponent(
                    out RestaurantPlacementFootprint footprint) ||
                !editable.CanMove ||
                !footprint.BlocksOtherPlacements ||
                candidate.GetComponentsInChildren<MeshRenderer>(true)
                    .Length == 0)
            {
                continue;
            }

            RestaurantPlacementValidationResult current =
                validation.ValidateCurrentPlacement(member);

            if (!current.IsValid)
                continue;

            target = candidate;
            targetMember = member;
            return;
        }

        throw new InvalidOperationException(
            "No existe un colocable móvil y válido para la regresión.");
    }

    private static void CaptureOriginalState()
    {
        originalPosition =
            targetMember.transform.position;

        originalRotation =
            targetMember.transform.rotation;

        originalScale =
            targetMember.transform.localScale;

        originalParent =
            targetMember.transform.parent;

        originalSiblingIndex =
            targetMember.transform.GetSiblingIndex();

        originalArea =
            targetMember.AssignedArea;

        targetRenderers =
            target.GetComponentsInChildren<MeshRenderer>(true);

        initiallyEnabledMeshRenderers =
            CountEnabledMeshRenderers();

        Check(
            initiallyEnabledMeshRenderers > 0,
            "El colocable elegido no tiene MeshRenderer activa.");
    }

    private static void CreateAndRegisterBlockingObstacle()
    {
        RemoveTemporaryObstacle();

        temporaryObstacleRoot =
            new GameObject(
                "BB_UniversalPreview_RegressionObstacle");

        temporaryObstacleRoot.transform.SetPositionAndRotation(
            originalPosition,
            originalRotation);

        temporaryObstacle =
            temporaryObstacleRoot.AddComponent<
                RestaurantPlacementObstacle>();

        Check(
            obstacles.RegisterObstacle(temporaryObstacle),
            "No se pudo registrar el obstáculo temporal.");
    }

    private static void RemoveTemporaryObstacle()
    {
        if (temporaryObstacle != null &&
            obstacles != null)
        {
            obstacles.UnregisterObstacle(
                temporaryObstacle);
        }

        if (temporaryObstacleRoot != null)
        {
            temporaryObstacleRoot.SetActive(false);
            UnityEngine.Object.Destroy(
                temporaryObstacleRoot);
        }

        temporaryObstacle = null;
        temporaryObstacleRoot = null;
    }

    private static void AssertValidUniversalPreview()
    {
        BistroBuilderUniversalPreviewState state =
            preview.Current;

        Check(
            state.IsVisible,
            "La preview válida no está visible.");

        Check(
            state.OwnerId ==
                BistroBuilderUniversalPreviewService.FurnitureOwner,
            "La preview válida no pertenece a Furniture.");

        Check(
            state.Domain ==
                BistroBuilderPreviewDomain.Furniture,
            "La preview válida no usa dominio Furniture.");

        Check(
            state.Validity ==
                BistroBuilderPreviewValidity.Valid,
            "La preview válida no está marcada como Valid.");

        Check(
            state.HasCandidatePose,
            "La preview válida no contiene pose candidata.");

        Check(
            state.CandidateSegments.Count == 8,
            "La huella válida no contiene cuatro segmentos.");

        Check(
            state.GhostSegments.Count == 8,
            "El movimiento existente no contiene ghost original.");
    }

    private static void AssertInvalidUniversalPreview()
    {
        BistroBuilderUniversalPreviewState state =
            preview.Current;

        Check(
            state.IsVisible,
            "La preview inválida no está visible.");

        Check(
            state.Validity ==
                BistroBuilderPreviewValidity.Invalid,
            "La preview conflictiva no está marcada Invalid.");

        Check(
            ReferenceEquals(
                state.ConflictObject,
                temporaryObstacle),
            "La preview no apunta al obstáculo que devolvió el validador.");

        Check(
            state.ConflictSegments.Count == 8,
            "El conflicto no contiene un contorno de cuatro segmentos.");
    }

    private static void AssertOriginalStateRestored()
    {
        Check(
            Vector3.Distance(
                targetMember.transform.position,
                originalPosition) <= 0.0001f,
            "La posición no fue restaurada.");

        Check(
            Quaternion.Angle(
                targetMember.transform.rotation,
                originalRotation) <= 0.01f,
            "La rotación no fue restaurada.");

        Check(
            Vector3.Distance(
                targetMember.transform.localScale,
                originalScale) <= 0.0001f,
            "La escala no fue restaurada.");

        Check(
            targetMember.transform.parent == originalParent,
            "El padre no fue restaurado.");

        Check(
            targetMember.transform.GetSiblingIndex() ==
                originalSiblingIndex,
            "El sibling index no fue restaurado.");

        Check(
            targetMember.AssignedArea == originalArea,
            "El área no fue restaurada.");
    }

    private static int CountEnabledMeshRenderers()
    {
        int count = 0;

        for (int i = 0;
             i < targetRenderers.Length;
             i++)
        {
            MeshRenderer renderer =
                targetRenderers[i];

            if (renderer != null &&
                renderer.enabled)
            {
                count++;
            }
        }

        return count;
    }

    private static void Finish(
        bool passed,
        string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= HandleLog;

        RemoveTemporaryObstacle();

        if (controller != null &&
            controller.HasActivePlacement)
        {
            controller.CancelActivePlacement();
        }

        Directory.CreateDirectory("Logs");

        string report =
            (passed ? "PASS " : "FAIL ") +
            message;

        File.WriteAllText(
            "Logs/UniversalPreviewFurnitureRegression.txt",
            report);

        Debug.Log(
            "BB_UNIVERSAL_PREVIEW_FURNITURE_" +
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
            throw new InvalidOperationException(message);
    }
}
