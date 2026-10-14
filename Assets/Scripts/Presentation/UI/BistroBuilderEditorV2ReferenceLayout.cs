using UnityEngine;
using TMPro;

/// <summary>One coordinate system for the approved 1672 × 941 composition.
/// Each canvas converts the same reference to its own units, avoiding mixed
/// pixel/canvas offsets and overlapping chrome at different resolutions.</summary>
public static class BistroBuilderEditorV2ReferenceLayout
{
    public static TMP_FontAsset HeadingRegular => BistroBuilderEditorV2ApprovedTypography.Heading;
    public static TMP_FontAsset Heading => BistroBuilderEditorV2ApprovedTypography.Heading;
    public static TMP_FontAsset Body => BistroBuilderTypography.Body;
    public static TMP_FontAsset BodyStrong => BistroBuilderTypography.Emphasis;
    public const float Width = 1672, Height = 941;
    public const float FooterHeight = 85;
    public static float Scale(RectTransform parent) =>
        Mathf.Min(parent.rect.width / Width, parent.rect.height / Height);

    public static void Panel(RectTransform rect, bool right, float width = 318)
    {
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        float s = Scale(parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 1);
        rect.localScale = Vector3.one * s;
        rect.anchoredPosition = new Vector2((right ? -10 : 9) * s, -92 * s);
        rect.sizeDelta = new Vector2(width, 730);
    }

    public static void Bar(RectTransform rect, bool top)
    {
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        float s = Scale(parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, top ? 1 : 0);
        rect.localScale = Vector3.one * s;
        // Both bars are opaque docks spanning the complete screen edge.
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(parent.rect.width / s, top ? 74 : FooterHeight);
    }
}
