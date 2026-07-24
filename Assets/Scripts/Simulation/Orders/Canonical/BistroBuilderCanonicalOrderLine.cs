using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unidad canónica de producción y servicio de una comanda.
///
/// Conserva referencias por identidad y no referencias directas a objetos de
/// escena. Esto permite reconstruirla durante un futuro service.runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderCanonicalOrderLine
{
    [SerializeField]
    private string lineId;

    [SerializeField]
    private string dishId;

    [SerializeField]
    private int priceCentsAtOrder;

    [SerializeField]
    private string primaryCustomerId;

    [SerializeField]
    private List<string> consumerCustomerIds = new List<string>();

    [SerializeField]
    private int courseIndex;

    [SerializeField]
    private BistroBuilderCanonicalOrderLineState state;

    [SerializeField]
    private int revision;

    [SerializeField]
    private string lastActorReferenceId;

    public string LineId => lineId ?? string.Empty;
    public string DishId => dishId ?? string.Empty;
    public int PriceCentsAtOrder => priceCentsAtOrder;
    public string PrimaryCustomerId => primaryCustomerId ?? string.Empty;
    public IReadOnlyList<string> ConsumerCustomerIds => consumerCustomerIds;
    public int CourseIndex => courseIndex;
    public BistroBuilderCanonicalOrderLineState State => state;
    public int Revision => revision;
    public string LastActorReferenceId => lastActorReferenceId ?? string.Empty;
    public bool IsTerminal =>
        BistroBuilderCanonicalOrderTransitionPolicy.IsTerminal(state);
    public bool IsShared => consumerCustomerIds != null &&
                            consumerCustomerIds.Count > 1;

    internal BistroBuilderCanonicalOrderLine(
        string lineId,
        BistroBuilderResolvedOrderDish dish,
        string primaryCustomerId,
        List<string> consumers,
        int courseIndex
    )
    {
        this.lineId = BistroBuilderOrderIdUtility.Normalize(lineId);
        dishId = BistroBuilderOrderIdUtility.Normalize(dish.DishId);
        priceCentsAtOrder = dish.PriceCents;
        this.primaryCustomerId =
            BistroBuilderOrderIdUtility.Normalize(primaryCustomerId);
        consumerCustomerIds = consumers != null
            ? new List<string>(consumers)
            : new List<string>();
        this.courseIndex = courseIndex;
        state = BistroBuilderCanonicalOrderLineState.Draft;
        revision = 0;
        lastActorReferenceId = string.Empty;
    }

    private BistroBuilderCanonicalOrderLine()
    {
    }

    internal bool TryTransition(
        BistroBuilderCanonicalOrderLineState target,
        string actorReferenceId,
        out string error
    )
    {
        if (!BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                state,
                target
            ))
        {
            error =
                "La línea " + LineId + " no puede pasar de " + state +
                " a " + target + ".";
            return false;
        }

        state = target;
        revision++;
        lastActorReferenceId =
            BistroBuilderOrderIdUtility.Normalize(actorReferenceId);
        error = string.Empty;
        return true;
    }

    internal bool ForceCancel(
        string actorReferenceId,
        out string error
    )
    {
        if (IsTerminal)
        {
            error = string.Empty;
            return false;
        }

        state = BistroBuilderCanonicalOrderLineState.Cancelled;
        revision++;
        lastActorReferenceId =
            BistroBuilderOrderIdUtility.Normalize(actorReferenceId);
        error = string.Empty;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(LineId))
        {
            error = "La línea contiene un LineId inválido.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(DishId))
        {
            error = "La línea " + LineId + " contiene un DishId inválido.";
            return false;
        }

        if (priceCentsAtOrder < 0 ||
            priceCentsAtOrder > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "La línea " + LineId + " contiene un precio inválido.";
            return false;
        }

        if (courseIndex < 0 || courseIndex > 20)
        {
            error = "La línea " + LineId + " contiene un pase inválido.";
            return false;
        }

        if (consumerCustomerIds == null ||
            consumerCustomerIds.Count == 0)
        {
            error = "La línea " + LineId + " no tiene consumidores.";
            return false;
        }

        HashSet<string> uniqueConsumers =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < consumerCustomerIds.Count;
             index++)
        {
            string normalized = BistroBuilderOrderIdUtility.Normalize(
                consumerCustomerIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(normalized))
            {
                error = "La línea " + LineId +
                        " contiene un CustomerId inválido.";
                return false;
            }

            if (!uniqueConsumers.Add(normalized))
            {
                error = "La línea " + LineId +
                        " contiene un consumidor duplicado.";
                return false;
            }

            consumerCustomerIds[index] = normalized;
        }

        primaryCustomerId =
            BistroBuilderOrderIdUtility.Normalize(primaryCustomerId);

        if (!string.IsNullOrEmpty(primaryCustomerId) &&
            !uniqueConsumers.Contains(primaryCustomerId))
        {
            error = "El cliente principal de la línea " + LineId +
                    " no figura entre sus consumidores.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal BistroBuilderCanonicalOrderLine Clone()
    {
        return new BistroBuilderCanonicalOrderLine
        {
            lineId = LineId,
            dishId = DishId,
            priceCentsAtOrder = priceCentsAtOrder,
            primaryCustomerId = PrimaryCustomerId,
            consumerCustomerIds =
                new List<string>(consumerCustomerIds),
            courseIndex = courseIndex,
            state = state,
            revision = revision,
            lastActorReferenceId = LastActorReferenceId
        };
    }
}
