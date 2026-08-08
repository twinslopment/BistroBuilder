using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia de la política de inventario 2.2C.
///
/// inventory.policy es independiente de inventory.canonical porque guarda
/// decisiones del jugador (mínimos) y no cantidades físicas. Si una partida
/// antigua no contiene esta sección, la política se reinicia de forma
/// determinista a mínimos cero sin arrastrar datos de la sesión anterior.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Inventory Policy Save Provider")]
public sealed class BistroBuilderInventoryPolicySaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "inventory.policy";
    public const int StableSectionVersion = 1;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderInventoryPlanningService planningService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 35;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderInventoryPolicySaveData);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 35;
    public int ApplyOrder => 35;
    public int FinalizeOrder => 9050;

    public BistroBuilderInventoryPlanningService PlanningService =>
        planningService;

    public BistroBuilderSaveGameService SaveGameService => saveGameService;

    private bool sectionAppliedThisLoad;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (planningService == null)
        {
            error = "Falta BistroBuilderInventoryPlanningService.";
            return false;
        }

        return planningService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        if (!planningService.TryCapturePolicySnapshot(
                out BistroBuilderInventoryPolicySaveData snapshot,
                out string error
            ))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        error = string.Empty;
        if (!(state is BistroBuilderInventoryPolicySaveData snapshot))
        {
            error = "inventory.policy no tiene el tipo esperado.";
            return false;
        }

        return snapshot.TryValidateBasic(out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;

        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateState(state, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        if (!planningService.TryReplacePolicySnapshot(
                (BistroBuilderInventoryPolicySaveData)state,
                true,
                out string applyError
            ))
        {
            context.Fail(applyError);
            yield break;
        }

        sectionAppliedThisLoad = true;
        context.SharedData.Set(
            "save.loaded_section." + StableSectionId,
            true
        );
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || sectionAppliedThisLoad)
        {
            return;
        }

        // Una partida anterior a 2.2C no tenía mínimos configurables. Se
        // resetean para impedir que una carga herede la política de la
        // partida que estaba abierta previamente.
        if (!planningService.TryResetPolicy(out string error))
        {
            context.Fail(
                "No pudo inicializarse inventory.policy para una partida " +
                "anterior a 2.2C. " + error
            );
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (planningService == null)
        {
            TryGetComponent(out planningService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
