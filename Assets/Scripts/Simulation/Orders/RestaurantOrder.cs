using System;

public sealed class RestaurantOrder
{
    public event Action<RestaurantOrder, OrderState> StateChanged;

    public int OrderId { get; }
    public RestaurantTable Table { get; }
    public CustomerGroup CustomerGroup { get; }
    public Waiter AssignedWaiter { get; }

    public OrderState CurrentState { get; private set; }

    public bool IsFinished =>
        CurrentState == OrderState.Completed ||
        CurrentState == OrderState.Cancelled;

    public RestaurantOrder(
        int orderId,
        RestaurantTable table,
        CustomerGroup customerGroup,
        Waiter assignedWaiter
    )
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        Table = table ??
            throw new ArgumentNullException(nameof(table));

        CustomerGroup = customerGroup ??
            throw new ArgumentNullException(nameof(customerGroup));

        AssignedWaiter = assignedWaiter ??
            throw new ArgumentNullException(nameof(assignedWaiter));

        OrderId = orderId;
        CurrentState = OrderState.Created;
    }

    public bool TrySetState(OrderState newState)
    {
        if (CurrentState == newState)
            return false;

        if (!CanTransitionTo(newState))
            return false;

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