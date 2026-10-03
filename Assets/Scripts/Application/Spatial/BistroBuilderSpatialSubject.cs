using System;
using UnityEngine;

/// <summary>
/// Vincula un objeto del restaurante con su Spatial Contract y su proxy.
/// El subjectId es persistente y no se deriva del InstanceID de Unity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderAdaptiveSpatialProxy))]
public sealed class BistroBuilderSpatialSubject : MonoBehaviour
{
    [SerializeField] private string subjectId = string.Empty;
    [SerializeField] private BistroBuilderSpatialContractDefinition contract;
    [SerializeField] private BistroBuilderAdaptiveSpatialProxy proxy;
    [SerializeField] private MonoBehaviour lifecycleOwner;
    [SerializeField] private bool requiresLifecycleOwner;

    private BistroBuilderSpatialInteractionService service;
    private BistroBuilderSpatialPortAnchors portAnchors;

    public string SubjectId => subjectId;
    public BistroBuilderSpatialContractDefinition Contract => contract;
    public BistroBuilderAdaptiveSpatialProxy Proxy => proxy;
    public bool IsRegistrationEligible => !requiresLifecycleOwner ||
        (lifecycleOwner != null && lifecycleOwner is IBistroBuilderSpatialLifecycleOwner owner && owner.IsSpatialLifecycleActive);

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (IsRegistrationEligible) service?.RegisterSubject(this);
    }

    private void OnDisable()
    {
        service?.UnregisterSubject(this);
    }

    public bool ValidateSubject(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            error = name + ": subjectId espacial vacío.";
            return false;
        }
        if (contract == null || !contract.ValidateDefinition(out error))
        {
            if (contract == null) error = subjectId + ": falta Spatial Contract.";
            return false;
        }
        if (proxy == null || !proxy.ValidateProxy(out error))
        {
            if (proxy == null) error = subjectId + ": falta Adaptive Spatial Proxy.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryGetPortWorld(
        string portId,
        out Vector3 position,
        out Vector3 forward,
        out float radius,
        out BistroBuilderSpatialConflictMode conflictMode)
    {
        position = default;
        forward = Vector3.forward;
        radius = 0.35f;
        conflictMode = BistroBuilderSpatialConflictMode.Reservable;
        CacheReferences();
        if (contract == null || string.IsNullOrWhiteSpace(portId)) return false;
        for (int i = 0; i < contract.Ports.Count; i++)
        {
            BistroBuilderSpatialPortDefinition port = contract.Ports[i];
            if (port == null || !string.Equals(port.portId, portId, StringComparison.Ordinal))
                continue;
            if (portAnchors != null &&
                portAnchors.TryGetAnchor(port.portId, out Transform anchor) &&
                anchor != null)
            {
                position = anchor.position;
                forward = anchor.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.000001f
                    ? forward.normalized
                    : transform.forward;
            }
            else
            {
                position = transform.TransformPoint(port.localPosition);
                forward = transform.TransformDirection(port.localForward).normalized;
            }
            radius = Mathf.Max(0.05f, port.radius);
            conflictMode = port.conflictMode;
            return true;
        }
        return false;
    }

    private void CacheReferences()
    {
        if (proxy == null) proxy = GetComponent<BistroBuilderAdaptiveSpatialProxy>();
        if (portAnchors == null) portAnchors = GetComponent<BistroBuilderSpatialPortAnchors>();
        if (service == null)
            service = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }

    public void Configure(
        string stableSubjectId,
        BistroBuilderSpatialContractDefinition definition,
        BistroBuilderAdaptiveSpatialProxy spatialProxy)
    {
        CacheReferences();
        string nextSubjectId = stableSubjectId ?? string.Empty;
        bool identityChanged = !string.Equals(
            subjectId,
            nextSubjectId,
            StringComparison.Ordinal);
        if (identityChanged && service != null && !string.IsNullOrWhiteSpace(subjectId))
            service.UnregisterSubject(this);

        subjectId = nextSubjectId;
        contract = definition;
        proxy = spatialProxy != null
            ? spatialProxy
            : GetComponent<BistroBuilderAdaptiveSpatialProxy>();
        CacheReferences();

        // AddComponent ejecuta OnEnable antes de que el binder pueda asignar el
        // subjectId. Registrar aquí evita un RebuildSubjects global posterior.
        if (isActiveAndEnabled && IsRegistrationEligible && service != null && !string.IsNullOrWhiteSpace(subjectId))
            service.RegisterSubject(this);
    }

    public void ConfigureLifecycleOwner(MonoBehaviour owner)
    {
        if (owner != null && (!(owner is IBistroBuilderSpatialLifecycleOwner) || !transform.IsChildOf(owner.transform)))
            throw new ArgumentException("A spatial lifecycle owner must own this subject hierarchy.", nameof(owner));
        lifecycleOwner = owner;
        requiresLifecycleOwner = owner != null;
        CacheReferences();
        if (!IsRegistrationEligible) service?.UnregisterSubject(this);
        else if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(subjectId)) service?.RegisterSubject(this);
    }
#if UNITY_EDITOR
    public void ConfigureForEditor(
        string stableSubjectId,
        BistroBuilderSpatialContractDefinition definition,
        BistroBuilderAdaptiveSpatialProxy spatialProxy)
    {
        Configure(stableSubjectId, definition, spatialProxy);
    }
#endif
}
