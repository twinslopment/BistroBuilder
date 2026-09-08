using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina únicamente el orden físico de avance por posiciones de espera ya certificadas.
/// No decide orden de negocio ni crea Claims/Spatial Leases.
/// </summary>
public sealed class BistroBuilderNavigationPhysicalQueueCoordinator
{
    private readonly Dictionary<string, QueueRecord> queues =
        new Dictionary<string, QueueRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> ownerQueues =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<QueueEntry> scratchEntries = new List<QueueEntry>(32);

    public int QueueCount => queues.Count;
    public int QueuedAgentCount => ownerQueues.Count;
    public long OverflowEventCount { get; private set; }

    public bool ConfigureQueue(string queueId, IReadOnlyList<Vector3> certifiedSlots)
    {
        if (string.IsNullOrWhiteSpace(queueId) || certifiedSlots == null || certifiedSlots.Count == 0)
            return false;

        if (!queues.TryGetValue(queueId, out QueueRecord record) || record == null)
        {
            record = new QueueRecord { queueId = queueId };
            queues[queueId] = record;
        }
        record.slots.Clear();
        for (int i = 0; i < certifiedSlots.Count; i++)
            record.slots.Add(certifiedSlots[i]);
        return true;
    }
    public bool Enqueue(string queueId, string ownerId, int logicalOrder, float now)
    {
        if (string.IsNullOrWhiteSpace(queueId) || string.IsNullOrWhiteSpace(ownerId) ||
            !queues.TryGetValue(queueId, out QueueRecord record) || record == null)
            return false;

        Remove(ownerId);
        record.entries.Add(new QueueEntry
        {
            ownerId = ownerId,
            logicalOrder = logicalOrder,
            enqueuedAt = now
        });
        ownerQueues[ownerId] = queueId;
        Sort(record);
        if (record.entries.Count > record.slots.Count)
            OverflowEventCount++;
        return true;
    }

    public void ClearQueueEntries(string queueId)
    {
        if (string.IsNullOrWhiteSpace(queueId) ||
            !queues.TryGetValue(queueId, out QueueRecord record) || record == null)
            return;
        for (int i = 0; i < record.entries.Count; i++)
        {
            QueueEntry entry = record.entries[i];
            if (entry != null) ownerQueues.Remove(entry.ownerId);
        }
        record.entries.Clear();
    }
    public void Remove(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !ownerQueues.TryGetValue(ownerId, out string queueId))
            return;
        ownerQueues.Remove(ownerId);
        if (!queues.TryGetValue(queueId, out QueueRecord record) || record == null) return;
        record.entries.RemoveAll(e => e != null && string.Equals(e.ownerId, ownerId, StringComparison.Ordinal));
    }

    public bool TryGetTarget(string ownerId, out string queueId, out int slotIndex, out Vector3 target, out bool overflow)
    {
        queueId = string.Empty;
        slotIndex = -1;
        target = default;
        overflow = false;
        if (string.IsNullOrWhiteSpace(ownerId) || !ownerQueues.TryGetValue(ownerId, out queueId) ||
            !queues.TryGetValue(queueId, out QueueRecord record) || record == null)
            return false;
        Sort(record);
        int index = record.entries.FindIndex(e => e != null && string.Equals(e.ownerId, ownerId, StringComparison.Ordinal));
        if (index < 0) return false;
        slotIndex = index;
        overflow = index >= record.slots.Count;
        if (overflow) return false;
        target = record.slots[index];
        return true;
    }
    public bool TryGetSnapshot(string queueId, out BistroBuilderNavigationPhysicalQueueSnapshot snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(queueId) || !queues.TryGetValue(queueId, out QueueRecord record) || record == null)
            return false;
        Sort(record);
        snapshot = new BistroBuilderNavigationPhysicalQueueSnapshot
        {
            queueId = queueId,
            capacity = record.slots.Count,
            queuedCount = record.entries.Count,
            overflowCount = Mathf.Max(0, record.entries.Count - record.slots.Count)
        };
        for (int i = 0; i < record.entries.Count; i++)
        {
            QueueEntry entry = record.entries[i];
            bool isOverflow = i >= record.slots.Count;
            snapshot.entries.Add(new BistroBuilderNavigationPhysicalQueueEntrySnapshot
            {
                ownerId = entry.ownerId,
                logicalOrder = entry.logicalOrder,
                slotIndex = isOverflow ? -1 : i,
                target = isOverflow ? default : record.slots[i],
                overflow = isOverflow
            });
        }
        return true;
    }

    public int WriteSnapshots(List<BistroBuilderNavigationPhysicalQueueSnapshot> results)
    {
        if (results == null) return 0;
        results.Clear();
        var ids = new List<string>(queues.Keys);
        ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
            if (TryGetSnapshot(ids[i], out BistroBuilderNavigationPhysicalQueueSnapshot snapshot))
                results.Add(snapshot);
        return results.Count;
    }
    private static void Sort(QueueRecord record)
    {
        record.entries.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int logical = a.logicalOrder.CompareTo(b.logicalOrder);
            if (logical != 0) return logical;
            int time = a.enqueuedAt.CompareTo(b.enqueuedAt);
            if (time != 0) return time;
            return string.CompareOrdinal(a.ownerId, b.ownerId);
        });
    }

    private sealed class QueueRecord
    {
        public string queueId;
        public readonly List<Vector3> slots = new List<Vector3>(16);
        public readonly List<QueueEntry> entries = new List<QueueEntry>(16);
    }

    private sealed class QueueEntry
    {
        public string ownerId;
        public int logicalOrder;
        public float enqueuedAt;
    }
}