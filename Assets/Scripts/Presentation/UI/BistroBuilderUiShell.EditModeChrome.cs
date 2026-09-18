using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    public const string EditModeTopBarName = "BB_UIUX_EditModeTopBar";
    public const string EditModeBottomBarName = "BB_UIUX_EditModeBottomBar";

    private RectTransform editModeTopBar;
    private RectTransform editModeBottomBar;
    private TMP_Text editModeMoneyText;
    private TMP_Text editModeClockText;
    private TMP_Text editModeToolStatusText;
    private BistroBuilderConstructionAuthoringRuntimeTool editModeConstructionTool;
    private RestaurantPlaceableCatalogPanel editModeCatalogPanel;
    private RestaurantEditModeService editModeChromeService;
    private RestaurantEditInteractionController editModeFurnitureController;
    private bool editModeChromeBuilt;

    private static readonly Color EditChromeSurface = new Color32(250, 248, 244, 250);
    private static readonly Color EditChromeCard = new Color32(255, 253, 249, 255);
    private static readonly Color EditChromeText = new Color32(36, 39, 35, 255);
    private static readonly Color EditChromeMuted = new Color32(112, 115, 108, 255);
    private static readonly Color EditChromeOlive = new Color32(103, 128, 70, 255);
    private static readonly Color EditChromeLine = new Color32(226, 221, 212, 255);
    private static Sprite editChromeRounded;

    private void EnsureEditModeChrome()
    {
        if (shellRoot == null || editModeChromeBuilt)
            return;

        editModeChromeService = FindScene<RestaurantEditModeService>();
        editModeConstructionTool = FindScene<BistroBuilderConstructionAuthoringRuntimeTool>();
        editModeCatalogPanel = FindScene<RestaurantPlaceableCatalogPanel>();
        editModeFurnitureController = FindScene<RestaurantEditInteractionController>();

        editModeTopBar = EnsureEditChromePanel(
            EditModeTopBarName,
            true,
            30f,
            30f,
            14f,
            58f);

        BuildEditTopChrome(editModeTopBar);

        editModeBottomBar = EnsureEditChromePanel(
            EditModeBottomBarName,
            false,
            20f,
            20f,
            12f,
            68f);

        BuildEditBottomChrome(editModeBottomBar);

        editModeTopBar.gameObject.SetActive(false);
        editModeBottomBar.gameObject.SetActive(false);
        editModeChromeBuilt = true;
    }

    private RectTransform EnsureEditChromePanel(
        string name,
        bool top,
        float left,
        float right,
        float edge,
        float height)
    {
        Transform existing = shellRoot.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : NewUi(name, shellRoot);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.offsetMin = top
            ? new Vector2(left, -edge - height)
            : new Vector2(left, edge);
        rect.offsetMax = top
            ? new Vector2(-right, -edge)
            : new Vector2(-right, edge + height);

        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = EditChromeSurface;
        image.sprite = EditChromeRoundedSprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        Shadow shadow = go.GetComponent<Shadow>() ?? go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.10f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
        return rect;
    }

    private void BuildEditTopChrome(RectTransform root)
    {
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>() ??
            root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 7, 7);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        EnsureEditBrandBlock(root);
        EnsureEditDivider(root, 1f, 34f);

        Button mode = EnsureEditTextButton(root, "EditModeTitle", "✎  Modo Edición", 168f, false);
        mode.interactable = false;
        TMP_Text modeLabel = mode.GetComponentInChildren<TMP_Text>(true);
        if (modeLabel != null)
        {
            modeLabel.fontSize = 15f;
            modeLabel.fontStyle = FontStyles.Bold;
            modeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            modeLabel.color = EditChromeText;
        }

        EnsureFlexibleSpacer(root, "EditTopSpacerA");

        Button undo = EnsureEditTextButton(root, "EditUndo", "↶", 42f, false);
        Button redo = EnsureEditTextButton(root, "EditRedo", "↷", 42f, false);
        undo.onClick.RemoveAllListeners();
        redo.onClick.RemoveAllListeners();
        undo.onClick.AddListener(() => editModeConstructionTool?.TryUndo(out _));
        redo.onClick.AddListener(() => editModeConstructionTool?.TryRedo(out _));

        EnsureEditIconButton(root, "EditPan", "✋", 42f, false).interactable = false;
        EnsureEditIconButton(root, "EditMove", "✥", 42f, false).interactable = false;
        EnsureEditIconButton(root, "EditGrid", "▦", 42f, false).interactable = false;

        EnsureFlexibleSpacer(root, "EditTopSpacerB");

        editModeClockText = EnsureEditInfoText(root, "EditClock", "☀  —", 120f, TextAlignmentOptions.MidlineRight);
        editModeMoneyText = EnsureEditInfoText(root, "EditMoney", "€ —", 112f, TextAlignmentOptions.MidlineRight);
        editModeMoneyText.color = new Color32(75, 113, 52, 255);
        editModeMoneyText.fontStyle = FontStyles.Bold;

        Button play = EnsureEditTextButton(root, "EditPlay", "▶", 48f, true);
        Image playImage = play.GetComponent<Image>();
        if (playImage != null) playImage.color = EditChromeOlive;
        TMP_Text playLabel = play.GetComponentInChildren<TMP_Text>(true);
        if (playLabel != null)
        {
            playLabel.color = Color.white;
            playLabel.fontSize = 23f;
        }
        play.onClick.RemoveAllListeners();
        play.onClick.AddListener(HandleEditModeClicked);
    }

    private void BuildEditBottomChrome(RectTransform root)
    {
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>() ??
            root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 7, 7);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        TMP_Text venue = EnsureEditInfoText(
            root,
            "EditVenue",
            "▣  Terreno de Restaurante\n     20 × 20",
            190f,
            TextAlignmentOptions.MidlineLeft);
        venue.fontSize = 12f;
        venue.color = EditChromeText;

        EnsureEditDivider(root, 1f, 42f);

        EnsureEditToolButton(root, "EditBuild", "▣\nConstruir", 74f,
            BistroBuilderConstructionRuntimeMode.Furniture, null);
        EnsureEditToolButton(root, "EditSurfaces", "◇\nSuperficies", 82f,
            BistroBuilderConstructionRuntimeMode.Room, null);
        EnsureEditToolButton(root, "EditWalls", "▥\nParedes", 72f,
            BistroBuilderConstructionRuntimeMode.Wall, null);
        EnsureEditToolButton(root, "EditDecor", "♜\nDecoración", 82f,
            BistroBuilderConstructionRuntimeMode.Furniture,
            RestaurantPlaceableItemCategory.Decoration);
        EnsureEditToolButton(root, "EditLighting", "◉\nIluminación", 82f,
            BistroBuilderConstructionRuntimeMode.Furniture,
            RestaurantPlaceableItemCategory.Lighting);
        Button services = EnsureEditTextButton(root, "EditServices", "⚙\nServicios", 78f, false);
        services.interactable = false;
        Button other = EnsureEditTextButton(root, "EditOther", "•••\nOtro", 68f, false);
        other.interactable = false;

        EnsureFlexibleSpacer(root, "EditBottomSpacer");

        Button delete = EnsureEditTextButton(root, "EditDelete", "▱  Eliminar", 102f, false);
        delete.onClick.RemoveAllListeners();
        delete.onClick.AddListener(() => editModeConstructionTool?.TryDeleteSelection(out _));

        Button rotate = EnsureEditTextButton(root, "EditRotate", "↻  Rotar", 92f, false);
        rotate.onClick.RemoveAllListeners();
        rotate.onClick.AddListener(() => editModeFurnitureController?.RotateActiveCandidateFromInterface());

        Button duplicate = EnsureEditTextButton(root, "EditDuplicate", "▣  Duplicar", 102f, false);
        duplicate.interactable = false;

        editModeToolStatusText = EnsureEditInfoText(
            root,
            "EditToolStatus",
            string.Empty,
            0f,
            TextAlignmentOptions.MidlineLeft);
        editModeToolStatusText.gameObject.SetActive(false);
    }

    private void RefreshEditModeChrome(bool editing, bool managing)
    {
        EnsureEditModeChrome();

        bool visible = editing && !managing;
        if (editModeTopBar != null) editModeTopBar.gameObject.SetActive(visible);
        if (editModeBottomBar != null) editModeBottomBar.gameObject.SetActive(visible);

        if (topNavigation != null) topNavigation.gameObject.SetActive(!visible);
        if (bottomOperations != null) bottomOperations.gameObject.SetActive(!visible);

        if (!visible)
            return;

        EnforceEditChromeVisuals();

        if (editModeConstructionTool == null)
            editModeConstructionTool = FindScene<BistroBuilderConstructionAuthoringRuntimeTool>();
        if (editModeCatalogPanel == null)
            editModeCatalogPanel = FindScene<RestaurantPlaceableCatalogPanel>();
        if (editModeFurnitureController == null)
            editModeFurnitureController = FindScene<RestaurantEditInteractionController>();

        if (editModeMoneyText != null)
        {
            editModeMoneyText.text = finance != null
                ? BistroBuilderFinanceUiFormat.Money(finance.CurrentBalanceCents)
                : "—";
        }

        if (editModeClockText != null)
        {
            string clock = bottomDateTimeText != null
                ? bottomDateTimeText.text
                : "Modo Edición";
            editModeClockText.text = "☀  " + clock;
        }

        UpdateEditToolButtons();
    }


    private void EnforceEditChromeVisuals()
    {
        if (editModeTopBar != null)
        {
            Image topImage = editModeTopBar.GetComponent<Image>();
            if (topImage != null)
            {
                topImage.color = EditChromeSurface;
                topImage.sprite = EditChromeRoundedSprite();
                topImage.type = Image.Type.Sliced;
            }

            foreach (Button button in editModeTopBar.GetComponentsInChildren<Button>(true))
            {
                Image image = button.GetComponent<Image>();
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                bool play = button.name == "EditPlay";

                if (image != null)
                {
                    image.color = play
                        ? EditChromeOlive
                        : new Color(1f, 1f, 1f, 0f);
                    image.sprite = EditChromeRoundedSprite();
                    image.type = Image.Type.Sliced;
                }

                if (label != null)
                {
                    label.color = play ? Color.white : EditChromeText;
                }
            }
        }

        if (editModeBottomBar != null)
        {
            Image bottomImage = editModeBottomBar.GetComponent<Image>();
            if (bottomImage != null)
            {
                bottomImage.color = EditChromeSurface;
                bottomImage.sprite = EditChromeRoundedSprite();
                bottomImage.type = Image.Type.Sliced;
            }

            string[] actionNames =
            {
                "EditDelete",
                "EditRotate",
                "EditDuplicate"
            };

            for (int index = 0; index < actionNames.Length; index++)
            {
                Transform found = editModeBottomBar.Find(actionNames[index]);
                if (found == null) continue;

                Image image = found.GetComponent<Image>();
                if (image != null)
                {
                    image.color = EditChromeCard;
                    image.sprite = EditChromeRoundedSprite();
                    image.type = Image.Type.Sliced;
                }

                Outline outline = found.GetComponent<Outline>() ??
                    found.gameObject.AddComponent<Outline>();
                outline.effectColor = EditChromeLine;
                outline.effectDistance = new Vector2(0.7f, -0.7f);
                outline.useGraphicAlpha = true;
            }
        }
    }

    private void UpdateEditToolButtons()
    {
        if (editModeBottomBar == null)
            return;

        BistroBuilderConstructionRuntimeMode active = editModeConstructionTool != null
            ? editModeConstructionTool.Mode
            : BistroBuilderConstructionRuntimeMode.Furniture;

        SetEditToolSelected("EditBuild", active == BistroBuilderConstructionRuntimeMode.Furniture);
        SetEditToolSelected("EditSurfaces", active == BistroBuilderConstructionRuntimeMode.Room);
        SetEditToolSelected("EditWalls", active == BistroBuilderConstructionRuntimeMode.Wall);
    }

    private void SetEditToolSelected(string name, bool selected)
    {
        Transform found = editModeBottomBar.Find(name);
        if (found == null) return;

        Image image = found.GetComponent<Image>();
        TMP_Text label = found.GetComponentInChildren<TMP_Text>(true);
        if (image != null)
            image.color = selected ? EditChromeOlive : new Color(1f, 1f, 1f, 0f);
        if (label != null)
            label.color = selected ? Color.white : EditChromeText;
    }

    private Button EnsureEditToolButton(
        Transform parent,
        string name,
        string label,
        float width,
        BistroBuilderConstructionRuntimeMode mode,
        RestaurantPlaceableItemCategory? category)
    {
        Button button = EnsureEditTextButton(parent, name, label, width, false);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            editModeConstructionTool?.SetMode(mode);

            if (category.HasValue)
                editModeCatalogPanel?.SelectCategoryFromInterface(category.Value);
            else if (mode == BistroBuilderConstructionRuntimeMode.Furniture)
                editModeCatalogPanel?.SelectAllFromInterface();

            UpdateEditToolButtons();
        });
        return button;
    }

    private Button EnsureEditTextButton(
        Transform parent,
        string name,
        string label,
        float width,
        bool filled)
    {
        Button button = EnsureButton(parent, name, label, width);
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = filled ? EditChromeOlive : new Color(1f, 1f, 1f, 0f);
            image.sprite = EditChromeRoundedSprite();
            image.type = Image.Type.Sliced;
        }

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.92f, 0.89f, 1f);
        colors.pressedColor = new Color(0.88f, 0.87f, 0.83f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = filled ? Color.white : EditChromeText;
            text.fontSize = 11.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
        }
        return button;
    }

    private Button EnsureEditIconButton(
        Transform parent,
        string name,
        string label,
        float width,
        bool filled)
    {
        Button button = EnsureEditTextButton(parent, name, label, width, filled);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.fontSize = 19f;
        return button;
    }

    private TMP_Text EnsureEditInfoText(
        Transform parent,
        string name,
        string value,
        float width,
        TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : NewUi(name, parent);
        TMP_Text text = go.GetComponent<TMP_Text>() ?? go.AddComponent<TextMeshProUGUI>();
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = width <= 0f ? 1f : 0f;
        text.text = value;
        text.fontSize = 12.5f;
        text.color = EditChromeMuted;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void EnsureEditBrandBlock(Transform parent)
    {
        Transform existing = parent.Find("EditBrand");
        GameObject go = existing != null ? existing.gameObject : NewUi("EditBrand", parent);
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minWidth = 220f;
        element.preferredWidth = 220f;
        element.flexibleWidth = 0f;

        TMP_Text label = go.GetComponent<TMP_Text>() ?? go.AddComponent<TextMeshProUGUI>();
        label.text = "⌂   <b>Bistro</b><color=#5E7D44><b>Builder</b></color>";
        label.fontSize = 21f;
        label.color = EditChromeText;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.richText = true;
    }

    private void EnsureFlexibleSpacer(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : NewUi(name, parent);
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minWidth = 8f;
        element.preferredWidth = 8f;
        element.flexibleWidth = 1f;
    }

    private void EnsureEditDivider(Transform parent, float width, float height)
    {
        string name = "EditDivider_" + parent.childCount;
        GameObject go = NewUi(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = EditChromeLine;
        image.raycastTarget = false;
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static Sprite EditChromeRoundedSprite()
    {
        if (editChromeRounded != null)
            return editChromeRounded;

        const int size = 64;
        const int radius = 18;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BB Edit Chrome Rounded",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        float left = radius - 0.5f;
        float right = size - radius - 0.5f;
        float bottom = radius - 0.5f;
        float top = size - radius - 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, left, right);
                float cy = Mathf.Clamp(y, bottom, top);
                float dx = x - cx;
                float dy = y - cy;
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = radius + 2f;
        editChromeRounded = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        return editChromeRounded;
    }
}
