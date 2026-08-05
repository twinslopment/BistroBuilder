using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Métricas runtime idempotentes de platos firma.
///
/// Registra selecciones, líneas pedidas y líneas consumidas utilizando la
/// fotografía histórica de cada comanda. Un cambio posterior de la carta no
/// reescribe métricas ya observadas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Signature Dish Telemetry Service")]
public sealed class BistroBuilderSignatureDishTelemetryService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1D-METRICS";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderMenuSelectionService selectionService;

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [Header("Depuración")]

    [SerializeField]
    private bool logChanges;

    private readonly HashSet<string> observedOrderLineIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> observedConsumedLineIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<BistroBuilderCanonicalOrder> orderBuffer =
        new List<BistroBuilderCanonicalOrder>(32);
    private readonly List<string> idBuffer = new List<string>(128);

    private BistroBuilderMenuSelectionService subscribedSelectionService;
    private BistroBuilderCanonicalOrderService subscribedOrderService;

    public event Action<BistroBuilderSignatureDishTelemetryChangedEvent>
        TelemetryChanged;

    public BistroBuilderMenuSelectionService SelectionService =>
        selectionService;
    public BistroBuilderCanonicalOrderService CanonicalOrderService =>
        canonicalOrderService;

    public int Revision { get; private set; }
    public long TotalSelections { get; private set; }
    public long SignatureSelections { get; private set; }
    public long TotalOrderedLines { get; private set; }
    public long SignatureOrderedLines { get; private set; }
    public long TotalConsumedLines { get; private set; }
    public long SignatureConsumedLines { get; private set; }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();

        if (Application.isPlaying)
        {
            TrySynchronizeCurrentOrders(out _);
        }
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (selectionService == null)
        {
            error = "Falta BistroBuilderMenuSelectionService en telemetría.";
            return false;
        }

        if (!selectionService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (canonicalOrderService == null)
        {
            error = "Falta BistroBuilderCanonicalOrderService en telemetría.";
            return false;
        }

        if (!canonicalOrderService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (canonicalOrderService.OfferService != null &&
            !ReferenceEquals(
                canonicalOrderService.OfferService,
                selectionService.OfferService
            ))
        {
            error = "Telemetría, selección y comandas no comparten oferta.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TrySynchronizeCurrentOrders(out string error)
    {
        CacheDependenciesIfNeeded();

        if (canonicalOrderService == null)
        {
            error = "Falta la autoridad canónica de comandas.";
            return false;
        }

        orderBuffer.Clear();
        canonicalOrderService.CopyOrderSnapshotsTo(orderBuffer);
        bool anyOrderChanged = false;
        bool anyConsumptionChanged = false;

        for (int index = 0; index < orderBuffer.Count; index++)
        {
            if (!TryObserveOrderSnapshotInternal(
                    orderBuffer[index],
                    out bool orderChanged,
                    out bool consumptionChanged,
                    out error
                ))
            {
                return false;
            }

            anyOrderChanged |= orderChanged;
            anyConsumptionChanged |= consumptionChanged;
        }

        if (anyOrderChanged)
        {
            Publish(
                BistroBuilderSignatureDishTelemetryChangeType.OrderObserved,
                string.Empty
            );
        }

        if (anyConsumptionChanged)
        {
            Publish(
                BistroBuilderSignatureDishTelemetryChangeType
                    .ConsumptionObserved,
                string.Empty
            );
        }

        error = string.Empty;
        return true;
    }

    public bool TryObserveOrderSnapshot(
        BistroBuilderCanonicalOrder order,
        out string error
    )
    {
        if (!TryObserveOrderSnapshotInternal(
                order,
                out bool orderChanged,
                out bool consumptionChanged,
                out error
            ))
        {
            return false;
        }

        string subjectId =
            order != null && order.Lines.Count == 1
                ? order.Lines[0].LineId
                : string.Empty;

        if (orderChanged)
        {
            Publish(
                BistroBuilderSignatureDishTelemetryChangeType.OrderObserved,
                subjectId
            );
        }

        if (consumptionChanged)
        {
            Publish(
                BistroBuilderSignatureDishTelemetryChangeType
                    .ConsumptionObserved,
                subjectId
            );
        }

        error = string.Empty;
        return true;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderSignatureDishTelemetrySnapshot snapshot,
        out string error
    )
    {
        idBuffer.Clear();
        idBuffer.AddRange(observedOrderLineIds);
        idBuffer.Sort(StringComparer.Ordinal);
        List<string> orderedIds = new List<string>(idBuffer);

        idBuffer.Clear();
        idBuffer.AddRange(observedConsumedLineIds);
        idBuffer.Sort(StringComparer.Ordinal);
        List<string> consumedIds = new List<string>(idBuffer);

        snapshot = new BistroBuilderSignatureDishTelemetrySnapshot(
            TotalSelections,
            SignatureSelections,
            TotalOrderedLines,
            SignatureOrderedLines,
            TotalConsumedLines,
            SignatureConsumedLines,
            orderedIds,
            consumedIds
        );

        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderSignatureDishTelemetrySnapshot snapshot,
        bool notify,
        out string error
    )
    {
        if (snapshot == null)
        {
            error = "El snapshot de telemetría es nulo.";
            return false;
        }

        if (!snapshot.TryValidate(out error))
        {
            return false;
        }

        HashSet<string> candidateOrdered =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> candidateConsumed =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < snapshot.ObservedOrderLineIds.Count;
             index++)
        {
            candidateOrdered.Add(snapshot.ObservedOrderLineIds[index]);
        }

        for (int index = 0;
             index < snapshot.ObservedConsumedLineIds.Count;
             index++)
        {
            candidateConsumed.Add(snapshot.ObservedConsumedLineIds[index]);
        }

        TotalSelections = snapshot.TotalSelections;
        SignatureSelections = snapshot.SignatureSelections;
        TotalOrderedLines = snapshot.TotalOrderedLines;
        SignatureOrderedLines = snapshot.SignatureOrderedLines;
        TotalConsumedLines = snapshot.TotalConsumedLines;
        SignatureConsumedLines = snapshot.SignatureConsumedLines;

        observedOrderLineIds.Clear();
        observedOrderLineIds.UnionWith(candidateOrdered);
        observedConsumedLineIds.Clear();
        observedConsumedLineIds.UnionWith(candidateConsumed);
        Revision++;

        if (notify)
        {
            TelemetryChanged?.Invoke(
                new BistroBuilderSignatureDishTelemetryChangedEvent(
                    BistroBuilderSignatureDishTelemetryChangeType
                        .StateRestored,
                    string.Empty,
                    Revision
                )
            );
        }

        error = string.Empty;
        return true;
    }

    public void ResetMetrics(bool notify)
    {
        TotalSelections = 0L;
        SignatureSelections = 0L;
        TotalOrderedLines = 0L;
        SignatureOrderedLines = 0L;
        TotalConsumedLines = 0L;
        SignatureConsumedLines = 0L;
        observedOrderLineIds.Clear();
        observedConsumedLineIds.Clear();
        Revision++;

        if (notify)
        {
            TelemetryChanged?.Invoke(
                new BistroBuilderSignatureDishTelemetryChangedEvent(
                    BistroBuilderSignatureDishTelemetryChangeType.Reset,
                    string.Empty,
                    Revision
                )
            );
        }
    }

    private bool TryObserveOrderSnapshotInternal(
        BistroBuilderCanonicalOrder order,
        out bool orderChanged,
        out bool consumptionChanged,
        out string error
    )
    {
        orderChanged = false;
        consumptionChanged = false;

        if (order == null)
        {
            error = "La telemetría recibió una comanda nula.";
            return false;
        }

        if (!order.TryValidate(out error))
        {
            return false;
        }

        for (int index = 0; index < order.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[index];

            if (line == null)
            {
                error = "La telemetría recibió una línea nula.";
                return false;
            }

            if (observedOrderLineIds.Add(line.LineId))
            {
                TotalOrderedLines++;

                if (line.WasSignatureDishAtOrder)
                {
                    SignatureOrderedLines++;
                }

                orderChanged = true;
            }

            if (line.State ==
                    BistroBuilderCanonicalOrderLineState.Consumed &&
                observedConsumedLineIds.Add(line.LineId))
            {
                TotalConsumedLines++;

                if (line.WasSignatureDishAtOrder)
                {
                    SignatureConsumedLines++;
                }

                consumptionChanged = true;
            }
        }

        error = string.Empty;
        return true;
    }

    private void HandleSelectionCompleted(
        BistroBuilderMenuSelectionCompletedEvent selection
    )
    {
        TotalSelections++;

        if (selection.Result.WasSignatureDishAtSelection)
        {
            SignatureSelections++;
        }

        Publish(
            BistroBuilderSignatureDishTelemetryChangeType.SelectionRecorded,
            selection.Result.DishId
        );
    }

    private void HandleOrdersChanged(
        BistroBuilderCanonicalOrderChangedEvent change
    )
    {
        if (canonicalOrderService == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(change.OrderId) &&
            canonicalOrderService.TryGetOrderSnapshot(
                change.OrderId,
                out BistroBuilderCanonicalOrder order
            ))
        {
            if (!TryObserveOrderSnapshotInternal(
                    order,
                    out bool orderChanged,
                    out bool consumptionChanged,
                    out string error
                ))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogError(error, this);
                }

                return;
            }

            if (orderChanged)
            {
                Publish(
                    BistroBuilderSignatureDishTelemetryChangeType
                        .OrderObserved,
                    change.LineId
                );
            }

            if (consumptionChanged)
            {
                Publish(
                    BistroBuilderSignatureDishTelemetryChangeType
                        .ConsumptionObserved,
                    change.LineId
                );
            }

            return;
        }

        if (change.ChangeType ==
                BistroBuilderCanonicalOrderChangeType.StateRestored ||
            change.ChangeType ==
                BistroBuilderCanonicalOrderChangeType.AllOrdersCleared)
        {
            TrySynchronizeCurrentOrders(out _);
        }
    }

    private void Publish(
        BistroBuilderSignatureDishTelemetryChangeType changeType,
        string subjectId
    )
    {
        Revision++;
        TelemetryChanged?.Invoke(
            new BistroBuilderSignatureDishTelemetryChangedEvent(
                changeType,
                subjectId,
                Revision
            )
        );

        if (logChanges)
        {
            Debug.Log(
                "Telemetría 2.1D: " + changeType +
                ". Selecciones firma: " + SignatureSelections + "/" +
                TotalSelections +
                ". Líneas firma consumidas: " +
                SignatureConsumedLines + "/" + TotalConsumedLines + ".",
                this
            );
        }
    }

    private void Subscribe()
    {
        CacheDependenciesIfNeeded();

        if (!ReferenceEquals(
                subscribedSelectionService,
                selectionService
            ))
        {
            if (subscribedSelectionService != null)
            {
                subscribedSelectionService.SelectionCompleted -=
                    HandleSelectionCompleted;
            }

            subscribedSelectionService = selectionService;

            if (subscribedSelectionService != null)
            {
                subscribedSelectionService.SelectionCompleted +=
                    HandleSelectionCompleted;
            }
        }

        if (!ReferenceEquals(
                subscribedOrderService,
                canonicalOrderService
            ))
        {
            if (subscribedOrderService != null)
            {
                subscribedOrderService.OrdersChanged -=
                    HandleOrdersChanged;
            }

            subscribedOrderService = canonicalOrderService;

            if (subscribedOrderService != null)
            {
                subscribedOrderService.OrdersChanged +=
                    HandleOrdersChanged;
            }
        }
    }

    private void Unsubscribe()
    {
        if (subscribedSelectionService != null)
        {
            subscribedSelectionService.SelectionCompleted -=
                HandleSelectionCompleted;
            subscribedSelectionService = null;
        }

        if (subscribedOrderService != null)
        {
            subscribedOrderService.OrdersChanged -= HandleOrdersChanged;
            subscribedOrderService = null;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (selectionService == null)
        {
            TryGetComponent(out selectionService);
        }

        if (canonicalOrderService == null)
        {
            TryGetComponent(out canonicalOrderService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
