using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado determinista del preflight espacial de una pose candidata.
/// BBSIS solo decide si el objeto puede utilizarse en esa pose.
/// </summary>
public readonly struct BistroBuilderSpatialPlacementEvaluation
{
    public bool IsValid { get; }
    public string RuleId { get; }
    public string UserMessage { get; }
    public string TechnicalMessage { get; }
    public string BlockingSubjectId { get; }
    public UnityEngine.Object RelatedObject { get; }

    private BistroBuilderSpatialPlacementEvaluation(
        bool valid,
        string ruleId,
        string userMessage,
        string technicalMessage,
        string blockerId,
        UnityEngine.Object relatedObject)
    {
        IsValid = valid;
        RuleId = ruleId ?? string.Empty;
        UserMessage = userMessage ?? string.Empty;
        TechnicalMessage = technicalMessage ?? string.Empty;
        BlockingSubjectId = blockerId ?? string.Empty;
        RelatedObject = relatedObject;
    }

    public static BistroBuilderSpatialPlacementEvaluation Valid()
    {
        return new BistroBuilderSpatialPlacementEvaluation(
            true, string.Empty, string.Empty, string.Empty,
            string.Empty, null);
    }

    public static BistroBuilderSpatialPlacementEvaluation Invalid(
        string ruleId,
        string userMessage,
        string technicalMessage,
        string blockerId = "",
        UnityEngine.Object relatedObject = null)
    {
        return new BistroBuilderSpatialPlacementEvaluation(
            false, ruleId, userMessage, technicalMessage,
            blockerId, relatedObject);
    }
}

/// <summary>
/// Evalúa una pose candidata contra la semántica espacial completa.
////// No mueve objetos, no construye rutas y no conoce decisiones de gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialPlacementAssessmentService :
    MonoBehaviour
{
    [SerializeField]
    private BistroBuilderSpatialInteractionService spatialService;

    private readonly List<MonoBehaviour> providers =
        new List<MonoBehaviour>(128);
    private readonly List<BistroBuilderSpatialSemanticVolume> semantics =
        new List<BistroBuilderSpatialSemanticVolume>(192);
    private readonly List<BistroBuilderSpatialSemanticVolume> candidateSemantics =
        new List<BistroBuilderSpatialSemanticVolume>(32);
    private readonly List<BistroBuilderSpatialVolume> candidateStaticVolumes =
        new List<BistroBuilderSpatialVolume>(16);
    private readonly List<RestaurantPlacementObstacle> obstacles =
        new List<RestaurantPlacementObstacle>(32);

    public int CachedProviderCount => providers.Count;
    public int EvaluationCount { get; private set; }

    private void Awake()
    {
        ResolveDependencies();
        RefreshProviderCache();
    }
    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (spatialService == null)
        {
            error = "Spatial Placement Assessment necesita BBSIS.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void RefreshProviderCache()
    {
        providers.Clear();
        MonoBehaviour[] found = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < found.Length; i++)
            RegisterProvider(found[i], false);
        providers.Sort(CompareProviders);
    }

    public bool RegisterProvider(MonoBehaviour behaviour)
    {
        return RegisterProvider(behaviour, true);
    }

    public void RegisterProviders(GameObject owner)
    {
        if (owner == null) return;
        MonoBehaviour[] found = owner.GetComponents<MonoBehaviour>();
        bool changed = false;
        for (int i = 0; i < found.Length; i++)
            changed |= RegisterProvider(found[i], false);
        if (changed) providers.Sort(CompareProviders);
    }

    public bool UnregisterProvider(MonoBehaviour behaviour)
    {
        return behaviour != null && providers.Remove(behaviour);
    }

    private bool RegisterProvider(MonoBehaviour behaviour, bool sort)
    {
        if (behaviour == null ||
            !(behaviour is IBistroBuilderSpatialSemanticProvider) ||
            providers.Contains(behaviour))
            return false;
        providers.Add(behaviour);
        if (sort) providers.Sort(CompareProviders);
        return true;
    }

    public BistroBuilderSpatialPlacementEvaluation EvaluateCandidate(
        BistroBuilderSpatialSubject subject,
        Vector3 candidatePosition,
        Quaternion candidateRotation,
        RestaurantArea candidateArea,
        RestaurantPlacementObstacleRegistry obstacleRegistry)
    {
        EvaluationCount++;
        ResolveDependencies();
        if (subject == null || subject.Proxy == null)
            return BistroBuilderSpatialPlacementEvaluation.Valid();
        if (spatialService == null)
            return BistroBuilderSpatialPlacementEvaluation.Invalid(
                "bbsis.system_unavailable",
                "No se puede validar todavía el espacio funcional.",
                "Falta la autoridad BBSIS.");

        CollectSemantics();
        candidateSemantics.Clear();
        MonoBehaviour[] candidateProviders =
            subject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < candidateProviders.Length; i++)
            if (candidateProviders[i] is
                IBistroBuilderSpatialSemanticProvider provider)
                provider.WriteSemanticVolumes(candidateSemantics);

        Quaternion deltaRotation =
            candidateRotation * Quaternion.Inverse(subject.transform.rotation);
        for (int i = 0; i < candidateSemantics.Count; i++)
        {
            BistroBuilderSpatialSemanticVolume semantic =
                candidateSemantics[i];
            if (semantic == null)
                continue;
            semantic.volume = TransformVolume(
                semantic.volume,
                subject.transform.position,
                candidatePosition,
                deltaRotation);
        }

        for (int i = 0; i < candidateSemantics.Count; i++)
        {
            BistroBuilderSpatialSemanticVolume semantic =
                candidateSemantics[i];
            if (!semantic.critical)
                continue;

            if (!IsInsideArea(semantic.volume, candidateArea))
                return BistroBuilderSpatialPlacementEvaluation.Invalid(
                    "bbsis.functional_space_outside_area",
                    "El objeto cabe, pero no queda espacio para utilizarlo.",
                    semantic.semanticId +
                    " queda fuera del área funcional.");

            if (TryFindObstacleConflict(
                    semantic.volume,
                    obstacleRegistry,
                    out RestaurantPlacementObstacle obstacle))
                return BistroBuilderSpatialPlacementEvaluation.Invalid(
                    "bbsis.functional_space_obstacle",
                    "El objeto cabe, pero su espacio de uso está bloqueado.",
                    semantic.semanticId + " intersecta " +
                    obstacle.ObstacleId + ".",
                    obstacle.ObstacleId,
                    obstacle);

            if (spatialService.TryFindStaticGeometryConflict(
                    semantic.volume,
                    subject.SubjectId,
                    semantic.relatedSubjectId,
                    out string blockerId))
                return BistroBuilderSpatialPlacementEvaluation.Invalid(
                    "bbsis.functional_space_static",
                    "El objeto cabe, pero su espacio de uso está bloqueado.",
                    semantic.semanticId +
                    " intersecta la geometría de " + blockerId + ".",
                    blockerId,
                    ResolveSubjectObject(blockerId));
        }

        BistroBuilderSpatialPlacementEvaluation pairResult =
            EvaluateSemanticPairs(subject);
        if (!pairResult.IsValid)
            return pairResult;

        candidateStaticVolumes.Clear();
        subject.Proxy.BuildWorldVolumes(
            BistroBuilderSpatialProxyLayer.Static,
            candidateStaticVolumes);
        for (int i = 0; i < candidateStaticVolumes.Count; i++)
            candidateStaticVolumes[i] = TransformVolume(
                candidateStaticVolumes[i],
                subject.transform.position,
                candidatePosition,
                deltaRotation);

        BistroBuilderSpatialPlacementEvaluation reverseResult =
            EvaluateStaticBodyAgainstSemantics(subject);
        return reverseResult;
    }

    private void CollectSemantics()
    {
        semantics.Clear();
        for (int i = 0; i < providers.Count; i++)
        {
            MonoBehaviour behaviour = providers[i];
            if (behaviour == null ||
                !behaviour.isActiveAndEnabled ||
                !(behaviour is IBistroBuilderSpatialSemanticProvider provider))
                continue;
            provider.WriteSemanticVolumes(semantics);
        }

        semantics.Sort(CompareSemantics);
    }

    private BistroBuilderSpatialPlacementEvaluation EvaluateSemanticPairs(
        BistroBuilderSpatialSubject subject)
    {
        for (int firstIndex = 0;
             firstIndex < candidateSemantics.Count;
             firstIndex++)
        {
            BistroBuilderSpatialSemanticVolume candidate =
                candidateSemantics[firstIndex];
            for (int secondIndex = 0;
                 secondIndex < semantics.Count;
                 secondIndex++)
            {
                BistroBuilderSpatialSemanticVolume other =
                    semantics[secondIndex];
                if (other == null ||
                    string.Equals(
                        other.subjectId,
                        subject.SubjectId,
                        StringComparison.Ordinal) ||
                    AreRelated(candidate, other) ||
                    !candidate.volume.Overlaps(other.volume))
                    continue;

                if (!BistroBuilderSpatialAssessmentService.TryClassifyPair(
                        candidate,
                        other,
                        out bool blocking,
                        out _) ||
                    !blocking)
                    continue;

                return BistroBuilderSpatialPlacementEvaluation.Invalid(
                    "bbsis.semantic_conflict",
                    "La colocación bloquea una zona necesaria para el servicio.",
                    candidate.semanticId + " solapa " +
                    other.semanticId + ".",
                    other.subjectId,
                    ResolveSubjectObject(other.subjectId));
            }
        }

        return BistroBuilderSpatialPlacementEvaluation.Valid();
    }

    private BistroBuilderSpatialPlacementEvaluation
        EvaluateStaticBodyAgainstSemantics(
            BistroBuilderSpatialSubject subject)
    {
        for (int volumeIndex = 0;
             volumeIndex < candidateStaticVolumes.Count;
             volumeIndex++)
        {
            BistroBuilderSpatialVolume body =
                candidateStaticVolumes[volumeIndex];
            for (int semanticIndex = 0;
                 semanticIndex < semantics.Count;
                 semanticIndex++)
            {
                BistroBuilderSpatialSemanticVolume other =
                    semantics[semanticIndex];
                if (other == null ||
                    !other.critical ||
                    other.conflictMode ==
                        BistroBuilderSpatialConflictMode.Compatible ||
                    other.conflictMode ==
                        BistroBuilderSpatialConflictMode.Degrade ||
                    string.Equals(
                        other.subjectId,
                        subject.SubjectId,
                        StringComparison.Ordinal) ||
                    IsRelatedToSubject(other, subject.SubjectId) ||
                    CandidateIsRelatedTo(other.subjectId) ||
                    IsBodySemanticCompatible(subject, other) ||
                    !body.Overlaps(other.volume))
                    continue;

                return BistroBuilderSpatialPlacementEvaluation.Invalid(
                    "bbsis.body_blocks_function",
                    "La colocación ocupa el espacio necesario para usar otro objeto.",
                    subject.SubjectId + " bloquea " +
                    other.semanticId + ".",
                    other.subjectId,
                    ResolveSubjectObject(other.subjectId));
            }
        }

        return BistroBuilderSpatialPlacementEvaluation.Valid();
    }

    private bool TryFindObstacleConflict(
        BistroBuilderSpatialVolume volume,
        RestaurantPlacementObstacleRegistry registry,
        out RestaurantPlacementObstacle blocker)
    {
        blocker = null;
        if (registry == null)
            return false;

        registry.CopyBlockingObstacles(obstacles);
        obstacles.Sort((left, right) =>
            string.CompareOrdinal(
                left != null ? left.ObstacleId : string.Empty,
                right != null ? right.ObstacleId : string.Empty));
        for (int i = 0; i < obstacles.Count; i++)
        {
            RestaurantPlacementObstacle obstacle = obstacles[i];
            if (obstacle == null || !obstacle.IsBlocking)
                continue;
            BistroBuilderSpatialVolume obstacleVolume =
                BistroBuilderSpatialVolume.Box(
                    obstacle.WorldCenter,
                    obstacle.WorldRightAxis,
                    obstacle.WorldForwardAxis,
                    obstacle.WorldSize * 0.5f);
            if (!volume.Overlaps(obstacleVolume))
                continue;
            blocker = obstacle;
            return true;
        }

        return false;
    }

    private UnityEngine.Object ResolveSubjectObject(string subjectId)
    {
        return spatialService != null &&
               spatialService.TryGetSubject(
                   subjectId,
                   out BistroBuilderSpatialSubject subject)
            ? subject
            : null;
    }

    private static BistroBuilderSpatialVolume TransformVolume(
        BistroBuilderSpatialVolume source,
        Vector3 currentRootPosition,
        Vector3 candidateRootPosition,
        Quaternion deltaRotation)
    {
        Vector3 center = candidateRootPosition +
            deltaRotation * (source.center - currentRootPosition);
        if (source.shapeKind == BistroBuilderSpatialShapeKind.Circle)
            return BistroBuilderSpatialVolume.Circle(
                center,
                source.radius);

        return BistroBuilderSpatialVolume.Box(
            center,
            deltaRotation * source.rightAxis,
            deltaRotation * source.forwardAxis,
            source.halfExtents);
    }

    private static bool IsInsideArea(
        BistroBuilderSpatialVolume volume,
        RestaurantArea area)
    {
        if (area == null)
            return true;
        if (!area.ContainsPosition(volume.center))
            return false;

        if (volume.shapeKind == BistroBuilderSpatialShapeKind.Circle)
        {
            float radius = Mathf.Max(0.01f, volume.radius);
            return area.ContainsPosition(
                       volume.center + Vector3.right * radius) &&
                   area.ContainsPosition(
                       volume.center - Vector3.right * radius) &&
                   area.ContainsPosition(
                       volume.center + Vector3.forward * radius) &&
                   area.ContainsPosition(
                       volume.center - Vector3.forward * radius);
        }

        Vector3 right = volume.rightAxis.normalized *
            volume.halfExtents.x;
        Vector3 forward = volume.forwardAxis.normalized *
            volume.halfExtents.y;
        return area.ContainsPosition(volume.center + right + forward) &&
               area.ContainsPosition(volume.center + right - forward) &&
               area.ContainsPosition(volume.center - right + forward) &&
               area.ContainsPosition(volume.center - right - forward);
    }

    private static bool AreRelated(
        BistroBuilderSpatialSemanticVolume first,
        BistroBuilderSpatialSemanticVolume second)
    {
        return IsRelatedToSubject(first, second.subjectId) ||
               IsRelatedToSubject(second, first.subjectId);
    }

    private static bool IsRelatedToSubject(
        BistroBuilderSpatialSemanticVolume semantic,
        string subjectId)
    {
        return semantic != null &&
               !string.IsNullOrWhiteSpace(semantic.relatedSubjectId) &&
               string.Equals(
                   semantic.relatedSubjectId,
                   subjectId,
                   StringComparison.Ordinal);
    }

    private bool CandidateIsRelatedTo(string subjectId)
    {
        for (int i = 0; i < candidateSemantics.Count; i++)
            if (IsRelatedToSubject(candidateSemantics[i], subjectId))
                return true;
        return false;
    }

    private static bool IsBodySemanticCompatible(
        BistroBuilderSpatialSubject subject,
        BistroBuilderSpatialSemanticVolume semantic)
    {
        return subject != null &&
               subject.Contract != null &&
               subject.Contract.HasTrait("seating.chair") &&
               semantic != null &&
               semantic.role == BistroBuilderSpatialSemanticRole.SeatBay;
    }

    private static int CompareProviders(
        MonoBehaviour first,
        MonoBehaviour second)
    {
        if (ReferenceEquals(first, second))
            return 0;
        if (first == null)
            return 1;
        if (second == null)
            return -1;
        string firstId =
            ((IBistroBuilderSpatialSemanticProvider)first).SpatialSubjectId;
        string secondId =
            ((IBistroBuilderSpatialSemanticProvider)second).SpatialSubjectId;
        int byId = string.CompareOrdinal(firstId, secondId);
        return byId != 0
            ? byId
            : first.GetInstanceID().CompareTo(second.GetInstanceID());
    }

    private static int CompareSemantics(
        BistroBuilderSpatialSemanticVolume first,
        BistroBuilderSpatialSemanticVolume second)
    {
        if (ReferenceEquals(first, second))
            return 0;
        if (first == null)
            return 1;
        if (second == null)
            return -1;
        int bySubject =
            string.CompareOrdinal(first.subjectId, second.subjectId);
        if (bySubject != 0)
            return bySubject;
        return string.CompareOrdinal(
            first.semanticId,
            second.semanticId);
    }

    private void ResolveDependencies()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderSpatialInteractionService service)
    {
        spatialService = service;
    }
#endif
}
