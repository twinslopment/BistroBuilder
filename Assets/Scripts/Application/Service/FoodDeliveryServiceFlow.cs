using System.Collections;
using UnityEngine;

/// <summary>
/// Ejecuta físicamente la recogida y entrega de una comanda.
///
/// La selección y asignación del camarero corresponde al
/// WaiterTaskCoordinator. Esta clase se limita a ejecutar:
/// - El desplazamiento hasta cocina.
/// - La recogida del plato.
/// - El desplazamiento hasta la mesa.
/// - La entrega de la comida.
/// - La confirmación o recuperación de la tarea.
/// </summary>
public sealed class FoodDeliveryServiceFlow : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private WaiterMovementView waiterMovementView;

    [Tooltip(
        "Coordinador central de tareas. Si no se configura " +
        "manualmente, se buscará automáticamente en la escena."
    )]
    [SerializeField]
    private WaiterTaskCoordinator taskCoordinator;

    [Header("Duraciones provisionales")]

    [SerializeField, Min(0.1f)]
    private float pickupDuration = 1f;

    [SerializeField, Min(0.1f)]
    private float servingDuration = 2f;

    /// <summary>
    /// Corrutina operativa actualmente en ejecución.
    ///
    /// Impide iniciar dos procesos de recogida o servicio
    /// simultáneamente para el mismo camarero.
    /// </summary>
    private Coroutine activeRoutine;

    private void Awake()
    {
        ResolveTaskCoordinator();
    }

    private void OnEnable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached +=
                HandleDestinationReached;
        }
    }

    private void Start()
    {
        ResolveTaskCoordinator();
        ValidateConfiguration();
    }

    private void OnDisable()
    {
        if (waiterMovementView != null)
        {
            waiterMovementView.DestinationReached -=
                HandleDestinationReached;
        }

        RestaurantOrder interruptedOrder =
            waiter != null
                ? waiter.AssignedOrder
                : null;

        bool wasExecutingDelivery =
            waiter != null &&
            (
                waiter.CurrentState ==
                    WaiterState.WalkingToKitchen ||
                waiter.CurrentState ==
                    WaiterState.WaitingForDish ||
                waiter.CurrentState ==
                    WaiterState.WalkingToServeTable ||
                waiter.CurrentState ==
                    WaiterState.ServingFood
            );

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        // Si el flujo se desactiva durante una entrega,
        // la tarea debe regresar al coordinador para no quedar
        // bloqueada indefinidamente.
        if (Application.isPlaying &&
            wasExecutingDelivery)
        {
            if (taskCoordinator != null)
            {
                taskCoordinator.ReportFoodDeliveryFailure(
                    waiter,
                    interruptedOrder
                );
            }

            if (waiter != null &&
                interruptedOrder != null &&
                waiter.AssignedOrder ==
                    interruptedOrder)
            {
                waiter.ClearAssignment();
            }
        }
    }

    /// <summary>
    /// Reacciona cuando el camarero alcanza un destino.
    ///
    /// El estado actual determina si ha llegado a cocina
    /// o a la mesa donde debe servir.
    /// </summary>
    private void HandleDestinationReached(
        WaiterMovementView movementView
    )
    {
        if (waiter == null ||
            activeRoutine != null)
        {
            return;
        }

        if (waiter.CurrentState ==
            WaiterState.WalkingToKitchen)
        {
            activeRoutine =
                StartCoroutine(
                    PickupFoodRoutine()
                );

            return;
        }

        if (waiter.CurrentState ==
            WaiterState.WalkingToServeTable)
        {
            activeRoutine =
                StartCoroutine(
                    ServeFoodRoutine()
                );
        }
    }

    /// <summary>
    /// Ejecuta la recogida de una comanda preparada.
    /// </summary>
    private IEnumerator PickupFoodRoutine()
    {
        RestaurantOrder order =
            waiter.AssignedOrder;

        if (order == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} " +
                "no tiene comanda asignada.",
                waiter
            );

            AbortDelivery(
                null,
                true
            );

            yield break;
        }

        if (order.CurrentState !=
            OrderState.ReadyForPickup)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} " +
                "no está lista para recoger.",
                this
            );

            AbortDelivery(
                order,
                true
            );

            yield break;
        }

        waiter.SetState(
            WaiterState.WaitingForDish
        );

        Debug.Log(
            $"Camarero {waiter.WaiterId} recoge la comanda " +
            $"{order.OrderId} en cocina.",
            this
        );

        yield return new WaitForSeconds(
            pickupDuration
        );

        bool assignmentStillValid =
            waiter.AssignedOrder == order &&
            waiter.AssignedTable ==
                order.Table;

        if (!assignmentStillValid)
        {
            Debug.LogWarning(
                "La asignación del camarero cambió " +
                "durante la recogida.",
                this
            );

            // No se limpia la asignación del camarero porque
            // podría haber recibido otro trabajo válido.
            AbortDelivery(
                order,
                false
            );

            yield break;
        }

        waiter.SetState(
            WaiterState.WalkingToServeTable
        );

        activeRoutine = null;
    }

    /// <summary>
    /// Ejecuta la entrega de la comida en la mesa.
    /// </summary>
    private IEnumerator ServeFoodRoutine()
    {
        RestaurantOrder order =
            waiter.AssignedOrder;

        if (order == null)
        {
            Debug.LogError(
                $"El camarero {waiter.WaiterId} " +
                "no tiene comanda asignada.",
                waiter
            );

            AbortDelivery(
                null,
                true
            );

            yield break;
        }

        RestaurantTable table =
            order.Table;

        CustomerGroup customerGroup =
            order.CustomerGroup;

        if (table == null ||
            customerGroup == null)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} " +
                "tiene datos incompletos.",
                this
            );

            AbortDelivery(
                order,
                true
            );

            yield break;
        }

        waiter.SetState(
            WaiterState.ServingFood
        );

        Debug.Log(
            $"Camarero {waiter.WaiterId} sirve la comanda " +
            $"{order.OrderId} en la mesa {table.TableId}.",
            this
        );

        yield return new WaitForSeconds(
            servingDuration
        );

        bool served =
            order.TrySetState(
                OrderState.Served
            );

        if (!served)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} " +
                "no pudo pasar a Served.",
                this
            );

            AbortDelivery(
                order,
                true
            );

            yield break;
        }

        table.SetState(
            TableState.Eating
        );

        customerGroup.SetState(
            CustomerGroupState.Eating
        );

        Debug.Log(
            $"Comanda {order.OrderId} servida al grupo " +
            $"{customerGroup.GroupId}.",
            this
        );

        bool taskCompleted =
            taskCoordinator != null &&
            taskCoordinator
                .TryCompleteFoodDeliveryTask(order);

        if (!taskCompleted)
        {
            Debug.LogWarning(
                $"No se encontró una tarea activa para completar " +
                $"el reparto de la comanda {order.OrderId}.",
                this
            );

            // Se solicita al coordinador que limpie cualquier
            // tarea inconsistente relacionada con el reparto.
            if (taskCoordinator != null)
            {
                taskCoordinator.ReportFoodDeliveryFailure(
                    waiter,
                    order
                );
            }
        }

        // Se elimina la referencia antes de cambiar el camarero
        // a Idle, porque ese evento puede generar inmediatamente
        // una nueva asignación.
        activeRoutine = null;

        waiter.ClearAssignment();
    }

    /// <summary>
    /// Informa al coordinador de que el reparto no pudo terminar.
    ///
    /// El coordinador decidirá si la tarea puede reintentarse
    /// o debe eliminarse definitivamente.
    /// </summary>
    private void AbortDelivery(
        RestaurantOrder order,
        bool clearWaiterAssignment
    )
    {
        activeRoutine = null;

        if (taskCoordinator != null)
        {
            taskCoordinator.ReportFoodDeliveryFailure(
                waiter,
                order
            );
        }

        if (!clearWaiterAssignment ||
            waiter == null)
        {
            return;
        }

        bool canClearAssignment =
            order == null ||
            waiter.AssignedOrder == order;

        if (canClearAssignment)
        {
            waiter.ClearAssignment();
        }
    }

    /// <summary>
    /// Localiza el coordinador cuando no se ha configurado
    /// manualmente en el Inspector.
    ///
    /// Esta búsqueda se realiza durante la inicialización,
    /// nunca de forma continua durante Update.
    /// </summary>
    private void ResolveTaskCoordinator()
    {
        if (taskCoordinator != null)
            return;

        taskCoordinator =
            FindFirstObjectByType<
                WaiterTaskCoordinator
            >();
    }

    /// <summary>
    /// Comprueba que todas las referencias esenciales
    /// estén configuradas.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "FoodDeliveryServiceFlow necesita " +
                "una referencia a Waiter.",
                this
            );
        }

        if (waiterMovementView == null)
        {
            Debug.LogError(
                "FoodDeliveryServiceFlow necesita una referencia " +
                "a WaiterMovementView.",
                this
            );
        }

        if (taskCoordinator == null)
        {
            Debug.LogError(
                "FoodDeliveryServiceFlow no ha encontrado " +
                "WaiterTaskCoordinator.",
                this
            );
        }
    }
}