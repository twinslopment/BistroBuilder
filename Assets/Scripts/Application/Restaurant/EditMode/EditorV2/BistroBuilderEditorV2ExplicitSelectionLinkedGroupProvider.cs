using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B8: proyecta la multiselección explícita de Editor V2 sobre la autoridad
/// universal de Linked Groups.
///
/// El proveedor no mueve objetos ni modifica jerarquías. Únicamente describe
/// qué colocables explícitamente seleccionados deben acompañar a la raíz.
/// RestaurantPlacementLinkedGroupService conserva la autoridad sobre preview,
/// validación, cancelación e historial.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Edit Mode/Editor V2/Explicit Selection Linked Group Provider")]
public sealed class BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider :
    MonoBehaviour,
    IRestaurantPlacementLinkedGroupProvider
{
    [SerializeField]
    private BistroBuilderEditorV2SelectionCoordinator selectionCoordinator;

    [SerializeField]
    private RestaurantPlaceableRegistry placeableRegistry;

    [SerializeField]
    private bool linkEnabled = true;

    [SerializeField]
    private int priority = 10;

    private readonly List<BistroBuilderEditorV2Selection> scratchSelections =
        new List<BistroBuilderEditorV2Selection>(32);

    public int Priority => priority;

    public bool IsLinkEnabled =>
        linkEnabled &&
        selectionCoordinator != null &&
        placeableRegistry != null;

    public void Configure(
        BistroBuilderEditorV2SelectionCoordinator selection,
        RestaurantPlaceableRegistry registry)
    {
        selectionCoordinator = selection;
        placeableRegistry = registry;
    }

    public void CollectLinkedMembers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results)
    {
        if (!IsLinkEnabled ||
            rootMember == null ||
            results == null ||
            !rootMember.TryGetComponent(
                out RestaurantPlaceableObject rootPlaceable) ||
            string.IsNullOrWhiteSpace(rootPlaceable.InstanceId) ||
            !selectionCoordinator.ContainsSelection(
                BistroBuilderEditorV2ToolFamily.Furniture,
                BistroBuilderEditorV2SelectionKind.Furniture,
                rootPlaceable.InstanceId))
        {
            return;
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
                string.IsNullOrWhiteSpace(selection.stableId) ||
                string.Equals(
                    selection.stableId,
                    rootPlaceable.InstanceId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            if (!placeableRegistry.TryGetByInstanceId(
                    selection.stableId,
                    out RestaurantPlaceableObject placeable) ||
                placeable == null ||
                !placeable.gameObject.activeInHierarchy ||
                !placeable.TryGetComponent(
                    out RestaurantAreaMember member) ||
                ReferenceEquals(member, rootMember))
            {
                continue;
            }

            results.Add(member);
        }

        scratchSelections.Clear();
    }

    public void NotifyLinkedGroupPoseApplied(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> linkedMembers)
    {
        // La multiselección explícita no posee relaciones funcionales.
        // Los providers semánticos (seating, módulos, etc.) notifican a
        // sus respectivos dominios cuando corresponde.
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

        if (placeableRegistry == null)
        {
            placeableRegistry =
                FindFirstObjectByType<RestaurantPlaceableRegistry>(
                    FindObjectsInactive.Include);
        }
    }
}
