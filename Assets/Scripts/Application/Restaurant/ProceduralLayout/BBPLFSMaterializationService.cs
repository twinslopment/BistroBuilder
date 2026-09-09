using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Materialization Service")]
public sealed class BBPLFSMaterializationService : MonoBehaviour
{
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private RestaurantPlaceableCatalogService placeableCatalogService;
    [SerializeField] private RestaurantPlaceableCreationService creationService;
    [SerializeField] private RestaurantPlacementTransactionService transactionService;
    [SerializeField] private RestaurantPlaceableDeletionService deletionService;

    private readonly List<RestaurantPlaceableObject> createdInOperation = new();

    public bool TryMaterialize(
        BBPLFSLayoutCandidate candidate,
        RestaurantArea targetArea,
        out string message)
    {
        message = string.Empty;
        createdInOperation.Clear();

        if (candidate == null || targetArea == null)
        {
            message = "Candidate or target area is unavailable.";
            return false;
        }

        if (editModeService == null || !editModeService.IsEditModeActive)
        {
            message = "BBPLFS can materialize layouts only while Edit Mode is active.";
            return false;
        }

        if (placeableCatalogService == null || creationService == null || transactionService == null)
        {
            message = "BBPLFS materialization dependencies are unavailable.";
            return false;
        }

        IReadOnlyList<BBPLFSLayoutPlacement> placements = candidate.Placements;
        for (int index = 0; index < placements.Count; index++)
        {
            BBPLFSLayoutPlacement placement = placements[index];
            if (!placeableCatalogService.TryGetItem(placement.ItemId, out RestaurantPlaceableItemDefinition definition))
            {
                message = "Catalog item not found: " + placement.ItemId;
                RollbackCreated();
                return false;
            }

            Vector3 surfaceAnchorPosition = BBPLFSPlacementPoseUtility.SurfaceAnchorForRootPose(
                definition.Prefab, placement.WorldPosition, placement.WorldRotation);

            bool began = creationService.TryBeginCreation(
                definition,
                surfaceAnchorPosition,
                placement.WorldRotation,
                targetArea.transform,
                out RestaurantPlaceableObject placeable,
                out RestaurantPlaceableCreationResult beginResult);

            if (!began)
            {
                message = beginResult.Message;
                RollbackCreated();
                return false;
            }

            bool previewed = transactionService.TryPreviewPlacement(
                placement.WorldPosition,
                placement.WorldRotation,
                out _,
                out _);

            if (!previewed)
            {
                creationService.TryCancelActiveCreation(out _);
                message = "The existing Edit Mode placement transaction rejected a generated pose.";
                RollbackCreated();
                return false;
            }

            if (!creationService.TryCommitActiveCreation(out RestaurantPlaceableCreationResult commitResult))
            {
                creationService.TryCancelActiveCreation(out _);
                message = commitResult.Message;
                RollbackCreated();
                return false;
            }

            if (placeable != null)
            {
                createdInOperation.Add(placeable);
            }
        }
        message = "Layout materialized through the classic Edit Mode pipeline.";
        return true;
    }

    private void RollbackCreated()
    {
        if (deletionService == null)
        {
            return;
        }

        for (int index = createdInOperation.Count - 1; index >= 0; index--)
        {
            RestaurantPlaceableObject placeable = createdInOperation[index];
            if (placeable != null)
            {
                deletionService.TryDelete(placeable, out _);
            }
        }

        createdInOperation.Clear();
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RestaurantEditModeService editMode,
        RestaurantPlaceableCatalogService catalog,
        RestaurantPlaceableCreationService creation,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableDeletionService deletion)
    {
        editModeService = editMode;
        placeableCatalogService = catalog;
        creationService = creation;
        transactionService = transaction;
        deletionService = deletion;
    }
#endif
}