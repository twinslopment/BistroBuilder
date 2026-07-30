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


    public int NextOrderId => Mathf.Max(1, nextOrderId);

    /// <summary>
    /// Restaura la siguiente identidad legacy incluso cuando el restaurante
    /// estaba cerrado y no existían comandas activas en service.runtime.
    /// Evita reutilizar OrderId antiguos después de cargar.
    /// </summary>
    public bool TryRestoreNextOrderId(int restoredNextOrderId)
    {
        if (restoredNextOrderId < 1)
        {
            return false;
        }

        nextOrderId = restoredNextOrderId;
        return true;
    }

    public void ClearRuntimeForLoad()
    {
        for (int index = 0; index < activeOrders.Count; index++)
        {
            canonicalIntegrationService?.NotifyLegacyOrderRemoved(
                activeOrders[index]
            );
        }

        activeOrders.Clear();
        canonicalIntegrationService?.ClearRuntimeLinksForLoad();
    }

    public bool TryRestoreRuntimeOrders(
        IList<BistroBuilderLegacyOrderSaveRecord> records,
        IReadOnlyDictionary<int, CustomerGroup> groupsById,
        RestaurantTableRegistry tableRegistry,
        BistroBuilderBarServiceRegistry barRegistry,
        IReadOnlyDictionary<int, Waiter> waitersById,
        int restoredNextOrderId,
        Dictionary<string, RestaurantOrder> ordersByCanonicalId,
        out string error
    )
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (records == null || groupsById == null || tableRegistry == null ||
            barRegistry == null || waitersById == null ||
            ordersByCanonicalId == null || restoredNextOrderId < 1 ||
            canonicalIntegrationService == null)
        {
            error = "Faltan datos o dependencias para restaurar las comandas.";
            return false;
        }

        ClearRuntimeForLoad();
        ordersByCanonicalId.Clear();
        var legacyIds = new HashSet<int>();
        var canonicalIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < records.Count; index++)
        {
            BistroBuilderLegacyOrderSaveRecord record = records[index];

            if (record == null || !record.TryValidate(out error) ||
                !legacyIds.Add(record.legacyOrderId) ||
                !canonicalIds.Add(record.canonicalOrderId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Las comandas legacy persistidas contienen duplicados.";
                }
                ClearRuntimeForLoad();
                return false;
            }

            if (!groupsById.TryGetValue(record.groupId, out CustomerGroup group) ||
                group == null ||
                !waitersById.TryGetValue(record.waiterId, out Waiter waiter) ||
                waiter == null)
            {
                error = "No se pudo resolver el grupo o camarero de una comanda.";
                ClearRuntimeForLoad();
                return false;
            }

            RestaurantOrder order;
            BistroBuilderServiceMode mode =
                (BistroBuilderServiceMode)record.serviceMode;

            try
            {
                if (mode == BistroBuilderServiceMode.TableService)
                {
                    if (!tableRegistry.TryGetTableById(
                            record.tableId,
                            out RestaurantTable table
                        ) || table == null)
                    {
                        error = "No se pudo resolver la mesa de una comanda.";
                        ClearRuntimeForLoad();
                        return false;
                    }

                    order = new RestaurantOrder(
                        record.legacyOrderId,
                        table,
                        group,
                        waiter,
                        record.canonicalOrderId,
                        canonicalIntegrationService
                    );
                }
                else
                {
                    if (!barRegistry.TryGetSpot(
                            record.barSpotId,
                            out BistroBuilderBarServiceSpot spot
                        ) || spot == null)
                    {
                        error = "No se pudo resolver la plaza de barra de una comanda.";
                        ClearRuntimeForLoad();
                        return false;
                    }

                    order = new RestaurantOrder(
                        record.legacyOrderId,
                        spot,
                        group,
                        waiter,
                        mode,
                        record.canonicalOrderId,
                        canonicalIntegrationService
                    );
                }
            }
            catch (Exception exception)
            {
                error = "No se pudo reconstruir una comanda legacy. " +
                        exception.Message;
                ClearRuntimeForLoad();
                return false;
            }

            if (!order.TryRestoreRuntimeState(
                    (OrderState)record.orderState,
                    out error
                ) ||
                !canonicalIntegrationService.TryRegisterLegacyOrder(
                    order,
                    out error
                ))
            {
                ClearRuntimeForLoad();
                return false;
            }

            activeOrders.Add(order);
            ordersByCanonicalId.Add(order.CanonicalOrderId, order);
            OrderCreated?.Invoke(order);
        }

        nextOrderId = restoredNextOrderId;
        return true;
    }

    public bool TryResumeCreatedOrders(out string error)
    {
        error = string.Empty;

        for (int index = 0; index < activeOrders.Count; index++)
        {
            RestaurantOrder order = activeOrders[index];

            if (order != null && order.CurrentState == OrderState.Created &&
                !order.TrySetState(OrderState.SentToKitchen))
            {
                error = "No se pudo reanudar la comanda " + order.OrderId +
                        ". " + order.LastTransitionError;
                return false;
            }
        }

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


    public RestaurantOrder CreateBarOrder(
        BistroBuilderBarServiceSpot barSpot,
        CustomerGroup customerGroup,
        Waiter waiter,
        BistroBuilderServiceMode serviceMode,
        IList<string> dishIds
    )
    {
        if (barSpot == null || customerGroup == null || waiter == null)
        {
            Debug.LogError(
                "No se puede crear una comanda de barra sin plaza, grupo " +
                "y camarero.",
                this
            );
            return null;
        }

        if (!BistroBuilderServiceModeUtility.IsBarMode(serviceMode) ||
            !ReferenceEquals(barSpot.AssignedCustomerGroup, customerGroup) ||
            !ReferenceEquals(customerGroup.AssignedBarSpot, barSpot))
        {
            Debug.LogError(
                "El contexto de barra no es coherente para crear la comanda.",
                this
            );
            return null;
        }

        RestaurantOrder existing = GetActiveOrderForBarSpot(barSpot);

        if (existing != null)
        {
            return existing;
        }

        CacheDependenciesIfNeeded();

        if (canonicalIntegrationService == null)
        {
            Debug.LogError(
                "Falta la integración canónica para crear la comanda de barra.",
                this
            );
            return null;
        }

        int legacyOrderId = nextOrderId;

        if (!canonicalIntegrationService.TryCreateCanonicalBarOrder(
                barSpot,
                customerGroup,
                waiter,
                legacyOrderId,
                serviceMode,
                dishIds,
                out string canonicalOrderId,
                out string creationError
            ))
        {
            Debug.LogError(
                "No se pudo crear la comanda canónica de barra. " +
                creationError,
                this
            );
            return null;
        }

        RestaurantOrder order;

        try
        {
            order = new RestaurantOrder(
                legacyOrderId,
                barSpot,
                customerGroup,
                waiter,
                serviceMode,
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
                "No se pudo registrar la comanda de barra. " +
                registrationError,
                this
            );
            return null;
        }

        nextOrderId++;
        activeOrders.Add(order);

        Debug.Log(
            "Comanda " + order.OrderId + " creada para la plaza " +
            barSpot.BarSpotId + ", grupo " + customerGroup.GroupId +
            ", modalidad " + serviceMode + ". CanonicalOrderId: " +
            order.CanonicalOrderId + ".",
            this
        );

        OrderCreated?.Invoke(order);
        return order;
    }

    public RestaurantOrder GetActiveOrderForBarSpot(
        BistroBuilderBarServiceSpot barSpot
    )
    {
        if (barSpot == null)
        {
            return null;
        }

        for (int index = 0; index < activeOrders.Count; index++)
        {
            RestaurantOrder order = activeOrders[index];

            if (order != null &&
                ReferenceEquals(order.BarSpot, barSpot) &&
                !order.IsFinished)
            {
                return order;
            }
        }

        return null;
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
