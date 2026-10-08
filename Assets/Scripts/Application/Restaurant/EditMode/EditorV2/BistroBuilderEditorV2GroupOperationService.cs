using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B8: fachada de operaciones colectivas para Editor V2.
///
/// No implementa reglas espaciales, lifecycle, economía ni historial.
/// Resuelve el Selection Set y delega en las autoridades ya existentes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Edit Mode/Editor V2/Group Operation Service")]
public sealed class BistroBuilderEditorV2GroupOperationService :
    MonoBehaviour
{
    [SerializeField]
    private BistroBuilderEditorV2SelectionCoordinator selectionCoordinator;

    [SerializeField]
    private RestaurantPlaceableRegistry registry;

    [SerializeField]
    private RestaurantEditInteractionController interactionController;

    [SerializeField]
    private RestaurantPlaceableCreationService creationService;

    [SerializeField]
    private RestaurantPlaceableDeletionService deletionService;

    private readonly List<BistroBuilderEditorV2Selection> scratchSelections =
        new List<BistroBuilderEditorV2Selection>(32);

    private readonly List<RestaurantPlaceableObject> scratchPlaceables =
        new List<RestaurantPlaceableObject>(32);

    public int SelectedPlaceableCount =>
        selectionCoordinator != null
            ? selectionCoordinator.SelectionCount
            : 0;

    public void Configure(
        BistroBuilderEditorV2SelectionCoordinator selection,
        RestaurantPlaceableRegistry placeableRegistry,
        RestaurantEditInteractionController interaction,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion)
    {
        selectionCoordinator = selection;
        registry = placeableRegistry;
        interactionController = interaction;
        creationService = creation;
        deletionService = deletion;
    }

    /// <summary>
    /// Abre la transacción de movimiento existente. Linked Groups recogerá
    /// automáticamente todos los miembros explícitos y semánticos.
    /// </summary>
    public bool TryBeginMoveSelection(out string error)
    {
        CacheDependencies();

        if (selectionCoordinator == null ||
            !selectionCoordinator.SelectionSetSupports(
                BistroBuilderEditorV2SelectionCapability.Move))
        {
            error =
                "La selección actual no puede moverse como conjunto.";
            return false;
        }

        if (interactionController == null ||
            !interactionController.TryBeginMoveSelected())
        {
            error =
                "No se pudo iniciar el movimiento del conjunto.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryDuplicateSelection(
        Vector3 worldOffset,
        out IReadOnlyList<RestaurantPlaceableObject> created,
        out string error)
    {
        created = Array.Empty<RestaurantPlaceableObject>();

        if (!TryResolveSelection(
                BistroBuilderEditorV2SelectionCapability.Duplicate,
                out error))
        {
            return false;
        }

        if (creationService == null)
        {
            error = "El servicio de creación no está disponible.";
            return false;
        }

        if (!creationService.TryDuplicateBatch(
                scratchPlaceables,
                worldOffset,
                out created,
                out RestaurantPlaceableBatchCreationResult result))
        {
            error = result.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryDeleteSelection(out string error)
    {
        if (!TryResolveSelection(
                BistroBuilderEditorV2SelectionCapability.Delete,
                out error))
        {
            return false;
        }

        if (deletionService == null)
        {
            error = "El servicio de eliminación no está disponible.";
            return false;
        }

        if (!deletionService.TryDeleteBatch(
                scratchPlaceables,
                out RestaurantPlaceableBatchDeletionResult result))
        {
            error = result.Message;
            return false;
        }

        interactionController?.ClearSelection();
        selectionCoordinator?.ClearSelectionSet();

        error = string.Empty;
        return true;
    }

    public int CopySelectedPlaceables(
        List<RestaurantPlaceableObject> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (!TryResolveSelection(
                BistroBuilderEditorV2SelectionCapability.Inspect,
                out _))
        {
            return 0;
        }

        results.AddRange(scratchPlaceables);
        return results.Count;
    }

    private bool TryResolveSelection(
        BistroBuilderEditorV2SelectionCapability required,
        out string error)
    {
        CacheDependencies();
        scratchSelections.Clear();
        scratchPlaceables.Clear();

        if (selectionCoordinator == null ||
            registry == null)
        {
            error =
                "La multiselección de Editor V2 no está disponible.";
            return false;
        }

        if (!selectionCoordinator.SelectionSetSupports(required))
        {
            error =
                "No todos los miembros admiten " + required + ".";
            return false;
        }

        selectionCoordinator.CopySelectionSet(
            scratchSelections,
            true);

        for (int i = 0; i < scratchSelections.Count; i++)
        {
            BistroBuilderEditorV2Selection selection =
                scratchSelections[i];

            if (selection.family !=
                    BistroBuilderEditorV2ToolFamily.Furniture ||
                selection.kind !=
                    BistroBuilderEditorV2SelectionKind.Furniture ||
                !selection.persistentIdentity ||
                !registry.TryGetByInstanceId(
                    selection.stableId,
                    out RestaurantPlaceableObject placeable) ||
                placeable == null ||
                !placeable.gameObject.activeInHierarchy)
            {
                scratchSelections.Clear();
                scratchPlaceables.Clear();
                error =
                    "La selección contiene un miembro que ya no está disponible.";
                return false;
            }

            scratchPlaceables.Add(placeable);
        }

        if (scratchPlaceables.Count == 0)
        {
            error = "No hay artículos seleccionados.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
    }

    private void CacheDependencies()
    {
        if (selectionCoordinator == null)
        {
            selectionCoordinator =
                FindFirstObjectByType<
                    BistroBuilderEditorV2SelectionCoordinator>(
                        FindObjectsInactive.Include);
        }

        if (registry == null)
        {
            registry =
                FindFirstObjectByType<RestaurantPlaceableRegistry>(
                    FindObjectsInactive.Include);
        }

        if (interactionController == null)
        {
            interactionController =
                FindFirstObjectByType<
                    RestaurantEditInteractionController>(
                        FindObjectsInactive.Include);
        }

        if (creationService == null)
        {
            creationService =
                FindFirstObjectByType<RestaurantPlaceableCreationService>(
                    FindObjectsInactive.Include);
        }

        if (deletionService == null)
        {
            deletionService =
                FindFirstObjectByType<RestaurantPlaceableDeletionService>(
                    FindObjectsInactive.Include);
        }
    }
}
