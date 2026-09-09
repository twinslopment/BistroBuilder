using UnityEngine;

/// <summary>
/// Define un artículo disponible para el catálogo del modo edición.
/// La función concreta del artículo la determina su prefab; esta definición
/// conserva identidad, presentación y condiciones económicas de autoría.
/// </summary>
[CreateAssetMenu(
    fileName = "PlaceableItemDefinition_",
    menuName =
        "Bistro Builder/Restaurant/Edit Mode/" +
        "Placeable Item Definition"
)]
public sealed class RestaurantPlaceableItemDefinition : ScriptableObject
{
    [Header("Identidad")]
    [SerializeField]
    private string itemId = "placeable_item";

    [SerializeField]
    private string displayName = "Artículo";

    [SerializeField]
    private RestaurantPlaceableItemCategory category =
        RestaurantPlaceableItemCategory.Furniture;

    [SerializeField, TextArea(2, 6)]
    private string description;

    [SerializeField]
    private Sprite catalogIcon;

    [Header("Creación")]
    [SerializeField]
    private RestaurantPlaceableObject prefab;

    [SerializeField]
    private RestaurantEditableObjectDefinition editableDefinition;

    [Header("Economía")]
    [Tooltip("Precio de compra mostrado en euros. Finanzas lo convierte a céntimos en el límite de 3F.")]
    [SerializeField, Min(0)]
    private int purchasePrice;

    [Tooltip("Comportamiento económico al retirar una instancia ya colocada.")]
    [SerializeField]
    private RestaurantPlaceableDisposalMode disposalMode =
        RestaurantPlaceableDisposalMode.Automatic;

    [Tooltip("Porcentaje de reventa en puntos básicos. 5000 = 50 %.")]
    [SerializeField, Range(0, 10000)]
    private int resaleBasisPoints = 5000;

    [Tooltip("Coste fijo de retirada en euros. En demolición automática, 0 usa el coste porcentual por defecto.")]
    [SerializeField, Min(0)]
    private int removalCost;

    [Tooltip("Coste porcentual de demolición en puntos básicos cuando no existe coste fijo. 1500 = 15 %.")]
    [SerializeField, Range(0, 10000)]
    private int demolitionBasisPoints = 1500;

    public string ItemId => NormalizeIdentifier(itemId);

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName.Trim();

    public RestaurantPlaceableItemCategory Category => category;
    public string Description => description;
    public Sprite CatalogIcon => catalogIcon;
    public RestaurantPlaceableObject Prefab => prefab;
    public RestaurantEditableObjectDefinition EditableDefinition => editableDefinition;
    public int PurchasePrice => Mathf.Max(0, purchasePrice);
    public long PurchasePriceCents => (long)PurchasePrice * 100L;
    public RestaurantPlaceableDisposalMode DisposalMode => disposalMode;
    public int ResaleBasisPoints => Mathf.Clamp(resaleBasisPoints, 0, 10000);
    public int RemovalCost => Mathf.Max(0, removalCost);
    public long RemovalCostCents => (long)RemovalCost * 100L;
    public int DemolitionBasisPoints => Mathf.Clamp(demolitionBasisPoints, 0, 10000);
    public bool HasValidPrefab => prefab != null;

    private void OnValidate()
    {
        itemId = NormalizeIdentifier(itemId);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = "placeable_item";
        }

        purchasePrice = Mathf.Max(0, purchasePrice);
        resaleBasisPoints = Mathf.Clamp(resaleBasisPoints, 0, 10000);
        removalCost = Mathf.Max(0, removalCost);
        demolitionBasisPoints = Mathf.Clamp(demolitionBasisPoints, 0, 10000);
    }

    private static string NormalizeIdentifier(string rawIdentifier)
    {
        if (string.IsNullOrWhiteSpace(rawIdentifier))
        {
            return string.Empty;
        }

        return rawIdentifier
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }
}

public enum RestaurantPlaceableItemCategory
{
    Furniture = 0,
    Seating = 1,
    Lighting = 2,
    Decoration = 3,
    KitchenEquipment = 4,
    ServiceEquipment = 5,
    Structural = 6,
    Other = 7
}

/// <summary>
/// Política económica de retirada. Automatic aplica reventa a artículos
/// móviles y demolición a elementos estructurales.
/// </summary>
public enum RestaurantPlaceableDisposalMode
{
    Automatic = 0,
    None = 1,
    Resale = 2,
    Demolition = 3,
    ResaleWithRemovalCost = 4
}
