using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traduce cambios de autoridades de gameplay ya existentes a eventos
/// semánticos de ACTIVIDAD. Nunca genera texto final ni toca la UI.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Activity/Gameplay Event Bridge")]
public sealed class BistroBuilderActivityRuntimeBridge : MonoBehaviour
{
    [SerializeField] private ActivityFeedService feed;
    [SerializeField] private BistroBuilderInventoryPlanningService inventoryPlanning;
    [SerializeField] private BistroBuilderAdvancedKitchenService kitchen;
    [SerializeField] private BistroBuilderAdvancedFrontOfHouseService frontOfHouse;
    [SerializeField] private TableAssignmentSystem tableAssignments;

    private readonly List<BistroBuilderFrontOfHouseQueueEntry> queue =
        new List<BistroBuilderFrontOfHouseQueueEntry>(24);

    private bool inventorySubscribed;
    private bool kitchenSubscribed;
    private bool frontOfHouseSubscribed;
    private bool tablesSubscribed;
    private float nextResolveAt;

    private void Awake() => ResolveDependencies();

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeAvailable();
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextResolveAt)
            return;

        ResolveDependencies();
        SubscribeAvailable();
        nextResolveAt = Time.unscaledTime + 1f;
    }

    private void OnDisable() => Unsubscribe();

    private void ResolveDependencies()
    {
        if (feed == null)
            feed = GetComponent<ActivityFeedService>();
        if (inventoryPlanning == null)
            inventoryPlanning = FindFirstObjectByType<BistroBuilderInventoryPlanningService>(FindObjectsInactive.Include);
        if (kitchen == null)
            kitchen = FindFirstObjectByType<BistroBuilderAdvancedKitchenService>(FindObjectsInactive.Include);
        if (frontOfHouse == null)
            frontOfHouse = FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>(FindObjectsInactive.Include);
        if (tableAssignments == null)
            tableAssignments = FindFirstObjectByType<TableAssignmentSystem>(FindObjectsInactive.Include);
    }

    private void SubscribeAvailable()
    {
        if (feed == null)
            return;

        if (!inventorySubscribed && inventoryPlanning != null)
        {
            inventoryPlanning.AlertActivated += HandleInventoryAlertActivated;
            inventoryPlanning.AlertCleared += HandleInventoryAlertCleared;
            inventorySubscribed = true;
        }

        if (!kitchenSubscribed && kitchen != null)
        {
            kitchen.LoadStateChanged += HandleKitchenStateChanged;
            kitchenSubscribed = true;
        }

        if (!frontOfHouseSubscribed && frontOfHouse != null)
        {
            frontOfHouse.OperationalStateChanged += HandleFrontOfHouseStateChanged;
            frontOfHouse.QueueChanged += HandleQueueChanged;
            frontOfHouse.GroupAbandoned += HandleGroupAbandoned;
            frontOfHouseSubscribed = true;
        }

        if (!tablesSubscribed && tableAssignments != null)
        {
            tableAssignments.CustomerGroupRegistered += HandleGroupRegistered;
            tableAssignments.TableAssigned += HandleTableAssigned;
            tablesSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (inventorySubscribed && inventoryPlanning != null)
        {
            inventoryPlanning.AlertActivated -= HandleInventoryAlertActivated;
            inventoryPlanning.AlertCleared -= HandleInventoryAlertCleared;
        }
        if (kitchenSubscribed && kitchen != null)
            kitchen.LoadStateChanged -= HandleKitchenStateChanged;
        if (frontOfHouseSubscribed && frontOfHouse != null)
        {
            frontOfHouse.OperationalStateChanged -= HandleFrontOfHouseStateChanged;
            frontOfHouse.QueueChanged -= HandleQueueChanged;
            frontOfHouse.GroupAbandoned -= HandleGroupAbandoned;
        }
        if (tablesSubscribed && tableAssignments != null)
        {
            tableAssignments.CustomerGroupRegistered -= HandleGroupRegistered;
            tableAssignments.TableAssigned -= HandleTableAssigned;
        }

        inventorySubscribed = false;
        kitchenSubscribed = false;
        frontOfHouseSubscribed = false;
        tablesSubscribed = false;
    }

    private void HandleInventoryAlertActivated(BistroBuilderInventoryAlertSnapshot alert)
    {
        if (feed == null || alert.Kind == BistroBuilderInventoryAlertKind.NearExpiry)
            return;

        ActivityEventId id;
        switch (alert.Kind)
        {
            case BistroBuilderInventoryAlertKind.LowStock:
                id = ActivityEventId.InventoryLow;
                break;
            case BistroBuilderInventoryAlertKind.CriticalStock:
                id = ActivityEventId.InventoryCritical;
                break;
            case BistroBuilderInventoryAlertKind.OutOfStock:
                id = ActivityEventId.InventoryOut;
                break;
            default:
                return;
        }

        string displayName = alert.IngredientId;
        if (inventoryPlanning != null &&
            inventoryPlanning.TryGetPlanningSnapshot(
                alert.IngredientId,
                out BistroBuilderInventoryPlanningSnapshot snapshot) &&
            !string.IsNullOrWhiteSpace(snapshot.DisplayName))
        {
            displayName = snapshot.DisplayName;
        }

        var target = new ActivityTargetRef(
            ActivityTargetType.Ingredient,
            alert.IngredientId);

        feed.PublishState(
            id,
            "inventory.stock",
            target,
            new ActivityEventPayload().Set("ingredient", displayName));
    }

    private void HandleInventoryAlertCleared(BistroBuilderInventoryAlertSnapshot alert)
    {
        if (feed == null || string.IsNullOrWhiteSpace(alert.IngredientId))
            return;

        var target = new ActivityTargetRef(
            ActivityTargetType.Ingredient,
            alert.IngredientId);

        feed.ClearState("inventory.stock", target);
        feed.ResolveTarget(ActivityTargetType.Ingredient, alert.IngredientId);
    }

    private void HandleKitchenStateChanged(BistroBuilderKitchenLoadState state)
    {
        if (feed == null)
            return;

        ActivityEventId id;
        switch (state)
        {
            case BistroBuilderKitchenLoadState.Loaded:
                id = ActivityEventId.KitchenStateLoaded;
                break;
            case BistroBuilderKitchenLoadState.Saturated:
                id = ActivityEventId.KitchenStateSaturated;
                break;
            case BistroBuilderKitchenLoadState.Blocked:
                id = ActivityEventId.KitchenStateBlocked;
                break;
            default:
                id = ActivityEventId.KitchenStateRecovered;
                break;
        }

        var payload = new ActivityEventPayload()
            .Set("pending", kitchen != null ? kitchen.QueuedCount : 0);

        feed.PublishState(
            id,
            "kitchen.load",
            new ActivityTargetRef(ActivityTargetType.Kitchen, "primary"),
            payload);
    }

    private void HandleFrontOfHouseStateChanged(
        BistroBuilderFrontOfHouseOperationalState state)
    {
        if (feed == null)
            return;

        ActivityEventId id;
        ActivityTargetRef target;

        switch (state)
        {
            case BistroBuilderFrontOfHouseOperationalState.Waiting:
                id = ActivityEventId.FohStateWaiting;
                target = new ActivityTargetRef(ActivityTargetType.Entrance, "main");
                break;
            case BistroBuilderFrontOfHouseOperationalState.Saturated:
                id = ActivityEventId.FohStateSaturated;
                target = new ActivityTargetRef(ActivityTargetType.Zone, "dining");
                break;
            case BistroBuilderFrontOfHouseOperationalState.ReducedPace:
                id = ActivityEventId.FohStateSlowed;
                target = new ActivityTargetRef(ActivityTargetType.Entrance, "main");
                break;
            default:
                id = ActivityEventId.FohStateRecovered;
                target = new ActivityTargetRef(ActivityTargetType.Entrance, "main");
                break;
        }

        feed.PublishState(
            id,
            "foh.operational",
            target,
            new ActivityEventPayload().Set(
                "groups",
                frontOfHouse != null ? frontOfHouse.WaitingGroupCount : 0));
    }

    private void HandleQueueChanged()
    {
        if (feed == null || frontOfHouse == null)
            return;

        queue.Clear();
        frontOfHouse.CopyQueueSnapshot(queue);
        if (queue.Count <= 0)
        {
            feed.ClearState(
                "foh.queue",
                new ActivityTargetRef(ActivityTargetType.Entrance, "main"));
            return;
        }

        feed.PublishState(
            ActivityEventId.FohStateWaiting,
            "foh.queue",
            new ActivityTargetRef(ActivityTargetType.Entrance, "main"),
            new ActivityEventPayload().Set("groups", queue.Count));

        for (int i = 0; i < queue.Count; i++)
        {
            BistroBuilderFrontOfHouseQueueEntry entry = queue[i];
            if (entry == null || entry.waitingSeconds < entry.toleranceSeconds)
                continue;

            int minutes = Mathf.Max(1, Mathf.RoundToInt(entry.waitingSeconds / 60f));
            feed.PublishState(
                ActivityEventId.WaitlistLongWait,
                "waitlist.long",
                new ActivityTargetRef(ActivityTargetType.WaitTicket, entry.groupId),
                new ActivityEventPayload().Set("minutes", minutes));
        }
    }

    private void HandleGroupRegistered(CustomerGroup group)
    {
        if (feed == null || group == null)
            return;

        feed.Publish(
            ActivityEventId.TableGroupArrived,
            new ActivityEventPayload().Set("guests", group.GroupSize),
            new ActivityTargetRef(ActivityTargetType.Group, group.GroupId));
    }

    private void HandleTableAssigned(CustomerGroup group, RestaurantTable table)
    {
        if (feed == null || table == null)
            return;

        feed.Resolve(
            ActivityEventId.TableGroupArrived,
            group != null ? group.GroupId.ToString() : null);

        feed.Publish(
            ActivityEventId.TableGroupSeated,
            new ActivityEventPayload().Set("table", table.TableId),
            new ActivityTargetRef(ActivityTargetType.Table, table.TableId));
    }

    private void HandleGroupAbandoned(int groupId)
    {
        if (feed == null || groupId <= 0)
            return;

        feed.ResolveTarget(ActivityTargetType.WaitTicket, groupId.ToString());
        feed.Publish(
            ActivityEventId.WaitlistGroupLeft,
            null,
            new ActivityTargetRef(ActivityTargetType.WaitTicket, groupId));
    }
}
