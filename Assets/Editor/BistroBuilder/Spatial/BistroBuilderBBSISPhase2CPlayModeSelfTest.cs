using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase2CPlayModeSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey =
        "BB.BBSIS.Phase2C.Play.Stage";
    private const string SuccessKey =
        "BB.BBSIS.Phase2C.Play.Success";
    private const string ReportPath =
        "BBSISPhase2CPlayModeReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;

    static BistroBuilderBBSISPhase2CPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 2C/PlayMode real")]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El PlayMode BBSIS 2C ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        playReadyAt = 0d;
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(
            StageKey,
            cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(
        PlayModeStateChange state)
    {
        string stage =
            SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith(
                "cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup +
                PlayReadyDelaySeconds;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains(
                "cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            playReadyAt = 0d;
            if (cli)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying)
            return;
        string stage =
            SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith(
                "run_", StringComparison.Ordinal))
            return;
        if (playReadyAt <= 0d)
            playReadyAt = EditorApplication.timeSinceStartup +
                PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt)
            return;
        playReadyAt = double.MaxValue;
        bool cli = stage.EndsWith(
            "cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(
                true,
                "PASS - preflight funcional, feedback, confirmación, " +
                "Undo/Redo y reconstrucción espacial funcionan en runtime.",
                cli);
        }
        catch (Exception exception)
        {
            Finish(
                false,
                "BBSIS Fase 2C PlayMode: " + exception.Message,
                cli);
        }
    }

    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialPlacementAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialPlacementAssessmentService>();
        BistroBuilderSpatialEditModeIntegration integration =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialEditModeIntegration>();
        BistroBuilderSpatialAssessmentService layoutAssessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        RestaurantPlacementValidationService validation =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementValidationService>();
        RestaurantPlacementHistoryService history =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementHistoryService>();
        if (spatial == null || assessment == null ||
            integration == null || layoutAssessment == null ||
            validation == null || history == null)
            throw new InvalidOperationException(
                "Faltan servicios BBSIS 2C o de edición.");

        if (!integration.ValidateConfiguration(out string error))
            throw new InvalidOperationException(error);

        assessment.RefreshProviderCache();
        ValidateFunctionalPreflight(assessment);
        ValidateHistoryRoundTrip(
            spatial,
            integration,
            validation,
            history);
        ValidateIncrementalHotPath(
            spatial,
            assessment,
            layoutAssessment,
            integration);

        int subjects = spatial.SubjectCount;
        int providers = assessment.CachedProviderCount;
        integration.RefreshNow();
        integration.RefreshNow();
        if (spatial.SubjectCount != subjects ||
            assessment.CachedProviderCount != providers)
            throw new InvalidOperationException(
                "La reconstrucción duplicó estado espacial.");
        if (spatial.ActiveLeaseCount != 0)
            throw new InvalidOperationException(
                "El Modo Edición dejó Spatial Leases huérfanos.");
    }

    private static void ValidateIncrementalHotPath(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderSpatialPlacementAssessmentService assessment,
        BistroBuilderSpatialAssessmentService layoutAssessment,
        BistroBuilderSpatialEditModeIntegration integration)
    {
        int subjectsBefore = spatial.SubjectCount;
        int providersBefore = assessment.CachedProviderCount;
        int layoutEvaluationsBefore = layoutAssessment.EvaluationCount;
        int fullBefore = integration.FullRefreshCount;
        int incrementalBefore = integration.IncrementalRefreshCount;
        int revisionBefore = spatial.Revision;

        integration.RequestRefresh();
        integration.RefreshIncrementalNow();

        if (integration.IncrementalRefreshCount != incrementalBefore + 1 ||
            integration.FullRefreshCount != fullBefore)
            throw new InvalidOperationException(
                "El hot path incremental ejecutó una reconstrucción completa.");
        if (layoutAssessment.EvaluationCount != layoutEvaluationsBefore)
            throw new InvalidOperationException(
                "El hot path incremental recalculó Spatial Quality global.");
        if (spatial.SubjectCount != subjectsBefore ||
            assessment.CachedProviderCount != providersBefore)
            throw new InvalidOperationException(
                "El hot path incremental alteró registros espaciales.");
        if (spatial.Revision <= revisionBefore)
            throw new InvalidOperationException(
                "El hot path incremental no publicó la revisión geométrica.");
    }

    private static void ValidateFunctionalPreflight(
        BistroBuilderSpatialPlacementAssessmentService assessment)
    {
        if (!TryFindSemanticFixture(
                out BistroBuilderSpatialSubject candidate,
                out BistroBuilderSpatialSemanticVolume semantic,
                out BistroBuilderSpatialVolume blocker))
            throw new InvalidOperationException(
                "No existe fixture espacial editable.");

        Vector3 blockedPosition =
            candidate.transform.position +
            (blocker.center - semantic.volume.center);
        BistroBuilderSpatialPlacementEvaluation blocked =
            assessment.EvaluateCandidate(
                candidate,
                blockedPosition,
                candidate.transform.rotation,
                null,
                null);
        if (blocked.IsValid ||
            string.IsNullOrWhiteSpace(blocked.UserMessage))
            throw new InvalidOperationException(
                "El preflight no bloqueó una zona funcional ocupada.");

        Vector3 clearPosition =
            new Vector3(
                12000f,
                candidate.transform.position.y,
                12000f);
        BistroBuilderSpatialPlacementEvaluation clear =
            assessment.EvaluateCandidate(
                candidate,
                clearPosition,
                candidate.transform.rotation,
                null,
                null);
        if (!clear.IsValid)
            throw new InvalidOperationException(
                "El preflight rechazó una pose espacialmente libre.");
    }

    private static void ValidateHistoryRoundTrip(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderSpatialEditModeIntegration integration,
        RestaurantPlacementValidationService validation,
        RestaurantPlacementHistoryService history)
    {
        if (!TryFindValidMove(
                validation,
                out RestaurantAreaMember member,
                out Vector3 destination))
            throw new InvalidOperationException(
                "No se encontró un movimiento real válido para Undo/Redo.");

        RestaurantPlacementStateSnapshot before =
            RestaurantPlacementStateSnapshot.Capture(member);
        Vector3 originalPosition = member.transform.position;
        Quaternion originalRotation = member.transform.rotation;
        member.transform.SetPositionAndRotation(
            destination,
            originalRotation);
        Physics.SyncTransforms();
        RestaurantPlacementStateSnapshot after =
            RestaurantPlacementStateSnapshot.Capture(member);

        var command = new RestaurantMovePlaceableHistoryCommand(
            member,
            before,
            after,
            validation,
            true);
        if (!history.TryRecordExecutedCommand(command))
            throw new InvalidOperationException(
                "No pudo registrarse el movimiento de prueba.");

        int originalSubjectCount = spatial.SubjectCount;
        if (!history.TryUndo(out _, out _, out _))
            throw new InvalidOperationException(
                "Undo real rechazado por BBSIS.");
        integration.RefreshIncrementalNow();
        if (Vector3.Distance(
                member.transform.position,
                originalPosition) > 0.0001f)
            throw new InvalidOperationException(
                "Undo no restauró la pose original.");

        if (!history.TryRedo(out _, out _, out _))
            throw new InvalidOperationException(
                "Redo real rechazado por BBSIS.");
        integration.RefreshIncrementalNow();
        if (Vector3.Distance(
                member.transform.position,
                destination) > 0.0001f)
            throw new InvalidOperationException(
                "Redo no restauró la pose confirmada.");

        if (!history.TryUndo(out _, out _, out _))
            throw new InvalidOperationException(
                "Undo final no pudo restaurar el fixture.");
        integration.RefreshIncrementalNow();
        if (Vector3.Distance(
                member.transform.position,
                originalPosition) > 0.0001f ||
            spatial.SubjectCount != originalSubjectCount)
            throw new InvalidOperationException(
                "Undo/Redo alteró identidad o topología BBSIS.");
    }

    private static bool TryFindValidMove(
        RestaurantPlacementValidationService validation,
        out RestaurantAreaMember member,
        out Vector3 destination)
    {
        member = null;
        destination = default;
        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
        Array.Sort(subjects, CompareSubjects);
        Vector3[] offsets =
        {
            new Vector3(0.01f, 0f, 0f),
            new Vector3(-0.01f, 0f, 0f),
            new Vector3(0f, 0f, 0.01f),
            new Vector3(0f, 0f, -0.01f),
            new Vector3(0.02f, 0f, 0f),
            new Vector3(0f, 0f, 0.02f)
        };

        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            RestaurantAreaMember current =
                subject != null
                    ? subject.GetComponent<RestaurantAreaMember>()
                    : null;
            if (current == null ||
                current.GetComponent<RestaurantPlacementFootprint>() == null)
                continue;

            for (int offsetIndex = 0;
                 offsetIndex < offsets.Length;
                 offsetIndex++)
            {
                Vector3 candidate =
                    current.transform.position + offsets[offsetIndex];
                RestaurantPlacementValidationResult result =
                    validation.ValidatePlacement(
                        current,
                        candidate,
                        current.transform.rotation);
                if (!result.IsValid)
                    continue;
                member = current;
                destination = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindSemanticFixture(
        out BistroBuilderSpatialSubject candidate,
        out BistroBuilderSpatialSemanticVolume semantic,
        out BistroBuilderSpatialVolume blocker)
    {
        candidate = null;
        semantic = null;
        blocker = default;
        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
        Array.Sort(subjects, CompareSubjects);
        var semantics =
            new List<BistroBuilderSpatialSemanticVolume>(16);
        var volumes =
            new List<BistroBuilderSpatialVolume>(8);

        for (int candidateIndex = 0;
             candidateIndex < subjects.Length;
             candidateIndex++)
        {
            BistroBuilderSpatialSubject current =
                subjects[candidateIndex];
            if (current == null ||
                current.GetComponent<RestaurantAreaMember>() == null)
                continue;

            semantics.Clear();
            MonoBehaviour[] behaviours =
                current.GetComponents<MonoBehaviour>();
            for (int b = 0; b < behaviours.Length; b++)
                if (behaviours[b] is
                    IBistroBuilderSpatialSemanticProvider provider)
                    provider.WriteSemanticVolumes(semantics);

            for (int semanticIndex = 0;
                 semanticIndex < semantics.Count;
                 semanticIndex++)
            {
                BistroBuilderSpatialSemanticVolume currentSemantic =
                    semantics[semanticIndex];
                if (currentSemantic == null ||
                    !currentSemantic.critical)
                    continue;

                for (int blockerIndex = 0;
                     blockerIndex < subjects.Length;
                     blockerIndex++)
                {
                    BistroBuilderSpatialSubject other =
                        subjects[blockerIndex];
                    if (other == null ||
                        other.Proxy == null ||
                        ReferenceEquals(other, current) ||
                        string.Equals(
                            currentSemantic.relatedSubjectId,
                            other.SubjectId,
                            StringComparison.Ordinal))
                        continue;

                    volumes.Clear();
                    other.Proxy.BuildWorldVolumes(
                        BistroBuilderSpatialProxyLayer.Static,
                        volumes);
                    if (volumes.Count == 0)
                        continue;
                    candidate = current;
                    semantic = currentSemantic;
                    blocker = volumes[0];
                    return true;
                }
            }
        }

        return false;
    }

    private static int CompareSubjects(
        BistroBuilderSpatialSubject first,
        BistroBuilderSpatialSubject second)
    {
        if (ReferenceEquals(first, second))
            return 0;
        if (first == null)
            return 1;
        if (second == null)
            return -1;
        return string.CompareOrdinal(
            first.SubjectId,
            second.SubjectId);
    }

    private static void Finish(
        bool success,
        string message,
        bool cli)
    {
        string report =
            "=== BISTRO BUILDER - BBSIS FASE 2C / " +
            "PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") +
            message;
        File.WriteAllText(
            Path.GetFullPath(ReportPath), report);
        if (success)
            Debug.Log(report);
        else
            Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(
            StageKey,
            cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
