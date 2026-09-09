using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderMotionRecipePlayerV1 : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private BistroBuilderCharacterAnimationDriver legacyDriver;
    [SerializeField, Min(0f)] private float defaultBlendSeconds = 0.12f;

    private PlayableGraph graph;
    private bool graphActive;

    public Animator Animator => animator != null ? animator : GetComponentInChildren<Animator>(true);

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (legacyDriver == null) legacyDriver = GetComponent<BistroBuilderCharacterAnimationDriver>();
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();

    public bool TryPlay(BistroBuilderMotionRecipe recipe, BistroBuilderMotionRecipeVariant variant, float tempo, out float duration)
    {
        duration = 0.6f;
        Stop();
        Animator targetAnimator = Animator;
        if (variant == null || targetAnimator == null) return false;
        float safeTempo = Mathf.Clamp(tempo, 0.5f, 1.5f);

        if (!string.IsNullOrWhiteSpace(variant.AnimatorStateName) && targetAnimator.runtimeAnimatorController != null)
        {
            targetAnimator.speed = safeTempo;
            targetAnimator.CrossFadeInFixedTime(
                Animator.StringToHash(variant.AnimatorStateName),
                Mathf.Max(0f, defaultBlendSeconds),
                variant.AnimatorLayer);
            duration = 0.6f / safeTempo;
            return true;
        }

        AnimationClip clip = variant.Clip;
        if (clip == null) return false;
        duration = Mathf.Max(0.01f, clip.length) / safeTempo;
        graph = PlayableGraph.Create("BBV1_Motion_" + GetInstanceID());
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "BB V1 Motion", targetAnimator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(safeTempo);

        bool needsLayerMixer = recipe != null &&
            (recipe.BodyMode != BistroBuilderAnimationBodyMode.FullBody || variant.AvatarMask != null) &&
            targetAnimator.runtimeAnimatorController != null;
        if (needsLayerMixer)
        {
            AnimatorControllerPlayable baseController = AnimatorControllerPlayable.Create(graph, targetAnimator.runtimeAnimatorController);
            AnimationLayerMixerPlayable mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(baseController, 0, mixer, 0);
            graph.Connect(playable, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 1f);
            if (variant.AvatarMask != null) mixer.SetLayerMaskFromAvatarMask(1, variant.AvatarMask);
            mixer.SetLayerAdditive(1, recipe.BodyMode == BistroBuilderAnimationBodyMode.Additive);
            output.SetSourcePlayable(mixer);
        }
        else
        {
            output.SetSourcePlayable(playable);
        }

        graph.Play();
        graphActive = true;
        return true;
    }

    public void Stop()
    {
        if (graphActive && graph.IsValid()) graph.Destroy();
        graphActive = false;
        Animator targetAnimator = Animator;
        if (targetAnimator != null) targetAnimator.speed = 1f;
    }

    public bool ReturnToBaselinePose()
    {
        Stop();
        if (legacyDriver == null) legacyDriver = GetComponent<BistroBuilderCharacterAnimationDriver>();
        return legacyDriver != null && legacyDriver.ReturnToBaselinePose();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Animator configuredAnimator, BistroBuilderCharacterAnimationDriver configuredLegacyDriver)
    {
        animator = configuredAnimator;
        legacyDriver = configuredLegacyDriver;
    }
#endif
}
