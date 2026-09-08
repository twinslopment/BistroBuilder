using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderKitchenStationKind
{
    ColdPrep = 0,
    Range = 1,
    Grill = 2,
    Oven = 3,
    Pastry = 4,
    Plating = 5,
    Fryer = 6,
    Bar = 7
}

public enum BistroBuilderKitchenLoadState
{
    Fluid = 0,
    Loaded = 1,
    Saturated = 2,
    Blocked = 3
}

public enum BistroBuilderKitchenIntakeMode
{
    Normal = 0,
    Reduced = 1,
    Paused = 2
}

public enum BistroBuilderKitchenPriorityKind
{
    Normal = 0,
    CourseSync = 10,
    WaitingRecovery = 20,
    IncidentReplacement = 30,
    PlayerPriority = 40
}

public enum BistroBuilderKitchenIncidentKind
{
    None = 0,
    Slowdown = 1,
    EquipmentFailure = 2,
    QualityRisk = 3
}

[Serializable]
public sealed class BistroBuilderKitchenStationDefinition
{
    public string stationId = string.Empty;
    public string displayName = string.Empty;
    public BistroBuilderKitchenStationKind kind;
    [Min(1)] public int baseCapacity = 1;
    [Range(2500, 20000)] public int speedBasisPoints = 10000;
    [Range(-3000, 3000)] public int qualityModifierBasisPoints;
    [Range(9000, 10000)] public int reliabilityBasisPoints = 9950;
    [Range(1, 5)] public int equipmentTier = 1;

    public BistroBuilderKitchenStationDefinition DeepClone()
    {
        return (BistroBuilderKitchenStationDefinition)MemberwiseClone();
    }

    public bool TryValidate(out string error)
    {
        stationId = BistroBuilderStaffStableIdUtility.Normalize(stationId);
        if (!BistroBuilderStaffStableIdUtility.IsValid(stationId) ||
            string.IsNullOrWhiteSpace(displayName) ||
            !Enum.IsDefined(typeof(BistroBuilderKitchenStationKind), kind) ||
            baseCapacity < 1 || baseCapacity > 16 ||
            speedBasisPoints < 2500 || speedBasisPoints > 20000 ||
            qualityModifierBasisPoints < -3000 || qualityModifierBasisPoints > 3000 ||
            reliabilityBasisPoints < 9000 || reliabilityBasisPoints > 10000 ||
            equipmentTier < 1 || equipmentTier > 5)
        {
            error = "La estación de cocina " + stationId + " contiene datos inválidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderKitchenDishRouteDefinition
{
    public string dishId = string.Empty;
    public List<string> stationIds = new List<string>();

    public BistroBuilderKitchenDishRouteDefinition DeepClone()
    {
        var clone = new BistroBuilderKitchenDishRouteDefinition { dishId = dishId };
        if (stationIds != null) clone.stationIds.AddRange(stationIds);
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderKitchenTaskSnapshot
{
    public string canonicalOrderId = string.Empty;
    public int legacyOrderId;
    public string lineId = string.Empty;
    public string dishId = string.Empty;
    public string stationId = string.Empty;
    public int stageIndex;
    public int stageCount = 1;
    public long sequence;
    public BistroBuilderKitchenPriorityKind priority;
    public string cookEmployeeId = string.Empty;
    public string cookDisplayName = string.Empty;
    public float totalSeconds;
    public float remainingSeconds;
    public int qualityBasisPoints;
    public BistroBuilderKitchenIncidentKind incident;
    public bool active;

    public BistroBuilderKitchenTaskSnapshot DeepClone()
    {
        return (BistroBuilderKitchenTaskSnapshot)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderKitchenStationSnapshot
{
    public string stationId = string.Empty;
    public string displayName = string.Empty;
    public BistroBuilderKitchenStationKind kind;
    public int capacity;
    public int activeCount;
    public int queuedCount;
    public float blockedSeconds;
    public BistroBuilderKitchenLoadState loadState;
    public List<BistroBuilderKitchenTaskSnapshot> tasks =
        new List<BistroBuilderKitchenTaskSnapshot>();
}

[Serializable]
public sealed class BistroBuilderAdvancedKitchenSnapshot
{
    public BistroBuilderKitchenLoadState loadState;
    public BistroBuilderKitchenIntakeMode intakeMode;
    public int activeCount;
    public int queuedCount;
    public int totalCapacity;
    public int completedLineCount;
    public int averageRecentQualityBasisPoints = 7000;
    public List<BistroBuilderKitchenStationSnapshot> stations =
        new List<BistroBuilderKitchenStationSnapshot>();
}

public readonly struct BistroBuilderKitchenIncidentEvent
{
    public string StationId { get; }
    public string LineId { get; }
    public BistroBuilderKitchenIncidentKind Kind { get; }
    public string Message { get; }

    public BistroBuilderKitchenIncidentEvent(
        string stationId,
        string lineId,
        BistroBuilderKitchenIncidentKind kind,
        string message)
    {
        StationId = stationId ?? string.Empty;
        LineId = lineId ?? string.Empty;
        Kind = kind;
        Message = message ?? string.Empty;
    }
}

public static class BistroBuilderAdvancedKitchenPolicy
{
    public const int BasisPoints = 10000;

    public static BistroBuilderKitchenLoadState ResolveLoad(
        int active,
        int queued,
        int capacity,
        bool hasBlockedRequiredStation)
    {
        if (hasBlockedRequiredStation && queued > 0)
            return BistroBuilderKitchenLoadState.Blocked;
        capacity = Math.Max(1, capacity);
        int workload = Math.Max(0, active) + Math.Max(0, queued);
        if (workload <= capacity) return BistroBuilderKitchenLoadState.Fluid;
        if (workload <= capacity * 2) return BistroBuilderKitchenLoadState.Loaded;
        return BistroBuilderKitchenLoadState.Saturated;
    }

    public static float ApplySpeed(float baseSeconds, int speedBasisPoints)
    {
        int speed = Mathf.Clamp(speedBasisPoints, 2500, 20000);
        return Mathf.Max(0.05f, baseSeconds * BasisPoints / speed);
    }

    public static int ResolveCookSpeedBasisPoints(BistroBuilderEmployeeRecord cook)
    {
        if (cook == null || cook.skills == null) return 9000;
        int skill = Mathf.Clamp(cook.skills.speed, 0, 100);
        int organization = Mathf.Clamp(cook.skills.organization, 0, 100);
        int experienceBonus = (int)Math.Min(1000L, Math.Max(0L, cook.experiencePoints) / 10L);
        return Mathf.Clamp(8000 + skill * 20 + organization * 8 + experienceBonus, 8000, 13000);
    }

    public static int ResolveQuality(
        BistroBuilderEmployeeRecord cook,
        BistroBuilderKitchenStationDefinition station,
        BistroBuilderKitchenLoadState load,
        BistroBuilderKitchenIncidentKind incident)
    {
        int quality = 7000;
        if (cook != null && cook.skills != null)
        {
            quality += (Mathf.Clamp(cook.skills.attentiveness, 0, 100) - 50) * 18;
            quality += (Mathf.Clamp(cook.skills.organization, 0, 100) - 50) * 12;
            quality += (int)Math.Min(600L, Math.Max(0L, cook.experiencePoints) / 15L);
        }
        else quality -= 350;
        if (station != null)
        {
            quality += station.qualityModifierBasisPoints;
            quality += (station.equipmentTier - 1) * 120;
        }
        if (load == BistroBuilderKitchenLoadState.Loaded) quality -= 250;
        else if (load == BistroBuilderKitchenLoadState.Saturated) quality -= 700;
        else if (load == BistroBuilderKitchenLoadState.Blocked) quality -= 900;
        if (incident == BistroBuilderKitchenIncidentKind.QualityRisk) quality -= 1200;
        else if (incident == BistroBuilderKitchenIncidentKind.Slowdown) quality -= 150;
        return Mathf.Clamp(quality, 2500, 10000);
    }

    public static int StableRoll(string value, int modulus)
    {
        unchecked
        {
            uint hash = 2166136261;
            string source = value ?? string.Empty;
            for (int i = 0; i < source.Length; i++)
            {
                hash ^= source[i];
                hash *= 16777619;
            }
            return modulus > 0 ? (int)(hash % (uint)modulus) : 0;
        }
    }
}