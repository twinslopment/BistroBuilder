using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderCharacterAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private BistroBuilderMotionCatalog motionCatalog;
    [SerializeField, Min(0f)] private float defaultBlendSeconds = 0.12f;
    [SerializeField] private string baselineMotionId = "locomotion.idle";

    private PlayableGraph transientGraph;
    private bool hasTransientGraph;

    public string CurrentMotionId { get; private set; } = string.Empty;
    public string LastSuccessfullyPlayedMotionId { get; private set; } = string.Empty;
    public Animator Animator => animator;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    private void OnDisable() => StopTransientMotion();
    private void OnDestroy() => StopTransientMotion();

    public bool TryPlaySemanticMotion(
        string requestedMotionId,
        string fallbackMotionId,
        float tempo,
        out float duration)
    {
        duration = 0.6f;
        StopTransientMotion();
        if (!TryResolveMotion(requestedMotionId, fallbackMotionId, out BistroBuilderMotionProfile profile))
            return false;

        CurrentMotionId = profile.MotionId;
        float safeTempo = Mathf.Clamp(tempo, 0.5f, 1.5f);
        duration = profile.NominalDuration / safeTempo;
        if (animator == null) return false;

        if (!string.IsNullOrWhiteSpace(profile.AnimatorStateName) && animator.runtimeAnimatorController != null)
        {
            animator.speed = safeTempo;
            animator.CrossFadeInFixedTime(
                Animator.StringToHash(profile.AnimatorStateName),
                Mathf.Max(0f, defaultBlendSeconds),
                profile.AnimatorLayer);
            LastSuccessfullyPlayedMotionId = profile.MotionId;
            return true;
        }

        if (profile.Clip == null) return false;
        transientGraph = PlayableGraph.Create("BB_" + profile.MotionId + "_" + GetInstanceID());
        transientGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(transientGraph, "BB Motion", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(transientGraph, profile.Clip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(safeTempo);
        output.SetSourcePlayable(playable);
        transientGraph.Play();
        hasTransientGraph = true;
        LastSuccessfullyPlayedMotionId = profile.MotionId;
        return true;
    }

    public void StopTransientMotion()
    {
        if (hasTransientGraph && transientGraph.IsValid()) transientGraph.Destroy();
        hasTransientGraph = false;
        CurrentMotionId = string.Empty;
        if (animator != null) animator.speed = 1f;
    }

    /// <summary>
    /// Devuelve la presentación a una pose base estable sin alterar el root lógico.
    /// Con AnimatorController deja que éste recupere su estado; sin controller mantiene
    /// un Idle semántico persistente mediante Playables.
    /// </summary>
    public bool ReturnToBaselinePose()
    {
        StopTransientMotion();
        if (animator == null) return false;
        if (animator.runtimeAnimatorController != null) return true;
        if (!TryResolveMotion(baselineMotionId, string.Empty, out BistroBuilderMotionProfile profile) ||
            profile == null || profile.Clip == null)
            return false;

        CurrentMotionId = profile.MotionId;
        transientGraph = PlayableGraph.Create("BB_Baseline_" + GetInstanceID());
        transientGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output =
            AnimationPlayableOutput.Create(transientGraph, "BB Baseline", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(transientGraph, profile.Clip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(1f);
        output.SetSourcePlayable(playable);
        transientGraph.Play();
        hasTransientGraph = true;
        return true;
    }
    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (motionCatalog != null && !motionCatalog.ValidateConfiguration(out error)) return false;
        return true;
    }

    private bool TryResolveMotion(
        string requestedMotionId,
        string fallbackMotionId,
        out BistroBuilderMotionProfile profile)
    {
        profile = null;
        if (motionCatalog == null) return false;
        if (motionCatalog.TryResolve(requestedMotionId, out profile)) return true;
        return motionCatalog.TryResolve(fallbackMotionId, out profile);
    }

    /// <summary>
    /// Permite que la autoridad de presentación inyecte el catálogo común
    /// sin sobrescribir una configuración específica del personaje.
    /// </summary>
    public void AssignCatalogIfMissing(BistroBuilderMotionCatalog catalog)
    {
        if (motionCatalog == null)
            motionCatalog = catalog;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Animator configuredAnimator, BistroBuilderMotionCatalog catalog)
    {
        animator = configuredAnimator;
        motionCatalog = catalog;
    }
#endif
}
