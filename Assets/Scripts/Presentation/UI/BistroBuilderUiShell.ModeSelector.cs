using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    public const string ModeSelectorName = "BB_UIUX_ModeSelector";

    private RectTransform modeSelectorRoot;
    private Button normalModeSelectorButton;
    private Button editModeSelectorButton;
    private TMP_Text modeSelectorStatus;
    private float modeSelectorStatusUntil;

    private void EnsureModeSelector()
    {
        if (shellRoot == null) return;

        Transform existing = shellRoot.Find(ModeSelectorName);
        modeSelectorRoot = existing as RectTransform;
        if (modeSelectorRoot == null)
        {
            modeSelectorRoot = NewUi(ModeSelectorName, shellRoot).GetComponent<RectTransform>();
            BuildModeSelector();
        }

        modeSelectorRoot.anchorMin = new Vector2(0.5f, 1f);
        modeSelectorRoot.anchorMax = new Vector2(0.5f, 1f);
        modeSelectorRoot.pivot = new Vector2(0.5f, 1f);
        modeSelectorRoot.sizeDelta = new Vector2(236f, 42f);
        modeSelectorRoot.SetAsLastSibling();
    }    private void BuildModeSelector()
    {
        modeSelectorRoot.gameObject.AddComponent<BistroBuilderTopBarSurface>();
        Image background = modeSelectorRoot.gameObject.AddComponent<Image>();
        background.color = new Color32(239, 225, 195, 248);
        background.raycastTarget = true;

        Outline frame = modeSelectorRoot.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color32(154, 101, 36, 255);
        frame.effectDistance = new Vector2(1.2f, -1.2f);
        frame.useGraphicAlpha = false;

        Shadow shadow = modeSelectorRoot.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -2f);

        normalModeSelectorButton = BuildModeSelectorButton(
            "NormalMode", "MODO NORMAL", -115f, SelectNormalPresentationMode);
        editModeSelectorButton = BuildModeSelectorButton(
            "EditMode", "MODO EDICIÓN", 2f, SelectEditPresentationMode);

        GameObject statusGo = NewUi("Status", modeSelectorRoot);
        modeSelectorStatus = statusGo.AddComponent<TextMeshProUGUI>();
        modeSelectorStatus.font = BistroBuilderTypography.Body;
        modeSelectorStatus.fontSize = 9f;
        modeSelectorStatus.color = new Color32(59, 43, 31, 255);
        modeSelectorStatus.alignment = TextAlignmentOptions.Center;
        modeSelectorStatus.raycastTarget = false;

        RectTransform statusRect = modeSelectorStatus.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -31f);
        statusRect.sizeDelta = new Vector2(228f, 10f);
        modeSelectorStatus.gameObject.SetActive(false);
    }    private Button BuildModeSelectorButton(
        string name,
        string label,
        float x,
        UnityEngine.Events.UnityAction action)
    {
        GameObject go = NewUi(name, modeSelectorRoot);
        Image image = go.AddComponent<Image>();
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(action);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -3f);
        rect.sizeDelta = new Vector2(112f, 27f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(169, 112, 42, 255);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        TMP_Text text = NewUi("Label", go.transform).AddComponent<TextMeshProUGUI>();
        Stretch(text.rectTransform);
        text.font = BistroBuilderTypography.Emphasis;
        text.fontSize = 10f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = label;

        return button;
    }    private void SelectNormalPresentationMode()
    {
        ResolveDependencies();
        if (topPopup != null) topPopup.gameObject.SetActive(false);
        GetComponent<BistroBuilderOptionsScreen>()?.Close();
        CloseSimpleManagementScreens();

        RestaurantEditModeService editMode = FindScene<RestaurantEditModeService>();
        if (editMode == null)
        {
            ShowModeSelectorStatus("Modo normal no disponible: falta la autoridad de modo.");
            return;
        }

        if (editMode.IsEditModeActive)
        {
            editController?.CancelActivePlacement();
            editController?.ClearSelection();

            if (!editMode.TryExitEditMode(
                    true,
                    out RestaurantEditModeFailureReason failureReason))
            {
                ShowModeSelectorStatus("No se pudo salir de Edición: " + failureReason + ".");
                return;
            }
        }

        ShowModeSelectorStatus("MODO NORMAL");
        nextRefreshAt = 0f;
        RefreshReadModels();
    }    private void SelectEditPresentationMode()
    {
        ResolveDependencies();
        if (topPopup != null) topPopup.gameObject.SetActive(false);
        GetComponent<BistroBuilderOptionsScreen>()?.Close();
        CloseSimpleManagementScreens();

        RestaurantEditModeService editMode = FindScene<RestaurantEditModeService>();
        if (editMode == null || editController == null)
        {
            ShowModeSelectorStatus("Modo Edición no disponible.");
            return;
        }

        if (!editMode.IsEditModeActive && !editController.TryEnterEditMode())
        {
            ShowModeSelectorStatus("No se pudo activar Modo Edición.");
            return;
        }

        ShowModeSelectorStatus("MODO EDICIÓN");
        nextRefreshAt = 0f;
        RefreshReadModels();
    }

    private void ShowModeSelectorStatus(string message)
    {
        EnsureModeSelector();
        if (modeSelectorStatus == null) return;
        modeSelectorStatus.text = message;
        modeSelectorStatus.gameObject.SetActive(true);
        modeSelectorStatusUntil = Time.unscaledTime + 1.8f;
    }    private void RefreshModeSelector(bool editing, bool managing)
    {
        EnsureModeSelector();
        if (modeSelectorRoot == null) return;

        float topOffset = editing
            ? 68f
            : (topNavigation != null ? 10f + topNavigation.rect.height : 68f);
        modeSelectorRoot.anchoredPosition = new Vector2(0f, -topOffset);
        modeSelectorRoot.gameObject.SetActive(!BistroBuilderNewGameOpeningPlayerScreen.IsOpeningMenuBlocking);
        modeSelectorRoot.SetAsLastSibling();

        ApplyModeSelectorState(normalModeSelectorButton, !editing);
        ApplyModeSelectorState(editModeSelectorButton, editing);

        normalModeSelectorButton.interactable = editing;
        editModeSelectorButton.interactable = !editing;

        if (modeSelectorStatus != null &&
            modeSelectorStatus.gameObject.activeSelf &&
            Time.unscaledTime >= modeSelectorStatusUntil)
        {
            modeSelectorStatus.gameObject.SetActive(false);
        }
    }

    private static void ApplyModeSelectorState(Button button, bool selected)
    {
        if (button == null) return;

        Image background = button.GetComponent<Image>();
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (background != null)
            background.color = selected
                ? new Color32(225, 181, 105, 255)
                : new Color32(248, 239, 218, 255);

        if (label != null)
            label.color = new Color32(59, 43, 31, 255);

        ColorBlock colors = button.colors;
        colors.normalColor = background != null ? background.color : Color.white;
        colors.highlightedColor = new Color32(250, 224, 171, 255);
        colors.pressedColor = new Color32(197, 141, 67, 255);
        colors.selectedColor = new Color32(225, 181, 105, 255);
        colors.disabledColor = colors.normalColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }
}