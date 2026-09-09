using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BB_SpatialFamilyCatalog",
    menuName = "Bistro Builder/Spatial/Family Catalog")]
public sealed class BistroBuilderSpatialFamilyCatalog : ScriptableObject
{
    [SerializeField] private List<BistroBuilderSpatialFamilyDefinition> families =
        new List<BistroBuilderSpatialFamilyDefinition>();

    public IReadOnlyList<BistroBuilderSpatialFamilyDefinition> Families =>
        families ?? (IReadOnlyList<BistroBuilderSpatialFamilyDefinition>)Array.Empty<BistroBuilderSpatialFamilyDefinition>();

    public bool ContainsFamily(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId) || families == null) return false;
        for (int i = 0; i < families.Count; i++)
        {
            BistroBuilderSpatialFamilyDefinition family = families[i];
            if (family != null &&
                string.Equals(family.familyId, familyId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public bool ValidateCatalog(out string error)
    {
        if (families == null || families.Count == 0)
        {
            error = "El catálogo de familias espaciales está vacío.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < families.Count; i++)
        {
            BistroBuilderSpatialFamilyDefinition family = families[i];
            if (family == null || string.IsNullOrWhiteSpace(family.familyId) ||
                !ids.Add(family.familyId))
            {
                error = "Familia espacial inválida o duplicada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureSeedForEditor()
    {
        if (families == null)
            families = new List<BistroBuilderSpatialFamilyDefinition>();
        else
            families.Clear();
        AddFamily("generic", "Genérico", "spatial.generic");
        AddFamily("seating.table", "Mesa", "seating.table", "service.table");
        AddFamily("seating.chair", "Silla", "seating.chair", "dynamic.seat");
        AddFamily("architecture.door", "Puerta", "architecture.door", "dynamic.sweep");
        AddFamily("work.kitchen", "Cocina", "work.station", "service.kitchen");
        AddFamily("work.bar", "Barra", "work.edge", "service.bar");
        AddFamily("work.pass", "Pass", "work.edge", "transfer.pass");
        AddFamily("logistics.cart", "Carrito", "mobility.cart", "carry.envelope");
    }

    private void AddFamily(string id, string label, params string[] traits)
    {
        var family = new BistroBuilderSpatialFamilyDefinition
        {
            familyId = id,
            displayName = label,
            defaultTraitIds = new List<string>()
        };
        if (traits != null)
            family.defaultTraitIds.AddRange(traits);
        families.Add(family);
    }
#endif
}
