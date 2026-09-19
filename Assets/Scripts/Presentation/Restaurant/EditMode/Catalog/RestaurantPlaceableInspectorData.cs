using System.Collections.Generic;
using BistroBuilder.FurnitureFinishes;
using UnityEngine;

public sealed class RestaurantPlaceableInspectorData
{
    private readonly List<RestaurantPlaceableInspectorRuleData> rules =
        new List<RestaurantPlaceableInspectorRuleData>(4);

    public RestaurantPlaceableItemDefinition Definition { get; }
    public string DisplayName => Definition != null ? Definition.DisplayName : string.Empty;
    public string Description { get; }
    public Sprite Preview => Definition != null ? Definition.InspectorPreview : null;
    public int Price => Definition != null ? Definition.PurchasePrice : 0;
    public Vector3 DimensionsCentimeters =>
        Definition != null ? Definition.DimensionsCentimeters : Vector3.zero;
    public FurnitureFinishProfile FinishProfile =>
        Definition != null ? Definition.FinishProfile : null;
    public IReadOnlyList<RestaurantPlaceableInspectorRuleData> Rules => rules;

    private RestaurantPlaceableInspectorData(
        RestaurantPlaceableItemDefinition definition)
    {
        Definition = definition;
        Description = ResolveDescription(definition);
        BuildRules(definition);
    }

    public static RestaurantPlaceableInspectorData From(
        RestaurantPlaceableItemDefinition definition)
    {
        return new RestaurantPlaceableInspectorData(definition);
    }

    public string ScopeLabel
    {
        get
        {
            if (Definition == null)
                return string.Empty;

            switch (Definition.PlacementScope)
            {
                case RestaurantPlaceableEnvironmentScope.InteriorOnly:
                    return "Interior";
                case RestaurantPlaceableEnvironmentScope.ExteriorOnly:
                    return "Exterior";
                default:
                    return "Interior / exterior";
            }
        }
    }

    private static string ResolveDescription(
        RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(definition.Description))
            return definition.Description.Trim();

        if (definition.EditableDefinition != null &&
            !string.IsNullOrWhiteSpace(definition.EditableDefinition.Description))
        {
            return definition.EditableDefinition.Description.Trim();
        }

        return "Sin descripción disponible.";
    }

    private void BuildRules(
        RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null)
            return;

        RestaurantPlaceableInspectorRuleFlags flags =
            definition.InspectorRules;

        if ((flags & RestaurantPlaceableInspectorRuleFlags.FloorSurface) != 0)
        {
            rules.Add(new RestaurantPlaceableInspectorRuleData(
                "Se puede colocar en suelos"));
        }

        if ((flags & RestaurantPlaceableInspectorRuleFlags.RequiresClearance) != 0)
        {
            rules.Add(new RestaurantPlaceableInspectorRuleData(
                "Requiere espacio libre"));
        }

        switch (definition.PlacementScope)
        {
            case RestaurantPlaceableEnvironmentScope.InteriorOnly:
                rules.Add(new RestaurantPlaceableInspectorRuleData(
                    "Apto para interior"));
                break;
            case RestaurantPlaceableEnvironmentScope.ExteriorOnly:
                rules.Add(new RestaurantPlaceableInspectorRuleData(
                    "Apto para exterior"));
                break;
            default:
                rules.Add(new RestaurantPlaceableInspectorRuleData(
                    "Apto para interior y exterior"));
                break;
        }
    }
}

public struct RestaurantPlaceableInspectorRuleData
{
    public string Label { get; }

    public RestaurantPlaceableInspectorRuleData(string label)
    {
        Label = label ?? string.Empty;
    }
}
