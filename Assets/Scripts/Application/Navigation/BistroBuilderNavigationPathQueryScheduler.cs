using System;
using System.Collections.Generic;

/// <summary>
/// Scheduler determinista de consultas globales de navegación.
/// Limita el trabajo de planificación por tick y mantiene prioridad estable.
/// No calcula rutas ni contiene geometría espacial.
/// </summary>
public sealed class BistroBuilderNavigationPathQueryScheduler
{
    private readonly Dictionary<string, Entry> pending =
        new Dictionary<string, Entry>(StringComparer.Ordinal);
    private readonly List<Entry> scratch = new List<Entry>(64);
    private long sequence;

    public int PendingCount => pending.Count;
    public long EnqueuedCount { get; private set; }
    public long CompletedCount { get; private set; }
    public long CancelledCount { get; private set; }
    public long CoalescedCount { get; private set; }

    public void Enqueue(
        string ownerId,
        BistroBuilderNavigationQueryPriority priority,
        int externalUrgency)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        if (pending.TryGetValue(ownerId, out Entry existing) && existing != null)
        {
            if (priority < existing.priority) existing.priority = priority;
            existing.externalUrgency = Math.Max(existing.externalUrgency, externalUrgency);
            CoalescedCount++;
            return;
        }

        pending[ownerId] = new Entry
        {
            ownerId = ownerId,
            priority = priority,
            externalUrgency = externalUrgency,
            sequence = ++sequence
        };
        EnqueuedCount++;
    }

    public int Dequeue(int budget, List<string> results)
    {
        if (results == null || budget <= 0 || pending.Count == 0) return 0;
        scratch.Clear();
        foreach (Entry entry in pending.Values)
            if (entry != null) scratch.Add(entry);
        scratch.Sort(CompareEntries);

        int count = Math.Min(budget, scratch.Count);
        for (int i = 0; i < count; i++)
        {
            Entry entry = scratch[i];
            if (entry == null || !pending.Remove(entry.ownerId)) continue;
            results.Add(entry.ownerId);
            CompletedCount++;
        }
        scratch.Clear();
        return count;
    }

    public bool Cancel(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !pending.Remove(ownerId))
            return false;
        CancelledCount++;
        return true;
    }

    public void Clear()
    {
        if (pending.Count > 0) CancelledCount += pending.Count;
        pending.Clear();
        scratch.Clear();
    }

    private static int CompareEntries(Entry first, Entry second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int priority = first.priority.CompareTo(second.priority);
        if (priority != 0) return priority;
        int urgency = second.externalUrgency.CompareTo(first.externalUrgency);
        if (urgency != 0) return urgency;
        int sequence = first.sequence.CompareTo(second.sequence);
        if (sequence != 0) return sequence;
        return string.CompareOrdinal(first.ownerId, second.ownerId);
    }

    private sealed class Entry
    {
        public string ownerId;
        public BistroBuilderNavigationQueryPriority priority;
        public int externalUrgency;
        public long sequence;
    }
}
