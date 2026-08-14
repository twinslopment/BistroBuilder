using System;
using System.Collections.Generic;

/// <summary>
/// Cálculos puros de coste de producto. No consulta Unity, inventario ni caja.
/// </summary>
public static class BistroBuilderProductCostEngine
{
    public static long RoundMicroCentsToCents(long microCents)
    {
        if (microCents < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(microCents));
        }

        return (long)decimal.Round(
            microCents / (decimal)BistroBuilderIngredientDefinition.MicroCentsPerCent,
            0,
            MidpointRounding.AwayFromZero
        );
    }

    public static bool TryCalculateActualCost(
        BistroBuilderInventoryReservationSnapshot reservation,
        IReadOnlyDictionary<string, BistroBuilderLotCostBasisRecord> basisByLotId,
        out long actualCostMicroCents,
        out BistroBuilderProductCostQuality quality,
        out string error
    )
    {
        actualCostMicroCents = 0L;
        quality = BistroBuilderProductCostQuality.Estimated;
        error = string.Empty;

        if (reservation == null ||
            reservation.Lines == null ||
            reservation.Lines.Count == 0 ||
            basisByLotId == null)
        {
            error = "La reserva o las bases de coste no son válidas.";
            return false;
        }

        decimal rawMicroCents = 0m;
        int actualAllocations = 0;
        int estimatedAllocations = 0;

        for (int lineIndex = 0; lineIndex < reservation.Lines.Count; lineIndex++)
        {
            BistroBuilderInventoryReservationLineSnapshot line =
                reservation.Lines[lineIndex];
            if (line == null || line.CanonicalMilliUnits <= 0L ||
                line.LotAllocations == null || line.LotAllocations.Count == 0)
            {
                error = "La reserva no conserva una asignación FEFO valorable.";
                return false;
            }

            long allocatedLine = 0L;
            for (int allocationIndex = 0;
                 allocationIndex < line.LotAllocations.Count;
                 allocationIndex++)
            {
                BistroBuilderInventoryLotAllocationSnapshot allocation =
                    line.LotAllocations[allocationIndex];
                if (allocation.CanonicalMilliUnits <= 0L ||
                    !basisByLotId.TryGetValue(
                        allocation.LotId,
                        out BistroBuilderLotCostBasisRecord basis
                    ) ||
                    basis == null ||
                    basis.basisQuantityCanonicalMilliUnits <= 0L ||
                    basis.totalCostMicroCents < 0L ||
                    !string.Equals(
                        basis.ingredientId,
                        line.IngredientId,
                        StringComparison.Ordinal
                    ))
                {
                    error = "Falta una base de coste válida para el lote " +
                            allocation.LotId + ".";
                    return false;
                }

                allocatedLine = checked(
                    allocatedLine + allocation.CanonicalMilliUnits
                );
                rawMicroCents +=
                    (decimal)allocation.CanonicalMilliUnits *
                    basis.totalCostMicroCents /
                    basis.basisQuantityCanonicalMilliUnits;

                if (basis.basisKind ==
                    BistroBuilderLotCostBasisKind.SupplierActual)
                {
                    actualAllocations++;
                }
                else
                {
                    estimatedAllocations++;
                }
            }

            if (allocatedLine != line.CanonicalMilliUnits)
            {
                error = "La asignación FEFO no coincide con la cantidad de " +
                        line.IngredientId + ".";
                return false;
            }
        }

        decimal rounded = decimal.Round(
            rawMicroCents,
            0,
            MidpointRounding.AwayFromZero
        );
        if (rounded < 0m || rounded > long.MaxValue)
        {
            error = "El coste real calculado queda fuera de rango.";
            return false;
        }

        actualCostMicroCents = (long)rounded;
        quality = actualAllocations > 0 && estimatedAllocations == 0
            ? BistroBuilderProductCostQuality.Actual
            : actualAllocations > 0
                ? BistroBuilderProductCostQuality.Mixed
                : BistroBuilderProductCostQuality.Estimated;
        return true;
    }

    public static int CalculateMarginBasisPoints(
        long salePriceCents,
        long costCents
    )
    {
        if (salePriceCents <= 0L)
        {
            return 0;
        }

        decimal margin = salePriceCents - costCents;
        decimal rounded = decimal.Round(
            margin * 10000m / salePriceCents,
            0,
            MidpointRounding.AwayFromZero
        );

        if (rounded > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (rounded < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)rounded;
    }

    public static BistroBuilderRecipeMarginBand ResolveMarginBand(
        long marginCents,
        int marginBasisPoints
    )
    {
        if (marginCents < 0L)
        {
            return BistroBuilderRecipeMarginBand.Loss;
        }
        if (marginBasisPoints < 4500)
        {
            return BistroBuilderRecipeMarginBand.Low;
        }
        if (marginBasisPoints < 6000)
        {
            return BistroBuilderRecipeMarginBand.Correct;
        }
        if (marginBasisPoints < 7500)
        {
            return BistroBuilderRecipeMarginBand.High;
        }
        return BistroBuilderRecipeMarginBand.Excellent;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderProductCostSnapshot snapshot,
        out string error
    )
    {
        error = string.Empty;
        if (snapshot == null ||
            !string.Equals(
                snapshot.schemaId,
                BistroBuilderProductCostSnapshot.CurrentSchemaId,
                StringComparison.Ordinal
            ) ||
            snapshot.schemaVersion !=
                BistroBuilderProductCostSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L ||
            snapshot.nextLineCostSequence < 1L ||
            snapshot.lotCostBases == null ||
            snapshot.consumedLineCosts == null)
        {
            error = "El snapshot de costes de producto no es válido.";
            return false;
        }

        var lotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.lotCostBases.Count; index++)
        {
            BistroBuilderLotCostBasisRecord basis = snapshot.lotCostBases[index];
            if (basis == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(basis.lotId) ||
                !BistroBuilderMenuIdUtility.IsValidStableId(basis.ingredientId) ||
                string.IsNullOrWhiteSpace(basis.sourceReferenceId) ||
                !Enum.IsDefined(typeof(BistroBuilderLotCostBasisKind), basis.basisKind) ||
                basis.basisQuantityCanonicalMilliUnits <= 0L ||
                basis.totalCostMicroCents < 0L ||
                basis.receivedDayIndex < 1 ||
                !lotIds.Add(basis.lotId))
            {
                error = "El snapshot contiene una base de coste de lote inválida.";
                return false;
            }
        }

        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        long expectedSequence = 1L;
        for (int index = 0; index < snapshot.consumedLineCosts.Count; index++)
        {
            BistroBuilderConsumedLineCostRecord record =
                snapshot.consumedLineCosts[index];
            if (record == null ||
                record.sequence != expectedSequence ||
                !string.Equals(
                    record.costRecordId,
                    BuildCostRecordId(expectedSequence),
                    StringComparison.Ordinal
                ) ||
                !BistroBuilderOrderIdUtility.IsValid(record.orderId) ||
                !BistroBuilderOrderIdUtility.IsValid(record.lineId) ||
                !BistroBuilderOrderIdUtility.IsValid(record.dishId) ||
                !lineIds.Add(record.lineId) ||
                record.dayIndex < 1 ||
                record.minuteOfDay < 0 || record.minuteOfDay > 1439 ||
                record.salePriceCents < 0 ||
                record.theoreticalCostMicroCents < 0L ||
                record.actualCostMicroCents < 0L ||
                record.theoreticalCostCents !=
                    RoundMicroCentsToCents(record.theoreticalCostMicroCents) ||
                record.actualCostCents !=
                    RoundMicroCentsToCents(record.actualCostMicroCents) ||
                record.theoreticalMarginCents !=
                    record.salePriceCents - record.theoreticalCostCents ||
                record.actualMarginCents !=
                    record.salePriceCents - record.actualCostCents ||
                record.theoreticalMarginBasisPoints !=
                    CalculateMarginBasisPoints(
                        record.salePriceCents,
                        record.theoreticalCostCents
                    ) ||
                record.actualMarginBasisPoints !=
                    CalculateMarginBasisPoints(
                        record.salePriceCents,
                        record.actualCostCents
                    ) ||
                record.theoreticalMarginBand !=
                    ResolveMarginBand(
                        record.theoreticalMarginCents,
                        record.theoreticalMarginBasisPoints
                    ) ||
                record.actualMarginBand !=
                    ResolveMarginBand(
                        record.actualMarginCents,
                        record.actualMarginBasisPoints
                    ) ||
                !Enum.IsDefined(
                    typeof(BistroBuilderProductCostQuality),
                    record.costQuality
                ))
            {
                error = "El snapshot contiene un coste de línea inválido.";
                return false;
            }

            expectedSequence++;
        }

        if (snapshot.nextLineCostSequence != expectedSequence)
        {
            error = "La secuencia de costes de línea no es continua.";
            return false;
        }

        return true;
    }

    public static string BuildCostRecordId(long sequence)
    {
        return "product_cost_" + sequence.ToString("D10");
    }
}
