using System;
using System.Collections.Generic;

[Flags]
public enum BistroBuilderCustomerSpecialNeed
{
    None = 0,
    AccessibleSeating = 1 << 0,
    QuietSeating = 1 << 1,
    DietaryAwareness = 1 << 2
}

[Flags]
public enum BistroBuilderCustomerTableFeature
{
    None = 0,
    Accessible = 1 << 0,
    Quiet = 1 << 1,
    VipPreferred = 1 << 2
}

/// <summary>Preferencias operativas agregadas de un grupo real.</summary>
[Serializable]
public sealed class BistroBuilderAdvancedCustomerServicePreference
{
    public int groupId;
    public bool isVip;
    public BistroBuilderCustomerSpecialNeed specialNeeds;
    public List<string> preferredZoneTagIds = new List<string>();

    public BistroBuilderAdvancedCustomerServicePreference DeepClone()
    {
        return new BistroBuilderAdvancedCustomerServicePreference
        {
            groupId = groupId,
            isVip = isVip,
            specialNeeds = specialNeeds,
            preferredZoneTagIds = preferredZoneTagIds != null
                ? new List<string>(preferredZoneTagIds)
                : new List<string>()
        };
    }
}

/// <summary>Descripción data-only de una mesa candidata.</summary>
public sealed class BistroBuilderAdvancedCustomerTableDescriptor
{
    public int tableId;
    public int capacity;
    public bool available;
    public BistroBuilderCustomerTableFeature features;
    public readonly List<string> semanticTags = new List<string>();
}

/// <summary>Resultado puro de evaluar una mesa contra un grupo.</summary>
public sealed class BistroBuilderAdvancedCustomerTableEvaluation
{
    public int preferenceScore;
    public int matchedPreferences;
    public int unmetSpecialNeeds;
    public bool suitable => unmetSpecialNeeds == 0;
}
