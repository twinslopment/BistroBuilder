using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAnimationV1Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string DataRoot = "Assets/Data/Animation/V1";
    private const string RecipesFolder = DataRoot + "/Recipes";
    private const string FamiliesFolder = DataRoot + "/Families";
    private const string CatalogPath = DataRoot + "/BistroBuilderMotionRecipeCatalog.asset";
    private const string BudgetPath = DataRoot + "/BistroBuilderAnimationBudgetProfile.asset";
    private const string LegacyCatalogPath = "Assets/Data/Animation/BistroBuilderMotionCatalog.asset";

    [MenuItem("Bistro Builder/Animation V1/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderAnimationV1Validator.Run();
    }

    public static void Install()
    {
        EnsureFolders();
        BistroBuilderMotionRecipeCatalog catalog = CompileMotionRecipes();
        EnsureFamilyProfiles();
        BistroBuilderAnimationBudgetProfile budgetProfile = EnsureBudgetProfile();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null) throw new InvalidOperationException("Animation V1 requires GameSystems in Prototype_Restaurant.");

        BistroBuilderPerceptualAnimationBudgeter budgeter = EnsureComponent<BistroBuilderPerceptualAnimationBudgeter>(systems);
        budgeter.ConfigureForEditor(budgetProfile);
        BistroBuilderCharacterAnimationServiceV1 service = EnsureComponent<BistroBuilderCharacterAnimationServiceV1>(systems);
        service.ConfigureForEditor(catalog, budgeter);
        MigrateSceneActors();
        MigrateSceneTargets();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BB CHARACTER & INTERACTION ANIMATION V1 - FOUNDATION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static BistroBuilderMotionRecipeCatalog CompileMotionRecipes()
    {
        string[] guids = AssetDatabase.FindAssets("t:BistroBuilderMotionProfile", new[] { "Assets/Data/Animation/Motions" });
        var sourceById = new Dictionary<string, BistroBuilderMotionProfile>(StringComparer.Ordinal);
        foreach (string guid in guids)
        {
            BistroBuilderMotionProfile profile = AssetDatabase.LoadAssetAtPath<BistroBuilderMotionProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile != null && !string.IsNullOrWhiteSpace(profile.MotionId)) sourceById[profile.MotionId] = profile;
        }
        if (sourceById.Count == 0) throw new InvalidOperationException("No legacy Motion Profiles available to compile V1 recipes.");

        var recipes = new List<BistroBuilderMotionRecipe>();
        foreach (BistroBuilderMotionProfile source in sourceById.Values.OrderBy(p => p.MotionId, StringComparer.Ordinal))
            recipes.Add(EnsureRecipe(source.MotionId, source, source.FallbackMotionId));

        AddAlias(recipes, sourceById, "carry.standard.1h", "transfer.table.1h", "transfer.generic");
        AddAlias(recipes, sourceById, "carry.standard.2h", "transfer.generic", "transfer.table.1h");
        AddAlias(recipes, sourceById, "workstation.use.standard", "transfer.generic", "transfer.table.1h");
        AddAlias(recipes, sourceById, "appliance.use.standard", "transfer.generic", "workstation.use.standard");
        AddAlias(recipes, sourceById, "handover.give.standard", "transfer.table.1h", "transfer.generic");
        AddAlias(recipes, sourceById, "handover.receive.standard", "transfer.table.1h", "transfer.generic");
        AddAlias(recipes, sourceById, "social.converse.standard", "social.talk.standard", "locomotion.idle");

        BistroBuilderMotionRecipeCatalog catalog = AssetDatabase.LoadAssetAtPath<BistroBuilderMotionRecipeCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderMotionRecipeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.ConfigureForEditor(recipes.OrderBy(r => r.MotionId, StringComparer.Ordinal).ToList());
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AddAlias(List<BistroBuilderMotionRecipe> recipes, Dictionary<string, BistroBuilderMotionProfile> sourceById, string aliasId, string sourceId, string fallbackId)
    {
        if (!sourceById.TryGetValue(sourceId, out BistroBuilderMotionProfile source)) throw new InvalidOperationException("Missing source motion for alias " + aliasId + ": " + sourceId);
        recipes.Add(EnsureRecipe(aliasId, source, fallbackId));
    }

    private static BistroBuilderMotionRecipe EnsureRecipe(string motionId, BistroBuilderMotionProfile source, string fallbackId)
    {
        string safe = motionId.Replace('.', '_');
        string path = RecipesFolder + "/" + safe + ".asset";
        BistroBuilderMotionRecipe recipe = AssetDatabase.LoadAssetAtPath<BistroBuilderMotionRecipe>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<BistroBuilderMotionRecipe>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        var variant = new BistroBuilderMotionRecipeVariant();
        variant.ConfigureForEditor(
            "primary",
            source.Clip,
            source.AnimatorStateName,
            source.AnimatorLayer,
            source.AvatarMask,
            false,
            1f,
            DetermineMinimumTier(motionId),
            source.BodyMode == BistroBuilderAnimationBodyMode.FullBody ? 0.40f : 0.25f);

        var markers = CloneMarkers(source);
        EnsureMarker(markers, "contact", 0.50f, BistroBuilderMotionSyncPointKind.SyncMarker);
        EnsureMarker(markers, "interrupt.safe", 0.82f, BistroBuilderMotionSyncPointKind.Gate);
        markers.Sort((a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));

        bool certified = source.Clip != null && !string.IsNullOrWhiteSpace(source.LicenseNote);
        recipe.ConfigureForEditor(
            motionId,
            source.BodyMode,
            source.Loop,
            source.NominalSpeedMetersPerSecond,
            source.Mirrorable,
            DetermineInterruptPolicy(motionId),
            fallbackId,
            new List<BistroBuilderMotionRecipeVariant> { variant },
            markers,
            certified,
            "bbv1:" + motionId + ":ual1",
            "bb.motion.compiler.v1",
            "1.0",
            0.85f,
            1.15f,
            source.SourceProvider,
            source.SourceReference,
            source.LicenseNote,
            source.SourceVersion);
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static List<BistroBuilderMotionSyncPoint> CloneMarkers(BistroBuilderMotionProfile source)
    {
        var result = new List<BistroBuilderMotionSyncPoint>();
        for (int i = 0; i < source.SyncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = source.SyncPoints[i];
            if (marker == null) continue;
            var clone = new BistroBuilderMotionSyncPoint();
            clone.ConfigureForEditor(marker.MarkerId, marker.NormalizedTime, marker.Kind);
            result.Add(clone);
        }
        return result;
    }

    private static void EnsureMarker(List<BistroBuilderMotionSyncPoint> markers, string id, float time, BistroBuilderMotionSyncPointKind kind)
    {
        if (markers.Any(m => m != null && m.MarkerId == id)) return;
        var marker = new BistroBuilderMotionSyncPoint();
        marker.ConfigureForEditor(id, time, kind);
        markers.Add(marker);
    }

    private static BistroBuilderAnimationQualityTier DetermineMinimumTier(string motionId)
    {
        return motionId.StartsWith("locomotion.", StringComparison.Ordinal) ? BistroBuilderAnimationQualityTier.Q0 : BistroBuilderAnimationQualityTier.Q1;
    }

    private static BistroBuilderMotionInterruptPolicy DetermineInterruptPolicy(string motionId)
    {
        if (motionId.StartsWith("portal.", StringComparison.Ordinal) || motionId.StartsWith("handover.", StringComparison.Ordinal)) return BistroBuilderMotionInterruptPolicy.AfterCommit;
        if (motionId == "locomotion.idle") return BistroBuilderMotionInterruptPolicy.Immediate;
        return BistroBuilderMotionInterruptPolicy.AtSafeMarker;
    }

    private static void EnsureFamilyProfiles()
    {
        EnsureFamily("seat.v1", BistroBuilderInteractionFamily.Seat, new[] { BistroBuilderInteractionOperation.Sit, BistroBuilderInteractionOperation.Stand }, "seat.sit.standard", "seat.generic", true);
        EnsureFamily("portal.v1", BistroBuilderInteractionFamily.Portal, new[] { BistroBuilderInteractionOperation.Open, BistroBuilderInteractionOperation.Close }, "portal.open.standard", "transfer.generic", true);
        EnsureFamily("transfer.v1", BistroBuilderInteractionFamily.Transfer, new[] { BistroBuilderInteractionOperation.Pickup, BistroBuilderInteractionOperation.Place }, "transfer.table.1h", "transfer.generic", true);
        EnsureFamily("carry.v1", BistroBuilderInteractionFamily.Carry, new[] { BistroBuilderInteractionOperation.Pickup, BistroBuilderInteractionOperation.Place, BistroBuilderInteractionOperation.Use }, "carry.standard.1h", "carry.standard.2h", false);
        EnsureFamily("workstation.v1", BistroBuilderInteractionFamily.Workstation, new[] { BistroBuilderInteractionOperation.Use }, "workstation.use.standard", "transfer.generic", true);
        EnsureFamily("appliance.v1", BistroBuilderInteractionFamily.Appliance, new[] { BistroBuilderInteractionOperation.Use }, "appliance.use.standard", "workstation.use.standard", true);
        EnsureFamily("handover.v1", BistroBuilderInteractionFamily.Handover, new[] { BistroBuilderInteractionOperation.Give, BistroBuilderInteractionOperation.Receive }, "handover.give.standard", "handover.receive.standard", true);
        EnsureFamily("social.v1", BistroBuilderInteractionFamily.Social, new[] { BistroBuilderInteractionOperation.Converse }, "social.converse.standard", "social.talk.standard", false);
    }

    private static void EnsureFamily(string id, BistroBuilderInteractionFamily family, IEnumerable<BistroBuilderInteractionOperation> operations, string primary, string fallback, bool commit)
    {
        string path = FamiliesFolder + "/" + id.Replace('.', '_') + ".asset";
        BistroBuilderInteractionFamilyProfile profile = AssetDatabase.LoadAssetAtPath<BistroBuilderInteractionFamilyProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<BistroBuilderInteractionFamilyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        profile.ConfigureForEditor(id, family, operations.ToList(), primary, fallback, commit);
        EditorUtility.SetDirty(profile);
    }

    private static BistroBuilderAnimationBudgetProfile EnsureBudgetProfile()
    {
        BistroBuilderAnimationBudgetProfile profile = AssetDatabase.LoadAssetAtPath<BistroBuilderAnimationBudgetProfile>(BudgetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<BistroBuilderAnimationBudgetProfile>();
            AssetDatabase.CreateAsset(profile, BudgetPath);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void MigrateSceneActors()
    {
        BistroBuilderMotionCatalog legacyCatalog = AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(LegacyCatalogPath);
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Animator animator in animators)
        {
            if (animator == null || animator.gameObject.scene != SceneManager.GetActiveScene()) continue;
            animator.applyRootMotion = false;
            GameObject root = animator.transform.root.gameObject;
            BistroBuilderCharacterAnimationDriver driver = EnsureComponent<BistroBuilderCharacterAnimationDriver>(root);
            driver.ConfigureForEditor(animator, legacyCatalog);
            EnsureComponent<BistroBuilderLocomotionAnimationPresenter>(root);
            BistroBuilderMotionRecipePlayerV1 player = EnsureComponent<BistroBuilderMotionRecipePlayerV1>(root);
            player.ConfigureForEditor(animator, driver);
            BistroBuilderAnimationActorBinding actor = EnsureComponent<BistroBuilderAnimationActorBinding>(root);
            actor.ConfigureForEditor(string.Empty, driver, player, root.GetComponent<BistroBuilderCharacterRigAdapter>(), root.GetComponent<BistroBuilderCarryPresenter>());
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(root);
        }
    }

    private static void MigrateSceneTargets()
    {
        BistroBuilderAssetInteractionDescriptor[] descriptors = UnityEngine.Object.FindObjectsByType<BistroBuilderAssetInteractionDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BistroBuilderAssetInteractionDescriptor descriptor in descriptors)
        {
            if (descriptor == null || descriptor.gameObject.scene != SceneManager.GetActiveScene() || descriptor.Slots.Count == 0) continue;
            var slots = new List<BistroBuilderAnimationTargetSlot>();
            for (int i = 0; i < descriptor.Slots.Count; i++)
            {
                BistroBuilderAnimationInteractionSlotDefinition old = descriptor.Slots[i];
                if (old == null || old.InteractionFrame == null) continue;
                var slot = new BistroBuilderAnimationTargetSlot();
                slot.ConfigureForEditor(old.SlotId, old.Family, old.InteractionFrame, old.SeatFrame, old.ExitFrame, old.RightHandTarget, old.LeftHandTarget, old.LookTarget);
                slots.Add(slot);
            }
            if (slots.Count == 0) continue;
            BistroBuilderInteractionTarget logicalTarget = descriptor.GetComponent<BistroBuilderInteractionTarget>();
            string stableId = logicalTarget != null && !string.IsNullOrWhiteSpace(logicalTarget.TargetId)
                ? logicalTarget.TargetId
                : "animation-target:" + GlobalObjectId.GetGlobalObjectIdSlow(descriptor).ToString();
            BistroBuilderAnimationTargetBinding binding = EnsureComponent<BistroBuilderAnimationTargetBinding>(descriptor.gameObject);
            binding.ConfigureForEditor(stableId, 1L, MapTargetKind(slots[0].Family), slots);
            EditorUtility.SetDirty(binding);
        }
    }

    private static BistroBuilderAnimationTargetKind MapTargetKind(BistroBuilderInteractionFamily family)
    {
        switch (family)
        {
            case BistroBuilderInteractionFamily.Seat: return BistroBuilderAnimationTargetKind.Seat;
            case BistroBuilderInteractionFamily.Portal: return BistroBuilderAnimationTargetKind.Portal;
            case BistroBuilderInteractionFamily.Transfer: return BistroBuilderAnimationTargetKind.Transferable;
            case BistroBuilderInteractionFamily.Workstation: return BistroBuilderAnimationTargetKind.Workstation;
            case BistroBuilderInteractionFamily.Appliance: return BistroBuilderAnimationTargetKind.Appliance;
            case BistroBuilderInteractionFamily.Handover: return BistroBuilderAnimationTargetKind.Handover;
            case BistroBuilderInteractionFamily.Social: return BistroBuilderAnimationTargetKind.Social;
            default: return BistroBuilderAnimationTargetKind.Transferable;
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Data/Animation", "V1");
        EnsureFolder(DataRoot, "Recipes");
        EnsureFolder(DataRoot, "Families");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
