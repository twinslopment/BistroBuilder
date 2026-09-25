using System;
using System.Globalization;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    private static readonly BBIconId[] TopIcons = {
        BBIconId.NavActivity, BBIconId.NavStaff, BBIconId.NavMenu,
        BBIconId.NavInventory, BBIconId.NavSuppliers, BBIconId.NavReservations,
        BBIconId.NavEconomy, BBIconId.NavMarketing, BBIconId.NavReputation
    };
    private readonly System.Collections.Generic.Dictionary<string, BBIconButton> topPresenters = new();
    private TMP_Text restaurantHeading, serviceHeading, calendarHeading, timeHeading;
    private BistroBuilderGeneralGameStateService topGameState;
    private GameClock topClock;
    private Button optionsButton, identityButton;
    private RectTransform topPopup;
    private string selectedNavigation = "Actividad";
    private bool dismissTopPopup;
    private TMP_FontAsset topBrandFont;
    private static readonly Color HeaderInk = BistroBuilderUiTokens.Background;
    private static readonly Color HeaderText = new Color(0.89f, 0.90f, 0.88f);

    private void EnsureIconNavigationContent()
    {
        EnsureApprovedTopBarV3Content();
    }

    private void EnsureIconNavigationButtons()
    {
        EnsureApprovedTopBarV3Buttons();
    }

    private Button HeaderIconButton(Transform parent, string name, string title, BBIconId id)
    {
        Button button = HeaderPlainButton(parent, name, title, 72);
        var layout = button.GetComponent<LayoutElement>();
        layout.minWidth = 62; layout.preferredWidth = 90; layout.flexibleWidth = 1;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (BistroBuilderTypography.Body != null) label.font = BistroBuilderTypography.Body;
        label.gameObject.SetActive(true);
        label.fontSize = 12; label.fontStyle = FontStyles.Normal; label.color = BBIconDesignTokens.Muted;
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0, 0); labelRect.anchorMax = new Vector2(1, 0);
        labelRect.pivot = new Vector2(0.5f, 0);
        labelRect.offsetMin = new Vector2(0, 8); labelRect.offsetMax = new Vector2(0, 27);
        var glow = HeaderImage(button.transform, "HoverGlow");
        glow.preserveAspect = false;
        glow.rectTransform.anchorMin = Vector2.zero; glow.rectTransform.anchorMax = Vector2.one;
        glow.rectTransform.offsetMin = new Vector2(3, 3); glow.rectTransform.offsetMax = new Vector2(-3, -3);
        glow.color = new Color(BBIconDesignTokens.SelectedSoft.r, BBIconDesignTokens.SelectedSoft.g, BBIconDesignTokens.SelectedSoft.b, 0f);
        var glowOutline = glow.GetComponent<Outline>();
        if (glowOutline == null) glowOutline = glow.gameObject.AddComponent<Outline>();
        glowOutline.effectColor = new Color(BBIconDesignTokens.Selected.r, BBIconDesignTokens.Selected.g, BBIconDesignTokens.Selected.b, 0f);
        glowOutline.effectDistance = new Vector2(1.2f, -1.2f);
        glowOutline.useGraphicAlpha = false;
        glow.transform.SetAsFirstSibling();

        var icon = HeaderImage(button.transform, "NavigationIcon");
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1);
        icon.rectTransform.pivot = new Vector2(0.5f, 1);
        icon.rectTransform.sizeDelta = new Vector2(23, 23); icon.rectTransform.anchoredPosition = new Vector2(0, -9);
        icon.rectTransform.localScale = Vector3.one;
        icon.rectTransform.localRotation = Quaternion.identity;
        var underline = HeaderImage(button.transform, "SelectionUnderline");
        underline.rectTransform.anchorMin = Vector2.zero; underline.rectTransform.anchorMax = new Vector2(1, 0);
        underline.rectTransform.offsetMin = new Vector2(7, 0); underline.rectTransform.offsetMax = new Vector2(-7, 3);
        var fx = button.GetComponent<BBIconButton>();
        if (fx == null) fx = button.gameObject.AddComponent<BBIconButton>();
        fx.ConfigureRuntime(id, icon, button);
        fx.ConfigureNavigationSurface(button.GetComponent<Image>(), underline, label, glow, glowOutline);
        fx.SetToggleSelectionOnClick(false);
        return button;
    }

    private static Button HeaderPlainButton(Transform parent, string name, string title, float width)
    {
        var button = EnsureButton(parent, name, title, width);
        button.transition = Selectable.Transition.None;
        button.GetComponent<Image>().color = Color.clear;
        var oldMotion = button.GetComponent<BistroBuilderUiSelectableMotion>();
        if (oldMotion != null) oldMotion.enabled = false;
        return button;
    }
    private static TMP_Text HeaderLabel(Transform parent, string name, string text, float size)
    {
        var found = parent.Find(name);
        var go = found != null ? found.gameObject : NewUi(name, parent);
        go.SetActive(true);
        var label = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        if (BistroBuilderTypography.Body != null) label.font = BistroBuilderTypography.Body;
        label.text = text; label.fontSize = size; label.color = HeaderText;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
    private static Image HeaderImage(Transform parent, string name)
    {
        var found = parent.Find(name);
        var go = found != null ? found.gameObject : NewUi(name, parent);
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.raycastTarget = false; image.preserveAspect = true; image.useSpriteMesh = true;
        return image;
    }
    private static void PlaceHeader(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private void HeaderDivider(string name, float x)
    {
        var image = HeaderImage(topNavigation, name);
        image.color = new Color(0.25f, 0.30f, 0.31f, 0.7f);
        PlaceHeader(image.rectTransform, x, 10, 1, 44);
    }

    private void EnsureTopPopup()
    {
        if (topPopup != null) return;
        topPopup = (RectTransform)NewUi("TopNavigationMenu", topNavigation).transform;
        var popupImage = topPopup.gameObject.AddComponent<Image>();
        popupImage.color = BistroBuilderUiTokens.SurfaceElevated;
        BistroBuilderSurface.Apply(popupImage, BistroBuilderSurfaceLevel.Floating);
        topPopup.pivot = new Vector2(1, 1);
        topPopup.sizeDelta = new Vector2(260, 413);
        var layout = topPopup.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 5;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        AddTopAction("Edición del local", () => HandleEditModeClicked());
        AddTopAction("Progreso", () => OpenDirectNavigation("Progreso"));
        AddTopAction("Comandas", () => InvokeLegacyTop("OpenAdvancedOrdersButton"));
        AddTopAction("Cocina", () => InvokeLegacyTop("OpenAdvancedKitchenButton"));
        AddTopAction("Camareros", () => InvokeLegacyTop("OpenWaiterOperations"));
        AddTopAction("Sala", () => InvokeLegacyTop("OpenFrontOfHouseOperations"));
        AddTopAction("Cierre del día", () => InvokeLegacyTop("OpenEndOfDayOperations"));
        AddTopAction("Horarios", () => InvokeLegacyTop("OpenScheduleButton"));
        closeManagementButton = AddTopAction("Cerrar paneles", () => CloseCurrentManagementScreen());
        topPopup.gameObject.SetActive(false);
    }
    private Button AddTopAction(string title, Action action)
    {
        var button = EnsureButton(topPopup, "Menu_" + Sanitize(title), title, 236);
        BBIconographyRuntime.Decorate(button, title switch {
            "Edición del local" => BBIconId.NavEditMode,
            "Progreso" => BBIconId.EconomyReport,
            "Comandas" => BBIconId.ObjectDish,
            "Cocina" => BBIconId.AreaKitchen,
            "Camareros" => BBIconId.ObjectWaiter,
            "Sala" => BBIconId.AreaDining,
            "Cierre del día" => BBIconId.EconomyReport,
            "Horarios" => BBIconId.NavReservations,
            _ => BBIconId.ActionCancel
        });
        button.GetComponent<LayoutElement>().preferredHeight = 38;
        button.GetComponent<LayoutElement>().minHeight = 38;
        button.GetComponent<Image>().color = Color.white;
        button.colors = BistroBuilderUiTokens.ButtonColors(BistroBuilderUiTokens.Surface2, BistroBuilderUiTokens.PrimaryHover, BistroBuilderUiTokens.PrimaryPressed);
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) BistroBuilderTypography.Apply(label, BistroBuilderUiStyleRole.Label);
        BistroBuilderSurface.Apply(button.GetComponent<Image>(), BistroBuilderSurfaceLevel.Base);
        button.onClick.AddListener(() => { topPopup.gameObject.SetActive(false); action(); });
        return button;
    }
    private void InvokeLegacyTop(string name)
    {
        if (!TryCloseManagementScreensBeforeOpening(name)) return;
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        FindLegacyLauncher(new[] { name }, buttons)?.onClick.Invoke();
    }
    private void ToggleTopPopup(bool left)
    {
        GetComponent<BistroBuilderOptionsScreen>()?.Close();
        bool show = !topPopup.gameObject.activeSelf;
        topPopup.anchorMin = topPopup.anchorMax = new Vector2(left ? 0 : 1, 0);
        topPopup.pivot = new Vector2(left ? 0 : 1, 1);
        topPopup.anchoredPosition = new Vector2(left ? 128 : -144, -6);
        topPopup.gameObject.SetActive(show);
        topNavigation.SetAsLastSibling();
        RefreshIconNavigation();
    }
    private void TickTopMenuInput()
    {
        if (topPopup == null || !topPopup.gameObject.activeSelf) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { dismissTopPopup = true; return; }
        if (Mouse.current?.leftButton.wasPressedThisFrame != true) return;
        Vector2 point = Mouse.current.position.ReadValue();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(topPopup, point, uiCamera) &&
            !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)optionsButton.transform, point, uiCamera) &&
            !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)identityButton.transform, point, uiCamera))
            dismissTopPopup = true;
    }
    private void LateUpdate()
    {
        if (!dismissTopPopup) return;
        dismissTopPopup = false;
        if (topPopup != null) topPopup.gameObject.SetActive(false);
    }
    private void SuppressLegacyTopBarArtifacts()
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.transform.IsChildOf(shellRoot)) continue;
            if (string.Equals(button.gameObject.name, "OpenScheduleButton", StringComparison.Ordinal))
                button.gameObject.SetActive(false);
        }
    }

    private void RefreshIconNavigation()
    {
        if (restaurantHeading == null) return;
        if (topGameState == null) topGameState = FindScene<BistroBuilderGeneralGameStateService>();
        if (topClock == null) topClock = FindScene<GameClock>();
        restaurantHeading.text = string.IsNullOrWhiteSpace(topGameState?.RestaurantName) ? "Mi restaurante" : topGameState.RestaurantName;
        bool editing = FindScene<RestaurantEditModeService>()?.IsEditModeActive == true;
        serviceHeading.text = editing ? "Diseño del local" : serviceState == null || serviceState.IsClosed ? "Preparación del servicio" :
            topClock != null && topClock.Hour >= 18 ? "Servicio de cena" : "Servicio de comidas";
        string dateText = string.Empty;
        if (topGameState != null)
        {
            int year = Mathf.Clamp(topGameState.CalendarYear, 1, 9999), month = Mathf.Clamp(topGameState.CalendarMonth, 1, 12);
            var date = new DateTime(year, month, Mathf.Clamp(topGameState.CalendarDay, 1, DateTime.DaysInMonth(year, month)));
            dateText = date.ToString("ddd, d MMM", CultureInfo.GetCultureInfo("es-ES"));
        }
        string clockText = topClock != null ? $"{topClock.Hour:00}:{topClock.Minute:00}" : "-";
        if (bottomDateTimeText != null) bottomDateTimeText.text = string.IsNullOrEmpty(dateText) ? clockText : dateText + "  ?  " + clockText;
        if (!IsAnyManagementScreenOpen()) selectedNavigation = "Actividad";
        foreach (var pair in topPresenters)
        {
            pair.Value.SetSelected(pair.Key == selectedNavigation);
            pair.Value.SetInteractable(proxyButtons[pair.Key].interactable);
        }
        RefreshApprovedTopBarV3State();
        var optionsPresenter = optionsButton != null ? optionsButton.GetComponent<BBIconButton>() : null;
        if (optionsPresenter != null)
            optionsPresenter.SetSelected(GetComponent<BistroBuilderOptionsScreen>()?.IsOpen == true);
    }
}
