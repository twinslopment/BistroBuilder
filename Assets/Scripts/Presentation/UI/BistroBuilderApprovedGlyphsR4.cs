using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Approved V2 silhouettes as our own vector font, rendered by native TMP SDF.
/// Text, hit targets and catalog authority remain separate native controls.</summary>
public static class BistroBuilderApprovedGlyphsR4
{
    static TMP_FontAsset font;
    public static TMP_FontAsset Font
    {
        get
        {
            if (font != null) return font;
            var source=Resources.Load<Font>("BistroBuilder/UI/GalleryViva/ApprovedGlyphsR4");
            if (source == null) return null;
            font=TMP_FontAsset.CreateFontAsset(source,128,12,GlyphRenderMode.SDFAA,2048,2048);
            font.name="Approved V2 vector silhouettes R4";
            font.boldStyle=0; font.boldSpacing=0;
            string all=""; for(int i=0;i<28;i++) all+=(char)(0xe000+i);
            font.TryAddCharacters(all);
            return font;
        }
    }
    public static bool Resolve(string key,out string character,out float aspect)
    {
        switch(key)
        {
            case "tools": character="\ue000"; aspect=1.0476190f; return true;
            case "cursor": character="\ue001"; aspect=0.8974359f; return true;
            case "chair": character="\ue002"; aspect=0.9230769f; return true;
            case "hammer": character="\ue003"; aspect=1.0256410f; return true;
            case "layers": character="\ue004"; aspect=1.0500000f; return true;
            case "move": character="\ue005"; aspect=0.9750000f; return true;
            case "rotate": character="\ue006"; aspect=1.0000000f; return true;
            case "duplicate": character="\ue007"; aspect=1.0500000f; return true;
            case "trash": character="\ue008"; aspect=0.9487179f; return true;
            case "magnet": character="\ue009"; aspect=1.0243902f; return true;
            case "grid": character="\ue00a"; aspect=1.0750000f; return true;
            case "cube": character="\ue00b"; aspect=1.0500000f; return true;
            case "undo": character="\ue00c"; aspect=0.9393939f; return true;
            case "redo": character="\ue00d"; aspect=0.9696970f; return true;
            case "sun": character="\ue00e"; aspect=1.0263158f; return true;
            case "camera": character="\ue00f"; aspect=1.0000000f; return true;
            case "gear": character="\ue010"; aspect=0.9714286f; return true;
            case "pencil": character="\ue011"; aspect=1.0344828f; return true;
            case "fleur": character="\ue012"; aspect=0.8333333f; return true;
            case "category-all": character="\ue013"; aspect=0.9032258f; return true;
            case "category-tables": character="\ue014"; aspect=0.9062500f; return true;
            case "category-seating": character="\ue015"; aspect=0.8750000f; return true;
            case "category-bar": character="\ue016"; aspect=1.0714286f; return true;
            case "category-kitchen": character="\ue017"; aspect=0.9677419f; return true;
            case "category-storage": character="\ue018"; aspect=0.9677419f; return true;
            case "category-lighting": character="\ue019"; aspect=1.0000000f; return true;
            case "category-decoration": character="\ue01a"; aspect=0.9354839f; return true;
            case "category-exterior": character="\ue01b"; aspect=1.0000000f; return true;
            default:character="";aspect=1;return false;
        }
    }
    public static TMP_Text Create(Transform parent,string name,string key,float x,float y,float w,float h,Color ink)
    {
        if(Font==null||!Resolve(key,out var character,out var aspect)) return null;
        var rect=BistroBuilderGalleryViva.Node(name,parent,x,y,w,h);
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=Font; text.text=character; text.fontSize=Mathf.Min(h,w/aspect);
        text.alignment=TextAlignmentOptions.Center; text.color=ink;
        text.textWrappingMode=TextWrappingModes.NoWrap;
        text.overflowMode=TextOverflowModes.Overflow;
        text.raycastTarget=false;
        return text;
    }
}
