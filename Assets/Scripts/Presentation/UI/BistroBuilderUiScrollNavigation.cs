using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Contrato 21C para scroll interno: vertical, suave, sin desplazar la cÃƒÂ¡mara
/// mientras el puntero estÃƒÂ¡ dentro del viewport del panel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public sealed class BistroBuilderUiScrollRegion : MonoBehaviour,
    IScrollHandler, IBeginDragHandler, IEndDragHandler
{
    public const string RuntimeRevision = "21C-SCROLL-NAV-V1";

    private ScrollRect scroll;
    private float targetNormalized = 1f;
    private bool wheelSmoothing;
    private bool dragging;
    private float nextHeaderScanAt;
    private GridLayoutGroup convertedGrid;
    private float convertedCellWidth;

    private void Awake()
    {
        scroll = GetComponent<ScrollRect>();
        ApplyContract();
    }

    private void OnEnable()
    {
        if (scroll == null) scroll = GetComponent<ScrollRect>();
        ApplyContract();
        SyncTarget();
    }

    public static BistroBuilderUiScrollRegion Configure(ScrollRect target)
    {
        if (target == null) return null;
        BistroBuilderUiScrollRegion region = target.GetComponent<BistroBuilderUiScrollRegion>();
        if (region == null) region = target.gameObject.AddComponent<BistroBuilderUiScrollRegion>();
        region.scroll = target;
        region.ApplyContract();
        return region;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!CanHandleWheel(eventData)) return;

        float overflow = GetVerticalOverflow();
        if (overflow <= 0.5f) return;

        float notches = NormalizeWheel(eventData.scrollDelta.y);
        if (Mathf.Abs(notches) <= 0.0001f) return;

        if (!wheelSmoothing)
            targetNormalized = scroll.verticalNormalizedPosition;

        targetNormalized = Mathf.Clamp01(
            targetNormalized + (notches * BistroBuilderUiTokens.ScrollWheelStepPixels / overflow));
        scroll.velocity = Vector2.zero;
        wheelSmoothing = true;
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        wheelSmoothing = false;
        if (scroll != null) targetNormalized = scroll.verticalNormalizedPosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        SyncTarget();
    }

    private void LateUpdate()
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;

        if (convertedGrid != null) RefreshGridColumns();
        if (Time.unscaledTime >= nextHeaderScanAt)
        {
            EnsureFixedHeader();
            nextHeaderScanAt = Time.unscaledTime + 0.5f;
        }

        if (dragging || !wheelSmoothing) return;

        float current = scroll.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-BistroBuilderUiTokens.ScrollSmoothingRate * Time.unscaledDeltaTime);
        float next = Mathf.Lerp(current, targetNormalized, t);
        scroll.verticalNormalizedPosition = next;
        scroll.velocity = Vector2.zero;

        if (Mathf.Abs(next - targetNormalized) <= 0.0005f)
        {
            scroll.verticalNormalizedPosition = targetNormalized;
            wheelSmoothing = false;
        }
    }

    private void ApplyContract()
    {
        if (scroll == null) return;

        bool wasHorizontalOnly = scroll.horizontal && !scroll.vertical;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = BistroBuilderUiTokens.ScrollDecelerationRate;
        scroll.elasticity = 0f;
        scroll.scrollSensitivity = 0f;

        if (scroll.horizontalScrollbar != null)
            scroll.horizontalScrollbar.gameObject.SetActive(false);

        EnsureViewportRaycast();
        if (wasHorizontalOnly) ConvertHorizontalContentToVerticalGrid();
        EnsureFixedHeader();
        SyncTarget();
    }

    private void EnsureViewportRaycast()
    {
        if (scroll.viewport == null) return;
        Graphic graphic = scroll.viewport.GetComponent<Graphic>();
        if (graphic == null)
        {
            Image image = scroll.viewport.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            graphic = image;
        }
        graphic.raycastTarget = true;
    }

    private bool CanHandleWheel(PointerEventData eventData)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null || eventData == null)
            return false;
        if (!scroll.IsActive() || !scroll.vertical) return false;

        Camera eventCamera = eventData.pressEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(
            scroll.viewport, eventData.position, eventCamera);
    }

    private float GetVerticalOverflow()
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null) return 0f;
        return Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
    }

    private static float NormalizeWheel(float raw)
    {
        if (Mathf.Abs(raw) <= 0.0001f) return 0f;
        float normalized = Mathf.Abs(raw) > 10f ? raw / 120f : raw;
        return Mathf.Clamp(normalized,
            -BistroBuilderUiTokens.ScrollMaximumNotchesPerEvent,
            BistroBuilderUiTokens.ScrollMaximumNotchesPerEvent);
    }

    private void SyncTarget()
    {
        if (scroll != null) targetNormalized = scroll.verticalNormalizedPosition;
    }

    private void ConvertHorizontalContentToVerticalGrid()
    {
        if (scroll.content == null || scroll.viewport == null) return;

        HorizontalLayoutGroup horizontal = scroll.content.GetComponent<HorizontalLayoutGroup>();
        if (horizontal == null) return;

        convertedCellWidth = ResolveCellWidth(scroll.content, 190f);
        float cellHeight = ResolveCellHeight(scroll.content, 142f);
        float spacing = Mathf.Max(8f, horizontal.spacing);
        horizontal.enabled = false;

        if (Application.isPlaying)
        {
            Destroy(horizontal);
            StartCoroutine(CompleteGridConversion(cellHeight, spacing));
            return;
        }

        DestroyImmediate(horizontal);
        ConfigureVerticalGrid(cellHeight, spacing);
    }

    private IEnumerator CompleteGridConversion(float cellHeight, float spacing)
    {
        yield return null;
        if (this == null || scroll == null || scroll.content == null) yield break;
        ConfigureVerticalGrid(cellHeight, spacing);
    }

    private void ConfigureVerticalGrid(float cellHeight, float spacing)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;

        convertedGrid = scroll.content.GetComponent<GridLayoutGroup>();
        if (convertedGrid == null) convertedGrid = scroll.content.gameObject.AddComponent<GridLayoutGroup>();
        convertedGrid.cellSize = new Vector2(convertedCellWidth, cellHeight);
        convertedGrid.spacing = new Vector2(spacing, spacing);
        convertedGrid.padding = new RectOffset(0, 12, 0, 12);
        convertedGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        convertedGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        convertedGrid.childAlignment = TextAnchor.UpperLeft;
        convertedGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        RectTransform content = scroll.content;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, content.sizeDelta.y);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        RefreshGridColumns();
    }

    private void RefreshGridColumns()
    {
        if (convertedGrid == null || scroll == null || scroll.viewport == null) return;
        float usableWidth = Mathf.Max(1f,
            scroll.viewport.rect.width - convertedGrid.padding.horizontal);
        float stride = Mathf.Max(1f, convertedCellWidth + convertedGrid.spacing.x);
        int columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + convertedGrid.spacing.x) / stride));
        if (convertedGrid.constraintCount != columns)
            convertedGrid.constraintCount = columns;
    }

    private static float ResolveCellWidth(RectTransform content, float fallback)
    {
        for (int i = 0; i < content.childCount; i++)
        {
            LayoutElement layout = content.GetChild(i).GetComponent<LayoutElement>();
            if (layout != null && layout.preferredWidth > 1f) return layout.preferredWidth;
        }
        return fallback;
    }

    private static float ResolveCellHeight(RectTransform content, float fallback)
    {
        for (int i = 0; i < content.childCount; i++)
        {
            LayoutElement layout = content.GetChild(i).GetComponent<LayoutElement>();
            if (layout != null && layout.preferredHeight > 1f) return layout.preferredHeight;
        }
        return fallback;
    }

    private void EnsureFixedHeader()
    {
        if (scroll == null || scroll.content == null) return;
        for (int i = 0; i < scroll.content.childCount; i++)
        {
            RectTransform child = scroll.content.GetChild(i) as RectTransform;
            if (child == null || !LooksLikeTableHeader(child.name)) continue;
            BistroBuilderUiStickyTableHeader.Attach(child, scroll);
            return;
        }
    }

    private static bool LooksLikeTableHeader(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string key = value.ToLowerInvariant();
        return key.Contains("header") || key.Contains("cabecera") ||
               key.Contains("columnhead") || key.Contains("columns") ||
               key.Contains("tablehead");
    }
}

/// <summary>
/// Mantiene una cabecera ya incluida en el Content visualmente fija en la parte
/// superior del viewport, sin sacarla del layout ni duplicar filas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderUiStickyTableHeader : MonoBehaviour
{
    private ScrollRect scroll;
    private RectTransform header;
    private Vector2 baseHeaderPosition;
    private Vector2 baseContentPosition;
    private Canvas liftCanvas;
    private bool captured;

    public static BistroBuilderUiStickyTableHeader Attach(RectTransform target, ScrollRect owner)
    {
        if (target == null || owner == null) return null;
        BistroBuilderUiStickyTableHeader sticky = target.GetComponent<BistroBuilderUiStickyTableHeader>();
        if (sticky == null) sticky = target.gameObject.AddComponent<BistroBuilderUiStickyTableHeader>();
        sticky.Bind(owner);
        return sticky;
    }

    private void Awake()
    {
        header = transform as RectTransform;
    }

    private void OnEnable()
    {
        CaptureBase();
    }

    private void Bind(ScrollRect owner)
    {
        if (scroll == owner && captured)
        {
            EnsureLiftCanvas();
            return;
        }

        scroll = owner;
        if (header == null) header = transform as RectTransform;
        CaptureBase();
        captured = true;
        EnsureLiftCanvas();
    }

    private void CaptureBase()
    {
        if (header == null) header = transform as RectTransform;
        if (header != null) baseHeaderPosition = header.anchoredPosition;
        if (scroll != null && scroll.content != null)
            baseContentPosition = scroll.content.anchoredPosition;
    }

    private void LateUpdate()
    {
        if (scroll == null || scroll.content == null || header == null) return;
        Vector2 contentDelta = scroll.content.anchoredPosition - baseContentPosition;
        header.anchoredPosition = new Vector2(
            baseHeaderPosition.x,
            baseHeaderPosition.y - contentDelta.y);
    }

    private void EnsureLiftCanvas()
    {
        if (header == null) return;
        liftCanvas = header.GetComponent<Canvas>();
        if (liftCanvas == null) liftCanvas = header.gameObject.AddComponent<Canvas>();
        Canvas parentCanvas = header.GetComponentInParent<Canvas>();
        liftCanvas.overrideSorting = true;
        liftCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 5 : 5;
    }
}
