using System;
using UnityEngine;

/// <summary>
/// Modalidad operativa de una visita o comanda.
///
/// TableService consume en una mesa.
/// BarService completa toda la visita en la barra.
/// WaitingAtBar conserva la posición en la cola de mesas mientras el grupo
/// mantiene una sesión temporal e independiente en la barra.
/// </summary>
public enum BistroBuilderServiceMode
{
    TableService = 0,
    BarService = 1,
    WaitingAtBar = 2
}

/// <summary>
/// Modalidades en las que un artículo de carta puede pedirse.
/// Se expresa como máscara para que una misma definición pueda utilizarse en
/// mesa, barra completa y barra de espera sin duplicar assets.
/// </summary>
[Flags]
public enum BistroBuilderDishServiceModeAvailability
{
    None = 0,
    TableService = 1 << 0,
    BarService = 1 << 1,
    WaitingAtBar = 1 << 2,
    All = TableService | BarService | WaitingAtBar
}

public enum BistroBuilderServiceDestinationKind
{
    None = -1,
    Table = 0,
    BarSpot = 1
}

/// <summary>
/// Funciones puras compartidas por comandas, reparto y UI para no volver a
/// introducir suposiciones de que todo destino operativo es una mesa.
/// </summary>
public static class BistroBuilderServiceModeUtility
{
    public static bool IsDefined(BistroBuilderServiceMode mode)
    {
        return Enum.IsDefined(typeof(BistroBuilderServiceMode), mode);
    }

    public static bool IsBarMode(BistroBuilderServiceMode mode)
    {
        return mode == BistroBuilderServiceMode.BarService ||
               mode == BistroBuilderServiceMode.WaitingAtBar;
    }

    public static BistroBuilderDishServiceModeAvailability ToAvailability(
        BistroBuilderServiceMode mode
    )
    {
        switch (mode)
        {
            case BistroBuilderServiceMode.TableService:
                return BistroBuilderDishServiceModeAvailability.TableService;
            case BistroBuilderServiceMode.BarService:
                return BistroBuilderDishServiceModeAvailability.BarService;
            case BistroBuilderServiceMode.WaitingAtBar:
                return BistroBuilderDishServiceModeAvailability.WaitingAtBar;
            default:
                return BistroBuilderDishServiceModeAvailability.None;
        }
    }

    public static bool IsValidAvailabilityMask(
        BistroBuilderDishServiceModeAvailability availability,
        bool allowNone
    )
    {
        int raw = (int)availability;
        int known = (int)BistroBuilderDishServiceModeAvailability.All;

        if ((raw & ~known) != 0)
        {
            return false;
        }

        return allowNone || availability !=
            BistroBuilderDishServiceModeAvailability.None;
    }

    public static string BuildDestinationReference(
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot
    )
    {
        if (table != null)
        {
            return BistroBuilderServiceOrderIdentityUtility
                .BuildTableReference(table.TableId);
        }

        return barSpot != null
            ? barSpot.BarSpotId
            : string.Empty;
    }

    public static Transform GetWaiterServicePoint(
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot
    )
    {
        if (table != null)
        {
            return table.WaiterServicePoint != null
                ? table.WaiterServicePoint
                : table.transform;
        }

        if (barSpot != null)
        {
            return barSpot.WaiterServicePoint != null
                ? barSpot.WaiterServicePoint
                : barSpot.transform;
        }

        return null;
    }

    public static Transform GetCustomerServicePoint(
        RestaurantTable table,
        BistroBuilderBarServiceSpot barSpot
    )
    {
        if (table != null)
        {
            return table.CustomerApproachPoint != null
                ? table.CustomerApproachPoint
                : table.transform;
        }

        if (barSpot != null)
        {
            return barSpot.CustomerPoint != null
                ? barSpot.CustomerPoint
                : barSpot.transform;
        }

        return null;
    }
}
