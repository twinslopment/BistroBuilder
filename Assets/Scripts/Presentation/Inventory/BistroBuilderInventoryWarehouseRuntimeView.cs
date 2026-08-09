using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI jugable definitiva funcional de Inventario/Almacén 2.2D.
///
/// Es estrictamente Presentation: consulta y envía comandos a
/// BistroBuilderInventoryWarehouseService. No modifica stock, lotes, mínimos
/// ni disponibilidad directamente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Inventory Warehouse Runtime View 2.2D")]
public sealed class BistroBuilderInventoryWarehouseRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "INVENTORY-2.2D-UI";

    [Header("Dependencias")]
    [SerializeField]
    private BistroBuilderInventoryWarehouseService warehouseService;

    [SerializeField]
    private BistroBuilderProfessionalCameraController cameraController;

    [SerializeField]
    private RestaurantEditInteractionController editInteractionController;

    [Header("Comportamiento")]
    [SerializeField]
    private bool showOpenButton = true;

    [SerializeField]
    [Min(20)]
    private int maximumMovementRows = 160;

    [SerializeField]
    [Min(10)]
    private int maximumReceiptRows = 80;

    private readonly List<BistroBuilderInventoryWarehouseIngredientSnapshot>
        ingredients =
            new List<BistroBuilderInventoryWarehouseIngredientSnapshot>(64);
    private readonly List<BistroBuilderInventoryAlertSnapshot> alerts =
        new List<BistroBuilderInventoryAlertSnapshot>(64);
    private readonly List<BistroBuilderInventoryWarehouseMovementSnapshot>
        movements =
            new List<BistroBuilderInventoryWarehouseMovementSnapshot>(192);
    private readonly List<BistroBuilderInventoryWarehouseReceiptSnapshot>
        receipts =
            new List<BistroBuilderInventoryWarehouseReceiptSnapshot>(96);
    private readonly List<Button> rowPool = new List<Button>(96);

    private Button openButton;
    private RectTransform modalRoot;
    private RectTransform mainListContent;
    private RectTransform mainListViewport;
    private RectTransform detailRoot;
    private RectTransform actionRoot;
    private Text titleText;
    private Text summaryText;
    private Text statusText;
    private Text detailTitleText;
    private Text detailBodyText;
    private Text minimumCurrentText;
    private Text adjustmentUnitText;
    private InputField searchInput;
    private InputField adjustmentInput;
    private InputField adjustmentNoteInput;
    private Button filterButton;
    private Button sortButton;
    private Button reasonButton;
    private Button stockTabButton;
    private Button alertsTabButton;
    private Button movementsTabButton;
    private Button receiptsTabButton;
    private Button minimumEditButton;

    // Editor modal independiente de stock mínimo. Evita depender del foco de
    // un InputField embebido en el panel durante un servicio con refrescos.
    // El valor puede introducirse íntegramente mediante botones, por lo que
    // siempre existe una ruta de interacción fiable incluso si el teclado no
    // entrega foco a uGUI en una configuración concreta.
    private RectTransform minimumEditorOverlay;
    private Text minimumEditorTitleText;
    private Text minimumEditorValueText;
    private Text minimumEditorUnitText;
    private string minimumEditorDraft = "0";
    private string minimumEditorIngredientId = string.Empty;

    private string selectedIngredientId = string.Empty;

    private BistroBuilderInventoryWarehouseFilter currentFilter =
        BistroBuilderInventoryWarehouseFilter.All;
    private BistroBuilderInventoryWarehouseSort currentSort =
        BistroBuilderInventoryWarehouseSort.Name;
    private BistroBuilderInventoryWarehouseSection currentSection =
        BistroBuilderInventoryWarehouseSection.Stock;
    private BistroBuilderInventoryManualAdjustmentReason currentReason =
        BistroBuilderInventoryManualAdjustmentReason.InventoryCorrection;

    private bool built;
    private bool subscribed;
    private bool suppressCallbacks;
    private bool refreshQueued;
    private Coroutine refreshRoutine;
    private bool cameraWasEnabled;
    private bool editInteractionWasEnabled;
    private bool inputGateApplied;
    private int subscriptionGeneration;

    public BistroBuilderInventoryWarehouseService WarehouseService =>
        warehouseService;
    public bool VisualTreeBuilt => built;
    public bool IsOpen => modalRoot != null && modalRoot.gameObject.activeSelf;
    public int VisibleRowCount { get; private set; }
    public int RowPoolCount => rowPool.Count;
    public int SubscriptionGeneration => subscriptionGeneration;
    public BistroBuilderInventoryWarehouseSection CurrentSection =>
        currentSection;
    public BistroBuilderInventoryWarehouseFilter CurrentFilter =>
        currentFilter;
    public BistroBuilderInventoryWarehouseSort CurrentSort => currentSort;
    public string SelectedIngredientId => selectedIngredientId;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVisualTree();
        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
    }

    private void Start()
    {
        EnsureVisualTree();
        if (openButton != null)
        {
            openButton.gameObject.SetActive(showOpenButton);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelQueuedRefresh();
        RestoreInputGate();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        ResolveDependencies();
        if (warehouseService == null)
        {
            error = "Falta BistroBuilderInventoryWarehouseService 2.2D.";
            return false;
        }

        return warehouseService.ValidateConfiguration(out error);
    }

    public bool TryOpenFromInterface(out string error)
    {
        error = string.Empty;
        EnsureVisualTree();
        ResolveDependencies();
        if (warehouseService == null ||
            !warehouseService.EnsureReady(out error))
        {
            return false;
        }

        CloseMinimumEditor();
        SetVisible(true);
        ApplyInputGate();
        return Refresh("Inventario actualizado.", out error);
    }

    public void Close()
    {
        CloseMinimumEditor();
        SetVisible(false);
        RestoreInputGate();
    }

    public bool TrySelectIngredientForTest(
        string ingredientId,
        out string error
    )
    {
        error = string.Empty;
        if (!warehouseService.TryGetIngredient(
                ingredientId,
                out BistroBuilderInventoryWarehouseIngredientSnapshot snapshot,
                out error
            ))
        {
            return false;
        }

        if (!string.Equals(
                selectedIngredientId,
                snapshot.IngredientId,
                StringComparison.Ordinal
            ))
        {
            CloseMinimumEditor();
        }
        selectedIngredientId = snapshot.IngredientId;
        currentSection = BistroBuilderInventoryWarehouseSection.Stock;
        return Refresh(string.Empty, out error);
    }

    public bool TrySetFilterForTest(
        BistroBuilderInventoryWarehouseFilter filter,
        out string error
    )
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderInventoryWarehouseFilter), filter))
        {
            error = "Filtro desconocido.";
            return false;
        }
        currentFilter = filter;
        return Refresh(string.Empty, out error);
    }

    public bool TrySetSortForTest(
        BistroBuilderInventoryWarehouseSort sort,
        out string error
    )
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderInventoryWarehouseSort), sort))
        {
            error = "Ordenación desconocida.";
            return false;
        }
        currentSort = sort;
        return Refresh(string.Empty, out error);
    }

    public bool TrySetSectionForTest(
        BistroBuilderInventoryWarehouseSection section,
        out string error
    )
    {
        error = string.Empty;
        if (!Enum.IsDefined(typeof(BistroBuilderInventoryWarehouseSection), section))
        {
            error = "Sección desconocida.";
            return false;
        }
        currentSection = section;
        return Refresh(string.Empty, out error);
    }

    public bool TryValidateVisibleContent(out string error)
    {
        error = string.Empty;
        bool wasOpen = IsOpen;
        if (!TryOpenFromInterface(out error))
        {
            return false;
        }

        try
        {
            Canvas.ForceUpdateCanvases();
            if (mainListViewport == null ||
                mainListViewport.GetComponent<RectMask2D>() == null)
            {
                error = "El listado 2.2D no utiliza RectMask2D.";
                return false;
            }

            if (mainListViewport.GetComponent<Mask>() != null)
            {
                error = "El viewport 2.2D conserva un Mask clásico no permitido.";
                return false;
            }

            if (summaryText == null || searchInput == null ||
                filterButton == null || sortButton == null ||
                detailBodyText == null || minimumEditButton == null ||
                minimumEditorOverlay == null || minimumEditorValueText == null ||
                adjustmentInput == null || adjustmentNoteInput == null)
            {
                error = "La UI 2.2D no contiene todos sus controles esenciales.";
                return false;
            }

            if (ingredients.Count == 0 || VisibleRowCount == 0)
            {
                error = "La UI 2.2D no muestra ingredientes del inventario real.";
                return false;
            }

            if (subscribed && subscriptionGeneration < 1)
            {
                error = "El ciclo de suscripción de la UI es incoherente.";
                return false;
            }

            return true;
        }
        finally
        {
            if (!wasOpen)
            {
                Close();
            }
        }
    }

    private void EnsureVisualTree()
    {
        if (built)
        {
            return;
        }

        RectTransform host = transform as RectTransform;
        if (host == null)
        {
            return;
        }

        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;

        openButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpenInventoryWarehouse",
            host,
            "INVENTARIO",
            HandleOpen,
            new Color(0.16f, 0.22f, 0.18f, 1f),
            13
        );
        SetRect(
            openButton.GetComponent<RectTransform>(),
            0f,
            1f,
            0f,
            1f,
            new Vector2(270f, -58f),
            new Vector2(402f, -18f)
        );

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "InventoryWarehouseModal",
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
            new Vector2(34f, 26f),
            new Vector2(-34f, -26f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        titleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            panel,
            "INVENTARIO / ALMACÉN",
            23,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(titleText.rectTransform, 0.02f, 0.93f, 0.52f, 0.985f, 0f);

        summaryText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Summary",
            panel,
            string.Empty,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(summaryText.rectTransform, 0.02f, 0.88f, 0.78f, 0.93f, 0f);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            panel,
            "CERRAR",
            Close,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            13
        );
        SetRect(closeButton.GetComponent<RectTransform>(), 0.88f, 0.935f, 0.98f, 0.98f, 0f);

        BuildTabs(panel);
        BuildToolbar(panel);
        BuildList(panel);
        BuildDetail(panel);
        BuildMinimumEditor(modalRoot);

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            panel,
            string.Empty,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(statusText.rectTransform, 0.02f, 0.015f, 0.98f, 0.065f, 0f);

        built = true;
    }

    private void BuildTabs(RectTransform panel)
    {
        stockTabButton = CreateTopButton(
            panel,
            "TabStock",
            "EXISTENCIAS",
            0.02f,
            () => SelectSection(BistroBuilderInventoryWarehouseSection.Stock)
        );
        alertsTabButton = CreateTopButton(
            panel,
            "TabAlerts",
            "ALERTAS",
            0.145f,
            () => SelectSection(BistroBuilderInventoryWarehouseSection.Alerts)
        );
        movementsTabButton = CreateTopButton(
            panel,
            "TabMovements",
            "MOVIMIENTOS",
            0.27f,
            () => SelectSection(BistroBuilderInventoryWarehouseSection.Movements)
        );
        receiptsTabButton = CreateTopButton(
            panel,
            "TabReceipts",
            "RECEPCIONES",
            0.395f,
            () => SelectSection(BistroBuilderInventoryWarehouseSection.Receipts)
        );
    }

    private Button CreateTopButton(
        RectTransform panel,
        string name,
        string label,
        float x,
        Action callback
    )
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name,
            panel,
            label,
            () => callback(),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12
        );
        SetRect(button.GetComponent<RectTransform>(), x, 0.815f, x + 0.115f, 0.865f, 0f);
        return button;
    }

    private void BuildToolbar(RectTransform panel)
    {
        searchInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "Search",
            panel,
            "Buscar ingrediente...",
            HandleSearchChanged,
            null
        );
        SetRect(searchInput.GetComponent<RectTransform>(), 0.02f, 0.755f, 0.28f, 0.805f, 0f);

        filterButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Filter",
            panel,
            "Filtro: Todos",
            CycleFilter,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12
        );
        SetRect(filterButton.GetComponent<RectTransform>(), 0.29f, 0.755f, 0.45f, 0.805f, 0f);

        sortButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Sort",
            panel,
            "Orden: Nombre",
            CycleSort,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12
        );
        SetRect(sortButton.GetComponent<RectTransform>(), 0.46f, 0.755f, 0.61f, 0.805f, 0f);

        Button openingButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpeningCheck",
            panel,
            "COMPROBAR APERTURA",
            HandleOpeningCheck,
            new Color(0.24f, 0.30f, 0.23f, 1f),
            12
        );
        SetRect(openingButton.GetComponent<RectTransform>(), 0.80f, 0.755f, 0.98f, 0.805f, 0f);
    }

    private void BuildList(RectTransform panel)
    {
        RectTransform listRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MainList",
            panel,
            new Vector2(0.02f, 0.08f),
            new Vector2(0.60f, 0.735f),
            Vector2.zero,
            Vector2.zero
        );
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "Scroll",
            listRoot,
            out mainListContent
        );
        mainListViewport = scroll.viewport;
    }

    private void BuildDetail(RectTransform panel)
    {
        detailRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Detail",
            panel,
            new Vector2(0.62f, 0.08f),
            new Vector2(0.98f, 0.735f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            detailRoot,
            new Color(0.065f, 0.075f, 0.07f, 0.78f)
        );

        detailTitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailTitle",
            detailRoot,
            "Selecciona un ingrediente",
            19,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        detailTitleText.raycastTarget = false;
        SetRect(detailTitleText.rectTransform, 0.04f, 0.88f, 0.96f, 0.98f, 0f);

        detailBodyText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailBody",
            detailRoot,
            string.Empty,
            13,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary
        );
        // El detalle comparte espacio con controles interactivos. Nunca debe
        // desbordarse sobre ellos ni capturar sus clics aunque cambie la
        // resolución o aumente la longitud de los textos localizados.
        detailBodyText.verticalOverflow = VerticalWrapMode.Truncate;
        detailBodyText.resizeTextForBestFit = true;
        detailBodyText.resizeTextMinSize = 10;
        detailBodyText.resizeTextMaxSize = 13;
        detailBodyText.lineSpacing = 0.92f;
        detailBodyText.raycastTarget = false;
        SetRect(detailBodyText.rectTransform, 0.04f, 0.46f, 0.96f, 0.88f, 0f);

        actionRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Actions",
            detailRoot,
            new Vector2(0.04f, 0.02f),
            new Vector2(0.96f, 0.44f),
            Vector2.zero,
            Vector2.zero
        );

        Text minimumLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "MinimumLabel",
            actionRoot,
            "Stock mínimo",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(minimumLabel.rectTransform, 0f, 0.83f, 0.34f, 0.98f, 0f);

        minimumCurrentText = BistroBuilderMenuEditorUiFactory.CreateText(
            "MinimumCurrent",
            actionRoot,
            "0",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary
        );
        SetRect(minimumCurrentText.rectTransform, 0.34f, 0.84f, 0.64f, 0.98f, 0f);

        minimumEditButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "EditMinimum",
            actionRoot,
            "EDITAR",
            OpenMinimumEditor,
            new Color(0.24f, 0.34f, 0.25f, 1f),
            11
        );
        SetRect(minimumEditButton.GetComponent<RectTransform>(), 0.68f, 0.84f, 1f, 0.98f, 0f);

        Text adjustmentLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "AdjustmentLabel",
            actionRoot,
            "Ajuste manual",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(adjustmentLabel.rectTransform, 0f, 0.64f, 0.34f, 0.80f, 0f);

        adjustmentInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "AdjustmentInput",
            actionRoot,
            "Cantidad",
            null,
            null
        );
        SetRect(adjustmentInput.GetComponent<RectTransform>(), 0.34f, 0.65f, 0.64f, 0.80f, 0f);

        adjustmentUnitText = BistroBuilderMenuEditorUiFactory.CreateText(
            "AdjustmentUnit",
            actionRoot,
            string.Empty,
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(adjustmentUnitText.rectTransform, 0.65f, 0.65f, 1f, 0.80f, 0f);

        reasonButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Reason",
            actionRoot,
            "Motivo: Corrección",
            CycleReason,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            11
        );
        SetRect(reasonButton.GetComponent<RectTransform>(), 0f, 0.45f, 0.48f, 0.60f, 0f);

        adjustmentNoteInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "AdjustmentNote",
            actionRoot,
            "Nota opcional",
            null,
            null
        );
        SetRect(adjustmentNoteInput.GetComponent<RectTransform>(), 0.50f, 0.45f, 1f, 0.60f, 0f);

        Button decrease = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Decrease",
            actionRoot,
            "REDUCIR",
            () => HandleAdjustment(false),
            new Color(0.42f, 0.20f, 0.17f, 1f),
            12
        );
        SetRect(decrease.GetComponent<RectTransform>(), 0f, 0.20f, 0.48f, 0.39f, 0f);

        Button increase = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Increase",
            actionRoot,
            "AUMENTAR",
            () => HandleAdjustment(true),
            new Color(0.20f, 0.38f, 0.25f, 1f),
            12
        );
        SetRect(increase.GetComponent<RectTransform>(), 0.52f, 0.20f, 1f, 0.39f, 0f);

        Text adjustmentHint = BistroBuilderMenuEditorUiFactory.CreateText(
            "AdjustmentHint",
            actionRoot,
            "Los ajustes generan un movimiento Correction y actualizan disponibilidad/alertas por la ruta canónica.",
            11,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(adjustmentHint.rectTransform, 0f, 0f, 1f, 0.17f, 0f);

    }

    private void BuildMinimumEditor(RectTransform parent)
    {
        minimumEditorOverlay = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MinimumEditorOverlay",
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            minimumEditorOverlay,
            new Color(0.015f, 0.018f, 0.016f, 0.92f)
        );

        RectTransform editorPanel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MinimumEditorPanel",
            minimumEditorOverlay,
            new Vector2(0.34f, 0.15f),
            new Vector2(0.66f, 0.86f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            editorPanel,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        minimumEditorTitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            editorPanel,
            "EDITAR STOCK MÍNIMO",
            19,
            TextAnchor.MiddleCenter,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(minimumEditorTitleText.rectTransform, 0.06f, 0.88f, 0.94f, 0.97f, 0f);

        RectTransform valuePanel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ValuePanel",
            editorPanel,
            new Vector2(0.08f, 0.74f),
            new Vector2(0.92f, 0.86f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            valuePanel,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        minimumEditorValueText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Value",
            valuePanel,
            "0",
            24,
            TextAnchor.MiddleRight,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(minimumEditorValueText.rectTransform, 0.05f, 0f, 0.75f, 1f, 0f);

        minimumEditorUnitText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Unit",
            valuePanel,
            string.Empty,
            16,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(minimumEditorUnitText.rectTransform, 0.78f, 0f, 0.96f, 1f, 0f);

        CreateMinimumKey(editorPanel, "Key7", "7", 0.08f, 0.59f, () => AppendMinimumEditorToken("7"));
        CreateMinimumKey(editorPanel, "Key8", "8", 0.37f, 0.59f, () => AppendMinimumEditorToken("8"));
        CreateMinimumKey(editorPanel, "Key9", "9", 0.66f, 0.59f, () => AppendMinimumEditorToken("9"));

        CreateMinimumKey(editorPanel, "Key4", "4", 0.08f, 0.46f, () => AppendMinimumEditorToken("4"));
        CreateMinimumKey(editorPanel, "Key5", "5", 0.37f, 0.46f, () => AppendMinimumEditorToken("5"));
        CreateMinimumKey(editorPanel, "Key6", "6", 0.66f, 0.46f, () => AppendMinimumEditorToken("6"));

        CreateMinimumKey(editorPanel, "Key1", "1", 0.08f, 0.33f, () => AppendMinimumEditorToken("1"));
        CreateMinimumKey(editorPanel, "Key2", "2", 0.37f, 0.33f, () => AppendMinimumEditorToken("2"));
        CreateMinimumKey(editorPanel, "Key3", "3", 0.66f, 0.33f, () => AppendMinimumEditorToken("3"));

        CreateMinimumKey(editorPanel, "Key0", "0", 0.08f, 0.20f, () => AppendMinimumEditorToken("0"));
        CreateMinimumKey(editorPanel, "KeyDecimal", ",", 0.37f, 0.20f, () => AppendMinimumEditorToken("."));
        CreateMinimumKey(editorPanel, "KeyBackspace", "BORRAR", 0.66f, 0.20f, BackspaceMinimumEditor);

        Button cancel = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Cancel",
            editorPanel,
            "CANCELAR",
            CloseMinimumEditor,
            new Color(0.30f, 0.24f, 0.22f, 1f),
            12
        );
        SetRect(cancel.GetComponent<RectTransform>(), 0.08f, 0.06f, 0.46f, 0.15f, 0f);

        Button save = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Save",
            editorPanel,
            "GUARDAR",
            CommitMinimumEditor,
            new Color(0.20f, 0.42f, 0.27f, 1f),
            12
        );
        SetRect(save.GetComponent<RectTransform>(), 0.54f, 0.06f, 0.92f, 0.15f, 0f);

        minimumEditorOverlay.gameObject.SetActive(false);
    }

    private Button CreateMinimumKey(
        RectTransform parent,
        string name,
        string label,
        float minX,
        float minY,
        Action callback
    )
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            name,
            parent,
            label,
            () => callback(),
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            15
        );
        SetRect(
            button.GetComponent<RectTransform>(),
            minX,
            minY,
            minX + 0.26f,
            minY + 0.10f,
            0f
        );
        return button;
    }

    private void OpenMinimumEditor()
    {
        if (!TryGetSelected(out BistroBuilderInventoryWarehouseIngredientSnapshot selected))
        {
            SetStatus("Selecciona un ingrediente.", true);
            return;
        }

        minimumEditorIngredientId = selected.IngredientId;
        minimumEditorDraft = FormatInputAmount(
            selected.MinimumStockCanonicalMilliUnits,
            selected.BaseUnit
        );
        if (string.IsNullOrWhiteSpace(minimumEditorDraft))
        {
            minimumEditorDraft = "0";
        }

        minimumEditorTitleText.text = "STOCK MÍNIMO · " + selected.DisplayName;
        minimumEditorUnitText.text = BistroBuilderMeasurementUtility.GetSymbol(
            GetDisplayUnit(selected.BaseUnit)
        );
        UpdateMinimumEditorValue();
        minimumEditorOverlay.gameObject.SetActive(true);
        minimumEditorOverlay.SetAsLastSibling();
    }

    private void CloseMinimumEditor()
    {
        minimumEditorIngredientId = string.Empty;
        minimumEditorDraft = "0";
        if (minimumEditorOverlay != null)
        {
            minimumEditorOverlay.gameObject.SetActive(false);
        }
    }

    private void AppendMinimumEditorToken(string token)
    {
        if (minimumEditorOverlay == null || !minimumEditorOverlay.gameObject.activeSelf)
        {
            return;
        }

        if (token == ".")
        {
            if (minimumEditorDraft.Contains("."))
            {
                return;
            }
            minimumEditorDraft = string.IsNullOrWhiteSpace(minimumEditorDraft)
                ? "0."
                : minimumEditorDraft + ".";
        }
        else
        {
            if (minimumEditorDraft == "0")
            {
                minimumEditorDraft = token;
            }
            else if (minimumEditorDraft.Length < 12)
            {
                minimumEditorDraft += token;
            }
        }

        UpdateMinimumEditorValue();
    }

    private void BackspaceMinimumEditor()
    {
        if (string.IsNullOrEmpty(minimumEditorDraft) ||
            minimumEditorDraft.Length <= 1)
        {
            minimumEditorDraft = "0";
        }
        else
        {
            minimumEditorDraft = minimumEditorDraft.Substring(
                0,
                minimumEditorDraft.Length - 1
            );
        }
        UpdateMinimumEditorValue();
    }

    private void UpdateMinimumEditorValue()
    {
        if (minimumEditorValueText != null)
        {
            minimumEditorValueText.text = string.IsNullOrWhiteSpace(minimumEditorDraft)
                ? "0"
                : minimumEditorDraft.Replace('.', ',');
        }
    }

    private void CommitMinimumEditor()
    {
        if (string.IsNullOrWhiteSpace(minimumEditorIngredientId))
        {
            SetStatus("No hay ingrediente vinculado al editor de mínimo.", true);
            return;
        }

        if (!warehouseService.TryGetIngredient(
                minimumEditorIngredientId,
                out BistroBuilderInventoryWarehouseIngredientSnapshot selected,
                out string error
            ))
        {
            SetStatus(error, true);
            return;
        }

        if (!TryParseDisplayQuantity(
                minimumEditorDraft,
                selected.BaseUnit,
                true,
                out long minimum,
                out error
            ))
        {
            SetStatus(error, true);
            return;
        }

        if (!warehouseService.TrySetMinimumStock(
                selected.IngredientId,
                minimum,
                out error
            ))
        {
            SetStatus(error, true);
            return;
        }

        CloseMinimumEditor();
        Refresh("Stock mínimo actualizado.", out _);
    }

    private void HandleOpen()
    {
        if (!TryOpenFromInterface(out string error))
        {
            SetStatus(error, true);
        }
    }

    private void SelectSection(BistroBuilderInventoryWarehouseSection section)
    {
        currentSection = section;
        Refresh(string.Empty, out _);
    }

    private void HandleSearchChanged(string ignored)
    {
        if (suppressCallbacks || currentSection != BistroBuilderInventoryWarehouseSection.Stock)
        {
            return;
        }
        Refresh(string.Empty, out _);
    }

    private void CycleFilter()
    {
        currentFilter = (BistroBuilderInventoryWarehouseFilter)(
            ((int)currentFilter + 1) % 4
        );
        Refresh(string.Empty, out _);
    }

    private void CycleSort()
    {
        currentSort = (BistroBuilderInventoryWarehouseSort)(
            ((int)currentSort + 1) % 4
        );
        Refresh(string.Empty, out _);
    }

    private void CycleReason()
    {
        currentReason = (BistroBuilderInventoryManualAdjustmentReason)(
            ((int)currentReason + 1) % 4
        );
        UpdateReasonButton();
    }

    private void HandleAdjustment(bool increase)
    {
        if (!TryGetSelected(out BistroBuilderInventoryWarehouseIngredientSnapshot selected))
        {
            SetStatus("Selecciona un ingrediente.", true);
            return;
        }

        if (!TryParseDisplayQuantity(
                adjustmentInput != null ? adjustmentInput.text : string.Empty,
                selected.BaseUnit,
                false,
                out long quantity,
                out string error
            ))
        {
            SetStatus(error, true);
            return;
        }

        long delta = increase ? quantity : -quantity;
        string note = adjustmentNoteInput != null
            ? adjustmentNoteInput.text
            : string.Empty;

        if (!warehouseService.TryAdjustStock(
                selected.IngredientId,
                delta,
                currentReason,
                note,
                out string operationId,
                out error
            ))
        {
            SetStatus(error, true);
            return;
        }

        if (adjustmentInput != null)
        {
            adjustmentInput.text = string.Empty;
        }
        if (adjustmentNoteInput != null)
        {
            adjustmentNoteInput.text = string.Empty;
        }

        Refresh(
            "Ajuste aplicado y registrado (" + operationId + ").",
            out _
        );
    }

    private void HandleOpeningCheck()
    {
        if (!warehouseService.TryEvaluateOpeningReadiness(
                out BistroBuilderInventoryOpeningReadinessSnapshot snapshot,
                out string error
            ))
        {
            SetStatus(error, true);
            return;
        }

        SetStatus(snapshot != null ? snapshot.Summary : "Comprobación completada.", snapshot != null && snapshot.HasWarnings);
    }

    private bool Refresh(string message, out string error)
    {
        error = string.Empty;
        if (warehouseService == null || !warehouseService.EnsureReady(out error))
        {
            SetStatus(error, true);
            return false;
        }

        UpdateTabs();
        bool success;
        switch (currentSection)
        {
            case BistroBuilderInventoryWarehouseSection.Alerts:
                success = RefreshAlerts(out error);
                break;
            case BistroBuilderInventoryWarehouseSection.Movements:
                success = RefreshMovements(out error);
                break;
            case BistroBuilderInventoryWarehouseSection.Receipts:
                success = RefreshReceipts(out error);
                break;
            default:
                success = RefreshStock(out error);
                break;
        }

        RefreshSummary();
        if (!string.IsNullOrWhiteSpace(message))
        {
            SetStatus(message, false);
        }
        else if (!success)
        {
            SetStatus(error, true);
        }

        return success;
    }

    private bool RefreshStock(out string error)
    {
        error = string.Empty;
        SetToolbarVisible(true);
        if (!warehouseService.CopyIngredientsTo(
                ingredients,
                currentFilter,
                currentSort,
                searchInput != null ? searchInput.text : string.Empty,
                out error
            ))
        {
            return false;
        }

        if (!ContainsIngredient(selectedIngredientId))
        {
            selectedIngredientId = ingredients.Count > 0
                ? ingredients[0].IngredientId
                : string.Empty;
        }

        EnsureRows(ingredients.Count);
        for (int index = 0; index < rowPool.Count; index++)
        {
            Button row = rowPool[index];
            bool active = index < ingredients.Count;
            row.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            BistroBuilderInventoryWarehouseIngredientSnapshot item =
                ingredients[index];
            ConfigureRow(
                row,
                BuildIngredientRowText(item),
                GetRowColor(item),
                item.IngredientId,
                HandleIngredientSelected
            );
        }
        VisibleRowCount = ingredients.Count;
        RefreshStockDetail();
        return true;
    }

    private bool RefreshAlerts(out string error)
    {
        error = string.Empty;
        SetToolbarVisible(false);
        if (!warehouseService.CopyAlertsTo(alerts, out error))
        {
            return false;
        }

        EnsureRows(Math.Max(1, alerts.Count));
        if (alerts.Count == 0)
        {
            ConfigureInfoOnlyRow(0, "Sin alertas activas.");
            HideRowsAfter(1);
            VisibleRowCount = 1;
        }
        else
        {
            for (int index = 0; index < alerts.Count; index++)
            {
                BistroBuilderInventoryAlertSnapshot alert = alerts[index];
                ConfigureRow(
                    rowPool[index],
                    BuildAlertRowText(alert),
                    GetAlertRowColor(alert.Severity),
                    string.Empty,
                    null
                );
            }
            HideRowsAfter(alerts.Count);
            VisibleRowCount = alerts.Count;
        }

        detailTitleText.text = "Alertas activas";
        detailBodyText.text =
            "Las alertas son estados deduplicados de 2.2C. No son movimientos ni crean stock.\n\n" +
            "Prioridad visual: agotado/crítico, bajo y próxima caducidad. La apertura sigue siendo informativa, no bloqueante.";
        actionRoot.gameObject.SetActive(false);
        return true;
    }

    private bool RefreshMovements(out string error)
    {
        error = string.Empty;
        SetToolbarVisible(false);
        if (!warehouseService.CopyMovementsTo(
                movements,
                maximumMovementRows,
                false,
                out error
            ))
        {
            return false;
        }

        EnsureRows(Math.Max(1, movements.Count));
        if (movements.Count == 0)
        {
            ConfigureInfoOnlyRow(0, "No hay movimientos jugables registrados.");
            HideRowsAfter(1);
            VisibleRowCount = 1;
        }
        else
        {
            for (int index = 0; index < movements.Count; index++)
            {
                BistroBuilderInventoryWarehouseMovementSnapshot movement =
                    movements[index];
                ConfigureRow(
                    rowPool[index],
                    BuildMovementRowText(movement),
                    BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                    string.Empty,
                    null
                );
            }
            HideRowsAfter(movements.Count);
            VisibleRowCount = movements.Count;
        }

        detailTitleText.text = "Movimientos de inventario";
        detailBodyText.text =
            "Historial jugable derivado del ledger canónico. Por claridad se ocultan reservas/liberaciones internas en esta vista.\n\n" +
            "Se muestran recepciones, consumo de cocina, ajustes, caducidad, stock inicial y mermas técnicas existentes.";
        actionRoot.gameObject.SetActive(false);
        return true;
    }

    private bool RefreshReceipts(out string error)
    {
        error = string.Empty;
        SetToolbarVisible(false);
        if (!warehouseService.CopyReceiptsTo(
                receipts,
                maximumReceiptRows,
                out error
            ))
        {
            return false;
        }

        EnsureRows(Math.Max(1, receipts.Count));
        if (receipts.Count == 0)
        {
            ConfigureInfoOnlyRow(0, "No hay recepciones de compra registradas.");
            HideRowsAfter(1);
            VisibleRowCount = 1;
        }
        else
        {
            for (int index = 0; index < receipts.Count; index++)
            {
                BistroBuilderInventoryWarehouseReceiptSnapshot receipt =
                    receipts[index];
                ConfigureRow(
                    rowPool[index],
                    BuildReceiptRowText(receipt),
                    BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                    string.Empty,
                    null
                );
            }
            HideRowsAfter(receipts.Count);
            VisibleRowCount = receipts.Count;
        }

        detailTitleText.text = "Recepciones recientes";
        detailBodyText.text =
            "Las recepciones se reconstruyen de movimientos Purchase de 2.2B agrupados por ReceiptId/OperationId.\n\n" +
            "La animación del repartidor sigue siendo únicamente visual y no es autoridad administrativa.";
        actionRoot.gameObject.SetActive(false);
        return true;
    }

    private void RefreshSummary()
    {
        if (!warehouseService.TryGetSummary(
                out BistroBuilderInventoryWarehouseSummarySnapshot summary,
                out _
            ))
        {
            return;
        }

        summaryText.text =
            summary.IngredientCount + " ingredientes · " +
            summary.LowStockCount + " bajos · " +
            summary.CriticalCount + " críticos · " +
            summary.OutOfStockCount + " agotados · " +
            summary.NearExpiryCount + " próximos a caducar";
    }

    private void RefreshStockDetail()
    {
        actionRoot.gameObject.SetActive(true);
        if (!TryGetSelected(out BistroBuilderInventoryWarehouseIngredientSnapshot item))
        {
            detailTitleText.text = "Sin selección";
            detailBodyText.text = "No hay ingredientes visibles con el filtro actual.";
            SetInputsEnabled(false);
            return;
        }

        SetInputsEnabled(true);
        detailTitleText.text = item.DisplayName;
        string expiration = item.NextExpirationDayIndex > 0
            ? "Día " + item.NextExpirationDayIndex +
              " (" + Math.Max(0, item.DaysUntilNextExpiration) + " día(s))"
            : "Sin caducidad próxima";
        string receipt = item.LastReceiptSequence > 0L
            ? FormatQuantity(item.LastReceiptQuantityCanonicalMilliUnits, item.BaseUnit) +
              " · origen " + (string.IsNullOrWhiteSpace(item.LastReceiptSourceId) ? "desconocido" : item.LastReceiptSourceId)
            : "Sin recepción de compra registrada";
        string forecast = BuildForecastText(item);

        detailBodyText.text =
            "Stock total: " + FormatQuantity(item.OnHandCanonicalMilliUnits, item.BaseUnit) + "\n" +
            "Disponible: " + FormatQuantity(item.AvailableCanonicalMilliUnits, item.BaseUnit) + "\n" +
            "Reservado: " + FormatQuantity(item.ReservedCanonicalMilliUnits, item.BaseUnit) + "\n" +
            "Mínimo: " + FormatQuantity(item.MinimumStockCanonicalMilliUnits, item.BaseUnit) + "\n" +
            "Estado: " + TranslateStockLevel(item.StockLevelState) + "\n\n" +
            "Próxima caducidad: " + expiration + "\n" +
            "Cantidad próxima a caducar: " + FormatQuantity(item.NearExpiryAvailableCanonicalMilliUnits, item.BaseUnit) + "\n" +
            "Previsión: " + forecast + "\n" +
            "Última recepción: " + receipt;

        suppressCallbacks = true;
        string symbol = BistroBuilderMeasurementUtility.GetSymbol(
            GetDisplayUnit(item.BaseUnit)
        );
        if (minimumCurrentText != null)
        {
            minimumCurrentText.text =
                FormatInputAmount(
                    item.MinimumStockCanonicalMilliUnits,
                    item.BaseUnit
                ) + " " + symbol;
        }
        adjustmentUnitText.text = symbol;
        suppressCallbacks = false;
        UpdateReasonButton();
    }

    private void HandleIngredientSelected(string ingredientId)
    {
        string nextIngredientId = ingredientId ?? string.Empty;
        if (!string.Equals(
                selectedIngredientId,
                nextIngredientId,
                StringComparison.Ordinal
            ))
        {
            CloseMinimumEditor();
        }
        selectedIngredientId = nextIngredientId;
        Refresh(string.Empty, out _);
    }

    private void EnsureRows(int count)
    {
        int target = Mathf.Max(0, count);
        while (rowPool.Count < target)
        {
            int rowIndex = rowPool.Count;
            Button row = BistroBuilderMenuEditorUiFactory.CreateButton(
                "Row_" + rowIndex,
                mainListContent,
                string.Empty,
                null,
                BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                12
            );
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;
            rowPool.Add(row);
        }
    }

    private void ConfigureRow(
        Button row,
        string label,
        Color color,
        string id,
        Action<string> callback
    )
    {
        if (row == null)
        {
            return;
        }

        row.gameObject.SetActive(true);
        row.onClick.RemoveAllListeners();
        if (callback != null)
        {
            string captured = id;
            row.onClick.AddListener(() => callback(captured));
        }

        Image image = row.targetGraphic as Image;
        if (image != null)
        {
            image.color = color;
        }

        Text text = row.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label ?? string.Empty;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Normal;
        }
    }

    private void ConfigureInfoOnlyRow(int index, string message)
    {
        EnsureRows(index + 1);
        ConfigureRow(
            rowPool[index],
            message,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            string.Empty,
            null
        );
    }

    private void HideRowsAfter(int count)
    {
        for (int index = count; index < rowPool.Count; index++)
        {
            rowPool[index].gameObject.SetActive(false);
        }
    }

    private void SetToolbarVisible(bool stockMode)
    {
        if (searchInput != null)
        {
            searchInput.gameObject.SetActive(stockMode);
        }
        if (filterButton != null)
        {
            filterButton.gameObject.SetActive(stockMode);
        }
        if (sortButton != null)
        {
            sortButton.gameObject.SetActive(stockMode);
        }

        UpdateFilterButton();
        UpdateSortButton();
    }

    private void UpdateTabs()
    {
        SetButtonSelected(stockTabButton, currentSection == BistroBuilderInventoryWarehouseSection.Stock);
        SetButtonSelected(alertsTabButton, currentSection == BistroBuilderInventoryWarehouseSection.Alerts);
        SetButtonSelected(movementsTabButton, currentSection == BistroBuilderInventoryWarehouseSection.Movements);
        SetButtonSelected(receiptsTabButton, currentSection == BistroBuilderInventoryWarehouseSection.Receipts);
        titleText.text = currentSection == BistroBuilderInventoryWarehouseSection.Stock
            ? "INVENTARIO / ALMACÉN"
            : "INVENTARIO / ALMACÉN · " + TranslateSection(currentSection);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = selected
                ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                : BistroBuilderMenuEditorUiFactory.SurfaceRaised;
        }
    }

    private void UpdateFilterButton()
    {
        Text label = filterButton != null
            ? filterButton.GetComponentInChildren<Text>(true)
            : null;
        if (label != null)
        {
            label.text = "Filtro: " + TranslateFilter(currentFilter);
        }
    }

    private void UpdateSortButton()
    {
        Text label = sortButton != null
            ? sortButton.GetComponentInChildren<Text>(true)
            : null;
        if (label != null)
        {
            label.text = "Orden: " + TranslateSort(currentSort);
        }
    }

    private void UpdateReasonButton()
    {
        Text label = reasonButton != null
            ? reasonButton.GetComponentInChildren<Text>(true)
            : null;
        if (label != null)
        {
            label.text = "Motivo: " + TranslateReason(currentReason);
        }
    }

    private bool TryGetSelected(
        out BistroBuilderInventoryWarehouseIngredientSnapshot snapshot
    )
    {
        snapshot = default;
        return warehouseService != null &&
               warehouseService.TryGetIngredient(
                   selectedIngredientId,
                   out snapshot,
                   out _
               );
    }

    private bool ContainsIngredient(string ingredientId)
    {
        if (string.IsNullOrWhiteSpace(ingredientId))
        {
            return false;
        }
        for (int index = 0; index < ingredients.Count; index++)
        {
            if (string.Equals(
                    ingredients[index].IngredientId,
                    ingredientId,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }
        return false;
    }

    private void SetInputsEnabled(bool enabledValue)
    {
        if (minimumEditButton != null)
        {
            minimumEditButton.interactable = enabledValue;
        }
        if (adjustmentInput != null)
        {
            adjustmentInput.interactable = enabledValue;
        }
        if (adjustmentNoteInput != null)
        {
            adjustmentNoteInput.interactable = enabledValue;
        }
        if (reasonButton != null)
        {
            reasonButton.interactable = enabledValue;
        }
    }

    private void QueueRefresh()
    {
        if (!IsOpen || refreshQueued)
        {
            return;
        }
        refreshQueued = true;
        refreshRoutine = StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        refreshQueued = false;
        refreshRoutine = null;
        if (IsOpen)
        {
            Refresh(string.Empty, out _);
        }
    }

    private void CancelQueuedRefresh()
    {
        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
        refreshQueued = false;
    }

    private void HandleDataChanged()
    {
        QueueRefresh();
    }

    private void Subscribe()
    {
        if (subscribed || warehouseService == null)
        {
            return;
        }
        warehouseService.DataChanged += HandleDataChanged;
        subscribed = true;
        subscriptionGeneration++;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }
        if (warehouseService != null)
        {
            warehouseService.DataChanged -= HandleDataChanged;
        }
        subscribed = false;
    }

    private void SetVisible(bool visible)
    {
        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);
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

    private void ResolveDependencies()
    {
        if (warehouseService == null)
        {
            warehouseService = FindSceneComponent<BistroBuilderInventoryWarehouseService>();
        }
        if (cameraController == null)
        {
            cameraController = FindSceneComponent<BistroBuilderProfessionalCameraController>();
        }
        if (editInteractionController == null)
        {
            editInteractionController = FindSceneComponent<RestaurantEditInteractionController>();
        }
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] items = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        return items != null && items.Length > 0 ? items[0] : null;
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText == null)
        {
            return;
        }
        statusText.text = message ?? string.Empty;
        statusText.color = warning
            ? BistroBuilderMenuEditorUiFactory.Warning
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private static string BuildIngredientRowText(
        BistroBuilderInventoryWarehouseIngredientSnapshot item
    )
    {
        string expiry = item.IsNearExpiry ? " · Próx. caducidad" : string.Empty;
        return item.DisplayName + "\n" +
               "Disponible " + FormatQuantity(item.AvailableCanonicalMilliUnits, item.BaseUnit) +
               " · " + TranslateStockLevel(item.StockLevelState) + expiry;
    }

    private static string BuildAlertRowText(BistroBuilderInventoryAlertSnapshot alert)
    {
        return alert.Message;
    }

    private static string BuildMovementRowText(
        BistroBuilderInventoryWarehouseMovementSnapshot movement
    )
    {
        string label = TranslateMovement(movement.TransactionType);
        long delta = movement.OnHandDeltaCanonicalMilliUnits;
        string sign = delta > 0L ? "+" : string.Empty;
        string quantity = delta != 0L
            ? sign + FormatSignedQuantity(delta, movement.BaseUnit)
            : "sin cambio físico";
        string reason = !string.IsNullOrWhiteSpace(movement.Reason)
            ? " · " + movement.Reason
            : string.Empty;
        return "#" + movement.Sequence + " · " + label + " · " + movement.IngredientDisplayName +
               " · " + quantity + reason;
    }

    private static string BuildReceiptRowText(
        BistroBuilderInventoryWarehouseReceiptSnapshot receipt
    )
    {
        var lines = new List<string>();
        int shown = Math.Min(3, receipt.Lines.Count);
        for (int index = 0; index < shown; index++)
        {
            BistroBuilderInventoryWarehouseReceiptLineSnapshot line = receipt.Lines[index];
            lines.Add(
                line.IngredientDisplayName + " " +
                FormatQuantity(line.CanonicalMilliUnits, line.BaseUnit)
            );
        }
        string remainder = receipt.Lines.Count > shown
            ? " · +" + (receipt.Lines.Count - shown) + " más"
            : string.Empty;
        return receipt.ReceiptId + " · " + receipt.Lines.Count + " ingrediente(s)" +
               " · " + string.Join(", ", lines) + remainder;
    }

    private static string BuildForecastText(
        BistroBuilderInventoryWarehouseIngredientSnapshot item
    )
    {
        switch (item.ForecastState)
        {
            case BistroBuilderInventoryForecastState.Available:
                return item.CoverageDays.ToString("0.0", CultureInfo.InvariantCulture) +
                       " días de cobertura (" +
                       FormatCanonicalDouble(item.AverageDailyConsumptionCanonicalMilliUnits, item.BaseUnit) +
                       "/día)";
            case BistroBuilderInventoryForecastState.NoConsumption:
                return "Sin consumo registrado en " + item.ConsumptionHistoryDays + " día(s).";
            default:
                return "Sin historial suficiente.";
        }
    }

    private static bool TryParseDisplayQuantity(
        string raw,
        BistroBuilderMeasurementUnit baseUnit,
        bool allowZero,
        out long canonicalMilliUnits,
        out string error
    )
    {
        canonicalMilliUnits = 0L;
        error = string.Empty;
        string normalized = raw != null ? raw.Trim().Replace(',', '.') : string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) ||
            !double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount) ||
            double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0d ||
            (!allowZero && amount <= 0d))
        {
            error = allowZero
                ? "Introduce una cantidad mayor o igual que cero."
                : "Introduce una cantidad mayor que cero.";
            return false;
        }

        if (amount == 0d)
        {
            canonicalMilliUnits = 0L;
            return true;
        }

        return BistroBuilderMeasurementUtility.TryConvertToCanonicalMilliUnits(
            amount,
            GetDisplayUnit(baseUnit),
            out canonicalMilliUnits,
            out error
        );
    }

    private static string FormatInputAmount(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        double amount = BistroBuilderMeasurementUtility.ConvertCanonicalMilliUnitsToDisplayAmount(
            Math.Max(0L, canonicalMilliUnits),
            GetDisplayUnit(baseUnit)
        );
        return amount.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatQuantity(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        double amount = BistroBuilderMeasurementUtility.ConvertCanonicalMilliUnitsToDisplayAmount(
            Math.Max(0L, canonicalMilliUnits),
            GetDisplayUnit(baseUnit)
        );
        return amount.ToString("0.##", CultureInfo.InvariantCulture) + " " +
               BistroBuilderMeasurementUtility.GetSymbol(GetDisplayUnit(baseUnit));
    }

    private static string FormatSignedQuantity(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        bool negative = canonicalMilliUnits < 0L;
        long absolute = canonicalMilliUnits == long.MinValue
            ? long.MaxValue
            : Math.Abs(canonicalMilliUnits);
        return (negative ? "-" : string.Empty) + FormatQuantity(absolute, baseUnit);
    }

    private static string FormatCanonicalDouble(
        double canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        if (canonicalMilliUnits <= 0d)
        {
            return "0 " + BistroBuilderMeasurementUtility.GetSymbol(GetDisplayUnit(baseUnit));
        }
        double amount = canonicalMilliUnits /
                        BistroBuilderMeasurementUtility.MilliUnitsPerCanonicalUnit;
        if (GetDisplayUnit(baseUnit) == BistroBuilderMeasurementUnit.Kilogram ||
            GetDisplayUnit(baseUnit) == BistroBuilderMeasurementUnit.Liter)
        {
            amount /= 1000d;
        }
        return amount.ToString("0.##", CultureInfo.InvariantCulture) + " " +
               BistroBuilderMeasurementUtility.GetSymbol(GetDisplayUnit(baseUnit));
    }

    private static BistroBuilderMeasurementUnit GetDisplayUnit(
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        switch (baseUnit)
        {
            case BistroBuilderMeasurementUnit.Gram:
                return BistroBuilderMeasurementUnit.Kilogram;
            case BistroBuilderMeasurementUnit.Milliliter:
                return BistroBuilderMeasurementUnit.Liter;
            default:
                return baseUnit;
        }
    }

    private static string TranslateStockLevel(BistroBuilderInventoryStockLevelState state)
    {
        switch (state)
        {
            case BistroBuilderInventoryStockLevelState.Low:
                return "Bajo";
            case BistroBuilderInventoryStockLevelState.Critical:
                return "Crítico";
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                return "Agotado";
            default:
                return "Normal";
        }
    }

    private static string TranslateFilter(BistroBuilderInventoryWarehouseFilter filter)
    {
        switch (filter)
        {
            case BistroBuilderInventoryWarehouseFilter.LowStock:
                return "Stock bajo";
            case BistroBuilderInventoryWarehouseFilter.CriticalOrOutOfStock:
                return "Críticos/agotados";
            case BistroBuilderInventoryWarehouseFilter.NearExpiry:
                return "Próx. caducar";
            default:
                return "Todos";
        }
    }

    private static string TranslateSort(BistroBuilderInventoryWarehouseSort sort)
    {
        switch (sort)
        {
            case BistroBuilderInventoryWarehouseSort.AvailableStock:
                return "Stock";
            case BistroBuilderInventoryWarehouseSort.Status:
                return "Estado";
            case BistroBuilderInventoryWarehouseSort.Expiration:
                return "Caducidad";
            default:
                return "Nombre";
        }
    }

    private static string TranslateReason(BistroBuilderInventoryManualAdjustmentReason reason)
    {
        switch (reason)
        {
            case BistroBuilderInventoryManualAdjustmentReason.BreakageOrLoss:
                return "Rotura/pérdida";
            case BistroBuilderInventoryManualAdjustmentReason.ReceivingError:
                return "Error recepción";
            case BistroBuilderInventoryManualAdjustmentReason.Other:
                return "Otro";
            default:
                return "Corrección";
        }
    }

    private static string TranslateSection(BistroBuilderInventoryWarehouseSection section)
    {
        switch (section)
        {
            case BistroBuilderInventoryWarehouseSection.Alerts:
                return "ALERTAS";
            case BistroBuilderInventoryWarehouseSection.Movements:
                return "MOVIMIENTOS";
            case BistroBuilderInventoryWarehouseSection.Receipts:
                return "RECEPCIONES";
            default:
                return "EXISTENCIAS";
        }
    }

    private static string TranslateMovement(BistroBuilderInventoryTransactionType type)
    {
        switch (type)
        {
            case BistroBuilderInventoryTransactionType.InitialStock:
                return "Stock inicial";
            case BistroBuilderInventoryTransactionType.Purchase:
                return "Recepción";
            case BistroBuilderInventoryTransactionType.Consumption:
                return "Consumo cocina";
            case BistroBuilderInventoryTransactionType.Waste:
                return "Merma";
            case BistroBuilderInventoryTransactionType.Correction:
                return "Ajuste";
            case BistroBuilderInventoryTransactionType.Expiration:
                return "Caducidad";
            case BistroBuilderInventoryTransactionType.Reservation:
                return "Reserva";
            case BistroBuilderInventoryTransactionType.ReservationRelease:
                return "Liberación";
            default:
                return type.ToString();
        }
    }

    private static Color GetRowColor(BistroBuilderInventoryWarehouseIngredientSnapshot item)
    {
        if (item.StockLevelState == BistroBuilderInventoryStockLevelState.OutOfStock)
        {
            return new Color(0.43f, 0.13f, 0.13f, 1f);
        }
        if (item.StockLevelState == BistroBuilderInventoryStockLevelState.Critical)
        {
            return new Color(0.38f, 0.18f, 0.14f, 1f);
        }
        if (item.StockLevelState == BistroBuilderInventoryStockLevelState.Low)
        {
            return new Color(0.32f, 0.27f, 0.14f, 1f);
        }
        if (item.IsNearExpiry)
        {
            return new Color(0.25f, 0.24f, 0.15f, 1f);
        }
        return BistroBuilderMenuEditorUiFactory.SurfaceRaised;
    }

    private static Color GetAlertRowColor(BistroBuilderInventoryAlertSeverity severity)
    {
        switch (severity)
        {
            case BistroBuilderInventoryAlertSeverity.Critical:
                return new Color(0.43f, 0.13f, 0.13f, 1f);
            case BistroBuilderInventoryAlertSeverity.Warning:
                return new Color(0.32f, 0.27f, 0.14f, 1f);
            default:
                return BistroBuilderMenuEditorUiFactory.SurfaceRaised;
        }
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        float margin
    )
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }

    private void OnValidate()
    {
        ResolveDependencies();
    }
#endif
}
