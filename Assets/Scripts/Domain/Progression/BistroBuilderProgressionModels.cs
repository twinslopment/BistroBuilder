using System;
using System.Collections.Generic;

/// <summary>Categorías universales de mejoras del restaurante.</summary>
public enum BistroBuilderUpgradeCategory
{
    DiningRoom = 0,
    Kitchen = 1,
    Terrace = 2,
    Bar = 3,
    Infrastructure = 4,
    AmbienceIdentity = 5
}


/// <summary>Canal de efecto funcional de una mejora adquirida.</summary>
public enum BistroBuilderUpgradeEffectKind
{
    PreparationDuration = 0,
    AmbienceScore = 1,
    FoodQualityPotential = 2
}

/// <summary>Efecto declarativo; basisPoints se suma al canal correspondiente.</summary>
[Serializable]
public sealed class BistroBuilderUpgradeEffectDefinition
{
    public BistroBuilderUpgradeEffectKind kind;
    public int basisPoints;
    public bool barServiceOnly;

    public BistroBuilderUpgradeEffectDefinition DeepClone()
    {
        return new BistroBuilderUpgradeEffectDefinition
        {
            kind = kind,
            basisPoints = basisPoints,
            barServiceOnly = barServiceOnly
        };
    }
}
/// <summary>Estado calculado de una mejora para el contexto actual.</summary>
public enum BistroBuilderUpgradeAvailabilityState
{
    Locked = 0,
    Available = 1,
    Purchased = 2
}

/// <summary>
/// Definición data-driven de una mejora. No contiene lógica de escena ni efectos
/// concretos: expresa coste, requisitos y compatibilidad de forma estable.
/// </summary>
[Serializable]
public sealed class BistroBuilderUpgradeDefinition
{
    public string upgradeId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public BistroBuilderUpgradeCategory category;
    public long costCents;
    public int requiredProgressionLevel = 1;
    public int requiredReputationBasisPoints;
    public List<string> prerequisiteUpgradeIds = new List<string>();
    public List<string> requiredCapabilityIds = new List<string>();
    public List<string> incompatibleCapabilityIds = new List<string>();
    public List<BistroBuilderUpgradeEffectDefinition> effects =
        new List<BistroBuilderUpgradeEffectDefinition>();

    public BistroBuilderUpgradeDefinition DeepClone()
    {
        return new BistroBuilderUpgradeDefinition
        {
            upgradeId = upgradeId,
            displayName = displayName,
            description = description,
            category = category,
            costCents = costCents,
            requiredProgressionLevel = requiredProgressionLevel,
            requiredReputationBasisPoints = requiredReputationBasisPoints,
            prerequisiteUpgradeIds = prerequisiteUpgradeIds != null
                ? new List<string>(prerequisiteUpgradeIds)
                : new List<string>(),
            requiredCapabilityIds = requiredCapabilityIds != null
                ? new List<string>(requiredCapabilityIds)
                : new List<string>(),
            incompatibleCapabilityIds = incompatibleCapabilityIds != null
                ? new List<string>(incompatibleCapabilityIds)
                : new List<string>(),
            effects = effects != null
                ? effects.ConvertAll(effect => effect?.DeepClone())
                : new List<BistroBuilderUpgradeEffectDefinition>()
        };
    }
}

/// <summary>Registro persistible de una mejora adquirida.</summary>
[Serializable]
public sealed class BistroBuilderPurchasedUpgradeRecord
{
    public string upgradeId = string.Empty;
    public int purchasedDayIndex;
    public long paidCents;

    public BistroBuilderPurchasedUpgradeRecord DeepClone()
    {
        return new BistroBuilderPurchasedUpgradeRecord
        {
            upgradeId = upgradeId,
            purchasedDayIndex = purchasedDayIndex,
            paidCents = paidCents
        };
    }
}

/// <summary>Estado canónico de adquisiciones de mejoras.</summary>
[Serializable]
public sealed class BistroBuilderUpgradeSnapshot
{
    public const int CurrentSchemaVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public List<BistroBuilderPurchasedUpgradeRecord> purchased =
        new List<BistroBuilderPurchasedUpgradeRecord>();

    public BistroBuilderUpgradeSnapshot DeepClone()
    {
        var clone = new BistroBuilderUpgradeSnapshot
        {
            schemaVersion = schemaVersion,
            revision = revision,
            purchased = new List<BistroBuilderPurchasedUpgradeRecord>()
        };
        if (purchased != null)
            for (int i = 0; i < purchased.Count; i++)
                clone.purchased.Add(purchased[i]?.DeepClone());
        return clone;
    }
}

/// <summary>Contexto puro empleado para calcular disponibilidad.</summary>
public sealed class BistroBuilderUpgradeAvailabilityContext
{
    public int progressionLevel = 1;
    public int reputationBasisPoints;
    public long availableCashCents;
    public readonly HashSet<string> purchasedUpgradeIds =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<string> capabilityIds =
        new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>Resultado inmutable de disponibilidad de una mejora.</summary>
public sealed class BistroBuilderUpgradeAvailability
{
    public BistroBuilderUpgradeAvailabilityState state;
    public bool affordable;
    public string blockedReason = string.Empty;
}