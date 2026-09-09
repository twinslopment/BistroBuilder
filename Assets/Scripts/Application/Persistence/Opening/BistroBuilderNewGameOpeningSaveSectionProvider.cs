using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/New Game Opening Save Provider")]
public sealed class BistroBuilderNewGameOpeningSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "new_game.opening.state";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderNewGameOpeningService openingService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 80;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderNewGameStateSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8010;
    public int ApplyOrder => 80;
    public int FinalizeOrder => 12080;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || openingService == null)
        {
            error = "Persistencia de apertura 16 necesita SaveGame y NewGameOpeningService.";
            return false;
        }
        return openingService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        BistroBuilderNewGameStateSnapshot snapshot = openingService.CreateSnapshot();
        if (!BistroBuilderNewGameEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderNewGameStateSnapshot snapshot))
        {
            error = "new_game.opening.state no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderNewGameEngine.TryValidateSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        if (!openingService.TryResetForLegacyLoad(out error)) context.Fail(error);
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error))
        {
            context.Fail(error);
            yield break;
        }
        if (!openingService.TryRestoreSnapshot(
                ((BistroBuilderNewGameStateSnapshot)state).DeepClone(), out error))
        {
            context.Fail(error);
            yield break;
        }
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context) { }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (openingService == null) TryGetComponent(out openingService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
