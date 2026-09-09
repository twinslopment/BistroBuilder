using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Climate Save Provider")]
public sealed class BistroBuilderClimateSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider
{
    public const string StableSectionId =
        BistroBuilderClimateSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderClimateSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderClimateService climateService;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 20;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderClimateSnapshot);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();

        if (climateService == null)
        {
            error = "Falta BistroBuilderClimateService.";
            return false;
        }

        return climateService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderClimateSnapshot snapshot =
            climateService.CreateSnapshot();

        if (!BistroBuilderClimateEngine.ValidateSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        return BistroBuilderClimateEngine.ValidateSnapshot(
            state as BistroBuilderClimateSnapshot,
            out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;

        if (!ValidateConfiguration(out string error))
            context.Fail(error);

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !climateService.TryRestoreSnapshot(
                (BistroBuilderClimateSnapshot)state,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        sectionAppliedThisLoad = true;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || sectionAppliedThisLoad)
            return;

        if (!climateService.TryInitializeForCurrentGame(out string error))
            context.Fail("No se pudo inicializar climate.runtime. " + error);
    }

    private void CacheDependencies()
    {
        if (climateService == null)
            TryGetComponent(out climateService);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
