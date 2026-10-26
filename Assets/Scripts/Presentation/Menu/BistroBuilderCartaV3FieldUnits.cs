using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Faithful Carta detail-field presentation. Source Text/InputField and their
/// real callbacks remain authoritative; suffixes are visual affordances only.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class BistroBuilderCartaV3FieldUnits : MonoBehaviour
{
    [SerializeField] private string unit;
    private Text source;
    private TextMeshProUGUI sourceMirror;
    private TextMeshProUGUI suffix;
    private string previous;

    public static void Install(Transform plate, string valueName, string unit)
    {
        if (plate == null) return;
        Text field = plate.Find(valueName)?.GetComponent<Text>();
        if (field == null) return;
        var feature = plate.GetComponent<BistroBuilderCartaV3FieldUnits>();
        if (feature == null)
            feature = plate.gameObject.AddComponent<BistroBuilderCartaV3FieldUnits>();
        feature.unit = unit;
        feature.source = field;
        feature.sourceMirror = field.transform.Find("CartaV3SharpText")
            ?.GetComponent<TextMeshProUGUI>();
        feature.Build();
        feature.Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Build()
    {
        if (suffix != null) return;
        RectTransform line = new GameObject("V3UnitDivider",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        line.SetParent(transform, false);
        line.anchorMin = new Vector2(1f, .16f);
        line.anchorMax = new Vector2(1f, .84f);
        line.offsetMin = new Vector2(-50f, 0f);
        line.offsetMax = new Vector2(-49f, 0f);
        Image edge = line.GetComponent<Image>();
        edge.color = new Color32(201, 166, 125, 150);
        edge.raycastTarget = false;

        RectTransform unitRect = new GameObject("V3UnitSuffix",
            typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<RectTransform>();
        unitRect.SetParent(transform, false);
        unitRect.anchorMin = new Vector2(1f, 0f);
        unitRect.anchorMax = new Vector2(1f, 1f);
        unitRect.offsetMin = new Vector2(-49f, 0f);
        unitRect.offsetMax = new Vector2(-3f, 0f);
        suffix = unitRect.GetComponent<TextMeshProUGUI>();
        suffix.font = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        suffix.fontSize = 18;
        suffix.color = new Color32(69, 40, 20, 255);
        suffix.alignment = TextAlignmentOptions.Midline;
        suffix.enableWordWrapping = false;
        suffix.raycastTarget = false;
        suffix.text = unit;
        // Leave the cost and margin numbers their own readable column.
        if (sourceMirror != null)
        {
            var textRect = sourceMirror.rectTransform;
            textRect.offsetMax = new Vector2(-49f, textRect.offsetMax.y);
        }
    }

    private void Refresh()
    {
        if (source == null || sourceMirror == null) return;
        string input = source.text ?? string.Empty;
        if (input == previous && sourceMirror.text == StripUnit(input))
            return;
        previous = input;
        sourceMirror.text = StripUnit(input);
    }

    private string StripUnit(string original)
    {
        string v = (original ?? string.Empty).Trim();
        if (v.EndsWith(unit, StringComparison.Ordinal))
            return v.Substring(0, v.Length - unit.Length).TrimEnd();
        return v;
    }

    /// <summary>
    /// Adds functional, reversible numeric price arrows to the InputField.
    /// Clicks dispatch its existing end-edit callback; no parallel economy.
    /// </summary>
    public static void AddPriceStepper(InputField price)
    {
        if (price == null || price.transform.Find("V3PriceStepper") != null)
            return;
        RectTransform host = new GameObject("V3PriceStepper",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        host.SetParent(price.transform, false);
        host.anchorMin = new Vector2(1f, 0f);
        host.anchorMax = new Vector2(1f, 1f);
        host.offsetMin = new Vector2(-39f, 2f);
        host.offsetMax = new Vector2(-2f, -2f);
        Image background = host.GetComponent<Image>();
        background.color = new Color32(249, 240, 225, 255);
        background.raycastTarget = false;

        RectTransform divider = new GameObject("V3PriceStepperDivider",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        divider.SetParent(host, false);
        divider.anchorMin = new Vector2(0, 0);
        divider.anchorMax = new Vector2(0, 1);
        divider.offsetMin = Vector2.zero;
        divider.offsetMax = new Vector2(1f, 0f);
        var dividerImage = divider.GetComponent<Image>();
        dividerImage.color = new Color32(191, 152, 110, 170);
        dividerImage.raycastTarget = false;

        CreateArrow(host, "V3PriceUp", true, () => AdjustPrice(price, .50m));
        CreateArrow(host, "V3PriceDown", false, () => AdjustPrice(price, -.50m));
        if (price.textComponent != null)
        {
            RectTransform inputText = price.textComponent.rectTransform;
            inputText.offsetMax = new Vector2(-42f, inputText.offsetMax.y);
        }
    }

    public static void AddPreparationUnit(InputField time)
    {
        if (time == null || time.transform.Find("V3PrepUnit") != null) return;
        RectTransform unitRect = new GameObject("V3PrepUnit",
            typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<RectTransform>();
        unitRect.SetParent(time.transform, false);
        unitRect.anchorMin = new Vector2(1f, 0f);
        unitRect.anchorMax = new Vector2(1f, 1f);
        unitRect.offsetMin = new Vector2(-48f, 0f);
        unitRect.offsetMax = new Vector2(-4f, 0f);
        TextMeshProUGUI text = unitRect.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Inter-Regular-SDF");
        text.fontSize = 16;
        text.text = "min";
        text.color = new Color32(85, 56, 31, 255);
        text.alignment = TextAlignmentOptions.Midline;
        text.raycastTarget = false;
        RectTransform line = new GameObject("V3PrepDivider",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        line.SetParent(time.transform, false);
        line.anchorMin = new Vector2(1f, .15f);
        line.anchorMax = new Vector2(1f, .85f);
        line.offsetMin = new Vector2(-51f, 0f);
        line.offsetMax = new Vector2(-50f, 0f);
        Image edge = line.GetComponent<Image>();
        edge.color = new Color32(196, 167, 132, 150);
        edge.raycastTarget = false;
        if (time.textComponent != null)
        {
            RectTransform inputText = time.textComponent.rectTransform;
            inputText.offsetMax = new Vector2(-53f, inputText.offsetMax.y);
        }
    }

    private static void CreateArrow(RectTransform parent, string name,
        bool up, Action action)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform),
            typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, up ? .5f : 0f);
        rect.anchorMax = new Vector2(1f, up ? 1f : .5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image hit = rect.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, .002f);
        Button button = rect.GetComponent<Button>();
        button.targetGraphic = hit;
        button.onClick.AddListener(() => action());

        Image arrow = new GameObject("Arrow", typeof(RectTransform),
            typeof(Image)).GetComponent<Image>();
        arrow.transform.SetParent(rect, false);
        RectTransform tr = arrow.rectTransform;
        tr.anchorMin = new Vector2(.5f, .5f);
        tr.anchorMax = new Vector2(.5f, .5f);
        tr.sizeDelta = new Vector2(14f, 14f);
        tr.anchoredPosition = Vector2.zero;
        arrow.sprite = BistroBuilderCartaReferenceV3Style.ReferenceIcon("chevron");
        arrow.color = new Color32(96, 59, 22, 255);
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        arrow.transform.localEulerAngles = new Vector3(0, 0, up ? 90f : -90f);
    }

    private static void AdjustPrice(InputField field, decimal delta)
    {
        if (field == null || !field.interactable) return;
        CultureInfo culture = CultureInfo.GetCultureInfo("es-ES");
        string cleaned = (field.text ?? "").Replace("€", "").Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Number,
            culture, out decimal price)) return;
        decimal adjusted = Math.Max(0m, Math.Min(10000m, price + delta));
        field.text = adjusted.ToString("0.00", culture);
        field.onEndEdit.Invoke(field.text);
    }
}
