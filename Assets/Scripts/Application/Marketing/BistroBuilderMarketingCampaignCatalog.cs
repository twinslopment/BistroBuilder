using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo data-driven de campañas de Marketing. Una única asset contiene
/// las definiciones; las campañas no son MonoBehaviours ni clases especiales.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_Marketing_Campaign_Catalog",
    menuName = "Bistro Builder/Marketing/Campaign Catalog")]
public sealed class BistroBuilderMarketingCampaignCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderMarketingCampaignDefinition> campaigns =
        new List<BistroBuilderMarketingCampaignDefinition>();

    private readonly Dictionary<string, BistroBuilderMarketingCampaignDefinition>
        byId = new Dictionary<string, BistroBuilderMarketingCampaignDefinition>(
            StringComparer.Ordinal);
    private bool indexBuilt;

    public IReadOnlyList<BistroBuilderMarketingCampaignDefinition> Campaigns =>
        campaigns;

    public int Count
    {
        get
        {
            EnsureIndex();
            return byId.Count;
        }
    }

    public bool TryGetCampaign(
        string campaignId,
        out BistroBuilderMarketingCampaignDefinition definition)
    {
        EnsureIndex();
        if (!byId.TryGetValue(
                BistroBuilderMarketingEngine.NormalizeId(campaignId),
                out BistroBuilderMarketingCampaignDefinition stored))
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
        return BistroBuilderMarketingEngine.TryValidateCatalog(campaigns, out error);
    }

#if UNITY_EDITOR
    /// <summary>API exclusiva del instalador/editor para publicar el seed.</summary>
    public void EditorReplaceAll(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions)
    {
        campaigns = new List<BistroBuilderMarketingCampaignDefinition>();
        if (definitions != null)
            for (int i = 0; i < definitions.Count; i++)
                campaigns.Add(definitions[i]?.DeepClone());
        indexBuilt = false;
        RebuildIndex();
    }
#endif

    private void EnsureIndex()
    {
        if (!indexBuilt)
            RebuildIndex();
    }

    private void RebuildIndex()
    {
        byId.Clear();
        if (campaigns != null)
        {
            for (int i = 0; i < campaigns.Count; i++)
            {
                BistroBuilderMarketingCampaignDefinition definition = campaigns[i];
                if (definition == null)
                    continue;
                string id = BistroBuilderMarketingEngine.NormalizeId(
                    definition.campaignId);
                if (id.Length > 0 && !byId.ContainsKey(id))
                    byId.Add(id, definition);
            }
        }
        indexBuilt = true;
    }

#if UNITY_EDITOR
    private void OnValidate() => indexBuilt = false;
#endif
}
