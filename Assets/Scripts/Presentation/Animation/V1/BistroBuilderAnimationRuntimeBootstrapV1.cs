using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Discovers animation presentation adapters for actors and targets materialized after scene load.
/// Gameplay, Interaction/Reservation, BBSIS and Navigation remain authoritative.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAnimationRuntimeBootstrapV1 : MonoBehaviour
{
    [SerializeField] private BistroBuilderCharacterAnimationServiceV1 service;
    [SerializeField, Min(0.1f)] private float scanIntervalSeconds = 0.5f;

    private float nextScanAt;
    public int LastActorsAdded { get; private set; }
    public int LastTargetsAdded { get; private set; }

    private void Awake()
    {
        if (service == null) service = GetComponent<BistroBuilderCharacterAnimationServiceV1>();
        ScanNow();
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextScanAt) return;
        ScanNow();
    }

    public void ConfigureRuntime(BistroBuilderCharacterAnimationServiceV1 configuredService, float configuredScanIntervalSeconds = 0.5f)
    {
        service = configuredService != null ? configuredService : GetComponent<BistroBuilderCharacterAnimationServiceV1>();
        scanIntervalSeconds = Mathf.Max(0.1f, configuredScanIntervalSeconds);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderCharacterAnimationServiceV1 configuredService, float configuredScanIntervalSeconds = 0.5f)
    {
        ConfigureRuntime(configuredService, configuredScanIntervalSeconds);
    }
#endif

    public void ScanNow()
    {
        LastActorsAdded = BootstrapActors();
        LastTargetsAdded = BootstrapTargets();
        if (service == null) service = GetComponent<BistroBuilderCharacterAnimationServiceV1>();
        service?.RefreshRegistry();
        nextScanAt = Time.unscaledTime + Mathf.Max(0.1f, scanIntervalSeconds);
    }

    public bool TryBootstrapActor(GameObject root)
    {
        if (root == null || !root.scene.IsValid()) return false;
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null) return false;

        BistroBuilderAnimationActorBinding binding = root.GetComponent<BistroBuilderAnimationActorBinding>();
        BistroBuilderCharacterAnimationDriver driver = root.GetComponent<BistroBuilderCharacterAnimationDriver>();
        bool semanticActor = root.GetComponent<Waiter>() != null || root.GetComponent<CustomerGroup>() != null || driver != null || binding != null;
        if (!semanticActor) return false;

        BistroBuilderMotionRecipePlayerV1 player = root.GetComponent<BistroBuilderMotionRecipePlayerV1>();
        if (player == null) player = root.AddComponent<BistroBuilderMotionRecipePlayerV1>();
        player.ConfigureRuntime(animator, driver);
        if (binding == null) binding = root.AddComponent<BistroBuilderAnimationActorBinding>();
        binding.ConfigureRuntime(string.Empty, driver, player, root.GetComponent<BistroBuilderCharacterRigAdapter>(), root.GetComponent<BistroBuilderCarryPresenter>());
        service?.RefreshRegistry();
        return binding.RecipePlayer != null && !string.IsNullOrWhiteSpace(binding.ActorId);
    }

    public bool TryBootstrapTarget(BistroBuilderAssetInteractionDescriptor descriptor)
    {
        if (descriptor == null || descriptor.Slots == null || descriptor.Slots.Count == 0) return false;
        BistroBuilderInteractionTarget logicalTarget = descriptor.GetComponent<BistroBuilderInteractionTarget>();
        if (logicalTarget == null || string.IsNullOrWhiteSpace(logicalTarget.TargetId)) return false;

        List<BistroBuilderAnimationTargetSlot> slots = BuildSlots(descriptor);
        if (slots.Count == 0) return false;
        BistroBuilderAnimationTargetBinding binding = descriptor.GetComponent<BistroBuilderAnimationTargetBinding>();
        if (binding == null) binding = descriptor.gameObject.AddComponent<BistroBuilderAnimationTargetBinding>();
        binding.ConfigureRuntime(logicalTarget.TargetId, Math.Max(1L, binding.Generation), MapTargetKind(slots[0].Family), slots);
        service?.RefreshRegistry();
        return binding.ValidateConfiguration(out _);
    }

    private int BootstrapActors()
    {
        int before = FindObjectsByType<BistroBuilderAnimationActorBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var visited = new HashSet<int>();
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null) continue;
            GameObject root = animator.transform.root.gameObject;
            if (root != null && visited.Add(root.GetInstanceID())) TryBootstrapActor(root);
        }
        int after = FindObjectsByType<BistroBuilderAnimationActorBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        return Mathf.Max(0, after - before);
    }

    private int BootstrapTargets()
    {
        int before = FindObjectsByType<BistroBuilderAnimationTargetBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        BistroBuilderAssetInteractionDescriptor[] descriptors = FindObjectsByType<BistroBuilderAssetInteractionDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < descriptors.Length; i++) TryBootstrapTarget(descriptors[i]);
        int after = FindObjectsByType<BistroBuilderAnimationTargetBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        return Mathf.Max(0, after - before);
    }

    private static List<BistroBuilderAnimationTargetSlot> BuildSlots(BistroBuilderAssetInteractionDescriptor descriptor)
    {
        var result = new List<BistroBuilderAnimationTargetSlot>();
        for (int i = 0; i < descriptor.Slots.Count; i++)
        {
            BistroBuilderAnimationInteractionSlotDefinition source = descriptor.Slots[i];
            if (source == null || source.InteractionFrame == null) continue;
            var slot = new BistroBuilderAnimationTargetSlot();
            slot.ConfigureRuntime(source.SlotId, source.Family, source.InteractionFrame, source.SeatFrame, source.ExitFrame, source.RightHandTarget, source.LeftHandTarget, source.LookTarget);
            result.Add(slot);
        }
        return result;
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
}
