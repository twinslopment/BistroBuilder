using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presets soportados por la fábrica de artículos colocables.
///
/// Todos comparten el mismo núcleo universal. Los componentes
/// funcionales solo se añaden cuando ya existe un sistema real que
/// pueda utilizarlos.
/// </summary>
public enum BistroBuilderPlaceableFactoryPreset
{
    GenericFurniture = 0,
    Table = 1,
    Chair = 2,
    Decoration = 3,
    FloorLamp = 4,
    KitchenEquipment = 5,
    ServiceEquipment = 6,
    Structural = 7
}

/// <summary>
/// Estado de un elemento dentro de la simulación previa.
/// </summary>
public enum BistroBuilderPlaceableFactoryPlanStatus
{
    Ready = 0,
    AlreadyConfigured = 1,
    Blocked = 2
}

/// <summary>
/// Configuración compartida de una ejecución de la fábrica.
/// </summary>
[Serializable]
public sealed class BistroBuilderPlaceableFactorySettings
{
    public BistroBuilderPlaceableFactoryPreset Preset =
        BistroBuilderPlaceableFactoryPreset.GenericFurniture;

    public int PurchasePrice;

    public int TableCapacity = 2;

    public bool CanMove = true;

    public bool CanRotate = true;

    public float RotationStepDegrees = 90f;

    public float MinimumClearance;

    public bool GenerateColliderWhenMissing = true;

    public bool AddToMainCatalog = true;

    public bool RunProjectHealthAfterCreation = true;

    public string SingleDisplayNameOverride = string.Empty;

    public string SingleDescriptionOverride = string.Empty;

    public readonly List<RestaurantAreaCapabilityDefinition>
        RequiredCapabilities =
            new List<RestaurantAreaCapabilityDefinition>();

    public BistroBuilderPlaceableFactorySettings Clone()
    {
        BistroBuilderPlaceableFactorySettings clone =
            new BistroBuilderPlaceableFactorySettings
            {
                Preset = Preset,
                PurchasePrice = PurchasePrice,
                TableCapacity = TableCapacity,
                CanMove = CanMove,
                CanRotate = CanRotate,
                RotationStepDegrees = RotationStepDegrees,
                MinimumClearance = MinimumClearance,
                GenerateColliderWhenMissing =
                    GenerateColliderWhenMissing,
                AddToMainCatalog = AddToMainCatalog,
                RunProjectHealthAfterCreation =
                    RunProjectHealthAfterCreation,
                SingleDisplayNameOverride =
                    SingleDisplayNameOverride,
                SingleDescriptionOverride =
                    SingleDescriptionOverride
            };

        clone.RequiredCapabilities.AddRange(
            RequiredCapabilities
        );

        return clone;
    }
}

/// <summary>
/// Plan inmutable mostrado antes de crear ningún asset.
/// </summary>
public sealed class BistroBuilderPlaceableFactoryPlan
{
    public GameObject SourceAsset
    {
        get;
    }

    public string SourcePath
    {
        get;
    }

    public BistroBuilderPlaceableFactoryPlanStatus Status
    {
        get;
    }

    public string StatusMessage
    {
        get;
    }

    public string ItemId
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string Description
    {
        get;
    }

    public string AssetStem
    {
        get;
    }

    public string PrefabPath
    {
        get;
    }

    public string EditableDefinitionPath
    {
        get;
    }

    public string ItemDefinitionPath
    {
        get;
    }

    public RestaurantPlaceableItemCategory Category
    {
        get;
    }

    public Bounds LocalBounds
    {
        get;
    }

    public bool HasUsableBounds
    {
        get;
    }

    public bool WillGenerateCollider
    {
        get;
    }

    public bool AddsFunctionalTable
    {
        get;
    }

    public BistroBuilderPlaceableFactoryPlan(
        GameObject sourceAsset,
        string sourcePath,
        BistroBuilderPlaceableFactoryPlanStatus status,
        string statusMessage,
        string itemId,
        string displayName,
        string description,
        string assetStem,
        string prefabPath,
        string editableDefinitionPath,
        string itemDefinitionPath,
        RestaurantPlaceableItemCategory category,
        Bounds localBounds,
        bool hasUsableBounds,
        bool willGenerateCollider,
        bool addsFunctionalTable
    )
    {
        SourceAsset = sourceAsset;
        SourcePath = sourcePath;
        Status = status;
        StatusMessage = statusMessage;
        ItemId = itemId;
        DisplayName = displayName;
        Description = description;
        AssetStem = assetStem;
        PrefabPath = prefabPath;
        EditableDefinitionPath = editableDefinitionPath;
        ItemDefinitionPath = itemDefinitionPath;
        Category = category;
        LocalBounds = localBounds;
        HasUsableBounds = hasUsableBounds;
        WillGenerateCollider = willGenerateCollider;
        AddsFunctionalTable = addsFunctionalTable;
    }
}

/// <summary>
/// Resultado agregado de una ejecución real.
/// </summary>
public sealed class BistroBuilderPlaceableFactoryBatchResult
{
    public int CreatedCount;
    public int SkippedCount;
    public int FailedCount;

    public readonly List<string> CreatedAssets =
        new List<string>();

    public readonly List<string> Messages =
        new List<string>();

    public bool Succeeded =>
        FailedCount == 0 &&
        CreatedCount > 0;

    public string BuildSummary()
    {
        return
            "Creados: " + CreatedCount + "\n" +
            "Omitidos: " + SkippedCount + "\n" +
            "Errores: " + FailedCount;
    }
}
