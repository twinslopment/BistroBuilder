using System;
using System.Collections.Generic;

/// <summary>
/// Fotografía persistible y versionada del inventario canónico.
///
/// 2.2A eleva el snapshot a v2 para que los lotes internos y las asignaciones
/// FEFO formen parte de la misma fuente autoritativa que balances, reservas y
/// libro de movimientos. No contiene referencias a objetos Unity.
/// </summary>
[Serializable]
public sealed class BistroBuilderInventoryRuntimeSnapshot
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public long nextTransactionSequence = 1L;
    public long nextLotSequence = 1L;
    public long runtimeRevision;
    public int lastShelfLifeProcessedDayIndex;

    /// <summary>
    /// Solo puede aparecer en el payload producido por la migración v1->v2.
    /// El servicio lo materializa a lotes reales al aplicar la carga.
    /// Nunca se vuelve a capturar como true.
    /// </summary>
    public bool requiresLotMaterialization;

    public List<BistroBuilderInventoryStockSaveRecord> stock =
        new List<BistroBuilderInventoryStockSaveRecord>();
    public List<BistroBuilderInventoryLotSaveRecord> lots =
        new List<BistroBuilderInventoryLotSaveRecord>();
    public List<BistroBuilderInventoryReservationSaveRecord> reservations =
        new List<BistroBuilderInventoryReservationSaveRecord>();
    public List<BistroBuilderInventoryOperationSaveRecord> operations =
        new List<BistroBuilderInventoryOperationSaveRecord>();
    public List<BistroBuilderInventoryTransactionSaveRecord> ledger =
        new List<BistroBuilderInventoryTransactionSaveRecord>();

    public bool TryValidateBasic(out string error)
    {
        error = string.Empty;

        if (schemaVersion != CurrentSchemaVersion)
        {
            error = "La versión del snapshot de inventario no es compatible.";
            return false;
        }

        if (nextTransactionSequence < 1L || nextLotSequence < 1L ||
            runtimeRevision < 0L || lastShelfLifeProcessedDayIndex < 0)
        {
            error = "Las secuencias del snapshot de inventario son inválidas.";
            return false;
        }

        if (stock == null || lots == null || reservations == null ||
            operations == null || ledger == null)
        {
            error = "El snapshot de inventario contiene colecciones nulas.";
            return false;
        }

        var ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        var stockById =
            new Dictionary<string, BistroBuilderInventoryStockSaveRecord>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < stock.Count; index++)
        {
            BistroBuilderInventoryStockSaveRecord record = stock[index];

            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }

            if (!ingredientIds.Add(record.ingredientId))
            {
                error = "El snapshot repite el ingrediente " +
                        record.ingredientId + ".";
                return false;
            }

            stockById.Add(record.ingredientId, record);
        }

        var lotIds = new HashSet<string>(StringComparer.Ordinal);
        var lotOnHandByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var lotReservedByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var lotById =
            new Dictionary<string, BistroBuilderInventoryLotSaveRecord>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < lots.Count; index++)
        {
            BistroBuilderInventoryLotSaveRecord lot = lots[index];

            if (lot == null || !lot.TryValidate(out error))
            {
                return false;
            }

            if (!stockById.ContainsKey(lot.ingredientId))
            {
                error = "El lote " + lot.lotId +
                        " referencia un ingrediente sin balance.";
                return false;
            }

            if (!lotIds.Add(lot.lotId))
            {
                error = "El snapshot repite el lote " + lot.lotId + ".";
                return false;
            }

            if (!requiresLotMaterialization &&
                lot.expirationDayIndex > 0 &&
                lot.expirationDayIndex <= lastShelfLifeProcessedDayIndex &&
                lot.onHandCanonicalMilliUnits -
                    lot.reservedCanonicalMilliUnits > 0L)
            {
                error = "El lote " + lot.lotId +
                        " conserva cantidad libre después de su caducidad " +
                        "ya procesada.";
                return false;
            }

            lotById.Add(lot.lotId, lot);

            try
            {
                lotOnHandByIngredient.TryGetValue(
                    lot.ingredientId,
                    out long onHand
                );
                lotReservedByIngredient.TryGetValue(
                    lot.ingredientId,
                    out long reserved
                );
                lotOnHandByIngredient[lot.ingredientId] =
                    checked(onHand + lot.onHandCanonicalMilliUnits);
                lotReservedByIngredient[lot.ingredientId] =
                    checked(reserved + lot.reservedCanonicalMilliUnits);
            }
            catch (OverflowException)
            {
                error = "Los lotes desbordan el rango del inventario.";
                return false;
            }
        }

        var reservationIds = new HashSet<string>(StringComparer.Ordinal);
        var allocatedByLot = new Dictionary<string, long>(StringComparer.Ordinal);

        for (int index = 0; index < reservations.Count; index++)
        {
            BistroBuilderInventoryReservationSaveRecord record =
                reservations[index];

            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }

            if (!reservationIds.Add(record.reservationId))
            {
                error = "El snapshot repite la reserva " +
                        record.reservationId + ".";
                return false;
            }

            bool active = (BistroBuilderInventoryReservationStatus)
                record.status == BistroBuilderInventoryReservationStatus.Active;

            for (int lineIndex = 0;
                 lineIndex < record.lines.Count;
                 lineIndex++)
            {
                BistroBuilderInventoryReservationLineSaveRecord line =
                    record.lines[lineIndex];

                if (!stockById.ContainsKey(line.ingredientId))
                {
                    error = "La reserva " + record.reservationId +
                            " referencia un ingrediente sin balance.";
                    return false;
                }

                if (requiresLotMaterialization)
                {
                    continue;
                }

                if (active &&
                    (line.lotAllocations == null ||
                     line.lotAllocations.Count == 0))
                {
                    error = "La reserva activa " + record.reservationId +
                            " no conserva sus asignaciones FEFO.";
                    return false;
                }

                if (!active && line.lotAllocations != null &&
                    line.lotAllocations.Count > 0)
                {
                    error = "La reserva cerrada " + record.reservationId +
                            " conserva asignaciones de lote activas.";
                    return false;
                }

                long allocatedLine = 0L;
                if (line.lotAllocations != null)
                {
                    for (int allocationIndex = 0;
                         allocationIndex < line.lotAllocations.Count;
                         allocationIndex++)
                    {
                        BistroBuilderInventoryLotAllocationSaveRecord allocation =
                            line.lotAllocations[allocationIndex];
                        if (allocation == null ||
                            !allocation.TryValidate(out error))
                        {
                            return false;
                        }

                        if (!lotById.TryGetValue(
                                allocation.lotId,
                                out BistroBuilderInventoryLotSaveRecord lot
                            ) ||
                            !string.Equals(
                                lot.ingredientId,
                                line.ingredientId,
                                StringComparison.Ordinal
                            ))
                        {
                            error = "La asignación FEFO de " +
                                    record.reservationId +
                                    " referencia un lote incompatible.";
                            return false;
                        }

                        try
                        {
                            allocatedLine = checked(
                                allocatedLine +
                                allocation.canonicalMilliUnits
                            );
                            allocatedByLot.TryGetValue(
                                allocation.lotId,
                                out long current
                            );
                            allocatedByLot[allocation.lotId] = checked(
                                current + allocation.canonicalMilliUnits
                            );
                        }
                        catch (OverflowException)
                        {
                            error = "Las asignaciones FEFO desbordan el rango.";
                            return false;
                        }
                    }
                }

                if (active && allocatedLine != line.canonicalMilliUnits)
                {
                    error = "La reserva " + record.reservationId +
                            " no asigna exactamente su cantidad a lotes.";
                    return false;
                }
            }
        }

        if (!requiresLotMaterialization)
        {
            foreach (KeyValuePair<string, BistroBuilderInventoryStockSaveRecord>
                     pair in stockById)
            {
                lotOnHandByIngredient.TryGetValue(pair.Key, out long onHand);
                lotReservedByIngredient.TryGetValue(pair.Key, out long reserved);

                if (pair.Value.onHandCanonicalMilliUnits != onHand ||
                    pair.Value.reservedCanonicalMilliUnits != reserved)
                {
                    error = "Los lotes de " + pair.Key +
                            " no coinciden con su balance agregado.";
                    return false;
                }
            }

            foreach (KeyValuePair<string, BistroBuilderInventoryLotSaveRecord>
                     pair in lotById)
            {
                allocatedByLot.TryGetValue(pair.Key, out long allocated);
                if (allocated != pair.Value.reservedCanonicalMilliUnits)
                {
                    error = "El reservado del lote " + pair.Key +
                            " no coincide con las reservas activas.";
                    return false;
                }
            }
        }

        var operationIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < operations.Count; index++)
        {
            BistroBuilderInventoryOperationSaveRecord record = operations[index];

            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }

            if (!operationIds.Add(record.operationId))
            {
                error = "El snapshot repite la operación " +
                        record.operationId + ".";
                return false;
            }
        }

        var transactionIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < ledger.Count; index++)
        {
            BistroBuilderInventoryTransactionSaveRecord record = ledger[index];
            long expectedSequence = index + 1L;

            if (record == null || !record.TryValidate(out error))
            {
                return false;
            }

            if (record.sequence != expectedSequence)
            {
                error = "El libro de inventario no conserva una secuencia " +
                        "continua en la posición " + index + ".";
                return false;
            }

            if (!transactionIds.Add(record.transactionId))
            {
                error = "El libro repite la transacción " +
                        record.transactionId + ".";
                return false;
            }
        }

        if (nextTransactionSequence != ledger.Count + 1L)
        {
            error = "La siguiente secuencia del libro no coincide con su " +
                    "número de movimientos.";
            return false;
        }

        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryStockSaveRecord
{
    public string ingredientId = string.Empty;
    public string storageLocationId = string.Empty;
    public long onHandCanonicalMilliUnits;
    public long reservedCanonicalMilliUnits;
    public long consumedCanonicalMilliUnits;
    public long wastedCanonicalMilliUnits;
    public long expiredCanonicalMilliUnits;
    public long revision;

    public bool TryValidate(out string error)
    {
        ingredientId = Normalize(ingredientId);
        storageLocationId = Normalize(storageLocationId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId) ||
            !BistroBuilderMenuIdUtility.IsValidStableId(storageLocationId))
        {
            error = "El snapshot contiene una identidad de stock inválida.";
            return false;
        }

        if (onHandCanonicalMilliUnits < 0L ||
            reservedCanonicalMilliUnits < 0L ||
            reservedCanonicalMilliUnits > onHandCanonicalMilliUnits ||
            consumedCanonicalMilliUnits < 0L ||
            wastedCanonicalMilliUnits < 0L ||
            expiredCanonicalMilliUnits < 0L ||
            revision < 0L)
        {
            error = "El balance persistido de " + ingredientId +
                    " es inválido.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return value != null
            ? value.Trim().ToLowerInvariant()
            : string.Empty;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryLotSaveRecord
{
    public string lotId = string.Empty;
    public string ingredientId = string.Empty;
    public string sourceId = string.Empty;
    public int receivedDayIndex;
    public int expirationDayIndex;
    public long onHandCanonicalMilliUnits;
    public long reservedCanonicalMilliUnits;
    public long revision;

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        lotId = lotId != null ? lotId.Trim().ToLowerInvariant() : string.Empty;
        ingredientId = ingredientId != null
            ? ingredientId.Trim().ToLowerInvariant()
            : string.Empty;
        sourceId = BistroBuilderInventoryRuntimeIdUtility.Normalize(sourceId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(lotId) ||
            !BistroBuilderMenuIdUtility.IsValidStableId(ingredientId) ||
            !BistroBuilderInventoryRuntimeIdUtility.TryValidateNormalized(
                sourceId,
                "SourceId de " + lotId,
                out error
            ) ||
            receivedDayIndex < 1 ||
            (expirationDayIndex != 0 &&
             expirationDayIndex <= receivedDayIndex) ||
            onHandCanonicalMilliUnits < 0L ||
            reservedCanonicalMilliUnits < 0L ||
            reservedCanonicalMilliUnits > onHandCanonicalMilliUnits ||
            revision < 0L)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "El lote persistido " + lotId + " es inválido.";
            }
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryLotAllocationSaveRecord
{
    public string lotId = string.Empty;
    public long canonicalMilliUnits;

    public bool TryValidate(out string error)
    {
        lotId = lotId != null ? lotId.Trim().ToLowerInvariant() : string.Empty;
        if (!BistroBuilderMenuIdUtility.IsValidStableId(lotId) ||
            canonicalMilliUnits <= 0L)
        {
            error = "El snapshot contiene una asignación de lote inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryReservationLineSaveRecord
{
    public string ingredientId = string.Empty;
    public long canonicalMilliUnits;
    public List<BistroBuilderInventoryLotAllocationSaveRecord> lotAllocations =
        new List<BistroBuilderInventoryLotAllocationSaveRecord>();

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        ingredientId = ingredientId != null
            ? ingredientId.Trim().ToLowerInvariant()
            : string.Empty;

        if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId) ||
            canonicalMilliUnits <= 0L)
        {
            error = "El snapshot contiene una línea de reserva inválida.";
            return false;
        }

        if (lotAllocations == null)
        {
            lotAllocations = new List<BistroBuilderInventoryLotAllocationSaveRecord>();
        }

        var lotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < lotAllocations.Count; index++)
        {
            BistroBuilderInventoryLotAllocationSaveRecord allocation =
                lotAllocations[index];
            if (allocation == null || !allocation.TryValidate(out error))
            {
                return false;
            }

            if (!lotIds.Add(allocation.lotId))
            {
                error = "Una línea de reserva repite el lote " +
                        allocation.lotId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryReservationSaveRecord
{
    public string reservationId = string.Empty;
    public string sourceId = string.Empty;
    public int status;
    public long revision;
    public List<BistroBuilderInventoryReservationLineSaveRecord> lines =
        new List<BistroBuilderInventoryReservationLineSaveRecord>();

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref reservationId,
                    "ReservationId",
                    out error
                ))
        {
            return false;
        }

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref sourceId,
                    "SourceId de la reserva " + reservationId,
                    out error
                ))
        {
            return false;
        }

        if (!Enum.IsDefined(
                typeof(BistroBuilderInventoryReservationStatus),
                status
            ) ||
            revision < 0L ||
            lines == null ||
            lines.Count == 0)
        {
            error = "La reserva " + reservationId +
                    " contiene estado, revisión o líneas inválidos.";
            return false;
        }

        var ingredientIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < lines.Count; index++)
        {
            BistroBuilderInventoryReservationLineSaveRecord line = lines[index];

            if (line == null)
            {
                error = "La reserva " + reservationId +
                        " contiene una línea nula en la posición " + index + ".";
                return false;
            }

            if (!line.TryValidate(out error))
            {
                return false;
            }

            if (!ingredientIds.Add(line.ingredientId))
            {
                error = "La reserva " + reservationId +
                        " repite un ingrediente.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryOperationSaveRecord
{
    public string operationId = string.Empty;
    public string fingerprint = string.Empty;

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref operationId,
                    "OperationId persistido",
                    out error
                ))
        {
            return false;
        }

        fingerprint = fingerprint != null ? fingerprint.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            error = "La operación " + operationId +
                    " no conserva su huella idempotente.";
            return false;
        }

        return true;
    }
}

[Serializable]
public sealed class BistroBuilderInventoryTransactionSaveRecord
{
    public long sequence;
    public string transactionId = string.Empty;
    public string operationId = string.Empty;
    public string ingredientId = string.Empty;
    public int transactionType;
    public long quantityCanonicalMilliUnits;
    public long onHandDeltaCanonicalMilliUnits;
    public long reservedDeltaCanonicalMilliUnits;
    public long previousOnHandCanonicalMilliUnits;
    public long newOnHandCanonicalMilliUnits;
    public long previousReservedCanonicalMilliUnits;
    public long newReservedCanonicalMilliUnits;
    public string sourceId = string.Empty;
    public string reason = string.Empty;
    public long timestampUtcTicks;

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref transactionId,
                    "TransactionId",
                    out error
                ))
        {
            return false;
        }

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref operationId,
                    "OperationId de " + transactionId,
                    out error
                ))
        {
            return false;
        }

        if (!BistroBuilderInventoryRuntimeIdUtility
                .TryNormalizeAndValidateRuntimeId(
                    ref sourceId,
                    "SourceId de " + transactionId,
                    out error
                ))
        {
            return false;
        }

        ingredientId = NormalizeStableId(ingredientId);
        reason = reason != null ? reason.Trim() : string.Empty;

        if (sequence < 1L ||
            !BistroBuilderMenuIdUtility.IsValidStableId(ingredientId) ||
            !Enum.IsDefined(
                typeof(BistroBuilderInventoryTransactionType),
                transactionType
            ) ||
            quantityCanonicalMilliUnits <= 0L ||
            previousOnHandCanonicalMilliUnits < 0L ||
            newOnHandCanonicalMilliUnits < 0L ||
            previousReservedCanonicalMilliUnits < 0L ||
            newReservedCanonicalMilliUnits < 0L ||
            newReservedCanonicalMilliUnits > newOnHandCanonicalMilliUnits ||
            timestampUtcTicks <= 0L)
        {
            error = "La transacción " + transactionId +
                    " contiene cantidades, tipo o ingrediente inválidos.";
            return false;
        }

        return true;
    }

    public BistroBuilderInventoryTransactionSnapshot ToSnapshot()
    {
        return new BistroBuilderInventoryTransactionSnapshot(
            sequence,
            transactionId,
            operationId,
            ingredientId,
            (BistroBuilderInventoryTransactionType)transactionType,
            quantityCanonicalMilliUnits,
            onHandDeltaCanonicalMilliUnits,
            reservedDeltaCanonicalMilliUnits,
            previousOnHandCanonicalMilliUnits,
            newOnHandCanonicalMilliUnits,
            previousReservedCanonicalMilliUnits,
            newReservedCanonicalMilliUnits,
            sourceId,
            reason,
            timestampUtcTicks
        );
    }

    public static BistroBuilderInventoryTransactionSaveRecord FromSnapshot(
        BistroBuilderInventoryTransactionSnapshot snapshot
    )
    {
        return new BistroBuilderInventoryTransactionSaveRecord
        {
            sequence = snapshot.Sequence,
            transactionId = snapshot.TransactionId,
            operationId = snapshot.OperationId,
            ingredientId = snapshot.IngredientId,
            transactionType = (int)snapshot.TransactionType,
            quantityCanonicalMilliUnits = snapshot.QuantityCanonicalMilliUnits,
            onHandDeltaCanonicalMilliUnits =
                snapshot.OnHandDeltaCanonicalMilliUnits,
            reservedDeltaCanonicalMilliUnits =
                snapshot.ReservedDeltaCanonicalMilliUnits,
            previousOnHandCanonicalMilliUnits =
                snapshot.PreviousOnHandCanonicalMilliUnits,
            newOnHandCanonicalMilliUnits =
                snapshot.NewOnHandCanonicalMilliUnits,
            previousReservedCanonicalMilliUnits =
                snapshot.PreviousReservedCanonicalMilliUnits,
            newReservedCanonicalMilliUnits =
                snapshot.NewReservedCanonicalMilliUnits,
            sourceId = snapshot.SourceId,
            reason = snapshot.Reason,
            timestampUtcTicks = snapshot.TimestampUtcTicks
        };
    }

    private static string NormalizeStableId(string value)
    {
        return value != null
            ? value.Trim().ToLowerInvariant()
            : string.Empty;
    }
}

/// <summary>
/// Contrato compartido por la persistencia del inventario para identidades
/// runtime. Estas identidades no son IDs de contenido y pueden superar el
/// límite de 96 caracteres de BistroBuilderMenuIdUtility.
/// </summary>
internal static class BistroBuilderInventoryRuntimeIdUtility
{
    internal const int MaximumRuntimeIdLength = 160;

    internal static string Normalize(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    internal static bool TryValidateNormalized(
        string value,
        string fieldName,
        out string error
    )
    {
        string effectiveFieldName = !string.IsNullOrWhiteSpace(fieldName)
            ? fieldName.Trim()
            : "La identidad runtime";

        if (string.IsNullOrWhiteSpace(value))
        {
            error = effectiveFieldName + " no puede estar vacío.";
            return false;
        }

        if (value.Length > MaximumRuntimeIdLength)
        {
            error = effectiveFieldName + " excede " +
                    MaximumRuntimeIdLength + " caracteres.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool TryNormalizeAndValidateRuntimeId(
        ref string value,
        string fieldName,
        out string error
    )
    {
        value = Normalize(value);
        return TryValidateNormalized(value, fieldName, out error);
    }
}
