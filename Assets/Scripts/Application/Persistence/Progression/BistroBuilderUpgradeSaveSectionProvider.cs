using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia opcional de las mejoras adquiridas. Las partidas anteriores a
/// Bloque 9 cargan un estado vacío sin inventar compras.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Upgrade Save Provider")]
public sealed class BistroBuilderUpgradeSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "progression.upgrades";
    public const int StableSectionVersion = BistroBuilderUpgradeSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderUpgradeService upgradeService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 500;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderUpgradeSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 8990;
    public int ApplyOrder => 500;
    public int FinalizeOrder => 11400;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || upgradeService == null)
        {
            error = "Upgrade Save necesita SaveGame y UpgradeService.";
            return false;
        }
        if (!upgradeService.ValidateConfiguration(out error)) return false;
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
        BistroBuilderUpgradeSnapshot snapshot = upgradeService.CreateSnapshot();
        if (!BistroBuilderProgressionEngine.TryValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderUpgradeSnapshot snapshot))
        {
            error = "progression.upgrades no tiene el tipo esperado.";
            return false;
        }
        return BistroBuilderProgressionEngine.TryValidateSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !upgradeService.TryResetForLegacyLoad(out error))
            context.Fail(error);
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !upgradeService.TryRestoreSnapshot(
                ((BistroBuilderUpgradeSnapshot)state).DeepClone(), out error))
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
        upgradeService.TryResetForLegacyLoad(out string error);
        if (!string.IsNullOrWhiteSpace(error)) context.Fail(error);
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (upgradeService == null) TryGetComponent(out upgradeService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}