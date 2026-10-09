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
        }
        Physics.SyncTransforms();

        // Phase 2: all members must be spatially valid before any activation.
        for (int i = 0; i < count; i++)
        {
            if (clones[i] == null ||
                !clones[i].TryGetComponent(out RestaurantAreaMember member))
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "Un prefab carece de RestaurantAreaMember.");
                return false;
            }
            RestaurantPlacementValidationResult placement =
                batchValidationService.ValidateCurrentPlacement(member);
            if (!placement.IsValid)
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, 0, "Plantilla no válida: " + placement.Status +
                    " (" + placement.UserMessage + ")");
                return false;
            }
            if (placement.CandidateArea != null)
                member.SetArea(placement.CandidateArea);
            states[i] = RestaurantPlacementStateSnapshot.Capture(member);
        }

        // Phase 3: finance/lifecycle are all-or-nothing, one undo entry.
        int appliedCount = 0;
        for (int i = 0; i < count; i++)
        {
            RestaurantPlaceableObject clone = clones[i];
            if (!lifecycleService.TryActivateInstance(
                    clone, states[i], out RestaurantPlaceableLifecycleResult activation))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    i + 1, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, appliedCount, activation.Message + rollback);
                return false;
            }
            activated[i] = true;

            if (economyGate != null &&
                !economyGate.TryAuthorizeCreation(clone, out string authorization))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    i + 1, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, appliedCount, authorization + rollback);
                return false;
            }
            if (economyGate != null &&
                !economyGate.TryCommitCreation(clone, out string financeError))
            {
                RollbackBatchCreation(clones, activated, financeCommitted,
                    i + 1, out string rollback);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count, appliedCount, financeError + rollback);
                return false;
            }
            financeCommitted[i] = economyGate != null;
            commands[i] = new RestaurantCreatePlaceableHistoryCommand(
                lifecycleService, clone, states[i]);
            appliedCount++;
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
