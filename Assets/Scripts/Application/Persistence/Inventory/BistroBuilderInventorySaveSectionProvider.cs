using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sección autoritativa inventory.canonical 368EF.
///
/// Persiste balances, reservas activas, operaciones idempotentes y libro.
/// La disponibilidad de la carta no se guarda: se recalcula al finalizar la
/// carga para impedir contradicciones con el inventario restaurado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Canonical Inventory Save Provider")]
public sealed class BistroBuilderInventorySaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "inventory.canonical";
    public const int StableSectionVersion = 1;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderInventoryService inventoryService;

    [SerializeField]
    private BistroBuilderDishAvailabilityService availabilityService;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 30;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderInventoryRuntimeSnapshot);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 30;
    public int ApplyOrder => 30;
    public int FinalizeOrder => 9000;

    private bool sectionAppliedThisLoad;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (inventoryService == null)
        {
            error = "Falta BistroBuilderInventoryService.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (availabilityService == null)
        {
            error = "Falta BistroBuilderDishAvailabilityService.";
            return false;
        }

        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        if (!inventoryService.ValidateRuntimeState(out string runtimeError))
        {
            context.Fail(runtimeError);
            yield break;
        }

        if (!inventoryService.TryCaptureRuntimeSnapshot(
                out BistroBuilderInventoryRuntimeSnapshot snapshot,
                out string error
            ))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(snapshot);
    }

    public bool ValidateState(object state, out string error)
    {
        error = string.Empty;

        if (!(state is BistroBuilderInventoryRuntimeSnapshot snapshot))
        {
            error = "inventory.canonical no tiene el tipo esperado.";
            return false;
        }

        return snapshot.TryValidateBasic(out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;

        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateState(state, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        if (!inventoryService.TryReplaceFromRuntimeSnapshot(
                (BistroBuilderInventoryRuntimeSnapshot)state,
                true,
                out string applyError
            ))
        {
            context.Fail(applyError);
            yield break;
        }

        sectionAppliedThisLoad = true;
        context.SharedData.Set(
            "save.loaded_section." + StableSectionId,
            true
        );
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || availabilityService == null)
        {
            return;
        }

        if (!sectionAppliedThisLoad)
        {
            // Migración determinista de partidas anteriores a 368EF.
            // Nunca conservamos el inventario de la sesión que estaba
            // abierta antes de cargar: una partida antigua cerrada parte
            // del perfil de apertura vigente. Una partida activa, en
            // cambio, no es segura sin su snapshot autoritativo.
            if (context.SharedData.TryGet(
                    BistroBuilderGeneralGameSaveSectionProvider
                        .SharedStateKey,
                    out BistroBuilderGeneralGameSaveData generalState
                ) &&
                generalState != null &&
                (BistroBuilderSaveSnapshotMode)generalState.snapshotMode ==
                    BistroBuilderSaveSnapshotMode.ActiveService)
            {
                context.Fail(
                    "La partida contiene un servicio activo pero no " +
                    "incluye inventory.canonical."
                );
                return;
            }

            if (!inventoryService.TryInitialize(out string migrationError))
            {
                context.Fail(
                    "No se pudo migrar el inventario de la partida " +
                    "anterior a 368EF. " + migrationError
                );
                return;
            }
        }

        if (!availabilityService.RecalculateAll(out string error))
        {
            context.Fail(
                "El inventario se restauró, pero la carta no pudo " +
                "reconciliarse. " + error
            );
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (inventoryService == null)
        {
            TryGetComponent(out inventoryService);
        }

        if (availabilityService == null)
        {
            TryGetComponent(out availabilityService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
