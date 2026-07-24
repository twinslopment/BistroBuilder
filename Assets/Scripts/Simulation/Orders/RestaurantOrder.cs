using System;

/// <summary>
/// Fachada operativa temporal de una comanda utilizada por el flujo de
/// servicio anterior a 367B.
///
/// Desde 367C puede quedar enlazada a una comanda canónica. En ese caso cada
/// transición legacy debe ser aprobada primero por
/// IRestaurantOrderTransitionGate. Así el flujo existente continúa
/// funcionando mientras la autoridad canónica recibe el estado de forma
/// atómica y no se producen dos versiones contradictorias de la comanda.
/// </summary>
public sealed class RestaurantOrder
{
    private readonly IRestaurantOrderTransitionGate transitionGate;

    public event Action<RestaurantOrder, OrderState> StateChanged;

    public int OrderId { get; }
    public RestaurantTable Table { get; }
    public CustomerGroup CustomerGroup { get; }
    public Waiter AssignedWaiter { get; }

    /// <summary>
    /// Identidad de la comanda canónica asociada.
    ///
    /// Puede estar vacía únicamente en pruebas o construcciones legacy que no
    /// hayan pasado todavía por la integración 367C.
    /// </summary>
    public string CanonicalOrderId { get; }

    public OrderState CurrentState { get; private set; }

    /// <summary>
    /// Motivo del último rechazo de transición.
    /// Se mantiene para diagnóstico sin escribir errores desde el dominio.
    /// </summary>
    public string LastTransitionError { get; private set; }

    public bool HasCanonicalOrder =>
        BistroBuilderOrderIdUtility.IsValid(CanonicalOrderId);

    public bool IsFinished =>
        CurrentState == OrderState.Completed ||
        CurrentState == OrderState.Cancelled;

    /// <summary>
    /// Constructor compatible con el código anterior.
    ///
    /// Se conserva para pruebas aisladas. El flujo jugable instalado con 367C
    /// utiliza el constructor interno enlazado a la autoridad canónica.
    /// </summary>
    public RestaurantOrder(
        int orderId,
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter assignedWaiter
    )
        : this(
            orderId,
            table,
            customerGroup,
            assignedWaiter,
            string.Empty,
            null
        )
    {
    }

    internal RestaurantOrder(
        int orderId,
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter assignedWaiter,
        string canonicalOrderId,
        IRestaurantOrderTransitionGate transitionGate
    )
    {
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId));
        }

        Table = table ??
            throw new ArgumentNullException(nameof(table));

        CustomerGroup = customerGroup ??
            throw new ArgumentNullException(nameof(customerGroup));

        AssignedWaiter = assignedWaiter ??
            throw new ArgumentNullException(nameof(assignedWaiter));

        string normalizedCanonicalId =
            BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (!string.IsNullOrEmpty(normalizedCanonicalId) &&
            !BistroBuilderOrderIdUtility.IsValid(normalizedCanonicalId))
        {
            throw new ArgumentException(
                "La identidad de comanda canónica no es válida.",
                nameof(canonicalOrderId)
            );
        }

        OrderId = orderId;
        CanonicalOrderId = normalizedCanonicalId;
        this.transitionGate = transitionGate;
        CurrentState = OrderState.Created;
        LastTransitionError = string.Empty;
    }

    public bool TrySetState(OrderState newState)
    {
        LastTransitionError = string.Empty;

        if (CurrentState == newState)
        {
            LastTransitionError =
                "La comanda ya se encuentra en el estado solicitado.";
            return false;
        }

        if (!CanTransitionTo(newState))
        {
            LastTransitionError =
                "La transición de " + CurrentState +
                " a " + newState + " no está permitida.";
            return false;
        }

        if (transitionGate != null &&
            !transitionGate.TryApproveTransition(
                this,
                CurrentState,
                newState,
                out string gateError
            ))
        {
            LastTransitionError = string.IsNullOrWhiteSpace(gateError)
                ? "La autoridad de comandas rechazó la transición."
                : gateError;
            return false;
        }

        CurrentState = newState;
        StateChanged?.Invoke(this, CurrentState);

        return true;
    }

    private bool CanTransitionTo(OrderState newState)
    {
        return CurrentState switch
        {
            OrderState.Created =>
                newState == OrderState.SentToKitchen ||
                newState == OrderState.Cancelled,

            OrderState.SentToKitchen =>
                newState == OrderState.Preparing ||
                newState == OrderState.Cancelled,

            OrderState.Preparing =>
                newState == OrderState.ReadyForPickup ||
                newState == OrderState.Cancelled,

            OrderState.ReadyForPickup =>
                newState == OrderState.Served ||
                newState == OrderState.Cancelled,

            OrderState.Served =>
                newState == OrderState.Completed,

            OrderState.Completed => false,
            OrderState.Cancelled => false,

            _ => false
        };
    }
}
