using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Advanced Layout Generator")]
public sealed class BBPLFSAdvancedLayoutGenerator : MonoBehaviour
{
    [SerializeField, Min(4)] private int maxUnitsPerCandidate = 48;
    [SerializeField, Min(16)] private int maxAnchorsPerPattern = 320;
    [SerializeField, Min(0.05f)] private float interSetClearance = 0.35f;
    [SerializeField, Range(1, 5)] private int maxReturnedCandidates = 5;

    private readonly List<BBPLFSFurnishingSet> setBuffer = new();
    private readonly List<RestaurantPlacementShape> existingShapes = new();
    private readonly List<RestaurantPlacementShape> candidateShapes = new();
    private readonly List<Vector3> anchors = new();
    private readonly List<RestaurantTableSeatSlot> seatSlots = new();

    public List<BBPLFSLayoutCandidate> GenerateCandidates(
        BBPLFSPremisesSpaceSnapshot space,
        BBPLFSLayoutBrief brief,
        BBPLFSAssetLayoutCatalog assetCatalog,
        BBPLFSFurnishingSetCatalog setCatalog)
    {
        var results = new List<BBPLFSLayoutCandidate>();
        if (space == null || brief == null || space.SourceArea == null || assetCatalog == null || setCatalog == null)
            return results;

        setCatalog.CollectForFunction(brief.SpaceFunction, setBuffer);
        if (setBuffer.Count == 0) return results;

        CollectExistingShapes(space.SourceArea);
        int existingCapacity = brief.SpaceFunction == BBPLFSSpaceFunction.Dining
            ? CountExistingDiningCapacity(space.SourceArea) : 0;
        int remainingCapacity = Mathf.Max(0, brief.TargetCapacity - existingCapacity);

        BBPLFSFurnishingSet selectedSet = SelectBestSet(setBuffer, remainingCapacity, assetCatalog, brief);
        if (selectedSet == null) return results;
        if (!TryResolveSetProfiles(selectedSet, assetCatalog, brief, out var profiles)) return results;

        var patterns = ResolvePatterns(selectedSet);
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        for (int patternIndex = 0; patternIndex < patterns.Count && results.Count < maxReturnedCandidates; patternIndex++)
        {
            BBPLFSPlacementPattern pattern = patterns[patternIndex];
            Quaternion rotation = PatternRotation(pattern, patternIndex);
            if (!TryMeasureUnit(selectedSet, profiles, rotation, out Vector2 unitSize)) continue;
            BuildAnchors(space.WorldBounds, unitSize, pattern, anchors);

            var placements = new List<BBPLFSLayoutPlacement>(128);
            candidateShapes.Clear();
            int generatedCapacity = 0;
            int requestedUnits = selectedSet.FunctionalCapacity > 0
                ? Mathf.CeilToInt(remainingCapacity / (float)selectedSet.FunctionalCapacity)
                : Mathf.Min(12, maxUnitsPerCandidate);
            requestedUnits = Mathf.Clamp(requestedUnits, remainingCapacity == 0 ? 1 : 1, maxUnitsPerCandidate);

            for (int anchorIndex = 0; anchorIndex < anchors.Count && generatedCapacity < requestedUnits * Mathf.Max(1, selectedSet.FunctionalCapacity); anchorIndex++)
            {
                if (TryBuildUnit(selectedSet, profiles, anchors[anchorIndex], rotation,
                        space.SourceArea, placements, candidateShapes))
                    generatedCapacity += selectedSet.FunctionalCapacity;
            }

            if (placements.Count == 0) continue;
            int totalCapacity = existingCapacity + generatedCapacity;
            string fingerprint = BuildFingerprint(placements);
            if (!fingerprints.Add(fingerprint)) continue;
            float score = Score(brief, totalCapacity, placements, profiles, pattern, unitSize);
            results.Add(new BBPLFSLayoutCandidate(
                "v1_" + pattern.ToString().ToLowerInvariant() + "_" + results.Count,
                totalCapacity, score, placements));
        }

        results.Sort((a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.CandidateId, b.CandidateId);
        });
        return results;
    }

    private BBPLFSFurnishingSet SelectBestSet(List<BBPLFSFurnishingSet> sets, int remaining,
        BBPLFSAssetLayoutCatalog catalog, BBPLFSLayoutBrief brief)
    {
        BBPLFSFurnishingSet best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < sets.Count; i++)
        {
            BBPLFSFurnishingSet set = sets[i];
            if (!TryResolveSetProfiles(set, catalog, brief, out _)) continue;
            float capacityScore = set.FunctionalCapacity <= 0 ? 0.5f :
                1f - Mathf.Clamp01(Mathf.Abs(remaining - set.FunctionalCapacity) / (float)Mathf.Max(1, remaining));
            float score = capacityScore + StyleSetScore(set, brief) * 0.25f;
            if (score > bestScore || (Mathf.Approximately(score, bestScore) &&
                (best == null || string.CompareOrdinal(set.SetId, best.SetId) < 0)))
            {
                best = set;
                bestScore = score;
            }
        }
        return best;
    }

    private bool TryResolveSetProfiles(BBPLFSFurnishingSet set, BBPLFSAssetLayoutCatalog catalog,
        BBPLFSLayoutBrief brief, out Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> resolved)
    {
        resolved = new Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile>();
        for (int slotIndex = 0; slotIndex < set.Slots.Count; slotIndex++)
        {
            BBPLFSFurnishingSlot slot = set.Slots[slotIndex];
            if (resolved.ContainsKey(slot.Role)) continue;
            BBPLFSAssetLayoutProfile best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < catalog.Profiles.Count; i++)
            {
                BBPLFSAssetLayoutProfile profile = catalog.Profiles[i];
                if (profile == null || !profile.IsUsable || profile.LayoutRole != slot.Role) continue;
                if (slot.Role == BBPLFSLayoutRole.DiningTable && set.FunctionalCapacity > 0 &&
                    profile.FunctionalCapacity < set.FunctionalCapacity) continue;
                float score = StyleProfileScore(profile, brief) - profile.ItemDefinition.PurchasePriceCents / 10000000f;
                if (score > bestScore || (Mathf.Approximately(score, bestScore) &&
                    (best == null || string.CompareOrdinal(profile.ItemDefinition.ItemId, best.ItemDefinition.ItemId) < 0)))
                {
                    best = profile;
                    bestScore = score;
                }
            }
            if (best == null && slot.Required) return false;
            if (best != null) resolved.Add(slot.Role, best);
        }
        return resolved.Count > 0;
    }

    private bool TryBuildUnit(BBPLFSFurnishingSet set,
        Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> profiles,
        Vector3 anchor, Quaternion rotation, RestaurantArea area,
        List<BBPLFSLayoutPlacement> output, List<RestaurantPlacementShape> shapes)
    {
        var unitPlacements = new List<BBPLFSLayoutPlacement>(16);
        var unitShapes = new List<RestaurantPlacementShape>(16);
        if (set.SpaceFunction == BBPLFSSpaceFunction.Dining &&
            profiles.TryGetValue(BBPLFSLayoutRole.DiningTable, out var tableProfile) &&
            profiles.TryGetValue(BBPLFSLayoutRole.DiningSeat, out var seatProfile))
        {
            if (!BuildDiningUnit(set, tableProfile, seatProfile, anchor, rotation, unitPlacements)) return false;
        }
        else
        {
            BuildLinearUnit(set, profiles, anchor, rotation, unitPlacements);
        }
        if (unitPlacements.Count == 0) return false;

        for (int i = 0; i < unitPlacements.Count; i++)
        {
            BBPLFSLayoutPlacement placement = unitPlacements[i];
            if (!TryFindProfileByItemId(profiles, placement.ItemId, out var profile)) return false;
            RestaurantPlacementShape shape = BuildShape(profile, placement.WorldPosition, placement.WorldRotation);
            if (!ShapeInsideArea(shape, area) || Conflicts(shape, existingShapes) || Conflicts(shape, shapes) || Conflicts(shape, unitShapes))
                return false;
            unitShapes.Add(shape);
        }
        output.AddRange(unitPlacements);
        shapes.AddRange(unitShapes);
        return true;
    }

    private bool BuildDiningUnit(BBPLFSFurnishingSet set, BBPLFSAssetLayoutProfile tableProfile,
        BBPLFSAssetLayoutProfile seatProfile, Vector3 anchor, Quaternion rotation,
        List<BBPLFSLayoutPlacement> output)
    {
        RestaurantPlaceableObject tablePrefab = tableProfile.ItemDefinition.Prefab;
        RestaurantPlaceableObject seatPrefab = seatProfile.ItemDefinition.Prefab;
        if (tablePrefab == null || seatPrefab == null ||
            !tablePrefab.TryGetComponent(out RestaurantTableSeatingConfiguration seating) ||
            !seatPrefab.TryGetComponent(out RestaurantSeat seat)) return false;
        Vector3 tableRootPosition = BBPLFSPlacementPoseUtility.RootPositionForSurfaceAnchor(
            tablePrefab, anchor, rotation);
        int slotCount = seating.WriteSlotsAtPose(tableRootPosition, rotation, seatSlots);
        int wanted = Mathf.Min(set.FunctionalCapacity, slotCount);
        if (wanted <= 0) return false;
        output.Add(new BBPLFSLayoutPlacement(tableProfile.ItemDefinition.ItemId, "table", tableRootPosition, rotation));
        for (int i = 0; i < wanted; i++)
        {
            RestaurantTableSeatSlot slot = seatSlots[i];
            Quaternion seatRotation = seat.CalculateRootRotationForFacingDirection(slot.FacingDirection);
            Vector3 seatPosition = seat.CalculateRootPositionForAssociationAtPose(slot.AssociationPosition, seatRotation);
            output.Add(new BBPLFSLayoutPlacement(seatProfile.ItemDefinition.ItemId, "seat", seatPosition, seatRotation));
        }
        return true;
    }

    private void BuildLinearUnit(BBPLFSFurnishingSet set,
        Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> profiles,
        Vector3 anchor, Quaternion rotation, List<BBPLFSLayoutPlacement> output)
    {
        float cursor = 0f;
        for (int slotIndex = 0; slotIndex < set.Slots.Count; slotIndex++)
        {
            BBPLFSFurnishingSlot slot = set.Slots[slotIndex];
            if (!profiles.TryGetValue(slot.Role, out var profile)) continue;
            for (int count = 0; count < slot.Count; count++)
            {
                Vector2 size = profile.FastFootprint;
                Vector3 offset = rotation * new Vector3(cursor + size.x * 0.5f, 0f, 0f);
                Vector3 surfaceAnchor = anchor + offset;
                Vector3 rootPosition = BBPLFSPlacementPoseUtility.RootPositionForSurfaceAnchor(
                    profile.ItemDefinition.Prefab, surfaceAnchor, rotation);
                output.Add(new BBPLFSLayoutPlacement(profile.ItemDefinition.ItemId,
                    slot.Role.ToString().ToLowerInvariant(), rootPosition, rotation));
                cursor += size.x + interSetClearance;
            }
        }
    }

    private bool TryMeasureUnit(BBPLFSFurnishingSet set,
        Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> profiles,
        Quaternion rotation, out Vector2 size)
    {
        var unit = new List<BBPLFSLayoutPlacement>(16);
        if (set.SpaceFunction == BBPLFSSpaceFunction.Dining &&
            profiles.TryGetValue(BBPLFSLayoutRole.DiningTable, out var table) &&
            profiles.TryGetValue(BBPLFSLayoutRole.DiningSeat, out var seat))
        {
            if (!BuildDiningUnit(set, table, seat, Vector3.zero, rotation, unit))
            { size = Vector2.zero; return false; }
        }
        else BuildLinearUnit(set, profiles, Vector3.zero, rotation, unit);
        if (unit.Count == 0) { size = Vector2.zero; return false; }
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        for (int i = 0; i < unit.Count; i++)
        {
            if (!TryFindProfileByItemId(profiles, unit[i].ItemId, out var profile)) continue;
            RestaurantPlacementShape shape = BuildShape(profile, unit[i].WorldPosition, unit[i].WorldRotation);
            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = shape.GetCorner((c == 0 || c == 3) ? 1 : -1, c < 2 ? 1 : -1);
                minX = Mathf.Min(minX, corner.x); maxX = Mathf.Max(maxX, corner.x);
                minZ = Mathf.Min(minZ, corner.z); maxZ = Mathf.Max(maxZ, corner.z);
            }
        }
        size = new Vector2(Mathf.Max(0.5f, maxX - minX), Mathf.Max(0.5f, maxZ - minZ));
        return true;
    }

    private void BuildAnchors(Bounds bounds, Vector2 unitSize, BBPLFSPlacementPattern pattern, List<Vector3> results)
    {
        results.Clear();
        float sx = Mathf.Max(0.5f, unitSize.x + interSetClearance);
        float sz = Mathf.Max(0.5f, unitSize.y + interSetClearance);
        float minX = bounds.min.x + sx * 0.5f, maxX = bounds.max.x - sx * 0.5f;
        float minZ = bounds.min.z + sz * 0.5f, maxZ = bounds.max.z - sz * 0.5f;
        Vector3 center = bounds.center; center.y = bounds.min.y;
        if (pattern == BBPLFSPlacementPattern.CenterAxis)
        {
            for (float z = minZ; z <= maxZ && results.Count < maxAnchorsPerPattern; z += sz)
            {
                results.Add(new Vector3(center.x, center.y, z));
                for (int side = 1; side <= 6 && results.Count < maxAnchorsPerPattern; side++)
                {
                    float dx = side * sx;
                    if (center.x - dx >= minX) results.Add(new Vector3(center.x - dx, center.y, z));
                    if (center.x + dx <= maxX) results.Add(new Vector3(center.x + dx, center.y, z));
                }
            }
            return;
        }
        if (pattern == BBPLFSPlacementPattern.WallBand || pattern == BBPLFSPlacementPattern.Perimeter)
        {
            for (float x = minX; x <= maxX && results.Count < maxAnchorsPerPattern; x += sx)
            { results.Add(new Vector3(x, center.y, minZ)); if (maxZ > minZ) results.Add(new Vector3(x, center.y, maxZ)); }
            for (float z = minZ + sz; z < maxZ && results.Count < maxAnchorsPerPattern; z += sz)
            { results.Add(new Vector3(minX, center.y, z)); if (maxX > minX) results.Add(new Vector3(maxX, center.y, z)); }
            return;
        }
        int row = 0;
        for (float z = minZ; z <= maxZ && results.Count < maxAnchorsPerPattern; z += sz, row++)
        {
            float stagger = pattern == BBPLFSPlacementPattern.Staggered && (row & 1) == 1 ? sx * 0.5f : 0f;
            for (float x = minX + stagger; x <= maxX && results.Count < maxAnchorsPerPattern; x += sx)
                results.Add(new Vector3(x, center.y, z));
        }
    }

    private void CollectExistingShapes(RestaurantArea area)
    {
        existingShapes.Clear();
        RestaurantPlacementFootprint[] footprints = FindObjectsByType<RestaurantPlacementFootprint>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < footprints.Length; i++)
        {
            RestaurantPlacementFootprint footprint = footprints[i];
            if (footprint == null || !footprint.BlocksOtherPlacements ||
                !footprint.TryGetComponent(out RestaurantAreaMember member)) continue;
            if (member.AssignedArea == area || (member.AssignedArea == null && area.ContainsPosition(member.ReferencePosition)))
                existingShapes.Add(footprint.BuildCurrentShape());
        }
    }

    private static int CountExistingDiningCapacity(RestaurantArea area)
    {
        int capacity = 0;
        RestaurantTable[] tables = FindObjectsByType<RestaurantTable>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < tables.Length; i++)
            if (tables[i] != null && tables[i].TryGetComponent(out RestaurantAreaMember member) &&
                (member.AssignedArea == area || (member.AssignedArea == null && area.ContainsPosition(member.ReferencePosition))))
                capacity += Mathf.Max(0, tables[i].Capacity);
        return capacity;
    }

    private static RestaurantPlacementShape BuildShape(BBPLFSAssetLayoutProfile profile, Vector3 position, Quaternion rotation)
    {
        if (profile.ItemDefinition.Prefab != null && profile.ItemDefinition.Prefab.TryGetComponent(out RestaurantPlacementFootprint footprint))
            return footprint.BuildShapeAtPose(position, rotation);
        Vector2 size = profile.FastFootprint;
        return new RestaurantPlacementShape(position, rotation * Vector3.right, rotation * Vector3.forward,
            size * 0.5f, 0f);
    }

    private static bool ShapeInsideArea(RestaurantPlacementShape shape, RestaurantArea area)
    {
        if (!area.ContainsPosition(shape.Center)) return false;
        return area.ContainsPosition(shape.GetCorner(1, 1)) && area.ContainsPosition(shape.GetCorner(-1, 1)) &&
               area.ContainsPosition(shape.GetCorner(-1, -1)) && area.ContainsPosition(shape.GetCorner(1, -1));
    }

    private static bool Conflicts(RestaurantPlacementShape shape, List<RestaurantPlacementShape> others)
    {
        for (int i = 0; i < others.Count; i++)
            if (RestaurantPlacementCollisionUtility.EvaluateConflict(shape, others[i]) != RestaurantPlacementConflictType.None)
                return true;
        return false;
    }

    private static bool TryFindProfileByItemId(Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> profiles,
        string itemId, out BBPLFSAssetLayoutProfile found)
    {
        foreach (var pair in profiles)
            if (pair.Value != null && string.Equals(pair.Value.ItemDefinition.ItemId, itemId, StringComparison.Ordinal))
            { found = pair.Value; return true; }
        found = null; return false;
    }

    private static List<BBPLFSPlacementPattern> ResolvePatterns(BBPLFSFurnishingSet set)
    {
        var result = new List<BBPLFSPlacementPattern>();
        for (int i = 0; i < set.AllowedPatterns.Count; i++) if (!result.Contains(set.AllowedPatterns[i])) result.Add(set.AllowedPatterns[i]);
        if (result.Count == 0) result.Add(BBPLFSPlacementPattern.InteriorGrid);
        return result;
    }

    private static Quaternion PatternRotation(BBPLFSPlacementPattern pattern, int index)
    {
        if (pattern == BBPLFSPlacementPattern.WallBand || pattern == BBPLFSPlacementPattern.Perimeter) return Quaternion.identity;
        return index % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
    }

    private static float StyleProfileScore(BBPLFSAssetLayoutProfile profile, BBPLFSLayoutBrief brief)
    {
        if (brief.StyleTags.Count == 0) return 0.5f;
        int hits = 0;
        for (int i = 0; i < brief.StyleTags.Count; i++)
            for (int j = 0; j < profile.StyleTags.Count; j++)
                if (string.Equals(brief.StyleTags[i], profile.StyleTags[j], StringComparison.OrdinalIgnoreCase)) { hits++; break; }
        return hits / (float)Mathf.Max(1, brief.StyleTags.Count);
    }

    private static float StyleSetScore(BBPLFSFurnishingSet set, BBPLFSLayoutBrief brief)
    {
        if (brief.StyleTags.Count == 0 || set.StyleTags.Count == 0) return 0.5f;
        int hits = 0;
        for (int i = 0; i < brief.StyleTags.Count; i++)
            for (int j = 0; j < set.StyleTags.Count; j++)
                if (string.Equals(brief.StyleTags[i], set.StyleTags[j], StringComparison.OrdinalIgnoreCase)) { hits++; break; }
        return hits / (float)Mathf.Max(1, brief.StyleTags.Count);
    }

    private static float Score(BBPLFSLayoutBrief brief, int capacity, List<BBPLFSLayoutPlacement> placements,
        Dictionary<BBPLFSLayoutRole, BBPLFSAssetLayoutProfile> profiles, BBPLFSPlacementPattern pattern, Vector2 unitSize)
    {
        float capacityFit = 1f - Mathf.Clamp01(Mathf.Abs(brief.TargetCapacity - capacity) / (float)Mathf.Max(1, brief.TargetCapacity));
        long cost = 0;
        float style = 0f;
        for (int i = 0; i < placements.Count; i++)
            if (TryFindProfileByItemId(profiles, placements[i].ItemId, out var profile))
            { cost += profile.ItemDefinition.PurchasePriceCents; style += StyleProfileScore(profile, brief); }
        style /= Mathf.Max(1, placements.Count);
        float economy = 1f / (1f + cost / 1000000f);
        float openness = Mathf.Clamp01((unitSize.x + unitSize.y) / 6f);
        float service = pattern == BBPLFSPlacementPattern.CenterAxis ? 1f : pattern == BBPLFSPlacementPattern.Staggered ? 0.85f : 0.75f;
        return brief.GoalProfile switch
        {
            BBPLFSGoalProfile.MaxCapacity => capacityFit * 0.75f + economy * 0.15f + service * 0.10f,
            BBPLFSGoalProfile.MaxComfort => openness * 0.45f + capacityFit * 0.25f + service * 0.20f + style * 0.10f,
            BBPLFSGoalProfile.ServiceEfficient => service * 0.50f + capacityFit * 0.30f + openness * 0.20f,
            BBPLFSGoalProfile.Premium => style * 0.45f + openness * 0.30f + capacityFit * 0.15f + service * 0.10f,
            _ => capacityFit * 0.35f + service * 0.25f + openness * 0.15f + style * 0.15f + economy * 0.10f
        };
    }

    private static string BuildFingerprint(List<BBPLFSLayoutPlacement> placements)
    {
        var parts = new List<string>(placements.Count);
        for (int i = 0; i < placements.Count; i++)
        {
            Vector3 p = placements[i].WorldPosition;
            parts.Add(placements[i].ItemId + ":" + Mathf.RoundToInt(p.x * 4f) + ":" + Mathf.RoundToInt(p.z * 4f));
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join("|", parts);
    }
}
