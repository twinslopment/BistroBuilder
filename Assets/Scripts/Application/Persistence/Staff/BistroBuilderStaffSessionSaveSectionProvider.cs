using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 4E — Persistencia del binding runtime EmployeeId ↔ WaiterId.
///
/// Se coordina explícitamente con service.runtime mediante órdenes de fase:
/// - Prepare 9100: después de que service.runtime (9000) limpie tareas/agentes.
/// - Apply 550: después de staff.state y de service.runtime (500).
/// - Finalize 11100: después de que service.runtime (11000) reanude el mundo.
///
/// No serializa GameObjects ni transforma esta sección en autoridad de tareas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Staff Session Save Provider")]
public sealed class BistroBuilderStaffSessionSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId =
        BistroBuilderStaffSessionSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderStaffService staffService;

    [SerializeField]
    private BistroBuilderStaffSessionService staffSessionService;

    [SerializeField]
    private RestaurantServiceStateService serviceStateService;

    private BistroBuilderStaffSessionSnapshot pendingData;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 550;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderStaffSessionSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 9100;
    public int ApplyOrder => 550;
    public int FinalizeOrder => 11100;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null || staffService == null ||
            staffSessionService == null || serviceStateService == null)
        {
            error =
                "4E staff.session.runtime necesita SaveGame, Staff, " +
                "StaffSessionService y RestaurantServiceStateService.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !staffSessionService.ValidateConfiguration(out error))
        {
            return false;
        }

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

        BistroBuilderStaffSessionSnapshot snapshot =
            staffSessionService.CreateSessionSnapshot();
        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                snapshot,
                staffService.CreateSnapshot(),
                out error))
        {
            context.Fail(error);
            yield break;
        }

        bool serviceActive = serviceStateService.IsServiceInProgress;
        if (serviceActive != snapshot.active)
        {
            context.Fail(
                "staff.session.runtime y el estado real de servicio no " +
                "coinciden durante la captura.");
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderStaffSessionSnapshot snapshot))
        {
            error = "staff.session.runtime no tiene el tipo esperado.";
            return false;
        }

        if (staffService == null)
        {
            CacheDependencies();
        }
        if (staffService == null)
        {
            error = "staff.session.runtime no puede validarse sin staff.state.";
            return false;
        }

        return BistroBuilderStaffSessionEngine.TryValidateSnapshot(
            snapshot,
            staffService.CreateSnapshot(),
            out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        pendingData = null;
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        if (!staffSessionService.PrepareForRuntimeLoad(out error))
        {
            context.Fail(error);
        }
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

        pendingData = ((BistroBuilderStaffSessionSnapshot)state).DeepClone();
        if (!staffSessionService.TryRestoreSessionSnapshot(
                pendingData,
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
        {
            pendingData = null;
            return;
        }

        bool sectionWasLoaded = context.SharedData.TryGet(
            "save.loaded_section." + StableSectionId,
            out bool loaded) && loaded;

        if (!sectionWasLoaded)
        {
            // Compatibilidad con partidas anteriores a 4E. Se descarta
            // cualquier sesión anterior. Si el save antiguo declara servicio
            // activo, 4D reconstruye un binding nuevo contra los Waiter ya
            // restaurados por service.runtime.
            BistroBuilderStaffSessionSnapshot inactive =
                BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            if (!staffSessionService.TryRestoreSessionSnapshot(
                    inactive,
                    out string legacyError))
            {
                context.Fail(legacyError);
                pendingData = null;
                return;
            }

            if (serviceStateService.IsServiceInProgress &&
                !staffSessionService.TryEnsureSessionStarted(out legacyError))
            {
                context.Fail(legacyError);
                pendingData = null;
                return;
            }
        }

        if (!staffSessionService.TryResumeAfterRuntimeLoad(out string error))
        {
            context.Fail(error);
        }

        pendingData = null;
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (staffService == null) TryGetComponent(out staffService);
        if (staffSessionService == null)
            TryGetComponent(out staffSessionService);
        if (serviceStateService == null)
            TryGetComponent(out serviceStateService);
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
