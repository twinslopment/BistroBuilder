using System;
using System.Collections.Generic;

/// <summary>
/// Definición data-driven de un arquetipo conductual de cliente.
/// No representa una persona concreta ni decide el flujo del servicio.
/// </summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerArchetypeDefinition
{
    public string archetypeId = string.Empty;
    public string displayName = string.Empty;
    public int baseWeight = 100;
    public int segmentAffinityBonusWeight = 200;
    public List<string> segmentAffinityIds = new List<string>();

    public float tableWaitToleranceSeconds = 75f;
    public float waiterWaitToleranceSeconds = 45f;
    public int foodWaitToleranceBasisPoints = 16000;
    public float billWaitToleranceSeconds = 40f;

    public int serviceSensitivityBasisPoints = 5000;
    public int qualitySensitivityBasisPoints = 5000;
    public int priceSensitivityBasisPoints = 5000;
    public int ambienceSensitivityBasisPoints = 5000;

    public List<string> preferredDishCategoryIds = new List<string>();
    public List<string> avoidedDishCategoryIds = new List<string>();
    public string preferredZoneTagId = string.Empty;
    public BistroBuilderCustomerSpecialNeed specialNeeds;

    public BistroBuilderAdvancedCustomerArchetypeDefinition DeepClone()
    {
        return new BistroBuilderAdvancedCustomerArchetypeDefinition
        {
            archetypeId = archetypeId,
            displayName = displayName,
            baseWeight = baseWeight,
            segmentAffinityBonusWeight = segmentAffinityBonusWeight,
            segmentAffinityIds = segmentAffinityIds != null
                ? new List<string>(segmentAffinityIds)
                : new List<string>(),
            tableWaitToleranceSeconds = tableWaitToleranceSeconds,
            waiterWaitToleranceSeconds = waiterWaitToleranceSeconds,
            foodWaitToleranceBasisPoints = foodWaitToleranceBasisPoints,
            billWaitToleranceSeconds = billWaitToleranceSeconds,
            serviceSensitivityBasisPoints = serviceSensitivityBasisPoints,
            qualitySensitivityBasisPoints = qualitySensitivityBasisPoints,
            priceSensitivityBasisPoints = priceSensitivityBasisPoints,
            ambienceSensitivityBasisPoints = ambienceSensitivityBasisPoints,
            preferredDishCategoryIds = preferredDishCategoryIds != null
                ? new List<string>(preferredDishCategoryIds)
                : new List<string>(),
            avoidedDishCategoryIds = avoidedDishCategoryIds != null
                ? new List<string>(avoidedDishCategoryIds)
                : new List<string>(),
            preferredZoneTagId = preferredZoneTagId,
            specialNeeds = specialNeeds
        };
    }
}

/// <summary>Perfil individual materializado para un miembro del grupo.</summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerMemberProfile
{
    public string customerId = string.Empty;
    public string displayName = string.Empty;
    public int memberIndex;
    public string archetypeId = string.Empty;

    public float tableWaitToleranceSeconds;
    public float waiterWaitToleranceSeconds;
    public int foodWaitToleranceBasisPoints;
    public float billWaitToleranceSeconds;

    public int serviceSensitivityBasisPoints;
    public int qualitySensitivityBasisPoints;
    public int priceSensitivityBasisPoints;
    public int ambienceSensitivityBasisPoints;

    public List<string> preferredDishCategoryIds = new List<string>();
    public List<string> avoidedDishCategoryIds = new List<string>();
    public string preferredZoneTagId = string.Empty;
    public BistroBuilderCustomerSpecialNeed specialNeeds;

    public BistroBuilderAdvancedCustomerMemberProfile DeepClone()
    {
        return new BistroBuilderAdvancedCustomerMemberProfile
        {
            customerId = customerId,
            displayName = displayName,
            memberIndex = memberIndex,
            archetypeId = archetypeId,
            tableWaitToleranceSeconds = tableWaitToleranceSeconds,
            waiterWaitToleranceSeconds = waiterWaitToleranceSeconds,
            foodWaitToleranceBasisPoints = foodWaitToleranceBasisPoints,
            billWaitToleranceSeconds = billWaitToleranceSeconds,
            serviceSensitivityBasisPoints = serviceSensitivityBasisPoints,
            qualitySensitivityBasisPoints = qualitySensitivityBasisPoints,
            priceSensitivityBasisPoints = priceSensitivityBasisPoints,
            ambienceSensitivityBasisPoints = ambienceSensitivityBasisPoints,
            preferredDishCategoryIds = preferredDishCategoryIds != null
                ? new List<string>(preferredDishCategoryIds)
                : new List<string>(),
            avoidedDishCategoryIds = avoidedDishCategoryIds != null
                ? new List<string>(avoidedDishCategoryIds)
                : new List<string>(),
            preferredZoneTagId = preferredZoneTagId,
            specialNeeds = specialNeeds
        };
    }
}

/// <summary>
/// Perfil conductual data-only del grupo actual. Un CustomerGroup físico puede
/// contener varios perfiles individuales sin multiplicar GameObjects/NPCs.
/// </summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerGroupProfile
{
    public const int CurrentSchemaVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public int groupId;
    public int dayIndex = 1;
    public string segmentId = "general";
    public bool returningVisit;
    public string returningReferenceId = string.Empty;
    public List<BistroBuilderAdvancedCustomerMemberProfile> members =
        new List<BistroBuilderAdvancedCustomerMemberProfile>();

    public BistroBuilderAdvancedCustomerGroupProfile DeepClone()
    {
        var clone = new BistroBuilderAdvancedCustomerGroupProfile
        {
            schemaVersion = schemaVersion,
            groupId = groupId,
            dayIndex = dayIndex,
            segmentId = segmentId,
            returningVisit = returningVisit,
            returningReferenceId = returningReferenceId,
            members = new List<BistroBuilderAdvancedCustomerMemberProfile>()
        };
        if (members != null)
            for (int i = 0; i < members.Count; i++)
                clone.members.Add(members[i]?.DeepClone());
        return clone;
    }
}
