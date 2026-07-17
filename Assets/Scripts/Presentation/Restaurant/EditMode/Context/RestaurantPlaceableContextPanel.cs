using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel contextual del modo edición para el artículo seleccionado.
///
/// La selección no modifica el restaurante. Las acciones se solicitan
/// explícitamente:
/// - Mover abre una transacción de colocación.
/// - Eliminar utiliza el servicio reversible y el historial.
///
/// La interfaz no contiene lógica espacial ni de ciclo de vida.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlaceableContextPanel :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantEditModeService editModeService;

    [SerializeField]
    private RestaurantEditInteractionController
        interactionController;

    [SerializeField]
    private RestaurantPlaceableDeletionService
        deletionService;

    [Header("Interfaz")]

    [SerializeField]
    private GameObject contentRoot;

    [SerializeField]
    private Text nameText;

    [SerializeField]
    private Text categoryText;

    [SerializeField]
    private Text statusText;

    [SerializeField]
    private Button moveButton;

    [SerializeField]
    private Button deleteButton;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ConfigureButtonListeners();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
        ConfigureButtonListeners();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RemoveButtonListeners();
    }

    private void Subscribe()
    {
        if (editModeService != null)
        {
            editModeService.EditModeEntered +=
                HandleEditModeChanged;

            editModeService.EditModeExited +=
                HandleEditModeChanged;
        }

        if (interactionController != null)
        {
            interactionController.SelectedEditableObjectChanged +=
                HandleSelectionChanged;

            interactionController.ActiveEditableObjectChanged +=
                HandleActivePlacementChanged;

            interactionController.InteractionMessageChanged +=
                HandleInteractionMessageChanged;
        }

        if (deletionService != null)
        {
            deletionService.PlaceableDeletionRejected +=
                HandleDeletionRejected;
        }
    }

    private void Unsubscribe()
    {
        if (editModeService != null)
        {
            editModeService.EditModeEntered -=
                HandleEditModeChanged;

            editModeService.EditModeExited -=
                HandleEditModeChanged;
        }

        if (interactionController != null)
        {
            interactionController.SelectedEditableObjectChanged -=
                HandleSelectionChanged;

            interactionController.ActiveEditableObjectChanged -=
                HandleActivePlacementChanged;

            interactionController.InteractionMessageChanged -=
                HandleInteractionMessageChanged;
        }

        if (deletionService != null)
        {
            deletionService.PlaceableDeletionRejected -=
                HandleDeletionRejected;
        }
    }

    private void ConfigureButtonListeners()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(
                HandleMoveClicked
            );

            moveButton.onClick.AddListener(
                HandleMoveClicked
            );
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(
                HandleDeleteClicked
            );

            deleteButton.onClick.AddListener(
                HandleDeleteClicked
            );
        }
    }

    private void RemoveButtonListeners()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(
                HandleMoveClicked
            );
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(
                HandleDeleteClicked
            );
        }
    }

    private void HandleEditModeChanged()
    {
        Refresh();
    }

    private void HandleSelectionChanged(
        RestaurantEditableObject editableObject
    )
    {
        Refresh();
    }

    private void HandleActivePlacementChanged(
        RestaurantEditableObject editableObject
    )
    {
        RefreshInteractivity();
    }

    private void HandleInteractionMessageChanged(
        string message
    )
    {
        if (statusText == null ||
            string.IsNullOrWhiteSpace(message) ||
            contentRoot == null ||
            !contentRoot.activeSelf)
        {
            return;
        }

        statusText.text =
            message;
    }

    private void HandleDeletionRejected(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableDeletionResult result
    )
    {
        if (statusText != null)
        {
            statusText.text =
                result.Message;
        }
    }

    private void HandleMoveClicked()
    {
        if (interactionController == null)
        {
            return;
        }

        interactionController.TryBeginMoveSelected();
        RefreshInteractivity();
    }

    private void HandleDeleteClicked()
    {
        if (interactionController == null ||
            deletionService == null)
        {
            return;
        }

        RestaurantEditableObject editableObject =
            interactionController.SelectedEditableObject;

        if (editableObject == null ||
            !editableObject.TryGetComponent(
                out RestaurantPlaceableObject placeable
            ))
        {
            if (statusText != null)
            {
                statusText.text =
                    "El objeto seleccionado no es un artículo colocable.";
            }

            return;
        }

        bool deleted =
            deletionService.TryDelete(
                placeable,
                out RestaurantPlaceableDeletionResult result
            );

        if (!deleted)
        {
            if (statusText != null)
            {
                statusText.text =
                    result.Message;
            }

            return;
        }

        interactionController.ClearSelection();
        Refresh();
    }

    private void Refresh()
    {
        bool shouldShow =
            editModeService != null &&
            editModeService.IsEditModeActive &&
            interactionController != null &&
            interactionController.HasSelection;

        if (contentRoot != null)
        {
            contentRoot.SetActive(
                shouldShow
            );
        }

        if (!shouldShow)
        {
            return;
        }

        RestaurantEditableObject editableObject =
            interactionController.SelectedEditableObject;

        RestaurantPlaceableObject placeable =
            editableObject != null
                ? editableObject.GetComponent<
                    RestaurantPlaceableObject
                >()
                : null;

        string displayName =
            placeable != null
                ? placeable.DisplayName
                : (
                    editableObject != null
                        ? editableObject.DisplayName
                        : "Artículo"
                );

        if (nameText != null)
        {
            nameText.text =
                displayName;
        }

        if (categoryText != null)
        {
            categoryText.text =
                ResolveCategoryLabel(placeable);
        }

        if (statusText != null)
        {
            statusText.text =
                "Seleccionado. Elige una acción.";
        }

        RefreshInteractivity();
    }

    private void RefreshInteractivity()
    {
        bool hasSelection =
            interactionController != null &&
            interactionController.HasSelection;

        bool placementActive =
            interactionController != null &&
            interactionController.HasActivePlacement;

        RestaurantEditableObject editableObject =
            interactionController != null
                ? interactionController.SelectedEditableObject
                : null;

        if (moveButton != null)
        {
            moveButton.interactable =
                hasSelection &&
                !placementActive &&
                editableObject != null &&
                editableObject.CanMove;
        }

        if (deleteButton != null)
        {
            deleteButton.interactable =
                hasSelection &&
                !placementActive &&
                editableObject != null &&
                editableObject.GetComponent<
                    RestaurantPlaceableObject
                >() != null;
        }
    }

    private static string ResolveCategoryLabel(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null ||
            placeable.ItemDefinition == null)
        {
            return "Artículo colocable";
        }

        switch (placeable.ItemDefinition.Category)
        {
            case RestaurantPlaceableItemCategory.Furniture:
                return "Mobiliario";

            case RestaurantPlaceableItemCategory.Seating:
                return "Asientos";

            case RestaurantPlaceableItemCategory.Lighting:
                return "Iluminación";

            case RestaurantPlaceableItemCategory.Decoration:
                return "Decoración";

            case RestaurantPlaceableItemCategory.KitchenEquipment:
                return "Equipamiento de cocina";

            case RestaurantPlaceableItemCategory.ServiceEquipment:
                return "Equipamiento de servicio";

            case RestaurantPlaceableItemCategory.Structural:
                return "Estructura";

            default:
                return "Otros";
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editModeService == null)
        {
            editModeService =
                FindFirstObjectByType<
                    RestaurantEditModeService
                >();
        }

        if (interactionController == null)
        {
            interactionController =
                FindFirstObjectByType<
                    RestaurantEditInteractionController
                >();
        }

        if (deletionService == null)
        {
            deletionService =
                FindFirstObjectByType<
                    RestaurantPlaceableDeletionService
                >();
        }
    }
}
