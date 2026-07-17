using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador de presentación del catálogo de artículos colocables.
///
/// Responsabilidades:
/// - Mostrar el catálogo únicamente durante el modo edición.
/// - Crear categorías y tarjetas a partir de plantillas.
/// - Filtrar artículos por categoría.
/// - Solicitar al controlador de interacción el inicio de creación.
/// - Bloquear nuevas selecciones mientras existe una colocación activa.
///
/// No contiene reglas de validación espacial, economía ni ciclo de vida.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlaceableCatalogPanel :
    MonoBehaviour
{
    private const int AllCategoriesCode = -1;

    [Header("Dependencias")]

    [SerializeField]
    private RestaurantEditModeService
        editModeService;

    [SerializeField]
    private RestaurantEditInteractionController
        interactionController;

    [SerializeField]
    private RestaurantPlaceableCatalogService
        catalogService;

    [Header("Estructura visual")]

    [SerializeField]
    private GameObject contentRoot;

    [SerializeField]
    private RectTransform categoryContainer;

    [SerializeField]
    private RectTransform itemContainer;

    [SerializeField]
    private RestaurantPlaceableCatalogCategoryView
        categoryTemplate;

    [SerializeField]
    private RestaurantPlaceableCatalogItemView
        itemTemplate;

    [SerializeField]
    private Text titleText;

    [SerializeField]
    private Text statusText;

    private readonly List<RestaurantPlaceableCatalogCategoryView>
        categoryViews =
            new List<RestaurantPlaceableCatalogCategoryView>(12);

    private readonly List<RestaurantPlaceableCatalogItemView>
        itemViews =
            new List<RestaurantPlaceableCatalogItemView>(64);

    private readonly HashSet<RestaurantPlaceableItemCategory>
        availableCategories =
            new HashSet<RestaurantPlaceableItemCategory>();

    private int selectedCategoryCode =
        AllCategoriesCode;

    private bool initialized;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void Start()
    {
        InitializeIfNeeded();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        initialized =
            true;

        if (titleText != null)
        {
            titleText.text =
                "Catálogo de artículos";
        }

        RebuildCatalogPresentation();
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
            interactionController.ActiveEditableObjectChanged +=
                HandleActiveEditableObjectChanged;

            interactionController.InteractionMessageChanged +=
                HandleInteractionMessageChanged;
        }

        if (catalogService != null)
        {
            catalogService.CatalogChanged +=
                HandleCatalogChanged;
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
            interactionController.ActiveEditableObjectChanged -=
                HandleActiveEditableObjectChanged;

            interactionController.InteractionMessageChanged -=
                HandleInteractionMessageChanged;
        }

        if (catalogService != null)
        {
            catalogService.CatalogChanged -=
                HandleCatalogChanged;
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

        if (catalogService == null)
        {
            catalogService =
                FindFirstObjectByType<
                    RestaurantPlaceableCatalogService
                >();
        }
    }

    private void HandleEditModeChanged()
    {
        RefreshVisibility();
        RefreshInteractivity();
    }

    private void HandleActiveEditableObjectChanged(
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
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        statusText.text =
            message;
    }

    private void HandleCatalogChanged()
    {
        RebuildCatalogPresentation();
    }

    private void RefreshVisibility()
    {
        bool shouldBeVisible =
            editModeService != null &&
            editModeService.IsEditModeActive;

        if (contentRoot != null)
        {
            contentRoot.SetActive(
                shouldBeVisible
            );
        }

        if (shouldBeVisible &&
            statusText != null &&
            !interactionController.HasActivePlacement)
        {
            statusText.text =
                "Selecciona un artículo para colocarlo.";
        }
    }

    private void RebuildCatalogPresentation()
    {
        ClearGeneratedViews();

        if (catalogService == null ||
            categoryContainer == null ||
            itemContainer == null ||
            categoryTemplate == null ||
            itemTemplate == null)
        {
            if (statusText != null)
            {
                statusText.text =
                    "El catálogo no está configurado.";
            }

            return;
        }

        IReadOnlyList<RestaurantPlaceableItemDefinition> items =
            catalogService.AvailableItems;

        availableCategories.Clear();

        for (int index = 0;
             index < items.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition item =
                items[index];

            if (item != null)
            {
                availableCategories.Add(
                    item.Category
                );
            }
        }

        if (selectedCategoryCode != AllCategoriesCode &&
            !availableCategories.Contains(
                (RestaurantPlaceableItemCategory)
                    selectedCategoryCode
            ))
        {
            selectedCategoryCode =
                AllCategoriesCode;
        }

        CreateCategoryView(
            AllCategoriesCode,
            "Todos"
        );

        foreach (RestaurantPlaceableItemCategory category
                 in Enum.GetValues(
                     typeof(RestaurantPlaceableItemCategory)
                 ))
        {
            if (!availableCategories.Contains(category))
            {
                continue;
            }

            CreateCategoryView(
                (int)category,
                GetCategoryLabel(category)
            );
        }

        RebuildItemViews();
        RefreshInteractivity();
    }

    private void RebuildItemViews()
    {
        ClearItemViews();

        if (catalogService == null ||
            itemContainer == null ||
            itemTemplate == null)
        {
            return;
        }

        IReadOnlyList<RestaurantPlaceableItemDefinition> items =
            catalogService.AvailableItems;

        for (int index = 0;
             index < items.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition item =
                items[index];

            if (item == null ||
                !MatchesSelectedCategory(item))
            {
                continue;
            }

            RestaurantPlaceableCatalogItemView view =
                Instantiate(
                    itemTemplate,
                    itemContainer
                );

            view.gameObject.name =
                "CatalogItem_" +
                item.ItemId;

            view.gameObject.SetActive(true);

            view.Bind(
                item,
                HandleItemSelected
            );

            itemViews.Add(view);
        }

        if (statusText != null &&
            itemViews.Count == 0)
        {
            statusText.text =
                "No hay artículos disponibles en esta categoría.";
        }
    }

    private void CreateCategoryView(
        int categoryCode,
        string label
    )
    {
        RestaurantPlaceableCatalogCategoryView view =
            Instantiate(
                categoryTemplate,
                categoryContainer
            );

        view.gameObject.name =
            categoryCode == AllCategoriesCode
                ? "Category_All"
                : "Category_" + categoryCode;

        view.gameObject.SetActive(true);

        view.Bind(
            categoryCode,
            label,
            categoryCode == selectedCategoryCode,
            HandleCategorySelected
        );

        categoryViews.Add(view);
    }

    private void HandleCategorySelected(
        int categoryCode
    )
    {
        if (interactionController != null &&
            interactionController.HasActivePlacement)
        {
            return;
        }

        selectedCategoryCode =
            categoryCode;

        for (int index = 0;
             index < categoryViews.Count;
             index++)
        {
            RestaurantPlaceableCatalogCategoryView view =
                categoryViews[index];

            if (view == null)
            {
                continue;
            }

            bool selected =
                view.gameObject.name ==
                (
                    categoryCode == AllCategoriesCode
                        ? "Category_All"
                        : "Category_" + categoryCode
                );

            view.SetSelected(selected);
        }

        RebuildItemViews();

        if (statusText != null)
        {
            statusText.text =
                "Selecciona un artículo para colocarlo.";
        }
    }

    private void HandleItemSelected(
        RestaurantPlaceableItemDefinition definition
    )
    {
        if (definition == null ||
            interactionController == null)
        {
            return;
        }

        bool began =
            interactionController.TryBeginPlaceableCreation(
                definition
            );

        if (statusText != null)
        {
            statusText.text =
                began
                    ? "Colocando " +
                      definition.DisplayName +
                      ". Clic para confirmar; Escape para cancelar."
                    : "No se pudo iniciar la colocación.";
        }

        RefreshInteractivity();
    }

    private void RefreshInteractivity()
    {
        bool canSelect =
            editModeService != null &&
            editModeService.IsEditModeActive &&
            interactionController != null &&
            !interactionController.HasActivePlacement;

        for (int index = 0;
             index < categoryViews.Count;
             index++)
        {
            RestaurantPlaceableCatalogCategoryView view =
                categoryViews[index];

            if (view != null)
            {
                view.SetInteractable(
                    canSelect
                );
            }
        }

        for (int index = 0;
             index < itemViews.Count;
             index++)
        {
            RestaurantPlaceableCatalogItemView view =
                itemViews[index];

            if (view != null)
            {
                view.SetInteractable(
                    canSelect
                );
            }
        }
    }

    private bool MatchesSelectedCategory(
        RestaurantPlaceableItemDefinition item
    )
    {
        return
            selectedCategoryCode == AllCategoriesCode ||
            (int)item.Category == selectedCategoryCode;
    }

    private void ClearGeneratedViews()
    {
        ClearCategoryViews();
        ClearItemViews();
    }

    private void ClearCategoryViews()
    {
        for (int index = 0;
             index < categoryViews.Count;
             index++)
        {
            RestaurantPlaceableCatalogCategoryView view =
                categoryViews[index];

            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        categoryViews.Clear();
    }

    private void ClearItemViews()
    {
        for (int index = 0;
             index < itemViews.Count;
             index++)
        {
            RestaurantPlaceableCatalogItemView view =
                itemViews[index];

            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        itemViews.Clear();
    }

    private static string GetCategoryLabel(
        RestaurantPlaceableItemCategory category
    )
    {
        switch (category)
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
                return "Cocina";

            case RestaurantPlaceableItemCategory.ServiceEquipment:
                return "Servicio";

            case RestaurantPlaceableItemCategory.Structural:
                return "Estructura";

            default:
                return "Otros";
        }
    }
}
