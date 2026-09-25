using System.Collections.Generic;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderUiShell
{
    private const string ApprovedTopBarResource =
        "BistroBuilder/UI/TopBar/BistroBuilder_NormalTopBar_v3";

    private const float ApprovedTopBarAspect = 1993f / 287f;
    private const float ApprovedTopBarTopInset = 0f;
    private const float ApprovedTopBarSideInset = 0f;
    private const float ApprovedTopBarHeightFraction = 0.18f;
    private const float ApprovedTopBarMinHeight = 170f;
    private const float ApprovedTopBarMaxHeight = 200f;

    private readonly Dictionary<string, BistroBuilderApprovedTopBarHotspot>
        approvedTopBarHotspots = new Dictionary<string, BistroBuilderApprovedTopBarHotspot>();

    private BistroBuilderApprovedTopBarHotspot approvedOptionsHotspot;
    private Sprite approvedTopBarSprite;

    private static readonly float[] ApprovedHotspotLeft =
    {
        0.2729554f, 0.3411942f, 0.4134471f, 0.4872052f, 0.5604616f,
        0.6342197f, 0.7054692f, 0.7787255f, 0.8494731f
    };

    private static readonly float[] ApprovedHotspotWidth =
    {
        0.0682388f, 0.0722529f, 0.0737581f, 0.0732564f, 0.0737581f,
        0.0712493f, 0.0732564f, 0.0707477f, 0.0717511f
    };

    private void EnsureApprovedTopBarV3Content()
    {
        EnsureCompactResponsiveTopBar();
        return;

        if (topNavigation == null) return;        var oldSurface = topNavigation.GetComponent<BistroBuilderTopBarSurface>();
        if (oldSurface == null) oldSurface = topNavigation.gameObject.AddComponent<BistroBuilderTopBarSurface>();
        oldSurface.enabled = true;

        Image rootImage = topNavigation.GetComponent<Image>();
        if (rootImage != null) rootImage.color = Color.clear;

        AspectRatioFitter fitter = topNavigation.GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.aspectMode = AspectRatioFitter.AspectMode.None;
        RefreshApprovedTopBarV3Layout();

        Transform existing = topNavigation.Find("ApprovedTopBarV3Background");
        Image background = existing != null ? existing.GetComponent<Image>() : null;
        if (background == null)
        {
            GameObject go = NewUi("ApprovedTopBarV3Background", topNavigation);
            background = go.AddComponent<Image>();
            background.raycastTarget = false;
        }

        Stretch(background.rectTransform);
        background.preserveAspect = false;

        if (approvedTopBarSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(ApprovedTopBarResource);
            if (texture != null)
            {
                approvedTopBarSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                approvedTopBarSprite.name = "BistroBuilder_NormalTopBar_v3_Runtime";
            }
        }        background.sprite = approvedTopBarSprite;
        background.color = Color.white;
        background.transform.SetAsFirstSibling();

        Transform oldNav = topNavigation.Find("NavigationContent");
        if (oldNav != null) oldNav.gameObject.SetActive(false);

        // ReconcileNavigation usa navContent como señal de que la navegación está lista.
        // En la v3 los hotspots viven directamente sobre la placa completa.
        navContent = topNavigation;

        foreach (string oldName in new[]
        {
            "Wordmark", "BrandDivider", "IdentityDivider", "RestaurantIdentity",
            "ClockDivider", "Calendar", "Clock"
        })
        {
            Transform old = topNavigation.Find(oldName);
            if (old != null) old.gameObject.SetActive(false);
        }

        topNavigation.SetAsLastSibling();
    }

    private void EnsureApprovedTopBarV3Buttons()
    {
        BuildCompactResponsiveTopBarButtons();
        return;

        EnsureApprovedTopBarV3Content();
        if (topNavigation == null) return;

        approvedTopBarHotspots.Clear();
        topPresenters.Clear();
        proxyButtons.Clear();

        identityButton = ApprovedTopBarButton(
            "ApprovedIdentity",
            0f,
            0.273f);
        identityButton.onClick.RemoveAllListeners();
        identityButton.onClick.AddListener(() => ToggleTopPopup(true));        restaurantHeading = EnsureApprovedHiddenText(
            identityButton.transform,
            "RestaurantNameState",
            "Mi restaurante");
        serviceHeading = EnsureApprovedHiddenText(
            identityButton.transform,
            "ServiceState",
            "Preparación del servicio");

        for (int i = 0; i < Navigation.Length; i++)
        {
            string title = Navigation[i].Label;
            Button button = ApprovedTopBarButton(
                "BBNav_" + Sanitize(title),
                ApprovedHotspotLeft[i],
                ApprovedHotspotWidth[i]);

            proxyButtons[title] = button;
            approvedTopBarHotspots[title] =
                button.GetComponent<BistroBuilderApprovedTopBarHotspot>();
        }

        optionsButton = ApprovedTopBarButton(
            "BBNav_Opciones",
            0.9212243f,
            0.0652283f);
        approvedOptionsHotspot =
            optionsButton.GetComponent<BistroBuilderApprovedTopBarHotspot>();

        optionsButton.onClick.RemoveAllListeners();
        optionsButton.onClick.AddListener(() =>
        {
            if (topPopup != null) topPopup.gameObject.SetActive(false);
            var options = GetComponent<BistroBuilderOptionsScreen>() ??
                          gameObject.AddComponent<BistroBuilderOptionsScreen>();
            options.Toggle();
            RefreshIconNavigation();
        });        calendarHeading = EnsureApprovedHiddenText(
            topNavigation,
            "ApprovedCalendarState",
            string.Empty);
        timeHeading = EnsureApprovedHiddenText(
            topNavigation,
            "ApprovedClockState",
            string.Empty);

        SuppressLegacyTopBarArtifacts();
        EnsureTopPopup();
        if (GetComponent<BistroBuilderOptionsScreen>() == null)
            gameObject.AddComponent<BistroBuilderOptionsScreen>();

        RefreshIconNavigation();
    }

    private Button ApprovedTopBarButton(
        string name,
        float normalizedLeft,
        float normalizedWidth)
    {
        Transform existing = topNavigation.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : NewUi(name, topNavigation);

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(normalizedLeft, 0f);
        rect.anchorMax = new Vector2(normalizedLeft + normalizedWidth, 0.90f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        TMP_Text oldLabel = go.GetComponentInChildren<TMP_Text>(true);
        if (oldLabel != null) oldLabel.gameObject.SetActive(false);

        foreach (Transform child in go.transform)
            if (child.name == "NavigationIcon" ||
                child.name == "SelectionUnderline" ||
                child.name == "HoverGlow")
                child.gameObject.SetActive(false);

        BistroBuilderApprovedTopBarHotspot hotspot =
            go.GetComponent<BistroBuilderApprovedTopBarHotspot>();
        if (hotspot == null)
            hotspot = go.AddComponent<BistroBuilderApprovedTopBarHotspot>();
        hotspot.Configure(button);

        go.transform.SetAsLastSibling();
        return button;
    }

    private static TMP_Text EnsureApprovedHiddenText(
        Transform parent,
        string name,
        string initial)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : NewUi(name, parent);        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = go.AddComponent<TextMeshProUGUI>();
        text.text = initial;
        text.raycastTarget = false;
        text.gameObject.SetActive(false);
        return text;
    }

    private void RefreshApprovedTopBarV3Layout()
    {
        ConfigureCompactTopBarRoot();
    }

    private void RefreshApprovedTopBarV3State()
    {
        RefreshApprovedTopBarV3Layout();
        foreach (var pair in approvedTopBarHotspots)
        {
            pair.Value.SetSelected(pair.Key == selectedNavigation);
            if (proxyButtons.TryGetValue(pair.Key, out Button button))
                pair.Value.SetInteractable(button != null && button.interactable);
        }

        if (approvedOptionsHotspot != null)
            approvedOptionsHotspot.SetSelected(
                GetComponent<BistroBuilderOptionsScreen>()?.IsOpen == true);
    }
}