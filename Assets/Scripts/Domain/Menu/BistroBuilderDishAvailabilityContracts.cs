using System;

/// <summary>
/// Estado derivado de un plato frente a carta, servicio e inventario.
/// No se persiste: siempre se recalcula desde datos autoritativos.
/// </summary>
public enum BistroBuilderDishAvailabilityState
{
    Available = 0,
    LowStock = 1,
    OutOfStock = 2,
    ManuallyPaused = 3,
    InvalidRecipe = 4,
    UnavailableForService = 5,
    Locked = 6,
    Disabled = 7
}

/// <summary>
/// Fotografía inmutable de disponibilidad de un plato.
/// </summary>
public readonly struct BistroBuilderDishAvailabilitySnapshot
{
    public string DishId { get; }
    public BistroBuilderDishAvailabilityState State { get; }
    public long AvailablePortions { get; }
    public string LimitingIngredientId { get; }
    public long LimitingIngredientAvailableCanonicalMilliUnits { get; }
    public long LimitingIngredientRequiredCanonicalMilliUnits { get; }
    public int Revision { get; }
    public string Reason { get; }

    public bool IsOrderable =>
        State == BistroBuilderDishAvailabilityState.Available ||
        State == BistroBuilderDishAvailabilityState.LowStock;

    public BistroBuilderDishAvailabilitySnapshot(
        string dishId,
        BistroBuilderDishAvailabilityState state,
        long availablePortions,
        string limitingIngredientId,
        long limitingIngredientAvailableCanonicalMilliUnits,
        long limitingIngredientRequiredCanonicalMilliUnits,
        int revision,
        string reason
    )
    {
        DishId = dishId ?? string.Empty;
        State = state;
        AvailablePortions = Math.Max(0L, availablePortions);
        LimitingIngredientId = limitingIngredientId ?? string.Empty;
        LimitingIngredientAvailableCanonicalMilliUnits = Math.Max(
            0L,
            limitingIngredientAvailableCanonicalMilliUnits
        );
        LimitingIngredientRequiredCanonicalMilliUnits = Math.Max(
            0L,
            limitingIngredientRequiredCanonicalMilliUnits
        );
        Revision = Math.Max(0, revision);
        Reason = reason ?? string.Empty;
    }
}

public readonly struct BistroBuilderDishAvailabilityChangedEvent
{
    public BistroBuilderDishAvailabilitySnapshot Previous { get; }
    public BistroBuilderDishAvailabilitySnapshot Current { get; }

    public BistroBuilderDishAvailabilityChangedEvent(
        BistroBuilderDishAvailabilitySnapshot previous,
        BistroBuilderDishAvailabilitySnapshot current
    )
    {
        Previous = previous;
        Current = current;
    }
}
