using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Adaptador Humanoid para IK y mirada contextual.
/// Animation Rigging corrige únicamente la presentación final; nunca decide gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCharacterRigAdapter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float weightResponse = 10f;
    [SerializeField, Range(0f, 1f)] private float defaultRotationWeight = 0.25f;
    [SerializeField, Range(0f, 1f)] private float defaultHintWeight = 0.35f;

    private RigBuilder rigBuilder;
    private Rig rig;
    private TwoBoneIKConstraint rightArm;
    private TwoBoneIKConstraint leftArm;
    private MultiAimConstraint headAim;
    private Transform rightTarget;
    private Transform leftTarget;
    private Transform rightHint;
    private Transform leftHint;
    private Transform lookTarget;
    private Transform rightSource;
    private Transform leftSource;
    private Transform lookSource;
    private float rightDesiredWeight;
    private float leftDesiredWeight;
    private float lookDesiredWeight;

    public bool IsReady { get; private set; }
    public Animator Animator => animator;
    public Transform RightHand => animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
    public Transform LeftHand => animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        EnsureRuntimeRig(out _);
    }

    private void LateUpdate()
    {
        if (!IsReady) return;
        SyncTarget(rightTarget, rightSource);
        SyncTarget(leftTarget, leftSource);
        SyncTarget(lookTarget, lookSource);
        rightArm.weight = MoveWeight(rightArm.weight, rightDesiredWeight);
        leftArm.weight = MoveWeight(leftArm.weight, leftDesiredWeight);
        headAim.weight = MoveWeight(headAim.weight, lookDesiredWeight);
    }
    public bool EnsureRuntimeRig(out string error)
    {
        error = string.Empty;
        if (IsReady) return true;
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            error = name + " necesita Animator Humanoid válido para Animation Rigging.";
            return false;
        }

        Transform rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (rightUpper == null || rightLower == null || rightHandBone == null ||
            leftUpper == null || leftLower == null || leftHandBone == null || head == null)
        {
            error = name + " no expone todos los huesos Humanoid requeridos.";
            return false;
        }
        rigBuilder = animator.GetComponent<RigBuilder>();
        if (rigBuilder == null) rigBuilder = animator.gameObject.AddComponent<RigBuilder>();

        GameObject rigObject = FindOrCreate(animator.transform, "BB_RuntimeRig");
        rig = rigObject.GetComponent<Rig>();
        if (rig == null) rig = rigObject.AddComponent<Rig>();
        rig.weight = 1f;

        rightTarget = FindOrCreate(rigObject.transform, "RightHandTarget").transform;
        leftTarget = FindOrCreate(rigObject.transform, "LeftHandTarget").transform;
        rightHint = FindOrCreate(rigObject.transform, "RightElbowHint").transform;
        leftHint = FindOrCreate(rigObject.transform, "LeftElbowHint").transform;
        lookTarget = FindOrCreate(rigObject.transform, "LookTarget").transform;

        rightTarget.SetPositionAndRotation(rightHandBone.position, rightHandBone.rotation);
        leftTarget.SetPositionAndRotation(leftHandBone.position, leftHandBone.rotation);
        rightHint.position = rightLower.position + animator.transform.forward * 0.25f;
        leftHint.position = leftLower.position + animator.transform.forward * 0.25f;
        lookTarget.position = head.position + animator.transform.forward * 2f;

        rightArm = ConfigureArm(rigObject.transform, "RightArmIK",
            rightUpper, rightLower, rightHandBone, rightTarget, rightHint);
        leftArm = ConfigureArm(rigObject.transform, "LeftArmIK",
            leftUpper, leftLower, leftHandBone, leftTarget, leftHint);
        headAim = ConfigureHeadAim(rigObject.transform, head, lookTarget);

        rigBuilder.layers.Clear();
        rigBuilder.layers.Add(new RigLayer(rig));
        rigBuilder.Build();
        rightArm.weight = 0f;
        leftArm.weight = 0f;
        headAim.weight = 0f;
        IsReady = true;
        return true;
    }

    public bool SetRightHandTarget(Transform target, float weight = 1f)
    {
        if (!EnsureRuntimeRig(out _)) return false;
        rightSource = target;
        rightDesiredWeight = target != null ? Mathf.Clamp01(weight) : 0f;
        return target != null;
    }

    public bool SetLeftHandTarget(Transform target, float weight = 1f)
    {
        if (!EnsureRuntimeRig(out _)) return false;
        leftSource = target;
        leftDesiredWeight = target != null ? Mathf.Clamp01(weight) : 0f;
        return target != null;
    }

    public bool SetLookTarget(Transform target, float weight = 0.35f)
    {
        if (!EnsureRuntimeRig(out _)) return false;
        lookSource = target;
        lookDesiredWeight = target != null ? Mathf.Clamp01(weight) : 0f;
        return target != null;
    }
    public void ClearInteractionTargets(bool clearLook = true)
    {
        rightSource = null;
        leftSource = null;
        rightDesiredWeight = 0f;
        leftDesiredWeight = 0f;
        if (clearLook)
        {
            lookSource = null;
            lookDesiredWeight = 0f;
        }
    }

    public void ClearInteractionTargetsImmediate(bool clearLook = true)
    {
        ClearInteractionTargets(clearLook);
        if (!IsReady) return;
        if (rightArm != null) rightArm.weight = 0f;
        if (leftArm != null) leftArm.weight = 0f;
        if (clearLook && headAim != null) headAim.weight = 0f;
    }
    public float GetHandError(bool right, Transform target)
    {
        Transform hand = right ? RightHand : LeftHand;
        if (hand == null || target == null) return float.PositiveInfinity;
        return Vector3.Distance(hand.position, target.position);
    }

    public bool ValidateConfiguration(out string error)
    {
        if (!EnsureRuntimeRig(out error)) return false;
        if (rigBuilder == null || rig == null || rightArm == null || leftArm == null || headAim == null)
        {
            error = name + " no construyó todos los constraints de Animation Rigging.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private float MoveWeight(float current, float desired)
    {
        return Mathf.MoveTowards(current, desired, Mathf.Max(0.1f, weightResponse) * Time.deltaTime);
    }
    private void SyncTarget(Transform proxy, Transform source)
    {
        if (proxy == null || source == null) return;
        proxy.SetPositionAndRotation(source.position, source.rotation);
    }

    private TwoBoneIKConstraint ConfigureArm(
        Transform parent,
        string objectName,
        Transform root,
        Transform mid,
        Transform tip,
        Transform target,
        Transform hint)
    {
        GameObject go = FindOrCreate(parent, objectName);
        TwoBoneIKConstraint constraint = go.GetComponent<TwoBoneIKConstraint>();
        if (constraint == null) constraint = go.AddComponent<TwoBoneIKConstraint>();
        TwoBoneIKConstraintData data = constraint.data;
        data.root = root;
        data.mid = mid;
        data.tip = tip;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = defaultRotationWeight;
        data.hintWeight = defaultHintWeight;
        data.maintainTargetPositionOffset = false;
        data.maintainTargetRotationOffset = false;
        constraint.data = data;
        return constraint;
    }
    private MultiAimConstraint ConfigureHeadAim(
        Transform parent,
        Transform head,
        Transform target)
    {
        GameObject go = FindOrCreate(parent, "HeadAim");
        MultiAimConstraint constraint = go.GetComponent<MultiAimConstraint>();
        if (constraint == null) constraint = go.AddComponent<MultiAimConstraint>();
        MultiAimConstraintData data = constraint.data;
        data.constrainedObject = head;
        WeightedTransformArray sources = new WeightedTransformArray(1);
        sources.Add(new WeightedTransform(target, 1f));
        data.sourceObjects = sources;
        data.aimAxis = MultiAimConstraintData.Axis.Z;
        data.upAxis = MultiAimConstraintData.Axis.Y;
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        data.maintainOffset = true;
        data.limits = new Vector2(-55f, 55f);
        data.constrainedXAxis = true;
        data.constrainedYAxis = true;
        data.constrainedZAxis = false;
        constraint.data = data;
        return constraint;
    }

    private static GameObject FindOrCreate(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing.gameObject;
        GameObject created = new GameObject(childName);
        created.transform.SetParent(parent, false);
        return created;
    }
}
