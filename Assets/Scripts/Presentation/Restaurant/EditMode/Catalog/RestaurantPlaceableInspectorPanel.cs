using System;
using System.Collections.Generic;
using BistroBuilder.FurnitureFinishes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RestaurantPlaceableInspectorPanel : MonoBehaviour
{
    private static readonly Color Panel = new Color32(250, 248, 244, 252);
    private static readonly Color Card = new Color32(255, 253, 249, 255);
    private static readonly Color Field = new Color32(243, 240, 233, 255);
    private static readonly Color Line = new Color32(226, 221, 212, 255);
    private static readonly Color TextPrimary = new Color32(31, 35, 29, 255);
    private static readonly Color TextMuted = new Color32(111, 115, 108, 255);
    private static readonly Color Olive = new Color32(103, 128, 70, 255);
    private static readonly Color OliveSoft = new Color32(234, 241, 226, 255);
    private static readonly Color InvalidSoft = new Color32(248, 232, 228, 255);
    private static readonly Color InvalidText = new Color32(153, 74, 64, 255);

    private static Sprite rounded10;
    private static Sprite rounded14;
    private static Sprite rounded18;
    private static Sprite rounded24;

    private RestaurantPlaceableCatalogPanel catalogPanel;
    private RestaurantEditInteractionController interactionController;
    private RestaurantEditModeService editModeService;
    private BistroBuilderUiShell uiShell;

    private RectTransform root;
    private RectTransform content;
    private TMP_FontAsset regularFont;
    private TMP_FontAsset semiBoldFont;

    private TMP_Text titleText;
    private Image previewImage;
    private TMP_Text nameText;
    private TMP_Text descriptionText;
    private TMP_Text priceText;
    private TMP_Text scopeText;
    private GameObject variantsSection;
    private RectTransform variantsRow;
    private TMP_Text dimensionsText;
    private RectTransform rulesContainer;
    private Image statusBackground;
    private Image statusIconBackground;
    private TMP_Text statusIcon;
    private TMP_Text statusTitle;
    private TMP_Text statusMessage;
    private TMP_Text favoriteText;

    private RestaurantPlaceableInspectorData currentData;
    private string lastInteractionMessage = string.Empty;
    private bool favorite;
    private bool built;

    private readonly List<GameObject> dynamicVariants =
        new List<GameObject>(8);
    private readonly List<GameObject> dynamicRules =
        new List<GameObject>(8);

    private void Awake()
    {
        catalogPanel = GetComponent<RestaurantPlaceableCatalogPanel>();
        CacheDependencies();
        LoadFonts();
        BuildIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void Start()
    {
        BuildIfNeeded();
        Hide();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (!built || root == null)
            return;

        bool editMode = editModeService != null &&
            editModeService.IsEditModeActive;
        bool managementOpen = uiShell != null &&
            uiShell.HasManagementScreenOpen;

        if (!editMode || managementOpen)
        {
            if (root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
            return;
        }

        if (currentData != null && !root.gameObject.activeSelf)
            root.gameObject.SetActive(true);

        ApplyScreenBounds();
    }

    private void CacheDependencies()
    {
        if (catalogPanel == null)
            catalogPanel = GetComponent<RestaurantPlaceableCatalogPanel>();
        if (interactionController == null)
            interactionController =
                FindFirstObjectByType<RestaurantEditInteractionController>(
                    FindObjectsInactive.Include);
        if (editModeService == null)
            editModeService =
                FindFirstObjectByType<RestaurantEditModeService>(
                    FindObjectsInactive.Include);
        if (uiShell == null)
            uiShell =
                FindFirstObjectByType<BistroBuilderUiShell>(
                    FindObjectsInactive.Include);
    }

    private void LoadFonts()
    {
        regularFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        semiBoldFont = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-SemiBold-SDF");

        TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
        if (regularFont == null) regularFont = fallback;
        if (semiBoldFont == null) semiBoldFont = fallback;
    }

    private void Subscribe()
    {
        if (catalogPanel != null)
        {
            catalogPanel.ItemSelected -= HandleItemSelected;
            catalogPanel.ItemSelected += HandleItemSelected;
        }

        if (interactionController != null)
        {
            interactionController.PlacementValidationChanged -=
                HandleValidationChanged;
            interactionController.PlacementValidationChanged +=
                HandleValidationChanged;

            interactionController.InteractionMessageChanged -=
                HandleInteractionMessageChanged;
            interactionController.InteractionMessageChanged +=
                HandleInteractionMessageChanged;
        }
    }

    private void Unsubscribe()
    {
        if (catalogPanel != null)
            catalogPanel.ItemSelected -= HandleItemSelected;

        if (interactionController != null)
        {
            interactionController.PlacementValidationChanged -=
                HandleValidationChanged;
            interactionController.InteractionMessageChanged -=
                HandleInteractionMessageChanged;
        }
    }

    private void BuildIfNeeded()
    {
        if (built)
            return;

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return;

        GameObject rootObject =
            new GameObject("BB_UIUX_PlaceableInspector", typeof(RectTransform));

        rootObject.AddComponent<BistroBuilderEditChromeSurface>();
        rootObject.transform.SetParent(canvas.transform, false);
        root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(388f, 820f);

        Image panelImage = rootObject.AddComponent<Image>();
        panelImage.color = Panel;
        ApplyRounded(panelImage, 24);

        Shadow panelShadow = rootObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.10f);
        panelShadow.effectDistance = new Vector2(0f, -2f);
        panelShadow.useGraphicAlpha = true;

        ScrollRect scroll = rootObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 22f;

        RectTransform viewport = CreateRect("Viewport", root);
        Stretch(viewport, 8f, 8f, 8f, 64f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(7, 7, 7, 7);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        BuildHeader();
        BuildPreview();
        BuildDetails();
        BuildVariants();
        BuildDimensions();
        BuildRules();
        BuildStatus();

        built = true;
        ApplyScreenBounds();
        root.gameObject.SetActive(false);
    }

    private void BuildHeader()
    {
        RectTransform row = CreateRect("InspectorHeader", root);
        row.anchorMin=new Vector2(0,1);row.anchorMax=new Vector2(1,1);row.pivot=new Vector2(.5f,1);row.offsetMin=new Vector2(16,-58);row.offsetMax=new Vector2(-16,-14);
        titleText = CreateTmp(
            "Title",
            row,
            "Artículo",
            semiBoldFont,
            26f,
            TextPrimary,
            TextAlignmentOptions.MidlineLeft);
        Stretch(titleText.rectTransform,0f,44f,0f,0f);

        Button close = CreateTextButton(
            "Close",
            row,
            "×",
            34f,
            34f,
            Card,
            TextMuted,
            24f);
        var closeRect=(RectTransform)close.transform;closeRect.anchorMin=closeRect.anchorMax=new Vector2(1f,.5f);closeRect.pivot=new Vector2(1f,.5f);closeRect.anchoredPosition=Vector2.zero;
        close.onClick.AddListener(Hide);
    }

    private void BuildPreview()
    {
        RectTransform shell = CreateLayoutRow("Preview", 232f);
        Image background = shell.gameObject.AddComponent<Image>();
        background.color = new Color32(246, 242, 236, 255);
        ApplyRounded(background, 14);

        previewImage = CreateImage("Image", shell);
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;
        Stretch(previewImage.rectTransform, 14f, 14f, 12f, 12f);

        Button favoriteButton = CreateTextButton(
            "Favorite",
            shell,
            "♡",
            44f,
            44f,
            Card,
            TextMuted,
            27f);

        RectTransform favoriteRect =
            favoriteButton.GetComponent<RectTransform>();
        favoriteRect.anchorMin = new Vector2(1f, 1f);
        favoriteRect.anchorMax = new Vector2(1f, 1f);
        favoriteRect.pivot = new Vector2(1f, 1f);
        favoriteRect.anchoredPosition = new Vector2(-9f, -9f);
        favoriteRect.sizeDelta = new Vector2(44f, 44f);

        favoriteText =
            favoriteButton.GetComponentInChildren<TMP_Text>(true);
        favoriteButton.onClick.AddListener(() =>
        {
            favorite = !favorite;
            if (favoriteText != null)
            {
                favoriteText.text = favorite ? "♥" : "♡";
                favoriteText.color = favorite ? Olive : TextMuted;
            }
        });
    }

    private void BuildDetails()
    {
        RectTransform section = CreateLayoutRow("Details", 140f);

        nameText = CreateTmp(
            "Name",
            section,
            "Artículo",
            semiBoldFont,
            17f,
            TextPrimary,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            nameText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -25f),
            new Vector2(0f, 0f));

        descriptionText = CreateTmp(
            "Description",
            section,
            string.Empty,
            regularFont,
            14f,
            TextMuted,
            TextAlignmentOptions.TopLeft);
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        SetAnchors(
            descriptionText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -75f),
            new Vector2(0f, -26f));

        priceText = CreateTmp(
            "Price",
            section,
            "0 €",
            semiBoldFont,
            28f,
            Olive,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(
            priceText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.42f, 0f),
            new Vector2(0f, 2f),
            new Vector2(0f, 38f));

        RectTransform scopePill = CreateRect("ScopePill", section);
        scopePill.anchorMin = new Vector2(0.63f, 0f);
        scopePill.anchorMax = new Vector2(1f, 0f);
        scopePill.pivot = new Vector2(1f, 0f);
        scopePill.offsetMin = new Vector2(0f, 4f);
        scopePill.offsetMax = new Vector2(0f, 36f);

        Image pill = scopePill.gameObject.AddComponent<Image>();
        pill.color = Field;
        ApplyRounded(pill, 14);

        scopeText = CreateTmp(
            "Scope",
            scopePill,
            "Interior",
            regularFont,
            13f,
            TextPrimary,
            TextAlignmentOptions.Center);
        Stretch(scopeText.rectTransform, 5f, 5f, 2f, 2f);
    }

    private void BuildVariants()
    {
        variantsSection = CreateLayoutRow("Variants", 82f).gameObject;

        TMP_Text label = CreateTmp(
            "Label",
            variantsSection.transform,
            "Variantes de color",
            semiBoldFont,
            14f,
            TextPrimary,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -20f),
            Vector2.zero);

        variantsRow = CreateRect("Row", variantsSection.transform);
        variantsRow.anchorMin = new Vector2(0f, 0f);
        variantsRow.anchorMax = new Vector2(1f, 0f);
        variantsRow.pivot = new Vector2(0f, 0f);
        variantsRow.offsetMin = new Vector2(0f, 2f);
        variantsRow.offsetMax = new Vector2(0f, 44f);

        HorizontalLayoutGroup rowLayout =
            variantsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 9f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
    }

    private void BuildDimensions()
    {
        RectTransform section = CreateLayoutRow("Dimensions", 64f);
        AddTopLine(section);

        TMP_Text label = CreateTmp(
            "Label",
            section,
            "Dimensiones",
            semiBoldFont,
            14f,
            TextPrimary,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -22f),
            new Vector2(0f, -4f));

        dimensionsText = CreateTmp(
            "Value",
            section,
            "Ancho —  |  Fondo —  |  Alto —",
            regularFont,
            12.5f,
            TextMuted,
            TextAlignmentOptions.BottomLeft);
        SetAnchors(
            dimensionsText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 2f),
            new Vector2(0f, 28f));
    }

    private void BuildRules()
    {
        RectTransform section = CreateLayoutRow("Rules", 132f);
        AddTopLine(section);

        TMP_Text label = CreateTmp(
            "Label",
            section,
            "Reglas de colocación",
            semiBoldFont,
            14f,
            TextPrimary,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -24f),
            new Vector2(0f, -5f));

        rulesContainer = CreateRect("Rows", section);
        rulesContainer.anchorMin = new Vector2(0f, 0f);
        rulesContainer.anchorMax = new Vector2(1f, 1f);
        rulesContainer.offsetMin = new Vector2(0f, 0f);
        rulesContainer.offsetMax = new Vector2(0f, -30f);

        VerticalLayoutGroup layout =
            rulesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
    }

    private void BuildStatus()
    {
        RectTransform box = CreateLayoutRow("Status", 66f);
        statusBackground = box.gameObject.AddComponent<Image>();
        statusBackground.color = OliveSoft;
        ApplyRounded(statusBackground, 12);

        RectTransform iconRoot = CreateRect("IconRoot", box);
        iconRoot.anchorMin = new Vector2(0f, 0.5f);
        iconRoot.anchorMax = new Vector2(0f, 0.5f);
        iconRoot.pivot = new Vector2(0f, 0.5f);
        iconRoot.anchoredPosition = new Vector2(12f, 0f);
        iconRoot.sizeDelta = new Vector2(40f, 40f);

        statusIconBackground = iconRoot.gameObject.AddComponent<Image>();
        statusIconBackground.color = Olive;
        statusIconBackground.sprite = GetRoundedSprite(18);
        statusIconBackground.type = Image.Type.Sliced;

        statusIcon = CreateTmp(
            "Icon",
            iconRoot,
            "✓",
            semiBoldFont,
            24f,
            Color.white,
            TextAlignmentOptions.Center);
        Stretch(statusIcon.rectTransform, 0f, 0f, 0f, 0f);

        statusTitle = CreateTmp(
            "Title",
            box,
            "Listo para colocar",
            semiBoldFont,
            14f,
            Olive,
            TextAlignmentOptions.BottomLeft);
        SetAnchors(
            statusTitle.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(56f, 0f),
            new Vector2(-8f, -7f));

        statusMessage = CreateTmp(
            "Message",
            box,
            "No hay obstrucciones en este espacio.",
            regularFont,
            11.5f,
            Olive,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            statusMessage.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(56f, 6f),
            new Vector2(-8f, 0f));
    }

    private void HandleItemSelected(
        RestaurantPlaceableItemDefinition definition)
    {
        ShowForDefinition(definition);
    }

    public void ShowForDefinition(
        RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null)
            return;

        currentData = RestaurantPlaceableInspectorData.From(definition);
        favorite = false;
        lastInteractionMessage = string.Empty;

        BuildIfNeeded();
        BindData(currentData);

        if (root != null)
            root.gameObject.SetActive(true);
    }

    private void BindData(RestaurantPlaceableInspectorData data)
    {
        if (data == null)
            return;

        if (titleText != null) titleText.text = data.DisplayName;
        if (nameText != null) nameText.text = data.DisplayName;
        if (descriptionText != null) descriptionText.text = data.Description;

        if (previewImage != null)
        {
            previewImage.sprite = data.Preview;
            previewImage.enabled = data.Preview != null;
        }

        if (priceText != null)
            priceText.text = data.Price.ToString("N0") + " €";

        if (scopeText != null)
            scopeText.text = "⌂  " + data.ScopeLabel;

        Vector3 dimensions = data.DimensionsCentimeters;
        if (dimensionsText != null)
        {
            dimensionsText.text = string.Format(
                "Ancho {0:0.#} cm  |  Fondo {1:0.#} cm  |  Alto {2:0.#} cm",
                dimensions.x,
                dimensions.z,
                dimensions.y);
        }

        if (favoriteText != null)
        {
            favoriteText.text = "♡";
            favoriteText.color = TextMuted;
        }

        RebuildVariants(data);
        RebuildRules(data);
        SetStatusNeutral();
    }

    private void RebuildVariants(RestaurantPlaceableInspectorData data)
    {
        for (int index = 0; index < dynamicVariants.Count; index++)
        {
            if (dynamicVariants[index] != null)
                Destroy(dynamicVariants[index]);
        }
        dynamicVariants.Clear();

        FurnitureFinishProfile profile =
            data != null ? data.FinishProfile : null;

        bool hasVariants =
            profile != null &&
            profile.Variants != null &&
            profile.Variants.Count > 0;

        if (variantsSection != null)
            variantsSection.SetActive(hasVariants);

        if (!hasVariants || variantsRow == null)
            return;

        for (int index = 0; index < profile.Variants.Count; index++)
        {
            FurnitureFinishProfile.VariantDefinition variant =
                profile.Variants[index];

            if (variant == null)
                continue;

            GameObject go = new GameObject(
                "Variant_" + variant.Id,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

            go.transform.SetParent(variantsRow, false);

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minWidth = 36f;
            element.preferredWidth = 36f;
            element.minHeight = 36f;
            element.preferredHeight = 36f;

            Image image = go.GetComponent<Image>();
            image.color = ResolveVariantColor(variant);
            image.sprite = GetRoundedSprite(18);
            image.type = Image.Type.Sliced;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color32(211, 205, 195, 255);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.useGraphicAlpha = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            string variantId = variant.Id;
            button.onClick.AddListener(() =>
            {
                if (TryApplyVariant(variantId))
                {
                    MarkSelectedVariant(variantId);
                }
            });

            dynamicVariants.Add(go);
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultVariantId))
            MarkSelectedVariant(profile.DefaultVariantId);
    }

    private void MarkSelectedVariant(string variantId)
    {
        for (int index = 0; index < dynamicVariants.Count; index++)
        {
            GameObject go = dynamicVariants[index];
            if (go == null)
                continue;

            bool selected = go.name ==
                "Variant_" + variantId;

            Outline outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected
                    ? Olive
                    : new Color32(211, 205, 195, 255);
                outline.effectDistance = selected
                    ? new Vector2(2f, -2f)
                    : new Vector2(1.2f, -1.2f);
            }
        }
    }

    private bool TryApplyVariant(string variantId)
    {
        if (interactionController == null ||
            interactionController.ActiveMember == null)
        {
            return false;
        }

        FurnitureFinishRuntimeBinding binding =
            interactionController.ActiveMember.GetComponentInParent<
                FurnitureFinishRuntimeBinding>();

        if (binding == null)
        {
            binding = interactionController.ActiveMember.GetComponentInChildren<
                FurnitureFinishRuntimeBinding>(true);
        }

        return binding != null &&
            binding.ApplyVariant(variantId);
    }

    private static Color ResolveVariantColor(
        FurnitureFinishProfile.VariantDefinition variant)
    {
        if (variant == null || variant.Bindings == null)
            return new Color32(136, 132, 122, 255);

        for (int index = 0; index < variant.Bindings.Count; index++)
        {
            FurnitureFinishProfile.ZoneFinishBinding binding =
                variant.Bindings[index];

            FurnitureFinishDefinition finish =
                binding != null ? binding.Finish : null;

            Material material =
                finish != null ? finish.Material : null;

            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");

            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            return material.color;
        }

        return new Color32(136, 132, 122, 255);
    }

    private void RebuildRules(RestaurantPlaceableInspectorData data)
    {
        for (int index = 0; index < dynamicRules.Count; index++)
        {
            if (dynamicRules[index] != null)
                Destroy(dynamicRules[index]);
        }
        dynamicRules.Clear();

        if (rulesContainer == null || data == null)
            return;

        for (int index = 0; index < data.Rules.Count; index++)
        {
            RestaurantPlaceableInspectorRuleData rule =
                data.Rules[index];

            RectTransform row = CreateRect(
                "Rule_" + index,
                rulesContainer);

            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 22f;
            layout.preferredHeight = 22f;

            RectTransform checkRoot = CreateRect(
                "Check",
                row);
            checkRoot.anchorMin = new Vector2(0f, 0.5f);
            checkRoot.anchorMax = new Vector2(0f, 0.5f);
            checkRoot.pivot = new Vector2(0f, 0.5f);
            checkRoot.anchoredPosition = Vector2.zero;
            checkRoot.sizeDelta = new Vector2(18f, 18f);

            Image checkBg =
                checkRoot.gameObject.AddComponent<Image>();
            checkBg.color = Olive;
            checkBg.sprite = GetRoundedSprite(6);
            checkBg.type = Image.Type.Sliced;

            TMP_Text check = CreateTmp(
                "Glyph",
                checkRoot,
                "✓",
                semiBoldFont,
                12f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(check.rectTransform, 0f, 0f, 0f, 0f);

            TMP_Text text = CreateTmp(
                "Text",
                row,
                rule.Label,
                regularFont,
                13f,
                TextPrimary,
                TextAlignmentOptions.MidlineLeft);
            SetAnchors(
                text.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(26f, 0f),
                Vector2.zero);

            dynamicRules.Add(row.gameObject);
        }
    }

    private void HandleValidationChanged(
        RestaurantPlacementValidationResult result)
    {
        if (currentData == null)
            return;

        if (result.IsValid)
        {
            SetStatus(
                true,
                "Listo para colocar",
                "No hay obstrucciones en este espacio.");
        }
        else
        {
            string message =
                !string.IsNullOrWhiteSpace(lastInteractionMessage)
                    ? lastInteractionMessage
                    : ResolveValidationMessage(result.Status);

            SetStatus(
                false,
                "No se puede colocar aquí",
                message);
        }
    }

    private void HandleInteractionMessageChanged(string message)
    {
        lastInteractionMessage = message ?? string.Empty;

        if (currentData == null ||
            interactionController == null ||
            interactionController.LastValidationResult.IsValid)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(lastInteractionMessage))
        {
            SetStatus(
                false,
                "No se puede colocar aquí",
                lastInteractionMessage);
        }
    }

    private void SetStatusNeutral()
    {
        SetStatus(
            true,
            "Listo para colocar",
            "Mueve el artículo para comprobar su posición.");
    }

    private void SetStatus(
        bool valid,
        string title,
        string message)
    {
        if (statusBackground != null)
            statusBackground.color = valid ? OliveSoft : InvalidSoft;

        if (statusTitle != null)
        {
            statusTitle.text = title;
            statusTitle.color = valid ? Olive : InvalidText;
        }

        if (statusMessage != null)
        {
            statusMessage.text = message;
            statusMessage.color = valid ? Olive : InvalidText;
        }

        if (statusIcon != null)
            statusIcon.text = valid ? "✓" : "!";

        if (statusIconBackground != null)
            statusIconBackground.color = valid ? Olive : InvalidText;
    }

    private static string ResolveValidationMessage(
        RestaurantPlacementValidationStatus status)
    {
        switch (status)
        {
            case RestaurantPlacementValidationStatus.OutsideRegisteredAreas:
                return "La posición queda fuera del local.";
            case RestaurantPlacementValidationStatus.MissingRequiredCapability:
                return "Esta zona no admite este artículo.";
            case RestaurantPlacementValidationStatus.FootprintOutsideCandidateArea:
                return "Parte del artículo queda fuera del área válida.";
            case RestaurantPlacementValidationStatus.PhysicalOverlap:
                return "Hay otro elemento ocupando este espacio.";
            case RestaurantPlacementValidationStatus.MinimumClearanceViolation:
                return "No se mantiene el espacio libre necesario.";
            case RestaurantPlacementValidationStatus.PlacementConstraintViolation:
                return "La posición incumple una regla de colocación.";
            default:
                return "Busca otra posición válida.";
        }
    }

    public void Hide()
    {
        currentData = null;
        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void ApplyScreenBounds()
    {
        if (root == null)
            return;

        float topInset = 84f;
        float bottomInset = 92f;

        GameObject topBar =
            GameObject.Find(BistroBuilderUiShell.EditModeTopBarName);
        if (topBar != null &&
            topBar.TryGetComponent(out RectTransform topRect) &&
            topRect.rect.height > 1f)
        {
            topInset = topRect.rect.height + 26f;
        }

        GameObject bottomBar =
            GameObject.Find(BistroBuilderUiShell.EditModeBottomBarName);
        if (bottomBar != null &&
            bottomBar.TryGetComponent(out RectTransform bottomRect) &&
            bottomRect.rect.height > 1f)
        {
            bottomInset = bottomRect.rect.height + 24f;
        }

        RectTransform canvasRect =
            root.parent as RectTransform;

        float canvasHeight =
            canvasRect != null && canvasRect.rect.height > 0f
                ? canvasRect.rect.height
                : 1080f;

        float available =
            Mathf.Max(420f, canvasHeight - topInset - bottomInset);

        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(388f, Mathf.Min(830f, available));
        root.anchoredPosition = new Vector2(-24f, -topInset);
    }

    private RectTransform CreateLayoutRow(string name, float height)
    {
        RectTransform rect = CreateRect(name, content);
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
        return rect;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(
        string name,
        Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        return rect.gameObject.AddComponent<Image>();
    }

    private TMP_Text CreateTmp(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.fontSize = size;
        text.fontWeight = FontWeight.Regular;
        text.extraPadding = true;
        text.color = color;
        text.alignment = alignment;
        text.text = value ?? string.Empty;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        return text;
    }

    private Button CreateTextButton(
        string name,
        Transform parent,
        string label,
        float width,
        float height,
        Color backgroundColor,
        Color textColor,
        float textSize)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = backgroundColor;
        ApplyRounded(image, Mathf.RoundToInt(height * 0.45f));

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.94f, 0.94f, 0.92f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.86f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateTmp(
            "Label",
            rect,
            label,
            semiBoldFont,
            textSize,
            textColor,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

        return button;
    }

    private static LayoutElement AddLayout(
        GameObject go,
        float preferredWidth,
        float flexibleWidth)
    {
        LayoutElement element =
            go.GetComponent<LayoutElement>();
        if(element==null)element=go.AddComponent<LayoutElement>();

        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
        return element;
    }

    private static void AddTopLine(RectTransform parent)
    {
        RectTransform line = CreateRect("TopLine", parent);
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.offsetMin = new Vector2(0f, -1f);
        line.offsetMax = Vector2.zero;

        Image image = line.gameObject.AddComponent<Image>();
        image.color = Line;
        image.raycastTarget = false;
    }

    private static void Stretch(
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

    private static void SetAnchors(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void ApplyRounded(Image image, int radius)
    {
        if (image == null)
            return;

        image.sprite = GetRoundedSprite(radius);
        image.type = Image.Type.Sliced;
    }

    private static Sprite GetRoundedSprite(int requestedRadius)
    {
        int radius = requestedRadius <= 10
            ? 10
            : requestedRadius <= 14
                ? 14
                : requestedRadius <= 18
                    ? 18
                    : 24;

        if (radius == 10 && rounded10 != null) return rounded10;
        if (radius == 14 && rounded14 != null) return rounded14;
        if (radius == 18 && rounded18 != null) return rounded18;
        if (radius == 24 && rounded24 != null) return rounded24;

        const int size = 64;
        Texture2D texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BB Inspector Rounded " + radius,
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
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = radius + 2f;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        if (radius == 10) rounded10 = sprite;
        else if (radius == 14) rounded14 = sprite;
        else if (radius == 18) rounded18 = sprite;
        else rounded24 = sprite;

        return sprite;
    }
}
