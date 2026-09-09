using System;
using System.Collections;
using UnityEngine;

/// <summary>Persistencia 10C del historial avanzado de clientes.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Advanced Customer History Save Provider")]
public sealed class BistroBuilderAdvancedCustomerHistorySaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "advanced_customers.state";
    public const int StableSectionVersion = 1;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderAdvancedCustomerHistoryService historyService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 420;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderAdvancedCustomerHistorySnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8960;
    public int ApplyOrder => 420;
    public int FinalizeOrder => 11360;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || historyService == null)
        {
            error = "10C persistence necesita SaveGame e historial avanzado.";
            return false;
        }
        if (!historyService.ValidateConfiguration(out error))
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

        BistroBuilderAdvancedCustomerHistorySnapshot snapshot =
            historyService.CreateSnapshot();
        if (!BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
                snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderAdvancedCustomerHistorySnapshot snapshot))
        {
            error = "advanced_customers.state no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderAdvancedCustomerHistoryEngine.TryValidateSnapshot(
            snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }
        if (!historyService.TryResetForLegacyLoad(out error))
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

        if (!historyService.TryRestoreSnapshot(
                ((BistroBuilderAdvancedCustomerHistorySnapshot)state).DeepClone(),
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
    }

    private void CacheDependencies()
    {
        if (saveGameService == null)
            TryGetComponent(out saveGameService);
        if (historyService == null)
            TryGetComponent(out historyService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
