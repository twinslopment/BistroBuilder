using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    private Canvas ivoryCanvas;
    private RectTransform ivoryPanel;
    private GameObject newGamePage, homePage;
    private TMP_InputField nameField;
    private TMP_Text ivoryStatus;
    private Button createButton, continueButton;
    private Button[] choiceButtons;
    private GameObject[] choiceChecks;
    private Texture2D referenceArt;
    private Sprite roundedIvory;
    private bool creating, returningHome;
    private int lastWidth, lastHeight;
    private static readonly Color Ink = new Color32(37, 37, 30, 255);
    private static readonly Color Ivory = new Color32(248, 239, 222, 255);
    private static readonly Color Brass = new Color32(176, 129, 61, 255);

    private void Update()
    {
        RefreshInitialActions();
        bool show = Application.isPlaying && IsVisible && openingService != null &&
                    openingService.Phase == BistroBuilderNewGamePhase.StartMenu;
        if (show && ivoryCanvas == null) BuildIvoryMenu();
        if (ivoryCanvas != null) ivoryCanvas.gameObject.SetActive(show);
        if (!show) return;
        RestoreModalInputState();
        if (lastWidth != Screen.width || lastHeight != Screen.height)
        {
            lastWidth = Screen.width; lastHeight = Screen.height;
            ivoryCanvas.scaleFactor = Mathf.Min(Screen.width / 1600f, Screen.height / 1000f);
        }
        continueButton.interactable = openingService.CanContinue && !openingService.IsSaveBusy;
        createButton.interactable = !creating && !openingService.IsSaveBusy && !string.IsNullOrWhiteSpace(nameField.text);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !creating)
            SetHome(!returningHome);
    }

    private void BuildIvoryMenu()
    {
        if (EventSystem.current == null)
            new GameObject("OpeningEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var root = new GameObject("NewGameIvoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        ivoryCanvas = root.GetComponent<Canvas>();
        ivoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ivoryCanvas.sortingOrder = 30000;
        roundedIvory = CreateIvoryRound();
        referenceArt = Resources.Load<Texture2D>("BistroBuilder/UI/Opening/ApprovedReference");
        Image backdrop = Surface(root.transform, "Backdrop", new Color32(28, 38, 40, 255), false);
        Stretch(backdrop.rectTransform);
        Image border = Surface(root.transform, "IvoryFrame", Brass);
        ivoryPanel = border.rectTransform;
        ivoryPanel.anchorMin = ivoryPanel.anchorMax = new Vector2(.5f, .5f);
        ivoryPanel.pivot = new Vector2(.5f, .5f);
        ivoryPanel.sizeDelta = new Vector2(1480, 930);
        Image inside = Surface(ivoryPanel, "OpaquePanel", Ivory);
        Place(inside.rectTransform, 5, 5, 1470, 920);
        Art(ivoryPanel, "Logo", new Rect(700, 54, 186, 78), 644, 10, 192, 85);
        Line(ivoryPanel, 8, 98, 1464);
        newGamePage = new GameObject("NewGamePage", typeof(RectTransform));
        newGamePage.transform.SetParent(ivoryPanel, false); Stretch((RectTransform)newGamePage.transform);
        var page = newGamePage.transform;
        Label(page, "NewGameTitle", "Nueva partida", 70, 124, 665, 76, 62, true);
        Label(page, "Subtitle", "Empieza una nueva historia.", 74, 200, 650, 42, 26);
        Image divider = Surface(page, "HeadingDivider", new Color32(177, 158, 128, 255), false);
        Place(divider.rectTransform, 750, 140, 1, 102);
        Label(page, "NameLabel", "Nombre del restaurante", 828, 143, 570, 35, 26, true);
        Image field = Surface(page, "RestaurantName", new Color32(255, 251, 243, 255));
        Place(field.rectTransform, 826, 186, 575, 58);
        nameField = field.gameObject.AddComponent<TMP_InputField>();
        var viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(field.transform, false); Place((RectTransform)viewport.transform, 17, 5, 541, 48);
        TMP_Text nameText = Label(viewport.transform, "Text", restaurantName, 0, 0, 541, 48, 26);
        nameField.textViewport = (RectTransform)viewport.transform;
        nameField.textComponent = (TextMeshProUGUI)nameText;
        nameField.text = restaurantName;
        nameField.characterLimit = 80;
        nameField.lineType = TMP_InputField.LineType.SingleLine;
        nameField.caretColor = Ink;
        nameField.selectionColor = new Color(0.75f, .59f, .34f, .3f);
        nameField.onValueChanged.AddListener(value => restaurantName = value);
        Label(page, "ChooseTitle", "Elige cómo empezar", 472, 277, 536, 50, 36, true, TextAlignmentOptions.Center);
        Line(page, 70, 303, 386); Line(page, 1024, 303, 386);

        choiceButtons = new Button[3]; choiceChecks = new GameObject[3];
        Choice(0, "Desde cero", "Un local vacío.\nDiseña todo a tu gusto.", new Rect(147, 399, 272, 196), 70, 348);
        Choice(1, "Lo esencial", "Distribución y equipo básicos.\nUna base para empezar.", new Rect(838, 398, 278, 203), 784, 348);
        Choice(2, "Últimos retoques", "Un restaurante casi terminado.\nDale tu toque y prepárate para abrir.", new Rect(478, 640, 291, 196), 427, 595);
        Line(page, 48, 827, 1384);
        var back = ActionButton(page, "Back", "Atrás", 48, 841, 244, 56, () => SetHome(true));
        ActionIcon(back.transform, "Arrow", BistroBuilderOpeningActionIcon.Shape.BackArrow, 18, 2, 52, 50);
        ((RectTransform)back.GetComponentInChildren<TMP_Text>().transform).anchoredPosition = new Vector2(62, 0);
        ((RectTransform)back.GetComponentInChildren<TMP_Text>().transform).sizeDelta = new Vector2(160, 56);
        back.GetComponent<BistroBuilderOpeningButtonFeedback>().Icon = back.transform.Find("Arrow") as RectTransform;
        createButton = ActionButton(page, "CreateRestaurant", "Crear restaurante", 1058, 841, 374, 56, BeginCreate);
        ActionIcon(createButton.transform, "Bell", BistroBuilderOpeningActionIcon.Shape.ServiceBell, 301, 0, 62, 56);
        ((RectTransform)createButton.GetComponentInChildren<TMP_Text>().transform).sizeDelta = new Vector2(300, 56);
        var feedback = createButton.GetComponent<BistroBuilderOpeningButtonFeedback>();
        feedback.Icon = createButton.transform.Find("Bell") as RectTransform; feedback.IsBell = true;
        ivoryStatus = Label(page, "Status", "", 320, 839, 715, 58, 18, false, TextAlignmentOptions.Center);
        ivoryStatus.color = new Color32(136, 56, 39, 255);
        BuildHomePage();
        SelectPreparation(0);
        SetHome(false);
    }

    private void Choice(int index, string title, string description, Rect art, float x, float y)
    {
        Button button = ActionButton(newGamePage.transform, "Choice" + index, "", x, y, 626, 222, () => SelectPreparation(index));
        Destroy(button.GetComponentInChildren<TMP_Text>().gameObject);
        Art(button.transform, "PremisesPreview", art, 16, 14, 262, 194);
        Label(button.transform, "Title", title, 299, 45, 308, 47, 34, true);
        Label(button.transform, "Description", description, 299, 101, 308, 100, 22);
        TMP_Text check = Label(button.transform, "Selected", "✓", 576, 10, 34, 34, 29, false, TextAlignmentOptions.Center);
        check.color = Brass;
        choiceButtons[index] = button; choiceChecks[index] = check.gameObject;
    }

    private void SelectPreparation(int index)
    {
        premises = index == 0 ? BistroBuilderStartingPremisesProfile.Empty : index == 1
            ? BistroBuilderStartingPremisesProfile.Essentials : BistroBuilderStartingPremisesProfile.FinishingTouches;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceChecks[i].SetActive(i == index);
            var colors = choiceButtons[i].colors;
            colors.normalColor = i == index ? new Color32(255, 229, 179, 255) : new Color32(255, 249, 239, 255);
            choiceButtons[i].colors = colors;
            choiceButtons[i].GetComponent<Outline>().effectColor = i == index ? Brass : new Color32(192, 176, 151, 255);
        }
    }

    private void BuildHomePage()
    {
        homePage = new GameObject("MainMenu", typeof(RectTransform));
        homePage.transform.SetParent(ivoryPanel, false); Stretch((RectTransform)homePage.transform);
        Label(homePage.transform, "Title", "Tu restaurante, tu historia", 240, 225, 1000, 90, 56, true, TextAlignmentOptions.Center);
        ActionButton(homePage.transform, "NewGame", "Nueva partida", 485, 375, 510, 80, () => SetHome(false));
        continueButton = ActionButton(homePage.transform, "Continue", "Continuar partida", 485, 484, 510, 80, () =>
        {
            if (!openingService.TryContinue(out statusMessage))
            { SetHome(false); ivoryStatus.text = statusMessage; }
        });
        ActionButton(homePage.transform, "Quit", "Salir", 485, 593, 510, 80, Application.Quit);
    }

    private void SetHome(bool home)
    {
        if (creating || openingService.IsSaveBusy) return;
        returningHome = home;
        newGamePage.SetActive(!home); homePage.SetActive(home);
        EventSystem.current?.SetSelectedGameObject(home ? homePage.transform.Find("NewGame").gameObject : choiceButtons[0].gameObject);
    }

    private void BeginCreate()
    {
        if (creating || string.IsNullOrWhiteSpace(nameField.text)) return;
        creating = true; createButton.interactable = false;
        ivoryStatus.text = "Preparando tu restaurante…";
        StartCoroutine(CreateAfterFeedback());
    }

    private IEnumerator CreateAfterFeedback()
    {
        yield return null; yield return null;
        if (openingService.TryCreateNewGame(nameField.text, premises, out statusMessage))
        { statusMessage = string.Empty; Hide(); ivoryCanvas.gameObject.SetActive(false); }
        else ivoryStatus.text = statusMessage;
        creating = false;
    }

    private Image Surface(Transform parent, string name, Color color, bool rounded = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color;
        if (rounded) { image.sprite = roundedIvory; image.type = Image.Type.Sliced; }
        return image;
    }
    private TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h,
        float size, bool heading = false, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = heading ? BistroBuilderTypography.Title : BistroBuilderTypography.Body;
        text.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal; text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .8f; text.color = Ink; text.text = value; text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
        return text;
    }
    private Button ActionButton(Transform parent, string name, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        Image image = Surface(parent, name, Color.white); Place(image.rectTransform, x, y, w, h);
        var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(192, 176, 151, 255); outline.effectDistance = new Vector2(1, -1);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = new Color32(246, 234, 213, 255);
        colors.highlightedColor = new Color32(255, 240, 203, 255); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(222, 197, 152, 255); colors.disabledColor = new Color32(218, 210, 194, 255);
        colors.fadeDuration = .12f; button.colors = colors;
        Label(image.transform, "Label", text, 0, 0, w, h, 26, false, TextAlignmentOptions.Center);
        button.onClick.AddListener(action);
        image.gameObject.AddComponent<BistroBuilderOpeningButtonFeedback>();
        return button;
    }
    private void ActionIcon(Transform parent, string name, BistroBuilderOpeningActionIcon.Shape shape, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(BistroBuilderOpeningActionIcon));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        go.GetComponent<BistroBuilderOpeningActionIcon>().Configure(shape);
    }
    private void Art(Transform parent, string name, Rect source, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        var image = go.GetComponent<RawImage>(); image.texture = referenceArt; image.raycastTarget = false;
        if (referenceArt != null) image.uvRect = new Rect(source.x / 1585f,
            1 - (source.y + source.height) / 992f, source.width / 1585f, source.height / 992f);
    }
    private void Line(Transform parent, float x, float y, float w) => Place(Surface(parent, "Divider", new Color32(184, 162, 127, 255), false).rectTransform, x, y, w, 1);
    private static void Place(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Sprite CreateIvoryRound()
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Opening rounded panel", filterMode = FilterMode.Bilinear };
        var pixels = new Color[4096];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        { float dx = Mathf.Max(16 - x, x - 47, 0), dy = Mathf.Max(16 - y, y - 47, 0); pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy))); }
        texture.SetPixels(pixels); texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }
    private void OnDestroy()
    {
        RestoreModalInputState();
        if (ivoryCanvas != null) Destroy(ivoryCanvas.gameObject);
        if (initialActionsCanvas != null) Destroy(initialActionsCanvas.gameObject);
        if (roundedIvory != null) { Destroy(roundedIvory.texture); Destroy(roundedIvory); }
    }
}
