using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierMarketSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes de mercado"
)]
public sealed class BistroBuilderSupplierMarketSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.market.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Ciclo")]
    [SerializeField, Min(1)] private int reviewEveryGameDays = 5;
    [SerializeField, Min(16)] private int maximumChangeHistoryEntries = 512;
    [SerializeField, Min(4)] private int maximumReviewHistoryEntries = 64;

    [Header("Probabilidad de cambio de precio por revisión")]
    [SerializeField, Range(0f, 1f)] private float stablePriceChangeChance = 0.24f;
    [SerializeField, Range(0f, 1f)] private float moderatePriceChangeChance = 0.44f;
    [SerializeField, Range(0f, 1f)] private float variablePriceChangeChance = 0.64f;

    [Header("Paso máximo por revisión")]
    [SerializeField, Min(0.1f)] private float stableMaximumStepPercent = 2.0f;
    [SerializeField, Min(0.1f)] private float moderateMaximumStepPercent = 4.0f;
    [SerializeField, Min(0.1f)] private float variableMaximumStepPercent = 6.5f;

    [Header("Disponibilidad")]
    [SerializeField, Range(0f, 1f)] private float veryStableAvailabilityMultiplier = 0.50f;
    [SerializeField, Range(0f, 2f)] private float stableAvailabilityMultiplier = 1.00f;
    [SerializeField, Range(0f, 3f)] private float variableAvailabilityMultiplier = 1.75f;
    [SerializeField, Range(0f, 3f)] private float seasonalAvailabilityMultiplier = 2.00f;

    [Header("Semilla")]
    [SerializeField] private int deterministicSalt = 2303001;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public int ReviewEveryGameDays => reviewEveryGameDays;
    public int MaximumChangeHistoryEntries => maximumChangeHistoryEntries;
    public int MaximumReviewHistoryEntries => maximumReviewHistoryEntries;
    public float StablePriceChangeChance => stablePriceChangeChance;
    public float ModeratePriceChangeChance => moderatePriceChangeChance;
    public float VariablePriceChangeChance => variablePriceChangeChance;
    public float StableMaximumStepPercent => stableMaximumStepPercent;
    public float ModerateMaximumStepPercent => moderateMaximumStepPercent;
    public float VariableMaximumStepPercent => variableMaximumStepPercent;
    public float VeryStableAvailabilityMultiplier => veryStableAvailabilityMultiplier;
    public float StableAvailabilityMultiplier => stableAvailabilityMultiplier;
    public float VariableAvailabilityMultiplier => variableAvailabilityMultiplier;
    public float SeasonalAvailabilityMultiplier => seasonalAvailabilityMultiplier;
    public int DeterministicSalt => deterministicSalt;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        reviewEveryGameDays = 5;
        maximumChangeHistoryEntries = Mathf.Max(16, maximumChangeHistoryEntries);
        maximumReviewHistoryEntries = Mathf.Max(4, maximumReviewHistoryEntries);
    }
#endif
}
