using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    private const float CompactTopBarHeightFraction = 0.09f;
    private const float CompactTopBarMinHeight = 84f;
    private const float CompactTopBarMaxHeight = 102f;

    private RectTransform compactTopBarRow;
    private Sprite compactBrandSprite;
    private readonly Sprite[] compactNavigationSprites = new Sprite[10];

    private void EnsureCompactResponsiveTopBar()
    {
        if (topNavigation == null) return;

        ConfigureCompactTopBarRoot();
        EnsureCompactTopBarBackground();
        EnsureCompactTopBarRow();
    }    private void ConfigureCompactTopBarRoot()
    {
        float viewportHeight = shellRoot != null && shellRoot.rect.height > 1f
            ? shellRoot.rect.height
            : Screen.height;
        float targetHeight = Mathf.Clamp(
            viewportHeight * CompactTopBarHeightFraction,
            CompactTopBarMinHeight,
            CompactTopBarMaxHeight);

        topNavigation.anchorMin = new Vector2(0f, 1f);
        topNavigation.anchorMax = new Vector2(1f, 1f);
        topNavigation.pivot = new Vector2(0.5f, 1f);
        topNavigation.anchoredPosition = Vector2.zero;
        topNavigation.sizeDelta = new Vector2(0f, targetHeight);

        AspectRatioFitter fitter = topNavigation.GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.aspectMode = AspectRatioFitter.AspectMode.None;

        Image rootImage = topNavigation.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = new Color(0.965f, 0.925f, 0.845f, 1f);
            rootImage.raycastTarget = true;
        }
    }    private void EnsureCompactTopBarBackground()
    {
        Transform approved = topNavigation.Find("ApprovedTopBarV3Background");
        if (approved != null) approved.gameObject.SetActive(false);

        Transform oldNav = topNavigation.Find("NavigationContent");
        if (oldNav != null) oldNav.gameObject.SetActive(false);

        foreach (string oldName in new[]
        {
            "Wordmark", "BrandDivider", "IdentityDivider", "RestaurantIdentity",
            "ClockDivider", "Calendar", "Clock"
        })
        {
            Transform old = topNavigation.Find(oldName);
            if (old != null) old.gameObject.SetActive(false);
        }

        EnsureCompactRule("CompactTopRule", true,
            new Color(0.22f, 0.18f, 0.12f, 0.55f));
        EnsureCompactRule("CompactBottomRule", false,
            new Color(0.45f, 0.31f, 0.13f, 0.72f));
    }    private void EnsureCompactRule(string name, bool top, Color color)
    {
        Transform existing = topNavigation.Find(name);
        GameObject go = existing != null ? existing.gameObject : NewUi(name, topNavigation);
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, top ? 2f : 3f);
    }    private void EnsureCompactTopBarRow()
    {
        Transform existing = topNavigation.Find("CompactTopBarRow");
        if (existing == null)
        {
            GameObject rowGo = NewUi("CompactTopBarRow", topNavigation);
            compactTopBarRow = rowGo.GetComponent<RectTransform>();
        }
        else
        {
            compactTopBarRow = existing as RectTransform;
        }

        Stretch(compactTopBarRow);
        compactTopBarRow.offsetMin = new Vector2(8f, 4f);
        compactTopBarRow.offsetMax = new Vector2(-8f, -4f);

        HorizontalLayoutGroup layout =
            compactTopBarRow.GetComponent<HorizontalLayoutGroup>() ??
            compactTopBarRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }    private void BuildCompactResponsiveTopBarButtons()
    {
        EnsureCompactResponsiveTopBar();
        if (compactTopBarRow == null) return;

        approvedTopBarHotspots.Clear();
        topPresenters.Clear();
        proxyButtons.Clear();

        identityButton = EnsureCompactBrandButton();
        identityButton.onClick.RemoveAllListeners();
        identityButton.onClick.AddListener(() => ToggleTopPopup(true));

        for (int i = 0; i < Navigation.Length; i++)
        {
            string title = Navigation[i].Label;
            Button button = EnsureCompactNavButton(
                "BBNav_" + Sanitize(title),
                title,
                i,
                ApprovedHotspotLeft[i],
                ApprovedHotspotWidth[i]);

            proxyButtons[title] = button;
            approvedTopBarHotspots[title] =
                button.GetComponent<BistroBuilderApprovedTopBarHotspot>();
        }

        optionsButton = EnsureCompactNavButton(
            "BBNav_Opciones",
            "Opciones",
            9,
            0.9212243f,
            0.0652283f);
        approvedOptionsHotspot =
            optionsButton.GetComponent<BistroBuilderApprovedTopBarHotspot>();        optionsButton.onClick.RemoveAllListeners();
        optionsButton.onClick.AddListener(() =>
        {
            if (topPopup != null) topPopup.gameObject.SetActive(false);
            var options = GetComponent<BistroBuilderOptionsScreen>() ??
                          gameObject.AddComponent<BistroBuilderOptionsScreen>();
            options.Toggle();
            RefreshIconNavigation();
        });

        restaurantHeading = EnsureApprovedHiddenText(
            identityButton.transform, "RestaurantNameState", "Mi restaurante");
        serviceHeading = EnsureApprovedHiddenText(
            identityButton.transform, "ServiceState", "Preparación del servicio");
        calendarHeading = EnsureApprovedHiddenText(
            topNavigation, "ApprovedCalendarState", string.Empty);
        timeHeading = EnsureApprovedHiddenText(
            topNavigation, "ApprovedClockState", string.Empty);

        navContent = topNavigation;
        SuppressLegacyTopBarArtifacts();
        EnsureTopPopup();
        if (GetComponent<BistroBuilderOptionsScreen>() == null)
            gameObject.AddComponent<BistroBuilderOptionsScreen>();

        RefreshIconNavigation();
        topNavigation.SetAsLastSibling();
    }    private Button EnsureCompactBrandButton()
    {
        Transform existing = compactTopBarRow.Find("ApprovedIdentity");
        GameObject go = existing != null
            ? existing.gameObject
            : NewUi("ApprovedIdentity", compactTopBarRow);

        Image surface = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        surface.color = Color.clear;

        Button button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;

        LayoutElement layout = go.GetComponent<LayoutElement>() ??
                               go.AddComponent<LayoutElement>();
        float viewportWidth = shellRoot != null ? shellRoot.rect.width : Screen.width;
        layout.minWidth = 220f;
        layout.preferredWidth = Mathf.Clamp(viewportWidth * 0.17f, 220f, 300f);
        layout.flexibleWidth = 0f;

        Image logo = EnsureCompactChildImage(go.transform, "CompactBrandArtwork");
        logo.sprite = GetCompactBrandSprite();
        logo.preserveAspect = true;
        RectTransform rect = logo.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 3f);
        rect.offsetMax = new Vector2(-8f, -3f);

        EnsureCompactSeparator(go.transform);
        return button;
    }    private Button EnsureCompactNavButton(
        string name,
        string label,
        int spriteIndex,
        float sourceLeft,
        float sourceWidth)
    {
        Transform existing = compactTopBarRow.Find(name);
        GameObject go = existing != null ? existing.gameObject : NewUi(name, compactTopBarRow);

        Image surface = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        surface.color = Color.clear;
        surface.raycastTarget = true;

        Button button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;

        LayoutElement layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        layout.minWidth = 82f;
        layout.preferredWidth = 112f;
        layout.flexibleWidth = 1f;

        Image icon = EnsureCompactChildImage(go.transform, "CompactIcon");
        icon.sprite = GetCompactNavigationSprite(
            spriteIndex, sourceLeft, sourceWidth, name);
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -6f);
        iconRect.sizeDelta = new Vector2(46f, 46f);

        TMP_Text text = EnsureCompactLabel(go.transform, label);
        RectTransform labelRect = text.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(3f, 9f);
        labelRect.offsetMax = new Vector2(-3f, 28f);        Image underline = EnsureCompactChildImage(go.transform, "CompactUnderline");
        underline.color = new Color(0.52f, 0.35f, 0.16f, 0.72f);
        underline.preserveAspect = false;
        RectTransform underlineRect = underline.rectTransform;
        underlineRect.anchorMin = new Vector2(0.26f, 0f);
        underlineRect.anchorMax = new Vector2(0.74f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0f);
        underlineRect.anchoredPosition = new Vector2(0f, 4f);
        underlineRect.sizeDelta = new Vector2(0f, 2f);

        EnsureCompactSeparator(go.transform);

        BistroBuilderApprovedTopBarHotspot hotspot =
            go.GetComponent<BistroBuilderApprovedTopBarHotspot>() ??
            go.AddComponent<BistroBuilderApprovedTopBarHotspot>();
        hotspot.Configure(button, iconRect);

        go.transform.SetAsLastSibling();
        return button;
    }    private static Image EnsureCompactChildImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : NewUi(name, parent);
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text EnsureCompactLabel(Transform parent, string label)
    {
        Transform existing = parent.Find("CompactLabel");
        GameObject go = existing != null ? existing.gameObject : NewUi("CompactLabel", parent);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.font = BistroBuilderTypography.Emphasis ?? BistroBuilderTypography.Body;
        text.fontSize = 11.5f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.20f, 0.18f, 0.15f, 0.96f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }    private static void EnsureCompactSeparator(Transform parent)
    {
        Image separator = EnsureCompactChildImage(parent, "CompactSeparator");
        separator.color = new Color(0.42f, 0.34f, 0.24f, 0.24f);
        separator.preserveAspect = false;

        RectTransform rect = separator.rectTransform;
        rect.anchorMin = new Vector2(1f, 0.08f);
        rect.anchorMax = new Vector2(1f, 0.92f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1f, 0f);
    }

    private Texture2D GetCompactTopBarTexture()
    {
        if (approvedTopBarSprite != null) return approvedTopBarSprite.texture;

        Texture2D texture = Resources.Load<Texture2D>(ApprovedTopBarResource);
        if (texture != null)
        {
            approvedTopBarSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
        return texture;
    }    private Sprite GetCompactBrandSprite()
    {
        if (compactBrandSprite != null) return compactBrandSprite;

        Texture2D texture = GetCompactTopBarTexture();
        if (texture == null) return null;

        float width = texture.width * 0.273f;
        compactBrandSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        compactBrandSprite.name = "BB_CompactBrand";
        return compactBrandSprite;
    }

    private Sprite GetCompactNavigationSprite(
        int index,
        float normalizedLeft,
        float normalizedWidth,
        string name)
    {
        if (index >= 0 && index < compactNavigationSprites.Length &&
            compactNavigationSprites[index] != null)
            return compactNavigationSprites[index];

        Texture2D texture = GetCompactTopBarTexture();
        if (texture == null) return null;

        float cellLeft = texture.width * normalizedLeft;
        float cellWidth = texture.width * normalizedWidth;
        float cropWidth = Mathf.Min(cellWidth * 0.78f, texture.height * 0.42f);
        float cropHeight = cropWidth;
        float x = cellLeft + (cellWidth - cropWidth) * 0.5f;
        float y = texture.height * 0.42f;

        y = Mathf.Clamp(y, 0f, texture.height - cropHeight);
        x = Mathf.Clamp(x, 0f, texture.width - cropWidth);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(x, y, cropWidth, cropHeight),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = "BB_CompactIcon_" + name;

        if (index >= 0 && index < compactNavigationSprites.Length)
            compactNavigationSprites[index] = sprite;
        return sprite;
    }
}