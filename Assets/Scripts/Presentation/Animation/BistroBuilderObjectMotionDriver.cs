using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderObjectMotionDriver : MonoBehaviour
{
    [SerializeField] private Transform movingPart;
    [SerializeField] private BistroBuilderObjectMotionProfile profile;
    [SerializeField] private BistroBuilderDynamicCirculationEnvelope spatialEnvelope;

    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Coroutine motionRoutine;

    public float Progress01 { get; private set; }
    public bool IsMoving => motionRoutine != null;
    public event Action<BistroBuilderObjectMotionDriver, float> ProgressChanged;

    private void Awake()
    {
        if (movingPart == null) movingPart = transform;
        if (spatialEnvelope == null) spatialEnvelope = GetComponent<BistroBuilderDynamicCirculationEnvelope>();
        CaptureClosedPose();
        ApplyProgress(Progress01);
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (movingPart == null)
        {
            error = name + " necesita MovingPart.";
            return false;
        }
        if (profile == null || !profile.ValidateConfiguration(out error))
            return false;
        return true;
    }

    public bool TryMoveTo(float targetProgress01, string ownerId)
    {
        if (!ValidateConfiguration(out _)) return false;
        if (motionRoutine != null) StopCoroutine(motionRoutine);
        motionRoutine = StartCoroutine(MoveRoutine(Mathf.Clamp01(targetProgress01), ownerId));
        return true;
    }

    public void SetProgressImmediate(float progress01)
    {
        if (movingPart == null) movingPart = transform;
        ApplyProgress(Mathf.Clamp01(progress01));
    }

    public void ReconcileTo(float authoritativeProgress01)
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }
        if (spatialEnvelope != null)
            spatialEnvelope.SetActiveWindow("motion:" + GetInstanceID(), false);
        SetProgressImmediate(authoritativeProgress01);
    }

    private IEnumerator MoveRoutine(float target, string ownerId)
    {
        string owner = string.IsNullOrWhiteSpace(ownerId)
            ? "motion:" + GetInstanceID()
            : ownerId.Trim();
        spatialEnvelope?.SetActiveWindow(owner, true);
        float start = Progress01;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, profile.Duration * Mathf.Abs(target - start));
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyProgress(Mathf.Lerp(start, target, profile.Curve.Evaluate(t)));
            yield return null;
        }
        ApplyProgress(target);
        spatialEnvelope?.SetActiveWindow(owner, false);
        motionRoutine = null;
    }

    private void CaptureClosedPose()
    {
        if (movingPart == null) return;
        closedLocalPosition = movingPart.localPosition;
        closedLocalRotation = movingPart.localRotation;
    }

    private void ApplyProgress(float progress)
    {
        Progress01 = Mathf.Clamp01(progress);
        if (movingPart == null || profile == null)
        {
            ProgressChanged?.Invoke(this, Progress01);
            return;
        }

        Vector3 axis = profile.LocalAxis;
        switch (profile.Primitive)
        {
            case BistroBuilderObjectMotionPrimitive.Hinge:
            case BistroBuilderObjectMotionPrimitive.Rotate:
                movingPart.localPosition = closedLocalPosition;
                movingPart.localRotation = closedLocalRotation *
                    Quaternion.AngleAxis(profile.Amount * Progress01, axis);
                break;
            case BistroBuilderObjectMotionPrimitive.Slide:
            case BistroBuilderObjectMotionPrimitive.Translate:
                movingPart.localRotation = closedLocalRotation;
                movingPart.localPosition = closedLocalPosition + axis * profile.Amount * Progress01;
                break;
        }
        ProgressChanged?.Invoke(this, Progress01);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Transform configuredMovingPart,
        BistroBuilderObjectMotionProfile configuredProfile,
        BistroBuilderDynamicCirculationEnvelope configuredEnvelope = null)
    {
        movingPart = configuredMovingPart;
        profile = configuredProfile;
        spatialEnvelope = configuredEnvelope;
        if (movingPart != null) CaptureClosedPose();
    }
#endif
}
