using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B10: atomically replaces one or several registered placeables.
/// Does not invent spatial, financial, history or persistence authorities.
/// </summary>
public readonly struct RestaurantPlaceableReplacementResult
{
    public bool Succeeded { get; }
    public int Count { get; }
    public string Message { get; }

    public RestaurantPlaceableReplacementResult(bool succeeded, int count, string message)
    {
        Succeeded = succeeded;
        Count = count;
        Message = message ?? string.Empty;
    }
}

public sealed partial class RestaurantPlaceableCreationService
{
    /// <summary>
    /// Quotes the *entire* purchase-minus-disposal using actual Finance
    /// acquisition history. Never passes a fabricated resale to the UI.
    /// Positive NetCostCents means the renovation spends money.
    /// </summary>
    public bool TryQuoteReplacement(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        RestaurantPlaceableItemDefinition replacement,
        out long netCostCents,
        out string error)
    {
        netCostCents = 0L;
        CacheBatchDependencies();

        if (!TryValidateReplacementInput(
                requested, replacement, out List<RestaurantPlaceableObject> sources,
                out error))
            return false;

        if (!(economyGate is BistroBuilderPlaceableFinanceBridge finance))
        {
            error = "Finanzas no dispone de una cotización real de reventa.";
            return false;
        }

        try
        {
            netCostCents = checked(
                replacement.PurchasePriceCents * sources.Count);
            foreach (RestaurantPlaceableObject source in sources)
            {
                if (!finance.TryGetDeletionPreview(
                        source, out BistroBuilderPlaceableDisposalPreview preview,
                        out error))
                    return false;
                netCostCents = checked(netCostCents - preview.NetCashCents);
            }
        }
        catch (OverflowException)
        {
            error = "El coste de la sustitución supera el rango financiero.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Replaces same-category furniture at existing anchor points and world
    /// rotations. No scale-forcing, cross-category promises or silent snaps.
    /// Any failure restores the exact previous registered world.
    /// </summary>
    public bool TryReplaceBatch(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        RestaurantPlaceableItemDefinition replacement,
        out IReadOnlyList<RestaurantPlaceableObject> created,
        out RestaurantPlaceableReplacementResult result)
    {
        created = Array.Empty<RestaurantPlaceableObject>();
        CacheBatchDependencies();

        RestaurantEditModeService editMode =
            FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);

        if (!DependenciesAreAvailable() || batchValidationService == null ||
            editMode == null || !editMode.IsEditModeActive ||
            HasActiveCreation || transactionService.HasActiveTransaction)
        {
            result = new RestaurantPlaceableReplacementResult(
                false, 0, "Modo edición, validación o servicios no disponibles.");
            return false;
        }

        // Financial commits are mandatory when Finance exists in the scene.
        if (FindFirstObjectByType<BistroBuilderFinanceService>(
                FindObjectsInactive.Include) != null && economyGate == null)
        {
            result = new RestaurantPlaceableReplacementResult(
                false, 0, "Finanzas no ha enlazado la sustitución.");
            return false;
        }

        if (!TryValidateReplacementInput(
                requested, replacement,
                out List<RestaurantPlaceableObject> sources,
                out string error))
        {
            result = new RestaurantPlaceableReplacementResult(false, 0, error);
            return false;
        }

        int count = sources.Count;
        var oldStates = new RestaurantPlacementStateSnapshot[count];
        var newStates = new RestaurantPlacementStateSnapshot[count];
        var clones = new RestaurantPlaceableObject[count];
        var oldDeactivated = new bool[count];
        var newActivated = new bool[count];
        var oldFinanceCommitted = new bool[count];
        var newFinanceCommitted = new bool[count];

        // Snapshot all old objects before changing any registry.
        for (int i = 0; i < count; i++)
        {
            if (!sources[i].TryGetComponent(out RestaurantAreaMember member))
            {
                result = new RestaurantPlaceableReplacementResult(
                    false, 0, "Un artículo no tiene RestaurantAreaMember.");
                return false;
            }
            oldStates[i] = RestaurantPlacementStateSnapshot.Capture(member);
        }

        // Retirement staging prevents the old footprint from being counted
        // as an obstruction to its replacement. It does not post money.
        for (int i = 0; i < count; i++)
        {
            if (!lifecycleService.TryDeactivateInstance(
                    sources[i], out _, out RestaurantPlaceableLifecycleResult retired))
            {
                error = "No se pudo apartar " + sources[i].DisplayName +
                    ": " + retired.Message;
                goto rollback;
            }
            oldDeactivated[i] = true;
        }

        for (int i = 0; i < count; i++)
        {
            RestaurantPlaceableObject source = sources[i];
            Vector3 originalAnchor = source.PlacementAnchor.position;
            Quaternion originalRotation = source.transform.rotation;

            if (!lifecycleService.TryCreateProvisionalInstance(
                    replacement, originalAnchor, originalRotation,
                    source.transform.parent, out clones[i],
                    out RestaurantPlaceableLifecycleResult createResult))
            {
                error = "No se pudo preparar el nuevo artículo: " +
                    createResult.Message;
                goto rollback;
            }

            if (!clones[i].TryGetComponent(out RestaurantAreaMember member))
            {
                error = "El nuevo artículo no tiene RestaurantAreaMember.";
                goto rollback;
            }
        }

        Physics.SyncTransforms();

        // Validate the *full set* together. No partial purchases or registry
        // publications are allowed before every pose passes.
        for (int i = 0; i < count; i++)
        {
            clones[i].TryGetComponent(out RestaurantAreaMember member);
            RestaurantPlacementValidationResult validation =
                batchValidationService.ValidateCurrentPlacement(member);
            if (!validation.IsValid || validation.CandidateArea == null)
            {
                error = "El nuevo tamaño o anclaje no cabe en el espacio: " +
                    validation.Status;
                goto rollback;
            }

            member.SetArea(validation.CandidateArea);
            newStates[i] = RestaurantPlacementStateSnapshot.Capture(member);
        }

        // Publish registries before Finance (the canonical lifecycle contract).
        for (int i = 0; i < count; i++)
        {
            if (!lifecycleService.TryActivateInstance(
                    clones[i], newStates[i],
                    out RestaurantPlaceableLifecycleResult activation))
            {
                error = "No se pudo registrar el reemplazo: " +
                    activation.Message;
                goto rollback;
            }
            newActivated[i] = true;
        }

        // Sale proceeds may fund the purchase; Finance still authorizes each
        // step and retains the ledger. Rollback compensates in reverse order.
        if (economyGate != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (!economyGate.TryAuthorizeDeletion(sources[i], out error) ||
                    !economyGate.TryCommitDeletion(sources[i], out error))
                    goto rollback;
                oldFinanceCommitted[i] = true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!economyGate.TryAuthorizeCreation(clones[i], out error) ||
                    !economyGate.TryCommitCreation(clones[i], out error))
                    goto rollback;
                newFinanceCommitted[i] = true;
            }
        }

        var deletedCommands = new IRestaurantEditHistoryCommand[count];
        var createdCommands = new IRestaurantEditHistoryCommand[count];
        for (int i = 0; i < count; i++)
        {
            deletedCommands[i] = new RestaurantDeletePlaceableHistoryCommand(
                lifecycleService, sources[i], oldStates[i]);
            createdCommands[i] = new RestaurantCreatePlaceableHistoryCommand(
                lifecycleService, clones[i], newStates[i]);
        }

        var command = new BistroBuilderEditorV2ReplaceHistoryCommand(
            deletedCommands, createdCommands);
        if (!command.IsValid || !historyService.TryRecordExecutedCommand(command))
        {
            error = "No se pudo registrar una única operación de sustitución.";
            goto rollback;
        }

        NotifyBatchRelationshipRebuild(clones);
        for (int i = 0; i < count; i++)
            CreationCommitted?.Invoke(clones[i]);

        created = clones;
        result = new RestaurantPlaceableReplacementResult(
            true, count, "Sustitución confirmada como una sola operación.");
        return true;

    rollback:
        bool recoverySucceeded = true;
        string recoveryErrors = string.Empty;

        // Reverse finance first, then restore identities and physical state.
        for (int i = count - 1; i >= 0; i--)
        {
            if (newFinanceCommitted[i] && economyGate != null &&
                !economyGate.TryRollbackCreation(clones[i], out string message))
            {
                recoverySucceeded = false;
                recoveryErrors += " Compra: " + message;
            }
        }
        for (int i = count - 1; i >= 0; i--)
        {
            if (oldFinanceCommitted[i] && economyGate != null &&
                !economyGate.TryRollbackDeletion(sources[i], out string message))
            {
                recoverySucceeded = false;
                recoveryErrors += " Retirada: " + message;
            }
        }

        for (int i = count - 1; i >= 0; i--)
        {
            if (newActivated[i] && clones[i] != null &&
                !lifecycleService.TryDeactivateInstance(
                    clones[i], out _, out _))
                recoverySucceeded = false;
        }
        DestroyBatchClones(clones);
        for (int i = count - 1; i >= 0; i--)
        {
            if (oldDeactivated[i] &&
                !lifecycleService.TryActivateInstance(
                    sources[i], oldStates[i], out _))
                recoverySucceeded = false;
        }

        if (!recoverySucceeded)
        {
            error += " ALERTA: rollback incompleto." + recoveryErrors;
            Debug.LogError("B10: " + error, this);
        }
        result = new RestaurantPlaceableReplacementResult(false, 0, error);
        return false;
    }

    private bool TryValidateReplacementInput(
        IReadOnlyList<RestaurantPlaceableObject> requested,
        RestaurantPlaceableItemDefinition replacement,
        out List<RestaurantPlaceableObject> sources,
        out string error)
    {
        sources = null;
        if (!DependenciesAreAvailable() || replacement == null ||
            !replacement.HasValidPrefab)
        {
            error = "La definición o el ciclo de vida no son válidos.";
            return false;
        }

        if (!TryBuildCanonicalDuplicateSources(requested, out sources, out error))
            return false;

        for (int i = 0; i < sources.Count; i++)
        {
            RestaurantPlaceableObject current = sources[i];
            if (current.ItemDefinition == replacement ||
                current.ItemDefinition.Category != replacement.Category)
            {
                error = "Solo puede sustituirse por otro artículo de la misma categoría.";
                return false;
            }

            // Do not deactivate a parent along with its child: that would
            // invalidate hierarchy snapshots and release ordering.
            Transform ancestor = current.transform.parent;
            while (ancestor != null)
            {
                for (int j = 0; j < sources.Count; j++)
                {
                    if (ancestor == sources[j].transform)
                    {
                        error = "Hay objetos anidados en la selección; sustituye el padre primero.";
                        return false;
                    }
                }
                ancestor = ancestor.parent;
            }
        }

        error = string.Empty;
        return true;
    }
}
