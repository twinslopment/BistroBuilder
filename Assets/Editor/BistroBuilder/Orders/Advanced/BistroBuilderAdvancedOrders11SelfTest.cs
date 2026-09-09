using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Autotest puro del dominio de Comandas avanzadas.</summary>
public static class BistroBuilderAdvancedOrders11SelfTest
{
    public static bool Run(out int passed, out int failed, out string report)
    {
        int localPassed = 0; int localFailed = 0;
        var lines = new List<string>();
        Action<bool,string> check = (condition,message) =>
        {
            if (condition) { localPassed++; lines.Add("[OK] " + message); }
            else { localFailed++; lines.Add("[FAIL] " + message); }
        };
        try
        {
            check(BistroBuilderAdvancedOrderMutationPolicy.CanCorrect(BistroBuilderCanonicalOrderLineState.Draft), "Draft admite corrección.");
            check(BistroBuilderAdvancedOrderMutationPolicy.CanCorrect(BistroBuilderCanonicalOrderLineState.Submitted), "Submitted admite corrección.");
            check(BistroBuilderAdvancedOrderMutationPolicy.CanCancel(BistroBuilderCanonicalOrderLineState.Queued), "Queued admite cancelación parcial.");
            check(!BistroBuilderAdvancedOrderMutationPolicy.CanCancel(BistroBuilderCanonicalOrderLineState.Preparing), "Preparing bloquea cancelación simple.");
            check(BistroBuilderAdvancedOrderMutationPolicy.CanReplaceAfterIncident(BistroBuilderCanonicalOrderLineState.Preparing), "Preparing permite resolución por incidencia/reposición.");
            check(BistroBuilderAdvancedOrderMutationPolicy.CanReturn(BistroBuilderCanonicalOrderLineState.Served), "Served permite devolución.");
            check(!BistroBuilderAdvancedOrderMutationPolicy.CanReturn(BistroBuilderCanonicalOrderLineState.Consumed), "Consumed bloquea devolución tardía.");
            check(BistroBuilderAdvancedOrderMutationPolicy.CanRepeat(BistroBuilderCanonicalOrderLineState.Served), "Una consumición servida puede repetirse.");

            BistroBuilderCanonicalOrderCreationRequest request = BuildRequest();
            bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
                request, new TestDishResolver(), 1,
                out BistroBuilderCanonicalOrder order,
                out BistroBuilderCanonicalOrderOperationResult createResult);
            check(created && createResult.Succeeded && order != null, "La fábrica crea fixture canónico 11.");
            if (order != null)
            {
                BistroBuilderCanonicalOrderLine original = order.Lines[0];
                int originalPrice = original.PriceCentsAtOrder;
                string sourceLineId = original.LineId;
                check(order.CalculateTotalPriceCents() == originalPrice, "La cuenta inicial incluye la línea original.");

                var repeat = new BistroBuilderCanonicalOrderLine(
                    "order_line_11_repeat_001",
                    new BistroBuilderResolvedOrderDish(
                        "dish_repeat_11", 1600, 0, false, "restaurant_11", 1),
                    original.PrimaryCustomerId,
                    new List<string>(original.ConsumerCustomerIds),
                    original.CourseIndex);
                bool metadata = repeat.TryApplyAdvancedMetadata(
                    BistroBuilderAdvancedOrderLineOriginKind.Repeat,
                    BistroBuilderAdvancedOrderBillingMode.Standard,
                    sourceLineId,
                    BistroBuilderAdvancedOrderIncidentKind.None,
                    "Repetición de prueba.", "test_11", out _);
                check(metadata && order.TryAddAdvancedLine(repeat, out _), "Una repetición conserva SourceLineId y entra en el agregado.");
                check(order.CalculateTotalPriceCents() == originalPrice + 1600, "La repetición incrementa la cuenta una sola vez.");

                var courtesy = new BistroBuilderCanonicalOrderLine(
                    "order_line_11_courtesy_001",
                    new BistroBuilderResolvedOrderDish(
                        "dish_courtesy_11", 2200, 0, false, "restaurant_11", 1),
                    original.PrimaryCustomerId,
                    new List<string>(original.ConsumerCustomerIds),
                    original.CourseIndex);
                courtesy.TryApplyAdvancedMetadata(
                    BistroBuilderAdvancedOrderLineOriginKind.Courtesy,
                    BistroBuilderAdvancedOrderBillingMode.Courtesy,
                    sourceLineId,
                    BistroBuilderAdvancedOrderIncidentKind.KitchenError,
                    "Reposición de cortesía.", "test_11", out _);
                check(order.TryAddAdvancedLine(courtesy, out _) && !courtesy.IsChargeable,
                    "La reposición de cortesía queda trazada y no es facturable.");
                check(order.CalculateTotalPriceCents() == originalPrice + 1600,
                    "Una cortesía no altera la cuenta.");

                order.TryTransitionLine(sourceLineId,
                    BistroBuilderCanonicalOrderLineState.Submitted, "test_11", out _);
                order.TryTransitionLine(sourceLineId,
                    BistroBuilderCanonicalOrderLineState.Queued, "test_11", out _);
                order.TryTransitionLine(sourceLineId,
                    BistroBuilderCanonicalOrderLineState.Preparing, "test_11", out _);
                bool incident = order.TryRegisterAdvancedIncident(
                    sourceLineId, BistroBuilderAdvancedOrderIncidentKind.QualityIssue,
                    "Calidad insuficiente.", "test_11", out _);
                bool failedLine = order.TryTransitionLine(sourceLineId,
                    BistroBuilderCanonicalOrderLineState.Failed, "test_11", out _);
                check(incident && failedLine, "Una incidencia post-preparación puede cerrar la línea como Failed.");
                check(order.CalculateTotalPriceCents() == 1600,
                    "La línea devuelta/failed desaparece de la cuenta sin duplicar cobros.");

                BistroBuilderCanonicalOrder clone = order.Clone();
                clone.TryGetLine("order_line_11_courtesy_001", out BistroBuilderCanonicalOrderLine clonedCourtesy);
                check(clonedCourtesy != null &&
                    clonedCourtesy.AdvancedOriginKind == BistroBuilderAdvancedOrderLineOriginKind.Courtesy &&
                    clonedCourtesy.AdvancedIncidentKind == BistroBuilderAdvancedOrderIncidentKind.KitchenError &&
                    clonedCourtesy.AdvancedSourceLineId == sourceLineId,
                    "Clone conserva origen, incidencia, billing y SourceLineId.");

                var runtime = new BistroBuilderCanonicalOrderRuntimeSnapshot(
                    5, new List<BistroBuilderCanonicalOrder> { order });
                string json = JsonUtility.ToJson(runtime);
                check(json.Contains("advancedOriginKind") && json.Contains("advancedBillingMode") &&
                    json.Contains("advancedSourceLineId") && json.Contains("advancedIncidentKind"),
                    "service.runtime serializa toda la metadata 11.");
                BistroBuilderCanonicalOrderRuntimeSnapshot restored =
                    JsonUtility.FromJson<BistroBuilderCanonicalOrderRuntimeSnapshot>(json);
                check(restored != null && restored.TryValidate(out _) && restored.Orders.Count == 1,
                    "La metadata 11 sobrevive round-trip persistente de service.runtime.");

                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.Submitted, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.Queued, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.Preparing, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.ReadyForPickup, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.AssignedForDelivery, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.InTransit, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.Served, "test_11", out _);
                order.TryTransitionLine(repeat.LineId,
                    BistroBuilderCanonicalOrderLineState.Consumed, "test_11", out _);
                order.TryTransitionLine(courtesy.LineId,
                    BistroBuilderCanonicalOrderLineState.Cancelled, "test_11", out _);
                check(order.State == BistroBuilderCanonicalOrderState.Completed,
                    "Una comanda con consumo válido y líneas Failed/Cancelled cierra como Completed.");
                check(order.TryValidate(out _),
                    "El agregado Completed con incidencias resueltas sigue siendo persistible y válido.");
            }
        }
        catch (Exception exception)
        {
            localFailed++;
            lines.Add("[FAIL] Excepción: " + exception.Message);
        }
        report = "=== BISTRO BUILDER — BLOQUE 11 / AUTOTEST ===\n" +
            string.Join("\n", lines) + "\nResultado: " + localPassed + " OK / " + localFailed + " fallos.";
        passed = localPassed;
        failed = localFailed;
        return localFailed == 0;
    }

    private static BistroBuilderCanonicalOrderCreationRequest BuildRequest()
    {
        var request = new BistroBuilderCanonicalOrderCreationRequest
        {
            externalReferenceId = "legacy_order_11_test",
            tableReferenceId = "table_11_test",
            customerGroupReferenceId = "group_11_test",
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch
        };
        request.lines.Add(new BistroBuilderCanonicalOrderLineRequest(
            "dish_original_11", "customer_11_01",
            new[] { "customer_11_01" }, 0));
        return request;
    }

    private sealed class TestDishResolver : IBistroBuilderOrderDishResolver
    {
        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish dish,
            out string rejectionReason)
        {
            dish = new BistroBuilderResolvedOrderDish(
                dishId, 1200, 0, false, "restaurant_11", 1);
            rejectionReason = string.Empty;
            return true;
        }
    }
}
