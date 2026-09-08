using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BistroBuilderUniversalEditCatalogIndex
{
    private readonly Dictionary<string, BistroBuilderEditCatalogDefinition> byId =
        new Dictionary<string, BistroBuilderEditCatalogDefinition>(StringComparer.Ordinal);
    private readonly List<BistroBuilderEditCatalogDefinition> ordered = new List<BistroBuilderEditCatalogDefinition>();
    public IReadOnlyList<BistroBuilderEditCatalogDefinition> Items => ordered;

    public void Rebuild(IEnumerable<BistroBuilderEditCatalogDefinition> definitions)
    {
        byId.Clear(); ordered.Clear(); if (definitions == null) return;
        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.definitionId)) continue;
            string id = Normalize(definition.definitionId); if (byId.ContainsKey(id)) continue;
            definition.definitionId = id; byId.Add(id, definition); ordered.Add(definition);
        }
        ordered.Sort((a,b) =>
        {
            int c = string.CompareOrdinal(a.categoryId, b.categoryId);
            return c != 0 ? c : string.CompareOrdinal(a.definitionId, b.definitionId);
        });
    }

    public bool TryGet(string definitionId, out BistroBuilderEditCatalogDefinition definition) =>
        byId.TryGetValue(Normalize(definitionId), out definition);
    public int Search(string query, string categoryId, List<BistroBuilderEditCatalogDefinition> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));
        results.Clear(); string q = Normalize(query); string category = Normalize(categoryId);
        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            if (!string.IsNullOrEmpty(category) && !string.Equals(Normalize(item.categoryId), category, StringComparison.Ordinal)) continue;
            if (string.IsNullOrEmpty(q) || Contains(item.definitionId, q) || Contains(item.semanticKind, q) || Contains(item.familyId, q) || TagsContain(item.tags, q))
                results.Add(item);
        }
        return results.Count;
    }

    public static BistroBuilderEditCatalogDefinition FromExistingPlaceable(RestaurantPlaceableItemDefinition source)
    {
        if (source == null) return null;
        return new BistroBuilderEditCatalogDefinition
        {
            definitionId = source.ItemId,
            semanticKind = "placeable",
            categoryId = source.Category.ToString().ToLowerInvariant(),
            familyId = source.ItemId,
            capabilities = BistroBuilderEditCapability.Place | BistroBuilderEditCapability.Move |
                BistroBuilderEditCapability.Rotate | BistroBuilderEditCapability.Remove |
                BistroBuilderEditCapability.Duplicate | BistroBuilderEditCapability.Snap,
            runtimeAssetKey = source.ItemId
        };
    }

    private static bool TagsContain(List<string> tags, string q)
    { if (tags == null) return false; for (int i=0;i<tags.Count;i++) if (Contains(tags[i], q)) return true; return false; }
    private static bool Contains(string value, string q) => Normalize(value).Contains(q);
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
}
public enum BistroBuilderEditEconomicChangeKind { Added = 0, Modified = 1, Removed = 2 }

[Serializable]
public sealed class BistroBuilderEditEconomicLine
{
    public BistroBuilderEditEconomicChangeKind kind;
    public BistroBuilderEditId entityId;
    public string definitionId = string.Empty;
    public float quantity = 1f;
    public float length;
    public float area;
}

[Serializable]
public sealed class BistroBuilderEditEconomicProposal
{
    public string sessionId = string.Empty;
    public long baselineRevision;
    public long draftRevision;
    public string draftFingerprint = string.Empty;
    public List<BistroBuilderEditEconomicLine> lines = new List<BistroBuilderEditEconomicLine>();
}

public readonly struct BistroBuilderEditEconomicAuthorization
{
    public readonly string authorizationId;
    public readonly long draftRevision;
    public readonly long totalCents;
    public BistroBuilderEditEconomicAuthorization(string id, long revision, long cents)
    { authorizationId = id ?? string.Empty; draftRevision = revision; totalCents = cents; }
    public bool IsValid => !string.IsNullOrWhiteSpace(authorizationId);
}
public interface IBistroBuilderEditEconomicGateway
{
    bool TryPrepareAuthorization(BistroBuilderEditEconomicProposal proposal,
        out BistroBuilderEditEconomicAuthorization authorization, out string error);
    bool TryFinalizeAuthorization(BistroBuilderEditEconomicAuthorization authorization, string commitOperationId, out string error);
    bool TryAbortAuthorization(BistroBuilderEditEconomicAuthorization authorization, out string error);
}

public enum BistroBuilderEditCommitJournalState
{
    None = 0, Prepared = 1, Publishing = 2, Published = 3, Finalized = 4, Aborted = 5
}

[Serializable]
public sealed class BistroBuilderEditCommitJournal
{
    public string operationId = string.Empty;
    public string sessionId = string.Empty;
    public long baselineRevision;
    public long draftRevision;
    public string draftFingerprint = string.Empty;
    public string economicAuthorizationId = string.Empty;
    public BistroBuilderEditCommitJournalState state;

    public bool Matches(string operation, string fingerprint) =>
        string.Equals(operationId, operation, StringComparison.Ordinal) &&
        string.Equals(draftFingerprint, fingerprint, StringComparison.Ordinal);
}

public enum BistroBuilderEditFeedbackEventType
{
    PreviewStarted = 0, PreviewChanged = 1, DraftChanged = 2, ValidationChanged = 3,
    Snapped = 4, BuildConfirmed = 5, BuildCompleted = 6, Modified = 7,
    Removed = 8, Undo = 9, Redo = 10, CommitRejected = 11
}
public enum BistroBuilderEditFeedbackStyle
{
    DirectionalReveal = 0, AreaFill = 1, CutAndInsert = 2, SurfaceWipe = 3,
    Settle = 4, ReverseReveal = 5, Pulse = 6
}

public readonly struct BistroBuilderEditFeedbackEvent
{
    public readonly BistroBuilderEditFeedbackEventType eventType;
    public readonly BistroBuilderEditId entityId;
    public readonly string semanticTag;
    public readonly BistroBuilderEditFeedbackStyle style;
    public readonly BistroBuilderEditDiagnosticSeverity validationSeverity;
    public readonly Bounds worldBounds;
    public BistroBuilderEditFeedbackEvent(BistroBuilderEditFeedbackEventType type, BistroBuilderEditId id,
        string tag, BistroBuilderEditFeedbackStyle style, BistroBuilderEditDiagnosticSeverity severity, Bounds bounds)
    { eventType = type; entityId = id; semanticTag = tag ?? string.Empty; this.style = style; validationSeverity = severity; worldBounds = bounds; }
}

public sealed class BistroBuilderEditFeedbackEventHub
{
    public event Action<BistroBuilderEditFeedbackEvent> EventPublished;
    public void Publish(BistroBuilderEditFeedbackEvent editEvent) => EventPublished?.Invoke(editEvent);
}

public interface IBistroBuilderEditCapabilityProvider
{
    bool TryGetCapabilities(BistroBuilderEditId entityId, out BistroBuilderEditCapability capabilities);
}
