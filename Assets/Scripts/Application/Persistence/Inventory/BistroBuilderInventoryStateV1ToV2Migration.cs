using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Migración consecutiva de inventory.canonical v1 a v2 (2.2A).
///
/// La versión histórica solo conocía balances agregados. Como un payload v1
/// no contiene fechas ni lotes, la migración conserva exactamente sus
/// balances, reservas, operaciones y libro, y marca el snapshot para que
/// BistroBuilderInventoryService materialice un lote interno por ingrediente
/// usando el calendario ya restaurado de la partida. De esta forma no se
/// inventan fechas anteriores ni se pierde una reserva de un servicio activo.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Inventory State V1 To V2 Migration"
)]
public sealed class BistroBuilderInventoryStateV1ToV2Migration :
    MonoBehaviour,
    IBistroBuilderSaveSectionMigration
{
    public string SectionId =>
        BistroBuilderInventorySaveSectionProvider.StableSectionId;

    public int FromVersion => 1;

    public int ToVersion => 2;

    public string FromSerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public string ToSerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public bool TryMigrate(
        byte[] sourcePayload,
        out byte[] migratedPayload,
        out string error
    )
    {
        migratedPayload = null;
        error = string.Empty;

        if (sourcePayload == null || sourcePayload.Length == 0)
        {
            error = "inventory.canonical v1 no contiene datos para migrar.";
            return false;
        }

        LegacySnapshotV1 source;
        try
        {
            source = JsonUtility.FromJson<LegacySnapshotV1>(
                Encoding.UTF8.GetString(sourcePayload)
            );
        }
        catch (Exception exception)
        {
            error = "No se pudo leer inventory.canonical v1: " +
                    exception.Message;
            return false;
        }

        if (!TryValidateLegacy(source, out error))
        {
            return false;
        }

        var target = new BistroBuilderInventoryRuntimeSnapshot
        {
            schemaVersion = BistroBuilderInventoryRuntimeSnapshot
                .CurrentSchemaVersion,
            nextTransactionSequence = source.nextTransactionSequence,
            nextLotSequence = 1L,
            runtimeRevision = source.runtimeRevision,
            lastShelfLifeProcessedDayIndex = 0,
            requiresLotMaterialization = true
        };

        for (int index = 0; index < source.stock.Count; index++)
        {
            LegacyStockRecordV1 record = source.stock[index];
            target.stock.Add(
                new BistroBuilderInventoryStockSaveRecord
                {
                    ingredientId = NormalizeStableId(record.ingredientId),
                    storageLocationId =
                        NormalizeStableId(record.storageLocationId),
                    onHandCanonicalMilliUnits =
                        record.onHandCanonicalMilliUnits,
                    reservedCanonicalMilliUnits =
                        record.reservedCanonicalMilliUnits,
                    consumedCanonicalMilliUnits =
                        record.consumedCanonicalMilliUnits,
                    wastedCanonicalMilliUnits =
                        record.wastedCanonicalMilliUnits,
                    expiredCanonicalMilliUnits = 0L,
                    revision = record.revision
                }
            );
        }

        for (int index = 0; index < source.reservations.Count; index++)
        {
            LegacyReservationRecordV1 record = source.reservations[index];
            var targetReservation =
                new BistroBuilderInventoryReservationSaveRecord
                {
                    reservationId = NormalizeRuntimeId(record.reservationId),
                    sourceId = NormalizeRuntimeId(record.sourceId),
                    status = record.status,
                    revision = record.revision
                };

            for (int lineIndex = 0;
                 lineIndex < record.lines.Count;
                 lineIndex++)
            {
                LegacyReservationLineV1 line = record.lines[lineIndex];
                targetReservation.lines.Add(
                    new BistroBuilderInventoryReservationLineSaveRecord
                    {
                        ingredientId = NormalizeStableId(line.ingredientId),
                        canonicalMilliUnits = line.canonicalMilliUnits,
                        lotAllocations =
                            new List<
                                BistroBuilderInventoryLotAllocationSaveRecord
                            >()
                    }
                );
            }

            target.reservations.Add(targetReservation);
        }

        for (int index = 0; index < source.operations.Count; index++)
        {
            LegacyOperationRecordV1 record = source.operations[index];
            target.operations.Add(
                new BistroBuilderInventoryOperationSaveRecord
                {
                    operationId = NormalizeRuntimeId(record.operationId),
                    fingerprint = record.fingerprint != null
                        ? record.fingerprint.Trim()
                        : string.Empty
                }
            );
        }

        for (int index = 0; index < source.ledger.Count; index++)
        {
            LegacyTransactionRecordV1 record = source.ledger[index];
            target.ledger.Add(
                new BistroBuilderInventoryTransactionSaveRecord
                {
                    sequence = record.sequence,
                    transactionId = NormalizeRuntimeId(record.transactionId),
                    operationId = NormalizeRuntimeId(record.operationId),
                    ingredientId = NormalizeStableId(record.ingredientId),
                    transactionType = record.transactionType,
                    quantityCanonicalMilliUnits =
                        record.quantityCanonicalMilliUnits,
                    onHandDeltaCanonicalMilliUnits =
                        record.onHandDeltaCanonicalMilliUnits,
                    reservedDeltaCanonicalMilliUnits =
                        record.reservedDeltaCanonicalMilliUnits,
                    previousOnHandCanonicalMilliUnits =
                        record.previousOnHandCanonicalMilliUnits,
                    newOnHandCanonicalMilliUnits =
                        record.newOnHandCanonicalMilliUnits,
                    previousReservedCanonicalMilliUnits =
                        record.previousReservedCanonicalMilliUnits,
                    newReservedCanonicalMilliUnits =
                        record.newReservedCanonicalMilliUnits,
                    sourceId = NormalizeRuntimeId(record.sourceId),
                    reason = record.reason != null
                        ? record.reason.Trim()
                        : string.Empty,
                    timestampUtcTicks = record.timestampUtcTicks
                }
            );
        }

        if (!target.TryValidateBasic(out error))
        {
            error = "La migración v1->v2 produjo un snapshot inválido. " +
                    error;
            return false;
        }

        try
        {
            migratedPayload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(target, false)
            );
        }
        catch (Exception exception)
        {
            migratedPayload = null;
            error = "No se pudo escribir inventory.canonical v2: " +
                    exception.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateLegacy(
        LegacySnapshotV1 source,
        out string error
    )
    {
        error = string.Empty;

        if (source == null ||
            source.schemaVersion != 1 ||
            source.nextTransactionSequence < 1L ||
            source.runtimeRevision < 0L ||
            source.stock == null ||
            source.reservations == null ||
            source.operations == null ||
            source.ledger == null)
        {
            error = "inventory.canonical v1 no cumple su contrato histórico.";
            return false;
        }

        var stockIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < source.stock.Count; index++)
        {
            LegacyStockRecordV1 record = source.stock[index];
            if (record == null)
            {
                error = "inventory.canonical v1 contiene un balance nulo.";
                return false;
            }

            string ingredientId = NormalizeStableId(record.ingredientId);
            string storageId = NormalizeStableId(record.storageLocationId);
            if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId) ||
                !BistroBuilderMenuIdUtility.IsValidStableId(storageId) ||
                !stockIds.Add(ingredientId) ||
                record.onHandCanonicalMilliUnits < 0L ||
                record.reservedCanonicalMilliUnits < 0L ||
                record.reservedCanonicalMilliUnits >
                    record.onHandCanonicalMilliUnits ||
                record.consumedCanonicalMilliUnits < 0L ||
                record.wastedCanonicalMilliUnits < 0L ||
                record.revision < 0L)
            {
                error = "inventory.canonical v1 contiene un balance inválido.";
                return false;
            }
        }

        var reservationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < source.reservations.Count; index++)
        {
            LegacyReservationRecordV1 record = source.reservations[index];
            if (record == null)
            {
                error = "inventory.canonical v1 contiene una reserva nula.";
                return false;
            }

            string reservationId = NormalizeRuntimeId(record.reservationId);
            string sourceId = NormalizeRuntimeId(record.sourceId);
            if (!TryValidateRuntimeId(reservationId) ||
                !TryValidateRuntimeId(sourceId) ||
                !reservationIds.Add(reservationId) ||
                !Enum.IsDefined(
                    typeof(BistroBuilderInventoryReservationStatus),
                    record.status
                ) ||
                record.revision < 0L ||
                record.lines == null ||
                record.lines.Count == 0)
            {
                error = "inventory.canonical v1 contiene una reserva inválida.";
                return false;
            }

            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            for (int lineIndex = 0;
                 lineIndex < record.lines.Count;
                 lineIndex++)
            {
                LegacyReservationLineV1 line = record.lines[lineIndex];
                string ingredientId = line != null
                    ? NormalizeStableId(line.ingredientId)
                    : string.Empty;
                if (line == null ||
                    !stockIds.Contains(ingredientId) ||
                    !lineIds.Add(ingredientId) ||
                    line.canonicalMilliUnits <= 0L)
                {
                    error = "inventory.canonical v1 contiene una línea de " +
                            "reserva inválida.";
                    return false;
                }
            }
        }

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < source.operations.Count; index++)
        {
            LegacyOperationRecordV1 record = source.operations[index];
            string operationId = record != null
                ? NormalizeRuntimeId(record.operationId)
                : string.Empty;
            if (record == null ||
                !TryValidateRuntimeId(operationId) ||
                !operationIds.Add(operationId) ||
                string.IsNullOrWhiteSpace(record.fingerprint))
            {
                error = "inventory.canonical v1 contiene una operación " +
                        "idempotente inválida.";
                return false;
            }
        }

        var transactionIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < source.ledger.Count; index++)
        {
            LegacyTransactionRecordV1 record = source.ledger[index];
            long expectedSequence = index + 1L;
            string transactionId = record != null
                ? NormalizeRuntimeId(record.transactionId)
                : string.Empty;
            string operationId = record != null
                ? NormalizeRuntimeId(record.operationId)
                : string.Empty;
            string sourceId = record != null
                ? NormalizeRuntimeId(record.sourceId)
                : string.Empty;
            string ingredientId = record != null
                ? NormalizeStableId(record.ingredientId)
                : string.Empty;

            if (record == null ||
                record.sequence != expectedSequence ||
                !TryValidateRuntimeId(transactionId) ||
                !transactionIds.Add(transactionId) ||
                !TryValidateRuntimeId(operationId) ||
                !TryValidateRuntimeId(sourceId) ||
                !stockIds.Contains(ingredientId) ||
                record.transactionType <
                    (int)BistroBuilderInventoryTransactionType.InitialStock ||
                record.transactionType >
                    (int)BistroBuilderInventoryTransactionType.Correction ||
                record.quantityCanonicalMilliUnits <= 0L ||
                record.previousOnHandCanonicalMilliUnits < 0L ||
                record.newOnHandCanonicalMilliUnits < 0L ||
                record.previousReservedCanonicalMilliUnits < 0L ||
                record.newReservedCanonicalMilliUnits < 0L ||
                record.newReservedCanonicalMilliUnits >
                    record.newOnHandCanonicalMilliUnits ||
                record.timestampUtcTicks <= 0L)
            {
                error = "inventory.canonical v1 contiene una transacción " +
                        "inválida en la posición " + index + ".";
                return false;
            }
        }

        if (source.nextTransactionSequence != source.ledger.Count + 1L)
        {
            error = "inventory.canonical v1 no conserva una secuencia " +
                    "continua del libro.";
            return false;
        }

        return true;
    }

    private static string NormalizeStableId(string value)
    {
        return BistroBuilderMenuIdUtility.NormalizeStableId(value);
    }

    private static string NormalizeRuntimeId(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    private static bool TryValidateRuntimeId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <=
                   BistroBuilderInventoryRuntimeIdUtility
                       .MaximumRuntimeIdLength;
    }

    [Serializable]
    private sealed class LegacySnapshotV1
    {
        public int schemaVersion;
        public long nextTransactionSequence = 1L;
        public long runtimeRevision;
        public List<LegacyStockRecordV1> stock =
            new List<LegacyStockRecordV1>();
        public List<LegacyReservationRecordV1> reservations =
            new List<LegacyReservationRecordV1>();
        public List<LegacyOperationRecordV1> operations =
            new List<LegacyOperationRecordV1>();
        public List<LegacyTransactionRecordV1> ledger =
            new List<LegacyTransactionRecordV1>();
    }

    [Serializable]
    private sealed class LegacyStockRecordV1
    {
        public string ingredientId = string.Empty;
        public string storageLocationId = string.Empty;
        public long onHandCanonicalMilliUnits;
        public long reservedCanonicalMilliUnits;
        public long consumedCanonicalMilliUnits;
        public long wastedCanonicalMilliUnits;
        public long revision;
    }

    [Serializable]
    private sealed class LegacyReservationLineV1
    {
        public string ingredientId = string.Empty;
        public long canonicalMilliUnits;
    }

    [Serializable]
    private sealed class LegacyReservationRecordV1
    {
        public string reservationId = string.Empty;
        public string sourceId = string.Empty;
        public int status;
        public long revision;
        public List<LegacyReservationLineV1> lines =
            new List<LegacyReservationLineV1>();
    }

    [Serializable]
    private sealed class LegacyOperationRecordV1
    {
        public string operationId = string.Empty;
        public string fingerprint = string.Empty;
    }

    [Serializable]
    private sealed class LegacyTransactionRecordV1
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
    }
}
