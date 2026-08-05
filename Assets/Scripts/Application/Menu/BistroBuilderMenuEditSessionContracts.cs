using System;

/// <summary>
/// Estado observable de la sesión runtime de edición de carta.
/// </summary>
public enum BistroBuilderMenuEditSessionState
{
    Closed = 0,
    OpenClean = 1,
    OpenDirty = 2,
    Committed = 3,
    Discarded = 4,
    Conflict = 5
}

/// <summary>
/// Cambio aplicado sobre el borrador de edición.
/// </summary>
public enum BistroBuilderMenuDraftChangeType
{
    SessionOpened = 0,
    DishAdded = 1,
    DishRemoved = 2,
    EnabledChanged = 3,
    PriceChanged = 4,
    AvailabilityChanged = 5,
    SoldOutChanged = 6,
    SignatureChanged = 7,
    UnlockChanged = 8,
    OrderChanged = 9,
    SessionCommitted = 10,
    SessionDiscarded = 11,
    SessionConflict = 12,
    DefaultsRestored = 13,
    CategoryOrderChanged = 14,
    PreparationDifficultyChanged = 15,
    PreparationTimeChanged = 16
}

/// <summary>
/// Evento inmutable de una sesión de edición.
/// </summary>
public readonly struct BistroBuilderMenuEditSessionChangedEvent
{
    public string RestaurantId { get; }

    public BistroBuilderMenuDraftChangeType ChangeType { get; }

    public string DishId { get; }

    public int BaseRestaurantRevision { get; }

    public int DraftChangeCount { get; }

    public BistroBuilderMenuEditSessionState State { get; }

    public BistroBuilderMenuEditSessionChangedEvent(
        string restaurantId,
        BistroBuilderMenuDraftChangeType changeType,
        string dishId,
        int baseRestaurantRevision,
        int draftChangeCount,
        BistroBuilderMenuEditSessionState state
    )
    {
        RestaurantId = restaurantId ?? string.Empty;
        ChangeType = changeType;
        DishId = dishId ?? string.Empty;
        BaseRestaurantRevision = baseRestaurantRevision;
        DraftChangeCount = draftChangeCount;
        State = state;
    }
}

/// <summary>
/// Resultado de un commit transaccional de carta.
/// </summary>
public readonly struct BistroBuilderMenuEditCommitResult
{
    public bool Succeeded { get; }

    public bool HadChanges { get; }

    public string RestaurantId { get; }

    public int PreviousRestaurantRevision { get; }

    public int CurrentRestaurantRevision { get; }

    public int AppliedChangeCount { get; }

    public string Message { get; }

    public BistroBuilderMenuEditCommitResult(
        bool succeeded,
        bool hadChanges,
        string restaurantId,
        int previousRestaurantRevision,
        int currentRestaurantRevision,
        int appliedChangeCount,
        string message
    )
    {
        Succeeded = succeeded;
        HadChanges = hadChanges;
        RestaurantId = restaurantId ?? string.Empty;
        PreviousRestaurantRevision = previousRestaurantRevision;
        CurrentRestaurantRevision = currentRestaurantRevision;
        AppliedChangeCount = appliedChangeCount;
        Message = message ?? string.Empty;
    }
}
