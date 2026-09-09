using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de perfiles, preferencias y paciencia 10A.</summary>
public static class BistroBuilderAdvancedCustomers10ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10A - Autotest", false, 10002)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        int okCount = 0;
        int failCount = 0;
        var lines = new List<string>
            { "=== BISTRO BUILDER — 10A / PERFILES INDIVIDUALES ===" };
        void Check(bool condition, string message)
        {
            if (condition) { okCount++; lines.Add("[OK] " + message); }
            else { failCount++; lines.Add("[FAIL] " + message); }
        }

        List<BistroBuilderAdvancedCustomerArchetypeDefinition> seed =
            BistroBuilderAdvancedCustomers10ASeed.BuildComplete();
        Check(seed.Count == 10,
            "El seed contiene 10 arquetipos conductuales data-driven.");
        Check(BistroBuilderAdvancedCustomerProfileEngine.TryValidateCatalog(seed, out _),
            "El catálogo valida identidades, afinidades, tolerancias y preferencias.");

        var workerAcquisition = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        workerAcquisition.segmentId = "workers";
        Check(BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
                7, 3, 4, workerAcquisition, seed,
                out var workerProfile, out _),
            "Un grupo real se descompone en perfiles individuales sin crear NPC extra.");
        Check(workerProfile != null && workerProfile.members.Count == 3,
            "El perfil conserva exactamente un miembro por comensal.");

        BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            7, 3, 4, workerAcquisition, seed, out var deterministicCopy, out _);
        Check(JsonUtility.ToJson(workerProfile) == JsonUtility.ToJson(deterministicCopy),
            "La misma visita genera perfiles deterministas, aptos para Save/Load.");

        var returningA = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        returningA.segmentId = "localresidents";
        returningA.returningVisit = true;
        returningA.guestRelationsReferenceId = "guest_cohort_000042";
        BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            20, 2, 8, returningA, seed, out var returnVisitOne, out _);
        BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            91, 2, 12, returningA, seed, out var returnVisitTwo, out _);
        Check(returnVisitOne.members[0].customerId == returnVisitTwo.members[0].customerId &&
              JsonUtility.ToJson(returnVisitOne.members[0]) ==
              JsonUtility.ToJson(returnVisitTwo.members[0]),
            "Un habitual conserva identidad conductual entre visitas por cohorte.");

        Check(BistroBuilderAdvancedCustomerProfileEngine.ComputePatiencePressureBasisPoints(
                30f, 60f) == 0 &&
              BistroBuilderAdvancedCustomerProfileEngine.ComputePatiencePressureBasisPoints(
                60f, 60f) > 0 &&
              BistroBuilderAdvancedCustomerProfileEngine.ComputePatiencePressureBasisPoints(
                90f, 60f) == 10000,
            "La paciencia individual escala gradualmente y alcanza saturación.");

        var foodie = Find(seed, "foodie_quality");
        Check(foodie != null && foodie.qualitySensitivityBasisPoints >= 9000 &&
              foodie.preferredDishCategoryIds.Contains("category_tasting_item"),
            "El perfil foodie prioriza calidad y categorías gastronómicas.");

        var price = Find(seed, "price_conscious");
        Check(price != null && price.priceSensitivityBasisPoints >= 9000,
            "El cliente sensible al precio reacciona más a la relación calidad/precio.");

        var sampleMember = new BistroBuilderAdvancedCustomerMemberProfile
        {
            preferredDishCategoryIds = new List<string> { "category_dessert" },
            avoidedDishCategoryIds = new List<string> { "category_tasting_item" }
        };
        Check(BistroBuilderAdvancedCustomerProfileEngine.ComputeCategoryPreferenceBasisPoints(
                  sampleMember, "category_dessert") > 0 &&
              BistroBuilderAdvancedCustomerProfileEngine.ComputeCategoryPreferenceBasisPoints(
                  sampleMember, "category_tasting_item") < 0,
            "Las preferencias y rechazos de categoría tienen signo funcional distinto.");

        var broken = BistroBuilderAdvancedCustomers10ASeed.BuildComplete();
        broken[0].archetypeId = broken[1].archetypeId;
        Check(!BistroBuilderAdvancedCustomerProfileEngine.TryValidateCatalog(broken, out _),
            "El catálogo rechaza arquetipos duplicados.");

        broken = BistroBuilderAdvancedCustomers10ASeed.BuildComplete();
        broken[0].preferredDishCategoryIds.Add("category_dessert");
        broken[0].avoidedDishCategoryIds.Add("category_dessert");
        Check(!BistroBuilderAdvancedCustomerProfileEngine.TryValidateCatalog(broken, out _),
            "Una categoría no puede ser preferida y evitada simultáneamente.");

        Check(BistroBuilderAdvancedCustomerProfileEngine.TryValidateGroupProfile(
                workerProfile, 3, out _),
            "El perfil individual generado supera validación estructural propia.");

        passed = okCount;
        failed = failCount;
        report = string.Join("\n", lines) + "\nResultado: " + passed +
            " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderAdvancedCustomerArchetypeDefinition Find(
        IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> definitions,
        string id)
    {
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null && definitions[i].archetypeId == id)
                return definitions[i];
        return null;
    }
}
