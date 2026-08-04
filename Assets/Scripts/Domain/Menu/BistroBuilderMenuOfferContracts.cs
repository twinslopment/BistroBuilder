using System;

/// <summary>
/// Motivos tipados por los que un artículo de carta no puede pedirse dentro
/// de un contexto comercial concreto.
///
/// El texto visible se conserva como diagnóstico, pero las decisiones de
/// código deben utilizar este enum o BlockFlags para no depender de cadenas.
/// </summary>
public enum BistroBuilderMenuOfferRejectionReason
{
    None = 0,
    InvalidContext = 1,
    DishNotInMenu = 2,
    MissingDefinition = 3,
    Locked = 4,
    Disabled = 5,
    ManuallySoldOut = 6,
    UnavailableForMealService = 7,
    UnsupportedServiceMode = 8,
    InvalidPrice = 9,
    InvalidRecipe = 10,
    OutOfStock = 11,
    AvailabilityUnknown = 12
}

/// <summary>
/// Conjunto completo de bloqueos detectados al evaluar una oferta.
/// La razón primaria solo representa el bloqueo de mayor prioridad.
/// </summary>
[Flags]
public enum BistroBuilderMenuOfferBlockFlags
{
    None = 0,
    DishNotInMenu = 1 << 0,
    MissingDefinition = 1 << 1,
    Locked = 1 << 2,
    Disabled = 1 << 3,
    ManuallySoldOut = 1 << 4,
    UnavailableForMealService = 1 << 5,
    UnsupportedServiceMode = 1 << 6,
    InvalidPrice = 1 << 7,
    InvalidRecipe = 1 << 8,
    OutOfStock = 1 << 9,
    AvailabilityUnknown = 1 << 10
}

/// <summary>
/// Origen que invalidó y reconstruyó la oferta observable.
/// </summary>
public enum BistroBuilderMenuOfferChangeType
{
    Initialized = 0,
    MenuChanged = 1,
    AvailabilityChanged = 2,
    ActiveRestaurantChanged = 3,
    MealServiceChanged = 4,
    DependenciesRebound = 5
}

/// <summary>
/// Contexto mínimo, explícito y validable de una consulta de oferta.
/// Separa la franja del día de la modalidad operativa.
/// </summary>
public readonly struct BistroBuilderMenuOfferContext
{
    public BistroBuilderMealServiceAvailability MealService { get; }

    public BistroBuilderServiceMode ServiceMode { get; }

    public BistroBuilderMenuOfferContext(
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode
    )
    {
        MealService = mealService;
        ServiceMode = serviceMode;
    }

    public bool TryValidate(out string error)
    {
        if (!IsConcreteMealService(MealService))
        {
            error = "La oferta necesita desayuno, comida o cena como " +
                    "servicio concreto.";
            return false;
        }

        if (!BistroBuilderServiceModeUtility.IsDefined(ServiceMode))
        {
            error = "La oferta contiene una modalidad operativa desconocida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                mealService,
                false
            ))
        {
            return false;
        }

        int raw = (int)mealService;
        return raw > 0 && (raw & (raw - 1)) == 0;
    }
}

/// <summary>
/// Fotografía comercial inmutable de un plato para una franja y modalidad.
/// Une definición, estado por restaurante y disponibilidad derivada sin
/// convertir ningún dato calculado en estado persistente.
/// </summary>
public readonly struct BistroBuilderMenuOfferItemSnapshot
{
    public string RestaurantId { get; }

    public string DishId { get; }

    public string DisplayName { get; }

    public string CategoryId { get; }

    public BistroBuilderDishCourse Course { get; }

    public BistroBuilderKitchenStationType RequiredStation { get; }

    public int PriceCents { get; }

    public int DisplayOrder { get; }

    public bool SignatureDish { get; }

    public BistroBuilderMealServiceAvailability MealService { get; }

    public BistroBuilderServiceMode ServiceMode { get; }

    public BistroBuilderDishServiceModeAvailability AllowedServiceModes
    {
        get;
    }

    public BistroBuilderDishAvailabilitySnapshot Availability { get; }

    public BistroBuilderMenuOfferBlockFlags BlockFlags { get; }

    public BistroBuilderMenuOfferRejectionReason PrimaryRejectionReason
    {
        get;
    }

    public string RejectionMessage { get; }

    public int OfferRevision { get; }

    public bool IsOrderable =>
        BlockFlags == BistroBuilderMenuOfferBlockFlags.None &&
        Availability.IsOrderable;

    public bool IsLowStock =>
        Availability.State == BistroBuilderDishAvailabilityState.LowStock;

    public BistroBuilderMenuOfferItemSnapshot(
        string restaurantId,
        string dishId,
        string displayName,
        string categoryId,
        BistroBuilderDishCourse course,
        BistroBuilderKitchenStationType requiredStation,
        int priceCents,
        int displayOrder,
        bool signatureDish,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        BistroBuilderDishServiceModeAvailability allowedServiceModes,
        BistroBuilderDishAvailabilitySnapshot availability,
        BistroBuilderMenuOfferBlockFlags blockFlags,
        BistroBuilderMenuOfferRejectionReason primaryRejectionReason,
        string rejectionMessage,
        int offerRevision
    )
    {
        RestaurantId = restaurantId ?? string.Empty;
        DishId = dishId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        CategoryId = categoryId ?? string.Empty;
        Course = course;
        RequiredStation = requiredStation;
        PriceCents = Math.Max(0, priceCents);
        DisplayOrder = Math.Max(0, displayOrder);
        SignatureDish = signatureDish;
        MealService = mealService;
        ServiceMode = serviceMode;
        AllowedServiceModes = allowedServiceModes;
        Availability = availability;
        BlockFlags = blockFlags;
        PrimaryRejectionReason = primaryRejectionReason;
        RejectionMessage = rejectionMessage ?? string.Empty;
        OfferRevision = Math.Max(0, offerRevision);
    }
}

/// <summary>
/// Evento consolidado. No publica una lista mutable: el consumidor solicita
/// un nuevo snapshot cuando necesita reconstruir su vista.
/// </summary>
public readonly struct BistroBuilderMenuOfferChangedEvent
{
    public BistroBuilderMenuOfferChangeType ChangeType { get; }

    public string RestaurantId { get; }

    public string DishId { get; }

    public BistroBuilderMealServiceAvailability MealService { get; }

    public int Revision { get; }

    public string Description { get; }

    public BistroBuilderMenuOfferChangedEvent(
        BistroBuilderMenuOfferChangeType changeType,
        string restaurantId,
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        int revision,
        string description
    )
    {
        ChangeType = changeType;
        RestaurantId = restaurantId ?? string.Empty;
        DishId = dishId ?? string.Empty;
        MealService = mealService;
        Revision = Math.Max(0, revision);
        Description = description ?? string.Empty;
    }
}
