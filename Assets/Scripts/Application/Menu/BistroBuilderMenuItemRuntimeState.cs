using System;
using UnityEngine;

/// <summary>
/// Estado modificable de un plato concreto dentro de la carta de una
/// partida. No contiene la definición completa: solo referencia DishId.
///
/// Los valores de preparación usan 0/0 como marcador histórico de
/// "heredar del catálogo". Este marcador permite migrar menu.state antiguos
/// sin inventar datos para platos temporalmente no resueltos.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuItemRuntimeState
{
    public const int InheritedPreparationValue = 0;

    [SerializeField]
    private string dishId = string.Empty;

    [SerializeField]
    private int currentPriceCents;

    [SerializeField]
    private bool unlocked = true;

    [SerializeField]
    private bool enabled = true;

    [SerializeField]
    private bool manuallySoldOut;

    [SerializeField]
    private bool signatureDish;

    [SerializeField]
    private BistroBuilderMealServiceAvailability availableServices =
        BistroBuilderMealServiceAvailability.All;

    [SerializeField]
    private int displayOrder;

    [SerializeField]
    private int preparationDifficulty;

    [SerializeField]
    private int basePreparationSeconds;

    public string DishId => dishId;
    public int CurrentPriceCents => currentPriceCents;
    public bool Unlocked => unlocked;
    public bool Enabled => enabled;
    public bool ManuallySoldOut => manuallySoldOut;
    public bool SignatureDish => signatureDish;
    public BistroBuilderMealServiceAvailability AvailableServices =>
        availableServices;
    public int DisplayOrder => displayOrder;
    public int PreparationDifficulty => preparationDifficulty;
    public int BasePreparationSeconds => basePreparationSeconds;

    public bool InheritsPreparationFromCatalog =>
        preparationDifficulty == InheritedPreparationValue &&
        basePreparationSeconds == InheritedPreparationValue;

    public BistroBuilderMenuItemRuntimeState()
    {
    }

    /// <summary>
    /// Constructor histórico. Conserva compatibilidad con pruebas y código
    /// anterior a 2.1F usando herencia de los valores canónicos.
    /// </summary>
    public BistroBuilderMenuItemRuntimeState(
        string dishId,
        int currentPriceCents,
        bool unlocked,
        bool enabled,
        bool manuallySoldOut,
        bool signatureDish,
        BistroBuilderMealServiceAvailability availableServices,
        int displayOrder
    ) : this(
        dishId,
        currentPriceCents,
        unlocked,
        enabled,
        manuallySoldOut,
        signatureDish,
        availableServices,
        displayOrder,
        InheritedPreparationValue,
        InheritedPreparationValue
    )
    {
    }

    public BistroBuilderMenuItemRuntimeState(
        string dishId,
        int currentPriceCents,
        bool unlocked,
        bool enabled,
        bool manuallySoldOut,
        bool signatureDish,
        BistroBuilderMealServiceAvailability availableServices,
        int displayOrder,
        int preparationDifficulty,
        int basePreparationSeconds
    )
    {
        this.dishId =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        this.currentPriceCents = currentPriceCents;
        this.unlocked = unlocked;
        this.enabled = enabled;
        this.manuallySoldOut = manuallySoldOut;
        this.signatureDish = signatureDish;
        this.availableServices = availableServices;
        this.displayOrder = displayOrder;
        this.preparationDifficulty = preparationDifficulty;
        this.basePreparationSeconds = basePreparationSeconds;
    }

    public static BistroBuilderMenuItemRuntimeState FromDefinition(
        BistroBuilderDishDefinition definition,
        int displayOrder,
        bool enabled,
        bool unlocked
    )
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        return new BistroBuilderMenuItemRuntimeState(
            definition.DishId,
            definition.BasePriceCents,
            unlocked,
            enabled,
            false,
            false,
            definition.DefaultAvailability,
            displayOrder,
            definition.Complexity,
            definition.BasePreparationSeconds
        );
    }

    public BistroBuilderMenuItemRuntimeState Clone()
    {
        return new BistroBuilderMenuItemRuntimeState(
            dishId,
            currentPriceCents,
            unlocked,
            enabled,
            manuallySoldOut,
            signatureDish,
            availableServices,
            displayOrder,
            preparationDifficulty,
            basePreparationSeconds
        );
    }

    public int ResolvePreparationDifficulty(
        BistroBuilderDishDefinition definition
    )
    {
        return InheritsPreparationFromCatalog && definition != null
            ? definition.Complexity
            : preparationDifficulty;
    }

    public int ResolveBasePreparationSeconds(
        BistroBuilderDishDefinition definition
    )
    {
        return InheritsPreparationFromCatalog && definition != null
            ? definition.BasePreparationSeconds
            : basePreparationSeconds;
    }

    /// <summary>
    /// Valida únicamente los datos propios de la entrada. No exige que el
    /// DishId exista en el catálogo actual, lo que permite conservar datos de
    /// una partida cuando una definición falta temporalmente.
    /// </summary>
    public bool TryValidateStructure(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(dishId))
        {
            error = "La entrada de carta contiene un DishId inválido.";
            return false;
        }

        if (currentPriceCents < 0 ||
            currentPriceCents > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "El precio actual de " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                availableServices,
                true
            ))
        {
            error = "La disponibilidad de " + dishId + " es inválida.";
            return false;
        }

        if (displayOrder < 0)
        {
            error = "El orden de presentación de " + dishId +
                    " no puede ser negativo.";
            return false;
        }

        bool inheritedDifficulty =
            preparationDifficulty == InheritedPreparationValue;
        bool inheritedTime =
            basePreparationSeconds == InheritedPreparationValue;

        if (inheritedDifficulty != inheritedTime)
        {
            error = "La preparación de " + dishId +
                    " mezcla valores heredados y explícitos.";
            return false;
        }

        if (!inheritedDifficulty &&
            (preparationDifficulty <
                BistroBuilderDishDefinition.MinimumPreparationDifficulty ||
             preparationDifficulty >
                BistroBuilderDishDefinition.MaximumPreparationDifficulty))
        {
            error = "La dificultad de preparación de " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (!inheritedTime &&
            (basePreparationSeconds <
                BistroBuilderDishDefinition.MinimumPreparationSeconds ||
             basePreparationSeconds >
                BistroBuilderDishDefinition.MaximumPreparationSeconds))
        {
            error = "El tiempo de preparación de " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryValidate(
        BistroBuilderDishCatalogService catalogService,
        out string error
    )
    {
        if (!TryValidateStructure(out error))
        {
            return false;
        }

        if (catalogService == null ||
            !catalogService.TryGetDefinition(dishId, out _))
        {
            error = "La carta referencia un plato inexistente: " + dishId + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void SetPriceCents(int value) => currentPriceCents = value;
    internal void SetUnlocked(bool value) => unlocked = value;
    internal void SetEnabled(bool value) => enabled = value;
    internal void SetManuallySoldOut(bool value) => manuallySoldOut = value;
    internal void SetSignatureDish(bool value) => signatureDish = value;

    internal void SetAvailableServices(
        BistroBuilderMealServiceAvailability value
    )
    {
        availableServices = value;
    }

    internal void SetDisplayOrder(int value) => displayOrder = value;

    internal void SetPreparationSettings(
        int difficulty,
        int preparationSeconds
    )
    {
        preparationDifficulty = difficulty;
        basePreparationSeconds = preparationSeconds;
    }
}
