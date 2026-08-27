using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime del consumo individual de clientes.
///
/// Responsabilidades:
/// - Registrar una sesión por comanda canónica activa.
/// - Resolver qué líneas pertenecen a cada CustomerId y pase.
/// - Iniciar y completar el consumo por cliente, no por grupo.
/// - Marcar líneas Served como Consumed únicamente cuando todos sus
///   consumidores han terminado.
/// - Mantener el estado de grupo/mesa como fachada coarse compatible.
/// - Bloquear la cuenta hasta que todas las líneas estén resueltas.
/// - Exponer un snapshot versionado para el futuro service.runtime.
///
/// No utiliza Find ni búsquedas por escena. Todas las dependencias se instalan
/// en GameSystems y las consultas se realizan mediante índices runtime.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Customers/Individual Customer Dining Service"
)]
public sealed class BistroBuilderCustomerDiningService : MonoBehaviour
{
    public const string RuntimeRevision = "367F";

    [Header("Dependencias")]

    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [SerializeField]
    private BistroBuilderOrderLineExecutionService lineExecutionService;

    [Header("Configuración provisional")]

    [Tooltip(
        "Duración provisional de cada pase por cliente hasta que existan " +
        "ritmos, rasgos y platos con tiempos propios de consumo."
    )]
    [SerializeField, Min(0.1f)]
    private float defaultEatingDurationSeconds = 6f;

    [Tooltip(
        "Desfase determinista entre clientes del mismo pase. Permite que un " +
        "plato compartido conserve progreso parcial real."
    )]
    [SerializeField, Min(0f)]
    private float perCustomerEatingDurationOffsetSeconds;

    [Header("Estado runtime persistible")]

    [SerializeField]
    private List<BistroBuilderCustomerDiningOrderRuntime> activeOrders =
        new List<BistroBuilderCustomerDiningOrderRuntime>();

    [Header("Depuración")]

    [SerializeField]
    private bool logTransitions = true;

    private readonly Dictionary<string, BistroBuilderCustomerDiningOrderRuntime>
        runtimeByOrderId =
            new Dictionary<string, BistroBuilderCustomerDiningOrderRuntime>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, RestaurantOrder> legacyByOrderId =
        new Dictionary<string, RestaurantOrder>(StringComparer.Ordinal);

    private readonly HashSet<string> pendingReconciliationOrderIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly List<string> pendingDrainBuffer = new List<string>(16);
    private readonly List<CustomerCompletionKey> completionBuffer =
        new List<CustomerCompletionKey>(32);
    private readonly List<string> lineIdBuffer = new List<string>(16);
    private readonly List<string> consumedLineBuffer = new List<string>(16);
    private readonly List<string> fullyClaimedLineBuffer =
        new List<string>(16);
    private readonly List<BistroBuilderCustomerDiningCustomerRuntime>
        customerCreationBuffer =
            new List<BistroBuilderCustomerDiningCustomerRuntime>(16);
    private readonly List<SharedLineProgressKey> sharedProgressBuffer =
        new List<SharedLineProgressKey>(16);

    private bool initialized;
    private bool subscriptionsActive;
    private bool mutationScopeActive;

    public event Action<BistroBuilderCustomerDiningChangedEvent>
        DiningChanged;

    public OrderSystem OrderSystem => orderSystem;
    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;
    public BistroBuilderOrderLineExecutionService LineExecutionService =>
        lineExecutionService;
    public float DefaultEatingDurationSeconds => defaultEatingDurationSeconds;
    public float PerCustomerEatingDurationOffsetSeconds =>
        perCustomerEatingDurationOffsetSeconds;
    public int ActiveOrderCount => activeOrders != null ? activeOrders.Count : 0;
    public int Revision { get; private set; }

    private void Awake()
    {
        ResolveDependencies();

        if (!RebuildRuntimeIndex(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
            return;
        }

        ReconcileExistingLegacyOrders();
        QueueAllOrdersForReconciliation();
        DrainPendingReconciliations();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        DrainPendingReconciliations();

        if (activeOrders == null || activeOrders.Count == 0)
        {
            return;
        }

        float deltaSeconds = Time.deltaTime;

        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (!AdvanceDiningTime(deltaSeconds, out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (orderSystem == null)
        {
            error = "Falta OrderSystem en el consumo individual.";
            return false;
        }

        if (canonicalOrderService == null)
        {
            error =
                "Falta BistroBuilderCanonicalOrderService en el consumo " +
                "individual.";
            return false;
        }

        if (lineExecutionService == null)
        {
            error =
                "Falta BistroBuilderOrderLineExecutionService en el consumo " +
                "individual.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!lineExecutionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (float.IsNaN(defaultEatingDurationSeconds) ||
            float.IsInfinity(defaultEatingDurationSeconds) ||
            defaultEatingDurationSeconds <= 0f)
        {
            error = "La duración individual de consumo debe ser positiva.";
            return false;
        }

        if (float.IsNaN(perCustomerEatingDurationOffsetSeconds) ||
            float.IsInfinity(perCustomerEatingDurationOffsetSeconds) ||
            perCustomerEatingDurationOffsetSeconds < 0f ||
            perCustomerEatingDurationOffsetSeconds > 60f)
        {
            error = "El desfase individual de consumo no es válido.";
            return false;
        }

        if (activeOrders == null)
        {
            error = "La colección runtime de consumo es nula.";
            return false;
        }

        for (int index = 0; index < activeOrders.Count; index++)
        {
            BistroBuilderCustomerDiningOrderRuntime runtime =
                activeOrders[index];

            if (runtime == null)
            {
                error = "El runtime de consumo contiene una entrada nula.";
                return false;
            }

            if (!runtime.TryValidate(out error))
            {
                return false;
            }

            if (!canonicalOrderService.TryGetOrderSnapshot(
                    runtime.OrderId,
                    out BistroBuilderCanonicalOrder orderSnapshot
                ) ||
                orderSnapshot == null)
            {
                error =
                    "No existe la comanda canónica del runtime de consumo " +
                    runtime.OrderId + ".";
                return false;
            }

            if (!ValidateRuntimeAgainstCanonical(
                    runtime,
                    orderSnapshot,
                    out error
                ))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public bool RebuildRuntimeIndex(out string error)
    {
        ResolveDependencies();

        if (activeOrders == null)
        {
            activeOrders = new List<BistroBuilderCustomerDiningOrderRuntime>();
        }

        runtimeByOrderId.Clear();
        legacyByOrderId.Clear();

        for (int index = 0; index < activeOrders.Count; index++)
        {
            BistroBuilderCustomerDiningOrderRuntime runtime =
                activeOrders[index];

            if (runtime == null)
            {
                error = "El runtime de consumo contiene una entrada nula.";
                initialized = false;
                return false;
            }

            if (!runtime.TryValidate(out error))
            {
                initialized = false;
                return false;
            }

            if (runtimeByOrderId.ContainsKey(runtime.OrderId))
            {
                error = "Existe un OrderId duplicado en el consumo: " +
                        runtime.OrderId + ".";
                initialized = false;
                return false;
            }

            runtimeByOrderId.Add(runtime.OrderId, runtime);
        }

        if (orderSystem != null)
        {
            IReadOnlyList<RestaurantOrder> activeLegacyOrders =
                orderSystem.ActiveOrders;

            for (int index = 0; index < activeLegacyOrders.Count; index++)
            {
                RestaurantOrder order = activeLegacyOrders[index];

                if (order != null && order.HasCanonicalOrder)
                {
                    legacyByOrderId[order.CanonicalOrderId] = order;
                }
            }
        }

        initialized = true;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Desmonta la proyeccion de consumo antes de reconstruir service.runtime.
    /// No toca comandas ni clientes: elimina unicamente indices, temporizadores
    /// y reconciliaciones que pertenecen al mundo que va a ser sustituido.
    /// </summary>
    public void ClearRuntimeForLoad()
    {
        if (activeOrders == null)
        {
            activeOrders =
                new List<BistroBuilderCustomerDiningOrderRuntime>();
        }
        else
        {
            activeOrders.Clear();
        }

        runtimeByOrderId.Clear();
        legacyByOrderId.Clear();
        pendingReconciliationOrderIds.Clear();
        pendingDrainBuffer.Clear();
        completionBuffer.Clear();
        lineIdBuffer.Clear();
        consumedLineBuffer.Clear();
        fullyClaimedLineBuffer.Clear();
        customerCreationBuffer.Clear();
        sharedProgressBuffer.Clear();
        mutationScopeActive = false;
        initialized = true;
    }

    /// <summary>
    /// Notifica explicitamente que una linea acaba de ser servida.
    ///
    /// El servicio también escucha los eventos canónicos para restauraciones o
    /// mutaciones externas. La operación es idempotente y nunca inicia dos
    /// temporizadores para el mismo cliente.
    /// </summary>
    public bool TryNotifyLineServed(
        RestaurantOrder order,
        string orderLineId,
        out BistroBuilderCustomerDiningNotificationResult notification,
        out string error
    )
    {
        notification = default;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (!TryValidateLinkedLegacyOrder(order, out error))
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);

        if (!canonicalOrderService.TryGetOrderAndLineSnapshot(
                order.CanonicalOrderId,
                normalizedLineId,
                out _,
                out BistroBuilderCanonicalOrderLine line
            ) ||
            line == null)
        {
            error = "No se encontró la línea canónica servida.";
            return false;
        }

        if (line.State != BistroBuilderCanonicalOrderLineState.Served &&
            line.State != BistroBuilderCanonicalOrderLineState.Consumed)
        {
            error = "La línea notificada no está servida ni consumida.";
            return false;
        }

        if (!runtimeByOrderId.ContainsKey(order.CanonicalOrderId))
        {
            if (!TryRegisterOrder(order, out error))
            {
                return false;
            }
        }

        int startedBefore = CountEatingCustomers(order.CanonicalOrderId);
        QueueReconciliation(order.CanonicalOrderId);
        DrainPendingReconciliations();
        int startedAfter = CountEatingCustomers(order.CanonicalOrderId);

        if (!runtimeByOrderId.TryGetValue(
                order.CanonicalOrderId,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            error = "La sesión de consumo desapareció durante la notificación.";
            return false;
        }

        bool allStartedOrCompleted =
            AreAllCustomersStartedOrCompleted(runtime);

        notification = new BistroBuilderCustomerDiningNotificationResult(
            Mathf.Max(0, startedAfter - startedBefore),
            allStartedOrCompleted,
            runtime.AllCustomersCompleted,
            "Línea servida reconciliada con el consumo individual."
        );

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Guardia utilizada por la entrega de cuenta. La cuenta no puede comenzar
    /// por un cambio accidental del estado coarse.
    /// </summary>
    public bool TryValidateBillReady(
        RestaurantOrder order,
        out string error
    )
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (!TryValidateLinkedLegacyOrder(order, out error))
        {
            return false;
        }

        if (!runtimeByOrderId.TryGetValue(
                order.CanonicalOrderId,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            error = "No existe una sesión de consumo para la comanda.";
            return false;
        }

        if (!runtime.AllCustomersCompleted || !runtime.BillRequested)
        {
            error = "Todavía existen clientes o pases pendientes de consumo.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica para validar la cuenta.";
            return false;
        }

        if (!AreAllLinesResolvedForBill(canonical))
        {
            error = "La comanda contiene líneas todavía no consumidas.";
            return false;
        }

        if (canonical.State != BistroBuilderCanonicalOrderState.Completed)
        {
            error = "La comanda canónica aún no está completada.";
            return false;
        }

        if (order.CurrentState != OrderState.Served)
        {
            error = "La fachada legacy no está preparada para la cuenta.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetOrderRuntimeSnapshot(
        string orderId,
        out BistroBuilderCustomerDiningOrderRuntime snapshot
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out _))
        {
            return false;
        }

        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!runtimeByOrderId.TryGetValue(
                normalized,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            return false;
        }

        snapshot = runtime.Clone();
        return true;
    }

    public int CopyOrderRuntimeSnapshotsTo(
        List<BistroBuilderCustomerDiningOrderRuntime> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        if (!EnsureInitialized(out _))
        {
            return 0;
        }

        for (int index = 0; index < activeOrders.Count; index++)
        {
            destination.Add(activeOrders[index].Clone());
        }

        destination.Sort(CompareRuntimes);
        return destination.Count;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderCustomerDiningRuntimeSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        snapshot = new BistroBuilderCustomerDiningRuntimeSnapshot(activeOrders);

        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sustitución atómica del runtime de consumo preparada para
    /// service.runtime. La restauración no busca objetos de escena; enlaza las
    /// fachadas legacy que ya estén presentes en OrderSystem.
    /// </summary>
    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderCustomerDiningRuntimeSnapshot snapshot,
        bool notify,
        out string error
    )
    {
        if (snapshot == null)
        {
            error = "El snapshot de consumo es nulo.";
            return false;
        }

        if (!snapshot.TryValidate(out error))
        {
            return false;
        }

        ResolveDependencies();

        if (canonicalOrderService == null)
        {
            error = "La autoridad canónica no está disponible.";
            return false;
        }

        List<BistroBuilderCustomerDiningOrderRuntime> candidates =
            new List<BistroBuilderCustomerDiningOrderRuntime>(
                snapshot.Orders.Count
            );
        Dictionary<string, BistroBuilderCustomerDiningOrderRuntime>
            candidateIndex =
                new Dictionary<
                    string,
                    BistroBuilderCustomerDiningOrderRuntime
                >(StringComparer.Ordinal);

        for (int index = 0; index < snapshot.Orders.Count; index++)
        {
            BistroBuilderCustomerDiningOrderRuntime candidate =
                snapshot.Orders[index].Clone();

            if (!canonicalOrderService.TryGetOrderSnapshot(
                    candidate.OrderId,
                    out BistroBuilderCanonicalOrder canonical
                ) ||
                canonical == null)
            {
                error = "No existe la comanda canónica " +
                        candidate.OrderId + " durante la restauración.";
                return false;
            }

            if (!ValidateRuntimeAgainstCanonical(
                    candidate,
                    canonical,
                    out error
                ))
            {
                return false;
            }

            candidates.Add(candidate);
            candidateIndex.Add(candidate.OrderId, candidate);
        }

        activeOrders.Clear();
        activeOrders.AddRange(candidates);
        runtimeByOrderId.Clear();

        foreach (KeyValuePair<string, BistroBuilderCustomerDiningOrderRuntime>
                 pair in candidateIndex)
        {
            runtimeByOrderId.Add(pair.Key, pair.Value);
        }

        legacyByOrderId.Clear();
        ReconcileExistingLegacyOrders(false);
        initialized = true;
        Revision++;

        if (notify)
        {
            PublishChange(
                BistroBuilderCustomerDiningChangeType.StateRestored,
                string.Empty,
                string.Empty,
                string.Empty,
                "Runtime de consumo individual restaurado atómicamente."
            );
        }

        QueueAllOrdersForReconciliation();
        DrainPendingReconciliations();
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Avanza los temporizadores de consumo. Es internal para que el autotest
    /// pueda ejecutar tiempo determinista sin depender del Play Mode.
    /// </summary>
    internal bool AdvanceDiningTime(
        float deltaSeconds,
        out string error
    )
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (float.IsNaN(deltaSeconds) ||
            float.IsInfinity(deltaSeconds) ||
            deltaSeconds < 0f)
        {
            error = "El incremento temporal de consumo no es válido.";
            return false;
        }

        if (deltaSeconds <= 0f)
        {
            error = string.Empty;
            return true;
        }

        completionBuffer.Clear();

        for (int orderIndex = 0;
             orderIndex < activeOrders.Count;
             orderIndex++)
        {
            BistroBuilderCustomerDiningOrderRuntime runtime =
                activeOrders[orderIndex];

            for (int customerIndex = 0;
                 customerIndex < runtime.Customers.Count;
                 customerIndex++)
            {
                BistroBuilderCustomerDiningCustomerRuntime customer =
                    runtime.Customers[customerIndex];

                if (customer != null && customer.AdvanceTime(deltaSeconds))
                {
                    completionBuffer.Add(
                        new CustomerCompletionKey(
                            runtime.OrderId,
                            customer.CustomerId,
                            customer.CurrentCourseIndex
                        )
                    );
                }
            }
        }

        bool previousMutationScope = mutationScopeActive;
        mutationScopeActive = true;

        try
        {
            for (int index = 0; index < completionBuffer.Count; index++)
            {
                CustomerCompletionKey completion = completionBuffer[index];

                if (!TryCompleteCustomerCourse(
                        completion.OrderId,
                        completion.CustomerId,
                        completion.CourseIndex,
                        out error
                    ))
                {
                    return false;
                }
            }
        }
        finally
        {
            mutationScopeActive = previousMutationScope;
        }

        DrainPendingReconciliations();
        error = string.Empty;
        return true;
    }

    internal bool TryRegisterOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (!TryValidateLinkedLegacyOrder(order, out error))
        {
            return false;
        }

        if (runtimeByOrderId.ContainsKey(order.CanonicalOrderId))
        {
            legacyByOrderId[order.CanonicalOrderId] = order;
            error = string.Empty;
            return true;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica que debe registrarse.";
            return false;
        }

        if (!TryCreateRuntime(
                order,
                canonical,
                out BistroBuilderCustomerDiningOrderRuntime runtime,
                out error
            ))
        {
            return false;
        }

        activeOrders.Add(runtime);
        runtimeByOrderId.Add(runtime.OrderId, runtime);
        legacyByOrderId[runtime.OrderId] = order;
        Revision++;

        PublishChange(
            BistroBuilderCustomerDiningChangeType.OrderRegistered,
            runtime.OrderId,
            string.Empty,
            string.Empty,
            "Sesión de consumo individual registrada."
        );

        QueueReconciliation(runtime.OrderId);
        DrainPendingReconciliations();
        error = string.Empty;
        return true;
    }

    private bool TryCompleteCustomerCourse(
        string orderId,
        string customerId,
        int expectedCourseIndex,
        out string error
    )
    {
        string normalizedOrderId =
            BistroBuilderOrderIdUtility.Normalize(orderId);
        string normalizedCustomerId =
            BistroBuilderOrderIdUtility.Normalize(customerId);

        if (!runtimeByOrderId.TryGetValue(
                normalizedOrderId,
                out BistroBuilderCustomerDiningOrderRuntime currentRuntime
            ))
        {
            error = "No existe la sesión de consumo indicada.";
            return false;
        }

        if (!currentRuntime.TryGetCustomer(
                normalizedCustomerId,
                out BistroBuilderCustomerDiningCustomerRuntime currentCustomer
            ) ||
            currentCustomer == null)
        {
            error = "No existe el cliente indicado en la sesión de consumo.";
            return false;
        }

        if (currentCustomer.State !=
                BistroBuilderCustomerDiningCustomerState.Eating ||
            currentCustomer.CurrentCourseIndex != expectedCourseIndex ||
            currentCustomer.RemainingEatingSeconds > 0f)
        {
            // El evento pudo quedar obsoleto por una restauración o por otra
            // mutación válida. Se considera idempotente y no se fuerza nada.
            error = string.Empty;
            return true;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                normalizedOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica durante el consumo.";
            return false;
        }

        BistroBuilderCustomerDiningOrderRuntime candidate =
            currentRuntime.Clone();

        if (!candidate.TryGetCustomer(
                normalizedCustomerId,
                out BistroBuilderCustomerDiningCustomerRuntime candidateCustomer
            ) ||
            candidateCustomer == null)
        {
            error = "No se pudo clonar el cliente de consumo.";
            return false;
        }

        lineIdBuffer.Clear();

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[lineIndex];

            if (!BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                    line,
                    normalizedCustomerId
                ) ||
                line.CourseIndex != expectedCourseIndex)
            {
                continue;
            }

            if (!BistroBuilderCustomerDiningPolicy.IsLineReadyForCustomer(line))
            {
                error = "El cliente intentó terminar un pase con la línea " +
                        line.LineId + " todavía en " + line.State + ".";
                return false;
            }

            if (line.State != BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                candidateCustomer.AddConsumedLineClaim(line.LineId);
            }

            lineIdBuffer.Add(line.LineId);
        }

        if (lineIdBuffer.Count == 0)
        {
            error = "El pase del cliente no contiene líneas canónicas.";
            return false;
        }

        if (TryFindNextPendingCourse(
                canonical,
                candidateCustomer,
                out int nextCourse,
                out bool hasFailedLine
            ))
        {
            candidateCustomer.SetWaitingForCourse(nextCourse);
        }
        else if (hasFailedLine)
        {
            candidateCustomer.SetFailed();
        }
        else
        {
            candidateCustomer.SetCompleted();
        }

        candidate.IncrementRevision();

        if (!candidate.TryValidate(out error))
        {
            return false;
        }

        if (!ValidateRuntimeAgainstCanonical(candidate, canonical, out error))
        {
            return false;
        }

        consumedLineBuffer.Clear();
        sharedProgressBuffer.Clear();

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[lineIndex];

            if (line.State != BistroBuilderCanonicalOrderLineState.Served ||
                !BistroBuilderCustomerDiningPolicy.ContainsNormalizedId(
                    lineIdBuffer,
                    line.LineId
                ))
            {
                continue;
            }

            if (HaveAllConsumersClaimedLine(candidate, line, out error))
            {
                consumedLineBuffer.Add(line.LineId);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                return false;
            }
            else if (line.IsShared)
            {
                int completedConsumers = CountLineConsumerClaims(
                    candidate,
                    line
                );

                sharedProgressBuffer.Add(
                    new SharedLineProgressKey(
                        line.LineId,
                        line.CourseIndex,
                        completedConsumers,
                        line.ConsumerCustomerIds.Count
                    )
                );
            }
        }

        // La operación canónica es atómica. Si falla, el runtime candidato no
        // sustituye al vigente y no quedan reclamaciones parciales.
        if (consumedLineBuffer.Count > 0)
        {
            BistroBuilderCanonicalOrderOperationResult consumeResult =
                canonicalOrderService.TryConsumeServedLines(
                    normalizedOrderId,
                    consumedLineBuffer,
                    normalizedCustomerId
                );

            if (!consumeResult.Succeeded)
            {
                error = "No se pudieron consumir las líneas del cliente. " +
                        consumeResult.Message;
                return false;
            }
        }

        ReplaceRuntime(currentRuntime, candidate);
        Revision++;

        PublishChange(
            BistroBuilderCustomerDiningChangeType.CustomerCompletedCourse,
            normalizedOrderId,
            normalizedCustomerId,
            string.Empty,
            "Cliente terminó el pase " + expectedCourseIndex + "."
        );

        for (int index = 0; index < sharedProgressBuffer.Count; index++)
        {
            SharedLineProgressKey progress = sharedProgressBuffer[index];
            PublishSharedProgress(
                normalizedOrderId,
                normalizedCustomerId,
                progress.LineId,
                progress.CourseIndex,
                progress.CompletedConsumerCount,
                progress.TotalConsumerCount
            );
        }

        for (int index = 0; index < consumedLineBuffer.Count; index++)
        {
            PublishChange(
                BistroBuilderCustomerDiningChangeType.LineConsumed,
                normalizedOrderId,
                normalizedCustomerId,
                consumedLineBuffer[index],
                "Línea consumida por todos sus consumidores."
            );
        }

        QueueReconciliation(normalizedOrderId);
        error = string.Empty;
        return true;
    }

    private void ReconcileOrder(string orderId)
    {
        if (!runtimeByOrderId.TryGetValue(
                orderId,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            return;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                orderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            return;
        }

        if (!TryRecoverFullyClaimedServedLines(
                runtime,
                canonical,
                out BistroBuilderCanonicalOrder reconciledCanonical,
                out string recoveryError
            ))
        {
            Debug.LogError(
                "No se pudieron reconciliar reclamaciones de consumo " +
                "persistidas para " + orderId + ". " + recoveryError,
                this
            );
            return;
        }

        canonical = reconciledCanonical;

        if (canonical.State == BistroBuilderCanonicalOrderState.Cancelled)
        {
            MarkAllNonTerminalCustomers(runtime, true);
            return;
        }

        if (canonical.State == BistroBuilderCanonicalOrderState.Failed)
        {
            MarkAllNonTerminalCustomers(runtime, false);
            return;
        }

        int startedCount = 0;

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            BistroBuilderCustomerDiningCustomerRuntime customer =
                runtime.Customers[index];

            if (customer == null || customer.IsTerminal ||
                customer.State == BistroBuilderCustomerDiningCustomerState.Eating)
            {
                continue;
            }

            if (!TryFindNextPendingCourse(
                    canonical,
                    customer,
                    out int courseIndex,
                    out bool hasFailedLine
                ))
            {
                if (hasFailedLine)
                {
                    customer.SetFailed();
                }
                else
                {
                    customer.SetCompleted();
                }

                runtime.IncrementRevision();
                Revision++;
                continue;
            }

            if (customer.CurrentCourseIndex != courseIndex)
            {
                customer.SetWaitingForCourse(courseIndex);
                runtime.IncrementRevision();
                Revision++;
            }

            if (!AreAllCustomerCourseLinesReady(
                    canonical,
                    customer.CustomerId,
                    courseIndex
                ))
            {
                continue;
            }

            if (customer.TryStartCourse(
                    courseIndex,
                    CalculateEatingDuration(runtime, customer),
                    out _
                ))
            {
                runtime.IncrementRevision();
                Revision++;
                startedCount++;

                PublishChange(
                    BistroBuilderCustomerDiningChangeType
                        .CustomerStartedCourse,
                    runtime.OrderId,
                    customer.CustomerId,
                    string.Empty,
                    "Cliente comienza a comer el pase " + courseIndex + "."
                );
            }
        }

        if (startedCount > 0 || AreAllCustomersStartedOrCompleted(runtime))
        {
            TryApplyCoarseEatingState(runtime);
        }

        if (runtime.AllCustomersCompleted)
        {
            TryRequestBill(runtime);
        }
    }

    /// <summary>
    /// Repara el pequeño intervalo transaccional posible entre las
    /// reclamaciones persistidas de todos los consumidores y la mutación
    /// canónica Served -> Consumed. Es especialmente importante al restaurar
    /// service.runtime después de una interrupción entre ambas escrituras.
    /// </summary>
    private bool TryRecoverFullyClaimedServedLines(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCanonicalOrder canonical,
        out BistroBuilderCanonicalOrder refreshedCanonical,
        out string error
    )
    {
        refreshedCanonical = canonical;
        fullyClaimedLineBuffer.Clear();

        if (runtime == null || canonical == null)
        {
            error = "El runtime o la comanda de reconciliación son nulos.";
            return false;
        }

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[lineIndex];

            if (line == null ||
                line.State != BistroBuilderCanonicalOrderLineState.Served)
            {
                continue;
            }

            if (HaveAllConsumersClaimedLine(runtime, line, out error))
            {
                fullyClaimedLineBuffer.Add(line.LineId);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                return false;
            }
        }

        if (fullyClaimedLineBuffer.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryConsumeServedLines(
                canonical.OrderId,
                fullyClaimedLineBuffer,
                "customer_dining_reconciliation"
            );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                canonical.OrderId,
                out refreshedCanonical
            ) ||
            refreshedCanonical == null)
        {
            error = "La comanda desapareció tras reconciliar el consumo.";
            return false;
        }

        for (int index = 0;
             index < fullyClaimedLineBuffer.Count;
             index++)
        {
            PublishChange(
                BistroBuilderCustomerDiningChangeType.LineConsumed,
                canonical.OrderId,
                string.Empty,
                fullyClaimedLineBuffer[index],
                "Línea consumida al reconciliar reclamaciones persistidas."
            );
        }

        error = string.Empty;
        return true;
    }

    private void TryApplyCoarseEatingState(
        BistroBuilderCustomerDiningOrderRuntime runtime
    )
    {
        if (!AreAllCustomersStartedOrCompleted(runtime) ||
            !TryGetLegacyOrder(runtime.OrderId, out RestaurantOrder order) ||
            order == null)
        {
            return;
        }

        RestaurantTable table = order.Table;
        CustomerGroup group = order.CustomerGroup;

        if (table == null || group == null)
        {
            return;
        }

        bool changed = false;

        if (table.CurrentState == TableState.WaitingForFood)
        {
            table.SetState(TableState.Eating);
            changed = true;
        }

        if (group.CurrentState == CustomerGroupState.WaitingForFood)
        {
            group.SetState(CustomerGroupState.Eating);
            changed = true;
        }

        if (changed)
        {
            PublishChange(
                BistroBuilderCustomerDiningChangeType
                    .GroupCoarseStateChanged,
                runtime.OrderId,
                string.Empty,
                string.Empty,
                "La fachada de grupo pasa a Eating; el consumo continúa " +
                "siendo individual."
            );
        }
    }

    private void TryRequestBill(
        BistroBuilderCustomerDiningOrderRuntime runtime
    )
    {
        if (runtime == null || runtime.BillRequested ||
            !runtime.AllCustomersCompleted)
        {
            return;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                runtime.OrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null ||
            !AreAllLinesResolvedForBill(canonical))
        {
            return;
        }

        if (!TryGetLegacyOrder(runtime.OrderId, out RestaurantOrder order) ||
            order == null)
        {
            return;
        }

        if (!lineExecutionService.TrySynchronizeLegacyOrder(
                order,
                out _,
                out _,
                out string synchronizationError
            ))
        {
            Debug.LogError(
                "No se pudo sincronizar la comanda antes de la cuenta. " +
                synchronizationError,
                this
            );
            return;
        }

        if (order.CurrentState != OrderState.Served ||
            order.Table == null ||
            order.CustomerGroup == null)
        {
            return;
        }

        runtime.MarkBillRequested();
        Revision++;

        order.Table.SetState(TableState.WaitingForBill);
        order.CustomerGroup.SetState(CustomerGroupState.WaitingForBill);

        PublishChange(
            BistroBuilderCustomerDiningChangeType.BillReady,
            runtime.OrderId,
            string.Empty,
            string.Empty,
            "Todos los clientes terminaron; la mesa solicita la cuenta."
        );
    }

    private bool TryCreateRuntime(
        RestaurantOrder legacy,
        BistroBuilderCanonicalOrder canonical,
        out BistroBuilderCustomerDiningOrderRuntime runtime,
        out string error
    )
    {
        runtime = null;
        customerCreationBuffer.Clear();

        HashSet<string> allCustomerIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> failedCustomers =
            new HashSet<string>(StringComparer.Ordinal);

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[lineIndex];

            if (line == null || line.ConsumerCustomerIds == null ||
                line.ConsumerCustomerIds.Count == 0)
            {
                error = "La comanda contiene una línea sin consumidores.";
                return false;
            }

            for (int consumerIndex = 0;
                 consumerIndex < line.ConsumerCustomerIds.Count;
                 consumerIndex++)
            {
                string customerId = BistroBuilderOrderIdUtility.Normalize(
                    line.ConsumerCustomerIds[consumerIndex]
                );

                if (!BistroBuilderOrderIdUtility.IsValid(customerId))
                {
                    error = "La comanda contiene un CustomerId inválido.";
                    return false;
                }

                allCustomerIds.Add(customerId);

                if (line.State == BistroBuilderCanonicalOrderLineState.Failed)
                {
                    failedCustomers.Add(customerId);
                }
            }
        }

        if (allCustomerIds.Count != legacy.CustomerGroup.GroupSize)
        {
            error = "El número de CustomerId de la comanda no coincide con " +
                    "el tamaño del grupo.";
            return false;
        }

        List<string> sortedCustomerIds =
            new List<string>(allCustomerIds);
        sortedCustomerIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < sortedCustomerIds.Count; index++)
        {
            string customerId = sortedCustomerIds[index];
            int firstCourse = 0;
            bool foundPendingCourse = false;

            for (int lineIndex = 0;
                 lineIndex < canonical.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line =
                    canonical.Lines[lineIndex];

                if (!BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                        line,
                        customerId
                    ) ||
                    line.State == BistroBuilderCanonicalOrderLineState.Cancelled ||
                    line.State == BistroBuilderCanonicalOrderLineState.Consumed ||
                    line.State == BistroBuilderCanonicalOrderLineState.Failed)
                {
                    continue;
                }

                if (!foundPendingCourse || line.CourseIndex < firstCourse)
                {
                    firstCourse = line.CourseIndex;
                    foundPendingCourse = true;
                }
            }

            BistroBuilderCustomerDiningCustomerRuntime customer =
                new BistroBuilderCustomerDiningCustomerRuntime(
                    customerId,
                    Mathf.Clamp(firstCourse, 0, 20)
                );

            for (int lineIndex = 0;
                 lineIndex < canonical.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line =
                    canonical.Lines[lineIndex];

                if (line.State ==
                        BistroBuilderCanonicalOrderLineState.Consumed &&
                    BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                        line,
                        customerId
                    ))
                {
                    customer.AddConsumedLineClaim(line.LineId);
                }
            }

            if (failedCustomers.Contains(customerId))
            {
                customer.SetFailed();
            }
            else if (!foundPendingCourse)
            {
                customer.SetCompleted();
            }
            else if (firstCourse != customer.CurrentCourseIndex)
            {
                customer.SetWaitingForCourse(firstCourse);
            }

            customerCreationBuffer.Add(customer);
        }

        runtime = new BistroBuilderCustomerDiningOrderRuntime(
            canonical.OrderId,
            legacy.OrderId,
            canonical.CustomerGroupReferenceId,
            canonical.TableReferenceId,
            customerCreationBuffer
        );

        if (!runtime.TryValidate(out error))
        {
            runtime = null;
            return false;
        }

        if (!ValidateRuntimeAgainstCanonical(runtime, canonical, out error))
        {
            runtime = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateRuntimeAgainstCanonical(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCanonicalOrder canonical,
        out string error
    )
    {
        if (runtime == null || canonical == null)
        {
            error = "No se puede validar un runtime o comanda nulos.";
            return false;
        }

        if (!string.Equals(
                runtime.OrderId,
                canonical.OrderId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                runtime.CustomerGroupReferenceId,
                canonical.CustomerGroupReferenceId,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                runtime.TableReferenceId,
                canonical.TableReferenceId,
                StringComparison.Ordinal
            ))
        {
            error = "Las referencias del consumo no coinciden con la comanda.";
            return false;
        }

        HashSet<string> canonicalCustomers =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> canonicalLineIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[lineIndex];
            canonicalLineIds.Add(line.LineId);

            for (int consumerIndex = 0;
                 consumerIndex < line.ConsumerCustomerIds.Count;
                 consumerIndex++)
            {
                canonicalCustomers.Add(
                    BistroBuilderOrderIdUtility.Normalize(
                        line.ConsumerCustomerIds[consumerIndex]
                    )
                );
            }
        }

        if (canonicalCustomers.Count != runtime.Customers.Count)
        {
            error = "El runtime no contiene todos los clientes canónicos.";
            return false;
        }

        for (int customerIndex = 0;
             customerIndex < runtime.Customers.Count;
             customerIndex++)
        {
            BistroBuilderCustomerDiningCustomerRuntime customer =
                runtime.Customers[customerIndex];

            if (!canonicalCustomers.Contains(customer.CustomerId))
            {
                error = "El runtime contiene un cliente ajeno a la comanda.";
                return false;
            }

            for (int lineIndex = 0;
                 lineIndex < customer.ConsumedLineIds.Count;
                 lineIndex++)
            {
                string lineId = customer.ConsumedLineIds[lineIndex];

                if (!canonicalLineIds.Contains(lineId) ||
                    !canonical.TryGetLine(
                        lineId,
                        out BistroBuilderCanonicalOrderLine line
                    ) ||
                    !BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                        line,
                        customer.CustomerId
                    ))
                {
                    error = "El cliente reclama una línea que no consume.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryFindNextPendingCourse(
        BistroBuilderCanonicalOrder canonical,
        BistroBuilderCustomerDiningCustomerRuntime customer,
        out int courseIndex,
        out bool hasFailedLine
    )
    {
        courseIndex = int.MaxValue;
        hasFailedLine = false;

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (!BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                    line,
                    customer.CustomerId
                ))
            {
                continue;
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Failed)
            {
                hasFailedLine = true;
                continue;
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Cancelled ||
                customer.HasConsumedLine(line.LineId))
            {
                continue;
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Consumed)
            {
                continue;
            }

            if (line.CourseIndex < courseIndex)
            {
                courseIndex = line.CourseIndex;
            }
        }

        if (courseIndex == int.MaxValue)
        {
            courseIndex = 0;
            return false;
        }

        return true;
    }

    private static bool AreAllCustomerCourseLinesReady(
        BistroBuilderCanonicalOrder canonical,
        string customerId,
        int courseIndex
    )
    {
        bool found = false;

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (!BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                    line,
                    customerId
                ) ||
                line.CourseIndex != courseIndex)
            {
                continue;
            }

            found = true;

            if (!BistroBuilderCustomerDiningPolicy.IsLineReadyForCustomer(line))
            {
                return false;
            }
        }

        return found;
    }

    private static bool HaveAllConsumersClaimedLine(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCanonicalOrderLine line,
        out string error
    )
    {
        for (int index = 0;
             index < line.ConsumerCustomerIds.Count;
             index++)
        {
            string customerId = line.ConsumerCustomerIds[index];

            if (!runtime.TryGetCustomer(
                    customerId,
                    out BistroBuilderCustomerDiningCustomerRuntime customer
                ) ||
                customer == null)
            {
                error = "No existe el consumidor " + customerId +
                        " de la línea compartida.";
                return false;
            }

            if (!customer.HasConsumedLine(line.LineId))
            {
                error = string.Empty;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool AreAllCustomersStartedOrCompleted(
        BistroBuilderCustomerDiningOrderRuntime runtime
    )
    {
        if (runtime == null || runtime.Customers.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            BistroBuilderCustomerDiningCustomerState state =
                runtime.Customers[index].State;

            if (state == BistroBuilderCustomerDiningCustomerState.WaitingForDish)
            {
                return false;
            }

            if (state == BistroBuilderCustomerDiningCustomerState.Failed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreAllLinesResolvedForBill(
        BistroBuilderCanonicalOrder canonical
    )
    {
        if (canonical == null || canonical.Lines.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            if (!BistroBuilderCustomerDiningPolicy.IsLineResolvedForBill(
                    canonical.Lines[index]
                ))
            {
                return false;
            }
        }

        return true;
    }

    private float CalculateEatingDuration(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCustomerDiningCustomerRuntime customer
    )
    {
        int ordinal = 0;

        if (runtime != null && customer != null)
        {
            for (int index = 0; index < runtime.Customers.Count; index++)
            {
                if (runtime.Customers[index] != null &&
                    string.Equals(
                        runtime.Customers[index].CustomerId,
                        customer.CustomerId,
                        StringComparison.Ordinal
                    ))
                {
                    ordinal = index;
                    break;
                }
            }
        }

        return Mathf.Max(
            0.1f,
            defaultEatingDurationSeconds +
            ordinal * perCustomerEatingDurationOffsetSeconds
        );
    }

    private static int CountLineConsumerClaims(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCanonicalOrderLine line
    )
    {
        if (runtime == null || line == null ||
            line.ConsumerCustomerIds == null)
        {
            return 0;
        }

        int count = 0;

        for (int index = 0;
             index < line.ConsumerCustomerIds.Count;
             index++)
        {
            if (runtime.TryGetCustomer(
                    line.ConsumerCustomerIds[index],
                    out BistroBuilderCustomerDiningCustomerRuntime customer
                ) &&
                customer != null &&
                customer.HasConsumedLine(line.LineId))
            {
                count++;
            }
        }

        return count;
    }

    private int CountEatingCustomers(string orderId)
    {
        if (!runtimeByOrderId.TryGetValue(
                orderId,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            return 0;
        }

        int count = 0;

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            if (runtime.Customers[index].State ==
                BistroBuilderCustomerDiningCustomerState.Eating)
            {
                count++;
            }
        }

        return count;
    }

    private void MarkAllNonTerminalCustomers(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        bool cancelled
    )
    {
        bool changed = false;

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            BistroBuilderCustomerDiningCustomerRuntime customer =
                runtime.Customers[index];

            if (customer.IsTerminal)
            {
                continue;
            }

            if (cancelled)
            {
                customer.SetCancelled();
            }
            else
            {
                customer.SetFailed();
            }

            changed = true;
        }

        if (changed)
        {
            runtime.IncrementRevision();
            Revision++;
        }
    }

    private void ReplaceRuntime(
        BistroBuilderCustomerDiningOrderRuntime current,
        BistroBuilderCustomerDiningOrderRuntime candidate
    )
    {
        int index = activeOrders.IndexOf(current);

        if (index < 0)
        {
            throw new InvalidOperationException(
                "El runtime de consumo no figura en la colección activa."
            );
        }

        activeOrders[index] = candidate;
        runtimeByOrderId[candidate.OrderId] = candidate;
    }

    private bool TryGetLegacyOrder(
        string canonicalOrderId,
        out RestaurantOrder order
    )
    {
        string normalized =
            BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (legacyByOrderId.TryGetValue(normalized, out order) &&
            order != null)
        {
            return true;
        }

        if (orderSystem == null)
        {
            order = null;
            return false;
        }

        IReadOnlyList<RestaurantOrder> activeLegacyOrders =
            orderSystem.ActiveOrders;

        for (int index = 0; index < activeLegacyOrders.Count; index++)
        {
            RestaurantOrder candidate = activeLegacyOrders[index];

            if (candidate != null &&
                string.Equals(
                    candidate.CanonicalOrderId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                legacyByOrderId[normalized] = candidate;
                order = candidate;
                return true;
            }
        }

        order = null;
        return false;
    }

    private bool TryValidateLinkedLegacyOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (order == null)
        {
            error = "La comanda legacy es nula.";
            return false;
        }

        if (!order.HasCanonicalOrder)
        {
            error = "La comanda legacy no está enlazada a una comanda canónica.";
            return false;
        }

        if (order.CustomerGroup == null || order.Table == null)
        {
            error = "La comanda legacy no conserva grupo o mesa.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No existe la comanda canónica enlazada.";
            return false;
        }

        string expectedGroup =
            BistroBuilderServiceOrderIdentityUtility.BuildGroupReference(
                order.CustomerGroup.GroupId
            );
        string expectedTable =
            BistroBuilderServiceOrderIdentityUtility.BuildTableReference(
                order.Table.TableId
            );

        if (!string.Equals(
                canonical.CustomerGroupReferenceId,
                expectedGroup,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                canonical.TableReferenceId,
                expectedTable,
                StringComparison.Ordinal
            ))
        {
            error = "La comanda legacy y la canónica no conservan las mismas " +
                    "referencias.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        // 367H: el consumo en barra tiene su propia autoridad y no puede
        // forzarse a través de una RestaurantTable inexistente.
        if (order != null && order.HasBarDestination)
        {
            return;
        }

        if (!TryRegisterOrder(order, out string error))
        {
            Debug.LogError(
                "No se pudo registrar el consumo individual. " + error,
                this
            );
        }
    }

    private void HandleOrderCompleted(RestaurantOrder order)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        RemoveRuntime(order, false);
    }

    private void HandleOrderCancelled(RestaurantOrder order)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        RemoveRuntime(order, true);
    }

    private void HandleCanonicalOrdersChanged(
        BistroBuilderCanonicalOrderChangedEvent change
    )
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        if (BistroBuilderOrderIdUtility.IsValid(change.OrderId))
        {
            QueueReconciliation(change.OrderId);
        }
        else
        {
            QueueAllOrdersForReconciliation();
        }

        // Las mutaciones canónicas publican eventos de forma síncrona. Aquí
        // solo se encola trabajo: TryNotifyLineServed, AdvanceDiningTime o el
        // siguiente Update realizan el drenaje fuera de la pila de mutación.
        // Esta regla evita repetir el defecto de reentrada corregido en 367D1.
    }

    private void RemoveRuntime(RestaurantOrder order, bool cancelled)
    {
        if (order == null || !order.HasCanonicalOrder)
        {
            return;
        }

        string orderId = order.CanonicalOrderId;

        if (!runtimeByOrderId.TryGetValue(
                orderId,
                out BistroBuilderCustomerDiningOrderRuntime runtime
            ))
        {
            legacyByOrderId.Remove(orderId);
            return;
        }

        if (cancelled)
        {
            MarkAllNonTerminalCustomers(runtime, true);
        }

        runtimeByOrderId.Remove(orderId);
        legacyByOrderId.Remove(orderId);
        activeOrders.Remove(runtime);
        pendingReconciliationOrderIds.Remove(orderId);
        Revision++;

        PublishChange(
            BistroBuilderCustomerDiningChangeType.OrderRemoved,
            orderId,
            string.Empty,
            string.Empty,
            "Sesión de consumo retirada del runtime activo."
        );
    }

    private void Subscribe()
    {
        if (subscriptionsActive)
        {
            return;
        }

        if (orderSystem != null)
        {
            orderSystem.OrderCreated -= HandleOrderCreated;
            orderSystem.OrderCreated += HandleOrderCreated;
            orderSystem.OrderCompleted -= HandleOrderCompleted;
            orderSystem.OrderCompleted += HandleOrderCompleted;
            orderSystem.OrderCancelled -= HandleOrderCancelled;
            orderSystem.OrderCancelled += HandleOrderCancelled;
        }

        if (canonicalOrderService != null)
        {
            canonicalOrderService.OrdersChanged -=
                HandleCanonicalOrdersChanged;
            canonicalOrderService.OrdersChanged +=
                HandleCanonicalOrdersChanged;
        }

        subscriptionsActive = true;
    }

    private void Unsubscribe()
    {
        if (!subscriptionsActive)
        {
            return;
        }

        if (orderSystem != null)
        {
            orderSystem.OrderCreated -= HandleOrderCreated;
            orderSystem.OrderCompleted -= HandleOrderCompleted;
            orderSystem.OrderCancelled -= HandleOrderCancelled;
        }

        if (canonicalOrderService != null)
        {
            canonicalOrderService.OrdersChanged -=
                HandleCanonicalOrdersChanged;
        }

        subscriptionsActive = false;
    }

    private void ReconcileExistingLegacyOrders(bool registerMissing = true)
    {
        if (orderSystem == null)
        {
            return;
        }

        IReadOnlyList<RestaurantOrder> activeLegacyOrders =
            orderSystem.ActiveOrders;

        for (int index = 0; index < activeLegacyOrders.Count; index++)
        {
            RestaurantOrder order = activeLegacyOrders[index];

            if (order == null || !order.HasCanonicalOrder)
            {
                continue;
            }

            legacyByOrderId[order.CanonicalOrderId] = order;

            if (registerMissing &&
                !runtimeByOrderId.ContainsKey(order.CanonicalOrderId))
            {
                TryRegisterOrder(order, out _);
            }
        }
    }

    private void QueueReconciliation(string orderId)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            pendingReconciliationOrderIds.Add(normalized);
        }
    }

    private void QueueAllOrdersForReconciliation()
    {
        for (int index = 0; index < activeOrders.Count; index++)
        {
            if (activeOrders[index] != null)
            {
                pendingReconciliationOrderIds.Add(
                    activeOrders[index].OrderId
                );
            }
        }
    }

    /// <summary>
    /// Guardia explícita contra reentrada síncrona.
    ///
    /// Las mutaciones canónicas publican eventos antes de retornar. Esos eventos
    /// solo encolan una nueva reconciliación; nunca entran recursivamente sobre
    /// el runtime que todavía se está sustituyendo.
    /// </summary>
    private void DrainPendingReconciliations()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            mutationScopeActive)
        {
            return;
        }

        mutationScopeActive = true;

        try
        {
            int safety = 0;

            while (pendingReconciliationOrderIds.Count > 0 && safety < 256)
            {
                safety++;
                pendingDrainBuffer.Clear();
                pendingDrainBuffer.AddRange(pendingReconciliationOrderIds);
                pendingReconciliationOrderIds.Clear();
                pendingDrainBuffer.Sort(StringComparer.Ordinal);

                for (int index = 0;
                     index < pendingDrainBuffer.Count;
                     index++)
                {
                    ReconcileOrder(pendingDrainBuffer[index]);
                }
            }

            if (pendingReconciliationOrderIds.Count > 0)
            {
                Debug.LogError(
                    "El consumo individual superó el límite de " +
                    "reconciliaciones de seguridad.",
                    this
                );
                pendingReconciliationOrderIds.Clear();
            }
        }
        finally
        {
            mutationScopeActive = false;
        }
    }

    private bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        return RebuildRuntimeIndex(out error);
    }

    private void ResolveDependencies()
    {
        if (orderSystem == null)
        {
            TryGetComponent(out orderSystem);
        }

        if (canonicalOrderService == null)
        {
            TryGetComponent(out canonicalOrderService);
        }

        if (lineExecutionService == null)
        {
            TryGetComponent(out lineExecutionService);
        }
    }

    private void PublishSharedProgress(
        string orderId,
        string customerId,
        string lineId,
        int courseIndex,
        int completedConsumerCount,
        int totalConsumerCount
    )
    {
        BistroBuilderCustomerDiningChangedEvent change =
            new BistroBuilderCustomerDiningChangedEvent(
                BistroBuilderCustomerDiningChangeType.SharedLineProgressed,
                orderId,
                customerId,
                lineId,
                courseIndex,
                completedConsumerCount,
                totalConsumerCount,
                Revision,
                "Progreso de plato compartido: " +
                completedConsumerCount + "/" + totalConsumerCount + "."
            );

        DiningChanged?.Invoke(change);

        if (logTransitions)
        {
            Debug.Log(
                "367F consumo compartido: SharedLineProgressed. OrderId: " +
                change.OrderId + ". CustomerId: " + change.CustomerId +
                ". LineId: " + change.LineId + ". Course: " +
                change.CourseIndex + ". Progreso: " +
                change.CompletedConsumerCount + "/" +
                change.TotalConsumerCount + ".",
                this
            );
        }
    }

    private void PublishChange(
        BistroBuilderCustomerDiningChangeType changeType,
        string orderId,
        string customerId,
        string lineId,
        string description
    )
    {
        BistroBuilderCustomerDiningChangedEvent change =
            new BistroBuilderCustomerDiningChangedEvent(
                changeType,
                orderId,
                customerId,
                lineId,
                Revision,
                description
            );

        DiningChanged?.Invoke(change);

        if (!logTransitions)
        {
            return;
        }

        Debug.Log(
            "367F consumo individual: " + changeType +
            ". OrderId: " +
            (string.IsNullOrEmpty(change.OrderId) ? "-" : change.OrderId) +
            ". CustomerId: " +
            (string.IsNullOrEmpty(change.CustomerId)
                ? "-"
                : change.CustomerId) +
            ". LineId: " +
            (string.IsNullOrEmpty(change.LineId) ? "-" : change.LineId) +
            ". " + change.Description,
            this
        );
    }

    private static int CompareRuntimes(
        BistroBuilderCustomerDiningOrderRuntime first,
        BistroBuilderCustomerDiningOrderRuntime second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int legacyComparison =
            first.LegacyOrderId.CompareTo(second.LegacyOrderId);

        return legacyComparison != 0
            ? legacyComparison
            : string.Compare(
                first.OrderId,
                second.OrderId,
                StringComparison.Ordinal
            );
    }

    private readonly struct SharedLineProgressKey
    {
        public string LineId { get; }
        public int CourseIndex { get; }
        public int CompletedConsumerCount { get; }
        public int TotalConsumerCount { get; }

        public SharedLineProgressKey(
            string lineId,
            int courseIndex,
            int completedConsumerCount,
            int totalConsumerCount
        )
        {
            LineId = BistroBuilderOrderIdUtility.Normalize(lineId);
            CourseIndex = courseIndex;
            CompletedConsumerCount = completedConsumerCount;
            TotalConsumerCount = totalConsumerCount;
        }
    }

    private readonly struct CustomerCompletionKey
    {
        public string OrderId { get; }
        public string CustomerId { get; }
        public int CourseIndex { get; }

        public CustomerCompletionKey(
            string orderId,
            string customerId,
            int courseIndex
        )
        {
            OrderId = orderId;
            CustomerId = customerId;
            CourseIndex = courseIndex;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        defaultEatingDurationSeconds = Mathf.Max(
            0.1f,
            defaultEatingDurationSeconds
        );
        perCustomerEatingDurationOffsetSeconds = Mathf.Clamp(
            perCustomerEatingDurationOffsetSeconds,
            0f,
            60f
        );
    }
#endif
}
