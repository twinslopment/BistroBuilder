using System;
using System.Collections.Generic;

/// <summary>
/// Memoria temporal de recoveries fallidos. Evita repetir la misma maniobra
/// bajo la misma firma de bloqueo y fuerza escalado progresivo.
/// </summary>
public sealed class BistroBuilderNavigationFailureMemory
{
    private readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(StringComparer.Ordinal);
    private readonly List<string> scratch = new List<string>(32);

    public int Count => entries.Count;
    public long SuppressedCount { get; private set; }

    public bool IsSuppressed(string ownerId, BistroBuilderNavigationRecoveryStage stage,
        string signature, float now)
    {
        string key = Key(ownerId, stage, signature);
        if (!entries.TryGetValue(key, out Entry entry)) return false;
        if (now >= entry.suppressUntil) return false;
        SuppressedCount++;
        return true;
    }

    public void RecordFailure(string ownerId, BistroBuilderNavigationRecoveryStage stage,
        string signature, float now)
    {
        string key = Key(ownerId, stage, signature);
        entries.TryGetValue(key, out Entry entry);
        entry.failures++;
        entry.lastFailureAt = now;
        entry.suppressUntil = now + Math.Min(8f, 0.8f + entry.failures * 1.35f);
        entries[key] = entry;
    }
    public void RecordSuccess(string ownerId, BistroBuilderNavigationRecoveryStage stage,
        string signature)
    {
        entries.Remove(Key(ownerId, stage, signature));
    }

    public void RemoveOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        scratch.Clear();
        string prefix = ownerId + "|";
        foreach (string key in entries.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) scratch.Add(key);
        for (int i = 0; i < scratch.Count; i++) entries.Remove(scratch[i]);
    }

    public void Cleanup(float now)
    {
        scratch.Clear();
        foreach (KeyValuePair<string, Entry> pair in entries)
            if (now - pair.Value.lastFailureAt > 20f) scratch.Add(pair.Key);
        for (int i = 0; i < scratch.Count; i++) entries.Remove(scratch[i]);
    }

    private static string Key(string ownerId, BistroBuilderNavigationRecoveryStage stage,
        string signature)
    {
        return (ownerId ?? string.Empty) + "|" + ((int)stage).ToString() + "|" +
               (signature ?? string.Empty);
    }

    private struct Entry
    {
        public int failures;
        public float lastFailureAt;
        public float suppressUntil;
    }
}
