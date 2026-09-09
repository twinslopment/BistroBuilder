using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BistroBuilderAnimationActorBinding : MonoBehaviour
{
    [SerializeField] private string actorId = string.Empty;
    [SerializeField] private long generation = 1;
    [SerializeField] private BistroBuilderCharacterAnimationDriver driver;
    [SerializeField] private BistroBuilderMotionRecipePlayerV1 recipePlayer;
    [SerializeField] private BistroBuilderCharacterRigAdapter rigAdapter;
    [SerializeField] private BistroBuilderCarryPresenter carryPresenter;

    public string ActorId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(actorId)) return actorId.Trim();
            Waiter waiter = GetComponent<Waiter>();
            if (waiter != null) return "waiter:" + waiter.WaiterId;
            CustomerGroup group = GetComponent<CustomerGroup>();
            if (group != null) return "customer-group:" + group.GroupId;
            return gameObject.name;
        }
    }

    public long Generation => Math.Max(1L, generation);
    public BistroBuilderCharacterAnimationDriver Driver => driver != null ? driver : GetComponent<BistroBuilderCharacterAnimationDriver>();
    public BistroBuilderMotionRecipePlayerV1 RecipePlayer => recipePlayer != null ? recipePlayer : GetComponent<BistroBuilderMotionRecipePlayerV1>();
    public BistroBuilderCharacterRigAdapter RigAdapter => rigAdapter != null ? rigAdapter : GetComponent<BistroBuilderCharacterRigAdapter>();
    public BistroBuilderCarryPresenter CarryPresenter => carryPresenter != null ? carryPresenter : GetComponent<BistroBuilderCarryPresenter>();

    public void ConfigureRuntime(
        string configuredActorId,
        BistroBuilderCharacterAnimationDriver configuredDriver,
        BistroBuilderMotionRecipePlayerV1 configuredRecipePlayer,
        BistroBuilderCharacterRigAdapter configuredRigAdapter,
        BistroBuilderCarryPresenter configuredCarryPresenter)
    {
        actorId = configuredActorId ?? string.Empty;
        generation = Math.Max(1L, generation);
        driver = configuredDriver != null ? configuredDriver : GetComponent<BistroBuilderCharacterAnimationDriver>();
        recipePlayer = configuredRecipePlayer != null ? configuredRecipePlayer : GetComponent<BistroBuilderMotionRecipePlayerV1>();
        rigAdapter = configuredRigAdapter != null ? configuredRigAdapter : GetComponent<BistroBuilderCharacterRigAdapter>();
        carryPresenter = configuredCarryPresenter != null ? configuredCarryPresenter : GetComponent<BistroBuilderCarryPresenter>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredActorId,
        BistroBuilderCharacterAnimationDriver configuredDriver,
        BistroBuilderMotionRecipePlayerV1 configuredRecipePlayer,
        BistroBuilderCharacterRigAdapter configuredRigAdapter,
        BistroBuilderCarryPresenter configuredCarryPresenter)
    {
        ConfigureRuntime(configuredActorId, configuredDriver, configuredRecipePlayer, configuredRigAdapter, configuredCarryPresenter);
    }
#endif

    private void Reset()
    {
        driver = GetComponent<BistroBuilderCharacterAnimationDriver>();
        recipePlayer = GetComponent<BistroBuilderMotionRecipePlayerV1>();
        rigAdapter = GetComponent<BistroBuilderCharacterRigAdapter>();
        carryPresenter = GetComponent<BistroBuilderCarryPresenter>();
    }
}
