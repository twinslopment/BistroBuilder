using UnityEngine;
using UnityEngine.UI;

/// <summary>Gallery-owned brass scrollbar; the existing ScrollRect owns navigation.</summary>
public static class BistroBuilderGalleryVivaScrollChrome
{
    private static Sprite trackSprite, handleSprite;
    public static void Install(ScrollRect scroll)
    {
        var rect = BistroBuilderGalleryViva.Node("BrassScrollbar", scroll.transform, 354, 4, 12, 199);
        var track = rect.gameObject.AddComponent<Image>();
        track.sprite = SpriteFor(false); track.type = Image.Type.Sliced;
        var area = BistroBuilderGalleryViva.Node("Sliding Area", rect, 2, 3, 8, 193);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(2,3); area.offsetMax = new Vector2(-2,-3);
        var handle = BistroBuilderGalleryViva.Node("Handle", area, 0, 0, 8, 50);
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
        handle.pivot = new Vector2(.5f,.5f); handle.offsetMin = handle.offsetMax = Vector2.zero;
        var image = handle.gameObject.AddComponent<Image>();
        image.sprite = SpriteFor(true); image.type = Image.Type.Sliced;
        var bar = rect.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = image;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = bar.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color32(255,238,187,255);
        colors.pressedColor = new Color32(207,164,92,255); colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = .1f; bar.colors = colors;
        for (int i=0; i<3; i++)
        {
            var grip = BistroBuilderGalleryViva.Node("Grip"+i, handle, 0, 0, 4, 1);
            grip.anchorMin = grip.anchorMax = grip.pivot = new Vector2(.5f,.5f);
            grip.anchoredPosition = new Vector2(0, (i-1)*3);
            var line = grip.gameObject.AddComponent<Image>();
            line.color = new Color32(118,74,33,170); line.raycastTarget = false;
        }
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing = 0;
    }
    public static void Layout(ScrollRect scroll)
    {
        float height = ((RectTransform)scroll.transform).rect.height;
        BistroBuilderGalleryViva.Place(scroll.viewport, 0, 0, 352, height);
        if (scroll.verticalScrollbar != null)
            BistroBuilderGalleryViva.Place((RectTransform)scroll.verticalScrollbar.transform, 354, 4, 12, height-8);
    }
    private static Sprite SpriteFor(bool thumb)
    {
        if (thumb && handleSprite != null) return handleSprite;
        if (!thumb && trackSprite != null) return trackSprite;
        const int w=48,h=96;
        var texture = new Texture2D(w,h,TextureFormat.RGBA32,false) { name = thumb ? "Gallery_Brass_Thumb" : "Gallery_Recessed_Track", filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp };
        var pixels = new Color[w*h];
        for(int y=0; y<h; y++) for(int x=0; x<w; x++)
        {
            // Rounded ends, an inset channel and a horizontal metal bevel.
            float dx=Mathf.Abs(x+.5f-w*.5f)-(w*.5f-10);
            float dy=Mathf.Abs(y+.5f-h*.5f)-(h*.5f-10);
            float distance=new Vector2(Mathf.Max(dx,0),Mathf.Max(dy,0)).magnitude+Mathf.Min(Mathf.Max(dx,dy),0)-10;
            float alpha=Mathf.Clamp01(.5f-distance);
            float edge=Mathf.Clamp01((-distance-1)/3);
            Color metal;
            if(thumb)
            {
                float t=x/(float)(w-1);
                metal=Color.Lerp(new Color32(255,224,146,255),new Color32(167,111,49,255),t);
                if(t<.22f) metal=Color.Lerp(new Color32(145,89,35,255),new Color32(255,242,186,255),t/.22f);
            }
            else metal=new Color32(218,198,166,255);
            metal=Color.Lerp(new Color32(156,113,65,255),metal,edge);
            metal.a=alpha; pixels[y*w+x]=metal;
        }
        texture.SetPixels(pixels); texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(12,12,12,12));
        sprite.name = texture.name;
        if(thumb)handleSprite=sprite;else trackSprite=sprite;
        return sprite;
    }
}
