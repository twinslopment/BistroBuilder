using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderFrontOfHouseOperationalState
{
    Fluid = 0,
    Waiting = 1,
    Saturated = 2,
    ReducedPace = 3
}

public enum BistroBuilderFrontOfHouseQueueReason
{
    Standard = 0,
    Reservation = 1,
    Vip = 2,
    PatienceRisk = 3,
    WaitingAtBar = 4
}

[Serializable]
public sealed class BistroBuilderFrontOfHouseQueueEntry
{
    public int groupId;
    public int partySize;
    public float waitingSeconds;
    public float toleranceSeconds;
    public float pressure01;
    public int priorityScore;
    public int queuePosition;
    public bool hasReservation;
    public bool isVip;
    public bool occupyingBar;
    public BistroBuilderFrontOfHouseQueueReason reason;

    public BistroBuilderFrontOfHouseQueueEntry DeepClone() =>
        (BistroBuilderFrontOfHouseQueueEntry)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderFrontOfHouseTableRotationRecord
{
    public int tableId;
    public int seatedCount;
    public long lastSeatSequence;

    public BistroBuilderFrontOfHouseTableRotationRecord DeepClone() =>
        (BistroBuilderFrontOfHouseTableRotationRecord)MemberwiseClone();
}

[Serializable]
public sealed class BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot
{
    public const int CurrentSchemaVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public long nextSeatSequence = 1L;
    public List<BistroBuilderFrontOfHouseTableRotationRecord> tableRotations =
        new List<BistroBuilderFrontOfHouseTableRotationRecord>();

    public BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot DeepClone()
    {
        var clone = new BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot
        {
            schemaVersion = schemaVersion,
            revision = revision,
            nextSeatSequence = nextSeatSequence,
            tableRotations = new List<BistroBuilderFrontOfHouseTableRotationRecord>()
        };
        if (tableRotations != null)
            for (int i = 0; i < tableRotations.Count; i++)
                clone.tableRotations.Add(tableRotations[i]?.DeepClone());
        return clone;
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion || revision < 0L ||
            nextSeatSequence < 1L || tableRotations == null ||
            tableRotations.Count > 512)
        {
            error = "El runtime avanzado de sala contiene una cabecera inválida.";
            return false;
        }

        var ids = new HashSet<int>();
        long maximumSequence = 0L;
        for (int i = 0; i < tableRotations.Count; i++)
        {
            BistroBuilderFrontOfHouseTableRotationRecord item = tableRotations[i];
            if (item == null || item.tableId < 1 || item.seatedCount < 0 ||
                item.lastSeatSequence < 0L || !ids.Add(item.tableId))
            {
                error = "El runtime avanzado de sala contiene rotaciones inválidas.";
                return false;
            }
            maximumSequence = Math.Max(maximumSequence, item.lastSeatSequence);
        }

        if (nextSeatSequence <= maximumSequence)
        {
            error = "La secuencia de rotación de sala no supera las ya utilizadas.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public static class BistroBuilderAdvancedFrontOfHousePolicy
{
    public static BistroBuilderFrontOfHouseOperationalState ResolveOperationalState(
        int queueCount,
        int freeTableCount,
        float maximumPressure01,
        float averageWaiterSaturation01)
    {
        if (queueCount <= 0 && averageWaiterSaturation01 < 0.72f)
            return BistroBuilderFrontOfHouseOperationalState.Fluid;
        if (maximumPressure01 >= 1f || (queueCount >= 5 && freeTableCount <= 0))
            return BistroBuilderFrontOfHouseOperationalState.Saturated;
        if (averageWaiterSaturation01 >= 0.78f)
            return BistroBuilderFrontOfHouseOperationalState.ReducedPace;
        return BistroBuilderFrontOfHouseOperationalState.Waiting;
    }

    public static int ComputeQueuePriority(
        float waitingSeconds,
        float toleranceSeconds,
        int partySize,
        bool hasReservation,
        bool isVip,
        bool occupyingBar)
    {
        float safeTolerance = Mathf.Max(10f, toleranceSeconds);
        float pressure = Mathf.Clamp01(waitingSeconds / safeTolerance);
        int score = Mathf.RoundToInt(pressure * 5000f) +
                    Mathf.RoundToInt(Mathf.Min(1800f, waitingSeconds * 18f));
        if (hasReservation) score += 8000;
        if (isVip) score += 3000;
        if (occupyingBar) score += 350;
        score -= Mathf.Max(0, partySize - 2) * 40;
        return score;
    }

    public static BistroBuilderFrontOfHouseQueueReason ResolveQueueReason(
        float waitingSeconds,
        float toleranceSeconds,
        bool hasReservation,
        bool isVip,
        bool occupyingBar)
    {
        if (hasReservation) return BistroBuilderFrontOfHouseQueueReason.Reservation;
        if (isVip) return BistroBuilderFrontOfHouseQueueReason.Vip;
        if (waitingSeconds >= Mathf.Max(10f, toleranceSeconds) * 0.72f)
            return BistroBuilderFrontOfHouseQueueReason.PatienceRisk;
        if (occupyingBar) return BistroBuilderFrontOfHouseQueueReason.WaitingAtBar;
        return BistroBuilderFrontOfHouseQueueReason.Standard;
    }

    public static bool ShouldAbandon(
        float waitingSeconds,
        float toleranceSeconds,
        bool hasReservation,
        bool occupyingBar)
    {
        if (occupyingBar) return false;
        float multiplier = hasReservation ? 1.55f : 1.15f;
        return waitingSeconds >= Mathf.Max(20f, toleranceSeconds) * multiplier;
    }

    public static float ComputeTableScore(
        int groupSize,
        int tableCapacity,
        float routeDistanceMeters,
        int preferenceScore,
        int seatedCount,
        long lastSeatSequence,
        bool protectedForReservation)
    {
        if (protectedForReservation || tableCapacity < groupSize)
            return float.MinValue;
        int waste = tableCapacity - groupSize;
        float rotationBonus = Mathf.Min(900f, seatedCount * 55f) -
                              Mathf.Min(700f, lastSeatSequence * 0.01f);
        return 5500f + preferenceScore * 0.35f - waste * 650f -
               Mathf.Max(0f, routeDistanceMeters) * 24f + rotationBonus;
    }
}
