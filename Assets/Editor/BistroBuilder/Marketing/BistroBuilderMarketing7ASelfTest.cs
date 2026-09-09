using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de 7A — Fundación de Marketing.</summary>
public static class BistroBuilderMarketing7ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Marketing/7A - Autotest fundación", false, 700)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
    }

    /// <summary>Entrada batchmode para CI/validación externa.</summary>
    public static void RunFromCommandLine()
    {
        bool ok = Run(out _, out _, out string report);
        if (!ok)
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }
        Debug.Log(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        List<BistroBuilderMarketingCampaignDefinition> seed =
            BistroBuilderMarketing7ASeedFactory.CreateSeed();

        Check(seed.Count == 35,
            "Catálogo semilla contiene 35 campañas.",
            ref passed, ref failed, lines);
        Check(BistroBuilderMarketingEngine.TryValidateCatalog(seed, out _),
            "Las 35 definiciones superan el contrato universal.",
            ref passed, ref failed, lines);
        Check(HasFivePerFamily(seed),
            "Cada una de las 7 familias contiene exactamente 5 campañas.",
            ref passed, ref failed, lines);
        Check(HasUniqueIds(seed),
            "Todos los CampaignId son estables y únicos.",
            ref passed, ref failed, lines);
        Check(HasTargetKinds(seed),
            "El seed cubre campañas sin objetivo, por DishId y por MenuId.",
            ref passed, ref failed, lines);
        Check(HasDiscountTradeoff(seed),
            "Promociones incluyen contrapartidas económicas de ticket medio.",
            ref passed, ref failed, lines);
        Check(HasOperationalPressure(seed),
            "Campañas intensas pueden elevar presión operativa.",
            ref passed, ref failed, lines);
        Check(BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory(
                "expense.marketing.localawareness"),
            "Finanzas 3F acepta subcategorías expense.marketing.*.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingSnapshot empty =
            BistroBuilderMarketingEngine.CreateEmptySnapshot();
        Check(BistroBuilderMarketingEngine.TryValidateSnapshot(empty, out _),
            "marketing.state vacío es válido.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingCampaignDefinition flyers =
            Find(seed, "marketing.local.flyers");
        bool created = BistroBuilderMarketingEngine.TryCreateCampaign(
            empty,
            flyers,
            3,
            string.Empty,
            "marketing_test_001",
            "marketing.expense.marketing_test_001",
            out BistroBuilderMarketingSnapshot active,
            out _);
        Check(created && active != null && active.campaigns.Count == 1,
            "El motor crea una instancia activa sin depender de escena.",
            ref passed, ref failed, lines);
        Check(active != null && active.revision == 1L &&
              active.campaigns[0].endDayExclusive == 6,
            "Alta incrementa revisión y calcula fin exclusivo.",
            ref passed, ref failed, lines);
        Check(!BistroBuilderMarketingEngine.TryCreateCampaign(
                active,
                flyers,
                3,
                string.Empty,
                "marketing_test_002",
                "marketing.expense.marketing_test_002",
                out _,
                out _),
            "No se apila la misma campaña activa sobre el mismo objetivo.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingCampaignDefinition dishWeek =
            Find(seed, "marketing.menu.dish_week");
        Check(!BistroBuilderMarketingEngine.TryCreateCampaign(
                empty,
                dishWeek,
                3,
                string.Empty,
                "marketing_test_target_bad",
                "marketing.expense.marketing_test_target_bad",
                out _,
                out _),
            "Una campaña de plato rechaza un DishId vacío.",
            ref passed, ref failed, lines);

        bool dishCreated = BistroBuilderMarketingEngine.TryCreateCampaign(
            active,
            dishWeek,
            3,
            "dish_fabada",
            "marketing_test_003",
            "marketing.expense.marketing_test_003",
            out BistroBuilderMarketingSnapshot withDish,
            out _);
        Check(dishCreated && withDish != null &&
              withDish.campaigns[1].targetId == "dish_fabada",
            "Objetivos de carta se guardan como identidad lógica.",
            ref passed, ref failed, lines);

        bool evaluated = BistroBuilderMarketingEngine.TryEvaluate(
            withDish,
            seed,
            new BistroBuilderMarketingEffectQuery
            {
                dayIndex = 3,
                segment = BistroBuilderMarketingCustomerSegment.LocalResidents,
                targetId = "dish_fabada"
            },
            out BistroBuilderMarketingEffectSnapshot effects,
            out _);
        Check(evaluated && effects != null &&
              effects.overallDemandBasisPoints == 1000 &&
              effects.targetDemandBasisPoints == 1800 &&
              effects.contributingCampaigns == 2,
            "Efectos de campañas activas se agregan de forma determinista.",
            ref passed, ref failed, lines);

        Check(BistroBuilderMarketingEngine.TryEvaluate(
                withDish,
                seed,
                new BistroBuilderMarketingEffectQuery
                {
                    dayIndex = 3,
                    segment = BistroBuilderMarketingCustomerSegment.LocalResidents,
                    targetId = "dish_merluza"
                },
                out BistroBuilderMarketingEffectSnapshot otherDish,
                out _) &&
              otherDish.targetDemandBasisPoints == 0,
            "TargetDemand no contamina otros DishId/MenuId.",
            ref passed, ref failed, lines);

        Check(BistroBuilderMarketingEngine.TryPruneExpired(
                withDish,
                10,
                out BistroBuilderMarketingSnapshot pruned,
                out bool changed,
                out _) && changed && pruned.campaigns.Count == 0,
            "Campañas vencidas se podan por día sin tocar otros sistemas.",
            ref passed, ref failed, lines);

        BistroBuilderMarketingSnapshot clone = withDish.DeepClone();
        clone.campaigns[0].campaignId = "mutated";
        Check(withDish.campaigns[0].campaignId != "mutated",
            "DeepClone aísla el estado persistible.",
            ref passed, ref failed, lines);
        Check(PersistentModelsContainNoUnityObjectReferences(),
            "Estado persistible de Marketing no contiene UnityEngine.Object.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — 7A / FUNDACIÓN MARKETING ===\n" +
                 string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static bool HasFivePerFamily(
        List<BistroBuilderMarketingCampaignDefinition> seed)
    {
        var counts = new Dictionary<BistroBuilderMarketingCampaignType, int>();
        foreach (BistroBuilderMarketingCampaignDefinition definition in seed)
        {
            counts.TryGetValue(definition.type, out int count);
            counts[definition.type] = count + 1;
        }
        Array values = Enum.GetValues(typeof(BistroBuilderMarketingCampaignType));
        if (counts.Count != values.Length)
            return false;
        foreach (BistroBuilderMarketingCampaignType type in values)
            if (!counts.TryGetValue(type, out int count) || count != 5)
                return false;
        return true;
    }

    private static bool HasUniqueIds(
        List<BistroBuilderMarketingCampaignDefinition> seed)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (BistroBuilderMarketingCampaignDefinition definition in seed)
            if (!ids.Add(BistroBuilderMarketingEngine.NormalizeId(
                    definition.campaignId)))
                return false;
        return true;
    }

    private static bool HasTargetKinds(
        List<BistroBuilderMarketingCampaignDefinition> seed)
    {
        bool none = false;
        bool dish = false;
        bool menu = false;
        foreach (BistroBuilderMarketingCampaignDefinition definition in seed)
        {
            none |= definition.targetKind == BistroBuilderMarketingTargetKind.None;
            dish |= definition.targetKind == BistroBuilderMarketingTargetKind.Dish;
            menu |= definition.targetKind == BistroBuilderMarketingTargetKind.Menu;
        }
        return none && dish && menu;
    }

    private static bool HasDiscountTradeoff(
        List<BistroBuilderMarketingCampaignDefinition> seed)
    {
        foreach (BistroBuilderMarketingCampaignDefinition definition in seed)
        foreach (BistroBuilderMarketingModifier modifier in definition.modifiers)
            if (modifier.kind == BistroBuilderMarketingModifierKind.AverageTicket &&
                modifier.basisPoints < 0)
                return true;
        return false;
    }

    private static bool HasOperationalPressure(
        List<BistroBuilderMarketingCampaignDefinition> seed)
    {
        foreach (BistroBuilderMarketingCampaignDefinition definition in seed)
        foreach (BistroBuilderMarketingModifier modifier in definition.modifiers)
            if (modifier.kind ==
                    BistroBuilderMarketingModifierKind.OperationalPressure &&
                modifier.basisPoints > 0)
                return true;
        return false;
    }

    private static BistroBuilderMarketingCampaignDefinition Find(
        List<BistroBuilderMarketingCampaignDefinition> seed,
        string id)
    {
        string normalized = BistroBuilderMarketingEngine.NormalizeId(id);
        return seed.Find(x => x != null &&
            BistroBuilderMarketingEngine.NormalizeId(x.campaignId) == normalized);
    }

    private static bool PersistentModelsContainNoUnityObjectReferences()
    {
        Type[] types =
        {
            typeof(BistroBuilderMarketingCampaignRecord),
            typeof(BistroBuilderMarketingSnapshot)
        };
        foreach (Type type in types)
        foreach (System.Reflection.FieldInfo field in type.GetFields())
            if (ContainsUnityObject(field.FieldType))
                return false;
        return true;
    }

    private static bool ContainsUnityObject(Type type)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return true;
        if (!type.IsGenericType)
            return false;
        foreach (Type argument in type.GetGenericArguments())
            if (ContainsUnityObject(argument))
                return true;
        return false;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + text);
        }
    }
}
