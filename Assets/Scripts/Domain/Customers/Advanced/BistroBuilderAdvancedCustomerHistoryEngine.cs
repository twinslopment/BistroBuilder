using System;
using System.Collections.Generic;

public static class BistroBuilderAdvancedCustomerHistoryEngine
{
    public const int MaximumCustomerRecords = 128;
    public const int MaximumRecentVisitsPerCustomer = 20;
    public const int RegularVisitThreshold = 3;
    public const int VipVisitThreshold = 5;
    public const int VipSatisfactionThresholdBasisPoints = 7500;
    public const long VipLifetimeSpendThresholdCents = 25000L;

    public static BistroBuilderAdvancedCustomerHistorySnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderAdvancedCustomerHistorySnapshot();
    }

    public static bool TryValidateSnapshot(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        out string error)
    {
        error = string.Empty;
        if (snapshot == null ||
            snapshot.schemaId != BistroBuilderAdvancedCustomerHistorySnapshot.CurrentSchemaId ||
            snapshot.schemaVersion != BistroBuilderAdvancedCustomerHistorySnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0 || snapshot.customers == null ||
            snapshot.pendingOutcomes == null || snapshot.pendingAssignments == null ||
            snapshot.customers.Count > MaximumCustomerRecords)
        {
            error = "advanced_customers.state contiene una cabecera inválida.";
            return false;
        }
        var cohortIds = new HashSet<string>(StringComparer.Ordinal);
        var experienceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.customers.Count; i++)
        {
            BistroBuilderAdvancedCustomerHistoryRecord record = snapshot.customers[i];
            if (record == null || !IsSafeId(record.cohortId) ||
                !cohortIds.Add(record.cohortId) || !IsSafeId(record.segmentId) ||
                record.firstVisitDay < 1 || record.lastVisitDay < record.firstVisitDay ||
                record.visitCount < 1 || record.lifetimeSpendCents < 0 ||
                record.satisfactionTotalBasisPoints < 0 ||
                record.recentVisits == null ||
                record.recentVisits.Count > MaximumRecentVisitsPerCustomer ||
                !Enum.IsDefined(typeof(BistroBuilderCustomerLoyaltyTier), record.loyaltyTier))
            {
                error = "Existe un historial de cliente inválido o duplicado.";
                return false;
            }

            for (int r = 0; r < record.recentVisits.Count; r++)
            {
                BistroBuilderAdvancedCustomerVisitHistoryRecord visit = record.recentVisits[r];
                if (visit == null || !IsSafeId(visit.experienceId) ||
                    !experienceIds.Add(visit.experienceId) || visit.dayIndex < 1 ||
                    visit.paidAmountCents < 0 || !ValidScore(visit.satisfactionBasisPoints) ||
                    !ValidScore(visit.waitingBasisPoints) ||
                    !ValidScore(visit.foodQualityBasisPoints) || !ValidScore(visit.valueBasisPoints))
                {
                    error = "Existe una visita histórica inválida o duplicada.";
                    return false;
                }
            }
        }

        var pendingGroups = new HashSet<int>();
        for (int i = 0; i < snapshot.pendingOutcomes.Count; i++)
        {
            BistroBuilderAdvancedCustomerVisitOutcome outcome = snapshot.pendingOutcomes[i];
            if (!TryValidateOutcome(outcome, out error) ||
                !pendingGroups.Add(outcome.groupId))
                return false;
        }

        pendingGroups.Clear();
        for (int i = 0; i < snapshot.pendingAssignments.Count; i++)
        {
            BistroBuilderAdvancedCustomerCohortAssignment assignment =
                snapshot.pendingAssignments[i];
            if (!TryValidateAssignment(assignment, out error) ||
                !pendingGroups.Add(assignment.groupId))
                return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryRegisterOutcome(
        BistroBuilderAdvancedCustomerHistorySnapshot source,
        BistroBuilderAdvancedCustomerVisitOutcome outcome,
        out BistroBuilderAdvancedCustomerHistorySnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error) ||
            !TryValidateOutcome(outcome, out error))
            return false;
        candidate = source.DeepClone();
        if (ContainsExperience(candidate, outcome.experienceId))
        {
            error = string.Empty;
            return true;
        }

        int index = FindOutcome(candidate.pendingOutcomes, outcome.groupId);
        if (index >= 0)
        {
            if (candidate.pendingOutcomes[index].experienceId == outcome.experienceId)
            {
                error = string.Empty;
                return true;
            }
            candidate.pendingOutcomes[index] = outcome.DeepClone();
        }
        else
        {
            candidate.pendingOutcomes.Add(outcome.DeepClone());
        }

        changed = true;
        if (!TryReconcile(candidate, outcome.groupId, out error))
            return false;
        candidate.revision = checked(source.revision + 1L);
        return TryValidateSnapshot(candidate, out error);
    }

    public static bool TryRegisterAssignment(
        BistroBuilderAdvancedCustomerHistorySnapshot source,
        BistroBuilderAdvancedCustomerCohortAssignment assignment,
        out BistroBuilderAdvancedCustomerHistorySnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error) ||
            !TryValidateAssignment(assignment, out error))
            return false;
        candidate = source.DeepClone();
        int index = FindAssignment(candidate.pendingAssignments, assignment.groupId);
        if (index >= 0)
        {
            if (candidate.pendingAssignments[index].cohortId == assignment.cohortId)
            {
                error = string.Empty;
                return true;
            }
            candidate.pendingAssignments[index] = assignment.DeepClone();
        }
        else
        {
            candidate.pendingAssignments.Add(assignment.DeepClone());
        }

        changed = true;
        if (!TryReconcile(candidate, assignment.groupId, out error))
            return false;
        candidate.revision = checked(source.revision + 1L);
        return TryValidateSnapshot(candidate, out error);
    }

    public static bool TryGetCustomer(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        string cohortId,
        out BistroBuilderAdvancedCustomerHistoryRecord record)
    {
        record = null;
        if (snapshot?.customers == null)
            return false;
        string normalized = NormalizeId(cohortId);
        for (int i = 0; i < snapshot.customers.Count; i++)
        {
            if (snapshot.customers[i] != null &&
                snapshot.customers[i].cohortId == normalized)
            {
                record = snapshot.customers[i].DeepClone();
                return true;
            }
        }
        return false;
    }

    private static bool TryReconcile(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        int groupId,
        out string error)
    {
        error = string.Empty;
        int outcomeIndex = FindOutcome(snapshot.pendingOutcomes, groupId);
        int assignmentIndex = FindAssignment(snapshot.pendingAssignments, groupId);
        if (outcomeIndex < 0 || assignmentIndex < 0)
            return true;

        BistroBuilderAdvancedCustomerVisitOutcome outcome =
            snapshot.pendingOutcomes[outcomeIndex];
        BistroBuilderAdvancedCustomerCohortAssignment assignment =
            snapshot.pendingAssignments[assignmentIndex];
        if (!TryCommitVisit(snapshot, outcome, assignment, out error))
            return false;

        snapshot.pendingOutcomes.RemoveAt(outcomeIndex);
        assignmentIndex = FindAssignment(snapshot.pendingAssignments, groupId);
        if (assignmentIndex >= 0)
            snapshot.pendingAssignments.RemoveAt(assignmentIndex);
        return true;
    }

    private static bool TryCommitVisit(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        BistroBuilderAdvancedCustomerVisitOutcome outcome,
        BistroBuilderAdvancedCustomerCohortAssignment assignment,
        out string error)
    {
        error = string.Empty;
        BistroBuilderAdvancedCustomerHistoryRecord record = null;
        for (int i = 0; i < snapshot.customers.Count; i++)
            if (snapshot.customers[i].cohortId == assignment.cohortId)
                record = snapshot.customers[i];
        if (record == null)
        {
            record = new BistroBuilderAdvancedCustomerHistoryRecord
            {
                cohortId = assignment.cohortId,
                segmentId = assignment.segmentId,
                firstVisitDay = assignment.dayIndex,
                lastVisitDay = assignment.dayIndex
            };
            snapshot.customers.Add(record);
        }

        for (int i = 0; i < record.recentVisits.Count; i++)
        {
            if (record.recentVisits[i].experienceId == outcome.experienceId)
                return true;
        }

        record.segmentId = assignment.segmentId;
        record.lastVisitDay = Math.Max(record.lastVisitDay, assignment.dayIndex);
        record.visitCount = checked(record.visitCount + 1);
        record.lifetimeSpendCents = checked(
            record.lifetimeSpendCents + outcome.paidAmountCents);
        record.satisfactionTotalBasisPoints = checked(
            record.satisfactionTotalBasisPoints +
            outcome.overallSatisfactionBasisPoints);
        record.recentVisits.Add(new BistroBuilderAdvancedCustomerVisitHistoryRecord
        {
            experienceId = outcome.experienceId,
            dayIndex = outcome.dayIndex,
            paidAmountCents = outcome.paidAmountCents,
            satisfactionBasisPoints = outcome.overallSatisfactionBasisPoints,
            waitingBasisPoints = outcome.waitingScoreBasisPoints,
            foodQualityBasisPoints = outcome.foodQualityScoreBasisPoints,
            valueBasisPoints = outcome.valueForMoneyScoreBasisPoints
        });
        while (record.recentVisits.Count > MaximumRecentVisitsPerCustomer)
            record.recentVisits.RemoveAt(0);

        record.loyaltyTier = ResolveLoyaltyTier(record);

        while (snapshot.customers.Count > MaximumCustomerRecords)
        {
            int oldestIndex = 0;
            for (int i = 1; i < snapshot.customers.Count; i++)
            {
                BistroBuilderAdvancedCustomerHistoryRecord left = snapshot.customers[i];
                BistroBuilderAdvancedCustomerHistoryRecord right = snapshot.customers[oldestIndex];
                if (left.lastVisitDay < right.lastVisitDay)
                    oldestIndex = i;
            }
            snapshot.customers.RemoveAt(oldestIndex);
        }

        return true;
    }

    public static BistroBuilderCustomerLoyaltyTier ResolveLoyaltyTier(
        BistroBuilderAdvancedCustomerHistoryRecord record)
    {
        if (record == null || record.visitCount <= 1)
            return BistroBuilderCustomerLoyaltyTier.New;
        if (record.visitCount >= VipVisitThreshold &&
            record.AverageSatisfactionBasisPoints >= VipSatisfactionThresholdBasisPoints &&
            record.lifetimeSpendCents >= VipLifetimeSpendThresholdCents)
            return BistroBuilderCustomerLoyaltyTier.Vip;
        if (record.visitCount >= RegularVisitThreshold)
            return BistroBuilderCustomerLoyaltyTier.Regular;
        return BistroBuilderCustomerLoyaltyTier.Returning;
    }

    public static bool TryValidateOutcome(
        BistroBuilderAdvancedCustomerVisitOutcome outcome,
        out string error)
    {
        if (outcome == null || outcome.groupId < 1 ||
            !IsSafeId(outcome.experienceId) || !IsSafeId(outcome.segmentId) ||
            outcome.dayIndex < 1 || outcome.paidAmountCents < 0 ||
            !ValidScore(outcome.overallSatisfactionBasisPoints) ||
            !ValidScore(outcome.waitingScoreBasisPoints) ||
            !ValidScore(outcome.foodQualityScoreBasisPoints) ||
            !ValidScore(outcome.valueForMoneyScoreBasisPoints))
        {
            error = "El resultado pendiente de una visita avanzada es inválido.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public static bool TryValidateAssignment(
        BistroBuilderAdvancedCustomerCohortAssignment assignment,
        out string error)
    {
        if (assignment == null || assignment.groupId < 1 ||
            !IsSafeId(assignment.cohortId) || !IsSafeId(assignment.segmentId) ||
            assignment.dayIndex < 1)
        {
            error = "La asignación de cohorte avanzada es inválida.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool ContainsExperience(
        BistroBuilderAdvancedCustomerHistorySnapshot snapshot,
        string experienceId)
    {
        if (snapshot?.customers == null)
            return false;
        for (int i = 0; i < snapshot.customers.Count; i++)
        {
            BistroBuilderAdvancedCustomerHistoryRecord record = snapshot.customers[i];
            if (record?.recentVisits == null) continue;
            for (int r = 0; r < record.recentVisits.Count; r++)
                if (record.recentVisits[r] != null &&
                    record.recentVisits[r].experienceId == experienceId)
                    return true;
        }
        return false;
    }

    private static int FindOutcome(
        IReadOnlyList<BistroBuilderAdvancedCustomerVisitOutcome> values,
        int groupId)
    {
        if (values == null) return -1;
        for (int i = 0; i < values.Count; i++)
            if (values[i] != null && values[i].groupId == groupId) return i;
        return -1;
    }

    private static int FindAssignment(
        IReadOnlyList<BistroBuilderAdvancedCustomerCohortAssignment> values,
        int groupId)
    {
        if (values == null) return -1;
        for (int i = 0; i < values.Count; i++)
            if (values[i] != null && values[i].groupId == groupId) return i;
        return -1;
    }

    private static bool ValidScore(int value) => value >= 0 && value <= 10000;
    private static string NormalizeId(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool IsSafeId(string value)
    {
        string normalized = NormalizeId(value);
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 96) return false;
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            bool allowed = c >= 'a' && c <= 'z' || c >= '0' && c <= '9' ||
                c == '_' || c == '-' || c == '.';
            if (!allowed) return false;
        }
        return true;
    }
}
