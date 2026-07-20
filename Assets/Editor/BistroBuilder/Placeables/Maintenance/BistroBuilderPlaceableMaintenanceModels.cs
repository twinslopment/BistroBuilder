using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Severidad de una incidencia de mantenimiento de artículos.
/// </summary>
public enum BistroBuilderPlaceableMaintenanceSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Blocker = 3
}

/// <summary>
/// Incidencia estructurada detectada en un artículo, prefab o catálogo.
/// </summary>
public sealed class BistroBuilderPlaceableMaintenanceFinding
{
    public BistroBuilderPlaceableMaintenanceSeverity Severity
    {
        get;
    }

    public string Code
    {
        get;
    }

    public string Message
    {
        get;
    }

    public string Recommendation
    {
        get;
    }

    public UnityEngine.Object Context
    {
        get;
    }

    public string AssetPath
    {
        get;
    }

    public bool IsAutoRepairable
    {
        get;
    }

    public BistroBuilderPlaceableMaintenanceFinding(
        BistroBuilderPlaceableMaintenanceSeverity severity,
        string code,
        string message,
        string recommendation,
        UnityEngine.Object context,
        string assetPath,
        bool isAutoRepairable
    )
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        Recommendation = recommendation ?? string.Empty;
        Context = context;
        AssetPath = assetPath ?? string.Empty;
        IsAutoRepairable = isAutoRepairable;
    }
}

/// <summary>
/// Informe agregado de mantenimiento.
/// </summary>
public sealed class BistroBuilderPlaceableMaintenanceReport
{
    public DateTime GeneratedAtUtc
    {
        get;
    } = DateTime.UtcNow;

    public readonly List<
        BistroBuilderPlaceableMaintenanceFinding
    > Findings =
        new List<
            BistroBuilderPlaceableMaintenanceFinding
        >();

    public readonly List<RestaurantPlaceableItemDefinition>
        Definitions =
            new List<RestaurantPlaceableItemDefinition>();

    public int BlockerCount =>
        Count(
            BistroBuilderPlaceableMaintenanceSeverity.Blocker
        );

    public int ErrorCount =>
        Count(
            BistroBuilderPlaceableMaintenanceSeverity.Error
        );

    public int WarningCount =>
        Count(
            BistroBuilderPlaceableMaintenanceSeverity.Warning
        );

    public int InfoCount =>
        Count(
            BistroBuilderPlaceableMaintenanceSeverity.Info
        );

    public int AutoRepairableCount
    {
        get
        {
            int count = 0;

            for (int index = 0;
                 index < Findings.Count;
                 index++)
            {
                if (Findings[index].IsAutoRepairable)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasBlockingIssues =>
        BlockerCount > 0 ||
        ErrorCount > 0;

    public void Add(
        BistroBuilderPlaceableMaintenanceSeverity severity,
        string code,
        string message,
        string recommendation,
        UnityEngine.Object context,
        string assetPath,
        bool isAutoRepairable
    )
    {
        Findings.Add(
            new BistroBuilderPlaceableMaintenanceFinding(
                severity,
                code,
                message,
                recommendation,
                context,
                assetPath,
                isAutoRepairable
            )
        );
    }

    private int Count(
        BistroBuilderPlaceableMaintenanceSeverity severity
    )
    {
        int count = 0;

        for (int index = 0;
             index < Findings.Count;
             index++)
        {
            if (Findings[index].Severity == severity)
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// Valores editables de un artículo existente.
/// El ItemId no se incluye porque es una identidad estable.
/// </summary>
[Serializable]
public sealed class BistroBuilderPlaceableEditDraft
{
    public RestaurantPlaceableItemDefinition Definition;

    public string DisplayName = string.Empty;

    public string Description = string.Empty;

    public RestaurantPlaceableItemCategory Category =
        RestaurantPlaceableItemCategory.Furniture;

    public int PurchasePrice;

    public bool CanMove = true;

    public bool CanRotate = true;

    public bool UseCustomGridSize;

    public float CustomGridSize = 0.25f;

    public bool UseCustomRotationStep = true;

    public float RotationStepDegrees = 90f;

    public float MinimumClearance;

    public bool RegenerateThumbnail = true;

    public readonly List<RestaurantAreaCapabilityDefinition>
        RequiredCapabilities =
            new List<RestaurantAreaCapabilityDefinition>();
}

/// <summary>
/// Resultado de una reparación o actualización por lotes.
/// </summary>
public sealed class BistroBuilderPlaceableMaintenanceResult
{
    public int ChangedCount;
    public int PreservedCount;
    public int FailedCount;

    public readonly List<string> Messages =
        new List<string>();

    public string BuildSummary()
    {
        return
            "Actualizados: " + ChangedCount + "\n" +
            "Sin cambios: " + PreservedCount + "\n" +
            "Errores: " + FailedCount;
    }
}
