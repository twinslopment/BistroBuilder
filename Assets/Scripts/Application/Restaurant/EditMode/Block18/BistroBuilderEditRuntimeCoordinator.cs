using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Edit Runtime Coordinator")]
public sealed class BistroBuilderEditRuntimeCoordinator : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditDocumentRuntimeService documentService;
    [SerializeField] private RestaurantEditModeService editModeService;
    [Tooltip("Autoridad externa que implementa IRestaurantEditModeAvailabilityRule.")]
    [SerializeField] private MonoBehaviour availabilityRuleSource;

    private readonly BistroBuilderEditFeedbackEventHub feedback = new BistroBuilderEditFeedbackEventHub();
    private BistroBuilderEditSession session;
    private IBistroBuilderEditEconomicGateway economyGateway;

    public event Action SessionChanged;
    public event Action<IReadOnlyList<BistroBuilderEditDiagnostic>> DiagnosticsChanged;
    public event Action<BistroBuilderEditDocument> CommitCompleted;

    public BistroBuilderEditFeedbackEventHub Feedback => feedback;
    public BistroBuilderEditSession Session => session;
    public bool HasSession => session != null &&
        session.State != BistroBuilderEditSessionState.Committed &&
        session.State != BistroBuilderEditSessionState.Cancelled;
    public bool IsDirty => HasSession &&
        (session.State == BistroBuilderEditSessionState.ActiveDirty ||
         session.State == BistroBuilderEditSessionState.ReviewingCommit &&
         !string.Equals(session.Draft.ComputeFingerprint(), session.Baseline.ComputeFingerprint(), StringComparison.Ordinal));
    public RestaurantEditModeService EditModeService => editModeService;
    public MonoBehaviour AvailabilityRuleSource => availabilityRuleSource;
    public bool CanUndo => session != null && session.CanUndo;
    public bool CanRedo => session != null && session.CanRedo;

    private void Awake()
    {
        CacheDependencies();
    }
    public void ConfigureAvailabilityRuntime(RestaurantEditModeService service, MonoBehaviour ruleSource)
    {
        editModeService = service;
        availabilityRuleSource = ruleSource;
    }

    public bool CanEditNow(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (availabilityRuleSource != null)
        {
            var rule = availabilityRuleSource as IRestaurantEditModeAvailabilityRule;
            if (rule == null)
            {
                error = "La autoridad de disponibilidad no implementa el contrato de edición.";
                return false;
            }
            if (!rule.CanEnterEditMode(out error))
            {
                if (string.IsNullOrWhiteSpace(error)) error = "El modo edición no está disponible.";
                return false;
            }
        }
        // El servicio existente puede estar activo: la regla externa sigue evaluándose arriba.
        return editModeService == null || editModeService.IsEditModeActive ||
            editModeService.CanEnterEditMode(out _, out error);
    }

    public void DiscardSessionForLoad()
    {
        if (HasSession) session.Cancel();
        session = null;
        DiagnosticsChanged?.Invoke(new List<BistroBuilderEditDiagnostic>());
        SessionChanged?.Invoke();
    }

    public bool TryBindEconomyGateway(IBistroBuilderEditEconomicGateway gateway, out string error)
    {
        error = string.Empty;
        if (gateway == null) { error = "Gateway económico nulo."; return false; }
        if (economyGateway != null && !ReferenceEquals(economyGateway, gateway))
        {
            error = "Ya existe una autoridad económica enlazada al editor.";
            return false;
        }
        economyGateway = gateway;
        return true;
    }

    public bool UnbindEconomyGateway(IBistroBuilderEditEconomicGateway gateway)
    {
        if (economyGateway == null || !ReferenceEquals(economyGateway, gateway)) return false;
        economyGateway = null;
        return true;
    }

    public bool TryBeginSession(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (documentService == null) { error = "Falta la autoridad del documento de edición."; return false; }
        if (HasSession) { error = "Ya existe una sesión de edición activa."; return false; }
        if (!CanEditNow(out error)) return false;
        session = documentService.CreateSession();
        SessionChanged?.Invoke();
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.PreviewStarted, default, "session",
            BistroBuilderEditFeedbackStyle.Pulse, BistroBuilderEditDiagnosticSeverity.Info, default));
        return true;
    }
    public bool TryExecute(IBistroBuilderEditCommand command,
        out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = null;
        error = string.Empty;
        if (!HasSession) { error = "No existe una sesión de edición activa."; return false; }
        if (!CanEditNow(out error)) return false;
        if (!session.TryExecute(command, out changeSet, out error)) return false;

        PublishChangeSet(changeSet, BistroBuilderEditFeedbackEventType.DraftChanged,
            BistroBuilderEditFeedbackStyle.DirectionalReveal);
        SessionChanged?.Invoke();
        return true;
    }

    public bool TryUndo(out string error)
    {
        error = string.Empty;
        if (!HasSession) { error = "No existe una sesión de edición activa."; return false; }
        if (!CanEditNow(out error)) return false;
        if (!session.TryUndo(out error)) return false;
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.Undo, default, "session",
            BistroBuilderEditFeedbackStyle.ReverseReveal, BistroBuilderEditDiagnosticSeverity.Info, default));
        SessionChanged?.Invoke();
        return true;
    }

    public bool TryRedo(out string error)
    {
        error = string.Empty;
        if (!HasSession) { error = "No existe una sesión de edición activa."; return false; }
        if (!CanEditNow(out error)) return false;
        if (!session.TryRedo(out error)) return false;
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.Redo, default, "session",
            BistroBuilderEditFeedbackStyle.DirectionalReveal, BistroBuilderEditDiagnosticSeverity.Info, default));
        SessionChanged?.Invoke();
        return true;
    }
    public bool TryReview(out List<BistroBuilderEditDiagnostic> diagnostics)
    {
        diagnostics = new List<BistroBuilderEditDiagnostic>();
        if (!HasSession) return false;
        bool valid = session.TryReview(out diagnostics);
        DiagnosticsChanged?.Invoke(diagnostics);
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.ValidationChanged, default, "session",
            BistroBuilderEditFeedbackStyle.Pulse,
            ResolveMaximumSeverity(diagnostics), default));
        if (!valid) session.RejectPreparedCommit();
        SessionChanged?.Invoke();
        return valid;
    }

    public bool TryCommit(out BistroBuilderEditDocument committed,
        out List<BistroBuilderEditDiagnostic> diagnostics, out string error)
    {
        committed = null;
        diagnostics = new List<BistroBuilderEditDiagnostic>();
        error = string.Empty;
        if (!HasSession) { error = "No existe una sesión de edición activa."; return false; }

        if (!CanEditNow(out error))
        {
            session.RejectPreparedCommit();
            return false;
        }
        if (documentService == null ||
            !string.Equals(documentService.GetCommittedSnapshot().ComputeFingerprint(),
                session.Baseline.ComputeFingerprint(), StringComparison.Ordinal))
        {
            error = "La baseline comprometida ha cambiado. Reabre la propuesta sobre la partida actual.";
            session.RejectPreparedCommit();
            return false;
        }

        BistroBuilderEditEconomicProposal proposal = BistroBuilderEditEconomicProposalBuilder.Build(session);
        if (proposal.lines.Count > 0 && economyGateway == null)
        {
            error = "La sesión contiene cambios económicos y no hay autoridad financiera enlazada.";
            return false;
        }

        bool succeeded = economyGateway != null
            ? TryCommitWithEconomy(out committed, out diagnostics, out error)
            : TryCommitWithoutEconomicChanges(out committed, out diagnostics, out error);
        if (!succeeded) return false;

        CommitCompleted?.Invoke(committed.DeepClone());
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.BuildCompleted, default, "session",
            BistroBuilderEditFeedbackStyle.Settle, BistroBuilderEditDiagnosticSeverity.Info, default));
        SessionChanged?.Invoke();
        return true;
    }
    public bool CancelSession()
    {
        if (session == null || session.State == BistroBuilderEditSessionState.Cancelled ||
            session.State == BistroBuilderEditSessionState.Committed) return false;
        session.Cancel();
        feedback.Publish(new BistroBuilderEditFeedbackEvent(
            BistroBuilderEditFeedbackEventType.Removed, default, "session_cancel",
            BistroBuilderEditFeedbackStyle.ReverseReveal, BistroBuilderEditDiagnosticSeverity.Info, default));
        SessionChanged?.Invoke();
        return true;
    }

    private bool TryCommitWithEconomy(out BistroBuilderEditDocument committed,
        out List<BistroBuilderEditDiagnostic> diagnostics, out string error)
    {
        var coordinator = new BistroBuilderEditCommitCoordinator(documentService, documentService, economyGateway);
        bool ok = coordinator.TryCommit(session, out committed, out _, out diagnostics, out error);
        if (!ok)
        {
            feedback.Publish(new BistroBuilderEditFeedbackEvent(
                BistroBuilderEditFeedbackEventType.CommitRejected, default, "session",
                BistroBuilderEditFeedbackStyle.Pulse, ResolveMaximumSeverity(diagnostics), default));
        }
        return ok;
    }

    private bool TryCommitWithoutEconomicChanges(out BistroBuilderEditDocument committed,
        out List<BistroBuilderEditDiagnostic> diagnostics, out string error)
    {
        committed = null;
        error = string.Empty;
        if (!session.TryPrepareCommit(out BistroBuilderEditDocument candidate, out diagnostics))
        {
            error = "La propuesta contiene bloqueantes.";
            return false;
        }
        string operationId = "edit_noecon_" + session.SessionId + "_" + candidate.revision;
        if (!documentService.TryPublish(session.BaselineRevision, candidate, operationId, out error))
        {
            session.RejectPreparedCommit();
            return false;
        }
        if (!session.FinalizePreparedCommit(candidate))
        {
            error = "La sesión no pudo finalizar el commit preparado.";
            return false;
        }
        committed = candidate.DeepClone();
        return true;
    }

    private void PublishChangeSet(BistroBuilderEditChangeSet changeSet,
        BistroBuilderEditFeedbackEventType eventType, BistroBuilderEditFeedbackStyle style)
    {
        if (changeSet == null) return;
        PublishIds(changeSet.created, eventType, "created", style);
        PublishIds(changeSet.modified, eventType, "modified", style);
        PublishIds(changeSet.removed, BistroBuilderEditFeedbackEventType.Removed, "removed",
            BistroBuilderEditFeedbackStyle.ReverseReveal);
    }

    private void PublishIds(IReadOnlyList<BistroBuilderEditId> ids,
        BistroBuilderEditFeedbackEventType eventType, string role, BistroBuilderEditFeedbackStyle style)
    {
        if (ids == null) return;
        for (int i = 0; i < ids.Count; i++)
            feedback.Publish(new BistroBuilderEditFeedbackEvent(eventType, ids[i], role, style,
                BistroBuilderEditDiagnosticSeverity.Info, default));
    }
    private static BistroBuilderEditDiagnosticSeverity ResolveMaximumSeverity(
        IReadOnlyList<BistroBuilderEditDiagnostic> diagnostics)
    {
        BistroBuilderEditDiagnosticSeverity severity = BistroBuilderEditDiagnosticSeverity.Info;
        if (diagnostics == null) return severity;
        for (int i = 0; i < diagnostics.Count; i++)
            if (diagnostics[i].severity > severity) severity = diagnostics[i].severity;
        return severity;
    }

    private void CacheDependencies()
    {
        if (documentService == null)
        {
            documentService = GetComponent<BistroBuilderEditDocumentRuntimeService>();
            if (documentService == null)
                documentService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        }
        if (editModeService == null)
            editModeService = GetComponent<RestaurantEditModeService>();
        if (availabilityRuleSource == null)
            availabilityRuleSource = editModeService != null
                ? editModeService.GetComponent<RestaurantServiceEditModeAvailabilityRule>()
                : GetComponent<RestaurantServiceEditModeAvailabilityRule>();
    }
}
