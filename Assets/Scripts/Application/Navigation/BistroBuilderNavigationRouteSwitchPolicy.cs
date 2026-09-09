using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decide wait-vs-detour por ETA con histéresis temporal.
/// Evita route flapping ante pequeñas oscilaciones de congestión.
/// </summary>
public sealed class BistroBuilderNavigationRouteSwitchPolicy
{
    private const float AbsoluteAdvantageSeconds = 0.8f;
    private const float RelativeAdvantage = 0.12f;
    private const float StableWindowSeconds = 0.7f;
    private const float SwitchCooldownSeconds = 2.4f;
    private readonly Dictionary<string, State> states =
        new Dictionary<string, State>(StringComparer.Ordinal);

    public bool ShouldSwitch(string ownerId, string alternativeKey,
        float currentEta, float alternateEta, float now, out float advantageSeconds)
    {
        advantageSeconds = Mathf.Max(0f, currentEta - alternateEta);
        if (string.IsNullOrWhiteSpace(ownerId) || !float.IsFinite(currentEta) ||
            !float.IsFinite(alternateEta)) return false;

        float threshold = Mathf.Max(AbsoluteAdvantageSeconds,
            Mathf.Max(0f, currentEta) * RelativeAdvantage);
        if (advantageSeconds <= threshold)
        {
            ResetCandidate(ownerId);
            return false;
        }

        if (!states.TryGetValue(ownerId, out State state)) state = new State();
        if (now - state.lastSwitchAt < SwitchCooldownSeconds) return false;
        string key = alternativeKey ?? string.Empty;
        if (!string.Equals(state.candidateKey, key, StringComparison.Ordinal))
        {
            state.candidateKey = key;
            state.candidateSince = now;
            states[ownerId] = state;
            return false;
        }
        if (now - state.candidateSince < StableWindowSeconds)
        {
            states[ownerId] = state;
            return false;
        }

        state.lastSwitchAt = now;
        state.candidateKey = string.Empty;
        state.candidateSince = 0f;
        states[ownerId] = state;
        return true;
    }

    public void RemoveOwner(string ownerId)
    {
        if (!string.IsNullOrWhiteSpace(ownerId)) states.Remove(ownerId);
    }

    private void ResetCandidate(string ownerId)
    {
        if (!states.TryGetValue(ownerId, out State state)) return;
        state.candidateKey = string.Empty;
        state.candidateSince = 0f;
        states[ownerId] = state;
    }

    private struct State
    {
        public string candidateKey;
        public float candidateSince;
        public float lastSwitchAt;
    }
}
