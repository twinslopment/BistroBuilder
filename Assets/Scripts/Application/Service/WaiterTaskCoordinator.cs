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
    public const string RuntimeRevision = "367H";
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

    [Header("Ejecución individual 367D")]

    [Tooltip(
        "Autoridad de ejecución de líneas usada para reservar, reintentar y " +
        "validar cada plato físico."
    )]
    [SerializeField]
    private BistroBuilderOrderLineExecutionService lineExecutionService;

    [Header("Rondas inteligentes 367G")]

    [Tooltip(
        "Agrupa líneas ya preparadas de una misma cocina en una sola " +
        "recogida y una ruta por varias mesas."
    )]
    [SerializeField]
    private bool enableMultiTableDeliveryRuns = true;

    [Tooltip(
        "Límite global de platos por ronda. La capacidad individual del " +
        "camarero puede reducir este valor."
    )]
    [SerializeField, Min(1)]
    private int maxDeliveryRunSize = 3;

    [Tooltip(
        "Prioriza grupos de líneas que permiten completar una mesa con la " +
        "capacidad restante."
    )]
    [SerializeField]
    private bool preferCompletingTables = true;

    [Tooltip(
        "Agrupa en una misma ronda únicamente comandas cuyo camarero " +
        "responsable es el mismo. Un apoyo puede ejecutar la ronda si el " +
        "responsable no está disponible, pero no mezcla responsabilidades."
    )]
    [SerializeField]
    private bool restrictRunsToSameResponsibleWaiter = true;

    [Tooltip(
        "Tiempo real máximo que una primera línea preparada permanece en " +
        "consolidación antes de asignarse. Permite incorporar platos que " +
        "terminan casi simultáneamente sin esperar a platos futuros."
    )]
    [SerializeField, Min(0f)]
    private float deliveryRunConsolidationSeconds = 0.8f;

    [SerializeField]
    private bool logDeliveryRuns = true;

    private int nextDeliveryRunId = 1;

    // Estado temporal del diagnóstico funcional 367G1. No se serializa ni
    // afecta a partidas normales. Solo puede armarse explícitamente desde la
    // ventana de Editor incluida en el hito.
    private bool diagnosticHoldEnabled;
    private bool diagnosticReleased;
    private int diagnosticTargetLineCount;
    private int diagnosticTargetTableCount;
    private float diagnosticDeadlineRealtime;
    private string diagnosticStatus = string.Empty;
    private BistroBuilderDeliveryRun diagnosticObservedRun;

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
    /// Manejador de línea lista asociado a cada cocina.
    /// Se conserva la cocina en el cierre para no depender de búsquedas.
    /// </summary>
    private readonly Dictionary<
        KitchenSystem,
        Action<BistroBuilderOrderLineReadyEvent>
    > kitchenLineReadyHandlers =
        new Dictionary<
            KitchenSystem,
            Action<BistroBuilderOrderLineReadyEvent>
        >();

    /// <summary>
    /// Relaciona cada LineId que está en pase o reparto con la cocina donde
    /// debe recogerse. La clave canónica permite varias líneas de una misma
    /// comanda sin colisiones.
    /// </summary>
    private readonly Dictionary<string, KitchenSystem>
        kitchenByOrderLineId =
            new Dictionary<string, KitchenSystem>(StringComparer.Ordinal);

    /// <summary>
    /// Instante real en el que cada línea preparada entró en consolidación.
    /// Se usa tiempo no escalado para que la velocidad de simulación no
    /// convierta una ventana operativa breve en una espera impredecible.
    /// </summary>
    private readonly Dictionary<string, float>
        deliveryTaskReadyRealtimeByLineId =
            new Dictionary<string, float>(StringComparer.Ordinal);

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
    /// Despierta el coordinador cuando madura la siguiente línea retenida por
    /// la ventana de consolidación.
    /// </summary>
    private Coroutine deferredDeliveryDispatchRoutine;
    private float deferredDeliveryDispatchAtRealtime = float.PositiveInfinity;

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

    public bool ManagesFoodDeliveryTasks => manageFoodDeliveryTasks;
    public bool MultiTableDeliveryRunsEnabled =>
        enableMultiTableDeliveryRuns;
    public int MaximumDeliveryRunSize => Mathf.Max(1, maxDeliveryRunSize);
    public bool PreferCompletingTables => preferCompletingTables;
    public bool RestrictsRunsToSameResponsibleWaiter =>
        restrictRunsToSameResponsibleWaiter;
    public float DeliveryRunConsolidationSeconds =>
        Mathf.Max(0f, deliveryRunConsolidationSeconds);
    public bool IsFunctionalDiagnosticArmed => diagnosticHoldEnabled;
    public bool IsFunctionalDiagnosticReleased => diagnosticReleased;
    public string FunctionalDiagnosticStatus => diagnosticStatus;
    public BistroBuilderDeliveryRun FunctionalDiagnosticRun =>
        diagnosticObservedRun;
    public BistroBuilderOrderLineExecutionService LineExecutionService =>
        lineExecutionService;

    /// <summary>
    /// Cálculo puro utilizado por runtime y autotest. Devuelve los segundos
    /// no escalados que aún faltan para que una línea ancla pueda salir.
    /// </summary>
    public static float CalculateConsolidationRemainingSeconds(
        float readyAtRealtime,
        float currentRealtime,
        float consolidationSeconds
    )
    {
        if (float.IsNaN(readyAtRealtime) ||
            float.IsInfinity(readyAtRealtime) ||
            float.IsNaN(currentRealtime) ||
            float.IsInfinity(currentRealtime) ||
            float.IsNaN(consolidationSeconds) ||
            float.IsInfinity(consolidationSeconds))
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            Mathf.Max(0f, consolidationSeconds) -
            Mathf.Max(0f, currentRealtime - readyAtRealtime)
        );
    }

    private void Awake()
    {
        EnsureTaskQueueCreated();
        ResolveLineExecutionService();
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

        if (deferredDeliveryDispatchRoutine != null)
        {
            StopCoroutine(deferredDeliveryDispatchRoutine);
            deferredDeliveryDispatchRoutine = null;
        }

        deferredDeliveryDispatchAtRealtime = float.PositiveInfinity;
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

        kitchenLineReadyHandlers.Clear();
        kitchenByOrderLineId.Clear();
        deliveryTaskReadyRealtimeByLineId.Clear();
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

        Action<BistroBuilderOrderLineReadyEvent> handler =
            readyEvent =>
                HandleOrderLineReady(
                    kitchenSystem,
                    readyEvent
                );

        kitchenLineReadyHandlers.Add(
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

        // Se retira primero del registro para impedir que la recuperación
        // de una ronda vuelva a crear tareas contra una cocina eliminada.
        registeredKitchens.Remove(
            kitchenSystem
        );

        CancelFoodTasksForKitchen(kitchenSystem);

        kitchenLineReadyHandlers.Remove(
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
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (order == null ||
            !order.HasValidDestination ||
            taskQueue == null)
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            return false;
        }

        bool found = taskQueue.TryGetActiveTask(
            WaiterTaskType.DeliverFood,
            order.Table,
            order,
            normalizedLineId,
            out WaiterTask task
        );

        if (!found)
            return false;

        bool completed = taskQueue.TryCompleteTask(task);

        if (completed)
        {
            kitchenByOrderLineId.Remove(normalizedLineId);
            deliveryTaskReadyRealtimeByLineId.Remove(normalizedLineId);
        }

        return completed;
    }

    /// <summary>
    /// Compatibilidad con herramientas antiguas: solo es inequívoco cuando la
    /// comanda tiene exactamente una tarea de reparto activa.
    /// </summary>
    public bool TryCompleteFoodDeliveryTask(RestaurantOrder order)
    {
        WaiterTask task = FindFoodDeliveryTask(null, order, string.Empty);
        return task != null &&
               TryCompleteFoodDeliveryTask(order, task.OrderLineId);
    }

    /// <summary>
    /// Recupera una tarea de reparto individual que no pudo terminar.
    /// La línea debe haberse devuelto antes al pase por la autoridad 367D.
    /// </summary>
    public bool ReportFoodDeliveryFailure(
        Waiter waiter,
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (taskQueue == null)
            return false;

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        WaiterTask task = FindFoodDeliveryTask(
            waiter,
            order,
            normalizedLineId
        );

        if (task == null)
            return false;

        RestaurantOrder affectedOrder = task.Order;
        string affectedLineId = task.OrderLineId;
        Waiter assignedWaiter = task.AssignedWaiter ?? waiter;

        if (affectedOrder != null &&
            BistroBuilderOrderIdUtility.IsValid(affectedLineId) &&
            lineExecutionService != null)
        {
            string actorReference = waiter != null
                ? BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(waiter.WaiterId)
                : "delivery_failure_recovery";

            lineExecutionService.TryReturnLineToPickup(
                affectedOrder,
                affectedLineId,
                actorReference,
                out _
            );
        }

        bool cancelled = taskQueue.TryCancelTask(task);

        if (!cancelled)
            return false;

        if (assignedWaiter != null &&
            ReferenceEquals(assignedWaiter.AssignedOrder, affectedOrder) &&
            string.Equals(
                assignedWaiter.AssignedOrderLineId,
                affectedLineId,
                StringComparison.Ordinal
            ))
        {
            assignedWaiter.ClearAssignment();
        }

        KitchenSystem sourceKitchen = null;

        bool lineCanBeRetried =
            affectedOrder != null &&
            IsOrderDestinationOperational(affectedOrder) &&
            BistroBuilderOrderIdUtility.IsValid(affectedLineId) &&
            lineExecutionService != null &&
            lineExecutionService.IsLineReadyForPickup(
                affectedOrder,
                affectedLineId
            ) &&
            kitchenByOrderLineId.TryGetValue(
                affectedLineId,
                out sourceKitchen
            ) &&
            sourceKitchen != null &&
            registeredKitchens.Contains(sourceKitchen);

        if (lineCanBeRetried)
        {
            CreateFoodDeliveryTask(
                sourceKitchen,
                affectedOrder,
                affectedLineId
            );
        }
        else if (BistroBuilderOrderIdUtility.IsValid(affectedLineId))
        {
            kitchenByOrderLineId.Remove(affectedLineId);
        }

        RequestDispatch();
        return true;
    }

    public bool ReportFoodDeliveryFailure(
        Waiter waiter,
        RestaurantOrder order
    )
    {
        string lineId = waiter != null
            ? waiter.AssignedOrderLineId
            : string.Empty;

        WaiterTask task = FindFoodDeliveryTask(waiter, order, lineId);
        return task != null && ReportFoodDeliveryFailure(
            waiter,
            order,
            task.OrderLineId
        );
    }

    /// <summary>
    /// Recupera de forma unitaria todas las líneas no servidas de una ronda.
    /// Las líneas ya entregadas permanecen irreversibles y no se duplican.
    /// </summary>
    public bool ReportFoodDeliveryRunFailure(
        Waiter waiter,
        BistroBuilderDeliveryRun deliveryRun
    )
    {
        if (deliveryRun == null || taskQueue == null)
            return false;

        bool handledAnyLine = false;
        string actorReference = waiter != null
            ? BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(waiter.WaiterId)
            : "delivery_run_failure_recovery";

        for (int index = 0; index < deliveryRun.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            if (item.State == BistroBuilderDeliveryRunItemState.Served)
                continue;

            handledAnyLine = true;

            if (lineExecutionService != null)
            {
                lineExecutionService.TryReturnLineToPickup(
                    item.Order,
                    item.OrderLineId,
                    actorReference,
                    out _
                );
            }

            WaiterTask task = item.Task;

            if (task != null)
            {
                taskQueue.TryCancelTask(task);
            }

            bool canRetry =
                item.Order != null &&
                IsOrderDestinationOperational(item.Order) &&
                lineExecutionService != null &&
                lineExecutionService.IsLineReadyForPickup(
                    item.Order,
                    item.OrderLineId
                ) &&
                deliveryRun.SourceKitchen != null &&
                registeredKitchens.Contains(deliveryRun.SourceKitchen);

            if (canRetry)
            {
                CreateFoodDeliveryTask(
                    deliveryRun.SourceKitchen,
                    item.Order,
                    item.OrderLineId
                );
            }
            else
            {
                kitchenByOrderLineId.Remove(item.OrderLineId);
            }
        }

        deliveryRun.TryCancel();

        if (waiter != null &&
            ReferenceEquals(waiter.AssignedDeliveryRun, deliveryRun))
        {
            waiter.ClearAssignment();
        }

        RequestDispatch();
        return handledAnyLine;
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

        if (!kitchenLineReadyHandlers.TryGetValue(
                kitchenSystem,
                out Action<BistroBuilderOrderLineReadyEvent> handler
            ))
        {
            return;
        }

        kitchenSystem.OrderLineReady -= handler;
        kitchenSystem.OrderLineReady += handler;
    }

    private void UnsubscribeFromKitchen(
        KitchenSystem kitchenSystem
    )
    {
        if (kitchenSystem == null)
            return;

        if (!kitchenLineReadyHandlers.TryGetValue(
                kitchenSystem,
                out Action<BistroBuilderOrderLineReadyEvent> handler
            ))
        {
            return;
        }

        kitchenSystem.OrderLineReady -= handler;
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

    private void HandleOrderLineReady(
        KitchenSystem kitchenSystem,
        BistroBuilderOrderLineReadyEvent readyEvent
    )
    {
        if (!manageFoodDeliveryTasks)
            return;

        RestaurantOrder order = readyEvent.Order;
        string orderLineId = readyEvent.OrderLineId;

        if (kitchenSystem == null ||
            order == null ||
            !BistroBuilderOrderIdUtility.IsValid(orderLineId))
        {
            return;
        }

        if (!order.HasValidDestination)
        {
            Debug.LogError(
                $"La comanda {order.OrderId} no tiene destino operativo.",
                this
            );
            return;
        }

        if (lineExecutionService == null ||
            !lineExecutionService.IsLineReadyForPickup(order, orderLineId))
        {
            return;
        }

        CreateFoodDeliveryTask(
            kitchenSystem,
            order,
            orderLineId
        );

        RequestDispatch();
    }

    /// <summary>
    /// Crea una tarea por plato físico. El índice de WaiterTaskQueue evita
    /// duplicar el mismo LineId y permite varias líneas de una sola comanda.
    /// </summary>
    private bool CreateFoodDeliveryTask(
        KitchenSystem kitchenSystem,
        RestaurantOrder order,
        string orderLineId
    )
    {
        if (kitchenSystem == null ||
            order == null ||
            !order.HasValidDestination)
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            return false;
        }

        bool created = taskQueue.TryCreateTask(
            WaiterTaskType.DeliverFood,
            WaiterTaskPriority.Urgent,
            order.Table,
            order,
            normalizedLineId,
            out WaiterTask task
        );

        if (!created && task == null)
        {
            kitchenByOrderLineId.Remove(normalizedLineId);
            return false;
        }

        kitchenByOrderLineId[normalizedLineId] = kitchenSystem;

        if (created ||
            !deliveryTaskReadyRealtimeByLineId.ContainsKey(normalizedLineId))
        {
            deliveryTaskReadyRealtimeByLineId[normalizedLineId] =
                Time.unscaledTime;
        }

        if (created)
        {
            Debug.Log(
                $"Tarea {task.TaskId} creada para repartir la línea " +
                $"{normalizedLineId} de la comanda {order.OrderId}.",
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
                WaiterTask task = GetNextDispatchableTask(
                    out float deferredSeconds
                );

                if (task == null)
                {
                    if (deferredSeconds >= 0f)
                    {
                        ScheduleDeferredDeliveryDispatch(deferredSeconds);
                    }

                    break;
                }

                if (!IsTaskStillValid(task))
                {
                    CancelTaskAndCleanup(task);
                    continue;
                }

                Waiter waiter =
                    FindBestAvailableWaiter(task);

                if (waiter == null)
                    break;

                if (task.Type == WaiterTaskType.DeliverFood &&
                    enableMultiTableDeliveryRuns)
                {
                    if (TryDispatchDeliveryRun(waiter, task))
                    {
                        continue;
                    }

                    // Evita repetir indefinidamente el mismo lote dentro del
                    // ciclo. Una nueva señal de estado volverá a intentarlo.
                    break;
                }

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
                    RollbackAcceptedTask(waiter, task);

                    Debug.LogError(
                        $"La tarea {task.TaskId} fue aceptada, " +
                        "pero no pudo comenzar. La asignación se revirtió.",
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
    /// Devuelve la tarea pendiente de mayor prioridad que ya puede salir.
    /// Las líneas de comida todavía jóvenes se omiten sin bloquear tareas de
    /// otros tipos y se programa un despertar exacto para su maduración.
    /// </summary>
    private WaiterTask GetNextDispatchableTask(out float deferredSeconds)
    {
        deferredSeconds = -1f;
        WaiterTask selectedTask = null;
        IReadOnlyList<WaiterTask> activeTasks = taskQueue.ActiveTasks;
        float now = Time.unscaledTime;

        for (int index = 0; index < activeTasks.Count; index++)
        {
            WaiterTask candidate = activeTasks[index];

            if (candidate == null || !candidate.IsPending)
                continue;

            // Una tarea inválida debe salir inmediatamente para que el bucle
            // principal la cancele; nunca se retiene por consolidación.
            if (!IsTaskStillValid(candidate))
                return candidate;

            if (candidate.Type == WaiterTaskType.DeliverFood &&
                enableMultiTableDeliveryRuns)
            {
                float remaining = GetDeliveryDispatchRemainingSeconds(
                    candidate,
                    now
                );

                if (remaining > 0f)
                {
                    if (deferredSeconds < 0f || remaining < deferredSeconds)
                        deferredSeconds = remaining;

                    continue;
                }
            }

            if (selectedTask == null ||
                candidate.Priority > selectedTask.Priority ||
                (candidate.Priority == selectedTask.Priority &&
                 candidate.CreationSequence <
                    selectedTask.CreationSequence))
            {
                selectedTask = candidate;
            }
        }

        return selectedTask;
    }

    private float GetDeliveryDispatchRemainingSeconds(
        WaiterTask task,
        float now
    )
    {
        if (task == null || task.Type != WaiterTaskType.DeliverFood)
            return 0f;

        if (diagnosticHoldEnabled && !diagnosticReleased)
        {
            if (HasReachedFunctionalDiagnosticTarget())
            {
                diagnosticReleased = true;
                diagnosticStatus =
                    "Objetivo alcanzado. Se libera el lote determinista.";
                CancelDeferredDeliveryDispatchWake();
                Debug.Log(
                    "367G1 diagnóstico: objetivo de " +
                    diagnosticTargetLineCount + " plato(s) y " +
                    diagnosticTargetTableCount +
                    " mesa(s) alcanzado. Se libera el reparto.",
                    this
                );
            }
            else if (now < diagnosticDeadlineRealtime)
            {
                return Mathf.Max(
                    0.05f,
                    diagnosticDeadlineRealtime - now
                );
            }
            else
            {
                diagnosticReleased = true;
                diagnosticStatus =
                    "Tiempo agotado antes de alcanzar el objetivo; " +
                    "se libera el reparto para no bloquear el servicio.";
                Debug.LogWarning(
                    "367G1 diagnóstico: tiempo agotado antes de reunir el " +
                    "lote objetivo. El reparto se libera sin bloquear.",
                    this
                );
            }
        }

        if (!deliveryTaskReadyRealtimeByLineId.TryGetValue(
                task.OrderLineId,
                out float readyAt
            ))
        {
            deliveryTaskReadyRealtimeByLineId[task.OrderLineId] = now;
            readyAt = now;
        }

        return CalculateConsolidationRemainingSeconds(
            readyAt,
            now,
            DeliveryRunConsolidationSeconds
        );
    }

    private bool HasReachedFunctionalDiagnosticTarget()
    {
        if (!diagnosticHoldEnabled || taskQueue == null)
            return false;

        int lineCount = 0;
        HashSet<string> destinations =
            new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<WaiterTask> activeTasks = taskQueue.ActiveTasks;

        for (int index = 0; index < activeTasks.Count; index++)
        {
            WaiterTask task = activeTasks[index];

            if (task == null ||
                task.Type != WaiterTaskType.DeliverFood ||
                !task.CanBeAssigned ||
                !IsTaskStillValid(task))
            {
                continue;
            }

            lineCount++;
            if (task.HasValidDestination)
                destinations.Add(task.DestinationReferenceId);
        }

        diagnosticStatus =
            "Esperando lote: " + lineCount + "/" +
            diagnosticTargetLineCount + " plato(s), " +
            destinations.Count + "/" + diagnosticTargetTableCount +
            " destino(s).";

        return lineCount >= diagnosticTargetLineCount &&
               destinations.Count >= diagnosticTargetTableCount;
    }

    private void ScheduleDeferredDeliveryDispatch(float delaySeconds)
    {
        float safeDelay = Mathf.Max(0.01f, delaySeconds);
        float requestedAt = Time.unscaledTime + safeDelay;

        if (deferredDeliveryDispatchRoutine != null &&
            requestedAt >= deferredDeliveryDispatchAtRealtime - 0.001f)
        {
            return;
        }

        if (deferredDeliveryDispatchRoutine != null)
            StopCoroutine(deferredDeliveryDispatchRoutine);

        deferredDeliveryDispatchAtRealtime = requestedAt;
        deferredDeliveryDispatchRoutine = StartCoroutine(
            DeferredDeliveryDispatchRoutine()
        );
    }

    private void CancelDeferredDeliveryDispatchWake()
    {
        if (deferredDeliveryDispatchRoutine != null)
        {
            StopCoroutine(deferredDeliveryDispatchRoutine);
            deferredDeliveryDispatchRoutine = null;
        }

        deferredDeliveryDispatchAtRealtime = float.PositiveInfinity;
    }

    private IEnumerator DeferredDeliveryDispatchRoutine()
    {
        while (isActiveAndEnabled)
        {
            float remaining =
                deferredDeliveryDispatchAtRealtime - Time.unscaledTime;

            if (remaining <= 0f)
                break;

            yield return null;
        }

        deferredDeliveryDispatchRoutine = null;
        deferredDeliveryDispatchAtRealtime = float.PositiveInfinity;

        if (isActiveAndEnabled)
            RequestDispatch();
    }

    /// <summary>
    /// Arma una retención diagnóstica de uso exclusivo en Play Mode. El lote
    /// se libera al reunir el número solicitado de líneas y mesas o al vencer
    /// el tiempo de seguridad. Nunca queda persistido en la escena.
    /// </summary>
    public bool TryArmFunctionalDeliveryDiagnostic(
        int targetLineCount,
        int targetTableCount,
        float timeoutSeconds,
        out string error
    )
    {
        if (!Application.isPlaying)
        {
            error = "La prueba funcional solo puede armarse en Play Mode.";
            return false;
        }

        if (targetLineCount < 2 || targetTableCount < 2 ||
            targetTableCount > targetLineCount ||
            float.IsNaN(timeoutSeconds) ||
            float.IsInfinity(timeoutSeconds) ||
            timeoutSeconds < 1f)
        {
            error = "Los objetivos del diagnóstico no son válidos.";
            return false;
        }

        diagnosticHoldEnabled = true;
        diagnosticReleased = false;
        diagnosticTargetLineCount = targetLineCount;
        diagnosticTargetTableCount = targetTableCount;
        diagnosticDeadlineRealtime = Time.unscaledTime + timeoutSeconds;
        diagnosticObservedRun = null;
        diagnosticStatus =
            "Diagnóstico armado. Esperando platos preparados.";
        error = string.Empty;
        RequestDispatch();
        return true;
    }

    public void CancelFunctionalDeliveryDiagnostic()
    {
        diagnosticHoldEnabled = false;
        diagnosticReleased = false;
        diagnosticObservedRun = null;
        diagnosticStatus = "Diagnóstico cancelado.";
        CancelDeferredDeliveryDispatchWake();
        RequestDispatch();
    }

    /// <summary>
    /// Construye, reserva y arranca una ronda multimesa como una sola
    /// transacción operativa.
    /// </summary>
    private bool TryDispatchDeliveryRun(
        Waiter waiter,
        WaiterTask anchorTask
    )
    {
        if (waiter == null ||
            anchorTask == null ||
            anchorTask.Type != WaiterTaskType.DeliverFood ||
            lineExecutionService == null ||
            !waiter.IsAvailable ||
            !IsTaskStillValid(anchorTask) ||
            !TryGetTaskKitchen(anchorTask, out KitchenSystem sourceKitchen))
        {
            return false;
        }

        int capacity = Mathf.Max(
            1,
            Mathf.Min(
                MaximumDeliveryRunSize,
                waiter.FoodDeliveryCapacity
            )
        );

        List<WaiterTask> orderedTasks = BuildDeliveryRunTasks(
            anchorTask,
            sourceKitchen,
            capacity
        );

        if (orderedTasks.Count == 0)
            return false;

        List<WaiterTask> assignedTasks =
            new List<WaiterTask>(orderedTasks.Count);

        for (int index = 0; index < orderedTasks.Count; index++)
        {
            WaiterTask task = orderedTasks[index];

            if (!taskQueue.TryAssignTask(task, waiter))
            {
                ReleaseAssignedDeliveryTasks(assignedTasks);
                return false;
            }

            assignedTasks.Add(task);
        }

        BistroBuilderDeliveryRun deliveryRun;

        try
        {
            deliveryRun = new BistroBuilderDeliveryRun(
                GetNextDeliveryRunId(),
                sourceKitchen,
                capacity,
                orderedTasks
            );
        }
        catch (Exception exception)
        {
            ReleaseAssignedDeliveryTasks(assignedTasks);
            Debug.LogError(
                "No se pudo construir la ronda 367G. " + exception.Message,
                this
            );
            return false;
        }

        if (!lineExecutionService.TryReserveDeliveryRun(
                deliveryRun,
                waiter,
                out string reservationError
            ))
        {
            ReleaseAssignedDeliveryTasks(assignedTasks);
            Debug.LogWarning(
                "No se pudo reservar la ronda " + deliveryRun.RunId +
                ". " + reservationError,
                this
            );
            return false;
        }

        if (!waiter.AssignDeliveryRun(deliveryRun))
        {
            ReturnDeliveryRunLinesToPickup(
                deliveryRun,
                "delivery_run_waiter_rejection"
            );
            ReleaseAssignedDeliveryTasks(assignedTasks);
            return false;
        }

        for (int index = 0; index < assignedTasks.Count; index++)
        {
            if (taskQueue.TryStartTask(assignedTasks[index]))
                continue;

            RollbackDeliveryRunDispatch(waiter, deliveryRun, assignedTasks);

            Debug.LogError(
                "La ronda " + deliveryRun.RunId +
                " fue reservada, pero una tarea no pudo comenzar.",
                this
            );
            return false;
        }

        if (diagnosticReleased &&
            diagnosticObservedRun == null &&
            deliveryRun.Items.Count >= diagnosticTargetLineCount &&
            deliveryRun.Stops.Count >= diagnosticTargetTableCount)
        {
            diagnosticObservedRun = deliveryRun;
            diagnosticStatus =
                "Ronda de diagnóstico creada: " + deliveryRun.Items.Count +
                " plato(s), " + deliveryRun.Stops.Count + " destino(s).";
        }

        if (logDeliveryRuns)
        {
            Debug.Log(
                "Ronda 367G1 " + deliveryRun.RunId + " asignada al camarero " +
                waiter.WaiterId + ": " + deliveryRun.Items.Count +
                " plato(s), " + deliveryRun.Stops.Count + " destino(s).",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Selecciona únicamente líneas ya preparadas, de la misma cocina y hasta
    /// la capacidad disponible. Primero completa la mesa ancla y después usa
    /// una ruta de vecino más próximo entre las demás mesas.
    /// </summary>
    private List<WaiterTask> BuildDeliveryRunTasks(
        WaiterTask anchorTask,
        KitchenSystem sourceKitchen,
        int capacity
    )
    {
        List<WaiterTask> result = new List<WaiterTask>(capacity);
        Dictionary<string, List<WaiterTask>> tasksByDestination =
            new Dictionary<string, List<WaiterTask>>(StringComparer.Ordinal);

        IReadOnlyList<WaiterTask> activeTasks = taskQueue.ActiveTasks;

        for (int index = 0; index < activeTasks.Count; index++)
        {
            WaiterTask candidate = activeTasks[index];

            if (candidate == null ||
                candidate.Type != WaiterTaskType.DeliverFood ||
                !candidate.CanBeAssigned ||
                !candidate.HasValidDestination ||
                !IsTaskStillValid(candidate) ||
                !IsDeliveryRunResponsibilityCompatible(anchorTask, candidate) ||
                !TryGetTaskKitchen(candidate, out KitchenSystem kitchen) ||
                !ReferenceEquals(kitchen, sourceKitchen))
            {
                continue;
            }

            string destinationId = candidate.DestinationReferenceId;

            if (!tasksByDestination.TryGetValue(
                    destinationId,
                    out List<WaiterTask> destinationTasks
                ))
            {
                destinationTasks = new List<WaiterTask>();
                tasksByDestination.Add(destinationId, destinationTasks);
            }

            destinationTasks.Add(candidate);
        }

        string anchorDestinationId = anchorTask.DestinationReferenceId;

        if (!tasksByDestination.TryGetValue(
                anchorDestinationId,
                out List<WaiterTask> anchorDestinationTasks
            ))
        {
            return result;
        }

        SortDeliveryTasks(anchorDestinationTasks);
        AddTasksWithinCapacity(result, anchorDestinationTasks, capacity);
        tasksByDestination.Remove(anchorDestinationId);

        Vector3 currentPosition = GetServicePosition(anchorTask);

        while (result.Count < capacity && tasksByDestination.Count > 0)
        {
            string selectedDestinationId = null;
            List<WaiterTask> selectedTasks = null;
            float shortestDistanceSquared = float.MaxValue;
            long oldestSequence = long.MaxValue;
            bool selectedCompletesDestination = false;
            int remainingCapacity = capacity - result.Count;

            foreach (KeyValuePair<string, List<WaiterTask>> pair
                     in tasksByDestination)
            {
                List<WaiterTask> destinationTasks = pair.Value;

                if (destinationTasks == null || destinationTasks.Count == 0)
                {
                    continue;
                }

                SortDeliveryTasks(destinationTasks);
                WaiterTask destinationAnchor = destinationTasks[0];

                bool completesDestination =
                    destinationTasks.Count <= remainingCapacity;
                float distanceSquared =
                    (GetServicePosition(destinationAnchor) - currentPosition)
                    .sqrMagnitude;
                long candidateOldestSequence =
                    destinationAnchor.CreationSequence;

                bool completionWins = preferCompletingTables &&
                    completesDestination && !selectedCompletesDestination;
                bool completionLoses = preferCompletingTables &&
                    !completesDestination && selectedCompletesDestination;

                if (completionLoses)
                {
                    continue;
                }

                bool closer = distanceSquared < shortestDistanceSquared;
                bool sameDistanceOlder = Mathf.Approximately(
                        distanceSquared,
                        shortestDistanceSquared
                    ) && candidateOldestSequence < oldestSequence;
                bool stableDestinationTie = Mathf.Approximately(
                        distanceSquared,
                        shortestDistanceSquared
                    ) && candidateOldestSequence == oldestSequence &&
                    selectedDestinationId != null &&
                    string.CompareOrdinal(
                        pair.Key,
                        selectedDestinationId
                    ) < 0;

                if (selectedDestinationId == null ||
                    completionWins ||
                    (!completionLoses &&
                     (closer || sameDistanceOlder || stableDestinationTie)))
                {
                    selectedDestinationId = pair.Key;
                    selectedTasks = destinationTasks;
                    shortestDistanceSquared = distanceSquared;
                    oldestSequence = candidateOldestSequence;
                    selectedCompletesDestination = completesDestination;
                }
            }

            if (selectedDestinationId == null || selectedTasks == null)
            {
                break;
            }

            AddTasksWithinCapacity(result, selectedTasks, capacity);
            currentPosition = GetServicePosition(selectedTasks[0]);
            tasksByDestination.Remove(selectedDestinationId);
        }

        return result;
    }

    private bool IsDeliveryRunResponsibilityCompatible(
        WaiterTask anchorTask,
        WaiterTask candidateTask
    )
    {
        if (!restrictRunsToSameResponsibleWaiter)
            return true;

        Waiter anchorResponsible = anchorTask != null &&
            anchorTask.Order != null
                ? anchorTask.Order.AssignedWaiter
                : null;
        Waiter candidateResponsible = candidateTask != null &&
            candidateTask.Order != null
                ? candidateTask.Order.AssignedWaiter
                : null;

        return ReferenceEquals(anchorResponsible, candidateResponsible);
    }

    private static void SortDeliveryTasks(List<WaiterTask> tasks)
    {
        tasks.Sort((left, right) =>
        {
            int priorityComparison = right.Priority.CompareTo(left.Priority);

            if (priorityComparison != 0)
                return priorityComparison;

            int sequenceComparison =
                left.CreationSequence.CompareTo(right.CreationSequence);

            return sequenceComparison != 0
                ? sequenceComparison
                : left.TaskId.CompareTo(right.TaskId);
        });
    }

    private static void AddTasksWithinCapacity(
        List<WaiterTask> destination,
        List<WaiterTask> source,
        int capacity
    )
    {
        for (int index = 0;
             index < source.Count && destination.Count < capacity;
             index++)
        {
            destination.Add(source[index]);
        }
    }

    private bool TryGetTaskKitchen(
        WaiterTask task,
        out KitchenSystem kitchenSystem
    )
    {
        kitchenSystem = null;

        return task != null &&
               BistroBuilderOrderIdUtility.IsValid(task.OrderLineId) &&
               kitchenByOrderLineId.TryGetValue(
                   task.OrderLineId,
                   out kitchenSystem
               ) &&
               kitchenSystem != null;
    }

    private static Vector3 GetServicePosition(WaiterTask task)
    {
        if (task == null)
        {
            return Vector3.zero;
        }

        Transform servicePoint =
            BistroBuilderServiceModeUtility.GetWaiterServicePoint(
                task.Table,
                task.BarSpot
            );

        if (servicePoint != null)
        {
            return servicePoint.position;
        }

        if (task.Table != null)
        {
            return task.Table.transform.position;
        }

        return task.BarSpot != null
            ? task.BarSpot.transform.position
            : Vector3.zero;
    }

    private int GetNextDeliveryRunId()
    {
        if (nextDeliveryRunId == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Se alcanzó el límite de identificadores de rondas."
            );
        }

        return nextDeliveryRunId++;
    }

    private void ReleaseAssignedDeliveryTasks(List<WaiterTask> tasks)
    {
        for (int index = tasks.Count - 1; index >= 0; index--)
        {
            taskQueue.TryReleaseTaskAssignment(tasks[index]);
        }
    }

    private void ReturnDeliveryRunLinesToPickup(
        BistroBuilderDeliveryRun deliveryRun,
        string actorReference
    )
    {
        if (deliveryRun == null || lineExecutionService == null)
            return;

        for (int index = 0; index < deliveryRun.Items.Count; index++)
        {
            BistroBuilderDeliveryRunItem item = deliveryRun.Items[index];

            if (item.State == BistroBuilderDeliveryRunItemState.Served)
                continue;

            lineExecutionService.TryReturnLineToPickup(
                item.Order,
                item.OrderLineId,
                actorReference,
                out _
            );
        }
    }

    private void RollbackDeliveryRunDispatch(
        Waiter waiter,
        BistroBuilderDeliveryRun deliveryRun,
        List<WaiterTask> tasks
    )
    {
        ReturnDeliveryRunLinesToPickup(
            deliveryRun,
            "delivery_run_dispatch_rollback"
        );

        for (int index = 0; index < tasks.Count; index++)
        {
            WaiterTask task = tasks[index];

            if (task.State == WaiterTaskState.Assigned)
            {
                taskQueue.TryReleaseTaskAssignment(task);
                continue;
            }

            if (task.State == WaiterTaskState.InProgress)
            {
                taskQueue.TryCancelTask(task);

                if (TryGetTaskKitchen(task, out KitchenSystem kitchen) &&
                    lineExecutionService.IsLineReadyForPickup(
                        task.Order,
                        task.OrderLineId
                    ))
                {
                    CreateFoodDeliveryTask(
                        kitchen,
                        task.Order,
                        task.OrderLineId
                    );
                }
            }
        }

        deliveryRun.TryCancel();

        if (waiter != null &&
            ReferenceEquals(waiter.AssignedDeliveryRun, deliveryRun))
        {
            waiter.ClearAssignment();
        }

        RequestDispatch();
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
                if (task.Order == null ||
                    !BistroBuilderOrderIdUtility.IsValid(task.OrderLineId) ||
                    lineExecutionService == null)
                {
                    return false;
                }

                return lineExecutionService.TryAssignLineForDelivery(
                    task.Order,
                    task.OrderLineId,
                    waiter,
                    out _
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

    private void RollbackAcceptedTask(
        Waiter waiter,
        WaiterTask task
    )
    {
        if (task == null)
            return;

        if (task.Type == WaiterTaskType.DeliverFood &&
            lineExecutionService != null &&
            task.Order != null &&
            BistroBuilderOrderIdUtility.IsValid(task.OrderLineId))
        {
            lineExecutionService.TryReturnLineToPickup(
                task.Order,
                task.OrderLineId,
                waiter,
                out _
            );
        }

        if (waiter != null &&
            ReferenceEquals(waiter.AssignedOrder, task.Order) &&
            string.Equals(
                waiter.AssignedOrderLineId,
                task.OrderLineId,
                StringComparison.Ordinal
            ))
        {
            waiter.ClearAssignment();
        }

        taskQueue.TryReleaseTaskAssignment(task);
    }

    /// <summary>
    /// Comprueba que la necesidad operativa de la tarea
    /// siga existiendo antes de asignarla.
    /// </summary>
    private bool IsTaskStillValid(
        WaiterTask task
    )
    {
        if (task == null)
        {
            return false;
        }

        if (task.Type != WaiterTaskType.DeliverFood && task.Table == null)
        {
            return false;
        }

        if (task.Type == WaiterTaskType.DeliverFood &&
            !task.HasValidDestination)
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
                       IsOrderDestinationOperational(task.Order) &&
                       BistroBuilderOrderIdUtility.IsValid(task.OrderLineId) &&
                       lineExecutionService != null &&
                       lineExecutionService.IsLineReadyForPickup(
                           task.Order,
                           task.OrderLineId
                       );

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

    private bool IsOrderDestinationOperational(RestaurantOrder order)
    {
        if (order == null || !order.HasValidDestination)
        {
            return false;
        }

        if (order.HasTableDestination)
        {
            return registeredTables.Contains(order.Table);
        }

        return order.BarSpot != null &&
               order.BarSpot.isActiveAndEnabled &&
               ReferenceEquals(
                   order.BarSpot.AssignedCustomerGroup,
                   order.CustomerGroup
               ) &&
               ReferenceEquals(
                   order.CustomerGroup != null
                       ? order.CustomerGroup.AssignedBarSpot
                       : null,
                   order.BarSpot
               );
    }

    /// <summary>
    /// Selecciona el camarero disponible más próximo
    /// al destino inicial de la tarea.
    /// </summary>
    private Waiter FindBestAvailableWaiter(
        WaiterTask task
    )
    {
        if (task == null)
            return null;

        // La persona que tomó la comanda conserva la responsabilidad de su
        // mesa. Se la prioriza siempre que siga registrada y disponible.
        Waiter responsibleWaiter = task.Type == WaiterTaskType.DeliverFood &&
            task.Order != null
                ? task.Order.AssignedWaiter
                : null;

        if (responsibleWaiter != null &&
            registeredWaiters.Contains(responsibleWaiter) &&
            responsibleWaiter.IsAvailable)
        {
            return responsibleWaiter;
        }

        // Si el responsable está ocupado o ausente, el camarero libre más
        // próximo actúa como apoyo para que los platos no se enfríen.
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
        if (task.Type == WaiterTaskType.DeliverFood &&
            BistroBuilderOrderIdUtility.IsValid(task.OrderLineId))
        {
            if (kitchenByOrderLineId.TryGetValue(
                    task.OrderLineId,
                    out KitchenSystem kitchenSystem
                ) &&
                kitchenSystem != null &&
                kitchenSystem.PickupPoint != null)
            {
                return kitchenSystem.PickupPoint.position;
            }
        }

        Transform servicePoint =
            BistroBuilderServiceModeUtility.GetWaiterServicePoint(
                task.Table,
                task.BarSpot
            );

        if (servicePoint != null)
        {
            return servicePoint.position;
        }

        if (task.Table != null)
        {
            return task.Table.transform.position;
        }

        return task.BarSpot != null
            ? task.BarSpot.transform.position
            : transform.position;
    }

    /// <summary>
    /// Localiza una tarea de reparto mediante la comanda
    /// o mediante el camarero que la tiene asignada.
    /// </summary>
    private WaiterTask FindFoodDeliveryTask(
        Waiter waiter,
        RestaurantOrder order,
        string orderLineId
    )
    {
        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (order != null &&
            order.HasValidDestination &&
            BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            if (taskQueue.TryGetActiveTask(
                    WaiterTaskType.DeliverFood,
                    order.Table,
                    order,
                    normalizedLineId,
                    out WaiterTask taskByLine
                ))
            {
                return taskByLine;
            }
        }

        if (waiter == null && order == null)
            return null;

        IReadOnlyList<WaiterTask> tasks = taskQueue.ActiveTasks;
        WaiterTask uniqueOrderTask = null;

        for (int index = 0; index < tasks.Count; index++)
        {
            WaiterTask task = tasks[index];

            if (task == null ||
                task.Type != WaiterTaskType.DeliverFood)
            {
                continue;
            }

            if (waiter != null &&
                ReferenceEquals(task.AssignedWaiter, waiter))
            {
                return task;
            }

            if (order != null && ReferenceEquals(task.Order, order))
            {
                // La compatibilidad por comanda solo es segura cuando existe
                // una única tarea activa para ella.
                if (uniqueOrderTask != null)
                    return null;

                uniqueOrderTask = task;
            }
        }

        return uniqueOrderTask;
    }

    /// <summary>
    /// Recupera las tareas asignadas a un camarero
    /// que abandona el registro.
    /// </summary>
    private void RecoverTasksAssignedToWaiter(
        Waiter waiter
    )
    {
        if (waiter == null || taskQueue == null)
            return;

        if (waiter.AssignedDeliveryRun != null)
        {
            ReportFoodDeliveryRunFailure(
                waiter,
                waiter.AssignedDeliveryRun
            );
            return;
        }

        IReadOnlyList<WaiterTask> tasks = taskQueue.ActiveTasks;

        for (int index = tasks.Count - 1; index >= 0; index--)
        {
            WaiterTask task = tasks[index];

            if (task == null ||
                !ReferenceEquals(task.AssignedWaiter, waiter))
            {
                continue;
            }

            if (task.State == WaiterTaskState.Assigned)
            {
                if (task.Type == WaiterTaskType.DeliverFood &&
                    lineExecutionService != null &&
                    task.Order != null &&
                    BistroBuilderOrderIdUtility.IsValid(task.OrderLineId))
                {
                    lineExecutionService.TryReturnLineToPickup(
                        task.Order,
                        task.OrderLineId,
                        waiter,
                        out _
                    );
                }

                taskQueue.TryReleaseTaskAssignment(task);

                if (ReferenceEquals(waiter.AssignedOrder, task.Order) ||
                    ReferenceEquals(waiter.AssignedTable, task.Table))
                {
                    waiter.ClearAssignment();
                }

                continue;
            }

            RestaurantTable affectedTable = task.Table;
            RestaurantOrder affectedOrder = task.Order;
            string affectedLineId = task.OrderLineId;
            WaiterTaskType affectedType = task.Type;

            if (affectedType == WaiterTaskType.DeliverFood &&
                lineExecutionService != null &&
                affectedOrder != null &&
                BistroBuilderOrderIdUtility.IsValid(affectedLineId))
            {
                lineExecutionService.TryReturnLineToPickup(
                    affectedOrder,
                    affectedLineId,
                    waiter,
                    out _
                );
            }

            taskQueue.TryCancelTask(task);

            if (ReferenceEquals(waiter.AssignedOrder, affectedOrder) ||
                ReferenceEquals(waiter.AssignedTable, affectedTable))
            {
                waiter.ClearAssignment();
            }

            if (affectedType == WaiterTaskType.DeliverFood &&
                affectedOrder != null &&
                BistroBuilderOrderIdUtility.IsValid(affectedLineId) &&
                lineExecutionService != null &&
                lineExecutionService.IsLineReadyForPickup(
                    affectedOrder,
                    affectedLineId
                ) &&
                kitchenByOrderLineId.TryGetValue(
                    affectedLineId,
                    out KitchenSystem kitchenSystem
                ) &&
                kitchenSystem != null)
            {
                CreateFoodDeliveryTask(
                    kitchenSystem,
                    affectedOrder,
                    affectedLineId
                );
            }
            else
            {
                SynchronizeTableTasks(affectedTable);
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
        if (task == null || taskQueue == null)
            return;

        if (task.State == WaiterTaskState.Completed ||
            task.State == WaiterTaskState.Cancelled)
        {
            return;
        }

        Waiter assignedRunWaiter = task.AssignedWaiter;

        if (task.Type == WaiterTaskType.DeliverFood &&
            assignedRunWaiter != null &&
            assignedRunWaiter.AssignedDeliveryRun != null &&
            assignedRunWaiter.AssignedDeliveryRun.ContainsLine(
                task.Order,
                task.OrderLineId
            ))
        {
            ReportFoodDeliveryRunFailure(
                assignedRunWaiter,
                assignedRunWaiter.AssignedDeliveryRun
            );
            return;
        }

        string orderLineId = task.OrderLineId;
        Waiter assignedWaiter = task.AssignedWaiter;

        if (task.Type == WaiterTaskType.DeliverFood &&
            task.Order != null &&
            BistroBuilderOrderIdUtility.IsValid(orderLineId) &&
            lineExecutionService != null)
        {
            string actorReference = assignedWaiter != null
                ? BistroBuilderServiceOrderIdentityUtility
                    .BuildWaiterReference(assignedWaiter.WaiterId)
                : "task_cancellation_recovery";

            lineExecutionService.TryReturnLineToPickup(
                task.Order,
                orderLineId,
                actorReference,
                out _
            );
        }

        taskQueue.TryCancelTask(task);

        if (assignedWaiter != null)
        {
            bool ownsFoodAssignment =
                task.Type == WaiterTaskType.DeliverFood &&
                ReferenceEquals(assignedWaiter.AssignedOrder, task.Order) &&
                string.Equals(
                    assignedWaiter.AssignedOrderLineId,
                    orderLineId,
                    StringComparison.Ordinal
                );

            bool ownsTableAssignment =
                task.Type != WaiterTaskType.DeliverFood &&
                ReferenceEquals(assignedWaiter.AssignedTable, task.Table);

            if (ownsFoodAssignment || ownsTableAssignment)
            {
                assignedWaiter.ClearAssignment();
            }
        }

        if (task.Type == WaiterTaskType.DeliverFood &&
            BistroBuilderOrderIdUtility.IsValid(orderLineId))
        {
            kitchenByOrderLineId.Remove(orderLineId);
            deliveryTaskReadyRealtimeByLineId.Remove(orderLineId);
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

        List<WaiterTask> tasks = new List<WaiterTask>(
            taskQueue.ActiveTasks
        );

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

        List<WaiterTask> tasks = new List<WaiterTask>(
            taskQueue.ActiveTasks
        );

        for (int index = tasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                tasks[index];

            if (task == null ||
                task.Type !=
                    WaiterTaskType.DeliverFood ||
                !BistroBuilderOrderIdUtility.IsValid(task.OrderLineId))
            {
                continue;
            }

            if (!kitchenByOrderLineId.TryGetValue(
                    task.OrderLineId,
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

    public bool ValidateIndividualDishFlowConfiguration(out string error)
    {
        ResolveLineExecutionService();

        if (!manageFoodDeliveryTasks)
        {
            error = "El coordinador no gestiona reparto de comida.";
            return false;
        }

        if (lineExecutionService == null)
        {
            error = "Falta BistroBuilderOrderLineExecutionService.";
            return false;
        }

        if (!lineExecutionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (enableMultiTableDeliveryRuns && maxDeliveryRunSize < 1)
        {
            error = "El tamaño máximo de ronda debe ser mayor que cero.";
            return false;
        }

        if (float.IsNaN(deliveryRunConsolidationSeconds) ||
            float.IsInfinity(deliveryRunConsolidationSeconds) ||
            deliveryRunConsolidationSeconds < 0f)
        {
            error = "La ventana de consolidación 367G1 no es válida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ResolveLineExecutionService()
    {
        if (lineExecutionService != null)
            return;

        lineExecutionService = GetComponent<
            BistroBuilderOrderLineExecutionService
        >();

        if (lineExecutionService == null)
        {
            lineExecutionService = FindFirstObjectByType<
                BistroBuilderOrderLineExecutionService
            >();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxDeliveryRunSize = Mathf.Max(1, maxDeliveryRunSize);
        deliveryRunConsolidationSeconds = Mathf.Max(
            0f,
            deliveryRunConsolidationSeconds
        );
    }
#endif

    private void ValidateRuntimeConfiguration()
    {
        if (registeredWaiters.Count == 0)
        {
            Debug.LogWarning(
                "WaiterTaskCoordinator no ha encontrado camareros.",
                this
            );
        }

        if (manageFoodDeliveryTasks && registeredKitchens.Count == 0)
        {
            Debug.LogWarning(
                "WaiterTaskCoordinator gestiona repartos, " +
                "pero no ha encontrado ninguna cocina.",
                this
            );
        }

        if (!manageFoodDeliveryTasks)
        {
            return;
        }

        if (!ValidateIndividualDishFlowConfiguration(
                out string configurationError
            ))
        {
            Debug.LogError(configurationError, this);
        }
    }
}
