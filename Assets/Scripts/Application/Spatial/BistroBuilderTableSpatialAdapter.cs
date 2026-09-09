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
    [SerializeField, Min(0.12f)] private float seatBayRadius = 0.28f;

    private readonly List<RestaurantTableSeatSlot> slots =
        new List<RestaurantTableSeatSlot>(16);

    public string SpatialSubjectId => subject != null ? subject.SubjectId : string.Empty;

    private void Awake() => CacheReferences();

    public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
    {
        if (results == null || table == null || subject == null) return 0;
        int before = results.Count;
        table.WriteCurrentSlots(slots);
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
        RestaurantSeat[] seats = FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat seat = seats[i];
            if (seat == null || !ReferenceEquals(seat.AssociatedTable, table) ||
                seat.AssociatedSlotIndex != slotIndex)
                continue;
            BistroBuilderSpatialSubject seatSubject =
                seat.GetComponent<BistroBuilderSpatialSubject>();
            return seatSubject != null ? seatSubject.SubjectId : string.Empty;
        }
        return string.Empty;
    }

    private void CacheReferences()
    {
        if (table == null) table = GetComponent<RestaurantTableSeatingConfiguration>();
        if (subject == null) subject = GetComponent<BistroBuilderSpatialSubject>();
    }
}

