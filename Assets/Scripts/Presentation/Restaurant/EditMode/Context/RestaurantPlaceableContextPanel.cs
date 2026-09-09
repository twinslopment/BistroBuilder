using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel contextual del modo edición. Presenta la política económica resuelta
/// por 3F sin duplicar cálculos financieros dentro de la UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlaceableContextPanel : MonoBehaviour
{
    [SerializeField]
    private RestaurantEditModeService editModeService;

    [SerializeField]
    private RestaurantEditInteractionController interactionController;

    [SerializeField]
    private RestaurantPlaceableDeletionService deletionService;

    [SerializeField]
    private BistroBuilderPlaceableFinanceBridge placeableFinanceBridge;

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

    private Text deleteButtonText;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        deleteButtonText = deleteButton != null
            ? deleteButton.GetComponentInChildren<Text>(true)
            : null;
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
            editModeService.EditModeEntered += HandleEditModeChanged;
            editModeService.EditModeExited += HandleEditModeChanged;
        }

        if (interactionController != null)
        {
            interactionController.SelectedEditableObjectChanged += HandleSelectionChanged;
            interactionController.ActiveEditableObjectChanged += HandleActivePlacementChanged;
            interactionController.InteractionMessageChanged += HandleInteractionMessageChanged;
        }

        if (deletionService != null)
        {
            deletionService.PlaceableDeletionRejected += HandleDeletionRejected;
        }
    }

    private void Unsubscribe()
    {
        if (editModeService != null)
        {
            editModeService.EditModeEntered -= HandleEditModeChanged;
            editModeService.EditModeExited -= HandleEditModeChanged;
        }

        if (interactionController != null)
        {
            interactionController.SelectedEditableObjectChanged -= HandleSelectionChanged;
            interactionController.ActiveEditableObjectChanged -= HandleActivePlacementChanged;
            interactionController.InteractionMessageChanged -= HandleInteractionMessageChanged;
        }

        if (deletionService != null)
        {
            deletionService.PlaceableDeletionRejected -= HandleDeletionRejected;
        }
    }

    private void ConfigureButtonListeners()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(HandleMoveClicked);
            moveButton.onClick.AddListener(HandleMoveClicked);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            deleteButton.onClick.AddListener(HandleDeleteClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(HandleMoveClicked);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
        }
    }

    private void HandleEditModeChanged()
    {
        Refresh();
    }

    private void HandleSelectionChanged(RestaurantEditableObject editableObject)
    {
        Refresh();
    }

    private void HandleActivePlacementChanged(RestaurantEditableObject editableObject)
    {
        RefreshInteractivity();
    }

    private void HandleInteractionMessageChanged(string message)
    {
        if (statusText != null &&
            !string.IsNullOrWhiteSpace(message) &&
            contentRoot != null && contentRoot.activeSelf)
        {
            statusText.text = message;
        }
    }

    private void HandleDeletionRejected(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableDeletionResult result)
    {
        if (statusText != null)
        {
            statusText.text = result.Message;
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
        if (interactionController == null || deletionService == null)
        {
            return;
        }

        RestaurantEditableObject editableObject =
            interactionController.SelectedEditableObject;
        if (editableObject == null ||
            !editableObject.TryGetComponent(out RestaurantPlaceableObject placeable))
        {
            if (statusText != null)
            {
                statusText.text = "El objeto seleccionado no es un artículo colocable.";
            }
            return;
        }

        if (!deletionService.TryDelete(
                placeable,
                out RestaurantPlaceableDeletionResult result))
        {
            if (statusText != null)
            {
                statusText.text = result.Message;
            }
            return;
        }

        interactionController.ClearSelection();
        Refresh();
    }

    private void Refresh()
    {
        bool shouldShow =
            editModeService != null && editModeService.IsEditModeActive &&
            interactionController != null && interactionController.HasSelection;

        if (contentRoot != null)
        {
            contentRoot.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            return;
        }

        RestaurantEditableObject editableObject =
            interactionController.SelectedEditableObject;
        RestaurantPlaceableObject placeable = editableObject != null
            ? editableObject.GetComponent<RestaurantPlaceableObject>()
            : null;

        if (nameText != null)
        {
            nameText.text = placeable != null
                ? placeable.DisplayName
                : editableObject != null
                    ? editableObject.DisplayName
                    : "Artículo";
        }

        if (categoryText != null)
        {
            categoryText.text = ResolveCategoryLabel(placeable);
        }

        if (statusText != null)
        {
            statusText.text = BuildSelectionStatus(placeable);
        }

        RefreshInteractivity();
    }

    private void RefreshInteractivity()
    {
        bool hasSelection =
            interactionController != null && interactionController.HasSelection;
        bool placementActive =
            interactionController != null && interactionController.HasActivePlacement;
        RestaurantEditableObject editableObject = interactionController != null
            ? interactionController.SelectedEditableObject
            : null;

        if (moveButton != null)
        {
            moveButton.interactable =
                hasSelection && !placementActive &&
                editableObject != null && editableObject.CanMove;
        }

        RestaurantPlaceableObject placeable = editableObject != null
            ? editableObject.GetComponent<RestaurantPlaceableObject>()
            : null;

        if (deleteButton != null)
        {
            deleteButton.interactable =
                hasSelection && !placementActive && placeable != null;
        }

        RefreshDeleteButtonLabel(placeable);
    }

    private void RefreshDeleteButtonLabel(RestaurantPlaceableObject placeable)
    {
        if (deleteButtonText == null)
        {
            return;
        }

        if (!TryResolveDisposal(placeable, out var preview))
        {
            deleteButtonText.text = "Eliminar";
            return;
        }

        if (preview.Mode == RestaurantPlaceableDisposalMode.Demolition)
        {
            deleteButtonText.text = preview.RemovalCostCents > 0L
                ? "Demoler " + FormatSigned(-preview.RemovalCostCents)
                : "Demoler";
            return;
        }

        if (preview.NetCashCents > 0L)
        {
            deleteButtonText.text = "Vender " + FormatSigned(preview.NetCashCents);
        }
        else if (preview.NetCashCents < 0L)
        {
            deleteButtonText.text = "Retirar " + FormatSigned(preview.NetCashCents);
        }
        else
        {
            deleteButtonText.text = preview.Mode == RestaurantPlaceableDisposalMode.None
                ? "Eliminar"
                : "Retirar";
        }
    }

    private string BuildSelectionStatus(RestaurantPlaceableObject placeable)
    {
        if (!TryResolveDisposal(placeable, out var preview))
        {
            return "Seleccionado. Elige una acción.";
        }

        if (preview.Mode == RestaurantPlaceableDisposalMode.Demolition &&
            preview.RemovalCostCents > 0L)
        {
            return "Coste de demolición: " +
                   FormatMoney(preview.RemovalCostCents) + ".";
        }

        if (preview.NetCashCents > 0L)
        {
            return "Valor neto de reventa: +" +
                   FormatMoney(preview.NetCashCents) + ".";
        }

        if (preview.NetCashCents < 0L)
        {
            return "Coste neto de retirada: -" +
                   FormatMoney(-preview.NetCashCents) + ".";
        }

        return "Seleccionado. Elige una acción.";
    }

    private bool TryResolveDisposal(
        RestaurantPlaceableObject placeable,
        out BistroBuilderPlaceableDisposalPreview preview)
    {
        preview = default;
        return placeable != null &&
               placeableFinanceBridge != null &&
               placeableFinanceBridge.TryGetDeletionPreview(
                   placeable,
                   out preview,
                   out _);
    }

    private static string ResolveCategoryLabel(RestaurantPlaceableObject placeable)
    {
        if (placeable == null || placeable.ItemDefinition == null)
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

    private static string FormatSigned(long signedCents)
    {
        return (signedCents > 0L ? "+" : "-") +
               FormatMoney(System.Math.Abs(signedCents));
    }

    private static string FormatMoney(long cents)
    {
        decimal euros = cents / 100m;
        return cents % 100L == 0L
            ? euros.ToString("N0") + " €"
            : euros.ToString("N2") + " €";
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editModeService == null)
        {
            editModeService = FindFirstObjectByType<RestaurantEditModeService>();
        }

        if (interactionController == null)
        {
            interactionController =
                FindFirstObjectByType<RestaurantEditInteractionController>();
        }

        if (deletionService == null)
        {
            deletionService =
                FindFirstObjectByType<RestaurantPlaceableDeletionService>();
        }

        if (placeableFinanceBridge == null)
        {
            placeableFinanceBridge =
                FindFirstObjectByType<BistroBuilderPlaceableFinanceBridge>();
        }
    }
}
