using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro de contratos, migración y resolución determinista de 2.1H/I.
/// No modifica la escena ni el estado runtime del jugador.
/// </summary>
public static class BistroBuilderMenuPortfolio21HISelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1H-I Multiple Menus and Rules Self-Test";

    [MenuItem(MenuPath, false, 200)]
    private static void RunFromMenu()
    {
        TestReport report = Run();
        string text = report.BuildReport();
        if (report.Failed > 0) Debug.LogError(text);
        else Debug.Log(text);
        EditorUtility.DisplayDialog("Bistro Builder", text, "Aceptar");
    }

    public static TestReport Run()
    {
        TestReport report = new TestReport();

        report.Check(
            BistroBuilderMenuSaveData.CurrentSchemaVersion == 5 &&
            BistroBuilderMenuSaveSectionProvider.StableSectionVersion == 5,
            "menu.state publica la versión 5 de forma coherente."
        );

        TestMigration(report);
        TestRuleValidation(report);
        TestMatching(report);
        TestPrecedence(report);
        TestCloning(report);
        TestJson(report);
        return report;
    }

    private static void TestMigration(TestReport report)
    {
        BistroBuilderMenuSaveDataV4 oldState = new BistroBuilderMenuSaveDataV4
        {
            schemaVersion = 4,
            activeRestaurantId = "restaurant_primary",
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>
            {
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId = "restaurant_primary",
                    revision = 7,
                    items = new List<BistroBuilderMenuItemSaveData>(),
                    unresolvedItems = new List<BistroBuilderMenuItemSaveData>()
                }
            },
            authoredDishRecipes = new List<BistroBuilderDishRecipeSaveData>(),
            unresolvedAuthoredDishRecipes = new List<BistroBuilderDishRecipeSaveData>()
        };

        GameObject root = new GameObject("2.1HI_Migration_Test");
        try
        {
            BistroBuilderMenuStateV4ToV5Migration migration =
                root.AddComponent<BistroBuilderMenuStateV4ToV5Migration>();
            byte[] source = Encoding.UTF8.GetBytes(JsonUtility.ToJson(oldState));
            bool migrated = migration.TryMigrate(source, out byte[] payload, out _);
            BistroBuilderMenuSaveData current = migrated
                ? JsonUtility.FromJson<BistroBuilderMenuSaveData>(Encoding.UTF8.GetString(payload))
                : null;

            report.Check(
                migrated && current != null && current.schemaVersion == 5,
                "La migración V4→V5 produce un contrato v5."
            );
            report.Check(
                current != null && current.restaurants.Count == 1 &&
                current.portfolios.Count == 1,
                "La migración conserva la carta operativa y crea un portfolio."
            );
            report.Check(
                current != null && current.portfolios[0].menus.Count == 1 &&
                current.portfolios[0].menus[0].menuId ==
                    BistroBuilderMenuPortfolioService.DefaultMenuId &&
                current.portfolios[0].menus[0].revision == 7,
                "La carta histórica se convierte en Carta principal sin perder revisión."
            );
            report.Check(
                current != null && current.portfolios[0].rules.Count == 0 &&
                current.activeEventIds.Count == 0 &&
                current.activePromotionIds.Count == 0,
                "La migración no inventa reglas, eventos ni promociones."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestRuleValidation(TestReport report)
    {
        BistroBuilderMenuActivationRuleRuntimeState valid = Rule(
            "rule_valid", "Regla válida", "menu_a", 10,
            BistroBuilderMenuActivationRuleType.Composite,
            20260101, 20261231, 0x7F,
            BistroBuilderMealServiceAvailability.All,
            720, 900, "event_a", "promo_a"
        );
        report.Check(valid.TryValidate(out _), "Una regla compuesta completa es válida.");

        BistroBuilderMenuActivationRuleRuntimeState missingEvent = Rule(
            "rule_event", "Evento", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Event,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        report.Check(!missingEvent.TryValidate(out _), "Una regla de evento sin EventId se rechaza.");

        BistroBuilderMenuActivationRuleRuntimeState incompleteTime = Rule(
            "rule_time", "Hora", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            600, -1, string.Empty, string.Empty
        );
        report.Check(!incompleteTime.TryValidate(out _), "Una franja horaria incompleta se rechaza.");

        BistroBuilderMenuActivationRuleRuntimeState invalidDate = Rule(
            "rule_date", "Fecha", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Season,
            20261301, 20261201, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        report.Check(!invalidDate.TryValidate(out _), "Un rango de fechas inválido se rechaza.");
    }

    private static void TestMatching(TestReport report)
    {
        HashSet<string> events = new HashSet<string>(StringComparer.Ordinal) { "event_wedding" };
        HashSet<string> promotions = new HashSet<string>(StringComparer.Ordinal) { "promo_summer" };
        BistroBuilderMenuActivationContext context = new BistroBuilderMenuActivationContext(
            20260807,
            DayOfWeek.Friday,
            23 * 60 + 30,
            BistroBuilderMealServiceAvailability.Dinner,
            events,
            promotions
        );

        BistroBuilderMenuActivationRuleRuntimeState overnight = Rule(
            "rule_night", "Noche", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 1 << (int)DayOfWeek.Friday,
            BistroBuilderMealServiceAvailability.Dinner,
            22 * 60, 2 * 60, string.Empty, string.Empty
        );
        report.Check(overnight.Matches(context), "Una franja nocturna incluye las 23:30.");

        BistroBuilderMenuActivationContext midday = new BistroBuilderMenuActivationContext(
            20260807, DayOfWeek.Friday, 12 * 60,
            BistroBuilderMealServiceAvailability.Lunch, events, promotions
        );
        report.Check(!overnight.Matches(midday), "Una franja nocturna excluye el mediodía.");

        BistroBuilderMenuActivationRuleRuntimeState eventRule = Rule(
            "rule_wedding", "Boda", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Event,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, "event_wedding", string.Empty
        );
        report.Check(eventRule.Matches(context), "Una regla de evento reconoce su señal activa.");

        BistroBuilderMenuActivationRuleRuntimeState promotionRule = Rule(
            "rule_summer", "Verano", "menu_a", 0,
            BistroBuilderMenuActivationRuleType.Promotion,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, "promo_summer"
        );
        report.Check(promotionRule.Matches(context), "Una regla de promoción reconoce su señal activa.");
    }

    private static void TestPrecedence(TestReport report)
    {
        BistroBuilderMenuActivationRuleRuntimeState low = Rule(
            "rule_low", "Baja", "menu_a", 1,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        BistroBuilderMenuActivationRuleRuntimeState high = Rule(
            "rule_high", "Alta", "menu_b", 2,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        report.Check(
            BistroBuilderMenuActivationRuleRuntimeState.IsHigherPrecedence(high, low),
            "La prioridad mayor gana el conflicto."
        );

        BistroBuilderMenuActivationRuleRuntimeState generic = Rule(
            "rule_generic", "Genérica", "menu_a", 5,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        BistroBuilderMenuActivationRuleRuntimeState specific = Rule(
            "rule_specific", "Específica", "menu_b", 5,
            BistroBuilderMenuActivationRuleType.Event,
            0, 0, 0, BistroBuilderMealServiceAvailability.Dinner,
            -1, -1, "event_a", string.Empty
        );
        report.Check(
            BistroBuilderMenuActivationRuleRuntimeState.IsHigherPrecedence(specific, generic),
            "A igual prioridad gana la regla más específica."
        );

        BistroBuilderMenuActivationRuleRuntimeState alpha = Rule(
            "rule_a", "A", "menu_a", 5,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        BistroBuilderMenuActivationRuleRuntimeState beta = Rule(
            "rule_b", "B", "menu_b", 5,
            BistroBuilderMenuActivationRuleType.Schedule,
            0, 0, 0, BistroBuilderMealServiceAvailability.None,
            -1, -1, string.Empty, string.Empty
        );
        report.Check(
            BistroBuilderMenuActivationRuleRuntimeState.IsHigherPrecedence(alpha, beta),
            "El RuleId ordinal resuelve empates de forma estable."
        );
    }

    private static void TestCloning(TestReport report)
    {
        BistroBuilderMenuItemRuntimeState item = new BistroBuilderMenuItemRuntimeState(
            "dish_test", 1500, true, true, false, false,
            BistroBuilderMealServiceAvailability.All, 0, 3, 300
        );
        BistroBuilderNamedMenuRuntimeState original = new BistroBuilderNamedMenuRuntimeState(
            "menu_a", "Carta A", 2,
            new[] { item }, Array.Empty<BistroBuilderMenuItemRuntimeState>()
        );
        BistroBuilderNamedMenuRuntimeState clone = original.Clone();
        clone.ReplaceItems(
            new[] { new BistroBuilderMenuItemRuntimeState(
                "dish_test", 2500, true, true, false, false,
                BistroBuilderMealServiceAvailability.All, 0, 3, 300
            ) },
            Array.Empty<BistroBuilderMenuItemRuntimeState>(),
            true
        );

        report.Check(
            original.Items[0].CurrentPriceCents == 1500 &&
            clone.Items[0].CurrentPriceCents == 2500,
            "Las cartas duplicadas no comparten sus entradas mutables."
        );
        report.Check(
            original.Revision == 2 && clone.Revision == 3,
            "Cada carta conserva una revisión independiente."
        );
    }

    private static void TestJson(TestReport report)
    {
        BistroBuilderMenuSaveData source = new BistroBuilderMenuSaveData
        {
            activeRestaurantId = "restaurant_primary",
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>
            {
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId = "restaurant_primary",
                    items = new List<BistroBuilderMenuItemSaveData>(),
                    unresolvedItems = new List<BistroBuilderMenuItemSaveData>()
                }
            },
            portfolios = new List<BistroBuilderRestaurantMenuPortfolioSaveData>
            {
                new BistroBuilderRestaurantMenuPortfolioSaveData
                {
                    restaurantId = "restaurant_primary",
                    fallbackMenuId = "menu_a",
                    activeMenuId = "menu_b",
                    manualOverrideMenuId = string.Empty,
                    menus = new List<BistroBuilderNamedMenuSaveData>
                    {
                        new BistroBuilderNamedMenuSaveData { menuId = "menu_a", displayName = "A" },
                        new BistroBuilderNamedMenuSaveData { menuId = "menu_b", displayName = "B" }
                    },
                    rules = new List<BistroBuilderMenuActivationRuleSaveData>
                    {
                        new BistroBuilderMenuActivationRuleSaveData
                        {
                            ruleId = "rule_event",
                            displayName = "Evento",
                            targetMenuId = "menu_b",
                            ruleType = (int)BistroBuilderMenuActivationRuleType.Event,
                            requiredEventId = "event_a"
                        }
                    }
                }
            },
            activeEventIds = new List<string> { "event_a" },
            activePromotionIds = new List<string> { "promo_a" }
        };
        BistroBuilderMenuSaveData restored = JsonUtility.FromJson<BistroBuilderMenuSaveData>(JsonUtility.ToJson(source));
        report.Check(
            restored != null && restored.schemaVersion == 5 &&
            restored.portfolios.Count == 1 && restored.portfolios[0].menus.Count == 2 &&
            restored.portfolios[0].rules[0].requiredEventId == "event_a",
            "El round-trip JSON conserva cartas, reglas y señales."
        );
    }

    private static BistroBuilderMenuActivationRuleRuntimeState Rule(
        string id,
        string name,
        string menuId,
        int priority,
        BistroBuilderMenuActivationRuleType type,
        int startDate,
        int endDate,
        int weekdays,
        BistroBuilderMealServiceAvailability services,
        int startMinute,
        int endMinute,
        string eventId,
        string promotionId
    )
    {
        return new BistroBuilderMenuActivationRuleRuntimeState(
            id, name, true, menuId, priority, type,
            startDate, endDate, weekdays, services,
            startMinute, endMinute, eventId, promotionId
        );
    }

    public sealed class TestReport
    {
        private readonly List<string> lines = new List<string>();
        public int Passed { get; private set; }
        public int Failed { get; private set; }

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                lines.Add("- OK: " + message);
            }
            else
            {
                Failed++;
                lines.Add("- FALLO: " + message);
            }
        }

        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder(6144);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.1H/I");
            builder.AppendLine("Pruebas superadas: " + Passed);
            builder.AppendLine("Pruebas fallidas: " + Failed);
            for (int index = 0; index < lines.Count; index++) builder.AppendLine(lines[index]);
            return builder.ToString().TrimEnd();
        }
    }
}
