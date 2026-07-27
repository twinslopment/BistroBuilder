using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad operativa de liberación de pases y coordinación de platos
/// compartidos.
///
/// Las líneas canónicas siguen siendo la única autoridad de estado. Este
/// servicio decide qué líneas Submitted pueden pasar a Queued y conserva una
/// proyección persistible de las decisiones tomadas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Orders/Course And Sharing Service")]
public sealed class BistroBuilderCourseAndSharingService : MonoBehaviour
{
    public const string RuntimeRevision = "367F";

    [Header("Dependencias")]

    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [SerializeField]
    private BistroBuilderOrderCompositionService compositionService;

    [SerializeField]
    private BistroBuilderCustomerDiningService customerDiningService;

    [Header("Estado runtime persistible")]

    [SerializeField]
    private List<BistroBuilderCourseOrderRuntime> activeOrders =
        new List<BistroBuilderCourseOrderRuntime>();

    [Header("Depuración")]

    [SerializeField]
    private bool logTransitions = true;

    private readonly Dictionary<string, BistroBuilderCourseOrderRuntime>
        runtimeByOrderId =
            new Dictionary<string, BistroBuilderCourseOrderRuntime>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, RestaurantOrder> legacyByOrderId =
        new Dictionary<string, RestaurantOrder>(StringComparer.Ordinal);

    private readonly HashSet<string> pendingEvaluationOrderIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly List<string> pendingBuffer = new List<string>(16);
    private readonly List<string> releaseLineBuffer = new List<string>(32);
    private readonly List<string> manualLineBuffer = new List<string>(32);

    private bool initialized;
    private bool subscriptionsActive;
    private bool evaluationScopeActive;

    public event Action<BistroBuilderCourseAndSharingChangedEvent>
        CourseAndSharingChanged;

    public OrderSystem OrderSystem => orderSystem;
    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;
    public BistroBuilderOrderCompositionService CompositionService =>
        compositionService;
    public BistroBuilderCustomerDiningService CustomerDiningService =>
        customerDiningService;
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
        QueueAllOrdersForEvaluation();
        DrainPendingEvaluations();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            DrainPendingEvaluations();
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (orderSystem == null)
        {
            error = "Falta OrderSystem en la coordinación de pases.";
            return false;
        }

        if (canonicalOrderService == null)
        {
            error = "Falta la autoridad canónica en la coordinación de pases.";
            return false;
        }

        if (compositionService == null)
        {
            error = "Falta BistroBuilderOrderCompositionService.";
            return false;
        }

        if (customerDiningService == null)
        {
            error = "Falta BistroBuilderCustomerDiningService.";
            return false;
        }

        // Se evita llamar a OrderSystem.ValidateConfiguration porque este
        // servicio también es dependencia de la integración 367C/367F.
        // Validar ambos de forma recursiva crearía un ciclo artificial.
        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!compositionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (activeOrders == null)
        {
            error = "La colección runtime de pases es nula.";
            return false;
        }

        for (int index = 0; index < activeOrders.Count; index++)
        {
            BistroBuilderCourseOrderRuntime runtime = activeOrders[index];

            if (runtime == null)
            {
                error = "La colección runtime 367F contiene una entrada nula.";
                return false;
            }

            if (!runtime.TryValidate(out error))
            {
                return false;
            }

            if (!canonicalOrderService.TryGetOrderSnapshot(
                    runtime.OrderId,
                    out BistroBuilderCanonicalOrder canonical
                ) ||
                canonical == null)
            {
                error = "No existe la comanda canónica del runtime 367F.";
                return false;
            }

            if (!ValidateRuntimeAgainstCanonical(runtime, canonical, out error))
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
            activeOrders = new List<BistroBuilderCourseOrderRuntime>();
        }

        runtimeByOrderId.Clear();
        legacyByOrderId.Clear();

        for (int index = 0; index < activeOrders.Count; index++)
        {
            BistroBuilderCourseOrderRuntime runtime = activeOrders[index];

            if (runtime == null)
            {
                error = "La colección runtime 367F contiene una entrada nula.";
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
                error = "Existe un OrderId duplicado en el runtime 367F.";
                initialized = false;
                return false;
            }

            runtimeByOrderId.Add(runtime.OrderId, runtime);
        }

        if (orderSystem != null)
        {
            IReadOnlyList<RestaurantOrder> legacyOrders =
                orderSystem.ActiveOrders;

            for (int index = 0; index < legacyOrders.Count; index++)
            {
                RestaurantOrder order = legacyOrders[index];

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
    /// Somete todas las líneas y libera únicamente el primer pase.
    /// La autoridad canónica aplica la operación sobre una copia profunda.
    /// </summary>
    public bool TrySubmitAndReleaseInitialCourse(
        RestaurantOrder order,
        string actorReferenceId,
        out BistroBuilderCanonicalOrderOperationResult result,
        out string error
    )
    {
        result = default;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (!TryValidateLinkedOrder(order, out error))
        {
            return false;
        }

        if (!TryRegisterOrder(order, out error))
        {
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica que debe liberarse.";
            return false;
        }

        if (!TryFindMinimumPendingCourse(
                canonical,
                out int initialCourseIndex
            ))
        {
            error = "La comanda no contiene un pase inicial pendiente.";
            return false;
        }

        result = canonicalOrderService.TrySubmitOrderAndReleaseCourse(
            canonical.OrderId,
            initialCourseIndex,
            actorReferenceId
        );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                canonical.OrderId,
                out BistroBuilderCanonicalOrder refreshed
            ) ||
            refreshed == null)
        {
            error = "La comanda desapareció tras liberar el pase inicial.";
            return false;
        }

        if (!runtimeByOrderId.TryGetValue(
                canonical.OrderId,
                out BistroBuilderCourseOrderRuntime runtime
            ))
        {
            error = "No existe el runtime 367F de la comanda liberada.";
            return false;
        }

        MarkReleasedLinesFromCanonical(runtime, refreshed);
        Revision++;

        PublishChange(
            BistroBuilderCourseAndSharingChangeType.InitialCourseReleased,
            canonical.OrderId,
            string.Empty,
            initialCourseIndex,
            0,
            0,
            "Pase inicial sometido y liberado hacia cocina."
        );

        QueueEvaluation(canonical.OrderId);
        error = string.Empty;
        return true;
    }

    public bool TryGetOrderRuntimeSnapshot(
        string orderId,
        out BistroBuilderCourseOrderRuntime snapshot
    )
    {
        snapshot = null;
        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!runtimeByOrderId.TryGetValue(
                normalized,
                out BistroBuilderCourseOrderRuntime runtime
            ) ||
            runtime == null)
        {
            return false;
        }

        snapshot = runtime.Clone();
        return true;
    }

    public int CopyOrderRuntimeSnapshotsTo(
        List<BistroBuilderCourseOrderRuntime> destination
    )
    {
        if (destination == null)
        {
            return 0;
        }

        destination.Clear();

        for (int index = 0; index < activeOrders.Count; index++)
        {
            if (activeOrders[index] != null)
            {
                destination.Add(activeOrders[index].Clone());
            }
        }

        destination.Sort(CompareRuntimes);
        return destination.Count;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderCourseAndSharingRuntimeSnapshot snapshot,
        out string error
    )
    {
        snapshot = new BistroBuilderCourseAndSharingRuntimeSnapshot
        {
            schemaVersion = 1,
            revision = Revision
        };

        for (int index = 0; index < activeOrders.Count; index++)
        {
            snapshot.orders.Add(activeOrders[index].Clone());
        }

        snapshot.orders.Sort(CompareRuntimes);

        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }

        return true;
    }

    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderCourseAndSharingRuntimeSnapshot snapshot,
        out string error
    )
    {
        if (snapshot == null)
        {
            error = "El snapshot 367F es nulo.";
            return false;
        }

        if (!snapshot.TryValidate(out error))
        {
            return false;
        }

        List<BistroBuilderCourseOrderRuntime> candidates =
            new List<BistroBuilderCourseOrderRuntime>(snapshot.orders.Count);

        for (int index = 0; index < snapshot.orders.Count; index++)
        {
            BistroBuilderCourseOrderRuntime candidate =
                snapshot.orders[index].Clone();

            if (!canonicalOrderService.TryGetOrderSnapshot(
                    candidate.OrderId,
                    out BistroBuilderCanonicalOrder canonical
                ) ||
                canonical == null ||
                !ValidateRuntimeAgainstCanonical(candidate, canonical, out error))
            {
                return false;
            }

            candidates.Add(candidate);
        }

        activeOrders = candidates;
        Revision = snapshot.revision;

        if (!RebuildRuntimeIndex(out error))
        {
            return false;
        }

        PublishChange(
            BistroBuilderCourseAndSharingChangeType.StateRestored,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            "Runtime de pases restaurado atómicamente."
        );

        QueueAllOrdersForEvaluation();
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Liberación manual para futuras acciones de camarero o jefe de sala.
    /// </summary>
    public bool TryReleaseCourseManually(
        string orderId,
        int courseIndex,
        string actorReferenceId,
        out string error
    )
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                courseIndex
            ))
        {
            error = "El pase manual indicado no es válido.";
            return false;
        }

        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!runtimeByOrderId.TryGetValue(
                normalized,
                out BistroBuilderCourseOrderRuntime runtime
            ))
        {
            error = "No existe el runtime 367F de la comanda.";
            return false;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                normalized,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica.";
            return false;
        }

        manualLineBuffer.Clear();

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null &&
                line.CourseIndex == courseIndex &&
                line.State == BistroBuilderCanonicalOrderLineState.Submitted)
            {
                manualLineBuffer.Add(line.LineId);
            }
        }

        if (manualLineBuffer.Count == 0)
        {
            error = "El pase no contiene líneas Submitted pendientes.";
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryReleaseSubmittedLines(
                normalized,
                manualLineBuffer,
                actorReferenceId
            );

        if (!result.Succeeded)
        {
            error = result.Message;
            return false;
        }

        if (!RefreshRuntimeAfterRelease(runtime, courseIndex, out error))
        {
            return false;
        }

        PublishReleasedLines(normalized, courseIndex, manualLineBuffer);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Punto determinista utilizado por editor, pruebas y restauración.
    /// </summary>
    public bool TryEvaluateOrderNow(string orderId, out string error)
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            error = "El OrderId de evaluación no es válido.";
            return false;
        }

        if (!runtimeByOrderId.ContainsKey(normalized))
        {
            error = "No existe un runtime 367F para la comanda indicada.";
            return false;
        }

        QueueEvaluation(normalized);
        DrainPendingEvaluations();
        error = string.Empty;
        return true;
    }

    private bool TryRegisterOrder(RestaurantOrder order, out string error)
    {
        if (!TryValidateLinkedOrder(order, out error))
        {
            return false;
        }

        legacyByOrderId[order.CanonicalOrderId] = order;

        if (runtimeByOrderId.ContainsKey(order.CanonicalOrderId))
        {
            error = string.Empty;
            return true;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "No se encontró la comanda canónica del runtime 367F.";
            return false;
        }

        if (!TryFindMinimumCourse(canonical, out int initialCourseIndex))
        {
            error = "La comanda no contiene pases válidos.";
            return false;
        }

        BistroBuilderCourseCoordinationPolicy policy =
            compositionService.CompositionProfile.CoordinationPolicy;

        BistroBuilderCourseOrderRuntime runtime =
            new BistroBuilderCourseOrderRuntime(
                canonical.OrderId,
                order.OrderId,
                policy,
                initialCourseIndex
            );

        MarkReleasedLinesFromCanonical(runtime, canonical);

        if (!runtime.TryValidate(out error) ||
            !ValidateRuntimeAgainstCanonical(runtime, canonical, out error))
        {
            return false;
        }

        activeOrders.Add(runtime);
        activeOrders.Sort(CompareRuntimes);
        runtimeByOrderId.Add(runtime.OrderId, runtime);
        Revision++;

        PublishChange(
            BistroBuilderCourseAndSharingChangeType.OrderRegistered,
            runtime.OrderId,
            string.Empty,
            runtime.InitialCourseIndex,
            0,
            0,
            "Sesión de pases y compartidos registrada."
        );

        error = string.Empty;
        return true;
    }

    private void EvaluateOrder(string orderId)
    {
        if (!runtimeByOrderId.TryGetValue(
                orderId,
                out BistroBuilderCourseOrderRuntime runtime
            ) ||
            runtime == null ||
            runtime.CoordinationPolicy ==
                BistroBuilderCourseCoordinationPolicy.Manual)
        {
            return;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                orderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null ||
            canonical.IsTerminal)
        {
            return;
        }

        releaseLineBuffer.Clear();
        int releasedCourseIndex = int.MaxValue;

        switch (runtime.CoordinationPolicy)
        {
            case BistroBuilderCourseCoordinationPolicy.PerTable:
                CollectPerTableReleaseLines(
                    canonical,
                    releaseLineBuffer,
                    out releasedCourseIndex
                );
                break;

            case BistroBuilderCourseCoordinationPolicy.PerCustomer:
                CollectPerCustomerReleaseLines(
                    canonical,
                    false,
                    releaseLineBuffer,
                    out releasedCourseIndex
                );
                break;

            case BistroBuilderCourseCoordinationPolicy.Hybrid:
                CollectPerCustomerReleaseLines(
                    canonical,
                    true,
                    releaseLineBuffer,
                    out releasedCourseIndex
                );
                break;
        }

        if (releaseLineBuffer.Count == 0 || releasedCourseIndex == int.MaxValue)
        {
            return;
        }

        BistroBuilderCanonicalOrderOperationResult result =
            canonicalOrderService.TryReleaseSubmittedLines(
                canonical.OrderId,
                releaseLineBuffer,
                "course_release_367f"
            );

        if (!result.Succeeded)
        {
            Debug.LogError(
                "367F no pudo liberar el siguiente pase. " + result.Message,
                this
            );
            return;
        }

        if (!RefreshRuntimeAfterRelease(
                runtime,
                releasedCourseIndex,
                out string error
            ))
        {
            Debug.LogError(error, this);
            return;
        }

        PublishReleasedLines(
            canonical.OrderId,
            releasedCourseIndex,
            releaseLineBuffer
        );
    }

    private void CollectPerTableReleaseLines(
        BistroBuilderCanonicalOrder canonical,
        List<string> destination,
        out int courseIndex
    )
    {
        courseIndex = FindMinimumSubmittedCourse(canonical);

        if (courseIndex == int.MaxValue ||
            !AreAllLowerCourseLinesResolved(canonical, courseIndex))
        {
            return;
        }

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null &&
                line.CourseIndex == courseIndex &&
                line.State == BistroBuilderCanonicalOrderLineState.Submitted)
            {
                destination.Add(line.LineId);
            }
        }
    }

    private void CollectPerCustomerReleaseLines(
        BistroBuilderCanonicalOrder canonical,
        bool hybrid,
        List<string> destination,
        out int courseIndex
    )
    {
        courseIndex = int.MaxValue;

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line == null ||
                line.State != BistroBuilderCanonicalOrderLineState.Submitted)
            {
                continue;
            }

            bool canRelease = hybrid && line.IsShared
                ? AreAllLowerCourseLinesResolved(canonical, line.CourseIndex)
                : AreAllConsumersReadyForCourse(
                    canonical,
                    line.ConsumerCustomerIds,
                    line.CourseIndex
                );

            if (!canRelease)
            {
                continue;
            }

            if (line.CourseIndex < courseIndex)
            {
                destination.Clear();
                courseIndex = line.CourseIndex;
            }

            if (line.CourseIndex == courseIndex)
            {
                destination.Add(line.LineId);
            }
        }
    }

    private static bool AreAllConsumersReadyForCourse(
        BistroBuilderCanonicalOrder canonical,
        IReadOnlyList<string> consumers,
        int targetCourseIndex
    )
    {
        if (consumers == null || consumers.Count == 0)
        {
            return false;
        }

        for (int consumerIndex = 0;
             consumerIndex < consumers.Count;
             consumerIndex++)
        {
            string customerId = consumers[consumerIndex];

            for (int lineIndex = 0;
                 lineIndex < canonical.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine lowerLine =
                    canonical.Lines[lineIndex];

                if (lowerLine == null ||
                    lowerLine.CourseIndex >= targetCourseIndex ||
                    !BistroBuilderCustomerDiningPolicy.LineContainsConsumer(
                        lowerLine,
                        customerId
                    ))
                {
                    continue;
                }

                if (!BistroBuilderCourseAndSharingPolicy
                        .IsLineResolvedForCourseAdvance(lowerLine))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AreAllLowerCourseLinesResolved(
        BistroBuilderCanonicalOrder canonical,
        int targetCourseIndex
    )
    {
        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null &&
                line.CourseIndex < targetCourseIndex &&
                !BistroBuilderCourseAndSharingPolicy
                    .IsLineResolvedForCourseAdvance(line))
            {
                return false;
            }
        }

        return true;
    }

    private static int FindMinimumSubmittedCourse(
        BistroBuilderCanonicalOrder canonical
    )
    {
        int result = int.MaxValue;

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null &&
                line.State == BistroBuilderCanonicalOrderLineState.Submitted &&
                line.CourseIndex < result)
            {
                result = line.CourseIndex;
            }
        }

        return result;
    }

    private bool RefreshRuntimeAfterRelease(
        BistroBuilderCourseOrderRuntime runtime,
        int courseIndex,
        out string error
    )
    {
        if (!canonicalOrderService.TryGetOrderSnapshot(
                runtime.OrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            error = "La comanda no pudo verificarse tras liberar el pase.";
            return false;
        }

        bool changed = MarkReleasedLinesFromCanonical(runtime, canonical);

        if (changed)
        {
            Revision++;
        }

        if (!runtime.IsCourseReleased(courseIndex))
        {
            error = "El runtime no registró el pase liberado.";
            return false;
        }

        return ValidateRuntimeAgainstCanonical(runtime, canonical, out error);
    }

    private bool MarkReleasedLinesFromCanonical(
        BistroBuilderCourseOrderRuntime runtime,
        BistroBuilderCanonicalOrder canonical
    )
    {
        bool changed = false;

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null &&
                BistroBuilderCourseAndSharingPolicy.IsLineReleased(line))
            {
                changed |= runtime.MarkLineReleased(
                    line.LineId,
                    line.CourseIndex
                );
            }
        }

        return changed;
    }

    private void PublishReleasedLines(
        string orderId,
        int courseIndex,
        List<string> lineIds
    )
    {
        PublishChange(
            BistroBuilderCourseAndSharingChangeType.CourseReleased,
            orderId,
            string.Empty,
            courseIndex,
            0,
            0,
            "Pase liberado hacia cocina con " + lineIds.Count + " línea(s)."
        );

        for (int index = 0; index < lineIds.Count; index++)
        {
            PublishChange(
                BistroBuilderCourseAndSharingChangeType.LineReleased,
                orderId,
                lineIds[index],
                courseIndex,
                0,
                0,
                "Línea Submitted liberada a Queued."
            );
        }
    }

    private bool ValidateRuntimeAgainstCanonical(
        BistroBuilderCourseOrderRuntime runtime,
        BistroBuilderCanonicalOrder canonical,
        out string error
    )
    {
        if (runtime == null || canonical == null ||
            !string.Equals(
                runtime.OrderId,
                canonical.OrderId,
                StringComparison.Ordinal
            ))
        {
            error = "El runtime 367F no coincide con su comanda canónica.";
            return false;
        }

        HashSet<string> canonicalLineIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line == null)
            {
                error = "La comanda 367F contiene una línea nula.";
                return false;
            }

            canonicalLineIds.Add(line.LineId);
        }

        for (int index = 0; index < runtime.ReleasedLineIds.Count; index++)
        {
            if (!canonicalLineIds.Contains(runtime.ReleasedLineIds[index]))
            {
                error = "El runtime 367F referencia una línea inexistente.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryFindMinimumCourse(
        BistroBuilderCanonicalOrder canonical,
        out int courseIndex
    )
    {
        courseIndex = int.MaxValue;

        if (canonical == null)
        {
            return false;
        }

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line != null && line.CourseIndex < courseIndex)
            {
                courseIndex = line.CourseIndex;
            }
        }

        return courseIndex != int.MaxValue;
    }

    private static bool TryFindMinimumPendingCourse(
        BistroBuilderCanonicalOrder canonical,
        out int courseIndex
    )
    {
        courseIndex = int.MaxValue;

        if (canonical == null)
        {
            return false;
        }

        for (int index = 0; index < canonical.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = canonical.Lines[index];

            if (line == null || line.IsTerminal)
            {
                continue;
            }

            if (line.CourseIndex < courseIndex)
            {
                courseIndex = line.CourseIndex;
            }
        }

        return courseIndex != int.MaxValue;
    }

    private bool TryValidateLinkedOrder(
        RestaurantOrder order,
        out string error
    )
    {
        if (order == null || !order.HasCanonicalOrder)
        {
            error = "La comanda legacy no está enlazada a una comanda canónica.";
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

        if (!string.Equals(
                canonical.ExternalReferenceId,
                BistroBuilderServiceOrderIdentityUtility
                    .BuildLegacyOrderReference(order.OrderId),
                StringComparison.Ordinal
            ))
        {
            error = "La comanda legacy y la canónica no comparten referencia.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        if (!TryRegisterOrder(order, out string error))
        {
            Debug.LogError("367F no pudo registrar la comanda. " + error, this);
        }
    }

    private void HandleOrderCompleted(RestaurantOrder order)
    {
        RemoveRuntime(order);
    }

    private void HandleOrderCancelled(RestaurantOrder order)
    {
        RemoveRuntime(order);
    }

    private void HandleDiningChanged(
        BistroBuilderCustomerDiningChangedEvent change
    )
    {
        if (change.ChangeType ==
                BistroBuilderCustomerDiningChangeType.CustomerCompletedCourse ||
            change.ChangeType ==
                BistroBuilderCustomerDiningChangeType.LineConsumed ||
            change.ChangeType ==
                BistroBuilderCustomerDiningChangeType.StateRestored)
        {
            if (BistroBuilderOrderIdUtility.IsValid(change.OrderId))
            {
                QueueEvaluation(change.OrderId);
            }
            else
            {
                QueueAllOrdersForEvaluation();
            }
        }

        if (change.ChangeType ==
                BistroBuilderCustomerDiningChangeType.SharedLineProgressed)
        {
            PublishChange(
                BistroBuilderCourseAndSharingChangeType.SharedLineProgressed,
                change.OrderId,
                change.LineId,
                change.CourseIndex,
                change.CompletedConsumerCount,
                change.TotalConsumerCount,
                change.Description
            );
        }
    }

    private void HandleCanonicalChanged(
        BistroBuilderCanonicalOrderChangedEvent change
    )
    {
        if (BistroBuilderOrderIdUtility.IsValid(change.OrderId))
        {
            QueueEvaluation(change.OrderId);
        }
    }

    private void RemoveRuntime(RestaurantOrder order)
    {
        if (order == null || !order.HasCanonicalOrder)
        {
            return;
        }

        string orderId = order.CanonicalOrderId;

        if (!runtimeByOrderId.TryGetValue(
                orderId,
                out BistroBuilderCourseOrderRuntime runtime
            ))
        {
            legacyByOrderId.Remove(orderId);
            return;
        }

        runtimeByOrderId.Remove(orderId);
        legacyByOrderId.Remove(orderId);
        activeOrders.Remove(runtime);
        pendingEvaluationOrderIds.Remove(orderId);
        Revision++;

        PublishChange(
            BistroBuilderCourseAndSharingChangeType.OrderRemoved,
            orderId,
            string.Empty,
            0,
            0,
            0,
            "Sesión de pases retirada del runtime activo."
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

        if (customerDiningService != null)
        {
            customerDiningService.DiningChanged -= HandleDiningChanged;
            customerDiningService.DiningChanged += HandleDiningChanged;
        }

        if (canonicalOrderService != null)
        {
            canonicalOrderService.OrdersChanged -= HandleCanonicalChanged;
            canonicalOrderService.OrdersChanged += HandleCanonicalChanged;
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

        if (customerDiningService != null)
        {
            customerDiningService.DiningChanged -= HandleDiningChanged;
        }

        if (canonicalOrderService != null)
        {
            canonicalOrderService.OrdersChanged -= HandleCanonicalChanged;
        }

        subscriptionsActive = false;
    }

    private void ReconcileExistingLegacyOrders()
    {
        if (orderSystem == null)
        {
            return;
        }

        IReadOnlyList<RestaurantOrder> orders = orderSystem.ActiveOrders;

        for (int index = 0; index < orders.Count; index++)
        {
            RestaurantOrder order = orders[index];

            if (order != null && order.HasCanonicalOrder)
            {
                TryRegisterOrder(order, out _);
            }
        }
    }

    private void QueueEvaluation(string orderId)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(orderId);

        if (BistroBuilderOrderIdUtility.IsValid(normalized))
        {
            pendingEvaluationOrderIds.Add(normalized);
        }
    }

    private void QueueAllOrdersForEvaluation()
    {
        for (int index = 0; index < activeOrders.Count; index++)
        {
            if (activeOrders[index] != null)
            {
                pendingEvaluationOrderIds.Add(activeOrders[index].OrderId);
            }
        }
    }

    /// <summary>
    /// Guardia explícita: eventos canónicos y de consumo solo encolan trabajo.
    /// Ninguna liberación reentra dentro de una mutación previa.
    /// </summary>
    private void DrainPendingEvaluations()
    {
        if (evaluationScopeActive)
        {
            return;
        }

        evaluationScopeActive = true;

        try
        {
            int safety = 0;

            while (pendingEvaluationOrderIds.Count > 0 && safety < 256)
            {
                safety++;
                pendingBuffer.Clear();
                pendingBuffer.AddRange(pendingEvaluationOrderIds);
                pendingEvaluationOrderIds.Clear();
                pendingBuffer.Sort(StringComparer.Ordinal);

                for (int index = 0; index < pendingBuffer.Count; index++)
                {
                    EvaluateOrder(pendingBuffer[index]);
                }
            }

            if (pendingEvaluationOrderIds.Count > 0)
            {
                Debug.LogError(
                    "367F superó el límite de evaluaciones de seguridad.",
                    this
                );
                pendingEvaluationOrderIds.Clear();
            }
        }
        finally
        {
            evaluationScopeActive = false;
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

        if (compositionService == null)
        {
            TryGetComponent(out compositionService);
        }

        if (customerDiningService == null)
        {
            TryGetComponent(out customerDiningService);
        }
    }

    private void PublishChange(
        BistroBuilderCourseAndSharingChangeType changeType,
        string orderId,
        string lineId,
        int courseIndex,
        int completedConsumerCount,
        int totalConsumerCount,
        string description
    )
    {
        BistroBuilderCourseAndSharingChangedEvent change =
            new BistroBuilderCourseAndSharingChangedEvent(
                changeType,
                orderId,
                lineId,
                courseIndex,
                completedConsumerCount,
                totalConsumerCount,
                Revision,
                description
            );

        CourseAndSharingChanged?.Invoke(change);

        if (logTransitions)
        {
            Debug.Log(
                "367F pases/compartidos: " + changeType +
                ". OrderId: " +
                (string.IsNullOrEmpty(change.OrderId) ? "-" : change.OrderId) +
                ". Course: " + courseIndex +
                ". LineId: " +
                (string.IsNullOrEmpty(change.LineId) ? "-" : change.LineId) +
                ". " + change.Description,
                this
            );
        }
    }

    private static int CompareRuntimes(
        BistroBuilderCourseOrderRuntime first,
        BistroBuilderCourseOrderRuntime second
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

        int legacy = first.LegacyOrderId.CompareTo(second.LegacyOrderId);
        return legacy != 0
            ? legacy
            : string.Compare(
                first.OrderId,
                second.OrderId,
                StringComparison.Ordinal
            );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
