#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

internal static class BistroBuilderSuppliers23A2SeedFactory
{
    public static List<BistroBuilderSupplierAuthoringRecord> CreateSixProvisionalSuppliers()
    {
        List<BistroBuilderSupplierAuthoringRecord> result =
            new List<BistroBuilderSupplierAuthoringRecord>();

        result.Add(CreateSupplier(
            "supplier_mercado_central",
            "Mercado Central",
            "Mercado Central",
            "Proveedor generalista equilibrado para el abastecimiento habitual del restaurante.",
            new Color(0.17f, 0.31f, 0.23f, 1f),
            new Color(0.82f, 0.70f, 0.44f, 1f),
            BistroBuilderSupplierCatalogFlags.Generalista |
            BistroBuilderSupplierCatalogFlags.FrutasYVerduras |
            BistroBuilderSupplierCatalogFlags.Carnes |
            BistroBuilderSupplierCatalogFlags.PescadosYMariscos |
            BistroBuilderSupplierCatalogFlags.Lacteos |
            BistroBuilderSupplierCatalogFlags.Secos |
            BistroBuilderSupplierCatalogFlags.AceitesYCondimentos,
            BistroBuilderSupplierCommercialModelFlags.Generalista |
            BistroBuilderSupplierCommercialModelFlags.Distribuidor,
            BistroBuilderSupplierScopeFlags.Regional,
            BistroBuilderSupplierPositioningFlags.Equilibrado,
            BistroBuilderSupplierReliabilityTier.Alta,
            0.97f,
            3000,
            800,
            15000,
            24f,
            true));

        result.Add(CreateSupplier(
            "supplier_distribuciones_norte",
            "Distribuciones Norte",
            "Distribuciones Norte",
            "Mayorista de catálogo amplio, formatos grandes y coste unitario competitivo.",
            new Color(0.09f, 0.20f, 0.35f, 1f),
            new Color(0.35f, 0.62f, 0.79f, 1f),
            BistroBuilderSupplierCatalogFlags.Generalista |
            BistroBuilderSupplierCatalogFlags.Secos |
            BistroBuilderSupplierCatalogFlags.Bebidas |
            BistroBuilderSupplierCatalogFlags.AceitesYCondimentos,
            BistroBuilderSupplierCommercialModelFlags.Mayorista |
            BistroBuilderSupplierCommercialModelFlags.Distribuidor,
            BistroBuilderSupplierScopeFlags.Nacional,
            BistroBuilderSupplierPositioningFlags.Economico,
            BistroBuilderSupplierReliabilityTier.Alta,
            0.96f,
            12000,
            1000,
            25000,
            48f,
            false));

        result.Add(CreateSupplier(
            "supplier_hosteleria_express",
            "Hostelería Express",
            "Express",
            "Proveedor de respuesta rápida para necesidades urgentes y reposiciones de última hora.",
            new Color(0.70f, 0.20f, 0.08f, 1f),
            new Color(0.95f, 0.57f, 0.16f, 1f),
            BistroBuilderSupplierCatalogFlags.Generalista,
            BistroBuilderSupplierCommercialModelFlags.Express |
            BistroBuilderSupplierCommercialModelFlags.Distribuidor,
            BistroBuilderSupplierScopeFlags.Regional,
            BistroBuilderSupplierPositioningFlags.Equilibrado,
            BistroBuilderSupplierReliabilityTier.Excelente,
            0.99f,
            2000,
            1200,
            18000,
            6f,
            true));

        result.Add(CreateSupplier(
            "supplier_huerta_clara",
            "Huerta Clara",
            "Huerta Clara",
            "Especialista regional en frutas y verduras frescas para hostelería.",
            new Color(0.25f, 0.47f, 0.18f, 1f),
            new Color(0.84f, 0.76f, 0.43f, 1f),
            BistroBuilderSupplierCatalogFlags.FrutasYVerduras,
            BistroBuilderSupplierCommercialModelFlags.Especialista |
            BistroBuilderSupplierCommercialModelFlags.ProductorLocal,
            BistroBuilderSupplierScopeFlags.Regional,
            BistroBuilderSupplierPositioningFlags.Equilibrado,
            BistroBuilderSupplierReliabilityTier.Alta,
            0.98f,
            4000,
            700,
            14000,
            24f,
            false));

        result.Add(CreateSupplier(
            "supplier_carnes_selectas",
            "Carnes Selectas",
            "Carnes Selectas",
            "Proveedor profesional especializado en carnes y preparado para futuras gamas de calidad y origen.",
            new Color(0.43f, 0.10f, 0.10f, 1f),
            new Color(0.76f, 0.48f, 0.35f, 1f),
            BistroBuilderSupplierCatalogFlags.Carnes,
            BistroBuilderSupplierCommercialModelFlags.Especialista |
            BistroBuilderSupplierCommercialModelFlags.Premium,
            BistroBuilderSupplierScopeFlags.Regional,
            BistroBuilderSupplierPositioningFlags.Premium,
            BistroBuilderSupplierReliabilityTier.Excelente,
            0.99f,
            7000,
            900,
            18000,
            24f,
            false));

        result.Add(CreateSupplier(
            "supplier_costa_fresca",
            "Costa Fresca",
            "Costa Fresca",
            "Especialista en pescado y marisco con disponibilidad y tarifas algo más dinámicas.",
            new Color(0.06f, 0.31f, 0.43f, 1f),
            new Color(0.31f, 0.68f, 0.72f, 1f),
            BistroBuilderSupplierCatalogFlags.PescadosYMariscos,
            BistroBuilderSupplierCommercialModelFlags.Especialista |
            BistroBuilderSupplierCommercialModelFlags.Premium,
            BistroBuilderSupplierScopeFlags.Regional,
            BistroBuilderSupplierPositioningFlags.Premium,
            BistroBuilderSupplierReliabilityTier.Alta,
            0.95f,
            6500,
            900,
            18000,
            24f,
            false));

        ConfigureProfiles(result);
        return result;
    }

    private static BistroBuilderSupplierAuthoringRecord CreateSupplier(
        string id,
        string name,
        string shortName,
        string description,
        Color primary,
        Color secondary,
        BistroBuilderSupplierCatalogFlags catalog,
        BistroBuilderSupplierCommercialModelFlags commercial,
        BistroBuilderSupplierScopeFlags scope,
        BistroBuilderSupplierPositioningFlags positioning,
        BistroBuilderSupplierReliabilityTier reliabilityTier,
        float reliability,
        long minimumOrderCents,
        long shippingCents,
        long freeShippingCents,
        float leadHours,
        bool availableFromStart)
    {
        BistroBuilderSupplierAuthoringRecord supplier =
            new BistroBuilderSupplierAuthoringRecord
            {
                displayName = name,
                shortName = shortName,
                description = description,
                primaryBrandColor = primary,
                secondaryBrandColor = secondary,
                textContrastColor = Color.white,
                catalogFlags = catalog,
                commercialModelFlags = commercial,
                scopeFlags = scope,
                positioningFlags = positioning,
                reliabilityTier = reliabilityTier,
                reliabilityValue = reliability,
                minimumOrderValueCents = minimumOrderCents,
                shippingCostCents = shippingCents,
                freeShippingEnabled = true,
                freeShippingThresholdCents = freeShippingCents,
                defaultLeadTimeGameHours = leadHours,
                isActive = true
            };

        supplier.AssignStableIdOnce(id);
        supplier.unlockProfile.availableFromStart = availableFromStart;
        supplier.deliveryWindows.Add(new BistroBuilderSupplierDeliveryWindowAuthoring());
        return supplier;
    }

    private static void ConfigureProfiles(List<BistroBuilderSupplierAuthoringRecord> suppliers)
    {
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            supplier.priceEvolutionProfile.reviewEveryGameDays = 5;
        }

        // Mayorista: promociones más largas y formatos grandes en 2.3B.
        BistroBuilderSupplierAuthoringRecord wholesale = suppliers[1];
        wholesale.promotionProfile.frequency = BistroBuilderSupplierPromotionFrequency.Media;
        wholesale.promotionProfile.minimumDiscountPercent = 4f;
        wholesale.promotionProfile.maximumDiscountPercent = 14f;
        wholesale.promotionProfile.minimumDurationDays = 5;
        wholesale.promotionProfile.maximumDurationDays = 15;
        wholesale.priceEvolutionProfile.profile = BistroBuilderSupplierPriceProfile.Estable;
        wholesale.availabilityProfile.profile = BistroBuilderSupplierAvailabilityProfile.MuyEstable;

        // Express: pocas promociones, mínima variación y logística rápida.
        BistroBuilderSupplierAuthoringRecord express = suppliers[2];
        express.promotionProfile.frequency = BistroBuilderSupplierPromotionFrequency.Baja;
        express.promotionProfile.minimumDiscountPercent = 2f;
        express.promotionProfile.maximumDiscountPercent = 8f;
        express.promotionProfile.minimumDurationDays = 2;
        express.promotionProfile.maximumDurationDays = 5;
        express.logisticsProfile.minimumDelayMinutes = 15;
        express.logisticsProfile.maximumDelayMinutes = 60;
        express.availabilityProfile.profile = BistroBuilderSupplierAvailabilityProfile.MuyEstable;

        // Huerta: más actividad promocional en frescos.
        BistroBuilderSupplierAuthoringRecord produce = suppliers[3];
        produce.promotionProfile.frequency = BistroBuilderSupplierPromotionFrequency.Alta;
        produce.promotionProfile.eligibleCatalogs = BistroBuilderSupplierCatalogFlags.FrutasYVerduras;
        produce.priceEvolutionProfile.profile = BistroBuilderSupplierPriceProfile.Moderado;

        // Carnes premium: promociones menos frecuentes.
        BistroBuilderSupplierAuthoringRecord meat = suppliers[4];
        meat.promotionProfile.frequency = BistroBuilderSupplierPromotionFrequency.Baja;
        meat.promotionProfile.eligibleCatalogs = BistroBuilderSupplierCatalogFlags.Carnes;

        // Pescado: mayor variabilidad de mercado y disponibilidad.
        BistroBuilderSupplierAuthoringRecord fish = suppliers[5];
        fish.priceEvolutionProfile.profile = BistroBuilderSupplierPriceProfile.Variable;
        fish.priceEvolutionProfile.minimumVariationPercent = -10f;
        fish.priceEvolutionProfile.maximumVariationPercent = 15f;
        fish.availabilityProfile.profile = BistroBuilderSupplierAvailabilityProfile.Variable;
        fish.promotionProfile.eligibleCatalogs = BistroBuilderSupplierCatalogFlags.PescadosYMariscos;
    }
}
#endif
