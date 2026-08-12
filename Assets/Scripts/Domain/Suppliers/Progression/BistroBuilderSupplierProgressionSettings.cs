using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierProgressionSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes de progresión"
)]
public sealed class BistroBuilderSupplierProgressionSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.progression.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Evaluación")]
    [SerializeField] private bool automaticEvaluationOnGameDayChange = true;
    [SerializeField] private bool lockedSuppliersRemainVisible = true;
    [SerializeField] private bool showExactUnlockRequirements = true;

    [Header("Volumen de compras")]
    [Tooltip("Un pedido empieza a contar cuando ya no puede cancelarse libremente y entra en reparto.")]
    [SerializeField] private bool countInDeliveryOrders = true;
    [SerializeField] private bool countDeliveredOrders = true;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public bool AutomaticEvaluationOnGameDayChange => automaticEvaluationOnGameDayChange;
    public bool LockedSuppliersRemainVisible => lockedSuppliersRemainVisible;
    public bool ShowExactUnlockRequirements => showExactUnlockRequirements;
    public bool CountInDeliveryOrders => countInDeliveryOrders;
    public bool CountDeliveredOrders => countDeliveredOrders;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
    }
#endif
}
