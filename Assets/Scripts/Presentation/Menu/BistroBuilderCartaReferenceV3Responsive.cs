using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private void OnEnable() => ApplyImmediate(true);
    private void LateUpdate() => ApplyImmediate(false);
    private void OnRectTransformDimensionsChange() => ApplyImmediate(true);

    public void ApplyImmediate(bool force = true)
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        float scale = Mathf.Max(0.5f, rootCanvas.scaleFactor);
        Text[] texts = GetComponentsInChildren<Text>(true);
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        int iconCount = 0;
        for (int i = 0; i < rects.Length; i++)
            if (IsStablePhysicalIcon(rects[i])) iconCount++;

        if (!force && Mathf.Abs(scale - lastScale) < 0.001f &&
            texts.Length == lastTextCount && iconCount == lastIconCount)
            return;

        // 1920x1080 uses scale 1.0. At 1280x720 the global Canvas is about
        // 0.667, so inverse compensation keeps Carta's type at the same
        // physical size as the approved HTML/reference while its panels shrink.
        float compensate = Mathf.Clamp(1f / scale, 1f, 1.55f);

        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null) continue;
            int id = text.GetInstanceID();
            if (!baseFontSizes.TryGetValue(id, out int baseSize))
            {
                baseSize = Mathf.Max(1, text.fontSize);
                baseFontSizes[id] = baseSize;
            }
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseSize * compensate));
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

        lastScale = scale;
        lastTextCount = texts.Length;
        lastIconCount = iconCount;
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
               name == "ReferenceRowStatus";
    }
}
