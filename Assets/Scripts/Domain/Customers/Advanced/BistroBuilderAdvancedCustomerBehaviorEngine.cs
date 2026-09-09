using System;

/// <summary>
/// Traduce espera objetiva + paciencia individual en reacción conductual.
/// Es puro: no cambia estados de servicio ni decide abandonos.
/// </summary>
public static class BistroBuilderAdvancedCustomerBehaviorEngine
{
    public static bool TryEvaluate(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderAdvancedCustomerGroupProfile profile,
        CustomerGroupState state,
        out BistroBuilderAdvancedCustomerGroupBehavior behavior,
        out string error)
    {
        behavior = null;
        if (!BistroBuilderCustomerExperienceEvaluator.TryValidateRuntimeVisit(
                visit, out error) ||
            !BistroBuilderAdvancedCustomerProfileEngine.TryValidateGroupProfile(
                profile, visit.partySize, out error) ||
            visit.groupId != profile.groupId)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "10F recibió visita/perfil incompatibles.";
            return false;
        }

        ResolveWait(state, visit,
            out float elapsed,
            out BistroBuilderCustomerBehaviorReason reason);
        behavior = new BistroBuilderAdvancedCustomerGroupBehavior
        {
            groupId = visit.groupId,
            serviceState = state,
            dominantReason = reason
        };
        long pressureSum = 0L;
        int maximum = 0;
        for (int i = 0; i < profile.members.Count; i++)
        {
            BistroBuilderAdvancedCustomerMemberProfile member = profile.members[i];
            float tolerance = ResolveTolerance(member, visit, reason);
            int pressure = reason == BistroBuilderCustomerBehaviorReason.None
                ? 0
                : BistroBuilderAdvancedCustomerProfileEngine
                    .ComputePatiencePressureBasisPoints(elapsed, tolerance);
            BistroBuilderCustomerBehaviorMood mood = ResolveMood(pressure);
            behavior.individuals.Add(new BistroBuilderAdvancedCustomerIndividualBehavior
            {
                customerId = member.customerId,
                memberIndex = member.memberIndex,
                patiencePressureBasisPoints = pressure,
                mood = mood,
                reason = reason
            });
            pressureSum += pressure;
            maximum = Math.Max(maximum, pressure);
            if (mood >= BistroBuilderCustomerBehaviorMood.Impatient)
                behavior.impatientMemberCount++;
        }

        int count = behavior.individuals.Count;
        behavior.averagePressureBasisPoints = count > 0
            ? (int)Math.Round(pressureSum / (double)count,
                MidpointRounding.AwayFromZero)
            : 0;
        behavior.maximumPressureBasisPoints = maximum;
        behavior.dominantMood = ResolveMood(maximum);
        error = string.Empty;
        return true;
    }

    public static BistroBuilderCustomerBehaviorMood ResolveMood(int pressureBasisPoints)
    {
        int pressure = Math.Max(0, Math.Min(10000, pressureBasisPoints));
        if (pressure < 2500) return BistroBuilderCustomerBehaviorMood.Calm;
        if (pressure < 5000) return BistroBuilderCustomerBehaviorMood.Attentive;
        if (pressure < 7500) return BistroBuilderCustomerBehaviorMood.Restless;
        if (pressure < 9000) return BistroBuilderCustomerBehaviorMood.Impatient;
        return BistroBuilderCustomerBehaviorMood.Critical;
    }

    private static void ResolveWait(
        CustomerGroupState state,
        BistroBuilderReputationVisitRuntimeRecord visit,
        out float elapsed,
        out BistroBuilderCustomerBehaviorReason reason)
    {
        elapsed = 0f;
        reason = BistroBuilderCustomerBehaviorReason.None;
        switch (state)
        {
            case CustomerGroupState.WaitingForTable:
                elapsed = visit.tableWaitSeconds;
                reason = BistroBuilderCustomerBehaviorReason.TableWait;
                break;
            case CustomerGroupState.WaitingForWaiter:
            case CustomerGroupState.WaitingForBarOrder:
                elapsed = visit.waiterWaitSeconds;
                reason = BistroBuilderCustomerBehaviorReason.WaiterWait;
                break;
            case CustomerGroupState.WaitingForFood:
            case CustomerGroupState.WaitingForBarItems:
                elapsed = visit.foodWaitSeconds;
                reason = BistroBuilderCustomerBehaviorReason.FoodWait;
                break;
            case CustomerGroupState.WaitingForBill:
                elapsed = visit.billWaitSeconds;
                reason = BistroBuilderCustomerBehaviorReason.BillWait;
                break;
        }
    }

    private static float ResolveTolerance(
        BistroBuilderAdvancedCustomerMemberProfile member,
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderCustomerBehaviorReason reason)
    {
        if (member == null) return 1f;
        switch (reason)
        {
            case BistroBuilderCustomerBehaviorReason.TableWait:
                return Math.Max(1f, member.tableWaitToleranceSeconds);
            case BistroBuilderCustomerBehaviorReason.WaiterWait:
                return Math.Max(1f, member.waiterWaitToleranceSeconds);
            case BistroBuilderCustomerBehaviorReason.FoodWait:
                return Math.Max(4f, visit.expectedFoodSeconds) *
                    member.foodWaitToleranceBasisPoints / 10000f;
            case BistroBuilderCustomerBehaviorReason.BillWait:
                return Math.Max(1f, member.billWaitToleranceSeconds);
            default:
                return 1f;
        }
    }
}
