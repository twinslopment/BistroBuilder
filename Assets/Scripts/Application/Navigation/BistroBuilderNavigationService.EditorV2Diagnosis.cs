using System.Collections;
using UnityEngine;

/// <summary>
/// B11 opt-in, incremental view of the *existing* Navigation route checks.
/// Never changes navigation decisions, geometry, routes or occupancy. Each
/// yield bounds work to a single authoritative CheckConnection rather than
/// freezing the UI for the entire restaurant.
/// </summary>
public sealed partial class BistroBuilderNavigationService
{
    public int B11GridCalls { get; private set; }
    public int B11DockCalls { get; private set; }
    public readonly System.Collections.Generic.List<string> B11RouteTimings =
        new System.Collections.Generic.List<string>(32);

    private void B11CheckTimed(
        BistroBuilderCirculationHealthReport report, string id,
        Vector3 from, Vector3 to, BistroBuilderNavigationAgentMask agent,
        string label)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int beforeGrid = B11GridCalls;
        int beforeDock = B11DockCalls;
        CheckConnection(report, id, from, to, agent, label);
        watch.Stop();
        B11RouteTimings.Add(id + ":ms=" + watch.ElapsedMilliseconds +
            ":grid=" + (B11GridCalls - beforeGrid) +
            ":dock=" + (B11DockCalls - beforeDock));
    }

    public IEnumerator ScanCirculationIncrementally(
        BistroBuilderCirculationHealthReport report)
    {
        if (report == null) yield break;
        B11RouteTimings.Clear();
        CirculationHealthEvaluationCount++;
        GameObject entranceObject = GameObject.Find("RestaurantEntrancePoint");
        Transform entrance = entranceObject != null ?
            entranceObject.transform : null;
        RestaurantTable[] tables = FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        KitchenSystem[] kitchens = FindObjectsByType<KitchenSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        if (entrance == null)
            AddIssue(report, "entrance_missing",
                BistroBuilderCirculationIssueSeverity.Blocking,
                "No existe un acceso de entrada navegable.", "Entrada");

        if (entrance != null)
            for (int i = 0; i < tables.Length; i++)
            {
                RestaurantTable table = tables[i];
                if (table == null || table.CustomerApproachPoint == null)
                    continue;
                B11CheckTimed(report, "customer_table_" + table.TableId,
                    entrance.position, table.CustomerApproachPoint.position,
                    BistroBuilderNavigationAgentMask.Customer,
                    "Entrada -> mesa " + table.TableId);
                yield return null;
            }

        for (int k = 0; k < kitchens.Length; k++)
        {
            KitchenSystem kitchen = kitchens[k];
            if (kitchen == null || kitchen.PickupPoint == null) continue;
            for (int i = 0; i < tables.Length; i++)
            {
                RestaurantTable table = tables[i];
                if (table == null || table.WaiterServicePoint == null) continue;
                B11CheckTimed(report,
                    "waiter_kitchen_table_" + k + "_" + table.TableId,
                    kitchen.PickupPoint.position,
                    table.WaiterServicePoint.position,
                    BistroBuilderNavigationAgentMask.Waiter,
                    "Cocina -> mesa " + table.TableId);
                yield return null;
            }
        }

        BistroBuilderGoodsReceivingRoute receiving =
            FindFirstObjectByType<BistroBuilderGoodsReceivingRoute>();
        if (receiving != null && receiving.SupplyAccessPoint != null &&
            receiving.WarehouseDropPoint != null)
        {
            B11CheckTimed(report, "delivery_warehouse",
                receiving.SupplyAccessPoint.position,
                receiving.WarehouseDropPoint.position,
                BistroBuilderNavigationAgentMask.Delivery,
                "Acceso suministros -> almacen");
            yield return null;
        }

        LastHealthReport = report;
    }
}
