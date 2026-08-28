using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 7C — Sección marketing.state del SaveGame universal.
/// Persiste campañas y contador de leads sin asumir autoridad de disco.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Marketing Save Provider")]
public sealed class BistroBuilderMarketingSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "marketing.state";
    public const int StableSectionVersion =
        BistroBuilderMarketingSaveData.CurrentVersion;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderMarketingService marketingService;
    [SerializeField] private BistroBuilderMarketingDemandIntegrationService
        demandIntegration;

    private BistroBuilderMarketingSaveData pendingData;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 400;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderMarketingSaveData);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    // service.runtime activa el scope de reconstrucción antes de Marketing.
    public int PrepareOrder => 8900;
    public int ApplyOrder => 400;
    // Se sincroniza después de service.runtime (11000) y Reservas (11200).
    public int FinalizeOrder => 11300;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || marketingService == null ||
            demandIntegration == null)
        {
            error = "7C necesita SaveGame, Marketing e integración 7B.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !demandIntegration.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        if (!demandIntegration.TryCapturePersistenceState(
                out int leadDay,
                out int leadCount,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        var data = new BistroBuilderMarketingSaveData
        {
            version = StableSectionVersion,
            state = marketingService.CreateSnapshot(),
            reservationLeadDay = leadDay,
            reservationLeadsGeneratedForDay = leadCount
        };

        if (!data.TryValidate(out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderMarketingSaveData data))
        {
            error = "marketing.state no tiene el tipo esperado.";
            return false;
        }

        return data.TryValidate(out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        pendingData = null;
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        if (!marketingService.TryResetForLegacyLoad(out error) ||
            !demandIntegration.TryRestorePersistenceState(0, 0, out error))
        {
            context.Fail(error);
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error))
        {
            context.Fail(error);
            yield break;
        }

        pendingData = ((BistroBuilderMarketingSaveData)state).DeepClone();
        if (!marketingService.TryRestoreSnapshot(pendingData.state, out error) ||
            !demandIntegration.TryRestorePersistenceState(
                pendingData.reservationLeadDay,
                pendingData.reservationLeadsGeneratedForDay,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        context.SharedData.Set(
            "save.loaded_section." + StableSectionId,
            true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
            return;

        if (!demandIntegration.TrySynchronizeAfterLoad(out string error))
        {
            context.Fail(error);
            return;
        }

        pendingData = null;
    }

    private void CacheDependencies()
    {
        if (saveGameService == null)
            TryGetComponent(out saveGameService);
        if (marketingService == null)
            TryGetComponent(out marketingService);
        if (demandIntegration == null)
            TryGetComponent(out demandIntegration);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
