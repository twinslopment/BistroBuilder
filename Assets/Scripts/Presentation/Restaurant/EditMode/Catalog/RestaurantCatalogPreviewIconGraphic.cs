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
    Check,
    Cursor, Plant, Picture, Divider, Textile, Accessory, Pendant, Sconce, FloorLamp, Sun,
    Checkout, Dining, Reception, Cart, Shield, Sign, Organization, Display, Tools, Auxiliary
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
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
            case RestaurantCatalogPreviewIcon.Plant:
                RectOutline(vh,.33f,.12f,.67f,.39f); Line(vh,P(.5f,.39f),P(.5f,.83f));
                Line(vh,P(.5f,.5f),P(.23f,.72f),T*2.8f);Line(vh,P(.5f,.55f),P(.76f,.79f),T*2.8f);break;
            case RestaurantCatalogPreviewIcon.Picture:
                RectOutline(vh,.15f,.13f,.85f,.87f);Line(vh,P(.21f,.3f),P(.44f,.53f));Line(vh,P(.44f,.53f),P(.62f,.35f));Line(vh,P(.62f,.35f),P(.8f,.66f));Circle(vh,P(.35f,.71f),.06f);break;
            case RestaurantCatalogPreviewIcon.Divider:
                for(float x=.17f;x<.9f;x+=.13f)Line(vh,P(x,.14f),P(x,.85f),T*1.7f);break;
            case RestaurantCatalogPreviewIcon.Textile:
                RectOutline(vh,.22f,.22f,.78f,.78f);Line(vh,P(.22f,.22f),P(.12f,.14f));Line(vh,P(.78f,.22f),P(.88f,.14f));Line(vh,P(.34f,.3f),P(.34f,.69f));Line(vh,P(.66f,.3f),P(.66f,.69f));break;
            case RestaurantCatalogPreviewIcon.Accessory:
                Circle(vh,P(.5f,.5f),.26f);Line(vh,P(.42f,.76f),P(.42f,.9f));Line(vh,P(.58f,.76f),P(.58f,.9f));Line(vh,P(.42f,.9f),P(.58f,.9f));break;
            case RestaurantCatalogPreviewIcon.Pendant:
                Line(vh,P(.5f,.9f),P(.5f,.65f));Line(vh,P(.5f,.65f),P(.17f,.3f));Line(vh,P(.17f,.3f),P(.83f,.3f));Line(vh,P(.83f,.3f),P(.5f,.65f));Line(vh,P(.5f,.3f),P(.5f,.18f));break;
            case RestaurantCatalogPreviewIcon.Sconce:
                Line(vh,P(.22f,.22f),P(.22f,.82f),T*2);Line(vh,P(.22f,.52f),P(.55f,.52f));Line(vh,P(.55f,.3f),P(.55f,.75f));Line(vh,P(.55f,.75f),P(.8f,.62f));Line(vh,P(.8f,.62f),P(.8f,.4f));Line(vh,P(.8f,.4f),P(.55f,.3f));break;
            case RestaurantCatalogPreviewIcon.FloorLamp: DrawLighting(vh);break;
            case RestaurantCatalogPreviewIcon.Sun:
                Circle(vh,P(.5f,.5f),.2f);
                for(int n=0;n<8;n++){float a=n*Mathf.PI/4;Line(vh,P(.5f+Mathf.Cos(a)*.31f,.5f+Mathf.Sin(a)*.31f),P(.5f+Mathf.Cos(a)*.44f,.5f+Mathf.Sin(a)*.44f));}break;
            case RestaurantCatalogPreviewIcon.Checkout:
                RectOutline(vh,.15f,.15f,.85f,.48f);RectOutline(vh,.33f,.53f,.7f,.83f);Line(vh,P(.28f,.27f),P(.7f,.27f));break;
            case RestaurantCatalogPreviewIcon.Dining:
                Line(vh,P(.1f,.25f),P(.9f,.25f));Line(vh,P(.2f,.32f),P(.8f,.32f));Line(vh,P(.2f,.32f),P(.33f,.64f));Line(vh,P(.33f,.64f),P(.67f,.64f));Line(vh,P(.67f,.64f),P(.8f,.32f));Line(vh,P(.5f,.64f),P(.5f,.8f));break;
            case RestaurantCatalogPreviewIcon.Reception: DrawTable(vh);RectOutline(vh,.38f,.75f,.62f,.9f);break;
            case RestaurantCatalogPreviewIcon.Cart:
                Line(vh,P(.08f,.85f),P(.25f,.85f));Line(vh,P(.25f,.85f),P(.37f,.34f));Line(vh,P(.37f,.34f),P(.79f,.34f));Line(vh,P(.25f,.72f),P(.86f,.72f));Line(vh,P(.86f,.72f),P(.79f,.34f));Circle(vh,P(.4f,.18f),.07f);Circle(vh,P(.75f,.18f),.07f);break;
            case RestaurantCatalogPreviewIcon.Shield:
                Line(vh,P(.2f,.8f),P(.5f,.92f));Line(vh,P(.5f,.92f),P(.8f,.8f));Line(vh,P(.8f,.8f),P(.75f,.36f));Line(vh,P(.75f,.36f),P(.5f,.12f));Line(vh,P(.5f,.12f),P(.25f,.36f));Line(vh,P(.25f,.36f),P(.2f,.8f));break;
            case RestaurantCatalogPreviewIcon.Sign:
                RectOutline(vh,.12f,.4f,.88f,.85f);Line(vh,P(.5f,.4f),P(.5f,.1f));Line(vh,P(.36f,.1f),P(.64f,.1f));Line(vh,P(.5f,.54f),P(.5f,.73f));break;
            case RestaurantCatalogPreviewIcon.Organization: DrawAll(vh);break;
            case RestaurantCatalogPreviewIcon.Display:
                RectOutline(vh,.23f,.12f,.77f,.89f);Line(vh,P(.23f,.38f),P(.77f,.38f));Line(vh,P(.23f,.64f),P(.77f,.64f));break;
            case RestaurantCatalogPreviewIcon.Tools:
                Line(vh,P(.2f,.15f),P(.8f,.85f),T*2);Line(vh,P(.2f,.85f),P(.8f,.15f),T*2);break;
            case RestaurantCatalogPreviewIcon.Auxiliary:
                RectOutline(vh,.24f,.18f,.76f,.75f);Line(vh,P(.24f,.75f),P(.38f,.9f));Line(vh,P(.38f,.9f),P(.64f,.9f));Line(vh,P(.64f,.9f),P(.76f,.75f));break;
            case RestaurantCatalogPreviewIcon.Cursor:
                DrawCursor(vh);
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
        radius *= Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
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

    private void DrawCursor(VertexHelper vh)
    {
        Line(vh, P(.22f, .82f), P(.44f, .18f));
        Line(vh, P(.44f, .18f), P(.56f, .42f));
        Line(vh, P(.56f, .42f), P(.80f, .34f));
        Line(vh, P(.80f, .34f), P(.22f, .82f));
        Line(vh, P(.53f, .42f), P(.70f, .66f));
    }
}
