using System.Collections.Generic;
using UnityEngine;

public sealed partial class BistroBuilderNewGameOpeningService
{
    // The authored scene is the furnished baseline. Essentials keeps its kitchen,
    // service equipment and a small functioning dining set, using canonical lifecycle.
    [SerializeField, Min(1)] private int essentialDiningTables = 2;

    private bool TryPrepareEssentials(out string error)
    {
        error = string.Empty;
        if (placeableRegistry == null || placeableLifecycleService == null)
        { error = "No está disponible el sistema de mobiliario."; return false; }
        var all = new List<RestaurantPlaceableObject>(placeableRegistry.RegisteredPlaceables);
        var tables = new List<RestaurantTable>();
        foreach (var item in all)
            if (item != null && item.TryGetComponent<RestaurantTable>(out var table)) tables.Add(table);
        tables.Sort((a, b) => { int z = a.transform.position.z.CompareTo(b.transform.position.z); return z != 0 ? z : a.transform.position.x.CompareTo(b.transform.position.x); });
        var keep = new HashSet<RestaurantTable>();
        for (int i = 0; i < Mathf.Min(essentialDiningTables, tables.Count); i++) keep.Add(tables[i]);
        // Determine every seat association before deactivation triggers topology updates.
        var remove = new List<RestaurantPlaceableObject>();
        foreach (var item in all)
        {
            if (item == null) continue;
            bool discard = item.ItemDefinition != null && item.ItemDefinition.Category == RestaurantPlaceableItemCategory.Decoration;
            if (item.TryGetComponent<RestaurantTable>(out var table)) discard = !keep.Contains(table);
            if (item.TryGetComponent<RestaurantSeat>(out var seat))
                discard = seat.AssociatedTable == null || !keep.Contains(seat.AssociatedTable.Table);
            if (discard) remove.Add(item);
        }
        foreach (var item in remove)
        {
            if (!placeableLifecycleService.TryDeactivateInstance(item, out _, out var deactivation))
            { error = deactivation.Message; return false; }
            if (!placeableLifecycleService.TryPermanentlyDestroyInstance(item, out var destruction))
            { error = destruction.Message; return false; }
        }
        Physics.SyncTransforms();
        return true;
    }
}
