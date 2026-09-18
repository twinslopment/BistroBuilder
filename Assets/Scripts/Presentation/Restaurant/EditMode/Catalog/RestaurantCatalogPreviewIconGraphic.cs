using UnityEngine;
using UnityEngine.UI;

public enum RestaurantCatalogPreviewIcon
{
    All,
    Table,
    Chair,
    Kitchen,
    Decoration,
    Lighting,
    Search,
    Filter,
    Close,
    Check
}

[DisallowMultipleComponent]
public sealed class RestaurantCatalogPreviewIconGraphic : MaskableGraphic
{
    [SerializeField]
    private RestaurantCatalogPreviewIcon icon;

    [SerializeField, Range(0.5f, 6f)]
    private float lineWidth = 1.8f;

    public RestaurantCatalogPreviewIcon Icon
    {
        get => icon;
        set
        {
            if (icon == value) return;
            icon = value;
            SetVerticesDirty();
        }
    }

    public float LineWidth
    {
        get => lineWidth;
        set
        {
            lineWidth = Mathf.Max(0.5f, value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        switch (icon)
        {
            case RestaurantCatalogPreviewIcon.All:
                DrawAll(vh);
                break;
            case RestaurantCatalogPreviewIcon.Table:
                DrawTable(vh);
                break;
            case RestaurantCatalogPreviewIcon.Chair:
                DrawChair(vh);
                break;
            case RestaurantCatalogPreviewIcon.Kitchen:
                DrawKitchen(vh);
                break;
            case RestaurantCatalogPreviewIcon.Decoration:
                DrawDecoration(vh);
                break;
            case RestaurantCatalogPreviewIcon.Lighting:
                DrawLighting(vh);
                break;
            case RestaurantCatalogPreviewIcon.Search:
                DrawSearch(vh);
                break;
            case RestaurantCatalogPreviewIcon.Filter:
                DrawFilter(vh);
                break;
            case RestaurantCatalogPreviewIcon.Close:
                DrawClose(vh);
                break;
            case RestaurantCatalogPreviewIcon.Check:
                DrawCheck(vh);
                break;
        }
    }

    private Vector2 P(float x, float y)
    {
        Rect r = rectTransform.rect;
        float s = Mathf.Min(r.width, r.height);
        float ox = r.x + (r.width - s) * 0.5f;
        float oy = r.y + (r.height - s) * 0.5f;
        return new Vector2(ox + x * s, oy + y * s);
    }

    private float T => lineWidth * Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 24f;

    private void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness = -1f)
    {
        float t = thickness > 0f ? thickness : T;
        Vector2 d = b - a;
        if (d.sqrMagnitude < 0.0001f) return;

        Vector2 n = new Vector2(-d.y, d.x).normalized * (t * 0.5f);
        int start = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = a - n; vh.AddVert(v);
        v.position = a + n; vh.AddVert(v);
        v.position = b + n; vh.AddVert(v);
        v.position = b - n; vh.AddVert(v);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private void RectOutline(VertexHelper vh, float x0, float y0, float x1, float y1)
    {
        Line(vh, P(x0, y0), P(x1, y0));
        Line(vh, P(x1, y0), P(x1, y1));
        Line(vh, P(x1, y1), P(x0, y1));
        Line(vh, P(x0, y1), P(x0, y0));
    }

    private void Circle(VertexHelper vh, Vector2 center, float radius, int segments = 20)
    {
        Vector2 prev = center + Vector2.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Line(vh, prev, next);
            prev = next;
        }
    }

    private void DrawAll(VertexHelper vh)
    {
        RectOutline(vh, .16f, .57f, .43f, .84f);
        RectOutline(vh, .57f, .57f, .84f, .84f);
        RectOutline(vh, .16f, .16f, .43f, .43f);
        RectOutline(vh, .57f, .16f, .84f, .43f);
    }

    private void DrawTable(VertexHelper vh)
    {
        RectOutline(vh, .16f, .28f, .84f, .72f);
        Line(vh, P(.16f, .56f), P(.84f, .56f));
        Line(vh, P(.36f, .28f), P(.36f, .12f));
        Line(vh, P(.64f, .28f), P(.64f, .12f));
    }

    private void DrawChair(VertexHelper vh)
    {
        Line(vh, P(.28f, .75f), P(.28f, .44f));
        Line(vh, P(.28f, .75f), P(.68f, .75f));
        Line(vh, P(.68f, .75f), P(.68f, .47f));
        Line(vh, P(.27f, .44f), P(.73f, .44f));
        Line(vh, P(.23f, .44f), P(.18f, .18f));
        Line(vh, P(.73f, .44f), P(.78f, .18f));
        Line(vh, P(.35f, .44f), P(.33f, .22f));
        Line(vh, P(.64f, .44f), P(.66f, .22f));
    }

    private void DrawKitchen(VertexHelper vh)
    {
        RectOutline(vh, .18f, .24f, .82f, .66f);
        Line(vh, P(.18f, .66f), P(.82f, .66f));
        Line(vh, P(.28f, .79f), P(.72f, .79f));
        Line(vh, P(.34f, .79f), P(.34f, .68f));
        Line(vh, P(.66f, .79f), P(.66f, .68f));
        Circle(vh, P(.36f, .45f), .08f);
        Circle(vh, P(.64f, .45f), .08f);
    }

    private void DrawDecoration(VertexHelper vh)
    {
        Circle(vh, P(.50f, .63f), .10f, 16);
        Circle(vh, P(.38f, .59f), .09f, 16);
        Circle(vh, P(.62f, .59f), .09f, 16);
        Line(vh, P(.50f, .53f), P(.50f, .18f));
        Line(vh, P(.50f, .36f), P(.34f, .28f));
        Line(vh, P(.50f, .31f), P(.66f, .23f));
        Line(vh, P(.34f, .28f), P(.28f, .18f));
        Line(vh, P(.66f, .23f), P(.72f, .14f));
    }

    private void DrawLighting(VertexHelper vh)
    {
        Line(vh, P(.50f, .82f), P(.50f, .66f));
        Line(vh, P(.34f, .66f), P(.66f, .66f));
        Line(vh, P(.34f, .66f), P(.24f, .42f));
        Line(vh, P(.66f, .66f), P(.76f, .42f));
        Line(vh, P(.24f, .42f), P(.76f, .42f));
        Line(vh, P(.50f, .42f), P(.50f, .17f));
        Line(vh, P(.36f, .17f), P(.64f, .17f));
    }

    private void DrawSearch(VertexHelper vh)
    {
        Circle(vh, P(.44f, .56f), .22f, 24);
        Line(vh, P(.60f, .40f), P(.80f, .20f));
    }

    private void DrawFilter(VertexHelper vh)
    {
        Line(vh, P(.18f, .72f), P(.82f, .72f));
        Line(vh, P(.28f, .50f), P(.72f, .50f));
        Line(vh, P(.40f, .28f), P(.60f, .28f));
    }

    private void DrawClose(VertexHelper vh)
    {
        Line(vh, P(.24f, .24f), P(.76f, .76f));
        Line(vh, P(.76f, .24f), P(.24f, .76f));
    }

    private void DrawCheck(VertexHelper vh)
    {
        Line(vh, P(.20f, .50f), P(.42f, .28f));
        Line(vh, P(.42f, .28f), P(.80f, .72f));
    }
}
