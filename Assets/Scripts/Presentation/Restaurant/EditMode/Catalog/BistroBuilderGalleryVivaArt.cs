using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small, cached native UI parts. Original reference supplies its ornaments;
/// product artwork always comes from the canonical item definition.</summary>
public static class BistroBuilderGalleryVivaArt
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    public static readonly Color Ink = new Color32(30, 27, 22, 255);
    public static readonly Color Muted = new Color32(126, 116, 102, 255);
    public static readonly Color Brass = new Color32(145, 83, 35, 255);

    public static Sprite Surface(bool selected = false, bool dark = false)
    {
        string key = selected ? "honey" : dark ? "dark" : "ivory";
        if (Cache.TryGetValue(key, out var sprite)) return sprite;
        const int n = 64;
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = Mathf.Max(7 - x, x - (n - 8), 0);
            float dy = Mathf.Max(7 - y, y - (n - 8), 0);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = Mathf.Min(x, y, n - 1 - x, n - 1 - y);
            Color fill = dark ? Color.Lerp(new Color32(41, 27, 17, 255), new Color32(77, 46, 25, 255), (float)y / n)
                : selected ? Color.Lerp(new Color32(243, 171, 47, 255), new Color32(255, 233, 161, 255), (float)y / n)
                : Color.Lerp(new Color32(242, 232, 217, 255), new Color32(253, 248, 238, 255), (float)y / n);
            if (edge < 1.2f || distance > 6.0f) fill = selected ? new Color32(231, 163, 48, 255) : new Color32(216, 190, 154, 255);
            else if (edge < 2.4f || distance > 4.8f) fill = new Color32(255, 251, 236, 255);
            pixels[x + y * n] = distance > 7 ? new Color32(0, 0, 0, 0) : (Color32)fill;
        }
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "GaleriaViva_" + key, filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 10);
        Cache[key] = sprite; return sprite;
    }

    public static Sprite CardSurface(bool selected)
    {
        if(!selected)return Surface();
        if(Cache.TryGetValue("selected-card-cream",out var cached))return cached;
        const int n=64;var pixels=new Color32[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
            float dx=Mathf.Max(7-x,x-(n-8),0),dy=Mathf.Max(7-y,y-(n-8),0);
            float distance=Mathf.Sqrt(dx*dx+dy*dy),edge=Mathf.Min(x,y,n-1-x,n-1-y);
            Color fill=Color.Lerp(new Color32(255,240,199,255),new Color32(255,250,240,255),(float)y/n);
            if(edge<1.2f||distance>6)fill=new Color32(235,164,31,255);
            else if(edge<2.4f||distance>4.8f)fill=new Color32(255,252,235,255);
            pixels[x+y*n]=distance>7?Color.clear:fill;
        }
        var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Approved V2 selected card cream",filterMode=FilterMode.Bilinear};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        cached=Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*10);
        Cache["selected-card-cream"]=cached;return cached;
    }
    public static Sprite Ornament(int index)
    {
        string key = "ornament_" + index;
        if (Cache.TryGetValue(key, out var sprite)) return sprite;
        var source = Resources.Load<Texture2D>("BistroBuilder/UI/GalleryViva/ApprovedReference");
        if (source == null || !source.isReadable) return null;
        // Top-origin rectangles contain only the approved category silhouettes.
        RectInt[] rects = { new RectInt(14, 10, 38, 44), new RectInt(14, 160, 28, 28),
            new RectInt(14, 203, 28, 28), new RectInt(14, 249, 28, 28), new RectInt(14, 293, 29, 27),
            new RectInt(14, 333, 29, 32), new RectInt(14, 382, 28, 29), new RectInt(11, 421, 31, 34),
            new RectInt(11, 470, 33, 31), new RectInt(11, 516, 32, 31) };
        RectInt r = rects[Mathf.Clamp(index, 0, rects.Length - 1)];
        var pixels = source.GetPixels(r.x, source.height - r.y - r.height, r.width, r.height);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float a = Mathf.Clamp01((.65f - c.g) / .35f);
            pixels[i] = new Color(.49f, .27f, .10f, a);
        }
        var texture = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, r.width, r.height), Vector2.one * .5f);
        Cache[key] = sprite; return sprite;
    }

    public static Sprite Circle()
    {
        if (Cache.TryGetValue("circle", out var sprite)) return sprite;
        const int n = 64; var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * 32);
            Color c = distance > 29.5f ? new Color32(221, 193, 152, 255) : new Color32(255, 250, 237, 255);
            c.a = Mathf.Clamp01(32 - distance); pixels[x + y * n] = c;
        }
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f);
        Cache["circle"] = sprite; return sprite;
    }
}

/// <summary>Vector UI symbols, independent of font glyphs and emoji.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroBuilderGalleryVivaSymbol : MaskableGraphic
{
    public enum Shape { Search, Filter, Heart, Star, Grid, Clock, Close, Left, Right, Down, Spark, Zoom, Bag, Link }
    public Shape Kind;
    public bool Filled;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        Vector2 P(float x, float y) => new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
        void Line(Vector2 a, Vector2 b, float thickness = .085f)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * Mathf.Min(r.width, r.height) * thickness * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero); vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero); vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        void Circle(float x, float y, float radius)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32, b = (i + 1) * Mathf.PI * 2 / 32;
                Line(P(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius), P(x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius), .075f);
            }
        }
        void Polygon(Vector2[] points, bool fill)
        {
            if (!fill) { for (int i = 0; i < points.Length; i++) Line(points[i], points[(i + 1) % points.Length], .075f); return; }
            Vector2 center = Vector2.zero; foreach (var p in points) center += p; center /= points.Length;
            int start = vh.currentVertCount; vh.AddVert(center, color, Vector2.zero);
            foreach (var p in points) vh.AddVert(p, color, Vector2.zero);
            for (int i = 0; i < points.Length; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
        }
        switch (Kind)
        {
            case Shape.Link:
                for(int ring=0;ring<2;ring++)for(int i=0;i<32;i++) {
                    float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;
                    Vector2 Q(float t) {
                        float u=Mathf.Cos(t)*.15f,v=Mathf.Sin(t)*.27f;
                        return P(.38f+ring*.24f+(u-v)*.7071f,.38f+ring*.24f+(u+v)*.7071f);
                    }
                    Line(Q(a),Q(b),.055f);
                }
                Line(P(.36f,.36f),P(.65f,.65f),.075f);break;
            case Shape.Zoom:
                Circle(.41f,.58f,.28f);Line(P(.61f,.36f),P(.89f,.08f),.10f);
                Line(P(.27f,.58f),P(.55f,.58f),.055f);Line(P(.41f,.44f),P(.41f,.72f),.055f);break;
            case Shape.Bag:
                Polygon(new[]{P(.2f,.12f),P(.8f,.12f),P(.75f,.68f),P(.25f,.68f)},true);
                Line(P(.36f,.65f),P(.36f,.88f));Line(P(.36f,.88f),P(.64f,.88f));Line(P(.64f,.88f),P(.64f,.65f));break;
            case Shape.Search: Circle(.41f, .58f, .28f); Line(P(.61f, .36f), P(.89f, .08f), .10f); break;
            case Shape.Filter: Line(P(.12f, .77f), P(.89f, .77f), .11f); Line(P(.23f, .56f), P(.77f, .56f), .11f); Polygon(new[] { P(.36f, .37f), P(.64f, .37f), P(.5f, .16f) }, true); break;
            case Shape.Close: Line(P(.23f, .23f), P(.77f, .77f), .12f); Line(P(.23f, .77f), P(.77f, .23f), .12f); break;
            case Shape.Left: Line(P(.63f, .22f), P(.36f, .5f), .11f); Line(P(.36f, .5f), P(.63f, .78f), .11f); break;
            case Shape.Right: Line(P(.37f, .22f), P(.64f, .5f), .11f); Line(P(.64f, .5f), P(.37f, .78f), .11f); break;
            case Shape.Down: Line(P(.2f, .64f), P(.5f, .34f)); Line(P(.5f, .34f), P(.8f, .64f)); break;
            case Shape.Clock: Circle(.5f, .5f, .36f); Line(P(.5f, .5f), P(.5f, .76f)); Line(P(.5f, .5f), P(.7f, .39f)); break;
            case Shape.Grid:
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                    Polygon(new[] { P(.1f + x * .44f, .1f + y * .44f), P(.46f + x * .44f, .1f + y * .44f), P(.46f + x * .44f, .46f + y * .44f), P(.1f + x * .44f, .46f + y * .44f) }, true);
                break;
            case Shape.Heart:
                var heart = new Vector2[40];
                for (int i = 0; i < heart.Length; i++)
                {
                    float t = i * Mathf.PI * 2 / heart.Length;
                    float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                    float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                    heart[i] = P(.5f + x / 39, .55f + y / 39);
                }
                Polygon(heart, Filled); break;
            default:
                int count = Kind == Shape.Spark ? 8 : 10; var star = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    float a = Mathf.PI * .5f + i * Mathf.PI * 2 / count;
                    float radius = i % 2 == 0 ? .46f : .20f;
                    star[i] = P(.5f + Mathf.Cos(a) * radius, .5f + Mathf.Sin(a) * radius);
                }
                Polygon(star, true); break;
        }
    }
}
