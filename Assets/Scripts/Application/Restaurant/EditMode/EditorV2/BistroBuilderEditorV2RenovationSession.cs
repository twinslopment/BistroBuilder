using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B5: common transactional renovation session for Editor V2.
///
/// Placement keeps using its real runtime authorities and Architecture keeps its
/// canonical Draft/commit path. This coordinator owns the transaction boundary:
/// baseline, pending summary, Apply and exact Discard.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Renovation Session")]
public sealed class BistroBuilderEditorV2RenovationSession :
    MonoBehaviour,
    IRestaurantEditModeExitGuard
{
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private BistroBuilderEditorV2Coordinator editorCoordinator;
    [SerializeField] private BistroBuilderEditorV2GlobalHistory globalHistory;
    [SerializeField] private RestaurantPlacementHistoryService placementHistory;
    [SerializeField] private RestaurantPlacementTransactionService placementTransaction;
    [SerializeField] private RestaurantPlaceableCreationService creationService;
    [SerializeField] private BistroBuilderEditRuntimeCoordinator architectureRuntime;
    [SerializeField] private BistroBuilderEditDocumentRuntimeService documentService;
    [SerializeField] private BistroBuilderArchitectureRuntimeMaterializer materializer;
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderPlaceableFinanceBridge placeableFinanceBridge;
    [SerializeField] private BistroBuilderEditFinanceGateway editFinanceGateway;
    [SerializeField, Min(64)] private int maximumSessionOperations = 2048;

    private BistroBuilderFinanceSnapshot baselineFinance;
    private string transactionId = string.Empty;
    private string lastError = string.Empty;
    private string lastTransition = "Inactive.";
    private bool active;
    private bool subscribed;
    private bool boundaryOperation;
    private int previousPlacementHistoryLimit = 50;
    private int previousGlobalHistoryLimit = 50;
    private bool limitsExpanded;

    public event Action<BistroBuilderEditorV2RenovationSnapshot> StateChanged;

    public bool IsActive => active;
    public string TransactionId => transactionId;
    public string LastError => lastError;
    public string LastTransition => lastTransition;

    public bool HasPendingChanges
    {
        get
        {
            if (!active)
                return false;

            bool historyDirty =
                (globalHistory != null && globalHistory.UndoCount > 0) ||
                (placementHistory != null && placementHistory.UndoCount > 0) ||
                (architectureRuntime != null && architectureRuntime.IsDirty);

            if (historyDirty)
                return true;

            if (baselineFinance == null || financeService == null)
                return false;

            return financeService.CurrentBalanceCents != baselineFinance.currentBalanceCents ||
                   financeService.TransactionCount !=
                       (baselineFinance.transactions != null ? baselineFinance.transactions.Count : 0);
        }
    }

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        RegisterExitGuard();

        if (editModeService != null && editModeService.IsEditModeActive)
            TryBeginSession(out _);
    }

    private void OnDisable()
    {
        if (active && HasPendingChanges)
            TryDiscardChangesInternal(false, out _);
        else
            CloseSessionBoundary();

        UnregisterExitGuard();
        Unsubscribe();
    }

    public void Configure(
        RestaurantEditModeService editMode,
        BistroBuilderEditorV2Coordinator coordinator,
        BistroBuilderEditorV2GlobalHistory history,
        RestaurantPlacementHistoryService placement,
        BistroBuilderEditRuntimeCoordinator architecture,
        BistroBuilderFinanceService finance)
    {
        UnregisterExitGuard();
        Unsubscribe();

        editModeService = editMode;
        editorCoordinator = coordinator;
        globalHistory = history;
        placementHistory = placement;
        architectureRuntime = architecture;
        financeService = finance;

        CacheDependencies();
        Subscribe();
        RegisterExitGuard();

        if (editModeService != null && editModeService.IsEditModeActive && !active)
            TryBeginSession(out _);
    }

    public bool TryBeginSession(out string error)
    {
        error = string.Empty;
        CacheDependencies();

        if (active)
            return true;

        if (editModeService == null || !editModeService.IsEditModeActive)
        {
            error = "B5 solo puede abrir una reforma con el modo edición activo.";
            return Fail(error);
        }

        if (!ValidateDependencies(out error))
            return Fail(error);

        if (architectureRuntime.HasSession)
        {
            if (architectureRuntime.IsDirty)
            {
                error =
                    "Existe un Draft arquitectónico anterior a la sesión B5. " +
                    "Debe resolverse antes de abrir una reforma común.";
                return Fail(error);
            }

            architectureRuntime.CancelSession();
        }

        ExpandHistoryLimits();
        placementHistory.ClearHistory();
        globalHistory.ClearGlobalOrdering();
        placeableFinanceBridge?.ResetTransientStateAtRenovationBoundary();

        baselineFinance = financeService.CreateSnapshot();
        if (baselineFinance == null)
        {
            RestoreHistoryLimits();
            error = "No se pudo capturar la baseline financiera de la reforma.";
            return Fail(error);
        }

        if (!architectureRuntime.TryBeginSession(out error))
        {
            baselineFinance = null;
            RestoreHistoryLimits();
            return Fail(error);
        }

        transactionId = "renovation:" + Guid.NewGuid().ToString("N");
        active = true;
        lastError = string.Empty;
        lastTransition = "Baseline de reforma capturada.";
        Publish();
        return true;
    }

    public bool TryApplyChanges(out string error)
    {
        error = string.Empty;
        if (!EnsureActive(out error))
            return false;

        if (!TryCancelProvisionalOperation(out error))
            return Fail(error);

        boundaryOperation = true;
        try
        {
            bool functionalChanges =
                (globalHistory != null && globalHistory.UndoCount > 0) ||
                (placementHistory != null && placementHistory.UndoCount > 0) ||
                (architectureRuntime != null && architectureRuntime.IsDirty);

            if (!functionalChanges)
            {
                if (!RestoreFinanceBaseline(out error))
                    return Fail(error);

                architectureRuntime?.CancelSession();
                placementHistory?.ClearHistory();
                globalHistory?.ClearGlobalOrdering();
                placeableFinanceBridge?.ResetTransientStateAtRenovationBoundary();
                lastTransition = "Reforma aplicada sin cambios netos.";
                return RebaseAfterBoundary(out error);
            }

            if (architectureRuntime != null && architectureRuntime.IsDirty)
            {
                if (!architectureRuntime.TryReview(out List<BistroBuilderEditDiagnostic> diagnostics))
                {
                    error = BuildDiagnosticSummary(diagnostics);
                    return Fail(error);
                }

                if (!architectureRuntime.TryCommit(out _, out diagnostics, out error))
                {
                    if (string.IsNullOrWhiteSpace(error))
                        error = BuildDiagnosticSummary(diagnostics);
                    return Fail(error);
                }
            }
            else if (architectureRuntime != null && architectureRuntime.HasSession)
            {
                architectureRuntime.CancelSession();
            }

            // Placement already mutated the live Draft through its canonical
            // services. Crossing Apply makes those mutations authoritative.
            placementHistory?.ClearHistory();
            globalHistory?.ClearGlobalOrdering();
            placeableFinanceBridge?.ResetTransientStateAtRenovationBoundary();

            RebuildCommittedArchitecture();
            lastTransition = "Reforma aplicada como una versión coherente.";
            lastError = string.Empty;
            return RebaseAfterBoundary(out error);
        }
        finally
        {
            boundaryOperation = false;
        }
    }

    public bool TryDiscardChanges(out string error)
    {
        return TryDiscardChangesInternal(true, out error);
    }

    private bool TryDiscardChangesInternal(bool rebase, out string error)
    {
        error = string.Empty;
        if (!active)
            return true;

        if (!TryCancelProvisionalOperation(out error))
            return Fail(error);

        boundaryOperation = true;
        try
        {
            int safety = Mathf.Max(64, maximumSessionOperations + 8);
            int undone = 0;

            while (globalHistory != null && globalHistory.CanUndo)
            {
                if (undone++ >= safety)
                {
                    error = "B5 abortó Discard por superar el límite seguro de historial.";
                    return Fail(error);
                }

                if (!globalHistory.TryUndo(out error))
                    return Fail("Discard no pudo revertir la cronología global: " + error);
            }

            if (placementHistory != null && placementHistory.UndoCount != 0)
            {
                error = "Discard detectó historial de Placement fuera de la cronología B5.";
                return Fail(error);
            }

            if (architectureRuntime != null && architectureRuntime.IsDirty)
            {
                error = "Discard no alcanzó exactamente la baseline arquitectónica.";
                return Fail(error);
            }

            if (architectureRuntime != null && architectureRuntime.HasSession)
                architectureRuntime.CancelSession();

            placementHistory?.ClearHistory();
            globalHistory?.ClearGlobalOrdering();

            if (!RestoreFinanceBaseline(out error))
                return Fail(error);

            placeableFinanceBridge?.ResetTransientStateAtRenovationBoundary();
            RebuildCommittedArchitecture();

            lastTransition = "Reforma descartada; baseline restaurada exactamente.";
            lastError = string.Empty;

            if (rebase && editModeService != null && editModeService.IsEditModeActive)
                return RebaseAfterBoundary(out error);

            CloseSessionBoundary();
            Publish();
            return true;
        }
        finally
        {
            boundaryOperation = false;
        }
    }

    public bool TryGetSnapshot(
        out BistroBuilderEditorV2RenovationSnapshot snapshot,
        out string error)
    {
        error = string.Empty;
        snapshot = BuildSnapshot();

        if (!active || baselineFinance == null || financeService == null)
            return true;

        long placementSignedCost;
        try
        {
            placementSignedCost = checked(
                baselineFinance.currentBalanceCents -
                financeService.CurrentBalanceCents);
        }
        catch (OverflowException)
        {
            error = "El coste provisional de Placement queda fuera de rango.";
            return false;
        }

        long architectureSignedCost = 0L;
        if (architectureRuntime != null &&
            architectureRuntime.HasSession &&
            architectureRuntime.IsDirty)
        {
            if (editFinanceGateway == null)
            {
                error = "No está disponible la pasarela financiera de reformas.";
                return false;
            }

            BistroBuilderEditEconomicProposal proposal =
                BistroBuilderEditEconomicProposalBuilder.Build(architectureRuntime.Session);

            if (!editFinanceGateway.TryQuoteProposal(
                    proposal,
                    out long creditCents,
                    out long debitCents,
                    out error))
            {
                return false;
            }

            try
            {
                architectureSignedCost = checked(debitCents - creditCents);
            }
            catch (OverflowException)
            {
                error = "El coste arquitectónico provisional queda fuera de rango.";
                return false;
            }
        }

        snapshot.placementSignedCostCents = placementSignedCost;
        snapshot.architectureSignedCostCents = architectureSignedCost;
        try
        {
            snapshot.totalSignedCostCents =
                checked(placementSignedCost + architectureSignedCost);
        }
        catch (OverflowException)
        {
            error = "El coste total de la reforma queda fuera de rango.";
            return false;
        }

        return true;
    }

    public bool CanExitEditMode(out string rejectionMessage)
    {
        rejectionMessage = string.Empty;

        if (!active || boundaryOperation || !HasPendingChanges)
            return true;

        int count = globalHistory != null ? globalHistory.UndoCount : 0;
        rejectionMessage =
            count > 0
                ? "Hay " + count +
                  " cambio(s) de reforma pendientes. Pulsa Aplicar cambios o Descartar."
                : "Hay cambios de reforma pendientes. Pulsa Aplicar cambios o Descartar.";
        return false;
    }

    private bool RebaseAfterBoundary(out string error)
    {
        error = string.Empty;

        baselineFinance = financeService != null
            ? financeService.CreateSnapshot()
            : null;
        if (baselineFinance == null)
        {
            CloseSessionBoundary();
            error =
                "Los cambios se resolvieron, pero no se pudo abrir una nueva baseline financiera.";
            lastError = error;
            Publish();
            return true;
        }

        if (architectureRuntime != null && architectureRuntime.HasSession)
            architectureRuntime.CancelSession();

        string beginError = string.Empty;
        if (architectureRuntime == null ||
            !architectureRuntime.TryBeginSession(out beginError))
        {
            CloseSessionBoundary();
            lastError =
                "Los cambios se resolvieron, pero no se pudo abrir una nueva sesión: " +
                beginError;
            Publish();
            return true;
        }

        placementHistory?.ClearHistory();
        globalHistory?.ClearGlobalOrdering();
        transactionId = "renovation:" + Guid.NewGuid().ToString("N");
        active = true;
        Publish();
        return true;
    }

    private bool RestoreFinanceBaseline(out string error)
    {
        error = string.Empty;
        if (baselineFinance == null)
            return true;
        if (financeService == null)
        {
            error = "Falta la autoridad financiera para restaurar la baseline.";
            return false;
        }

        return financeService.TryRestoreSnapshot(baselineFinance, out error);
    }

    private bool TryCancelProvisionalOperation(out string error)
    {
        error = string.Empty;

        if (editorCoordinator != null &&
            !editorCoordinator.TryCancelActiveOperation(out error))
            return false;

        if (creationService != null && creationService.HasActiveCreation)
        {
            if (!creationService.TryCancelActiveCreation(out RestaurantPlaceableCreationResult result))
            {
                error = result.Message;
                return false;
            }
        }

        if (placementTransaction != null && placementTransaction.HasActiveTransaction)
        {
            if (!placementTransaction.CancelPlacement())
            {
                error = "No se pudo cancelar la colocación provisional.";
                return false;
            }
        }

        return true;
    }

    private void RebuildCommittedArchitecture()
    {
        if (materializer != null && documentService != null)
            materializer.Rebuild(documentService.GetCommittedSnapshot());
    }

    private bool EnsureActive(out string error)
    {
        if (active)
        {
            error = string.Empty;
            return true;
        }

        return TryBeginSession(out error);
    }

    private void HandleEditModeEntered()
    {
        TryBeginSession(out _);
    }

    private void HandleEditModeExited()
    {
        if (boundaryOperation)
            return;

        if (active && HasPendingChanges)
        {
            TryDiscardChangesInternal(false, out _);
            return;
        }

        if (architectureRuntime != null && architectureRuntime.HasSession)
            architectureRuntime.CancelSession();

        CloseSessionBoundary();
        Publish();
    }

    private void HandleDependencyChanged()
    {
        if (active)
        {
            lastTransition = HasPendingChanges
                ? "Draft de reforma modificado."
                : "Draft de reforma coincide con la baseline.";
            Publish();
        }
    }

    private void HandleFinanceTransaction(BistroBuilderFinanceTransactionRecord _)
    {
        HandleDependencyChanged();
    }

    private void HandleFinanceRestored()
    {
        HandleDependencyChanged();
    }

    private void ExpandHistoryLimits()
    {
        if (limitsExpanded)
            return;

        if (placementHistory != null)
            previousPlacementHistoryLimit =
                placementHistory.SetMaximumHistoryEntriesRuntime(maximumSessionOperations);
        if (globalHistory != null)
            previousGlobalHistoryLimit =
                globalHistory.SetMaximumEntriesRuntime(maximumSessionOperations);

        limitsExpanded = true;
    }

    private void RestoreHistoryLimits()
    {
        if (!limitsExpanded)
            return;

        if (placementHistory != null)
            placementHistory.SetMaximumHistoryEntriesRuntime(previousPlacementHistoryLimit);
        if (globalHistory != null)
            globalHistory.SetMaximumEntriesRuntime(previousGlobalHistoryLimit);

        limitsExpanded = false;
    }

    private void CloseSessionBoundary()
    {
        active = false;
        transactionId = string.Empty;
        baselineFinance = null;
        RestoreHistoryLimits();
    }

    private bool ValidateDependencies(out string error)
    {
        if (editModeService == null ||
            globalHistory == null ||
            placementHistory == null ||
            architectureRuntime == null ||
            documentService == null ||
            financeService == null)
        {
            error =
                "B5 necesita EditMode, historial global, Placement, " +
                "Architecture, documento canónico y Finanzas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void CacheDependencies()
    {
        if (editModeService == null)
            editModeService = FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
        if (editorCoordinator == null)
            editorCoordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include);
        if (globalHistory == null)
            globalHistory = FindFirstObjectByType<BistroBuilderEditorV2GlobalHistory>(
                FindObjectsInactive.Include);
        if (placementHistory == null)
            placementHistory = FindFirstObjectByType<RestaurantPlacementHistoryService>(
                FindObjectsInactive.Include);
        if (placementTransaction == null)
            placementTransaction = FindFirstObjectByType<RestaurantPlacementTransactionService>(
                FindObjectsInactive.Include);
        if (creationService == null)
            creationService = FindFirstObjectByType<RestaurantPlaceableCreationService>(
                FindObjectsInactive.Include);
        if (architectureRuntime == null)
            architectureRuntime = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>(
                FindObjectsInactive.Include);
        if (documentService == null)
            documentService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>(
                FindObjectsInactive.Include);
        if (materializer == null)
            materializer = FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>(
                FindObjectsInactive.Include);
        if (financeService == null)
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>(
                FindObjectsInactive.Include);
        if (placeableFinanceBridge == null)
            placeableFinanceBridge = FindFirstObjectByType<BistroBuilderPlaceableFinanceBridge>(
                FindObjectsInactive.Include);
        if (editFinanceGateway == null)
            editFinanceGateway = FindFirstObjectByType<BistroBuilderEditFinanceGateway>(
                FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (editModeService != null)
        {
            editModeService.EditModeEntered += HandleEditModeEntered;
            editModeService.EditModeExited += HandleEditModeExited;
        }

        if (globalHistory != null)
            globalHistory.HistoryChanged += HandleDependencyChanged;
        if (architectureRuntime != null)
            architectureRuntime.SessionChanged += HandleDependencyChanged;
        if (financeService != null)
        {
            financeService.TransactionPosted += HandleFinanceTransaction;
            financeService.StateRestored += HandleFinanceRestored;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (editModeService != null)
        {
            editModeService.EditModeEntered -= HandleEditModeEntered;
            editModeService.EditModeExited -= HandleEditModeExited;
        }

        if (globalHistory != null)
            globalHistory.HistoryChanged -= HandleDependencyChanged;
        if (architectureRuntime != null)
            architectureRuntime.SessionChanged -= HandleDependencyChanged;
        if (financeService != null)
        {
            financeService.TransactionPosted -= HandleFinanceTransaction;
            financeService.StateRestored -= HandleFinanceRestored;
        }

        subscribed = false;
    }

    private void RegisterExitGuard()
    {
        editModeService?.RegisterExitGuard(this);
    }

    private void UnregisterExitGuard()
    {
        editModeService?.UnregisterExitGuard(this);
    }

    private bool Fail(string error)
    {
        lastError = string.IsNullOrWhiteSpace(error)
            ? "B5 rechazó la operación transaccional."
            : error;
        lastTransition = "Operación B5 rechazada.";
        Publish();
        return false;
    }

    private BistroBuilderEditorV2RenovationSnapshot BuildSnapshot()
    {
        int baselineTransactions =
            baselineFinance != null && baselineFinance.transactions != null
                ? baselineFinance.transactions.Count
                : 0;

        return new BistroBuilderEditorV2RenovationSnapshot
        {
            transactionId = transactionId ?? string.Empty,
            isActive = active,
            hasPendingChanges = HasPendingChanges,
            operationCount = globalHistory != null ? globalHistory.UndoCount : 0,
            baselineBalanceCents =
                baselineFinance != null ? baselineFinance.currentBalanceCents : 0L,
            currentBalanceCents =
                financeService != null ? financeService.CurrentBalanceCents : 0L,
            baselineTransactionCount = baselineTransactions,
            currentTransactionCount =
                financeService != null ? financeService.TransactionCount : 0,
            lastTransition = lastTransition ?? string.Empty,
            lastError = lastError ?? string.Empty
        };
    }

    private void Publish()
    {
        StateChanged?.Invoke(BuildSnapshot());
    }

    private static string BuildDiagnosticSummary(
        IReadOnlyList<BistroBuilderEditDiagnostic> diagnostics)
    {
        if (diagnostics == null || diagnostics.Count == 0)
            return "La reforma contiene bloqueantes.";

        for (int i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i] != null &&
                diagnostics[i].severity == BistroBuilderEditDiagnosticSeverity.Blocking)
            {
                return diagnostics[i].message;
            }
        }

        return diagnostics[0] != null
            ? diagnostics[0].message
            : "La reforma contiene bloqueantes.";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumSessionOperations = Mathf.Max(64, maximumSessionOperations);
    }
#endif
}

[Serializable]
public struct BistroBuilderEditorV2RenovationSnapshot
{
    public string transactionId;
    public bool isActive;
    public bool hasPendingChanges;
    public int operationCount;
    public long baselineBalanceCents;
    public long currentBalanceCents;
    public int baselineTransactionCount;
    public int currentTransactionCount;
    public long placementSignedCostCents;
    public long architectureSignedCostCents;
    public long totalSignedCostCents;
    public string lastTransition;
    public string lastError;
}
