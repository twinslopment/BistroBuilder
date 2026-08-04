using UnityEngine;

/// <summary>
/// Definición canónica de una categoría visible de la carta.
///
/// La identidad persistente es CategoryId. El enum histórico se conserva
/// únicamente como puente de compatibilidad con las definiciones creadas
/// antes de 2.1B.
/// </summary>
[CreateAssetMenu(
    fileName = "DishCategoryDefinition",
    menuName = "Bistro Builder/Menu/Dish Category Definition",
    order = 90
)]
public sealed class BistroBuilderDishCategoryDefinition : ScriptableObject
{
    public const int MaximumDisplayOrder = 100000;

    [Header("Identidad estable")]

    [SerializeField]
    private string categoryId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [Header("Compatibilidad y presentación")]

    [Tooltip(
        "Indica que esta categoría representa uno de los valores del enum " +
        "histórico. Las categorías nuevas pueden dejarlo desactivado."
    )]
    [SerializeField]
    private bool hasLegacyMapping = true;

    [SerializeField]
    private BistroBuilderDishCategory legacyCategory =
        BistroBuilderDishCategory.MainCourse;

    [SerializeField]
    [Min(0)]
    private int displayOrder;

    [SerializeField]
    private bool visible = true;

    public string CategoryId => categoryId;

    public string DisplayName => displayName;

    public bool HasLegacyMapping => hasLegacyMapping;

    public BistroBuilderDishCategory LegacyCategory => legacyCategory;

    public int DisplayOrder => displayOrder;

    public bool Visible => visible;

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(categoryId))
        {
            error = "El CategoryId '" + categoryId +
                    "' no es estable o válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "La categoría " + categoryId +
                    " no tiene nombre visible.";
            return false;
        }

        if (hasLegacyMapping)
        {
            if (!System.Enum.IsDefined(
                    typeof(BistroBuilderDishCategory),
                    legacyCategory
                ))
            {
                error = "La categoría " + categoryId +
                        " contiene un valor histórico desconocido.";
                return false;
            }

            string expectedId =
                BistroBuilderDishCategoryIdUtility.FromLegacyCategory(
                    legacyCategory
                );

            if (!string.Equals(
                    categoryId,
                    expectedId,
                    System.StringComparison.Ordinal
                ))
            {
                error = "La categoría " + categoryId +
                        " no coincide con su categoría histórica " +
                        legacyCategory + ".";
                return false;
            }
        }

        if (displayOrder < 0 || displayOrder > MaximumDisplayOrder)
        {
            error = "El orden de la categoría " + categoryId +
                    " queda fuera del rango permitido.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        categoryId =
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);
        displayName = displayName != null
            ? displayName.Trim()
            : string.Empty;
        displayOrder = Mathf.Clamp(
            displayOrder,
            0,
            MaximumDisplayOrder
        );
    }
#endif
}
