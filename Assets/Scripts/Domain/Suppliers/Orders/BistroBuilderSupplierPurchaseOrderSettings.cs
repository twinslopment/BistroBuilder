using UnityEngine;

[CreateAssetMenu(
    fileName = "BistroBuilderSupplierPurchaseOrderSettings",
    menuName = "Bistro Builder/Proveedores/Ajustes de pedidos de compra"
)]
public sealed class BistroBuilderSupplierPurchaseOrderSettings : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.orders.settings";
    public const int CurrentSchemaVersion = 1;

    [SerializeField] private string schemaId = CurrentSchemaId;
    [SerializeField] private int schemaVersion = CurrentSchemaVersion;

    [Header("Identidad")]
    [SerializeField] private string currencyCode = "EUR";
    [SerializeField] private string displayCodePrefix = "PO";

    [Header("Límites defensivos")]
    [SerializeField, Min(1)] private int maximumLinesPerOrder = 64;
    [SerializeField, Min(128)] private int maximumOrdersInSnapshot = 4096;

    [Header("Cancelación")]
    [SerializeField] private bool allowCancelConfirmed = true;
    [SerializeField] private bool allowCancelPendingDelivery = true;

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public string CurrencyCode => string.IsNullOrWhiteSpace(currencyCode) ? "EUR" : currencyCode.Trim().ToUpperInvariant();
    public string DisplayCodePrefix => string.IsNullOrWhiteSpace(displayCodePrefix) ? "PO" : displayCodePrefix.Trim().ToUpperInvariant();
    public int MaximumLinesPerOrder => maximumLinesPerOrder;
    public int MaximumOrdersInSnapshot => maximumOrdersInSnapshot;
    public bool AllowCancelConfirmed => allowCancelConfirmed;
    public bool AllowCancelPendingDelivery => allowCancelPendingDelivery;

#if UNITY_EDITOR
    public void EditorEnsureSchemaAndDefaults()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "EUR" : currencyCode.Trim().ToUpperInvariant();
        displayCodePrefix = string.IsNullOrWhiteSpace(displayCodePrefix) ? "PO" : displayCodePrefix.Trim().ToUpperInvariant();
        maximumLinesPerOrder = Mathf.Max(1, maximumLinesPerOrder);
        maximumOrdersInSnapshot = Mathf.Max(128, maximumOrdersInSnapshot);
    }
#endif
}
