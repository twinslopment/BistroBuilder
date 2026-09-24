using System;
using System.Collections.Generic;
using System.Text;

public static class ActivityIconFamilyCatalog
{
    private static readonly Dictionary<string, string> Families =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "activity.people.arrival", "activity.people.arrival" },
            { "activity.table.seated", "activity.table.seated" },
            { "activity.table.attention", "activity.table.attention" },
            { "activity.wait.alert", "activity.wait.alert" },
            { "activity.table.bill", "activity.table.bill" },
            { "activity.table.complete", "activity.table.complete" },
            { "activity.order.new", "activity.order.new" },
            { "activity.order.ready", "activity.order.ready" },
            { "activity.order.priority", "activity.order.priority" },
            { "activity.order.error", "activity.order.error" },
            { "activity.dish.problem", "activity.dish.problem" },
            { "activity.dish.trending", "activity.dish.trending" },
            { "activity.kitchen.loaded", "activity.kitchen.state" },
            { "activity.kitchen.saturated", "activity.kitchen.state" },
            { "activity.kitchen.blocked", "activity.kitchen.state" },
            { "activity.kitchen.recovered", "activity.kitchen.state" },
            { "activity.kitchen.equipment", "activity.kitchen.equipment" },
            { "activity.flow.slowed", "activity.flow.state" },
            { "activity.flow.recovered", "activity.flow.state" },
            { "activity.wait.queue", "activity.flow.state" },
            { "activity.zone.saturated", "activity.flow.state" },
            { "activity.wait.group", "activity.wait.group" },
            { "activity.wait.left", "activity.wait.group" },
            { "activity.table.available", "activity.table.available" },
            { "activity.table.reset", "activity.table.available" },
            { "activity.bar.wait", "activity.bar.state" },
            { "activity.bar.saturated", "activity.bar.state" },
            { "activity.staff.shortage", "activity.staff.state" },
            { "activity.staff.overloaded", "activity.staff.state" },
            { "activity.staff.support", "activity.staff.state" },
            { "activity.reservation.soon", "activity.reservation" },
            { "activity.reservation.arrived", "activity.reservation" },
            { "activity.reservation.seated", "activity.reservation" },
            { "activity.reservation.group", "activity.reservation.group" },
            { "activity.reservation.peak", "activity.reservation.group" },
            { "activity.guest.special", "activity.guest.special" },
            { "activity.guest.regular", "activity.guest.special" },
            { "activity.stock.low", "activity.stock.state" },
            { "activity.stock.critical", "activity.stock.state" },
            { "activity.stock.out", "activity.stock.state" },
            { "activity.stock.blocked", "activity.stock.state" },
            { "activity.stock.updated", "activity.stock.state" },
            { "activity.supplier.delivery", "activity.supplier" },
            { "activity.supplier.issue", "activity.supplier" },
            { "activity.reputation.positive", "activity.reputation" },
            { "activity.reputation.negative", "activity.reputation" },
            { "activity.reputation.recovered", "activity.reputation" },
            { "activity.reputation.word_of_mouth", "activity.reputation" },
            { "activity.marketing.campaign", "activity.marketing" },
            { "activity.marketing.risk", "activity.marketing" },
            { "activity.opportunity.upsell", "activity.opportunity" },
            { "activity.opportunity.drink", "activity.opportunity" },
            { "activity.opportunity.dessert", "activity.opportunity" },
            { "activity.opportunity.group", "activity.opportunity" },
            { "activity.opportunity.bar", "activity.opportunity" },
            { "activity.trend.up", "activity.trend.up" }
        };

    private static readonly string[] CanonicalFamilies = BuildCanonicalFamilies();

    public static IReadOnlyList<string> AllFamilies => CanonicalFamilies;

    public static bool TryResolveFamily(string iconKey, out string familyKey)
    {
        familyKey = string.Empty;
        if (string.IsNullOrWhiteSpace(iconKey))
            return false;
        return Families.TryGetValue(iconKey.Trim(), out familyKey);
    }

    public static string ResolveFamily(string iconKey)
    {
        return TryResolveFamily(iconKey, out string family)
            ? family
            : iconKey ?? string.Empty;
    }

    public static string ToRuntimeResourceName(string familyKey)
    {
        if (string.IsNullOrWhiteSpace(familyKey))
            return "BB_Activity_Unknown";

        string value = familyKey.StartsWith("activity.", StringComparison.Ordinal)
            ? familyKey.Substring("activity.".Length)
            : familyKey;
        string[] parts = value.Split('.');
        var builder = new StringBuilder("BB_Activity");
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
                continue;
            builder.Append('_');
            builder.Append(char.ToUpperInvariant(parts[i][0]));
            if (parts[i].Length > 1)
                builder.Append(parts[i].Substring(1));
        }
        return builder.ToString();
    }

    private static string[] BuildCanonicalFamilies()
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string family in Families.Values)
            unique.Add(family);

        string[] result = new string[unique.Count];
        unique.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }
}

public static class ActivityEventCatalog
{
    private static readonly Dictionary<string, ActivityEventDefinition> ById =
        new Dictionary<string, ActivityEventDefinition>(StringComparer.Ordinal);

    private static readonly List<ActivityEventDefinition> Definitions =
        new List<ActivityEventDefinition>(72);

    public static IReadOnlyList<ActivityEventDefinition> All => Definitions;

    static ActivityEventCatalog()
    {
        Add(ActivityEventId.TableGroupArrived, "Nuevo grupo", "{guests} personas", ActivitySeverity.Info, "activity.people.arrival", ActivityTargetType.Group);
        Add(ActivityEventId.TableGroupSeated, "Mesa {table}", "Grupo sentado", ActivitySeverity.Info, "activity.table.seated", ActivityTargetType.Table);
        Add(ActivityEventId.TableAttentionNeeded, "Mesa {table}", "Necesita atención", ActivitySeverity.Attention, "activity.table.attention", ActivityTargetType.Table);
        Add(ActivityEventId.TableWaitingExcessive, "Mesa {table}", "Espera demasiado sus platos", ActivitySeverity.Critical, "activity.wait.alert", ActivityTargetType.Table, aggregation: ActivityAggregationRule.WaitingTables60Seconds);
        Add(ActivityEventId.TableBillRequested, "Mesa {table}", "Ha pedido la cuenta", ActivitySeverity.Info, "activity.table.bill", ActivityTargetType.Table);
        Add(ActivityEventId.TableFinished, "Mesa {table}", "Servicio finalizado", ActivitySeverity.Info, "activity.table.complete", ActivityTargetType.Table);
        Add(ActivityEventId.TableCustomerUnhappy, "Cliente descontento", "Mesa {table}", ActivitySeverity.Attention, "activity.reputation.negative", ActivityTargetType.Table);
        Add(ActivityEventId.TableCustomerRecovered, "Incidencia resuelta", "Mesa {table} recuperada", ActivitySeverity.Positive, "activity.reputation.recovered", ActivityTargetType.Table);

        Add(ActivityEventId.OrderCreated, "Nuevo pedido", "Mesa {table} · {lines} platos", ActivitySeverity.Info, "activity.order.new", ActivityTargetType.Order);
        Add(ActivityEventId.OrderReady, "Pedido listo", "Mesa {table}", ActivitySeverity.Info, "activity.order.ready", ActivityTargetType.Order);
        Add(ActivityEventId.OrderPrioritySet, "Comanda prioritaria", "Mesa {table}", ActivitySeverity.Attention, "activity.order.priority", ActivityTargetType.Order);
        Add(ActivityEventId.OrderError, "Error de pedido", "Mesa {table}", ActivitySeverity.Attention, "activity.order.error", ActivityTargetType.Order);
        Add(ActivityEventId.DishColdOrPoor, "Problema de plato", "Mesa {table}", ActivitySeverity.Attention, "activity.dish.problem", ActivityTargetType.Order);
        Add(ActivityEventId.DishBlockedStock, "Plato no disponible", "{dish}", ActivitySeverity.Critical, "activity.stock.blocked", ActivityTargetType.Dish);
        Add(ActivityEventId.DishTrending, "Plato destacado", "{dish} · {count} pedidos", ActivitySeverity.Positive, "activity.dish.trending", ActivityTargetType.Dish);
        Add(ActivityEventId.KitchenStateLoaded, "Cocina cargada", "Aumenta la cola", ActivitySeverity.Attention, "activity.kitchen.loaded", ActivityTargetType.Kitchen, isStateEvent: true);
        Add(ActivityEventId.KitchenStateSaturated, "Cocina saturada", "{pending} comandas pendientes", ActivitySeverity.Critical, "activity.kitchen.saturated", ActivityTargetType.Kitchen, isStateEvent: true);
        Add(ActivityEventId.KitchenStateBlocked, "Cocina bloqueada", "Requiere actuación", ActivitySeverity.Critical, "activity.kitchen.blocked", ActivityTargetType.Kitchen, isStateEvent: true);
        Add(ActivityEventId.KitchenStateRecovered, "Cocina fluida", "Ritmo recuperado", ActivitySeverity.Positive, "activity.kitchen.recovered", ActivityTargetType.Kitchen, isStateEvent: true);
        Add(ActivityEventId.KitchenEquipmentIssue, "Incidencia de cocina", "{equipment}", ActivitySeverity.Critical, "activity.kitchen.equipment", ActivityTargetType.Kitchen);
        Add(ActivityEventId.KitchenPriorityLimit, "Prioridades completas", "Máximo alcanzado", ActivitySeverity.Attention, "activity.order.priority", ActivityTargetType.Kitchen);

        Add(ActivityEventId.FohStateWaiting, "Entrada con espera", "{groups} grupos", ActivitySeverity.Attention, "activity.wait.queue", ActivityTargetType.Entrance, isStateEvent: true);
        Add(ActivityEventId.FohStateSaturated, "Sala saturada", "Requiere ajuste", ActivitySeverity.Critical, "activity.zone.saturated", ActivityTargetType.Zone, isStateEvent: true);
        Add(ActivityEventId.FohStateSlowed, "Ritmo reducido", "Entrada controlada", ActivitySeverity.Attention, "activity.flow.slowed", ActivityTargetType.Entrance, isStateEvent: true);
        Add(ActivityEventId.FohStateRecovered, "Entrada fluida", "Ritmo recuperado", ActivitySeverity.Positive, "activity.flow.recovered", ActivityTargetType.Entrance, isStateEvent: true);
        Add(ActivityEventId.WaitlistGroupAdded, "Nuevo grupo en espera", "{guests} personas", ActivitySeverity.Info, "activity.wait.group", ActivityTargetType.WaitTicket);
        Add(ActivityEventId.WaitlistLongWait, "Espera elevada", "{minutes} min", ActivitySeverity.Critical, "activity.wait.alert", ActivityTargetType.WaitTicket, isStateEvent: true);
        Add(ActivityEventId.WaitlistTableAvailable, "Mesa disponible", "Grupo en espera puede sentarse", ActivitySeverity.Opportunity, "activity.table.available", ActivityTargetType.WaitTicket);
        Add(ActivityEventId.WaitlistSentToBar, "Espera en barra", "{guests} personas", ActivitySeverity.Info, "activity.bar.wait", ActivityTargetType.WaitTicket);
        Add(ActivityEventId.WaitlistGroupLeft, "Grupo perdido", "Abandona la espera", ActivitySeverity.Attention, "activity.wait.left", ActivityTargetType.WaitTicket);
        Add(ActivityEventId.BarSaturated, "Barra saturada", "No absorbe más espera", ActivitySeverity.Critical, "activity.bar.saturated", ActivityTargetType.Bar, isStateEvent: true);
        Add(ActivityEventId.ZoneStaffShortage, "Falta personal", "{zone}", ActivitySeverity.Critical, "activity.staff.shortage", ActivityTargetType.Zone, aggregation: ActivityAggregationRule.StaffShortageSimultaneous, isStateEvent: true);
        Add(ActivityEventId.TableNeedsReset, "Mesa pendiente", "Limpieza/preparación", ActivitySeverity.Attention, "activity.table.reset", ActivityTargetType.Table);
        Add(ActivityEventId.TableReady, "Mesa preparada", "Disponible de nuevo", ActivitySeverity.Positive, "activity.table.available", ActivityTargetType.Table);

        AddReservation(ActivityEventId.ReservationArrivingSoon, "Reserva próxima", "{guests} pax · {minutes} min", ActivitySeverity.Reservation, "activity.reservation.soon", ActivityTargetType.Reservation);
        AddReservation(ActivityEventId.ReservationArrived, "Reserva llegada", "{guests} pax", ActivitySeverity.Reservation, "activity.reservation.arrived", ActivityTargetType.Reservation);
        AddReservation(ActivityEventId.ReservationSeated, "Reserva sentada", "Mesa {table}", ActivitySeverity.Reservation, "activity.reservation.seated", ActivityTargetType.Table);
        AddReservation(ActivityEventId.ReservationLargeGroup, "Grupo grande próximo", "{guests} pax", ActivitySeverity.Attention, "activity.reservation.group", ActivityTargetType.Reservation);
        AddReservation(ActivityEventId.ReservationSpecialGuest, "Cliente especial", "Reserva próxima", ActivitySeverity.Opportunity, "activity.guest.special", ActivityTargetType.Reservation);
        AddReservation(ActivityEventId.ReservationPeakWindow, "Pico de reservas", "{count} entradas próximas", ActivitySeverity.Critical, "activity.reservation.peak", ActivityTargetType.ReservationGroup);

        Add(ActivityEventId.InventoryLow, "Inventario bajo", "{ingredient}", ActivitySeverity.Attention, "activity.stock.low", ActivityTargetType.Ingredient, featureGate: "inventory", isStateEvent: true);
        Add(ActivityEventId.InventoryCritical, "Stock crítico", "{ingredient}", ActivitySeverity.Critical, "activity.stock.critical", ActivityTargetType.Ingredient, featureGate: "inventory", isStateEvent: true);
        Add(ActivityEventId.InventoryOut, "Stock agotado", "{ingredient}", ActivitySeverity.Critical, "activity.stock.out", ActivityTargetType.Ingredient, featureGate: "inventory", isStateEvent: true);
        Add(ActivityEventId.InventoryUpdated, "Inventario actualizado", "{source}", ActivitySeverity.Info, "activity.stock.updated", ActivityTargetType.Inventory, featureGate: "inventory");
        Add(ActivityEventId.SupplierDeliveryReceived, "Entrega recibida", "{supplier}", ActivitySeverity.Positive, "activity.supplier.delivery", ActivityTargetType.Supplier, featureGate: "suppliers");
        Add(ActivityEventId.SupplierIssue, "Problema de suministro", "{supplier}", ActivitySeverity.Attention, "activity.supplier.issue", ActivityTargetType.Supplier, featureGate: "suppliers");

        Add(ActivityEventId.StaffOverloaded, "Empleado saturado", "{employee}", ActivitySeverity.Attention, "activity.staff.overloaded", ActivityTargetType.Employee, featureGate: "staff", isStateEvent: true);
        Add(ActivityEventId.StaffZoneUncovered, "Zona sin cobertura", "{zone}", ActivitySeverity.Critical, "activity.staff.shortage", ActivityTargetType.Zone, featureGate: "staff", isStateEvent: true);
        Add(ActivityEventId.StaffSupportNeeded, "Apoyo requerido", "{zone}", ActivitySeverity.Attention, "activity.staff.support", ActivityTargetType.Zone, featureGate: "staff");
        Add(ActivityEventId.StaffUpsellOpportunity, "Venta sugerida", "Mesa {table}", ActivitySeverity.Opportunity, "activity.opportunity.upsell", ActivityTargetType.Table, featureGate: "staff", aggregation: ActivityAggregationRule.UpsellByZone);

        Add(ActivityEventId.ReputationGoodReview, "¡Buena reseña!", "“{excerpt}”", ActivitySeverity.Positive, "activity.reputation.positive", ActivityTargetType.Reputation, featureGate: "reputation");
        Add(ActivityEventId.ReputationBadReview, "Reseña negativa", "“{excerpt}”", ActivitySeverity.Attention, "activity.reputation.negative", ActivityTargetType.Reputation, featureGate: "reputation");
        Add(ActivityEventId.ReputationWordOfMouth, "Boca a boca", "Demanda orgánica al alza", ActivitySeverity.Positive, "activity.reputation.word_of_mouth", ActivityTargetType.Reputation, featureGate: "reputation");
        Add(ActivityEventId.MarketingCampaignStarted, "Campaña activa", "{campaign}", ActivitySeverity.Info, "activity.marketing.campaign", ActivityTargetType.Marketing, featureGate: "marketing");
        Add(ActivityEventId.MarketingDemandSpike, "Demanda al alza", "+{percent}% prevista", ActivitySeverity.Opportunity, "activity.trend.up", ActivityTargetType.Marketing, featureGate: "marketing");
        Add(ActivityEventId.MarketingCapacityRisk, "Demanda excesiva", "Capacidad en riesgo", ActivitySeverity.Critical, "activity.marketing.risk", ActivityTargetType.Marketing, featureGate: "marketing");
        Add(ActivityEventId.OpportunityRegularGuest, "Cliente habitual", "Mesa {table}", ActivitySeverity.Opportunity, "activity.guest.regular", ActivityTargetType.Table);
        Add(ActivityEventId.OpportunitySpecialGuest, "Cliente importante", "Mesa {table}", ActivitySeverity.Opportunity, "activity.guest.special", ActivityTargetType.Table);
        Add(ActivityEventId.OpportunityDrink, "Oportunidad de bebida", "Mesa {table}", ActivitySeverity.Opportunity, "activity.opportunity.drink", ActivityTargetType.Table, aggregation: ActivityAggregationRule.UpsellByZone);
        Add(ActivityEventId.OpportunityDessert, "Oportunidad de postre", "Mesa {table}", ActivitySeverity.Opportunity, "activity.opportunity.dessert", ActivityTargetType.Table, aggregation: ActivityAggregationRule.UpsellByZone);
        Add(ActivityEventId.OpportunityWalkinGroup, "Mesa aprovechable", "Grupo espontáneo de {guests}", ActivitySeverity.Opportunity, "activity.opportunity.group", ActivityTargetType.Group);
        Add(ActivityEventId.OpportunityBarWaitSale, "Espera rentable", "Grupo puede pasar a barra", ActivitySeverity.Opportunity, "activity.opportunity.bar", ActivityTargetType.WaitTicket);
        Add(ActivityEventId.OpportunityHighDemand, "Día de gran afluencia", "{guests} comensales (+{percent}%)", ActivitySeverity.Opportunity, "activity.trend.up", ActivityTargetType.Restaurant);
    }

    public static bool TryGet(ActivityEventId id, out ActivityEventDefinition definition) =>
        TryGet(id.Value, out definition);

    public static bool TryGet(string eventId, out ActivityEventDefinition definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(eventId) &&
               ById.TryGetValue(eventId.Trim(), out definition);
    }

    public static ActivityEventDefinition GetRequired(ActivityEventId id)
    {
        if (!TryGet(id, out ActivityEventDefinition definition))
            throw new KeyNotFoundException("EventId de ACTIVIDAD no registrado: " + id.Value);
        return definition;
    }

    public static bool Validate(out string error)
    {
        error = string.Empty;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Count; i++)
        {
            ActivityEventDefinition definition = Definitions[i];
            if (definition == null || definition.EventId.IsEmpty)
            {
                error = "Existe una definición de ACTIVIDAD sin EventId.";
                return false;
            }

            if (!ids.Add(definition.EventId.Value))
            {
                error = "EventId duplicado en ACTIVIDAD: " + definition.EventId.Value;
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.IconKey) ||
                string.IsNullOrWhiteSpace(definition.IconFamilyKey) ||
                string.IsNullOrWhiteSpace(definition.TitleKey) ||
                string.IsNullOrWhiteSpace(definition.BodyKey))
            {
                error = "Definición incompleta: " + definition.EventId.Value;
                return false;
            }
        }

        return true;
    }

    private static void AddReservation(
        ActivityEventId id,
        string title,
        string body,
        ActivitySeverity severity,
        string icon,
        ActivityTargetType target)
    {
        Add(id, title, body, severity, icon, target,
            categoryOverride: ActivityCategory.Reservation,
            lifetimeOverride: ActivityLifetime.ServiceSession,
            featureGate: "reservations");
    }

    private static void Add(
        ActivityEventId id,
        string title,
        string body,
        ActivitySeverity severity,
        string icon,
        ActivityTargetType target,
        ActivityAggregationRule aggregation = ActivityAggregationRule.None,
        string featureGate = "",
        bool isStateEvent = false,
        ActivityCategory? categoryOverride = null,
        ActivityLifetime? lifetimeOverride = null)
    {
        ActivityCategory category = categoryOverride ?? ResolveCategory(severity);
        ActivityLifetime lifetime = lifetimeOverride ?? ResolveLifetime(severity);
        double dedup = severity == ActivitySeverity.Positive ? 10d :
            severity == ActivitySeverity.Opportunity ? 5d : 1d;
        string keyRoot = "activity.events." + id.Value;
        var definition = new ActivityEventDefinition(
            id,
            category,
            severity,
            icon,
            ActivityIconFamilyCatalog.ResolveFamily(icon),
            keyRoot + ".title",
            keyRoot + ".body",
            title,
            body,
            target,
            lifetime,
            aggregation,
            featureGate,
            dedup,
            isStateEvent);

        if (ById.ContainsKey(id.Value))
            throw new InvalidOperationException("EventId duplicado: " + id.Value);

        ById.Add(id.Value, definition);
        Definitions.Add(definition);
    }

    private static ActivityCategory ResolveCategory(ActivitySeverity severity)
    {
        if (severity == ActivitySeverity.Opportunity)
            return ActivityCategory.Opportunity;
        if (severity == ActivitySeverity.Attention || severity == ActivitySeverity.Critical)
            return ActivityCategory.Incident;
        return ActivityCategory.Event;
    }

    private static ActivityLifetime ResolveLifetime(ActivitySeverity severity)
    {
        return severity == ActivitySeverity.Critical
            ? ActivityLifetime.StickyUntilResolved
            : ActivityLifetime.Transient;
    }
}
