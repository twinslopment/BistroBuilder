using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Edit Document Runtime Service")]
public sealed class BistroBuilderEditDocumentRuntimeService : MonoBehaviour,
    IBistroBuilderEditDocumentStore, IBistroBuilderEditCommitJournalStore, IBistroBuilderEditCapabilityProvider
{
    [SerializeField] private BistroBuilderEditDocument committedDocument = new BistroBuilderEditDocument();

    private readonly HashSet<string> publishedOperations = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderEditCommitJournal> journals =
        new Dictionary<string, BistroBuilderEditCommitJournal>(StringComparer.Ordinal);
    private readonly List<IBistroBuilderEditValidationProvider> validationProviders =
        new List<IBistroBuilderEditValidationProvider>();

    public event Action<BistroBuilderEditDocument> DocumentPublished;
    public long Revision => committedDocument != null ? committedDocument.revision : 0L;

    private void Awake()
    {
        if (committedDocument == null) committedDocument = new BistroBuilderEditDocument();
    }

    public BistroBuilderEditSession CreateSession()
    {
        var orchestrator = new BistroBuilderEditValidationOrchestrator();
        for (int i = 0; i < validationProviders.Count; i++)
            orchestrator.Register(validationProviders[i]);
        return new BistroBuilderEditSession(
            GetCommittedSnapshot(),
            null,
            orchestrator);
    }

    public bool RegisterValidationProvider(
        IBistroBuilderEditValidationProvider provider)
    {
        if (provider == null)
            return false;
        if (validationProviders.Contains(provider))
            return true;
        validationProviders.Add(provider);
        validationProviders.Sort((left, right) =>
            string.CompareOrdinal(left.SourceSystem, right.SourceSystem));
        return true;
    }

    public bool UnregisterValidationProvider(
        IBistroBuilderEditValidationProvider provider)
    {
        return provider != null && validationProviders.Remove(provider);
    }

    public int ValidationProviderCount => validationProviders.Count;

    public BistroBuilderEditDocument GetCommittedSnapshot()
    {
        return committedDocument != null ? committedDocument.DeepClone() : new BistroBuilderEditDocument();
    }
    public bool TryPublish(long expectedBaselineRevision, BistroBuilderEditDocument candidate,
        string operationId, out string error)
    {
        error = string.Empty;
        if (candidate == null || string.IsNullOrWhiteSpace(operationId))
        {
            error = "Publicación inválida.";
            return false;
        }
        if (publishedOperations.Contains(operationId)) return true;
        if (committedDocument == null) committedDocument = new BistroBuilderEditDocument();
        if (committedDocument.revision != expectedBaselineRevision)
        {
            error = "La baseline comprometida ha cambiado.";
            return false;
        }
        if (candidate.revision != expectedBaselineRevision + 1)
        {
            error = "La revisión candidata no es N+1.";
            return false;
        }

        committedDocument = candidate.DeepClone();
        publishedOperations.Add(operationId);
        DocumentPublished?.Invoke(committedDocument.DeepClone());
        return true;
    }

    public bool ReplaceCommittedForLoad(BistroBuilderEditDocument loaded, out string error)
    {
        error = string.Empty;
        if (loaded == null) { error = "Documento de edición nulo."; return false; }
        committedDocument = loaded.DeepClone();
        publishedOperations.Clear();
        journals.Clear();
        DocumentPublished?.Invoke(committedDocument.DeepClone());
        return true;
    }
    public bool TryWrite(BistroBuilderEditCommitJournal journal, out string error)
    {
        error = string.Empty;
        if (journal == null || string.IsNullOrWhiteSpace(journal.operationId))
        {
            error = "Journal inválido.";
            return false;
        }
        journals[journal.operationId] = CloneJournal(journal);
        return true;
    }

    public bool TryRead(string operationId, out BistroBuilderEditCommitJournal journal)
    {
        journal = null;
        if (string.IsNullOrWhiteSpace(operationId) ||
            !journals.TryGetValue(operationId, out BistroBuilderEditCommitJournal stored))
            return false;
        journal = CloneJournal(stored);
        return true;
    }

    public bool TryGetCapabilities(BistroBuilderEditId entityId, out BistroBuilderEditCapability capabilities)
    {
        capabilities = BistroBuilderEditCapability.None;
        if (committedDocument == null || !entityId.IsValid) return false;
        if (committedDocument.FindWall(entityId) != null)
        {
            capabilities = BistroBuilderEditCapability.Remove | BistroBuilderEditCapability.Duplicate |
                BistroBuilderEditCapability.ChangeFinish | BistroBuilderEditCapability.EditEndpoints;
            return true;
        }
        if (committedDocument.FindOpening(entityId) != null)
        {
            capabilities = BistroBuilderEditCapability.Remove | BistroBuilderEditCapability.Duplicate |
                BistroBuilderEditCapability.Resize | BistroBuilderEditCapability.MoveAlongHost |
                BistroBuilderEditCapability.Flip | BistroBuilderEditCapability.ChangeVariant;
            return true;
        }
        return false;
    }
    private static BistroBuilderEditCommitJournal CloneJournal(BistroBuilderEditCommitJournal source)
    {
        return new BistroBuilderEditCommitJournal
        {
            operationId = source.operationId,
            sessionId = source.sessionId,
            baselineRevision = source.baselineRevision,
            draftRevision = source.draftRevision,
            draftFingerprint = source.draftFingerprint,
            economicAuthorizationId = source.economicAuthorizationId,
            state = source.state
        };
    }
}
