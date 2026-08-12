using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierDeliveryPresentationSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes de entrega física"
)]
public sealed class BistroBuilderSupplierDeliveryPresentationSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.delivery.presentation.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Arranque")]
    [SerializeField] private bool autoStartReadyDeliveries = true;
    [SerializeField, Min(1)] private int maximumQueuedPresentations = 8;

    [Header("Prefabs opcionales")]
    [Tooltip("Si queda vacío, 2.3H crea una furgoneta fallback con primitivas.")]
    [SerializeField] private GameObject vanPrefab;
    [Tooltip("Si queda vacío, 2.3H crea un camión ligero fallback con primitivas.")]
    [SerializeField] private GameObject lightTruckPrefab;
    [Tooltip("Si queda vacío, 2.3H crea un repartidor fallback.")]
    [SerializeField] private GameObject driverPrefab;
    [Tooltip("Si queda vacío, 2.3H crea una carretilla fallback.")]
    [SerializeField] private GameObject trolleyPrefab;
    [Tooltip("Si queda vacío, 2.3H crea cajas fallback.")]
    [SerializeField] private GameObject boxPrefab;

    [Header("Velocidades")]
    [SerializeField, Min(0.1f)] private float vehicleSpeedMetersPerSecond = 5.0f;
    [SerializeField, Min(0.1f)] private float driverSpeedMetersPerSecond = 1.7f;
    [SerializeField, Min(1f)] private float vehicleTurnDegreesPerSecond = 180f;
    [SerializeField, Min(1f)] private float driverTurnDegreesPerSecond = 360f;

    [Header("Tiempos visuales")]
    [SerializeField, Min(0f)] private float parkPauseSeconds = 0.6f;
    [SerializeField, Min(0f)] private float driverExitSeconds = 0.5f;
    [SerializeField, Min(0f)] private float rearDoorSeconds = 0.7f;
    [SerializeField, Min(0f)] private float trolleyPrepareSeconds = 0.45f;
    [SerializeField, Min(0f)] private float unloadSecondsPerTrip = 0.8f;
    [SerializeField, Min(0f)] private float trolleyStowSeconds = 0.45f;
    [SerializeField, Min(0f)] private float driverEnterSeconds = 0.45f;
    [SerializeField, Min(0f)] private float cleanupDelaySeconds = 0.5f;

    [Header("Branding obligatorio")]
    [Tooltip("Nombre y/o logo en ambos laterales. Si no hay logo, siempre se muestra el nombre.")]
    [SerializeField] private bool requireBrandingOnBothSides = true;
    [SerializeField] private bool showSupplierNameWhenLogoExists = true;
    [SerializeField, Range(0.2f, 2f)] private float brandingPanelHeight = 0.62f;
    [SerializeField, Range(0.5f, 4f)] private float brandingPanelLength = 2.1f;
    [SerializeField, Range(0.01f, 0.2f)] private float brandingPanelThickness = 0.035f;

    [Header("Carga visual")]
    [SerializeField, Range(1, 12)] private int maximumVisibleBoxesPerTrip = 6;
    [SerializeField] private Vector3 trolleyLoadLocalOffset = new Vector3(0f, 0.72f, 0f);
    [SerializeField] private Vector3 driverTrolleyFollowOffset = new Vector3(0f, 0f, -0.85f);

    [Header("Movimiento del repartidor")]
    [Tooltip("Si el prefab ya incluye NavMeshAgent y existe NavMesh válido, se usa. Si no, se usa el motor de waypoints determinista.")]
    [SerializeField] private bool preferNavMeshWhenAvailable = true;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public bool AutoStartReadyDeliveries => autoStartReadyDeliveries;
    public int MaximumQueuedPresentations => maximumQueuedPresentations;
    public GameObject VanPrefab => vanPrefab;
    public GameObject LightTruckPrefab => lightTruckPrefab;
    public GameObject DriverPrefab => driverPrefab;
    public GameObject TrolleyPrefab => trolleyPrefab;
    public GameObject BoxPrefab => boxPrefab;
    public float VehicleSpeedMetersPerSecond => vehicleSpeedMetersPerSecond;
    public float DriverSpeedMetersPerSecond => driverSpeedMetersPerSecond;
    public float VehicleTurnDegreesPerSecond => vehicleTurnDegreesPerSecond;
    public float DriverTurnDegreesPerSecond => driverTurnDegreesPerSecond;
    public float ParkPauseSeconds => parkPauseSeconds;
    public float DriverExitSeconds => driverExitSeconds;
    public float RearDoorSeconds => rearDoorSeconds;
    public float TrolleyPrepareSeconds => trolleyPrepareSeconds;
    public float UnloadSecondsPerTrip => unloadSecondsPerTrip;
    public float TrolleyStowSeconds => trolleyStowSeconds;
    public float DriverEnterSeconds => driverEnterSeconds;
    public float CleanupDelaySeconds => cleanupDelaySeconds;
    public bool RequireBrandingOnBothSides => requireBrandingOnBothSides;
    public bool ShowSupplierNameWhenLogoExists => showSupplierNameWhenLogoExists;
    public float BrandingPanelHeight => brandingPanelHeight;
    public float BrandingPanelLength => brandingPanelLength;
    public float BrandingPanelThickness => brandingPanelThickness;
    public int MaximumVisibleBoxesPerTrip => maximumVisibleBoxesPerTrip;
    public Vector3 TrolleyLoadLocalOffset => trolleyLoadLocalOffset;
    public Vector3 DriverTrolleyFollowOffset => driverTrolleyFollowOffset;
    public bool PreferNavMeshWhenAvailable => preferNavMeshWhenAvailable;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        maximumQueuedPresentations = Mathf.Max(1, maximumQueuedPresentations);
        vehicleSpeedMetersPerSecond = Mathf.Max(0.1f, vehicleSpeedMetersPerSecond);
        driverSpeedMetersPerSecond = Mathf.Max(0.1f, driverSpeedMetersPerSecond);
        vehicleTurnDegreesPerSecond = Mathf.Max(1f, vehicleTurnDegreesPerSecond);
        driverTurnDegreesPerSecond = Mathf.Max(1f, driverTurnDegreesPerSecond);
        parkPauseSeconds = Mathf.Max(0f, parkPauseSeconds);
        driverExitSeconds = Mathf.Max(0f, driverExitSeconds);
        rearDoorSeconds = Mathf.Max(0f, rearDoorSeconds);
        trolleyPrepareSeconds = Mathf.Max(0f, trolleyPrepareSeconds);
        unloadSecondsPerTrip = Mathf.Max(0f, unloadSecondsPerTrip);
        trolleyStowSeconds = Mathf.Max(0f, trolleyStowSeconds);
        driverEnterSeconds = Mathf.Max(0f, driverEnterSeconds);
        cleanupDelaySeconds = Mathf.Max(0f, cleanupDelaySeconds);
        brandingPanelHeight = Mathf.Clamp(brandingPanelHeight, 0.2f, 2f);
        brandingPanelLength = Mathf.Clamp(brandingPanelLength, 0.5f, 4f);
        brandingPanelThickness = Mathf.Clamp(brandingPanelThickness, 0.01f, 0.2f);
        maximumVisibleBoxesPerTrip = Mathf.Clamp(maximumVisibleBoxesPerTrip, 1, 12);
    }
#endif
}
