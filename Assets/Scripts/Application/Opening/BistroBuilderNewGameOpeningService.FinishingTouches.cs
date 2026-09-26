using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BistroBuilderNewGameOpeningService
{
    private const float FinishingWallHeight = 2.5f;
    private const float FinishingWallThickness = 0.12f;
    private const float FinishingOuterInset = 0.30f;
    private const float FinishingZoneInset = 0.35f;

    private BBPLFSAutoFurnishService finishingAutoFurnish;
    private RestaurantAreaRegistry finishingAreaRegistry;

    private bool TryPrepareFinishingTouchesBlueprint(out string error)
    {
        error = string.Empty;

        // FinishingTouches must never inherit development furniture or test geometry.
        // Start from the same clean canonical baseline as Empty, then build a real preset.
        if (!TryPrepareEmptyPremises(out error))
            return false;

        if (editDocumentService == null)
        {
            error = "No está disponible el documento de arquitectura para preparar Últimos retoques.";
            return false;
        }

        if (!TryResolveFinishingBounds(
                out Bounds premisesBounds,
                out Bounds diningBounds,
                out Bounds kitchenBounds,
                out error))
            return false;

        BistroBuilderEditDocument document = BuildFinishingTouchesDocument(
            premisesBounds,
            diningBounds,
            kitchenBounds);

        if (!editDocumentService.ReplaceCommittedForLoad(document, out error))
        {
            error = "No pudo publicarse la arquitectura inicial de Últimos retoques: " + error;
            return false;
        }

        Physics.SyncTransforms();
        return ValidateFinishingBlueprint(document, out error);
    }

    private bool TryMaterializeFinishingTouchesFurniture(out string error)
    {
        error = string.Empty;
        finishingAutoFurnish ??= FindFirstObjectByType<BBPLFSAutoFurnishService>();
        if (finishingAutoFurnish == null)
        {
            error = "BBPLFS no está disponible para amueblar Últimos retoques.";
            return false;
        }

        if (!editModeService.IsEditModeActive)
        {
            error = "Últimos retoques necesita Modo Edición activo para materializar el mobiliario.";
            return false;
        }

        if (!finishingAutoFurnish.Generate(out string generationMessage))
        {
            error = "BBPLFS no pudo generar el comedor inicial: " + generationMessage;
            return false;
        }

        if (finishingAutoFurnish.Candidates == null || finishingAutoFurnish.Candidates.Count == 0)
        {
            error = "BBPLFS no devolvió ninguna propuesta válida para el comedor inicial.";
            return false;
        }

        if (!finishingAutoFurnish.Preview(0, out string previewMessage))
        {
            error = "BBPLFS no pudo previsualizar la propuesta inicial: " + previewMessage;
            return false;
        }

        if (!finishingAutoFurnish.Accept(out string materializationMessage))
        {
            finishingAutoFurnish.CancelPreview();
            error = "BBPLFS no pudo materializar el comedor inicial: " + materializationMessage;
            return false;
        }

        Physics.SyncTransforms();

        if (!ValidateFinishingFurniture(out error))
        {
            TryRollbackFinishingTouchesWorld();
            return false;
        }

        return true;
    }

    private bool ValidateFinishingBlueprint(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty;
        if (document == null || document.walls == null || document.walls.Count < 5)
        {
            error = "Últimos retoques no generó una arquitectura cerrada suficiente.";
            return false;
        }

        if (!HasFunctionalZone(document, "dining") || !HasFunctionalZone(document, "kitchen"))
        {
            error = "Últimos retoques necesita zonas funcionales de comedor y cocina.";
            return false;
        }

        bool hasEntrance = false;
        bool hasKitchenPassage = false;
        for (int i = 0; i < document.openings.Count; i++)
        {
            BistroBuilderOpeningRecord opening = document.openings[i];
            if (opening == null) continue;
            if (string.Equals(opening.openingId.Value, "starter.finishing.opening.entrance", StringComparison.Ordinal))
                hasEntrance = true;
            if (string.Equals(opening.openingId.Value, "starter.finishing.opening.kitchen", StringComparison.Ordinal))
                hasKitchenPassage = true;
        }

        if (!hasEntrance || !hasKitchenPassage)
        {
            error = "Últimos retoques necesita una entrada y un paso de servicio hacia cocina.";
            return false;
        }

        return true;
    }

    private bool ValidateFinishingFurniture(out string error)
    {
        error = string.Empty;
        if (tableRegistry == null)
        {
            error = "No está disponible el registro de mesas después de amueblar Últimos retoques.";
            return false;
        }

        int seats = 0;
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            if (table != null && table.gameObject.activeInHierarchy)
                seats += Mathf.Max(0, table.Capacity);
        }

        if (tableRegistry.RegisteredTableCount <= 0 || seats < minimumDiningSeats)
        {
            error = "El comedor generado no alcanza el mínimo operativo de " +
                    minimumDiningSeats + " plazas.";
            return false;
        }

        return true;
    }

    private void TryRollbackFinishingTouchesWorld()
    {
        TryPrepareEmptyPremises(out _);
        if (editDocumentService != null)
            editDocumentService.ReplaceCommittedForLoad(new BistroBuilderEditDocument(), out _);
        finishingAutoFurnish?.CancelPreview();
        Physics.SyncTransforms();
    }

    private bool TryResolveFinishingBounds(
        out Bounds premisesBounds,
        out Bounds diningBounds,
        out Bounds kitchenBounds,
        out string error)
    {
        premisesBounds = default;
        diningBounds = default;
        kitchenBounds = default;
        error = string.Empty;

        finishingAreaRegistry ??= FindFirstObjectByType<RestaurantAreaRegistry>();
        RestaurantArea diningArea = ResolveArea("dining_main");
        RestaurantArea kitchenArea = ResolveArea("kitchen_main");

        if (diningArea == null || kitchenArea == null ||
            !TryGetAreaBounds(diningArea, out diningBounds) ||
            !TryGetAreaBounds(kitchenArea, out kitchenBounds))
        {
            error = "No se pudieron resolver los límites canónicos de comedor y cocina.";
            return false;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                !string.Equals(renderer.gameObject.name, "Floor_Test", StringComparison.OrdinalIgnoreCase))
                continue;
            premisesBounds = renderer.bounds;
            break;
        }

        if (premisesBounds.size.x < 1f || premisesBounds.size.z < 1f)
        {
            premisesBounds = diningBounds;
            premisesBounds.Encapsulate(kitchenBounds);
        }

        if (premisesBounds.size.x < 4f || premisesBounds.size.z < 4f)
        {
            error = "El local es demasiado pequeño para el preset Últimos retoques.";
            return false;
        }

        return true;
    }

    private RestaurantArea ResolveArea(string areaId)
    {
        if (finishingAreaRegistry != null &&
            finishingAreaRegistry.TryGetAreaById(areaId, out RestaurantArea registered))
            return registered;

        RestaurantArea[] areas = FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < areas.Length; i++)
            if (areas[i] != null && string.Equals(areas[i].AreaId, areaId, StringComparison.Ordinal))
                return areas[i];
        return null;
    }

    private static bool TryGetAreaBounds(RestaurantArea area, out Bounds bounds)
    {
        bounds = default;
        if (area == null || area.BoundaryColliders == null)
            return false;

        bool found = false;
        for (int i = 0; i < area.BoundaryColliders.Count; i++)
        {
            Collider collider = area.BoundaryColliders[i];
            if (collider == null || !collider.enabled)
                continue;
            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
        return found;
    }

    private static BistroBuilderEditDocument BuildFinishingTouchesDocument(
        Bounds premises,
        Bounds dining,
        Bounds kitchen)
    {
        float minX = premises.min.x + FinishingOuterInset;
        float maxX = premises.max.x - FinishingOuterInset;
        float minZ = premises.min.z + FinishingOuterInset;
        float maxZ = premises.max.z - FinishingOuterInset;

        float partitionZ = Mathf.Clamp(
            (dining.max.z + kitchen.min.z) * 0.5f,
            minZ + 2f,
            maxZ - 2f);

        var document = new BistroBuilderEditDocument
        {
            documentId = "starter.finishing-touches.v1",
            revision = 1
        };

        BistroBuilderWallRecord south = StarterWall(
            "starter.finishing.wall.south",
            new Vector2(minX, minZ),
            new Vector2(maxX, minZ));
        BistroBuilderWallRecord east = StarterWall(
            "starter.finishing.wall.east",
            new Vector2(maxX, minZ),
            new Vector2(maxX, maxZ));
        BistroBuilderWallRecord north = StarterWall(
            "starter.finishing.wall.north",
            new Vector2(maxX, maxZ),
            new Vector2(minX, maxZ));
        BistroBuilderWallRecord west = StarterWall(
            "starter.finishing.wall.west",
            new Vector2(minX, maxZ),
            new Vector2(minX, minZ));
        BistroBuilderWallRecord kitchenPartition = StarterWall(
            "starter.finishing.wall.kitchen-partition",
            new Vector2(minX, partitionZ),
            new Vector2(maxX, partitionZ));

        document.walls.Add(south);
        document.walls.Add(east);
        document.walls.Add(north);
        document.walls.Add(west);
        document.walls.Add(kitchenPartition);

        document.openings.Add(StarterOpening(
            "starter.finishing.opening.entrance",
            south.wallId,
            0.50f,
            Mathf.Clamp((maxX - minX) * 0.08f, 1.35f, 1.80f),
            2.20f));
        document.openings.Add(StarterOpening(
            "starter.finishing.opening.kitchen",
            kitchenPartition.wallId,
            0.72f,
            1.20f,
            2.20f));

        document.zones.Add(StarterZone(
            "starter.finishing.zone.dining",
            "zone.dining",
            dining,
            minX,
            maxX,
            minZ,
            partitionZ));
        document.zones.Add(StarterZone(
            "starter.finishing.zone.kitchen",
            "zone.kitchen",
            kitchen,
            minX,
            maxX,
            partitionZ,
            maxZ));

        return document;
    }

    private static BistroBuilderWallRecord StarterWall(
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
            height = FinishingWallHeight,
            thickness = FinishingWallThickness,
            wallDefinitionId = "wall.default"
        };
    }

    private static BistroBuilderOpeningRecord StarterOpening(
        string id,
        BistroBuilderEditId wallId,
        float position01,
        float width,
        float height)
    {
        return new BistroBuilderOpeningRecord
        {
            openingId = new BistroBuilderEditId(id),
            hostWallId = wallId,
            axisPosition01 = Mathf.Clamp01(position01),
            width = width,
            bottomElevation = 0f,
            height = height,
            openingType = "door",
            fillDefinitionId = string.Empty,
            flipped = false
        };
    }

    private static BistroBuilderFunctionalZoneRecord StarterZone(
        string id,
        string definition,
        Bounds source,
        float outerMinX,
        float outerMaxX,
        float outerMinZ,
        float outerMaxZ)
    {
        float minX = Mathf.Clamp(source.min.x + FinishingZoneInset, outerMinX, outerMaxX);
        float maxX = Mathf.Clamp(source.max.x - FinishingZoneInset, outerMinX, outerMaxX);
        float minZ = Mathf.Clamp(source.min.z + FinishingZoneInset, outerMinZ, outerMaxZ);
        float maxZ = Mathf.Clamp(source.max.z - FinishingZoneInset, outerMinZ, outerMaxZ);

        var zone = new BistroBuilderFunctionalZoneRecord
        {
            zoneId = new BistroBuilderEditId(id),
            zoneDefinitionId = definition
        };
        zone.explicitRegion.Add(new Vector2(minX, minZ));
        zone.explicitRegion.Add(new Vector2(maxX, minZ));
        zone.explicitRegion.Add(new Vector2(maxX, maxZ));
        zone.explicitRegion.Add(new Vector2(minX, maxZ));
        return zone;
    }
}
