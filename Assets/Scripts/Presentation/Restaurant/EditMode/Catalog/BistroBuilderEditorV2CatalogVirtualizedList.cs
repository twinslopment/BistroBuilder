using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B9: ScrollRect virtualizado sobre el catálogo UI existente.
/// El número de tarjetas instanciadas depende solo del viewport, nunca
/// del número de artículos. Ni reparenting de assets ni segundo catálogo.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2CatalogVirtualizedList : MonoBehaviour
{
    private const int OverscanRows = 2;
    private const int MaxVisibleCards = 128;
    private const int MaxSpriteCache = 64;

    private readonly List<RestaurantPlaceableItemDefinition> results =
        new List<RestaurantPlaceableItemDefinition>(128);
    private readonly List<RestaurantPlaceableCatalogItemView> pool =
        new List<RestaurantPlaceableCatalogItemView>(24);
    private readonly List<int> boundIndices = new List<int>(24);
    private readonly Dictionary<string, Sprite> spriteCache =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Queue<string> spriteOrder = new Queue<string>();

    private RectTransform container;
    private RectTransform viewport;
    private RestaurantPlaceableCatalogItemView template;
    private ScrollRect scroll;
    private GridLayoutGroup grid;
    private ContentSizeFitter fitter;
    private RestaurantPlaceableCatalogApprovedSkin approvedSkin;
    private RestaurantPlaceableCatalogPreviewSkin previewSkin;
    private Action<RestaurantPlaceableItemDefinition> onSelected;
    private bool initialized;
    private bool cardsInteractable = true;
    private int columns = 1;
    private int firstIndex = -1;
    private int lastVisibleCount = -1;
    private float lastWidth = -1f;
    private float lastHeight = -1f;
    private float lastCellWidth = -1f;
    private float lastCellHeight = -1f;
    private float lastScrollY = -1f;
    private float rowStride = 257f;
    private float colStride = 190f;
    private int leftPadding;
    private int topPadding;
    private int bottomPadding;

    public bool IsReady => initialized && container != null && viewport != null;
    public int FilteredCount => results.Count;
    public int PoolCount => pool.Count;
    public int VisibleCount { get; private set; }
    public int RebindCount { get; private set; }
    public float ContentHeight => container != null ? container.rect.height : 0f;
    public int ColumnCount => columns;
    public int FirstVisibleIndex => firstIndex < 0 ? 0 : firstIndex;

    public void InvalidateThumbnails()
    {
        spriteCache.Clear();
        spriteOrder.Clear();
        // Una republicación SAVIC puede conservar ItemId y referencia de
        // definición, pero cambiar texto, precio o miniatura en el mismo objeto.
        for (int i = 0; i < boundIndices.Count; i++)
            boundIndices[i] = -1;
    }

    private Sprite ResolveThumbnail(RestaurantPlaceableItemDefinition item)
    {
        if (item == null) return null;
        string key = item.ItemId;
        if (spriteCache.TryGetValue(key, out Sprite sprite)) return sprite;
        // Las definiciones canónicas exponen Sprite directamente. Por ahora
        // se retrasa su vinculación gráfica hasta entrar en el viewport;
        // SAVIC podrá aportar un proveedor de streaming cuando exista.
        sprite = item.CatalogIcon;
        while (spriteCache.Count >= MaxSpriteCache && spriteOrder.Count > 0)
            spriteCache.Remove(spriteOrder.Dequeue());
        spriteCache[key] = sprite;
        spriteOrder.Enqueue(key);
        return sprite;
    }

    public bool ResultsMatch(IReadOnlyList<RestaurantPlaceableItemDefinition> items)
    {
        if (items == null || items.Count != results.Count) return false;
        for (int i = 0; i < items.Count; i++)
            if (!ReferenceEquals(items[i], results[i])) return false;
        return true;
    }

    public void SetInteractable(bool enabled)
    {
        cardsInteractable = enabled;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) pool[i].SetInteractable(enabled);
    }

    public void Configure(RectTransform content,
        RestaurantPlaceableCatalogItemView cardTemplate,
        Action<RestaurantPlaceableItemDefinition> callback)
    {
        if (content == null || cardTemplate == null) return;
        if (initialized && ReferenceEquals(container, content) &&
            ReferenceEquals(template, cardTemplate))
        {
            onSelected = callback;
            return;
        }
        ReleasePool();
        container = content;
        template = cardTemplate;
        onSelected = callback;
        scroll = content.GetComponentInParent<ScrollRect>(true);
        viewport = scroll != null ? scroll.viewport : null;
        grid = content.GetComponent<GridLayoutGroup>();
        fitter = content.GetComponent<ContentSizeFitter>();
        approvedSkin = GetComponent<RestaurantPlaceableCatalogApprovedSkin>();
        previewSkin = GetComponent<RestaurantPlaceableCatalogPreviewSkin>();
        initialized = scroll != null && viewport != null;
        if (!initialized) return;

        if (grid != null) grid.enabled = false;
        if (fitter != null) fitter.enabled = false;
        scroll.onValueChanged.RemoveListener(OnScroll);
        scroll.onValueChanged.AddListener(OnScroll);
        container.anchorMin = new Vector2(0f, 1f);
        container.anchorMax = new Vector2(1f, 1f);
        container.pivot = new Vector2(0.5f, 1f);
        container.anchoredPosition = Vector2.zero;
        lastWidth = lastHeight = -1f;
        RefreshWindow(true);
    }

    public void SetResults(IReadOnlyList<RestaurantPlaceableItemDefinition> matches,
        bool preserveAnchor)
    {
        string anchor = null;
        if (preserveAnchor && firstIndex >= 0 && firstIndex < results.Count &&
            results[firstIndex] != null)
            anchor = results[firstIndex].ItemId;

        results.Clear();
        if (matches != null)
        {
            for (int i = 0; i < matches.Count; i++)
                if (matches[i] != null) results.Add(matches[i]);
        }

        if (IsReady)
        {
            LayoutContent();
            float y = 0f;
            if (anchor != null)
            {
                for (int i = 0; i < results.Count; i++)
                    if (string.Equals(results[i].ItemId, anchor, StringComparison.Ordinal))
                    {
                        y = (i / columns) * rowStride;
                        break;
                    }
            }
            scroll.StopMovement();
            container.anchoredPosition = new Vector2(
                container.anchoredPosition.x,
                Mathf.Clamp(y, 0f, Mathf.Max(0f, ContentHeight - viewport.rect.height)));
            RefreshWindow(true);
        }
    }

    /// <summary>Public useful for screen-resize tests and authoring skins.</summary>
    public void InvalidateGeometry()
    {
        lastWidth = lastHeight = lastCellWidth = lastCellHeight = -1f;
        if (!IsReady) return;
        LayoutContent();
        RefreshWindow(true);
    }

    private void LateUpdate()
    {
        if (!IsReady) return;
        float width = viewport.rect.width;
        float height = viewport.rect.height;
        if (!Mathf.Approximately(width, lastWidth) ||
            !Mathf.Approximately(height, lastHeight) ||
            (grid != null && (!Mathf.Approximately(grid.cellSize.x, lastCellWidth) ||
                              !Mathf.Approximately(grid.cellSize.y, lastCellHeight))))
        {
            LayoutContent();
            RefreshWindow(true);
            return;
        }

        if (!Mathf.Approximately(container.anchoredPosition.y, lastScrollY))
            RefreshWindow(false);
    }

    private void LayoutContent()
    {
        if (!IsReady) return;
        // PreviewSkin ajusta las medidas existentes; nosotros solo deshabilitamos
        // los LayoutGroups para evitar recalcular todos los elementos inexistentes.
        if (grid != null) grid.enabled = false;
        if (fitter != null) fitter.enabled = false;
        float cellWidth = grid != null ? grid.cellSize.x : 178.5f;
        float cellHeight = grid != null ? grid.cellSize.y : 245f;
        float gapX = grid != null ? grid.spacing.x : 11f;
        float gapY = grid != null ? grid.spacing.y : 12f;
        leftPadding = grid != null ? grid.padding.left : 0;
        topPadding = grid != null ? grid.padding.top : 0;
        bottomPadding = grid != null ? grid.padding.bottom : 0;

        lastWidth = Mathf.Max(1f, viewport.rect.width);
        lastHeight = Mathf.Max(1f, viewport.rect.height);
        lastCellWidth = cellWidth;
        lastCellHeight = cellHeight;
        rowStride = Mathf.Max(1f, cellHeight + gapY);
        colStride = Mathf.Max(1f, cellWidth + gapX);
        int rightPadding = grid != null ? grid.padding.right : 0;
        columns = Mathf.Clamp(Mathf.FloorToInt(
            (lastWidth - leftPadding - rightPadding + gapX) / colStride), 1, 16);
        int rows = (results.Count + columns - 1) / columns;
        float height = topPadding + bottomPadding +
            (rows > 0 ? rows * rowStride - gapY : 0f);
        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, Mathf.Max(height, lastHeight));
        if (container.anchoredPosition.y > Mathf.Max(0f, height - lastHeight))
        {
            scroll.StopMovement();
            container.anchoredPosition = new Vector2(
                container.anchoredPosition.x, Mathf.Max(0f, height - lastHeight));
        }
    }

    private void OnScroll(Vector2 _) => RefreshWindow(false);

    public void RefreshWindow(bool force)
    {
        if (!IsReady) return;
        if (grid != null && grid.enabled) grid.enabled = false;
        if (fitter != null && fitter.enabled) fitter.enabled = false;

        float y = Mathf.Max(0f, container.anchoredPosition.y);
        int startRow = Mathf.Max(0, Mathf.FloorToInt(
            Mathf.Max(0f, y - topPadding) / rowStride) - OverscanRows);
        int rowCount = Mathf.CeilToInt(
            Mathf.Max(1f, viewport.rect.height) / rowStride) + OverscanRows * 2 + 2;
        int start = startRow * columns;
        int count = Mathf.Min(results.Count - start, rowCount * columns);
        count = Mathf.Clamp(count, 0, MaxVisibleCards);

        if (!force && start == firstIndex && count == lastVisibleCount &&
            Mathf.Approximately(lastScrollY, y))
            return;

        GrowPool(count);

        // Reusar por índice absoluto: evita rebinding si el rango solo
        // se desplaza una fila y las mismas tarjetas continúan visibles.
        for (int slot = 0; slot < count; slot++)
        {
            int dataIndex = start + slot;
            var view = pool[slot];
            if (boundIndices[slot] != dataIndex ||
                !ReferenceEquals(view.Definition, results[dataIndex]))
            {
                view.Bind(results[dataIndex], onSelected, ResolveThumbnail);
                view.SetInteractable(cardsInteractable);
                boundIndices[slot] = dataIndex;
                approvedSkin?.StyleVirtualCard(view);
                previewSkin?.StyleVirtualCard(view);
                RebindCount++;
            }

            RectTransform rect = view.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(lastCellWidth, lastCellHeight);
                int row = dataIndex / columns;
                int col = dataIndex % columns;
                rect.anchoredPosition = new Vector2(
                    leftPadding + col * colStride,
                    -(topPadding + row * rowStride));
            }
            if (!view.gameObject.activeSelf)
                view.gameObject.SetActive(true);
        }

        for (int slot = count; slot < pool.Count; slot++)
        {
            if (pool[slot] != null && pool[slot].gameObject.activeSelf)
                pool[slot].gameObject.SetActive(false);
        }

        VisibleCount = count;
        firstIndex = start;
        lastVisibleCount = count;
        lastScrollY = y;
    }

    private void GrowPool(int desired)
    {
        while (pool.Count < desired)
        {
            var view = Instantiate(template, container);
            view.name = "EditorV2_B9_PooledCard_" + pool.Count;
            view.gameObject.SetActive(false);
            pool.Add(view);
            boundIndices.Add(-1);
        }
    }

    public void RefreshStyledCards()
    {
        for (int i = 0; i < VisibleCount; i++)
        {
            var view = pool[i];
            if (view == null || view.Definition == null) continue;
            approvedSkin?.StyleVirtualCard(view);
            previewSkin?.StyleVirtualCard(view);
        }
    }

    private void ReleasePool()
    {
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScroll);
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) Destroy(pool[i].gameObject);
        pool.Clear();
        boundIndices.Clear();
        results.Clear();
        firstIndex = lastVisibleCount = -1;
        initialized = false;
    }

    private void OnDestroy() => ReleasePool();
}
