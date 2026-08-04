using UnityEngine;

/// <summary>
/// Definición canónica e inmutable durante la partida de un plato.
///
/// Contiene datos de diseño compartidos por todas las partidas. Las
/// decisiones del jugador —precio actual, activación o agotado— pertenecen
/// a BistroBuilderRestaurantMenuService y nunca se escriben aquí.
/// </summary>
[CreateAssetMenu(
    fileName = "DishDefinition",
    menuName = "Bistro Builder/Menu/Dish Definition",
    order = 100
)]
public sealed class BistroBuilderDishDefinition : ScriptableObject
{
    public const int CurrentDefinitionVersion = 1;
    public const int MaximumPriceCents = 100000000;
    public const int MaximumPreparationSeconds = 86400;

    [Header("Identidad estable")]

    [Tooltip(
        "Versión de contenido de esta definición. Se incrementará cuando " +
        "una migración de definición cambie su contrato canónico."
    )]
    [SerializeField]
    [Min(1)]
    private int definitionVersion = CurrentDefinitionVersion;

    [SerializeField]
    private string dishId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [TextArea(2, 5)]
    [SerializeField]
    private string description = string.Empty;

    [Header("Clasificación")]

    [SerializeField]
    private BistroBuilderDishCategory category =
        BistroBuilderDishCategory.MainCourse;

    [Tooltip(
        "Identidad estable de categoría. El enum anterior se conserva solo " +
        "como compatibilidad de autoría y migración."
    )]
    [SerializeField]
    private string categoryId = string.Empty;

    [SerializeField]
    private BistroBuilderDishCourse course =
        BistroBuilderDishCourse.Main;

    [SerializeField]
    private BistroBuilderMealServiceAvailability defaultAvailability =
        BistroBuilderMealServiceAvailability.Lunch |
        BistroBuilderMealServiceAvailability.Dinner;

    [Header("Modalidades de servicio")]

    [Tooltip(
        "Modalidades en las que puede pedirse este artículo. Los assets " +
        "anteriores a 367H con valor None se interpretan como TableService " +
        "hasta que el instalador los normalice."
    )]
    [SerializeField]
    private BistroBuilderDishServiceModeAvailability allowedServiceModes =
        BistroBuilderDishServiceModeAvailability.TableService;

    [Header("Producción")]

    [SerializeField]
    private BistroBuilderKitchenStationType requiredStation =
        BistroBuilderKitchenStationType.HotKitchen;

    [SerializeField]
    [Min(1)]
    private int basePreparationSeconds = 300;

    [SerializeField]
    [Range(1, 10)]
    private int complexity = 1;

    [SerializeField]
    private string recipeId = string.Empty;

    [Header("Comercial")]

    [SerializeField]
    [Min(0)]
    private int basePriceCents = 1000;

    [Header("Consumo")]

    [SerializeField]
    private bool shareable;

    [SerializeField]
    [Min(1)]
    private int minimumConsumers = 1;

    [SerializeField]
    [Min(1)]
    private int maximumConsumers = 1;

    public int DefinitionVersion => definitionVersion;

    public string DishId => dishId;

    public string DisplayName => displayName;

    public string Description => description;

    public BistroBuilderDishCategory Category => category;

    public string CategoryId
    {
        get
        {
            string normalized =
                BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);

            if (BistroBuilderMenuIdUtility.IsValidStableId(normalized))
            {
                return normalized;
            }

            return BistroBuilderDishCategoryIdUtility.FromLegacyCategory(
                category
            );
        }
    }

    public bool HasExplicitCategoryId =>
        BistroBuilderMenuIdUtility.IsValidStableId(
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId)
        );

    public BistroBuilderDishCourse Course => course;

    public BistroBuilderMealServiceAvailability DefaultAvailability =>
        defaultAvailability;

    public BistroBuilderDishServiceModeAvailability AllowedServiceModes =>
        allowedServiceModes == BistroBuilderDishServiceModeAvailability.None
            ? BistroBuilderDishServiceModeAvailability.TableService
            : allowedServiceModes;

    public BistroBuilderKitchenStationType RequiredStation =>
        requiredStation;

    public bool IsAvailableForServiceMode(BistroBuilderServiceMode mode)
    {
        if (!BistroBuilderServiceModeUtility.IsDefined(mode))
        {
            return false;
        }

        BistroBuilderDishServiceModeAvailability required =
            BistroBuilderServiceModeUtility.ToAvailability(mode);

        return required != BistroBuilderDishServiceModeAvailability.None &&
               (AllowedServiceModes & required) == required;
    }

    public int BasePreparationSeconds => basePreparationSeconds;

    public int Complexity => complexity;

    public string RecipeId => recipeId;

    public int BasePriceCents => basePriceCents;

    public bool Shareable => shareable;

    public int MinimumConsumers => minimumConsumers;

    public int MaximumConsumers => maximumConsumers;

    /// <summary>
    /// Valida todos los invariantes que deben cumplirse antes de que el
    /// plato pueda entrar en el catálogo o en una partida guardada.
    /// </summary>
    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(dishId))
        {
            error = "El DishId '" + dishId + "' no es estable o válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "El plato " + dishId + " no tiene nombre visible.";
            return false;
        }

        if (definitionVersion < 1 ||
            definitionVersion > CurrentDefinitionVersion)
        {
            error = "La versión de definición del plato " + dishId +
                    " no está soportada: " + definitionVersion + ".";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            string normalizedCategoryId =
                BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);

            if (!BistroBuilderMenuIdUtility.IsValidStableId(
                    normalizedCategoryId
                ) ||
                !string.Equals(
                    categoryId,
                    normalizedCategoryId,
                    System.StringComparison.Ordinal
                ))
            {
                error = "El CategoryId del plato " + dishId +
                        " no es estable o válido.";
                return false;
            }
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(CategoryId))
        {
            error = "El plato " + dishId +
                    " no puede resolver una categoría estable.";
            return false;
        }

        if (BistroBuilderDishCategoryIdUtility.TryGetLegacyCategory(
                CategoryId,
                out BistroBuilderDishCategory mappedLegacyCategory
            ) &&
            mappedLegacyCategory != category)
        {
            error = "El plato " + dishId +
                    " contiene una categoría estable distinta de su " +
                    "clasificación histórica.";
            return false;
        }

        if (!System.Enum.IsDefined(
                typeof(BistroBuilderDishCategory),
                category
            ) ||
            !System.Enum.IsDefined(
                typeof(BistroBuilderDishCourse),
                course
            ) ||
            !System.Enum.IsDefined(
                typeof(BistroBuilderKitchenStationType),
                requiredStation
            ))
        {
            error = "El plato " + dishId +
                    " contiene una clasificación desconocida.";
            return false;
        }

        if (!BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                AllowedServiceModes,
                false
            ))
        {
            error = "El plato " + dishId +
                    " no tiene modalidades de servicio válidas.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                defaultAvailability,
                false
            ))
        {
            error = "El plato " + dishId +
                    " no tiene una disponibilidad de servicio válida.";
            return false;
        }

        if (basePriceCents < 0 ||
            basePriceCents > MaximumPriceCents)
        {
            error = "El precio base del plato " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (basePreparationSeconds < 1 ||
            basePreparationSeconds > MaximumPreparationSeconds)
        {
            error = "El tiempo de preparación del plato " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (complexity < 1 || complexity > 10)
        {
            error = "La complejidad del plato " + dishId +
                    " debe estar entre 1 y 10.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(recipeId) &&
            !BistroBuilderMenuIdUtility.IsValidStableId(recipeId))
        {
            error = "El RecipeId del plato " + dishId +
                    " no es estable o válido.";
            return false;
        }

        if (minimumConsumers < 1 || maximumConsumers < minimumConsumers)
        {
            error = "El rango de consumidores del plato " + dishId +
                    " es inválido.";
            return false;
        }

        if (!shareable &&
            (minimumConsumers != 1 || maximumConsumers != 1))
        {
            error = "Un plato individual debe declarar exactamente un consumidor.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // No se limita al máximo conocido: una versión futura debe
        // conservarse para que TryValidate la rechace explícitamente, no
        // degradarse en silencio al abrir el asset con una versión anterior.
        definitionVersion = Mathf.Max(1, definitionVersion);
        dishId = BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        categoryId =
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);
        recipeId = BistroBuilderMenuIdUtility.NormalizeStableId(recipeId);
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        basePriceCents = Mathf.Clamp(
            basePriceCents,
            0,
            MaximumPriceCents
        );
        basePreparationSeconds = Mathf.Clamp(
            basePreparationSeconds,
            1,
            MaximumPreparationSeconds
        );
        complexity = Mathf.Clamp(complexity, 1, 10);
        if (allowedServiceModes ==
            BistroBuilderDishServiceModeAvailability.None)
        {
            allowedServiceModes =
                BistroBuilderDishServiceModeAvailability.TableService;
        }

        minimumConsumers = Mathf.Max(1, minimumConsumers);
        maximumConsumers = Mathf.Max(minimumConsumers, maximumConsumers);

        if (!shareable)
        {
            minimumConsumers = 1;
            maximumConsumers = 1;
        }
    }
#endif
}
