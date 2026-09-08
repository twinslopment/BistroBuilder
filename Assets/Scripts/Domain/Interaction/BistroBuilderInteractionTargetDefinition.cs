using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderInteractionSpatialBindingDefinition
{
    public BistroBuilderInteractionSpatialBindingKind bindingKind =
        BistroBuilderInteractionSpatialBindingKind.None;
    public string bindingId = string.Empty;
    public BistroBuilderSpatialClaimKind claimKind =
        BistroBuilderSpatialClaimKind.Interaction;

    public bool RequiresBbsis =>
        bindingKind != BistroBuilderInteractionSpatialBindingKind.None;
}

[Serializable]
public sealed class BistroBuilderInteractionSlotDefinition
{
    public string slotId = string.Empty;
    [Min(1)] public int capacity = 1;
    public List<string> interactionIds = new List<string>();
    public List<BistroBuilderInteractionHolderKind> allowedHolderKinds =
        new List<BistroBuilderInteractionHolderKind>();
    public BistroBuilderInteractionSpatialBindingDefinition spatialBinding =
        new BistroBuilderInteractionSpatialBindingDefinition();

    public bool AllowsInteraction(string interactionId)
    {
        return ContainsOrdinal(interactionIds, interactionId);
    }

    public bool AllowsHolder(BistroBuilderInteractionHolderKind holderKind)
    {
        return allowedHolderKinds.Count == 0 || allowedHolderKinds.Contains(holderKind);
    }

    private static bool ContainsOrdinal(List<string> source, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        for (int i = 0; i < source.Count; i++)
            if (string.Equals(source[i], value, StringComparison.Ordinal)) return true;
        return false;
    }
}
[Serializable]
public sealed class BistroBuilderInteractionChannelDefinition
{
    public string channelId = string.Empty;
    [Min(1)] public int capacity = 1;
    public bool targetExclusive;
    public List<string> interactionIds = new List<string>();
    public List<BistroBuilderInteractionHolderKind> allowedHolderKinds =
        new List<BistroBuilderInteractionHolderKind>();
    public BistroBuilderInteractionSpatialBindingDefinition spatialBinding =
        new BistroBuilderInteractionSpatialBindingDefinition();
    public List<BistroBuilderInteractionSlotDefinition> slots =
        new List<BistroBuilderInteractionSlotDefinition>();

    public bool AllowsInteraction(string interactionId)
    {
        if (string.IsNullOrWhiteSpace(interactionId)) return false;
        for (int i = 0; i < interactionIds.Count; i++)
            if (string.Equals(interactionIds[i], interactionId, StringComparison.Ordinal))
                return true;
        return false;
    }

    public bool AllowsHolder(BistroBuilderInteractionHolderKind holderKind)
    {
        return allowedHolderKinds.Count == 0 || allowedHolderKinds.Contains(holderKind);
    }

    public bool TryGetSlot(string slotId, out BistroBuilderInteractionSlotDefinition slot)
    {
        slot = null;
        if (string.IsNullOrWhiteSpace(slotId)) return false;
        for (int i = 0; i < slots.Count; i++)
        {
            BistroBuilderInteractionSlotDefinition candidate = slots[i];
            if (candidate != null && string.Equals(candidate.slotId, slotId, StringComparison.Ordinal))
            {
                slot = candidate;
                return true;
            }
        }
        return false;
    }
}
/// <summary>
/// Perfil componible reutilizable entre assets de la misma semántica.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_InteractionProfile",
    menuName = "Bistro Builder/Interaction/Interaction Profile")]
public sealed class BistroBuilderInteractionProfileDefinition : ScriptableObject
{
    [SerializeField] private string profileId = string.Empty;
    [SerializeField] private List<BistroBuilderInteractionChannelDefinition> channels =
        new List<BistroBuilderInteractionChannelDefinition>();

    public string ProfileId => profileId;
    public IReadOnlyList<BistroBuilderInteractionChannelDefinition> Channels => channels;

    public bool ValidateProfile(out string error)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            error = name + ": profileId vacío.";
            return false;
        }
        return BistroBuilderInteractionDefinitionValidation.ValidateChannels(
            profileId, channels, out error);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string stableProfileId,
        IEnumerable<BistroBuilderInteractionChannelDefinition> definitions)
    {
        profileId = stableProfileId ?? string.Empty;
        channels.Clear();
        if (definitions == null) return;
        foreach (BistroBuilderInteractionChannelDefinition definition in definitions)
            if (definition != null) channels.Add(definition);
    }
#endif
}

/// <summary>
/// Contrato lógico universal de un target interactuable.
/// La geometría vive exclusivamente en BBSIS.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_InteractionTarget",
    menuName = "Bistro Builder/Interaction/Target Definition")]
public sealed class BistroBuilderInteractionTargetDefinition : ScriptableObject
{
    [SerializeField] private string definitionId = string.Empty;
    [SerializeField] private List<BistroBuilderInteractionProfileDefinition> profiles =
        new List<BistroBuilderInteractionProfileDefinition>();
    [SerializeField] private List<BistroBuilderInteractionChannelDefinition> channels =
        new List<BistroBuilderInteractionChannelDefinition>();
    public string DefinitionId => definitionId;
    public IReadOnlyList<BistroBuilderInteractionProfileDefinition> Profiles => profiles;
    public IReadOnlyList<BistroBuilderInteractionChannelDefinition> Channels => channels;

    public bool TryGetChannel(
        string channelId,
        out BistroBuilderInteractionChannelDefinition channel)
    {
        channel = null;
        if (string.IsNullOrWhiteSpace(channelId)) return false;
        if (TryFindChannel(channels, channelId, out channel)) return true;
        for (int i = 0; i < profiles.Count; i++)
        {
            BistroBuilderInteractionProfileDefinition profile = profiles[i];
            if (profile == null) continue;
            IReadOnlyList<BistroBuilderInteractionChannelDefinition> profileChannels = profile.Channels;
            for (int c = 0; c < profileChannels.Count; c++)
            {
                BistroBuilderInteractionChannelDefinition candidate = profileChannels[c];
                if (candidate != null && string.Equals(
                        candidate.channelId, channelId, StringComparison.Ordinal))
                {
                    channel = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    public bool ValidateDefinition(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            error = name + ": definitionId vacío.";
            return false;
        }
        var all = new List<BistroBuilderInteractionChannelDefinition>();
        for (int i = 0; i < profiles.Count; i++)
        {
            BistroBuilderInteractionProfileDefinition profile = profiles[i];
            if (profile == null || !profile.ValidateProfile(out error)) return false;
            for (int c = 0; c < profile.Channels.Count; c++)
                all.Add(profile.Channels[c]);
        }
        all.AddRange(channels);
        return BistroBuilderInteractionDefinitionValidation.ValidateChannels(
            definitionId, all, out error);
    }
    private static bool TryFindChannel(
        List<BistroBuilderInteractionChannelDefinition> source,
        string channelId,
        out BistroBuilderInteractionChannelDefinition channel)
    {
        for (int i = 0; i < source.Count; i++)
        {
            BistroBuilderInteractionChannelDefinition candidate = source[i];
            if (candidate != null && string.Equals(
                    candidate.channelId, channelId, StringComparison.Ordinal))
            {
                channel = candidate;
                return true;
            }
        }
        channel = null;
        return false;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string stableDefinitionId,
        IEnumerable<BistroBuilderInteractionProfileDefinition> profileDefinitions,
        IEnumerable<BistroBuilderInteractionChannelDefinition> localChannels)
    {
        definitionId = stableDefinitionId ?? string.Empty;
        profiles.Clear();
        channels.Clear();
        if (profileDefinitions != null)
            foreach (BistroBuilderInteractionProfileDefinition profile in profileDefinitions)
                if (profile != null) profiles.Add(profile);
        if (localChannels != null)
            foreach (BistroBuilderInteractionChannelDefinition channel in localChannels)
                if (channel != null) channels.Add(channel);
    }
#endif
}

internal static class BistroBuilderInteractionDefinitionValidation
{
    public static bool ValidateChannels(
        string ownerId,
        IList<BistroBuilderInteractionChannelDefinition> channels,
        out string error)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < channels.Count; i++)
        {
            BistroBuilderInteractionChannelDefinition channel = channels[i];
            if (channel == null || string.IsNullOrWhiteSpace(channel.channelId) ||
                channel.capacity < 1 || !ids.Add("channel:" + channel.channelId))
            {
                error = ownerId + ": channel inválido o duplicado.";
                return false;
            }
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            for (int s = 0; s < channel.slots.Count; s++)
            {
                BistroBuilderInteractionSlotDefinition slot = channel.slots[s];
                if (slot == null || string.IsNullOrWhiteSpace(slot.slotId) ||
                    slot.capacity < 1 || !slotIds.Add(slot.slotId))
                {
                    error = ownerId + ": slot inválido o duplicado en " + channel.channelId + ".";
                    return false;
                }
                if (slot.interactionIds.Count == 0)
                {
                    error = ownerId + ": slot " + slot.slotId + " sin InteractionId.";
                    return false;
                }
                if (!ValidateBinding(ownerId, slot.spatialBinding, out error)) return false;
            }
            if (channel.interactionIds.Count == 0 && channel.slots.Count == 0)
            {
                error = ownerId + ": channel " + channel.channelId + " no ofrece interacciones.";
                return false;
            }
            if (!ValidateBinding(ownerId, channel.spatialBinding, out error)) return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool ValidateBinding(
        string ownerId,
        BistroBuilderInteractionSpatialBindingDefinition binding,
        out string error)
    {
        error = string.Empty;
        if (binding == null || !binding.RequiresBbsis) return true;
        if (string.IsNullOrWhiteSpace(binding.bindingId))
        {
            error = ownerId + ": binding BBSIS sin ID.";
            return false;
        }
        return true;
    }
}