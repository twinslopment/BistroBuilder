using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantiene la topología confirmada entre mesas y sillas.
///
/// Solo recalcula ante eventos de colocación, historial, altas o
/// bajas. No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seating Topology Service"
)]
public sealed class RestaurantSeatingTopologyService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantSeatRegistry seatRegistry;

    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [SerializeField]
    private RestaurantPlacementTransactionService transactionService;

    [SerializeField]
    private RestaurantPlacementHistoryService historyService;

    [Header("Depuración")]

    [SerializeField]
    private bool logTopologySummary = true;

    private readonly List<RestaurantSeat>
        seatBuffer =
            new List<RestaurantSeat>(32);

    private readonly List<RestaurantTable>
        tableBuffer =
            new List<RestaurantTable>(16);

    private readonly List<TableSlotState>
        tableSlotStates =
            new List<TableSlotState>(16);

    private readonly List<RestaurantTableSeatSlot>
        slotGenerationBuffer =
            new List<RestaurantTableSeatSlot>(16);

    private Coroutine rebuildRoutine;

    public event Action TopologyRebuilt;

    public int AssociatedSeatCount { get; private set; }

    public int UnassociatedSeatCount { get; private set; }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
        RequestRebuild();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
            rebuildRoutine = null;
        }
    }

    /// <summary>
    /// Agrupa varios eventos del mismo frame en una sola reconstrucción.
    /// </summary>
    public void RequestRebuild()
    {
        if (!isActiveAndEnabled ||
            rebuildRoutine != null)
        {
            return;
        }

        rebuildRoutine =
            StartCoroutine(
                RebuildNextFrameRoutine()
            );
    }

    public void RebuildImmediately()
    {
        if (rebuildRoutine != null)
        {
            StopCoroutine(rebuildRoutine);
            rebuildRoutine = null;
        }

        BuildTopology();
    }

    private IEnumerator RebuildNextFrameRoutine()
    {
        yield return null;

        rebuildRoutine = null;
        BuildTopology();
    }

    private void BuildTopology()
    {
        CollectObjects();
        BuildTableSlotStates();

        AssociatedSeatCount = 0;
        UnassociatedSeatCount = 0;

        for (int index = 0;
             index < seatBuffer.Count;
             index++)
        {
            seatBuffer[index].ApplyTopology(
                null,
                -1,
                RestaurantSeatTopologyStatus.Unassigned,
                "Pendiente de asociación."
            );
        }

        for (int seatIndex = 0;
             seatIndex < seatBuffer.Count;
             seatIndex++)
        {
            RestaurantSeat seat =
                seatBuffer[seatIndex];

            RestaurantSeatSlotMatch bestMatch =
                RestaurantSeatSlotMatch.Invalid();

            TableSlotState bestState = null;
            int bestSlotArrayIndex = -1;

            for (int tableIndex = 0;
                 tableIndex < tableSlotStates.Count;
                 tableIndex++)
            {
                TableSlotState state =
                    tableSlotStates[tableIndex];

                for (int slotIndex = 0;
                     slotIndex < state.Slots.Count;
                     slotIndex++)
                {
                    if (state.Occupied[slotIndex])
                    {
                        continue;
                    }

                    bool matched =
                        state.Configuration
                            .TryEvaluateSeatAgainstSlot(
                                seat,
                                seat.transform.position,
                                seat.transform.rotation,
                                state.Slots[slotIndex],
                                out RestaurantSeatSlotMatch match
                            );

                    if (!matched ||
                        match.Score >= bestMatch.Score)
                    {
                        continue;
                    }

                    bestMatch = match;
                    bestState = state;
                    bestSlotArrayIndex = slotIndex;
                }
            }

            if (bestState == null ||
                bestSlotArrayIndex < 0)
            {
                seat.ApplyTopology(
                    null,
                    -1,
                    RestaurantSeatTopologyStatus.NoCompatibleTable,
                    "La silla no coincide con una plaza válida."
                );

                UnassociatedSeatCount++;
                continue;
            }

            bestState.Occupied[bestSlotArrayIndex] = true;

            seat.ApplyTopology(
                bestState.Configuration,
                bestMatch.Slot.SlotIndex,
                RestaurantSeatTopologyStatus.Associated,
                "Asociada a la mesa " +
                bestState.Configuration.Table.TableId +
                ", plaza " +
                bestMatch.Slot.SlotIndex +
                "."
            );

            AssociatedSeatCount++;
        }

        if (logTopologySummary)
        {
            Debug.Log(
                nameof(RestaurantSeatingTopologyService) +
                " ha resuelto " +
                AssociatedSeatCount +
                " silla(s) asociada(s) y " +
                UnassociatedSeatCount +
                " sin asociación.",
                this
            );
        }

        TopologyRebuilt?.Invoke();
    }

    private void CollectObjects()
    {
        seatBuffer.Clear();
        tableBuffer.Clear();

        if (seatRegistry != null)
        {
            foreach (RestaurantSeat seat
                     in seatRegistry.RegisteredSeats)
            {
                if (seat != null &&
                    seat.isActiveAndEnabled)
                {
                    seatBuffer.Add(seat);
                }
            }
        }

        if (tableRegistry != null)
        {
            foreach (RestaurantTable table
                     in tableRegistry.RegisteredTables)
            {
                if (table != null &&
                    table.isActiveAndEnabled)
                {
                    tableBuffer.Add(table);
                }
            }
        }

        seatBuffer.Sort(CompareSeats);
        tableBuffer.Sort(CompareTables);
    }

    private void BuildTableSlotStates()
    {
        tableSlotStates.Clear();

        for (int index = 0;
             index < tableBuffer.Count;
             index++)
        {
            RestaurantTable table =
                tableBuffer[index];

            if (!table.TryGetComponent(
                    out RestaurantTableSeatingConfiguration configuration
                ) ||
                configuration.Definition == null)
            {
                continue;
            }

            configuration.WriteCurrentSlots(
                slotGenerationBuffer
            );

            if (slotGenerationBuffer.Count == 0)
            {
                continue;
            }

            tableSlotStates.Add(
                new TableSlotState(
                    configuration,
                    slotGenerationBuffer
                )
            );
        }
    }

    private void HandleSeatRegistered(RestaurantSeat seat)
    {
        RequestRebuild();
    }

    private void HandleSeatUnregistered(RestaurantSeat seat)
    {
        RequestRebuild();
    }

    private void HandleTableRegistered(RestaurantTable table)
    {
        RequestRebuild();
    }

    private void HandleTableUnregistered(RestaurantTable table)
    {
        RequestRebuild();
    }

    private void HandlePlacementCommitted(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result
    )
    {
        RequestRebuild();
    }

    private void HandleUndoPerformed(RestaurantAreaMember member)
    {
        RequestRebuild();
    }

    private void HandleRedoPerformed(RestaurantAreaMember member)
    {
        RequestRebuild();
    }

    private void Subscribe()
    {
        if (seatRegistry != null)
        {
            seatRegistry.SeatRegistered -= HandleSeatRegistered;
            seatRegistry.SeatUnregistered -= HandleSeatUnregistered;
            seatRegistry.SeatRegistered += HandleSeatRegistered;
            seatRegistry.SeatUnregistered += HandleSeatUnregistered;
        }

        if (tableRegistry != null)
        {
            tableRegistry.TableRegistered -= HandleTableRegistered;
            tableRegistry.TableUnregistered -= HandleTableUnregistered;
            tableRegistry.TableRegistered += HandleTableRegistered;
            tableRegistry.TableUnregistered += HandleTableUnregistered;
        }

        if (transactionService != null)
        {
            transactionService.PlacementCommitted -=
                HandlePlacementCommitted;

            transactionService.PlacementCommitted +=
                HandlePlacementCommitted;
        }

        if (historyService != null)
        {
            historyService.UndoPerformed -= HandleUndoPerformed;
            historyService.RedoPerformed -= HandleRedoPerformed;
            historyService.UndoPerformed += HandleUndoPerformed;
            historyService.RedoPerformed += HandleRedoPerformed;
        }
    }

    private void Unsubscribe()
    {
        if (seatRegistry != null)
        {
            seatRegistry.SeatRegistered -= HandleSeatRegistered;
            seatRegistry.SeatUnregistered -= HandleSeatUnregistered;
        }

        if (tableRegistry != null)
        {
            tableRegistry.TableRegistered -= HandleTableRegistered;
            tableRegistry.TableUnregistered -= HandleTableUnregistered;
        }

        if (transactionService != null)
        {
            transactionService.PlacementCommitted -=
                HandlePlacementCommitted;
        }

        if (historyService != null)
        {
            historyService.UndoPerformed -= HandleUndoPerformed;
            historyService.RedoPerformed -= HandleRedoPerformed;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (seatRegistry == null)
        {
            TryGetComponent(out seatRegistry);
        }

        if (tableRegistry == null)
        {
            TryGetComponent(out tableRegistry);
        }

        if (transactionService == null)
        {
            TryGetComponent(out transactionService);
        }

        if (historyService == null)
        {
            TryGetComponent(out historyService);
        }
    }

    private static int CompareSeats(
        RestaurantSeat first,
        RestaurantSeat second
    )
    {
        string firstId =
            first != null &&
            first.PlaceableObject != null
                ? first.PlaceableObject.InstanceId
                : first != null
                    ? first.name
                    : string.Empty;

        string secondId =
            second != null &&
            second.PlaceableObject != null
                ? second.PlaceableObject.InstanceId
                : second != null
                    ? second.name
                    : string.Empty;

        return string.Compare(
            firstId,
            secondId,
            StringComparison.Ordinal
        );
    }

    private static int CompareTables(
        RestaurantTable first,
        RestaurantTable second
    )
    {
        int firstId = first != null
            ? first.TableId
            : int.MaxValue;

        int secondId = second != null
            ? second.TableId
            : int.MaxValue;

        return firstId.CompareTo(secondId);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif

    private sealed class TableSlotState
    {
        public RestaurantTableSeatingConfiguration Configuration
        {
            get;
        }

        public List<RestaurantTableSeatSlot> Slots
        {
            get;
        }

        public bool[] Occupied
        {
            get;
        }

        public TableSlotState(
            RestaurantTableSeatingConfiguration configuration,
            List<RestaurantTableSeatSlot> sourceSlots
        )
        {
            Configuration = configuration;
            Slots =
                new List<RestaurantTableSeatSlot>(sourceSlots);
            Occupied = new bool[Slots.Count];
        }
    }
}
