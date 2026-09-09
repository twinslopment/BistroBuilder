using System;
using System.Collections.Generic;

/// <summary>Contenido semilla canónico de 9A. Los valores podrán balancearse sin cambiar código.</summary>
public static class BistroBuilderProgression9ASeed
{
    public static List<BistroBuilderUpgradeDefinition> Build()
    {
        return new List<BistroBuilderUpgradeDefinition>
        {
            D("dining.comfort_seating", "Asientos más cómodos",
                "Mejora básica de confort del comedor.", BistroBuilderUpgradeCategory.DiningRoom,
                25000, 1, 0, null, "facility.dining_room"),
            D("dining.service_station", "Estación de apoyo de sala",
                "Organiza el apoyo de servicio sin cambiar la autoridad de Personal.", BistroBuilderUpgradeCategory.DiningRoom,
                55000, 2, 0, "dining.comfort_seating", "facility.dining_room"),
            D("dining.acoustic_treatment", "Tratamiento acústico",
                "Mejora la calidad ambiental del comedor.", BistroBuilderUpgradeCategory.DiningRoom,
                110000, 3, 5400, "dining.service_station", "facility.dining_room"),

            D("kitchen.prep_organization", "Organización de preparación",
                "Mejora funcional de la zona de preparación.", BistroBuilderUpgradeCategory.Kitchen,
                35000, 1, 0, null, "facility.kitchen"),
            D("kitchen.pass_optimization", "Optimización del pase",
                "Mejora el flujo físico del pase de cocina.", BistroBuilderUpgradeCategory.Kitchen,
                85000, 2, 0, "kitchen.prep_organization", "facility.kitchen"),
            D("kitchen.workflow_upgrade", "Flujo de cocina avanzado",
                "Mejora avanzada de organización operativa.", BistroBuilderUpgradeCategory.Kitchen,
                175000, 4, 5600, "kitchen.pass_optimization", "facility.kitchen"),

            D("terrace.basic_comfort", "Confort básico de terraza",
                "Primera mejora para locales que realmente disponen de terraza.", BistroBuilderUpgradeCategory.Terrace,
                45000, 2, 0, null, "facility.terrace"),
            D("terrace.weather_protection", "Protección climática",
                "Aumenta la utilidad de una terraza compatible.", BistroBuilderUpgradeCategory.Terrace,
                120000, 3, 5200, "terrace.basic_comfort", "facility.terrace"),
            D("terrace.premium_comfort", "Confort avanzado de terraza",
                "Mejora de alto nivel sin convertirla en obligación de progresión.", BistroBuilderUpgradeCategory.Terrace,
                210000, 5, 5900, "terrace.weather_protection", "facility.terrace"),

            D("bar.storage_upgrade", "Almacenaje de barra",
                "Mejora funcional para locales con barra operativa.", BistroBuilderUpgradeCategory.Bar,
                40000, 1, 0, null, "facility.bar"),
            D("bar.service_station", "Estación de servicio de barra",
                "Amplía la capacidad funcional de una barra existente.", BistroBuilderUpgradeCategory.Bar,
                90000, 2, 0, "bar.storage_upgrade", "facility.bar"),
            D("bar.specialist_setup", "Equipamiento especializado de barra",
                "Mejora avanzada para una identidad de barra más desarrollada.", BistroBuilderUpgradeCategory.Bar,
                190000, 4, 5700, "bar.service_station", "facility.bar"),

            D("infrastructure.storage_efficiency", "Almacenaje eficiente",
                "Mejora transversal del back-of-house.", BistroBuilderUpgradeCategory.Infrastructure,
                50000, 1, 0, null, "restaurant.base"),
            D("infrastructure.energy_efficiency", "Eficiencia energética",
                "Inversión estructural para un local más eficiente.", BistroBuilderUpgradeCategory.Infrastructure,
                125000, 3, 0, "infrastructure.storage_efficiency", "restaurant.base"),
            D("infrastructure.back_of_house", "Back-of-house avanzado",
                "Mejora estructural de alto nivel dentro del techo natural del local.", BistroBuilderUpgradeCategory.Infrastructure,
                240000, 5, 5600, "infrastructure.energy_efficiency", "restaurant.base"),

            D("ambience.lighting_plan", "Plan de iluminación",
                "Refuerza ambiente e identidad sin imponer un estilo concreto.", BistroBuilderUpgradeCategory.AmbienceIdentity,
                30000, 1, 0, null, "restaurant.base"),
            D("ambience.identity_details", "Detalles de identidad",
                "Permite profundizar en la personalidad del restaurante.", BistroBuilderUpgradeCategory.AmbienceIdentity,
                65000, 2, 0, "ambience.lighting_plan", "restaurant.base"),
            D("ambience.signature_atmosphere", "Atmósfera de autor",
                "Mejora avanzada de identidad; opcional, no una meta universal de lujo.", BistroBuilderUpgradeCategory.AmbienceIdentity,
                155000, 4, 5800, "ambience.identity_details", "restaurant.base")
        };
    }

    private static BistroBuilderUpgradeDefinition D(
        string id, string name, string description, BistroBuilderUpgradeCategory category,
        long costCents, int level, int reputation, string prerequisite, string capability)
    {
        var definition = new BistroBuilderUpgradeDefinition
        {
            upgradeId = id,
            displayName = name,
            description = description,
            category = category,
            costCents = costCents,
            requiredProgressionLevel = level,
            requiredReputationBasisPoints = reputation
        };
        if (!string.IsNullOrWhiteSpace(prerequisite))
            definition.prerequisiteUpgradeIds.Add(prerequisite);
        if (!string.IsNullOrWhiteSpace(capability))
            definition.requiredCapabilityIds.Add(capability);
        definition.effects.AddRange(BuildEffects(category, level));
        return definition;
    }

    private static List<BistroBuilderUpgradeEffectDefinition> BuildEffects(
        BistroBuilderUpgradeCategory category,
        int level)
    {
        int safeLevel = Math.Max(1, Math.Min(5, level));
        var effects = new List<BistroBuilderUpgradeEffectDefinition>();
        if (category == BistroBuilderUpgradeCategory.Kitchen)
            effects.Add(E(BistroBuilderUpgradeEffectKind.PreparationDuration, -(180 + safeLevel * 70), false));
        else if (category == BistroBuilderUpgradeCategory.Bar)
            effects.Add(E(BistroBuilderUpgradeEffectKind.PreparationDuration, -(140 + safeLevel * 55), true));
        else if (category == BistroBuilderUpgradeCategory.DiningRoom)
            effects.Add(E(BistroBuilderUpgradeEffectKind.AmbienceScore, 90 + safeLevel * 35, false));
        else if (category == BistroBuilderUpgradeCategory.Terrace)
            effects.Add(E(BistroBuilderUpgradeEffectKind.AmbienceScore, 80 + safeLevel * 35, false));
        else if (category == BistroBuilderUpgradeCategory.AmbienceIdentity)
            effects.Add(E(BistroBuilderUpgradeEffectKind.AmbienceScore, 140 + safeLevel * 45, false));
        else if (category == BistroBuilderUpgradeCategory.Infrastructure)
            effects.Add(E(BistroBuilderUpgradeEffectKind.FoodQualityPotential, 60 + safeLevel * 25, false));
        return effects;
    }

    private static BistroBuilderUpgradeEffectDefinition E(
        BistroBuilderUpgradeEffectKind kind, int basisPoints, bool barOnly)
    {
        return new BistroBuilderUpgradeEffectDefinition
        {
            kind = kind,
            basisPoints = basisPoints,
            barServiceOnly = barOnly
        };
    }
}