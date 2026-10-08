using System;
using System.Collections.Generic;

/// <summary>
/// Resultado de una retirada grupal B8.
/// </summary>
public readonly struct RestaurantPlaceableBatchDeletionResult
{
    public bool Succeeded { get; }
    public int RequestedCount { get; }
    public int AppliedCount { get; }
    public string Message { get; }

    private RestaurantPlaceableBatchDeletionResult(
        bool succeeded,
        int requestedCount,
        int appliedCount,
        string message)
    {
        Succeeded = succeeded;
        RequestedCount = requestedCount;
        AppliedCount = appliedCount;
        Message = message ?? string.Empty;
    }

    public static RestaurantPlaceableBatchDeletionResult Success(
        int count,
        string message)
    {
        return new RestaurantPlaceableBatchDeletionResult(
            true,
            count,
            count,
            message);
    }

    public static RestaurantPlaceableBatchDeletionResult Failure(
        int requestedCount,
        int appliedCount,
        string message)
    {
        return new RestaurantPlaceableBatchDeletionResult(
            false,
            requestedCount,
            appliedCount,
            message);
    }
}

public sealed partial class RestaurantPlaceableDeletionService
{
    /// <summary>
    /// B8: elimina un conjunto de colocables como una sola transacción
    /// observable e histórica.
    ///
    /// Cada miembro utiliza el mismo ciclo de vida y la misma frontera
    /// económica que la eliminación individual. Si cualquier paso falla,
    /// revierte en orden inverso todos los miembros ya aplicados.
    /// </summary>
    public bool TryDeleteBatch(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        out RestaurantPlaceableBatchDeletionResult result)
    {
        if (!DependenciesAreAvailable())
        {
            result = RestaurantPlaceableBatchDeletionResult.Failure(
                requested?.Count ?? 0,
                0,
                "El sistema de eliminación no está disponible.");
            return false;
        }

        if (!editModeService.IsEditModeActive)
        {
            result = RestaurantPlaceableBatchDeletionResult.Failure(
                requested?.Count ?? 0,
                0,
                "La eliminación grupal solo está disponible en modo edición.");
            return false;
        }

        if (transactionService.HasActiveTransaction)
        {
            result = RestaurantPlaceableBatchDeletionResult.Failure(
                requested?.Count ?? 0,
                0,
                "Hay una colocación activa. Confírmala o cancélala primero.");
            return false;
        }

        if (!TryBuildCanonicalBatch(
                requested,
                out List<RestaurantPlaceableObject> targets,
                out string error))
        {
            result = RestaurantPlaceableBatchDeletionResult.Failure(
                requested?.Count ?? 0,
                0,
                error);
            return false;
        }

        int count = targets.Count;
        var states = new RestaurantPlacementStateSnapshot[count];
        var commands = new IRestaurantEditHistoryCommand[count];
        int appliedCount = 0;

        for (int i = 0; i < count; i++)
        {
            RestaurantPlaceableObject target = targets[i];

            if (!lifecycleService.TryDeactivateInstance(
                    target,
                    out states[i],
                    out RestaurantPlaceableLifecycleResult lifecycleResult))
            {
                RollbackBatchDeletion(
                    targets,
                    states,
                    appliedCount,
                    true,
                    out string rollbackError);

                result = RestaurantPlaceableBatchDeletionResult.Failure(
                    count,
                    appliedCount,
                    "No se pudo retirar " + target.DisplayName + ". " +
                    lifecycleResult.Message + rollbackError);
                return false;
            }

            if (economyGate != null &&
                !economyGate.TryAuthorizeDeletion(
                    target,
                    out string authorizationError))
            {
                lifecycleService.TryActivateInstance(
                    target,
                    states[i],
                    out _);

                RollbackBatchDeletion(
                    targets,
                    states,
                    appliedCount,
                    true,
                    out string rollbackError);

                result = RestaurantPlaceableBatchDeletionResult.Failure(
                    count,
                    appliedCount,
                    authorizationError + rollbackError);
                return false;
            }

            if (economyGate != null &&
                !economyGate.TryCommitDeletion(
                    target,
                    out string economyError))
            {
                lifecycleService.TryActivateInstance(
                    target,
                    states[i],
                    out _);

                RollbackBatchDeletion(
                    targets,
                    states,
                    appliedCount,
                    true,
                    out string rollbackError);

                result = RestaurantPlaceableBatchDeletionResult.Failure(
                    count,
                    appliedCount,
                    economyError + rollbackError);
                return false;
            }

            commands[i] =
                new RestaurantDeletePlaceableHistoryCommand(
                    lifecycleService,
                    target,
                    states[i]);

            appliedCount++;
        }

        var compound =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Delete,
                "Eliminar " + count + " artículos",
                commands);

        if (!compound.IsValid ||
            !historyService.TryRecordExecutedCommand(compound))
        {
            RollbackBatchDeletion(
                targets,
                states,
                appliedCount,
                true,
                out string rollbackError);

            result = RestaurantPlaceableBatchDeletionResult.Failure(
                count,
                appliedCount,
                "El historial rechazó la eliminación grupal." +
                rollbackError);
            return false;
        }

        for (int i = 0; i < count; i++)
            PlaceableDeleted?.Invoke(targets[i]);

        result = RestaurantPlaceableBatchDeletionResult.Success(
            count,
            "Se retiraron " + count +
            " artículos como una única operación.");

        return true;
    }

    private bool TryBuildCanonicalBatch(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        out List<RestaurantPlaceableObject> targets,
        out string error)
    {
        targets = new List<RestaurantPlaceableObject>(
            requested?.Count ?? 0);
        error = string.Empty;

        if (requested == null || requested.Count == 0)
        {
            error = "No se ha indicado ningún artículo.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < requested.Count; i++)
        {
            RestaurantPlaceableObject placeable = requested[i];

            if (placeable == null ||
                string.IsNullOrWhiteSpace(placeable.InstanceId))
            {
                error =
                    "La selección contiene un artículo sin identidad estable.";
                return false;
            }

            if (!lifecycleService.IsRegistered(placeable))
            {
                error =
                    placeable.DisplayName +
                    " no está registrado como instancia activa.";
                return false;
            }

            if (ids.Add(placeable.InstanceId))
                targets.Add(placeable);
        }

        targets.Sort(
            (a, b) => string.Compare(
                a.InstanceId,
                b.InstanceId,
                StringComparison.Ordinal));

        if (targets.Count == 0)
        {
            error = "No hay artículos únicos que eliminar.";
            return false;
        }

        return true;
    }

    private void RollbackBatchDeletion(
        IReadOnlyList<RestaurantPlaceableObject> targets,
        IReadOnlyList<RestaurantPlacementStateSnapshot> states,
        int appliedCount,
        bool rollbackEconomy,
        out string rollbackError)
    {
        rollbackError = string.Empty;
        bool worldOk = true;
        bool financeOk = true;

        for (int i = appliedCount - 1; i >= 0; i--)
        {
            RestaurantPlaceableObject target = targets[i];

            if (rollbackEconomy &&
                economyGate != null &&
                !economyGate.TryRollbackDeletion(
                    target,
                    out string financeError))
            {
                financeOk = false;
                rollbackError +=
                    " Reversión financiera fallida para " +
                    target.DisplayName + ": " + financeError;
            }

            if (!lifecycleService.TryActivateInstance(
                    target,
                    states[i],
                    out RestaurantPlaceableLifecycleResult lifecycleResult))
            {
                worldOk = false;
                rollbackError +=
                    " Restauración fallida para " +
                    target.DisplayName + ": " +
                    lifecycleResult.Message;
            }
        }

        if (worldOk && financeOk && appliedCount > 0)
            rollbackError += " El conjunto anterior fue restaurado.";
    }
}
