using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact row used by the approved Carta/Platos V3 composition.
/// Presentation only: selection is delegated to the editor service/view.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMenuEditorDishRowView : MonoBehaviour
{
    private Button button;
    private Image background;
    private Image thumbnail;
    private AspectRatioFitter thumbnailAspect;
    private Text nameText;
    private Text subtitleText;
    private Text priceText;
    private Text availabilityText;
    private Image availabilityTrack;
    private Image availabilityKnob;
    private Text modalityText;
    private Text preparationText;
    private Text marginText;
    private Action<string> selectionCallback;
    private string dishId = string.Empty;
    private static BistroBuilderDishImageCatalog imageCatalog;

    public string DishId => dishId;

    public void Initialize()
    {
        if (button != null) return;

        RectTransform root = transform as RectTransform;
        background = root.GetComponent<Image>();
        if (background == null)
            background = BistroBuilderMenuEditorUiFactory.AddImage(
                root, BistroBuilderMenuEditorUiFactory.SurfaceRaised);

        button = gameObject.GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(HandleClicked);

        // Eight dishes should fill the approved visual table rather than
        // leaving a large unused parchment block at the bottom.
        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(this, 80f);

        RectTransform thumbFrame = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Thumbnail", transform,
            new Vector2(0.058f, 0.12f), new Vector2(0.15f, 0.88f),
            Vector2.zero, Vector2.zero);
        Image thumbBackground = BistroBuilderMenuEditorUiFactory.AddImage(
            thumbFrame, new Color32(234, 215, 190, 255));
        thumbBackground.raycastTarget = false;
        thumbFrame.gameObject.AddComponent<RectMask2D>();
        BuildReferenceGrip();

        RectTransform photo = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ThumbnailPhoto", thumbFrame,
            new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, Vector2.zero);
        thumbnail = BistroBuilderMenuEditorUiFactory.AddImage(
            photo, Color.white);
        thumbnail.raycastTarget = false;
        thumbnail.preserveAspect = false;
        thumbnailAspect = photo.gameObject.AddComponent<AspectRatioFitter>();
        thumbnailAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        thumbnailAspect.aspectRatio = 1.6f;

        nameText = CreateText("Name", 0.16f, 0.44f, 0.46f, 0.98f, 12, true);
        subtitleText = CreateText("Subtitle", 0.16f, 0.44f, 0.02f, 0.48f, 9, false);
        priceText = CreateText("Price", 0.44f, 0.54f, 0f, 1f, 11, true);
        availabilityText = CreateText("Available", 0.54f, 0.64f, 0f, 1f, 15, true);
        availabilityText.gameObject.SetActive(false);
        RectTransform switchRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "AvailabilityTrack", transform,
            new Vector2(.59f, .5f), new Vector2(.59f, .5f),
            new Vector2(-26f, -13f), new Vector2(26f, 13f));
        availabilityTrack = BistroBuilderMenuEditorUiFactory.AddImage(
            switchRect, Color.white);
        RectTransform knobRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "AvailabilityKnob", switchRect,
            new Vector2(1f, .5f), new Vector2(1f, .5f),
            new Vector2(-18f, -8f), new Vector2(-2f, 8f));
        availabilityKnob = BistroBuilderMenuEditorUiFactory.AddImage(
            knobRect, Color.white);
        BistroBuilderCartaReferenceV3Style.StyleAvailabilitySwitch(
            availabilityTrack, availabilityKnob, false);
        modalityText = CreateText("Modality", 0.64f, 0.75f, 0f, 1f, 10, false);
        RectTransform clockRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ReferencePreparationClock", transform,
            new Vector2(.759f, .5f), new Vector2(.759f, .5f),
            new Vector2(-13f, -13f), new Vector2(13f, 13f));
        Image clockImage = BistroBuilderMenuEditorUiFactory.AddImage(
            clockRect, Color.white);
        BistroBuilderCartaReferenceV3Style.StyleDishPreparationClock(clockImage);
        preparationText = CreateText("Preparation", 0.776f, 0.87f, 0f, 1f, 10, false);
        marginText = CreateText("Margin", 0.87f, 1f, 0f, 1f, 10, true);
    }

    // Six discreet dots from the approved V3 table: a selection/reorder cue
    // only. The actual position controls remain the arrows in dish detail.
    private void BuildReferenceGrip()
    {
        RectTransform grip = BistroBuilderMenuEditorUiFactory.CreateRect(
            "ReferenceRowGrip", transform,
            new Vector2(0.025f, .5f), new Vector2(0.025f, .5f),
            new Vector2(-4f, -9f), new Vector2(4f, 9f));
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                RectTransform dot = BistroBuilderMenuEditorUiFactory.CreateRect(
                    "Dot_" + row + "_" + column, grip,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(column * 5f, row * 6f),
                    new Vector2(column * 5f + 2.5f, row * 6f + 2.5f));
                Image dotImage = BistroBuilderMenuEditorUiFactory.AddImage(
                    dot, new Color32(159, 127, 87, 210));
                dotImage.raycastTarget = false;
            }
        }
    }

    public void Bind(
        BistroBuilderMenuEditorDishSnapshot snapshot,
        bool selected,
        Action<string> onSelected)
    {
        Initialize();
        selectionCallback = onSelected;
        dishId = snapshot != null ? snapshot.DishId : string.Empty;
        bool valid = snapshot != null;
        button.interactable = valid;

        if (!valid)
        {
            nameText.text = subtitleText.text = priceText.text =
                availabilityText.text = modalityText.text =
                preparationText.text = marginText.text = string.Empty;
            thumbnail.sprite = null;
            thumbnail.color = new Color(1f, 1f, 1f, 0f);
            if (thumbnailAspect != null) thumbnailAspect.aspectRatio = 1.6f;
            return;
        }

        BistroBuilderCartaReferenceV3Style.StyleDishRow(button, selected);

        // Runtime theme refreshes can restore Overflow and an oversized font.
        // Reapply a single-line-safe fit after each style pass so long dish
        // names never paint over PRICE at 1280x720 or 1920x1080.
        ConstrainCellText(nameText, 12, 19);
        ConstrainCellText(subtitleText, 10, 15);
        ConstrainCellText(priceText, 11, 17);
        ConstrainCellText(modalityText, 10, 15);
        ConstrainCellText(preparationText, 10, 15);
        ConstrainCellText(marginText, 11, 16);

        nameText.text = snapshot.DisplayName;
        subtitleText.text = ShortSubtitle(snapshot);
        priceText.text = BistroBuilderMenuEditorUtility.FormatMoney(
            snapshot.CurrentPriceCents);
        availabilityText.text = string.Empty;
        BistroBuilderCartaReferenceV3Style.StyleAvailabilitySwitch(
            availabilityTrack, availabilityKnob,
            snapshot.Included && snapshot.Enabled);
        modalityText.text = ResolveModality(snapshot);
        preparationText.text =
            BistroBuilderMenuEditorUtility.FormatPreparationDuration(
                snapshot.PreparationSeconds);
        marginText.text = snapshot.HasValidEconomics
            ? (snapshot.GrossMarginBasisPoints / 100f).ToString("0") + "%"
            : "—";
        marginText.color = ResolveMarginColor(snapshot);

        // Preserve native, bound dish data while rendering each cell through
        // crisp SDF glyphs. This avoids blurring at non-integer Canvas scales.
        BistroBuilderCartaV3SdfMirror.Install(nameText, true);
        BistroBuilderCartaV3SdfMirror.Install(subtitleText, false);
        BistroBuilderCartaV3SdfMirror.Install(priceText, false, true);
        BistroBuilderCartaV3SdfMirror.Install(modalityText, false);
        BistroBuilderCartaV3SdfMirror.Install(preparationText, false);
        BistroBuilderCartaV3SdfMirror.Install(marginText, false, true);

        if (imageCatalog == null)
            imageCatalog = BistroBuilderDishImageCatalog.LoadDefault();
        Sprite image = imageCatalog != null
            ? imageCatalog.GetImageOrNull(snapshot.DishId, snapshot.DisplayName)
            : null;
        thumbnail.sprite = image;
        thumbnail.color = image != null
            ? Color.white
            : new Color(1f, 1f, 1f, 0f);
        if (thumbnailAspect != null)
            thumbnailAspect.aspectRatio = image != null && image.rect.height > 0f
                ? image.rect.width / image.rect.height
                : 1.6f;
    }

    private Text CreateText(
        string name,
        float minX,
        float maxX,
        float minY,
        float maxY,
        int fontSize,
        bool bold)
    {
        Text text = BistroBuilderMenuEditorUiFactory.CreateText(
            name, transform, string.Empty, fontSize,
            TextAnchor.MiddleLeft,
            BistroBuilderMenuEditorUiFactory.TextPrimary,
            bold ? FontStyle.Bold : FontStyle.Normal);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(5f, 1f);
        rect.offsetMax = new Vector2(-4f, -1f);
        return text;
    }

    private static void ConstrainCellText(Text text, int minimum, int maximum)
    {
        if (text == null) return;
        // Keep hierarchy and text content untouched, so updates, sorting,
        // selection and data bindings keep their existing behavior.
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minimum;
        text.resizeTextMaxSize = maximum;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        // Numeric and operational columns are centered in the approved
        // six-column reference; only names/descriptions are left-aligned.
        switch (text.name)
        {
            case "Price":
            case "Modality":
            case "Preparation":
            case "Margin":
                text.alignment = TextAnchor.MiddleCenter;
                break;
            default:
                text.alignment = TextAnchor.MiddleLeft;
                break;
        }
    }

    private static string ShortSubtitle(BistroBuilderMenuEditorDishSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Description))
        {
            string value = snapshot.Description.Trim();
            return value.Length <= 34 ? value : value.Substring(0, 31) + "…";
        }
        return snapshot.CategoryName;
    }

    private static string ResolveModality(BistroBuilderMenuEditorDishSnapshot snapshot)
    {
        string category = (snapshot.CategoryName ?? string.Empty).ToLowerInvariant();
        if (category.Contains("postre")) return "Postre";
        if (category.Contains("bebida")) return "Bebida";
        return "Normal";
    }

    private void HandleClicked()
    {
        if (!string.IsNullOrEmpty(dishId))
            selectionCallback?.Invoke(dishId);
    }

    private static Color ResolveMarginColor(
        BistroBuilderMenuEditorDishSnapshot snapshot)
    {
        if (!snapshot.HasValidEconomics)
            return BistroBuilderMenuEditorUiFactory.TextSecondary;
        switch (snapshot.MarginBand)
        {
            case BistroBuilderRecipeMarginBand.Loss:
                return BistroBuilderMenuEditorUiFactory.Negative;
            case BistroBuilderRecipeMarginBand.Low:
                return BistroBuilderMenuEditorUiFactory.Warning;
            default:
                return new Color32(58, 132, 54, 255);
        }
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClicked);
    }
}
