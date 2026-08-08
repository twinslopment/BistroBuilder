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

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [Header("Depuración")]

    [SerializeField]
    private bool logInitialization = true;

    private readonly Dictionary<string, StockState> stockByIngredientId =
        new Dictionary<string, StockState>(StringComparer.Ordinal);

    private readonly Dictionary<string, ReservationState> reservationsById =
        new Dictionary<string, ReservationState>(StringComparer.Ordinal);

    private readonly Dictionary<string, OperationRecord> operationsById =
        new Dictionary<string, OperationRecord>(StringComparer.Ordinal);

    private readonly Dictionary<string, LotState> lotsById =
        new Dictionary<string, LotState>(StringComparer.Ordinal);

    private readonly List<BistroBuilderInventoryTransactionSnapshot> ledger =
        new List<BistroBuilderInventoryTransactionSnapshot>();

    private long nextTransactionSequence = 1L;
    private long nextLotSequence = 1L;
    private long runtimeRevision;
    private int lastShelfLifeProcessedDayIndex;
    private bool initialized;

    public event Action<BistroBuilderInventoryStockSnapshot> StockChanged;

    public event Action<BistroBuilderInventoryReservationSnapshot>
        ReservationChanged;

    public event Action<BistroBuilderInventoryTransactionSnapshot>
        TransactionRecorded;

    public event Action<BistroBuilderInventoryLotSnapshot> LotChanged;

    public bool IsInitialized => initialized;

    public int StockEntryCount => stockByIngredientId.Count;

    public int ReservationCount => reservationsById.Count;

    public int TransactionCount => ledger.Count;

    public int LotCount => lotsById.Count;

    public int LastShelfLifeProcessedDayIndex =>
        lastShelfLifeProcessedDayIndex;

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

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }
    }

    private void OnDisable()
    {
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
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

        if (generalGameStateService == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService para fechar lotes.";
            return false;
        }

        if (!generalGameStateService.ValidateConfiguration(out error))
        {
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
        lotsById.Clear();
        ledger.Clear();
        nextTransactionSequence = 1L;
        nextLotSequence = 1L;
        runtimeRevision = 0L;
        lastShelfLifeProcessedDayIndex = CurrentDayIndex;
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
        var lotMutations = new List<PendingLotMutation>();
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
            lotMutations.Add(
                BuildNewLotMutation(
                    state,
                    quantity,
                    openingStockProfile.ProfileId,
                    CurrentDayIndex
                )
            );
        }

        if (pending.Count > 0)
        {
            CommitMutations(pending, lotMutations);
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

        snapshot = state.ToSnapshot(CurrentDayIndex, lotsById.Values);
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
            destination.Add(stockByIngredientId[keys[index]].ToSnapshot(CurrentDayIndex, lotsById.Values));
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

    public bool TryGetLotSnapshot(
        string lotId,
        out BistroBuilderInventoryLotSnapshot snapshot
    )
    {
        snapshot = default;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(lotId);

        if (!initialized || string.IsNullOrWhiteSpace(normalized) ||
            !lotsById.TryGetValue(normalized, out LotState lot))
        {
            return false;
        }

        snapshot = lot.ToSnapshot(CurrentDayIndex);
        return true;
    }

    public void CopyLotSnapshotsTo(
        List<BistroBuilderInventoryLotSnapshot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        var lotIds = new List<string>(lotsById.Keys);
        lotIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < lotIds.Count; index++)
        {
            destination.Add(
                lotsById[lotIds[index]].ToSnapshot(CurrentDayIndex)
            );
        }
    }

    /// <summary>
    /// Evalúa caducidad para el día actual. Es idempotente por DayIndex y
    /// nunca usa horas del servicio. Las cantidades ya reservadas quedan
    /// comprometidas con su reserva existente; la parte libre caducada se
    /// retira inmediatamente y no puede formar nuevas reservas.
    /// </summary>
    public bool TryProcessShelfLifeForCurrentDay(out string error)
    {
        return TryProcessShelfLifeThroughDay(CurrentDayIndex, out error);
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

        if (!EnsureInventoryReady(out error))
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

        PendingLotMutation lotMutation = BuildNewLotMutation(
            state,
            canonicalMilliUnits,
            normalizedSource,
            CurrentDayIndex
        );

        RememberOperation(normalizedOperation, fingerprint);
        CommitMutations(
            new List<PendingMutation> { mutation },
            new List<PendingLotMutation> { lotMutation }
        );
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

        if (!EnsureInventoryReady(out error))
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
        var lotMutations = new List<PendingLotMutation>();
        var reservationLines = new List<ReservationLineState>(
            aggregated.Count
        );

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            StockState state = stockByIngredientId[pair.Key];

            if (!TryBuildFefoReservationAllocation(
                    state,
                    pair.Value,
                    out List<LotAllocationState> allocations,
                    out List<PendingLotMutation> lineLotMutations,
                    out error
                ))
            {
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
            lotMutations.AddRange(lineLotMutations);
            reservationLines.Add(
                new ReservationLineState(
                    pair.Key,
                    pair.Value,
                    allocations
                )
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
        CommitMutations(pending, lotMutations);
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

        if (!EnsureInventoryReady(out error) ||
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

        if (!TryBuildFefoAvailableRemoval(
                state,
                canonicalMilliUnits,
                out List<PendingLotMutation> lotMutations,
                out error
            ))
        {
            error = "No se puede registrar merma de " + state.IngredientId +
                    ". " + error;
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
        CommitMutations(
            new List<PendingMutation> { mutation },
            lotMutations
        );
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

        if (!EnsureInventoryReady(out error) ||
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

        List<PendingLotMutation> lotMutations;
        if (delta > 0L)
        {
            lotMutations = new List<PendingLotMutation>
            {
                BuildNewLotMutation(
                    state,
                    delta,
                    normalizedSource,
                    CurrentDayIndex
                )
            };
        }
        else if (!TryBuildFefoAvailableRemoval(
                     state,
                     -delta,
                     out lotMutations,
                     out error
                 ))
        {
            return false;
        }

        if (!TryBuildMutation(
                state,
                BistroBuilderInventoryTransactionType.Correction,
                Math.Abs(delta),
                delta,
                0L,
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
        CommitMutations(
            new List<PendingMutation> { mutation },
            lotMutations
        );
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
        var activeAllocatedByLot = new Dictionary<string, long>(
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

                if (line == null ||
                    !stockByIngredientId.ContainsKey(line.IngredientId) ||
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

                if (line.LotAllocations == null ||
                    line.LotAllocations.Count == 0)
                {
                    error = "La reserva activa " + reservation.ReservationId +
                            " no conserva su asignación FEFO.";
                    return false;
                }

                long allocatedLine = 0L;
                var lotIds = new HashSet<string>(StringComparer.Ordinal);

                for (int allocationIndex = 0;
                     allocationIndex < line.LotAllocations.Count;
                     allocationIndex++)
                {
                    LotAllocationState allocation =
                        line.LotAllocations[allocationIndex];

                    if (allocation == null ||
                        allocation.Quantity <= 0L ||
                        !lotIds.Add(allocation.LotId) ||
                        !lotsById.TryGetValue(
                            allocation.LotId,
                            out LotState lot
                        ) ||
                        !string.Equals(
                            lot.IngredientId,
                            line.IngredientId,
                            StringComparison.Ordinal
                        ))
                    {
                        error = "La reserva " + reservation.ReservationId +
                                " contiene una asignación FEFO inválida.";
                        return false;
                    }

                    try
                    {
                        allocatedLine = checked(
                            allocatedLine + allocation.Quantity
                        );
                        activeAllocatedByLot.TryGetValue(
                            allocation.LotId,
                            out long lotAllocated
                        );
                        activeAllocatedByLot[allocation.LotId] = checked(
                            lotAllocated + allocation.Quantity
                        );
                    }
                    catch (OverflowException)
                    {
                        error = "Las asignaciones FEFO activas desbordan el " +
                                "rango permitido.";
                        return false;
                    }
                }

                if (allocatedLine != line.Quantity)
                {
                    error = "La reserva " + reservation.ReservationId +
                            " no asigna exactamente su cantidad de " +
                            line.IngredientId + " a lotes.";
                    return false;
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

        var lotOnHandByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var lotReservedByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );

        foreach (KeyValuePair<string, LotState> pair in lotsById)
        {
            LotState lot = pair.Value;

            if (lot == null ||
                !string.Equals(pair.Key, lot.LotId, StringComparison.Ordinal) ||
                !stockByIngredientId.ContainsKey(lot.IngredientId) ||
                !BistroBuilderMenuIdUtility.IsValidStableId(lot.LotId) ||
                lot.ReceivedDayIndex < 1 ||
                lot.ReceivedDayIndex > CurrentDayIndex ||
                (lot.ExpirationDayIndex != 0 &&
                 lot.ExpirationDayIndex <= lot.ReceivedDayIndex) ||
                (lot.ExpirationDayIndex > 0 &&
                 lot.ExpirationDayIndex <=
                    lastShelfLifeProcessedDayIndex &&
                 lot.Available > 0L) ||
                lot.OnHand < 0L ||
                lot.Reserved < 0L ||
                lot.Reserved > lot.OnHand ||
                lot.Revision < 0L)
            {
                error = "El lote interno " + pair.Key + " es inválido.";
                return false;
            }

            activeAllocatedByLot.TryGetValue(
                lot.LotId,
                out long expectedLotReserved
            );
            if (expectedLotReserved != lot.Reserved)
            {
                error = "El reservado del lote " + lot.LotId +
                        " no coincide con las reservas activas. Esperado: " +
                        expectedLotReserved + "; real: " + lot.Reserved + ".";
                return false;
            }

            try
            {
                lotOnHandByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long lotOnHand
                );
                lotReservedByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long lotReserved
                );
                lotOnHandByIngredient[lot.IngredientId] = checked(
                    lotOnHand + lot.OnHand
                );
                lotReservedByIngredient[lot.IngredientId] = checked(
                    lotReserved + lot.Reserved
                );
            }
            catch (OverflowException)
            {
                error = "Los lotes internos desbordan el rango del " +
                        "inventario.";
                return false;
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
                state.Expired < 0L ||
                state.Revision < 0L ||
                !BistroBuilderMenuIdUtility.IsValidStableId(
                    state.StorageLocationId
                ))
            {
                error = "Balance interno inválido para " + pair.Key + ".";
                return false;
            }

            activeReserved.TryGetValue(pair.Key, out long expectedReserved);
            lotOnHandByIngredient.TryGetValue(pair.Key, out long expectedOnHand);
            lotReservedByIngredient.TryGetValue(
                pair.Key,
                out long expectedLotReserved
            );

            if (expectedReserved != state.Reserved ||
                expectedLotReserved != state.Reserved)
            {
                error = "La reserva agregada de " + pair.Key +
                        " no coincide con sus reservas/lotes. Reservas: " +
                        expectedReserved + "; lotes: " + expectedLotReserved +
                        "; balance: " + state.Reserved + ".";
                return false;
            }

            if (expectedOnHand != state.OnHand)
            {
                error = "La existencia agregada de " + pair.Key +
                        " no coincide con sus lotes. Lotes: " +
                        expectedOnHand + "; balance: " + state.OnHand + ".";
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
        var reconstructedExpired = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var transactionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (string ingredientId in stockByIngredientId.Keys)
        {
            reconstructedOnHand.Add(ingredientId, 0L);
            reconstructedReserved.Add(ingredientId, 0L);
            reconstructedConsumed.Add(ingredientId, 0L);
            reconstructedWasted.Add(ingredientId, 0L);
            reconstructedExpired.Add(ingredientId, 0L);
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
                else if (transaction.TransactionType ==
                    BistroBuilderInventoryTransactionType.Expiration)
                {
                    reconstructedExpired[transaction.IngredientId] = checked(
                        reconstructedExpired[transaction.IngredientId] +
                        transaction.QuantityCanonicalMilliUnits
                    );
                }
            }
            catch (OverflowException)
            {
                error = "El libro desborda sus acumulados de consumo, " +
                        "merma o caducidad en " + transaction.TransactionId +
                        ".";
                return false;
            }
        }

        foreach (KeyValuePair<string, StockState> pair in stockByIngredientId)
        {
            if (reconstructedOnHand[pair.Key] != pair.Value.OnHand ||
                reconstructedReserved[pair.Key] != pair.Value.Reserved ||
                reconstructedConsumed[pair.Key] != pair.Value.Consumed ||
                reconstructedWasted[pair.Key] != pair.Value.Wasted ||
                reconstructedExpired[pair.Key] != pair.Value.Expired)
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

            case BistroBuilderInventoryTransactionType.Expiration:
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

        if (!EnsureInventoryReady(out error))
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
        var lotMutations = new List<PendingLotMutation>();

        for (int index = 0; index < reservation.Lines.Count; index++)
        {
            ReservationLineState line = reservation.Lines[index];
            StockState state = stockByIngredientId[line.IngredientId];

            if (state.Reserved < line.Quantity ||
                line.LotAllocations == null ||
                line.LotAllocations.Count == 0)
            {
                error = "La reserva " + normalizedReservation +
                        " no puede cerrarse porque su asignación FEFO de " +
                        line.IngredientId + " es incoherente.";
                return false;
            }

            long allocated = 0L;
            for (int allocationIndex = 0;
                 allocationIndex < line.LotAllocations.Count;
                 allocationIndex++)
            {
                LotAllocationState allocation =
                    line.LotAllocations[allocationIndex];
                if (!lotsById.TryGetValue(
                        allocation.LotId,
                        out LotState lot
                    ) ||
                    !string.Equals(
                        lot.IngredientId,
                        line.IngredientId,
                        StringComparison.Ordinal
                    ) ||
                    allocation.Quantity <= 0L ||
                    lot.Reserved < allocation.Quantity ||
                    (targetStatus ==
                         BistroBuilderInventoryReservationStatus.Consumed &&
                     lot.OnHand < allocation.Quantity))
                {
                    error = "La reserva " + normalizedReservation +
                            " referencia un lote FEFO inválido.";
                    return false;
                }

                allocated = checked(allocated + allocation.Quantity);
                long newOnHand = targetStatus ==
                    BistroBuilderInventoryReservationStatus.Consumed
                        ? lot.OnHand - allocation.Quantity
                        : lot.OnHand;
                lotMutations.Add(
                    new PendingLotMutation(
                        lot,
                        false,
                        newOnHand,
                        lot.Reserved - allocation.Quantity
                    )
                );
            }

            if (allocated != line.Quantity)
            {
                error = "La reserva " + normalizedReservation +
                        " no asigna exactamente la cantidad de " +
                        line.IngredientId + ".";
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
        CommitMutations(pending, lotMutations);

        if (targetStatus ==
            BistroBuilderInventoryReservationStatus.Released)
        {
            if (!TryExpireAvailableLotsAtDay(
                    CurrentDayIndex,
                    "Liberación de reserva caducada.",
                    out string expirationError
                ))
            {
                Debug.LogError(expirationError, this);
            }
        }

        for (int index = 0; index < reservation.Lines.Count; index++)
        {
            reservation.Lines[index].LotAllocations.Clear();
        }

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

    private int CurrentDayIndex
    {
        get
        {
            return generalGameStateService != null
                ? Math.Max(1, generalGameStateService.DayIndex)
                : 1;
        }
    }

    private bool EnsureInventoryReady(out string error)
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (generalGameStateService != null &&
            generalGameStateService.DayIndex > lastShelfLifeProcessedDayIndex &&
            (saveGameService == null || !saveGameService.IsBusy))
        {
            return TryProcessShelfLifeThroughDay(
                generalGameStateService.DayIndex,
                out error
            );
        }

        error = string.Empty;
        return true;
    }

    private void HandleCalendarChanged()
    {
        if (!initialized || generalGameStateService == null)
        {
            return;
        }

        // Durante una carga, game.general se aplica antes que inventory.canonical.
        // No procesamos el inventario de la sesión anterior: el proveedor de
        // inventario reconciliará el día una vez restaurado su snapshot.
        if (saveGameService != null && saveGameService.IsBusy)
        {
            return;
        }

        if (!TryProcessShelfLifeThroughDay(
                generalGameStateService.DayIndex,
                out string error
            ))
        {
            Debug.LogError(
                "No se pudo procesar la caducidad diaria. " + error,
                this
            );
        }
    }

    private bool TryProcessShelfLifeThroughDay(
        int dayIndex,
        out string error
    )
    {
        error = string.Empty;

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (dayIndex < 1)
        {
            error = "El DayIndex de caducidad debe ser mayor que cero.";
            return false;
        }

        if (dayIndex <= lastShelfLifeProcessedDayIndex)
        {
            return true;
        }

        if (!TryExpireAvailableLotsAtDay(
                dayIndex,
                "Caducidad diaria FEFO.",
                out error
            ))
        {
            return false;
        }

        lastShelfLifeProcessedDayIndex = dayIndex;
        return true;
    }

    private bool TryExpireAvailableLotsAtDay(
        int dayIndex,
        string reason,
        out string error
    )
    {
        error = string.Empty;
        var stockMutations = new List<PendingMutation>();
        var lotMutations = new List<PendingLotMutation>();
        var operations = new List<KeyValuePair<string, string>>();

        var ingredientIds = new List<string>(stockByIngredientId.Keys);
        ingredientIds.Sort(StringComparer.Ordinal);

        for (int ingredientIndex = 0;
             ingredientIndex < ingredientIds.Count;
             ingredientIndex++)
        {
            string ingredientId = ingredientIds[ingredientIndex];
            StockState state = stockByIngredientId[ingredientId];
            long quantityToExpire = 0L;
            var lineLotMutations = new List<PendingLotMutation>();

            foreach (LotState lot in lotsById.Values)
            {
                if (lot == null ||
                    !string.Equals(
                        lot.IngredientId,
                        ingredientId,
                        StringComparison.Ordinal
                    ) ||
                    lot.ExpirationDayIndex <= 0 ||
                    lot.ExpirationDayIndex > dayIndex ||
                    lot.Available <= 0L)
                {
                    continue;
                }

                long quantity = lot.Available;
                try
                {
                    quantityToExpire = checked(quantityToExpire + quantity);
                }
                catch (OverflowException)
                {
                    error = "La caducidad de " + ingredientId +
                            " excede el rango permitido.";
                    return false;
                }

                lineLotMutations.Add(
                    new PendingLotMutation(
                        lot,
                        false,
                        lot.OnHand - quantity,
                        lot.Reserved
                    )
                );
            }

            if (quantityToExpire <= 0L)
            {
                continue;
            }

            long expirationSequence = nextTransactionSequence +
                                      stockMutations.Count;
            string operationId = "inventory_expire_tx_" +
                                 expirationSequence.ToString("D8");
            string fingerprint = "expiration|" + dayIndex + "|" +
                                 ingredientId + "|" + quantityToExpire;
            OperationReplay replay = EvaluateOperation(
                operationId,
                fingerprint,
                out error
            );
            if (replay == OperationReplay.Conflict)
            {
                return false;
            }
            if (replay == OperationReplay.Replayed)
            {
                continue;
            }

            if (!TryBuildMutation(
                    state,
                    BistroBuilderInventoryTransactionType.Expiration,
                    quantityToExpire,
                    -quantityToExpire,
                    0L,
                    0L,
                    0L,
                    quantityToExpire,
                    operationId,
                    "inventory_shelf_life",
                    reason,
                    out PendingMutation stockMutation,
                    out error
                ))
            {
                return false;
            }

            stockMutations.Add(stockMutation);
            lotMutations.AddRange(lineLotMutations);
            operations.Add(
                new KeyValuePair<string, string>(operationId, fingerprint)
            );
        }

        for (int index = 0; index < operations.Count; index++)
        {
            RememberOperation(
                operations[index].Key,
                operations[index].Value
            );
        }

        CommitMutations(stockMutations, lotMutations);
        return true;
    }

    private PendingLotMutation BuildNewLotMutation(
        StockState state,
        long quantity,
        string sourceId,
        int receivedDayIndex
    )
    {
        int shelfLifeDays = state != null && state.Definition != null
            ? Math.Max(0, state.Definition.DefaultShelfLifeDays)
            : 0;
        int expirationDayIndex = 0;
        if (shelfLifeDays > 0)
        {
            long candidate = (long)receivedDayIndex + shelfLifeDays;
            expirationDayIndex = candidate <= int.MaxValue
                ? (int)candidate
                : int.MaxValue;
        }

        string lotId;
        do
        {
            if (nextLotSequence < 1L)
            {
                nextLotSequence = 1L;
            }

            lotId = "inventory_lot_" + nextLotSequence.ToString("D8");
            nextLotSequence++;
        }
        while (lotsById.ContainsKey(lotId));

        var lot = new LotState(
            lotId,
            state.IngredientId,
            string.IsNullOrWhiteSpace(sourceId)
                ? "inventory_internal"
                : sourceId,
            Math.Max(1, receivedDayIndex),
            expirationDayIndex,
            shelfLifeDays
        );

        return new PendingLotMutation(
            lot,
            true,
            quantity,
            0L
        );
    }

    private bool TryBuildFefoReservationAllocation(
        StockState state,
        long quantity,
        out List<LotAllocationState> allocations,
        out List<PendingLotMutation> lotMutations,
        out string error
    )
    {
        allocations = new List<LotAllocationState>();
        lotMutations = new List<PendingLotMutation>();
        error = string.Empty;

        if (state == null || quantity <= 0L)
        {
            error = "La asignación FEFO solicitada es inválida.";
            return false;
        }

        List<LotState> candidates = GetFefoAvailableLots(
            state.IngredientId,
            CurrentDayIndex
        );
        long remaining = quantity;

        for (int index = 0;
             index < candidates.Count && remaining > 0L;
             index++)
        {
            LotState lot = candidates[index];
            long allocated = Math.Min(lot.Available, remaining);
            if (allocated <= 0L)
            {
                continue;
            }

            allocations.Add(
                new LotAllocationState(lot.LotId, allocated)
            );
            lotMutations.Add(
                new PendingLotMutation(
                    lot,
                    false,
                    lot.OnHand,
                    lot.Reserved + allocated
                )
            );
            remaining -= allocated;
        }

        if (remaining > 0L)
        {
            error = "Stock FEFO utilizable insuficiente de " +
                    state.IngredientId + ". Disponible: " +
                    (quantity - remaining) + "; solicitado: " + quantity + ".";
            allocations.Clear();
            lotMutations.Clear();
            return false;
        }

        return true;
    }

    private bool TryBuildFefoAvailableRemoval(
        StockState state,
        long quantity,
        out List<PendingLotMutation> lotMutations,
        out string error
    )
    {
        lotMutations = new List<PendingLotMutation>();
        error = string.Empty;

        if (state == null || quantity <= 0L)
        {
            error = "La retirada FEFO solicitada es inválida.";
            return false;
        }

        List<LotState> candidates = GetFefoAvailableLots(
            state.IngredientId,
            CurrentDayIndex
        );
        long remaining = quantity;

        for (int index = 0;
             index < candidates.Count && remaining > 0L;
             index++)
        {
            LotState lot = candidates[index];
            long removed = Math.Min(lot.Available, remaining);
            if (removed <= 0L)
            {
                continue;
            }

            lotMutations.Add(
                new PendingLotMutation(
                    lot,
                    false,
                    lot.OnHand - removed,
                    lot.Reserved
                )
            );
            remaining -= removed;
        }

        if (remaining > 0L)
        {
            error = "Stock FEFO utilizable insuficiente de " +
                    state.IngredientId + ".";
            lotMutations.Clear();
            return false;
        }

        return true;
    }

    private List<LotState> GetFefoAvailableLots(
        string ingredientId,
        int currentDayIndex
    )
    {
        var result = new List<LotState>();
        foreach (LotState lot in lotsById.Values)
        {
            if (lot == null ||
                !string.Equals(
                    lot.IngredientId,
                    ingredientId,
                    StringComparison.Ordinal
                ) ||
                lot.Available <= 0L ||
                lot.IsExpired(currentDayIndex))
            {
                continue;
            }

            result.Add(lot);
        }

        result.Sort(CompareLotsForFefo);
        return result;
    }

    private static int CompareLotsForFefo(LotState left, LotState right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }
        if (left == null)
        {
            return 1;
        }
        if (right == null)
        {
            return -1;
        }

        int leftExpiration = left.ExpirationDayIndex > 0
            ? left.ExpirationDayIndex
            : int.MaxValue;
        int rightExpiration = right.ExpirationDayIndex > 0
            ? right.ExpirationDayIndex
            : int.MaxValue;
        int comparison = leftExpiration.CompareTo(rightExpiration);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.ReceivedDayIndex.CompareTo(right.ReceivedDayIndex);
        return comparison != 0
            ? comparison
            : string.CompareOrdinal(left.LotId, right.LotId);
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
        long expiredDelta,
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
        long newExpired;

        try
        {
            newOnHand = checked(state.OnHand + onHandDelta);
            newReserved = checked(state.Reserved + reservedDelta);
            newConsumed = checked(state.Consumed + consumedDelta);
            newWasted = checked(state.Wasted + wastedDelta);
            newExpired = checked(state.Expired + expiredDelta);
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
            newWasted > maximum ||
            newExpired < 0L ||
            newExpired > maximum)
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
            newExpired,
            operationId,
            sourceId,
            SanitizeReason(reason)
        );
        return true;
    }

    /// <summary>
    /// Aplica lotes y balances como una única revisión y publica eventos
    /// después. Los acumulados agregados son una caché auditada de los lotes.
    /// </summary>
    private void CommitMutations(List<PendingMutation> mutations)
    {
        CommitMutations(mutations, null);
    }

    private void CommitMutations(
        List<PendingMutation> mutations,
        List<PendingLotMutation> lotMutations
    )
    {
        bool hasStock = mutations != null && mutations.Count > 0;
        bool hasLots = lotMutations != null && lotMutations.Count > 0;
        if (!hasStock && !hasLots)
        {
            return;
        }

        runtimeRevision++;

        var changedLotIds = new HashSet<string>(StringComparer.Ordinal);
        if (hasLots)
        {
            for (int index = 0; index < lotMutations.Count; index++)
            {
                PendingLotMutation mutation = lotMutations[index];
                LotState lot = mutation.Lot;
                if (lot == null)
                {
                    continue;
                }

                if (mutation.IsNew && !lotsById.ContainsKey(lot.LotId))
                {
                    lotsById.Add(lot.LotId, lot);
                }

                lot.OnHand = mutation.NewOnHand;
                lot.Reserved = mutation.NewReserved;
                lot.Revision = runtimeRevision;
                changedLotIds.Add(lot.LotId);
            }
        }

        if (hasStock)
        {
            for (int index = 0; index < mutations.Count; index++)
            {
                PendingMutation mutation = mutations[index];
                mutation.State.OnHand = mutation.NewOnHand;
                mutation.State.Reserved = mutation.NewReserved;
                mutation.State.Consumed = mutation.NewConsumed;
                mutation.State.Wasted = mutation.NewWasted;
                mutation.State.Expired = mutation.NewExpired;
                mutation.State.Revision = runtimeRevision;
            }
        }

        var stockSnapshots = new List<BistroBuilderInventoryStockSnapshot>(
            hasStock ? mutations.Count : 0
        );
        var transactions =
            new List<BistroBuilderInventoryTransactionSnapshot>(
                hasStock ? mutations.Count : 0
            );
        long timestamp = DateTime.UtcNow.Ticks;

        if (hasStock)
        {
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
                stockSnapshots.Add(
                    mutation.State.ToSnapshot(
                        CurrentDayIndex,
                        lotsById.Values
                    )
                );
            }
        }

        foreach (string lotId in changedLotIds)
        {
            if (lotsById.TryGetValue(lotId, out LotState lot))
            {
                PublishLotChanged(lot.ToSnapshot(CurrentDayIndex));
            }
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

    private void PublishLotChanged(
        BistroBuilderInventoryLotSnapshot snapshot
    )
    {
        Action<BistroBuilderInventoryLotSnapshot> handlers = LotChanged;
        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<BistroBuilderInventoryLotSnapshot>)
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
        return BistroBuilderInventoryRuntimeIdUtility.TryValidateNormalized(
            value,
            fieldName,
            out error
        );
    }

    private static string NormalizeIngredientId(string value)
    {
        return BistroBuilderMenuIdUtility.NormalizeStableId(value);
    }

    private static string NormalizeRuntimeId(string value)
    {
        return BistroBuilderInventoryRuntimeIdUtility.Normalize(value);
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

        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }

        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }
    }


    /// <summary>
    /// Captura una fotografía profunda del inventario, incluido el libro y
    /// las operaciones idempotentes. Es segura para guardados con el servicio
    /// abierto porque el orquestador congela la simulación antes de invocarla.
    /// </summary>
    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderInventoryRuntimeSnapshot snapshot,
        out string error
    )
    {
        snapshot = null;
        error = string.Empty;

        if (!EnsureInitialized(out error) ||
            !ValidateRuntimeState(out error))
        {
            return false;
        }

        var captured = new BistroBuilderInventoryRuntimeSnapshot
        {
            nextTransactionSequence = nextTransactionSequence,
            nextLotSequence = nextLotSequence,
            runtimeRevision = runtimeRevision,
            lastShelfLifeProcessedDayIndex =
                lastShelfLifeProcessedDayIndex,
            requiresLotMaterialization = false
        };

        var ingredientIds = new List<string>(stockByIngredientId.Keys);
        ingredientIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < ingredientIds.Count; index++)
        {
            StockState state = stockByIngredientId[ingredientIds[index]];
            captured.stock.Add(
                new BistroBuilderInventoryStockSaveRecord
                {
                    ingredientId = state.IngredientId,
                    storageLocationId = state.StorageLocationId,
                    onHandCanonicalMilliUnits = state.OnHand,
                    reservedCanonicalMilliUnits = state.Reserved,
                    consumedCanonicalMilliUnits = state.Consumed,
                    wastedCanonicalMilliUnits = state.Wasted,
                    expiredCanonicalMilliUnits = state.Expired,
                    revision = state.Revision
                }
            );
        }

        var lotIds = new List<string>(lotsById.Keys);
        lotIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < lotIds.Count; index++)
        {
            LotState lot = lotsById[lotIds[index]];
            captured.lots.Add(
                new BistroBuilderInventoryLotSaveRecord
                {
                    lotId = lot.LotId,
                    ingredientId = lot.IngredientId,
                    sourceId = lot.SourceId,
                    receivedDayIndex = lot.ReceivedDayIndex,
                    expirationDayIndex = lot.ExpirationDayIndex,
                    onHandCanonicalMilliUnits = lot.OnHand,
                    reservedCanonicalMilliUnits = lot.Reserved,
                    revision = lot.Revision
                }
            );
        }

        var reservationIds = new List<string>(reservationsById.Keys);
        reservationIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < reservationIds.Count; index++)
        {
            ReservationState reservation =
                reservationsById[reservationIds[index]];
            var record = new BistroBuilderInventoryReservationSaveRecord
            {
                reservationId = reservation.ReservationId,
                sourceId = reservation.SourceId,
                status = (int)reservation.Status,
                revision = reservation.Revision
            };

            for (int lineIndex = 0;
                 lineIndex < reservation.Lines.Count;
                 lineIndex++)
            {
                ReservationLineState line = reservation.Lines[lineIndex];
                var lineRecord =
                    new BistroBuilderInventoryReservationLineSaveRecord
                    {
                        ingredientId = line.IngredientId,
                        canonicalMilliUnits = line.Quantity
                    };

                for (int allocationIndex = 0;
                     allocationIndex < line.LotAllocations.Count;
                     allocationIndex++)
                {
                    LotAllocationState allocation =
                        line.LotAllocations[allocationIndex];
                    lineRecord.lotAllocations.Add(
                        new BistroBuilderInventoryLotAllocationSaveRecord
                        {
                            lotId = allocation.LotId,
                            canonicalMilliUnits = allocation.Quantity
                        }
                    );
                }

                lineRecord.lotAllocations.Sort(
                    (left, right) => string.CompareOrdinal(
                        left != null ? left.lotId : string.Empty,
                        right != null ? right.lotId : string.Empty
                    )
                );
                record.lines.Add(lineRecord);
            }

            record.lines.Sort(
                (left, right) => string.CompareOrdinal(
                    left != null ? left.ingredientId : string.Empty,
                    right != null ? right.ingredientId : string.Empty
                )
            );
            captured.reservations.Add(record);
        }

        var operationIds = new List<string>(operationsById.Keys);
        operationIds.Sort(StringComparer.Ordinal);

        for (int index = 0; index < operationIds.Count; index++)
        {
            string operationId = operationIds[index];
            captured.operations.Add(
                new BistroBuilderInventoryOperationSaveRecord
                {
                    operationId = operationId,
                    fingerprint = operationsById[operationId].Fingerprint
                }
            );
        }

        for (int index = 0; index < ledger.Count; index++)
        {
            captured.ledger.Add(
                BistroBuilderInventoryTransactionSaveRecord.FromSnapshot(
                    ledger[index]
                )
            );
        }

        if (!captured.TryValidateBasic(out error))
        {
            return false;
        }

        snapshot = captured;
        return true;
    }

    /// <summary>
    /// Sustituye el inventario de forma atómica desde una fotografía.
    /// Ingredientes añadidos por una versión posterior del juego se crean con
    /// stock cero; ingredientes desconocidos contenidos en la partida bloquean
    /// la carga para no perder ni reinterpretar mercancía silenciosamente.
    /// </summary>
    public bool TryReplaceFromRuntimeSnapshot(
        BistroBuilderInventoryRuntimeSnapshot snapshot,
        bool notify,
        out string error
    )
    {
        error = string.Empty;

        if (snapshot == null)
        {
            error = "El snapshot de inventario es nulo.";
            return false;
        }

        if (!snapshot.TryValidateBasic(out error))
        {
            return false;
        }

        CacheDependenciesIfNeeded();

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (recipeCatalogService.IngredientCatalog == null)
        {
            error = "El catálogo canónico de ingredientes no está disponible.";
            return false;
        }

        if (!recipeCatalogService.ValidateConfiguration(out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "El catálogo de ingredientes y recetas no es válido.";
            }
            return false;
        }

        BistroBuilderInventoryRuntimeSnapshot rollback = null;

        if (initialized &&
            !TryCaptureRuntimeSnapshot(out rollback, out error))
        {
            return false;
        }

        if (!TryBuildRuntimeFromSnapshot(
                snapshot,
                out Dictionary<string, StockState> candidateStock,
                out Dictionary<string, LotState> candidateLots,
                out Dictionary<string, ReservationState> candidateReservations,
                out Dictionary<string, OperationRecord> candidateOperations,
                out List<BistroBuilderInventoryTransactionSnapshot>
                    candidateLedger,
                out long candidateNextLotSequence,
                out int candidateLastShelfLifeDay,
                out error
            ))
        {
            return false;
        }

        ApplyRuntimeReplacement(
            candidateStock,
            candidateLots,
            candidateReservations,
            candidateOperations,
            candidateLedger,
            snapshot.nextTransactionSequence,
            candidateNextLotSequence,
            snapshot.runtimeRevision,
            candidateLastShelfLifeDay
        );

        if (!ValidateRuntimeState(out error))
        {
            if (rollback != null &&
                TryBuildRuntimeFromSnapshot(
                    rollback,
                    out Dictionary<string, StockState> rollbackStock,
                    out Dictionary<string, LotState> rollbackLots,
                    out Dictionary<string, ReservationState>
                        rollbackReservations,
                    out Dictionary<string, OperationRecord>
                        rollbackOperations,
                    out List<BistroBuilderInventoryTransactionSnapshot>
                        rollbackLedger,
                    out long rollbackNextLotSequence,
                    out int rollbackLastShelfLifeDay,
                    out _
                ))
            {
                ApplyRuntimeReplacement(
                    rollbackStock,
                    rollbackLots,
                    rollbackReservations,
                    rollbackOperations,
                    rollbackLedger,
                    rollback.nextTransactionSequence,
                    rollbackNextLotSequence,
                    rollback.runtimeRevision,
                    rollbackLastShelfLifeDay
                );
            }

            return false;
        }

        if (notify)
        {
            foreach (KeyValuePair<string, StockState> pair
                     in stockByIngredientId)
            {
                StockChanged?.Invoke(pair.Value.ToSnapshot(CurrentDayIndex, lotsById.Values));
            }

            foreach (KeyValuePair<string, LotState> pair in lotsById)
            {
                LotChanged?.Invoke(pair.Value.ToSnapshot(CurrentDayIndex));
            }

            foreach (KeyValuePair<string, ReservationState> pair
                     in reservationsById)
            {
                ReservationChanged?.Invoke(pair.Value.ToSnapshot());
            }
        }

        return true;
    }

    private bool TryBuildRuntimeFromSnapshot(
        BistroBuilderInventoryRuntimeSnapshot snapshot,
        out Dictionary<string, StockState> candidateStock,
        out Dictionary<string, LotState> candidateLots,
        out Dictionary<string, ReservationState> candidateReservations,
        out Dictionary<string, OperationRecord> candidateOperations,
        out List<BistroBuilderInventoryTransactionSnapshot> candidateLedger,
        out long candidateNextLotSequence,
        out int candidateLastShelfLifeDay,
        out string error
    )
    {
        candidateStock = new Dictionary<string, StockState>(
            StringComparer.Ordinal
        );
        candidateLots = new Dictionary<string, LotState>(
            StringComparer.Ordinal
        );
        candidateReservations = new Dictionary<string, ReservationState>(
            StringComparer.Ordinal
        );
        candidateOperations = new Dictionary<string, OperationRecord>(
            StringComparer.Ordinal
        );
        candidateLedger =
            new List<BistroBuilderInventoryTransactionSnapshot>(
                snapshot.ledger.Count
            );
        candidateNextLotSequence = snapshot.nextLotSequence;
        candidateLastShelfLifeDay = snapshot.lastShelfLifeProcessedDayIndex;
        error = string.Empty;

        if (!snapshot.requiresLotMaterialization &&
            candidateLastShelfLifeDay > CurrentDayIndex)
        {
            error = "El inventario guardado afirma haber procesado un día " +
                    "posterior al calendario de la partida.";
            return false;
        }

        BistroBuilderIngredientCatalog catalog =
            recipeCatalogService.IngredientCatalog;
        var savedStockById =
            new Dictionary<string, BistroBuilderInventoryStockSaveRecord>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < snapshot.stock.Count; index++)
        {
            BistroBuilderInventoryStockSaveRecord record = snapshot.stock[index];

            if (!catalog.TryGetDefinition(
                    record.ingredientId,
                    out BistroBuilderIngredientDefinition definition
                ) ||
                definition == null)
            {
                error = "La partida contiene el ingrediente desconocido " +
                        record.ingredientId + ".";
                return false;
            }

            savedStockById.Add(record.ingredientId, record);
        }

        IReadOnlyList<BistroBuilderIngredientDefinition> definitions =
            catalog.Definitions;

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderIngredientDefinition definition = definitions[index];

            if (definition == null)
            {
                error = "El catálogo actual contiene un ingrediente nulo.";
                return false;
            }

            var state = new StockState(
                definition,
                BistroBuilderInventoryStorageLocationIds
                    .FromIngredientStorage(definition.StorageType)
            );

            if (savedStockById.TryGetValue(
                    definition.IngredientId,
                    out BistroBuilderInventoryStockSaveRecord record
                ))
            {
                state.StorageLocationId = record.storageLocationId;
                state.OnHand = record.onHandCanonicalMilliUnits;
                state.Reserved = record.reservedCanonicalMilliUnits;
                state.Consumed = record.consumedCanonicalMilliUnits;
                state.Wasted = record.wastedCanonicalMilliUnits;
                state.Expired = record.expiredCanonicalMilliUnits;
                state.Revision = record.revision;
            }

            candidateStock.Add(definition.IngredientId, state);
        }

        if (snapshot.requiresLotMaterialization)
        {
            // La migración v1->v2 no conoce el catálogo ni el calendario.
            // Materializamos aquí un lote por ingrediente existente, fechado
            // en el día cargado, conservando exactamente balances y reservas.
            long localSequence = 1L;
            var ingredientIds = new List<string>(candidateStock.Keys);
            ingredientIds.Sort(StringComparer.Ordinal);
            int receivedDay = CurrentDayIndex;

            for (int index = 0; index < ingredientIds.Count; index++)
            {
                StockState state = candidateStock[ingredientIds[index]];
                if (state.OnHand <= 0L)
                {
                    continue;
                }

                int shelfLife = Math.Max(
                    0,
                    state.Definition.DefaultShelfLifeDays
                );
                int expiration = 0;
                if (shelfLife > 0)
                {
                    long candidateExpiration = (long)receivedDay + shelfLife;
                    expiration = candidateExpiration <= int.MaxValue
                        ? (int)candidateExpiration
                        : int.MaxValue;
                }

                string lotId = "inventory_lot_" +
                               localSequence.ToString("D8");
                localSequence++;
                var lot = new LotState(
                    lotId,
                    state.IngredientId,
                    "inventory_migration_v1",
                    receivedDay,
                    expiration,
                    shelfLife
                )
                {
                    OnHand = state.OnHand,
                    Reserved = state.Reserved,
                    Revision = state.Revision
                };
                candidateLots.Add(lotId, lot);
            }

            candidateNextLotSequence = Math.Max(1L, localSequence);
            candidateLastShelfLifeDay = receivedDay;
        }
        else
        {
            for (int index = 0; index < snapshot.lots.Count; index++)
            {
                BistroBuilderInventoryLotSaveRecord record = snapshot.lots[index];
                if (!candidateStock.TryGetValue(
                        record.ingredientId,
                        out StockState state
                    ))
                {
                    error = "El lote " + record.lotId +
                            " referencia un ingrediente desconocido.";
                    return false;
                }

                if (record.receivedDayIndex > CurrentDayIndex)
                {
                    error = "El lote " + record.lotId +
                            " tiene una recepción posterior al calendario " +
                            "de la partida.";
                    return false;
                }

                int shelfLife = record.expirationDayIndex > 0
                    ? Math.Max(
                        1,
                        record.expirationDayIndex - record.receivedDayIndex
                    )
                    : 0;
                var lot = new LotState(
                    record.lotId,
                    record.ingredientId,
                    record.sourceId,
                    record.receivedDayIndex,
                    record.expirationDayIndex,
                    shelfLife
                )
                {
                    OnHand = record.onHandCanonicalMilliUnits,
                    Reserved = record.reservedCanonicalMilliUnits,
                    Revision = record.revision
                };
                candidateLots.Add(record.lotId, lot);
            }
        }

        for (int index = 0; index < snapshot.reservations.Count; index++)
        {
            BistroBuilderInventoryReservationSaveRecord record =
                snapshot.reservations[index];
            var lines = new List<ReservationLineState>(record.lines.Count);
            bool active = (BistroBuilderInventoryReservationStatus)
                record.status == BistroBuilderInventoryReservationStatus.Active;

            for (int lineIndex = 0;
                 lineIndex < record.lines.Count;
                 lineIndex++)
            {
                BistroBuilderInventoryReservationLineSaveRecord line =
                    record.lines[lineIndex];

                if (!candidateStock.ContainsKey(line.ingredientId))
                {
                    error = "La reserva " + record.reservationId +
                            " referencia el ingrediente desconocido " +
                            line.ingredientId + ".";
                    return false;
                }

                var allocations = new List<LotAllocationState>();
                if (snapshot.requiresLotMaterialization && active)
                {
                    LotState migratedLot = null;
                    foreach (LotState candidate in candidateLots.Values)
                    {
                        if (string.Equals(
                                candidate.IngredientId,
                                line.ingredientId,
                                StringComparison.Ordinal
                            ))
                        {
                            migratedLot = candidate;
                            break;
                        }
                    }

                    if (migratedLot == null ||
                        migratedLot.Reserved < line.canonicalMilliUnits)
                    {
                        error = "No se pudo materializar la reserva " +
                                record.reservationId + " en lotes.";
                        return false;
                    }

                    allocations.Add(
                        new LotAllocationState(
                            migratedLot.LotId,
                            line.canonicalMilliUnits
                        )
                    );
                }
                else if (line.lotAllocations != null)
                {
                    for (int allocationIndex = 0;
                         allocationIndex < line.lotAllocations.Count;
                         allocationIndex++)
                    {
                        BistroBuilderInventoryLotAllocationSaveRecord allocation =
                            line.lotAllocations[allocationIndex];
                        if (!candidateLots.TryGetValue(
                                allocation.lotId,
                                out LotState lot
                            ) ||
                            !string.Equals(
                                lot.IngredientId,
                                line.ingredientId,
                                StringComparison.Ordinal
                            ))
                        {
                            error = "La reserva " + record.reservationId +
                                    " contiene una asignación FEFO inválida.";
                            return false;
                        }

                        allocations.Add(
                            new LotAllocationState(
                                allocation.lotId,
                                allocation.canonicalMilliUnits
                            )
                        );
                    }
                }

                lines.Add(
                    new ReservationLineState(
                        line.ingredientId,
                        line.canonicalMilliUnits,
                        allocations
                    )
                );
            }

            var reservation = new ReservationState(
                record.reservationId,
                record.sourceId,
                lines
            )
            {
                Status =
                    (BistroBuilderInventoryReservationStatus)record.status,
                Revision = record.revision
            };

            candidateReservations.Add(record.reservationId, reservation);
        }

        for (int index = 0; index < snapshot.operations.Count; index++)
        {
            BistroBuilderInventoryOperationSaveRecord record =
                snapshot.operations[index];
            candidateOperations.Add(
                record.operationId,
                new OperationRecord(record.fingerprint)
            );
        }

        for (int index = 0; index < snapshot.ledger.Count; index++)
        {
            BistroBuilderInventoryTransactionSaveRecord record =
                snapshot.ledger[index];

            if (!candidateStock.ContainsKey(record.ingredientId))
            {
                error = "El libro de inventario referencia el ingrediente " +
                        "desconocido " + record.ingredientId + ".";
                return false;
            }

            candidateLedger.Add(record.ToSnapshot());
        }

        return TryValidateCandidateRuntime(
            candidateStock,
            candidateLots,
            candidateReservations,
            candidateLedger,
            snapshot.nextTransactionSequence,
            candidateNextLotSequence,
            candidateLastShelfLifeDay,
            out error
        );
    }

    private static bool TryValidateCandidateRuntime(
        Dictionary<string, StockState> candidateStock,
        Dictionary<string, LotState> candidateLots,
        Dictionary<string, ReservationState> candidateReservations,
        List<BistroBuilderInventoryTransactionSnapshot> candidateLedger,
        long candidateNextSequence,
        long candidateNextLotSequence,
        int candidateLastShelfLifeDay,
        out string error
    )
    {
        error = string.Empty;
        if (candidateNextLotSequence < 1L || candidateLastShelfLifeDay < 0)
        {
            error = "Las secuencias de lotes restauradas son inválidas.";
            return false;
        }

        var reservedByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var allocatedByLot = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ReservationState> pair
                 in candidateReservations)
        {
            ReservationState reservation = pair.Value;
            if (reservation == null)
            {
                error = "El snapshot contiene una reserva nula.";
                return false;
            }

            bool active = reservation.Status ==
                BistroBuilderInventoryReservationStatus.Active;
            for (int index = 0; index < reservation.Lines.Count; index++)
            {
                ReservationLineState line = reservation.Lines[index];
                if (!candidateStock.ContainsKey(line.IngredientId) ||
                    line.Quantity <= 0L)
                {
                    error = "Una reserva restaurada contiene una línea inválida.";
                    return false;
                }

                if (!active)
                {
                    continue;
                }

                reservedByIngredient.TryGetValue(
                    line.IngredientId,
                    out long current
                );
                try
                {
                    reservedByIngredient[line.IngredientId] =
                        checked(current + line.Quantity);
                }
                catch (OverflowException)
                {
                    error = "Las reservas persistidas desbordan el rango.";
                    return false;
                }

                long allocatedLine = 0L;
                for (int allocationIndex = 0;
                     allocationIndex < line.LotAllocations.Count;
                     allocationIndex++)
                {
                    LotAllocationState allocation =
                        line.LotAllocations[allocationIndex];
                    if (allocation == null || allocation.Quantity <= 0L ||
                        !candidateLots.TryGetValue(
                            allocation.LotId,
                            out LotState lot
                        ) ||
                        !string.Equals(
                            lot.IngredientId,
                            line.IngredientId,
                            StringComparison.Ordinal
                        ))
                    {
                        error = "Una reserva restaurada contiene una " +
                                "asignación FEFO inválida.";
                        return false;
                    }

                    try
                    {
                        allocatedLine = checked(
                            allocatedLine + allocation.Quantity
                        );
                        allocatedByLot.TryGetValue(
                            allocation.LotId,
                            out long allocated
                        );
                        allocatedByLot[allocation.LotId] = checked(
                            allocated + allocation.Quantity
                        );
                    }
                    catch (OverflowException)
                    {
                        error = "Las asignaciones FEFO desbordan el rango.";
                        return false;
                    }
                }

                if (allocatedLine != line.Quantity)
                {
                    error = "Una reserva activa no coincide con sus lotes.";
                    return false;
                }
            }
        }

        var lotOnHandByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );
        var lotReservedByIngredient = new Dictionary<string, long>(
            StringComparer.Ordinal
        );

        foreach (KeyValuePair<string, LotState> pair in candidateLots)
        {
            LotState lot = pair.Value;
            if (lot == null ||
                !string.Equals(pair.Key, lot.LotId, StringComparison.Ordinal) ||
                !candidateStock.ContainsKey(lot.IngredientId) ||
                lot.ReceivedDayIndex < 1 ||
                (lot.ExpirationDayIndex != 0 &&
                 lot.ExpirationDayIndex <= lot.ReceivedDayIndex) ||
                (lot.ExpirationDayIndex > 0 &&
                 lot.ExpirationDayIndex <= candidateLastShelfLifeDay &&
                 lot.Available > 0L) ||
                lot.OnHand < 0L || lot.Reserved < 0L ||
                lot.Reserved > lot.OnHand || lot.Revision < 0L)
            {
                error = "El lote restaurado " + pair.Key + " es inválido.";
                return false;
            }

            allocatedByLot.TryGetValue(pair.Key, out long expectedReserved);
            if (lot.Reserved != expectedReserved)
            {
                error = "El lote " + pair.Key +
                        " no coincide con sus reservas activas.";
                return false;
            }

            try
            {
                lotOnHandByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long onHand
                );
                lotReservedByIngredient.TryGetValue(
                    lot.IngredientId,
                    out long reserved
                );
                lotOnHandByIngredient[lot.IngredientId] =
                    checked(onHand + lot.OnHand);
                lotReservedByIngredient[lot.IngredientId] =
                    checked(reserved + lot.Reserved);
            }
            catch (OverflowException)
            {
                error = "Los lotes restaurados desbordan el rango.";
                return false;
            }
        }

        foreach (KeyValuePair<string, StockState> pair in candidateStock)
        {
            StockState state = pair.Value;
            reservedByIngredient.TryGetValue(pair.Key, out long expected);
            lotOnHandByIngredient.TryGetValue(pair.Key, out long lotOnHand);
            lotReservedByIngredient.TryGetValue(pair.Key, out long lotReserved);

            if (state == null || state.OnHand < 0L || state.Reserved < 0L ||
                state.Reserved > state.OnHand || state.Reserved != expected ||
                state.OnHand != lotOnHand || state.Reserved != lotReserved ||
                state.Consumed < 0L || state.Wasted < 0L ||
                state.Expired < 0L || state.Revision < 0L)
            {
                error = "El balance restaurado de " + pair.Key +
                        " no coincide con sus lotes y reservas.";
                return false;
            }
        }

        if (candidateNextSequence != candidateLedger.Count + 1L)
        {
            error = "La secuencia restaurada del libro es inválida.";
            return false;
        }

        return true;
    }

    private void ApplyRuntimeReplacement(
        Dictionary<string, StockState> candidateStock,
        Dictionary<string, LotState> candidateLots,
        Dictionary<string, ReservationState> candidateReservations,
        Dictionary<string, OperationRecord> candidateOperations,
        List<BistroBuilderInventoryTransactionSnapshot> candidateLedger,
        long candidateNextSequence,
        long candidateNextLotSequence,
        long candidateRevision,
        int candidateLastShelfLifeDay
    )
    {
        stockByIngredientId.Clear();
        lotsById.Clear();
        reservationsById.Clear();
        operationsById.Clear();
        ledger.Clear();

        foreach (KeyValuePair<string, StockState> pair in candidateStock)
        {
            stockByIngredientId.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, LotState> pair in candidateLots)
        {
            lotsById.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, ReservationState> pair
                 in candidateReservations)
        {
            reservationsById.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, OperationRecord> pair
                 in candidateOperations)
        {
            operationsById.Add(pair.Key, pair.Value);
        }

        ledger.AddRange(candidateLedger);
        nextTransactionSequence = candidateNextSequence;
        nextLotSequence = candidateNextLotSequence;
        runtimeRevision = candidateRevision;
        lastShelfLifeProcessedDayIndex = candidateLastShelfLifeDay;
        initialized = true;
        enabled = true;
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
        // Estos acumulados son una caché estrictamente auditada contra los
        // lotes. Los lotes son la fuente física primaria desde 2.2A.
        public long OnHand;
        public long Reserved;
        public long Consumed;
        public long Wasted;
        public long Expired;
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

        public BistroBuilderInventoryStockSnapshot ToSnapshot(
            int currentDayIndex,
            IEnumerable<LotState> lots
        )
        {
            int nextExpirationDay = 0;
            BistroBuilderInventoryFreshnessState freshness =
                BistroBuilderInventoryFreshnessState.Fresh;
            bool foundUsable = false;

            if (lots != null)
            {
                foreach (LotState lot in lots)
                {
                    if (lot == null ||
                        !string.Equals(
                            lot.IngredientId,
                            IngredientId,
                            StringComparison.Ordinal
                        ) ||
                        lot.Available <= 0L)
                    {
                        // La frescura agregada describe stock utilizable.
                        // Las cantidades ya comprometidas en reservas activas
                        // no deben convertir visualmente todo el ingrediente
                        // en "caducado" mientras se termina ese compromiso.
                        continue;
                    }

                    BistroBuilderInventoryFreshnessState lotFreshness =
                        lot.GetFreshnessState(currentDayIndex);
                    if (!foundUsable || lotFreshness > freshness)
                    {
                        freshness = lotFreshness;
                    }
                    foundUsable = true;

                    if (lot.ExpirationDayIndex > currentDayIndex &&
                        (nextExpirationDay == 0 ||
                         lot.ExpirationDayIndex < nextExpirationDay))
                    {
                        nextExpirationDay = lot.ExpirationDayIndex;
                    }
                }
            }

            if (!foundUsable)
            {
                freshness = BistroBuilderInventoryFreshnessState.Fresh;
            }

            return new BistroBuilderInventoryStockSnapshot(
                IngredientId,
                StorageLocationId,
                Definition.BaseUnit,
                OnHand,
                Reserved,
                Consumed,
                Wasted,
                Expired,
                nextExpirationDay,
                freshness,
                Revision
            );
        }
    }

    private sealed class LotAllocationState
    {
        public string LotId { get; set; }
        public long Quantity { get; }

        public LotAllocationState(string lotId, long quantity)
        {
            LotId = lotId;
            Quantity = quantity;
        }
    }

    private sealed class ReservationLineState
    {
        public string IngredientId { get; }
        public long Quantity { get; }
        public List<LotAllocationState> LotAllocations { get; }

        public ReservationLineState(
            string ingredientId,
            long quantity,
            List<LotAllocationState> lotAllocations = null
        )
        {
            IngredientId = ingredientId;
            Quantity = quantity;
            LotAllocations = lotAllocations != null
                ? lotAllocations
                : new List<LotAllocationState>();
        }
    }

    private sealed class LotState
    {
        public string LotId { get; }
        public string IngredientId { get; }
        public string SourceId { get; }
        public int ReceivedDayIndex { get; }
        public int ExpirationDayIndex { get; }
        public int ShelfLifeDays { get; }
        public long OnHand;
        public long Reserved;
        public long Revision;
        public long Available => OnHand - Reserved;

        public LotState(
            string lotId,
            string ingredientId,
            string sourceId,
            int receivedDayIndex,
            int expirationDayIndex,
            int shelfLifeDays
        )
        {
            LotId = lotId;
            IngredientId = ingredientId;
            SourceId = sourceId;
            ReceivedDayIndex = receivedDayIndex;
            ExpirationDayIndex = expirationDayIndex;
            ShelfLifeDays = shelfLifeDays;
        }

        public bool IsExpired(int currentDayIndex)
        {
            return ExpirationDayIndex > 0 &&
                   currentDayIndex >= ExpirationDayIndex;
        }

        public BistroBuilderInventoryFreshnessState GetFreshnessState(
            int currentDayIndex
        )
        {
            if (IsExpired(currentDayIndex))
            {
                return BistroBuilderInventoryFreshnessState.Expired;
            }

            if (ExpirationDayIndex <= 0 || ShelfLifeDays <= 0)
            {
                return BistroBuilderInventoryFreshnessState.Fresh;
            }

            int remaining = ExpirationDayIndex - currentDayIndex;
            int nearExpiryDays = Math.Max(1,
                (int)Math.Ceiling(ShelfLifeDays * 0.15d));
            if (remaining <= nearExpiryDays)
            {
                return BistroBuilderInventoryFreshnessState.NearExpiry;
            }

            int age = Math.Max(0, currentDayIndex - ReceivedDayIndex);
            double ageRatio = (double)age / ShelfLifeDays;
            if (ageRatio >= 0.65d)
            {
                return BistroBuilderInventoryFreshnessState.Aging;
            }

            if (ageRatio >= 0.30d)
            {
                return BistroBuilderInventoryFreshnessState.Good;
            }

            return BistroBuilderInventoryFreshnessState.Fresh;
        }

        public BistroBuilderInventoryLotSnapshot ToSnapshot(
            int currentDayIndex
        )
        {
            return new BistroBuilderInventoryLotSnapshot(
                LotId,
                IngredientId,
                SourceId,
                ReceivedDayIndex,
                ExpirationDayIndex,
                OnHand,
                Reserved,
                GetFreshnessState(currentDayIndex),
                Revision
            );
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
                ReservationLineState line = Lines[index];
                var allocations =
                    new List<BistroBuilderInventoryLotAllocationSnapshot>(
                        line.LotAllocations.Count
                    );
                for (int allocationIndex = 0;
                     allocationIndex < line.LotAllocations.Count;
                     allocationIndex++)
                {
                    LotAllocationState allocation =
                        line.LotAllocations[allocationIndex];
                    allocations.Add(
                        new BistroBuilderInventoryLotAllocationSnapshot(
                            allocation.LotId,
                            allocation.Quantity
                        )
                    );
                }

                snapshots.Add(
                    new BistroBuilderInventoryReservationLineSnapshot(
                        line.IngredientId,
                        line.Quantity,
                        allocations
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

    private readonly struct PendingLotMutation
    {
        public LotState Lot { get; }
        public bool IsNew { get; }
        public long NewOnHand { get; }
        public long NewReserved { get; }

        public PendingLotMutation(
            LotState lot,
            bool isNew,
            long newOnHand,
            long newReserved
        )
        {
            Lot = lot;
            IsNew = isNew;
            NewOnHand = newOnHand;
            NewReserved = newReserved;
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
        public long NewExpired { get; }
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
            long newExpired,
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
            NewExpired = newExpired;
            OperationId = operationId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
