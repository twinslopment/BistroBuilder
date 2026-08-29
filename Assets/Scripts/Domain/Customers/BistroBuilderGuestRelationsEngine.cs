using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras de reputación y retorno. No conoce Unity ni Marketing.
/// </summary>
public static class BistroBuilderGuestRelationsEngine
{
    public const int MaximumReputationPoints = 100;
    public const int MaximumCohorts = 128;
    public const int MaximumAppliedReputationSources = 512;
    public const int ReputationDemandBasisPointsPerPoint = 100;
    public const int MaximumReputationDemandBasisPoints = 5000;
    public const int RepeatVisitOpportunityPool = 7;
    public const int MaximumReturnGroupsPerService = 3;

    public static BistroBuilderGuestRelationsSnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderGuestRelationsSnapshot
        {
            schemaId = BistroBuilderGuestRelationsSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderGuestRelationsSnapshot.CurrentSchemaVersion,
            revision = 0L,
            reputationPoints = 0,
            nextCohortSequence = 1,
            appliedReputationSourceIds = new List<string>(),
            cohorts = new List<BistroBuilderGuestVisitCohortRecord>()
        };
    }
    public static bool TryValidateSnapshot(
        BistroBuilderGuestRelationsSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId,
                BistroBuilderGuestRelationsSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion !=
                BistroBuilderGuestRelationsSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L || snapshot.reputationPoints < 0 ||
            snapshot.reputationPoints > MaximumReputationPoints ||
            snapshot.nextCohortSequence < 1 ||
            snapshot.appliedReputationSourceIds == null ||
            snapshot.cohorts == null ||
            snapshot.appliedReputationSourceIds.Count >
                MaximumAppliedReputationSources ||
            snapshot.cohorts.Count > MaximumCohorts)
        {
            error = "guest_relations.state contiene una cabecera inválida.";
            return false;
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.appliedReputationSourceIds.Count; i++)
        {
            string id = NormalizeId(snapshot.appliedReputationSourceIds[i]);
            if (!IsSafeId(id) || !sourceIds.Add(id))
            {
                error = "Existe una fuente de reputación inválida o duplicada.";
                return false;
            }
        }

        var cohortIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.cohorts.Count; i++)
        {
            BistroBuilderGuestVisitCohortRecord cohort = snapshot.cohorts[i];
            string cohortId = cohort != null ? NormalizeId(cohort.cohortId) : string.Empty;
            string segmentId = cohort != null ? NormalizeId(cohort.segmentId) : string.Empty;
            if (cohort == null || !IsSafeId(cohortId) || !cohortIds.Add(cohortId) ||
                !IsSafeId(segmentId) || cohort.partySize < 1 || cohort.partySize > 32 ||
                cohort.visitCount < 1 || cohort.lastVisitDay < 1)
            {
                error = "Existe una cohorte de clientes inválida o duplicada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static int ComputeReputationDemandBasisPoints(int reputationPoints)
    {
        int safe = Math.Max(0, Math.Min(MaximumReputationPoints, reputationPoints));
        return Math.Min(
            MaximumReputationDemandBasisPoints,
            safe * ReputationDemandBasisPointsPerPoint);
    }

    public static int ConvertRepeatVisitBasisPointsToCount(
        int basisPoints,
        int eligibleCohorts,
        int availableSlots)
    {
        if (basisPoints <= 0 || eligibleCohorts <= 0 || availableSlots <= 0)
            return 0;

        int safeBasisPoints = Math.Min(50000, basisPoints);
        double expected = RepeatVisitOpportunityPool *
            safeBasisPoints / 10000.0;
        int count = (int)Math.Round(expected, MidpointRounding.AwayFromZero);
        return Math.Max(
            0,
            Math.Min(
                MaximumReturnGroupsPerService,
                Math.Min(eligibleCohorts, Math.Min(availableSlots, count))));
    }

    public static bool TryApplyReputationCredit(
        BistroBuilderGuestRelationsSnapshot source,
        string sourceId,
        int points,
        out BistroBuilderGuestRelationsSnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error))
            return false;

        string normalizedSource = NormalizeId(sourceId);
        if (!IsSafeId(normalizedSource) || points < -100 || points > 100)
        {
            error = "La aportación de reputación es inválida.";
            return false;
        }

        candidate = source.DeepClone();
        if (candidate.appliedReputationSourceIds.Contains(normalizedSource))
        {
            error = string.Empty;
            return true;
        }

        candidate.appliedReputationSourceIds.Add(normalizedSource);
        if (candidate.appliedReputationSourceIds.Count >
            MaximumAppliedReputationSources)
        {
            candidate.appliedReputationSourceIds.RemoveAt(0);
        }

        candidate.reputationPoints = Math.Max(
            0,
            Math.Min(MaximumReputationPoints,
                candidate.reputationPoints + points));
        candidate.revision = checked(source.revision + 1L);
        changed = true;
        return TryValidateSnapshot(candidate, out error);
    }

    public static bool TryRecordCompletedVisit(
        BistroBuilderGuestRelationsSnapshot source,
        string segmentId,
        int partySize,
        int dayIndex,
        string returningCohortId,
        out BistroBuilderGuestRelationsSnapshot candidate,
        out string cohortId,
        out string error)
    {
        candidate = null;
        cohortId = string.Empty;
        if (!TryValidateSnapshot(source, out error))
            return false;

        string segment = NormalizeId(segmentId);
        if (!IsSafeId(segment)) segment = "general";
        string returningId = NormalizeId(returningCohortId);
        if (partySize < 1 || partySize > 32 || dayIndex < 1)
        {
            error = "La visita completada contiene datos inválidos.";
            return false;
        }

        candidate = source.DeepClone();
        BistroBuilderGuestVisitCohortRecord existing = null;
        if (IsSafeId(returningId))
        {
            for (int i = 0; i < candidate.cohorts.Count; i++)
            {
                if (string.Equals(
                        NormalizeId(candidate.cohorts[i].cohortId),
                        returningId,
                        StringComparison.Ordinal))
                {
                    existing = candidate.cohorts[i];
                    break;
                }
            }
        }

        if (existing != null)
        {
            existing.segmentId = segment;
            existing.partySize = partySize;
            existing.visitCount = checked(existing.visitCount + 1);
            existing.lastVisitDay = dayIndex;
            cohortId = existing.cohortId;
        }
        else
        {
            cohortId = "guest_cohort_" +
                candidate.nextCohortSequence.ToString("D6");
            candidate.nextCohortSequence = checked(
                candidate.nextCohortSequence + 1);
            candidate.cohorts.Add(new BistroBuilderGuestVisitCohortRecord
            {
                cohortId = cohortId,
                segmentId = segment,
                partySize = partySize,
                visitCount = 1,
                lastVisitDay = dayIndex
            });
        }

        while (candidate.cohorts.Count > MaximumCohorts)
        {
            int oldestIndex = 0;
            for (int i = 1; i < candidate.cohorts.Count; i++)
            {
                BistroBuilderGuestVisitCohortRecord left = candidate.cohorts[i];
                BistroBuilderGuestVisitCohortRecord right = candidate.cohorts[oldestIndex];
                if (left.lastVisitDay < right.lastVisitDay ||
                    (left.lastVisitDay == right.lastVisitDay &&
                     string.CompareOrdinal(left.cohortId, right.cohortId) < 0))
                    oldestIndex = i;
            }
            candidate.cohorts.RemoveAt(oldestIndex);
        }

        candidate.revision = checked(source.revision + 1L);
        return TryValidateSnapshot(candidate, out error);
    }

    public static void CopyEligibleCohorts(
        BistroBuilderGuestRelationsSnapshot snapshot,
        int dayIndex,
        List<BistroBuilderGuestVisitCohortRecord> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (snapshot == null || dayIndex < 1)
            return;

        for (int i = 0; i < snapshot.cohorts.Count; i++)
        {
            BistroBuilderGuestVisitCohortRecord cohort = snapshot.cohorts[i];
            if (cohort != null && cohort.lastVisitDay < dayIndex)
                destination.Add(cohort.DeepClone());
        }

        destination.Sort((left, right) =>
        {
            int day = right.lastVisitDay.CompareTo(left.lastVisitDay);
            return day != 0
                ? day
                : string.CompareOrdinal(left.cohortId, right.cohortId);
        });
    }

    public static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsSafeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
            return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool allowed =
                c >= 'a' && c <= 'z' ||
                c >= '0' && c <= '9' ||
                c == '_' || c == '-' || c == '.';
            if (!allowed) return false;
        }
        return true;
    }
}
