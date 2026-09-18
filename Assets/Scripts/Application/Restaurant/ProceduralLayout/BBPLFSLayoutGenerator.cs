using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Layout Generator")]
public sealed class BBPLFSLayoutGenerator : MonoBehaviour
{
    [Header("Search budget")]
    [SerializeField, Min(1)] private int maxDiningSets = 40;
    [SerializeField, Min(0.1f)] private float defaultClearance = 0.75f;

    public List<BBPLFSLayoutCandidate> GenerateDiningCandidates(
        BBPLFSPremisesSpaceSnapshot space,
        BBPLFSLayoutBrief brief,
        BBPLFSAssetLayoutProfile tableProfile,
        BBPLFSAssetLayoutProfile chairProfile)
    {
        List<BBPLFSLayoutCandidate> results = new();
        if (space == null || brief == null || tableProfile == null || chairProfile == null)
        {
            return results;
        }

        float clearance = ResolveClearance(brief.GoalProfile);
        AddCandidate(results, space, brief, tableProfile, chairProfile, clearance, false, false, "rows_x");
        AddCandidate(results, space, brief, tableProfile, chairProfile, clearance, true, false, "rows_z");
        AddCandidate(results, space, brief, tableProfile, chairProfile, clearance, false, true, "staggered");
        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        return results;
    }

    private void AddCandidate(
        List<BBPLFSLayoutCandidate> results,
        BBPLFSPremisesSpaceSnapshot space,
        BBPLFSLayoutBrief brief,
        BBPLFSAssetLayoutProfile tableProfile,
        BBPLFSAssetLayoutProfile chairProfile,
        float clearance,
        bool rotatePattern,
        bool stagger,
        string id)
    {
        Bounds bounds = space.WorldBounds;
        Vector2 table = tableProfile.FastFootprint;
        Vector2 chair = chairProfile.FastFootprint;
        float setWidth = table.x + (chair.x * 2f) + (clearance * 0.6f);
        float setDepth = table.y + (chair.y * 2f) + (clearance * 0.6f);

        if (rotatePattern)
        {
            (setWidth, setDepth) = (setDepth, setWidth);
        }

        float usableWidth = Mathf.Max(0f, bounds.size.x - (clearance * 2f));
        float usableDepth = Mathf.Max(0f, bounds.size.z - (clearance * 2f));
        int columns = Mathf.Max(0, Mathf.FloorToInt(usableWidth / setWidth));
        int rows = Mathf.Max(0, Mathf.FloorToInt(usableDepth / setDepth));
        int seatsPerSet = Mathf.Clamp(tableProfile.FunctionalCapacity, 1, 4);
        int requestedSets = Mathf.CeilToInt(brief.TargetCapacity / (float)seatsPerSet);
        int setCount = Mathf.Min(requestedSets, columns * rows, maxDiningSets);

        if (setCount <= 0)
        {
            return;
        }

        List<BBPLFSLayoutPlacement> placements = new(setCount * (1 + seatsPerSet));
        float originX = bounds.min.x + clearance + (setWidth * 0.5f);
        float originZ = bounds.min.z + clearance + (setDepth * 0.5f);
        int produced = 0;

        for (int row = 0; row < rows && produced < setCount; row++)
        {
            for (int column = 0; column < columns && produced < setCount; column++)
            {
                float x = originX + (column * setWidth);
                if (stagger && (row & 1) == 1)
                {
                    x += setWidth * 0.5f;
                }

                if (x + (setWidth * 0.5f) > bounds.max.x - clearance)
                {
                    continue;
                }
                float z = originZ + (row * setDepth);
                Vector3 center = new(x, bounds.center.y, z);
                AddDiningSet(placements, center, rotatePattern, tableProfile, chairProfile, table, chair, clearance, seatsPerSet);
                produced++;
            }
        }

        if (produced <= 0)
        {
            return;
        }

        int capacity = produced * seatsPerSet;
        float capacityFit = 1f - (Mathf.Abs(brief.TargetCapacity - capacity) / (float)Mathf.Max(brief.TargetCapacity, capacity));
        float density = produced / (float)Mathf.Max(1, columns * rows);
        float score = ScoreCandidate(brief.GoalProfile, capacityFit, density, clearance);
        results.Add(new BBPLFSLayoutCandidate(id, capacity, score, placements));
    }

    private static void AddDiningSet(
        List<BBPLFSLayoutPlacement> placements,
        Vector3 center,
        bool rotatePattern,
        BBPLFSAssetLayoutProfile tableProfile,
        BBPLFSAssetLayoutProfile chairProfile,
        Vector2 table,
        Vector2 chair,
        float clearance,
        int seatCount)
    {
        Quaternion tableRotation = rotatePattern ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        placements.Add(new BBPLFSLayoutPlacement(
            tableProfile.ItemDefinition.ItemId,
            "table",
            center,
            tableRotation));

        float xOffset = (table.x * 0.5f) + (chair.x * 0.5f) + (clearance * 0.2f);
        float zOffset = (table.y * 0.5f) + (chair.y * 0.5f) + (clearance * 0.2f);
        Vector3[] offsets =
        {
            new(-xOffset, 0f, 0f),
            new(xOffset, 0f, 0f),
            new(0f, 0f, -zOffset),
            new(0f, 0f, zOffset)
        };
        float[] yaws = { 90f, -90f, 0f, 180f };
        for (int index = 0; index < Mathf.Min(seatCount, offsets.Length); index++)
        {
            Vector3 offset = rotatePattern
                ? new Vector3(offsets[index].z, 0f, -offsets[index].x)
                : offsets[index];

            float yaw = yaws[index] + (rotatePattern ? 90f : 0f);
            placements.Add(new BBPLFSLayoutPlacement(
                chairProfile.ItemDefinition.ItemId,
                "seat",
                center + offset,
                Quaternion.Euler(0f, yaw, 0f)));
        }
    }

    private float ResolveClearance(BBPLFSGoalProfile profile)
    {
        switch (profile)
        {
            case BBPLFSGoalProfile.MaxCapacity:
                return Mathf.Max(0.45f, defaultClearance * 0.7f);
            case BBPLFSGoalProfile.MaxComfort:
            case BBPLFSGoalProfile.Premium:
                return defaultClearance * 1.35f;
            case BBPLFSGoalProfile.ServiceEfficient:
                return defaultClearance * 1.15f;
            default:
                return defaultClearance;
        }
    }

    private static float ScoreCandidate(
        BBPLFSGoalProfile profile,
        float capacityFit,
        float density,
        float clearance)
    {
        float comfort = Mathf.Clamp01(clearance / 1.2f);
        float capacity = Mathf.Clamp01(capacityFit);
        float compactness = Mathf.Clamp01(density);

        return profile switch
        {
            BBPLFSGoalProfile.MaxCapacity => (capacity * 0.7f) + (compactness * 0.3f),
            BBPLFSGoalProfile.MaxComfort => (comfort * 0.65f) + (capacity * 0.35f),
            BBPLFSGoalProfile.ServiceEfficient => (comfort * 0.55f) + (capacity * 0.45f),
            BBPLFSGoalProfile.Premium => (comfort * 0.7f) + (capacity * 0.3f),
            _ => (capacity * 0.5f) + (comfort * 0.3f) + (compactness * 0.2f)
        };
    }
}
