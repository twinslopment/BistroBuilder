using System;

/// <summary>
/// Filtros de presentación publicados por el editor jugable de carta.
/// No se persisten y no afectan a la oferta ni al borrador.
/// </summary>
public enum BistroBuilderMenuEditorFilter
{
    All = 0,
    Included = 1,
    Active = 2,
    Signature = 3,
    NeedsAttention = 4
}

/// <summary>
/// Motivo consolidado por el que debe reconstruirse la vista 2.1E.
/// </summary>
public enum BistroBuilderMenuEditorChangeType
{
    Opened = 0,
    Closed = 1,
    DraftChanged = 2,
    ContextChanged = 3,
    AvailabilityChanged = 4,
    ConflictDetected = 5,
    Applied = 6,
    Discarded = 7,
    Reloaded = 8
}

/// <summary>
/// Evento ligero del editor. La UI solicita una fotografía nueva en vez de
/// recibir colecciones mutables dentro del evento.
/// </summary>
public readonly struct BistroBuilderMenuEditorChangedEvent
{
    public BistroBuilderMenuEditorChangeType ChangeType { get; }

    public string DishId { get; }

    public string Message { get; }

    public int Revision { get; }

    public BistroBuilderMenuEditorChangedEvent(
        BistroBuilderMenuEditorChangeType changeType,
        string dishId,
        string message,
        int revision
    )
    {
        ChangeType = changeType;
        DishId = dishId ?? string.Empty;
        Message = message ?? string.Empty;
        Revision = Math.Max(0, revision);
    }
}

/// <summary>
/// Cabecera inmutable de una reconstrucción completa del editor.
/// </summary>
public readonly struct BistroBuilderMenuEditorSummarySnapshot
{
    public string RestaurantId { get; }

    public BistroBuilderMealServiceAvailability MealService { get; }

    public BistroBuilderServiceMode ServiceMode { get; }

    public BistroBuilderMenuEditSessionState SessionState { get; }

    public int DraftChangeCount { get; }

    public int CatalogDishCount { get; }

    public int IncludedDishCount { get; }

    public int ActiveDishCount { get; }

    public int SignatureDishCount { get; }

    public int AttentionCount { get; }

    public bool HasExternalConflict { get; }

    public bool InventoryReady { get; }

    public string InventoryStatus { get; }

    public BistroBuilderMenuEditorSummarySnapshot(
        string restaurantId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        BistroBuilderMenuEditSessionState sessionState,
        int draftChangeCount,
        int catalogDishCount,
        int includedDishCount,
        int activeDishCount,
        int signatureDishCount,
        int attentionCount,
        bool hasExternalConflict,
        bool inventoryReady,
        string inventoryStatus
    )
    {
        RestaurantId = restaurantId ?? string.Empty;
        MealService = mealService;
        ServiceMode = serviceMode;
        SessionState = sessionState;
        DraftChangeCount = Math.Max(0, draftChangeCount);
        CatalogDishCount = Math.Max(0, catalogDishCount);
        IncludedDishCount = Math.Max(0, includedDishCount);
        ActiveDishCount = Math.Max(0, activeDishCount);
        SignatureDishCount = Math.Max(0, signatureDishCount);
        AttentionCount = Math.Max(0, attentionCount);
        HasExternalConflict = hasExternalConflict;
        InventoryReady = inventoryReady;
        InventoryStatus = inventoryStatus ?? string.Empty;
    }
}

/// <summary>
/// Fotografía inmutable de un plato dentro del editor 2.1E. Une definición,
/// borrador, oferta previsualizada y escandallo sin exponer referencias
/// mutables de la sesión.
/// </summary>
public sealed class BistroBuilderMenuEditorDishSnapshot
{
    public string DishId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string CategoryId { get; }

    public string CategoryName { get; }

    public int CategoryDisplayOrder { get; }

    public BistroBuilderDishCourse Course { get; }

    public BistroBuilderKitchenStationType RequiredStation { get; }

    public BistroBuilderDishServiceModeAvailability AllowedServiceModes
    {
        get;
    }

    public int BasePriceCents { get; }

    public int CurrentPriceCents { get; }

    public int DefaultPreparationDifficulty { get; }

    public int PreparationDifficulty { get; }

    public int DefaultPreparationSeconds { get; }

    public int PreparationSeconds { get; }

    public bool Included { get; }

    public bool Unlocked { get; }

    public bool Enabled { get; }

    public bool ManuallySoldOut { get; }

    public bool SignatureDish { get; }

    public BistroBuilderMealServiceAvailability AvailableServices { get; }

    public int DisplayOrder { get; }

    public bool IsModified { get; }

    public bool HasValidRecipe { get; }

    public string RecipeSummary { get; }

    public bool HasValidEconomics { get; }

    public int CostPerPortionCents { get; }

    public int GrossMarginCents { get; }

    public int GrossMarginBasisPoints { get; }

    public BistroBuilderRecipeMarginBand MarginBand { get; }

    public bool IsOrderable { get; }

    public bool IsLowStock { get; }

    public long AvailablePortions { get; }

    public BistroBuilderMenuOfferBlockFlags BlockFlags { get; }

    public BistroBuilderMenuOfferRejectionReason PrimaryRejectionReason
    {
        get;
    }

    public string AvailabilityMessage { get; }

    public bool NeedsAttention =>
        Included &&
        (!IsOrderable || !HasValidRecipe || !HasValidEconomics ||
         GrossMarginCents < 0);

    public BistroBuilderMenuEditorDishSnapshot(
        string dishId,
        string displayName,
        string description,
        string categoryId,
        string categoryName,
        int categoryDisplayOrder,
        BistroBuilderDishCourse course,
        BistroBuilderKitchenStationType requiredStation,
        BistroBuilderDishServiceModeAvailability allowedServiceModes,
        int basePriceCents,
        int currentPriceCents,
        int defaultPreparationDifficulty,
        int preparationDifficulty,
        int defaultPreparationSeconds,
        int preparationSeconds,
        bool included,
        bool unlocked,
        bool enabled,
        bool manuallySoldOut,
        bool signatureDish,
        BistroBuilderMealServiceAvailability availableServices,
        int displayOrder,
        bool isModified,
        bool hasValidRecipe,
        string recipeSummary,
        bool hasValidEconomics,
        int costPerPortionCents,
        int grossMarginCents,
        int grossMarginBasisPoints,
        BistroBuilderRecipeMarginBand marginBand,
        bool isOrderable,
        bool isLowStock,
        long availablePortions,
        BistroBuilderMenuOfferBlockFlags blockFlags,
        BistroBuilderMenuOfferRejectionReason primaryRejectionReason,
        string availabilityMessage
    )
    {
        DishId = dishId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        CategoryId = categoryId ?? string.Empty;
        CategoryName = categoryName ?? string.Empty;
        CategoryDisplayOrder = Math.Max(0, categoryDisplayOrder);
        Course = course;
        RequiredStation = requiredStation;
        AllowedServiceModes = allowedServiceModes;
        BasePriceCents = Math.Max(0, basePriceCents);
        CurrentPriceCents = Math.Max(0, currentPriceCents);
        DefaultPreparationDifficulty = Math.Max(
            BistroBuilderDishDefinition.MinimumPreparationDifficulty,
            Math.Min(
                BistroBuilderDishDefinition.MaximumPreparationDifficulty,
                defaultPreparationDifficulty
            )
        );
        PreparationDifficulty = Math.Max(
            BistroBuilderDishDefinition.MinimumPreparationDifficulty,
            Math.Min(
                BistroBuilderDishDefinition.MaximumPreparationDifficulty,
                preparationDifficulty
            )
        );
        DefaultPreparationSeconds = Math.Max(
            BistroBuilderDishDefinition.MinimumPreparationSeconds,
            Math.Min(
                BistroBuilderDishDefinition.MaximumPreparationSeconds,
                defaultPreparationSeconds
            )
        );
        PreparationSeconds = Math.Max(
            BistroBuilderDishDefinition.MinimumPreparationSeconds,
            Math.Min(
                BistroBuilderDishDefinition.MaximumPreparationSeconds,
                preparationSeconds
            )
        );
        Included = included;
        Unlocked = unlocked;
        Enabled = enabled;
        ManuallySoldOut = manuallySoldOut;
        SignatureDish = signatureDish;
        AvailableServices = availableServices;
        DisplayOrder = Math.Max(0, displayOrder);
        IsModified = isModified;
        HasValidRecipe = hasValidRecipe;
        RecipeSummary = recipeSummary ?? string.Empty;
        HasValidEconomics = hasValidEconomics;
        CostPerPortionCents = Math.Max(0, costPerPortionCents);
        GrossMarginCents = grossMarginCents;
        GrossMarginBasisPoints = grossMarginBasisPoints;
        MarginBand = marginBand;
        IsOrderable = isOrderable;
        IsLowStock = isLowStock;
        AvailablePortions = Math.Max(0L, availablePortions);
        BlockFlags = blockFlags;
        PrimaryRejectionReason = primaryRejectionReason;
        AvailabilityMessage = availabilityMessage ?? string.Empty;
    }
}
