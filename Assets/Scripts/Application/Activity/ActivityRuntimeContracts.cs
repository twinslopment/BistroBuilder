using System;
using System.Collections.Generic;

public enum ActivityCategory
{
    Incident = 0,
    Opportunity = 1,
    Event = 2,
    Reservation = 3
}

public enum ActivitySeverity
{
    Info = 0,
    Positive = 1,
    Attention = 2,
    Critical = 3,
    Opportunity = 4,
    Reservation = 5
}

public enum ActivityLifetime
{
    Transient = 0,
    StickyUntilResolved = 1,
    ServiceSession = 2
}

public enum ActivityTargetType
{
    None = 0,
    Group = 1,
    Table = 2,
    Order = 3,
    Dish = 4,
    Kitchen = 5,
    Entrance = 6,
    Zone = 7,
    WaitTicket = 8,
    Bar = 9,
    Reservation = 10,
    ReservationGroup = 11,
    Ingredient = 12,
    Inventory = 13,
    Supplier = 14,
    Employee = 15,
    Reputation = 16,
    Marketing = 17,
    Restaurant = 18,
    Online = 19
}

public enum ActivityAggregationRule
{
    None = 0,
    WaitingTables60Seconds = 1,
    StaffShortageSimultaneous = 2,
    UpsellByZone = 3
}

public enum ActivityFilter
{
    Today = 0,
    Incidents = 1,
    Opportunities = 2,
    Reservations = 3
}

[Serializable]
public readonly struct ActivityEventId : IEquatable<ActivityEventId>
{
    private readonly string value;

    public ActivityEventId(string value)
    {
        this.value = value == null ? string.Empty : value.Trim();
    }

    public string Value => value ?? string.Empty;
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public bool Equals(ActivityEventId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ActivityEventId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(ActivityEventId left, ActivityEventId right) =>
        left.Equals(right);

    public static bool operator !=(ActivityEventId left, ActivityEventId right) =>
        !left.Equals(right);

    public static readonly ActivityEventId TableGroupArrived = new ActivityEventId("table.group_arrived");
    public static readonly ActivityEventId TableGroupSeated = new ActivityEventId("table.group_seated");
    public static readonly ActivityEventId TableAttentionNeeded = new ActivityEventId("table.attention_needed");
    public static readonly ActivityEventId TableWaitingExcessive = new ActivityEventId("table.waiting_excessive");
    public static readonly ActivityEventId TableBillRequested = new ActivityEventId("table.bill_requested");
    public static readonly ActivityEventId TableFinished = new ActivityEventId("table.finished");
    public static readonly ActivityEventId TableCustomerUnhappy = new ActivityEventId("table.customer_unhappy");
    public static readonly ActivityEventId TableCustomerRecovered = new ActivityEventId("table.customer_recovered");

    public static readonly ActivityEventId OrderCreated = new ActivityEventId("order.created");
    public static readonly ActivityEventId OrderReady = new ActivityEventId("order.ready");
    public static readonly ActivityEventId OrderPrioritySet = new ActivityEventId("order.priority_set");
    public static readonly ActivityEventId OrderError = new ActivityEventId("order.error");
    public static readonly ActivityEventId DishColdOrPoor = new ActivityEventId("dish.cold_or_poor");
    public static readonly ActivityEventId DishBlockedStock = new ActivityEventId("dish.blocked_stock");
    public static readonly ActivityEventId DishTrending = new ActivityEventId("dish.trending");
    public static readonly ActivityEventId KitchenStateLoaded = new ActivityEventId("kitchen.state_loaded");
    public static readonly ActivityEventId KitchenStateSaturated = new ActivityEventId("kitchen.state_saturated");
    public static readonly ActivityEventId KitchenStateBlocked = new ActivityEventId("kitchen.state_blocked");
    public static readonly ActivityEventId KitchenStateRecovered = new ActivityEventId("kitchen.state_recovered");
    public static readonly ActivityEventId KitchenEquipmentIssue = new ActivityEventId("kitchen.equipment_issue");
    public static readonly ActivityEventId KitchenPriorityLimit = new ActivityEventId("kitchen.priority_limit");

    public static readonly ActivityEventId FohStateWaiting = new ActivityEventId("foh.state_waiting");
    public static readonly ActivityEventId FohStateSaturated = new ActivityEventId("foh.state_saturated");
    public static readonly ActivityEventId FohStateSlowed = new ActivityEventId("foh.state_slowed");
    public static readonly ActivityEventId FohStateRecovered = new ActivityEventId("foh.state_recovered");
    public static readonly ActivityEventId WaitlistGroupAdded = new ActivityEventId("waitlist.group_added");
    public static readonly ActivityEventId WaitlistLongWait = new ActivityEventId("waitlist.long_wait");
    public static readonly ActivityEventId WaitlistTableAvailable = new ActivityEventId("waitlist.table_available");
    public static readonly ActivityEventId WaitlistSentToBar = new ActivityEventId("waitlist.sent_to_bar");
    public static readonly ActivityEventId WaitlistGroupLeft = new ActivityEventId("waitlist.group_left");
    public static readonly ActivityEventId BarSaturated = new ActivityEventId("bar.saturated");
    public static readonly ActivityEventId ZoneStaffShortage = new ActivityEventId("zone.staff_shortage");
    public static readonly ActivityEventId TableNeedsReset = new ActivityEventId("table.needs_reset");
    public static readonly ActivityEventId TableReady = new ActivityEventId("table.ready");

    public static readonly ActivityEventId ReservationArrivingSoon = new ActivityEventId("reservation.arriving_soon");
    public static readonly ActivityEventId ReservationArrived = new ActivityEventId("reservation.arrived");
    public static readonly ActivityEventId ReservationSeated = new ActivityEventId("reservation.seated");
    public static readonly ActivityEventId ReservationLargeGroup = new ActivityEventId("reservation.large_group");
    public static readonly ActivityEventId ReservationSpecialGuest = new ActivityEventId("reservation.special_guest");
    public static readonly ActivityEventId ReservationPeakWindow = new ActivityEventId("reservation.peak_window");

    public static readonly ActivityEventId InventoryLow = new ActivityEventId("inventory.low");
    public static readonly ActivityEventId InventoryCritical = new ActivityEventId("inventory.critical");
    public static readonly ActivityEventId InventoryOut = new ActivityEventId("inventory.out");
    public static readonly ActivityEventId InventoryUpdated = new ActivityEventId("inventory.updated");
    public static readonly ActivityEventId SupplierDeliveryReceived = new ActivityEventId("supplier.delivery_received");
    public static readonly ActivityEventId SupplierIssue = new ActivityEventId("supplier.issue");

    public static readonly ActivityEventId StaffOverloaded = new ActivityEventId("staff.overloaded");
    public static readonly ActivityEventId StaffZoneUncovered = new ActivityEventId("staff.zone_uncovered");
    public static readonly ActivityEventId StaffSupportNeeded = new ActivityEventId("staff.support_needed");
    public static readonly ActivityEventId StaffUpsellOpportunity = new ActivityEventId("staff.upsell_opportunity");

    public static readonly ActivityEventId ReputationGoodReview = new ActivityEventId("reputation.good_review");
    public static readonly ActivityEventId ReputationBadReview = new ActivityEventId("reputation.bad_review");
    public static readonly ActivityEventId ReputationWordOfMouth = new ActivityEventId("reputation.word_of_mouth");
    public static readonly ActivityEventId MarketingCampaignStarted = new ActivityEventId("marketing.campaign_started");
    public static readonly ActivityEventId MarketingDemandSpike = new ActivityEventId("marketing.demand_spike");
    public static readonly ActivityEventId MarketingCapacityRisk = new ActivityEventId("marketing.capacity_risk");
    public static readonly ActivityEventId OpportunityRegularGuest = new ActivityEventId("opportunity.regular_guest");
    public static readonly ActivityEventId OpportunitySpecialGuest = new ActivityEventId("opportunity.special_guest");
    public static readonly ActivityEventId OpportunityDrink = new ActivityEventId("opportunity.drink");
    public static readonly ActivityEventId OpportunityDessert = new ActivityEventId("opportunity.dessert");
    public static readonly ActivityEventId OpportunityWalkinGroup = new ActivityEventId("opportunity.walkin_group");
    public static readonly ActivityEventId OpportunityBarWaitSale = new ActivityEventId("opportunity.bar_wait_sale");
    public static readonly ActivityEventId OpportunityHighDemand = new ActivityEventId("opportunity.high_demand");

    public static readonly ActivityEventId OnlineOrderProblem = new ActivityEventId("online.order_problem");
    public static readonly ActivityEventId OnlinePaused = new ActivityEventId("online.paused");
    public static readonly ActivityEventId OnlineCapacityRisk = new ActivityEventId("online.capacity_risk");
}

[Serializable]
public sealed class ActivityTemplateParameter
{
    public string key = string.Empty;
    public string value = string.Empty;

    public ActivityTemplateParameter() { }

    public ActivityTemplateParameter(string key, object value)
    {
        this.key = key == null ? string.Empty : key.Trim();
        this.value = value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    public ActivityTemplateParameter DeepClone() =>
        new ActivityTemplateParameter(key, value);
}

[Serializable]
public sealed class ActivityEventPayload
{
    public List<ActivityTemplateParameter> parameters =
        new List<ActivityTemplateParameter>();

    public ActivityEventPayload Set(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return this;

        string normalized = key.Trim();
        for (int i = 0; i < parameters.Count; i++)
        {
            ActivityTemplateParameter item = parameters[i];
            if (item != null && string.Equals(item.key, normalized, StringComparison.Ordinal))
            {
                item.value = value != null
                    ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty;
                return this;
            }
        }

        parameters.Add(new ActivityTemplateParameter(normalized, value));
        return this;
    }

    public bool TryGet(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        for (int i = 0; i < parameters.Count; i++)
        {
            ActivityTemplateParameter item = parameters[i];
            if (item == null || !string.Equals(item.key, key, StringComparison.Ordinal))
                continue;
            value = item.value ?? string.Empty;
            return true;
        }

        return false;
    }

    public ActivityEventPayload DeepClone()
    {
        var clone = new ActivityEventPayload();
        for (int i = 0; i < parameters.Count; i++)
            if (parameters[i] != null)
                clone.parameters.Add(parameters[i].DeepClone());
        return clone;
    }
}

[Serializable]
public sealed class ActivityTargetRef
{
    public ActivityTargetType targetType = ActivityTargetType.None;
    public string targetId = string.Empty;

    public ActivityTargetRef() { }

    public ActivityTargetRef(ActivityTargetType targetType, object targetId)
    {
        this.targetType = targetType;
        this.targetId = targetId != null
            ? Convert.ToString(targetId, System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public bool IsEmpty =>
        targetType == ActivityTargetType.None || string.IsNullOrWhiteSpace(targetId);

    public ActivityTargetRef DeepClone() =>
        new ActivityTargetRef(targetType, targetId);

    public override string ToString() =>
        targetType + ":" + (targetId ?? string.Empty);
}

public sealed class ActivityEventDefinition
{
    public ActivityEventId EventId { get; }
    public ActivityCategory Category { get; }
    public ActivitySeverity Severity { get; }
    public string IconKey { get; }
    public string IconFamilyKey { get; }
    public string TitleKey { get; }
    public string BodyKey { get; }
    public string FallbackTitle { get; }
    public string FallbackBody { get; }
    public ActivityTargetType TargetType { get; }
    public ActivityLifetime Lifetime { get; }
    public ActivityAggregationRule Aggregation { get; }
    public string FeatureGate { get; }
    public double DedupWindowMinutes { get; }
    public bool IsStateEvent { get; }

    public ActivityEventDefinition(
        ActivityEventId eventId,
        ActivityCategory category,
        ActivitySeverity severity,
        string iconKey,
        string iconFamilyKey,
        string titleKey,
        string bodyKey,
        string fallbackTitle,
        string fallbackBody,
        ActivityTargetType targetType,
        ActivityLifetime lifetime,
        ActivityAggregationRule aggregation,
        string featureGate,
        double dedupWindowMinutes,
        bool isStateEvent)
    {
        EventId = eventId;
        Category = category;
        Severity = severity;
        IconKey = iconKey ?? string.Empty;
        IconFamilyKey = iconFamilyKey ?? string.Empty;
        TitleKey = titleKey ?? string.Empty;
        BodyKey = bodyKey ?? string.Empty;
        FallbackTitle = fallbackTitle ?? string.Empty;
        FallbackBody = fallbackBody ?? string.Empty;
        TargetType = targetType;
        Lifetime = lifetime;
        Aggregation = aggregation;
        FeatureGate = featureGate ?? string.Empty;
        DedupWindowMinutes = Math.Max(0d, dedupWindowMinutes);
        IsStateEvent = isStateEvent;
    }
}

[Serializable]
public sealed class ActivityEventInstance
{
    public long sequence;
    public string eventId = string.Empty;
    public ActivityTargetRef target = new ActivityTargetRef();
    public ActivityEventPayload payload = new ActivityEventPayload();
    public int dayIndex = 1;
    public double minuteOfDay;
    public bool resolved;
    public int resolvedDayIndex;
    public double resolvedMinuteOfDay;

    public double AbsoluteGameMinute =>
        (Math.Max(1, dayIndex) - 1) * 1440d + Math.Max(0d, minuteOfDay);

    public ActivityEventInstance DeepClone()
    {
        return new ActivityEventInstance
        {
            sequence = sequence,
            eventId = eventId ?? string.Empty,
            target = target != null ? target.DeepClone() : new ActivityTargetRef(),
            payload = payload != null ? payload.DeepClone() : new ActivityEventPayload(),
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            resolved = resolved,
            resolvedDayIndex = resolvedDayIndex,
            resolvedMinuteOfDay = resolvedMinuteOfDay
        };
    }
}

public sealed class ActivityDisplayEntry
{
    public ActivityEventDefinition Definition { get; set; }
    public ActivityEventInstance Primary { get; set; }
    public List<ActivityTargetRef> Targets { get; } = new List<ActivityTargetRef>();
    public string TitleOverride { get; set; }
    public string BodyOverride { get; set; }
    public int AggregatedCount { get; set; } = 1;

    public bool IsResolved => Primary != null && Primary.resolved;
}

[Serializable]
public sealed class ActivityFeedSaveSnapshot
{
    public int schemaVersion = 1;
    public long nextSequence = 1;
    public List<ActivityEventInstance> events = new List<ActivityEventInstance>();
}

public readonly struct ActivityPublishResult
{
    public bool Accepted { get; }
    public bool Deduplicated { get; }
    public ActivityEventInstance Event { get; }

    public ActivityPublishResult(bool accepted, bool deduplicated, ActivityEventInstance activityEvent)
    {
        Accepted = accepted;
        Deduplicated = deduplicated;
        Event = activityEvent;
    }
}

public interface IActivityEventPublisher
{
    ActivityPublishResult Publish(
        ActivityEventId eventId,
        ActivityEventPayload payload = null,
        ActivityTargetRef target = null);
}
