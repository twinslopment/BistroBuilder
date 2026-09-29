using System;
using BistroBuilder.FurnitureFinishes;
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

    [SerializeField]
    private RestaurantPlaceableEnvironmentScope placementScope =
        RestaurantPlaceableEnvironmentScope.InteriorAndExterior;

    [SerializeField, TextArea(2, 6)]
    private string description;

    [SerializeField]
    private Sprite catalogIcon;

    [Header("Inspector contextual")]

    [Tooltip("Preview grande del inspector derecho. Si queda vacío se usa CatalogIcon.")]
    [SerializeField]
    private Sprite inspectorPreview;

    [Tooltip("Dimensiones físicas autoradas en centímetros: X=ancho, Y=alto, Z=fondo.")]
    [SerializeField]
    private Vector3 dimensionsCentimeters;

    [Tooltip("Perfil canónico de acabados/variantes. Opcional en artículos sin variantes.")]
    [SerializeField]
    private FurnitureFinishProfile finishProfile;

    [Tooltip("Reglas descriptivas que el inspector puede exponer. La validación runtime sigue siendo autoridad.")]
    [SerializeField]
    private RestaurantPlaceableInspectorRuleFlags inspectorRules =
        RestaurantPlaceableInspectorRuleFlags.None;

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

    [Header("Clasificación del catálogo")]
    [Tooltip("Subcategoría de presentación: plants, pictures, dividers, textiles, accessories; ceiling, wall, floor, exterior, ambient; checkout, dining, reception, support, safety; signage, organization, display, technical, auxiliary.")]
    [SerializeField] private string catalogSubcategory = "";
    public string CatalogSubcategory => catalogSubcategory ?? "";

    public string ItemId => NormalizeIdentifier(itemId);

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName.Trim();

    public RestaurantPlaceableItemCategory Category => category;
    public RestaurantPlaceableEnvironmentScope PlacementScope => placementScope;
    public string Description => description;
    public Sprite CatalogIcon => catalogIcon;
    public Sprite InspectorPreview => inspectorPreview != null ? inspectorPreview : catalogIcon;
    public Vector3 DimensionsCentimeters => dimensionsCentimeters;
    public float WidthCentimeters => Mathf.Max(0f, dimensionsCentimeters.x);
    public float HeightCentimeters => Mathf.Max(0f, dimensionsCentimeters.y);
    public float DepthCentimeters => Mathf.Max(0f, dimensionsCentimeters.z);
    public FurnitureFinishProfile FinishProfile => finishProfile;
    public RestaurantPlaceableInspectorRuleFlags InspectorRules => inspectorRules;
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
        dimensionsCentimeters = new Vector3(
            Mathf.Max(0f, dimensionsCentimeters.x),
            Mathf.Max(0f, dimensionsCentimeters.y),
            Mathf.Max(0f, dimensionsCentimeters.z));
    }

#if UNITY_EDITOR
    public bool EditorApplyInspectorMetadata(
        Sprite preview,
        Vector3 dimensionsCm,
        FurnitureFinishProfile profile,
        RestaurantPlaceableInspectorRuleFlags rules,
        bool preserveManualValues = true)
    {
        bool changed = false;

        if ((!preserveManualValues || inspectorPreview == null) &&
            preview != inspectorPreview)
        {
            inspectorPreview = preview;
            changed = true;
        }

        Vector3 sanitized = new Vector3(
            Mathf.Max(0f, dimensionsCm.x),
            Mathf.Max(0f, dimensionsCm.y),
            Mathf.Max(0f, dimensionsCm.z));

        if ((!preserveManualValues || dimensionsCentimeters.sqrMagnitude <= 0.0001f) &&
            (dimensionsCentimeters - sanitized).sqrMagnitude > 0.0001f)
        {
            dimensionsCentimeters = sanitized;
            changed = true;
        }

        if ((!preserveManualValues || finishProfile == null) &&
            profile != finishProfile)
        {
            finishProfile = profile;
            changed = true;
        }

        if ((!preserveManualValues || inspectorRules == RestaurantPlaceableInspectorRuleFlags.None) &&
            rules != inspectorRules)
        {
            inspectorRules = rules;
            changed = true;
        }

        return changed;
    }
#endif

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

[Flags]
public enum RestaurantPlaceableInspectorRuleFlags
{
    None = 0,
    FloorSurface = 1 << 0,
    RequiresClearance = 1 << 1
}

public enum RestaurantPlaceableEnvironmentScope
{
    InteriorAndExterior = 0,
    InteriorOnly = 1,
    ExteriorOnly = 2
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
