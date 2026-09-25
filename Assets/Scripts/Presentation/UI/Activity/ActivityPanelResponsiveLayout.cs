using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActivityPanelResponsiveLayout : MonoBehaviour
{
    public const float BaselineWidth = 360f;
    public const float MinimumWidth = 356f;
    public const float MaximumWidth = 392f;
    public const float LeftMargin = 12f;
    public const float TopClearance = 76f;
    public const float BottomClearance = 76f;

    private RectTransform panel;
    private Canvas canvas;
    private Vector2 lastCanvasSize;

    public struct LayoutPlan
    {
        public Vector2 logicalCanvasSize;
        public float panelWidth;
        public float panelHeight;
    }

    private void Awake()
    {
        panel = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        Apply(true);
    }
    private void LateUpdate()
    {
        Apply(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            Apply(false);
    }

    public void Apply(bool force)
    {
        if (panel == null)
            panel = transform as RectTransform;
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (panel == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 canvasSize = canvasRect.rect.size;
        LayoutPlan plan = EvaluateLogicalCanvas(canvasSize);
        bool canvasUnchanged =
            (canvasSize - lastCanvasSize).sqrMagnitude < 0.25f;

        if (!force && canvasUnchanged && MatchesPlan(plan))
            return;

        lastCanvasSize = canvasSize;
        ApplyPlan(plan);
    }
    private void ApplyPlan(LayoutPlan plan)
    {
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(
            LeftMargin,
            (BottomClearance - TopClearance) * 0.5f);
        panel.sizeDelta = new Vector2(
            plan.panelWidth,
            -(TopClearance + BottomClearance));
    }

    private bool MatchesPlan(LayoutPlan plan)
    {
        if (panel == null)
            return false;

        return
            Vector2.Distance(panel.anchorMin, new Vector2(0f, 0f)) < 0.001f &&
            Vector2.Distance(panel.anchorMax, new Vector2(0f, 1f)) < 0.001f &&
            Vector2.Distance(panel.pivot, new Vector2(0f, 0.5f)) < 0.001f &&
            Mathf.Abs(panel.anchoredPosition.x - LeftMargin) < 0.25f &&
            Mathf.Abs(panel.anchoredPosition.y) < 0.25f &&
            Mathf.Abs(panel.sizeDelta.x - plan.panelWidth) < 0.25f &&
            Mathf.Abs(
                panel.sizeDelta.y +
                TopClearance +
                BottomClearance) < 0.25f;
    }

    public static LayoutPlan EvaluateLogicalCanvas(Vector2 logicalCanvasSize)
    {
        float referenceWidth =
            Mathf.Max(1f, BistroBuilderUiTokens.ReferenceResolution.x);
        float proportionalWidth =
            logicalCanvasSize.x * (BaselineWidth / referenceWidth);
        float width = Mathf.Clamp(
            proportionalWidth,
            MinimumWidth,
            MaximumWidth);
        float height = Mathf.Max(
            0f,
            logicalCanvasSize.y - TopClearance - BottomClearance);

        return new LayoutPlan
        {
            logicalCanvasSize = logicalCanvasSize,
            panelWidth = width,
            panelHeight = height
        };
    }
    public static LayoutPlan EvaluatePixelResolution(
        int pixelWidth,
        int pixelHeight)
    {
        Vector2 reference = BistroBuilderUiTokens.ReferenceResolution;
        float widthRatio = Mathf.Max(
            0.0001f,
            pixelWidth / reference.x);
        float heightRatio = Mathf.Max(
            0.0001f,
            pixelHeight / reference.y);

        // CanvasScaler MatchWidthOrHeight = 0.5:
        // geometric mean of width and height scale ratios.
        float scale = Mathf.Sqrt(widthRatio * heightRatio);
        Vector2 logical = new Vector2(
            pixelWidth / scale,
            pixelHeight / scale);
        return EvaluateLogicalCanvas(logical);
    }
}
