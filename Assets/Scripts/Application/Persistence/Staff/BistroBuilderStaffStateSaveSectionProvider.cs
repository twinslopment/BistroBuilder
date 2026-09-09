using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 4E — Persistencia de la autoridad canónica de Personal.
///
/// Guarda exclusivamente staff.state: identidades EmployeeId, contratos,
/// disponibilidad, experiencia, habilidades, rendimiento y desarrollo.
/// No contiene Waiter, tareas ni referencias de escena.
///
/// Es opcional para mantener compatibilidad con partidas anteriores a 4E.
/// Cuando una partida antigua no contiene la sección, PrepareForLoad deja una
/// plantilla vacía y 4D puede ejecutar su bootstrap legacy controlado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Staff State Save Provider")]
public sealed class BistroBuilderStaffStateSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = BistroBuilderStaffSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderStaffSnapshot.CurrentSchemaVersion;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderStaffService staffService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 420;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderStaffSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    // Prepare se ordena de MAYOR a MENOR en SaveGameService. service.runtime
    // (9000) limpia primero tareas/agentes; después 4D desmonta bindings (8950),
    // contratación limpia mercado (8900) y staff.state vacía la plantilla al
    // final (8850), cuando ya no quedan referencias runtime a EmployeeId.
    public int PrepareOrder => 8850;

    // Employee debe existir antes de restaurar EmployeeId ↔ WaiterId y antes
    // de que service.runtime reconstruya el mundo operativo.
    public int ApplyOrder => 400;

    public int FinalizeOrder => 10500;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (saveGameService == null)
        {
            error = "4E staff.state necesita BistroBuilderSaveGameService.";
            return false;
        }
        if (staffService == null)
        {
            error = "4E staff.state necesita BistroBuilderStaffService.";
            return false;
        }
        return staffService.ValidateConfiguration(out error);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !staffService.EnsureReady(out error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderStaffSnapshot snapshot = staffService.CreateSnapshot();
        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                snapshot,
                staffService.RoleCatalog,
                out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderStaffSnapshot snapshot))
        {
            error = "staff.state no tiene el tipo esperado.";
            return false;
        }

        if (staffService == null)
        {
            CacheDependencies();
        }
        if (staffService == null || staffService.RoleCatalog == null)
        {
            error = "staff.state no puede validarse sin catálogo de roles.";
            return false;
        }

        return BistroBuilderStaffEngine.TryValidateSnapshot(
            snapshot,
            staffService.RoleCatalog,
            out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        // La operación universal de Load ya dispone de rollback de secciones.
        // Vaciar aquí evita que una partida antigua sin staff.state herede por
        // accidente la plantilla de la partida abierta anteriormente.
        if (!staffService.TryInitializeFresh(out error))
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

        if (!staffService.TryRestoreSnapshot(
                (BistroBuilderStaffSnapshot)state,
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
        // No hay runtime que reanudar. 4D finaliza después de service.runtime.
    }

    private void CacheDependencies()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (staffService == null) TryGetComponent(out staffService);
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
