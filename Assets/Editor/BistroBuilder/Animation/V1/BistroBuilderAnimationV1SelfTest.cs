using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAnimationV1SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Animation V1/Autotest")]
    public static void Run()
    {
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BB CHARACTER & INTERACTION ANIMATION SYSTEM V1 - SELFTEST\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        var h1 = new BistroBuilderAnimationExecutionHandle("same", 1);
        var h2 = new BistroBuilderAnimationExecutionHandle("same", 2);
        Check(h1.IsValid && h2.IsValid && !h1.Equals(h2), "Execution generations prevent stale handle aliasing");

        var t1 = new BistroBuilderAnimationTargetHandle(BistroBuilderAnimationTargetKind.Seat, "seat-1", 1);
        var t2 = new BistroBuilderAnimationTargetHandle(BistroBuilderAnimationTargetKind.Seat, "seat-1", 2);
        Check(t1.IsValid && t2.IsValid && !t1.Equals(t2), "Target generations prevent stale handle aliasing");

        var validRequest = new BistroBuilderAnimationExecutionRequest
        {
            ownerId = "test-owner",
            actorId = "test-actor",
            family = BistroBuilderInteractionFamily.Seat,
            operation = BistroBuilderInteractionOperation.Sit,
            requestedMotionId = "seat.sit.standard",
            commitTimeoutSeconds = 1f,
            watchdogTimeoutSeconds = 2f
        };
        Check(validRequest.Validate(out _), "Minimal semantic execution request validates without scene references");
        validRequest.ownerId = string.Empty;
        Check(!validRequest.Validate(out _), "Execution request rejects missing owner identity");

        AnimationClip lowCostClip = new AnimationClip { name = "bb-selftest-low" };
        AnimationClip highCostClip = new AnimationClip { name = "bb-selftest-high" };
        var lowCost = new BistroBuilderMotionRecipeVariant();
        lowCost.ConfigureForEditor("low", lowCostClip, string.Empty, 0, null, false, 1f, BistroBuilderAnimationQualityTier.Q4, 0f);
        var highCost = new BistroBuilderMotionRecipeVariant();
        highCost.ConfigureForEditor("high", highCostClip, string.Empty, 0, null, false, 1f, BistroBuilderAnimationQualityTier.Q4, 1f);

        BistroBuilderMotionRecipe costRecipe = ScriptableObject.CreateInstance<BistroBuilderMotionRecipe>();
        costRecipe.ConfigureForEditor(
            "selftest.cost", BistroBuilderAnimationBodyMode.FullBody, false, 1f, false,
            BistroBuilderMotionInterruptPolicy.Immediate, string.Empty,
            new List<BistroBuilderMotionRecipeVariant> { lowCost, highCost },
            new List<BistroBuilderMotionSyncPoint>(), true, "selftest-cost-cert",
            "selftest", "1.0", 0.9f, 1.1f, "selftest", "selftest", "CC0", "1");

        var scheduler = new BistroBuilderMotionVariantScheduler(2);
        Check(scheduler.TrySelect(costRecipe, "actor", BistroBuilderAnimationQualityTier.Q4, "exec-cost", out BistroBuilderMotionRecipeVariant lowSelected) && lowSelected.VariantId == "low",
            "Q4 scheduler prefers cheaper variant when quality is degraded");

        var variantA = new BistroBuilderMotionRecipeVariant();
        variantA.ConfigureForEditor("a", lowCostClip, string.Empty, 0, null, false, 1f, BistroBuilderAnimationQualityTier.Q4, 0f);
        var variantB = new BistroBuilderMotionRecipeVariant();
        variantB.ConfigureForEditor("b", highCostClip, string.Empty, 0, null, false, 1f, BistroBuilderAnimationQualityTier.Q4, 0f);
        BistroBuilderMotionRecipe repeatRecipe = ScriptableObject.CreateInstance<BistroBuilderMotionRecipe>();
        repeatRecipe.ConfigureForEditor(
            "selftest.repeat", BistroBuilderAnimationBodyMode.FullBody, false, 1f, false,
            BistroBuilderMotionInterruptPolicy.Immediate, string.Empty,
            new List<BistroBuilderMotionRecipeVariant> { variantA, variantB },
            new List<BistroBuilderMotionSyncPoint>(), true, "selftest-repeat-cert",
            "selftest", "1.0", 0.9f, 1.1f, "selftest", "selftest", "CC0", "1");

        var antiRepeat = new BistroBuilderMotionVariantScheduler(1);
        bool firstOk = antiRepeat.TrySelect(repeatRecipe, "actor-repeat", BistroBuilderAnimationQualityTier.Q4, "exec-1", out BistroBuilderMotionRecipeVariant first);
        bool secondOk = antiRepeat.TrySelect(repeatRecipe, "actor-repeat", BistroBuilderAnimationQualityTier.Q4, "exec-2", out BistroBuilderMotionRecipeVariant second);
        Check(firstOk && secondOk && first.VariantId != second.VariantId,
            "Variant scheduler enforces a cosmetic no-repeat window");
        Check(costRecipe.ValidateConfiguration(out _), "Synthetic certified Motion Recipe validates");

        BistroBuilderAnimationBudgetProfile budgetProfile = ScriptableObject.CreateInstance<BistroBuilderAnimationBudgetProfile>();
        budgetProfile.maximumQ0Actors = 1;
        budgetProfile.maximumQ1Actors = 2;
        budgetProfile.protectedMinimumQuality = BistroBuilderAnimationQualityTier.Q2;
        Check(budgetProfile.ValidateConfiguration(out _), "Perceptual Animation Budget profile validates ordered caps");

        GameObject budgetGo = new GameObject("BB_AnimationV1_SelfTest_Budgeter");
        BistroBuilderPerceptualAnimationBudgeter budgeter = budgetGo.AddComponent<BistroBuilderPerceptualAnimationBudgeter>();
        budgeter.ConfigureForEditor(budgetProfile);
        budgeter.SetRuntimePressure(0f, 0f);

        var qh1 = new BistroBuilderAnimationExecutionHandle("q", 1);
        var qh2 = new BistroBuilderAnimationExecutionHandle("q", 2);
        var qh3 = new BistroBuilderAnimationExecutionHandle("q", 3);
        BistroBuilderAnimationQualityTier q1 = budgeter.Evaluate(qh1, 0f, false);
        budgeter.Assign(qh1, q1);
        BistroBuilderAnimationQualityTier q2 = budgeter.Evaluate(qh2, 0f, false);
        budgeter.Assign(qh2, q2);
        BistroBuilderAnimationQualityTier q3 = budgeter.Evaluate(qh3, 0f, false);
        budgeter.Assign(qh3, q3);
        Check(q1 == BistroBuilderAnimationQualityTier.Q0 && q2 == BistroBuilderAnimationQualityTier.Q1 && q3 == BistroBuilderAnimationQualityTier.Q2,
            "Budgeter enforces Q0 and Q0+Q1 actor caps deterministically");

        var protectedHandle = new BistroBuilderAnimationExecutionHandle("protected", 1);
        BistroBuilderAnimationQualityTier protectedTier = budgeter.Evaluate(protectedHandle, 0f, true);
        Check(protectedTier == BistroBuilderAnimationQualityTier.Q0,
            "Protected interaction can bypass crowd caps for contact-critical presentation");

        budgeter.Release(qh1);
        budgeter.Release(qh2);
        budgeter.Release(qh3);
        Check(budgeter.ActiveAssignmentCount == 0, "Budget assignments release without leaks");

        UnityEngine.Object.DestroyImmediate(budgetGo);
        UnityEngine.Object.DestroyImmediate(budgetProfile);
        UnityEngine.Object.DestroyImmediate(costRecipe);
        UnityEngine.Object.DestroyImmediate(repeatRecipe);
        UnityEngine.Object.DestroyImmediate(lowCostClip);
        UnityEngine.Object.DestroyImmediate(highCostClip);

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Result: " + ok + " OK / " + fail + " errors.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
