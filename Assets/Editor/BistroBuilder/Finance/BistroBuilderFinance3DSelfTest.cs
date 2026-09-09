using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3DSelfTest
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3D - Autotest", false, 3032)]
    public static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3D",
            "Autotest: " + passed + " OK / " + failed + " fallos.",
            "Aceptar"
        );
        if (!ok) Debug.LogError("3D — Autotest fallido.");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report
    )
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();

        try
        {
            bool validationOk = BistroBuilderFinance3DValidator.ValidateCurrentScene(
                out _, out int validationErrors, out _);
            Check(validationOk && validationErrors == 0,
                "La instalación 3D supera el validador estructural.",
                ref passed, ref failed, builder);

            Check(BistroBuilderProductCostEngine.RoundMicroCentsToCents(4999L) == 0L,
                "El redondeo mantiene 0,4999 céntimos en 0.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.RoundMicroCentsToCents(5000L) == 1L,
                "El redondeo AwayFromZero convierte 0,5 céntimos en 1.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.RoundMicroCentsToCents(14999L) == 1L,
                "No se redondea antes de cerrar el coste de línea.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.RoundMicroCentsToCents(15000L) == 2L,
                "El cierre a céntimos redondea una sola vez.",
                ref passed, ref failed, builder);

            var basisByLot = new Dictionary<string, BistroBuilderLotCostBasisRecord>(StringComparer.Ordinal)
            {
                ["inventory_lot_00000001"] = new BistroBuilderLotCostBasisRecord
                {
                    lotId = "inventory_lot_00000001",
                    ingredientId = "ingredient_test",
                    sourceReferenceId = "purchase_order_test",
                    basisKind = BistroBuilderLotCostBasisKind.SupplierActual,
                    basisQuantityCanonicalMilliUnits = 2000L,
                    totalCostMicroCents = 100000L,
                    receivedDayIndex = 1
                },
                ["inventory_lot_00000002"] = new BistroBuilderLotCostBasisRecord
                {
                    lotId = "inventory_lot_00000002",
                    ingredientId = "ingredient_test",
                    sourceReferenceId = "opening_test",
                    basisKind = BistroBuilderLotCostBasisKind.ReferenceEstimate,
                    basisQuantityCanonicalMilliUnits = 1000L,
                    totalCostMicroCents = 200000L,
                    receivedDayIndex = 1
                }
            };

            var reservation = new BistroBuilderInventoryReservationSnapshot(
                "inventory_reservation_test",
                "order_line_test",
                BistroBuilderInventoryReservationStatus.Active,
                1L,
                new List<BistroBuilderInventoryReservationLineSnapshot>
                {
                    new BistroBuilderInventoryReservationLineSnapshot(
                        "ingredient_test",
                        1000L,
                        new List<BistroBuilderInventoryLotAllocationSnapshot>
                        {
                            new BistroBuilderInventoryLotAllocationSnapshot(
                                "inventory_lot_00000001", 600L),
                            new BistroBuilderInventoryLotAllocationSnapshot(
                                "inventory_lot_00000002", 400L)
                        })
                });

            bool actualOk = BistroBuilderProductCostEngine.TryCalculateActualCost(
                reservation,
                basisByLot,
                out long mixedCost,
                out BistroBuilderProductCostQuality mixedQuality,
                out string error);
            Check(actualOk,
                "La valoración FEFO mixta se calcula sin error: " + error,
                ref passed, ref failed, builder);
            Check(mixedCost == 110000L,
                "El coste FEFO usa proporcionalmente la base de cada lote.",
                ref passed, ref failed, builder);
            Check(mixedQuality == BistroBuilderProductCostQuality.Mixed,
                "Combinar lote real y estimado produce calidad Mixed.",
                ref passed, ref failed, builder);

            basisByLot["inventory_lot_00000002"].basisKind =
                BistroBuilderLotCostBasisKind.SupplierActual;
            Check(BistroBuilderProductCostEngine.TryCalculateActualCost(
                    reservation, basisByLot, out _, out var actualQuality, out _) &&
                  actualQuality == BistroBuilderProductCostQuality.Actual,
                "Todos los lotes reales producen calidad Actual.",
                ref passed, ref failed, builder);

            basisByLot["inventory_lot_00000001"].basisKind =
                BistroBuilderLotCostBasisKind.ReferenceEstimate;
            basisByLot["inventory_lot_00000002"].basisKind =
                BistroBuilderLotCostBasisKind.ReferenceEstimate;
            Check(BistroBuilderProductCostEngine.TryCalculateActualCost(
                    reservation, basisByLot, out _, out var estimatedQuality, out _) &&
                  estimatedQuality == BistroBuilderProductCostQuality.Estimated,
                "Todos los lotes de referencia producen calidad Estimated.",
                ref passed, ref failed, builder);

            var missingBasis = new Dictionary<string, BistroBuilderLotCostBasisRecord>(
                basisByLot,
                StringComparer.Ordinal);
            missingBasis.Remove("inventory_lot_00000002");
            Check(!BistroBuilderProductCostEngine.TryCalculateActualCost(
                    reservation, missingBasis, out _, out _, out _),
                "Una asignación sin base de coste se rechaza.",
                ref passed, ref failed, builder);

            var malformedReservation = new BistroBuilderInventoryReservationSnapshot(
                "inventory_reservation_bad",
                "order_line_bad",
                BistroBuilderInventoryReservationStatus.Active,
                1L,
                new List<BistroBuilderInventoryReservationLineSnapshot>
                {
                    new BistroBuilderInventoryReservationLineSnapshot(
                        "ingredient_test",
                        1001L,
                        new List<BistroBuilderInventoryLotAllocationSnapshot>
                        {
                            new BistroBuilderInventoryLotAllocationSnapshot(
                                "inventory_lot_00000001", 1000L)
                        })
                });
            Check(!BistroBuilderProductCostEngine.TryCalculateActualCost(
                    malformedReservation, basisByLot, out _, out _, out _),
                "Una asignación FEFO que no cubre su línea se rechaza.",
                ref passed, ref failed, builder);

            Check(BistroBuilderProductCostEngine.CalculateMarginBasisPoints(1000L, 400L) == 6000,
                "Margen 10,00 - 4,00 equivale a 60,00%.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.CalculateMarginBasisPoints(0L, 0L) == 0,
                "Un plato gratuito no divide entre cero.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.ResolveMarginBand(-1L, -1) ==
                  BistroBuilderRecipeMarginBand.Loss,
                "Margen monetario negativo se clasifica como Loss.",
                ref passed, ref failed, builder);
            Check(BistroBuilderProductCostEngine.ResolveMarginBand(1L, 6000) ==
                  BistroBuilderRecipeMarginBand.High,
                "El umbral del 60% conserva la clasificación existente.",
                ref passed, ref failed, builder);

            var snapshot = BuildValidSnapshot();
            Check(BistroBuilderProductCostEngine.TryValidateSnapshot(snapshot, out error),
                "Snapshot 3D completo válido: " + error,
                ref passed, ref failed, builder);
            Check(snapshot.DeepClone().consumedLineCosts[0] != snapshot.consumedLineCosts[0],
                "DeepClone no comparte registros de líneas.",
                ref passed, ref failed, builder);
            Check(snapshot.DeepClone().lotCostBases[0] != snapshot.lotCostBases[0],
                "DeepClone no comparte bases de lote.",
                ref passed, ref failed, builder);

            BistroBuilderProductCostSnapshot duplicateLot = snapshot.DeepClone();
            duplicateLot.lotCostBases.Add(duplicateLot.lotCostBases[0].DeepClone());
            Check(!BistroBuilderProductCostEngine.TryValidateSnapshot(duplicateLot, out _),
                "Un LotId duplicado se rechaza.",
                ref passed, ref failed, builder);

            BistroBuilderProductCostSnapshot duplicateLine = snapshot.DeepClone();
            duplicateLine.consumedLineCosts.Add(
                duplicateLine.consumedLineCosts[0].DeepClone());
            duplicateLine.consumedLineCosts[1].sequence = 2L;
            duplicateLine.consumedLineCosts[1].costRecordId =
                BistroBuilderProductCostEngine.BuildCostRecordId(2L);
            duplicateLine.nextLineCostSequence = 3L;
            Check(!BistroBuilderProductCostEngine.TryValidateSnapshot(duplicateLine, out _),
                "Un LineId ya valorado no puede duplicarse.",
                ref passed, ref failed, builder);

            BistroBuilderProductCostSnapshot brokenSequence = snapshot.DeepClone();
            brokenSequence.nextLineCostSequence = 99L;
            Check(!BistroBuilderProductCostEngine.TryValidateSnapshot(brokenSequence, out _),
                "Una secuencia discontinua se rechaza.",
                ref passed, ref failed, builder);

            BistroBuilderProductCostSnapshot brokenActual = snapshot.DeepClone();
            brokenActual.consumedLineCosts[0].actualCostCents++;
            Check(!BistroBuilderProductCostEngine.TryValidateSnapshot(brokenActual, out _),
                "El coste redondeado debe coincidir con microcéntimos.",
                ref passed, ref failed, builder);

            BistroBuilderProductCostSnapshot brokenMargin = snapshot.DeepClone();
            brokenMargin.consumedLineCosts[0].actualMarginCents++;
            Check(!BistroBuilderProductCostEngine.TryValidateSnapshot(brokenMargin, out _),
                "El margen no puede contradecir precio y coste.",
                ref passed, ref failed, builder);

            var createdLots = new List<BistroBuilderInventoryLotSnapshot>
            {
                new BistroBuilderInventoryLotSnapshot(
                    "inventory_lot_00000003",
                    "ingredient_test",
                    "supplier_test",
                    2,
                    0,
                    1000L,
                    0L,
                    BistroBuilderInventoryFreshnessState.Fresh,
                    2L)
            };
            var receipt = new BistroBuilderGoodsReceiptSnapshot(
                "receipt_test",
                "supplier_test",
                2,
                2L,
                false,
                new List<BistroBuilderGoodsReceiptLineSnapshot>
                {
                    new BistroBuilderGoodsReceiptLineSnapshot(
                        "ingredient_test", 1000L)
                },
                createdLots);
            createdLots.Clear();
            Check(receipt.CreatedLots.Count == 1,
                "La recepción conserva una copia propia de los lotes creados.",
                ref passed, ref failed, builder);
            Check(receipt.CreatedLots[0].LotId == "inventory_lot_00000003",
                "La trazabilidad de recepción conserva el LotId exacto.",
                ref passed, ref failed, builder);

            var legacyReceipt = new BistroBuilderGoodsReceiptSnapshot(
                "receipt_legacy",
                "supplier_test",
                2,
                2L,
                false,
                new List<BistroBuilderGoodsReceiptLineSnapshot>());
            Check(legacyReceipt.CreatedLots.Count == 0,
                "El constructor histórico de recepción sigue siendo compatible.",
                ref passed, ref failed, builder);

            Check(BistroBuilderProductCostSnapshot.CurrentSchemaId ==
                  "finance.product_cost.runtime" &&
                  BistroBuilderProductCostSnapshot.CurrentSchemaVersion == 1,
                "3D publica un esquema persistente propio v1.",
                ref passed, ref failed, builder);
            Check(BistroBuilderLotCostBasisKind.SupplierActual !=
                  BistroBuilderLotCostBasisKind.ReferenceEstimate,
                "Coste real y estimado permanecen dimensiones explícitas.",
                ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine(
                "[ERROR] Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message);
        }

        builder.Insert(0,
            "3D — AUTOTEST COSTES DE PRODUCTO Y MÁRGENES\n" +
            "Correctos: " + passed + "  Fallos: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static BistroBuilderProductCostSnapshot BuildValidSnapshot()
    {
        var snapshot = new BistroBuilderProductCostSnapshot();
        snapshot.lotCostBases.Add(new BistroBuilderLotCostBasisRecord
        {
            lotId = "inventory_lot_00000001",
            ingredientId = "ingredient_test",
            sourceReferenceId = "purchase_order_test",
            basisKind = BistroBuilderLotCostBasisKind.SupplierActual,
            basisQuantityCanonicalMilliUnits = 1000L,
            totalCostMicroCents = 4000000L,
            receivedDayIndex = 1
        });

        long theoreticalMicro = 3500000L;
        long actualMicro = 4000000L;
        long theoreticalCents =
            BistroBuilderProductCostEngine.RoundMicroCentsToCents(theoreticalMicro);
        long actualCents =
            BistroBuilderProductCostEngine.RoundMicroCentsToCents(actualMicro);
        int sale = 1000;
        long theoreticalMargin = sale - theoreticalCents;
        long actualMargin = sale - actualCents;
        int theoreticalBps =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                sale, theoreticalCents);
        int actualBps =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                sale, actualCents);

        snapshot.consumedLineCosts.Add(new BistroBuilderConsumedLineCostRecord
        {
            sequence = 1L,
            costRecordId = BistroBuilderProductCostEngine.BuildCostRecordId(1L),
            orderId = "order_000001",
            lineId = "order_line_000001",
            dishId = "dish_test",
            mealService = BistroBuilderMealServiceAvailability.Lunch,
            serviceMode = BistroBuilderServiceMode.TableService,
            dayIndex = 1,
            minuteOfDay = 720,
            salePriceCents = sale,
            theoreticalCostMicroCents = theoreticalMicro,
            theoreticalCostCents = theoreticalCents,
            actualCostMicroCents = actualMicro,
            actualCostCents = actualCents,
            theoreticalMarginCents = theoreticalMargin,
            theoreticalMarginBasisPoints = theoreticalBps,
            theoreticalMarginBand = BistroBuilderProductCostEngine.ResolveMarginBand(
                theoreticalMargin, theoreticalBps),
            actualMarginCents = actualMargin,
            actualMarginBasisPoints = actualBps,
            actualMarginBand = BistroBuilderProductCostEngine.ResolveMarginBand(
                actualMargin, actualBps),
            costQuality = BistroBuilderProductCostQuality.Actual
        });
        snapshot.nextLineCostSequence = 2L;
        snapshot.revision = 3L;
        return snapshot;
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder
    )
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }
}
