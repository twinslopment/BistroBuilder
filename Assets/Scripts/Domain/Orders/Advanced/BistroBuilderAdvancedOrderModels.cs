using System;
using System.Collections.Generic;

/// <summary>Origen funcional de una línea añadida durante una revisión de comanda.</summary>
public enum BistroBuilderAdvancedOrderLineOriginKind
{
    Original = 0,
    Correction = 1,
    Repeat = 2,
    Replacement = 3,
    Courtesy = 4
}

/// <summary>Determina si una línea forma parte del importe final de la cuenta.</summary>
public enum BistroBuilderAdvancedOrderBillingMode
{
    Standard = 0,
    Courtesy = 1
}

/// <summary>Incidencia explícita asociada a una línea de comanda.</summary>
public enum BistroBuilderAdvancedOrderIncidentKind
{
    None = 0,
    CustomerChange = 1,
    WrongDish = 2,
    DuplicateOrder = 3,
    KitchenError = 4,
    QualityIssue = 5,
    AllergyRisk = 6,
    MissingItem = 7,
    ServiceError = 8
}

/// <summary>Resoluciones jugables soportadas por el bloque 11.</summary>
public enum BistroBuilderAdvancedOrderResolutionKind
{
    None = 0,
    Cancel = 1,
    Correct = 2,
    Repeat = 3,
    Replace = 4,
    ReturnAndReplace = 5,
    ReturnWithoutReplacement = 6
}

/// <summary>Motivo estable por el que una mutación no está permitida.</summary>
public enum BistroBuilderAdvancedOrderRestriction
{
    None = 0,
    OrderTerminal = 1,
    LineTerminal = 2,
    PreparationStarted = 3,
    NotYetServed = 4,
    AlreadyConsumed = 5,
    DishUnavailable = 6,
    InventoryUnavailable = 7,
    InvalidRequest = 8
}

[Serializable]
public sealed class BistroBuilderAdvancedOrderMutationResult
{
    public bool succeeded;
    public BistroBuilderAdvancedOrderRestriction restriction;
    public string message = string.Empty;
    public string orderId = string.Empty;
    public string sourceLineId = string.Empty;
    public string newLineId = string.Empty;

    public static BistroBuilderAdvancedOrderMutationResult Success(
        string message, string orderId, string sourceLineId, string newLineId = "")
    {
        return new BistroBuilderAdvancedOrderMutationResult
        {
            succeeded = true,
            restriction = BistroBuilderAdvancedOrderRestriction.None,
            message = message ?? string.Empty,
            orderId = orderId ?? string.Empty,
            sourceLineId = sourceLineId ?? string.Empty,
            newLineId = newLineId ?? string.Empty
        };
    }

    public static BistroBuilderAdvancedOrderMutationResult Failure(
        BistroBuilderAdvancedOrderRestriction restriction,
        string message, string orderId = "", string sourceLineId = "")
    {
        return new BistroBuilderAdvancedOrderMutationResult
        {
            succeeded = false,
            restriction = restriction,
            message = message ?? string.Empty,
            orderId = orderId ?? string.Empty,
            sourceLineId = sourceLineId ?? string.Empty
        };
    }
}

[Serializable]
public sealed class BistroBuilderAdvancedOrderActionAvailability
{
    public bool canCorrect;
    public bool canCancel;
    public bool canRepeat;
    public bool canReplace;
    public bool canReturn;
    public string restrictionLabel = string.Empty;
}

/// <summary>
/// Política pura del bloque 11. Centraliza restricciones por estado para que
/// UI, camareros, cocina y autotests no inventen reglas diferentes.
/// </summary>
public static class BistroBuilderAdvancedOrderMutationPolicy
{
    public static bool CanCorrect(BistroBuilderCanonicalOrderLineState state) =>
        state == BistroBuilderCanonicalOrderLineState.Draft ||
        state == BistroBuilderCanonicalOrderLineState.Submitted ||
        state == BistroBuilderCanonicalOrderLineState.Queued;

    public static bool CanCancel(BistroBuilderCanonicalOrderLineState state) =>
        CanCorrect(state);

    public static bool CanRepeat(BistroBuilderCanonicalOrderLineState state) =>
        state != BistroBuilderCanonicalOrderLineState.Draft &&
        state != BistroBuilderCanonicalOrderLineState.Cancelled &&
        state != BistroBuilderCanonicalOrderLineState.Failed;

    public static bool CanReplaceAfterIncident(
        BistroBuilderCanonicalOrderLineState state) =>
        state == BistroBuilderCanonicalOrderLineState.Preparing ||
        state == BistroBuilderCanonicalOrderLineState.ReadyForPickup ||
        state == BistroBuilderCanonicalOrderLineState.AssignedForDelivery ||
        state == BistroBuilderCanonicalOrderLineState.InTransit ||
        state == BistroBuilderCanonicalOrderLineState.Served;

    public static bool CanReturn(BistroBuilderCanonicalOrderLineState state) =>
        state == BistroBuilderCanonicalOrderLineState.Served;

    public static BistroBuilderCanonicalOrderLineState ResolveNewLineState(
        BistroBuilderCanonicalOrderLineState sourceState,
        BistroBuilderAdvancedOrderLineOriginKind origin)
    {
        if (origin == BistroBuilderAdvancedOrderLineOriginKind.Correction)
        {
            if (sourceState == BistroBuilderCanonicalOrderLineState.Draft)
                return BistroBuilderCanonicalOrderLineState.Draft;
            if (sourceState == BistroBuilderCanonicalOrderLineState.Submitted)
                return BistroBuilderCanonicalOrderLineState.Submitted;
        }
        return BistroBuilderCanonicalOrderLineState.Queued;
    }

    public static BistroBuilderAdvancedOrderActionAvailability Evaluate(
        BistroBuilderCanonicalOrder order,
        BistroBuilderCanonicalOrderLine line)
    {
        var result = new BistroBuilderAdvancedOrderActionAvailability();
        if (order == null || line == null)
        {
            result.restrictionLabel = "Comanda o línea no disponible";
            return result;
        }
        if (order.IsTerminal)
        {
            result.restrictionLabel = "La comanda ya está cerrada";
            return result;
        }
        result.canCorrect = CanCorrect(line.State);
        result.canCancel = CanCancel(line.State);
        result.canRepeat = CanRepeat(line.State);
        result.canReplace = CanReplaceAfterIncident(line.State);
        result.canReturn = CanReturn(line.State);
        result.restrictionLabel = ResolveRestrictionLabel(line.State);
        return result;
    }

    public static string ResolveRestrictionLabel(
        BistroBuilderCanonicalOrderLineState state)
    {
        switch (state)
        {
            case BistroBuilderCanonicalOrderLineState.Draft:
            case BistroBuilderCanonicalOrderLineState.Submitted:
            case BistroBuilderCanonicalOrderLineState.Queued:
                return "Se puede corregir o cancelar antes de preparar";
            case BistroBuilderCanonicalOrderLineState.Preparing:
                return "La preparación ya ha comenzado: requiere incidencia/reemplazo";
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery:
            case BistroBuilderCanonicalOrderLineState.InTransit:
                return "El plato ya está producido: requiere incidencia/reemplazo";
            case BistroBuilderCanonicalOrderLineState.Served:
                return "El plato ya fue servido: puede devolverse o reemplazarse";
            case BistroBuilderCanonicalOrderLineState.Consumed:
                return "El plato ya fue consumido y no puede modificarse";
            default:
                return "La línea está cerrada";
        }
    }
}
