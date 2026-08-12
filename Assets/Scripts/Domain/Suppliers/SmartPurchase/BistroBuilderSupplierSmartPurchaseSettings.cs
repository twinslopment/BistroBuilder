using UnityEngine;

[CreateAssetMenu(fileName = "BistroBuilderSupplierSmartPurchaseSettings", menuName = "Bistro Builder/Proveedores/2.3F Motor de Compra Inteligente")]
public sealed class BistroBuilderSupplierSmartPurchaseSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.smart_purchase.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Cobertura objetivo (días)")]
    [Min(0.5f)] public float savingTargetCoverageDays = 5f;
    [Min(0.5f)] public float balancedTargetCoverageDays = 4f;
    [Min(0.5f)] public float urgentTargetCoverageDays = 2.5f;

    [Header("Horizontes")]
    [Min(1)] public int expiryRiskWindowDays = 3;
    [Min(1)] public int criticalStockoutHours = 24;
    [Min(1)] public int highStockoutHours = 48;

    [Header("Pesos — Ahorrar")]
    [Range(0f, 10f)] public float savingCostWeight = 5.0f;
    [Range(0f, 10f)] public float savingSpeedWeight = 1.0f;
    [Range(0f, 10f)] public float savingReliabilityWeight = 1.5f;
    [Range(0f, 10f)] public float savingWasteWeight = 3.0f;
    [Range(0f, 10f)] public float savingStockoutWeight = 4.0f;

    [Header("Pesos — Equilibrado")]
    [Range(0f, 10f)] public float balancedCostWeight = 3.0f;
    [Range(0f, 10f)] public float balancedSpeedWeight = 3.0f;
    [Range(0f, 10f)] public float balancedReliabilityWeight = 3.5f;
    [Range(0f, 10f)] public float balancedWasteWeight = 3.0f;
    [Range(0f, 10f)] public float balancedStockoutWeight = 5.0f;

    [Header("Pesos — Urgente")]
    [Range(0f, 10f)] public float urgentCostWeight = 1.0f;
    [Range(0f, 10f)] public float urgentSpeedWeight = 6.0f;
    [Range(0f, 10f)] public float urgentReliabilityWeight = 5.0f;
    [Range(0f, 10f)] public float urgentWasteWeight = 1.5f;
    [Range(0f, 10f)] public float urgentStockoutWeight = 8.0f;

    [Header("Penalizaciones")]
    [Range(0f, 10f)] public float limitedAvailabilityPenalty = 1.5f;
    [Range(0f, 20f)] public float supplierMinimumGapPenalty = 6.0f;
    [Range(0f, 10f)] public float overstockPenaltyScale = 4.0f;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;

#if UNITY_EDITOR
    public void EditorEnsureSchema()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
    }
#endif
}
