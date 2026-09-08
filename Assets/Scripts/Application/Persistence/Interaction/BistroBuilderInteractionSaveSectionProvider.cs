using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistencia canónica de Interaction & Reservation.
/// Grants efímeros y Spatial Leases se reconstruyen tras Load.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Interaction & Reservation Save Provider")]
public sealed class BistroBuilderInteractionSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering,
    IBistroBuilderSaveOperationGuard
{
    public const string StableSectionId =
        BistroBuilderInteractionCanonicalSnapshot.CurrentSchemaId;
    public const int StableSectionVersion =
        BistroBuilderInteractionCanonicalSnapshot.CurrentSchemaVersion;

    [SerializeField] private BistroBuilderInteractionService interactionService;
    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 920;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderInteractionCanonicalSnapshot);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;
    public int PrepareOrder => 900;
    public int ApplyOrder => 920;
    public int FinalizeOrder => 980;
    public int Priority => 920;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (interactionService == null)
        {
            error = "Falta BistroBuilderInteractionService.";
            return false;
        }
        return interactionService.ValidateConfiguration(out error);
    }

    public bool CanSave(out string rejectionMessage)
    {
        CacheDependencies();
        if (interactionService == null)
        {
            rejectionMessage = "Interaction & Reservation no está disponible.";
            return false;
        }
        return interactionService.CanCaptureCanonicalState(out rejectionMessage);
    }

    public bool CanLoad(out string rejectionMessage)
    {
        rejectionMessage = string.Empty;
        return ValidateConfiguration(out rejectionMessage);
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error) ||
            !interactionService.CanCaptureCanonicalState(out error))
        {
            context.Fail(error);
            yield break;
        }
        BistroBuilderInteractionCanonicalSnapshot snapshot =
            interactionService.CaptureCanonicalSnapshot();
        if (!interactionService.ValidateCanonicalSnapshot(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }
        context.Complete(snapshot);
    }
    public bool ValidateState(object state, out string error)
    {
        CacheDependencies();
        if (interactionService == null)
        {
            error = "Interaction & Reservation no está disponible.";
            return false;
        }
        if (!(state is BistroBuilderInteractionCanonicalSnapshot snapshot))
        {
            error = "interaction.reservation.runtime no tiene el tipo esperado.";
            return false;
        }
        return interactionService.ValidateCanonicalSnapshot(snapshot, out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;
        CacheDependencies();
        if (interactionService == null)
        {
            context.Fail("Falta BistroBuilderInteractionService durante PrepareForLoad.");
            yield break;
        }
        interactionService.ResetTransientRuntimeStateAfterLoad();
        yield break;
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error) ||
            !interactionService.TryRestoreCanonicalSnapshot(
                (BistroBuilderInteractionCanonicalSnapshot)state, out error))
        {
            context.Fail(error);
            yield break;
        }
        sectionAppliedThisLoad = true;
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || interactionService == null) return;
        interactionService.RebuildTargets();

        if (!sectionAppliedThisLoad && !ReconcileLegacySeating(context))
            return;

        if (!ReconcileKitchenProcessPermits(context))
            return;

        if (!ReconcileDishCustody(context))
            return;

        interactionService.RunOrphanAudit();
        if (!interactionService.ValidateRuntimeInvariants(out string error))
            context.Fail("Reservation Reconciliation Barrier falló: " + error);
    }

    private bool ReconcileLegacySeating(BistroBuilderSaveLoadContext context)
    {
        BistroBuilderSeatingReservationCoordinator seating =
            FindFirstObjectByType<BistroBuilderSeatingReservationCoordinator>();
        if (seating == null)
        {
            context.Fail("Falta Seating Reservation Coordinator durante reconciliación legacy.");
            return false;
        }

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Array.Sort(groups, (a, b) =>
            (a != null ? a.GroupId : int.MaxValue).CompareTo(
                b != null ? b.GroupId : int.MaxValue));
        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || group.AssignedTable == null) continue;
            if (!seating.TryEnsureAssignedTableRight(group, out string error))
            {
                context.Fail("No pudo reconciliarse seating legacy: " + error);
                return false;
            }
        }
        return true;
    }

    private bool ReconcileKitchenProcessPermits(BistroBuilderSaveLoadContext context)
    {
        BistroBuilderAdvancedKitchenService[] kitchens =
            FindObjectsByType<BistroBuilderAdvancedKitchenService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Array.Sort(kitchens, (a, b) => string.CompareOrdinal(
            a != null ? a.name : string.Empty,
            b != null ? b.name : string.Empty));

        for (int i = 0; i < kitchens.Length; i++)
        {
            BistroBuilderAdvancedKitchenService kitchen = kitchens[i];
            if (kitchen == null) continue;
            if (!kitchen.TryReconcileInteractionProcessPermitsAfterLoad(out string error))
            {
                context.Fail("No pudo reconciliarse la ocupación lógica de cocina: " + error);
                return false;
            }
        }
        return true;
    }

    private bool ReconcileDishCustody(BistroBuilderSaveLoadContext context)
    {
        BistroBuilderDishCustodyCoordinator custody =
            FindFirstObjectByType<BistroBuilderDishCustodyCoordinator>();
        if (custody == null)
        {
            context.Fail("Falta Dish Custody Coordinator durante reconciliación.");
            return false;
        }

        if (!custody.TryReconcileAfterLoad(out string error))
        {
            context.Fail("No pudo reconciliarse Custody de platos: " + error);
            return false;
        }
        return true;
    }

    private void CacheDependencies()
    {
        if (interactionService == null)
            interactionService = GetComponent<BistroBuilderInteractionService>();
        if (interactionService == null)
            interactionService = FindFirstObjectByType<BistroBuilderInteractionService>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderInteractionService service)
    {
        interactionService = service;
    }
#endif
}
