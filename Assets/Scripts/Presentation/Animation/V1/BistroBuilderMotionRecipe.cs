using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderMotionRecipeVariant
{
    [SerializeField] private string variantId = "default";
    [SerializeField] private AnimationClip clip;
    [SerializeField] private string animatorStateName = string.Empty;
    [SerializeField, Min(0)] private int animatorLayer;
    [SerializeField] private AvatarMask avatarMask;
    [SerializeField] private bool mirrored;
    [SerializeField, Min(0.01f)] private float weight = 1f;
    [SerializeField] private BistroBuilderAnimationQualityTier minimumQualityTier = BistroBuilderAnimationQualityTier.Q1;
    [SerializeField, Range(0f, 1f)] private float estimatedCpuCost = 0.25f;

    public string VariantId => BistroBuilderMotionProfile.NormalizeId(variantId);
    public AnimationClip Clip => clip;
    public string AnimatorStateName => animatorStateName ?? string.Empty;
    public int AnimatorLayer => Mathf.Max(0, animatorLayer);
    public AvatarMask AvatarMask => avatarMask;
    public bool Mirrored => mirrored;
    public float Weight => Mathf.Max(0.01f, weight);
    public BistroBuilderAnimationQualityTier MinimumQualityTier => minimumQualityTier;
    public float EstimatedCpuCost => Mathf.Clamp01(estimatedCpuCost);

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredVariantId,
        AnimationClip configuredClip,
        string configuredAnimatorStateName,
        int configuredAnimatorLayer,
        AvatarMask configuredAvatarMask,
        bool configuredMirrored,
        float configuredWeight,
        BistroBuilderAnimationQualityTier configuredMinimumQualityTier,
        float configuredEstimatedCpuCost)
    {
        variantId = configuredVariantId ?? string.Empty;
        clip = configuredClip;
        animatorStateName = configuredAnimatorStateName ?? string.Empty;
        animatorLayer = Mathf.Max(0, configuredAnimatorLayer);
        avatarMask = configuredAvatarMask;
        mirrored = configuredMirrored;
        weight = Mathf.Max(0.01f, configuredWeight);
        minimumQualityTier = configuredMinimumQualityTier;
        estimatedCpuCost = Mathf.Clamp01(configuredEstimatedCpuCost);
    }
#endif
    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(VariantId)) { error = "Motion recipe variant needs VariantId."; return false; }
        if (clip == null && string.IsNullOrWhiteSpace(AnimatorStateName)) { error = "Motion recipe variant " + VariantId + " needs clip or Animator state."; return false; }
        return true;
    }
}

[CreateAssetMenu(fileName = "BBMotionRecipe", menuName = "Bistro Builder/Animation V1/Motion Recipe")]
public sealed class BistroBuilderMotionRecipe : ScriptableObject
{
    [SerializeField] private string motionId = "motion.generic";
    [SerializeField] private BistroBuilderAnimationBodyMode bodyMode = BistroBuilderAnimationBodyMode.FullBody;
    [SerializeField] private bool loop;
    [SerializeField, Min(0.01f)] private float nominalSpeedMetersPerSecond = 1.25f;
    [SerializeField] private bool mirrorable;
    [SerializeField] private BistroBuilderMotionInterruptPolicy interruptPolicy = BistroBuilderMotionInterruptPolicy.AtSafeMarker;
    [SerializeField] private string fallbackMotionId = string.Empty;
    [SerializeField] private List<BistroBuilderMotionSyncPoint> syncPoints = new List<BistroBuilderMotionSyncPoint>();
    [SerializeField] private List<BistroBuilderMotionRecipeVariant> variants = new List<BistroBuilderMotionRecipeVariant>();
    [Header("Adaptation Certificate")]
    [SerializeField] private bool certified;
    [SerializeField] private string certificationId = string.Empty;
    [SerializeField] private string compilerVersion = "bb.motion.compiler.v1";
    [SerializeField] private string schemaVersion = "1.0";
    [SerializeField, Range(0.5f, 1.4f)] private float certifiedMinScale = 0.85f;
    [SerializeField, Range(0.5f, 1.4f)] private float certifiedMaxScale = 1.15f;
    [Header("Provenance")]
    [SerializeField] private string sourceProvider = string.Empty;
    [SerializeField] private string sourceReference = string.Empty;
    [SerializeField] private string licenseNote = string.Empty;
    [SerializeField] private string sourceVersion = string.Empty;

    public string MotionId => BistroBuilderMotionProfile.NormalizeId(motionId);
    public BistroBuilderAnimationBodyMode BodyMode => bodyMode;
    public bool Loop => loop;
    public float NominalSpeedMetersPerSecond => Mathf.Max(0.01f, nominalSpeedMetersPerSecond);
    public bool Mirrorable => mirrorable;
    public BistroBuilderMotionInterruptPolicy InterruptPolicy => interruptPolicy;
    public string FallbackMotionId => BistroBuilderMotionProfile.NormalizeId(fallbackMotionId);
    public IReadOnlyList<BistroBuilderMotionSyncPoint> SyncPoints => syncPoints;
    public IReadOnlyList<BistroBuilderMotionRecipeVariant> Variants => variants;
    public bool Certified => certified;
    public string CertificationId => certificationId ?? string.Empty;
    public string CompilerVersion => compilerVersion ?? string.Empty;
    public string SchemaVersion => schemaVersion ?? string.Empty;
    public float CertifiedMinScale => Mathf.Min(certifiedMinScale, certifiedMaxScale);
    public float CertifiedMaxScale => Mathf.Max(certifiedMinScale, certifiedMaxScale);
    public string SourceProvider => sourceProvider ?? string.Empty;
    public string SourceReference => sourceReference ?? string.Empty;
    public string LicenseNote => licenseNote ?? string.Empty;
    public string SourceVersion => sourceVersion ?? string.Empty;

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredMotionId,
        BistroBuilderAnimationBodyMode configuredBodyMode,
        bool configuredLoop,
        float configuredNominalSpeed,
        bool configuredMirrorable,
        BistroBuilderMotionInterruptPolicy configuredInterruptPolicy,
        string configuredFallbackMotionId,
        List<BistroBuilderMotionRecipeVariant> configuredVariants,
        List<BistroBuilderMotionSyncPoint> configuredSyncPoints,
        bool configuredCertified,
        string configuredCertificationId,
        string configuredCompilerVersion,
        string configuredSchemaVersion,
        float configuredMinScale,
        float configuredMaxScale,
        string configuredSourceProvider,
        string configuredSourceReference,
        string configuredLicenseNote,
        string configuredSourceVersion)
    {
        motionId = configuredMotionId ?? string.Empty;
        bodyMode = configuredBodyMode;
        loop = configuredLoop;
        nominalSpeedMetersPerSecond = Mathf.Max(0.01f, configuredNominalSpeed);
        mirrorable = configuredMirrorable;
        interruptPolicy = configuredInterruptPolicy;
        fallbackMotionId = configuredFallbackMotionId ?? string.Empty;
        variants = configuredVariants ?? new List<BistroBuilderMotionRecipeVariant>();
        syncPoints = configuredSyncPoints ?? new List<BistroBuilderMotionSyncPoint>();
        certified = configuredCertified;
        certificationId = configuredCertificationId ?? string.Empty;
        compilerVersion = configuredCompilerVersion ?? string.Empty;
        schemaVersion = configuredSchemaVersion ?? string.Empty;
        certifiedMinScale = configuredMinScale;
        certifiedMaxScale = configuredMaxScale;
        sourceProvider = configuredSourceProvider ?? string.Empty;
        sourceReference = configuredSourceReference ?? string.Empty;
        licenseNote = configuredLicenseNote ?? string.Empty;
        sourceVersion = configuredSourceVersion ?? string.Empty;
    }
#endif
    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(MotionId)) { error = name + " needs MotionId."; return false; }
        if (variants == null || variants.Count == 0) { error = name + " needs at least one runtime variant."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < variants.Count; i++)
        {
            BistroBuilderMotionRecipeVariant variant = variants[i];
            if (variant == null || !variant.ValidateConfiguration(out error)) return false;
            if (bodyMode == BistroBuilderAnimationBodyMode.UpperBody && variant.AvatarMask == null && string.IsNullOrWhiteSpace(variant.AnimatorStateName))
            {
                error = name + " upper-body variant " + variant.VariantId + " needs an AvatarMask or a controller-layer state.";
                return false;
            }
            if (!ids.Add(variant.VariantId)) { error = name + " contains duplicate VariantId: " + variant.VariantId; return false; }
        }
        float previous = -1f;
        for (int i = 0; i < syncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = syncPoints[i];
            if (marker == null || string.IsNullOrWhiteSpace(marker.MarkerId)) { error = name + " contains invalid sync point."; return false; }
            if (marker.NormalizedTime < previous) { error = name + " sync points are not ordered."; return false; }
            previous = marker.NormalizedTime;
        }
        if (certified && string.IsNullOrWhiteSpace(certificationId)) { error = name + " is certified but has no CertificationId."; return false; }
        return true;
    }

    public float FindMarkerTime01(string markerId, float fallback)
    {
        string normalized = BistroBuilderMotionProfile.NormalizeId(markerId);
        for (int i = 0; i < syncPoints.Count; i++)
        {
            BistroBuilderMotionSyncPoint marker = syncPoints[i];
            if (marker != null && marker.MarkerId == normalized) return marker.NormalizedTime;
        }
        return Mathf.Clamp01(fallback);
    }
}
