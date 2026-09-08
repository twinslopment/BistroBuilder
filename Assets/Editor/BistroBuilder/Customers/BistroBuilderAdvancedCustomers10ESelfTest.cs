using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAdvancedCustomers10ESelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10E - Autotest", false, 10042)]
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
        var excellent = new BistroBuilderAdvancedCustomerIndividualExperience
        {
            customerId = "customer.excellent",
            memberIndex = 1,
            serviceScoreBasisPoints = 9000,
            waitingScoreBasisPoints = 8500,
            foodQualityScoreBasisPoints = 9500,
            valueForMoneyScoreBasisPoints = 8500,
            ambienceScoreBasisPoints = 9000,
            overallSatisfactionBasisPoints = 9100
        };
        var poor = new BistroBuilderAdvancedCustomerIndividualExperience
        {
            customerId = "customer.poor",
            memberIndex = 1,
            serviceScoreBasisPoints = 3000,
            waitingScoreBasisPoints = 2500,
            foodQualityScoreBasisPoints = 3500,
            valueForMoneyScoreBasisPoints = 3000,
            ambienceScoreBasisPoints = 4000,
            overallSatisfactionBasisPoints = 3100
        };
        var advocate = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateIndividual(
            excellent, BistroBuilderCustomerLoyaltyTier.Regular);
        var detractor = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateIndividual(
            poor, BistroBuilderCustomerLoyaltyTier.New);
        Check(advocate.recommendationBasisPoints > detractor.recommendationBasisPoints,
            "Una experiencia excelente genera más recomendación que una mala.");
        Check(advocate.returnIntentBasisPoints > detractor.returnIntentBasisPoints,
            "La experiencia individual afecta realmente a la intención de volver.");
        var sameNew = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateIndividual(
            excellent, BistroBuilderCustomerLoyaltyTier.New);
        var sameVip = BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateIndividual(
            excellent, BistroBuilderCustomerLoyaltyTier.Vip);
        Check(sameVip.returnIntentBasisPoints > sameNew.returnIntentBasisPoints,
            "La fidelidad previa aumenta intención de retorno sin alterar Reputación.");
        Check(sameVip.recommendationBasisPoints == sameNew.recommendationBasisPoints,
            "La recomendación se deriva de la experiencia, no se infla por ser VIP.");

        BistroBuilderAdvancedCustomerHistoryRecord loyal = BuildHistory(
            "cohort.loyal", BistroBuilderCustomerLoyaltyTier.Vip,
            6, 42000, 8500, 8200, 8800, 8400);
        BistroBuilderAdvancedCustomerHistoryRecord weak = BuildHistory(
            "cohort.weak", BistroBuilderCustomerLoyaltyTier.Returning,
            2, 8000, 5200, 4500, 5000, 4700);
        var loyalAdvocacy =
            BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateHistory(loyal);
        var weakAdvocacy =
            BistroBuilderAdvancedCustomerAdvocacyEngine.EvaluateHistory(weak);
        Check(loyalAdvocacy.returnPriority > weakAdvocacy.returnPriority,
            "El historial diferencia qué cohorte tiene mayor probabilidad de volver.");
        Check(loyalAdvocacy.returnIntentBasisPoints > weakAdvocacy.returnIntentBasisPoints,
            "Satisfacción sostenida y fidelidad elevan la intención de retorno.");

        passed = p; failed = f;
        report = "=== BISTRO BUILDER — 10E / FIDELIDAD Y RECOMENDACIÓN ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        return f == 0;
    }

    private static BistroBuilderAdvancedCustomerHistoryRecord BuildHistory(
        string cohortId,
        BistroBuilderCustomerLoyaltyTier tier,
        int visits,
        long spend,
        int satisfaction,
        int waiting,
        int food,
        int value)
    {
        var record = new BistroBuilderAdvancedCustomerHistoryRecord
        {
            cohortId = cohortId,
            segmentId = "general",
            firstVisitDay = 1,
            lastVisitDay = visits,
            visitCount = visits,
            lifetimeSpendCents = spend,
            satisfactionTotalBasisPoints = (long)satisfaction * visits,
            loyaltyTier = tier
        };
        record.recentVisits.Add(new BistroBuilderAdvancedCustomerVisitHistoryRecord
        {
            experienceId = "visit." + cohortId + ".01",
            dayIndex = visits,
            paidAmountCents = visits > 0 ? spend / visits : 0L,
            satisfactionBasisPoints = satisfaction,
            waitingBasisPoints = waiting,
            foodQualityBasisPoints = food,
            valueBasisPoints = value
        });
        return record;
    }
}
