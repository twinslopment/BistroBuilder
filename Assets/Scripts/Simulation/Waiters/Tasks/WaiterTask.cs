using System;

/// <summary>
/// Representa una tarea individual de camarero. Desde 367H una tarea de
/// reparto puede dirigirse a una mesa o a una plaza de barra real.
/// </summary>
public sealed class WaiterTask
{
    public int TaskId { get; }
    public WaiterTaskType Type { get; }
    public WaiterTaskPriority Priority { get; private set; }
    public WaiterTaskState State { get; private set; }
    public RestaurantTable Table { get; }
    public BistroBuilderBarServiceSpot BarSpot { get; }
    public RestaurantOrder Order { get; }
    public string OrderLineId { get; }
    public Waiter AssignedWaiter { get; private set; }
    public long CreationSequence { get; }

    public BistroBuilderServiceDestinationKind DestinationKind =>
        Table != null
            ? BistroBuilderServiceDestinationKind.Table
            : BistroBuilderServiceDestinationKind.BarSpot;

    public string DestinationReferenceId =>
        BistroBuilderServiceModeUtility.BuildDestinationReference(
            Table,
            BarSpot
        );

    public bool HasValidDestination =>
        Table != null ^ BarSpot != null;

    public bool IsPending => State == WaiterTaskState.Pending;

    public bool CanBeAssigned =>
        State == WaiterTaskState.Pending && AssignedWaiter == null;

    public WaiterTask(
        int taskId,
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        long creationSequence
    )
        : this(
            taskId,
            type,
            priority,
            table,
            null,
            order,
            string.Empty,
            creationSequence
        )
    {
    }

    public WaiterTask(
        int taskId,
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        string orderLineId,
        long creationSequence
    )
        : this(
            taskId,
            type,
            priority,
            table,
            null,
            order,
            orderLineId,
            creationSequence
        )
    {
    }

    public WaiterTask(
        int taskId,
        WaiterTaskType type,
        WaiterTaskPriority priority,
        BistroBuilderBarServiceSpot barSpot,
        RestaurantOrder order,
        string orderLineId,
        long creationSequence
    )
        : this(
            taskId,
            type,
            priority,
            null,
            barSpot,
            order,
            orderLineId,
            creationSequence
        )
    {
    }

    private WaiterTask(
        int taskId,
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot,
        RestaurantOrder order,
        string orderLineId,
        long creationSequence
    )
    {
        if (taskId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taskId));
        }

        if (creationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creationSequence));
        }

        if (type == WaiterTaskType.DeliverFood)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if ((table == null) == (barSpot == null))
            {
                throw new ArgumentException(
                    "Una tarea de reparto necesita exactamente un destino."
                );
            }
        }
        else if (table == null || barSpot != null)
        {
            throw new ArgumentException(
                "Las tareas legacy no alimentarias necesitan una mesa."
            );
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!string.IsNullOrEmpty(normalizedLineId) &&
            !BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            throw new ArgumentException(
                "La tarea contiene un OrderLineId inválido.",
                nameof(orderLineId)
            );
        }

        TaskId = taskId;
        Type = type;
        Priority = priority;
        Table = table;
        BarSpot = barSpot;
        Order = order;
        OrderLineId = normalizedLineId;
        CreationSequence = creationSequence;
        State = WaiterTaskState.Pending;
    }

    public bool TryChangePriority(WaiterTaskPriority newPriority)
    {
        if (State != WaiterTaskState.Pending)
        {
            return false;
        }

        Priority = newPriority;
        return true;
    }

    public bool TryAssignWaiter(Waiter waiter)
    {
        if (waiter == null || !CanBeAssigned || !waiter.IsAvailable)
        {
            return false;
        }

        AssignedWaiter = waiter;
        State = WaiterTaskState.Assigned;
        return true;
    }

    public bool TryReleaseAssignment()
    {
        if (State != WaiterTaskState.Assigned)
        {
            return false;
        }

        AssignedWaiter = null;
        State = WaiterTaskState.Pending;
        return true;
    }

    public bool TryStart()
    {
        if (State != WaiterTaskState.Assigned)
        {
            return false;
        }

        State = WaiterTaskState.InProgress;
        return true;
    }

    public bool TryComplete()
    {
        if (State != WaiterTaskState.Assigned &&
            State != WaiterTaskState.InProgress)
        {
            return false;
        }

        State = WaiterTaskState.Completed;
        return true;
    }

    public bool TryCancel()
    {
        if (State == WaiterTaskState.Completed ||
            State == WaiterTaskState.Cancelled)
        {
            return false;
        }

        State = WaiterTaskState.Cancelled;
        AssignedWaiter = null;
        return true;
    }
}
