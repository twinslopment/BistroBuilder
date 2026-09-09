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
    private bool wasSignatureDishAtOrder;

    [SerializeField]
    private string restaurantIdAtOrder;

    [SerializeField]
    private int menuOfferRevisionAtOrder;

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

    [SerializeField]
    private BistroBuilderAdvancedOrderLineOriginKind advancedOriginKind;

    [SerializeField]
    private BistroBuilderAdvancedOrderBillingMode advancedBillingMode;

    [SerializeField]
    private string advancedSourceLineId;

    [SerializeField]
    private BistroBuilderAdvancedOrderIncidentKind advancedIncidentKind;

    [SerializeField]
    private string advancedChangeReason;

    public string LineId => lineId ?? string.Empty;
    public string DishId => dishId ?? string.Empty;
    public int PriceCentsAtOrder => priceCentsAtOrder;
    public bool WasSignatureDishAtOrder => wasSignatureDishAtOrder;
    public string RestaurantIdAtOrder => restaurantIdAtOrder ?? string.Empty;
    public int MenuOfferRevisionAtOrder => menuOfferRevisionAtOrder;
    public string PrimaryCustomerId => primaryCustomerId ?? string.Empty;
    public IReadOnlyList<string> ConsumerCustomerIds => consumerCustomerIds;
    public int CourseIndex => courseIndex;
    public BistroBuilderCanonicalOrderLineState State => state;
    public int Revision => revision;
    public string LastActorReferenceId => lastActorReferenceId ?? string.Empty;
    public BistroBuilderAdvancedOrderLineOriginKind AdvancedOriginKind => advancedOriginKind;
    public BistroBuilderAdvancedOrderBillingMode AdvancedBillingMode => advancedBillingMode;
    public string AdvancedSourceLineId => advancedSourceLineId ?? string.Empty;
    public BistroBuilderAdvancedOrderIncidentKind AdvancedIncidentKind => advancedIncidentKind;
    public string AdvancedChangeReason => advancedChangeReason ?? string.Empty;
    public bool IsChargeable =>
        advancedBillingMode == BistroBuilderAdvancedOrderBillingMode.Standard &&
        state != BistroBuilderCanonicalOrderLineState.Cancelled &&
        state != BistroBuilderCanonicalOrderLineState.Failed;
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
        wasSignatureDishAtOrder = dish.SignatureDish;
        restaurantIdAtOrder =
            BistroBuilderMenuIdUtility.NormalizeStableId(dish.RestaurantId);
        menuOfferRevisionAtOrder = Math.Max(0, dish.MenuOfferRevision);
        this.primaryCustomerId =
            BistroBuilderOrderIdUtility.Normalize(primaryCustomerId);
        consumerCustomerIds = consumers != null
            ? new List<string>(consumers)
            : new List<string>();
        this.courseIndex = courseIndex;
        state = BistroBuilderCanonicalOrderLineState.Draft;
        revision = 0;
        lastActorReferenceId = string.Empty;
        advancedOriginKind = BistroBuilderAdvancedOrderLineOriginKind.Original;
        advancedBillingMode = BistroBuilderAdvancedOrderBillingMode.Standard;
        advancedSourceLineId = string.Empty;
        advancedIncidentKind = BistroBuilderAdvancedOrderIncidentKind.None;
        advancedChangeReason = string.Empty;
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

    internal bool TryApplyAdvancedMetadata(
        BistroBuilderAdvancedOrderLineOriginKind originKind,
        BistroBuilderAdvancedOrderBillingMode billingMode,
        string sourceLineId,
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string changeReason,
        string actorReferenceId,
        out string error)
    {
        string normalizedSource = BistroBuilderOrderIdUtility.Normalize(sourceLineId);
        if (originKind != BistroBuilderAdvancedOrderLineOriginKind.Original &&
            !BistroBuilderOrderIdUtility.IsValid(normalizedSource))
        {
            error = "Una línea revisada necesita SourceLineId válido.";
            return false;
        }
        if (!Enum.IsDefined(typeof(BistroBuilderAdvancedOrderLineOriginKind), originKind) ||
            !Enum.IsDefined(typeof(BistroBuilderAdvancedOrderBillingMode), billingMode) ||
            !Enum.IsDefined(typeof(BistroBuilderAdvancedOrderIncidentKind), incidentKind))
        {
            error = "La metadata avanzada de comanda contiene un enum inválido.";
            return false;
        }
        string reason = string.IsNullOrWhiteSpace(changeReason)
            ? string.Empty : changeReason.Trim();
        if (reason.Length > 180)
        {
            error = "El motivo de revisión supera 180 caracteres.";
            return false;
        }
        advancedOriginKind = originKind;
        advancedBillingMode = billingMode;
        advancedSourceLineId = normalizedSource;
        advancedIncidentKind = incidentKind;
        advancedChangeReason = reason;
        revision++;
        lastActorReferenceId = BistroBuilderOrderIdUtility.Normalize(actorReferenceId);
        error = string.Empty;
        return true;
    }

    internal bool TryRegisterAdvancedIncident(
        BistroBuilderAdvancedOrderIncidentKind incidentKind,
        string changeReason,
        string actorReferenceId,
        out string error)
    {
        if (incidentKind == BistroBuilderAdvancedOrderIncidentKind.None)
        {
            error = "Debe indicarse una incidencia real.";
            return false;
        }
        return TryApplyAdvancedMetadata(
            advancedOriginKind,
            advancedBillingMode,
            advancedSourceLineId,
            incidentKind,
            changeReason,
            actorReferenceId,
            out error);
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

        if (!string.IsNullOrEmpty(RestaurantIdAtOrder) &&
            !BistroBuilderMenuIdUtility.IsValidStableId(RestaurantIdAtOrder))
        {
            error = "La línea " + LineId +
                    " contiene un RestaurantId histórico inválido.";
            return false;
        }

        if (menuOfferRevisionAtOrder < 0)
        {
            error = "La línea " + LineId +
                    " contiene una revisión de oferta negativa.";
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

        if (!Enum.IsDefined(typeof(BistroBuilderAdvancedOrderLineOriginKind), advancedOriginKind) ||
            !Enum.IsDefined(typeof(BistroBuilderAdvancedOrderBillingMode), advancedBillingMode) ||
            !Enum.IsDefined(typeof(BistroBuilderAdvancedOrderIncidentKind), advancedIncidentKind))
        {
            error = "La línea " + LineId + " contiene metadata avanzada inválida.";
            return false;
        }
        advancedSourceLineId = BistroBuilderOrderIdUtility.Normalize(advancedSourceLineId);
        if (advancedOriginKind != BistroBuilderAdvancedOrderLineOriginKind.Original &&
            !BistroBuilderOrderIdUtility.IsValid(advancedSourceLineId))
        {
            error = "La línea " + LineId + " no conserva el origen de su revisión.";
            return false;
        }
        if ((advancedChangeReason ?? string.Empty).Length > 180)
        {
            error = "La línea " + LineId + " contiene un motivo demasiado largo.";
            return false;
        }
        error = string.Empty;
        return true;
    }


    internal void NormalizeForActiveServiceCheckpoint()
    {
        if (state == BistroBuilderCanonicalOrderLineState.AssignedForDelivery ||
            state == BistroBuilderCanonicalOrderLineState.InTransit)
        {
            state = BistroBuilderCanonicalOrderLineState.ReadyForPickup;
            revision++;
            lastActorReferenceId = "service.runtime";
        }
    }

    internal BistroBuilderCanonicalOrderLine Clone()
    {
        return new BistroBuilderCanonicalOrderLine
        {
            lineId = LineId,
            dishId = DishId,
            priceCentsAtOrder = priceCentsAtOrder,
            wasSignatureDishAtOrder = wasSignatureDishAtOrder,
            restaurantIdAtOrder = RestaurantIdAtOrder,
            menuOfferRevisionAtOrder = menuOfferRevisionAtOrder,
            primaryCustomerId = PrimaryCustomerId,
            consumerCustomerIds =
                new List<string>(consumerCustomerIds),
            courseIndex = courseIndex,
            state = state,
            revision = revision,
            lastActorReferenceId = LastActorReferenceId,
            advancedOriginKind = advancedOriginKind,
            advancedBillingMode = advancedBillingMode,
            advancedSourceLineId = AdvancedSourceLineId,
            advancedIncidentKind = advancedIncidentKind,
            advancedChangeReason = AdvancedChangeReason
        };
    }
}
