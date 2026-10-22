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

        BistroBuilderMenuEditorUiFactory.SetLayoutHeight(this, 62f);

        RectTransform thumbRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "Thumbnail", transform,
            new Vector2(0.015f, 0.12f), new Vector2(0.10f, 0.88f),
            Vector2.zero, Vector2.zero);
        thumbnail = BistroBuilderMenuEditorUiFactory.AddImage(
            thumbRect, new Color32(234, 215, 190, 255));
        thumbnail.preserveAspect = false;

        nameText = CreateText("Name", 0.11f, 0.44f, 0.46f, 0.98f, 12, true);
        subtitleText = CreateText("Subtitle", 0.11f, 0.44f, 0.02f, 0.48f, 9, false);
        priceText = CreateText("Price", 0.44f, 0.54f, 0f, 1f, 11, true);
        availabilityText = CreateText("Available", 0.54f, 0.64f, 0f, 1f, 15, true);
        availabilityText.gameObject.SetActive(false);
        RectTransform switchRect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "AvailabilityTrack", transform,
            new Vector2(.59f, .5f), new Vector2(.59f, .5f),
            new Vector2(-20f, -10f), new Vector2(20f, 10f));
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
        preparationText = CreateText("Preparation", 0.75f, 0.87f, 0f, 1f, 10, false);
        marginText = CreateText("Margin", 0.87f, 1f, 0f, 1f, 10, true);
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
            return;
        }

        BistroBuilderCartaReferenceV3Style.StyleDishRow(button, selected);

        nameText.text = snapshot.DisplayName;
        subtitleText.text = ShortSubtitle(snapshot);
        priceText.text = BistroBuilderMenuEditorUtility.FormatMoney(
            snapshot.CurrentPriceCents);
        availabilityText.text = string.Empty;
        BistroBuilderCartaReferenceV3Style.StyleAvailabilitySwitch(
            availabilityTrack, availabilityKnob,
            snapshot.Included && snapshot.Enabled);
        modalityText.text = ResolveModality(snapshot);
        preparationText.text = "◷ " +
            BistroBuilderMenuEditorUtility.FormatPreparationDuration(
                snapshot.PreparationSeconds);
        marginText.text = snapshot.HasValidEconomics
            ? (snapshot.GrossMarginBasisPoints / 100f).ToString("0") + "%"
            : "—";
        marginText.color = ResolveMarginColor(snapshot);

        if (imageCatalog == null)
            imageCatalog = BistroBuilderDishImageCatalog.LoadDefault();
        Sprite image = imageCatalog != null
            ? imageCatalog.GetImageOrNull(snapshot.DishId)
            : null;
        thumbnail.sprite = image;
        thumbnail.color = image != null
            ? Color.white
            : new Color32(234, 215, 190, 255);
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
