using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 5D — Persistencia de staff.schedule dentro del SaveGame universal.
/// No guarda Staff, Waiter, tareas ni dinero.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Staff Schedule Save Provider")]
public sealed class BistroBuilderStaffScheduleSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = BistroBuilderStaffScheduleSnapshot.CurrentSchemaId;
    public const int StableSectionVersion = BistroBuilderStaffScheduleSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderSaveGameService saveGameService;
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 445;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderStaffScheduleSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    // Prepare es descendente. Se limpia después de recruitment (8900) y antes
    // de staff.state (8850), sin tocar bindings 4D ya desmontados en 8950.
    public int PrepareOrder => 8875;
    // Staff debe existir primero (400). El horario se aplica antes del mundo
    // operativo (500) y del binding final 4D (550).
    public int ApplyOrder => 450;
    // No reanuda runtime; queda listo antes de 4D/service.runtime.
    public int FinalizeOrder => 10700;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || staffService == null || scheduleService == null)
        {
            error = "5D staff.schedule necesita SaveGame, Staff y ScheduleService.";
            return false;
        }
        if (!staffService.ValidateConfiguration(out error) ||
            !scheduleService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !scheduleService.EnsureReady(out error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderStaffScheduleSnapshot snapshot = scheduleService.CreateSnapshot();
        if (!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                snapshot, staffService.CreateSnapshot(), out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderStaffScheduleSnapshot snapshot))
        {
            error = "staff.schedule no tiene el tipo esperado.";
            return false;
        }
        CacheDependencies();
        if (staffService == null)
        {
            error = "staff.schedule no puede validarse sin StaffService.";
            return false;
        }
        return BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
            snapshot, staffService.CreateSnapshot(), out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !scheduleService.TryResetForLegacyLoad(out error))
        {
            context.Fail(error);
        }
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !scheduleService.TryRestoreSnapshot(
                (BistroBuilderStaffScheduleSnapshot)state, out error))
        {
            context.Fail(error);
            yield break;
        }

        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        // Sin runtime propio que reanudar.
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (staffService == null) TryGetComponent(out staffService);
        if (scheduleService == null) TryGetComponent(out scheduleService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
