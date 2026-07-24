using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de entrada operativo para crear, consultar y cerrar comandas.
///
/// Desde 367C toda comanda jugable se crea primero en la autoridad canónica.
/// RestaurantOrder permanece como fachada temporal para los sistemas legacy
/// de cocina, camareros, cuenta y mesa.
/// </summary>
public sealed class OrderSystem : MonoBehaviour
{
    [Header("Identificación de comandas")]
    [SerializeField, Min(1)]
    private int nextOrderId = 1;

    [Header("Integración canónica 367C")]
    [SerializeField]
    private BistroBuilderCanonicalOrderIntegrationService
        canonicalIntegrationService;

    private readonly List<RestaurantOrder> activeOrders = new();

    public event Action<RestaurantOrder> OrderCreated;
    public event Action<RestaurantOrder> OrderCompleted;
    public event Action<RestaurantOrder> OrderCancelled;

    public IReadOnlyList<RestaurantOrder> ActiveOrders =>
        activeOrders;

    public BistroBuilderCanonicalOrderIntegrationService
        CanonicalIntegrationService =>
            canonicalIntegrationService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (nextOrderId < 1)
        {
            error = "La siguiente identidad legacy de comanda es inválida.";
            return false;
        }

        if (canonicalIntegrationService == null)
        {
            error =
                "OrderSystem no tiene asignada la integración canónica 367C.";
            return false;
        }

        if (!canonicalIntegrationService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

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

        CacheDependenciesIfNeeded();

        if (canonicalIntegrationService == null)
        {
            Debug.LogError(
                "No se puede crear la comanda: falta la integración " +
                "canónica 367C.",
                this
            );

            return null;
        }

        int legacyOrderId = nextOrderId;

        if (!canonicalIntegrationService.TryCreateCanonicalOrder(
                table,
                customerGroup,
                waiter,
                legacyOrderId,
                out string canonicalOrderId,
                out string creationError
            ))
        {
            Debug.LogError(
                "No se pudo crear la comanda canónica para la mesa " +
                table.TableId + ". " + creationError,
                this
            );

            return null;
        }

        RestaurantOrder order;

        try
        {
            order = new RestaurantOrder(
                legacyOrderId,
                table,
                customerGroup,
                waiter,
                canonicalOrderId,
                canonicalIntegrationService
            );
        }
        catch (Exception exception)
        {
            canonicalIntegrationService
                .TryRollbackUnregisteredCanonicalOrder(
                    canonicalOrderId,
                    out _
                );

            Debug.LogException(exception, this);
            return null;
        }

        if (!canonicalIntegrationService.TryRegisterLegacyOrder(
                order,
                out string registrationError
            ))
        {
            canonicalIntegrationService
                .TryRollbackUnregisteredCanonicalOrder(
                    canonicalOrderId,
                    out _
                );

            Debug.LogError(
                "No se pudo registrar el enlace legacy-canónico. " +
                registrationError,
                this
            );

            return null;
        }

        nextOrderId++;
        activeOrders.Add(order);

        Debug.Log(
            $"Comanda {order.OrderId} creada para la mesa " +
            $"{table.TableId}, grupo {customerGroup.GroupId}. " +
            $"CanonicalOrderId: {order.CanonicalOrderId}.",
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
        {
            return null;
        }

        foreach (RestaurantOrder order in activeOrders)
        {
            if (order.Table == table && !order.IsFinished)
            {
                return order;
            }
        }

        return null;
    }

    public bool CompleteOrder(RestaurantOrder order)
    {
        if (order == null ||
            !activeOrders.Contains(order))
        {
            return false;
        }

        if (!order.TrySetState(OrderState.Completed))
        {
            Debug.LogError(
                "No se pudo completar la comanda " +
                order.OrderId + ". " +
                order.LastTransitionError,
                this
            );

            return false;
        }

        activeOrders.Remove(order);
        canonicalIntegrationService?.NotifyLegacyOrderRemoved(order);

        Debug.Log(
            $"Comanda {order.OrderId} completada.",
            this
        );

        OrderCompleted?.Invoke(order);

        return true;
    }

    /// <summary>
    /// Cancela y retira una comanda activa de manera coordinada.
    ///
    /// La puerta de transición cancela primero la comanda canónica. Solo si
    /// esa operación termina correctamente se elimina la fachada legacy.
    /// </summary>
    public bool CancelOrder(RestaurantOrder order)
    {
        if (order == null ||
            !activeOrders.Contains(order))
        {
            return false;
        }

        if (!order.TrySetState(OrderState.Cancelled))
        {
            Debug.LogError(
                "No se pudo cancelar la comanda " +
                order.OrderId + ". " +
                order.LastTransitionError,
                this
            );

            return false;
        }

        activeOrders.Remove(order);
        canonicalIntegrationService?.NotifyLegacyOrderRemoved(order);

        Debug.Log(
            $"Comanda {order.OrderId} cancelada.",
            this
        );

        OrderCancelled?.Invoke(order);
        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (canonicalIntegrationService == null)
        {
            TryGetComponent(out canonicalIntegrationService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
