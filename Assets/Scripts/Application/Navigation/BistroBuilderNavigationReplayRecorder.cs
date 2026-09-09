using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Registro acotado y determinista de incidentes/decisiones de Navigation.
/// Permite reproducir la línea temporal sin alterar el runtime.
/// </summary>
public sealed class BistroBuilderNavigationReplayRecorder
{
    private const int DefaultCapacity = 4096;
    private readonly List<BistroBuilderNavigationReplayEvent> events =
        new List<BistroBuilderNavigationReplayEvent>(DefaultCapacity);
    private readonly int capacity;
    private long sequence;

    public BistroBuilderNavigationReplayRecorder(int capacity = DefaultCapacity)
    {
        this.capacity = Mathf.Max(128, capacity);
    }

    public int Count => events.Count;
    public string Label { get; private set; } = "Navigation Incident";

    public void Clear(string label = null)
    {
        events.Clear();
        sequence = 0;
        if (!string.IsNullOrWhiteSpace(label)) Label = label;
    }

    public void Record(
        string eventType,
        string ownerId,
        Vector3 position,
        BistroBuilderNavigationDecisionTrace trace,
        BistroBuilderNavigationTopologySnapshot topology,
        string detail = "")
    {        if (events.Count >= capacity)
            events.RemoveAt(0);

        events.Add(new BistroBuilderNavigationReplayEvent
        {
            sequence = ++sequence,
            eventType = eventType ?? string.Empty,
            ownerId = ownerId ?? string.Empty,
            position = position,
            state = trace != null ? trace.state : BistroBuilderNavigationTravelState.Idle,
            waitingReason = trace != null ? trace.waitingReason : BistroBuilderNavigationWaitingReason.None,
            recoveryStage = trace != null ? trace.recoveryStage : BistroBuilderNavigationRecoveryStage.None,
            blockerId = trace != null ? trace.blockerId ?? string.Empty : string.Empty,
            detail = detail ?? string.Empty,
            topology = topology
        });
    }

    public int WriteEvents(List<BistroBuilderNavigationReplayEvent> results)
    {
        if (results == null) return 0;
        results.Clear();
        for (int i = 0; i < events.Count; i++)
            results.Add(Clone(events[i]));
        return results.Count;
    }

    public BistroBuilderNavigationReplayBundle CaptureBundle()
    {
        var bundle = new BistroBuilderNavigationReplayBundle { label = Label };
        for (int i = 0; i < events.Count; i++)
            bundle.events.Add(Clone(events[i]));
        bundle.deterministicDigest = ComputeDigest(bundle.events);
        return bundle;
    }
    public static bool ValidateBundle(BistroBuilderNavigationReplayBundle bundle, out string error)
    {
        error = string.Empty;
        if (bundle == null || bundle.events == null)
        {
            error = "Replay bundle nulo.";
            return false;
        }
        long previous = 0;
        for (int i = 0; i < bundle.events.Count; i++)
        {
            BistroBuilderNavigationReplayEvent e = bundle.events[i];
            if (e == null || e.sequence <= previous)
            {
                error = "Secuencia de replay no monotónica en índice " + i + ".";
                return false;
            }
            previous = e.sequence;
        }
        string digest = ComputeDigest(bundle.events);
        if (!string.Equals(digest, bundle.deterministicDigest, StringComparison.Ordinal))
        {
            error = "Digest determinista de replay no coincide.";
            return false;
        }
        return true;
    }

    public static string ComputeDigest(IReadOnlyList<BistroBuilderNavigationReplayEvent> source)
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    HashEvent(ref hash, source[i]);
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }
    private static void HashEvent(ref ulong hash, BistroBuilderNavigationReplayEvent e)
    {
        if (e == null) { HashString(ref hash, "<null>"); return; }
        HashString(ref hash, e.sequence.ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, e.eventType);
        HashString(ref hash, e.ownerId);
        HashString(ref hash, Quantized(e.position.x));
        HashString(ref hash, Quantized(e.position.y));
        HashString(ref hash, Quantized(e.position.z));
        HashString(ref hash, ((int)e.state).ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, ((int)e.waitingReason).ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, ((int)e.recoveryStage).ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, e.blockerId);
        HashString(ref hash, e.detail);
        HashString(ref hash, e.topology.spatialRevision.ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, e.topology.navigationRevision.ToString(CultureInfo.InvariantCulture));
        HashString(ref hash, e.topology.trafficEpoch.ToString(CultureInfo.InvariantCulture));
    }

    private static string Quantized(float value) =>
        Mathf.Round(value * 1000f).ToString(CultureInfo.InvariantCulture);

    private static void HashString(ref ulong hash, string value)
    {
        unchecked
        {
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            hash ^= 255;
            hash *= 1099511628211UL;
        }
    }
    private static BistroBuilderNavigationReplayEvent Clone(BistroBuilderNavigationReplayEvent e)
    {
        if (e == null) return null;
        return new BistroBuilderNavigationReplayEvent
        {
            sequence = e.sequence,
            eventType = e.eventType,
            ownerId = e.ownerId,
            position = e.position,
            state = e.state,
            waitingReason = e.waitingReason,
            recoveryStage = e.recoveryStage,
            blockerId = e.blockerId,
            detail = e.detail,
            topology = e.topology
        };
    }
}