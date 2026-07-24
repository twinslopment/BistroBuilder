using System;
using System.Collections.Generic;

/// <summary>
/// Fábrica atómica de comandas canónicas.
///
/// Valida y resuelve todas las líneas antes de construir el agregado. Un fallo
/// en un solo plato no deja una comanda parcial registrada.
/// </summary>
public static class BistroBuilderCanonicalOrderFactory
{
    public static bool TryCreate(
        BistroBuilderCanonicalOrderCreationRequest request,
        IBistroBuilderOrderDishResolver dishResolver,
        long sequenceNumber,
        out BistroBuilderCanonicalOrder order,
        out BistroBuilderCanonicalOrderOperationResult result
    )
    {
        order = null;

        if (request == null || dishResolver == null)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "La solicitud o el resolvedor de platos no están disponibles."
            );
            return false;
        }

        if (sequenceNumber < 1)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "La secuencia de comanda debe ser positiva."
            );
            return false;
        }

        string tableId =
            BistroBuilderOrderIdUtility.Normalize(request.tableReferenceId);
        string groupId = BistroBuilderOrderIdUtility.Normalize(
            request.customerGroupReferenceId
        );
        string externalId = BistroBuilderOrderIdUtility.Normalize(
            request.externalReferenceId
        );

        if (!BistroBuilderOrderIdUtility.IsValid(tableId) ||
            !BistroBuilderOrderIdUtility.IsValid(groupId) ||
            !string.IsNullOrEmpty(externalId) &&
            !BistroBuilderOrderIdUtility.IsValid(externalId))
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidReferenceId,
                "La mesa, el grupo o la referencia externa no tienen una " +
                "identidad estable válida."
            );
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                request.mealService,
                false
            ) ||
            request.mealService == BistroBuilderMealServiceAvailability.All)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "Debe indicarse un servicio de comida concreto."
            );
            return false;
        }

        if (request.lines == null || request.lines.Count == 0)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "La comanda debe contener al menos una línea."
            );
            return false;
        }

        List<BistroBuilderCanonicalOrderLine> candidateLines =
            new List<BistroBuilderCanonicalOrderLine>(request.lines.Count);

        for (int index = 0; index < request.lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLineRequest lineRequest =
                request.lines[index];

            if (!TryCreateLine(
                    lineRequest,
                    request.mealService,
                    dishResolver,
                    out BistroBuilderCanonicalOrderLine line,
                    out result
                ))
            {
                return false;
            }

            candidateLines.Add(line);
        }

        order = new BistroBuilderCanonicalOrder(
            BistroBuilderOrderIdUtility.NewOrderId(),
            sequenceNumber,
            externalId,
            tableId,
            groupId,
            request.mealService,
            candidateLines
        );

        if (!order.TryValidate(out string validationError))
        {
            order = null;
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                validationError
            );
            return false;
        }

        result = BistroBuilderCanonicalOrderOperationResult.Success(
            "Comanda canónica creada correctamente.",
            order.OrderId,
            string.Empty
        );
        return true;
    }

    private static bool TryCreateLine(
        BistroBuilderCanonicalOrderLineRequest request,
        BistroBuilderMealServiceAvailability mealService,
        IBistroBuilderOrderDishResolver dishResolver,
        out BistroBuilderCanonicalOrderLine line,
        out BistroBuilderCanonicalOrderOperationResult result
    )
    {
        line = null;

        if (request == null || request.courseIndex < 0 ||
            request.courseIndex > 20)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                "Una línea contiene un pase inválido o es nula."
            );
            return false;
        }

        string dishId =
            BistroBuilderOrderIdUtility.Normalize(request.dishId);

        if (!BistroBuilderOrderIdUtility.IsValid(dishId))
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidReferenceId,
                "Una línea contiene un DishId inválido."
            );
            return false;
        }

        if (!dishResolver.TryResolveOrderableDish(
                dishId,
                mealService,
                out BistroBuilderResolvedOrderDish resolvedDish,
                out string rejectionReason
            ))
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.DishUnavailable,
                string.IsNullOrWhiteSpace(rejectionReason)
                    ? "El plato no puede pedirse."
                    : rejectionReason
            );
            return false;
        }

        if (request.consumerCustomerIds == null ||
            request.consumerCustomerIds.Count == 0)
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidReferenceId,
                "Una línea no contiene consumidores."
            );
            return false;
        }

        List<string> consumers =
            new List<string>(request.consumerCustomerIds.Count);
        HashSet<string> unique =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < request.consumerCustomerIds.Count;
             index++)
        {
            string customerId = BistroBuilderOrderIdUtility.Normalize(
                request.consumerCustomerIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(customerId))
            {
                result = Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .InvalidReferenceId,
                    "Una línea contiene un CustomerId inválido."
                );
                return false;
            }

            if (!unique.Add(customerId))
            {
                result = Failure(
                    BistroBuilderCanonicalOrderFailureReason
                        .DuplicateReferenceId,
                    "Una línea contiene el mismo consumidor más de una vez."
                );
                return false;
            }

            consumers.Add(customerId);
        }

        string primary = BistroBuilderOrderIdUtility.Normalize(
            request.primaryCustomerId
        );

        if (!string.IsNullOrEmpty(primary) &&
            !BistroBuilderOrderIdUtility.IsValid(primary))
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidReferenceId,
                "El cliente principal de una línea no es válido."
            );
            return false;
        }

        if (!string.IsNullOrEmpty(primary) && !unique.Contains(primary))
        {
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidReferenceId,
                "El cliente principal no figura entre los consumidores."
            );
            return false;
        }

        line = new BistroBuilderCanonicalOrderLine(
            BistroBuilderOrderIdUtility.NewLineId(),
            resolvedDish,
            primary,
            consumers,
            request.courseIndex
        );

        if (!line.TryValidate(out string error))
        {
            line = null;
            result = Failure(
                BistroBuilderCanonicalOrderFailureReason.InvalidRequest,
                error
            );
            return false;
        }

        result = BistroBuilderCanonicalOrderOperationResult.Success(
            "Línea canónica creada correctamente.",
            string.Empty,
            line.LineId
        );
        return true;
    }

    private static BistroBuilderCanonicalOrderOperationResult Failure(
        BistroBuilderCanonicalOrderFailureReason reason,
        string message
    )
    {
        return BistroBuilderCanonicalOrderOperationResult.Failure(
            reason,
            message,
            string.Empty,
            string.Empty
        );
    }
}
