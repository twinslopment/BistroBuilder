using System;
using UnityEngine;

/// <summary>
/// Instancia runtime de un Interaction Target. Declara identidad y semántica;
/// no almacena geometría ni rutas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderInteractionTarget : MonoBehaviour
{
    [SerializeField] private string targetId = string.Empty;
    [SerializeField] private BistroBuilderInteractionTargetDefinition definition;
    [SerializeField] private BistroBuilderSpatialSubject spatialSubject;
    [SerializeField] private BistroBuilderInteractionTargetAdmissionState admissionState =
        BistroBuilderInteractionTargetAdmissionState.Open;

    private BistroBuilderInteractionService service;

    public string TargetId => targetId;
    public BistroBuilderInteractionTargetDefinition Definition => definition;
    public BistroBuilderSpatialSubject SpatialSubject => spatialSubject;
    public BistroBuilderInteractionTargetAdmissionState AdmissionState => admissionState;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        service?.RegisterTarget(this);
    }

    private void OnDisable()
    {
        service?.UnregisterTarget(this, BistroBuilderInteractionReasonCode.TargetInvalidated);
    }

    public void SetAdmissionState(BistroBuilderInteractionTargetAdmissionState state)
    {
        if (admissionState == state) return;
        admissionState = state;
        CacheReferences();
        service?.NotifyTargetAdmissionChanged(this);
    }

    public bool ValidateTarget(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            error = name + ": targetId lógico vacío.";
            return false;
        }
        if (definition == null || !definition.ValidateDefinition(out error))
        {
            if (definition == null) error = targetId + ": falta Target Definition.";
            return false;
        }

        bool requiresSpatial = false;
        for (int p = 0; p < definition.Profiles.Count; p++)
        {
            BistroBuilderInteractionProfileDefinition profile = definition.Profiles[p];
            if (profile == null) continue;
            for (int c = 0; c < profile.Channels.Count; c++)
                requiresSpatial |= ChannelRequiresSpatial(profile.Channels[c]);
        }
        for (int c = 0; c < definition.Channels.Count; c++)
            requiresSpatial |= ChannelRequiresSpatial(definition.Channels[c]);

        if (requiresSpatial && spatialSubject == null)
        {
            error = targetId + ": usa bindings BBSIS pero no tiene Spatial Subject.";
            return false;
        }
        if (spatialSubject != null && !ValidateBindingsAgainstBbsis(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryResolve(
        string channelId,
        string slotId,
        string interactionId,
        BistroBuilderInteractionHolderKind holderKind,
        out BistroBuilderInteractionChannelDefinition channel,
        out BistroBuilderInteractionSlotDefinition slot,
        out BistroBuilderInteractionSpatialBindingDefinition binding,
        out int capacity,
        out BistroBuilderInteractionReasonCode reason)
    {
        channel = null;
        slot = null;
        binding = null;
        capacity = 0;
        reason = BistroBuilderInteractionReasonCode.None;
        if (admissionState == BistroBuilderInteractionTargetAdmissionState.Closed)
        {
            channel = null;
            reason = BistroBuilderInteractionReasonCode.TargetClosed;
            return false;
        }
        if (admissionState == BistroBuilderInteractionTargetAdmissionState.Draining)
        {
            channel = null;
            reason = BistroBuilderInteractionReasonCode.TargetDraining;
            return false;
        }
        if (definition == null || !definition.TryGetChannel(channelId, out channel) || channel == null)
        {
            reason = BistroBuilderInteractionReasonCode.TargetUnavailable;
            return false;
        }
        if (!string.IsNullOrWhiteSpace(slotId))
        {
            if (!channel.TryGetSlot(slotId, out slot) || slot == null)
            {
                reason = BistroBuilderInteractionReasonCode.SlotUnavailable;
                return false;
            }
            if (!slot.AllowsInteraction(interactionId) || !slot.AllowsHolder(holderKind))
            {
                reason = BistroBuilderInteractionReasonCode.NotEligible;
                return false;
            }
            capacity = Mathf.Max(1, slot.capacity);
            binding = slot.spatialBinding != null && slot.spatialBinding.RequiresBbsis
                ? slot.spatialBinding
                : channel.spatialBinding;
            return true;
        }
        if (!channel.AllowsInteraction(interactionId) || !channel.AllowsHolder(holderKind))
        {
            reason = BistroBuilderInteractionReasonCode.NotEligible;
            return false;
        }
        capacity = Mathf.Max(1, channel.capacity);
        binding = channel.spatialBinding;
        return true;
    }

    public bool EvaluateConditions(
        in BistroBuilderInteractionConditionContext context,
        out BistroBuilderInteractionReasonCode reason)
    {
        reason = BistroBuilderInteractionReasonCode.None;
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is IBistroBuilderInteractionConditionProvider provider))
                continue;
            if (provider.Evaluate(in context, out reason)) continue;
            if (reason == BistroBuilderInteractionReasonCode.None)
                reason = BistroBuilderInteractionReasonCode.NotEligible;
            return false;
        }
        return true;
    }
    private bool ValidateBindingsAgainstBbsis(out string error)
    {
        error = string.Empty;
        if (spatialSubject == null || spatialSubject.Contract == null) return true;
        for (int p = 0; p < definition.Profiles.Count; p++)
        {
            BistroBuilderInteractionProfileDefinition profile = definition.Profiles[p];
            if (profile == null) continue;
            for (int c = 0; c < profile.Channels.Count; c++)
                if (!ValidateChannelBinding(profile.Channels[c], out error)) return false;
        }
        for (int c = 0; c < definition.Channels.Count; c++)
            if (!ValidateChannelBinding(definition.Channels[c], out error)) return false;
        return true;
    }

    private bool ValidateChannelBinding(
        BistroBuilderInteractionChannelDefinition channel,
        out string error)
    {
        error = string.Empty;
        if (channel == null) return true;
        if (!ValidateBinding(channel.spatialBinding, out error)) return false;
        for (int i = 0; i < channel.slots.Count; i++)
        {
            BistroBuilderInteractionSlotDefinition slot = channel.slots[i];
            if (slot != null && !ValidateBinding(slot.spatialBinding, out error)) return false;
        }
        return true;
    }

    private bool ValidateBinding(
        BistroBuilderInteractionSpatialBindingDefinition binding,
        out string error)
    {
        error = string.Empty;
        if (binding == null || !binding.RequiresBbsis) return true;
        if (spatialSubject == null || spatialSubject.Contract == null)
        {
            error = targetId + ": binding BBSIS sin Spatial Subject válido.";
            return false;
        }
        bool found = binding.bindingKind == BistroBuilderInteractionSpatialBindingKind.Port
            ? HasPort(binding.bindingId)
            : HasWorkEdge(binding.bindingId);
        if (found) return true;
        error = targetId + ": binding BBSIS inexistente: " + binding.bindingId + ".";
        return false;
    }

    private bool HasPort(string portId)
    {
        for (int i = 0; i < spatialSubject.Contract.Ports.Count; i++)
            if (spatialSubject.Contract.Ports[i] != null && string.Equals(
                    spatialSubject.Contract.Ports[i].portId, portId, StringComparison.Ordinal))
                return true;
        return false;
    }
    private bool HasWorkEdge(string edgeId)
    {
        for (int i = 0; i < spatialSubject.Contract.WorkEdges.Count; i++)
            if (spatialSubject.Contract.WorkEdges[i] != null && string.Equals(
                    spatialSubject.Contract.WorkEdges[i].edgeId, edgeId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static bool ChannelRequiresSpatial(BistroBuilderInteractionChannelDefinition channel)
    {
        if (channel == null) return false;
        if (channel.spatialBinding != null && channel.spatialBinding.RequiresBbsis) return true;
        for (int i = 0; i < channel.slots.Count; i++)
        {
            BistroBuilderInteractionSlotDefinition slot = channel.slots[i];
            if (slot != null && slot.spatialBinding != null && slot.spatialBinding.RequiresBbsis)
                return true;
        }
        return false;
    }

    private void CacheReferences()
    {
        if (spatialSubject == null) spatialSubject = GetComponent<BistroBuilderSpatialSubject>();
        if (service == null) service = FindFirstObjectByType<BistroBuilderInteractionService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string stableTargetId,
        BistroBuilderInteractionTargetDefinition targetDefinition,
        BistroBuilderSpatialSubject subject)
    {
        targetId = stableTargetId ?? string.Empty;
        definition = targetDefinition;
        spatialSubject = subject != null ? subject : GetComponent<BistroBuilderSpatialSubject>();
    }
#endif
}