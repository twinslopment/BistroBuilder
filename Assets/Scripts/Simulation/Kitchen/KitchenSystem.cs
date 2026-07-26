using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procesa unidades físicas de plato de forma individual.
///
/// Mantiene una sola capacidad provisional de producción, pero cada línea
/// conserva su propio estado, duración y evento de pase. Las futuras estaciones
/// de cocina podrán reutilizar el mismo contrato y repartir la cola.
/// </summary>
public sealed class KitchenSystem : MonoBehaviour
{
    /// <summary>
    /// Revisión del runtime individual instalada.
    /// </summary>
    public const string RuntimeRevision = "367D1";
    [Header("Identidad persistente")]
    [SerializeField]
    private string kitchenId = "kitchen_main";

    [Header("Referencias")]
    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderOrderLineExecutionService lineExecutionService;

    [SerializeField]
    private Transform pickupPoint;

    [Header("Preparación por plato")]
    [Tooltip(
        "Convierte los segundos de definición del plato en segundos reales " +
        "de la simulación provisional."
    )]
    [SerializeField, Min(0.0001f)]
    private float preparationDurationScale = 0.01f;

    [SerializeField, Min(0.05f)]
    private float minimumPreparationDuration = 0.25f;

    [SerializeField, Min(0.1f)]
    private float maximumPreparationDuration = 30f;

    [Header("Estado actual")]
    [SerializeField]
    private KitchenState currentState = KitchenState.Idle;

    private readonly Queue<LineWorkItem> pendingLines = new();
    private readonly HashSet<string> trackedLineIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<RestaurantOrder> subscribedOrders =
        new HashSet<RestaurantOrder>();

    private LineWorkItem activeWork;
    private Coroutine processingRoutine;

    // Impide que StartCoroutine reentre antes de devolver el manejador.
    // Unity ejecuta la corrutina hasta su primer yield de forma síncrona;
    // por eso comprobar solo processingRoutine == null no es suficiente.
    private bool processingLoopClaimed;
    private bool suppressProcessingRestart;

    private long nextWorkSequence;
    private bool hasStarted;

    public event Action<KitchenState> StateChanged;

    /// <summary>
    /// Evento heredado de compatibilidad. Solo se emite cuando todas las líneas
    /// activas han abandonado la fase de cocina.
    /// </summary>
    public event Action<RestaurantOrder> OrderReady;

    public event Action<BistroBuilderOrderLineReadyEvent> OrderLineReady;

    public string KitchenId => kitchenId ?? string.Empty;
    public KitchenState CurrentState => currentState;
    public RestaurantOrder ActiveOrder => activeWork?.Order;
    public string ActiveOrderLineId => activeWork?.OrderLineId ?? string.Empty;
    public float ActiveRemainingPreparationSeconds =>
        activeWork != null ? activeWork.RemainingDurationSeconds : 0f;
    public int PendingOrderCount => pendingLines.Count;
    public int PendingLineCount => pendingLines.Count;
    public Transform PickupPoint => pickupPoint;
    public BistroBuilderOrderLineExecutionService LineExecutionService =>
        lineExecutionService;

    private void Awake()
    {
        kitchenId = BistroBuilderOrderIdUtility.Normalize(kitchenId);
    }

    private void OnEnable()
    {
        if (orderSystem != null)
        {
            orderSystem.OrderCreated -= HandleOrderCreated;
            orderSystem.OrderCreated += HandleOrderCreated;
        }

        if (hasStarted)
        {
            SynchronizeExistingOrders();
            EnsureProcessingRoutine();
        }
    }

    private void Start()
    {
        hasStarted = true;

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
            return;
        }

        SynchronizeExistingOrders();
    }

    private void OnDisable()
    {
        if (orderSystem != null)
        {
            orderSystem.OrderCreated -= HandleOrderCreated;
        }

        foreach (RestaurantOrder order in subscribedOrders)
        {
            if (order != null)
            {
                order.StateChanged -= HandleOrderStateChanged;
            }
        }

        subscribedOrders.Clear();

        StopProcessingRoutine();

        if (Application.isPlaying &&
            activeWork != null &&
            lineExecutionService != null)
        {
            lineExecutionService.TryInterruptPreparation(
                activeWork.Order,
                activeWork.OrderLineId,
                KitchenId,
                out _
            );
        }

        pendingLines.Clear();
        trackedLineIds.Clear();
        activeWork = null;
        UpdateKitchenState();
    }

    public bool ValidateConfiguration(out string error)
    {
        kitchenId = BistroBuilderOrderIdUtility.Normalize(kitchenId);

        if (!BistroBuilderOrderIdUtility.IsValid(kitchenId))
        {
            error = "KitchenSystem necesita un KitchenId estable válido.";
            return false;
        }

        if (orderSystem == null)
        {
            error = "KitchenSystem necesita una referencia a OrderSystem.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error))
        {
            return false;
        }

        if (lineExecutionService == null)
        {
            error =
                "KitchenSystem necesita BistroBuilderOrderLineExecutionService.";
            return false;
        }

        if (!lineExecutionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (pickupPoint == null)
        {
            error = "KitchenSystem no tiene PickupPoint asignado.";
            return false;
        }

        if (float.IsNaN(preparationDurationScale) ||
            float.IsInfinity(preparationDurationScale) ||
            preparationDurationScale <= 0f)
        {
            error = "La escala de preparación es inválida.";
            return false;
        }

        if (float.IsNaN(minimumPreparationDuration) ||
            float.IsInfinity(minimumPreparationDuration) ||
            float.IsNaN(maximumPreparationDuration) ||
            float.IsInfinity(maximumPreparationDuration) ||
            minimumPreparationDuration <= 0f ||
            maximumPreparationDuration < minimumPreparationDuration)
        {
            error = "Los límites de preparación son inválidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderKitchenRuntimeSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        snapshot = new BistroBuilderKitchenRuntimeSnapshot
        {
            kitchenId = KitchenId,
            nextSequence = nextWorkSequence
        };

        if (activeWork != null)
        {
            snapshot.workItems.Add(activeWork.ToSaveData(true));
        }

        foreach (LineWorkItem item in pendingLines)
        {
            if (item != null)
            {
                snapshot.workItems.Add(item.ToSaveData(false));
            }
        }

        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reconstrucción preparada para service.runtime.
    ///
    /// El diccionario debe haberse creado después de restaurar las comandas
    /// legacy y canónicas. No se aceptan referencias parciales.
    /// </summary>
    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderKitchenRuntimeSnapshot snapshot,
        IReadOnlyDictionary<string, RestaurantOrder> ordersByCanonicalId,
        out string error
    )
    {
        if (snapshot == null)
        {
            error = "El snapshot de cocina es nulo.";
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!snapshot.TryValidate(out error))
        {
            return false;
        }

        if (!string.Equals(
                snapshot.kitchenId,
                KitchenId,
                StringComparison.Ordinal
            ))
        {
            error = "El snapshot pertenece a otra cocina.";
            return false;
        }

        if (ordersByCanonicalId == null)
        {
            error = "El registro de comandas restauradas es nulo.";
            return false;
        }

        List<LineWorkItem> candidates = new List<LineWorkItem>();

        for (int index = 0; index < snapshot.workItems.Count; index++)
        {
            BistroBuilderKitchenLineWorkSaveData data =
                snapshot.workItems[index];

            if (!ordersByCanonicalId.TryGetValue(
                    data.canonicalOrderId,
                    out RestaurantOrder order
                ))
            {
                error = "No se pudo resolver una comanda del snapshot.";
                return false;
            }

            if (order == null || order.OrderId != data.legacyOrderId)
            {
                error =
                    "La comanda resuelta no coincide con el snapshot.";
                return false;
            }

            if (!lineExecutionService.TryGetLineSnapshot(
                    order,
                    data.orderLineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine line,
                    out error
                ))
            {
                return false;
            }

            bool compatible = data.wasActive
                ? line.State == BistroBuilderCanonicalOrderLineState.Preparing
                : line.State == BistroBuilderCanonicalOrderLineState.Queued;

            if (!compatible ||
                !string.Equals(
                    line.DishId,
                    data.dishId,
                    StringComparison.Ordinal
                ))
            {
                error =
                    "El estado canónico no coincide con el snapshot de cocina.";
                return false;
            }

            candidates.Add(LineWorkItem.FromSaveData(order, data));
        }

        StopProcessingRoutine();

        pendingLines.Clear();
        trackedLineIds.Clear();
        activeWork = null;
        nextWorkSequence = snapshot.nextSequence;

        candidates.Sort((left, right) =>
            left.Sequence.CompareTo(right.Sequence));

        for (int index = 0; index < candidates.Count; index++)
        {
            LineWorkItem item = candidates[index];
            trackedLineIds.Add(item.OrderLineId);
            pendingLines.Enqueue(item);
        }

        UpdateKitchenState();
        EnsureProcessingRoutine();
        error = string.Empty;
        return true;
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        SubscribeToOrder(order);
    }

    private void SubscribeToOrder(RestaurantOrder order)
    {
        if (order == null || !subscribedOrders.Add(order))
        {
            return;
        }

        order.StateChanged -= HandleOrderStateChanged;
        order.StateChanged += HandleOrderStateChanged;

        if (order.CurrentState == OrderState.SentToKitchen ||
            order.CurrentState == OrderState.Preparing)
        {
            EnqueueQueuedLines(order);
        }
    }

    private void HandleOrderStateChanged(
        RestaurantOrder order,
        OrderState newState
    )
    {
        if (newState == OrderState.SentToKitchen ||
            newState == OrderState.Preparing)
        {
            EnqueueQueuedLines(order);
            return;
        }

        if (newState == OrderState.Completed ||
            newState == OrderState.Cancelled)
        {
            UnsubscribeFromOrder(order);
        }
    }

    private void UnsubscribeFromOrder(RestaurantOrder order)
    {
        if (order == null || !subscribedOrders.Remove(order))
        {
            return;
        }

        order.StateChanged -= HandleOrderStateChanged;
    }

    private void SynchronizeExistingOrders()
    {
        IReadOnlyList<RestaurantOrder> activeOrders = orderSystem.ActiveOrders;

        for (int index = 0; index < activeOrders.Count; index++)
        {
            SubscribeToOrder(activeOrders[index]);
        }
    }

    private void EnqueueQueuedLines(RestaurantOrder order)
    {
        if (order == null || order.IsFinished)
        {
            return;
        }

        if (!lineExecutionService.CanonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot
            ))
        {
            Debug.LogError(
                "No se pudo leer la comanda canónica " +
                order.CanonicalOrderId + ".",
                this
            );
            return;
        }

        if (snapshot == null)
        {
            Debug.LogError(
                "La fotografía canónica de la comanda " +
                order.CanonicalOrderId + " es nula.",
                this
            );
            return;
        }

        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[index];

            if (line == null ||
                line.State != BistroBuilderCanonicalOrderLineState.Queued ||
                !trackedLineIds.Add(line.LineId))
            {
                continue;
            }

            if (!lineExecutionService.TryResolvePreparationDurationSeconds(
                    order,
                    line.LineId,
                    preparationDurationScale,
                    minimumPreparationDuration,
                    maximumPreparationDuration,
                    out float duration,
                    out string error
                ))
            {
                trackedLineIds.Remove(line.LineId);
                Debug.LogError(error, this);
                continue;
            }

            pendingLines.Enqueue(
                new LineWorkItem(
                    order,
                    line.LineId,
                    line.DishId,
                    nextWorkSequence++,
                    duration,
                    duration,
                    false
                )
            );

            Debug.Log(
                "Línea " + line.LineId + " (" + line.DishId +
                ") añadida a la cola de cocina " + KitchenId + ".",
                this
            );
        }

        UpdateKitchenState();
        EnsureProcessingRoutine();
    }

    private void EnsureProcessingRoutine()
    {
        if (!isActiveAndEnabled ||
            pendingLines.Count == 0 ||
            !TryClaimProcessingLoop())
        {
            return;
        }

        try
        {
            Coroutine startedRoutine = StartCoroutine(ProcessLinesRoutine());

            // Si la corrutina terminó de forma síncrona antes del primer yield,
            // el bloque finally ya liberó la reclamación y no debemos conservar
            // un manejador obsoleto.
            if (processingLoopClaimed)
            {
                processingRoutine = startedRoutine;
            }
        }
        catch
        {
            processingRoutine = null;
            ReleaseProcessingLoopClaim();
            throw;
        }
    }

    private bool TryClaimProcessingLoop()
    {
        if (processingLoopClaimed)
        {
            return false;
        }

        processingLoopClaimed = true;
        return true;
    }

    private void ReleaseProcessingLoopClaim()
    {
        processingLoopClaimed = false;
    }

    private void StopProcessingRoutine()
    {
        Coroutine routineToStop = processingRoutine;
        processingRoutine = null;
        ReleaseProcessingLoopClaim();

        suppressProcessingRestart = true;

        try
        {
            if (routineToStop != null)
            {
                StopCoroutine(routineToStop);
            }
        }
        finally
        {
            suppressProcessingRestart = false;
        }
    }

    private IEnumerator ProcessLinesRoutine()
    {
        try
        {
            while (pendingLines.Count > 0)
            {
            activeWork = pendingLines.Dequeue();

            if (activeWork == null ||
                activeWork.Order == null ||
                activeWork.Order.IsFinished)
            {
                ReleaseActiveTracking();
                continue;
            }

            if (!lineExecutionService.TryGetLineSnapshot(
                    activeWork.Order,
                    activeWork.OrderLineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine line,
                    out string readError
                ))
            {
                Debug.LogWarning(readError, this);
                ReleaseActiveTracking();
                continue;
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Queued)
            {
                if (!lineExecutionService.TryBeginPreparation(
                        activeWork.Order,
                        activeWork.OrderLineId,
                        KitchenId,
                        out string beginError
                    ))
                {
                    Debug.LogError(beginError, this);
                    ReleaseActiveTracking();
                    continue;
                }
            }
            else if (line.State !=
                     BistroBuilderCanonicalOrderLineState.Preparing)
            {
                ReleaseActiveTracking();
                continue;
            }

            Debug.Log(
                "Cocina " + KitchenId + " prepara la línea " +
                activeWork.OrderLineId + " (" + activeWork.DishId + ").",
                this
            );

            UpdateKitchenState();

            while (activeWork != null &&
                   activeWork.RemainingDurationSeconds > 0f)
            {
                activeWork.RemainingDurationSeconds = Mathf.Max(
                    0f,
                    activeWork.RemainingDurationSeconds - Time.deltaTime
                );

                yield return null;
            }

            if (activeWork == null)
            {
                continue;
            }

            RestaurantOrder completedOrder = activeWork.Order;
            string completedLineId = activeWork.OrderLineId;
            string completedDishId = activeWork.DishId;

            bool completed =
                lineExecutionService.TryCompletePreparation(
                    completedOrder,
                    completedLineId,
                    KitchenId,
                    out bool productionComplete,
                    out string completionError
                );

            if (!completed)
            {
                Debug.LogError(completionError, this);
                ReleaseActiveTracking();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(completionError))
            {
                Debug.LogWarning(completionError, this);
            }

            Debug.Log(
                "Línea " + completedLineId + " lista para recoger.",
                this
            );

            OrderLineReady?.Invoke(
                new BistroBuilderOrderLineReadyEvent(
                    this,
                    completedOrder,
                    completedLineId,
                    completedDishId
                )
            );

            if (productionComplete)
            {
                OrderReady?.Invoke(completedOrder);
            }

                ReleaseActiveTracking();
            }
        }
        finally
        {
            processingRoutine = null;
            ReleaseProcessingLoopClaim();
            UpdateKitchenState();

            // Cubre altas realizadas durante una transición síncrona al final
            // del ciclo, sin permitir dos consumidores simultáneos.
            if (!suppressProcessingRestart &&
                isActiveAndEnabled &&
                pendingLines.Count > 0)
            {
                EnsureProcessingRoutine();
            }
        }
    }

    private void ReleaseActiveTracking()
    {
        if (activeWork != null)
        {
            trackedLineIds.Remove(activeWork.OrderLineId);
        }

        activeWork = null;
        UpdateKitchenState();
    }

    private void UpdateKitchenState()
    {
        int workload = pendingLines.Count + (activeWork != null ? 1 : 0);

        KitchenState newState = workload switch
        {
            0 => KitchenState.Idle,
            1 => KitchenState.Working,
            <= 4 => KitchenState.Busy,
            _ => KitchenState.Overloaded
        };

        if (currentState == newState)
        {
            return;
        }

        currentState = newState;

        Debug.Log(
            "Estado de cocina cambiado a " + currentState + ".",
            this
        );

        StateChanged?.Invoke(currentState);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        kitchenId = BistroBuilderOrderIdUtility.Normalize(kitchenId);
        preparationDurationScale = Mathf.Max(
            0.0001f,
            preparationDurationScale
        );
        minimumPreparationDuration = Mathf.Max(
            0.05f,
            minimumPreparationDuration
        );
        maximumPreparationDuration = Mathf.Max(
            minimumPreparationDuration,
            maximumPreparationDuration
        );
    }
#endif

    private sealed class LineWorkItem
    {
        public RestaurantOrder Order { get; }
        public string OrderLineId { get; }
        public string DishId { get; }
        public long Sequence { get; }
        public float TotalDurationSeconds { get; }
        public float RemainingDurationSeconds { get; set; }
        public bool RestoredAsActive { get; }

        public LineWorkItem(
            RestaurantOrder order,
            string orderLineId,
            string dishId,
            long sequence,
            float totalDurationSeconds,
            float remainingDurationSeconds,
            bool restoredAsActive
        )
        {
            Order = order;
            OrderLineId =
                BistroBuilderOrderIdUtility.Normalize(orderLineId);
            DishId = BistroBuilderOrderIdUtility.Normalize(dishId);
            Sequence = sequence;
            TotalDurationSeconds = totalDurationSeconds;
            RemainingDurationSeconds = remainingDurationSeconds;
            RestoredAsActive = restoredAsActive;
        }

        public BistroBuilderKitchenLineWorkSaveData ToSaveData(bool active)
        {
            return new BistroBuilderKitchenLineWorkSaveData
            {
                canonicalOrderId = Order?.CanonicalOrderId ?? string.Empty,
                orderLineId = OrderLineId,
                dishId = DishId,
                legacyOrderId = Order?.OrderId ?? 0,
                sequence = Sequence,
                totalDurationSeconds = TotalDurationSeconds,
                remainingDurationSeconds = RemainingDurationSeconds,
                wasActive = active
            };
        }

        public static LineWorkItem FromSaveData(
            RestaurantOrder order,
            BistroBuilderKitchenLineWorkSaveData data
        )
        {
            return new LineWorkItem(
                order,
                data.orderLineId,
                data.dishId,
                data.sequence,
                data.totalDurationSeconds,
                data.remainingDurationSeconds,
                data.wasActive
            );
        }
    }
}
