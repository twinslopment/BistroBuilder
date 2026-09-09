using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia independiente de reputación e historial de visitas.
/// La sección es opcional para mantener compatibilidad con partidas antiguas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Guest Relations Save Provider")]
public sealed class BistroBuilderGuestRelationsSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "guest_relations.state";
    public const int StableSectionVersion = 1;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderGuestRelationsService guestRelationsService;

    [SerializeField]
    private BistroBuilderMarketingGuestRelationsBridge marketingBridge;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 410;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderGuestRelationsSnapshot);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 8950;
    public int ApplyOrder => 410;
    public int FinalizeOrder => 11350;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || guestRelationsService == null ||
            marketingBridge == null)
        {
            error = "GuestRelations necesita SaveGame, autoridad y puente Marketing.";
            return false;
        }

        if (!guestRelationsService.ValidateConfiguration(out error) ||
            !marketingBridge.ValidateConfiguration(out error))
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
        BistroBuilderGuestRelationsSnapshot snapshot =
            guestRelationsService.CreateSnapshot();
        if (!BistroBuilderGuestRelationsEngine.TryValidateSnapshot(
                snapshot,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderGuestRelationsSnapshot snapshot))
        {
            error = "guest_relations.state no tiene el tipo esperado.";
            return false;
        }

        return BistroBuilderGuestRelationsEngine.TryValidateSnapshot(
            snapshot,
            out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        if (!guestRelationsService.TryResetForLegacyLoad(out error))
            context.Fail(error);
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

        if (!guestRelationsService.TryRestoreSnapshot(
                ((BistroBuilderGuestRelationsSnapshot)state).DeepClone(),
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

        if (!marketingBridge.TrySynchronizeReputationCredits(out string error))
            context.Fail(error);
    }

    private void CacheDependencies()
    {
        if (saveGameService == null)
            TryGetComponent(out saveGameService);
        if (guestRelationsService == null)
            TryGetComponent(out guestRelationsService);
        if (marketingBridge == null)
            TryGetComponent(out marketingBridge);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
