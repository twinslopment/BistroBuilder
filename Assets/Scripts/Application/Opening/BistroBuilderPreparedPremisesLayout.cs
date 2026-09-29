using System;
using UnityEngine;

/// <summary>
/// Builds the deterministic authored architecture used by prepared new-game profiles.
/// The immutable outer premises envelope is rendered separately and is never persisted
/// as player-authored construction.
/// </summary>
public static class BistroBuilderPreparedPremisesLayout
{
    private const float Inset = 0.35f;
    private const float WallHeight = 2.8f;
    private const float WallThickness = 0.12f;

    public static bool TryBuild(
        BistroBuilderStartingPremisesProfile profile,
        Bounds premisesBounds,
        Vector3 entranceWorldPosition,
        out BistroBuilderEditDocument document,
        out string error)
    {
        error = string.Empty;
        document = new BistroBuilderEditDocument
        {
            documentId = "prepared-premises-" + profile.ToString().ToLowerInvariant(),
            revision = 1
        };

        if (premisesBounds.size.x < 6f || premisesBounds.size.z < 6f)
        {
            error = "El local inicial no tiene superficie suficiente para preparar una distribución segura.";
            return false;
        }

        if (profile == BistroBuilderStartingPremisesProfile.Empty)
            return true;

        float minX = premisesBounds.min.x + Inset;
        float maxX = premisesBounds.max.x - Inset;
        float minZ = premisesBounds.min.z + Inset;
        float maxZ = premisesBounds.max.z - Inset;
        float width = maxX - minX;
        float depth = maxZ - minZ;

        if (width < 5.5f || depth < 5.5f)
        {
            error = "La envolvente útil del local es demasiado pequeña para la preparación inicial.";
            return false;
        }

        float dividerZ = Mathf.Clamp(
            Mathf.Lerp(minZ, maxZ, 0.66f),
            minZ + 2.8f,
            maxZ - 2.8f);

        float bathroomWidth = Mathf.Clamp(width * 0.17f, 2.6f, 3.4f);
        float bathroomWestX = maxX - bathroomWidth;
        float kitchenDoorX = Mathf.Clamp(
            premisesBounds.center.x + Mathf.Min(4f, width * 0.22f),
            minX + 1.6f,
            bathroomWestX - 1.6f);

        BistroBuilderWallRecord kitchenDivider = Wall(
            "prepared-kitchen-divider",
            new Vector2(minX, dividerZ),
            new Vector2(maxX, dividerZ));

        BistroBuilderWallRecord bathroomWest = Wall(
            "prepared-bathroom-west",
            new Vector2(bathroomWestX, dividerZ),
            new Vector2(bathroomWestX, maxZ));

        document.walls.Add(kitchenDivider);
        document.walls.Add(bathroomWest);

        document.openings.Add(Door(
            "prepared-kitchen-door",
            kitchenDivider,
            Mathf.InverseLerp(minX, maxX, kitchenDoorX),
            1.25f));

        float bathroomDoorZ = Mathf.Clamp(
            dividerZ + 1.45f,
            dividerZ + 1.0f,
            maxZ - 1.0f);

        document.openings.Add(Door(
            "prepared-bathroom-door",
            bathroomWest,
            Mathf.InverseLerp(dividerZ, maxZ, bathroomDoorZ),
            0.92f));

        document.zones.Add(Zone(
            "prepared-zone-dining",
            "zone.dining",
            new Vector2(minX, minZ),
            new Vector2(maxX, minZ),
            new Vector2(maxX, dividerZ - 0.08f),
            new Vector2(minX, dividerZ - 0.08f)));

        document.zones.Add(Zone(
            "prepared-zone-kitchen",
            "zone.kitchen",
            new Vector2(minX, dividerZ + 0.08f),
            new Vector2(bathroomWestX - 0.08f, dividerZ + 0.08f),
            new Vector2(bathroomWestX - 0.08f, maxZ),
            new Vector2(minX, maxZ)));

        document.zones.Add(Zone(
            "prepared-zone-bathroom",
            "zone.bathroom",
            new Vector2(bathroomWestX + 0.08f, dividerZ + 0.08f),
            new Vector2(maxX, dividerZ + 0.08f),
            new Vector2(maxX, maxZ),
            new Vector2(bathroomWestX + 0.08f, maxZ)));

        return true;
    }

    private static BistroBuilderWallRecord Wall(
        string id,
        Vector2 start,
        Vector2 end)
    {
        return new BistroBuilderWallRecord
        {
            wallId = new BistroBuilderEditId(id),
            buildPlaneId = "default",
            axisStart = start,
            axisEnd = end,
            baseElevation = 0f,
            height = WallHeight,
            thickness = WallThickness,
            wallDefinitionId = "wall.default"
        };
    }

    private static BistroBuilderOpeningRecord Door(
        string id,
        BistroBuilderWallRecord host,
        float axisPosition01,
        float width)
    {
        return new BistroBuilderOpeningRecord
        {
            openingId = new BistroBuilderEditId(id),
            hostWallId = host.wallId,
            axisPosition01 = Mathf.Clamp01(axisPosition01),
            width = width,
            bottomElevation = 0f,
            height = 2.15f,
            openingType = "door",
            fillDefinitionId = "door.oak",
            flipped = false
        };
    }

    private static BistroBuilderFunctionalZoneRecord Zone(
        string id,
        string definitionId,
        params Vector2[] points)
    {
        var zone = new BistroBuilderFunctionalZoneRecord
        {
            zoneId = new BistroBuilderEditId(id),
            zoneDefinitionId = definitionId
        };
        if (points != null)
            zone.explicitRegion.AddRange(points);
        return zone;
    }
}
