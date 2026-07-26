using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Construye referencias estables para enlazar el servicio legacy con el
/// dominio canónico.
///
/// Mientras no exista todavía una entidad Customer individual, cada miembro
/// de CustomerGroup recibe una identidad determinista basada en GroupId y en
/// su posición dentro del grupo. El futuro sistema de clientes podrá
/// sustituir esta fuente sin cambiar OrderId, OrderLineId ni DishId.
/// </summary>
public static class BistroBuilderServiceOrderIdentityUtility
{
    public static string BuildLegacyOrderReference(int legacyOrderId)
    {
        return legacyOrderId > 0
            ? "legacy_order_" +
              legacyOrderId.ToString("D6", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static string BuildTableReference(int tableId)
    {
        return tableId > 0
            ? "table_" +
              tableId.ToString("D6", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static string BuildGroupReference(int groupId)
    {
        return groupId > 0
            ? "group_" +
              groupId.ToString("D6", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static string BuildCustomerReference(
        int groupId,
        int memberIndex
    )
    {
        return groupId > 0 && memberIndex > 0
            ? "customer_g" +
              groupId.ToString("D6", CultureInfo.InvariantCulture) +
              "_p" +
              memberIndex.ToString("D3", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static string BuildKitchenReference(string kitchenId)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(kitchenId);

        return BistroBuilderOrderIdUtility.IsValid(normalized)
            ? normalized
            : string.Empty;
    }

    public static string BuildWaiterReference(int waiterId)
    {
        return waiterId > 0
            ? "waiter_" +
              waiterId.ToString("D6", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static bool TryBuildCustomerReferences(
        int groupId,
        int groupSize,
        List<string> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "La colección de destino de clientes es nula.";
            return false;
        }

        destination.Clear();

        if (groupId < 1)
        {
            error = "El grupo no tiene una identidad válida.";
            return false;
        }

        if (groupSize < 1)
        {
            error = "El grupo no contiene clientes.";
            return false;
        }

        for (int memberIndex = 1;
             memberIndex <= groupSize;
             memberIndex++)
        {
            string customerId = BuildCustomerReference(
                groupId,
                memberIndex
            );

            if (!BistroBuilderOrderIdUtility.IsValid(customerId))
            {
                destination.Clear();
                error =
                    "No se pudo generar una identidad estable de cliente.";
                return false;
            }

            destination.Add(customerId);
        }

        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Traduce estados coarse del flujo anterior a estados de línea canónicos.
///
/// La traducción es unidireccional y explícita. No intenta inferir estados a
/// partir de nombres ni de valores numéricos del enum.
/// </summary>
public static class BistroBuilderLegacyCanonicalOrderStateMap
{
    public static bool TryGetLineTarget(
        OrderState legacyState,
        out BistroBuilderCanonicalOrderLineState target,
        out bool cancelOrder
    )
    {
        cancelOrder = false;

        switch (legacyState)
        {
            case OrderState.Created:
                target = BistroBuilderCanonicalOrderLineState.Draft;
                return true;

            case OrderState.SentToKitchen:
                target = BistroBuilderCanonicalOrderLineState.Queued;
                return true;

            case OrderState.Preparing:
                target = BistroBuilderCanonicalOrderLineState.Preparing;
                return true;

            case OrderState.ReadyForPickup:
                target =
                    BistroBuilderCanonicalOrderLineState.ReadyForPickup;
                return true;

            case OrderState.Served:
                target = BistroBuilderCanonicalOrderLineState.Served;
                return true;

            case OrderState.Completed:
                target = BistroBuilderCanonicalOrderLineState.Consumed;
                return true;

            case OrderState.Cancelled:
                target = BistroBuilderCanonicalOrderLineState.Cancelled;
                cancelOrder = true;
                return true;

            default:
                target = BistroBuilderCanonicalOrderLineState.Draft;
                return false;
        }
    }

    public static bool IsAggregateCompatible(
        OrderState legacyState,
        BistroBuilderCanonicalOrderState canonicalState
    )
    {
        return legacyState switch
        {
            OrderState.Created =>
                canonicalState == BistroBuilderCanonicalOrderState.Draft,

            OrderState.SentToKitchen =>
                canonicalState ==
                    BistroBuilderCanonicalOrderState.InProgress,

            OrderState.Preparing =>
                canonicalState ==
                    BistroBuilderCanonicalOrderState.InProgress,

            OrderState.ReadyForPickup =>
                canonicalState ==
                    BistroBuilderCanonicalOrderState.ReadyForPickup,

            OrderState.Served =>
                canonicalState == BistroBuilderCanonicalOrderState.Served,

            OrderState.Completed =>
                canonicalState ==
                    BistroBuilderCanonicalOrderState.Completed,

            OrderState.Cancelled =>
                canonicalState ==
                    BistroBuilderCanonicalOrderState.Cancelled,

            _ => false
        };
    }

    /// <summary>
    /// Valida estados coarse cuando 367D procesa líneas individualmente.
    /// No modifica el agregado canónico.
    /// </summary>
    public static bool TryValidateIndividualLineCompatibility(
        OrderState legacyState,
        IReadOnlyList<BistroBuilderCanonicalOrderLine> lines,
        out string error
    )
    {
        if (lines == null || lines.Count == 0)
        {
            error = "La comanda canónica no contiene líneas.";
            return false;
        }

        bool hasActive = false;
        bool anyPreparingOrLater = false;
        bool allPastKitchen = true;
        bool allServed = true;

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = lines[index];

            if (line == null)
            {
                error = "La comanda canónica contiene una línea nula.";
                return false;
            }

            BistroBuilderCanonicalOrderLineState state = line.State;

            if (state == BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                continue;
            }

            if (state == BistroBuilderCanonicalOrderLineState.Failed)
            {
                error =
                    "Una línea fallida requiere un flujo de incidencia antes " +
                    "de continuar la comanda.";
                return false;
            }

            hasActive = true;

            int stateValue = (int)state;

            if (stateValue >=
                    (int)BistroBuilderCanonicalOrderLineState.Preparing &&
                stateValue <=
                    (int)BistroBuilderCanonicalOrderLineState.Consumed)
            {
                anyPreparingOrLater = true;
            }

            if (state == BistroBuilderCanonicalOrderLineState.Draft ||
                state == BistroBuilderCanonicalOrderLineState.Submitted ||
                state == BistroBuilderCanonicalOrderLineState.Queued ||
                state == BistroBuilderCanonicalOrderLineState.Preparing)
            {
                allPastKitchen = false;
            }

            if (state != BistroBuilderCanonicalOrderLineState.Served &&
                state != BistroBuilderCanonicalOrderLineState.Consumed)
            {
                allServed = false;
            }
        }

        if (!hasActive)
        {
            error = "La comanda no contiene líneas activas.";
            return false;
        }

        bool compatible = legacyState switch
        {
            OrderState.Preparing => anyPreparingOrLater,
            OrderState.ReadyForPickup => allPastKitchen,
            OrderState.Served => allServed,
            _ => false
        };

        error = compatible
            ? string.Empty
            : "Las líneas canónicas no son compatibles con " +
              legacyState + ".";

        return compatible;
    }

}
