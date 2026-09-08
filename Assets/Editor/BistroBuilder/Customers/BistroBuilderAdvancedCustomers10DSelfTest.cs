using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de 10D: zona/mesa, VIP y necesidades especiales.</summary>
public static class BistroBuilderAdvancedCustomers10DSelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10D - Autotest", false, 10032)]
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
        int p = 0; int f = 0;
        var lines = new List<string>();
        void Check(bool condition, string text)
        {
            if (condition) { p++; lines.Add("[OK] " + text); }
            else { f++; lines.Add("[FAIL] " + text); }
        }
        var profile = new BistroBuilderAdvancedCustomerGroupProfile
        {
            groupId = 77,
            dayIndex = 4,
            segmentId = "highvalue",
            returningVisit = true,
            returningReferenceId = "guest.cohort.77"
        };
        profile.members.Add(new BistroBuilderAdvancedCustomerMemberProfile
        {
            customerId = "guest.cohort.77.member01",
            memberIndex = 1,
            archetypeId = "high_value",
            tableWaitToleranceSeconds = 60f,
            waiterWaitToleranceSeconds = 30f,
            foodWaitToleranceBasisPoints = 15000,
            billWaitToleranceSeconds = 30f,
            serviceSensitivityBasisPoints = 9000,
            qualitySensitivityBasisPoints = 9000,
            priceSensitivityBasisPoints = 1500,
            ambienceSensitivityBasisPoints = 8500,
            preferredDishCategoryIds = new List<string>(),
            avoidedDishCategoryIds = new List<string>(),
            preferredZoneTagId = "premium",
            specialNeeds = BistroBuilderCustomerSpecialNeed.AccessibleSeating |
                BistroBuilderCustomerSpecialNeed.QuietSeating
        });
        var history = new BistroBuilderAdvancedCustomerHistoryRecord
        {
            cohortId = "guest.cohort.77",
            segmentId = "highvalue",
            firstVisitDay = 1,
            lastVisitDay = 4,
            visitCount = 6,
            lifetimeSpendCents = 42000,
            satisfactionTotalBasisPoints = 48000,
            loyaltyTier = BistroBuilderCustomerLoyaltyTier.Vip
        };

        bool built = BistroBuilderAdvancedCustomerServicePreferenceEngine.TryBuildPreference(
            profile, history,
            out BistroBuilderAdvancedCustomerServicePreference preference,
            out string error);
        Check(built && string.IsNullOrEmpty(error),
            "La preferencia agregada se construye desde perfil + historial.");
        Check(preference != null && preference.isVip,
            "Un historial VIP se proyecta como tratamiento VIP.");
        Check(preference != null &&
            (preference.specialNeeds & BistroBuilderCustomerSpecialNeed.AccessibleSeating) != 0 &&
            (preference.specialNeeds & BistroBuilderCustomerSpecialNeed.QuietSeating) != 0,
            "Las necesidades especiales de los miembros se agregan al grupo.");
        Check(preference != null && preference.preferredZoneTagIds.Contains("premium"),
            "La zona preferida individual llega a la preferencia de servicio.");
        var premium = new BistroBuilderAdvancedCustomerTableDescriptor
        {
            tableId = 1,
            capacity = 4,
            available = true,
            features = BistroBuilderCustomerTableFeature.Accessible |
                BistroBuilderCustomerTableFeature.Quiet |
                BistroBuilderCustomerTableFeature.VipPreferred
        };
        premium.semanticTags.Add("premium");
        premium.semanticTags.Add("quiet");
        var standard = new BistroBuilderAdvancedCustomerTableDescriptor
        {
            tableId = 2,
            capacity = 4,
            available = true,
            features = BistroBuilderCustomerTableFeature.None
        };
        standard.semanticTags.Add("dining");
        BistroBuilderAdvancedCustomerTableEvaluation good =
            BistroBuilderAdvancedCustomerServicePreferenceEngine.EvaluateTable(
                preference, premium, 2);
        BistroBuilderAdvancedCustomerTableEvaluation bad =
            BistroBuilderAdvancedCustomerServicePreferenceEngine.EvaluateTable(
                preference, standard, 2);
        Check(good.unmetSpecialNeeds == 0 && bad.unmetSpecialNeeds >= 2,
            "Accesibilidad y zona tranquila son necesidades operativas reales.");
        Check(good.preferenceScore > bad.preferenceScore,
            "Una mesa VIP/premium adecuada supera una mesa estándar.");
        premium.available = false;
        BistroBuilderAdvancedCustomerTableEvaluation unavailable =
            BistroBuilderAdvancedCustomerServicePreferenceEngine.EvaluateTable(
                preference, premium, 2);
        Check(unavailable.preferenceScore < -100000,
            "Una mesa no disponible nunca puede ganar por preferencias.");

        var acquisition = new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = "general",
            sourceSystemId = "service.test",
            discoverySourceId = "organic"
        };
        List<BistroBuilderAdvancedCustomerArchetypeDefinition> seed =
            BistroBuilderAdvancedCustomers10ASeed.BuildComplete();
        bool firstOk = BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            551, 3, 2, acquisition, seed,
            out BistroBuilderAdvancedCustomerGroupProfile first, out _);
        bool secondOk = BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            551, 3, 2, acquisition, seed,
            out BistroBuilderAdvancedCustomerGroupProfile second, out _);
        Check(firstOk && secondOk &&
            JsonUtility.ToJson(first) == JsonUtility.ToJson(second),
            "Perfil, preferencias y necesidades son deterministas para la misma identidad.");

        passed = p; failed = f;
        report = "=== BISTRO BUILDER — 10D / MESA, VIP Y NECESIDADES ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        return f == 0;
    }
}
