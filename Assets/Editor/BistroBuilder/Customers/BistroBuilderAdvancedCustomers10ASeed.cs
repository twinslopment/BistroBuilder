using System.Collections.Generic;

/// <summary>Arquetipos V1 para perfiles individuales de clientes.</summary>
public static class BistroBuilderAdvancedCustomers10ASeed
{
    public static List<BistroBuilderAdvancedCustomerArchetypeDefinition> Build()
    {
        return new List<BistroBuilderAdvancedCustomerArchetypeDefinition>
        {
            D("balanced", "Cliente equilibrado", 240,
                A("general", "groups"), 75f, 45f, 16000, 40f,
                5000, 5000, 5000, 5000),
            D("local_regular", "Habitual de barrio", 100,
                A("localresidents", "traditional"), 90f, 50f, 17500, 45f,
                5200, 6200, 4800, 4500,
                P("category_main_course", "category_shared_dish")),
            D("worker_quick", "Cliente con prisa", 90,
                A("workers"), 35f, 22f, 12000, 20f,
                8500, 4300, 6000, 2200,
                P("category_main_course", "category_beverage")),
            D("social", "Cliente social", 100,
                A("youngadults", "groups"), 65f, 45f, 16000, 45f,
                5600, 5000, 5700, 7600,
                P("category_beverage", "category_shared_dish"))
        };
    }

    public static List<BistroBuilderAdvancedCustomerArchetypeDefinition> BuildExtended()
    {
        List<BistroBuilderAdvancedCustomerArchetypeDefinition> result = Build();
        result.Add(D("foodie_quality", "Amante de la gastronomía", 80,
            A("foodies"), 95f, 55f, 20000, 50f,
            5800, 9500, 2500, 6500,
            P("category_tasting_item", "category_starter", "category_main_course")));
        result.Add(D("traditional_patient", "Cliente tradicional", 90,
            A("traditional", "localresidents"), 105f, 60f, 19000, 55f,
            6000, 7200, 5200, 4500,
            P("category_main_course", "category_dessert"),
            P("category_tasting_item")));
        result.Add(D("price_conscious", "Cliente sensible al precio", 90,
            A("pricesensitive"), 70f, 45f, 17000, 45f,
            5000, 5000, 9800, 3800,
            P("category_main_course", "category_side_dish"),
            P("category_tasting_item")));
        return result;
    }

    public static List<BistroBuilderAdvancedCustomerArchetypeDefinition> BuildComplete()
    {
        List<BistroBuilderAdvancedCustomerArchetypeDefinition> result = BuildExtended();
        result.Add(D("planner", "Cliente planificador", 80,
            A("planners"), 55f, 35f, 15000, 30f,
            7600, 6200, 4200, 6000,
            P("category_starter", "category_main_course"), null, "quiet"));
        result.Add(D("couple_experience", "Pareja de experiencia", 80,
            A("couples"), 85f, 55f, 18000, 55f,
            6200, 7000, 3500, 9000,
            P("category_starter", "category_dessert"), null, "quiet"));
        result.Add(D("high_value", "Cliente premium", 60,
            A("highvalue"), 65f, 30f, 15000, 30f,
            9000, 9000, 1500, 8500,
            P("category_tasting_item", "category_main_course", "category_dessert"),
            null, "premium"));
        return result;
    }

    private static BistroBuilderAdvancedCustomerArchetypeDefinition D(
        string id, string name, int weight, List<string> affinities,
        float tableWait, float waiterWait, int foodWaitBp, float billWait,
        int service, int quality, int price, int ambience,
        List<string> preferred = null, List<string> avoided = null,
        string zone = "")
    {
        return new BistroBuilderAdvancedCustomerArchetypeDefinition
        {
            archetypeId = id,
            displayName = name,
            baseWeight = weight,
            segmentAffinityBonusWeight = 450,
            segmentAffinityIds = affinities ?? new List<string>(),
            tableWaitToleranceSeconds = tableWait,
            waiterWaitToleranceSeconds = waiterWait,
            foodWaitToleranceBasisPoints = foodWaitBp,
            billWaitToleranceSeconds = billWait,
            serviceSensitivityBasisPoints = service,
            qualitySensitivityBasisPoints = quality,
            priceSensitivityBasisPoints = price,
            ambienceSensitivityBasisPoints = ambience,
            preferredDishCategoryIds = preferred ?? new List<string>(),
            avoidedDishCategoryIds = avoided ?? new List<string>(),
            preferredZoneTagId = zone
        };
    }

    private static List<string> A(params string[] ids) =>
        ids != null ? new List<string>(ids) : new List<string>();

    private static List<string> P(params string[] ids) =>
        ids != null ? new List<string>(ids) : new List<string>();
}
