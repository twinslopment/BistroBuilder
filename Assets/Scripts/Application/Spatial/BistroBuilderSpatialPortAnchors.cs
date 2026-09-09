using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderSpatialPortAnchorBinding
{
    public string portId = string.Empty;
    public Transform anchor;
}

/// <summary>
/// Permite que un Spatial Contract reutilizable resuelva Ports contra
/// anclajes reales del asset sin acoplarse a su malla ni jerarquía visual.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialPortAnchors : MonoBehaviour
{
    [SerializeField] private List<BistroBuilderSpatialPortAnchorBinding> bindings =
        new List<BistroBuilderSpatialPortAnchorBinding>();

    public bool TryGetAnchor(string portId, out Transform anchor)
    {
        anchor = null;
        if (string.IsNullOrWhiteSpace(portId)) return false;
        for (int i = 0; i < bindings.Count; i++)
        {
            BistroBuilderSpatialPortAnchorBinding binding = bindings[i];
            if (binding == null || binding.anchor == null) continue;
            if (!string.Equals(binding.portId, portId, StringComparison.Ordinal)) continue;
            anchor = binding.anchor;
            return true;
        }
        return false;
    }

    public void ClearBindings() => bindings.Clear();

    public void AddBinding(string portId, Transform anchor)
    {
        if (string.IsNullOrWhiteSpace(portId) || anchor == null) return;
        bindings.Add(new BistroBuilderSpatialPortAnchorBinding
        {
            portId = portId.Trim(),
            anchor = anchor
        });
    }

#if UNITY_EDITOR
    public void ClearForEditor() => ClearBindings();

    public void AddForEditor(string portId, Transform anchor) =>
        AddBinding(portId, anchor);
#endif
}
