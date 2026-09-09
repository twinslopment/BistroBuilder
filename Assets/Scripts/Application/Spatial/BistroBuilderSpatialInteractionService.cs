using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad única de semántica espacial de BBSIS.
/// Gameplay decide qué; BBSIS decide dónde/si; Navegación 17 decide cómo llegar.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialInteractionService : MonoBehaviour
{
    [SerializeField] private BistroBuilderSpatialFamilyCatalog familyCatalog;
    [SerializeField, Min(0.05f)] private float cleanupIntervalSeconds = 0.25f;
    [SerializeField, Min(4)] private int alternateDestinationSamples = 12;
    [SerializeField, Min(1)] private int alternateDestinationRings = 5;
    [SerializeField, Min(0.05f)] private float alternateDestinationStep = 0.22f;

    private readonly Dictionary<string, BistroBuilderSpatialSubject> subjects =
        new Dictionary<string, BistroBuilderSpatialSubject>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderSpatialLease> leases =
        new Dictionary<string, BistroBuilderSpatialLease>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderSpatialEpisode> episodes =
        new Dictionary<string, BistroBuilderSpatialEpisode>(StringComparer.Ordinal);
    private readonly List<string> scratchIds = new List<string>(64);
    private readonly List<string> scratchSubjectIds = new List<string>(64);
    private readonly List<BistroBuilderSpatialVolume> scratchVolumes =
        new List<BistroBuilderSpatialVolume>(32);

    private long nextSequence = 1;
    private float nextCleanupAt;

    public event Action<int> SpatialTopologyChanged;
    public event Action<BistroBuilderSpatialLease> LeaseGranted;
    public event Action<string> LeaseReleased;
    public event Action<BistroBuilderSpatialEpisode> EpisodeChanged;

    public int Revision { get; private set; } = 1;
    public int SubjectCount => subjects.Count;
    public int ActiveLeaseCount => leases.Count;
    public int ActiveEpisodeCount => episodes.Count;
    public BistroBuilderSpatialFamilyCatalog FamilyCatalog => familyCatalog;
    public long GrantedLeaseCount { get; private set; }
    public long RejectedLeaseCount { get; private set; }

    private void Awake()
    {
        RebuildSubjects();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCleanupAt) return;
        CleanupExpiredLeases();
        nextCleanupAt = Time.unscaledTime + cleanupIntervalSeconds;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (cleanupIntervalSeconds <= 0f || alternateDestinationSamples < 4 ||
            alternateDestinationRings < 1 || alternateDestinationStep <= 0f)
        {
            error = "Configuración base BBSIS inválida.";
            return false;
        }
        if (familyCatalog == null)
        {
            error = "BBSIS necesita un catálogo de familias espaciales.";
            return false;
        }
        if (!familyCatalog.ValidateCatalog(out error)) return false;
        error = string.Empty;
        return true;
    }

    public void RebuildSubjects()
    {
        subjects.Clear();
        BistroBuilderSpatialSubject[] found = FindObjectsByType<BistroBuilderSpatialSubject>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Array.Sort(found, CompareSubjects);
        for (int i = 0; i < found.Length; i++)
            RegisterSubject(found[i], false);
        PurgeOrphanSubjectLeases();
        BumpRevision();
    }

    public bool RegisterSubject(BistroBuilderSpatialSubject subject)
    {
        return RegisterSubject(subject, true);
    }

    private bool RegisterSubject(BistroBuilderSpatialSubject subject, bool bump)
    {
        if (subject == null || string.IsNullOrWhiteSpace(subject.SubjectId)) return false;
        if (subjects.TryGetValue(subject.SubjectId, out BistroBuilderSpatialSubject existing) &&
            existing != null && existing != subject)
            return false;
        if (subjects.TryGetValue(
                subject.SubjectId,
                out BistroBuilderSpatialSubject stale) &&
            stale == null)
            ReleaseSubjectLeases(subject.SubjectId);
        subjects[subject.SubjectId] = subject;
        if (bump) BumpRevision();
        return true;
    }

    public void UnregisterSubject(BistroBuilderSpatialSubject subject)
    {
        if (subject == null || string.IsNullOrWhiteSpace(subject.SubjectId)) return;
        if (subjects.TryGetValue(subject.SubjectId, out BistroBuilderSpatialSubject current) &&
            current == subject)
        {
            subjects.Remove(subject.SubjectId);
            ReleaseSubjectLeases(subject.SubjectId);
            BumpRevision();
        }
    }

    public bool TryGetSubject(string subjectId, out BistroBuilderSpatialSubject subject)
    {
        subject = null;
        if (string.IsNullOrWhiteSpace(subjectId)) return false;
        if (!subjects.TryGetValue(subjectId, out subject) || subject == null)
        {
            bool removed = subjects.Remove(subjectId);
            int released = ReleaseSubjectLeases(subjectId);
            if (removed || released > 0)
                BumpRevision();
            subject = null;
            return false;
        }
        return true;
    }

    public bool TryBeginEpisode(
        string ownerId,
        string purposeId,
        string subjectId,
        out BistroBuilderSpatialEpisode episode)
    {
        episode = null;
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(purposeId))
            return false;
        string id = "episode:" + nextSequence.ToString("D12");
        episode = new BistroBuilderSpatialEpisode
        {
            episodeId = id,
            ownerId = ownerId,
            purposeId = purposeId,
            subjectId = subjectId ?? string.Empty,
            state = BistroBuilderSpatialEpisodeState.Active,
            sequence = nextSequence++
        };
        episodes[id] = episode;
        EpisodeChanged?.Invoke(episode);
        return true;
    }

    public bool EndEpisode(string episodeId, BistroBuilderSpatialEpisodeState finalState)
    {
        if (string.IsNullOrWhiteSpace(episodeId) ||
            !episodes.TryGetValue(episodeId, out BistroBuilderSpatialEpisode episode) ||
            episode == null)
            return false;
        episode.state = finalState == BistroBuilderSpatialEpisodeState.Active
            ? BistroBuilderSpatialEpisodeState.Completed
            : finalState;
        ReleaseEpisodeLeases(episodeId);
        episodes.Remove(episodeId);
        EpisodeChanged?.Invoke(episode);
        return true;
    }

    public bool TryAcquireLease(
        BistroBuilderSpatialClaimRequest request,
        out BistroBuilderSpatialLease lease,
        out BistroBuilderSpatialLeaseDecision decision)
    {
        lease = null;
        decision = new BistroBuilderSpatialLeaseDecision();
        CleanupExpiredLeases();
        if (!ValidateRequest(request, decision))
        {
            RejectedLeaseCount++;
            return false;
        }

        if (request.validateAgainstStaticGeometry &&
            TryFindStaticGeometryConflict(
                request.volume,
                request.subjectId,
                request.relatedSubjectId,
                out string blockingSubjectId))
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.StaticGeometryConflict;
            decision.blockingSubjectId = blockingSubjectId;
            decision.message = "Conflicto con geometría espacial estática del subject " +
                               blockingSubjectId + ".";
            RejectedLeaseCount++;
            return false;
        }

        BistroBuilderSpatialLease blockingLease = null;
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
        {
            BistroBuilderSpatialLease other = pair.Value;
            if (other == null || string.Equals(other.ownerId, request.ownerId, StringComparison.Ordinal))
                continue;
            if (!request.volume.Overlaps(other.volume)) continue;
            if (!Conflicts(request.conflictMode, other.conflictMode)) continue;
            if (blockingLease == null ||
                CompareLeasesDeterministically(other, blockingLease) < 0)
                blockingLease = other;
        }
        if (blockingLease != null)
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.Conflict;
            decision.blockingLeaseId = blockingLease.leaseId;
            decision.message = "Conflicto espacial con lease activo " +
                               blockingLease.leaseId + ".";
            RejectedLeaseCount++;
            return false;
        }
        string leaseId = "lease:" + nextSequence.ToString("D12");
        lease = new BistroBuilderSpatialLease
        {
            leaseId = leaseId,
            ownerId = request.ownerId,
            subjectId = request.subjectId ?? string.Empty,
            portId = request.portId ?? string.Empty,
            episodeId = request.episodeId ?? string.Empty,
            kind = request.kind,
            conflictMode = request.conflictMode,
            volume = request.volume,
            priority = request.priority,
            sequence = nextSequence++,
            expiresAtUnscaled = request.durationSeconds > 0f
                ? Time.unscaledTime + request.durationSeconds
                : 0f
        };
        leases[leaseId] = lease;
        GrantedLeaseCount++;
        decision.granted = true;
        decision.failure = BistroBuilderSpatialLeaseFailure.None;
        decision.message = "Lease espacial concedido.";
        LeaseGranted?.Invoke(lease);
        BumpRevision();
        return true;
    }

    public bool RefreshLease(string leaseId, float durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(leaseId) ||
            !leases.TryGetValue(leaseId, out BistroBuilderSpatialLease lease) || lease == null)
            return false;
        lease.expiresAtUnscaled = durationSeconds > 0f
            ? Time.unscaledTime + durationSeconds
            : 0f;
        return true;
    }

    public bool TryReservePointWithAlternates(
        string ownerId,
        BistroBuilderSpatialClaimKind kind,
        Vector3 preferred,
        float radius,
        int priority,
        float durationSeconds,
        Func<Vector3, bool> candidateValidator,
        out Vector3 reserved,
        out string leaseId)
    {
        reserved = preferred;
        leaseId = string.Empty;
        float r = Mathf.Max(0.05f, radius);
        if (TryAcquirePointLease(ownerId, kind, preferred, r, priority,
                durationSeconds, out leaseId))
            return true;

        for (int ring = 1; ring <= alternateDestinationRings; ring++)
        {
            float distance = r + ring * alternateDestinationStep;
            for (int i = 0; i < alternateDestinationSamples; i++)
            {
                float angle = i * Mathf.PI * 2f / alternateDestinationSamples;
                Vector3 candidate = preferred +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (candidateValidator != null && !candidateValidator(candidate)) continue;
                if (!TryAcquirePointLease(ownerId, kind, candidate, r, priority,
                        durationSeconds, out leaseId))
                    continue;
                reserved = candidate;
                return true;
            }
        }
        return false;
    }

    public bool TryAcquirePointLease(
        string ownerId,
        BistroBuilderSpatialClaimKind kind,
        Vector3 point,
        float radius,
        int priority,
        float durationSeconds,
        out string leaseId)
    {
        leaseId = string.Empty;
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId ?? string.Empty,
            kind = kind,
            conflictMode = BistroBuilderSpatialConflictMode.Reservable,
            volume = BistroBuilderSpatialVolume.Circle(point, radius),
            priority = priority,
            durationSeconds = durationSeconds
        };
        if (!TryAcquireLease(request, out BistroBuilderSpatialLease lease, out _))
            return false;
        leaseId = lease.leaseId;
        return true;
    }

    public bool RefreshOwnerLease(
        string ownerId,
        BistroBuilderSpatialClaimKind kind,
        float durationSeconds)
    {
        string bestId = string.Empty;
        long bestSequence = long.MinValue;
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
        {
            BistroBuilderSpatialLease lease = pair.Value;
            if (lease == null || lease.kind != kind ||
                !string.Equals(lease.ownerId, ownerId, StringComparison.Ordinal))
                continue;
            if (lease.sequence > bestSequence)
            {
                bestSequence = lease.sequence;
                bestId = pair.Key;
            }
        }
        return !string.IsNullOrEmpty(bestId) && RefreshLease(bestId, durationSeconds);
    }

    public int ReleaseOwnerLeases(string ownerId, BistroBuilderSpatialClaimKind? kind = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return 0;
        scratchIds.Clear();
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
        {
            BistroBuilderSpatialLease lease = pair.Value;
            if (lease == null ||
                !string.Equals(lease.ownerId, ownerId, StringComparison.Ordinal))
                continue;
            if (kind.HasValue && lease.kind != kind.Value) continue;
            scratchIds.Add(pair.Key);
        }
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);
        if (scratchIds.Count > 0) BumpRevision();
        return scratchIds.Count;
    }
    public bool ReleaseLease(string leaseId)
    {
        if (string.IsNullOrWhiteSpace(leaseId) || !leases.ContainsKey(leaseId)) return false;
        ReleaseLeaseInternal(leaseId);
        BumpRevision();
        return true;
    }

    public int CountLeases(BistroBuilderSpatialClaimKind kind)
    {
        CleanupExpiredLeases();
        int count = 0;
        foreach (BistroBuilderSpatialLease lease in leases.Values)
            if (lease != null && lease.kind == kind) count++;
        return count;
    }

    public bool BlocksTraversalPoint(Vector3 point, float radius, string requesterId)
    {
        CleanupExpiredLeases();
        BistroBuilderSpatialVolume probe = BistroBuilderSpatialVolume.Circle(point, radius);
        foreach (BistroBuilderSpatialLease lease in leases.Values)
        {
            if (lease == null || string.Equals(lease.ownerId, requesterId, StringComparison.Ordinal))
                continue;
            if (lease.conflictMode == BistroBuilderSpatialConflictMode.Compatible ||
                lease.conflictMode == BistroBuilderSpatialConflictMode.Degrade)
                continue;
            if (lease.kind != BistroBuilderSpatialClaimKind.DynamicSweep &&
                lease.kind != BistroBuilderSpatialClaimKind.Mobility &&
                lease.kind != BistroBuilderSpatialClaimKind.Carry &&
                lease.kind != BistroBuilderSpatialClaimKind.TraversalGate)
                continue;
            if (probe.Overlaps(lease.volume)) return true;
        }
        return false;
    }

    public bool IsPointReserved(
        Vector3 point,
        float radius,
        string requesterId,
        BistroBuilderSpatialClaimKind kind)
    {
        CleanupExpiredLeases();
        BistroBuilderSpatialVolume probe = BistroBuilderSpatialVolume.Circle(point, radius);
        foreach (BistroBuilderSpatialLease lease in leases.Values)
        {
            if (lease == null || lease.kind != kind ||
                string.Equals(lease.ownerId, requesterId, StringComparison.Ordinal))
                continue;
            if (probe.Overlaps(lease.volume)) return true;
        }
        return false;
    }

    public BistroBuilderSpatialRuntimeSnapshot CaptureRuntimeSnapshot()
    {
        return new BistroBuilderSpatialRuntimeSnapshot
        {
            topologyRevision = Revision
        };
    }

    public void ResetTransientRuntimeStateAfterLoad()
    {
        scratchIds.Clear();
        foreach (string leaseId in leases.Keys)
            scratchIds.Add(leaseId);
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);

        scratchIds.Clear();
        foreach (string episodeId in episodes.Keys)
            scratchIds.Add(episodeId);
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            if (episodes.TryGetValue(
                    scratchIds[i],
                    out BistroBuilderSpatialEpisode episode) &&
                episode != null)
            {
                episode.state = BistroBuilderSpatialEpisodeState.Cancelled;
                EpisodeChanged?.Invoke(episode);
            }
        episodes.Clear();
        RebuildSubjects();
    }
    public bool TryFindStaticGeometryConflict(
        BistroBuilderSpatialVolume volume,
        string ownerSubjectId,
        string relatedSubjectId,
        out string blockingSubjectId)
    {
        blockingSubjectId = string.Empty;
        scratchSubjectIds.Clear();
        foreach (string id in subjects.Keys)
            scratchSubjectIds.Add(id);
        scratchSubjectIds.Sort(StringComparer.Ordinal);

        for (int subjectIndex = 0; subjectIndex < scratchSubjectIds.Count; subjectIndex++)
        {
            string id = scratchSubjectIds[subjectIndex];
            if (string.Equals(id, ownerSubjectId, StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(relatedSubjectId) &&
                 string.Equals(id, relatedSubjectId, StringComparison.Ordinal)))
                continue;

            if (!subjects.TryGetValue(id, out BistroBuilderSpatialSubject subject) ||
                subject == null || subject.Proxy == null)
                continue;

            scratchVolumes.Clear();
            subject.Proxy.BuildWorldVolumes(
                BistroBuilderSpatialProxyLayer.Static,
                scratchVolumes);
            for (int volumeIndex = 0; volumeIndex < scratchVolumes.Count; volumeIndex++)
            {
                if (!volume.Overlaps(scratchVolumes[volumeIndex])) continue;
                blockingSubjectId = id;
                return true;
            }
        }

        return false;
    }
    private bool ValidateRequest(
        BistroBuilderSpatialClaimRequest request,
        BistroBuilderSpatialLeaseDecision decision)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ownerId))
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.InvalidRequest;
            decision.message = "Claim sin ownerId estable.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(request.episodeId))
        {
            if (!episodes.TryGetValue(request.episodeId, out BistroBuilderSpatialEpisode episode) ||
                episode == null || episode.state != BistroBuilderSpatialEpisodeState.Active ||
                !string.Equals(episode.ownerId, request.ownerId, StringComparison.Ordinal))
            {
                decision.failure = BistroBuilderSpatialLeaseFailure.EpisodeUnavailable;
                decision.message = "Spatial Episode inexistente, cerrado o de otro propietario.";
                return false;
            }
        }
        if (!string.IsNullOrWhiteSpace(request.subjectId) &&
            !subjects.ContainsKey(request.subjectId))
        {
            decision.failure = BistroBuilderSpatialLeaseFailure.SubjectUnavailable;
            decision.message = "Spatial Subject no registrado: " + request.subjectId;
            return false;
        }
        return true;
    }

    private static bool Conflicts(
        BistroBuilderSpatialConflictMode first,
        BistroBuilderSpatialConflictMode second)
    {
        if (first == BistroBuilderSpatialConflictMode.Compatible ||
            second == BistroBuilderSpatialConflictMode.Compatible)
            return false;
        if (first == BistroBuilderSpatialConflictMode.Degrade &&
            second == BistroBuilderSpatialConflictMode.Degrade)
            return false;
        if (first == BistroBuilderSpatialConflictMode.Degrade ||
            second == BistroBuilderSpatialConflictMode.Degrade)
            return false;
        return true;
    }

    private void CleanupExpiredLeases()
    {
        if (leases.Count == 0) return;
        float now = Time.unscaledTime;
        scratchIds.Clear();
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
        {
            BistroBuilderSpatialLease lease = pair.Value;
            if (lease == null ||
                (lease.expiresAtUnscaled > 0f && lease.expiresAtUnscaled <= now))
                scratchIds.Add(pair.Key);
        }
        if (scratchIds.Count == 0) return;
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);
        BumpRevision();
    }

    private void ReleaseEpisodeLeases(string episodeId)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
            if (pair.Value != null &&
                string.Equals(pair.Value.episodeId, episodeId, StringComparison.Ordinal))
                scratchIds.Add(pair.Key);
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);
        if (scratchIds.Count > 0) BumpRevision();
    }

    private int PurgeOrphanSubjectLeases()
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
        {
            BistroBuilderSpatialLease lease = pair.Value;
            if (lease == null || string.IsNullOrWhiteSpace(lease.subjectId))
                continue;
            if (!subjects.ContainsKey(lease.subjectId))
                scratchIds.Add(pair.Key);
        }
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);
        return scratchIds.Count;
    }
    private int ReleaseSubjectLeases(string subjectId)
    {
        scratchIds.Clear();
        foreach (KeyValuePair<string, BistroBuilderSpatialLease> pair in leases)
            if (pair.Value != null &&
                string.Equals(pair.Value.subjectId, subjectId, StringComparison.Ordinal))
                scratchIds.Add(pair.Key);
        scratchIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < scratchIds.Count; i++)
            ReleaseLeaseInternal(scratchIds[i]);
        return scratchIds.Count;
    }
    private void ReleaseLeaseInternal(string leaseId)
    {
        if (!leases.Remove(leaseId)) return;
        LeaseReleased?.Invoke(leaseId);
    }

    private void BumpRevision()
    {
        Revision = Revision == int.MaxValue ? 1 : Revision + 1;
        SpatialTopologyChanged?.Invoke(Revision);
    }

    private static int CompareLeasesDeterministically(
        BistroBuilderSpatialLease first,
        BistroBuilderSpatialLease second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int bySequence = first.sequence.CompareTo(second.sequence);
        return bySequence != 0
            ? bySequence
            : string.CompareOrdinal(first.leaseId, second.leaseId);
    }
    private static int CompareSubjects(
        BistroBuilderSpatialSubject first,
        BistroBuilderSpatialSubject second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int byId = string.CompareOrdinal(first.SubjectId, second.SubjectId);
        if (byId != 0) return byId;
        return first.GetInstanceID().CompareTo(second.GetInstanceID());
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderSpatialFamilyCatalog catalog)
    {
        familyCatalog = catalog;
    }
#endif
}
