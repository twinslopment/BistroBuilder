using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Expone Seat Bays calculados por la configuraciÃ³n real de una mesa.
/// No modifica capacidad, asignaciÃ³n ni reglas del sistema de seating.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RestaurantTableSeatingConfiguration))]
[RequireComponent(typeof(BistroBuilderSpatialSubject))]
public sealed class BistroBuilderTableSpatialAdapter : MonoBehaviour,
    IBistroBuilderSpatialSemanticProvider
{
    [SerializeField] private RestaurantTableSeatingConfiguration table;
    [SerializeField] private BistroBuilderSpatialSubject subject;
    [SerializeField] private RestaurantSeatRegistry seatRegistry;
    [SerializeField, Min(0.12f)] private float seatBayRadius = 0.28f;

    private readonly List<RestaurantTableSeatSlot> slots =
        new List<RestaurantTableSeatSlot>(16);
    private readonly Dictionary<int, RestaurantSeat> associatedSeats =
        new Dictionary<int, RestaurantSeat>(16);

    public string SpatialSubjectId => subject != null ? subject.SubjectId : string.Empty;

    private void Awake() => CacheReferences();

    public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null || table == null || subject == null) return 0;
        int before = results.Count;
        table.WriteCurrentSlots(slots);
        CacheAssociatedSeatsOnce();
        for (int i = 0; i < slots.Count; i++)
        {
            RestaurantTableSeatSlot slot = slots[i];
            results.Add(new BistroBuilderSpatialSemanticVolume
            {
                subjectId = subject.SubjectId,
                semanticId = "table.seat_bay." + slot.SlotIndex,
                relatedSubjectId = ResolveSeatSubjectId(slot.SlotIndex),
                role = BistroBuilderSpatialSemanticRole.SeatBay,
                layer = BistroBuilderSpatialProxyLayer.Operational,
                conflictMode = BistroBuilderSpatialConflictMode.Reservable,
                volume = BistroBuilderSpatialVolume.Circle(
                    slot.AssociationPosition,
                    Mathf.Max(0.12f, seatBayRadius)),
                critical = true
            });
        }
        return results.Count - before;
    }

    public void Configure(
        RestaurantTableSeatingConfiguration sourceTable,
        BistroBuilderSpatialSubject sourceSubject)
    {
        table = sourceTable;
        subject = sourceSubject;
        CacheReferences();
    }

    private string ResolveSeatSubjectId(int slotIndex)
    {
        if (!associatedSeats.TryGetValue(slotIndex, out RestaurantSeat seat))
            return string.Empty;
        BistroBuilderSpatialSubject seatSubject =
            seat.GetComponent<BistroBuilderSpatialSubject>();
        return seatSubject != null ? seatSubject.SubjectId : string.Empty;
    }

    private void CacheAssociatedSeatsOnce()
    {
        associatedSeats.Clear();
        if (seatRegistry == null)
            seatRegistry = FindFirstObjectByType<RestaurantSeatRegistry>(
                FindObjectsInactive.Include);

        // The canonical registry avoids a global FindObjectsByType call per
        // individual seat bay, preserving the previous lowest-instance-ID
        // tie break if transiently more than one seat occupies a slot.
        if (seatRegistry != null)
        {
            foreach (RestaurantSeat seat in seatRegistry.RegisteredSeats)
                ConsiderSeat(seat);
            return;
        }

        // Legacy scenes without an initialized seat registry: one fallback
        // discovery PER TABLE, never per slot.
        RestaurantSeat[] fallback = FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < fallback.Length; i++)
            ConsiderSeat(fallback[i]);
    }

    private void ConsiderSeat(RestaurantSeat seat)
    {
        if (seat == null || !seat.gameObject.activeInHierarchy ||
            !ReferenceEquals(seat.AssociatedTable, table) ||
            seat.AssociatedSlotIndex < 0)
            return;
        int index = seat.AssociatedSlotIndex;
        if (!associatedSeats.TryGetValue(index, out RestaurantSeat previous) ||
            previous == null || seat.GetInstanceID() < previous.GetInstanceID())
            associatedSeats[index] = seat;
    }

    private void CacheReferences()
    {
        if (table == null) table = GetComponent<RestaurantTableSeatingConfiguration>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
        if (seatRegistry == null)
            seatRegistry = FindFirstObjectByType<RestaurantSeatRegistry>(
                FindObjectsInactive.Include);
    }
}

