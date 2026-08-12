using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierLogisticsPlanningSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes de planificación logística"
)]
public sealed class BistroBuilderSupplierLogisticsPlanningSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.logistics.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;
    [SerializeField] private string deterministicSeedText = "bistro-logistics-v1";

    [Header("Calendario")]
    [Tooltip("0=Lunes ... 6=Domingo para el día 1 de la partida.")]
    [SerializeField, Range(0, 6)] private int firstGameDayWeekday = 0;
    [SerializeField, Min(1)] private int maximumWindowSearchDays = 21;
    [SerializeField, Range(0, 1439)] private int fallbackWindowStartMinuteOfDay = 8 * 60;
    [SerializeField, Range(1, 1440)] private int fallbackWindowEndMinuteOfDay = 12 * 60;

    [Header("Fiabilidad y retrasos")]
    [SerializeField, Range(0.1f, 3f)] private float delayChanceMultiplier = 1f;
    [SerializeField, Range(1, 5000)] private int minimumDelayChanceBasisPoints = 25;
    [SerializeField, Range(100, 9000)] private int maximumDelayChanceBasisPoints = 4000;
    [SerializeField, Min(0)] private int fallbackMinimumDelayMinutes = 30;
    [SerializeField, Min(0)] private int fallbackMaximumDelayMinutes = 180;

    [Header("Carga visual para 2.3H")]
    [SerializeField, Min(1)] private int smallPackageLoadUnits = 1;
    [SerializeField, Min(1)] private int mediumPackageLoadUnits = 2;
    [SerializeField, Min(1)] private int largePackageLoadUnits = 4;
    [SerializeField, Min(1)] private int lightTruckThresholdLoadUnits = 18;
    [SerializeField, Min(1)] private int visualLoadUnitCapacity = 8;
    [SerializeField, Min(1)] private int tripCapacityLoadUnits = 18;
    [SerializeField, Range(1, 3)] private int maximumSuggestedTrips = 3;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public string DeterministicSeedText => string.IsNullOrWhiteSpace(deterministicSeedText) ? "bistro-logistics-v1" : deterministicSeedText.Trim();
    public int FirstGameDayWeekday => firstGameDayWeekday;
    public int MaximumWindowSearchDays => maximumWindowSearchDays;
    public int FallbackWindowStartMinuteOfDay => fallbackWindowStartMinuteOfDay;
    public int FallbackWindowEndMinuteOfDay => fallbackWindowEndMinuteOfDay;
    public float DelayChanceMultiplier => delayChanceMultiplier;
    public int MinimumDelayChanceBasisPoints => minimumDelayChanceBasisPoints;
    public int MaximumDelayChanceBasisPoints => maximumDelayChanceBasisPoints;
    public int FallbackMinimumDelayMinutes => fallbackMinimumDelayMinutes;
    public int FallbackMaximumDelayMinutes => fallbackMaximumDelayMinutes;
    public int SmallPackageLoadUnits => smallPackageLoadUnits;
    public int MediumPackageLoadUnits => mediumPackageLoadUnits;
    public int LargePackageLoadUnits => largePackageLoadUnits;
    public int LightTruckThresholdLoadUnits => lightTruckThresholdLoadUnits;
    public int VisualLoadUnitCapacity => visualLoadUnitCapacity;
    public int TripCapacityLoadUnits => tripCapacityLoadUnits;
    public int MaximumSuggestedTrips => maximumSuggestedTrips;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        if (string.IsNullOrWhiteSpace(deterministicSeedText)) deterministicSeedText = "bistro-logistics-v1";
        firstGameDayWeekday = Mathf.Clamp(firstGameDayWeekday, 0, 6);
        maximumWindowSearchDays = Mathf.Max(1, maximumWindowSearchDays);
        fallbackWindowStartMinuteOfDay = Mathf.Clamp(fallbackWindowStartMinuteOfDay, 0, 1439);
        fallbackWindowEndMinuteOfDay = Mathf.Clamp(fallbackWindowEndMinuteOfDay, fallbackWindowStartMinuteOfDay + 1, 1440);
        delayChanceMultiplier = Mathf.Clamp(delayChanceMultiplier, 0.1f, 3f);
        minimumDelayChanceBasisPoints = Mathf.Clamp(minimumDelayChanceBasisPoints, 1, 5000);
        maximumDelayChanceBasisPoints = Mathf.Clamp(maximumDelayChanceBasisPoints, minimumDelayChanceBasisPoints, 9000);
        fallbackMinimumDelayMinutes = Mathf.Max(0, fallbackMinimumDelayMinutes);
        fallbackMaximumDelayMinutes = Mathf.Max(fallbackMinimumDelayMinutes, fallbackMaximumDelayMinutes);
        smallPackageLoadUnits = Mathf.Max(1, smallPackageLoadUnits);
        mediumPackageLoadUnits = Mathf.Max(1, mediumPackageLoadUnits);
        largePackageLoadUnits = Mathf.Max(1, largePackageLoadUnits);
        lightTruckThresholdLoadUnits = Mathf.Max(1, lightTruckThresholdLoadUnits);
        visualLoadUnitCapacity = Mathf.Max(1, visualLoadUnitCapacity);
        tripCapacityLoadUnits = Mathf.Max(1, tripCapacityLoadUnits);
        maximumSuggestedTrips = Mathf.Clamp(maximumSuggestedTrips, 1, 3);
    }
#endif
}
