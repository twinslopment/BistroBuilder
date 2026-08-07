using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Vista runtime de 2.1H/I para administrar varias cartas independientes y
/// reglas deterministas de temporada, evento, promoción y horario.
///
/// No edita platos: cada carta se abre posteriormente en el editor 2.1E.
/// Todas las operaciones se delegan en BistroBuilderMenuPortfolioService.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMenuPortfolioRuntimeView : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1HI-UI";

    [Header("Dependencias")]
    [SerializeField] private BistroBuilderMenuPortfolioService portfolioService;
    [SerializeField] private BistroBuilderMenuEditorRuntimeView menuEditorView;

    [Header("Comportamiento")]
    [SerializeField] private bool showOpenButton = true;

    private readonly List<Button> menuButtons = new List<Button>();
    private readonly List<Button> ruleButtons = new List<Button>();
    private readonly Toggle[] weekdayToggles = new Toggle[7];

    private Button openButton;
    private RectTransform modalRoot;
    private RectTransform menuContent;
    private RectTransform ruleContent;
    private Text headerText;
    private Text resolutionText;
    private Text statusText;
    private Text targetMenuText;
    private Text ruleTypeText;
    private Text signalText;
    private InputField menuNameInput;
    private InputField ruleNameInput;
    private InputField priorityInput;
    private InputField startDateInput;
    private InputField endDateInput;
    private InputField startTimeInput;
    private InputField endTimeInput;
    private InputField eventIdInput;
    private InputField promotionIdInput;
    private Toggle enabledToggle;
    private Toggle breakfastToggle;
    private Toggle lunchToggle;
    private Toggle dinnerToggle;

    private BistroBuilderRestaurantMenuPortfolioRuntimeState snapshot;
    private string selectedMenuId = string.Empty;
    private string selectedRuleId = string.Empty;
    private string selectedTargetMenuId = string.Empty;
    private BistroBuilderMenuActivationRuleType selectedRuleType =
        BistroBuilderMenuActivationRuleType.Schedule;
    private bool built;
    private bool subscribed;

    public BistroBuilderMenuPortfolioService PortfolioService => portfolioService;
    public BistroBuilderMenuEditorRuntimeView MenuEditorView => menuEditorView;
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
        if (openButton != null) openButton.gameObject.SetActive(showOpenButton);
    }

    private void Update()
    {
        if (!IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetVisible(false);
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (portfolioService == null || menuEditorView == null)
        {
            error = "Faltan las dependencias de la vista de cartas y reglas.";
            return false;
        }

        if (!portfolioService.ValidateConfiguration(out error)) return false;
        if (GetComponentInParent<Canvas>() == null)
        {
            error = "La vista 2.1H/I debe estar bajo un Canvas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryValidateVisibleContent(out string error)
    {
        EnsureVisualTree();
        bool wasOpen = IsOpen;
        if (!wasOpen)
        {
            SetVisible(true);
        }

        try
        {
            RefreshAll(string.Empty);
            Canvas.ForceUpdateCanvases();
            ScrollRect[] scrolls =
                modalRoot.GetComponentsInChildren<ScrollRect>(true);
            if (scrolls.Length < 2)
            {
                error = "La vista 2.1H/I necesita dos listas desplazables.";
                return false;
            }

            for (int index = 0; index < scrolls.Length; index++)
            {
                if (scrolls[index].viewport == null ||
                    scrolls[index].viewport.GetComponent<RectMask2D>() == null ||
                    scrolls[index].viewport.GetComponent<Mask>() != null)
                {
                    error = "Los scrolls de 2.1H/I deben usar RectMask2D.";
                    return false;
                }
            }

            if (snapshot == null || menuButtons.Count != snapshot.MenuCount)
            {
                error = "La lista visual no representa todas las cartas.";
                return false;
            }

            if (ruleButtons.Count != snapshot.RuleCount)
            {
                error = "La lista visual no representa todas las reglas.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        finally
        {
            if (!wasOpen)
            {
                SetVisible(false);
            }
        }
    }

    public bool TryOpen(out string error)
    {
        EnsureVisualTree();
        error = string.Empty;
        if (portfolioService == null)
        {
            error = "No está disponible el servicio de portfolios de carta.";
            ShowStatus(error, true);
            return false;
        }

        if (!portfolioService.ValidateConfiguration(out error))
        {
            ShowStatus(error, true);
            return false;
        }

        if (portfolioService.EditSessionService != null &&
            portfolioService.EditSessionService.HasOpenSession)
        {
            error = "Cierra o descarta primero el editor de carta.";
            ShowStatus(error, true);
            return false;
        }

        SetVisible(true);
        RefreshAll("Gestor de cartas preparado.");
        error = string.Empty;
        return true;
    }

    public void Close()
    {
        SetVisible(false);
    }

    private void EnsureVisualTree()
    {
        if (built) return;

        RectTransform host = transform as RectTransform;
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = Vector2.zero;

        openButton = BistroBuilderMenuEditorUiFactory.CreateButton(
            "OpenMenuPortfolio",
            host,
            "CARTAS",
            HandleOpen,
            new Color(0.17f, 0.24f, 0.20f, 1f),
            14
        );
        RectTransform openRect = openButton.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(0f, 1f);
        openRect.anchorMax = new Vector2(0f, 1f);
        openRect.pivot = new Vector2(0f, 1f);
        openRect.anchoredPosition = new Vector2(144f, -18f);
        openRect.sizeDelta = new Vector2(118f, 40f);

        modalRoot = BistroBuilderMenuEditorUiFactory.CreateRect(
            "MenuPortfolioModal",
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
            new Vector2(28f, 24f),
            new Vector2(-28f, -24f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            panel,
            BistroBuilderMenuEditorUiFactory.Surface
        );

        BuildHeader(panel);
        BuildMenusPanel(panel);
        BuildRulesPanel(panel);
        BuildRuleEditor(panel);
        BuildFooter(panel);
        built = true;
    }

    private void BuildHeader(RectTransform panel)
    {
        RectTransform header = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Header", panel,
            new Vector2(0f, 1f), Vector2.one,
            new Vector2(16f, -66f), new Vector2(-16f, -10f)
        );
        BistroBuilderMenuEditorUiFactory.AddImage(
            header,
            BistroBuilderMenuEditorUiFactory.SurfaceRaised
        );

        headerText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Title", header, "Cartas, temporadas y promociones", 23,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            FontStyle.Bold
        );
        SetRect(headerText.rectTransform, 0.015f, 0f, 0.47f, 1f, 0f);

        resolutionText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Resolution", header, string.Empty, 13,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(resolutionText.rectTransform, 0.48f, 0f, 0.83f, 1f, 4f);

        Button close = BistroBuilderMenuEditorUiFactory.CreateButton(
            "Close", header, "Cerrar", Close,
            new Color(0.25f, 0.15f, 0.14f, 1f), 14
        );
        SetRect(close.GetComponent<RectTransform>(), 0.86f, 0.14f, 0.985f, 0.86f, 0f);
    }

    private void BuildMenusPanel(RectTransform panel)
    {
        RectTransform root = CreateCard(
            "Menus", panel,
            new Vector2(0f, 0f), new Vector2(0.30f, 1f),
            new Vector2(16f, 78f), new Vector2(-6f, -78f)
        );
        AddTitle(root, "CARTAS INDEPENDIENTES", 0.92f, 1f);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "MenuList", root, out menuContent
        );
        SetRect(scroll.GetComponent<RectTransform>(), 0.03f, 0.43f, 0.97f, 0.90f, 0f);

        menuNameInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "MenuName", root, "Nombre de la carta", null, null
        );
        SetRect(menuNameInput.GetComponent<RectTransform>(), 0.03f, 0.35f, 0.97f, 0.41f, 0f);

        Button create = MakeButton(root, "Crear desde activa", CreateMenu, 0.03f, 0.27f, 0.48f, 0.33f, true);
        Button duplicate = MakeButton(root, "Duplicar", DuplicateMenu, 0.52f, 0.27f, 0.97f, 0.33f, false);
        Button rename = MakeButton(root, "Renombrar", RenameMenu, 0.03f, 0.19f, 0.48f, 0.25f, false);
        Button remove = MakeButton(root, "Eliminar", DeleteMenu, 0.52f, 0.19f, 0.97f, 0.25f, false);
        Button fallback = MakeButton(root, "Fijar como base", SetFallback, 0.03f, 0.11f, 0.48f, 0.17f, false);
        Button manual = MakeButton(root, "Activar manual", SetManual, 0.52f, 0.11f, 0.97f, 0.17f, true);
        Button automatic = MakeButton(root, "Reglas automáticas", ClearManual, 0.03f, 0.03f, 0.48f, 0.09f, false);
        Button edit = MakeButton(root, "Editar carta activa", OpenActiveMenuEditor, 0.52f, 0.03f, 0.97f, 0.09f, true);
        _ = create; _ = duplicate; _ = rename; _ = remove; _ = fallback; _ = manual; _ = automatic; _ = edit;
    }

    private void BuildRulesPanel(RectTransform panel)
    {
        RectTransform root = CreateCard(
            "Rules", panel,
            new Vector2(0.30f, 0f), new Vector2(0.58f, 1f),
            new Vector2(6f, 78f), new Vector2(-6f, -78f)
        );
        AddTitle(root, "REGLAS DE ACTIVACIÓN", 0.92f, 1f);

        ScrollRect scroll = BistroBuilderMenuEditorUiFactory.CreateScrollView(
            "RuleList", root, out ruleContent
        );
        SetRect(scroll.GetComponent<RectTransform>(), 0.03f, 0.34f, 0.97f, 0.90f, 0f);

        signalText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Signals", root, string.Empty, 12, TextAnchor.UpperLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(signalText.rectTransform, 0.04f, 0.26f, 0.96f, 0.33f, 0f);

        eventIdInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "EventId", root, "event_id", null, null
        );
        SetRect(eventIdInput.GetComponent<RectTransform>(), 0.03f, 0.19f, 0.55f, 0.25f, 0f);
        MakeButton(root, "+ Evento", ActivateEvent, 0.57f, 0.19f, 0.76f, 0.25f, true);
        MakeButton(root, "−", DeactivateEvent, 0.78f, 0.19f, 0.97f, 0.25f, false);

        promotionIdInput = BistroBuilderMenuEditorUiFactory.CreateInputField(
            "PromotionId", root, "promotion_id", null, null
        );
        SetRect(promotionIdInput.GetComponent<RectTransform>(), 0.03f, 0.11f, 0.55f, 0.17f, 0f);
        MakeButton(root, "+ Promo", ActivatePromotion, 0.57f, 0.11f, 0.76f, 0.17f, true);
        MakeButton(root, "−", DeactivatePromotion, 0.78f, 0.11f, 0.97f, 0.17f, false);

        MakeButton(root, "Nueva regla", NewRule, 0.03f, 0.03f, 0.48f, 0.09f, true);
        MakeButton(root, "Eliminar regla", DeleteRule, 0.52f, 0.03f, 0.97f, 0.09f, false);
    }

    private void BuildRuleEditor(RectTransform panel)
    {
        RectTransform root = CreateCard(
            "RuleEditor", panel,
            new Vector2(0.58f, 0f), Vector2.one,
            new Vector2(6f, 78f), new Vector2(-16f, -78f)
        );
        AddTitle(root, "DETALLE DE REGLA", 0.92f, 1f);

        ruleNameInput = CreateField(root, "RuleName", "Nombre de la regla", 0.03f, 0.84f, 0.68f, 0.90f);
        priorityInput = CreateField(root, "Priority", "Prioridad (-1000..1000)", 0.71f, 0.84f, 0.97f, 0.90f);

        Button typeButton = MakeButton(root, "Tipo", CycleRuleType, 0.03f, 0.76f, 0.48f, 0.82f, false);
        ruleTypeText = typeButton.GetComponentInChildren<Text>();
        Button targetButton = MakeButton(root, "Carta destino", CycleTargetMenu, 0.52f, 0.76f, 0.97f, 0.82f, false);
        targetMenuText = targetButton.GetComponentInChildren<Text>();

        enabledToggle = MakeToggle(root, "Enabled", "Regla activa", 0.03f, 0.70f, 0.35f, 0.74f);
        breakfastToggle = MakeToggle(root, "Breakfast", "Desayuno", 0.36f, 0.70f, 0.56f, 0.74f);
        lunchToggle = MakeToggle(root, "Lunch", "Comida", 0.57f, 0.70f, 0.75f, 0.74f);
        dinnerToggle = MakeToggle(root, "Dinner", "Cena", 0.76f, 0.70f, 0.97f, 0.74f);

        AddMiniLabel(root, "Fechas inclusivas (vacío = cualquiera)", 0.03f, 0.65f, 0.97f, 0.69f);
        startDateInput = CreateField(root, "StartDate", "Inicio YYYY-MM-DD", 0.03f, 0.59f, 0.48f, 0.64f);
        endDateInput = CreateField(root, "EndDate", "Fin YYYY-MM-DD", 0.52f, 0.59f, 0.97f, 0.64f);

        AddMiniLabel(root, "Franja horaria (vacío = cualquiera; admite noche)", 0.03f, 0.54f, 0.97f, 0.58f);
        startTimeInput = CreateField(root, "StartTime", "Inicio HH:mm", 0.03f, 0.48f, 0.48f, 0.53f);
        endTimeInput = CreateField(root, "EndTime", "Fin HH:mm", 0.52f, 0.48f, 0.97f, 0.53f);

        AddMiniLabel(root, "Días de la semana (ninguno = cualquiera)", 0.03f, 0.43f, 0.97f, 0.47f);
        string[] labels = { "D", "L", "M", "X", "J", "V", "S" };
        for (int index = 0; index < weekdayToggles.Length; index++)
        {
            float left = 0.03f + index * 0.135f;
            weekdayToggles[index] = MakeToggle(
                root, "Weekday" + index, labels[index],
                left, 0.37f, left + 0.12f, 0.42f
            );
        }

        AddMiniLabel(root, "Condiciones opcionales", 0.03f, 0.32f, 0.97f, 0.36f);
        InputField requiredEvent = CreateField(root, "RequiredEvent", "EventId requerido", 0.03f, 0.26f, 0.48f, 0.31f);
        InputField requiredPromotion = CreateField(root, "RequiredPromotion", "PromotionId requerido", 0.52f, 0.26f, 0.97f, 0.31f);
        // Reutilizamos referencias exclusivas para la regla mediante campos privados auxiliares.
        ruleEventInput = requiredEvent;
        rulePromotionInput = requiredPromotion;

        AddMiniLabel(
            root,
            "Desempate: prioridad → especificidad → RuleId estable.",
            0.03f, 0.19f, 0.97f, 0.24f
        );
        MakeButton(root, "Guardar regla", SaveRule, 0.03f, 0.10f, 0.97f, 0.17f, true);
        MakeButton(root, "Limpiar formulario", NewRule, 0.03f, 0.03f, 0.97f, 0.09f, false);
    }

    private InputField ruleEventInput;
    private InputField rulePromotionInput;

    private void BuildFooter(RectTransform panel)
    {
        statusText = BistroBuilderMenuEditorUiFactory.CreateText(
            "Status", panel, "Sin cambios.", 13, TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextSecondary
        );
        SetRect(statusText.rectTransform, 0.02f, 0.015f, 0.98f, 0.08f, 0f);
    }

    private void RefreshAll(string message)
    {
        string error = string.Empty;
        if (portfolioService == null)
        {
            ShowStatus("No está disponible el servicio de portfolios de carta.", true);
            return;
        }

        if (!portfolioService.TryGetActivePortfolioSnapshot(out snapshot, out error))
        {
            ShowStatus(error, true);
            return;
        }

        if (string.IsNullOrEmpty(selectedMenuId) || !snapshot.TryGetMenu(selectedMenuId, out _))
        {
            selectedMenuId = snapshot.ActiveMenuId;
        }
        if (!string.IsNullOrEmpty(selectedRuleId) && !snapshot.TryGetRule(selectedRuleId, out _))
        {
            selectedRuleId = string.Empty;
        }
        if (string.IsNullOrEmpty(selectedTargetMenuId) || !snapshot.TryGetMenu(selectedTargetMenuId, out _))
        {
            selectedTargetMenuId = selectedMenuId;
        }

        RebuildMenuRows();
        RebuildRuleRows();
        RefreshHeader();
        RefreshSignals();
        RefreshRuleEditorFromSelection();
        ShowStatus(message, false);
    }

    private void RebuildMenuRows()
    {
        ClearRows(menuContent, menuButtons);
        if (snapshot == null) return;

        for (int index = 0; index < snapshot.Menus.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState menu = snapshot.Menus[index];
            string suffix = string.Empty;
            if (menu.MenuId == snapshot.ActiveMenuId) suffix += " · ACTIVA";
            if (menu.MenuId == snapshot.FallbackMenuId) suffix += " · BASE";
            if (menu.MenuId == snapshot.ManualOverrideMenuId) suffix += " · MANUAL";
            string id = menu.MenuId;
            Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
                "Menu_" + id,
                menuContent,
                menu.DisplayName + suffix + "\n" + menu.ItemCount + " platos · rev " + menu.Revision,
                () => SelectMenu(id),
                id == selectedMenuId
                    ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                    : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                13
            );
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 58f);
            menuButtons.Add(button);
        }
    }

    private void RebuildRuleRows()
    {
        ClearRows(ruleContent, ruleButtons);
        if (snapshot == null) return;

        for (int index = 0; index < snapshot.Rules.Count; index++)
        {
            BistroBuilderMenuActivationRuleRuntimeState rule = snapshot.Rules[index];
            string id = rule.RuleId;
            string label = (rule.Enabled ? "● " : "○ ") + rule.DisplayName +
                "\nP" + rule.Priority + " · " + GetTypeLabel(rule.RuleType) +
                " → " + GetMenuName(rule.TargetMenuId);
            Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
                "Rule_" + id,
                ruleContent,
                label,
                () => SelectRule(id),
                id == selectedRuleId
                    ? BistroBuilderMenuEditorUiFactory.SurfaceSelected
                    : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
                12
            );
            BistroBuilderMenuEditorUiFactory.SetLayoutHeight(button, 58f);
            ruleButtons.Add(button);
        }
    }

    private void RefreshHeader()
    {
        if (snapshot == null) return;
        portfolioService.TryResolveCurrent(
            out BistroBuilderMenuResolutionResult resolution,
            out _,
            out _
        );
        headerText.text = "Cartas y reglas · " + snapshot.MenuCount + " cartas · " + snapshot.RuleCount + " reglas";
        resolutionText.text = "Efectiva: " + GetMenuName(snapshot.ActiveMenuId) +
            "\n" + resolution.Description;
    }

    private void RefreshSignals()
    {
        if (portfolioService == null || portfolioService.ContextService == null) return;
        BistroBuilderMenuActivationContextService context = portfolioService.ContextService;
        signalText.text = "Eventos: " + JoinOrNone(context.ActiveEventIds) +
            "\nPromociones: " + JoinOrNone(context.ActivePromotionIds);
    }

    private void RefreshRuleEditorFromSelection()
    {
        if (snapshot != null && !string.IsNullOrEmpty(selectedRuleId) &&
            snapshot.TryGetRule(selectedRuleId, out BistroBuilderMenuActivationRuleRuntimeState rule))
        {
                ruleNameInput.text = rule.DisplayName;
                priorityInput.text = rule.Priority.ToString(CultureInfo.InvariantCulture);
                selectedTargetMenuId = rule.TargetMenuId;
                selectedRuleType = rule.RuleType;
                startDateInput.text = FormatDateKey(rule.StartDateKey);
                endDateInput.text = FormatDateKey(rule.EndDateKey);
                startTimeInput.text = FormatMinute(rule.StartMinute);
                endTimeInput.text = FormatMinute(rule.EndMinute);
                ruleEventInput.text = rule.RequiredEventId;
                rulePromotionInput.text = rule.RequiredPromotionId;
                enabledToggle.isOn = rule.Enabled;
                breakfastToggle.isOn = (rule.MealServices & BistroBuilderMealServiceAvailability.Breakfast) != 0;
                lunchToggle.isOn = (rule.MealServices & BistroBuilderMealServiceAvailability.Lunch) != 0;
                dinnerToggle.isOn = (rule.MealServices & BistroBuilderMealServiceAvailability.Dinner) != 0;
                for (int index = 0; index < weekdayToggles.Length; index++)
                {
                    weekdayToggles[index].isOn = (rule.WeekdayMask & (1 << index)) != 0;
                }
        }
        else
        {
            ruleNameInput.text = string.Empty;
            priorityInput.text = "0";
            startDateInput.text = string.Empty;
            endDateInput.text = string.Empty;
            startTimeInput.text = string.Empty;
            endTimeInput.text = string.Empty;
            ruleEventInput.text = string.Empty;
            rulePromotionInput.text = string.Empty;
            enabledToggle.isOn = true;
            breakfastToggle.isOn = false;
            lunchToggle.isOn = false;
            dinnerToggle.isOn = false;
            for (int index = 0; index < weekdayToggles.Length; index++)
            {
                weekdayToggles[index].isOn = false;
            }
        }

        UpdateRuleButtonLabels();
    }

    private void SelectMenu(string menuId)
    {
        selectedMenuId = menuId;
        selectedTargetMenuId = menuId;
        if (snapshot != null && snapshot.TryGetMenu(menuId, out BistroBuilderNamedMenuRuntimeState menu))
        {
            menuNameInput.text = menu.DisplayName;
        }
        RebuildMenuRows();
        UpdateRuleButtonLabels();
    }

    private void SelectRule(string ruleId)
    {
        selectedRuleId = ruleId;
        RefreshRuleEditorFromSelection();
        RebuildRuleRows();
    }

    private void CreateMenu()
    {
        string name = menuNameInput.text;
        if (portfolioService.TryCreateMenuFromActive(name, true, out string id, out string error))
        {
            selectedMenuId = id;
            selectedTargetMenuId = id;
            RefreshAll("Carta creada y activada manualmente.");
        }
        else ShowStatus(error, true);
    }

    private void DuplicateMenu()
    {
        if (portfolioService.TryDuplicateMenu(selectedMenuId, menuNameInput.text, true, out string id, out string error))
        {
            selectedMenuId = id;
            selectedTargetMenuId = id;
            RefreshAll("Carta duplicada y activada manualmente.");
        }
        else ShowStatus(error, true);
    }

    private void RenameMenu()
    {
        if (portfolioService.TryRenameMenu(selectedMenuId, menuNameInput.text, out string error))
            RefreshAll("Carta renombrada.");
        else ShowStatus(error, true);
    }

    private void DeleteMenu()
    {
        if (portfolioService.TryDeleteMenu(selectedMenuId, out string error))
        {
            selectedMenuId = string.Empty;
            selectedTargetMenuId = string.Empty;
            RefreshAll("Carta eliminada.");
        }
        else ShowStatus(error, true);
    }

    private void SetFallback()
    {
        if (portfolioService.TrySetFallbackMenu(selectedMenuId, out string error)) RefreshAll("Carta base actualizada.");
        else ShowStatus(error, true);
    }

    private void SetManual()
    {
        if (portfolioService.TrySetManualOverride(selectedMenuId, out string error)) RefreshAll("Carta activada manualmente.");
        else ShowStatus(error, true);
    }

    private void ClearManual()
    {
        if (portfolioService.TryClearManualOverride(out string error)) RefreshAll("Resolución automática reactivada.");
        else ShowStatus(error, true);
    }

    private void OpenActiveMenuEditor()
    {
        if (snapshot == null ||
            !string.Equals(
                selectedMenuId,
                snapshot.ActiveMenuId,
                StringComparison.Ordinal
            ))
        {
            ShowStatus(
                "Activa primero la carta seleccionada antes de editarla.",
                true
            );
            return;
        }

        Close();
        string error = string.Empty;
        if (menuEditorView == null)
        {
            ShowStatus("No está disponible el editor de carta.", true);
            return;
        }

        if (!menuEditorView.TryOpenFromInterface(out error))
        {
            ShowStatus(
                string.IsNullOrWhiteSpace(error)
                    ? "No se pudo abrir el editor de carta."
                    : error,
                true
            );
        }
    }

    private void NewRule()
    {
        selectedRuleId = string.Empty;
        selectedRuleType = BistroBuilderMenuActivationRuleType.Schedule;
        selectedTargetMenuId = string.IsNullOrEmpty(selectedMenuId)
            ? (snapshot != null ? snapshot.FallbackMenuId : string.Empty)
            : selectedMenuId;
        RefreshRuleEditorFromSelection();
        RebuildRuleRows();
        ShowStatus("Formulario de regla limpio.", false);
    }

    private void SaveRule()
    {
        if (!TryParseInt(priorityInput.text, 0, out int priority) ||
            priority < BistroBuilderMenuActivationRuleRuntimeState.MinimumPriority ||
            priority > BistroBuilderMenuActivationRuleRuntimeState.MaximumPriority)
        {
            ShowStatus("La prioridad debe estar entre -1000 y 1000.", true);
            return;
        }

        if (!TryParseDateKey(startDateInput.text, out int startDate, out string error) ||
            !TryParseDateKey(endDateInput.text, out int endDate, out error) ||
            !TryParseMinute(startTimeInput.text, out int startMinute, out error) ||
            !TryParseMinute(endTimeInput.text, out int endMinute, out error))
        {
            ShowStatus(error, true);
            return;
        }

        int weekdayMask = 0;
        for (int index = 0; index < weekdayToggles.Length; index++)
        {
            if (weekdayToggles[index].isOn) weekdayMask |= 1 << index;
        }
        BistroBuilderMealServiceAvailability services = BistroBuilderMealServiceAvailability.None;
        if (breakfastToggle.isOn) services |= BistroBuilderMealServiceAvailability.Breakfast;
        if (lunchToggle.isOn) services |= BistroBuilderMealServiceAvailability.Lunch;
        if (dinnerToggle.isOn) services |= BistroBuilderMealServiceAvailability.Dinner;

        string ruleId = string.IsNullOrEmpty(selectedRuleId)
            ? "rule_" + Guid.NewGuid().ToString("N").Substring(0, 16)
            : selectedRuleId;
        BistroBuilderMenuActivationRuleRuntimeState rule =
            new BistroBuilderMenuActivationRuleRuntimeState(
                ruleId,
                ruleNameInput.text,
                enabledToggle.isOn,
                selectedTargetMenuId,
                priority,
                selectedRuleType,
                startDate,
                endDate,
                weekdayMask,
                services,
                startMinute,
                endMinute,
                ruleEventInput.text,
                rulePromotionInput.text
            );

        if (portfolioService.TryUpsertRule(rule, out error))
        {
            selectedRuleId = ruleId;
            RefreshAll("Regla guardada y resolución recalculada.");
        }
        else ShowStatus(error, true);
    }

    private void DeleteRule()
    {
        if (string.IsNullOrEmpty(selectedRuleId))
        {
            ShowStatus("Selecciona una regla para eliminarla.", true);
            return;
        }
        if (portfolioService.TryDeleteRule(selectedRuleId, out string error))
        {
            selectedRuleId = string.Empty;
            RefreshAll("Regla eliminada.");
        }
        else ShowStatus(error, true);
    }

    private void CycleRuleType()
    {
        selectedRuleType = (BistroBuilderMenuActivationRuleType)(((int)selectedRuleType + 1) % 5);
        UpdateRuleButtonLabels();
    }

    private void CycleTargetMenu()
    {
        if (snapshot == null || snapshot.MenuCount == 0) return;
        int current = -1;
        for (int index = 0; index < snapshot.Menus.Count; index++)
        {
            if (snapshot.Menus[index].MenuId == selectedTargetMenuId) current = index;
        }
        selectedTargetMenuId = snapshot.Menus[(current + 1) % snapshot.MenuCount].MenuId;
        UpdateRuleButtonLabels();
    }

    private void UpdateRuleButtonLabels()
    {
        if (ruleTypeText != null) ruleTypeText.text = "Tipo: " + GetTypeLabel(selectedRuleType);
        if (targetMenuText != null) targetMenuText.text = "Destino: " + GetMenuName(selectedTargetMenuId);
    }

    private void ActivateEvent() => SetSignal(eventIdInput.text, true, true);
    private void DeactivateEvent() => SetSignal(eventIdInput.text, false, true);
    private void ActivatePromotion() => SetSignal(promotionIdInput.text, true, false);
    private void DeactivatePromotion() => SetSignal(promotionIdInput.text, false, false);

    private void SetSignal(string id, bool active, bool isEvent)
    {
        string error;
        bool ok;
        if (isEvent)
        {
            ok = portfolioService.ContextService.TrySetEventActive(
                id,
                active,
                out error
            );
        }
        else
        {
            ok = portfolioService.ContextService.TrySetPromotionActive(
                id,
                active,
                out error
            );
        }

        if (ok)
        {
            RefreshAll(
                (isEvent ? "Evento " : "Promoción ") +
                (active ? "activado/a." : "desactivado/a.")
            );
        }
        else
        {
            ShowStatus(error, true);
        }
    }

    private void HandleOpen()
    {
        TryOpen(out _);
    }

    private void HandlePortfolioChanged()
    {
        if (IsOpen) RefreshAll(string.Empty);
    }

    private void HandleActiveMenuChanged(BistroBuilderMenuResolutionResult result)
    {
        if (IsOpen) RefreshAll("Carta efectiva actualizada: " + GetMenuName(result.MenuId) + ".");
    }

    private void Subscribe()
    {
        if (subscribed || portfolioService == null) return;
        portfolioService.PortfolioChanged += HandlePortfolioChanged;
        portfolioService.ActiveMenuChanged += HandleActiveMenuChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || portfolioService == null) return;
        portfolioService.PortfolioChanged -= HandlePortfolioChanged;
        portfolioService.ActiveMenuChanged -= HandleActiveMenuChanged;
        subscribed = false;
    }

    private void ResolveDependencies()
    {
        if (portfolioService == null) portfolioService = FindFirstObjectByType<BistroBuilderMenuPortfolioService>();
        if (menuEditorView == null) TryGetComponent(out menuEditorView);
    }

    private void SetVisible(bool visible)
    {
        if (modalRoot != null)
        {
            modalRoot.gameObject.SetActive(visible);
            if (visible) modalRoot.SetAsLastSibling();
        }
        if (openButton != null) openButton.gameObject.SetActive(showOpenButton && !visible);
    }

    private void ShowStatus(string value, bool warning)
    {
        if (statusText == null) return;
        statusText.text = string.IsNullOrWhiteSpace(value) ? "Sin cambios." : value;
        statusText.color = warning
            ? BistroBuilderMenuEditorUiFactory.Warning
            : BistroBuilderMenuEditorUiFactory.TextSecondary;
    }

    private string GetMenuName(string menuId)
    {
        if (snapshot != null && snapshot.TryGetMenu(menuId, out BistroBuilderNamedMenuRuntimeState menu)) return menu.DisplayName;
        return string.IsNullOrEmpty(menuId) ? "—" : menuId;
    }

    private static string GetTypeLabel(BistroBuilderMenuActivationRuleType type)
    {
        switch (type)
        {
            case BistroBuilderMenuActivationRuleType.Season: return "Temporada";
            case BistroBuilderMenuActivationRuleType.Event: return "Evento";
            case BistroBuilderMenuActivationRuleType.Promotion: return "Promoción";
            case BistroBuilderMenuActivationRuleType.Composite: return "Compuesta";
            default: return "Horario";
        }
    }

    private static string JoinOrNone(IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0) return "ninguno";
        return string.Join(", ", values);
    }

    private static string FormatDateKey(int value)
    {
        if (value == 0) return string.Empty;
        return (value / 10000).ToString("0000") + "-" +
            (value / 100 % 100).ToString("00") + "-" +
            (value % 100).ToString("00");
    }

    private static string FormatMinute(int value)
    {
        if (value == BistroBuilderMenuActivationRuleRuntimeState.AnyMinute) return string.Empty;
        return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
    }

    private static bool TryParseDateKey(string text, out int value, out string error)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = string.Empty;
            return true;
        }
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            error = "Usa fechas con formato YYYY-MM-DD.";
            return false;
        }
        value = date.Year * 10000 + date.Month * 100 + date.Day;
        error = string.Empty;
        return true;
    }

    private static bool TryParseMinute(string text, out int value, out string error)
    {
        value = BistroBuilderMenuActivationRuleRuntimeState.AnyMinute;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = string.Empty;
            return true;
        }
        if (!TimeSpan.TryParseExact(text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan time) || time.TotalMinutes > 1439)
        {
            error = "Usa horas con formato HH:mm entre 00:00 y 23:59.";
            return false;
        }
        value = (int)time.TotalMinutes;
        error = string.Empty;
        return true;
    }

    private static bool TryParseInt(string text, int fallback, out int value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = fallback;
            return true;
        }
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static RectTransform CreateCard(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = BistroBuilderMenuEditorUiFactory.CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        BistroBuilderMenuEditorUiFactory.AddImage(rect, new Color(0.07f, 0.078f, 0.073f, 1f));
        return rect;
    }

    private static void AddTitle(Transform parent, string text, float minY, float maxY)
    {
        Text label = BistroBuilderMenuEditorUiFactory.CreateText("Title", parent, text, 14, TextAnchor.MiddleLeft, BistroBuilderMenuEditorUiFactory.Accent, FontStyle.Bold);
        SetRect(label.rectTransform, 0.04f, minY, 0.96f, maxY, 0f);
    }

    private static void AddMiniLabel(Transform parent, string text, float minX, float minY, float maxX, float maxY)
    {
        Text label = BistroBuilderMenuEditorUiFactory.CreateText("Hint", parent, text, 11, TextAnchor.MiddleLeft, BistroBuilderMenuEditorUiFactory.TextSecondary);
        SetRect(label.rectTransform, minX, minY, maxX, maxY, 0f);
    }

    private static InputField CreateField(Transform parent, string name, string placeholder, float minX, float minY, float maxX, float maxY)
    {
        InputField input = BistroBuilderMenuEditorUiFactory.CreateInputField(name, parent, placeholder, null, null);
        SetRect(input.GetComponent<RectTransform>(), minX, minY, maxX, maxY, 0f);
        return input;
    }

    private static Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action, float minX, float minY, float maxX, float maxY, bool positive)
    {
        Button button = BistroBuilderMenuEditorUiFactory.CreateButton(
            label.Replace(" ", string.Empty), parent, label, action,
            positive ? BistroBuilderMenuEditorUiFactory.Positive : BistroBuilderMenuEditorUiFactory.SurfaceRaised,
            12
        );
        SetRect(button.GetComponent<RectTransform>(), minX, minY, maxX, maxY, 0f);
        return button;
    }

    private static Toggle MakeToggle(Transform parent, string name, string label, float minX, float minY, float maxX, float maxY)
    {
        Toggle toggle = BistroBuilderMenuEditorUiFactory.CreateToggle(name, parent, label, null);
        SetRect(toggle.GetComponent<RectTransform>(), minX, minY, maxX, maxY, 0f);
        return toggle;
    }

    private static void SetRect(RectTransform rect, float minX, float minY, float maxX, float maxY, float inset)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void ClearRows(RectTransform content, List<Button> pool)
    {
        pool.Clear();
        if (content == null) return;
        for (int index = content.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.Destroy(content.GetChild(index).gameObject);
        }
    }
}
