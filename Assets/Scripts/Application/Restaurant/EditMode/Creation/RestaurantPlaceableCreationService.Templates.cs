using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolved canonical catalog item and target anchor for one B12 slot.</summary>
public readonly struct BistroBuilderEditorV2TemplateSpawn
{
    public readonly RestaurantPlaceableItemDefinition definition;
    public readonly Vector3 anchor;
    public readonly Quaternion rotation;

    public BistroBuilderEditorV2TemplateSpawn(
        RestaurantPlaceableItemDefinition item, Vector3 worldAnchor, Quaternion worldRotation)
    {
        definition = item;
        anchor = worldAnchor;
        rotation = worldRotation;
    }
}

public sealed partial class RestaurantPlaceableCreationService
{
    /// <summary>
    /// B12: an atomic new-set creation using the B8 placement, finance,
    /// lifecycle and single-history-command authorities. No source instances.
    /// </summary>
    public bool TryCreateTemplateBatch(
        IReadOnlyList<BistroBuilderEditorV2TemplateSpawn> requested,
        out IReadOnlyList<RestaurantPlaceableObject> created,
        out RestaurantPlaceableBatchCreationResult result)
    {
        created = Array.Empty<RestaurantPlaceableObject>();
        CacheBatchDependencies();
        int count = requested?.Count ?? 0;
        if (!DependenciesAreAvailable() || batchValidationService == null)
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                count, 0, "El pipeline de creación no está disponible.");
            return false;
        }
        if (count == 0 || count > 64)
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                count, 0, "La composición requiere entre 1 y 64 artículos.");
            return false;
        }
        if (HasActiveCreation || transactionService.HasActiveTransaction)
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                count, 0, "Hay otra transacción de edición activa.");
            return false;
        }
        // Validate every definition before any GameObject or expense is created.
        for (int i = 0; i < count; i++)
        {
            if (requested[i].definition == null ||
                !requested[i].definition.HasValidPrefab ||
                !IsFiniteTemplatePoint(requested[i].anchor) ||
                !IsFiniteTemplateRotation(requested[i].rotation))
            {
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "Una referencia de catálogo o pose no es válida.");
                return false;
            }
        }

        var clones = new RestaurantPlaceableObject[count];
        var states = new RestaurantPlacementStateSnapshot[count];
        var activated = new bool[count];
        var financeCommitted = new bool[count];
        var commands = new IRestaurantEditHistoryCommand[count];

        // Phase 1: provisional, not registered, no money or history.
        for (int i = 0; i < count; i++)
        {
            var spec = requested[i];
            if (!lifecycleService.TryCreateProvisionalInstance(
                spec.definition, spec.anchor, spec.rotation, null,
                out clones[i], out RestaurantPlaceableLifecycleResult creation))
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "No se pudo instanciar la plantilla: " + creation.Message);
                return false;
            }
            // Inactive provisional siblings must not be detected by seating
            // clearance Physics queries before the table is published.
            // Lifecycle.TryActivateInstance restores activity in order.
            clones[i].gameObject.SetActive(false);
        }
        Physics.SyncTransforms();

        // Phase 2: structural checks are performed against all provisional
        // instances. Functional evaluation is deferred to ordered activation:
        // chair rules require the new table to be registered first.
        for (int i = 0; i < count; i++)
        {
            if (clones[i] != null &&
                clones[i].TryGetComponent(out RestaurantAreaMember _))
                continue;
            DestroyBatchClones(clones);
            result = RestaurantPlaceableBatchCreationResult.Failure(
                count, 0, "Un prefab carece de RestaurantAreaMember.");
            return false;
        }

        // The normal Placement registry only sees committed footprints.
        // Explicitly check the 1..64 provisional members against one another
        // before any lifecycle, money or undo entry is published.
        for (int i = 0; i < count; i++)
        {
            if (!clones[i].TryGetComponent(
                    out RestaurantPlacementFootprint footprintI) ||
                !footprintI.BlocksOtherPlacements)
                continue;
            RestaurantPlacementShape shapeI = footprintI.BuildCurrentShape();
            for (int j = i + 1; j < count; j++)
            {
                if (!clones[j].TryGetComponent(
                        out RestaurantPlacementFootprint footprintJ) ||
                    !footprintJ.BlocksOtherPlacements)
                    continue;
                RestaurantPlacementConflictType conflict =
                    RestaurantPlacementCollisionUtility.EvaluateConflict(
                        shapeI, footprintJ.BuildCurrentShape());
                if (conflict == RestaurantPlacementConflictType.None)
                    continue;
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "Conflicto interno de la plantilla (" +
                    i + ", " + j + "): " + conflict);
                return false;
            }
        }

        // Phase 3: ordered staged activation. Seating rules must be able
        // to resolve the new table before the new chairs are validated.
        // All changes are synchronous, unpublished to history and reversible.
        int[] order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int rankA = TemplateActivationRank(clones[a]);
            int rankB = TemplateActivationRank(clones[b]);
            int comparison = rankA.CompareTo(rankB);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });

        int appliedCount = 0;
        for (int step = 0; step < count; step++)
        {
            int i = order[step];
            RestaurantPlaceableObject clone = clones[i];
            clone.TryGetComponent(out RestaurantAreaMember member);
            RestaurantPlacementValidationResult placement =
                batchValidationService.ValidateCurrentPlacement(member);
            if (!placement.IsValid)
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    count, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "Plantilla no válida: " + placement.Status +
                    " (" + placement.UserMessage + ")" + rollback);
                return false;
            }
            if (placement.CandidateArea != null)
                member.SetArea(placement.CandidateArea);
            states[i] = RestaurantPlacementStateSnapshot.Capture(member);
            if (!lifecycleService.TryActivateInstance(
                    clone, states[i], out RestaurantPlaceableLifecycleResult activation))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    count, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, activation.Message + rollback);
                return false;
            }
            activated[i] = true;
            appliedCount++;
        }

        // Phase 4: only charge if ALL members are spatially valid and active.
        // Undo entries still become visible as a single compound operation.
        for (int step = 0; step < count; step++)
        {
            int i = order[step];
            RestaurantPlaceableObject clone = clones[i];
            if (economyGate != null &&
                !economyGate.TryAuthorizeCreation(clone, out string authorization))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    count, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, authorization + rollback);
                return false;
            }
            if (economyGate != null &&
                !economyGate.TryCommitCreation(clone, out string financeError))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    count, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, financeError + rollback);
                return false;
            }
            financeCommitted[i] = economyGate != null;
            commands[step] = new RestaurantCreatePlaceableHistoryCommand(
                lifecycleService, clone, states[i]);
        }

        var compound = new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
            RestaurantEditHistoryCommandType.Create,
            "Colocar plantilla (" + count + " artículos)", commands);
        if (!compound.IsValid || !historyService.TryRecordExecutedCommand(compound))
        {
            RollbackBatchCreation(clones, activated, financeCommitted,
                count, out string rollback);
            result = RestaurantPlaceableBatchCreationResult.Failure(
                count, appliedCount, "Historial rechazó la plantilla." + rollback);
            return false;
        }

        NotifyBatchRelationshipRebuild(clones);
        foreach (var item in clones)
            CreationCommitted?.Invoke(item);
        created = clones;
        result = RestaurantPlaceableBatchCreationResult.Success(
            count, "Composición colocada como una transacción atómica.");
        return true;
    }

    private static int TemplateActivationRank(RestaurantPlaceableObject obj)
    {
        if (obj.TryGetComponent<RestaurantTable>(out _) ||
            obj.TryGetComponent<RestaurantTableSeatingConfiguration>(out _))
            return 0;
        if (obj.TryGetComponent<RestaurantSeat>(out _))
            return 2;
        return 1;
    }

    private static bool IsFiniteTemplatePoint(Vector3 point)
        => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
           !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
           !float.IsNaN(point.z) && !float.IsInfinity(point.z);

    private static bool IsFiniteTemplateRotation(Quaternion rotation)
        => !float.IsNaN(rotation.x) && !float.IsInfinity(rotation.x) &&
           !float.IsNaN(rotation.y) && !float.IsInfinity(rotation.y) &&
           !float.IsNaN(rotation.z) && !float.IsInfinity(rotation.z) &&
           !float.IsNaN(rotation.w) && !float.IsInfinity(rotation.w);
}
