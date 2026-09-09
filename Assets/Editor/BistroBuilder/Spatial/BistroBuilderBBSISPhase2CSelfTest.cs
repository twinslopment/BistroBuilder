using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2CSelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2C/Autotest")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2C - AUTOTEST");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialPlacementAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialPlacementAssessmentService>();
        BistroBuilderSpatialPlacementConstraintRule rule =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialPlacementConstraintRule>();
        BistroBuilderSpatialEditModeIntegration integration =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialEditModeIntegration>();

        Check(spatial != null,
            "Servicio BBSIS disponible", report);
        Check(assessment != null,
            "Assessment candidato disponible", report);
        Check(rule != null,
            "Regla de colocación disponible", report);
        Check(integration != null,
            "Sincronizador de edición disponible", report);
        if (spatial == null || assessment == null ||
            rule == null || integration == null)
        {
            Finish(report);
            return;
        }

        integration.RefreshNow();
        assessment.RefreshProviderCache();
        bool fixtureFound = TryFindFixture(
            out BistroBuilderSpatialSubject candidate,
            out RestaurantAreaMember member,
            out BistroBuilderSpatialSemanticVolume semantic,
            out BistroBuilderSpatialVolume blocker,
            out BistroBuilderSpatialSubject blockingSubject);
        Check(fixtureFound,
            "Fixture real editable con semántica crítica", report);
        if (!fixtureFound)
        {
            Finish(report);
            return;
        }

        Vector3 clearPosition =
            new Vector3(10000f, candidate.transform.position.y, 10000f);
        BistroBuilderSpatialPlacementEvaluation clear =
            assessment.EvaluateCandidate(
                candidate,
                clearPosition,
                candidate.transform.rotation,
                null,
                null);
        Check(clear.IsValid,
            "Pose libre acepta espacio funcional completo", report);

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
        Check(!blocked.IsValid,
            "Pose visualmente posible rechaza uso bloqueado", report);
        Check(!string.IsNullOrWhiteSpace(blocked.RuleId) &&
              !string.IsNullOrWhiteSpace(blocked.UserMessage),
            "Rechazo contiene causa y mensaje jugable", report);

        BistroBuilderSpatialPlacementEvaluation repeated =
            assessment.EvaluateCandidate(
                candidate,
                blockedPosition,
                candidate.transform.rotation,
                null,
                null);
        Check(blocked.IsValid == repeated.IsValid &&
              string.Equals(
                  blocked.RuleId,
                  repeated.RuleId,
                  StringComparison.Ordinal) &&
              string.Equals(
                  blocked.BlockingSubjectId,
                  repeated.BlockingSubjectId,
                  StringComparison.Ordinal),
            "Preflight repetido es determinista", report);

        RestaurantPlacementConstraintContext context =
            new RestaurantPlacementConstraintContext(
                member,
                blockedPosition,
                candidate.transform.rotation,
                null,
                member.GetComponent<RestaurantPlacementFootprint>(),
                UnityEngine.Object.FindFirstObjectByType<
                    RestaurantPlacementRegistry>(),
                UnityEngine.Object.FindFirstObjectByType<
                    RestaurantPlacementObstacleRegistry>());
        RestaurantPlacementConstraintEvaluation mapped =
            rule.Evaluate(context);
        Check(!mapped.IsValid &&
              mapped.ShouldOverrideGenericConflicts,
            "Regla BBSIS bloquea confirmación transaccional", report);
        Check(string.Equals(
                  mapped.UserMessage,
                  blocked.UserMessage,
                  StringComparison.Ordinal),
            "Feedback conserva el mensaje espacial exacto", report);

        if (member.AssignedArea != null)
        {
            BistroBuilderSpatialPlacementEvaluation outside =
                assessment.EvaluateCandidate(
                    candidate,
                    clearPosition,
                    candidate.transform.rotation,
                    member.AssignedArea,
                    null);
            Check(!outside.IsValid &&
                  string.Equals(
                      outside.RuleId,
                      "bbsis.functional_space_outside_area",
                      StringComparison.Ordinal),
                "Clearance fuera del área se rechaza", report);
        }
        else
        {
            Check(true,
                "Clearance fuera del área no aplica al fixture",
                report);
        }

        Check(TryValidateChairSeatBayCompatibility(assessment),
            "Silla y Seat Bay mantienen compatibilidad funcional", report);

        int leasesBefore = spatial.ActiveLeaseCount;
        int providersBefore = assessment.CachedProviderCount;
        bool stressStable = true;
        for (int i = 0; i < 300; i++)
        {
            BistroBuilderSpatialPlacementEvaluation value =
                assessment.EvaluateCandidate(
                    candidate,
                    (i & 1) == 0
                        ? clearPosition
                        : blockedPosition,
                    candidate.transform.rotation,
                    null,
                    null);
            if (((i & 1) == 0) != value.IsValid)
            {
                stressStable = false;
                break;
            }
        }
        Check(stressStable,
            "Stress 300 preflights alternos estable", report);
        Check(spatial.ActiveLeaseCount == leasesBefore,
            "Preflight no crea Spatial Leases", report);

        int refreshBefore = integration.RefreshCount;
        int subjectCountBefore = spatial.SubjectCount;
        integration.RefreshNow();
        Check(integration.RefreshCount == refreshBefore + 1 &&
              integration.LastTopologyRevision == spatial.Revision,
            "Refresh confirmado reconstruye revisión espacial",
            report);
        Check(spatial.SubjectCount == subjectCountBefore,
            "Refresh no duplica Spatial Subjects", report);
        Check(assessment.CachedProviderCount == providersBefore,
            "Refresh no duplica proveedores semánticos", report);
        Check(integration.LastSpatialQuality >= 0f &&
              integration.LastSpatialQuality <= 1f,
            "Spatial Quality post-edición permanece acotada",
            report);

        Finish(report);
    }

    private static bool TryFindFixture(
        out BistroBuilderSpatialSubject candidate,
        out RestaurantAreaMember member,
        out BistroBuilderSpatialSemanticVolume semantic,
        out BistroBuilderSpatialVolume blocker,
        out BistroBuilderSpatialSubject blockingSubject)
    {
        candidate = null;
        member = null;
        semantic = null;
        blocker = default;
        blockingSubject = null;

        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
        Array.Sort(subjects, CompareSubjects);
        var semanticBuffer =
            new List<BistroBuilderSpatialSemanticVolume>(16);
        var volumeBuffer =
            new List<BistroBuilderSpatialVolume>(8);

        for (int candidateIndex = 0;
             candidateIndex < subjects.Length;
             candidateIndex++)
        {
            BistroBuilderSpatialSubject current =
                subjects[candidateIndex];
            RestaurantAreaMember currentMember =
                current != null
                    ? current.GetComponent<RestaurantAreaMember>()
                    : null;
            if (current == null || currentMember == null)
                continue;

            semanticBuffer.Clear();
            MonoBehaviour[] behaviours =
                current.GetComponents<MonoBehaviour>();
            for (int b = 0; b < behaviours.Length; b++)
                if (behaviours[b] is
                    IBistroBuilderSpatialSemanticProvider provider)
                    provider.WriteSemanticVolumes(semanticBuffer);

            for (int semanticIndex = 0;
                 semanticIndex < semanticBuffer.Count;
                 semanticIndex++)
            {
                BistroBuilderSpatialSemanticVolume currentSemantic =
                    semanticBuffer[semanticIndex];
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

                    volumeBuffer.Clear();
                    other.Proxy.BuildWorldVolumes(
                        BistroBuilderSpatialProxyLayer.Static,
                        volumeBuffer);
                    if (volumeBuffer.Count == 0)
                        continue;

                    candidate = current;
                    member = currentMember;
                    semantic = currentSemantic;
                    blocker = volumeBuffer[0];
                    blockingSubject = other;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryValidateChairSeatBayCompatibility(
        BistroBuilderSpatialPlacementAssessmentService assessment)
    {
        var semantics =
            new List<BistroBuilderSpatialSemanticVolume>(16);
        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null)
                continue;
            semantics.Clear();
            MonoBehaviour[] behaviours = subject.GetComponents<MonoBehaviour>();
            for (int b = 0; b < behaviours.Length; b++)
                if (behaviours[b] is
                    IBistroBuilderSpatialSemanticProvider provider)
                    provider.WriteSemanticVolumes(semantics);
            bool isChair = subject.Contract != null &&
                subject.Contract.HasTrait("seating.chair");
            if (!isChair)
                continue;
            RestaurantAreaMember member =
                subject.GetComponent<RestaurantAreaMember>();
            BistroBuilderSpatialPlacementEvaluation evaluation =
                assessment.EvaluateCandidate(
                    subject,
                    subject.transform.position,
                    subject.transform.rotation,
                    member != null ? member.AssignedArea : null,
                    null);
            if (!string.Equals(
                    evaluation.RuleId,
                    "bbsis.body_blocks_function",
                    StringComparison.Ordinal))
                return true;
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

    private static void Finish(StringBuilder report)
    {
        report.AppendLine(
            "Resultado: " + LastPassed + " OK / " +
            LastFailed + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Autotest BBSIS Fase 2C fallido.");
    }

    private static void Check(
        bool condition,
        string label,
        StringBuilder report)
    {
        if (condition)
        {
            LastPassed++;
            report.AppendLine("OK - " + label);
        }
        else
        {
            LastFailed++;
            report.AppendLine("FAIL - " + label);
        }
    }
}
