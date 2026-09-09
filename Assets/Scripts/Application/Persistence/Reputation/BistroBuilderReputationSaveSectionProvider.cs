using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia de la autoridad canónica de Reputación.
/// Es opcional para cargar partidas anteriores al Bloque 8.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Reputation Save Provider")]
public sealed class BistroBuilderReputationSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "reputation.state";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderGuestRelationsService guestRelationsService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 420;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderReputationSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8960;
    public int ApplyOrder => 420;
    public int FinalizeOrder => 11360;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || reputationService == null || guestRelationsService == null)
        {
            error = "Reputation Save necesita SaveGame, Reputación y GuestRelations.";
            return false;
        }
        if (!reputationService.ValidateConfiguration(out error) ||
            !guestRelationsService.ValidateConfiguration(out error))
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
        BistroBuilderReputationSnapshot snapshot = reputationService.CreateSnapshot();
        if (!BistroBuilderReputationEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderReputationSnapshot snapshot))
        {
            error = "reputation.state no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderReputationEngine.TryValidateSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !reputationService.TryResetForLegacyLoad(out error))
            context.Fail(error);
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !reputationService.TryRestoreSnapshot(
                ((BistroBuilderReputationSnapshot)state).DeepClone(), out error))
        {
            context.Fail(error);
            yield break;
        }
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed) return;
        if (context.SharedData.TryGet(
                "save.loaded_section." + StableSectionId,
                out bool loaded) && loaded)
            return;

        int legacyPoints = guestRelationsService.LegacyStoredReputationPoints;
        if (legacyPoints <= 0) return;
        if (!reputationService.TryApplyExternalReputationCredit(
                "migration.guest_relations.v1",
                legacyPoints,
                out _,
                out string error))
            context.Fail(error);
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (guestRelationsService == null) TryGetComponent(out guestRelationsService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
