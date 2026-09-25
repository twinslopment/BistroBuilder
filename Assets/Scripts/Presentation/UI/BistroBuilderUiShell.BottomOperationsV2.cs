using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    private const string NormalBottomAtlasResource =
        "BistroBuilder/UI/BottomBar/BottomBarIconAtlas96";
    private const int NormalBottomAtlasCell = 96;

    private static readonly Color BottomCream = new Color32(239, 225, 195, 255);
    private static readonly Color BottomCreamLight = new Color32(249, 239, 218, 255);
    private static readonly Color BottomGold = new Color32(169, 112, 42, 255);
    private static readonly Color BottomGoldLight = new Color32(225, 181, 105, 255);
    private static readonly Color BottomInk = new Color32(59, 43, 31, 255);
    private static readonly Color BottomGreen = new Color32(68, 126, 63, 255);
    private static readonly Color BottomWarning = new Color32(176, 110, 34, 255);
    private static readonly Color BottomCritical = new Color32(166, 60, 43, 255);

    private RectTransform normalBottomBarRoot;
    private RectTransform normalBottomContent;
    private RectTransform normalBottomLeft;
    private RectTransform normalBottomCenter;
    private RectTransform normalBottomRight;
    private RectTransform normalBottomWeatherTime;    private Image normalBottomFrameSurface;
    private Image normalBottomCashIcon;
    private Image normalBottomSatisfactionIcon;
    private Image normalBottomKitchenIcon;
    private Image normalBottomWaitingIcon;
    private Image normalBottomWeatherIcon;
    private Image normalBottomPauseIcon;

    private TMP_Text normalBottomCashValue;
    private TMP_Text normalBottomSatisfactionValue;
    private TMP_Text normalBottomKitchenValue;
    private TMP_Text normalBottomWaitingValue;
    private TMP_Text normalBottomRestaurantName;
    private TMP_Text normalBottomDate;
    private TMP_Text normalBottomTime;

    private Button normalBottomPauseButton;
    private Button normalBottomSpeed1;
    private Button normalBottomSpeed2;
    private Button normalBottomSpeed3;

    private BistroBuilderClimateService normalBottomClimate;
    private Texture2D normalBottomAtlas;
    private readonly Sprite[] normalBottomSprites = new Sprite[15];
    private int normalBottomLastPixelWidth = -1;
    private int normalBottomLastPixelHeight = -1;    private void EnsureNormalBottomBar()
    {
        if (bottomOperations == null) return;

        normalBottomBarRoot = bottomOperations;
        Image rootImage = bottomOperations.GetComponent<Image>();
        if (rootImage == null) rootImage = bottomOperations.gameObject.AddComponent<Image>();
        rootImage.raycastTarget = true;

        normalBottomFrameSurface = EnsureBottomImage(
            bottomOperations, "BottomBarV2_FrameSurface", BottomCream);
        StretchWithOffsets(normalBottomFrameSurface.rectTransform, 3f, 3f, 3f, 3f);
        normalBottomFrameSurface.transform.SetAsFirstSibling();

        normalBottomContent = EnsureBottomRect(
            bottomOperations, "BottomBarV2_Content");
        StretchWithOffsets(normalBottomContent, 8f, 8f, 7f, 7f);
        HorizontalLayoutGroup layout = EnsureBottomHorizontal(normalBottomContent, 4f);
        layout.padding = new RectOffset(0, 0, 0, 0);

        normalBottomLeft = EnsureBottomSection(normalBottomContent, "StatusCluster");
        normalBottomCenter = EnsureBottomSection(normalBottomContent, "RestaurantIdentity");
        normalBottomRight = EnsureBottomSection(normalBottomContent, "TimeCluster");

        EnsureBottomLeftStatus();
        EnsureBottomIdentity();
        EnsureBottomRightControls();
        EnsureBottomRivets();
        LoadNormalBottomSprites();

        bottomStatusContent = normalBottomContent;
        ReapplyNormalBottomBarStyle();

        // EnsureShell can run more than once (Awake + OnEnable).
        // EnsureBar resets its legacy height to 64, so force a responsive
        // reconciliation every time the definitive normal HUD is ensured.
        normalBottomLastPixelWidth = -1;
        normalBottomLastPixelHeight = -1;
        RefreshNormalBottomBar(false, false);
    }    private void EnsureBottomLeftStatus()
    {
        HorizontalLayoutGroup layout = EnsureBottomHorizontal(normalBottomLeft, 2f);
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childForceExpandWidth = true;

        EnsureBottomStatusTile(
            normalBottomLeft, "CashTile", "Caja", 0,
            out normalBottomCashIcon, out normalBottomCashValue);
        EnsureBottomStatusTile(
            normalBottomLeft, "SatisfactionTile", "Satisfacción", 3,
            out normalBottomSatisfactionIcon, out normalBottomSatisfactionValue);
        EnsureBottomStatusTile(
            normalBottomLeft, "KitchenTile", "Cocina", 6,
            out normalBottomKitchenIcon, out normalBottomKitchenValue);
        EnsureBottomStatusTile(
            normalBottomLeft, "WaitingTile", "Espera", 7,
            out normalBottomWaitingIcon, out normalBottomWaitingValue);

        cashText = normalBottomCashValue;
        satisfactionText = normalBottomSatisfactionValue;
        kitchenText = normalBottomKitchenValue;
        waitingText = normalBottomWaitingValue;
    }

    private void EnsureBottomIdentity()
    {
        normalBottomRestaurantName = EnsureBottomText(
            normalBottomCenter, "RestaurantName", "Mi restaurante",
            14f, 27f, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform nameRect = normalBottomRestaurantName.rectTransform;
        nameRect.anchorMin = new Vector2(0.08f, 0f);
        nameRect.anchorMax = new Vector2(0.92f, 1f);
        nameRect.offsetMin = new Vector2(6f, 5f);
        nameRect.offsetMax = new Vector2(-6f, -5f);
        normalBottomRestaurantName.overflowMode = TextOverflowModes.Ellipsis;

        EnsureBottomDiamond(normalBottomCenter, "LeftOrnament", 0.035f);
        EnsureBottomDiamond(normalBottomCenter, "RightOrnament", 0.965f);
    }    private void EnsureBottomRightControls()
    {
        HorizontalLayoutGroup layout = EnsureBottomHorizontal(normalBottomRight, 4f);
        layout.padding = new RectOffset(7, 7, 5, 5);
        layout.childForceExpandWidth = false;

        normalBottomWeatherTime = EnsureBottomRect(
            normalBottomRight, "WeatherAndTime");
        LayoutElement weatherLayout = EnsureBottomLayout(normalBottomWeatherTime);
        weatherLayout.minWidth = 120f;
        weatherLayout.flexibleWidth = 1f;

        normalBottomWeatherIcon = EnsureBottomImage(
            normalBottomWeatherTime, "WeatherIcon", Color.white);
        RectTransform weatherRect = normalBottomWeatherIcon.rectTransform;
        weatherRect.anchorMin = new Vector2(0.02f, 0.12f);
        weatherRect.anchorMax = new Vector2(0.31f, 0.88f);
        weatherRect.offsetMin = Vector2.zero;
        weatherRect.offsetMax = Vector2.zero;
        normalBottomWeatherIcon.preserveAspect = true;

        normalBottomDate = EnsureBottomText(
            normalBottomWeatherTime, "Date", "lun., 1 ene.",
            10f, 15f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        SetAnchoredArea(normalBottomDate.rectTransform, 0.32f, 0.51f, 0.98f, 0.93f);

        normalBottomTime = EnsureBottomText(
            normalBottomWeatherTime, "Time", "10:00",
            15f, 23f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetAnchoredArea(normalBottomTime.rectTransform, 0.32f, 0.06f, 0.98f, 0.57f);

        normalBottomPauseButton = EnsureBottomIconButton(
            normalBottomRight, "Pause", 13, out normalBottomPauseIcon);
        normalBottomPauseButton.onClick.RemoveAllListeners();
        normalBottomPauseButton.onClick.AddListener(OnNormalBottomPauseClicked);

        normalBottomSpeed1 = EnsureBottomSpeedButton(normalBottomRight, "Speed1", "1x", 1f);
        normalBottomSpeed2 = EnsureBottomSpeedButton(normalBottomRight, "Speed2", "2x", 2f);
        normalBottomSpeed3 = EnsureBottomSpeedButton(normalBottomRight, "Speed3", "3x", 3f);
    }    private void EnsureBottomStatusTile(
        Transform parent,
        string name,
        string labelText,
        int spriteIndex,
        out Image icon,
        out TMP_Text value)
    {
        RectTransform tile = EnsureBottomRect(parent, name);
        LayoutElement tileLayout = EnsureBottomLayout(tile);
        tileLayout.minWidth = 0f;
        tileLayout.preferredWidth = -1f;
        tileLayout.flexibleWidth = 1f;

        Image surface = tile.GetComponent<Image>();
        if (surface == null) surface = tile.gameObject.AddComponent<Image>();
        surface.color = BottomCreamLight;
        surface.raycastTarget = false;
        EnsureBottomOutline(surface, BottomGold, new Vector2(1f, -1f));

        icon = EnsureBottomImage(tile, "Icon", Color.white);
        icon.rectTransform.anchorMin = new Vector2(0.035f, 0.16f);
        icon.rectTransform.anchorMax = new Vector2(0.34f, 0.86f);
        icon.rectTransform.offsetMin = Vector2.zero;
        icon.rectTransform.offsetMax = Vector2.zero;
        icon.preserveAspect = true;

        TMP_Text label = EnsureBottomText(
            tile, "Label", labelText, 9f, 13f,
            FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        SetAnchoredArea(label.rectTransform, 0.35f, 0.51f, 0.97f, 0.91f);

        value = EnsureBottomText(
            tile, "Value", "—", 11f, 18f,
            FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetAnchoredArea(value.rectTransform, 0.35f, 0.08f, 0.97f, 0.57f);

        Image accent = EnsureBottomImage(tile, "Accent", BottomGold);
        accent.rectTransform.anchorMin = new Vector2(0.40f, 0.07f);
        accent.rectTransform.anchorMax = new Vector2(0.75f, 0.07f);
        accent.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        accent.rectTransform.sizeDelta = new Vector2(0f, 2f);
        SetNormalBottomSprite(icon, spriteIndex);
    }    private Button EnsureBottomIconButton(
        Transform parent,
        string name,
        int spriteIndex,
        out Image icon)
    {
        RectTransform rect = EnsureBottomRect(parent, name);
        LayoutElement element = EnsureBottomLayout(rect);
        element.minWidth = 46f;
        element.preferredWidth = 52f;
        element.flexibleWidth = 0f;

        Image surface = rect.GetComponent<Image>();
        if (surface == null) surface = rect.gameObject.AddComponent<Image>();
        Button button = rect.GetComponent<Button>();
        if (button == null) button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;

        icon = EnsureBottomImage(rect, "Icon", Color.white);
        SetAnchoredArea(icon.rectTransform, 0.17f, 0.17f, 0.83f, 0.83f);
        icon.preserveAspect = true;
        SetNormalBottomSprite(icon, spriteIndex);

        EnsureBottomOutline(surface, BottomGold, new Vector2(1f, -1f));
        ApplyNormalBottomButtonPalette(button, true);
        return button;
    }

    private Button EnsureBottomSpeedButton(
        Transform parent, string name, string labelText, float speed)
    {
        RectTransform rect = EnsureBottomRect(parent, name);
        LayoutElement element = EnsureBottomLayout(rect);
        element.minWidth = 43f;
        element.preferredWidth = 49f;
        element.flexibleWidth = 0f;

        Image surface = rect.GetComponent<Image>();
        if (surface == null) surface = rect.gameObject.AddComponent<Image>();
        Button button = rect.GetComponent<Button>();
        if (button == null) button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        EnsureBottomOutline(surface, BottomGold, new Vector2(1f, -1f));

        TMP_Text label = EnsureBottomText(
            rect, "Label", labelText, 12f, 18f,
            FontStyles.Bold, TextAlignmentOptions.Center);
        StretchWithOffsets(label.rectTransform, 2f, 2f, 2f, 2f);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SetNormalBottomSpeed(speed));
        ApplyNormalBottomButtonPalette(button, false);
        return button;
    }    private void RefreshNormalBottomBar(bool editing, bool managing)
    {
        if (normalBottomBarRoot == null) return;

        normalBottomBarRoot.gameObject.SetActive(!editing);
        if (editing) return;

        ResolveNormalBottomDependencies();
        RefreshNormalBottomBarLayout();

        if (normalBottomCashValue != null)
            normalBottomCashValue.text = finance != null
                ? BistroBuilderFinanceUiFormat.Money(finance.CurrentBalanceCents)
                : "—";

        int satisfaction = experience != null
            ? experience.LastRecordedSatisfactionBasisPoints
            : 0;
        if (normalBottomSatisfactionValue != null)
            normalBottomSatisfactionValue.text = satisfaction > 0
                ? (satisfaction / 100f).ToString("0") + "%"
                : "—";
        SetNormalBottomSprite(
            normalBottomSatisfactionIcon,
            ResolveSatisfactionSpriteIndex(satisfaction));

        BistroBuilderKitchenLoadState kitchenState = kitchen != null
            ? kitchen.LoadState
            : BistroBuilderKitchenLoadState.Fluid;
        if (normalBottomKitchenValue != null)
        {
            normalBottomKitchenValue.text = KitchenLabel(kitchenState);
            normalBottomKitchenValue.color = ResolveKitchenBottomColor(kitchenState);
        }

        int waiting = ResolveWaitingClientCount();
        if (normalBottomWaitingValue != null)
        {
            normalBottomWaitingValue.text =
                waiting == 1 ? "1 cliente" : waiting + " clientes";
            normalBottomWaitingValue.color =
                waiting > 0 ? BottomWarning : BottomInk;
        }        if (normalBottomRestaurantName != null)
            normalBottomRestaurantName.text =
                string.IsNullOrWhiteSpace(topGameState?.RestaurantName)
                    ? "Mi restaurante"
                    : topGameState.RestaurantName;

        RefreshNormalBottomWeather();
        RefreshNormalBottomDateTime();
        RefreshNormalBottomClockControls();
    }

    private void RefreshNormalBottomWeather()
    {
        if (normalBottomWeatherIcon == null) return;
        BistroBuilderWeatherState weather =
            normalBottomClimate != null ? normalBottomClimate.CurrentWeather : null;
        int spriteIndex = 8;
        if (weather != null)
        {
            if (weather.isSnowing) spriteIndex = 11;
            else if (weather.isRaining) spriteIndex = 10;
            else if (weather.isWindy) spriteIndex = 12;
            else if (weather.cloudState == BistroBuilderCloudState.Cloudy) spriteIndex = 9;
        }
        SetNormalBottomSprite(normalBottomWeatherIcon, spriteIndex);
    }

    private void RefreshNormalBottomDateTime()
    {
        if (normalBottomDate != null)
        {
            string dateText = "—";
            if (topGameState != null)
            {
                int year = Mathf.Clamp(topGameState.CalendarYear, 1, 9999);
                int month = Mathf.Clamp(topGameState.CalendarMonth, 1, 12);
                int day = Mathf.Clamp(
                    topGameState.CalendarDay,
                    1,
                    DateTime.DaysInMonth(year, month));
                DateTime date = new DateTime(year, month, day);
                dateText = date.ToString(
                    "ddd, d MMM",
                    CultureInfo.GetCultureInfo("es-ES")).ToLowerInvariant();
                if (!dateText.EndsWith(".")) dateText += ".";
            }
            normalBottomDate.text = dateText;
        }

        if (normalBottomTime != null)
            normalBottomTime.text = topClock != null
                ? $"{topClock.Hour:00}:{topClock.Minute:00}"
                : "—";
    }    private void RefreshNormalBottomClockControls()
    {
        if (topClock == null) return;

        bool effectivelyPaused = topClock.IsEffectivelyPaused;
        SetNormalBottomSprite(
            normalBottomPauseIcon,
            effectivelyPaused ? 14 : 13);
        if (normalBottomPauseButton != null)
            normalBottomPauseButton.interactable = !topClock.IsRuntimeSuspended;

        ApplyNormalBottomButtonPalette(
            normalBottomPauseButton, effectivelyPaused);
        ApplyNormalBottomButtonPalette(
            normalBottomSpeed1,
            Mathf.Approximately(topClock.SpeedMultiplier, 1f));
        ApplyNormalBottomButtonPalette(
            normalBottomSpeed2,
            Mathf.Approximately(topClock.SpeedMultiplier, 2f));
        ApplyNormalBottomButtonPalette(
            normalBottomSpeed3,
            Mathf.Approximately(topClock.SpeedMultiplier, 3f));
    }

    private void OnNormalBottomPauseClicked()
    {
        ResolveNormalBottomDependencies();
        if (topClock == null || topClock.IsRuntimeSuspended) return;
        topClock.SetPaused(!topClock.IsPaused);
        RefreshNormalBottomClockControls();
    }

    private void SetNormalBottomSpeed(float speed)
    {
        ResolveNormalBottomDependencies();
        if (topClock == null) return;
        topClock.SetSpeedMultiplier(speed);
        RefreshNormalBottomClockControls();
    }

    private void ResolveNormalBottomDependencies()
    {
        if (topGameState == null)
            topGameState = FindScene<BistroBuilderGeneralGameStateService>();
        if (topClock == null)
            topClock = FindScene<GameClock>();
        if (normalBottomClimate == null)
            normalBottomClimate = FindScene<BistroBuilderClimateService>();
    }    private void RefreshNormalBottomBarLayout()
    {
        if (canvas == null || normalBottomContent == null) return;

        int pixelWidth = Mathf.RoundToInt(canvas.pixelRect.width);
        int pixelHeight = Mathf.RoundToInt(canvas.pixelRect.height);
        if (pixelWidth == normalBottomLastPixelWidth &&
            pixelHeight == normalBottomLastPixelHeight)
            return;

        normalBottomLastPixelWidth = pixelWidth;
        normalBottomLastPixelHeight = pixelHeight;

        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        float height = Mathf.Clamp(pixelHeight * 0.085f / scale, 78f, 108f);
        bottomOperations.sizeDelta = new Vector2(0f, height);

        float aspect = pixelHeight > 0 ? pixelWidth / (float)pixelHeight : 1.777f;
        float leftWeight;
        float centerWeight;
        float rightWeight;
        if (aspect < 1.60f)
        {
            leftWeight = 0.48f;
            centerWeight = 0.20f;
            rightWeight = 0.32f;
        }
        else if (aspect > 2.05f)
        {
            leftWeight = 0.40f;
            centerWeight = 0.32f;
            rightWeight = 0.28f;
        }
        else
        {
            leftWeight = 0.44f;
            centerWeight = 0.27f;
            rightWeight = 0.29f;
        }

        SetBottomSectionWeight(normalBottomLeft, leftWeight);
        SetBottomSectionWeight(normalBottomCenter, centerWeight);
        SetBottomSectionWeight(normalBottomRight, rightWeight);

        bool compact = pixelWidth < 1280;
        float controlWidth = compact ? 42f : 50f;
        SetBottomControlWidth(normalBottomPauseButton, compact ? 44f : 52f);
        SetBottomControlWidth(normalBottomSpeed1, controlWidth);
        SetBottomControlWidth(normalBottomSpeed2, controlWidth);
        SetBottomControlWidth(normalBottomSpeed3, controlWidth);

        if (normalBottomRestaurantName != null)
            normalBottomRestaurantName.fontSizeMax = compact ? 21f : 27f;
    }    private void LoadNormalBottomSprites()
    {
        if (normalBottomSprites[0] != null) return;
        if (normalBottomAtlas == null)
            normalBottomAtlas = Resources.Load<Texture2D>(NormalBottomAtlasResource);
        if (normalBottomAtlas == null) return;

        for (int index = 0; index < normalBottomSprites.Length; index++)
        {
            int column = index % 5;
            int topRow = index / 5;
            int unityRow = 2 - topRow;
            normalBottomSprites[index] = Sprite.Create(
                normalBottomAtlas,
                new Rect(
                    column * NormalBottomAtlasCell,
                    unityRow * NormalBottomAtlasCell,
                    NormalBottomAtlasCell,
                    NormalBottomAtlasCell),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            normalBottomSprites[index].name =
                "BottomBarIcon_" + index;
        }
    }

    private void SetNormalBottomSprite(Image target, int index)
    {
        if (target == null) return;
        LoadNormalBottomSprites();
        if (index < 0 || index >= normalBottomSprites.Length) return;
        target.sprite = normalBottomSprites[index];
        target.color = Color.white;
    }

    private static int ResolveSatisfactionSpriteIndex(int basisPoints)
    {
        if (basisPoints <= 0) return 3;
        BistroBuilderCustomerSatisfactionBand band =
            BistroBuilderReputationEngine.GetSatisfactionBand(basisPoints);
        switch (band)
        {
            case BistroBuilderCustomerSatisfactionBand.VeryBad: return 1;
            case BistroBuilderCustomerSatisfactionBand.Bad: return 2;
            case BistroBuilderCustomerSatisfactionBand.Good: return 4;
            case BistroBuilderCustomerSatisfactionBand.Excellent: return 5;
            default: return 3;
        }
    }    private static Color ResolveKitchenBottomColor(
        BistroBuilderKitchenLoadState state)
    {
        switch (state)
        {
            case BistroBuilderKitchenLoadState.Loaded:
                return BottomWarning;
            case BistroBuilderKitchenLoadState.Saturated:
            case BistroBuilderKitchenLoadState.Blocked:
                return BottomCritical;
            default:
                return BottomGreen;
        }
    }

    private void ReapplyNormalBottomBarStyle()
    {
        if (normalBottomBarRoot == null) return;

        Image root = normalBottomBarRoot.GetComponent<Image>();
        if (root != null) root.color = BottomGold;
        if (normalBottomFrameSurface != null)
        {
            normalBottomFrameSurface.color = BottomCream;
            EnsureBottomOutline(
                normalBottomFrameSurface,
                new Color32(102, 67, 28, 255),
                new Vector2(1.5f, -1.5f));
        }

        foreach (Transform child in normalBottomContent)
        {
            Image image = child.GetComponent<Image>();
            if (image != null) image.color = BottomCream;
        }

        ReapplyNormalBottomTypography();
    }

    private void ReapplyNormalBottomTypography()
    {
        if (normalBottomBarRoot == null) return;

        TMP_Text[] texts = normalBottomBarRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;
            if (BistroBuilderTypography.Body != null)
                text.font = BistroBuilderTypography.Body;
            text.color = BottomInk;
        }

        foreach (string tileName in new[]
        {
            "CashTile", "SatisfactionTile", "KitchenTile", "WaitingTile"
        })
        {
            Transform tile = normalBottomBarRoot.Find("BottomBarV2_Content/StatusCluster/" + tileName);
            TMP_Text label = tile != null ? tile.Find("Label")?.GetComponent<TMP_Text>() : null;
            if (label != null)
                label.color = new Color32(126, 88, 43, 255);
        }

        if (normalBottomRestaurantName != null)
        {
            normalBottomRestaurantName.color = new Color32(72, 48, 27, 255);
            normalBottomRestaurantName.fontStyle = FontStyles.Bold;
        }
        if (normalBottomDate != null)
            normalBottomDate.color = new Color32(111, 79, 47, 255);
        if (normalBottomTime != null)
            normalBottomTime.color = BottomInk;
    }

    private static RectTransform EnsureBottomSection(
        Transform parent, string name)
    {
        RectTransform rect = EnsureBottomRect(parent, name);
        Image surface = rect.GetComponent<Image>();
        if (surface == null) surface = rect.gameObject.AddComponent<Image>();
        surface.color = BottomCream;
        surface.raycastTarget = false;
        EnsureBottomOutline(surface, BottomGold, new Vector2(1f, -1f));

        LayoutElement layout = EnsureBottomLayout(rect);
        layout.minWidth = 0f;
        layout.preferredWidth = -1f;
        layout.flexibleWidth = 1f;
        return rect;
    }    private static RectTransform EnsureBottomRect(
        Transform parent, string name)
    {
        Transform found = parent.Find(name);
        GameObject go = found != null
            ? found.gameObject
            : NewUi(name, parent);
        return go.GetComponent<RectTransform>();
    }

    private static Image EnsureBottomImage(
        Transform parent, string name, Color color)
    {
        RectTransform rect = EnsureBottomRect(parent, name);
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text EnsureBottomText(
        Transform parent,
        string name,
        string initial,
        float minSize,
        float maxSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = EnsureBottomRect(parent, name);
        TMP_Text text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (BistroBuilderTypography.Body != null)
            text.font = BistroBuilderTypography.Body;
        text.text = initial;
        text.color = BottomInk;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }    private static HorizontalLayoutGroup EnsureBottomHorizontal(
        Transform parent, float spacing)
    {
        HorizontalLayoutGroup layout = parent.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return layout;
    }

    private static LayoutElement EnsureBottomLayout(RectTransform rect)
    {
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        return element;
    }

    private static void EnsureBottomOutline(
        Graphic graphic, Color color, Vector2 distance)
    {
        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = false;
    }

    private static void SetAnchoredArea(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }    private static void StretchWithOffsets(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void EnsureBottomDiamond(
        Transform parent, string name, float x)
    {
        Image diamond = EnsureBottomImage(parent, name, BottomGoldLight);
        RectTransform rect = diamond.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(x, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8f, 8f);
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void EnsureBottomRivets()
    {
        EnsureBottomRivet("RivetBL", new Vector2(0f, 0f), new Vector2(7f, 7f));
        EnsureBottomRivet("RivetTL", new Vector2(0f, 1f), new Vector2(7f, -7f));
        EnsureBottomRivet("RivetBR", new Vector2(1f, 0f), new Vector2(-7f, 7f));
        EnsureBottomRivet("RivetTR", new Vector2(1f, 1f), new Vector2(-7f, -7f));
    }

    private void EnsureBottomRivet(
        string name, Vector2 anchor, Vector2 position)
    {
        Image rivet = EnsureBottomImage(normalBottomBarRoot, name, BottomGoldLight);
        RectTransform rect = rivet.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(7f, 7f);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        rivet.transform.SetAsLastSibling();
    }    private static void SetBottomSectionWeight(
        RectTransform section, float weight)
    {
        if (section == null) return;
        LayoutElement layout = EnsureBottomLayout(section);
        layout.minWidth = 0f;
        layout.preferredWidth = 0f;
        layout.flexibleWidth = Mathf.Max(0.01f, weight);
    }

    private static void SetBottomControlWidth(Button button, float width)
    {
        if (button == null) return;
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
    }

    private static void ApplyNormalBottomButtonPalette(
        Button button, bool selected)
    {
        if (button == null) return;
        Color normal = selected ? BottomGoldLight : BottomCreamLight;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = new Color32(250, 224, 171, 255);
        colors.pressedColor = new Color32(197, 141, 67, 255);
        colors.selectedColor = BottomGoldLight;
        colors.disabledColor = new Color32(187, 177, 157, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (button.targetGraphic != null)
            button.targetGraphic.color = normal;
    }
}
