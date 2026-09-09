using System;
using System.Collections.Generic;

/// <summary>Familias jugables de campañas de Marketing V1.</summary>
public enum BistroBuilderMarketingCampaignType
{
    LocalAwareness = 0,
    Promotions = 1,
    Digital = 2,
    InfluencersPress = 3,
    EventsExperiences = 4,
    LoyaltyReferral = 5,
    MenuDishPromotion = 6
}

/// <summary>Tipo de objetivo lógico requerido por una campaña.</summary>
public enum BistroBuilderMarketingTargetKind
{
    None = 0,
    Dish = 1,
    Menu = 2
}

/// <summary>Segmentos de cliente que una campaña puede priorizar.</summary>
public enum BistroBuilderMarketingCustomerSegment
{
    Any = 0,
    LocalResidents = 1,
    Workers = 2,
    YoungAdults = 3,
    Groups = 4,
    Couples = 5,
    Foodies = 6,
    Traditional = 7,
    PriceSensitive = 8,
    Planners = 9,
    HighValue = 10
}

/// <summary>Franja a la que se aplica un modificador de Marketing.</summary>
public enum BistroBuilderMarketingDayPart
{
    Any = 0,
    Breakfast = 1,
    Lunch = 2,
    Afternoon = 3,
    Dinner = 4,
    LateNight = 5
}

/// <summary>
/// Efectos universales. 100 puntos básicos = 1 %. Los puntos planos se
/// reservan para magnitudes no porcentuales como reputación.
/// </summary>
public enum BistroBuilderMarketingModifierKind
{
    OverallDemand = 0,
    ReservationDemand = 1,
    WalkInDemand = 2,
    Reputation = 3,
    AverageTicket = 4,
    RepeatVisit = 5,
    OperationalPressure = 6,
    TargetDemand = 7
}

[Serializable]
public sealed class BistroBuilderMarketingModifier
{
    public BistroBuilderMarketingModifierKind kind;
    public int basisPoints;
    public int flatPoints;
    public BistroBuilderMarketingCustomerSegment segment =
        BistroBuilderMarketingCustomerSegment.Any;
    public BistroBuilderMarketingDayPart dayPart =
        BistroBuilderMarketingDayPart.Any;

    public BistroBuilderMarketingModifier DeepClone() =>
        (BistroBuilderMarketingModifier)MemberwiseClone();
}

/// <summary>
/// Definición de contenido de una campaña. Es dato de catálogo, no estado.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingCampaignDefinition
{
    public string campaignId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public BistroBuilderMarketingCampaignType type;
    public BistroBuilderMarketingTargetKind targetKind;
    public long baseCostCents;
    public int durationDays = 1;
    public int minProgressionLevel = 1;
    public List<BistroBuilderMarketingModifier> modifiers =
        new List<BistroBuilderMarketingModifier>();

    public BistroBuilderMarketingCampaignDefinition DeepClone()
    {
        var clone = (BistroBuilderMarketingCampaignDefinition)MemberwiseClone();
        clone.modifiers = new List<BistroBuilderMarketingModifier>();
        if (modifiers != null)
            for (int i = 0; i < modifiers.Count; i++)
                clone.modifiers.Add(modifiers[i]?.DeepClone());
        return clone;
    }
}

/// <summary>Instancia persistible de una campaña ya contratada.</summary>
[Serializable]
public sealed class BistroBuilderMarketingCampaignRecord
{
    public string instanceId = string.Empty;
    public string campaignId = string.Empty;
    public string targetId = string.Empty;
    public int startDayIndex = 1;
    public int endDayExclusive = 2;
    public long paidCostCents;
    public string financeOperationId = string.Empty;
    public long revision = 1L;

    public bool IsActiveOnDay(int dayIndex) =>
        dayIndex >= startDayIndex && dayIndex < endDayExclusive;

    public BistroBuilderMarketingCampaignRecord DeepClone() =>
        (BistroBuilderMarketingCampaignRecord)MemberwiseClone();
}

/// <summary>Fuente de verdad de Marketing. No contiene referencias Unity.</summary>
[Serializable]
public sealed class BistroBuilderMarketingSnapshot
{
    public const string CurrentSchemaId = "marketing.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public List<BistroBuilderMarketingCampaignRecord> campaigns =
        new List<BistroBuilderMarketingCampaignRecord>();

    public BistroBuilderMarketingSnapshot DeepClone()
    {
        var clone = new BistroBuilderMarketingSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            campaigns = new List<BistroBuilderMarketingCampaignRecord>()
        };
        if (campaigns != null)
            for (int i = 0; i < campaigns.Count; i++)
                clone.campaigns.Add(campaigns[i]?.DeepClone());
        return clone;
    }
}

/// <summary>Contexto con el que otro sistema consulta el efecto comercial.</summary>
public sealed class BistroBuilderMarketingEffectQuery
{
    public int dayIndex;
    public BistroBuilderMarketingCustomerSegment segment =
        BistroBuilderMarketingCustomerSegment.Any;
    public BistroBuilderMarketingDayPart dayPart =
        BistroBuilderMarketingDayPart.Any;
    public string targetId = string.Empty;
}

/// <summary>Resultado agregado; Marketing no materializa clientes ni reservas.</summary>
public sealed class BistroBuilderMarketingEffectSnapshot
{
    public int overallDemandBasisPoints;
    public int reservationDemandBasisPoints;
    public int walkInDemandBasisPoints;
    public int reputationFlatPoints;
    public int averageTicketBasisPoints;
    public int repeatVisitBasisPoints;
    public int operationalPressureBasisPoints;
    public int targetDemandBasisPoints;
    public int contributingCampaigns;
}
