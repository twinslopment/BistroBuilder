using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class BistroBuilderTableHudDemoBootstrap
{
    public const string CommandLineFlag = "-bb-table-hud-demo";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!HasDemoFlag()) return;

        var host = new GameObject("BB_TableHudDemoBootstrap");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderTableHudDemoRunner>();
    }

    private static bool HasDemoFlag()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class BistroBuilderTableHudDemoRunner : MonoBehaviour
{
    private const int DemoGroupId = 9901;
    private const int DemoWaiterId = 9901;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2f);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (TrySeed()) yield break;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Debug.LogError("BB_TABLE_HUD_DEMO_FAIL|No se pudo preparar la mesa demo.");
    }

    private bool TrySeed()
    {
        var opening = FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>(
            FindObjectsInactive.Include);
        opening?.Hide();

        OrderSystem orders = FindFirstObjectByType<OrderSystem>(
            FindObjectsInactive.Include);
        BistroBuilderCanonicalOrderService canonical =
            FindFirstObjectByType<BistroBuilderCanonicalOrderService>(
                FindObjectsInactive.Include);
        CustomerGroupSpawner spawner =
            FindFirstObjectByType<CustomerGroupSpawner>(FindObjectsInactive.Include);
        TableAssignmentSystem assignments =
            FindFirstObjectByType<TableAssignmentSystem>(FindObjectsInactive.Include);

        if (orders == null || canonical == null || spawner == null || assignments == null)
            return false;
        if (!orders.ValidateConfiguration(out _))
            return false;

        RestaurantTable table = FindDemoTable();
        if (table == null)
            return false;
        if (table.AssignedCustomerGroup != null)
        {
            if (table.AssignedCustomerGroup.GroupId == DemoGroupId)
                return true;
            return false;
        }

        CustomerGroup prefab = GetPrivateField<CustomerGroup>(
            spawner, "customerGroupPrefab");
        if (prefab == null)
            return false;

        int groupSize = Mathf.Clamp(table.Capacity, 2, 4);
        Vector3 customerPosition =
            table.transform.position + new Vector3(0f, 0.15f, 0f);

        CustomerGroup group = Instantiate(
            prefab, customerPosition, Quaternion.identity);
        group.name = "BB_TableHudDemo_Customers";

        DisableFlow<CustomerArrivalFlow>(group.gameObject);
        DisableFlow<CustomerSeatingFlow>(group.gameObject);
        DisableFlow<CustomerDiningFlow>(group.gameObject);
        DisableFlow<CustomerExitFlow>(group.gameObject);
        DisableFlow<CustomerMovementView>(group.gameObject);

        if (!group.Initialize(DemoGroupId, groupSize))
            return false;

        group.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>()
            ?.EnsureVisuals();
        assignments.RegisterCustomerGroup(group);

        if (!group.AssignTable(table))
            return false;
        group.ResetWaitingTime();
        group.SetState(CustomerGroupState.Eating);

        table.SetState(TableState.WaitingForWaiter);

        GameObject waiterObject = new GameObject("BB_TableHudDemo_Waiter");
        Waiter waiter = waiterObject.AddComponent<Waiter>();
        SetPrivateField(waiter, "waiterId", DemoWaiterId);

        if (!waiter.AssignTable(table))
            return false;

        RestaurantOrder order = orders.CreateOrder(table, waiter);
        if (order == null || !order.HasCanonicalOrder)
            return false;

        SeedLineStates(canonical, order.CanonicalOrderId);

        table.SetState(TableState.Eating);
        group.SetState(CustomerGroupState.Eating);
        PositionCustomersAroundTable(group, table);

        Debug.Log(
            "BB_TABLE_HUD_DEMO_READY|Table=" + table.TableId +
            "|Customers=" + groupSize +
            "|Order=" + order.CanonicalOrderId
        );

        return true;
    }

    private static RestaurantTable FindDemoTable()
    {
        RestaurantTable[] tables =
            FindObjectsByType<RestaurantTable>(FindObjectsSortMode.None);
        RestaurantTable best = null;
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTable table = tables[i];
            if (table == null || table.AssignedCustomerGroup != null)
                continue;

            if (best == null || table.Capacity > best.Capacity)
                best = table;

            if (table.Capacity >= 4 && table.TableId == 4)
                return table;
        }

        return best;
    }

    private static void SeedLineStates(
        BistroBuilderCanonicalOrderService canonical,
        string orderId)
    {
        if (!canonical.TryGetOrderSnapshot(
                orderId,
                out BistroBuilderCanonicalOrder snapshot) ||
            snapshot == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.Lines.Count; i++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[i];
            if (line == null)
                continue;

            BistroBuilderCanonicalOrderLineState target =
                i == 0
                    ? BistroBuilderCanonicalOrderLineState.Served
                    : i == 1
                        ? BistroBuilderCanonicalOrderLineState.Preparing
                        : BistroBuilderCanonicalOrderLineState.Submitted;
            AdvanceLine(canonical, line.LineId, line.State, target);
        }
    }

    private static void AdvanceLine(
        BistroBuilderCanonicalOrderService canonical,
        string lineId,
        BistroBuilderCanonicalOrderLineState current,
        BistroBuilderCanonicalOrderLineState target)
    {
        BistroBuilderCanonicalOrderLineState[] sequence =
        {
            BistroBuilderCanonicalOrderLineState.Draft,
            BistroBuilderCanonicalOrderLineState.Submitted,
            BistroBuilderCanonicalOrderLineState.Queued,
            BistroBuilderCanonicalOrderLineState.Preparing,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
            BistroBuilderCanonicalOrderLineState.InTransit,
            BistroBuilderCanonicalOrderLineState.Served
        };

        int from = Array.IndexOf(sequence, current);
        int to = Array.IndexOf(sequence, target);
        if (from < 0 || to < 0 || to <= from)
            return;

        for (int i = from + 1; i <= to; i++)
        {
            canonical.TryTransitionLine(
                lineId,
                sequence[i],
                "demo:table-hud"
            );
        }
    }

    private static void PositionCustomersAroundTable(
        CustomerGroup group,
        RestaurantTable table)
    {
        if (group == null || table == null)
            return;
        group.transform.position =
            table.transform.position + new Vector3(0f, 0.15f, 0f);
        group.transform.rotation = table.transform.rotation;

        group.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>()
            ?.EnsureVisuals();
    }

    private static void DisableFlow<T>(GameObject target)
        where T : Behaviour
    {
        if (target == null)
            return;

        T component = target.GetComponent<T>();
        if (component != null)
            component.enabled = false;
    }

    private static T GetPrivateField<T>(
        object instance,
        string fieldName)
        where T : class
    {
        if (instance == null)
            return null;

        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        return field?.GetValue(instance) as T;
    }

    private static void SetPrivateField(
        object instance,
        string fieldName,
        object value)
    {
        if (instance == null)
            return;
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        field?.SetValue(instance, value);
    }
}
