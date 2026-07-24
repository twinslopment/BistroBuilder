using System;

/// <summary>
/// Tipo de modificación aplicada a la carta runtime.
/// </summary>
public enum BistroBuilderMenuChangeType
{
    Initialized = 0,
    DishAdded = 1,
    DishRemoved = 2,
    EnabledChanged = 3,
    PriceChanged = 4,
    AvailabilityChanged = 5,
    SoldOutChanged = 6,
    SignatureChanged = 7,
    UnlockChanged = 8,
    OrderChanged = 9,
    StateReplaced = 10
}

/// <summary>
/// Motivo controlado por el que una operación de carta no puede aplicarse.
/// </summary>
public enum BistroBuilderMenuMutationFailureReason
{
    None = 0,
    InvalidConfiguration = 1,
    InvalidDishId = 2,
    DishDefinitionNotFound = 3,
    DishAlreadyExists = 4,
    DishNotInMenu = 5,
    InvalidPrice = 6,
    InvalidAvailability = 7,
    InvalidState = 8,
    NoChange = 9
}

/// <summary>
/// Resultado explícito de una operación sobre la carta.
/// </summary>
public struct BistroBuilderMenuMutationResult
{
    public bool Succeeded;
    public BistroBuilderMenuMutationFailureReason FailureReason;
    public string Message;

    public static BistroBuilderMenuMutationResult Success(string message)
    {
        return new BistroBuilderMenuMutationResult
        {
            Succeeded = true,
            FailureReason = BistroBuilderMenuMutationFailureReason.None,
            Message = message ?? string.Empty
        };
    }

    public static BistroBuilderMenuMutationResult Failure(
        BistroBuilderMenuMutationFailureReason reason,
        string message
    )
    {
        return new BistroBuilderMenuMutationResult
        {
            Succeeded = false,
            FailureReason = reason,
            Message = message ?? string.Empty
        };
    }
}

/// <summary>
/// Evento ligero de dominio publicado después de una modificación válida.
/// No expone referencias mutables del estado interno.
/// </summary>
public sealed class BistroBuilderMenuChangedEvent
{
    public BistroBuilderMenuChangeType ChangeType { get; }

    public string DishId { get; }

    public int Revision { get; }

    public string Description { get; }

    public BistroBuilderMenuChangedEvent(
        BistroBuilderMenuChangeType changeType,
        string dishId,
        int revision,
        string description
    )
    {
        ChangeType = changeType;
        DishId = dishId ?? string.Empty;
        Revision = revision;
        Description = description ?? string.Empty;
    }
}
