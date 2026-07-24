using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agregado canónico de comanda.
///
/// La comanda es dueña de sus líneas y de su máquina de estados agregada.
/// Ningún sistema externo modifica directamente una línea.
/// </summary>
[Serializable]
public sealed class BistroBuilderCanonicalOrder
{
    [SerializeField]
    private string orderId;

    [SerializeField]
    private long sequenceNumber;

    [SerializeField]
    private string externalReferenceId;

    [SerializeField]
    private string tableReferenceId;

    [SerializeField]
    private string customerGroupReferenceId;

    [SerializeField]
    private BistroBuilderMealServiceAvailability mealService;

    [SerializeField]
    private string createdUtc;

    [SerializeField]
    private BistroBuilderCanonicalOrderState state;

    [SerializeField]
    private int revision;

    [SerializeField]
    private List<BistroBuilderCanonicalOrderLine> lines =
        new List<BistroBuilderCanonicalOrderLine>();

    public string OrderId => orderId ?? string.Empty;
    public long SequenceNumber => sequenceNumber;
    public string ExternalReferenceId => externalReferenceId ?? string.Empty;
    public string TableReferenceId => tableReferenceId ?? string.Empty;
    public string CustomerGroupReferenceId =>
        customerGroupReferenceId ?? string.Empty;
    public BistroBuilderMealServiceAvailability MealService => mealService;
    public string CreatedUtc => createdUtc ?? string.Empty;
    public BistroBuilderCanonicalOrderState State => state;
    public int Revision => revision;
    public IReadOnlyList<BistroBuilderCanonicalOrderLine> Lines => lines;
    public bool IsTerminal =>
        state == BistroBuilderCanonicalOrderState.Completed ||
        state == BistroBuilderCanonicalOrderState.Cancelled ||
        state == BistroBuilderCanonicalOrderState.Failed;

    internal BistroBuilderCanonicalOrder(
        string orderId,
        long sequenceNumber,
        string externalReferenceId,
        string tableReferenceId,
        string customerGroupReferenceId,
        BistroBuilderMealServiceAvailability mealService,
        List<BistroBuilderCanonicalOrderLine> lines
    )
    {
        this.orderId = BistroBuilderOrderIdUtility.Normalize(orderId);
        this.sequenceNumber = sequenceNumber;
        this.externalReferenceId =
            BistroBuilderOrderIdUtility.Normalize(externalReferenceId);
        this.tableReferenceId =
            BistroBuilderOrderIdUtility.Normalize(tableReferenceId);
        this.customerGroupReferenceId =
            BistroBuilderOrderIdUtility.Normalize(customerGroupReferenceId);
        this.mealService = mealService;
        this.lines = lines != null
            ? new List<BistroBuilderCanonicalOrderLine>(lines)
            : new List<BistroBuilderCanonicalOrderLine>();
        createdUtc = DateTimeOffset.UtcNow.ToString("O");
        state = BistroBuilderCanonicalOrderState.Draft;
        revision = 0;
        RefreshDerivedState();
    }

    private BistroBuilderCanonicalOrder()
    {
    }

    public bool TryGetLine(
        string lineId,
        out BistroBuilderCanonicalOrderLine line
    )
    {
        line = null;
        string normalized = BistroBuilderOrderIdUtility.Normalize(lineId);

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine candidate = lines[index];

            if (candidate != null &&
                string.Equals(
                    candidate.LineId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                line = candidate;
                return true;
            }
        }

        return false;
    }

    internal bool TryTransitionLine(
        string lineId,
        BistroBuilderCanonicalOrderLineState target,
        string actorReferenceId,
        out string error
    )
    {
        if (IsTerminal)
        {
            error = "La comanda ya está en un estado terminal.";
            return false;
        }

        if (!TryGetLine(lineId, out BistroBuilderCanonicalOrderLine line))
        {
            error = "La línea indicada no pertenece a la comanda.";
            return false;
        }

        if (!line.TryTransition(target, actorReferenceId, out error))
        {
            return false;
        }

        revision++;
        RefreshDerivedState();
        return true;
    }

    internal bool TryCancel(
        string actorReferenceId,
        out string error
    )
    {
        if (IsTerminal)
        {
            error = "La comanda ya está en un estado terminal.";
            return false;
        }

        bool changed = false;

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = lines[index];

            if (line != null && !line.IsTerminal)
            {
                changed |= line.ForceCancel(actorReferenceId, out _);
            }
        }

        if (!changed)
        {
            error = "La comanda no contiene líneas cancelables.";
            return false;
        }

        revision++;
        RefreshDerivedState();
        error = string.Empty;
        return true;
    }

    public int CalculateTotalPriceCents()
    {
        long total = 0;

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = lines[index];

            if (line != null &&
                line.State !=
                    BistroBuilderCanonicalOrderLineState.Cancelled)
            {
                total += line.PriceCentsAtOrder;
            }
        }

        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(OrderId))
        {
            error = "La comanda contiene un OrderId inválido.";
            return false;
        }

        if (sequenceNumber < 1)
        {
            error = "La comanda contiene una secuencia inválida.";
            return false;
        }

        if (!string.IsNullOrEmpty(ExternalReferenceId) &&
            !BistroBuilderOrderIdUtility.IsValid(ExternalReferenceId))
        {
            error = "La referencia externa de la comanda no es válida.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(TableReferenceId))
        {
            error = "La referencia de mesa de la comanda no es válida.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(
                CustomerGroupReferenceId
            ))
        {
            error = "La referencia de grupo de la comanda no es válida.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                mealService,
                false
            ) ||
            mealService == BistroBuilderMealServiceAvailability.All)
        {
            error = "La comanda no contiene un servicio concreto válido.";
            return false;
        }

        if (lines == null || lines.Count == 0)
        {
            error = "La comanda no contiene líneas.";
            return false;
        }

        HashSet<string> lineIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = lines[index];

            if (line == null)
            {
                error = "La comanda contiene una línea nula.";
                return false;
            }

            if (!line.TryValidate(out error))
            {
                return false;
            }

            if (!lineIds.Add(line.LineId))
            {
                error = "La comanda contiene un LineId duplicado.";
                return false;
            }
        }

        BistroBuilderCanonicalOrderState expected =
            CalculateDerivedState(lines);

        if (state != expected)
        {
            error = "El estado agregado de la comanda no coincide con sus " +
                    "líneas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal BistroBuilderCanonicalOrder Clone()
    {
        List<BistroBuilderCanonicalOrderLine> clonedLines =
            new List<BistroBuilderCanonicalOrderLine>(lines.Count);

        for (int index = 0; index < lines.Count; index++)
        {
            clonedLines.Add(lines[index].Clone());
        }

        return new BistroBuilderCanonicalOrder
        {
            orderId = OrderId,
            sequenceNumber = sequenceNumber,
            externalReferenceId = ExternalReferenceId,
            tableReferenceId = TableReferenceId,
            customerGroupReferenceId = CustomerGroupReferenceId,
            mealService = mealService,
            createdUtc = CreatedUtc,
            state = state,
            revision = revision,
            lines = clonedLines
        };
    }

    private void RefreshDerivedState()
    {
        state = CalculateDerivedState(lines);
    }

    private static BistroBuilderCanonicalOrderState CalculateDerivedState(
        IList<BistroBuilderCanonicalOrderLine> source
    )
    {
        if (source == null || source.Count == 0)
        {
            return BistroBuilderCanonicalOrderState.Draft;
        }

        int draft = 0;
        int submitted = 0;
        int queuedOrPreparing = 0;
        int ready = 0;
        int delivery = 0;
        int served = 0;
        int consumed = 0;
        int cancelled = 0;
        int failed = 0;

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = source[index];

            if (line == null)
            {
                failed++;
                continue;
            }

            switch (line.State)
            {
                case BistroBuilderCanonicalOrderLineState.Draft:
                    draft++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Submitted:
                    submitted++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Queued:
                case BistroBuilderCanonicalOrderLineState.Preparing:
                    queuedOrPreparing++;
                    break;
                case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
                    ready++;
                    break;
                case BistroBuilderCanonicalOrderLineState
                    .AssignedForDelivery:
                case BistroBuilderCanonicalOrderLineState.InTransit:
                    delivery++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Served:
                    served++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Consumed:
                    consumed++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Cancelled:
                    cancelled++;
                    break;
                case BistroBuilderCanonicalOrderLineState.Failed:
                    failed++;
                    break;
            }
        }

        int total = source.Count;

        if (cancelled == total)
        {
            return BistroBuilderCanonicalOrderState.Cancelled;
        }

        if (failed > 0 && failed + cancelled + consumed == total)
        {
            return BistroBuilderCanonicalOrderState.Failed;
        }

        if (consumed + cancelled == total)
        {
            return BistroBuilderCanonicalOrderState.Completed;
        }

        if (served > 0 &&
            served + consumed + cancelled == total)
        {
            return BistroBuilderCanonicalOrderState.Served;
        }

        if (delivery > 0)
        {
            return BistroBuilderCanonicalOrderState.InDelivery;
        }

        // Una comanda solo se considera completamente lista para recoger
        // cuando todas sus líneas activas permanecen en el pase.
        //
        // Si alguna línea ya fue servida o consumida mientras otra continúa
        // lista, el agregado está en progreso mixto. Marcarlo como
        // ReadyForPickup podría provocar que sistemas futuros tratasen toda
        // la comanda como un único lote aún pendiente de recogida.
        if (ready > 0 &&
            ready + cancelled == total)
        {
            return BistroBuilderCanonicalOrderState.ReadyForPickup;
        }

        if (queuedOrPreparing > 0 || ready > 0 || served > 0 ||
            consumed > 0 || failed > 0)
        {
            return BistroBuilderCanonicalOrderState.InProgress;
        }

        if (submitted > 0 && submitted + cancelled == total)
        {
            return BistroBuilderCanonicalOrderState.Submitted;
        }

        if (draft > 0)
        {
            return BistroBuilderCanonicalOrderState.Draft;
        }

        return BistroBuilderCanonicalOrderState.InProgress;
    }
}
