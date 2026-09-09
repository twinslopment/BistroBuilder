using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Edit Capability Router")]
public sealed class BistroBuilderEditCapabilityRouter : MonoBehaviour, IBistroBuilderEditCapabilityProvider
{
    [SerializeField] private MonoBehaviour[] providerSources = new MonoBehaviour[0];
    private readonly List<IBistroBuilderEditCapabilityProvider> providers = new List<IBistroBuilderEditCapabilityProvider>(4);

    private void Awake() => RebuildProviders();
    private void OnEnable() => RebuildProviders();

    public void RebuildProviders()
    {
        providers.Clear();
        if (providerSources == null) return;
        for (int i = 0; i < providerSources.Length; i++)
        {
            if (!(providerSources[i] is IBistroBuilderEditCapabilityProvider provider) || providers.Contains(provider)) continue;
            providers.Add(provider);
        }
    }

    public bool TryGetCapabilities(BistroBuilderEditId entityId, out BistroBuilderEditCapability capabilities)
    {
        capabilities = BistroBuilderEditCapability.None;
        bool found = false;
        for (int i = 0; i < providers.Count; i++)
        {
            if (!providers[i].TryGetCapabilities(entityId, out BistroBuilderEditCapability current)) continue;
            capabilities |= current;
            found = true;
        }
        return found;
    }
    public bool HasCapability(BistroBuilderEditId entityId, BistroBuilderEditCapability capability)
    {
        return TryGetCapabilities(entityId, out BistroBuilderEditCapability capabilities) &&
               (capabilities & capability) == capability;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) RebuildProviders();
    }
#endif
}
