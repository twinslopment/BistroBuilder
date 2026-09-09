using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderSpatialPortDefinition
{
    public string portId = string.Empty;
    public BistroBuilderSpatialPortKind kind = BistroBuilderSpatialPortKind.Interaction;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localForward = Vector3.forward;
    [Min(0.05f)] public float radius = 0.35f;
    public BistroBuilderSpatialConflictMode conflictMode =
        BistroBuilderSpatialConflictMode.Reservable;
}

[Serializable]
public sealed class BistroBuilderSpatialWorkEdgeDefinition
{
    public string edgeId = string.Empty;
    public Vector3 localStart = Vector3.left * 0.5f;
    public Vector3 localEnd = Vector3.right * 0.5f;
    [Min(0.05f)] public float serviceDepth = 0.55f;
    public BistroBuilderSpatialConflictMode conflictMode =
        BistroBuilderSpatialConflictMode.Reservable;
}

[Serializable]
public sealed class BistroBuilderSpatialGateDefinition
{
    public string gateId = string.Empty;
    public Vector3 localStart = Vector3.left * 0.5f;
    public Vector3 localEnd = Vector3.right * 0.5f;
    [Min(0.1f)] public float minimumWidth = 0.75f;
    public bool criticalRoute;
}

/// <summary>
/// Contrato semántico de espacio de un asset. Define cómo puede usarse;
/// la geometría concreta vive en Adaptive Spatial Proxy.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_SpatialContract",
    menuName = "Bistro Builder/Spatial/Spatial Contract")]
public sealed class BistroBuilderSpatialContractDefinition : ScriptableObject
{
    [SerializeField] private string contractId = string.Empty;
    [SerializeField] private string familyId = "generic";
    [SerializeField, Min(1)] private int contractVersion = 1;
    [SerializeField] private BistroBuilderAdaptiveSpatialProxyMode preferredProxyMode =
        BistroBuilderAdaptiveSpatialProxyMode.Simple;
    [SerializeField] private List<string> traitIds = new List<string>();
    [SerializeField] private List<BistroBuilderSpatialPortDefinition> ports =
        new List<BistroBuilderSpatialPortDefinition>();
    [SerializeField] private List<BistroBuilderSpatialWorkEdgeDefinition> workEdges =
        new List<BistroBuilderSpatialWorkEdgeDefinition>();
    [SerializeField] private List<BistroBuilderSpatialGateDefinition> gates =
        new List<BistroBuilderSpatialGateDefinition>();

    public string ContractId => contractId;
    public string FamilyId => familyId;
    public int ContractVersion => contractVersion;
    public BistroBuilderAdaptiveSpatialProxyMode PreferredProxyMode => preferredProxyMode;
    public IReadOnlyList<string> TraitIds => traitIds;
    public IReadOnlyList<BistroBuilderSpatialPortDefinition> Ports => ports;
    public IReadOnlyList<BistroBuilderSpatialWorkEdgeDefinition> WorkEdges => workEdges;
    public IReadOnlyList<BistroBuilderSpatialGateDefinition> Gates => gates;

    public bool HasTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId)) return false;
        for (int i = 0; i < traitIds.Count; i++)
            if (string.Equals(traitIds[i], traitId, StringComparison.Ordinal))
                return true;
        return false;
    }

    public bool ValidateDefinition(out string error)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            error = "Spatial Contract sin contractId estable.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(familyId))
        {
            error = contractId + ": familyId vacío.";
            return false;
        }
        if (contractVersion < 1)
        {
            error = contractId + ": contractVersion inválido.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ports.Count; i++)
        {
            BistroBuilderSpatialPortDefinition port = ports[i];
            if (port == null || string.IsNullOrWhiteSpace(port.portId) ||
                !ids.Add("port:" + port.portId))
            {
                error = contractId + ": Port inválido o duplicado.";
                return false;
            }
        }

        for (int i = 0; i < workEdges.Count; i++)
        {
            BistroBuilderSpatialWorkEdgeDefinition edge = workEdges[i];
            if (edge == null || string.IsNullOrWhiteSpace(edge.edgeId) ||
                !ids.Add("edge:" + edge.edgeId))
            {
                error = contractId + ": Work Edge inválido o duplicado.";
                return false;
            }
        }
        for (int i = 0; i < gates.Count; i++)
        {
            BistroBuilderSpatialGateDefinition gate = gates[i];
            if (gate == null || string.IsNullOrWhiteSpace(gate.gateId) ||
                !ids.Add("gate:" + gate.gateId))
            {
                error = contractId + ": Spatial Gate inválido o duplicado.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public void ClearSemanticGeometryForEditor()
    {
        ports.Clear();
        workEdges.Clear();
        gates.Clear();
    }

    public void AddPortForEditor(BistroBuilderSpatialPortDefinition port)
    {
        if (port != null) ports.Add(port);
    }

    public void AddWorkEdgeForEditor(BistroBuilderSpatialWorkEdgeDefinition edge)
    {
        if (edge != null) workEdges.Add(edge);
    }

    public void AddGateForEditor(BistroBuilderSpatialGateDefinition gate)
    {
        if (gate != null) gates.Add(gate);
    }

    public void SetContractVersionForEditor(int version)
    {
        contractVersion = Mathf.Max(1, version);
    }
    public void ConfigureForEditor(
        string stableContractId,
        string stableFamilyId,
        BistroBuilderAdaptiveSpatialProxyMode mode,
        IEnumerable<string> traits)
    {
        contractId = stableContractId ?? string.Empty;
        familyId = string.IsNullOrWhiteSpace(stableFamilyId) ? "generic" : stableFamilyId;
        contractVersion = Mathf.Max(1, contractVersion);
        preferredProxyMode = mode;
        traitIds.Clear();
        if (traits != null)
        {
            foreach (string trait in traits)
                if (!string.IsNullOrWhiteSpace(trait) && !traitIds.Contains(trait))
                    traitIds.Add(trait);
        }
    }
#endif
}

[Serializable]
public sealed class BistroBuilderSpatialFamilyDefinition
{
    public string familyId = string.Empty;
    public string displayName = string.Empty;
    public List<string> defaultTraitIds = new List<string>();
}
