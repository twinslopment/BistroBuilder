using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Furnishing Set Catalog")]
public sealed class BBPLFSFurnishingSetCatalog : MonoBehaviour
{
    [SerializeField] private BBPLFSFurnishingSet[] sets = Array.Empty<BBPLFSFurnishingSet>();
    public IReadOnlyList<BBPLFSFurnishingSet> Sets => sets;

    public int CollectForFunction(BBPLFSSpaceFunction function, List<BBPLFSFurnishingSet> results)
    {
        if (results == null) throw new ArgumentNullException(nameof(results));
        results.Clear();
        for (int i = 0; i < sets.Length; i++)
            if (sets[i] != null && sets[i].SpaceFunction == function) results.Add(sets[i]);
        results.Sort((a, b) =>
        {
            int byCapacity = a.FunctionalCapacity.CompareTo(b.FunctionalCapacity);
            return byCapacity != 0 ? byCapacity : string.CompareOrdinal(a.SetId, b.SetId);
        });
        return results.Count;
    }

#if UNITY_EDITOR
    public void EditorSetSets(BBPLFSFurnishingSet[] configuredSets)
    {
        sets = configuredSets ?? Array.Empty<BBPLFSFurnishingSet>();
    }
#endif
}
