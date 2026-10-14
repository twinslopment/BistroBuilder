using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persisted relationships between existing native authorities, with no copied occupancy.</summary>
public static class RestaurantBarSeatPersistence
{
    public static bool TryCapture(RestaurantPlaceableObject placeable,
        out RestaurantBarSeatLinkSaveRecord link, out string error)
    {
        link = null; error = string.Empty;
        var seat = placeable.GetComponent<BistroBuilderBarSeatBinding>();
        if (seat == null) return true;
        if (!seat.ValidateRuntimeAssociation(out error)) return false;
        var bar = seat.AttachedSpot.GetComponentInParent<BistroBuilderBarPlaceableBinding>();
        var barPlaceable = bar != null ? bar.GetComponent<RestaurantPlaceableObject>() : null;
        if (barPlaceable == null || !barPlaceable.HasInstanceId || !bar.IsRuntimeRegistered)
        { error = "The native bar seat has no persistable placeable bar identity."; return false; }
        for (int index = 0; index < bar.Spots.Count; index++)
            if (bar.Spots[index] == seat.AttachedSpot)
            {
                link = new RestaurantBarSeatLinkSaveRecord { seatInstanceId = Normalize(placeable.InstanceId),
                    barInstanceId = Normalize(barPlaceable.InstanceId), spotIndex = index };
                return true;
            }
        error = "The native seat refers to a spot outside its persisted bar."; return false;
    }

    public static bool ValidateState(RestaurantStructureSaveData data,
        Func<string, RestaurantPlaceableItemDefinition> resolveDefinition, out string error)
    {
        error = string.Empty;
        if (data.barSeatLinks == null)
        { error = "The native bar seat link list is null."; return false; }
        var records = new Dictionary<string, RestaurantPlaceableSaveRecord>(StringComparer.Ordinal);
        var stools = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in data.placeables)
        {
            records[Normalize(record.instanceId)] = record;
            if (resolveDefinition(record.itemId)?.Prefab?.GetComponent<BistroBuilderBarSeatBinding>() != null)
                stools.Add(Normalize(record.instanceId));
        }
        var usedSeats = new HashSet<string>(StringComparer.Ordinal);
        var usedSpots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in data.barSeatLinks)
        {
            if (link == null || !stools.Contains(Normalize(link.seatInstanceId)) ||
                !records.TryGetValue(Normalize(link.barInstanceId), out var barRecord) || link.spotIndex < 0 ||
                !usedSeats.Add(Normalize(link.seatInstanceId)) ||
                !usedSpots.Add(Normalize(link.barInstanceId) + ":" + link.spotIndex))
            { error = "A native bar seat link is missing, duplicated or references an unknown instance."; return false; }
            var seatRecord = records[Normalize(link.seatInstanceId)];
            var seatPrefab = resolveDefinition(seatRecord.itemId)?.Prefab;
            var barPrefab = resolveDefinition(barRecord.itemId)?.Prefab;
            var bar = barPrefab != null ? barPrefab.GetComponent<BistroBuilderBarPlaceableBinding>() : null;
            var seat = seatPrefab != null ? seatPrefab.GetComponent<BistroBuilderBarSeatBinding>() : null;
            if (bar == null || seat == null || link.spotIndex >= bar.Spots.Count ||
                Vector3.Distance(seatRecord.localScale.ToVector3(), Vector3.one) > 0.0001f ||
                Vector3.Distance(barRecord.localScale.ToVector3(), Vector3.one) > 0.0001f ||
                !seat.ValidateSavedAssociation(bar.Spots[link.spotIndex], barPrefab.transform,
                    seatRecord.worldPosition.ToVector3(), seatRecord.worldRotation.ToQuaternion(),
                    barRecord.worldPosition.ToVector3(), barRecord.worldRotation.ToQuaternion(), out error))
            { if (string.IsNullOrEmpty(error)) error = "The saved native stool/bar pose or spot contract is invalid."; return false; }
        }
        if (stools.Count != usedSeats.Count)
        { error = "A persisted bar stool lacks its native bar/spot link."; return false; }
        return true;
    }

    public static bool ValidateRestored(RestaurantStructureSaveData data,
        IReadOnlyDictionary<string, RestaurantPlaceableObject> placeables, out string error)
    {
        foreach (var link in data.barSeatLinks)
        {
            if (!placeables.TryGetValue(Normalize(link.seatInstanceId), out var seatObject) ||
                !placeables.TryGetValue(Normalize(link.barInstanceId), out var barObject))
            { error = "A native bar seat relation was not reconstructed."; return false; }
            var seat = seatObject.GetComponent<BistroBuilderBarSeatBinding>();
            var bar = barObject.GetComponent<BistroBuilderBarPlaceableBinding>();
            if (seat == null || bar == null || link.spotIndex >= bar.Spots.Count ||
                seat.AttachedSpot != bar.Spots[link.spotIndex] || !seat.ValidateRuntimeAssociation(out error))
            { error = "The reconstructed stool association differs from its saved native bar/spot IDs."; return false; }
        }
        error = string.Empty; return true;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
