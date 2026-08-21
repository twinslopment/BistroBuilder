using System;
using System.Collections;
using BistroBuilder.LivingArchitecture.Domain;
using BistroBuilder.LivingArchitecture.Runtime;
using UnityEngine;

/// <summary>
/// Sección versionada de Arquitectura Viva dentro del SaveGame universal.
/// Persiste únicamente topología canónica; regiones, meshes y GameObjects se reconstruyen.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Living Architecture Save Provider")]
public sealed class BistroBuilderLivingArchitectureSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "architecture.living";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private ArchitectureStateService architectureState;

    private ArchitectureBuilding rollbackBuilding;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 45;
    public bool IsRequired => false;
    public Type StateType => typeof(ArchitecturePersistenceState);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 45;
    public int ApplyOrder => 45;
    public int FinalizeOrder => 8500;

    private void Awake() => CacheDependenciesIfNeeded();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();
        if (saveGameService == null) { error = "Falta BistroBuilderSaveGameService."; return false; }
        if (architectureState == null) { error = "Falta ArchitectureStateService."; return false; }
        architectureState.EnsureInitialized();
        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out var error)) { context.Fail(error); yield break; }
        var building = architectureState.CaptureClone();
        var validation = ArchitectureValidator.Validate(new ArchitectureSnapshot { Building = building });
        if (!validation.IsValid) { context.Fail("architecture.living contiene topología inválida y no puede guardarse."); yield break; }
        context.Complete(ArchitecturePersistence.Capture(building));
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is ArchitecturePersistenceState data)) { error = "architecture.living no tiene el tipo esperado."; return false; }
        return ArchitecturePersistence.TryRestore(data, out _, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;
        if (!ValidateConfiguration(out var error)) { context.Fail(error); yield break; }
        rollbackBuilding = architectureState.CaptureClone();
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!(state is ArchitecturePersistenceState data)) { context.Fail("architecture.living no tiene el tipo esperado."); yield break; }
        if (!ArchitecturePersistence.TryRestore(data, out var restored, out var restoreError)) { context.Fail(restoreError); yield break; }
        if (!architectureState.TryReplace(restored, out var replaceError)) { context.Fail(replaceError); yield break; }
        sectionAppliedThisLoad = true;
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
        {
            if (rollbackBuilding != null) architectureState.TryReplace(rollbackBuilding, out _);
            rollbackBuilding = null;
            return;
        }

        if (!sectionAppliedThisLoad)
        {
            // Partidas anteriores a LA8 migran de forma determinista a arquitectura vacía nueva.
            architectureState.ResetToEmpty();
        }
        rollbackBuilding = null;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (architectureState == null) TryGetComponent(out architectureState);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependenciesIfNeeded();
    private void OnValidate() => CacheDependenciesIfNeeded();
#endif
}
