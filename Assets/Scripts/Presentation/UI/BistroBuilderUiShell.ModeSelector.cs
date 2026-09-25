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
        modeSelectorRoot.sizeDelta = new Vector2(248f, 48f);
        modeSelectorRoot.SetAsLastSibling();
    }    private void BuildModeSelector()
    {
        Image background = modeSelectorRoot.gameObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.07f, 0.065f, 0.94f);
        background.raycastTarget = true;

        Shadow shadow = modeSelectorRoot.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -2f);

        normalModeSelectorButton = BuildModeSelectorButton(
            "NormalMode", "MODO NORMAL", -122f, SelectNormalPresentationMode);
        editModeSelectorButton = BuildModeSelectorButton(
            "EditMode", "MODO EDICIÓN", 2f, SelectEditPresentationMode);

        GameObject statusGo = NewUi("Status", modeSelectorRoot);
        modeSelectorStatus = statusGo.AddComponent<TextMeshProUGUI>();
        modeSelectorStatus.font = BistroBuilderTypography.Body;
        modeSelectorStatus.fontSize = 9.5f;
        modeSelectorStatus.color = BistroBuilderUiTokens.ContentLight;
        modeSelectorStatus.alignment = TextAlignmentOptions.Center;
        modeSelectorStatus.raycastTarget = false;

        RectTransform statusRect = modeSelectorStatus.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -34f);
        statusRect.sizeDelta = new Vector2(240f, 12f);
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
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -3f);
        rect.sizeDelta = new Vector2(120f, 30f);

        TMP_Text text = NewUi("Label", go.transform).AddComponent<TextMeshProUGUI>();
        Stretch(text.rectTransform);
        text.font = BistroBuilderTypography.Emphasis;
        text.fontSize = 10.5f;
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
            ? 84f
            : (topNavigation != null ? 18f + topNavigation.rect.height : 72f);
        modeSelectorRoot.anchoredPosition = new Vector2(0f, -topOffset);
        modeSelectorRoot.gameObject.SetActive(true);
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
                ? BistroBuilderUiTokens.Primary
                : new Color(0.16f, 0.18f, 0.17f, 0.96f);

        if (label != null)
            label.color = selected
                ? BistroBuilderUiTokens.ContentLight
                : new Color(0.82f, 0.83f, 0.80f, 1f);
    }
}