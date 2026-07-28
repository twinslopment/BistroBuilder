using System;

/// <summary>
/// Fachada operativa temporal de una comanda utilizada por los sistemas de
/// cocina, reparto y cuenta. Desde 367H el destino puede ser una mesa o una
/// plaza de barra real; nunca se crea una RestaurantTable ficticia.
/// </summary>
public sealed class RestaurantOrder
{
    private readonly IRestaurantOrderTransitionGate transitionGate;

    public event Action<RestaurantOrder, OrderState> StateChanged;

    public int OrderId { get; }
    public RestaurantTable Table { get; }
    public BistroBuilderBarServiceSpot BarSpot { get; }
    public CustomerGroup CustomerGroup { get; }
    public Waiter AssignedWaiter { get; }
    public BistroBuilderServiceMode ServiceMode { get; }

    public BistroBuilderServiceDestinationKind DestinationKind =>
        Table != null
            ? BistroBuilderServiceDestinationKind.Table
            : BistroBuilderServiceDestinationKind.BarSpot;

    public string ServiceDestinationReferenceId =>
        BistroBuilderServiceModeUtility.BuildDestinationReference(
            Table,
            BarSpot
        );

    public bool HasTableDestination => Table != null;
    public bool HasBarDestination => BarSpot != null;
    public bool HasValidDestination => HasTableDestination ^ HasBarDestination;

    /// <summary>
    /// Identidad de la comanda canónica asociada.
    /// </summary>
    public string CanonicalOrderId { get; }

    public OrderState CurrentState { get; private set; }
    public string LastTransitionError { get; private set; }

    public bool HasCanonicalOrder =>
        BistroBuilderOrderIdUtility.IsValid(CanonicalOrderId);

    public bool IsFinished =>
        CurrentState == OrderState.Completed ||
        CurrentState == OrderState.Cancelled;

    public RestaurantOrder(
        int orderId,
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter assignedWaiter
    )
        : this(
            orderId,
            table,
            null,
            customerGroup,
            assignedWaiter,
            BistroBuilderServiceMode.TableService,
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
        : this(
            orderId,
            table,
            null,
            customerGroup,
            assignedWaiter,
            BistroBuilderServiceMode.TableService,
            canonicalOrderId,
            transitionGate
        )
    {
    }

    public RestaurantOrder(
        int orderId,
        BistroBuilderBarServiceSpot barSpot,
        CustomerGroup customerGroup,
        Waiter assignedWaiter,
        BistroBuilderServiceMode serviceMode
    )
        : this(
            orderId,
            null,
            barSpot,
            customerGroup,
            assignedWaiter,
            serviceMode,
            string.Empty,
            null
        )
    {
    }

    internal RestaurantOrder(
        int orderId,
        BistroBuilderBarServiceSpot barSpot,
        CustomerGroup customerGroup,
        Waiter assignedWaiter,
        BistroBuilderServiceMode serviceMode,
        string canonicalOrderId,
        IRestaurantOrderTransitionGate transitionGate
    )
        : this(
            orderId,
            null,
            barSpot,
            customerGroup,
            assignedWaiter,
            serviceMode,
            canonicalOrderId,
            transitionGate
        )
    {
    }

    private RestaurantOrder(
        int orderId,
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot,
        CustomerGroup customerGroup,
        Waiter assignedWaiter,
        BistroBuilderServiceMode serviceMode,
        string canonicalOrderId,
        IRestaurantOrderTransitionGate transitionGate
    )
    {
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId));
        }

        if (!BistroBuilderServiceModeUtility.IsDefined(serviceMode))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceMode));
        }

        bool expectsTable =
            serviceMode == BistroBuilderServiceMode.TableService;

        if (expectsTable && (table == null || barSpot != null))
        {
            throw new ArgumentException(
                "TableService necesita exactamente una mesa."
            );
        }

        if (!expectsTable && (barSpot == null || table != null))
        {
            throw new ArgumentException(
                "Una modalidad de barra necesita exactamente una plaza."
            );
        }

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
        Table = table;
        BarSpot = barSpot;
        ServiceMode = serviceMode;
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
