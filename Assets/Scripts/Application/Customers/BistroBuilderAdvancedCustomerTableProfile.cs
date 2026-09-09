using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Metadatos opcionales de una mesa para preferencias avanzadas.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantTable))]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Table Profile")]
public sealed class BistroBuilderAdvancedCustomerTableProfile : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderCustomerTableFeature features;

    [SerializeField]
    private List<string> semanticTags = new List<string>();

    public BistroBuilderCustomerTableFeature Features => features;
    public IReadOnlyList<string> SemanticTags => semanticTags;

    public bool ValidateConfiguration(out string error)
    {
        if ((features & ~(BistroBuilderCustomerTableFeature.Accessible |
                          BistroBuilderCustomerTableFeature.Quiet |
                          BistroBuilderCustomerTableFeature.VipPreferred)) != 0)
        {
            error = "La mesa contiene features avanzadas desconocidas.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (semanticTags == null) semanticTags = new List<string>();
        for (int i = 0; i < semanticTags.Count; i++)
        {
            string id = Normalize(semanticTags[i]);
            if (!BistroBuilderAdvancedCustomerProfileEngine.IsSafeId(id) || !ids.Add(id))
            {
                error = "La mesa contiene un tag semántico inválido o duplicado.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    public bool TryBuildDescriptor(
        RestaurantTable table,
        out BistroBuilderAdvancedCustomerTableDescriptor descriptor,
        out string error)
    {
        descriptor = null;
        error = string.Empty;
        if (table == null || !ReferenceEquals(table.gameObject, gameObject) ||
            !ValidateConfiguration(out error))
            return false;

        descriptor = new BistroBuilderAdvancedCustomerTableDescriptor
        {
            tableId = table.TableId,
            capacity = table.Capacity,
            available = table.IsAvailable,
            features = features
        };
        AddTags(descriptor.semanticTags, semanticTags);
        AddSpatialTags(descriptor.semanticTags);
        InferFeaturesFromTags(descriptor);
        error = string.Empty;
        return true;
    }

    public bool TryConfigure(
        BistroBuilderCustomerTableFeature newFeatures,
        IReadOnlyList<string> tags,
        out string error)
    {
        features = newFeatures;
        semanticTags = new List<string>();
        AddTags(semanticTags, tags);
        return ValidateConfiguration(out error);
    }

    private void AddSpatialTags(List<string> target)
    {
        RestaurantAreaMember member = GetComponent<RestaurantAreaMember>();
        RestaurantArea area = member != null ? member.AssignedArea : null;
        if (area == null) return;
        AddTag(target, area.AreaId);
        if (area.Definition != null)
            AddTag(target, area.Definition.AreaTypeId);
    }

    private static void InferFeaturesFromTags(
        BistroBuilderAdvancedCustomerTableDescriptor descriptor)
    {
        if (Contains(descriptor.semanticTags, "accessible"))
            descriptor.features |= BistroBuilderCustomerTableFeature.Accessible;
        if (Contains(descriptor.semanticTags, "quiet"))
            descriptor.features |= BistroBuilderCustomerTableFeature.Quiet;
        if (Contains(descriptor.semanticTags, "vip") ||
            Contains(descriptor.semanticTags, "premium"))
            descriptor.features |= BistroBuilderCustomerTableFeature.VipPreferred;
    }

    private static void AddTags(List<string> target, IReadOnlyList<string> source)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++) AddTag(target, source[i]);
    }

    private static void AddTag(List<string> target, string value)
    {
        string id = Normalize(value);
        if (!BistroBuilderAdvancedCustomerProfileEngine.IsSafeId(id) || Contains(target, id))
            return;
        target.Add(id);
    }

    private static bool Contains(IReadOnlyList<string> values, string id)
    {
        if (values == null) return false;
        string normalized = Normalize(id);
        for (int i = 0; i < values.Count; i++)
            if (string.Equals(Normalize(values[i]), normalized, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string Normalize(string value) =>
        BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(value);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (semanticTags == null) semanticTags = new List<string>();
        for (int i = 0; i < semanticTags.Count; i++)
            semanticTags[i] = Normalize(semanticTags[i]);
    }
#endif
}
