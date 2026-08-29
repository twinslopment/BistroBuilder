using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Catálogo data-driven universal de mejoras del Bloque 9.</summary>
[CreateAssetMenu(
    fileName = "BB_Upgrade_Catalog",
    menuName = "Bistro Builder/Progression/Upgrade Catalog")]
public sealed class BistroBuilderUpgradeCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderUpgradeDefinition> upgrades =
        new List<BistroBuilderUpgradeDefinition>();

    private readonly Dictionary<string, BistroBuilderUpgradeDefinition> byId =
        new Dictionary<string, BistroBuilderUpgradeDefinition>(StringComparer.Ordinal);
    private bool indexBuilt;

    public IReadOnlyList<BistroBuilderUpgradeDefinition> Upgrades => upgrades;
    public int Count { get { EnsureIndex(); return byId.Count; } }

    public bool TryGetUpgrade(string upgradeId, out BistroBuilderUpgradeDefinition definition)
    {
        EnsureIndex();
        if (!byId.TryGetValue(BistroBuilderProgressionEngine.NormalizeId(upgradeId), out var stored))
        {
            definition = null;
            return false;
        }
        definition = stored.DeepClone();
        return true;
    }

    public bool ValidateConfiguration(out string error)
    {
        EnsureIndex();
        return BistroBuilderProgressionEngine.TryValidateCatalog(upgrades, out error);
    }

#if UNITY_EDITOR
    public void EditorReplaceAll(IReadOnlyList<BistroBuilderUpgradeDefinition> definitions)
    {
        upgrades = new List<BistroBuilderUpgradeDefinition>();
        if (definitions != null)
            for (int i = 0; i < definitions.Count; i++)
                upgrades.Add(definitions[i]?.DeepClone());
        indexBuilt = false;
        RebuildIndex();
    }
#endif

    private void EnsureIndex()
    {
        if (!indexBuilt) RebuildIndex();
    }

    private void RebuildIndex()
    {
        byId.Clear();
        if (upgrades != null)
            for (int i = 0; i < upgrades.Count; i++)
            {
                var definition = upgrades[i];
                if (definition == null) continue;
                string id = BistroBuilderProgressionEngine.NormalizeId(definition.upgradeId);
                if (id.Length > 0 && !byId.ContainsKey(id)) byId.Add(id, definition);
            }
        indexBuilt = true;
    }

#if UNITY_EDITOR
    private void OnValidate() => indexBuilt = false;
#endif
}