using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración ligera del agente operativo para el Bloque 13.
/// No sustituye la identidad persistente de Personal.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedWaiterProfile : MonoBehaviour
{
    [SerializeField] private string primaryZoneId = "dining";
    [SerializeField] private List<string> secondaryZoneIds = new List<string> { "bar" };
    [SerializeField, Range(2, 6)] private int simultaneousPlanCapacity = 3;
    [SerializeField, Range(0.75f, 1.25f)] private float serviceEfficiency = 1f;

    public string PrimaryZoneId => NormalizeZone(primaryZoneId);
    public IReadOnlyList<string> SecondaryZoneIds => secondaryZoneIds;
    public int SimultaneousPlanCapacity => Mathf.Clamp(simultaneousPlanCapacity, 2, 6);
    public float ServiceEfficiency => Mathf.Clamp(serviceEfficiency, 0.75f, 1.25f);

    public void CopySecondaryZones(List<string> destination)
    {
        if (destination == null) return;
        destination.Clear();
        for (int i = 0; i < secondaryZoneIds.Count; i++)
            destination.Add(NormalizeZone(secondaryZoneIds[i]));
    }

    public bool TryRestorePersistentSettings(
        string primaryZone,
        IReadOnlyList<string> secondaryZones,
        int planCapacity,
        float efficiency,
        out string error)
    {
        if (planCapacity < 2 || planCapacity > 6 ||
            float.IsNaN(efficiency) || float.IsInfinity(efficiency) ||
            efficiency < 0.75f || efficiency > 1.25f)
        {
            error = "El perfil persistente del camarero avanzado es inválido.";
            return false;
        }

        primaryZoneId = NormalizeZone(primaryZone);
        secondaryZoneIds ??= new List<string>();
        secondaryZoneIds.Clear();
        if (secondaryZones != null)
        {
            for (int i = 0; i < secondaryZones.Count; i++)
            {
                string normalized = NormalizeZone(secondaryZones[i]);
                if (string.Equals(normalized, primaryZoneId, StringComparison.Ordinal) ||
                    secondaryZoneIds.Contains(normalized))
                    continue;
                secondaryZoneIds.Add(normalized);
            }
        }
        simultaneousPlanCapacity = planCapacity;
        serviceEfficiency = efficiency;
        error = string.Empty;
        return true;
    }
    public bool SupportsZone(string zoneId, out BistroBuilderWaiterResponsibilityKind responsibility)
    {
        string normalized = NormalizeZone(zoneId);
        if (string.Equals(PrimaryZoneId, normalized, StringComparison.Ordinal))
        {
            responsibility = BistroBuilderWaiterResponsibilityKind.Primary;
            return true;
        }

        for (int i = 0; i < secondaryZoneIds.Count; i++)
        {
            if (string.Equals(NormalizeZone(secondaryZoneIds[i]), normalized, StringComparison.Ordinal))
            {
                responsibility = BistroBuilderWaiterResponsibilityKind.Secondary;
                return true;
            }
        }

        responsibility = BistroBuilderWaiterResponsibilityKind.Support;
        return false;
    }

    public static string NormalizeZone(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "dining"
            : value.Trim().ToLowerInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        primaryZoneId = NormalizeZone(primaryZoneId);
        simultaneousPlanCapacity = Mathf.Clamp(simultaneousPlanCapacity, 2, 6);
        serviceEfficiency = Mathf.Clamp(serviceEfficiency, 0.75f, 1.25f);
        secondaryZoneIds ??= new List<string>();
        for (int i = secondaryZoneIds.Count - 1; i >= 0; i--)
        {
            string normalized = NormalizeZone(secondaryZoneIds[i]);
            if (string.Equals(normalized, primaryZoneId, StringComparison.Ordinal))
                secondaryZoneIds.RemoveAt(i);
            else
                secondaryZoneIds[i] = normalized;
        }
    }
#endif
}
