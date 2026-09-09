using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 8E. Persiste las visitas todavía abiertas para no perder esperas ni
/// duplicar experiencias al cargar una partida durante un servicio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Reputation Runtime Save Provider")]
public sealed class BistroBuilderReputationRuntimeSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "reputation.runtime";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderCustomerExperienceTrackingService trackingService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 510;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderReputationRuntimeSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8955;
    public int ApplyOrder => 510;
    public int FinalizeOrder => 11370;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || trackingService == null)
        {
            error = "Reputation Runtime Save necesita SaveGame y Experience Tracking.";
            return false;
        }
        return trackingService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        BistroBuilderReputationRuntimeSnapshot snapshot =
            trackingService.CreateRuntimeSnapshot();
        if (!BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
                snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderReputationRuntimeSnapshot snapshot))
        {
            error = "reputation.runtime no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
            snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !trackingService.TryResetRuntimeForLegacyLoad(out error))
            context.Fail(error);
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !trackingService.TryRestoreRuntimeSnapshot(
                ((BistroBuilderReputationRuntimeSnapshot)state).DeepClone(), out error))
        {
            context.Fail(error);
            yield break;
        }
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed) return;
        if (!context.SharedData.TryGet(
                "save.loaded_section." + StableSectionId, out bool loaded) || !loaded)
            trackingService.TryResetRuntimeForLegacyLoad(out _);
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (trackingService == null) TryGetComponent(out trackingService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
