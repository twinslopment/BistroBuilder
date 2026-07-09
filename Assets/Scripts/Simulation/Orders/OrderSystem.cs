using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OrderSystem : MonoBehaviour
{
    [Header("Identificación de comandas")]
    [SerializeField, Min(1)]
    private int nextOrderId = 1;

    private readonly List<RestaurantOrder> activeOrders = new();

    public event Action<RestaurantOrder> OrderCreated;
    public event Action<RestaurantOrder> OrderCompleted;

    public IReadOnlyList<RestaurantOrder> ActiveOrders =>
        activeOrders;

    public RestaurantOrder CreateOrder(
        RestaurantTable table,
        Waiter waiter
    )
    {
        if (table == null)
        {
            Debug.LogError(
                "No se puede crear una comanda sin mesa.",
                this
            );

            return null;
        }

        CustomerGroup customerGroup =
            table.AssignedCustomerGroup;

        if (customerGroup == null)
        {
            Debug.LogError(
                $"La mesa {table.TableId} no tiene un grupo asignado.",
                table
            );

            return null;
        }

        if (waiter == null)
        {
            Debug.LogError(
                "No se puede crear una comanda sin camarero.",
                this
            );

            return null;
        }

        if (waiter.AssignedTable != table)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} no está asignado " +
                $"a la mesa {table.TableId}.",
                waiter
            );

            return null;
        }

        RestaurantOrder existingOrder =
            GetActiveOrderForTable(table);

        if (existingOrder != null)
        {
            Debug.LogWarning(
                $"La mesa {table.TableId} ya tiene una comanda activa.",
                table
            );

            return existingOrder;
        }

        RestaurantOrder order = new(
            nextOrderId,
            table,
            customerGroup,
            waiter
        );

        nextOrderId++;
        activeOrders.Add(order);

        Debug.Log(
            $"Comanda {order.OrderId} creada para la mesa " +
            $"{table.TableId}, grupo {customerGroup.GroupId}.",
            this
        );

        OrderCreated?.Invoke(order);

        return order;
    }

    public RestaurantOrder GetActiveOrderForTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return null;

        foreach (RestaurantOrder order in activeOrders)
        {
            if (order.Table == table && !order.IsFinished)
                return order;
        }

        return null;
    }

    public bool CompleteOrder(RestaurantOrder order)
    {
        if (order == null)
            return false;

        if (!activeOrders.Contains(order))
            return false;

        if (!order.TrySetState(OrderState.Completed))
            return false;

        activeOrders.Remove(order);

        Debug.Log(
            $"Comanda {order.OrderId} completada.",
            this
        );

        OrderCompleted?.Invoke(order);

        return true;
    }
}