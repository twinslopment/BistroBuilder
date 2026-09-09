using System;
using UnityEngine;

/// <summary>
/// Planificador determinista de maniobras locales de recuperación.
/// Solo propone candidatos; la validez espacial la certifica el callback de BBSIS/Navigation.
/// </summary>
public sealed class BistroBuilderNavigationRecoveryPlanner
{
    private static readonly float[] EscapeDistances = { 0.45f, 0.7f, 0.95f, 1.2f };
    private static readonly float[] RetreatDistances = { 0.55f, 0.8f, 1.1f, 1.4f, 1.7f };

    public bool TryFindEscapePocket(
        string ownerId,
        Vector3 current,
        Vector3 forward,
        Func<Vector3, bool> validator,
        out Vector3 target)
    {
        target = current;
        Vector3 f = HorizontalNormalized(forward, Vector3.forward);
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        int side = StableSide(ownerId);

        for (int d = 0; d < EscapeDistances.Length; d++)
        {
            float distance = EscapeDistances[d];
            if (TryCandidate(current, right * side, f, distance, validator, out target) ||
                TryCandidate(current, -right * side, f, distance, validator, out target))
                return true;
        }
        return false;
    }
    public bool TryFindRetreat(
        string ownerId,
        Vector3 current,
        Vector3 forward,
        Func<Vector3, bool> validator,
        out Vector3 target)
    {
        target = current;
        Vector3 f = HorizontalNormalized(forward, Vector3.forward);
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        int side = StableSide(ownerId);

        for (int d = 0; d < RetreatDistances.Length; d++)
        {
            float distance = RetreatDistances[d];
            Vector3 backward = -f * distance;
            Vector3 lateral = right * side * Mathf.Min(0.35f, distance * 0.25f);
            Vector3 candidate = current + backward;
            candidate.y = current.y;
            if (validator == null || validator(candidate))
            {
                target = candidate;
                return true;
            }

            candidate = current + backward + lateral;
            candidate.y = current.y;
            if (validator == null || validator(candidate))
            {
                target = candidate;
                return true;
            }
        }
        return false;
    }
    private static bool TryCandidate(
        Vector3 current,
        Vector3 lateral,
        Vector3 forward,
        float distance,
        Func<Vector3, bool> validator,
        out Vector3 target)
    {
        Vector3 candidate = current + lateral * distance;
        candidate.y = current.y;
        if (validator == null || validator(candidate))
        {
            target = candidate;
            return true;
        }

        candidate = current + lateral * distance + forward * Mathf.Min(0.25f, distance * 0.25f);
        candidate.y = current.y;
        if (validator == null || validator(candidate))
        {
            target = candidate;
            return true;
        }
        target = current;
        return false;
    }

    private static Vector3 HorizontalNormalized(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.000001f ? value.normalized : fallback;
    }
    private static int StableSide(string ownerId)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string value = ownerId ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return (hash & 1u) == 0u ? 1 : -1;
        }
    }
}