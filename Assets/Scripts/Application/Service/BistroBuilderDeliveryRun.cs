using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderDeliveryRunState
{
    Planned = 0,
    Assigned = 1,
    PickingUp = 2,
    InTransit = 3,
    Completed = 4,
    Cancelled = 5
}

public enum BistroBuilderDeliveryRunItemState
{
    Assigned = 0,
    InTransit = 1,
    Served = 2,
    Failed = 3
}

/// <summary>
/// Plato físico incluido en una ronda. Conserva un destino operativo real:
/// mesa o plaza de barra, nunca una mesa proxy.
/// </summary>
public sealed class BistroBuilderDeliveryRunItem
{
    public WaiterTask Task { get; }
    public RestaurantOrder Order { get; }
    public RestaurantTable Table { get; }
    public BistroBuilderBarServiceSpot BarSpot { get; }
    public string OrderLineId { get; }
    public BistroBuilderDeliveryRunItemState State { get; private set; }

    public BistroBuilderServiceDestinationKind DestinationKind =>
        Table != null
            ? BistroBuilderServiceDestinationKind.Table
            : BistroBuilderServiceDestinationKind.BarSpot;

    public string DestinationReferenceId =>
        BistroBuilderServiceModeUtility.BuildDestinationReference(
            Table,
            BarSpot
        );

    public Transform WaiterServicePoint =>
        BistroBuilderServiceModeUtility.GetWaiterServicePoint(
            Table,
            BarSpot
        );

    public bool IsFinished =>
        State == BistroBuilderDeliveryRunItemState.Served ||
        State == BistroBuilderDeliveryRunItemState.Failed;

    public BistroBuilderDeliveryRunItem(WaiterTask task)
    {
        if (task == null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        if (task.Type != WaiterTaskType.DeliverFood ||
            task.Order == null ||
            !task.HasValidDestination)
        {
            throw new ArgumentException(
                "La ronda necesita una tarea DeliverFood con destino válido.",
                nameof(task)
            );
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(task.OrderLineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            throw new ArgumentException(
                "La tarea de reparto no contiene un LineId válido.",
                nameof(task)
            );
        }

        Task = task;
        Order = task.Order;
        Table = task.Table;
        BarSpot = task.BarSpot;
        OrderLineId = normalizedLineId;
        State = BistroBuilderDeliveryRunItemState.Assigned;
    }

    public bool HasSameDestination(BistroBuilderDeliveryRunItem other)
    {
        return other != null &&
               ReferenceEquals(Table, other.Table) &&
               ReferenceEquals(BarSpot, other.BarSpot);
    }

    public bool TryMarkInTransit()
    {
        if (State != BistroBuilderDeliveryRunItemState.Assigned)
        {
            return false;
        }

        State = BistroBuilderDeliveryRunItemState.InTransit;
        return true;
    }

    public bool TryMarkServed()
    {
        if (State != BistroBuilderDeliveryRunItemState.InTransit)
        {
            return false;
        }

        State = BistroBuilderDeliveryRunItemState.Served;
        return true;
    }

    public bool TryMarkFailed()
    {
        if (IsFinished)
        {
            return false;
        }

        State = BistroBuilderDeliveryRunItemState.Failed;
        return true;
    }
}

/// <summary>
/// Parada de una ronda. Agrupa líneas por destino operativo, tanto de mesa
/// como de barra.
/// </summary>
public sealed class BistroBuilderDeliveryStop
{
    private readonly List<BistroBuilderDeliveryRunItem> items;
    private readonly IReadOnlyList<BistroBuilderDeliveryRunItem> readOnlyItems;

    public RestaurantTable Table { get; }
    public BistroBuilderBarServiceSpot BarSpot { get; }
    public IReadOnlyList<BistroBuilderDeliveryRunItem> Items => readOnlyItems;

    public BistroBuilderServiceDestinationKind DestinationKind =>
        Table != null
            ? BistroBuilderServiceDestinationKind.Table
            : BistroBuilderServiceDestinationKind.BarSpot;

    public string DestinationReferenceId =>
        BistroBuilderServiceModeUtility.BuildDestinationReference(
            Table,
            BarSpot
        );

    public Transform WaiterServicePoint =>
        BistroBuilderServiceModeUtility.GetWaiterServicePoint(
            Table,
            BarSpot
        );

    public int RemainingLineCount
    {
        get
        {
            int count = 0;

            for (int index = 0; index < items.Count; index++)
            {
                if (!items[index].IsFinished)
                {
                    count++;
                }
            }

            return count;
        }
    }

    internal BistroBuilderDeliveryStop(
        BistroBuilderDeliveryRunItem anchor,
        List<BistroBuilderDeliveryRunItem> stopItems
    )
    {
        if (anchor == null)
        {
            throw new ArgumentNullException(nameof(anchor));
        }

        if (stopItems == null || stopItems.Count == 0)
        {
            throw new ArgumentException(
                "Una parada necesita al menos una línea.",
                nameof(stopItems)
            );
        }

        Table = anchor.Table;
        BarSpot = anchor.BarSpot;
        items = stopItems;
        readOnlyItems = items.AsReadOnly();
    }

    public bool ContainsDestinationOf(BistroBuilderDeliveryRunItem item)
    {
        return item != null &&
               ReferenceEquals(Table, item.Table) &&
               ReferenceEquals(BarSpot, item.BarSpot);
    }
}

/// <summary>
/// Una sola recogida y una ruta ordenada por destinos de mesa o barra.
/// </summary>
public sealed class BistroBuilderDeliveryRun
{
    private readonly List<BistroBuilderDeliveryRunItem> items;
    private readonly List<BistroBuilderDeliveryStop> stops;
    private readonly IReadOnlyList<BistroBuilderDeliveryRunItem> readOnlyItems;
    private readonly IReadOnlyList<BistroBuilderDeliveryStop> readOnlyStops;

    public int RunId { get; }
    public KitchenSystem SourceKitchen { get; }
    public int Capacity { get; }
    public Waiter AssignedWaiter { get; private set; }
    public BistroBuilderDeliveryRunState State { get; private set; }
    public int CurrentStopIndex { get; private set; }

    public IReadOnlyList<BistroBuilderDeliveryRunItem> Items => readOnlyItems;
    public IReadOnlyList<BistroBuilderDeliveryStop> Stops => readOnlyStops;

    public BistroBuilderDeliveryStop CurrentStop =>
        CurrentStopIndex >= 0 && CurrentStopIndex < stops.Count
            ? stops[CurrentStopIndex]
            : null;

    public int RemainingLineCount
    {
        get
        {
            int count = 0;

            for (int index = 0; index < items.Count; index++)
            {
                if (!items[index].IsFinished)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsTerminal =>
        State == BistroBuilderDeliveryRunState.Completed ||
        State == BistroBuilderDeliveryRunState.Cancelled;

    public BistroBuilderDeliveryRun(
        int runId,
        KitchenSystem sourceKitchen,
        int capacity,
        IReadOnlyList<WaiterTask> orderedTasks
    )
    {
        if (runId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runId));
        }

        SourceKitchen = sourceKitchen ??
            throw new ArgumentNullException(nameof(sourceKitchen));

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (orderedTasks == null || orderedTasks.Count == 0 ||
            orderedTasks.Count > capacity)
        {
            throw new ArgumentException(
                "La ronda no contiene un lote válido.",
                nameof(orderedTasks)
            );
        }

        RunId = runId;
        Capacity = capacity;
        CurrentStopIndex = -1;
        State = BistroBuilderDeliveryRunState.Planned;
        items = new List<BistroBuilderDeliveryRunItem>(orderedTasks.Count);
        stops = new List<BistroBuilderDeliveryStop>();

        HashSet<string> lineIds =
            new HashSet<string>(StringComparer.Ordinal);

        BistroBuilderDeliveryRunItem currentAnchor = null;
        List<BistroBuilderDeliveryRunItem> currentStopItems = null;

        for (int index = 0; index < orderedTasks.Count; index++)
        {
            BistroBuilderDeliveryRunItem item =
                new BistroBuilderDeliveryRunItem(orderedTasks[index]);

            if (!lineIds.Add(item.OrderLineId))
            {
                throw new ArgumentException(
                    "La ronda contiene un LineId duplicado: " +
                    item.OrderLineId + ".",
                    nameof(orderedTasks)
                );
            }

            items.Add(item);

            if (currentAnchor == null ||
                !currentAnchor.HasSameDestination(item))
            {
                if (currentAnchor != null)
                {
                    stops.Add(
                        new BistroBuilderDeliveryStop(
                            currentAnchor,
                            currentStopItems
                        )
                    );
                }

                currentAnchor = item;
                currentStopItems =
                    new List<BistroBuilderDeliveryRunItem>();
            }

            currentStopItems.Add(item);
        }

        stops.Add(
            new BistroBuilderDeliveryStop(
                currentAnchor,
                currentStopItems
            )
        );

        readOnlyItems = items.AsReadOnly();
        readOnlyStops = stops.AsReadOnly();
    }

    public bool TryAssignWaiter(Waiter waiter)
    {
        if (waiter == null || State != BistroBuilderDeliveryRunState.Planned)
        {
            return false;
        }

        AssignedWaiter = waiter;
        State = BistroBuilderDeliveryRunState.Assigned;
        return true;
    }

    public bool TryBeginPickup()
    {
        if (State != BistroBuilderDeliveryRunState.Assigned)
        {
            return false;
        }

        State = BistroBuilderDeliveryRunState.PickingUp;
        return true;
    }

    public bool TryMarkLineInTransit(
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (State != BistroBuilderDeliveryRunState.PickingUp ||
            !TryGetItem(order, orderLineId, out var item))
        {
            return false;
        }

        return item.TryMarkInTransit();
    }

    public bool TryBeginDelivery()
    {
        if (State != BistroBuilderDeliveryRunState.PickingUp)
        {
            return false;
        }

        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].State !=
                BistroBuilderDeliveryRunItemState.InTransit)
            {
                return false;
            }
        }

        CurrentStopIndex = 0;
        State = BistroBuilderDeliveryRunState.InTransit;
        return true;
    }

    public bool TryMarkLineServed(
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (State != BistroBuilderDeliveryRunState.InTransit ||
            !TryGetItem(order, orderLineId, out var item) ||
            CurrentStop == null ||
            !CurrentStop.ContainsDestinationOf(item))
        {
            return false;
        }

        return item.TryMarkServed();
    }

    public bool TryAdvanceStop()
    {
        if (State != BistroBuilderDeliveryRunState.InTransit ||
            CurrentStop == null ||
            CurrentStop.RemainingLineCount > 0 ||
            CurrentStopIndex + 1 >= stops.Count)
        {
            return false;
        }

        CurrentStopIndex++;
        return true;
    }

    public bool TryComplete()
    {
        if (State != BistroBuilderDeliveryRunState.InTransit ||
            RemainingLineCount != 0)
        {
            return false;
        }

        State = BistroBuilderDeliveryRunState.Completed;
        return true;
    }

    public bool TryCancel()
    {
        if (IsTerminal)
        {
            return false;
        }

        for (int index = 0; index < items.Count; index++)
        {
            if (!items[index].IsFinished)
            {
                items[index].TryMarkFailed();
            }
        }

        State = BistroBuilderDeliveryRunState.Cancelled;
        return true;
    }

    public bool ContainsLine(RestaurantOrder order, string orderLineId)
    {
        return TryGetItem(order, orderLineId, out _);
    }

    public bool TryGetItem(
        RestaurantOrder order,
        string orderLineId,
        out BistroBuilderDeliveryRunItem item
    )
    {
        item = null;
        string normalized =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (order == null ||
            !BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            return false;
        }

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderDeliveryRunItem candidate = items[index];

            if (ReferenceEquals(candidate.Order, order) &&
                string.Equals(
                    candidate.OrderLineId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }
}
