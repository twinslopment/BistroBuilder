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

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderMenuEditorService editorService;

    [SerializeField]
    private BistroBuilderProfessionalCameraController cameraController;

    [SerializeField]
    private RestaurantEditInteractionController editInteractionController;

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

    public bool VisualTreeBuilt => visualTreeBuilt;

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

            if (scrollRects.Length < 3)
            {
                error = "La vista 2.1E no contiene los tres scrolls esperados.";
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
        BistroBuilderMenuEditorUiFactory.AddImage(
            modalRoot,
            BistroBuilderMenuEditorUiFactory.Overlay
        );

        RectTransform panel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Panel",
            modalRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(34f, 28f),
            new Vector2(-34f, -28f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        BuildHeader(panel);
        BuildBody(panel);
        BuildFooter(panel);
        BuildConfirmation(modalRoot);
        visualTreeBuilt = true;
    }

    private void BuildHeader(RectTransform panel)
    {
        RectTransform header = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Header",
            panel,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(18f, -70f),
            new Vector2(-18f, -12f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            header,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        titleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            header,
            "Carta y platos",
            24,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
        titleText.rectTransform.anchorMax = new Vector2(0.32f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(16f, 0f);
        titleText.rectTransform.offsetMax = Vector2.zero;

        contextText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Context",
            header,
            string.Empty,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        contextText.rectTransform.anchorMin = new Vector2(0.32f, 0f);
        contextText.rectTransform.anchorMax = new Vector2(0.53f, 1f);
        contextText.rectTransform.offsetMin = new Vector2(8f, 0f);
        contextText.rectTransform.offsetMax = new Vector2(-8f, 0f);

        mealServiceButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "MealService",
            header,
            "Comida",
            CycleMealService,
            new Color(0.16f, 0.20f, 0.18f, 1f),
            14
        );
        SetAnchoredColumn(mealServiceButton, 0.54f, 0.67f, 9f);

        serviceModeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "ServiceMode",
            header,
            "Mesa",
            CycleServiceMode,
            new Color(0.16f, 0.20f, 0.18f, 1f),
            14
        );
        SetAnchoredColumn(serviceModeButton, 0.68f, 0.83f, 9f);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            header,
            "Cerrar",
            RequestClose,
            new Color(0.25f, 0.15f, 0.14f, 1f),
            14
        );
        SetAnchoredColumn(closeButton, 0.88f, 0.985f, 9f);
    }

    private void BuildBody(RectTransform panel)
    {
        RectTransform body = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Body",
            panel,
            Vector2.zero,
            Vector2.one,
            new Vector2(18f, 78f),
            new Vector2(-18f, -82f)
        );

        RectTransform sidebar = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Sidebar",
            body,
            Vector2.zero,
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(226f, 0f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            sidebar,
            new Color(0.07f, 0.078f, 0.073f, 1f)
        );
        BuildSidebar(sidebar);

        RectTransform detail = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Detail",
            body,
            new Vector2(1f, 0f),
            Vector2.one,
            new Vector2(-382f, 0f),
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            detail,
            new Color(0.07f, 0.078f, 0.073f, 1f)
        );
        BuildDetail(detail);

        RectTransform list = BistroBuilderMenuEditorUiFactory.CreateRect(
            "List",
            body,
            Vector2.zero,
            Vector2.one,
            new Vector2(238f, 0f),
            new Vector2(-394f, 0f)
        );
        BuildList(list);
    }

    private void BuildSidebar(RectTransform sidebar)
    {
        searchInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Search",
            sidebar,
            "Buscar plato…",
            HandleSearchChanged,
            null
        );
        RectTransform searchRect = searchInput.GetComponent<RectTransform>();
        searchRect.anchorMin = new Vector2(0f, 1f);
        searchRect.anchorMax = new Vector2(1f, 1f);
        searchRect.offsetMin = new Vector2(10f, -48f);
        searchRect.offsetMax = new Vector2(-10f, -10f);

        Text categoryLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "CategoryLabel",
            sidebar,
            "CATEGORÍAS",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        categoryLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        categoryLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        categoryLabel.rectTransform.offsetMin = new Vector2(12f, -82f);
        categoryLabel.rectTransform.offsetMax = new Vector2(-10f, -56f);

        ScrollRect categories = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "Categories",
            sidebar,
            out categoryContent
        );
        RectTransform categoriesRect = categories.GetComponent<RectTransform>();
        categoriesRect.anchorMin = new Vector2(0f, 0.42f);
        categoriesRect.anchorMax = new Vector2(1f, 1f);
        categoriesRect.offsetMin = new Vector2(8f, 6f);
        categoriesRect.offsetMax = new Vector2(-8f, -84f);

        Text filterLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "FilterLabel",
            sidebar,
            "FILTROS",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        filterLabel.rectTransform.anchorMin = new Vector2(0f, 0.38f);
        filterLabel.rectTransform.anchorMax = new Vector2(1f, 0.42f);
        filterLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
        filterLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

        RectTransform filterRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Filters",
            sidebar,
            Vector2.zero,
            new Vector2(1f, 0.38f),
            new Vector2(8f, 8f),
            new Vector2(-8f, -4f)
        );
        VerticalLayoutGroup layout =
            filterRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        CreateFilterButton(filterRoot, "Todos", BistroBuilderMenuEditorFilter.All);
        CreateFilterButton(filterRoot, "En carta", BistroBuilderMenuEditorFilter.Included);
        CreateFilterButton(filterRoot, "Activos", BistroBuilderMenuEditorFilter.Active);
        CreateFilterButton(filterRoot, "Platos firma", BistroBuilderMenuEditorFilter.Signature);
        CreateFilterButton(filterRoot, "Requieren atención", BistroBuilderMenuEditorFilter.NeedsAttention);
    }

    private void BuildList(RectTransform list)
    {
        RectTransform listHeader = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ListHeader",
            list,
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(0f, -42f),
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            listHeader,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        Text columns = BistroBuilderMenuEditorUiFactory.CreateText(
            "Columns",
            listHeader,
            "PLATO                         CATEGORÍA      PRECIO      MARGEN      SERVICIOS      ESTADO",
            11,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary,
            FontStyle.Bold
        );
        columns.rectTransform.offsetMin = new Vector2(10f, 0f);
        columns.rectTransform.offsetMax = new Vector2(-120f, 0f);

        listCountText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Count",
            listHeader,
            "0 platos",
            12,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        listCountText.rectTransform.anchorMin = new Vector2(0.78f, 0f);
        listCountText.rectTransform.anchorMax = Vector2.one;
        listCountText.rectTransform.offsetMin = Vector2.zero;
        listCountText.rectTransform.offsetMax = new Vector2(-10f, 0f);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "DishScroll",
            list,
            out listContent
        );
        RectTransform scrollRect = scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = new Vector2(0f, -48f);
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
            new Color(0.22f, 0.24f, 0.22f, 1f),
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
            new Color(0f, 0f, 0f, 0.72f)
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
            new Color(0.22f, 0.24f, 0.22f, 1f),
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

    private void HandleOpenClicked()
    {
        TryOpenFromInterface(out _);
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
        titleText.text = "Carta y platos · " +
            summary.IncludedDishCount + "/" + summary.CatalogDishCount;
        contextText.text = "Restaurante: " + summary.RestaurantId;
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
            categoryButtonIds.Count != categoryIdBuffer.Count + 1;

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
        }

        if (!string.IsNullOrEmpty(selectedCategoryId) &&
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
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 34f);
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
            Image image = categoryButtons[index].targetGraphic as Image;

            if (image != null)
            {
                image.color = string.Equals(
                    selectedCategoryId,
                    categoryButtonIds[index],
                    StringComparison.Ordinal
                )
                    ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                    : BistroBuilderMenuEditorUiFactory.SurfaceRaised;
            }
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
            Image image = filterButtons[index].targetGraphic as Image;

            if (image != null)
            {
                image.color = filterValues[index] == filter
                    ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                    : BistroBuilderMenuEditorUiFactory.SurfaceRaised;
            }
        }
    }

    private void RefreshFilteredList()
    {
        filtered.Clear();

        for (int index = 0; index < snapshots.Count; index++)
        {
            BistroBuilderMenuEditorDishSnapshot item = snapshots[index];

            if (BistroBuilderMenuEditorUtility.Matches(
                    item,
                    selectedCategoryId,
                    filter,
                    searchText
                ))
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
            detailSubtitleText.text =
                selectedSnapshot.CategoryName + " · " +
                selectedSnapshot.Course + " · " +
                selectedSnapshot.RequiredStation;
            descriptionText.text = string.IsNullOrWhiteSpace(
                selectedSnapshot.Description
            )
                ? "Sin descripción."
                : selectedSnapshot.Description;
            SetButtonLabel(
                includeButton,
                selectedSnapshot.Included
                    ? "Retirar de la carta"
                    : "Añadir a la carta"
            );
            Image includeImage = includeButton.targetGraphic as Image;

            if (includeImage != null)
            {
                includeImage.color = selectedSnapshot.Included
                    ? new Color(0.34f, 0.18f, 0.16f, 1f)
                    : BistroBuilderMenuEditorUiFactory.Positive;
            }

            bool editable = selectedSnapshot.Included;
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
        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);

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
            text.text = label ?? string.Empty;
        }
    }
}
