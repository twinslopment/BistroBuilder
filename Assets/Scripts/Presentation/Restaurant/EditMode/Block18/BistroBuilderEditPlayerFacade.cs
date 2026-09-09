using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderEditPlayerSnapshot
{
    public bool hasSession;
    public bool isDirty;
    public bool canUndo;
    public bool canRedo;
    public bool canCommit;
    public string sessionState = "Inactive";
    public long baselineRevision;
    public long draftRevision;
    public int blockingCount;
    public int riskCount;
    public int infoCount;
    public string statusMessage = string.Empty;
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Edit Player Facade")]
public sealed class BistroBuilderEditPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditRuntimeCoordinator coordinator;
    private readonly List<BistroBuilderEditDiagnostic> diagnostics = new List<BistroBuilderEditDiagnostic>();
    private string statusMessage = string.Empty;

    public event Action Changed;

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
    }
    private void OnDisable()
    {
        Unbind();
    }

    public BistroBuilderEditPlayerSnapshot CreateSnapshot()
    {
        var snapshot = new BistroBuilderEditPlayerSnapshot { statusMessage = statusMessage };
        if (coordinator == null) return snapshot;
        BistroBuilderEditSession session = coordinator.Session;
        snapshot.hasSession = coordinator.HasSession;
        snapshot.isDirty = coordinator.IsDirty;
        snapshot.canUndo = coordinator.CanUndo;
        snapshot.canRedo = coordinator.CanRedo;
        snapshot.canCommit = coordinator.IsDirty && coordinator.CanEditNow(out _) && CountBlocking() == 0;
        snapshot.sessionState = session != null ? session.State.ToString() : "Inactive";
        snapshot.baselineRevision = session != null ? session.BaselineRevision : 0L;
        snapshot.draftRevision = session != null ? session.DraftRevision : 0L;
        snapshot.blockingCount = CountSeverity(BistroBuilderEditDiagnosticSeverity.Blocking);
        snapshot.riskCount = CountSeverity(BistroBuilderEditDiagnosticSeverity.Risk);
        snapshot.infoCount = CountSeverity(BistroBuilderEditDiagnosticSeverity.Info);
        snapshot.statusMessage = statusMessage;
        return snapshot;
    }

    public bool BeginEdit()
    {
        string error = "Falta el coordinador de edición.";
        if (coordinator == null || !coordinator.TryBeginSession(out error))
        {
            SetStatus(error);
            return false;
        }
        diagnostics.Clear();
        SetStatus("Edición iniciada.");
        return true;
    }
    public bool Undo()
    {
        string error = "Falta el coordinador de edición.";
        if (coordinator == null || !coordinator.TryUndo(out error))
        {
            SetStatus(error);
            return false;
        }
        SetStatus("Cambio deshecho.");
        return true;
    }

    public bool Redo()
    {
        string error = "Falta el coordinador de edición.";
        if (coordinator == null || !coordinator.TryRedo(out error))
        {
            SetStatus(error);
            return false;
        }
        SetStatus("Cambio rehecho.");
        return true;
    }

    public bool Review()
    {
        List<BistroBuilderEditDiagnostic> current = null;
        if (coordinator == null || !coordinator.TryReview(out current))
        {
            ReplaceDiagnostics(current);
            SetStatus(CountBlocking() > 0 ? "Hay cambios que deben corregirse." : "No se pudo revisar la propuesta.");
            return false;
        }
        ReplaceDiagnostics(current);
        SetStatus("Propuesta lista para confirmar.");
        return true;
    }

    public bool Commit()
    {
        List<BistroBuilderEditDiagnostic> current = null;
        string error = "Falta el coordinador de edición.";
        if (coordinator == null || !coordinator.TryCommit(out _, out current, out error))
        {
            ReplaceDiagnostics(current);
            SetStatus(error);
            return false;
        }
        ReplaceDiagnostics(current);
        SetStatus("Cambios confirmados.");
        return true;
    }
    public bool Cancel()
    {
        if (coordinator == null || !coordinator.CancelSession()) return false;
        diagnostics.Clear();
        SetStatus("Cambios descartados.");
        return true;
    }

    private void Bind()
    {
        if (coordinator == null) return;
        coordinator.SessionChanged -= HandleSessionChanged;
        coordinator.SessionChanged += HandleSessionChanged;
        coordinator.DiagnosticsChanged -= HandleDiagnosticsChanged;
        coordinator.DiagnosticsChanged += HandleDiagnosticsChanged;
    }

    private void Unbind()
    {
        if (coordinator == null) return;
        coordinator.SessionChanged -= HandleSessionChanged;
        coordinator.DiagnosticsChanged -= HandleDiagnosticsChanged;
    }

    private void HandleSessionChanged()
    {
        if (coordinator == null || !coordinator.HasSession)
            diagnostics.Clear();
        else
            diagnostics.RemoveAll(item => item == null || item.draftRevision != coordinator.Session.DraftRevision);
        Changed?.Invoke();
    }
    private void HandleDiagnosticsChanged(IReadOnlyList<BistroBuilderEditDiagnostic> current)
    {
        ReplaceDiagnostics(current);
        Changed?.Invoke();
    }

    private void ReplaceDiagnostics(IReadOnlyList<BistroBuilderEditDiagnostic> current)
    {
        diagnostics.Clear();
        if (current == null) return;
        for (int i = 0; i < current.Count; i++) diagnostics.Add(current[i]);
    }
    private int CountBlocking() => CountSeverity(BistroBuilderEditDiagnosticSeverity.Blocking);

    private int CountSeverity(BistroBuilderEditDiagnosticSeverity severity)
    {
        int count = 0;
        for (int i = 0; i < diagnostics.Count; i++)
            if (diagnostics[i].severity == severity) count++;
        return count;
    }

    private void SetStatus(string message)
    {
        statusMessage = message ?? string.Empty;
        Changed?.Invoke();
    }

    private void CacheDependencies()
    {
        if (coordinator == null)
        {
            coordinator = GetComponent<BistroBuilderEditRuntimeCoordinator>();
            if (coordinator == null)
                coordinator = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        }
    }
}
