using UnityEngine;
using UnityEngine.UI;

/// <summary>Preserves usable form heights on short screens, with vertical scrolling.</summary>
public sealed class BistroBuilderManagementViewport : MonoBehaviour
{
    RectTransform content;
    float minimumHeight;
    public static void Install(RectTransform viewport, RectTransform panel, float height)
    {
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.viewport = viewport; scroll.content = panel;
        scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
        var sizing = viewport.gameObject.AddComponent<BistroBuilderManagementViewport>();
        sizing.content = panel; sizing.minimumHeight = height;
        panel.anchorMin = new Vector2(0, 1); panel.anchorMax = Vector2.one; panel.pivot = new Vector2(.5f, 1);
        sizing.Resize();
    }
    void OnRectTransformDimensionsChange() => Resize();
    void OnEnable() => Resize();
    void Resize()
    {
        if (content == null) return;
        float height = Mathf.Max(minimumHeight, ((RectTransform)transform).rect.height);
        content.sizeDelta = new Vector2(-36, height);
        content.anchoredPosition = new Vector2(0, 0);
    }
}
