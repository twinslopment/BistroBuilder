using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BBMotionProfile",
    menuName = "Bistro Builder/Animation/Motion Profile")]
public sealed class BistroBuilderMotionProfile : ScriptableObject
{
    [SerializeField] private string motionId = "motion.generic";
    [SerializeField] private AnimationClip clip;
    [SerializeField] private string animatorStateName = string.Empty;
    [SerializeField, Min(0)] private int animatorLayer;
    [SerializeField] private BistroBuilderAnimationBodyMode bodyMode = BistroBuilderAnimationBodyMode.FullBody;
    [SerializeField] private AvatarMask avatarMask;
    [SerializeField] private bool loop;
    [SerializeField, Min(0.01f)] private float fallbackDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float nominalSpeedMetersPerSecond = 1.25f;
    [SerializeField] private bool mirrorable;
    [SerializeField] private string fallbackMotionId = string.Empty;
    [SerializeField] private List<BistroBuilderMotionSyncPoint> syncPoints = new List<BistroBuilderMotionSyncPoint>();
    [Header("Trazabilidad")]
    [SerializeField] private string sourceProvider = string.Empty;
    [SerializeField] private string sourceReference = string.Empty;
    [SerializeField] private string licenseNote = string.Empty;
    [SerializeField] private string sourceVersion = string.Empty;

    public string MotionId => NormalizeId(motionId);
    public AnimationClip Clip => clip;
    public string AnimatorStateName => animatorStateName ?? string.Empty;
    public int AnimatorLayer => Mathf.Max(0, animatorLayer);
    public BistroBuilderAnimationBodyMode BodyMode => bodyMode;
    public AvatarMask AvatarMask => avatarMask;
    public bool Loop => loop;
    public float NominalDuration => clip != null ? Mathf.Max(0.01f, clip.length) : Mathf.Max(0.01f, fallbackDuration);
    public float NominalSpeedMetersPerSecond => Mathf.Max(0.01f, nominalSpeedMetersPerSecond);
    public bool Mirrorable => mirrorable;
    public string FallbackMotionId => NormalizeId(fallbackMotionId);
    public IReadOnlyList<BistroBuilderMotionSyncPoint> SyncPoints => syncPoints;
    public string SourceProvider => sourceProvider ?? string.Empty;
    public string SourceReference => sourceReference ?? string.Empty;
    public string LicenseNote => licenseNote ?? string.Empty;
    public string SourceVersion => sourceVersion ?? string.Empty;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(MotionId))
        {
            error = name + " necesita MotionId.";
            return false;
        }
        float previous = -1f;
        for (int i = 0; i < syncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = syncPoints[i];
            if (marker == null || string.IsNullOrWhiteSpace(marker.MarkerId))
            {
                error = name + " contiene un SyncPoint inválido.";
                return false;
            }
            if (marker.NormalizedTime < previous)
            {
                error = name + " tiene SyncPoints fuera de orden temporal.";
                return false;
            }
            previous = marker.NormalizedTime;
        }
        return true;
    }

    public float FindMarkerTime01(string markerId, float fallback)
    {
        string normalized = NormalizeId(markerId);
        if (string.IsNullOrWhiteSpace(normalized)) return Mathf.Clamp01(fallback);
        for (int i = 0; i < syncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = syncPoints[i];
            if (marker != null && marker.MarkerId == normalized)
                return marker.NormalizedTime;
        }
        return Mathf.Clamp01(fallback);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredMotionId,
        AnimationClip configuredClip,
        BistroBuilderAnimationBodyMode configuredBodyMode,
        bool configuredLoop,
        float configuredNominalSpeed,
        bool configuredMirrorable,
        string configuredFallbackMotionId,
        string configuredSourceProvider,
        string configuredSourceReference,
        string configuredLicenseNote,
        string configuredSourceVersion)
    {
        motionId = configuredMotionId ?? string.Empty;
        clip = configuredClip;
        animatorStateName = string.Empty;
        animatorLayer = 0;
        bodyMode = configuredBodyMode;
        loop = configuredLoop;
        nominalSpeedMetersPerSecond = Mathf.Max(0.01f, configuredNominalSpeed);
        mirrorable = configuredMirrorable;
        fallbackMotionId = configuredFallbackMotionId ?? string.Empty;
        sourceProvider = configuredSourceProvider ?? string.Empty;
        sourceReference = configuredSourceReference ?? string.Empty;
        licenseNote = configuredLicenseNote ?? string.Empty;
        sourceVersion = configuredSourceVersion ?? string.Empty;
    }
#endif

    public static string NormalizeId(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().ToLowerInvariant();
}
