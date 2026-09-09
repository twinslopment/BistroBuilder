using System;
using System.Collections;
using UnityEngine;

/// <summary>Persistencia del historial de cierres del Bloque 15.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/End Of Day Save Provider 15")]
public sealed class BistroBuilderEndOfDaySaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "end_of_day.state";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderEndOfDayService endOfDayService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 430;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderEndOfDayHistorySnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8970;
    public int ApplyOrder => 430;
    public int FinalizeOrder => 11370;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || endOfDayService == null)
        {
            error = "Persistencia 15 necesita SaveGame y EndOfDayService.";
            return false;
        }
        return endOfDayService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        BistroBuilderEndOfDayHistorySnapshot snapshot = endOfDayService.CreateSnapshot();
        if (!BistroBuilderEndOfDayEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderEndOfDayHistorySnapshot snapshot))
        {
            error = "end_of_day.state no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderEndOfDayEngine.TryValidateSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        if (!endOfDayService.TryResetForLegacyLoad(out error)) context.Fail(error);
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !endOfDayService.TryRestoreSnapshot(
                ((BistroBuilderEndOfDayHistorySnapshot)state).DeepClone(), out error))
        {
            context.Fail(error);
            yield break;
        }
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context == null || context.HasFailed || endOfDayService == null) return;
        endOfDayService.ReconcileAfterRuntimeLoad();
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (endOfDayService == null) TryGetComponent(out endOfDayService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
