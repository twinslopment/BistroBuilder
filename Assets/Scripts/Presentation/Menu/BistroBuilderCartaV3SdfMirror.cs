using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pixel-sharp, resolution-independent visual mirror of existing uGUI labels.
/// The original Text retains all gameplay bindings, layout and QA metadata.
/// Recoleta / Inter official SDF assets are loaded through Resources.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCartaV3SdfMirror : MonoBehaviour
{
    [SerializeField] private bool useRecoleta;
    [SerializeField] private bool useSemibold;
    private Text source;
    private TextMeshProUGUI display;
    private int previousFontSize = -1;
    private string previousText;
    private Color previousColor;
    private TextAnchor previousAlignment;

    public static void Install(Text text, bool recoleta, bool semibold = false)
    {
        if (text == null) return;
        BistroBuilderCartaV3SdfMirror mirror =
            text.GetComponent<BistroBuilderCartaV3SdfMirror>();
        if (mirror == null)
            mirror = text.gameObject.AddComponent<BistroBuilderCartaV3SdfMirror>();
        mirror.useRecoleta = recoleta;
        mirror.useSemibold = semibold;
        mirror.EnsureDisplay();
        mirror.Refresh(true);
    }

    private void OnEnable() => Refresh(true);
    private void LateUpdate() => Refresh(false);

    private void EnsureDisplay()
    {
        if (source == null) source = GetComponent<Text>();
        if (source == null) return;
        if (display == null)
        {
            Transform existing = transform.Find("CartaV3SharpText");
            if (existing != null) display = existing.GetComponent<TextMeshProUGUI>();
        }
        if (display == null)
        {
            GameObject node = new GameObject("CartaV3SharpText",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            node.transform.SetParent(transform, false);
            RectTransform r = node.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            display = node.GetComponent<TextMeshProUGUI>();
        }
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/" +
            (useRecoleta ? "Recoleta-SDF" :
                useSemibold ? "Inter-SemiBold-SDF" : "Inter-Regular-SDF"));
        if (font != null) display.font = font;
        display.enableWordWrapping = false;
        display.overflowMode = TextOverflowModes.Ellipsis;
        display.raycastTarget = false;
        display.enableAutoSizing = false;
        display.fontStyle = FontStyles.Normal;
        display.margin = Vector4.zero;
        source.enabled = false;
    }

    private void Refresh(bool force)
    {
        EnsureDisplay();
        if (source == null || display == null) return;
        source.enabled = false;
        if (!force && previousFontSize == source.fontSize &&
            previousText == source.text && previousColor == source.color &&
            previousAlignment == source.alignment)
            return;
        previousFontSize = source.fontSize;
        previousText = source.text;
        previousColor = source.color;
        previousAlignment = source.alignment;
        display.text = source.text ?? string.Empty;
        display.fontSize = source.fontSize;
        display.color = source.color;
        switch (source.alignment)
        {
            case TextAnchor.MiddleRight:
                display.alignment = TextAlignmentOptions.MidlineRight;
                break;
            case TextAnchor.MiddleCenter:
                display.alignment = TextAlignmentOptions.Midline;
                break;
            default:
                display.alignment = TextAlignmentOptions.MidlineLeft;
                break;
        }
        if (source.resizeTextForBestFit)
        {
            display.enableAutoSizing = true;
            display.fontSizeMin = Mathf.Max(8, source.resizeTextMinSize);
            display.fontSizeMax = Mathf.Max(display.fontSizeMin,
                source.resizeTextMaxSize);
        }
        else
            display.enableAutoSizing = false;
    }
}
