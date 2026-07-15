using UnityEngine;

/// <summary>
/// Define un artículo disponible para el catálogo del modo edición.
///
/// La definición es genérica y puede representar:
/// - Mesas y sillas.
/// - Iluminación.
/// - Plantas y decoración.
/// - Equipamiento de cocina.
/// - Barras y equipamiento de servicio.
/// - Elementos estructurales permitidos.
///
/// La función concreta del artículo no se codifica aquí.
/// La determina la composición de componentes de su prefab.
/// </summary>
[CreateAssetMenu(
    fileName = "PlaceableItemDefinition_",
    menuName =
        "Bistro Builder/Restaurant/Edit Mode/" +
        "Placeable Item Definition"
)]
public sealed class RestaurantPlaceableItemDefinition :
    ScriptableObject
{
    [Header("Identidad")]

    [Tooltip(
        "Identificador técnico estable del artículo. Se utilizará " +
        "en catálogo, guardado, desbloqueos y economía."
    )]
    [SerializeField]
    private string itemId =
        "placeable_item";

    [Tooltip(
        "Nombre que se mostrará al jugador."
    )]
    [SerializeField]
    private string displayName =
        "Artículo";

    [Tooltip(
        "Categoría principal utilizada para organizar el catálogo."
    )]
    [SerializeField]
    private RestaurantPlaceableItemCategory category =
        RestaurantPlaceableItemCategory.Furniture;

    [Tooltip(
        "Descripción del artículo."
    )]
    [SerializeField]
    [TextArea(2, 6)]
    private string description;

    [Tooltip(
        "Icono futuro del catálogo. Puede permanecer vacío durante " +
        "el prototipo técnico."
    )]
    [SerializeField]
    private Sprite catalogIcon;

    [Header("Creación")]

    [Tooltip(
        "Prefab que se instanciará al añadir este artículo."
    )]
    [SerializeField]
    private RestaurantPlaceableObject prefab;

    [Tooltip(
        "Reglas compartidas de movimiento, rotación y cuadrícula."
    )]
    [SerializeField]
    private RestaurantEditableObjectDefinition editableDefinition;

    [Header("Economía provisional")]

    [Tooltip(
        "Precio base de compra. La integración financiera se añadirá " +
        "posteriormente."
    )]
    [SerializeField]
    [Min(0)]
    private int purchasePrice;

    public string ItemId
    {
        get
        {
            return NormalizeIdentifier(
                itemId
            );
        }
    }

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return name;
            }

            return displayName.Trim();
        }
    }

    public RestaurantPlaceableItemCategory Category
    {
        get
        {
            return category;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public Sprite CatalogIcon
    {
        get
        {
            return catalogIcon;
        }
    }

    public RestaurantPlaceableObject Prefab
    {
        get
        {
            return prefab;
        }
    }

    public RestaurantEditableObjectDefinition EditableDefinition
    {
        get
        {
            return editableDefinition;
        }
    }

    public int PurchasePrice
    {
        get
        {
            return Mathf.Max(
                0,
                purchasePrice
            );
        }
    }

    public bool HasValidPrefab
    {
        get
        {
            return prefab != null;
        }
    }

    private void OnValidate()
    {
        itemId =
            NormalizeIdentifier(
                itemId
            );

        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId =
                "placeable_item";
        }

        purchasePrice =
            Mathf.Max(
                0,
                purchasePrice
            );
    }

    /// <summary>
    /// Mantiene los identificadores en un formato estable.
    /// </summary>
    private static string NormalizeIdentifier(
        string rawIdentifier
    )
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

/// <summary>
/// Categoría visual y funcional utilizada por el catálogo.
/// </summary>
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
