using System;
using System.Collections.Generic;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Vista runtime completa del editor de carta 2.1E.
///
/// Construye una única jerarquía uGUI reutilizable, mantiene un pool de filas
/// y delega toda decisión de dominio en BistroBuilderMenuEditorService. No
/// escribe directamente en la carta, inventario, comandas ni assets.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMenuEditorRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1E-UI";
    private const string SpecialCategoryId = "__special";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderMenuEditorService editorService;

    [SerializeField]
    private BistroBuilderProfessionalCameraController cameraController;

    [SerializeField]
    private RestaurantEditInteractionController editInteractionController;

    [SerializeField]
    private BistroBuilderDishRecipeAuthoringRuntimeView authoringView;

    [SerializeField]
    private BistroBuilderMenuPortfolioRuntimeView portfolioView;

    [SerializeField]
    private BistroBuilderDishImageCatalog dishImageCatalog;

    [Header("Comportamiento")]

    [SerializeField]
    private bool showOpenButton = true;

    private readonly List<BistroBuilderMenuEditorDishSnapshot> snapshots =
        new List<BistroBuilderMenuEditorDishSnapshot>(64);

    private readonly List<BistroBuilderMenuEditorDishSnapshot> filtered =
        new List<BistroBuilderMenuEditorDishSnapshot>(64);

    private readonly Dictionary<string, BistroBuilderMenuEditorDishSnapshot>
        snapshotByDishId =
            new Dictionary<string, BistroBuilderMenuEditorDishSnapshot>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderMenuEditorDishRowView> rowPool =
        new List<BistroBuilderMenuEditorDishRowView>(64);

    private readonly List<Button> categoryButtons = new List<Button>(16);
    private readonly List<string> categoryButtonIds = new List<string>(16);

    private readonly HashSet<string> categoryIdSet =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly List<string> categoryIdBuffer = new List<string>(16);
    private readonly List<string> categoryNameBuffer = new List<string>(16);

    private readonly List<Button> filterButtons = new List<Button>(5);
    private readonly List<BistroBuilderMenuEditorFilter> filterValues =
        new List<BistroBuilderMenuEditorFilter>(5);

    private Button openButton;
    private Button managementTabButton;
    private Button dishesTabButton;
    private RectTransform modalRoot;
    private RectTransform listContent;
    private RectTransform categoryContent;
    private RectTransform confirmationRoot;
    private Text titleText;
    private Text contextText;
    private Text listCountText;
    private Text statusText;
    private Text detailTitleText;
    private Text detailSubtitleText;
    private Text descriptionText;
    private Image detailDishImage;
    private Text detailCategoryText;
    private Text detailModeText;
    private Text detailOrderText;
    private Text detailCostText;
    private Text detailMarginText;
    private Text detailStatusBadgeText;
    private Text economicsText;
    private Text availabilityText;
    private Text recipeText;
    private InputField searchInput;
    private InputField priceInput;
    private InputField preparationDifficultyInput;
    private InputField preparationTimeInput;
    private Text preparationSummaryText;
    private Toggle enabledToggle;
    private Toggle breakfastToggle;
    private Toggle lunchToggle;
    private Toggle dinnerToggle;
    private Toggle soldOutToggle;
    private Toggle signatureToggle;
    private Button includeButton;
    private Button moveUpButton;
    private Button moveDownButton;
    private Button restoreDefaultsButton;
    private Button mealServiceButton;
    private Button serviceModeButton;
    private Button newDishButton;
    private Button editDishRecipeButton;
    private Button applyButton;
    private Button discardButton;
    private Button reloadButton;
    private GameObject detailContentRoot;

    private BistroBuilderMenuEditorSummarySnapshot summary;
    private BistroBuilderMenuEditorDishSnapshot selectedSnapshot;
    private BistroBuilderMenuEditorFilter filter =
        BistroBuilderMenuEditorFilter.All;
    private string selectedCategoryId = string.Empty;
    private string selectedDishId = string.Empty;
    private string searchText = string.Empty;
    private bool visualTreeBuilt;
    private bool subscribed;
    private bool suppressCallbacks;
    private bool cameraWasEnabled;
    private bool editInteractionWasEnabled;
    private bool inputGateApplied;

    public BistroBuilderMenuEditorService EditorService => editorService;

    public BistroBuilderProfessionalCameraController CameraController =>
        cameraController;

    public RestaurantEditInteractionController EditInteractionController =>
        editInteractionController;

    public BistroBuilderDishRecipeAuthoringRuntimeView AuthoringView =>
        authoringView;

    public bool VisualTreeBuilt => visualTreeBuilt;
    public bool IsOpen => modalRoot != null && modalRoot.gameObject.activeSelf;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVisualTree();
        SetEditorVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void Start()
    {
        EnsureVisualTree();
        openButton.gameObject.SetActive(showOpenButton);
    }

    private void Update()
    {
        if (!Application.isPlaying || modalRoot == null ||
            !modalRoot.gameObject.activeSelf)
        {
            return;
        }

        if (authoringView != null && authoringView.IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            RequestClose();
        }
    }

    private void OnDisable()
    {
        if (editorService != null && editorService.IsOpen)
        {
            editorService.TryClose(true, out _);
        }

        Unsubscribe();
        RestoreInputGate();
    }

    private void OnDestroy()
    {
        if (editorService != null && editorService.IsOpen)
        {
            editorService.TryClose(true, out _);
        }
    }

    public bool TryValidateVisibleContent(out string error)
    {
        EnsureVisualTree();

        if (editorService == null || !editorService.IsOpen)
        {
            error = "El editor debe tener una sesión abierta para validar su contenido visual.";
            return false;
        }

        bool wasVisible = modalRoot != null && modalRoot.gameObject.activeSelf;

        if (!wasVisible)
        {
            SetEditorVisible(true);
        }

        try
        {
            RefreshSnapshot(string.Empty);
            Canvas.ForceUpdateCanvases();

            if (categoryContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
            }

            if (listContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            }

            Canvas.ForceUpdateCanvases();

            ScrollRect[] scrollRects = modalRoot.GetComponentsInChildren<ScrollRect>(true);

            if (scrollRects.Length < 2)
            {
                error = "La vista Carta/Platos no contiene los scrolls de categorías y platos esperados.";
                return false;
            }

            for (int index = 0; index < scrollRects.Length; index++)
            {
                ScrollRect scrollRect = scrollRects[index];

                if (scrollRect.viewport == null)
                {
                    error = "El scroll " + scrollRect.name + " no tiene viewport.";
                    return false;
                }

                if (scrollRect.viewport.GetComponent<RectMask2D>() == null)
                {
                    error = "El viewport de " + scrollRect.name +
                        " no utiliza RectMask2D.";
                    return false;
                }

                if (scrollRect.viewport.GetComponent<Mask>() != null)
                {
                    error = "El viewport de " + scrollRect.name +
                        " conserva un Mask clásico incompatible.";
                    return false;
                }
            }

            int activeRows = 0;

            for (int index = 0; index < rowPool.Count; index++)
            {
                if (rowPool[index] != null && rowPool[index].gameObject.activeSelf)
                {
                    activeRows++;
                }
            }

            if (filtered.Count > 0 && activeRows != filtered.Count)
            {
                error = "La lista contiene " + filtered.Count +
                    " platos, pero solo " + activeRows +
                    " filas visuales están activas.";
                return false;
            }

            if (snapshots.Count > 0 && categoryButtons.Count == 0)
            {
                error = "Hay platos cargados, pero no existen botones de categoría.";
                return false;
            }

            if (filtered.Count > 0 &&
                (listContent == null || listContent.rect.height <= 1f))
            {
                error = "El contenido de la lista no tiene altura visible.";
                return false;
            }

            if (preparationDifficultyInput == null ||
                preparationTimeInput == null ||
                preparationSummaryText == null)
            {
                error = "El detalle no contiene los controles 2.1F de preparación.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        finally
        {
            if (!wasVisible)
            {
                SetEditorVisible(false);
            }
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (editorService == null)
        {
            error = "Falta BistroBuilderMenuEditorService.";
            return false;
        }

        if (!editorService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (authoringView == null ||
            !authoringView.ValidateConfiguration(out error))
        {
            return false;
        }

        if (GetComponentInParent<Canvas>() == null)
        {
            error = "La vista 2.1E debe estar bajo un Canvas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryOpenFromInterface(out string error)
    {
        EnsureVisualTree();

        if (!editorService.TryOpen(out error))
        {
            ShowStatus(error, true);
            return false;
        }

        SetEditorVisible(true);
        ApplyInputGate();
        RefreshSnapshot("Carta cargada.");
        error = string.Empty;
        return true;
    }

    private void EnsureVisualTree()
    {
        if (visualTreeBuilt)
        {
            return;
        }

        RectTransform host = transform as RectTransform;
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;

        openButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpenMenuEditor",
            host,
            "MENÚ",
            HandleOpenClicked,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            15
        );
        RectTransform openRect = openButton.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(0f, 1f);
        openRect.anchorMax = new Vector2(0f, 1f);
        openRect.pivot = new Vector2(0f, 1f);
        openRect.anchoredPosition = new Vector2(18f, -18f);
        openRect.sizeDelta = new Vector2(118f, 40f);

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MenuEditorModal",
            host,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        if (modalRoot.GetComponent<BistroBuilderUiStyleIsolation>() == null)
            modalRoot.gameObject.AddComponent<BistroBuilderUiStyleIsolation>();
        BistroBuilderMenuEditorUiFactory.AddImage(
            modalRoot,
            BistroBuilderMenuEditorUiFactory.Overlay
        );
        modalRoot.offsetMin = new Vector2(0, 76);
        modalRoot.offsetMax = new Vector2(0, -76);

        RectTransform panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Panel",
            modalRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(18f, 12f),
            new Vector2(-18f, -12f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface
        );
        BistroBuilderManagementViewport.Install(modalRoot, panel, 760);
        BistroBuilderMenuEditorUiFactory.StylePlate(panel);

        BuildHeader(panel);
        BuildTabs(panel);
        BuildBody(panel);
        BuildApprovedFooter(panel);
        BuildConfirmation(modalRoot);
        BistroBuilderCartaReferenceV3Style.ApplyEditor(panel);
        BistroBuilderCartaReferenceV3Style.StyleNavigationTab(
            managementTabButton, false
        );
        BistroBuilderCartaReferenceV3Style.StyleNavigationTab(
            dishesTabButton, true
        );
        visualTreeBuilt = true;
    }

    private void BuildHeader(RectTransform panel)
    {
        RectTransform header = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Header",
            panel,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(14f, -66f),
            new Vector2(-14f, -8f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            header,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        titleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            header,
            "CARTA",
            30,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
        titleText.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(58f, 0f);
        titleText.rectTransform.offsetMax = Vector2.zero;
        BistroBuilderMenuEditorUiFactory.AddMenuHeaderIcon(header);

        contextText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Context",
            header,
            "Define, organiza y personaliza los platos de tu carta.",
            12,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        contextText.rectTransform.anchorMin = new Vector2(0.56f, 0f);
        contextText.rectTransform.anchorMax = new Vector2(0.91f, 1f);
        contextText.rectTransform.offsetMin = new Vector2(8f, 0f);
        contextText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        mealServiceButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MealService",
            header,
            "Comida",
            CycleMealService,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        SetAnchoredColumn(mealServiceButton, 0.54f, 0.67f, 9f);

        serviceModeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "ServiceMode",
            header,
            "Mesa",
            CycleServiceMode,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        SetAnchoredColumn(serviceModeButton, 0.68f, 0.79f, 9f);

        newDishButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "NewDish",
            header,
            "Nuevo plato",
            OpenNewDishAuthoring,
            BistroBuilderMenuEditorUiFactory.Positive,
            13
        );
        SetAnchoredColumn(newDishButton, 0.80f, 0.89f, 9f);

        // The approved Carta shell keeps service selectors out of this screen.
        // They remain alive for domain state/refresh, but are not part of the
        // visual contract shown to the player.
        mealServiceButton.gameObject.SetActive(false);
        serviceModeButton.gameObject.SetActive(false);
        newDishButton.gameObject.SetActive(false);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            header,
            "×",
            RequestClose,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            22
        );
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.offsetMin = new Vector2(-44f, -18f);
        closeRect.offsetMax = new Vector2(-8f, 18f);
    }

    private void BuildTabs(RectTransform panel)
    {
        RectTransform tabs = BistroBuilderMenuEditorUiFactory.CreateRect(
            "CartaTabs",
            panel,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(18f, -112f),
            new Vector2(-18f, -76f)
        );

        managementTabButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "CartaTabManagement",
            tabs,
            "Cartas y reglas",
            OpenManagementTab,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        SetRect(
            managementTabButton.GetComponent<RectTransform>(),
            0f, 0f, 0.18f, 1f, 0f
        );

        dishesTabButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "CartaTabDishes",
            tabs,
            "Platos",
            () => { },
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        SetRect(
            dishesTabButton.GetComponent<RectTransform>(),
            0.185f, 0f, 0.31f, 1f, 0f
        );
    }

    private void BuildBody(RectTransform panel)
    {
        RectTransform body = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Body",
            panel,
            Vector2.zero,
            Vector2.one,
            new Vector2(12f, 58f),
            new Vector2(-12f, -116f)
        );

        RectTransform sidebar = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Sidebar",
            body,
            Vector2.zero,
            new Vector2(0.16f, 1f),
            Vector2.zero,
            new Vector2(-4f, 0f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            sidebar,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );
        BistroBuilderMenuEditorUiFactory.StylePlate(sidebar);
        BuildSidebar(sidebar);

        RectTransform detail = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Detail",
            body,
            new Vector2(0.72f, 0f),
            Vector2.one,
            new Vector2(4f, 0f),
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            detail,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );
        BistroBuilderMenuEditorUiFactory.StylePlate(detail);
        BuildApprovedDetail(detail);

        RectTransform list = BistroBuilderMenuEditorUiFactory.CreateRect(
            "List",
            body,
            new Vector2(0.16f, 0f),
            new Vector2(0.72f, 1f),
            new Vector2(4f, 0f),
            new Vector2(-4f, 0f)
        );
        BuildList(list);
    }

    private void BuildSidebar(RectTransform sidebar)
    {
        ScrollRect categories = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "Categories",
            sidebar,
            out categoryContent
        );
        RectTransform categoriesRect = categories.GetComponent<RectTransform>();
        categoriesRect.anchorMin = Vector2.zero;
        categoriesRect.anchorMax = Vector2.one;
        categoriesRect.offsetMin = new Vector2(7f, 8f);
        categoriesRect.offsetMax = new Vector2(-7f, -8f);
    }

    private void BuildList(RectTransform list)
    {
        BistroBuilderMenuEditorUiFactory.AddImage(
            list,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );
        BistroBuilderMenuEditorUiFactory.StylePlate(list);

        RectTransform listHeader = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ListHeader",
            list,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(8f, -44f),
            new Vector2(-8f, -6f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            listHeader,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        searchInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Search",
            listHeader,
            "Buscar plato…",
            HandleSearchChanged,
            null
        );
        SetRect(searchInput.GetComponent<RectTransform>(),
            0f, 0.08f, 0.54f, 0.92f, 0f);

        Text orderLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "OrderLabel", listHeader, "Ordenar por", 11,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(orderLabel.rectTransform, 0.58f, 0f, 0.72f, 1f, 0f);

        Button orderButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OrderCategory", listHeader, "Categoría", () => { },
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 12
        );
        SetRect(orderButton.GetComponent<RectTransform>(),
            0.73f, 0.08f, 0.92f, 0.92f, 0f);

        Button orderDirection = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OrderDirection", listHeader, "↕", () => { },
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 14
        );
        SetRect(orderDirection.GetComponent<RectTransform>(),
            0.93f, 0.08f, 1f, 0.92f, 0f);

        RectTransform columnHeader = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ColumnHeader",
            list,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(8f, -73f),
            new Vector2(-8f, -47f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            columnHeader,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        Text columns = BistroBuilderMenuEditorUiFactory.CreateText(
            "Columns",
            columnHeader,
            "PLATO                         PRECIO      DISPONIBLE      MODALIDAD       PREP.        MARGEN",
            10,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        columns.rectTransform.offsetMin = new Vector2(10f, 0f);
        columns.rectTransform.offsetMax = new Vector2(-64f, 0f);

        listCountText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Count",
            columnHeader,
            string.Empty,
            10,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        listCountText.rectTransform.anchorMin = new Vector2(0.91f, 0f);
        listCountText.rectTransform.anchorMax = Vector2.one;
        listCountText.rectTransform.offsetMin = Vector2.zero;
        listCountText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "DishScroll",
            list,
            out listContent
        );
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(8f, 8f);
        scrollRect.offsetMax = new Vector2(-8f, -76f);
    }

    private void BuildApprovedDetail(RectTransform detail)
    {
        RectTransform content = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DetailCanvas",
            detail,
            Vector2.zero,
            Vector2.one,
            new Vector2(9f, 9f),
            new Vector2(-9f, -9f)
        );
        detailContentRoot = content.gameObject;

        Text heading = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailHeading", content, "Detalle del plato", 18,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(heading.rectTransform, 0f, 0.915f, 0.70f, 1f, 0f);

        Text editing = BistroBuilderMenuEditorUiFactory.CreateText(
            "EditingCaption", content, "Editando plato", 10,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(editing.rectTransform, 0.70f, 0.915f, 1f, 1f, 0f);

        detailTitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DishTitle", content, "Selecciona un plato", 20,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(detailTitleText.rectTransform, 0f, 0.835f, 0.72f, 0.915f, 0f);

        detailStatusBadgeText = CreateDetailReadOnlyValue(
            content, "StatusBadge", "En carta",
            0.76f, 0.852f, 1f, 0.902f, true
        );

        detailSubtitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DishSubtitle", content, string.Empty, 11,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(detailSubtitleText.rectTransform, 0f, 0.785f, 0.74f, 0.842f, 0f);

        RectTransform photoFrame = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DishPhotoFrame", content,
            new Vector2(0f, 0.535f), new Vector2(1f, 0.785f),
            Vector2.zero, Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            photoFrame, new Color32(229, 204, 171, 255)
        );
        RectTransform photo = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DishPhoto", photoFrame, Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f)
        );
        detailDishImage = BistroBuilderMenuEditorUiFactory.AddImage(
            photo, new Color32(241, 224, 201, 255)
        );
        detailDishImage.preserveAspect = false;

        editDishRecipeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "EditDishPhoto", photoFrame, "✎",
            OpenSelectedDishAuthoring,
            new Color32(58, 43, 31, 225), 16
        );
        SetRect(editDishRecipeButton.GetComponent<RectTransform>(),
            0.88f, 0.04f, 0.98f, 0.24f, 0f);

        CreateDetailCaption(content, "PriceCaption", "Precio",
            0.485f, 0.525f);
        priceInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Price", content, "0,00", null, HandlePriceEndEdit
        );
        SetRect(priceInput.GetComponent<RectTransform>(),
            0.36f, 0.475f, 1f, 0.525f, 0f);

        CreateDetailCaption(content, "CategoryCaption", "Categoría",
            0.425f, 0.465f);
        detailCategoryText = CreateDetailReadOnlyValue(
            content, "CategoryValue", "Entrantes",
            0.36f, 0.415f, 1f, 0.465f, false
        );

        CreateDetailCaption(content, "AvailableCaption", "Disponible",
            0.365f, 0.405f);
        enabledToggle = BistroBuilderMenuEditorUiFactory.CreateToggle(
            "AvailableToggle", content, "En carta", HandleEnabledChanged
        );
        SetRect(enabledToggle.GetComponent<RectTransform>(),
            0.36f, 0.355f, 1f, 0.405f, 0f);

        CreateDetailCaption(content, "ModeCaption", "Modalidad",
            0.305f, 0.345f);
        detailModeText = CreateDetailReadOnlyValue(
            content, "ModeValue", "Normal",
            0.36f, 0.295f, 1f, 0.345f, false
        );

        CreateDetailCaption(content, "PrepCaption", "Tiempo de preparación",
            0.245f, 0.285f);
        preparationTimeInput =
            BistroBuilderMenuEditorUiFactory.CreateInputField(
                "PreparationTime", content, "minutos o mm:ss",
                null, HandlePreparationTimeEndEdit
            );
        SetRect(preparationTimeInput.GetComponent<RectTransform>(),
            0.36f, 0.235f, 1f, 0.285f, 0f);

        CreateDetailCaption(content, "OrderCaption", "Posición en la carta",
            0.185f, 0.225f);
        detailOrderText = CreateDetailReadOnlyValue(
            content, "OrderValue", "1",
            0.36f, 0.175f, 0.76f, 0.225f, false
        );
        moveUpButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MoveUp", content, "↑", () => MoveSelected(-1),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 12
        );
        SetRect(moveUpButton.GetComponent<RectTransform>(),
            0.78f, 0.175f, 0.88f, 0.225f, 0f);
        moveDownButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MoveDown", content, "↓", () => MoveSelected(1),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 12
        );
        SetRect(moveDownButton.GetComponent<RectTransform>(),
            0.90f, 0.175f, 1f, 0.225f, 0f);

        CreateDetailCaption(content, "CostCaption", "Coste (por ración)",
            0.125f, 0.165f);
        detailCostText = CreateDetailReadOnlyValue(
            content, "CostValue", "0,00 €",
            0.36f, 0.115f, 1f, 0.165f, false
        );

        CreateDetailCaption(content, "MarginCaption", "Margen",
            0.065f, 0.105f);
        detailMarginText = CreateDetailReadOnlyValue(
            content, "MarginValue", "0 %",
            0.36f, 0.055f, 1f, 0.105f, false
        );

        // Preserve all existing authoring bindings without exposing the
        // obsolete editor controls in the approved compact composition.
        RectTransform hiddenBindings = BistroBuilderMenuEditorUiFactory.CreateRect(
            "HiddenBindings", content, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero
        );
        hiddenBindings.gameObject.SetActive(false);
        descriptionText = AddDetailText(hiddenBindings, string.Empty, 10, 1f, false);
        soldOutToggle = AddDetailToggle(hiddenBindings, "Agotado", HandleSoldOutChanged);
        signatureToggle = AddDetailToggle(hiddenBindings, "Firma", HandleSignatureChanged);
        breakfastToggle = AddDetailToggle(hiddenBindings, "Desayuno", HandleBreakfastChanged);
        lunchToggle = AddDetailToggle(hiddenBindings, "Comida", HandleLunchChanged);
        dinnerToggle = AddDetailToggle(hiddenBindings, "Cena", HandleDinnerChanged);
        preparationDifficultyInput =
            BistroBuilderMenuEditorUiFactory.CreateInputField(
                "PreparationDifficulty", hiddenBindings, "1-10",
                null, HandlePreparationDifficultyEndEdit
            );
        preparationSummaryText = AddDetailText(hiddenBindings, string.Empty, 10, 1f, false);
        restoreDefaultsButton = AddDetailButton(
            hiddenBindings, "Restaurar valores predeterminados",
            RestoreSelectedDefaults, 1f
        );
        economicsText = AddDetailText(hiddenBindings, string.Empty, 10, 1f, false);
        availabilityText = AddDetailText(hiddenBindings, string.Empty, 10, 1f, false);
        recipeText = AddDetailText(hiddenBindings, string.Empty, 10, 1f, false);
    }

    private void CreateDetailCaption(
        Transform parent,
        string name,
        string value,
        float minY,
        float maxY
    )
    {
        Text label = BistroBuilderMenuEditorUiFactory.CreateText(
            name, parent, value, 11,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(label.rectTransform, 0f, minY, 0.34f, maxY, 0f);
    }

    private Text CreateDetailReadOnlyValue(
        Transform parent,
        string name,
        string value,
        float minX,
        float minY,
        float maxX,
        float maxY,
        bool badge
    )
    {
        RectTransform plate = BistroBuilderMenuEditorUiFactory.CreateRect(
            name + "Plate", parent,
            new Vector2(minX, minY), new Vector2(maxX, maxY),
            Vector2.zero, Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            plate,
            badge
                ? new Color32(225, 240, 209, 255)
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            name, plate, value, badge ? 10 : 11,
            badge ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
            badge
                ? new Color32(50, 96, 45, 255)
                : BistroBuilderMenuEditorUiFactory.TextPrimary,
            badge ? FontStyle.Bold : FontStyle.Normal
        );
        text.rectTransform.offsetMin = new Vector2(8f, 0f);
        text.rectTransform.offsetMax = new Vector2(-8f, 0f);
        return text;
    }

    private void BuildDetail(RectTransform detail)
    {
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "DetailScroll",
            detail,
            out RectTransform content
        );
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.offsetMin = new Vector2(6f, 6f);
        scrollRect.offsetMax = new Vector2(-6f, -6f);
        detailContentRoot = content.gameObject;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 14);
        layout.spacing = 8f;

        detailTitleText = AddDetailText(content, "Selecciona un plato", 21, 38f, true);
        detailSubtitleText = AddDetailText(content, string.Empty, 12, 26f, false);
        descriptionText = AddDetailText(content, string.Empty, 14, 64f, false);
        editDishRecipeButton = AddDetailButton(
            content,
            "Editar plato y receta",
            OpenSelectedDishAuthoring,
            38f
        );
        includeButton = AddDetailButton(content, "Añadir a carta", ToggleIncluded, 38f);

        enabledToggle = AddDetailToggle(content, "Activo en la carta", HandleEnabledChanged);
        soldOutToggle = AddDetailToggle(content, "Agotado manualmente", HandleSoldOutChanged);
        signatureToggle = AddDetailToggle(content, "Plato firma", HandleSignatureChanged);

        Text priceLabel = AddDetailText(content, "Precio de venta", 13, 22f, true);
        priceLabel.color = BistroBuilderMenuEditorUiFactory.TextSecondary;
        priceInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Price",
            content,
            "0,00",
            null,
            HandlePriceEndEdit
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(priceInput, 38f);

        Text difficultyLabel = AddDetailText(
            content,
            "Dificultad de preparación (1-10)",
            13,
            22f,
            true
        );
        difficultyLabel.color =
            BistroBuilderMenuEditorUiFactory.TextSecondary;
        preparationDifficultyInput =
            BistroBuilderMenuEditorUiFactory.CreateInputField(
                "PreparationDifficulty",
                content,
                "1-10",
                null,
                HandlePreparationDifficultyEndEdit
            );
        preparationDifficultyInput.contentType =
            InputField.ContentType.IntegerNumber;
        preparationDifficultyInput.characterValidation =
            InputField.CharacterValidation.Integer;
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(
            preparationDifficultyInput,
            38f
        );

        Text preparationTimeLabel = AddDetailText(
            content,
            "Tiempo base de preparación",
            13,
            22f,
            true
        );
        preparationTimeLabel.color =
            BistroBuilderMenuEditorUiFactory.TextSecondary;
        preparationTimeInput =
            BistroBuilderMenuEditorUiFactory.CreateInputField(
                "PreparationTime",
                content,
                "minutos o mm:ss",
                null,
                HandlePreparationTimeEndEdit
            );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(
            preparationTimeInput,
            38f
        );
        preparationSummaryText = AddDetailText(
            content,
            string.Empty,
            12,
            42f,
            false
        );
        preparationSummaryText.color =
            BistroBuilderMenuEditorUiFactory.TextSecondary;

        AddDetailText(content, "Servicios", 13, 22f, true).color =
            BistroBuilderMenuEditorUiFactory.TextSecondary;
        breakfastToggle = AddDetailToggle(content, "Desayuno", HandleBreakfastChanged);
        lunchToggle = AddDetailToggle(content, "Comida", HandleLunchChanged);
        dinnerToggle = AddDetailToggle(content, "Cena", HandleDinnerChanged);

        RectTransform orderRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "OrderActions",
            content,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        HorizontalLayoutGroup orderLayout =
            orderRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        orderLayout.spacing = 6f;
        orderLayout.childControlHeight = true;
        orderLayout.childControlWidth = true;
        orderLayout.childForceExpandHeight = true;
        orderLayout.childForceExpandWidth = true;
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(orderRoot, 38f);
        moveUpButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MoveUp",
            orderRoot,
            "Subir",
            () => MoveSelected(-1),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        moveDownButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MoveDown",
            orderRoot,
            "Bajar",
            () => MoveSelected(1),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );

        restoreDefaultsButton = AddDetailButton(
            content,
            "Restaurar valores predeterminados",
            RestoreSelectedDefaults,
            38f
        );

        economicsText = AddDetailText(content, string.Empty, 14, 82f, false);
        availabilityText = AddDetailText(content, string.Empty, 14, 90f, false);
        recipeText = AddDetailText(content, string.Empty, 13, 170f, false);
    }

    private void BuildApprovedFooter(RectTransform panel)
    {
        RectTransform footer = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Footer",
            panel,
            Vector2.zero,
            new Vector2(1f, 0f),
            new Vector2(12f, 8f),
            new Vector2(-12f, 52f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            footer,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        Button addDish = BistroBuilderMenuEditorUiFactory.CreateButton(
            "AddDish", footer, "Añadir plato", OpenNewDishAuthoring,
            BistroBuilderMenuEditorUiFactory.Positive, 13
        );
        SetRect(addDish.GetComponent<RectTransform>(),
            0f, 0.08f, 0.18f, 0.92f, 0f);

        includeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "RemoveDish", footer, "Eliminar", ToggleIncluded,
            BistroBuilderMenuEditorUiFactory.Negative, 13
        );
        SetRect(includeButton.GetComponent<RectTransform>(),
            0.19f, 0.08f, 0.34f, 0.92f, 0f);

        Button duplicate = BistroBuilderMenuEditorUiFactory.CreateButton(
            "DuplicateDish", footer, "Duplicar", () => { },
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 13
        );
        SetRect(duplicate.GetComponent<RectTransform>(),
            0.35f, 0.08f, 0.48f, 0.92f, 0f);
        duplicate.interactable = false;

        reloadButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Reload", footer, "Recargar", ReloadConflict,
            BistroBuilderMenuEditorUiFactory.Warning, 12
        );
        SetRect(reloadButton.GetComponent<RectTransform>(),
            0.56f, 0.08f, 0.67f, 0.92f, 0f);

        applyButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Apply", footer, "Guardar cambios", ApplyChanges,
            BistroBuilderMenuEditorUiFactory.Positive, 13
        );
        SetRect(applyButton.GetComponent<RectTransform>(),
            0.69f, 0.08f, 0.84f, 0.92f, 0f);

        discardButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Discard", footer, "Descartar", DiscardChanges,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised, 13
        );
        SetRect(discardButton.GetComponent<RectTransform>(),
            0.85f, 0.08f, 1f, 0.92f, 0f);

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            footer,
            string.Empty,
            10,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        statusText.gameObject.SetActive(false);
    }

    private void BuildFooter(RectTransform panel)
    {
        RectTransform footer = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Footer",
            panel,
            Vector2.zero,
            new Vector2(1f, 0f),
            new Vector2(18f, 12f),
            new Vector2(-18f, 66f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            footer,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            footer,
            "Sin cambios pendientes.",
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        statusText.rectTransform.anchorMin = Vector2.zero;
        statusText.rectTransform.anchorMax = new Vector2(0.52f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(14f, 0f);
        statusText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        reloadButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Reload",
            footer,
            "Recargar carta",
            ReloadConflict,
            BistroBuilderMenuEditorUiFactory.Warning,
            13
        );
        SetAnchoredColumn(reloadButton, 0.54f, 0.66f, 8f);

        discardButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Discard",
            footer,
            "Descartar",
            DiscardChanges,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        SetAnchoredColumn(discardButton, 0.68f, 0.80f, 8f);

        applyButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Apply",
            footer,
            "Aplicar cambios",
            ApplyChanges,
            BistroBuilderMenuEditorUiFactory.Positive,
            14
        );
        SetAnchoredColumn(applyButton, 0.81f, 0.985f, 8f);
    }

    private void BuildConfirmation(RectTransform parent)
    {
        confirmationRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "DiscardConfirmation",
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            confirmationRoot,
            BistroBuilderMenuEditorUiFactory.Overlay
        );

        RectTransform card = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Card",
            confirmationRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-250f, -105f),
            new Vector2(250f, 105f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            card,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        Text message = BistroBuilderMenuEditorUiFactory.CreateText(
            "Message",
            card,
            "Hay cambios pendientes. ¿Quieres descartarlos y cerrar?",
            17,
            TextAnchor.MiddleCenter,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        message.rectTransform.anchorMin = new Vector2(0f, 0.42f);
        message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = new Vector2(24f, 0f);
        message.rectTransform.offsetMax = new Vector2(-24f, -12f);

        Button keepButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "KeepEditing",
            card,
            "Seguir editando",
            () => confirmationRoot.gameObject.SetActive(false),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        keepButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.08f, 0.10f);
        keepButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.47f, 0.34f);
        keepButton.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        keepButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        Button discardCloseButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "DiscardAndClose",
            card,
            "Descartar y cerrar",
            ConfirmDiscardAndClose,
            BistroBuilderMenuEditorUiFactory.Negative,
            14
        );
        discardCloseButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.53f, 0.10f);
        discardCloseButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.92f, 0.34f);
        discardCloseButton.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        discardCloseButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        confirmationRoot.gameObject.SetActive(false);
    }

    private void OpenManagementTab()
    {
        if (editorService != null && editorService.HasPendingChanges)
        {
            ShowStatus(
                "Aplica o descarta los cambios antes de volver a Cartas y reglas.",
                true
            );
            return;
        }

        ResolveDependencies();
        if (portfolioView == null)
        {
            ShowStatus("No está disponible la gestión de cartas y reglas.", true);
            return;
        }

        CloseEditor(false);
        if (!portfolioView.TryOpen(out string error))
        {
            TryOpenFromInterface(out _);
            ShowStatus(
                string.IsNullOrWhiteSpace(error)
                    ? "No se pudo abrir Cartas y reglas."
                    : error,
                true
            );
        }
    }

    private void HandleOpenClicked()
    {
        TryOpenFromInterface(out _);
    }

    public void RequestCloseFromInterface()
    {
        RequestClose();
    }

    private void RequestClose()
    {
        if (editorService != null && editorService.HasPendingChanges)
        {
            confirmationRoot.gameObject.SetActive(true);
            confirmationRoot.SetAsLastSibling();
            return;
        }

        CloseEditor(false);
    }

    private void ConfirmDiscardAndClose()
    {
        confirmationRoot.gameObject.SetActive(false);
        CloseEditor(true);
    }

    private void CloseEditor(bool discardPending)
    {
        if (editorService != null &&
            !editorService.TryClose(discardPending, out string error))
        {
            ShowStatus(error, true);
            return;
        }

        SetEditorVisible(false);
        RestoreInputGate();
    }

    private void HandleSearchChanged(string value)
    {
        searchText = value ?? string.Empty;
        RefreshFilteredList();
    }

    private void CycleMealService()
    {
        BistroBuilderMealServiceAvailability next;

        switch (editorService.PreviewMealService)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                next = BistroBuilderMealServiceAvailability.Lunch;
                break;
            case BistroBuilderMealServiceAvailability.Lunch:
                next = BistroBuilderMealServiceAvailability.Dinner;
                break;
            default:
                next = BistroBuilderMealServiceAvailability.Breakfast;
                break;
        }

        if (!editorService.TrySetPreviewContext(
                next,
                editorService.PreviewServiceMode,
                out string error
            ))
        {
            ShowStatus(error, true);
        }
    }

    private void CycleServiceMode()
    {
        BistroBuilderServiceMode next;

        switch (editorService.PreviewServiceMode)
        {
            case BistroBuilderServiceMode.TableService:
                next = BistroBuilderServiceMode.BarService;
                break;
            case BistroBuilderServiceMode.BarService:
                next = BistroBuilderServiceMode.WaitingAtBar;
                break;
            default:
                next = BistroBuilderServiceMode.TableService;
                break;
        }

        if (!editorService.TrySetPreviewContext(
                editorService.PreviewMealService,
                next,
                out string error
            ))
        {
            ShowStatus(error, true);
        }
    }

    private void OpenNewDishAuthoring()
    {
        if (authoringView == null)
        {
            ShowStatus("Falta la vista de creación de platos 2.1G.", true);
            return;
        }

        authoringView.OpenNew();
    }

    private void OpenSelectedDishAuthoring()
    {
        if (authoringView == null || selectedSnapshot == null)
        {
            ShowStatus("Selecciona un plato para editarlo.", true);
            return;
        }

        authoringView.OpenExisting(selectedSnapshot.DishId);
    }

    private void HandleAuthoringSaved(string dishId)
    {
        selectedDishId = dishId ?? string.Empty;
        RefreshSnapshot("Plato y receta guardados en el borrador.");
    }

    private void ToggleIncluded()
    {
        if (selectedSnapshot == null)
        {
            return;
        }

        BistroBuilderMenuMutationResult result = selectedSnapshot.Included
            ? editorService.TryRemoveDish(selectedSnapshot.DishId)
            : editorService.TryAddDish(selectedSnapshot.DishId);
        HandleMutationResult(result);
    }

    private void HandleEnabledChanged(bool value)
    {
        if (suppressCallbacks || selectedSnapshot == null)
        {
            return;
        }

        HandleMutationResult(
            editorService.TrySetEnabled(selectedSnapshot.DishId, value)
        );
    }

    private void HandleSoldOutChanged(bool value)
    {
        if (suppressCallbacks || selectedSnapshot == null)
        {
            return;
        }

        HandleMutationResult(
            editorService.TrySetManuallySoldOut(
                selectedSnapshot.DishId,
                value
            )
        );
    }

    private void HandleSignatureChanged(bool value)
    {
        if (suppressCallbacks || selectedSnapshot == null)
        {
            return;
        }

        HandleMutationResult(
            editorService.TrySetSignatureDish(selectedSnapshot.DishId, value)
        );
    }

    private void HandleBreakfastChanged(bool value)
    {
        SetServiceFlag(BistroBuilderMealServiceAvailability.Breakfast, value);
    }

    private void HandleLunchChanged(bool value)
    {
        SetServiceFlag(BistroBuilderMealServiceAvailability.Lunch, value);
    }

    private void HandleDinnerChanged(bool value)
    {
        SetServiceFlag(BistroBuilderMealServiceAvailability.Dinner, value);
    }

    private void SetServiceFlag(
        BistroBuilderMealServiceAvailability flag,
        bool value
    )
    {
        if (suppressCallbacks || selectedSnapshot == null)
        {
            return;
        }

        BistroBuilderMealServiceAvailability next = value
            ? selectedSnapshot.AvailableServices | flag
            : selectedSnapshot.AvailableServices & ~flag;
        HandleMutationResult(
            editorService.TrySetAvailability(selectedSnapshot.DishId, next)
        );
    }

    private void HandlePriceEndEdit(string value)
    {
        if (suppressCallbacks || selectedSnapshot == null ||
            !selectedSnapshot.Included)
        {
            return;
        }

        if (!BistroBuilderMenuEditorUtility.TryParseMoney(
                value,
                out int cents,
                out string parseError
            ))
        {
            ShowStatus(parseError, true);
            RefreshDetail();
            return;
        }

        HandleMutationResult(
            editorService.TrySetPriceCents(selectedSnapshot.DishId, cents)
        );
    }

    private void HandlePreparationDifficultyEndEdit(string value)
    {
        if (suppressCallbacks || selectedSnapshot == null ||
            !selectedSnapshot.Included)
        {
            return;
        }

        if (!BistroBuilderMenuEditorUtility.TryParsePreparationDifficulty(
                value,
                out int difficulty,
                out string parseError
            ))
        {
            ShowStatus(parseError, true);
            RefreshDetail();
            return;
        }

        HandleMutationResult(
            editorService.TrySetPreparationDifficulty(
                selectedSnapshot.DishId,
                difficulty
            )
        );
    }

    private void HandlePreparationTimeEndEdit(string value)
    {
        if (suppressCallbacks || selectedSnapshot == null ||
            !selectedSnapshot.Included)
        {
            return;
        }

        if (!BistroBuilderMenuEditorUtility.TryParsePreparationDuration(
                value,
                out int preparationSeconds,
                out string parseError
            ))
        {
            ShowStatus(parseError, true);
            RefreshDetail();
            return;
        }

        HandleMutationResult(
            editorService.TrySetBasePreparationSeconds(
                selectedSnapshot.DishId,
                preparationSeconds
            )
        );
    }

    private void MoveSelected(int direction)
    {
        if (selectedSnapshot == null)
        {
            return;
        }

        HandleMutationResult(
            editorService.TryMoveDishWithinCategory(
                selectedSnapshot.DishId,
                direction
            )
        );
    }

    private void RestoreSelectedDefaults()
    {
        if (selectedSnapshot == null)
        {
            return;
        }

        HandleMutationResult(
            editorService.TryRestoreDishDefaults(selectedSnapshot.DishId)
        );
    }

    private void ApplyChanges()
    {
        if (!editorService.TryApplyAndContinue(
                out BistroBuilderMenuEditCommitResult result,
                out string error
            ))
        {
            ShowStatus(error, true);
            RefreshSnapshot(error);
            return;
        }

        string message = result.HadChanges
            ? "Cambios aplicados: " + result.AppliedChangeCount + "."
            : "No había cambios pendientes.";
        ShowStatus(message, false);
    }

    private void DiscardChanges()
    {
        if (!editorService.TryDiscardAndContinue(out string error))
        {
            ShowStatus(error, true);
            return;
        }

        ShowStatus("Cambios descartados.", false);
    }

    private void ReloadConflict()
    {
        if (!editorService.TryReloadAfterConflict(out string error))
        {
            ShowStatus(error, true);
            return;
        }

        ShowStatus("Carta recargada.", false);
    }

    private void HandleMutationResult(BistroBuilderMenuMutationResult result)
    {
        if (!result.Succeeded)
        {
            ShowStatus(result.Message, true);
            RefreshDetail();
            return;
        }

        ShowStatus(result.Message, false);
    }

    private void HandleEditorChanged(BistroBuilderMenuEditorChangedEvent change)
    {
        if (modalRoot == null || !modalRoot.gameObject.activeSelf)
        {
            return;
        }

        RefreshSnapshot(change.Message);
    }

    private void RefreshSnapshot(string message)
    {
        if (editorService == null || !editorService.IsOpen)
        {
            return;
        }

        if (!editorService.TryBuildSnapshot(
                snapshots,
                out summary,
                out string error
            ))
        {
            ShowStatus(error, true);
            return;
        }

        snapshotByDishId.Clear();

        for (int index = 0; index < snapshots.Count; index++)
        {
            BistroBuilderMenuEditorDishSnapshot item = snapshots[index];
            snapshotByDishId[item.DishId] = item;
        }

        if (!string.IsNullOrEmpty(selectedDishId) &&
            !snapshotByDishId.ContainsKey(selectedDishId))
        {
            selectedDishId = string.Empty;
        }

        RebuildCategories();
        RefreshHeader();
        RefreshFilteredList();
        RefreshFooter();

        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowStatus(message, false);
        }
    }

    private void RefreshHeader()
    {
        // Keep Recoleta DEMO restricted to clean ASCII. Dynamic counts and
        // Unicode punctuation are presented in the official Inter caption.
        titleText.text = "CARTA";
        contextText.text = "Define, organiza y personaliza los platos de tu carta.";
        SetButtonLabel(
            mealServiceButton,
            BistroBuilderMenuEditorUtility.GetMealServiceLabel(
                summary.MealService
            )
        );
        SetButtonLabel(
            serviceModeButton,
            BistroBuilderMenuEditorUtility.GetServiceModeLabel(
                summary.ServiceMode
            )
        );
    }

    private void RebuildCategories()
    {
        categoryIdSet.Clear();
        categoryIdBuffer.Clear();
        categoryNameBuffer.Clear();

        for (int index = 0; index < snapshots.Count; index++)
        {
            BistroBuilderMenuEditorDishSnapshot item = snapshots[index];

            if (categoryIdSet.Add(item.CategoryId))
            {
                categoryIdBuffer.Add(item.CategoryId);
                categoryNameBuffer.Add(item.CategoryName);
            }
        }

        bool hierarchyChanged =
            categoryButtonIds.Count != categoryIdBuffer.Count + 2;

        if (!hierarchyChanged)
        {
            for (int index = 0; index < categoryIdBuffer.Count; index++)
            {
                if (!string.Equals(
                        categoryButtonIds[index + 1],
                        categoryIdBuffer[index],
                        StringComparison.Ordinal
                    ))
                {
                    hierarchyChanged = true;
                    break;
                }
            }
        }

        if (hierarchyChanged)
        {
            for (int index = 0; index < categoryButtons.Count; index++)
            {
                Destroy(categoryButtons[index].gameObject);
            }

            categoryButtons.Clear();
            categoryButtonIds.Clear();
            CreateCategoryButton(string.Empty, "Todas");

            for (int index = 0; index < categoryIdBuffer.Count; index++)
            {
                CreateCategoryButton(
                    categoryIdBuffer[index],
                    categoryNameBuffer[index]
                );
            }
            CreateCategoryButton(SpecialCategoryId, "Especiales");
        }

        if (!string.IsNullOrEmpty(selectedCategoryId) &&
            selectedCategoryId != SpecialCategoryId &&
            !categoryIdSet.Contains(selectedCategoryId))
        {
            selectedCategoryId = string.Empty;
        }

        RefreshCategorySelection();
    }

    private void CreateCategoryButton(string categoryId, string label)
    {
        string capturedId = categoryId;
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Category_" + (string.IsNullOrEmpty(categoryId) ? "all" : categoryId),
            categoryContent,
            label,
            () => SelectCategory(capturedId),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 42f);
        categoryButtons.Add(button);
        categoryButtonIds.Add(categoryId);
    }

    private void SelectCategory(string categoryId)
    {
        selectedCategoryId = categoryId ?? string.Empty;
        RefreshCategorySelection();
        RefreshFilteredList();
    }

    private void RefreshCategorySelection()
    {
        for (int index = 0; index < categoryButtons.Count; index++)
        {
            bool selected = string.Equals(
                selectedCategoryId,
                categoryButtonIds[index],
                StringComparison.Ordinal
            );
            BistroBuilderCartaReferenceV3Style.StyleSelectorButton(
                categoryButtons[index],
                selected
            );
        }
    }

    private void CreateFilterButton(
        Transform parent,
        string label,
        BistroBuilderMenuEditorFilter value
    )
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Filter_" + value,
            parent,
            label,
            () =>
            {
                filter = value;
                RefreshFilterSelection();
                RefreshFilteredList();
            },
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 32f);
        filterButtons.Add(button);
        filterValues.Add(value);
        RefreshFilterSelection();
    }

    private void RefreshFilterSelection()
    {
        for (int index = 0; index < filterButtons.Count; index++)
        {
            BistroBuilderCartaReferenceV3Style.StyleSelectorButton(
                filterButtons[index],
                filterValues[index] == filter
            );
        }
    }

    private void RefreshFilteredList()
    {
        filtered.Clear();

        for (int index = 0; index < snapshots.Count; index++)
        {
            BistroBuilderMenuEditorDishSnapshot item = snapshots[index];

            bool matches = selectedCategoryId == SpecialCategoryId
                ? item.SignatureDish &&
                  BistroBuilderMenuEditorUtility.Matches(
                      item, string.Empty, filter, searchText)
                : BistroBuilderMenuEditorUtility.Matches(
                      item, selectedCategoryId, filter, searchText);

            if (matches)
            {
                filtered.Add(item);
            }
        }

        EnsureRowPool(filtered.Count);

        for (int index = 0; index < rowPool.Count; index++)
        {
            bool active = index < filtered.Count;
            rowPool[index].gameObject.SetActive(active);

            if (active)
            {
                BistroBuilderMenuEditorDishSnapshot item = filtered[index];
                rowPool[index].Bind(
                    item,
                    string.Equals(
                        selectedDishId,
                        item.DishId,
                        StringComparison.Ordinal
                    ),
                    SelectDish
                );
            }
        }

        listCountText.text = filtered.Count + " de " + snapshots.Count;

        if (string.IsNullOrEmpty(selectedDishId) && filtered.Count > 0)
        {
            selectedDishId = filtered[0].DishId;
        }

        RefreshDetail();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    private void EnsureRowPool(int count)
    {
        while (rowPool.Count < count)
        {
            RectTransform row = BistroBuilderMenuEditorUiFactory.CreateRect(
                "DishRow_" + rowPool.Count,
                listContent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero
            );
            BistroBuilderMenuEditorUiFactory.AddImage(
                row,
                BistroBuilderMenuEditorUiFactory.SurfaceRaised
            );
            BistroBuilderMenuEditorDishRowView view =
                row.gameObject.AddComponent<
                    BistroBuilderMenuEditorDishRowView
                >();
            view.Initialize();
            rowPool.Add(view);
        }
    }

    private void SelectDish(string dishId)
    {
        selectedDishId = dishId ?? string.Empty;
        RefreshFilteredList();
    }

    private void RefreshDetail()
    {
        selectedSnapshot = null;

        if (!string.IsNullOrEmpty(selectedDishId))
        {
            snapshotByDishId.TryGetValue(
                selectedDishId,
                out selectedSnapshot
            );
        }

        bool hasSelection = selectedSnapshot != null;
        detailContentRoot.SetActive(hasSelection);

        if (!hasSelection)
        {
            return;
        }

        suppressCallbacks = true;

        try
        {
            detailTitleText.text = selectedSnapshot.DisplayName;
            detailSubtitleText.text = string.IsNullOrWhiteSpace(
                selectedSnapshot.Description)
                ? selectedSnapshot.CategoryName
                : ShortDetailSubtitle(selectedSnapshot.Description);
            descriptionText.text = selectedSnapshot.Description ?? string.Empty;

            if (dishImageCatalog == null)
                dishImageCatalog = BistroBuilderDishImageCatalog.LoadDefault();
            Sprite dishSprite = dishImageCatalog != null
                ? dishImageCatalog.GetImageOrNull(selectedSnapshot.DishId)
                : null;
            if (detailDishImage != null)
            {
                detailDishImage.sprite = dishSprite;
                detailDishImage.color = dishSprite != null
                    ? Color.white
                    : new Color32(241, 224, 201, 255);
            }

            if (detailStatusBadgeText != null)
            {
                detailStatusBadgeText.text = selectedSnapshot.Included
                    ? "En carta"
                    : "Fuera";
                detailStatusBadgeText.color = selectedSnapshot.Included
                    ? new Color32(50, 96, 45, 255)
                    : BistroBuilderMenuEditorUiFactory.TextSecondary;
            }
            if (detailCategoryText != null)
                detailCategoryText.text = selectedSnapshot.CategoryName;
            if (detailModeText != null)
                detailModeText.text = ResolveApprovedModality(selectedSnapshot);
            if (detailOrderText != null)
                detailOrderText.text = (selectedSnapshot.DisplayOrder + 1).ToString();
            if (detailCostText != null)
                detailCostText.text = BistroBuilderMenuEditorUtility.FormatMoney(
                    selectedSnapshot.CostPerPortionCents);
            if (detailMarginText != null)
            {
                detailMarginText.text = selectedSnapshot.HasValidEconomics
                    ? (selectedSnapshot.GrossMarginBasisPoints / 100f)
                        .ToString("0") + " %"
                    : "—";
                detailMarginText.color = selectedSnapshot.GrossMarginCents < 0
                    ? BistroBuilderMenuEditorUiFactory.Negative
                    : new Color32(58, 132, 54, 255);
            }
            SetButtonLabel(
                includeButton,
                selectedSnapshot.Included
                    ? "Eliminar"
                    : "Añadir a carta"
            );
            BistroBuilderCartaReferenceV3Style.StyleEditorInclusionButton(
                includeButton,
                selectedSnapshot.Included
            );

            bool editable = selectedSnapshot.Included;
            editDishRecipeButton.interactable = true;
            enabledToggle.interactable = editable;
            soldOutToggle.interactable = editable;
            breakfastToggle.interactable = editable;
            lunchToggle.interactable = editable;
            dinnerToggle.interactable = editable;
            priceInput.interactable = editable;
            preparationDifficultyInput.interactable = editable;
            preparationTimeInput.interactable = editable;
            moveUpButton.interactable = editable;
            moveDownButton.interactable = editable;
            restoreDefaultsButton.interactable = editable;
            signatureToggle.interactable = editable &&
                (selectedSnapshot.SignatureDish ||
                 selectedSnapshot.Unlocked &&
                 selectedSnapshot.Enabled &&
                 selectedSnapshot.AvailableServices !=
                    BistroBuilderMealServiceAvailability.None);

            enabledToggle.SetIsOnWithoutNotify(selectedSnapshot.Enabled);
            soldOutToggle.SetIsOnWithoutNotify(
                selectedSnapshot.ManuallySoldOut
            );
            signatureToggle.SetIsOnWithoutNotify(
                selectedSnapshot.SignatureDish
            );
            breakfastToggle.SetIsOnWithoutNotify(
                (selectedSnapshot.AvailableServices &
                 BistroBuilderMealServiceAvailability.Breakfast) != 0
            );
            lunchToggle.SetIsOnWithoutNotify(
                (selectedSnapshot.AvailableServices &
                 BistroBuilderMealServiceAvailability.Lunch) != 0
            );
            dinnerToggle.SetIsOnWithoutNotify(
                (selectedSnapshot.AvailableServices &
                 BistroBuilderMealServiceAvailability.Dinner) != 0
            );
            priceInput.SetTextWithoutNotify(
                BistroBuilderMenuEditorUtility.FormatEditableMoney(
                    selectedSnapshot.CurrentPriceCents
                )
            );
            preparationDifficultyInput.SetTextWithoutNotify(
                selectedSnapshot.PreparationDifficulty.ToString()
            );
            preparationTimeInput.SetTextWithoutNotify(
                BistroBuilderMenuEditorUtility.FormatPreparationDuration(
                    selectedSnapshot.PreparationSeconds
                )
            );
            preparationSummaryText.text =
                BistroBuilderMenuEditorUtility.GetPreparationDifficultyLabel(
                    selectedSnapshot.PreparationDifficulty
                ) + " · " +
                selectedSnapshot.PreparationDifficulty + "/10 · " +
                BistroBuilderMenuEditorUtility.FormatPreparationDuration(
                    selectedSnapshot.PreparationSeconds
                ) +
                (selectedSnapshot.PreparationDifficulty !=
                    selectedSnapshot.DefaultPreparationDifficulty ||
                 selectedSnapshot.PreparationSeconds !=
                    selectedSnapshot.DefaultPreparationSeconds
                    ? " · Modificado"
                    : " · Predeterminado");

            economicsText.text = selectedSnapshot.HasValidEconomics
                ? "Escandallo por ración\n" +
                  "Coste: " +
                  BistroBuilderMenuEditorUtility.FormatMoney(
                      selectedSnapshot.CostPerPortionCents
                  ) + "\n" +
                  "Margen bruto: " +
                  BistroBuilderMenuEditorUtility.FormatMoney(
                      selectedSnapshot.GrossMarginCents
                  ) + " (" +
                  (selectedSnapshot.GrossMarginBasisPoints / 100f)
                      .ToString("0.0") + " %)"
                : "Escandallo no disponible.";
            economicsText.color = selectedSnapshot.GrossMarginCents < 0
                ? BistroBuilderMenuEditorUiFactory.Negative
                : BistroBuilderMenuEditorUiFactory.TextPrimary;

            availabilityText.text =
                "Disponibilidad · " +
                BistroBuilderMenuEditorUtility.GetMealServiceLabel(
                    summary.MealService
                ) + " · " +
                BistroBuilderMenuEditorUtility.GetServiceModeLabel(
                    summary.ServiceMode
                ) + "\n" +
                selectedSnapshot.AvailabilityMessage +
                (selectedSnapshot.IsOrderable
                    ? "\nRaciones posibles: " +
                      selectedSnapshot.AvailablePortions
                    : string.Empty);
            availabilityText.color = selectedSnapshot.IsOrderable
                ? BistroBuilderMenuEditorUiFactory.Positive
                : BistroBuilderMenuEditorUiFactory.Warning;
            recipeText.text = "Receta\n" + selectedSnapshot.RecipeSummary;
        }
        finally
        {
            suppressCallbacks = false;
        }
    }

    private static string ShortDetailSubtitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim();
        return trimmed.Length <= 42
            ? trimmed
            : trimmed.Substring(0, 39) + "…";
    }

    private static string ResolveApprovedModality(
        BistroBuilderMenuEditorDishSnapshot snapshot)
    {
        if (snapshot == null) return "Normal";
        string category = (snapshot.CategoryName ?? string.Empty)
            .ToLowerInvariant();
        if (category.Contains("postre")) return "Postre";
        if (category.Contains("bebida")) return "Bebida";
        return "Normal";
    }

    private void RefreshFooter()
    {
        bool conflict = summary.HasExternalConflict;
        reloadButton.gameObject.SetActive(conflict);
        applyButton.interactable = !conflict &&
            summary.DraftChangeCount > 0;
        discardButton.interactable = summary.DraftChangeCount > 0 || conflict;

        if (conflict)
        {
            ShowStatus(
                "La carta cambió fuera del editor. Recarga antes de aplicar.",
                true
            );
        }
        else if (summary.DraftChangeCount > 0)
        {
            ShowStatus(
                summary.DraftChangeCount + " cambio(s) pendiente(s).",
                false
            );
        }
        else if (!summary.InventoryReady)
        {
            ShowStatus(summary.InventoryStatus, true);
        }
    }

    private void ShowStatus(string message, bool warning)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = string.IsNullOrWhiteSpace(message)
            ? "Sin cambios pendientes."
            : message;
        statusText.color = warning
            ? BistroBuilderMenuEditorUiFactory.Warning
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private void SetEditorVisible(bool visible)
    {
        if (!visible && authoringView != null && authoringView.IsOpen)
        {
            authoringView.Close();
        }

        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);
            if (visible) BistroBuilderManagementSafeArea.Install(modalRoot);

            if (visible)
            {
                modalRoot.SetAsLastSibling();
            }
        }

        if (openButton != null)
        {
            openButton.gameObject.SetActive(showOpenButton && !visible);
        }
    }

    private void ApplyInputGate()
    {
        if (inputGateApplied)
        {
            return;
        }

        if (cameraController != null)
        {
            cameraWasEnabled = cameraController.enabled;
            cameraController.enabled = false;
        }

        if (editInteractionController != null)
        {
            editInteractionWasEnabled = editInteractionController.enabled;
            editInteractionController.enabled = false;
        }

        inputGateApplied = true;
    }

    private void RestoreInputGate()
    {
        if (!inputGateApplied)
        {
            return;
        }

        if (cameraController != null)
        {
            cameraController.enabled = cameraWasEnabled;
        }

        if (editInteractionController != null)
        {
            editInteractionController.enabled = editInteractionWasEnabled;
        }

        inputGateApplied = false;
    }

    private void Subscribe()
    {
        if (subscribed || editorService == null)
        {
            return;
        }

        editorService.EditorChanged += HandleEditorChanged;

        if (authoringView != null)
        {
            authoringView.Saved += HandleAuthoringSaved;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (editorService != null)
        {
            editorService.EditorChanged -= HandleEditorChanged;
        }

        if (authoringView != null)
        {
            authoringView.Saved -= HandleAuthoringSaved;
        }

        subscribed = false;
    }

    private void ResolveDependencies()
    {
        if (editorService == null)
        {
            editorService = FindFirstObjectByType<
                BistroBuilderMenuEditorService
            >();
        }

        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<
                BistroBuilderProfessionalCameraController
            >();
        }

        if (editInteractionController == null)
        {
            editInteractionController = FindFirstObjectByType<
                RestaurantEditInteractionController
            >();
        }

        if (authoringView == null)
        {
            TryGetComponent(out authoringView);
        }

        if (portfolioView == null)
        {
            TryGetComponent(out portfolioView);
        }

        if (dishImageCatalog == null)
        {
            dishImageCatalog = BistroBuilderDishImageCatalog.LoadDefault();
        }
    }

    private Text AddDetailText(
        Transform parent,
        string value,
        int fontSize,
        float height,
        bool bold
    )
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailText",
            parent,
            value,
            fontSize,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            bold ? FontStyle.Bold : FontStyle.Normal
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(text, height);
        return text;
    }

    private Button AddDetailButton(
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction callback,
        float height
    )
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            "DetailButton",
            parent,
            label,
            callback,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, height);
        return button;
    }

    private Toggle AddDetailToggle(
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction<bool> callback
    )
    {
        Toggle toggle = BistroBuilderMenuEditorUiFactory.CreateToggle(
            "DetailToggle",
            parent,
            label,
            callback
        );
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(toggle, 30f);
        return toggle;
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        float inset
    )
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetAnchoredColumn(
        Button button,
        float minX,
        float maxX,
        float verticalMargin
    )
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.offsetMin = new Vector2(0f, verticalMargin);
        rect.offsetMax = new Vector2(0f, -verticalMargin);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);

        if (text != null)
        {
            BistroBuilderMenuEditorUiFactory.SetButtonDisplay(text, label);
        }
    }
}
