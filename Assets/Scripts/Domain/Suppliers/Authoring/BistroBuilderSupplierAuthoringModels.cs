using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taxonomía multidimensional del catálogo de proveedores.
/// Los flags permiten que un proveedor pertenezca simultáneamente
/// a varias familias de catálogo sin forzar una jerarquía rígida.
/// </summary>
[Flags]
public enum BistroBuilderSupplierCatalogFlags
{
    None = 0,
    Generalista = 1 << 0,
    FrutasYVerduras = 1 << 1,
    Carnes = 1 << 2,
    PescadosYMariscos = 1 << 3,
    Lacteos = 1 << 4,
    Panaderia = 1 << 5,
    Bebidas = 1 << 6,
    Secos = 1 << 7,
    AceitesYCondimentos = 1 << 8,
    Otros = 1 << 9
}

[Flags]
public enum BistroBuilderSupplierCommercialModelFlags
{
    None = 0,
    Generalista = 1 << 0,
    Mayorista = 1 << 1,
    Especialista = 1 << 2,
    Express = 1 << 3,
    ProductorLocal = 1 << 4,
    Distribuidor = 1 << 5,
    Premium = 1 << 6
}

[Flags]
public enum BistroBuilderSupplierScopeFlags
{
    None = 0,
    Local = 1 << 0,
    Regional = 1 << 1,
    Nacional = 1 << 2,
    Internacional = 1 << 3
}

[Flags]
public enum BistroBuilderSupplierPositioningFlags
{
    None = 0,
    Economico = 1 << 0,
    Equilibrado = 1 << 1,
    Premium = 1 << 2
}

public enum BistroBuilderSupplierReliabilityTier
{
    Irregular = 0,
    Normal = 1,
    Alta = 2,
    Excelente = 3
}

public enum BistroBuilderSupplierPriceProfile
{
    Estable = 0,
    Moderado = 1,
    Variable = 2
}

public enum BistroBuilderSupplierAvailabilityProfile
{
    MuyEstable = 0,
    Estable = 1,
    Variable = 2,
    Estacional = 3
}

public enum BistroBuilderSupplierPromotionFrequency
{
    MuyBaja = 0,
    Baja = 1,
    Media = 2,
    Alta = 3
}

public enum BistroBuilderSupplierVehiclePreference
{
    Automatico = 0,
    Furgoneta = 1,
    CamionLigero = 2
}

public enum BistroBuilderCommercialPackageLogisticSize
{
    Pequeno = 0,
    Medio = 1,
    Grande = 2
}

/// <summary>
/// Disponibilidad comercial base de una oferta. En 2.3B solo define
/// el estado inicial; la evolución dinámica se activa en 2.3C.
/// </summary>
public enum BistroBuilderSupplierOfferAvailability
{
    Disponible = 0,
    StockLimitado = 1,
    TemporalmenteAgotado = 2
}

public enum BistroBuilderSupplierUnlockRuleKind
{
    Ninguna = 0,
    DiasAbierto = 1,
    VolumenComprasCentimos = 2,
    FacturacionCentimos = 3,
    Reputacion = 4,
    TamanoRestaurante = 5,
    CategoriaCulinaria = 6,
    ConsumoFamiliaIngrediente = 7
}

[Serializable]
public sealed class BistroBuilderSupplierDeliveryWindowAuthoring
{
    [Min(0)]
    public int startMinuteOfDay = 8 * 60;

    [Min(0)]
    public int endMinuteOfDay = 12 * 60;

    public bool monday = true;
    public bool tuesday = true;
    public bool wednesday = true;
    public bool thursday = true;
    public bool friday = true;
    public bool saturday = true;
    public bool sunday = false;

    public BistroBuilderSupplierDeliveryWindowAuthoring DeepClone()
    {
        return new BistroBuilderSupplierDeliveryWindowAuthoring
        {
            startMinuteOfDay = startMinuteOfDay,
            endMinuteOfDay = endMinuteOfDay,
            monday = monday,
            tuesday = tuesday,
            wednesday = wednesday,
            thursday = thursday,
            friday = friday,
            saturday = saturday,
            sunday = sunday
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierPromotionProfileAuthoring
{
    public BistroBuilderSupplierPromotionFrequency frequency =
        BistroBuilderSupplierPromotionFrequency.Media;

    [Range(0f, 100f)]
    public float minimumDiscountPercent = 5f;

    [Range(0f, 100f)]
    public float maximumDiscountPercent = 15f;

    [Min(1)]
    public int minimumDurationDays = 2;

    [Min(1)]
    public int maximumDurationDays = 7;

    public BistroBuilderSupplierCatalogFlags eligibleCatalogs =
        BistroBuilderSupplierCatalogFlags.Generalista;

    public BistroBuilderSupplierPromotionProfileAuthoring DeepClone()
    {
        return new BistroBuilderSupplierPromotionProfileAuthoring
        {
            frequency = frequency,
            minimumDiscountPercent = minimumDiscountPercent,
            maximumDiscountPercent = maximumDiscountPercent,
            minimumDurationDays = minimumDurationDays,
            maximumDurationDays = maximumDurationDays,
            eligibleCatalogs = eligibleCatalogs
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierPriceEvolutionProfileAuthoring
{
    public BistroBuilderSupplierPriceProfile profile =
        BistroBuilderSupplierPriceProfile.Moderado;

    [Range(-50f, 0f)]
    public float minimumVariationPercent = -8f;

    [Range(0f, 100f)]
    public float maximumVariationPercent = 12f;

    [Min(1)]
    public int reviewEveryGameDays = 5;

    public BistroBuilderSupplierPriceEvolutionProfileAuthoring DeepClone()
    {
        return new BistroBuilderSupplierPriceEvolutionProfileAuthoring
        {
            profile = profile,
            minimumVariationPercent = minimumVariationPercent,
            maximumVariationPercent = maximumVariationPercent,
            reviewEveryGameDays = reviewEveryGameDays
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierAvailabilityProfileAuthoring
{
    public BistroBuilderSupplierAvailabilityProfile profile =
        BistroBuilderSupplierAvailabilityProfile.Estable;

    [Range(0f, 1f)]
    public float limitedStockWeight = 0.08f;

    [Range(0f, 1f)]
    public float temporaryOutOfStockWeight = 0.02f;

    public BistroBuilderSupplierAvailabilityProfileAuthoring DeepClone()
    {
        return new BistroBuilderSupplierAvailabilityProfileAuthoring
        {
            profile = profile,
            limitedStockWeight = limitedStockWeight,
            temporaryOutOfStockWeight = temporaryOutOfStockWeight
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierLogisticsProfileAuthoring
{
    public BistroBuilderSupplierVehiclePreference preferredVehicle =
        BistroBuilderSupplierVehiclePreference.Automatico;

    [Min(0)]
    public int minimumDelayMinutes = 30;

    [Min(0)]
    public int maximumDelayMinutes = 180;

    [Tooltip("Identificador lógico del perfil visual del vehículo. El asset 3D se enlazará en 2.3H.")]
    public string vehiclePresentationProfileId = "vehicle_supplier_default";

    [Tooltip("Identificador lógico del perfil visual del repartidor. El asset/Animator se enlazará en 2.3H.")]
    public string driverPresentationProfileId = "driver_supplier_default";

    public BistroBuilderSupplierLogisticsProfileAuthoring DeepClone()
    {
        return new BistroBuilderSupplierLogisticsProfileAuthoring
        {
            preferredVehicle = preferredVehicle,
            minimumDelayMinutes = minimumDelayMinutes,
            maximumDelayMinutes = maximumDelayMinutes,
            vehiclePresentationProfileId = vehiclePresentationProfileId,
            driverPresentationProfileId = driverPresentationProfileId
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierUnlockConditionAuthoring
{
    public BistroBuilderSupplierUnlockRuleKind kind =
        BistroBuilderSupplierUnlockRuleKind.Ninguna;

    public long numericThreshold;
    public string stringThreshold;

    public BistroBuilderSupplierUnlockConditionAuthoring DeepClone()
    {
        return new BistroBuilderSupplierUnlockConditionAuthoring
        {
            kind = kind,
            numericThreshold = numericThreshold,
            stringThreshold = stringThreshold
        };
    }
}

[Serializable]
public sealed class BistroBuilderSupplierUnlockProfileAuthoring
{
    public bool availableFromStart;

    [Tooltip("En 2.3I se interpretarán las condiciones. En 2.3A se almacenan y validan sin gameplay.")]
    public List<BistroBuilderSupplierUnlockConditionAuthoring> conditions =
        new List<BistroBuilderSupplierUnlockConditionAuthoring>();

    public BistroBuilderSupplierUnlockProfileAuthoring DeepClone()
    {
        BistroBuilderSupplierUnlockProfileAuthoring clone =
            new BistroBuilderSupplierUnlockProfileAuthoring
            {
                availableFromStart = availableFromStart
            };

        if (conditions != null)
        {
            for (int index = 0; index < conditions.Count; index++)
            {
                if (conditions[index] != null)
                {
                    clone.conditions.Add(conditions[index].DeepClone());
                }
            }
        }

        return clone;
    }
}

/// <summary>
/// Registro maestro de autoría de un proveedor.
/// No es una instancia de gameplay ni una entrega/pedido.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierAuthoringRecord
{
    [SerializeField]
    private string supplierId;

    public string displayName = "Nuevo proveedor";
    public string shortName = "Proveedor";

    [TextArea(2, 5)]
    public string description;

    public Sprite logo;
    public Color primaryBrandColor = new Color(0.20f, 0.35f, 0.26f, 1f);
    public Color secondaryBrandColor = new Color(0.90f, 0.84f, 0.64f, 1f);
    public Color textContrastColor = Color.white;

    public BistroBuilderSupplierCatalogFlags catalogFlags =
        BistroBuilderSupplierCatalogFlags.Generalista;

    public BistroBuilderSupplierCommercialModelFlags commercialModelFlags =
        BistroBuilderSupplierCommercialModelFlags.Generalista;

    public BistroBuilderSupplierScopeFlags scopeFlags =
        BistroBuilderSupplierScopeFlags.Regional;

    public BistroBuilderSupplierPositioningFlags positioningFlags =
        BistroBuilderSupplierPositioningFlags.Equilibrado;

    public List<string> customTags = new List<string>();

    public BistroBuilderSupplierReliabilityTier reliabilityTier =
        BistroBuilderSupplierReliabilityTier.Alta;

    [Range(0f, 1f)]
    public float reliabilityValue = 0.97f;

    [Min(0)]
    public long minimumOrderValueCents = 3000;

    [Min(0)]
    public long shippingCostCents = 800;

    public bool freeShippingEnabled = true;

    [Min(0)]
    public long freeShippingThresholdCents = 15000;

    [Min(0.1f)]
    public float defaultLeadTimeGameHours = 24f;

    public List<BistroBuilderSupplierDeliveryWindowAuthoring> deliveryWindows =
        new List<BistroBuilderSupplierDeliveryWindowAuthoring>();

    public BistroBuilderSupplierPromotionProfileAuthoring promotionProfile =
        new BistroBuilderSupplierPromotionProfileAuthoring();

    public BistroBuilderSupplierPriceEvolutionProfileAuthoring priceEvolutionProfile =
        new BistroBuilderSupplierPriceEvolutionProfileAuthoring();

    public BistroBuilderSupplierAvailabilityProfileAuthoring availabilityProfile =
        new BistroBuilderSupplierAvailabilityProfileAuthoring();

    public BistroBuilderSupplierLogisticsProfileAuthoring logisticsProfile =
        new BistroBuilderSupplierLogisticsProfileAuthoring();

    public BistroBuilderSupplierUnlockProfileAuthoring unlockProfile =
        new BistroBuilderSupplierUnlockProfileAuthoring();

    [Tooltip("Ofertas base proveedor→formato activadas en 2.3B. No son promociones ni pedidos.")]
    public List<BistroBuilderSupplierBaseOfferAuthoringRecord> baseOffers =
        new List<BistroBuilderSupplierBaseOfferAuthoringRecord>();

    public bool isActive = true;

    public string SupplierId => supplierId;

    public void AssignStableIdOnce(string value)
    {
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            return;
        }

        supplierId = NormalizeId(value, "supplier");
    }

#if UNITY_EDITOR
    public void EditorForceAssignIdForMigration(string value)
    {
        supplierId = NormalizeId(value, "supplier");
    }
#endif

    public BistroBuilderSupplierAuthoringRecord DeepClone(bool keepIdentity)
    {
        BistroBuilderSupplierAuthoringRecord clone =
            new BistroBuilderSupplierAuthoringRecord
            {
                supplierId = keepIdentity ? supplierId : null,
                displayName = displayName,
                shortName = shortName,
                description = description,
                logo = logo,
                primaryBrandColor = primaryBrandColor,
                secondaryBrandColor = secondaryBrandColor,
                textContrastColor = textContrastColor,
                catalogFlags = catalogFlags,
                commercialModelFlags = commercialModelFlags,
                scopeFlags = scopeFlags,
                positioningFlags = positioningFlags,
                reliabilityTier = reliabilityTier,
                reliabilityValue = reliabilityValue,
                minimumOrderValueCents = minimumOrderValueCents,
                shippingCostCents = shippingCostCents,
                freeShippingEnabled = freeShippingEnabled,
                freeShippingThresholdCents = freeShippingThresholdCents,
                defaultLeadTimeGameHours = defaultLeadTimeGameHours,
                promotionProfile = promotionProfile != null ? promotionProfile.DeepClone() : new BistroBuilderSupplierPromotionProfileAuthoring(),
                priceEvolutionProfile = priceEvolutionProfile != null ? priceEvolutionProfile.DeepClone() : new BistroBuilderSupplierPriceEvolutionProfileAuthoring(),
                availabilityProfile = availabilityProfile != null ? availabilityProfile.DeepClone() : new BistroBuilderSupplierAvailabilityProfileAuthoring(),
                logisticsProfile = logisticsProfile != null ? logisticsProfile.DeepClone() : new BistroBuilderSupplierLogisticsProfileAuthoring(),
                unlockProfile = unlockProfile != null ? unlockProfile.DeepClone() : new BistroBuilderSupplierUnlockProfileAuthoring(),
                isActive = isActive
            };

        if (customTags != null)
        {
            clone.customTags.AddRange(customTags);
        }

        if (deliveryWindows != null)
        {
            for (int index = 0; index < deliveryWindows.Count; index++)
            {
                if (deliveryWindows[index] != null)
                {
                    clone.deliveryWindows.Add(deliveryWindows[index].DeepClone());
                }
            }
        }

        if (baseOffers != null)
        {
            for (int index = 0; index < baseOffers.Count; index++)
            {
                if (baseOffers[index] != null)
                {
                    clone.baseOffers.Add(baseOffers[index].DeepClone(keepIdentity));
                }
            }
        }

        return clone;
    }

    public static string NormalizeId(string value, string prefix)
    {
        string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "id" : prefix.Trim().ToLowerInvariant();
        string safeValue = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim().ToLowerInvariant();
        safeValue = safeValue.Replace(' ', '_').Replace('-', '_');
        return safeValue.StartsWith(safePrefix + "_", StringComparison.Ordinal) ? safeValue : safePrefix + "_" + safeValue;
    }
}

/// <summary>
/// Formato comercial reutilizable del ingrediente.
/// La cantidad se guarda en micro-unidades de la unidad base canónica
/// para evitar depender de float en datos económicos/logísticos.
/// </summary>
[Serializable]
public sealed class BistroBuilderCommercialPackageAuthoringRecord
{
    [SerializeField]
    private string packageFormatId;

    public string displayName = "Nuevo formato";
    public string packageType = "Caja";

    [Min(1)]
    public long netQuantityMicrounits = 1000000L;

    public Sprite packageImage;
    public BistroBuilderCommercialPackageLogisticSize logisticSize =
        BistroBuilderCommercialPackageLogisticSize.Medio;

    public bool isActive = true;

    public string PackageFormatId => packageFormatId;

    public double NetQuantityInBaseUnits =>
        netQuantityMicrounits / 1000000.0;

    public void AssignStableIdOnce(string value)
    {
        if (!string.IsNullOrWhiteSpace(packageFormatId))
        {
            return;
        }

        packageFormatId =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(value, "package");
    }

#if UNITY_EDITOR
    public void EditorForceAssignIdForMigration(string value)
    {
        packageFormatId =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(value, "package");
    }
#endif

    public BistroBuilderCommercialPackageAuthoringRecord DeepClone(bool keepIdentity)
    {
        return new BistroBuilderCommercialPackageAuthoringRecord
        {
            packageFormatId = keepIdentity ? packageFormatId : null,
            displayName = displayName,
            packageType = packageType,
            netQuantityMicrounits = netQuantityMicrounits,
            packageImage = packageImage,
            logisticSize = logisticSize,
            isActive = isActive
        };
    }
}


/// <summary>
/// Oferta base de un proveedor para un formato comercial canónico.
///
/// No representa una promoción ni un pedido. Define la referencia estable
/// proveedor→ingrediente→formato y las condiciones de catálogo sobre las que
/// 2.3C/2.3D construirán precio de mercado, disponibilidad y promociones.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierBaseOfferAuthoringRecord
{
    [SerializeField]
    private string supplierOfferId;

    [Tooltip("IngredientId canónico al que pertenece el formato.")]
    public string ingredientId;

    [Tooltip("PackageFormatId definido en Editor de Ingredientes.")]
    public string packageFormatId;

    [Min(1)]
    public long basePriceCents = 100;

    [Min(1)]
    public int minimumPackageCount = 1;

    [Min(1)]
    public int orderIncrement = 1;

    public BistroBuilderSupplierOfferAvailability initialAvailability =
        BistroBuilderSupplierOfferAvailability.Disponible;

    public bool promotionEligible = true;

    [Tooltip("Si está desactivado, se usa el plazo general del proveedor.")]
    public bool overrideLeadTime;

    [Min(0.1f)]
    public float leadTimeOverrideGameHours = 24f;

    [Range(-50f, 0f)]
    public float minimumMarketVariationPercent = -10f;

    [Range(0f, 100f)]
    public float maximumMarketVariationPercent = 15f;

    public int sortOrder;
    public bool isActive = true;

    public string SupplierOfferId => supplierOfferId;

    public void AssignStableIdOnce(string value)
    {
        if (!string.IsNullOrWhiteSpace(supplierOfferId))
        {
            return;
        }

        supplierOfferId =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(value, "offer");
    }

#if UNITY_EDITOR
    public void EditorForceAssignIdForMigration(string value)
    {
        supplierOfferId =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(value, "offer");
    }
#endif

    public BistroBuilderSupplierBaseOfferAuthoringRecord DeepClone(bool keepIdentity)
    {
        return new BistroBuilderSupplierBaseOfferAuthoringRecord
        {
            supplierOfferId = keepIdentity ? supplierOfferId : null,
            ingredientId = ingredientId,
            packageFormatId = packageFormatId,
            basePriceCents = basePriceCents,
            minimumPackageCount = minimumPackageCount,
            orderIncrement = orderIncrement,
            initialAvailability = initialAvailability,
            promotionEligible = promotionEligible,
            overrideLeadTime = overrideLeadTime,
            leadTimeOverrideGameHours = leadTimeOverrideGameHours,
            minimumMarketVariationPercent = minimumMarketVariationPercent,
            maximumMarketVariationPercent = maximumMarketVariationPercent,
            sortOrder = sortOrder,
            isActive = isActive
        };
    }
}

/// <summary>
/// Metadatos reservados para la futura capa de calidad/origen.
/// Se almacenan desde 2.3A para no forzar una migración del modelo cuando
/// se activen proveedores premium, productores, certificaciones u origen.
/// Actualmente no tienen efecto en gameplay.
/// </summary>
[Serializable]
public sealed class BistroBuilderIngredientFutureSourcingMetadataAuthoring
{
    public string qualityTierId;
    public string originId;
    public string producerId;
    public List<string> certificationIds = new List<string>();

    public BistroBuilderIngredientFutureSourcingMetadataAuthoring DeepClone()
    {
        BistroBuilderIngredientFutureSourcingMetadataAuthoring clone =
            new BistroBuilderIngredientFutureSourcingMetadataAuthoring
            {
                qualityTierId = qualityTierId,
                originId = originId,
                producerId = producerId
            };

        if (certificationIds != null)
        {
            clone.certificationIds.AddRange(certificationIds);
        }

        return clone;
    }
}

/// <summary>
/// Capa visual/comercial de un ingrediente ya existente en el dominio
/// canónico de recetas/inventario. No crea un ingrediente paralelo.
/// </summary>
[Serializable]
public sealed class BistroBuilderIngredientAuthoringRecord
{
    [SerializeField]
    private string ingredientId;

    [Tooltip("Nombre leído del catálogo canónico en la última sincronización.")]
    public string displayNameSnapshot;

    [Tooltip("Unidad base leída del dominio canónico. En 2.3A se valida y no se publica como autoridad alternativa.")]
    public string canonicalUnitSnapshot;

    [Tooltip("Categoría leída del dominio canónico en la última sincronización.")]
    public string categorySnapshot;

    public Sprite displayImage;

    public List<BistroBuilderCommercialPackageAuthoringRecord> commercialPackages =
        new List<BistroBuilderCommercialPackageAuthoringRecord>();

    public BistroBuilderIngredientFutureSourcingMetadataAuthoring futureSourcing =
        new BistroBuilderIngredientFutureSourcingMetadataAuthoring();

    public bool isActive = true;

    public string IngredientId => ingredientId;

    public void AssignStableIdOnce(string value)
    {
        if (!string.IsNullOrWhiteSpace(ingredientId))
        {
            return;
        }

        ingredientId = NormalizeIngredientId(value);
    }

    public void RefreshCanonicalSnapshot(string displayName, string unit, string category)
    {
        displayNameSnapshot = displayName ?? string.Empty;
        canonicalUnitSnapshot = unit ?? string.Empty;
        categorySnapshot = category ?? string.Empty;
    }

    public BistroBuilderIngredientAuthoringRecord DeepClone(bool keepIdentity)
    {
        BistroBuilderIngredientAuthoringRecord clone =
            new BistroBuilderIngredientAuthoringRecord
            {
                ingredientId = keepIdentity ? ingredientId : null,
                displayNameSnapshot = displayNameSnapshot,
                canonicalUnitSnapshot = canonicalUnitSnapshot,
                categorySnapshot = categorySnapshot,
                displayImage = displayImage,
                futureSourcing = futureSourcing != null
                    ? futureSourcing.DeepClone()
                    : new BistroBuilderIngredientFutureSourcingMetadataAuthoring(),
                isActive = isActive
            };

        if (commercialPackages != null)
        {
            for (int index = 0; index < commercialPackages.Count; index++)
            {
                if (commercialPackages[index] != null)
                {
                    clone.commercialPackages.Add(
                        commercialPackages[index].DeepClone(keepIdentity));
                }
            }
        }

        return clone;
    }

    public static string NormalizeIngredientId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }
}
