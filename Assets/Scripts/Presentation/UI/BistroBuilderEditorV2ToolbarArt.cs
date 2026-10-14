using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Cached, opaque toolbar surfaces. The outer approved dock remains separate.</summary>
public static class BistroBuilderEditorV2ToolbarArt
{
    public enum State { Available, Hovered, Pressed, Selected, SelectedHovered, SelectedPressed, Disabled }
    public readonly struct Palette
    {
        public readonly Color Top, Bottom, Ink, Border;
        public Palette(Color top, Color bottom, Color ink, Color border)
        { Top=top; Bottom=bottom; Ink=ink; Border=border; }
    }
    static readonly Dictionary<int,Sprite> sprites=new Dictionary<int,Sprite>();
    static TMP_FontAsset deleteFont;
    static Color C(byte r,byte g,byte b)=>new Color32(r,g,b,255);
    public static TMP_FontAsset DeleteFont
    {
        get {
            if(deleteFont!=null)return deleteFont;
            var source=Resources.Load<Font>("BistroBuilder/UI/GalleryViva/ToolbarTrashR7");
            if(source==null)return null;
            deleteFont=TMP_FontAsset.CreateFontAsset(source,128,12,GlyphRenderMode.SDFAA,512,512);
            deleteFont.name="Editor V2 toolbar bin R7";deleteFont.boldStyle=0;
            deleteFont.TryAddCharacters("\ue008");return deleteFont;
        }
    }
    public static Palette Colors(bool destructive,State state)
    {
        if(state==State.Disabled)
            return destructive ? new Palette(C(243,230,222),C(235,217,206),C(120,84,74),C(187,148,126))
                : new Palette(C(241,234,219),C(229,217,197),C(104,92,77),C(188,166,132));
        if(destructive) {
            if(state==State.Pressed)return new Palette(C(137,41,39),C(116,32,31),C(255,249,238),C(104,43,31));
            if(state==State.Hovered)return new Palette(C(186,63,51),C(156,43,40),C(255,249,238),C(158,101,49));
            return new Palette(C(169,54,46),C(144,39,37),C(255,249,238),C(136,87,46));
        }
        bool selected=state==State.Selected || state==State.SelectedHovered || state==State.SelectedPressed;
        if(selected) {
            if(state==State.SelectedPressed)return new Palette(C(230,169,69),C(241,187,89),C(70,42,21),C(140,95,39));
            if(state==State.SelectedHovered)return new Palette(C(255,229,168),C(249,191,79),C(70,42,21),C(164,108,40));
            return new Palette(C(255,226,161),C(242,183,74),C(70,42,21),C(171,121,52));
        }
        if(state==State.Pressed)return new Palette(C(228,212,183),C(239,225,204),C(74,46,25),C(150,109,62));
        if(state==State.Hovered)return new Palette(C(255,248,229),C(245,228,195),C(74,46,25),C(178,129,65));
        return new Palette(C(255,250,237),C(244,234,215),C(74,46,25),C(193,161,115));
    }
    public static void Decorate(Image image,bool destructive,State state)
    {
        int key=(int)state+(destructive?16:0);
        if(!sprites.TryGetValue(key,out var sprite)) {
            sprite=Create(Colors(destructive,state),"BB_V2_Toolbar_"+key);sprites.Add(key,sprite);
        }
        image.sprite=sprite;image.type=Image.Type.Sliced;image.fillCenter=true;
        image.pixelsPerUnitMultiplier=2;image.color=Color.white;
    }
    static Sprite Create(Palette palette,string name)
    {
        const int size=128;const float radius=10;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
            float px=x+.5f,py=y+.5f;
            float cx=Mathf.Max(radius-px,px-(size-radius),0);
            float cy=Mathf.Max(radius-py,py-(size-radius),0);
            float distance=Mathf.Sqrt(cx*cx+cy*cy);
            float edge=Mathf.Min(Mathf.Min(px,py),Mathf.Min(size-px,size-py));
            if(cx>0 && cy>0)edge=radius-distance;
            Color fill=Color.Lerp(palette.Bottom,palette.Top,(float)y/(size-1));
            if(edge<2)fill=palette.Border;
            else if(edge<3.2f)fill=Color.Lerp(fill,Color.white,py>size*.5f?.48f:.08f);
            // A single fine lower lip, without the previous heavy bevel/outline.
            if(py<4.2f && edge>=3.2f)fill=Color.Lerp(fill,palette.Border,.18f);
            fill.a=Mathf.Clamp01(edge+.5f);pixels[x+y*size]=fill;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.name=name;texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,
            SpriteMeshType.FullRect,Vector4.one*12);
        sprite.name=name;return sprite;
    }
}
