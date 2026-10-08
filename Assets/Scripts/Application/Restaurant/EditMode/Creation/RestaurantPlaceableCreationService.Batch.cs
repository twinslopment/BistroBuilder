using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct RestaurantPlaceableBatchCreationResult
{
    public bool Succeeded { get; }
    public int RequestedCount { get; }
    public int CreatedCount { get; }
    public string Message { get; }

    private RestaurantPlaceableBatchCreationResult(
        bool succeeded,
        int requestedCount,
        int createdCount,
        string message)
    {
        Succeeded = succeeded;
        RequestedCount = requestedCount;
        CreatedCount = createdCount;
        Message = message ?? string.Empty;
    }

    public static RestaurantPlaceableBatchCreationResult Success(
        int count,
        string message)
    {
        return new RestaurantPlaceableBatchCreationResult(
            true,
            count,
            count,
            message);
    }

    public static RestaurantPlaceableBatchCreationResult Failure(
        int requestedCount,
        int createdCount,
        string message)
    {
        return new RestaurantPlaceableBatchCreationResult(
            false,
            requestedCount,
            createdCount,
            message);
    }
}

public sealed partial class RestaurantPlaceableCreationService
{
    private RestaurantPlacementValidationService batchValidationService;
    private RestaurantPlacementLinkedGroupService batchLinkedGroupService;

    /// <summary>
    /// B8: duplica un conjunto completo conservando su geometría relativa.
    /// La operación publica un único comando histórico y revierte mundo,
    /// registros y economía si falla cualquier miembro.
    /// </summary>
    public bool TryDuplicateBatch(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        Vector3 worldOffset,
        out IReadOnlyList<RestaurantPlaceableObject> created,
        out RestaurantPlaceableBatchCreationResult result)
    {
        created = Array.Empty<RestaurantPlaceableObject>();
        CacheBatchDependencies();

        if (!DependenciesAreAvailable() ||
            batchValidationService == null)
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                requested?.Count ?? 0,
                0,
                "El sistema de duplicación grupal no está disponible.");
            return false;
        }

        if (HasActiveCreation ||
            transactionService.HasActiveTransaction)
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                requested?.Count ?? 0,
                0,
                "Hay otra colocación activa.");
            return false;
        }

        if (!TryBuildCanonicalDuplicateSources(
                requested,
                out List<RestaurantPlaceableObject> sources,
                out string error))
        {
            result = RestaurantPlaceableBatchCreationResult.Failure(
                requested?.Count ?? 0,
                0,
                error);
            return false;
        }

        int count = sources.Count;
        var clones = new RestaurantPlaceableObject[count];
        var states = new RestaurantPlacementStateSnapshot[count];
        var activated = new bool[count];
        var financeCommitted = new bool[count];
        var commands = new IRestaurantEditHistoryCommand[count];

        // Fase 1: materialización provisional. Ningún registro ni dinero cambia.
        for (int i = 0; i < count; i++)
        {
            RestaurantPlaceableObject source = sources[i];
            Vector3 anchorPosition =
                source.PlacementAnchor.position + worldOffset;

            if (!lifecycleService.TryCreateProvisionalInstance(
                    source.ItemDefinition,
                    anchorPosition,
                    source.transform.rotation,
                    source.transform.parent,
                    out clones[i],
                    out RestaurantPlaceableLifecycleResult lifecycleResult))
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    0,
                    "No se pudo duplicar " + source.DisplayName + ". " +
                    lifecycleResult.Message);
                return false;
            }
        }

        Physics.SyncTransforms();

        // Fase 2: el conjunto completo debe ser válido antes de publicar nada.
        for (int i = 0; i < count; i++)
        {
            if (clones[i] == null ||
                !clones[i].TryGetComponent(
                    out RestaurantAreaMember member))
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    0,
                    "Un duplicado no contiene RestaurantAreaMember.");
                return false;
            }

            RestaurantPlacementValidationResult validation =
                batchValidationService.ValidateCurrentPlacement(member);

            if (!validation.IsValid)
            {
                DestroyBatchClones(clones);
                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    0,
                    "La duplicación grupal no es válida: " +
                    validation.Status + ".");
                return false;
            }

            if (validation.CandidateArea != null)
                member.SetArea(validation.CandidateArea);

            states[i] =
                RestaurantPlacementStateSnapshot.Capture(member);
        }

        // Fase 3: publicación atómica. Cada paso confirmado puede revertirse.
        int appliedCount = 0;

        for (int i = 0; i < count; i++)
        {
            RestaurantPlaceableObject clone = clones[i];

            if (!lifecycleService.TryActivateInstance(
                    clone,
                    states[i],
                    out RestaurantPlaceableLifecycleResult activationResult))
            {
                RollbackBatchCreation(
                    clones,
                    activated,
                    financeCommitted,
                    appliedCount,
                    out string rollbackError);

                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    appliedCount,
                    "No se pudo activar un duplicado. " +
                    activationResult.Message + rollbackError);
                return false;
            }

            activated[i] = true;

            if (economyGate != null &&
                !economyGate.TryAuthorizeCreation(
                    clone,
                    out string authorizationError))
            {
                RollbackBatchCreation(
                    clones,
                    activated,
                    financeCommitted,
                    i + 1,
                    out string rollbackError);

                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    appliedCount,
                    authorizationError + rollbackError);
                return false;
            }

            if (economyGate != null &&
                !economyGate.TryCommitCreation(
                    clone,
                    out string economyError))
            {
                RollbackBatchCreation(
                    clones,
                    activated,
                    financeCommitted,
                    i + 1,
                    out string rollbackError);

                result = RestaurantPlaceableBatchCreationResult.Failure(
                    count,
                    appliedCount,
                    economyError + rollbackError);
                return false;
            }

            financeCommitted[i] = economyGate != null;

            commands[i] =
                new RestaurantCreatePlaceableHistoryCommand(
                    lifecycleService,
                    clone,
                    states[i]);

            appliedCount++;
        }

        var compound =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Create,
                "Duplicar " + count + " artículos",
                commands);

        if (!compound.IsValid ||
            !historyService.TryRecordExecutedCommand(compound))
        {
            RollbackBatchCreation(
                clones,
                activated,
                financeCommitted,
                count,
                out string rollbackError);

            result = RestaurantPlaceableBatchCreationResult.Failure(
                count,
                appliedCount,
                "El historial rechazó la duplicación grupal." +
                rollbackError);
            return false;
        }

        NotifyBatchRelationshipRebuild(clones);

        for (int i = 0; i < count; i++)
            CreationCommitted?.Invoke(clones[i]);

        created = clones;
        result = RestaurantPlaceableBatchCreationResult.Success(
            count,
            "Se duplicaron " + count +
            " artículos como una única operación.");

        return true;
    }

    private bool TryBuildCanonicalDuplicateSources(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        out List<RestaurantPlaceableObject> sources,
        out string error)
    {
        sources = new List<RestaurantPlaceableObject>(
            requested?.Count ?? 0);
        error = string.Empty;

        if (requested == null || requested.Count == 0)
        {
            error = "No se ha indicado ningún artículo que duplicar.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < requested.Count; i++)
        {
            RestaurantPlaceableObject source = requested[i];

            if (source == null ||
                !source.HasInstanceId ||
                source.ItemDefinition == null)
            {
                error =
                    "La selección contiene un artículo no duplicable.";
                return false;
            }

            if (!lifecycleService.IsRegistered(source))
            {
                error =
                    source.DisplayName +
                    " no es una instancia activa registrada.";
                return false;
            }

            if (ids.Add(source.InstanceId))
                sources.Add(source);
        }

        sources.Sort(
            (a, b) => string.Compare(
                a.InstanceId,
                b.InstanceId,
                StringComparison.Ordinal));

        return sources.Count > 0;
    }

    private void RollbackBatchCreation(
        IReadOnlyList<RestaurantPlaceableObject> clones,
        IReadOnlyList<bool> activated,
        IReadOnlyList<bool> financeCommitted,
        int touchedCount,
        out string rollbackError)
    {
        rollbackError = string.Empty;

        int upper =
            Mathf.Min(
                touchedCount,
                clones?.Count ?? 0);

        for (int i = upper - 1; i >= 0; i--)
        {
            RestaurantPlaceableObject clone = clones[i];
            if (clone == null)
                continue;

            if (i < financeCommitted.Count &&
                financeCommitted[i] &&
                economyGate != null &&
                !economyGate.TryRollbackCreation(
                    clone,
                    out string financeError))
            {
                rollbackError +=
                    " Reversión financiera fallida para " +
                    clone.DisplayName + ": " + financeError;
            }

            if (i < activated.Count && activated[i])
                lifecycleService.TryDeactivateInstance(
                    clone,
                    out _,
                    out _);
        }

        DestroyBatchClones(clones);

        if (string.IsNullOrEmpty(rollbackError) &&
            touchedCount > 0)
        {
            rollbackError =
                " El conjunto provisional fue revertido por completo.";
        }
    }

    private void DestroyBatchClones(
        IReadOnlyList<RestaurantPlaceableObject> clones)
    {
        if (clones == null)
            return;

        for (int i = 0; i < clones.Count; i++)
        {
            RestaurantPlaceableObject clone = clones[i];
            if (clone != null)
                lifecycleService.TryPermanentlyDestroyInstance(
                    clone,
                    out _);
        }
    }

    private void NotifyBatchRelationshipRebuild(
        IReadOnlyList<RestaurantPlaceableObject> clones)
    {
        if (batchLinkedGroupService == null ||
            clones == null ||
            clones.Count == 0)
        {
            return;
        }

        RestaurantAreaMember root = null;
        var followers =
            new List<RestaurantAreaMember>(
                Mathf.Max(0, clones.Count - 1));

        for (int i = 0; i < clones.Count; i++)
        {
            if (clones[i] == null ||
                !clones[i].TryGetComponent(
                    out RestaurantAreaMember member))
            {
                continue;
            }

            if (root == null)
                root = member;
            else
                followers.Add(member);
        }

        if (root != null)
        {
            batchLinkedGroupService.NotifyConfirmedGroupPoseApplied(
                root,
                followers);
        }
    }

    private void CacheBatchDependencies()
    {
        if (batchValidationService == null)
            TryGetComponent(out batchValidationService);

        if (batchLinkedGroupService == null)
            TryGetComponent(out batchLinkedGroupService);
    }
}
