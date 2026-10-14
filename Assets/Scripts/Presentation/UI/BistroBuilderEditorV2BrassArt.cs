using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unified Editor V2 ornament atlas: nine-sliced ivory/brass frame and relief
/// buttons shared by the bars, Galeria Viva and inspector. Runtime art is
/// cached; no GameObject, material or texture is allocated each frame.
/// </summary>
public static class BistroBuilderEditorV2BrassArt
{
    public enum Style { Frame, Button, Selected, Destructive, Dark }
    private static readonly Dictionary<Style, Sprite> Sprites =
        new Dictionary<Style, Sprite>();

    public static Sprite Get(Style style)
    {
        if (!Sprites.TryGetValue(style, out Sprite sprite) || sprite == null)
        {
            sprite = Create(style);
            Sprites[style] = sprite;
        }
        return sprite;
    }

    private static Texture2D reference;
    public static Texture2D Reference => reference != null ? reference :
        (reference = Resources.Load<Texture2D>("BistroBuilder/UI/GalleryViva/ApprovedFullReference"));
    private static Sprite frame, brand;
    private static readonly Dictionary<string, Sprite> ReferenceFrames = new Dictionary<string, Sprite>();

    public static void DecorateReferenceFrame(Image image, string role)
    {
        Decorate(image, Style.Frame);
        if (Reference == null) return;
        if (!ReferenceFrames.TryGetValue(role, out var sprite))
        {
            sprite = role == "Header" ? Crop("ApprovedHeaderEdges", 8, 8, 1656, 74, Vector4.one * 14) :
                role == "Venue" ? Crop("ApprovedVenueEdges", 681, 9, 314, 71, Vector4.one * 14) :
                frame;
            ReferenceFrames[role] = sprite;
        }
        image.sprite = sprite;
        image.type = Image.Type.Sliced; image.fillCenter = false;
        if(role=="Catalog")ReferenceEdge(image,"CatalogTop",9,92,557,10,0,1,1,1,new Vector2(0,-10),Vector2.zero);
        if(role=="Header") {
            // Cover every inset and corner under the ornamental frame.
            var paper=image.transform.Find("V2Paper") as RectTransform;
            paper.offsetMin=paper.offsetMax=Vector2.zero;
            // The full-size paper is a child, so the ornamental edge must be
            // another child above it rather than the parent's earlier draw.
            if(image.transform.Find("HeaderOrnaments")==null) {
                var node=new GameObject("HeaderOrnaments",typeof(RectTransform),typeof(Image));
                node.transform.SetParent(image.transform,false);
                var rect=(RectTransform)node.transform;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                var edge=node.GetComponent<Image>();edge.sprite=sprite;
                edge.type=Image.Type.Sliced;edge.fillCenter=false;edge.raycastTarget=false;
            }
            image.sprite=null;image.color=Color.clear;
        }
        if(role=="Footer") {
            image.sprite=null;image.color=Color.clear;
            // Fill the complete dock, including the caption gaps and corners.
            // The source patch is empty opaque paper, never restaurant imagery.
            var paper=image.transform.Find("V2Paper") as RectTransform;
            paper.offsetMin=paper.offsetMax=Vector2.zero;
            var ornaments=BistroBuilderGalleryViva.Node("FooterOrnaments",image.transform,0,0,1656,103);
            var ornamentHost=ornaments.gameObject.AddComponent<Image>();
            ornamentHost.color=Color.clear;ornamentHost.raycastTarget=false;
            int[,] strips={ {8,830,1656,5},{8,921,1656,12},{8,830,25,103},{1636,830,28,103},
                {30,835,247,12},{359,835,259,12},{630,835,218,12},{939,835,248,12},
                {1201,835,173,12},{1505,835,131,12},{600,835,39,98},{1170,835,40,98} };
            for(int i=0;i<strips.GetLength(0);i++)
                FixedReferenceStrip(ornamentHost,"FooterOrnament_"+i,strips[i,0],strips[i,1],strips[i,2],strips[i,3],strips[i,0]-8,strips[i,1]-830);
            FitFooter(image.rectTransform);
        }
    }
    public static void FitFooter(RectTransform footer)
    {
        var ornaments=footer.Find("FooterOrnaments") as RectTransform;
        if(ornaments!=null)
            ornaments.localScale=new Vector3(footer.rect.width/1656f,footer.rect.height/103f,1);
    }
    public static void DecoratePreviewFrame(Image image)
    {
        if(Reference==null)return;
        image.sprite=null;image.color=Color.clear;
        FixedReferenceStrip(image,"PreviewTop",1360,142,286,8,0,0);
        FixedReferenceStrip(image,"PreviewBottom",1360,306,286,8,0,164);
        FixedReferenceStrip(image,"PreviewLeft",1360,150,8,156,0,8);
        FixedReferenceStrip(image,"PreviewRight",1638,150,8,156,278,8);
    }
    static void FixedReferenceStrip(Image parent,string id,int x,int y,int w,int h,float dx,float dy)
    {
        if(parent.transform.Find(id)!=null)return;
        if(!ReferenceFrames.TryGetValue(id,out var sprite)) {
            sprite=Crop(id,x,y,w,h,Vector4.zero);ReferenceFrames[id]=sprite;
        }
        var node=BistroBuilderGalleryViva.Node(id,parent.transform,dx,dy,w,h);
        var art=node.gameObject.AddComponent<Image>();art.sprite=sprite;art.raycastTarget=false;
    }

    private static void ReferenceEdge(Image parent,string id,float x,float y,float w,float h,
        float minX,float minY,float maxX,float maxY,Vector2 offsetMin,Vector2 offsetMax)
    {
        if(parent.transform.Find(id)!=null)return;
        if(!ReferenceFrames.TryGetValue(id,out var sprite)) {
            sprite=Crop(id,x,y,w,h,Vector4.zero);ReferenceFrames[id]=sprite;
        }
        var node=new GameObject(id,typeof(RectTransform),typeof(Image));node.transform.SetParent(parent.transform,false);
        var r=(RectTransform)node.transform;r.anchorMin=new Vector2(minX,minY);r.anchorMax=new Vector2(maxX,maxY);
        r.offsetMin=offsetMin;r.offsetMax=offsetMax;
        var image=node.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
    }
    public static Sprite Brand => brand != null ? brand :
        (brand = Crop("ApprovedBrand", 19, 3, 202, 75, Vector4.zero));

    private static Sprite Crop(string name, float x, float y, float w, float h, Vector4 border)
    {
        if (Reference == null) return null;
        var sprite = Sprite.Create(Reference, new Rect(x, Reference.height-y-h, w, h),
            Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, border);
        sprite.name = name;
        return sprite;
    }

    public static void Decorate(Image image, Style style)
    {
        if (image == null) return;
        if (style == Style.Frame && Reference != null)
        {
            if (frame == null) frame = Crop("ApprovedFrameEdges", 1344, 92, 318, 730, Vector4.one * 12);
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = Color.white;
            // Only the empty paper and ornamental border are sampled. Every
            // label, field, product and action is a real native UI control.
            if (image.transform.Find("V2Paper") == null)
            {
                var paper = new GameObject("V2Paper", typeof(RectTransform), typeof(RawImage));
                paper.transform.SetParent(image.transform, false);
                paper.transform.SetAsFirstSibling();
                var r = (RectTransform)paper.transform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.one * 10; r.offsetMax = Vector2.one * -10;
                var raw = paper.GetComponent<RawImage>(); raw.texture = Reference;
                raw.uvRect = new Rect(22f/1672, 131f/941, 100f/1672, 110f/941);
                raw.raycastTarget = false;
            }
            return;
        }
        var sprite = Get(style);
        if (image.sprite != sprite) image.sprite = sprite;
        image.type = Image.Type.Sliced; image.fillCenter = true;
        image.color = Color.white;
    }

    private static Sprite Create(Style style)
    {
        const int size = 128;
        const int radius = 9;
        var pixels = new Color32[size * size];
        bool dark = style == Style.Dark;
        bool red = style == Style.Destructive;
        bool highlighted = style == Style.Selected;
        bool frame = style == Style.Frame;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f;
                float py = y + .5f;
                float cornerX = Mathf.Max(radius - px, px - (size - radius), 0f);
                float cornerY = Mathf.Max(radius - py, py - (size - radius), 0f);
                float cornerDistance = Mathf.Sqrt(cornerX * cornerX +
                    cornerY * cornerY);
                if (cornerDistance > radius)
                {
                    pixels[x + y * size] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float edge = Mathf.Min(px, py, size - px, size - py);
                bool brassRim = edge <= (frame ? 2.0f : 1.4f) ||
                    cornerDistance > radius - (frame ? 2.0f : 1.4f);
                bool innerHighlight = edge < (frame ? 4.0f : 3.0f);
                bool insetShadow = edge > (frame ? 4.0f : 3.0f) &&
                    edge < (frame ? 5.0f : 4.0f);
                float v = 1f - Mathf.Clamp01((float)y / size);
                Color fill = dark
                    ? Color.Lerp(new Color32(71, 45, 26, 255),
                        new Color32(36, 22, 14, 255), v)
                    : red
                    ? Color.Lerp(new Color32(244, 26, 29, 255),
                        new Color32(179, 0, 7, 255), v)
                    : highlighted
                    ? Color.Lerp(new Color32(255, 226, 159, 255),
                        new Color32(248, 178, 43, 255), v)
                    : Color.Lerp(new Color32(255, 250, 238, 255),
                        new Color32(239, 223, 198, 255), v);

                if (brassRim)
                {
                    fill = Color.Lerp(new Color32(236, 192, 116, 255),
                        new Color32(119, 70, 31, 255), v);
                }
                else if (innerHighlight)
                {
                    fill = Color.Lerp(fill, new Color32(255, 252, 232, 255),
                        y > size / 2 ? .15f : .65f);
                }
                else if (insetShadow)
                {
                    fill = Color.Lerp(fill, new Color32(108, 64, 33, 255),
                        frame ? .22f : .13f);
                }

                // Small engraved corner screws belong ONLY to the framing,
                // not to individual buttons or category cards.
                if (frame)
                {
                    float nearX = x < size / 2 ? 16f : size - 17f;
                    float nearY = y < size / 2 ? 16f : size - 17f;
                    float dx = x - nearX, dy = y - nearY;
                    float screwDistance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (screwDistance < 6.2f)
                    {
                        fill = screwDistance > 4.8f
                            ? new Color32(111, 65, 28, 255)
                            : screwDistance > 2.8f
                            ? new Color32(247, 205, 120, 255)
                            : new Color32(140, 95, 49, 255);
                        if (Mathf.Abs(dx + dy * .75f) < 1.0f &&
                            screwDistance < 3.5f)
                            fill = new Color32(63, 43, 27, 255);
                    }
                }

                fill.a = Mathf.Clamp01(radius + .5f - cornerDistance);
                pixels[x + y * size] = (Color32)fill;
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BB_EditorV2_Brass_" + style;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
        sprite.name = texture.name;
        return sprite;
    }
}
