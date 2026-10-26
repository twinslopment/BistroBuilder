using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Final ornamental details for the real, data-bound dish editor.
/// All art is drawn in procedural Unity code, not from a flattened UI render.
/// This remains independent of dish data and preserves existing actions.
/// </summary>
internal static class BistroBuilderCartaV3DetailPolish
{
    private static Sprite placeholderIcon;
    private static Sprite badge;
    private static Sprite dot;
    private static Sprite chevron;
    public static Sprite ActiveStatusPill => GreenPill();

    public static void Apply(RectTransform panel)
    {
        if (panel == null) return;
        Transform content = panel.Find("Body/Detail/DetailCanvas");
        if (content == null) return;

        Transform statusPlate = content.Find("StatusBadgePlate");
        Image background = statusPlate?.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = GreenPill();
            background.color = Color.white;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
            Text status = statusPlate.Find("StatusBadge")?.GetComponent<Text>();
            if (status != null)
            {
                RectTransform t = status.rectTransform;
                t.offsetMin = new Vector2(32f, 0f);
                t.offsetMax = new Vector2(-26f, 0f);
                status.color = new Color32(31, 77, 28, 255);
            }

            if (statusPlate.Find("V3GreenStatusDot") == null)
            {
                RectTransform node = Rect("V3GreenStatusDot", statusPlate,
                    new Vector2(0f,.5f), new Vector2(0f,.5f),
                    new Vector2(14f,-8f),new Vector2(30f,8f));
                Image graphic = BistroBuilderMenuEditorUiFactory.AddImage(node,
                    Color.white);
                graphic.sprite = GreenDot();
                graphic.preserveAspect = true;
                graphic.raycastTarget = false;
            }
            if (statusPlate.Find("V3GreenStatusArrow") == null)
            {
                RectTransform node = Rect("V3GreenStatusArrow", statusPlate,
                    new Vector2(1f,.5f), new Vector2(1f,.5f),
                    new Vector2(-24f,-6f),new Vector2(-10f,6f));
                Image graphic = BistroBuilderMenuEditorUiFactory.AddImage(node,
                    new Color32(28, 96, 34, 255));
                graphic.sprite = DownChevron();
                graphic.preserveAspect = true;
                graphic.raycastTarget = false;
            }
        }

        Transform hint = content.Find("DishPhotoFrame/MissingDishPhotoHint");
        if (hint != null && hint.Find("V3PhotoPlaceholderIcon") == null)
        {
            RectTransform icon = Rect("V3PhotoPlaceholderIcon", hint,
                new Vector2(.5f,.75f),new Vector2(.5f,.75f),
                new Vector2(-27f,-26f),new Vector2(27f,26f));
            Image graphic = BistroBuilderMenuEditorUiFactory.AddImage(icon,
                new Color32(165, 138, 102, 255));
            graphic.sprite = Placeholder();
            graphic.preserveAspect = true;
            graphic.raycastTarget = false;
        }
    }

    private static RectTransform Rect(string name, Transform parent,
        Vector2 a, Vector2 b, Vector2 min, Vector2 max) =>
        BistroBuilderMenuEditorUiFactory.CreateRect(name, parent, a, b,
            min, max);

    private static Sprite GreenPill()
    {
        if (badge != null) return badge;
        const int size = 96;
        const float radius = 15f;
        Texture2D tex = NewTexture(size, "Carta V3 status badge");
        for (int y=0; y<size; ++y)
        for (int x=0; x<size; ++x)
        {
            float xx=x+.5f, yy=y+.5f;
            float dx=Mathf.Clamp(xx,radius,size-radius);
            float dy=Mathf.Clamp(yy,radius,size-radius);
            float inside = radius - Vector2.Distance(
                new Vector2(xx,yy),new Vector2(dx,dy));
            float alpha=Mathf.Clamp01(inside+.5f);
            float edge=Mathf.Min(Mathf.Min(xx, size-xx),
                Mathf.Min(yy,size-yy));
            Color fill=Color.Lerp(new Color32(217,239,206,255),
                new Color32(233,247,222,255), yy / size);
            if(edge<2.5f || inside < 2.5f)
                fill=Color.Lerp(fill,new Color32(111,170,107,255),.35f);
            fill.a=alpha;
            tex.SetPixel(x,y,fill);
        }
        tex.Apply(false,true);
        badge=Sprite.Create(tex,new Rect(0,0,size,size),
            new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,
            new Vector4(20,20,20,20));
        return badge;
    }

    private static Sprite GreenDot()
    {
        if(dot!=null)return dot;
        Texture2D tex=NewTexture(64,"Carta V3 luminous green status dot");
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dist=Vector2.Distance(new Vector2(x+.5f,y+.5f),
                new Vector2(32f,32f));
            float alpha=Mathf.Clamp01((30.5f-dist)*1.2f);
            Color c=Color.Lerp(new Color32(9,107,32,255),
                new Color32(41,180,54,255),
                Mathf.Clamp01((30f-dist)/26f));
            if(dist<12f && x<28 && y>34)
                c=Color.Lerp(c,Color.white,.3f);
            c.a=alpha;
            tex.SetPixel(x,y,c);
        }
        tex.Apply(false,true);
        dot=Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100f);
        return dot;
    }

    private static Sprite DownChevron()
    {
        if(chevron!=null)return chevron;
        Texture2D tex=NewTexture(64,"Carta V3 green drop arrow");
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            // A downward triangle: upper edge y=42, apex y=19.
            float halfWidth=20f * Mathf.Clamp01((y-19f)/23f);
            float aa=Mathf.Clamp01(halfWidth-Mathf.Abs(x-32f)+.5f);
            aa*=Mathf.Clamp01(y-18.5f)*Mathf.Clamp01(43.5f-y);
            tex.SetPixel(x,y,new Color(1f,1f,1f,aa));
        }
        tex.Apply(false,true);
        chevron=Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100f);
        return chevron;
    }

    private static Sprite Placeholder()
    {
        if(placeholderIcon!=null)return placeholderIcon;
        const int size=96;
        Texture2D tex=NewTexture(size,"Carta V3 missing photo symbol");
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float xx=x+.5f,yy=y+.5f;
            bool border=(x>=11 && x<=84 && y>=12 && y<=83) &&
                (x<17 || x>78 || y<18 || y>77);
            float sun=Vector2.Distance(new Vector2(xx,yy),
                new Vector2(67f,64f));
            bool sunShape=sun<9f;
            bool mountain=(y>=25 && y<=52) &&
                (Mathf.Abs(y-(x*.75f-7f))<4f ||
                Mathf.Abs(y-(-x*.8f+90f))<4f);
            bool lowerMountain=(y>=20 && y<=48 &&
                y<Mathf.Min(x*.75f-7f,-x*.8f+90f));
            float alpha=(border || sunShape || mountain || lowerMountain)
                ? 1f : 0f;
            tex.SetPixel(x,y,new Color(1f,1f,1f,alpha));
        }
        tex.Apply(false,true);
        placeholderIcon=Sprite.Create(tex,new Rect(0,0,size,size),
            new Vector2(.5f,.5f),100f);
        return placeholderIcon;
    }

    private static Texture2D NewTexture(int size,string name)
    {
        return new Texture2D(size,size,TextureFormat.RGBA32,false)
        {
            name=name,filterMode=FilterMode.Bilinear,
            wrapMode=TextureWrapMode.Clamp
        };
    }
}
