using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Premises Capture Service")]
public sealed class BBPLFSPremisesCaptureService : MonoBehaviour
{
    [SerializeField] private RestaurantAreaRegistry areaRegistry;

    private void Awake()
    {
        if (areaRegistry == null)
        {
            areaRegistry = FindFirstObjectByType<RestaurantAreaRegistry>();
        }
    }

    public BBPLFSPremisesModel CaptureAll()
    {
        List<BBPLFSPremisesSpaceSnapshot> spaces = new();

        if (areaRegistry != null)
        {
            foreach (RestaurantArea area in areaRegistry.RegisteredAreas)
            {
                if (TryCreateSnapshot(area, out BBPLFSPremisesSpaceSnapshot snapshot))
                {
                    spaces.Add(snapshot);
                }
            }
        }
        spaces.Sort((a, b) => string.CompareOrdinal(a.SpaceId, b.SpaceId));
        return new BBPLFSPremisesModel(BuildRevision(spaces), spaces);
    }

    public bool TryCaptureArea(
        RestaurantArea area,
        out BBPLFSPremisesSpaceSnapshot snapshot)
    {
        return TryCreateSnapshot(area, out snapshot);
    }

    private static bool TryCreateSnapshot(
        RestaurantArea area,
        out BBPLFSPremisesSpaceSnapshot snapshot)
    {
        snapshot = null;

        if (area == null || area.BoundaryColliders == null)
        {
            return false;
        }

        bool hasBounds = false;
        Bounds bounds = default;

        for (int index = 0; index < area.BoundaryColliders.Count; index++)
        {
            Collider collider = area.BoundaryColliders[index];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
        if (!hasBounds)
        {
            return false;
        }

        snapshot = new BBPLFSPremisesSpaceSnapshot(area, bounds);
        return true;
    }

    private static string BuildRevision(
        IReadOnlyList<BBPLFSPremisesSpaceSnapshot> spaces)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < spaces.Count; index++)
            {
                BBPLFSPremisesSpaceSnapshot space = spaces[index];
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(space.SpaceId ?? string.Empty);
                hash = (hash * 31) + space.WorldBounds.center.GetHashCode();
                hash = (hash * 31) + space.WorldBounds.size.GetHashCode();
            }

            return "premises_" + hash.ToString("X8");
        }
    }
}