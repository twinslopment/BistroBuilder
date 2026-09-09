using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Asset Layout Catalog")]
public sealed class BBPLFSAssetLayoutCatalog : MonoBehaviour
{
    [SerializeField] private BBPLFSAssetLayoutProfile[] profiles = Array.Empty<BBPLFSAssetLayoutProfile>();

    public IReadOnlyList<BBPLFSAssetLayoutProfile> Profiles => profiles;

    public bool TryGetFirst(
        BBPLFSLayoutRole role,
        out BBPLFSAssetLayoutProfile profile)
    {
        profile = null;
        if (profiles == null)
        {
            return false;
        }
        for (int index = 0; index < profiles.Length; index++)
        {
            BBPLFSAssetLayoutProfile candidate = profiles[index];
            if (candidate == null || !candidate.IsUsable || candidate.LayoutRole != role)
            {
                continue;
            }

            if (profile == null || string.CompareOrdinal(
                    candidate.ItemDefinition.ItemId,
                    profile.ItemDefinition.ItemId) < 0)
            {
                profile = candidate;
            }
        }

        return profile != null;
    }

#if UNITY_EDITOR
    public void EditorSetProfiles(BBPLFSAssetLayoutProfile[] configuredProfiles)
    {
        profiles = configuredProfiles ?? Array.Empty<BBPLFSAssetLayoutProfile>();
    }
#endif
}