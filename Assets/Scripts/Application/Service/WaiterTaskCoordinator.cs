using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina centralmente las tareas operativas de los camareros.
///
/// Está preparado para gestionar dinámicamente:
/// - Cualquier número de mesas.
/// - Cualquier número de camareros.
/// - Cualquier número de cocinas y comandas.
/// - Elementos creados o retirados durante la partida.
///
/// El coordinador funciona mediante eventos. No utiliza Update ni
/// realiza búsquedas continuas cada frame.
/// </summary>
public sealed class WaiterTaskCoordinator : MonoBehaviour
{
    [Header("Descubrimiento inicial")]

    [Tooltip(
        "Busca al comenzar las mesas, camareros y cocinas " +
        "que ya existen en la escena."
    )]
    [SerializeField]
    private bool discoverSceneObjectsOnStart = true;

    [Header("Migración de sistemas")]

    [Tooltip(
        "Gestiona centralmente las tareas para tomar comandas. " +
        "Debe permanecer desactivado mientras siga activo " +
        "WaiterAssignmentSystem."
    )]
    [SerializeField]
    private bool manageTakeOrderTasks;

    [Tooltip(
        "Gestiona centralmente la recogida y entrega de comida."
    )]
    [SerializeField]
    private bool manageFoodDeliveryTasks = true;

    [Tooltip(
        "Gestiona centralmente la entrega de cuentas. " +
        "Debe permanecer desactivado mientras siga activo " +
        "BillAssignmentSystem."
    )]
    [SerializeField]
    private bool manageBillTasks;

    [Tooltip(
        "Gestiona centralmente la limpieza de mesas. " +
        "Debe permanecer desactivado mientras siga activo " +
        "TableCleaningAssignmentSystem."
    )]
    [SerializeField]
    private bool manageCleaningTasks;

    /// <summary>
    /// Mesas conocidas actualmente por el coordinador.
    /// HashSet impide registros duplicados.
    /// </summary>
    private readonly HashSet<RestaurantTable>
        registeredTables =
            new HashSet<RestaurantTable>();

    /// <summary>
    /// Camareros conocidos actualmente por el coordinador.
    /// </summary>
    private readonly HashSet<Waiter>
        registeredWaiters =
            new HashSet<Waiter>();

    /// <summary>
    /// Cocinas conocidas actualmente por el coordinador.
    /// </summary>
    private readonly HashSet<KitchenSystem>
        registeredKitchens =
            new HashSet<KitchenSystem>();

    /// <summary>
    /// Almacena un manejador de OrderReady para cada cocina.
    ///
    /// Es necesario porque el evento entrega la comanda,
    /// pero no indica directamente qué cocina lo emitió.
    /// </summary>
    private readonly Dictionary<
        KitchenSystem,
        Action<RestaurantOrder>
    > kitchenOrderReadyHandlers =
        new Dictionary<
            KitchenSystem,
            Action<RestaurantOrder>
        >();

    /// <summary>
    /// Relaciona cada comanda preparada con la cocina
    /// donde debe recogerse.
    /// </summary>
    private readonly Dictionary<
        RestaurantOrder,
        KitchenSystem
    > kitchenByOrder =
        new Dictionary<
            RestaurantOrder,
            KitchenSystem
        >();

    /// <summary>
    /// Cola central de tareas activas.
    /// </summary>
    private WaiterTaskQueue taskQueue;

    /// <summary>
    /// Evita despachos reentrantes provocados por eventos
    /// encadenados durante una asignación.
    /// </summary>
    private bool isDispatching;

    /// <summary>
    /// Indica que existe una petición de reparto pendiente.
    ///
    /// Los eventos de mesas, camareros y cocina no reparten tareas
    /// directamente dentro de su propia pila de llamadas. En su lugar,
    /// solicitan un reparto que se ejecuta al frame siguiente.
    /// </summary>
    private bool dispatchRequested;

    /// <summary>
    /// Corrutina que agrupa las peticiones de reparto y las ejecuta
    /// fuera de las cadenas de eventos que las originaron.
    /// </summary>
    private Coroutine dispatchRoutine;

    /// <summary>
    /// Indica si Start ya se ha ejecutado.
    /// </summary>
    private bool hasStarted;

    public int RegisteredTableCount =>
        registeredTables.Count;

    public int RegisteredWaiterCount =>
        registeredWaiters.Count;

    public int RegisteredKitchenCount =>
        registeredKitchens.Count;

    public int ActiveTaskCount =>
        taskQueue != null
            ? taskQueue.Count
            : 0;

    public IReadOnlyList<WaiterTask> ActiveTasks =>
        taskQueue != null
            ? taskQueue.ActiveTasks
            : Array.Empty<WaiterTask>();

    private void Awake()
    {
        EnsureTaskQueueCreated();
    }

    private void OnEnable()
    {
        EnsureTaskQueueCreated();
        SubscribeToRegisteredElements();

        if (!hasStarted)
            return;

        SynchronizeAllTables();
        RequestDispatch();
    }

    private void Start()
    {
        hasStarted = true;

        if (discoverSceneObjectsOnStart)
        {
            DiscoverExistingSceneObjects();
        }

        SynchronizeAllTables();
        ValidateRuntimeConfiguration();
        RequestDispatch();
    }

    private void OnDisable()
    {
        UnsubscribeFromRegisteredElements();

        if (dispatchRoutine != null)
        {
            StopCoroutine(dispatchRoutine);
            dispatchRoutine = null;
        }

        dispatchRequested = false;
        isDispatching = false;
    }

    private void OnDestroy()
    {
        UnsubscribeFromRegisteredElements();

        if (taskQueue != null)
        {
            taskQueue.Clear();
        }

        registeredTables.Clear();
        registeredWaiters.Clear();
        registeredKitchens.Clear();

        kitchenOrderReadyHandlers.Clear();
        kitchenByOrder.Clear();
    }

    /// <summary>
    /// Registra una mesa nueva o recién activada.
    /// </summary>
    public bool RegisterTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return false;

        /*
         * Una mesa puede registrarse desde el Awake de otro sistema.
         * Por tanto, este método público no puede asumir que el Awake
         * de WaiterTaskCoordinator ya haya creado la cola.
         */
        EnsureTaskQueueCreated();

        if (!registeredTables.Add(table))
            return false;

        if (isActiveAndEnabled)
        {
            SubscribeToTable(table);
        }

        SynchronizeTableTasks(table);
        RequestDispatch();

        return true;
    }

    /// <summary>
    /// Retira una mesa y cancela todas sus tareas.
    /// </summary>
    public bool UnregisterTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return false;

        if (!registeredTables.Remove(table))
            return false;

        UnsubscribeFromTable(table);
        CancelTasksForTable(table);

        return true;
    }

    /// <summary>
    /// Registra un camarero nuevo o que comienza su turno.
    /// </summary>
    public bool RegisterWaiter(
        Waiter waiter
    )
    {
        if (waiter == null)
            return false;

        if (!registeredWaiters.Add(waiter))
            return false;

        if (isActiveAndEnabled)
        {
            SubscribeToWaiter(waiter);
        }

        RequestDispatch();

        return true;
    }

    /// <summary>
    /// Retira un camarero y recupera las tareas que tenía.
    /// </summary>
    public bool UnregisterWaiter(
        Waiter waiter
    )
    {
        if (waiter == null)
            return false;

        if (!registeredWaiters.Remove(waiter))
            return false;

        UnsubscribeFromWaiter(waiter);
        RecoverTasksAssignedToWaiter(waiter);
        RequestDispatch();

        return true;
    }

    /// <summary>
    /// Registra una cocina y escucha sus comandas preparadas.
    /// </summary>
    public bool RegisterKitchenSystem(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null)
            return false;

        if (!registeredKitchens.Add(kitchenSystem))
            return false;

        Action<RestaurantOrder> handler =
            order =>
                HandleOrderReady(
                    kitchenSystem,
                    order
                );

        kitchenOrderReadyHandlers.Add(
            kitchenSystem,
            handler
        );

        if (isActiveAndEnabled)
        {
            SubscribeToKitchen(kitchenSystem);
        }

        return true;
    }

    /// <summary>
    /// Retira una cocina y cancela sus repartos pendientes.
    /// </summary>
    public bool UnregisterKitchenSystem(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null)
            return false;

        if (!registeredKitchens.Contains(
                kitchenSystem
            ))
        {
            return false;
        }

        UnsubscribeFromKitchen(kitchenSystem);
        CancelFoodTasksForKitchen(kitchenSystem);

        registeredKitchens.Remove(
            kitchenSystem
        );

        kitchenOrderReadyHandlers.Remove(
            kitchenSystem
        );

        return true;
    }

    /// <summary>
    /// Completa la tarea de una comanda ya servida.
    ///
    /// FoodDeliveryServiceFlow debe llamar a este método
    /// cuando el servicio termina correctamente.
    /// </summary>
    public bool TryCompleteFoodDeliveryTask(
        RestaurantOrder order
    )
    {
        if (order == null ||
            order.Table == null ||
            taskQueue == null)
        {
            return false;
        }

        bool found =
            taskQueue.TryGetActiveTask(
                WaiterTaskType.DeliverFood,
                order.Table,
                order,
                out WaiterTask task
            );

        if (!found)
            return false;

        bool completed =
            taskQueue.TryCompleteTask(task);

        if (completed)
        {
            kitchenByOrder.Remove(order);
        }

        return completed;
    }

    /// <summary>
    /// Recupera una tarea de reparto que no pudo terminar.
    ///
    /// Si la comanda sigue lista para recoger, se crea una nueva
    /// tarea pendiente. En caso contrario se retira definitivamente.
    /// </summary>
    public bool ReportFoodDeliveryFailure(
        Waiter waiter,
        RestaurantOrder order
    )
    {
        if (taskQueue == null)
            return false;

        WaiterTask task =
            FindFoodDeliveryTask(
                waiter,
                order
            );

        if (task == null)
            return false;

        RestaurantOrder affectedOrder =
            task.Order;

        bool cancelled =
            taskQueue.TryCancelTask(task);

        if (!cancelled)
            return false;

        // Se declara antes de la expresión para garantizar que
        // siempre esté inicializada cuando se utilice después.
        KitchenSystem sourceKitchen = null;

        bool orderCanBeRetried =
            affectedOrder != null &&
            affectedOrder.Table != null &&
            affectedOrder.CurrentState ==
                OrderState.ReadyForPickup &&
            kitchenByOrder.TryGetValue(
                affectedOrder,
                out sourceKitchen
            ) &&
            sourceKitchen != null &&
            registeredKitchens.Contains(
                sourceKitchen
            );

        if (orderCanBeRetried)
        {
            CreateFoodDeliveryTask(
                sourceKitchen,
                affectedOrder
            );
        }
        else if (affectedOrder != null)
        {
            kitchenByOrder.Remove(
                affectedOrder
            );
        }

        RequestDispatch();

        return true;
    }

    /// <summary>
    /// Descubre una sola vez los elementos ya existentes
    /// en la escena.
    ///
    /// Los elementos creados posteriormente deberán registrarse
    /// mediante los métodos públicos correspondientes.
    /// </summary>
    private void DiscoverExistingSceneObjects()
    {
        KitchenSystem[] kitchens =
            FindObjectsByType<KitchenSystem>(
                FindObjectsSortMode.None
            );

        foreach (KitchenSystem kitchen in kitchens)
        {
            RegisterKitchenSystem(kitchen);
        }

        Waiter[] waiters =
            FindObjectsByType<Waiter>(
                FindObjectsSortMode.None
            );

        foreach (Waiter waiter in waiters)
        {
            RegisterWaiter(waiter);
        }

        RestaurantTable[] tables =
            FindObjectsByType<RestaurantTable>(
                FindObjectsSortMode.None
            );

        foreach (RestaurantTable table in tables)
        {
            RegisterTable(table);
        }
    }

    /// <summary>
    /// Crea la cola central únicamente cuando todavía no existe.
    /// </summary>
    private void EnsureTaskQueueCreated()
    {
        if (taskQueue == null)
        {
            taskQueue =
                new WaiterTaskQueue();
        }
    }

    /// <summary>
    /// Suscribe todos los elementos que ya estaban registrados.
    /// </summary>
    private void SubscribeToRegisteredElements()
    {
        foreach (RestaurantTable table
                 in registeredTables)
        {
            SubscribeToTable(table);
        }

        foreach (Waiter waiter
                 in registeredWaiters)
        {
            SubscribeToWaiter(waiter);
        }

        foreach (KitchenSystem kitchen
                 in registeredKitchens)
        {
            SubscribeToKitchen(kitchen);
        }
    }

    /// <summary>
    /// Elimina todas las suscripciones activas.
    /// </summary>
    private void UnsubscribeFromRegisteredElements()
    {
        foreach (RestaurantTable table
                 in registeredTables)
        {
            UnsubscribeFromTable(table);
        }

        foreach (Waiter waiter
                 in registeredWaiters)
        {
            UnsubscribeFromWaiter(waiter);
        }

        foreach (KitchenSystem kitchen
                 in registeredKitchens)
        {
            UnsubscribeFromKitchen(kitchen);
        }
    }

    private void SubscribeToTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return;

        // El -= previo mantiene la suscripción idempotente.
        table.StateChanged -=
            HandleTableStateChanged;

        table.StateChanged +=
            HandleTableStateChanged;
    }

    private void UnsubscribeFromTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return;

        table.StateChanged -=
            HandleTableStateChanged;
    }

    private void SubscribeToWaiter(
        Waiter waiter
    )
    {
        if (waiter == null)
            return;

        waiter.StateChanged -=
            HandleWaiterStateChanged;

        waiter.StateChanged +=
            HandleWaiterStateChanged;
    }

    private void UnsubscribeFromWaiter(
        Waiter waiter
    )
    {
        if (waiter == null)
            return;

        waiter.StateChanged -=
            HandleWaiterStateChanged;
    }

    private void SubscribeToKitchen(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null)
            return;

        if (!kitchenOrderReadyHandlers.TryGetValue(
                kitchenSystem,
                out Action<RestaurantOrder> handler
            ))
        {
            return;
        }

        kitchenSystem.OrderReady -= handler;
        kitchenSystem.OrderReady += handler;
    }

    private void UnsubscribeFromKitchen(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null)
            return;

        if (!kitchenOrderReadyHandlers.TryGetValue(
                kitchenSystem,
                out Action<RestaurantOrder> handler
            ))
        {
            return;
        }

        kitchenSystem.OrderReady -= handler;
    }

    private void HandleTableStateChanged(
        RestaurantTable table,
        TableState newState
    )
    {
        SynchronizeTableTasks(table);
        RequestDispatch();
    }

    private void HandleWaiterStateChanged(
        Waiter waiter,
        WaiterState newState
    )
    {
        if (newState != WaiterState.Idle)
            return;

        RequestDispatch();
    }

    private void HandleOrderReady(
        KitchenSystem kitchenSystem,
        RestaurantOrder order
    )
    {
        if (!manageFoodDeliveryTasks)
            return;

        if (kitchenSystem == null ||
            order == null)
        {
            return;
        }

        if (order.Table == null)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no tiene mesa asignada.",
                this
            );

            return;
        }

        if (order.CurrentState !=
            OrderState.ReadyForPickup)
        {
            return;
        }

        CreateFoodDeliveryTask(
            kitchenSystem,
            order
        );

        RequestDispatch();
    }

    /// <summary>
    /// Crea una tarea de reparto sin duplicar una comanda
    /// que ya estuviera registrada.
    /// </summary>
    private bool CreateFoodDeliveryTask(
        KitchenSystem kitchenSystem,
        RestaurantOrder order
    )
    {
        if (kitchenSystem == null ||
            order == null ||
            order.Table == null)
        {
            return false;
        }

        kitchenByOrder[order] =
            kitchenSystem;

        bool created =
            taskQueue.TryCreateTask(
                WaiterTaskType.DeliverFood,
                WaiterTaskPriority.Urgent,
                order.Table,
                order,
                out WaiterTask task
            );

        if (created)
        {
            Debug.Log(
                $"Tarea {task.TaskId} creada para repartir " +
                $"la comanda {order.OrderId}.",
                this
            );
        }

        return created;
    }

    private void SynchronizeAllTables()
    {
        foreach (RestaurantTable table
                 in registeredTables)
        {
            if (table != null)
            {
                SynchronizeTableTasks(table);
            }
        }
    }

    private void SynchronizeTableTasks(
        RestaurantTable table
    )
    {
        if (table == null)
            return;

        /*
         * Protección adicional para cualquier llamada futura que
         * sincronice mesas antes de la inicialización normal.
         */
        EnsureTaskQueueCreated();

        SynchronizeTakeOrderTask(table);
        SynchronizeDeliverBillTask(table);
        SynchronizeCleanTableTask(table);
    }

    private void SynchronizeTakeOrderTask(
        RestaurantTable table
    )
    {
        bool taskExists =
            taskQueue.TryGetActiveTask(
                WaiterTaskType.TakeOrder,
                table,
                null,
                out WaiterTask task
            );

        if (!manageTakeOrderTasks)
        {
            if (taskExists)
            {
                taskQueue.TryCancelTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.TakingOrder)
        {
            if (taskExists)
            {
                taskQueue.TryCompleteTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.WaitingForWaiter)
        {
            if (!taskExists)
            {
                taskQueue.TryCreateTask(
                    WaiterTaskType.TakeOrder,
                    WaiterTaskPriority.Normal,
                    table,
                    null,
                    out _
                );
            }

            return;
        }

        if (taskExists)
        {
            taskQueue.TryCancelTask(task);
        }
    }

    private void SynchronizeDeliverBillTask(
        RestaurantTable table
    )
    {
        bool taskExists =
            taskQueue.TryGetActiveTask(
                WaiterTaskType.DeliverBill,
                table,
                null,
                out WaiterTask task
            );

        if (!manageBillTasks)
        {
            if (taskExists)
            {
                taskQueue.TryCancelTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.Paying)
        {
            if (taskExists)
            {
                taskQueue.TryCompleteTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.WaitingForBill)
        {
            if (!taskExists)
            {
                taskQueue.TryCreateTask(
                    WaiterTaskType.DeliverBill,
                    WaiterTaskPriority.High,
                    table,
                    null,
                    out _
                );
            }

            return;
        }

        if (taskExists)
        {
            taskQueue.TryCancelTask(task);
        }
    }

    private void SynchronizeCleanTableTask(
        RestaurantTable table
    )
    {
        bool taskExists =
            taskQueue.TryGetActiveTask(
                WaiterTaskType.CleanTable,
                table,
                null,
                out WaiterTask task
            );

        if (!manageCleaningTasks)
        {
            if (taskExists)
            {
                taskQueue.TryCancelTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.Free)
        {
            if (taskExists)
            {
                taskQueue.TryCompleteTask(task);
            }

            return;
        }

        if (table.CurrentState ==
            TableState.Dirty)
        {
            if (!taskExists)
            {
                taskQueue.TryCreateTask(
                    WaiterTaskType.CleanTable,
                    WaiterTaskPriority.Low,
                    table,
                    null,
                    out _
                );
            }

            return;
        }

        if (taskExists)
        {
            taskQueue.TryCancelTask(task);
        }
    }

    /// <summary>
    /// Solicita un reparto de tareas para el siguiente frame.
    ///
    /// Esta espera evita asignar nuevas tareas dentro de las propias
    /// cadenas de eventos de RestaurantTable, Waiter o CustomerGroup.
    /// También agrupa varias peticiones producidas durante el mismo frame.
    /// </summary>
    private void RequestDispatch()
    {
        if (!isActiveAndEnabled ||
            taskQueue == null)
        {
            return;
        }

        dispatchRequested = true;

        if (dispatchRoutine != null)
        {
            return;
        }

        dispatchRoutine =
            StartCoroutine(
                DispatchRequestedTasksRoutine()
            );
    }

    /// <summary>
    /// Ejecuta las peticiones de reparto fuera de la pila de eventos
    /// que cambió el estado de una mesa, una comanda o un camarero.
    /// </summary>
    private IEnumerator DispatchRequestedTasksRoutine()
    {
        while (isActiveAndEnabled &&
               dispatchRequested)
        {
            dispatchRequested = false;

            // Se espera un frame completo para que todos los sistemas
            // terminen de procesar el cambio de estado que originó
            // la petición.
            yield return null;

            DispatchPendingTasks();
        }

        dispatchRoutine = null;
    }

    /// <summary>
    /// Distribuye todas las tareas posibles entre todos los
    /// camareros disponibles.
    /// </summary>
    private void DispatchPendingTasks()
    {
        if (!isActiveAndEnabled ||
            isDispatching ||
            taskQueue == null)
        {
            return;
        }

        isDispatching = true;

        try
        {
            while (true)
            {
                WaiterTask task =
                    taskQueue.GetNextPendingTask();

                if (task == null)
                    break;

                if (!IsTaskStillValid(task))
                {
                    CancelTaskAndCleanup(task);
                    continue;
                }

                Waiter waiter =
                    FindClosestAvailableWaiter(task);

                if (waiter == null)
                    break;

                if (!taskQueue.TryAssignTask(
                        task,
                        waiter
                    ))
                {
                    continue;
                }

                bool waiterAcceptedTask =
                    TrySendTaskToWaiter(
                        waiter,
                        task
                    );

                if (!waiterAcceptedTask)
                {
                    taskQueue
                        .TryReleaseTaskAssignment(task);

                    Debug.LogWarning(
                        $"El camarero {waiter.WaiterId} no pudo " +
                        $"aceptar la tarea {task.TaskId}.",
                        this
                    );

                    // Evita repetir indefinidamente la misma
                    // asignación dentro del mismo ciclo.
                    break;
                }

                if (!taskQueue.TryStartTask(task))
                {
                    Debug.LogError(
                        $"La tarea {task.TaskId} fue aceptada, " +
                        "pero no pudo comenzar.",
                        this
                    );

                    break;
                }

                Debug.Log(
                    $"Tarea {task.TaskId} ({task.Type}) asignada " +
                    $"al camarero {waiter.WaiterId}.",
                    this
                );
            }
        }
        finally
        {
            isDispatching = false;
        }
    }

    /// <summary>
    /// Traduce una tarea genérica a la operación concreta
    /// que debe ejecutar el camarero.
    /// </summary>
    private bool TrySendTaskToWaiter(
        Waiter waiter,
        WaiterTask task
    )
    {
        if (waiter == null ||
            task == null)
        {
            return false;
        }

        switch (task.Type)
        {
            case WaiterTaskType.TakeOrder:
                return waiter.AssignTable(
                    task.Table
                );

            case WaiterTaskType.DeliverFood:
                return task.Order != null &&
                       task.Order.CurrentState ==
                           OrderState.ReadyForPickup &&
                       waiter.AssignOrderForPickup(
                           task.Order
                       );

            case WaiterTaskType.DeliverBill:
                return waiter.AssignTableForBill(
                    task.Table
                );

            case WaiterTaskType.CleanTable:
                return waiter.AssignTableForCleaning(
                    task.Table
                );

            default:
                return false;
        }
    }

    /// <summary>
    /// Comprueba que la necesidad operativa de la tarea
    /// siga existiendo antes de asignarla.
    /// </summary>
    private bool IsTaskStillValid(
        WaiterTask task
    )
    {
        if (task == null ||
            task.Table == null)
        {
            return false;
        }

        switch (task.Type)
        {
            case WaiterTaskType.TakeOrder:
                return manageTakeOrderTasks &&
                       task.Table.CurrentState ==
                           TableState.WaitingForWaiter;

            case WaiterTaskType.DeliverFood:
                return manageFoodDeliveryTasks &&
                       task.Order != null &&
                       task.Order.CurrentState ==
                           OrderState.ReadyForPickup;

            case WaiterTaskType.DeliverBill:
                return manageBillTasks &&
                       task.Table.CurrentState ==
                           TableState.WaitingForBill;

            case WaiterTaskType.CleanTable:
                return manageCleaningTasks &&
                       task.Table.CurrentState ==
                           TableState.Dirty;

            default:
                return false;
        }
    }

    /// <summary>
    /// Selecciona el camarero disponible más próximo
    /// al destino inicial de la tarea.
    /// </summary>
    private Waiter FindClosestAvailableWaiter(
        WaiterTask task
    )
    {
        if (task == null)
            return null;

        Vector3 destinationPosition =
            GetTaskDestinationPosition(task);

        Waiter closestWaiter = null;

        float shortestDistanceSquared =
            float.MaxValue;

        foreach (Waiter waiter
                 in registeredWaiters)
        {
            if (waiter == null ||
                !waiter.IsAvailable)
            {
                continue;
            }

            float distanceSquared =
                (
                    waiter.transform.position -
                    destinationPosition
                ).sqrMagnitude;

            bool isCloser =
                distanceSquared <
                shortestDistanceSquared;

            // El ID se usa únicamente como desempate determinista.
            bool sameDistanceButLowerId =
                Mathf.Approximately(
                    distanceSquared,
                    shortestDistanceSquared
                ) &&
                closestWaiter != null &&
                waiter.WaiterId <
                closestWaiter.WaiterId;

            if (!isCloser &&
                !sameDistanceButLowerId)
            {
                continue;
            }

            shortestDistanceSquared =
                distanceSquared;

            closestWaiter = waiter;
        }

        return closestWaiter;
    }

    /// <summary>
    /// Obtiene el destino inicial según el tipo de tarea.
    ///
    /// El reparto de comida comienza en el punto de recogida
    /// de la cocina. El resto comienza en la mesa.
    /// </summary>
    private Vector3 GetTaskDestinationPosition(
        WaiterTask task
    )
    {
        if (task.Type ==
                WaiterTaskType.DeliverFood &&
            task.Order != null &&
            kitchenByOrder.TryGetValue(
                task.Order,
                out KitchenSystem kitchenSystem
            ) &&
            kitchenSystem != null &&
            kitchenSystem.PickupPoint != null)
        {
            return kitchenSystem
                .PickupPoint
                .position;
        }

        if (task.Table != null &&
            task.Table.WaiterServicePoint != null)
        {
            return task.Table
                .WaiterServicePoint
                .position;
        }

        if (task.Table != null)
        {
            return task.Table
                .transform
                .position;
        }

        return transform.position;
    }

    /// <summary>
    /// Localiza una tarea de reparto mediante la comanda
    /// o mediante el camarero que la tiene asignada.
    /// </summary>
    private WaiterTask FindFoodDeliveryTask(
        Waiter waiter,
        RestaurantOrder order
    )
    {
        if (order != null &&
            order.Table != null &&
            taskQueue.TryGetActiveTask(
                WaiterTaskType.DeliverFood,
                order.Table,
                order,
                out WaiterTask taskByOrder
            ))
        {
            return taskByOrder;
        }

        if (waiter == null)
            return null;

        IReadOnlyList<WaiterTask> tasks =
            taskQueue.ActiveTasks;

        for (int index = 0;
             index < tasks.Count;
             index++)
        {
            WaiterTask task =
                tasks[index];

            if (task == null ||
                task.Type !=
                    WaiterTaskType.DeliverFood)
            {
                continue;
            }

            if (ReferenceEquals(
                    task.AssignedWaiter,
                    waiter
                ))
            {
                return task;
            }
        }

        return null;
    }

    /// <summary>
    /// Recupera las tareas asignadas a un camarero
    /// que abandona el registro.
    /// </summary>
    private void RecoverTasksAssignedToWaiter(
        Waiter waiter
    )
    {
        if (waiter == null ||
            taskQueue == null)
        {
            return;
        }

        IReadOnlyList<WaiterTask> tasks =
            taskQueue.ActiveTasks;

        for (int index = tasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                tasks[index];

            if (task == null ||
                !ReferenceEquals(
                    task.AssignedWaiter,
                    waiter
                ))
            {
                continue;
            }

            if (task.State ==
                WaiterTaskState.Assigned)
            {
                taskQueue
                    .TryReleaseTaskAssignment(task);

                continue;
            }

            RestaurantTable affectedTable =
                task.Table;

            RestaurantOrder affectedOrder =
                task.Order;

            WaiterTaskType affectedType =
                task.Type;

            taskQueue.TryCancelTask(task);

            if (affectedType ==
                    WaiterTaskType.DeliverFood &&
                affectedOrder != null &&
                affectedOrder.CurrentState ==
                    OrderState.ReadyForPickup &&
                kitchenByOrder.TryGetValue(
                    affectedOrder,
                    out KitchenSystem kitchenSystem
                ) &&
                kitchenSystem != null)
            {
                CreateFoodDeliveryTask(
                    kitchenSystem,
                    affectedOrder
                );
            }
            else
            {
                SynchronizeTableTasks(
                    affectedTable
                );
            }
        }
    }

    /// <summary>
    /// Cancela una tarea y limpia sus datos auxiliares.
    /// </summary>
    private void CancelTaskAndCleanup(
        WaiterTask task
    )
    {
        if (task == null)
            return;

        RestaurantOrder order =
            task.Order;

        taskQueue.TryCancelTask(task);

        if (task.Type ==
                WaiterTaskType.DeliverFood &&
            order != null)
        {
            kitchenByOrder.Remove(order);
        }
    }

    /// <summary>
    /// Cancela todas las tareas relacionadas con una mesa.
    /// </summary>
    private void CancelTasksForTable(
        RestaurantTable table
    )
    {
        if (table == null ||
            taskQueue == null)
        {
            return;
        }

        IReadOnlyList<WaiterTask> tasks =
            taskQueue.ActiveTasks;

        for (int index = tasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                tasks[index];

            if (task == null ||
                !ReferenceEquals(
                    task.Table,
                    table
                ))
            {
                continue;
            }

            CancelTaskAndCleanup(task);
        }
    }

    /// <summary>
    /// Cancela todas las tareas de reparto asociadas
    /// a una cocina determinada.
    /// </summary>
    private void CancelFoodTasksForKitchen(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null ||
            taskQueue == null)
        {
            return;
        }

        IReadOnlyList<WaiterTask> tasks =
            taskQueue.ActiveTasks;

        for (int index = tasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                tasks[index];

            if (task == null ||
                task.Type !=
                    WaiterTaskType.DeliverFood ||
                task.Order == null)
            {
                continue;
            }

            if (!kitchenByOrder.TryGetValue(
                    task.Order,
                    out KitchenSystem sourceKitchen
                ))
            {
                continue;
            }

            if (!ReferenceEquals(
                    sourceKitchen,
                    kitchenSystem
                ))
            {
                continue;
            }

            CancelTaskAndCleanup(task);
        }
    }

    private void ValidateRuntimeConfiguration()
    {
        if (registeredWaiters.Count == 0)
        {
            Debug.LogWarning(
                "WaiterTaskCoordinator no ha encontrado camareros.",
                this
            );
        }

        if (manageFoodDeliveryTasks &&
            registeredKitchens.Count == 0)
        {
            Debug.LogWarning(
                "WaiterTaskCoordinator gestiona repartos, " +
                "pero no ha encontrado ninguna cocina.",
                this
            );
        }
    }
}
