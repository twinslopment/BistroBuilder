using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderSupplierDeliveryPresentationState
{
    Queued = 0,
    VehicleEntering = 1,
    Parked = 2,
    DriverExiting = 3,
    OpeningRearDoors = 4,
    PreparingTrolley = 5,
    GoingToWarehouse = 6,
    Unloading = 7,
    ReturningToVehicle = 8,
    StowingTrolley = 9,
    ClosingRearDoors = 10,
    DriverEnteringVehicle = 11,
    VehicleExiting = 12,
    Completed = 13,
    Cancelled = 14
}

[Serializable]
public sealed class BistroBuilderSupplierDeliveryManifestLine
{
    public string purchaseOrderLineId;
    public string supplierOfferId;
    public string ingredientId;
    public string ingredientDisplayName;
    public string canonicalUnit;
    public string packageFormatId;
    public string packageDisplayName;
    public int packageCount;
    public long totalNetQuantityMicrounits;

    public BistroBuilderSupplierDeliveryManifestLine DeepClone()
    {
        return (BistroBuilderSupplierDeliveryManifestLine)MemberwiseClone();
    }
}

/// <summary>
/// Contrato visual -> recepción. 2.3H lo emite una única vez al terminar la
/// descarga visual. NO escribe Inventario. 2.2B/2.3L consumirá este contrato.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierReceivingHandoff
{
    public string handoffId;
    public string logisticsPlanId;
    public string purchaseOrderId;
    public string orderDisplayCode;
    public string supplierId;
    public string supplierDisplayName;
    public int gameDay;
    public int visualTripsCompleted;
    public int totalPackageCount;
    public long totalNetQuantityMicrounits;
    public List<BistroBuilderSupplierDeliveryManifestLine> lines =
        new List<BistroBuilderSupplierDeliveryManifestLine>();

    public BistroBuilderSupplierReceivingHandoff DeepClone()
    {
        BistroBuilderSupplierReceivingHandoff clone = new BistroBuilderSupplierReceivingHandoff
        {
            handoffId = handoffId,
            logisticsPlanId = logisticsPlanId,
            purchaseOrderId = purchaseOrderId,
            orderDisplayCode = orderDisplayCode,
            supplierId = supplierId,
            supplierDisplayName = supplierDisplayName,
            gameDay = gameDay,
            visualTripsCompleted = visualTripsCompleted,
            totalPackageCount = totalPackageCount,
            totalNetQuantityMicrounits = totalNetQuantityMicrounits
        };
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] != null) clone.lines.Add(lines[i].DeepClone());
        }
        return clone;
    }
}

/// <summary>
/// Datos de branding resueltos desde supplier.authoring. El vehículo siempre
/// debe mostrar al menos el nombre del proveedor en ambos laterales. Si existe
/// logo, se muestra junto al nombre.
/// </summary>
public sealed class BistroBuilderSupplierDeliveryBrandingData
{
    public string supplierId;
    public string displayName;
    public Sprite logo;
    public Color primaryColor = Color.gray;
    public Color secondaryColor = Color.white;
    public Color textColor = Color.white;

    public bool HasLogo => logo != null;
    public bool HasReadableIdentity => !string.IsNullOrWhiteSpace(displayName);
}

[Serializable]
public sealed class BistroBuilderSupplierDeliveryPresentationRecord
{
    public string presentationId;
    public string logisticsPlanId;
    public string purchaseOrderId;
    public string orderDisplayCode;
    public string supplierId;
    public BistroBuilderSupplierDeliveryPresentationState state;
    public int currentTrip = 1;
    public int totalTrips = 1;
    public bool receivingHandoffEmitted;
    public string handoffId;
    public int startedGameDay;
    public int completedGameDay;
    public long stateRevision = 1;

    // Información suficiente para reanudar/recrear una presentación en 2.3J.
    public BistroBuilderSupplierVehiclePreference vehicle;
    public int visualLoadUnits;
    public int logisticsLoadUnits;
    public int appliedDelayGameMinutes;
    public string vehiclePresentationProfileId;
    public string driverPresentationProfileId;

    public bool IsTerminal => state == BistroBuilderSupplierDeliveryPresentationState.Completed ||
                              state == BistroBuilderSupplierDeliveryPresentationState.Cancelled;

    public BistroBuilderSupplierDeliveryPresentationRecord DeepClone()
    {
        return (BistroBuilderSupplierDeliveryPresentationRecord)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderSupplierDeliveryPresentationSnapshot
{
    public const string CurrentSchemaId = "supplier.delivery.presentation.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int currentGameDay = 1;
    public ulong sourceLogisticsSeed;
    public long presentationRevision = 1;
    public long nextPresentationSequence = 1;
    public List<BistroBuilderSupplierDeliveryPresentationRecord> presentations =
        new List<BistroBuilderSupplierDeliveryPresentationRecord>();

    public BistroBuilderSupplierDeliveryPresentationSnapshot DeepClone()
    {
        BistroBuilderSupplierDeliveryPresentationSnapshot clone =
            new BistroBuilderSupplierDeliveryPresentationSnapshot
            {
                schemaId = schemaId,
                schemaVersion = schemaVersion,
                currentGameDay = currentGameDay,
                sourceLogisticsSeed = sourceLogisticsSeed,
                presentationRevision = presentationRevision,
                nextPresentationSequence = nextPresentationSequence
            };
        if (presentations != null)
        {
            for (int i = 0; i < presentations.Count; i++)
                if (presentations[i] != null) clone.presentations.Add(presentations[i].DeepClone());
        }
        return clone;
    }
}
