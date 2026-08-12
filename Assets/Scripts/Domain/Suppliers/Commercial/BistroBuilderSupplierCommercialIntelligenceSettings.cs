using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierCommercialIntelligenceSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes del motor comercial"
)]
public sealed class BistroBuilderSupplierCommercialIntelligenceSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.commercial.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Probabilidad de iniciar campaña en una revisión")]
    [SerializeField, Range(0f, 1f)] private float veryLowCampaignChance = 0.06f;
    [SerializeField, Range(0f, 1f)] private float lowCampaignChance = 0.13f;
    [SerializeField, Range(0f, 1f)] private float mediumCampaignChance = 0.23f;
    [SerializeField, Range(0f, 1f)] private float highCampaignChance = 0.36f;

    [Header("Separación mínima entre campañas del mismo proveedor")]
    [SerializeField, Min(1)] private int veryLowCooldownReviews = 4;
    [SerializeField, Min(1)] private int lowCooldownReviews = 3;
    [SerializeField, Min(1)] private int mediumCooldownReviews = 2;
    [SerializeField, Min(1)] private int highCooldownReviews = 1;

    [Header("Tamaño de campaña")]
    [SerializeField, Min(1)] private int veryLowMaximumOffersPerCampaign = 1;
    [SerializeField, Min(1)] private int lowMaximumOffersPerCampaign = 1;
    [SerializeField, Min(1)] private int mediumMaximumOffersPerCampaign = 2;
    [SerializeField, Min(1)] private int highMaximumOffersPerCampaign = 3;
    [SerializeField, Min(1)] private int maximumActivePromotionsPerSupplier = 3;

    [Header("Rotación")]
    [SerializeField, Min(0)] private int offerReuseCooldownDays = 15;
    [SerializeField] private bool requireFullyAvailableStock = true;

    [Header("Historial")]
    [SerializeField, Min(16)] private int maximumPromotionHistoryEntries = 256;
    [SerializeField, Min(8)] private int maximumReviewHistoryEntries = 96;

    [Header("Semilla")]
    [SerializeField] private int deterministicSalt = 2304001;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public float VeryLowCampaignChance => veryLowCampaignChance;
    public float LowCampaignChance => lowCampaignChance;
    public float MediumCampaignChance => mediumCampaignChance;
    public float HighCampaignChance => highCampaignChance;
    public int VeryLowCooldownReviews => veryLowCooldownReviews;
    public int LowCooldownReviews => lowCooldownReviews;
    public int MediumCooldownReviews => mediumCooldownReviews;
    public int HighCooldownReviews => highCooldownReviews;
    public int VeryLowMaximumOffersPerCampaign => veryLowMaximumOffersPerCampaign;
    public int LowMaximumOffersPerCampaign => lowMaximumOffersPerCampaign;
    public int MediumMaximumOffersPerCampaign => mediumMaximumOffersPerCampaign;
    public int HighMaximumOffersPerCampaign => highMaximumOffersPerCampaign;
    public int MaximumActivePromotionsPerSupplier => maximumActivePromotionsPerSupplier;
    public int OfferReuseCooldownDays => offerReuseCooldownDays;
    public bool RequireFullyAvailableStock => requireFullyAvailableStock;
    public int MaximumPromotionHistoryEntries => maximumPromotionHistoryEntries;
    public int MaximumReviewHistoryEntries => maximumReviewHistoryEntries;
    public int DeterministicSalt => deterministicSalt;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        veryLowCooldownReviews = Mathf.Max(1, veryLowCooldownReviews);
        lowCooldownReviews = Mathf.Max(1, lowCooldownReviews);
        mediumCooldownReviews = Mathf.Max(1, mediumCooldownReviews);
        highCooldownReviews = Mathf.Max(1, highCooldownReviews);
        veryLowMaximumOffersPerCampaign = Mathf.Max(1, veryLowMaximumOffersPerCampaign);
        lowMaximumOffersPerCampaign = Mathf.Max(1, lowMaximumOffersPerCampaign);
        mediumMaximumOffersPerCampaign = Mathf.Max(1, mediumMaximumOffersPerCampaign);
        highMaximumOffersPerCampaign = Mathf.Max(1, highMaximumOffersPerCampaign);
        maximumActivePromotionsPerSupplier = Mathf.Max(1, maximumActivePromotionsPerSupplier);
        offerReuseCooldownDays = Mathf.Max(0, offerReuseCooldownDays);
        maximumPromotionHistoryEntries = Mathf.Max(16, maximumPromotionHistoryEntries);
        maximumReviewHistoryEntries = Mathf.Max(8, maximumReviewHistoryEntries);
    }
#endif
}
