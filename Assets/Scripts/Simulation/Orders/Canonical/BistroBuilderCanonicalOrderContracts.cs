using System;
using System.Collections.Generic;

/// <summary>
/// Utilidades de identidad estable utilizadas por las comandas canónicas.
///
/// Las identidades de cliente, grupo, mesa, comanda y línea se guardan como
/// referencias de datos. Nunca dependen del nombre de un GameObject ni de la
/// posición de una entidad en una colección.
/// </summary>
public static class BistroBuilderOrderIdUtility
{
    public const int MaximumIdLength = 96;

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);

        if (normalized.Length < 3 ||
            normalized.Length > MaximumIdLength)
        {
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char character = normalized[index];

            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= '0' && character <= '9' ||
                character == '_' ||
                character == '-' ||
                character == '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    public static string NewOrderId()
    {
        return "order_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
    }

    public static string NewLineId()
    {
        return "order_line_" +
               Guid.NewGuid().ToString("N").ToLowerInvariant();
    }
}

/// <summary>
/// Estado agregado de una comanda.
///
/// Se calcula a partir de sus líneas; no constituye una segunda autoridad
/// independiente.
/// </summary>
public enum BistroBuilderCanonicalOrderState
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    ReadyForPickup = 3,
    InDelivery = 4,
    Served = 5,
    Completed = 6,
    Cancelled = 7,
    Failed = 8
}

/// <summary>
/// Estado de una unidad concreta de producción y servicio.
///
/// Una línea representa un plato físico. Dos unidades del mismo plato se
/// representan con dos líneas para conservar preparación, transporte y
/// entrega independientes.
/// </summary>
public enum BistroBuilderCanonicalOrderLineState
{
    Draft = 0,
    Submitted = 1,
    Queued = 2,
    Preparing = 3,
    ReadyForPickup = 4,
    AssignedForDelivery = 5,
    InTransit = 6,
    Served = 7,
    Consumed = 8,
    Cancelled = 9,
    Failed = 10
}

public enum BistroBuilderCanonicalOrderChangeType
{
    OrderCreated = 0,
    LineStateChanged = 1,
    OrderCancelled = 2,
    OrderRemoved = 3,
    StateRestored = 4,
    AllOrdersCleared = 5
}

public enum BistroBuilderCanonicalOrderFailureReason
{
    None = 0,
    InvalidConfiguration = 1,
    InvalidRequest = 2,
    InvalidOrderId = 3,
    InvalidLineId = 4,
    InvalidReferenceId = 5,
    DuplicateReferenceId = 6,
    DishUnavailable = 7,
    DishDefinitionNotFound = 8,
    OrderNotFound = 9,
    LineNotFound = 10,
    InvalidTransition = 11,
    OrderAlreadyTerminal = 12,
    OrderNotTerminal = 13,
    DuplicateOrderId = 14,
    DuplicateLineId = 15,
    InvalidSnapshot = 16,
    NoOrderableDishes = 17,
    NoChange = 18
}

/// <summary>
/// Regla única de transiciones de línea.
///
/// Cocina, reparto, UI y futura carga de servicio activo consultarán la misma
/// política, evitando que cada sistema invente su propia máquina de estados.
/// </summary>
public static class BistroBuilderCanonicalOrderTransitionPolicy
{
    public static bool IsTerminal(
        BistroBuilderCanonicalOrderLineState state
    )
    {
        return state == BistroBuilderCanonicalOrderLineState.Consumed ||
               state == BistroBuilderCanonicalOrderLineState.Cancelled ||
               state == BistroBuilderCanonicalOrderLineState.Failed;
    }

    public static bool CanTransition(
        BistroBuilderCanonicalOrderLineState current,
        BistroBuilderCanonicalOrderLineState target
    )
    {
        if (current == target || IsTerminal(current))
        {
            return false;
        }

        switch (current)
        {
            case BistroBuilderCanonicalOrderLineState.Draft:
                return target ==
                           BistroBuilderCanonicalOrderLineState.Submitted ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Cancelled;

            case BistroBuilderCanonicalOrderLineState.Submitted:
                return target ==
                           BistroBuilderCanonicalOrderLineState.Queued ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Cancelled ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState.Queued:
                return target ==
                           BistroBuilderCanonicalOrderLineState.Preparing ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Cancelled ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState.Preparing:
                return target ==
                           BistroBuilderCanonicalOrderLineState
                               .ReadyForPickup ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
                return target ==
                           BistroBuilderCanonicalOrderLineState
                               .AssignedForDelivery ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState
                .AssignedForDelivery:
                return target ==
                           BistroBuilderCanonicalOrderLineState.InTransit ||
                       target ==
                           BistroBuilderCanonicalOrderLineState
                               .ReadyForPickup ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState.InTransit:
                return target ==
                           BistroBuilderCanonicalOrderLineState.Served ||
                       target ==
                           BistroBuilderCanonicalOrderLineState
                               .ReadyForPickup ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            case BistroBuilderCanonicalOrderLineState.Served:
                return target ==
                           BistroBuilderCanonicalOrderLineState.Consumed ||
                       target ==
                           BistroBuilderCanonicalOrderLineState.Failed;

            default:
                return false;
        }
    }

    /// <summary>
    /// Siguiente estado normal utilizado únicamente por herramientas de
    /// prueba. Los sistemas jugables deben solicitar la transición concreta.
    /// </summary>
    public static bool TryGetNormalNextState(
        BistroBuilderCanonicalOrderLineState current,
        out BistroBuilderCanonicalOrderLineState next
    )
    {
        switch (current)
        {
            case BistroBuilderCanonicalOrderLineState.Draft:
                next = BistroBuilderCanonicalOrderLineState.Submitted;
                return true;
            case BistroBuilderCanonicalOrderLineState.Submitted:
                next = BistroBuilderCanonicalOrderLineState.Queued;
                return true;
            case BistroBuilderCanonicalOrderLineState.Queued:
                next = BistroBuilderCanonicalOrderLineState.Preparing;
                return true;
            case BistroBuilderCanonicalOrderLineState.Preparing:
                next =
                    BistroBuilderCanonicalOrderLineState.ReadyForPickup;
                return true;
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
                next =
                    BistroBuilderCanonicalOrderLineState
                        .AssignedForDelivery;
                return true;
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery:
                next = BistroBuilderCanonicalOrderLineState.InTransit;
                return true;
            case BistroBuilderCanonicalOrderLineState.InTransit:
                next = BistroBuilderCanonicalOrderLineState.Served;
                return true;
            case BistroBuilderCanonicalOrderLineState.Served:
                next = BistroBuilderCanonicalOrderLineState.Consumed;
                return true;
            default:
                next = current;
                return false;
        }
    }
}

/// <summary>
/// Datos canónicos de un plato resueltos en el momento de pedir.
/// El precio queda congelado para que cambios posteriores en carta no alteren
/// una comanda ya aceptada.
/// </summary>
public readonly struct BistroBuilderResolvedOrderDish
{
    public string DishId { get; }
    public int PriceCents { get; }
    public int DisplayOrder { get; }

    public BistroBuilderResolvedOrderDish(
        string dishId,
        int priceCents,
        int displayOrder
    )
    {
        DishId = BistroBuilderOrderIdUtility.Normalize(dishId);
        PriceCents = priceCents;
        DisplayOrder = displayOrder;
    }
}

/// <summary>
/// Contrato desacoplado entre el dominio de comandas y la carta.
/// Inventario podrá envolverlo más adelante sin modificar la comanda.
/// </summary>
public interface IBistroBuilderOrderDishResolver
{
    bool TryResolveOrderableDish(
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderResolvedOrderDish dish,
        out string rejectionReason
    );
}

[Serializable]
public sealed class BistroBuilderCanonicalOrderLineRequest
{
    public string dishId;
    public string primaryCustomerId;
    public List<string> consumerCustomerIds = new List<string>();
    public int courseIndex;

    public BistroBuilderCanonicalOrderLineRequest()
    {
    }

    public BistroBuilderCanonicalOrderLineRequest(
        string dishId,
        string primaryCustomerId,
        IEnumerable<string> consumerCustomerIds,
        int courseIndex
    )
    {
        this.dishId = dishId;
        this.primaryCustomerId = primaryCustomerId;
        this.courseIndex = courseIndex;

        if (consumerCustomerIds != null)
        {
            this.consumerCustomerIds.AddRange(consumerCustomerIds);
        }
    }
}

[Serializable]
public sealed class BistroBuilderCanonicalOrderCreationRequest
{
    public string externalReferenceId;
    public string tableReferenceId;
    public string customerGroupReferenceId;
    public BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    public List<BistroBuilderCanonicalOrderLineRequest> lines =
        new List<BistroBuilderCanonicalOrderLineRequest>();
}

public readonly struct BistroBuilderCanonicalOrderOperationResult
{
    public bool Succeeded { get; }
    public BistroBuilderCanonicalOrderFailureReason FailureReason { get; }
    public string Message { get; }
    public string OrderId { get; }
    public string LineId { get; }

    private BistroBuilderCanonicalOrderOperationResult(
        bool succeeded,
        BistroBuilderCanonicalOrderFailureReason failureReason,
        string message,
        string orderId,
        string lineId
    )
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        Message = message ?? string.Empty;
        OrderId = orderId ?? string.Empty;
        LineId = lineId ?? string.Empty;
    }

    public static BistroBuilderCanonicalOrderOperationResult Success(
        string message,
        string orderId,
        string lineId
    )
    {
        return new BistroBuilderCanonicalOrderOperationResult(
            true,
            BistroBuilderCanonicalOrderFailureReason.None,
            message,
            orderId,
            lineId
        );
    }

    public static BistroBuilderCanonicalOrderOperationResult Failure(
        BistroBuilderCanonicalOrderFailureReason reason,
        string message,
        string orderId,
        string lineId
    )
    {
        return new BistroBuilderCanonicalOrderOperationResult(
            false,
            reason,
            message,
            orderId,
            lineId
        );
    }
}

public readonly struct BistroBuilderCanonicalOrderChangedEvent
{
    public BistroBuilderCanonicalOrderChangeType ChangeType { get; }
    public string OrderId { get; }
    public string LineId { get; }
    public int Revision { get; }
    public string Description { get; }

    public BistroBuilderCanonicalOrderChangedEvent(
        BistroBuilderCanonicalOrderChangeType changeType,
        string orderId,
        string lineId,
        int revision,
        string description
    )
    {
        ChangeType = changeType;
        OrderId = orderId ?? string.Empty;
        LineId = lineId ?? string.Empty;
        Revision = revision;
        Description = description ?? string.Empty;
    }
}
