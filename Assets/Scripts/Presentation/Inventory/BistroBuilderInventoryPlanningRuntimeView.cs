using System;
using System.Collections.Generic;
using System.Globalization;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI jugable inicial de Inventario/Almacén para 2.2C.
///
/// Presenta únicamente información agregada: stock, reservado, disponible,
/// mínimo, alertas, próxima caducidad y cobertura. Nunca permite seleccionar
/// o gestionar lotes internos manualmente.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderInventoryPlanningRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "INVENTORY-2.2C-UI";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderInventoryPlanningService planningService;

    [SerializeField]
    private BistroBuilderProfessionalCameraController cameraController;

    [SerializeField]
    private RestaurantEditInteractionController editInteractionController;

    [Header("Comportamiento")]

    [SerializeField]
    private bool showOpenButton = true;

    private readonly List<BistroBuilderInventoryPlanningSnapshot> snapshots =
        new List<BistroBuilderInventoryPlanningSnapshot>(64);

    private readonly List<BistroBuilderInventoryAlertSnapshot> alerts =
        new List<BistroBuilderInventoryAlertSnapshot>(64);

    private readonly List<Button> rowPool = new List<Button>(64);

    private Button openButton;
    private RectTransform modalRoot;
    private RectTransform listContent;
    private RectTransform listViewport;
    private Text summaryText;
    private Text statusText;
    private Text detailTitleText;
    private Text detailStockText;
    private Text detailExpiryText;
    private Text detailForecastText;
    private Text alertText;
    private Text minimumUnitText;
    private InputField minimumInput;
    private Button applyMinimumButton;
    private Button openingCheckButton;

    private string selectedIngredientId = string.Empty;
    private bool built;
    private bool subscribed;
    private bool suppressCallbacks;
    private bool cameraWasEnabled;
    private bool editInteractionWasEnabled;
    private bool inputGateApplied;

    public BistroBuilderInventoryPlanningService PlanningService =>
        planningService;

    public bool VisualTreeBuilt => built;

    public bool IsOpen => modalRoot != null && modalRoot.gameObject.activeSelf;

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
        RestoreInputGate();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        ResolveDependencies();

        if (planningService == null)
        {
            error = "Falta BistroBuilderInventoryPlanningService.";
            return false;
        }

        return planningService.ValidateConfiguration(out error);
    }

    public bool TryOpenFromInterface(out string error)
    {
        error = string.Empty;
        EnsureVisualTree();
        ResolveDependencies();

        if (planningService == null ||
            !planningService.EnsureInitialized(out error))
        {
            return false;
        }

        SetVisible(true);
        ApplyInputGate();
        Refresh("Inventario actualizado.");
        return true;
    }

    public void Close()
    {
        SetVisible(false);
        RestoreInputGate();
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
            Refresh(string.Empty);
            Canvas.ForceUpdateCanvases();

            if (listViewport == null ||
                listViewport.GetComponent<RectMask2D>() == null)
            {
                error = "El listado de inventario no utiliza RectMask2D.";
                return false;
            }

            if (snapshots.Count == 0)
            {
                error = "La UI de inventario no contiene ingredientes.";
                return false;
            }

            int activeRows = 0;
            for (int index = 0; index < rowPool.Count; index++)
            {
                if (rowPool[index] != null && rowPool[index].gameObject.activeSelf)
                {
                    activeRows++;
                }
            }

            if (activeRows != snapshots.Count)
            {
                error = "La UI no representa todas las filas de inventario.";
                return false;
            }

            if (minimumInput == null || detailStockText == null ||
                detailForecastText == null || alertText == null)
            {
                error = "Faltan controles de stock mínimo, previsión o alertas.";
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
            "OpenInventoryPlanning",
            host,
            "INVENTARIO",
            HandleOpen,
            new Color(0.18f, 0.22f, 0.20f, 1f),
            13
        );
        RectTransform openRect = openButton.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(0f, 1f);
        openRect.anchorMax = new Vector2(0f, 1f);
        openRect.pivot = new Vector2(0f, 1f);
        openRect.anchoredPosition = new Vector2(270f, -18f);
        openRect.sizeDelta = new Vector2(132f, 40f);

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "InventoryPlanningModal",
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
            new Vector2(42f, 32f),
            new Vector2(-42f, -32f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        Text title = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title",
            panel,
            "ALMACÉN E INVENTARIO",
            23,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(title.rectTransform, 0.02f, 0.925f, 0.55f, 0.985f, 0f);

        summaryText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Summary",
            panel,
            string.Empty,
            14,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(summaryText.rectTransform, 0.02f, 0.875f, 0.73f, 0.925f, 0f);

        Button closeButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close",
            panel,
            "CERRAR",
            Close,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            14
        );
        SetRect(
            closeButton.GetComponent<RectTransform>(),
            0.86f,
            0.93f,
            0.98f,
            0.98f,
            0f
        );

        RectTransform listRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "IngredientList",
            panel,
            new Vector2(0.02f, 0.13f),
            new Vector2(0.36f, 0.86f),
            Vector2.zero,
            Vector2.zero
        );
        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "Scroll",
            listRoot,
            out listContent
        );
        listViewport = scroll.viewport;

        RectTransform detail = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Detail",
            panel,
            new Vector2(0.38f, 0.13f),
            new Vector2(0.72f, 0.86f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            detail,
            new Color(0.07f, 0.08f, 0.075f, 0.75f)
        );

        detailTitleText = BistroBuilderMenuEditorUiFactory.CreateText(
            "DetailTitle",
            detail,
            "Selecciona un ingrediente",
            20,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(detailTitleText.rectTransform, 0.04f, 0.86f, 0.96f, 0.98f, 0f);

        detailStockText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Stock",
            detail,
            string.Empty,
            15,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary
        );
        SetRect(detailStockText.rectTransform, 0.04f, 0.62f, 0.96f, 0.86f, 0f);

        Text minimumLabel = BistroBuilderMenuEditorUiFactory.CreateText(
            "MinimumLabel",
            detail,
            "Stock mínimo configurable",
            14,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(minimumLabel.rectTransform, 0.04f, 0.54f, 0.96f, 0.61f, 0f);

        minimumInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "MinimumInput",
            detail,
            "0",
            null,
            null
        );
        SetRect(
            minimumInput.GetComponent<RectTransform>(),
            0.04f,
            0.46f,
            0.58f,
            0.54f,
            0f
        );

        minimumUnitText = BistroBuilderMenuEditorUiFactory.CreateText(
            "MinimumUnit",
            detail,
            string.Empty,
            14,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(minimumUnitText.rectTransform, 0.60f, 0.46f, 0.72f, 0.54f, 0f);

        applyMinimumButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "ApplyMinimum",
            detail,
            "GUARDAR MÍNIMO",
            HandleApplyMinimum,
            BistroBuilderMenuEditorUiFactory.SurfaceSelected,
            13
        );
        SetRect(
            applyMinimumButton.GetComponent<RectTransform>(),
            0.74f,
            0.46f,
            0.96f,
            0.54f,
            0f
        );

        detailExpiryText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Expiry",
            detail,
            string.Empty,
            14,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.Warning
        );
        SetRect(detailExpiryText.rectTransform, 0.04f, 0.34f, 0.96f, 0.45f, 0f);

        detailForecastText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Forecast",
            detail,
            string.Empty,
            14,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary
        );
        SetRect(detailForecastText.rectTransform, 0.04f, 0.16f, 0.96f, 0.34f, 0f);

        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status",
            detail,
            string.Empty,
            13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(statusText.rectTransform, 0.04f, 0.03f, 0.96f, 0.14f, 0f);

        RectTransform alertPanel = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Alerts",
            panel,
            new Vector2(0.74f, 0.22f),
            new Vector2(0.98f, 0.86f),
            Vector2.zero,
            Vector2.zero
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            alertPanel,
            new Color(0.09f, 0.075f, 0.055f, 0.8f)
        );

        Text alertTitle = BistroBuilderMenuEditorUiFactory.CreateText(
            "AlertTitle",
            alertPanel,
            "ALERTAS ACTIVAS",
            16,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.Warning,
            FontStyle.Bold
        );
        SetRect(alertTitle.rectTransform, 0.05f, 0.88f, 0.95f, 0.98f, 0f);

        alertText = BistroBuilderMenuEditorUiFactory.CreateText(
            "AlertText",
            alertPanel,
            string.Empty,
            13,
            TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary
        );
        SetRect(alertText.rectTransform, 0.05f, 0.05f, 0.95f, 0.87f, 0f);

        openingCheckButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpeningCheck",
            panel,
            "COMPROBAR APERTURA",
            HandleOpeningCheck,
            BistroBuilderMenuEditorUiFactory.Accent,
            13
        );
        SetRect(
            openingCheckButton.GetComponent<RectTransform>(),
            0.74f,
            0.13f,
            0.98f,
            0.20f,
            0f
        );

        Text note = BistroBuilderMenuEditorUiFactory.CreateText(
            "Note",
            panel,
            "Los lotes son internos. La previsión usa consumo real y no bloquea la apertura.",
            12,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(note.rectTransform, 0.02f, 0.025f, 0.98f, 0.10f, 0f);

        built = true;
    }

    private void HandleOpen()
    {
        if (!TryOpenFromInterface(out string error))
        {
            Debug.LogWarning(error, this);
        }
    }

    private void HandleApplyMinimum()
    {
        if (suppressCallbacks || string.IsNullOrWhiteSpace(selectedIngredientId))
        {
            return;
        }

        if (!planningService.TryGetPlanningSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryPlanningSnapshot selected
            ))
        {
            SetStatus("No se pudo resolver el ingrediente seleccionado.", true);
            return;
        }

        string raw = minimumInput != null ? minimumInput.text : string.Empty;
        if (!TryParseMinimum(
                raw,
                GetDisplayUnit(selected.BaseUnit),
                out long minimum,
                out string error
            ))
        {
            SetStatus(error, true);
            return;
        }

        if (!planningService.TrySetMinimumStock(
                selectedIngredientId,
                minimum,
                out error
            ))
        {
            SetStatus(error, true);
            return;
        }

        Refresh("Stock mínimo actualizado.");
    }

    private void HandleOpeningCheck()
    {
        if (!planningService.TryEvaluateOpeningReadiness(
                out BistroBuilderInventoryOpeningReadinessSnapshot readiness,
                out string error
            ))
        {
            SetStatus(error, true);
            return;
        }

        SetStatus(readiness.Summary, readiness.HasWarnings);
    }

    private void Refresh(string status)
    {
        if (planningService == null)
        {
            return;
        }

        planningService.TryRecalculateAll(out _);
        planningService.CopyPlanningSnapshotsTo(snapshots);
        planningService.CopyActiveAlertsTo(alerts);

        if (snapshots.Count > 0 &&
            !ContainsIngredient(selectedIngredientId))
        {
            selectedIngredientId = snapshots[0].IngredientId;
        }

        EnsureRows();
        for (int index = 0; index < rowPool.Count; index++)
        {
            Button row = rowPool[index];
            if (row == null)
            {
                continue;
            }

            bool active = index < snapshots.Count;
            row.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            BistroBuilderInventoryPlanningSnapshot snapshot = snapshots[index];
            string id = snapshot.IngredientId;
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => SelectIngredient(id));

            Text label = row.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = BuildRowLabel(snapshot);
            }

            Image image = row.targetGraphic as Image;
            if (image != null)
            {
                image.color = GetStockColor(snapshot.StockLevelState);
            }
        }

        summaryText.text = snapshots.Count + " ingredientes · " +
                           alerts.Count + " alerta(s) activa(s) · Día " +
                           (planningService.GeneralGameStateService != null
                               ? planningService.GeneralGameStateService.DayIndex
                               : 0);

        alertText.text = BuildAlertText();
        RefreshDetail();
        SetStatus(status, false);
    }

    private void EnsureRows()
    {
        while (rowPool.Count < snapshots.Count)
        {
            Button row = BistroBuilderMenuEditorUiFactory.CreateButton(
                "IngredientRow_" + rowPool.Count,
                listContent,
                string.Empty,
                null,
                BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                13
            );
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(row, 42f);
            rowPool.Add(row);
        }
    }

    private void SelectIngredient(string ingredientId)
    {
        selectedIngredientId = ingredientId ?? string.Empty;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (planningService == null ||
            !planningService.TryGetPlanningSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryPlanningSnapshot snapshot
            ))
        {
            detailTitleText.text = "Selecciona un ingrediente";
            detailStockText.text = string.Empty;
            detailExpiryText.text = string.Empty;
            detailForecastText.text = string.Empty;
            minimumInput.text = string.Empty;
            minimumUnitText.text = string.Empty;
            applyMinimumButton.interactable = false;
            return;
        }

        applyMinimumButton.interactable = true;
        detailTitleText.text = snapshot.DisplayName + " · " +
                               TranslateStockLevel(snapshot.StockLevelState);
        detailTitleText.color = GetTextColor(snapshot.StockLevelState);

        detailStockText.text =
            "Físico: " + FormatQuantity(snapshot.OnHandCanonicalMilliUnits, snapshot.BaseUnit) +
            "\nReservado: " + FormatQuantity(snapshot.ReservedCanonicalMilliUnits, snapshot.BaseUnit) +
            "\nDisponible: " + FormatQuantity(snapshot.AvailableCanonicalMilliUnits, snapshot.BaseUnit) +
            "\nMínimo: " + FormatQuantity(snapshot.MinimumStockCanonicalMilliUnits, snapshot.BaseUnit);

        suppressCallbacks = true;
        minimumInput.text = FormatInputAmount(
            snapshot.MinimumStockCanonicalMilliUnits,
            snapshot.BaseUnit
        );
        minimumUnitText.text = BistroBuilderMeasurementUtility.GetSymbol(
            GetDisplayUnit(snapshot.BaseUnit)
        );
        suppressCallbacks = false;

        if (snapshot.NextExpirationDayIndex > snapshot.CurrentDayIndex)
        {
            int days = snapshot.NextExpirationDayIndex - snapshot.CurrentDayIndex;
            detailExpiryText.text = "Próxima caducidad: en " + days +
                                    (days == 1 ? " día" : " días") +
                                    (snapshot.NearExpiryAvailableCanonicalMilliUnits > 0L
                                        ? "\nCantidad próxima: " + FormatQuantity(
                                            snapshot.NearExpiryAvailableCanonicalMilliUnits,
                                            snapshot.BaseUnit
                                        )
                                        : string.Empty);
        }
        else
        {
            detailExpiryText.text = "Sin caducidad próxima relevante.";
        }

        switch (snapshot.ForecastState)
        {
            case BistroBuilderInventoryForecastState.Available:
                double dailyDisplay = ConvertCanonicalDoubleToDisplay(
                    snapshot.AverageDailyConsumptionCanonicalMilliUnits,
                    snapshot.BaseUnit
                );
                detailForecastText.text =
                    "Previsión básica\nConsumo medio: " +
                    dailyDisplay.ToString("0.##", CultureInfo.InvariantCulture) + " " +
                    BistroBuilderMeasurementUtility.GetSymbol(
                        GetDisplayUnit(snapshot.BaseUnit)
                    ) +
                    "/día\nCobertura estimada: " +
                    snapshot.CoverageDays.ToString("0.0", CultureInfo.InvariantCulture) +
                    " días\nBase histórica: " + snapshot.ConsumptionHistoryDays +
                    " día(s) de partida.";
                break;

            case BistroBuilderInventoryForecastState.NoConsumption:
                detailForecastText.text =
                    "Previsión básica\nNo hay consumo registrado en " +
                    snapshot.ConsumptionHistoryDays +
                    " día(s) de partida. Sin cobertura calculable.";
                break;

            default:
                detailForecastText.text =
                    "Previsión básica\nSin historial suficiente. Se necesitan al menos " +
                    planningService.MinimumHistoryDaysForForecast +
                    " días de partida.";
                break;
        }
    }

    private string BuildAlertText()
    {
        if (alerts.Count == 0)
        {
            return "Sin alertas activas.";
        }

        var lines = new List<string>(alerts.Count);
        int shown = Math.Min(12, alerts.Count);
        for (int index = 0; index < shown; index++)
        {
            lines.Add("• " + alerts[index].Message);
        }

        if (alerts.Count > shown)
        {
            lines.Add("… y " + (alerts.Count - shown) + " más.");
        }

        return string.Join("\n", lines);
    }

    private static string BuildRowLabel(
        BistroBuilderInventoryPlanningSnapshot snapshot
    )
    {
        return snapshot.DisplayName + "\n" +
               FormatQuantity(snapshot.AvailableCanonicalMilliUnits, snapshot.BaseUnit) +
               " · " + TranslateStockLevel(snapshot.StockLevelState);
    }

    private bool ContainsIngredient(string ingredientId)
    {
        if (string.IsNullOrWhiteSpace(ingredientId))
        {
            return false;
        }

        for (int index = 0; index < snapshots.Count; index++)
        {
            if (string.Equals(
                    snapshots[index].IngredientId,
                    ingredientId,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryParseMinimum(
        string raw,
        BistroBuilderMeasurementUnit baseUnit,
        out long canonicalMilliUnits,
        out string error
    )
    {
        canonicalMilliUnits = 0L;
        error = string.Empty;
        string normalized = raw != null ? raw.Trim().Replace(',', '.') : string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Introduce un stock mínimo o 0 para desactivarlo.";
            return false;
        }

        if (!double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double amount
            ) || double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0d)
        {
            error = "El stock mínimo debe ser un número mayor o igual que cero.";
            return false;
        }

        if (amount == 0d)
        {
            canonicalMilliUnits = 0L;
            return true;
        }

        return BistroBuilderMeasurementUtility.TryConvertToCanonicalMilliUnits(
            amount,
            baseUnit,
            out canonicalMilliUnits,
            out error
        );
    }

    private static string FormatInputAmount(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        BistroBuilderMeasurementUnit displayUnit = GetDisplayUnit(baseUnit);
        double amount = BistroBuilderMeasurementUtility
            .ConvertCanonicalMilliUnitsToDisplayAmount(
                canonicalMilliUnits,
                displayUnit
            );
        return amount.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatQuantity(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        BistroBuilderMeasurementUnit displayUnit = GetDisplayUnit(baseUnit);
        double amount = BistroBuilderMeasurementUtility
            .ConvertCanonicalMilliUnitsToDisplayAmount(
                Math.Max(0L, canonicalMilliUnits),
                displayUnit
            );
        return amount.ToString("0.##", CultureInfo.InvariantCulture) + " " +
               BistroBuilderMeasurementUtility.GetSymbol(displayUnit);
    }

    private static double ConvertCanonicalDoubleToDisplay(
        double canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        if (canonicalMilliUnits <= 0d)
        {
            return 0d;
        }

        BistroBuilderMeasurementUnit displayUnit = GetDisplayUnit(baseUnit);
        double amount = canonicalMilliUnits /
                        BistroBuilderMeasurementUtility.MilliUnitsPerCanonicalUnit;
        if (displayUnit == BistroBuilderMeasurementUnit.Kilogram ||
            displayUnit == BistroBuilderMeasurementUnit.Liter)
        {
            amount /= 1000d;
        }
        return amount;
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

    private static string TranslateStockLevel(
        BistroBuilderInventoryStockLevelState state
    )
    {
        switch (state)
        {
            case BistroBuilderInventoryStockLevelState.Low:
                return "Bajo";
            case BistroBuilderInventoryStockLevelState.Critical:
                return "Crítico";
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                return "Sin stock";
            default:
                return "Normal";
        }
    }

    private static Color GetStockColor(
        BistroBuilderInventoryStockLevelState state
    )
    {
        switch (state)
        {
            case BistroBuilderInventoryStockLevelState.Low:
                return new Color(0.36f, 0.29f, 0.15f, 1f);
            case BistroBuilderInventoryStockLevelState.Critical:
                return new Color(0.42f, 0.19f, 0.14f, 1f);
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                return new Color(0.45f, 0.12f, 0.12f, 1f);
            default:
                return BistroBuilderMenuEditorUiFactory.SurfaceRaised;
        }
    }

    private static Color GetTextColor(
        BistroBuilderInventoryStockLevelState state
    )
    {
        switch (state)
        {
            case BistroBuilderInventoryStockLevelState.Low:
                return BistroBuilderMenuEditorUiFactory.Warning;
            case BistroBuilderInventoryStockLevelState.Critical:
            case BistroBuilderInventoryStockLevelState.OutOfStock:
                return BistroBuilderMenuEditorUiFactory.Negative;
            default:
                return BistroBuilderMenuEditorUiFactory.TextPrimary;
        }
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

    private void Subscribe()
    {
        if (subscribed || planningService == null)
        {
            return;
        }

        planningService.PlanningChanged += HandlePlanningChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (planningService != null)
        {
            planningService.PlanningChanged -= HandlePlanningChanged;
        }
        subscribed = false;
    }

    private void HandlePlanningChanged()
    {
        if (IsOpen)
        {
            Refresh(string.Empty);
        }
    }

    private void ResolveDependencies()
    {
        if (planningService == null)
        {
            planningService = FindFirstObjectByType<
                BistroBuilderInventoryPlanningService
            >();
        }
    }

    private static void SetRect(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY,
        float padding
    )
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
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
