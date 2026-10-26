using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Keeps Carta V3 typography and authored glyphs at a stable physical size
/// while the global management Canvas scales between 1920x1080 and 1280x720.
/// Layout panels/buttons remain responsive through their anchors.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCartaReferenceV3Responsive : MonoBehaviour
{
    private readonly Dictionary<int, int> baseFontSizes = new Dictionary<int, int>();
    private readonly Dictionary<int, Vector3> baseIconScales = new Dictionary<int, Vector3>();
    private Canvas rootCanvas;
    private float lastScale = -1f;
    private int lastTextCount = -1;
    private int lastIconCount = -1;
    private TextMeshProUGUI approvedHeader;
    private float approvedHeaderBaseSize = -1f;

    private void OnEnable() => ApplyImmediate(true);
    private void LateUpdate() => ApplyImmediate(false);
    private void OnRectTransformDimensionsChange() => ApplyImmediate(true);

    public void ApplyImmediate(bool force = true)
    {
        if (rootCanvas == null)
        {
            Canvas nearest = GetComponentInParent<Canvas>();
            rootCanvas = nearest != null ? nearest.rootCanvas : null;
        }
        if (rootCanvas == null) return;
        if (approvedHeader == null)
        {
            Transform headerNode = transform.Find("Header/ReferenceHeaderTitleTMP");
            approvedHeader = headerNode?.GetComponent<TextMeshProUGUI>();
            if (approvedHeader != null)
                approvedHeaderBaseSize = Mathf.Max(1f, approvedHeader.fontSize);
        }

        float scale = Mathf.Max(0.5f, rootCanvas.scaleFactor);
        Text[] texts = GetComponentsInChildren<Text>(true);
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        int iconCount = 0;
        for (int i = 0; i < rects.Length; i++)
            if (IsStablePhysicalIcon(rects[i])) iconCount++;

        // 1920x1080 uses scale 1.0. At 1280x720 the global Canvas is about
        // 0.667, so inverse compensation keeps Carta's type at the same
        // physical size as the approved HTML/reference while its panels shrink.
        float compensate = Mathf.Clamp(1f / scale, 1f, 1.55f);

        // Bind()/selection changes reapply the base visual theme after the
        // first layout pass. Inspect the actual Text.fontSize before taking
        // the fast path; otherwise those rows silently shrink at 1280x720
        // until some unrelated resolution or hierarchy change occurs.
        if (!force && Mathf.Abs(scale - lastScale) < 0.001f &&
            texts.Length == lastTextCount && iconCount == lastIconCount &&
            !NeedsTextReapply(texts, compensate) &&
            !NeedsApprovedHeaderReapply(compensate))
            return;

        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null) continue;
            // Detail captions must remain readable at 1280x720. Their fixed
            // 12px best-fit ceiling previously cancelled Canvas compensation.
            // Long captions can still shrink to 9px and wrap inside their
            // own RectTransform rather than colliding with the input column.
            if (text.name.EndsWith("Caption", System.StringComparison.Ordinal) &&
                text.resizeTextForBestFit)
            {
                text.resizeTextMinSize = 13;
                text.resizeTextMaxSize =
                    Mathf.RoundToInt(19f * compensate);
            }
            ApplyDishRowFitBounds(text, compensate);
            ApplyDishDetailFitBounds(text, compensate);
            int id = text.GetInstanceID();
            if (!baseFontSizes.TryGetValue(id, out int baseSize))
            {
                baseSize = Mathf.Max(1, text.fontSize);
                baseFontSizes[id] = baseSize;
            }
            // Compact form values have a 28-unit field and cannot retain
            // the full inverse Canvas compensation at 1280x720. Otherwise
            // uGUI Truncate culls the entire line (value bound, no pixels).
            float textScale = AdaptiveTextScale(text, compensate);
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseSize * textScale));
        }

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (!IsStablePhysicalIcon(rect)) continue;
            int id = rect.GetInstanceID();
            if (!baseIconScales.TryGetValue(id, out Vector3 baseScale))
            {
                baseScale = rect.localScale;
                baseIconScales[id] = baseScale;
            }
            rect.localScale = baseScale * compensate;
        }

        if (approvedHeader != null && approvedHeaderBaseSize > 0f)
            approvedHeader.fontSize = approvedHeaderBaseSize *
                Mathf.Min(compensate, 1.13f);

        // The Windows player otherwise occasionally paints empty price and
        // read-only value fields on the first frame at 1280x720, despite
        // having correct bound strings. Rebuild geometry on scale changes,
        // never every frame, so the corrective pass does not cost gameplay FPS.
        if (force || Mathf.Abs(scale - lastScale) > .001f)
        {
            foreach (Text source in texts)
                if (source != null) source.SetAllDirty();
            foreach (TMP_Text crisp in GetComponentsInChildren<TMP_Text>(true))
            {
                if (crisp == null) continue;
                crisp.SetAllDirty();
                crisp.ForceMeshUpdate(true, true);
            }
        }

        lastScale = scale;
        lastTextCount = texts.Length;
        lastIconCount = iconCount;
    }

    private static float AdaptiveTextScale(Text text, float compensate)
    {
        if (IsCompactDetailValue(text)) return Mathf.Min(compensate, 1.10f);
        if (text == null) return compensate;
        if (text.name == "DetailHeading")
            return Mathf.Min(compensate, 1.12f);
        if (text.name == "Label" && text.transform.parent != null)
        {
            string parent = text.transform.parent.name;
            if (parent.StartsWith("Category_", System.StringComparison.Ordinal))
                return Mathf.Min(compensate, 1.05f);
            if (parent.StartsWith("CartaTab", System.StringComparison.Ordinal))
                return Mathf.Min(compensate, 1.12f);
        }
        return compensate;
    }

    private static bool IsCompactDetailValue(Text text)
    {
        if (text == null) return false;
        switch (text.name)
        {
            case "CategoryValue":
            case "ModeValue":
            case "OrderValue":
            case "CostValue":
            case "MarginValue":
                return true;
            case "Text":
                Transform parent = text.transform.parent;
                return parent != null && parent.GetComponent<InputField>() != null &&
                    (parent.name == "Price" || parent.name == "PreparationTime");
            default:
                return false;
        }
    }

    private bool NeedsApprovedHeaderReapply(float compensate)
    {
        if (approvedHeader == null || approvedHeaderBaseSize <= 0f)
            return false;
        return Mathf.Abs(approvedHeader.fontSize -
            approvedHeaderBaseSize * Mathf.Min(compensate, 1.13f)) > .1f;
    }

    private bool NeedsTextReapply(Text[] texts, float compensate)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null) continue;
            if (!baseFontSizes.TryGetValue(text.GetInstanceID(), out int baseSize))
                return true;
            float textScale = AdaptiveTextScale(text, compensate);
            int expected = Mathf.Max(1, Mathf.RoundToInt(baseSize * textScale));
            if (text.fontSize != expected)
                return true;
            if (TryGetDishRowFitBounds(text, out int min, out int max) &&
                (text.resizeTextMinSize != Mathf.RoundToInt(min * compensate) ||
                 text.resizeTextMaxSize != Mathf.RoundToInt(max * compensate)))
                return true;
            if (TryGetDishDetailFitBounds(text, out min, out max) &&
                (text.resizeTextMinSize != Mathf.RoundToInt(min * compensate) ||
                 text.resizeTextMaxSize != Mathf.RoundToInt(max * compensate)))
                return true;
        }
        return false;
    }

    // DishRowView applies canonical best-fit limits on each Bind(). Apply
    // the same scale factor to those limits as to Text.fontSize. Otherwise
    // the capped fit renderer silently shrinks the visible text back to its
    // 1920px size when the global Canvas is scaled down to 1280px.
    private static void ApplyDishRowFitBounds(Text text, float compensate)
    {
        if (!TryGetDishRowFitBounds(text, out int min, out int max))
            return;
        text.resizeTextMinSize = Mathf.RoundToInt(min * compensate);
        text.resizeTextMaxSize = Mathf.RoundToInt(max * compensate);
    }

    private static bool TryGetDishRowFitBounds(Text text, out int min, out int max)
    {
        min = 0;
        max = 0;
        if (text == null || !text.resizeTextForBestFit ||
            text.transform.parent == null ||
            text.transform.parent.GetComponent<BistroBuilderMenuEditorDishRowView>() == null)
            return false;

        switch (text.name)
        {
            case "Name": min = 12; max = 19; return true;
            case "Subtitle": min = 10; max = 15; return true;
            case "Price": min = 11; max = 17; return true;
            case "Modality":
            case "Preparation": min = 10; max = 15; return true;
            case "Margin": min = 11; max = 16; return true;
            default: return false;
        }
    }

    private static void ApplyDishDetailFitBounds(Text text, float compensate)
    {
        if (!TryGetDishDetailFitBounds(text, out int min, out int max))
            return;
        text.resizeTextMinSize = Mathf.RoundToInt(min * compensate);
        text.resizeTextMaxSize = Mathf.RoundToInt(max * compensate);
    }

    private static bool TryGetDishDetailFitBounds(Text text, out int min, out int max)
    {
        min = 0;
        max = 0;
        if (text == null || !text.resizeTextForBestFit ||
            text.transform.parent == null)
            return false;
        string parentName = text.transform.parent.name;
        if (parentName == "DetailCanvas")
        {
            if (text.name == "DishTitle")
            {
                min = 17;
                max = 29;
                return true;
            }
            if (text.name == "DishSubtitle")
            {
                min = 13;
                max = 19;
                return true;
            }
        }
        if (parentName == "StatusBadgePlate" && text.name == "StatusBadge")
        {
            min = 12;
            max = 18;
            return true;
        }
        return false;
    }

    private static bool IsStablePhysicalIcon(RectTransform rect)
    {
        if (rect == null) return false;
        string name = rect.name;
        return name == "ReferenceActionIcon" ||
               name == "CartaHeaderIcon" ||
               name == "SectionIcon" ||
               name == "EntryIcon" ||
               name == "ReferenceRowChevron" ||
               name == "ReferenceRowStatus" ||
               // Approved Carta pictograms were missing from the inverse
               // scale compensation and became too small at 1280x720.
               name == "ReferenceCategoryIcon" ||
               name == "ReferenceFieldIcon" ||
               name == "ReferencePreparationClock" ||
               name == "ReferenceTabIcon";
    }
}
