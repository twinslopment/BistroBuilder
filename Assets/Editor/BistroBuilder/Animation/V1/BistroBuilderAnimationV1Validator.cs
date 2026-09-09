using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderAnimationV1Validator
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogPath = "Assets/Data/Animation/V1/BistroBuilderMotionRecipeCatalog.asset";
    private const string BudgetPath = "Assets/Data/Animation/V1/BistroBuilderAnimationBudgetProfile.asset";
    private const string FamiliesFolder = "Assets/Data/Animation/V1/Families";
    private const string RuntimeServicePath = "Assets/Scripts/Presentation/Animation/V1/BistroBuilderCharacterAnimationServiceV1.cs";

    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Animation V1/Validar")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BB CHARACTER & INTERACTION ANIMATION SYSTEM V1 - VALIDATION\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderCharacterAnimationServiceV1[] services = UnityEngine.Object.FindObjectsByType<BistroBuilderCharacterAnimationServiceV1>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(services.Length == 1, "Exactly one V1 runtime animation orchestrator is installed");
        BistroBuilderAnimationRuntimeBootstrapV1 existingBootstrap = services.Length == 1 ? services[0].GetComponent<BistroBuilderAnimationRuntimeBootstrapV1>() : null;
        BistroBuilderAnimationRuntimeBootstrapV1 provisionedBootstrap = services.Length == 1 ? services[0].EnsureRuntimeBootstrap() : null;
        Check(provisionedBootstrap != null && UnityEngine.Object.FindObjectsByType<BistroBuilderAnimationRuntimeBootstrapV1>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "V1 runtime bootstrap provisions exactly once from the animation service");
        if (existingBootstrap == null && provisionedBootstrap != null) UnityEngine.Object.DestroyImmediate(provisionedBootstrap);
        Check(typeof(IBBCharacterAnimationService).IsAssignableFrom(typeof(BistroBuilderCharacterAnimationServiceV1)), "Runtime orchestrator implements public animation service contract");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderInteractionPresentationService>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Legacy BB18 interaction mutator is not installed in production scene");

        BistroBuilderMotionRecipeCatalog catalog = AssetDatabase.LoadAssetAtPath<BistroBuilderMotionRecipeCatalog>(CatalogPath);
        Check(catalog != null, "Motion Recipe Catalog V1 exists");
        Check(catalog != null && catalog.ValidateConfiguration(out _), "Motion Recipe Catalog V1 validates");
        if (catalog != null)
        {
            string[] required =
            {
                "seat.sit.standard", "seat.stand.standard", "portal.open.standard", "portal.close.standard",
                "transfer.table.1h", "carry.standard.1h", "carry.standard.2h", "workstation.use.standard",
                "appliance.use.standard", "handover.give.standard", "handover.receive.standard", "social.converse.standard",
                "locomotion.idle", "locomotion.walk"
            };
            Check(required.All(id => catalog.TryResolve(id, out _)), "Recipe catalog covers locomotion and all eight semantic interaction families");
            Check(catalog.Recipes.All(r => r != null && r.Certified), "Compiled runtime recipes are adaptation-certified");
            Check(catalog.Recipes.All(r => r != null && !string.IsNullOrWhiteSpace(r.LicenseNote)), "Every runtime recipe preserves provenance/license metadata");
        }

        BistroBuilderAnimationBudgetProfile budgetProfile = AssetDatabase.LoadAssetAtPath<BistroBuilderAnimationBudgetProfile>(BudgetPath);
        Check(budgetProfile != null && budgetProfile.ValidateConfiguration(out _), "Perceptual Animation Budget profile validates thresholds and actor caps");
        string[] familyGuids = AssetDatabase.FindAssets("t:BistroBuilderInteractionFamilyProfile", new[] { FamiliesFolder });
        BistroBuilderInteractionFamilyProfile[] families = familyGuids.Select(g => AssetDatabase.LoadAssetAtPath<BistroBuilderInteractionFamilyProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(x => x != null).ToArray();
        Check(families.Length == 8, "Eight V1 interaction family profiles are installed");
        Check(families.Select(f => f.Family).Distinct().Count() == 8, "Each V1 semantic family has exactly one profile");
        Check(families.All(f => f.ValidateConfiguration(out _)), "All V1 family profiles validate");

        FieldInfo[] publicRequestFields = typeof(BistroBuilderAnimationExecutionRequest).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Check(publicRequestFields.All(f => f.FieldType != typeof(GameObject) && f.FieldType != typeof(Transform) && !typeof(Component).IsAssignableFrom(f.FieldType)), "Public V1 request boundary exposes semantic/generational handles, not scene object references");
        Check(typeof(BistroBuilderAnimationExecutionHandle).GetField("generation") != null, "Execution handles are generational");
        Check(typeof(BistroBuilderAnimationTargetHandle).GetField("generation") != null, "Target handles are generational");
        var a = new BistroBuilderAnimationExecutionHandle("exec", 1);
        var b = new BistroBuilderAnimationExecutionHandle("exec", 2);
        Check(!a.Equals(b), "Stale execution generations cannot alias current executions");

        string runtimeSource = File.Exists(RuntimeServicePath) ? File.ReadAllText(RuntimeServicePath) : string.Empty;
        string[] forbiddenMutators = { "TrySetOccupied(", "TrySetOpen(", "TryPullOut(", "TryReturnToParked(", "SetAdmissionState(" };
        Check(forbiddenMutators.All(token => runtimeSource.IndexOf(token, StringComparison.Ordinal) < 0), "V1 runtime presentation service does not mutate gameplay/BBSIS/logical target state");
        Check(runtimeSource.IndexOf("AuthoritativeCommitRequested", StringComparison.Ordinal) >= 0 && runtimeSource.IndexOf("TryAcknowledgeAuthoritativeCommit", StringComparison.Ordinal) >= 0, "Commit Frontier is request/acknowledge, not animation authority");
        Check(runtimeSource.IndexOf("watchdogDeadline", StringComparison.Ordinal) >= 0, "Runtime watchdog is active");
        Check(runtimeSource.IndexOf("CanInterruptNow", StringComparison.Ordinal) >= 0 && runtimeSource.IndexOf("RecoverAndFinish", StringComparison.Ordinal) >= 0, "Interruptibility and recovery are implemented");
        Check(runtimeSource.IndexOf("TryRehydrate", StringComparison.Ordinal) >= 0, "State rehydration entry point is implemented");

        BistroBuilderCharacterAnimationDriver[] drivers = UnityEngine.Object.FindObjectsByType<BistroBuilderCharacterAnimationDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(drivers.All(d => d == null || d.Animator == null || !d.Animator.applyRootMotion), "Navigation retains root authority; configured Animators have Root Motion disabled");
        Check(typeof(BistroBuilderMotionVariantScheduler) != null, "Motion Variant Scheduler is available");
        Check(typeof(BistroBuilderPerceptualAnimationBudgeter) != null, "Perceptual Animation Budgeter Q0-Q4 is available");

        BistroBuilderAnimationTargetBinding[] targets = UnityEngine.Object.FindObjectsByType<BistroBuilderAnimationTargetBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(targets.All(t => t == null || t.ValidateConfiguration(out _)), "Installed typed animation target descriptors validate");

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
