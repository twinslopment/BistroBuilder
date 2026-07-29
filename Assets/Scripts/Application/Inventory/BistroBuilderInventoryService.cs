using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Inventario canónico de ingredientes y libro de movimientos 368B.
///
/// Responsabilidades:
/// - Mantener balances físicos, reservados, disponibles, consumidos y merma.
/// - Ejecutar reservas de varios ingredientes de forma atómica.
/// - Liberar o consumir una reserva una sola vez.
/// - Rechazar dobles aplicaciones mediante OperationId idempotente.
/// - Registrar cada cambio en un libro append-only auditable.
///
/// 368B todavía no se conecta automáticamente a las comandas. Esa unión se
/// realizará en 368C sobre este núcleo ya validado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Canonical Inventory Service")]
public sealed class BistroBuilderInventoryService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderOpeningStockProfile openingStockProfile;

    [Header("Depuración")]

    [SerializeField]
    private bool logInitialization = true;

    private readonly Dictionary<string, StockState> stockByIngredientId =
        new Dictionary<string, StockState>(StringComparer.Ordinal);

    private readonly Dictionary<string, ReservationState> reservationsById =
        new Dictionary<string, ReservationState>(StringComparer.Ordinal);

    private readonly Dictionary<string, OperationRecord> operationsById =
        new Dictionary<string, OperationRecord>(StringComparer.Ordinal);

    private readonly List<BistroBuilderInventoryTransactionSnapshot> ledger =
        new List<BistroBuilderInventoryTransactionSnapshot>();

    private long nextTransactionSequence = 1L;
    private long runtimeRevision;
    private bool initialized;

    public event Action<BistroBuilderInventoryStockSnapshot> StockChanged;

    public event Action<BistroBuilderInventoryReservationSnapshot>
        ReservationChanged;

    public event Action<BistroBuilderInventoryTransactionSnapshot>
        TransactionRecorded;

    public bool IsInitialized => initialized;

    public int StockEntryCount => stockByIngredientId.Count;

    public int ReservationCount => reservationsById.Count;

    public int TransactionCount => ledger.Count;

    public long RuntimeRevision => runtimeRevision;

    public BistroBuilderOpeningStockProfile OpeningStockProfile =>
        openingStockProfile;

    private void Awake()
    {
        CacheDependenciesIfNeeded();

        if (!TryInitialize(out string error))
        {
            Debug.LogError(error, this);
            enabled = false;
            return;
        }

        if (logInitialization)
        {
            Debug.Log(
                nameof(BistroBuilderInventoryService) +
                " ha inicializado " + StockEntryCount +
                " ingrediente(s), " + TransactionCount +
                " movimiento(s) y " + ReservationCount +
                " reserva(s).",
                this
            );
        }
    }

    /// <summary>
    /// Comprueba únicamente dependencias y datos de autoría. No modifica el
    /// estado runtime.
    /// </summary>
    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (!recipeCatalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (openingStockProfile == null)
        {
            error = "Falta BistroBuilderOpeningStockProfile.";
            return false;
        }

        if (!openingStockProfile.TryValidate(out error))
        {
            return false;
        }

        IReadOnlyList<BistroBuilderOpeningStockLine> openingLines =
            openingStockProfile.Lines;

        for (int index = 0; index < openingLines.Count; index++)
        {
            BistroBuilderOpeningStockLine line = openingLines[index];

            if (line == null || line.Ingredient == null)
            {
                error = "El perfil de stock inicial contiene una línea " +
                        "incompleta.";
                return false;
            }

            if (!recipeCatalogService.TryGetIngredient(
                    line.Ingredient.IngredientId,
                    out BistroBuilderIngredientDefinition catalogued
                ) ||
                catalogued != line.Ingredient)
            {
                error = "El stock inicial referencia un ingrediente no " +
                        "catalogado: " + line.Ingredient.IngredientId + ".";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reconstruye el runtime desde el perfil de apertura.
    ///
    /// Está pensado para iniciar una partida nueva y para autotests. No debe
    /// llamarse durante un servicio activo una vez conectado a guardado.
    /// </summary>
    public bool TryInitialize(out string error)
    {
        error = string.Empty;

        if (!ValidateConfiguration(out error))
        {
            initialized = false;
            return false;
        }

        stockByIngredientId.Clear();
        reservationsById.Clear();
        operationsById.Clear();
        ledger.Clear();
        nextTransactionSequence = 1L;
        runtimeRevision = 0L;
        initialized = false;

        BistroBuilderIngredientCatalog ingredientCatalog =
            recipeCatalogService.IngredientCatalog;
        IReadOnlyList<BistroBuilderIngredientDefinition> ingredients =
            ingredientCatalog.Definitions;

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientDefinition ingredient = ingredients[index];

            if (ingredient == null)
            {
                error = "El catálogo de ingredientes contiene una " +
                        "referencia nula.";
                return false;
            }

            stockByIngredientId.Add(
                ingredient.IngredientId,
                new StockState(
                    ingredient,
                    BistroBuilderInventoryStorageLocationIds
                        .FromIngredientStorage(ingredient.StorageType)
                )
            );
        }

        var pending = new List<PendingMutation>();
        IReadOnlyList<BistroBuilderOpeningStockLine> openingLines =
            openingStockProfile.Lines;

        for (int index = 0; index < openingLines.Count; index++)
        {
            BistroBuilderOpeningStockLine line = openingLines[index];

            if (!line.TryGetCanonicalMilliUnits(
                    out long quantity,
                    out error
                ))
            {
                return false;
            }

            StockState state =
                stockByIngredientId[line.Ingredient.IngredientId];
            state.StorageLocationId = line.StorageLocationId;

            if (!TryBuildMutation(
                    state,
                    BistroBuilderInventoryTransactionType.InitialStock,
                    quantity,
                    quantity,
                    0L,
                    0L,
                    0L,
                    "opening_stock_" + openingStockProfile.ProfileId,
                    openingStockProfile.ProfileId,
                    "Existencias iniciales de partida.",
                    out PendingMutation mutation,
                    out error
                ))
            {
                return false;
            }

            pending.Add(mutation);
        }

        if (pending.Count > 0)
        {
            CommitMutations(pending);
        }

        initialized = true;

        if (!ValidateRuntimeState(out error))
        {
            initialized = false;
            return false;
        }

        return true;
    }

    public bool TryGetStockSnapshot(
        string ingredientId,
        out BistroBuilderInventoryStockSnapshot snapshot
    )
    {
        snapshot = default;
        string normalized = NormalizeIngredientId(ingredientId);

        if (!initialized ||
            string.IsNullOrWhiteSpace(normalized) ||
            !stockByIngredientId.TryGetValue(normalized, out StockState state))
        {
            return false;
        }

        snapshot = state.ToSnapshot();
        return true;
    }

    public void CopyStockSnapshotsTo(
        List<BistroBuilderInventoryStockSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        var keys = new List<string>(stockByIngredientId.Keys);
        keys.Sort(StringComparer.Ordinal);

        for (int index = 0; index < keys.Count; index++)
        {
            destination.Add(stockByIngredientId[keys[index]].ToSnapshot());
        }
    }

    public void CopyTransactionsTo(
        List<BistroBuilderInventoryTransactionSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        destination.AddRange(ledger);
    }

    public bool TryGetReservationSnapshot(
        string reservationId,
        out BistroBuilderInventoryReservationSnapshot snapshot
    )
    {
        snapshot = null;
        string normalized = NormalizeRuntimeId(reservationId);

        if (!initialized ||
            string.IsNullOrWhiteSpace(normalized) ||
            !reservationsById.TryGetValue(
                normalized,
                out ReservationState reservation
            ))
        {
            return false;
        }

        snapshot = reservation.ToSnapshot();
        return true;
    }

    /// <summary>
    /// Añade una recepción física de stock.
    /// </summary>
    public bool TryAddStock(
        string operationId,
        string sourceId,
        string ingredientId,
        long canonicalMilliUnits,
        BistroBuilderInventoryTransactionType transactionType,
        string reason,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (transactionType != BistroBuilderInventoryTransactionType.Purchase &&
            transactionType !=
                BistroBuilderInventoryTransactionType.InitialStock)
        {
            error = "TryAddStock solo admite Purchase o InitialStock.";
            return false;
        }

        if (!TryGetKnownState(
                ingredientId,
                out StockState state,
                out error
            ) ||
            !TryValidatePositiveQuantity(canonicalMilliUnits, out error))
        {
            return false;
        }

        string normalizedOperation = NormalizeRuntimeId(operationId);
        string normalizedSource = NormalizeRuntimeId(sourceId);
        string fingerprint = transactionType + "|" + state.IngredientId +
                             "|" + canonicalMilliUnits + "|" +
                             normalizedSource;

        OperationReplay replay = EvaluateOperation(
            normalizedOperation,
            fingerprint,
            out error
        );

        if (replay == OperationReplay.Conflict)
        {
            return false;
        }

        if (replay == OperationReplay.Replayed)
        {
            return true;
        }

        if (!TryValidateRuntimeId(normalizedOperation, "OperationId", out error) ||
            !TryValidateRuntimeId(normalizedSource, "SourceId", out error))
        {
            return false;
        }

        if (!TryBuildMutation(
                state,
                transactionType,
                canonicalMilliUnits,
                canonicalMilliUnits,
                0L,
                0L,
                0L,
                normalizedOperation,
                normalizedSource,
                reason,
                out PendingMutation mutation,
                out error
            ))
        {
            return false;
        }

        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(new List<PendingMutation> { mutation });
        return true;
    }

    /// <summary>
    /// Reserva varias líneas como una única transacción lógica.
    ///
    /// Primero valida y agrega todas las cantidades. Si falta cualquier
    /// ingrediente, no modifica ningún balance.
    /// </summary>
    public bool TryCreateReservation(
        string operationId,
        string reservationId,
        string sourceId,
        IReadOnlyList<BistroBuilderInventoryQuantityLine> requestedLines,
        out BistroBuilderInventoryReservationSnapshot reservationSnapshot,
        out string error
    )
    {
        reservationSnapshot = null;
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalizedOperation = NormalizeRuntimeId(operationId);
        string normalizedReservation = NormalizeRuntimeId(reservationId);
        string normalizedSource = NormalizeRuntimeId(sourceId);

        if (!TryValidateRuntimeId(normalizedOperation, "OperationId", out error) ||
            !TryValidateRuntimeId(
                normalizedReservation,
                "ReservationId",
                out error
            ) ||
            !TryValidateRuntimeId(normalizedSource, "SourceId", out error))
        {
            return false;
        }

        if (!TryAggregateLines(
                requestedLines,
                out SortedDictionary<string, long> aggregated,
                out error
            ))
        {
            return false;
        }

        string fingerprint = BuildReservationFingerprint(
            "reserve",
            normalizedReservation,
            normalizedSource,
            aggregated
        );
        OperationReplay replay = EvaluateOperation(
            normalizedOperation,
            fingerprint,
            out error
        );

        if (replay == OperationReplay.Conflict)
        {
            return false;
        }

        if (replay == OperationReplay.Replayed)
        {
            return TryGetReservationSnapshot(
                normalizedReservation,
                out reservationSnapshot
            );
        }

        if (reservationsById.ContainsKey(normalizedReservation))
        {
            error = "Ya existe la reserva " + normalizedReservation + ".";
            return false;
        }

        var pending = new List<PendingMutation>(aggregated.Count);
        var reservationLines = new List<ReservationLineState>(
            aggregated.Count
        );

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            StockState state = stockByIngredientId[pair.Key];

            if (state.Available < pair.Value)
            {
                error = "Stock insuficiente de " + pair.Key +
                        ". Disponible: " + state.Available +
                        "; solicitado: " + pair.Value + ".";
                return false;
            }

            if (!TryBuildMutation(
                    state,
                    BistroBuilderInventoryTransactionType.Reservation,
                    pair.Value,
                    0L,
                    pair.Value,
                    0L,
                    0L,
                    normalizedOperation,
                    normalizedSource,
                    "Reserva " + normalizedReservation + ".",
                    out PendingMutation mutation,
                    out error
                ))
            {
                return false;
            }

            pending.Add(mutation);
            reservationLines.Add(
                new ReservationLineState(pair.Key, pair.Value)
            );
        }

        var reservation = new ReservationState(
            normalizedReservation,
            normalizedSource,
            reservationLines
        );
        reservation.Revision = runtimeRevision + 1L;
        reservationsById.Add(normalizedReservation, reservation);

        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(pending);
        reservationSnapshot = reservation.ToSnapshot();
        PublishReservationChanged(reservationSnapshot);
        return true;
    }

    public bool TryReleaseReservation(
        string operationId,
        string reservationId,
        string reason,
        out string error
    )
    {
        return TryCloseReservation(
            operationId,
            reservationId,
            BistroBuilderInventoryReservationStatus.Released,
            reason,
            out error
        );
    }

    public bool TryConsumeReservation(
        string operationId,
        string reservationId,
        string reason,
        out string error
    )
    {
        return TryCloseReservation(
            operationId,
            reservationId,
            BistroBuilderInventoryReservationStatus.Consumed,
            reason,
            out error
        );
    }

    public bool TryRegisterWaste(
        string operationId,
        string sourceId,
        string ingredientId,
        long canonicalMilliUnits,
        string reason,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error) ||
            !TryGetKnownState(
                ingredientId,
                out StockState state,
                out error
            ) ||
            !TryValidatePositiveQuantity(canonicalMilliUnits, out error))
        {
            return false;
        }

        string normalizedOperation = NormalizeRuntimeId(operationId);
        string normalizedSource = NormalizeRuntimeId(sourceId);
        string fingerprint = "waste|" + state.IngredientId + "|" +
                             canonicalMilliUnits + "|" + normalizedSource;
        OperationReplay replay = EvaluateOperation(
            normalizedOperation,
            fingerprint,
            out error
        );

        if (replay == OperationReplay.Conflict)
        {
            return false;
        }

        if (replay == OperationReplay.Replayed)
        {
            return true;
        }

        if (!TryValidateRuntimeId(normalizedOperation, "OperationId", out error) ||
            !TryValidateRuntimeId(normalizedSource, "SourceId", out error))
        {
            return false;
        }

        if (state.Available < canonicalMilliUnits)
        {
            error = "No se puede registrar merma de " + state.IngredientId +
                    " porque la cantidad libre es insuficiente.";
            return false;
        }

        if (!TryBuildMutation(
                state,
                BistroBuilderInventoryTransactionType.Waste,
                canonicalMilliUnits,
                -canonicalMilliUnits,
                0L,
                0L,
                canonicalMilliUnits,
                normalizedOperation,
                normalizedSource,
                reason,
                out PendingMutation mutation,
                out error
            ))
        {
            return false;
        }

        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(new List<PendingMutation> { mutation });
        return true;
    }

    /// <summary>
    /// Ajusta la existencia física a un valor contado. Nunca permite dejar
    /// menos existencia física que la ya reservada.
    /// </summary>
    public bool TryCorrectOnHand(
        string operationId,
        string sourceId,
        string ingredientId,
        long targetOnHandCanonicalMilliUnits,
        string reason,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error) ||
            !TryGetKnownState(
                ingredientId,
                out StockState state,
                out error
            ))
        {
            return false;
        }

        if (targetOnHandCanonicalMilliUnits < 0L ||
            targetOnHandCanonicalMilliUnits >
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
        {
            error = "La corrección de inventario queda fuera de rango.";
            return false;
        }

        string normalizedOperation = NormalizeRuntimeId(operationId);
        string normalizedSource = NormalizeRuntimeId(sourceId);
        string fingerprint = "correction|" + state.IngredientId + "|" +
                             targetOnHandCanonicalMilliUnits + "|" +
                             normalizedSource;
        OperationReplay replay = EvaluateOperation(
            normalizedOperation,
            fingerprint,
            out error
        );

        if (replay == OperationReplay.Conflict)
        {
            return false;
        }

        if (replay == OperationReplay.Replayed)
        {
            return true;
        }

        if (!TryValidateRuntimeId(normalizedOperation, "OperationId", out error) ||
            !TryValidateRuntimeId(normalizedSource, "SourceId", out error))
        {
            return false;
        }

        if (targetOnHandCanonicalMilliUnits < state.Reserved)
        {
            error = "La corrección no puede dejar menos stock físico que " +
                    "el ya reservado.";
            return false;
        }

        long delta = targetOnHandCanonicalMilliUnits - state.OnHand;

        if (delta == 0L)
        {
            RememberOperation(normalizedOperation, fingerprint);
            return true;
        }

        if (!TryBuildMutation(
                state,
                BistroBuilderInventoryTransactionType.Correction,
                Math.Abs(delta),
                delta,
                0L,
                0L,
                0L,
                normalizedOperation,
                normalizedSource,
                reason,
                out PendingMutation mutation,
                out error
            ))
        {
            return false;
        }

        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(new List<PendingMutation> { mutation });
        return true;
    }

    /// <summary>
    /// Auditoría completa del runtime: balances, reservas y reconstrucción
    /// del libro de movimientos.
    /// </summary>
    public bool ValidateRuntimeState(out string error)
    {
        error = string.Empty;

        if (recipeCatalogService == null ||
            recipeCatalogService.IngredientCatalog == null)
        {
            error = "No existe catálogo de ingredientes para auditar.";
            return false;
        }

        BistroBuilderIngredientCatalog catalog =
            recipeCatalogService.IngredientCatalog;

        if (stockByIngredientId.Count != catalog.DefinitionCount)
        {
            error = "El inventario contiene " + stockByIngredientId.Count +
                    " balances y el catálogo " + catalog.DefinitionCount +
                    ".";
            return false;
        }

        var activeReserved = new Dictionary<string, long>(
            StringComparer.Ordinal
        );

        foreach (KeyValuePair<string, ReservationState> pair
                 in reservationsById)
        {
            ReservationState reservation = pair.Value;

            if (reservation == null ||
                !string.Equals(
                    pair.Key,
                    reservation.ReservationId,
                    StringComparison.Ordinal
                ) ||
                !Enum.IsDefined(
                    typeof(BistroBuilderInventoryReservationStatus),
                    reservation.Status
                ))
            {
                error = "El índice de reservas contiene una identidad o " +
                        "estado incoherente.";
                return false;
            }

            for (int index = 0; index < reservation.Lines.Count; index++)
            {
                ReservationLineState line = reservation.Lines[index];

                if (!stockByIngredientId.ContainsKey(line.IngredientId) ||
                    line.Quantity <= 0L)
                {
                    error = "La reserva " + reservation.ReservationId +
                            " contiene una línea inválida.";
                    return false;
                }

                if (reservation.Status !=
                    BistroBuilderInventoryReservationStatus.Active)
                {
                    continue;
                }

                activeReserved.TryGetValue(
                    line.IngredientId,
                    out long accumulated
                );

                try
                {
                    activeReserved[line.IngredientId] =
                        checked(accumulated + line.Quantity);
                }
                catch (OverflowException)
                {
                    error = "Las reservas activas desbordan el rango.";
                    return false;
                }
            }
        }

        foreach (KeyValuePair<string, StockState> pair in stockByIngredientId)
        {
            StockState state = pair.Value;

            if (!catalog.TryGetDefinition(
                    pair.Key,
                    out BistroBuilderIngredientDefinition catalogued
                ) ||
                state == null ||
                state.Definition != catalogued)
            {
                error = "El balance de " + pair.Key +
                        " no referencia su definición canónica.";
                return false;
            }

            if (state.Definition == null ||
                !string.Equals(
                    pair.Key,
                    state.IngredientId,
                    StringComparison.Ordinal
                ) ||
                state.OnHand < 0L ||
                state.Reserved < 0L ||
                state.Reserved > state.OnHand ||
                state.Consumed < 0L ||
                state.Wasted < 0L ||
                state.Revision < 0L ||
                !BistroBuilderMenuIdUtility.IsValidStableId(
                    state.StorageLocationId
                ))
            {
                error = "Balance interno inválido para " + pair.Key + ".";
                return false;
            }

            activeReserved.TryGetValue(pair.Key, out long expectedReserved);

            if (expectedReserved != state.Reserved)
            {
                error = "La reserva agregada de " + pair.Key +
                        " no coincide con su balance. Esperado: " +
                        expectedReserved + "; real: " + state.Reserved +
                        ".";
                return false;
            }
        }

        var reconstructedOnHand = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var reconstructedReserved = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var reconstructedConsumed = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var reconstructedWasted = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var transactionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (string ingredientId in stockByIngredientId.Keys)
        {
            reconstructedOnHand.Add(ingredientId, 0L);
            reconstructedReserved.Add(ingredientId, 0L);
            reconstructedConsumed.Add(ingredientId, 0L);
            reconstructedWasted.Add(ingredientId, 0L);
        }

        for (int index = 0; index < ledger.Count; index++)
        {
            BistroBuilderInventoryTransactionSnapshot transaction =
                ledger[index];
            long expectedSequence = index + 1L;

            if (transaction.Sequence != expectedSequence ||
                transaction.TransactionId !=
                    "inventory_tx_" + expectedSequence.ToString("D8") ||
                !transactionIds.Add(transaction.TransactionId) ||
                !reconstructedOnHand.ContainsKey(transaction.IngredientId) ||
                !IsTransactionShapeValid(transaction))
            {
                error = "El libro contiene una transacción mal indexada o " +
                        "incoherente en la posición " + index + ".";
                return false;
            }

            long previousOnHand =
                reconstructedOnHand[transaction.IngredientId];
            long previousReserved =
                reconstructedReserved[transaction.IngredientId];

            if (previousOnHand !=
                    transaction.PreviousOnHandCanonicalMilliUnits ||
                previousReserved !=
                    transaction.PreviousReservedCanonicalMilliUnits)
            {
                error = "El libro no encadena correctamente " +
                        transaction.TransactionId + ".";
                return false;
            }

            long nextOnHand;
            long nextReserved;

            try
            {
                nextOnHand = checked(
                    previousOnHand +
                    transaction.OnHandDeltaCanonicalMilliUnits
                );
                nextReserved = checked(
                    previousReserved +
                    transaction.ReservedDeltaCanonicalMilliUnits
                );
            }
            catch (OverflowException)
            {
                error = "El libro desborda al reconstruir " +
                        transaction.TransactionId + ".";
                return false;
            }

            if (nextOnHand != transaction.NewOnHandCanonicalMilliUnits ||
                nextReserved != transaction.NewReservedCanonicalMilliUnits ||
                nextOnHand < 0L ||
                nextReserved < 0L ||
                nextReserved > nextOnHand)
            {
                error = "La transacción " + transaction.TransactionId +
                        " produce un balance inválido.";
                return false;
            }

            reconstructedOnHand[transaction.IngredientId] = nextOnHand;
            reconstructedReserved[transaction.IngredientId] = nextReserved;

            try
            {
                if (transaction.TransactionType ==
                    BistroBuilderInventoryTransactionType.Consumption)
                {
                    reconstructedConsumed[transaction.IngredientId] = checked(
                        reconstructedConsumed[transaction.IngredientId] +
                        transaction.QuantityCanonicalMilliUnits
                    );
                }
                else if (transaction.TransactionType ==
                    BistroBuilderInventoryTransactionType.Waste)
                {
                    reconstructedWasted[transaction.IngredientId] = checked(
                        reconstructedWasted[transaction.IngredientId] +
                        transaction.QuantityCanonicalMilliUnits
                    );
                }
            }
            catch (OverflowException)
            {
                error = "El libro desborda sus acumulados de consumo o " +
                        "merma en " + transaction.TransactionId + ".";
                return false;
            }
        }

        foreach (KeyValuePair<string, StockState> pair in stockByIngredientId)
        {
            if (reconstructedOnHand[pair.Key] != pair.Value.OnHand ||
                reconstructedReserved[pair.Key] != pair.Value.Reserved ||
                reconstructedConsumed[pair.Key] != pair.Value.Consumed ||
                reconstructedWasted[pair.Key] != pair.Value.Wasted)
            {
                error = "El libro reconstruido no coincide con el balance " +
                        "runtime de " + pair.Key + ".";
                return false;
            }
        }

        return true;
    }


    private static bool IsTransactionShapeValid(
        BistroBuilderInventoryTransactionSnapshot transaction
    )
    {
        long quantity = transaction.QuantityCanonicalMilliUnits;

        if (quantity <= 0L)
        {
            return false;
        }

        switch (transaction.TransactionType)
        {
            case BistroBuilderInventoryTransactionType.InitialStock:
            case BistroBuilderInventoryTransactionType.Purchase:
                return transaction.OnHandDeltaCanonicalMilliUnits == quantity &&
                       transaction.ReservedDeltaCanonicalMilliUnits == 0L;

            case BistroBuilderInventoryTransactionType.Reservation:
                return transaction.OnHandDeltaCanonicalMilliUnits == 0L &&
                       transaction.ReservedDeltaCanonicalMilliUnits == quantity;

            case BistroBuilderInventoryTransactionType.ReservationRelease:
                return transaction.OnHandDeltaCanonicalMilliUnits == 0L &&
                       transaction.ReservedDeltaCanonicalMilliUnits == -quantity;

            case BistroBuilderInventoryTransactionType.Consumption:
                return transaction.OnHandDeltaCanonicalMilliUnits == -quantity &&
                       transaction.ReservedDeltaCanonicalMilliUnits == -quantity;

            case BistroBuilderInventoryTransactionType.Waste:
                return transaction.OnHandDeltaCanonicalMilliUnits == -quantity &&
                       transaction.ReservedDeltaCanonicalMilliUnits == 0L;

            case BistroBuilderInventoryTransactionType.Correction:
                return transaction.ReservedDeltaCanonicalMilliUnits == 0L &&
                       transaction.OnHandDeltaCanonicalMilliUnits != 0L &&
                       Math.Abs(transaction.OnHandDeltaCanonicalMilliUnits) ==
                           quantity;

            default:
                return false;
        }
    }

    private bool TryCloseReservation(
        string operationId,
        string reservationId,
        BistroBuilderInventoryReservationStatus targetStatus,
        string reason,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalizedOperation = NormalizeRuntimeId(operationId);
        string normalizedReservation = NormalizeRuntimeId(reservationId);

        if (!TryValidateRuntimeId(normalizedOperation, "OperationId", out error) ||
            !TryValidateRuntimeId(
                normalizedReservation,
                "ReservationId",
                out error
            ))
        {
            return false;
        }

        string action = targetStatus ==
            BistroBuilderInventoryReservationStatus.Consumed
                ? "consume"
                : "release";
        string fingerprint = action + "|" + normalizedReservation;
        OperationReplay replay = EvaluateOperation(
            normalizedOperation,
            fingerprint,
            out error
        );

        if (replay == OperationReplay.Conflict)
        {
            return false;
        }

        if (replay == OperationReplay.Replayed)
        {
            return true;
        }

        if (!reservationsById.TryGetValue(
                normalizedReservation,
                out ReservationState reservation
            ))
        {
            error = "No existe la reserva " + normalizedReservation + ".";
            return false;
        }

        if (reservation.Status !=
            BistroBuilderInventoryReservationStatus.Active)
        {
            error = "La reserva " + normalizedReservation +
                    " ya está " + reservation.Status + ".";
            return false;
        }

        var pending = new List<PendingMutation>(reservation.Lines.Count);

        for (int index = 0; index < reservation.Lines.Count; index++)
        {
            ReservationLineState line = reservation.Lines[index];
            StockState state = stockByIngredientId[line.IngredientId];

            if (state.Reserved < line.Quantity)
            {
                error = "La reserva " + normalizedReservation +
                        " no puede cerrarse porque el balance reservado de " +
                        line.IngredientId + " es incoherente.";
                return false;
            }

            BistroBuilderInventoryTransactionType transactionType;
            long onHandDelta;
            long consumedDelta;

            if (targetStatus ==
                BistroBuilderInventoryReservationStatus.Consumed)
            {
                transactionType =
                    BistroBuilderInventoryTransactionType.Consumption;
                onHandDelta = -line.Quantity;
                consumedDelta = line.Quantity;
            }
            else
            {
                transactionType =
                    BistroBuilderInventoryTransactionType.ReservationRelease;
                onHandDelta = 0L;
                consumedDelta = 0L;
            }

            if (!TryBuildMutation(
                    state,
                    transactionType,
                    line.Quantity,
                    onHandDelta,
                    -line.Quantity,
                    consumedDelta,
                    0L,
                    normalizedOperation,
                    reservation.SourceId,
                    string.IsNullOrWhiteSpace(reason)
                        ? action + " " + normalizedReservation + "."
                        : reason,
                    out PendingMutation mutation,
                    out error
                ))
            {
                return false;
            }

            pending.Add(mutation);
        }

        reservation.Status = targetStatus;
        reservation.Revision = runtimeRevision + 1L;
        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(pending);
        PublishReservationChanged(reservation.ToSnapshot());
        return true;
    }

    private bool TryAggregateLines(
        IReadOnlyList<BistroBuilderInventoryQuantityLine> requestedLines,
        out SortedDictionary<string, long> aggregated,
        out string error
    )
    {
        aggregated = new SortedDictionary<string, long>(
            StringComparer.Ordinal
        );
        error = string.Empty;

        if (requestedLines == null || requestedLines.Count == 0)
        {
            error = "La reserva debe contener al menos una línea.";
            return false;
        }

        for (int index = 0; index < requestedLines.Count; index++)
        {
            BistroBuilderInventoryQuantityLine line = requestedLines[index];

            if (line == null)
            {
                error = "La reserva contiene una línea nula en la posición " +
                        index + ".";
                return false;
            }

            if (!TryGetKnownState(
                    line.IngredientId,
                    out StockState state,
                    out error
                ) ||
                !TryValidatePositiveQuantity(
                    line.CanonicalMilliUnits,
                    out error
                ))
            {
                return false;
            }

            aggregated.TryGetValue(state.IngredientId, out long current);

            try
            {
                long combined = checked(current + line.CanonicalMilliUnits);

                if (combined >
                    BistroBuilderMeasurementUtility
                        .MaximumCanonicalMilliUnits)
                {
                    error = "La reserva de " + state.IngredientId +
                            " excede el rango permitido.";
                    return false;
                }

                aggregated[state.IngredientId] = combined;
            }
            catch (OverflowException)
            {
                error = "La reserva de " + state.IngredientId +
                        " excede el rango permitido.";
                return false;
            }
        }

        return true;
    }

    private static string BuildReservationFingerprint(
        string action,
        string reservationId,
        string sourceId,
        SortedDictionary<string, long> lines
    )
    {
        var builder = new StringBuilder(256);
        builder.Append(action);
        builder.Append('|');
        builder.Append(reservationId);
        builder.Append('|');
        builder.Append(sourceId);

        foreach (KeyValuePair<string, long> pair in lines)
        {
            builder.Append('|');
            builder.Append(pair.Key);
            builder.Append(':');
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }

    private bool TryBuildMutation(
        StockState state,
        BistroBuilderInventoryTransactionType transactionType,
        long quantity,
        long onHandDelta,
        long reservedDelta,
        long consumedDelta,
        long wastedDelta,
        string operationId,
        string sourceId,
        string reason,
        out PendingMutation mutation,
        out string error
    )
    {
        mutation = default;
        error = string.Empty;

        if (state == null)
        {
            error = "No existe el balance que se pretende modificar.";
            return false;
        }

        long newOnHand;
        long newReserved;
        long newConsumed;
        long newWasted;

        try
        {
            newOnHand = checked(state.OnHand + onHandDelta);
            newReserved = checked(state.Reserved + reservedDelta);
            newConsumed = checked(state.Consumed + consumedDelta);
            newWasted = checked(state.Wasted + wastedDelta);
        }
        catch (OverflowException)
        {
            error = "El movimiento de " + state.IngredientId +
                    " excede el rango permitido.";
            return false;
        }

        long maximum =
            BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits;

        if (quantity <= 0L ||
            newOnHand < 0L ||
            newOnHand > maximum ||
            newReserved < 0L ||
            newReserved > newOnHand ||
            newConsumed < 0L ||
            newConsumed > maximum ||
            newWasted < 0L ||
            newWasted > maximum)
        {
            error = "El movimiento de " + state.IngredientId +
                    " produciría un balance inválido.";
            return false;
        }

        mutation = new PendingMutation(
            state,
            transactionType,
            quantity,
            onHandDelta,
            reservedDelta,
            newOnHand,
            newReserved,
            newConsumed,
            newWasted,
            operationId,
            sourceId,
            SanitizeReason(reason)
        );
        return true;
    }

    /// <summary>
    /// Aplica primero todos los balances y publica después eventos. Así un
    /// observador nunca ve media reserva aplicada.
    /// </summary>
    private void CommitMutations(List<PendingMutation> mutations)
    {
        if (mutations == null || mutations.Count == 0)
        {
            return;
        }

        runtimeRevision++;

        for (int index = 0; index < mutations.Count; index++)
        {
            PendingMutation mutation = mutations[index];
            mutation.State.OnHand = mutation.NewOnHand;
            mutation.State.Reserved = mutation.NewReserved;
            mutation.State.Consumed = mutation.NewConsumed;
            mutation.State.Wasted = mutation.NewWasted;
            mutation.State.Revision = runtimeRevision;
        }

        var stockSnapshots = new List<BistroBuilderInventoryStockSnapshot>(
            mutations.Count
        );
        var transactions =
            new List<BistroBuilderInventoryTransactionSnapshot>(
                mutations.Count
            );
        long timestamp = DateTime.UtcNow.Ticks;

        for (int index = 0; index < mutations.Count; index++)
        {
            PendingMutation mutation = mutations[index];
            long sequence = nextTransactionSequence++;
            var transaction =
                new BistroBuilderInventoryTransactionSnapshot(
                    sequence,
                    "inventory_tx_" + sequence.ToString("D8"),
                    mutation.OperationId,
                    mutation.State.IngredientId,
                    mutation.TransactionType,
                    mutation.Quantity,
                    mutation.OnHandDelta,
                    mutation.ReservedDelta,
                    mutation.PreviousOnHand,
                    mutation.NewOnHand,
                    mutation.PreviousReserved,
                    mutation.NewReserved,
                    mutation.SourceId,
                    mutation.Reason,
                    timestamp
                );

            ledger.Add(transaction);
            transactions.Add(transaction);
            stockSnapshots.Add(mutation.State.ToSnapshot());
        }

        for (int index = 0; index < transactions.Count; index++)
        {
            PublishTransactionRecorded(transactions[index]);
            PublishStockChanged(stockSnapshots[index]);
        }
    }

    private void PublishStockChanged(
        BistroBuilderInventoryStockSnapshot snapshot
    )
    {
        Action<BistroBuilderInventoryStockSnapshot> handlers = StockChanged;

        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<BistroBuilderInventoryStockSnapshot>)
                    invocationList[index]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void PublishReservationChanged(
        BistroBuilderInventoryReservationSnapshot snapshot
    )
    {
        Action<BistroBuilderInventoryReservationSnapshot> handlers =
            ReservationChanged;

        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<BistroBuilderInventoryReservationSnapshot>)
                    invocationList[index]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void PublishTransactionRecorded(
        BistroBuilderInventoryTransactionSnapshot snapshot
    )
    {
        Action<BistroBuilderInventoryTransactionSnapshot> handlers =
            TransactionRecorded;

        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<BistroBuilderInventoryTransactionSnapshot>)
                    invocationList[index]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private bool TryGetKnownState(
        string ingredientId,
        out StockState state,
        out string error
    )
    {
        state = null;
        error = string.Empty;
        string normalized = NormalizeIngredientId(ingredientId);

        if (string.IsNullOrWhiteSpace(normalized) ||
            !stockByIngredientId.TryGetValue(normalized, out state))
        {
            error = "No existe el ingrediente de inventario '" +
                    (ingredientId ?? string.Empty) + "'.";
            return false;
        }

        return true;
    }

    private bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        error = "El inventario canónico no está inicializado.";
        return false;
    }

    private OperationReplay EvaluateOperation(
        string operationId,
        string fingerprint,
        out string error
    )
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(operationId))
        {
            return OperationReplay.None;
        }

        if (!operationsById.TryGetValue(
                operationId,
                out OperationRecord existing
            ))
        {
            return OperationReplay.None;
        }

        if (string.Equals(
                existing.Fingerprint,
                fingerprint,
                StringComparison.Ordinal
            ))
        {
            return OperationReplay.Replayed;
        }

        error = "El OperationId " + operationId +
                " ya fue utilizado para una operación diferente.";
        return OperationReplay.Conflict;
    }

    private void RememberOperation(string operationId, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        operationsById[operationId] = new OperationRecord(fingerprint);
    }

    private static bool TryValidatePositiveQuantity(
        long quantity,
        out string error
    )
    {
        if (quantity <= 0L ||
            quantity >
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
        {
            error = "La cantidad canónica debe ser mayor que cero y quedar " +
                    "dentro del rango permitido.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateRuntimeId(
        string value,
        string fieldName,
        out string error
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            error = fieldName + " no puede estar vacío.";
            return false;
        }

        if (value.Length > 160)
        {
            error = fieldName + " excede 160 caracteres.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeIngredientId(string value)
    {
        return BistroBuilderMenuIdUtility.NormalizeStableId(value);
    }

    private static string NormalizeRuntimeId(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    private static string SanitizeReason(string value)
    {
        string result = value != null ? value.Trim() : string.Empty;

        if (result.Length > 256)
        {
            result = result.Substring(0, 256);
        }

        return result;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif

    private enum OperationReplay
    {
        None = 0,
        Replayed = 1,
        Conflict = 2
    }

    private sealed class OperationRecord
    {
        public string Fingerprint { get; }

        public OperationRecord(string fingerprint)
        {
            Fingerprint = fingerprint ?? string.Empty;
        }
    }

    private sealed class StockState
    {
        public BistroBuilderIngredientDefinition Definition { get; }
        public string IngredientId => Definition.IngredientId;
        public string StorageLocationId;
        public long OnHand;
        public long Reserved;
        public long Consumed;
        public long Wasted;
        public long Revision;
        public long Available => OnHand - Reserved;

        public StockState(
            BistroBuilderIngredientDefinition definition,
            string storageLocationId
        )
        {
            Definition = definition;
            StorageLocationId = storageLocationId;
        }

        public BistroBuilderInventoryStockSnapshot ToSnapshot()
        {
            return new BistroBuilderInventoryStockSnapshot(
                IngredientId,
                StorageLocationId,
                Definition.BaseUnit,
                OnHand,
                Reserved,
                Consumed,
                Wasted,
                Revision
            );
        }
    }

    private sealed class ReservationLineState
    {
        public string IngredientId { get; }
        public long Quantity { get; }

        public ReservationLineState(string ingredientId, long quantity)
        {
            IngredientId = ingredientId;
            Quantity = quantity;
        }
    }

    private sealed class ReservationState
    {
        public string ReservationId { get; }
        public string SourceId { get; }
        public List<ReservationLineState> Lines { get; }
        public BistroBuilderInventoryReservationStatus Status;
        public long Revision;

        public ReservationState(
            string reservationId,
            string sourceId,
            List<ReservationLineState> lines
        )
        {
            ReservationId = reservationId;
            SourceId = sourceId;
            Lines = lines;
            Status = BistroBuilderInventoryReservationStatus.Active;
        }

        public BistroBuilderInventoryReservationSnapshot ToSnapshot()
        {
            var snapshots =
                new List<BistroBuilderInventoryReservationLineSnapshot>(
                    Lines.Count
                );

            for (int index = 0; index < Lines.Count; index++)
            {
                snapshots.Add(
                    new BistroBuilderInventoryReservationLineSnapshot(
                        Lines[index].IngredientId,
                        Lines[index].Quantity
                    )
                );
            }

            return new BistroBuilderInventoryReservationSnapshot(
                ReservationId,
                SourceId,
                Status,
                Revision,
                snapshots
            );
        }
    }

    private readonly struct PendingMutation
    {
        public StockState State { get; }
        public BistroBuilderInventoryTransactionType TransactionType { get; }
        public long Quantity { get; }
        public long OnHandDelta { get; }
        public long ReservedDelta { get; }
        public long PreviousOnHand { get; }
        public long NewOnHand { get; }
        public long PreviousReserved { get; }
        public long NewReserved { get; }
        public long NewConsumed { get; }
        public long NewWasted { get; }
        public string OperationId { get; }
        public string SourceId { get; }
        public string Reason { get; }

        public PendingMutation(
            StockState state,
            BistroBuilderInventoryTransactionType transactionType,
            long quantity,
            long onHandDelta,
            long reservedDelta,
            long newOnHand,
            long newReserved,
            long newConsumed,
            long newWasted,
            string operationId,
            string sourceId,
            string reason
        )
        {
            State = state;
            TransactionType = transactionType;
            Quantity = quantity;
            OnHandDelta = onHandDelta;
            ReservedDelta = reservedDelta;
            PreviousOnHand = state.OnHand;
            NewOnHand = newOnHand;
            PreviousReserved = state.Reserved;
            NewReserved = newReserved;
            NewConsumed = newConsumed;
            NewWasted = newWasted;
            OperationId = operationId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
