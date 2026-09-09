using UnityEngine;

/// <summary>
/// Mantiene la presentación visual de objetos transportados sobre locomoción base.
/// El ownership sigue perteneciendo a gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCarryPresenter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private BistroBuilderCarrySocketSet sockets;
    [SerializeField] private BistroBuilderCharacterRigAdapter rigAdapter;

    private BistroBuilderTransferableVisual currentVisual;
    private BistroBuilderCarryableDescriptor currentDescriptor;
    private Transform plateCarryTarget;

    public bool IsCarrying => currentVisual != null;
    public BistroBuilderCarryMode CurrentCarryMode => currentDescriptor != null
        ? currentDescriptor.CarryMode
        : BistroBuilderCarryMode.GenericOneHand;

    private void Awake()
    {
        EnsureConfigured(out _);
    }

    public bool EnsureConfigured(out string error)
    {
        error = string.Empty;
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (sockets == null) sockets = GetComponent<BistroBuilderCarrySocketSet>();
        if (sockets == null) sockets = gameObject.AddComponent<BistroBuilderCarrySocketSet>();
        if (rigAdapter == null) rigAdapter = GetComponent<BistroBuilderCharacterRigAdapter>();
        if (rigAdapter == null) rigAdapter = gameObject.AddComponent<BistroBuilderCharacterRigAdapter>();
        if (!sockets.EnsureFromHumanoid(animator, out error)) return false;
        if (!rigAdapter.EnsureRuntimeRig(out error)) return false;
        EnsurePlateCarryTarget();
        return true;
    }

    public bool TryResolveSocket(
        BistroBuilderCarryableDescriptor descriptor,
        out Transform socket)
    {
        socket = null;
        if (!EnsureConfigured(out _)) return false;
        BistroBuilderCarrySocketKind kind = descriptor != null
            ? descriptor.ResolveSocketKind()
            : BistroBuilderCarrySocketKind.RightHand;
        return sockets.TryGet(kind, out socket);
    }

    public bool ReconcileCarriedVisual(
        BistroBuilderTransferableVisual visual,
        BistroBuilderCarryableDescriptor descriptor)
    {
        if (visual == null || !TryResolveSocket(descriptor, out Transform socket))
            return false;
        if (!visual.AttachVisual(socket)) return false;
        if (descriptor != null)
        {
            visual.transform.localPosition = descriptor.CarriedLocalPosition;
            visual.transform.localRotation = descriptor.CarriedLocalRotation;
        }
        BeginCarryPose(visual, descriptor);
        return true;
    }
    public void BeginCarryPose(
        BistroBuilderTransferableVisual visual,
        BistroBuilderCarryableDescriptor descriptor)
    {
        if (!EnsureConfigured(out _)) return;
        currentVisual = visual;
        currentDescriptor = descriptor;
        rigAdapter.ClearInteractionTargets(false);

        if (descriptor == null)
        {
            rigAdapter.SetLookTarget(visual != null ? visual.transform : null, 0.15f);
            return;
        }

        switch (descriptor.CarryMode)
        {
            case BistroBuilderCarryMode.Plate:
                rigAdapter.SetRightHandTarget(plateCarryTarget, 1f);
                break;
            case BistroBuilderCarryMode.Tray:
                rigAdapter.SetRightHandTarget(descriptor.RightGrip, 0.95f);
                rigAdapter.SetLeftHandTarget(descriptor.LeftGrip, 0.75f);
                break;
            case BistroBuilderCarryMode.BoxTwoHand:
                rigAdapter.SetRightHandTarget(descriptor.RightGrip, 1f);
                rigAdapter.SetLeftHandTarget(descriptor.LeftGrip, 1f);
                break;
        }
        rigAdapter.SetLookTarget(descriptor.LookTarget, 0.18f);
    }
    public void EndCarryPose()
    {
        currentVisual = null;
        currentDescriptor = null;
        if (rigAdapter != null) rigAdapter.ClearInteractionTargets();
    }

    public float CurrentRightHandError
    {
        get
        {
            if (rigAdapter == null || currentDescriptor == null) return float.PositiveInfinity;
            Transform target = currentDescriptor.CarryMode == BistroBuilderCarryMode.Plate
                ? plateCarryTarget
                : currentDescriptor.RightGrip;
            return rigAdapter.GetHandError(true, target);
        }
    }

    public float CurrentLeftHandError =>
        rigAdapter != null && currentDescriptor != null && currentDescriptor.UsesLeftHandIK
            ? rigAdapter.GetHandError(false, currentDescriptor.LeftGrip)
            : 0f;
    public bool ValidateConfiguration(out string error)
    {
        if (!EnsureConfigured(out error)) return false;
        if (plateCarryTarget == null)
        {
            error = name + " no pudo crear PlateCarryTarget.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void EnsurePlateCarryTarget()
    {
        if (plateCarryTarget != null || animator == null) return;
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null) return;
        Transform existing = chest.Find("BB_PlateCarryTarget");
        if (existing != null)
        {
            plateCarryTarget = existing;
            return;
        }
        GameObject go = new GameObject("BB_PlateCarryTarget");
        plateCarryTarget = go.transform;
        plateCarryTarget.SetParent(chest, false);
        plateCarryTarget.localPosition = new Vector3(0.24f, -0.28f, 0.35f);
        plateCarryTarget.localRotation = Quaternion.Euler(0f, -12f, 0f);
    }
}
