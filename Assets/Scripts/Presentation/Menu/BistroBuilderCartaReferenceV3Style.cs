using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BistroBuilder.UI.Iconography;

/// <summary>
/// Carta Gestor reference V3: faithful runtime uGUI skin for the 1280x720
/// approved Personal-V8-styled screenshot. Presentation only; no domain logic.
/// Icons are exported unmodified from the reference HTML SVG symbol definitions.
/// </summary>
internal static class BistroBuilderCartaReferenceV3Style
{
    private const string IconRoot = "BistroBuilder/UI/CartaReferenceV3Icons/";
    private const string RasterRoot = "BistroBuilder/UI/CartaReferenceV3IconsRaster/";
    private static readonly Color32 Ink = new Color32(57, 34, 18, 255);
    private static readonly Color32 Muted = new Color32(103, 76, 51, 255);
    private static readonly Color32 Brass = new Color32(156, 106, 39, 255);
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static Font recoleta, inter, interBold;
    private static Sprite outer, card, normalButton, selectedRow, plainRow, input, danger, emblem, dotGreen, dotGrey, dotBrass;

    // Isolated approved Platos skin. The established Cartas y reglas screen
    // intentionally retains its existing textures and dimensions.
    private static Sprite editorOuter, editorCard, editorButton,
        editorSelected, editorRow, editorField, editorDanger, categoryGrid,
        editorSwitchOn, editorSwitchOff, editorSwitchKnob, editorGold, editorSearchIcon;
    private static BBIconCatalog categoryIcons;

    private static void EnsureEditorSurfaces()
    {
        EnsureSurfaces();
        if (editorOuter != null) return;
        editorOuter = Surface(new Color32(248, 239, 225, 255),
            new Color32(243, 231, 214, 255),
            new Color32(154, 114, 65, 255), 13, 1);
        editorCard = Surface(new Color32(249, 242, 232, 255),
            new Color32(246, 236, 222, 255),
            new Color32(196, 162, 120, 255), 10, 1);
        editorButton = Surface(new Color32(249, 236, 212, 255),
            new Color32(222, 186, 127, 255),
            new Color32(152, 106, 53, 255), 8, 1);
        editorSelected = Surface(new Color32(252, 225, 177, 255),
            new Color32(235, 184, 100, 255),
            new Color32(194, 139, 63, 255), 8, 1);
        editorRow = Surface(new Color32(249, 242, 230, 255),
            new Color32(246, 237, 224, 255),
            new Color32(223, 202, 173, 255), 4, 1);
        editorField = Surface(new Color32(250, 244, 235, 255),
            new Color32(246, 236, 220, 255),
            new Color32(207, 179, 144, 255), 6, 1);
        editorDanger = Surface(new Color32(197, 73, 55, 255),
            new Color32(150, 38, 28, 255),
            new Color32(116, 36, 26, 255), 8, 1);
        editorGold = Surface(new Color32(213, 161, 73, 255),
            new Color32(148, 96, 24, 255),
            new Color32(116, 72, 24, 255), 8, 1);
        editorSwitchOn = Surface(new Color32(220, 161, 45, 255),
            new Color32(204, 139, 27, 255),
            new Color32(173, 115, 22, 255), 12, 1);
        editorSwitchOff = Surface(new Color32(222, 211, 195, 255),
            new Color32(219, 207, 190, 255),
            new Color32(199, 181, 159, 255), 12, 1);
        editorSwitchKnob = Dot(new Color32(255, 255, 255, 255),
            new Color32(245, 239, 231, 255));
    }

    private static void AddEditorFineRim(Transform panel)
    {
        if (panel == null || panel.Find("ReferenceFineRimTop") != null) return;
        // Two restrained inset strokes mimic the approved ivory/brass
        // raised cabinet frame. Pure decoration: never blocks hit targets.
        AddEditorFineLine(panel, "ReferenceFineRimTop",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(5f, -5f), new Vector2(-5f, -4f),
            new Color32(164, 117, 64, 144));
        AddEditorFineLine(panel, "ReferenceFineRimBottom",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(5f, 4f), new Vector2(-5f, 5f),
            new Color32(220, 190, 153, 190));
        AddEditorFineLine(panel, "ReferenceFineRimLeft",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(4f, 5f), new Vector2(5f, -5f),
            new Color32(183, 140, 89, 148));
        AddEditorFineLine(panel, "ReferenceFineRimRight",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-5f, 5f), new Vector2(-4f, -5f),
            new Color32(214, 177, 136, 190));
        AddEditorFineLine(panel, "ReferenceInnerHighlightTop",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(7f, -7f), new Vector2(-7f, -6f),
            new Color32(255, 251, 245, 190));
        AddEditorFineLine(panel, "ReferenceInnerHighlightLeft",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(7f, 7f), new Vector2(8f, -7f),
            new Color32(255, 249, 236, 157));
    }

    private static void AddEditorFineLine(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
        Vector2 offsetMax, Color color)
    {
        RectTransform r = Rect(name, parent, anchorMin, anchorMax,
            offsetMin, offsetMax);
        Image image = BistroBuilderMenuEditorUiFactory.AddImage(r, color);
        image.raycastTarget = false;
    }

    private static void RemoveOldBevel(Transform node)
    {
        if (node == null) return;
        // The legacy Outline + Shadow stacked on top of the 9-slice rim,
        // producing the dark double borders visible in the rejected build.
        Outline outline = node.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        Shadow shadow = node.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
    }

    private static Font Recoleta => recoleta != null ? recoleta :
        (recoleta = Resources.Load<Font>("BistroBuilder/UI/Typography/Recoleta"));
    private static Font Inter => inter != null ? inter :
        (inter = Resources.Load<Font>("BistroBuilder/UI/Typography/Inter-Regular"));
    private static Font InterBold => interBold != null ? interBold :
        (interBold = Resources.Load<Font>("BistroBuilder/UI/Typography/Inter-SemiBold"));

    public static bool IsDestructiveStyle(Button button)
    {
        return button != null && button.GetComponent<Image>() != null &&
            button.GetComponent<Image>().sprite == danger;
    }

    public static Sprite ReferenceIcon(string name)
    {
        if (Sprites.TryGetValue(name, out Sprite sprite)) return sprite;

        // Unity Vector Graphics can import the reference SVG as a named sprite
        // without producing visible pixels on the current uGUI Image renderer.
        // Prefer a pixel-perfect transparent raster of that SAME SVG drawing.
        Texture2D texture = Resources.Load<Texture2D>(RasterRoot + name);
        if (texture != null)
        {
            sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "CartaV3_" + name;
        }
        if (sprite == null)
            sprite = Resources.Load<Sprite>(RasterRoot + name);
        if (sprite == null)
            sprite = Resources.Load<Sprite>(IconRoot + name); // safe source fallback
        if (sprite != null) Sprites.Add(name, sprite);
        return sprite;
    }

    private static Color Lerp(Color32 a, Color32 b, float t) =>
        Color.Lerp((Color)a, (Color)b, Mathf.Clamp01(t));

    private static Sprite Surface(Color32 top, Color32 bottom, Color32 stroke, int radius, int rim)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Carta V3 9-slice surface"
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = x < radius ? radius : x >= size - radius ? size - radius - 1 : x;
            int cy = y < radius ? radius : y >= size - radius ? size - radius - 1 : y;
            bool inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
            bool edge = x < rim || y < rim || x >= size-rim || y >= size-rim;
            Color fill = Lerp(bottom, top, (float)y / (size-1));
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? (Color)stroke : fill);
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f,.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(13,13,13,13));
    }

    private static void EnsureSurfaces()
    {
        if (outer != null) return;
        outer = Surface(new Color32(246,237,224,255),new Color32(238,224,205,255),
            new Color32(126,83,31,255),14,3);
        card = Surface(new Color32(246,237,224,255),new Color32(241,229,212,255),
            new Color32(174,129,79,255),11,2);
        normalButton = Surface(new Color32(244,231,211,255),new Color32(225,199,162,255),
            new Color32(146,98,48,255),7,2);
        selectedRow = Surface(new Color32(248,226,186,255),new Color32(235,192,112,255),
            new Color32(184,126,55,255),8,2);
        plainRow = Surface(new Color32(246,236,222,255),new Color32(245,235,220,255),
            new Color32(214,187,154,255),3,1);
        input = Surface(new Color32(244,232,215,255),new Color32(236,220,199,255),
            new Color32(184,143,97,255),6,1);
        danger = Surface(new Color32(185,61,48,255),new Color32(145,34,25,255),
            new Color32(106,29,20,255),6,2);
        emblem = Surface(new Color32(238,210,165,255),new Color32(208,160,100,255),
            new Color32(155,104,47,255),7,1);
        dotGreen = Dot(new Color32(115,201,66,255),new Color32(57,117,29,255));
        dotGrey = Dot(new Color32(198,199,197,255),new Color32(112,108,103,255));
        dotBrass = Dot(new Color32(255,213,130,255),new Color32(118,72,27,255));
    }

    private static Sprite Dot(Color32 center, Color32 edge)
    {
        Texture2D tex = new Texture2D(16,16,TextureFormat.RGBA32,false);
        tex.filterMode = FilterMode.Bilinear;
        for(int y=0;y<16;y++) for(int x=0;x<16;x++)
        {
            float d = Vector2.Distance(new Vector2(x,y),new Vector2(7.5f,7.5f));
            tex.SetPixel(x,y,d>7.4f?Color.clear:Lerp(center,edge,d/7.4f));
        }
        tex.Apply(false,true);
        return Sprite.Create(tex,new Rect(0,0,16,16),new Vector2(.5f,.5f),100f);
    }

    private static void SkinImage(Image image, Sprite sprite)
    {
        if(image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void Anchor(RectTransform rect, float left,float bottom,float right,float top,
        Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = new Vector2(left,bottom);
        rect.anchorMax = new Vector2(right,top);
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
    }

    private static RectTransform Rect(string name, Transform parent,
        Vector2 anchorMin,Vector2 anchorMax,Vector2 min,Vector2 max)
    {
        return BistroBuilderMenuEditorUiFactory.CreateRect(name,parent,
            anchorMin,anchorMax,min,max);
    }

    private static void Line(Transform parent,string name,float minX,float maxX,float y)
    {
        RectTransform line = Rect(name,parent,new Vector2(minX,y),new Vector2(maxX,y),
            Vector2.zero,new Vector2(0,1));
        Image img = BistroBuilderMenuEditorUiFactory.AddImage(line,new Color32(207,170,130,255));
        img.raycastTarget=false;
    }

    private static void SetTypeface(Text t, bool heading, int size, bool emphasized=false)
    {
        if(t==null) return;
        bool ascii=true;
        foreach(char c in t.text ?? string.Empty)
            if(c<32 || c>126) { ascii=false;break; }
        Font target = heading && ascii ? Recoleta : emphasized || heading ? InterBold : Inter;
        if(target!=null)t.font=target;
        t.fontSize=size;
        t.fontStyle=heading && target==Recoleta ? FontStyle.Bold : FontStyle.Normal;
        t.color=Ink;
        t.horizontalOverflow=HorizontalWrapMode.Overflow;
        t.verticalOverflow=VerticalWrapMode.Truncate;
        t.raycastTarget=false;
    }

    // Reproduce the V3 HTML workaround for the official Recoleta DEMO:
    // render safe ASCII with Recoleta and unsupported accented glyphs in Inter.
    // The original Text remains in the hierarchy for UI tests/accessibility.
    private static void MixedTypefaceRuns(Text original)
    {
        if(original==null || original.text==null)return;
        string value=original.text;
        bool mixed=false;
        foreach(char c in value) if(c>126){mixed=true;break;}
        if(!mixed || original.transform.parent.Find("ReferenceTypefaceRuns")!=null)return;
        RectTransform src=original.rectTransform;
        RectTransform group=Rect("ReferenceTypefaceRuns",original.transform.parent,
            src.anchorMin,src.anchorMax,src.offsetMin,src.offsetMax);
        HorizontalLayoutGroup layout=group.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment=original.alignment;
        layout.spacing=0;
        layout.childControlHeight=true;layout.childControlWidth=true;
        layout.childForceExpandHeight=true;layout.childForceExpandWidth=false;
        string run="";bool wasAscii=value[0]<=126;
        for(int i=0;i<value.Length;i++)
        {
            bool ascii=value[i]<=126;
            if(ascii!=wasAscii && run.Length>0)
            {
                AddTypefaceRun(group,run,wasAscii,original);
                run="";
            }
            run+=value[i];wasAscii=ascii;
        }
        if(run.Length>0)AddTypefaceRun(group,run,wasAscii,original);
        original.enabled=false;
    }

    private static void AddTypefaceRun(Transform parent,string value,bool ascii,Text template)
    {
        Text t=BistroBuilderMenuEditorUiFactory.CreateText(
            "TypefaceRun",parent,value,template.fontSize,TextAnchor.MiddleLeft,template.color);
        t.font=ascii?(Recoleta??InterBold):InterBold;
        t.fontSize=template.fontSize;
        t.fontStyle=ascii && t.font==Recoleta ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow=HorizontalWrapMode.Overflow;
        t.verticalOverflow=VerticalWrapMode.Truncate;
    }

    private static Image AddReferenceIcon(string name,Transform parent,string id,
        float x1,float y1,float x2,float y2,Vector2 lo,Vector2 hi)
    {
        Sprite sprite=ReferenceIcon(id);
        if(sprite==null) return null;
        RectTransform node=Rect(name,parent,new Vector2(x1,y1),new Vector2(x2,y2),lo,hi);
        Image image=BistroBuilderMenuEditorUiFactory.AddImage(node,Color.white);
        image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
        return image;
    }

    public static void Apply(RectTransform panel)
    {
        if(panel==null)return;
        EnsureSurfaces();
        SkinImage(panel.GetComponent<Image>(),outer);
        foreach(string name in new[]{"Menus","Rules","RuleEditor"})
        {
            Transform node=panel.Find(name);
            if(node==null)continue;
            SkinImage(node.GetComponent<Image>(),card);
            Line(node,"ReferenceHeaderRule",.035f,.965f,.910f);
        }
        foreach(var pair in new[]{
            new[]{"Header/CartaHeaderIcon","book"},
            new[]{"Menus/SectionIcon","book"},
            new[]{"Rules/SectionIcon","clock"},
            new[]{"RuleEditor/SectionIcon","file"}
        })
        {
            Transform node=panel.Find(pair[0]);
            Image image=node?.GetComponent<Image>();
            Sprite sprite=ReferenceIcon(pair[1]);
            if(image==null || sprite==null)continue;
            image.sprite=sprite;image.color=Color.white;
            image.type=Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;
            bool main=pair[0].StartsWith("Header/",StringComparison.Ordinal);
            if(main)
                Anchor(image.rectTransform,0,0,0,1,new Vector2(14,13),new Vector2(42,-13));
            else
                Anchor(image.rectTransform,.028f,.92f,.108f,1f,
                    new Vector2(0,10),new Vector2(0,-10));
        }
        Transform header=panel.Find("Header");
        if(header!=null)
        {
            Image headerImage=header.GetComponent<Image>();
            if(headerImage!=null){headerImage.color=Color.clear;headerImage.raycastTarget=false;}
            Line(header,"ReferenceTopRule",0,1,0);
            Text title=header.Find("Title")?.GetComponent<Text>();
            if(title!=null)
            {
                title.text="CARTA";SetTypeface(title,true,34,true);
                Anchor(title.rectTransform,.067f,0,.36f,1,Vector2.zero,Vector2.zero);
            }
            Text tagline=header.Find("Tagline")?.GetComponent<Text>();
            if(tagline!=null)
            {
                SetTypeface(tagline,false,12,true);tagline.alignment=TextAnchor.MiddleCenter;
                Anchor(tagline.rectTransform,.64f,.18f,.985f,.86f,
                    new Vector2(8,0),new Vector2(-8,0));
                RectTransform backing=Rect("ReferenceTaglinePlate",header,
                    new Vector2(.64f,.18f),new Vector2(.985f,.86f),
                    Vector2.zero,Vector2.zero);
                SkinImage(BistroBuilderMenuEditorUiFactory.AddImage(backing,Color.white),input);
                backing.SetSiblingIndex(tagline.transform.GetSiblingIndex());
            }
            Transform close=header.Find("Close");
            if(close!=null)
            {
                // The approved Carta V3 reference has no close tile in the header.
                // Navigation/Escape still closes the management view.
                close.gameObject.SetActive(false);
            }
        }
        foreach(string name in new[]{"Menus","Rules","RuleEditor"})
        {
            Transform root=panel.Find(name);
            if(root==null)continue;
            ScrollRect scroll=root.GetComponentInChildren<ScrollRect>(true);
            if(scroll!=null)
            {
                Image scrollBackground=scroll.GetComponent<Image>();
                if(scrollBackground!=null)
                {scrollBackground.color=Color.clear;scrollBackground.raycastTarget=false;}
                VerticalLayoutGroup list=scroll.content?.GetComponent<VerticalLayoutGroup>();
                if(list!=null){list.spacing=0f;list.padding=new RectOffset(0,0,0,0);}
            }
            Text caption=root.Find("Subheading")?.GetComponent<Text>();
            if(caption!=null)
            {
                SetTypeface(caption,true,21,true);
                Anchor(caption.rectTransform,.13f,.92f,.81f,1f,Vector2.zero,Vector2.zero);
                // Match the HTML V3: only the accented ó comes from Inter.
                MixedTypefaceRuns(caption);
            }
            Text count=root.Find(name=="Menus"?"MenuCount":"RuleCount")?.GetComponent<Text>();
            if(count!=null){SetTypeface(count,false,11);count.alignment=TextAnchor.MiddleRight;}
        }
        foreach(InputField field in panel.GetComponentsInChildren<InputField>(true))
        {
            SkinImage(field.GetComponent<Image>(),input);
            if(field.textComponent!=null) SetTypeface(field.textComponent,false,13);
            if(field.placeholder is Text hint)
            {
                SetTypeface(hint,false,12);hint.color=new Color32(139,108,78,255);
            }
        }
        foreach(Button b in panel.GetComponentsInChildren<Button>(true))
        {
            if(b.name.StartsWith("Menu_",StringComparison.Ordinal) ||
                b.name.StartsWith("Rule_",StringComparison.Ordinal) ||
                b.name.StartsWith("CartaTab",StringComparison.Ordinal) ||
                b.name=="Close")continue;
            SkinButton(b);
        }
        foreach(Toggle toggle in panel.GetComponentsInChildren<Toggle>(true))
        {
            Text label=toggle.transform.Find("Label")?.GetComponent<Text>();
            if(label!=null)SetTypeface(label,false,12);
        }
        foreach(string path in new[]{"Rules/Signals","Status","Resolution"})
        {
            Text value=panel.Find(path)?.GetComponent<Text>();
            if(value!=null){SetTypeface(value,false,11);value.color=Muted;}
        }
        Transform rules=panel.Find("Rules");
        Transform detail=panel.Find("RuleEditor");
        if(rules!=null) Line(rules,"ReferenceEventsRule",.035f,.965f,.32f);
        if(detail!=null)
        {
            Line(detail,"ReferenceBottomRule",.035f,.965f,.185f);
            foreach(Text caption in detail.GetComponentsInChildren<Text>(true))
        {
            if(caption.name!="Hint")continue;
            SetTypeface(caption,false,11);
        }
        }
        AddCorners(panel);
        BistroBuilderCartaReferenceV3Responsive responsive =
            panel.GetComponent<BistroBuilderCartaReferenceV3Responsive>();
        if (responsive == null)
            responsive = panel.gameObject.AddComponent<BistroBuilderCartaReferenceV3Responsive>();
        responsive.ApplyImmediate(true);
    }

    private static void AddCorners(RectTransform panel)
    {
        foreach(var item in new[]{
            new Vector2(0,0),new Vector2(0,1),new Vector2(1,0),new Vector2(1,1)})
        {
            RectTransform r=Rect("ReferenceBrassRivet",panel,item,item,
                new Vector2(item.x==0?7:-15,item.y==0?7:-15),
                new Vector2(item.x==0?15:-7,item.y==0?15:-7));
            Image img=BistroBuilderMenuEditorUiFactory.AddImage(r,Color.white);
            img.sprite=dotBrass;img.raycastTarget=false;
        }
        AddFrameAccents(panel);
    }

    private static void AddFrameAccents(RectTransform panel)
    {
        if (panel == null || panel.Find("ReferenceInnerFrameTop") != null) return;
        Color32 lineColor = new Color32(170, 118, 54, 190);

        RectTransform top = Rect("ReferenceInnerFrameTop", panel,
            new Vector2(0,1), new Vector2(1,1),
            new Vector2(8,-8), new Vector2(-8,-7));
        BistroBuilderMenuEditorUiFactory.AddImage(top,lineColor).raycastTarget=false;

        RectTransform bottom = Rect("ReferenceInnerFrameBottom", panel,
            Vector2.zero, new Vector2(1,0),
            new Vector2(8,7), new Vector2(-8,8));
        BistroBuilderMenuEditorUiFactory.AddImage(bottom,lineColor).raycastTarget=false;

        RectTransform left = Rect("ReferenceInnerFrameLeft", panel,
            Vector2.zero, new Vector2(0,1),
            new Vector2(7,8), new Vector2(8,-8));
        BistroBuilderMenuEditorUiFactory.AddImage(left,lineColor).raycastTarget=false;

        RectTransform right = Rect("ReferenceInnerFrameRight", panel,
            new Vector2(1,0), Vector2.one,
            new Vector2(-8,8), new Vector2(-7,-8));
        BistroBuilderMenuEditorUiFactory.AddImage(right,lineColor).raycastTarget=false;
    }

    private static string ButtonIcon(string name)
    {
        switch(name)
        {
            case "Creardesdeactiva":return "plus";
            case "Duplicar":return "copy";
            case "Renombrar":return "edit";
            case "Eliminar":return "trash";
            case "Fijarcomobase":return "star";
            case "Activarmanual":return "power";
            case "Reglasautomáticas":return "clock";
            case "Editarcartaactiva":return "edit";
            case "NuevaRegla":return "plus";
            case "Nuevaregla":return "plus";
            case "Eliminarregla":return "trash";
            // + Evento / + Promo already carry the plus in their text in V3.
            case "+Evento":return null;
            case "+Promo":return null;
            case "Guardarregla":return "save";
            case "Limpiarformulario":return "broom";
            case "AddDish":return "plus";
            case "RemoveDish":return "trash";
            case "DuplicateDish":return "copy";
            case "Apply":return "save";
            case "EditDishPhoto":return "edit";
            default:return null;
        }
    }

    private static void SkinButton(Button button)
    {
        bool destructive=button.name=="Eliminar" || button.name=="Eliminarregla" ||
            button.name=="ConfirmDeletion" || button.name=="DiscardAndClose" ||
            button.name=="RemoveDish";
        bool flatField=button.name=="Tipo" || button.name=="Cartadestino";

        // Carta V3 is authoritative: suppress every legacy/design-system child
        // graphic before installing the single approved brown/white glyph.
        foreach(Image child in button.GetComponentsInChildren<Image>(true))
        {
            if(child == button.GetComponent<Image>()) continue;
            if(child.transform.name == "ReferenceActionIcon") continue;
            child.enabled = false;
        }

        SkinImage(button.GetComponent<Image>(),flatField?input:destructive?danger:normalButton);
        Image bg=button.GetComponent<Image>();
        if(bg!=null)bg.raycastTarget=true;
        ColorBlock colors=button.colors;
        colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.03f,1.03f,1.03f,1);
        colors.pressedColor=new Color(.91f,.89f,.87f,1);
        colors.selectedColor=Color.white;
        colors.disabledColor=Color.white;
        button.colors=colors;
        Text label=button.GetComponentInChildren<Text>(true);
        if(label==null)return;
        SetTypeface(label,!flatField,flatField?13:12,true);
        label.alignment=flatField?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
        if(flatField)
        {
            if(Inter!=null)label.font=Inter;
            label.rectTransform.offsetMin=new Vector2(10,2);
            label.rectTransform.offsetMax=new Vector2(-8,-2);
            return;
        }
        if(destructive)label.color=Color.white;
        string id=ButtonIcon(button.name);
        if(id==null)return;
        if(destructive && id=="trash")id="trash-white";
        Sprite sprite=ReferenceIcon(id);
        if(sprite==null)return;
        Image icon=AddReferenceIcon("ReferenceActionIcon",button.transform,id,
            0,.5f,0,.5f,new Vector2(12,-7),new Vector2(27,8));
        if(icon!=null)
        {
            icon.color=Color.white;
            label.rectTransform.offsetMin=new Vector2(22,2);
            label.rectTransform.offsetMax=new Vector2(-4,-2);
        }
        MixedTypefaceRuns(label);
    }

    private static void SkinEditorButton(Button button)
    {
        SkinButton(button); // Retain the existing action, icon and interaction.
        if (button == null) return;
        bool destructive = button.name == "RemoveDish" ||
            button.name == "ConfirmDeletion" ||
            button.name == "DiscardAndClose";
        bool plain = button.name == "OrderCategory" ||
            button.name == "OrderDirection" ||
            button.name == "MoveUp" || button.name == "MoveDown";
        bool primary = button.name == "AddDish" || button.name == "Apply";
        Image background = button.GetComponent<Image>();
        SkinImage(background, destructive ? editorDanger :
            primary ? editorGold : plain ? editorField : editorButton);
        if (background != null) background.raycastTarget = true;
        bool fieldStepper = button.name == "MoveUp" || button.name == "MoveDown";
        if (fieldStepper && background != null)
        {
            // The position field owns the ivory surface. Keep both arrows
            // clickable but remove their separate heavy button frames.
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = Color.clear;
        }
        Text label = button.transform.Find("Label")?.GetComponent<Text>();
        if (fieldStepper && label != null)
        {
            if (InterBold != null) label.font = InterBold;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Brass;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }
        if (label != null)
        {
            // Footer buttons are the primary actions in the approved
            // screenshot; 12px inherited button labels look miniature.
            if (button.name == "AddDish" || button.name == "RemoveDish" ||
                button.name == "DuplicateDish" || button.name == "Apply" ||
                button.name == "Discard")
            {
                SetTypeface(label, false, 18, true);
                label.alignment = TextAnchor.MiddleCenter;
                label.color = primary || destructive ? Color.white : Ink;
            }
            if (primary) label.color = Color.white;
        }
        Transform mixed = button.transform.Find("ReferenceTypefaceRuns");
        if (mixed != null)
        {
            bool footerButton = button.name == "AddDish" ||
                button.name == "RemoveDish" || button.name == "DuplicateDish" ||
                button.name == "Apply" || button.name == "Discard";
            foreach (Text segment in mixed.GetComponentsInChildren<Text>(true))
            {
                if (footerButton) segment.fontSize = 18;
                if (primary || destructive) segment.color = Color.white;
            }
        }
        // Sorting changes its label at runtime. A fixed V3 mixed-font overlay
        // would keep displaying "Categoría" even after sorting by price/name.
        // This single dynamic control uses Inter, which includes accents.
        if (button.name == "OrderCategory")
        {
            if (mixed != null) mixed.gameObject.SetActive(false);
            if (label != null)
            {
                label.enabled = true;
                if (InterBold != null) label.font = InterBold;
                label.fontStyle = FontStyle.Normal;
                label.alignment = TextAnchor.MiddleCenter;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 16;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
        RemoveOldBevel(button.transform);
    }

    private static void DecorateEditorValue(Transform panel, string relativePath,
        string glyph, string iconId = null)
    {
        Transform root = panel.Find(relativePath);
        if (root == null || root.Find("ReferenceFieldIconPlate") != null) return;
        RectTransform plate = Rect("ReferenceFieldIconPlate", root,
            new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(2f, -12f), new Vector2(28f, 12f));
        Image plateImage = BistroBuilderMenuEditorUiFactory.AddImage(plate,
            new Color32(232, 215, 190, 255));
        plateImage.raycastTarget = false;
        if (!string.IsNullOrEmpty(iconId))
        {
            Image icon = AddReferenceIcon("ReferenceFieldIcon", plate, iconId,
                0f, 0f, 1f, 1f,
                new Vector2(4f, 4f), new Vector2(-4f, -4f));
            if (icon != null)
            {
                icon.enabled = true;
                icon.color = Brass;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
        }
        else
        {
            Text symbol = BistroBuilderMenuEditorUiFactory.CreateText(
                "ReferenceFieldGlyph", plate, glyph, 15,
                TextAnchor.MiddleCenter, Brass, FontStyle.Bold);
            symbol.color = Brass;
        }

        foreach (Text value in root.GetComponentsInChildren<Text>(true))
        {
            if (value.name == "ReferenceFieldGlyph" || value.name == "ReferenceFieldIcon")
                continue;
            if (value.name == "Label") continue;
            value.rectTransform.offsetMin =
                new Vector2(Mathf.Max(value.rectTransform.offsetMin.x, 34f),
                    value.rectTransform.offsetMin.y);
        }
    }

    public static void DecorateEditorAvailability(Toggle toggle)
    {
        if (toggle == null || toggle.transform.Find("ReferenceAvailabilityTrack") != null)
            return;

        Image background = toggle.gameObject.GetComponent<Image>();
        if (background == null)
            background = toggle.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = true;
        toggle.targetGraphic = background;
        toggle.transition = Selectable.Transition.None;

        Transform legacyBox = toggle.transform.Find("Box");
        if (legacyBox != null)
        {
            Image oldImage = legacyBox.GetComponent<Image>();
            if (oldImage != null)
            {
                oldImage.color = Color.clear;
                oldImage.raycastTarget = false;
            }
            Text check = legacyBox.Find("Check")?.GetComponent<Text>();
            if (check != null) check.color = Color.clear;
        }

        RectTransform track = Rect("ReferenceAvailabilityTrack",
            toggle.transform, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(2f, -10f), new Vector2(42f, 10f));
        Image trackImage = BistroBuilderMenuEditorUiFactory.AddImage(track, Color.white);
        trackImage.raycastTarget = false;
        RectTransform knob = Rect("ReferenceAvailabilityKnob", track,
            new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(-8f, -8f), new Vector2(8f, 8f));
        Image knobImage = BistroBuilderMenuEditorUiFactory.AddImage(knob, Color.white);
        knobImage.raycastTarget = false;

        Text label = toggle.transform.Find("Label")?.GetComponent<Text>();
        if (label != null)
        {
            SetTypeface(label, false, 12);
            label.color = Muted;
            label.rectTransform.offsetMin = new Vector2(52f, 0f);
        }
        StyleAvailabilitySwitch(trackImage, knobImage, toggle.isOn);
        toggle.onValueChanged.AddListener(value =>
            StyleAvailabilitySwitch(trackImage, knobImage, value));
    }

    public static void RefreshEditorAvailability(Toggle toggle, bool isOn)
    {
        if (toggle == null) return;
        Image track = toggle.transform.Find("ReferenceAvailabilityTrack")
            ?.GetComponent<Image>();
        Image knob = toggle.transform.Find("ReferenceAvailabilityTrack/ReferenceAvailabilityKnob")
            ?.GetComponent<Image>();
        StyleAvailabilitySwitch(track, knob, isOn);
    }

    private static Sprite SearchMagnifier()
    {
        if (editorSearchIcon != null) return editorSearchIcon;
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CartaV3_SearchMagnifier",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 start = new Vector2(18f, 12f);
        Vector2 end = new Vector2(27f, 3f);
        Vector2 axis = end - start;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + .5f, y + .5f);
            float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(12f, 20f)) - 7f);
            float t = Mathf.Clamp01(Vector2.Dot(p - start, axis) /
                axis.sqrMagnitude);
            float stem = Vector2.Distance(p, start + axis * t);
            float alpha = Mathf.Max(
                Mathf.Clamp01(1.65f - ring),
                Mathf.Clamp01(1.45f - stem));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        editorSearchIcon = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f), 100f);
        return editorSearchIcon;
    }

    private static void DecorateSearchField(Transform root)
    {
        InputField search = root?.Find("Body/List/ListHeader/Search")
            ?.GetComponent<InputField>();
        if (search == null || search.transform.Find("ReferenceSearchIcon") != null)
            return;
        RectTransform iconRect = Rect("ReferenceSearchIcon",
            search.transform, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(8f, -11f), new Vector2(30f, 11f));
        Image icon = BistroBuilderMenuEditorUiFactory.AddImage(iconRect, Brass);
        icon.sprite = SearchMagnifier();
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        if (search.textComponent != null)
        {
            RectTransform textRect = search.textComponent.rectTransform;
            textRect.offsetMin = new Vector2(37f, textRect.offsetMin.y);
        }
        if (search.placeholder is Text placeholder)
        {
            RectTransform placeholderRect = placeholder.rectTransform;
            placeholderRect.offsetMin =
                new Vector2(37f, placeholderRect.offsetMin.y);
        }
    }

    public static void ApplyEditor(RectTransform panel)
    {
        if (panel == null) return;
        EnsureEditorSurfaces();
        SkinImage(panel.GetComponent<Image>(), editorOuter);
        RemoveOldBevel(panel);

        foreach (string path in new[] { "Body/Sidebar", "Body/List", "Body/Detail", "Footer" })
        {
            Transform node = panel.Find(path);
            if (node == null) continue;
            SkinImage(node.GetComponent<Image>(), editorCard);
            RemoveOldBevel(node);
            AddEditorFineRim(node);
        }

        Transform listHeader = panel.Find("Body/List/ListHeader");
        if (listHeader != null)
            SkinImage(listHeader.GetComponent<Image>(), editorField);
        Transform columnHeader = panel.Find("Body/List/ColumnHeader");
        if (columnHeader != null)
            SkinImage(columnHeader.GetComponent<Image>(), editorRow);

        Transform header = panel.Find("Header");
        if (header != null)
        {
            Image headerImage = header.GetComponent<Image>();
            if (headerImage != null)
            {
                headerImage.color = Color.clear;
                headerImage.raycastTarget = false;
            }
            if (header.Find("ReferenceTopRule") == null)
                Line(header, "ReferenceTopRule", 0, 1, 0);

            Text title = header.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "CARTA";
                SetTypeface(title, true, 32, true);
                Anchor(title.rectTransform, .074f, 0, .36f, 1,
                    Vector2.zero, Vector2.zero);
            }

            Transform iconNode = header.Find("CartaHeaderIcon");
            Image icon = iconNode?.GetComponent<Image>();
            Sprite book = ReferenceIcon("book");
            if (icon != null && book != null)
            {
                icon.sprite = book;
                icon.color = Color.white;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Anchor(icon.rectTransform, 0, 0, 0, 1,
                    new Vector2(13, 6), new Vector2(69, -6));
            }

            Text context = header.Find("Context")?.GetComponent<Text>();
            if (context != null)
            {
                SetTypeface(context, false, 11);
                context.alignment = TextAnchor.MiddleRight;
                context.color = Muted;
            }

            Transform close = header.Find("Close");
            if (close != null)
            {
                close.gameObject.SetActive(true);
                Button closeButton = close.GetComponent<Button>();
                if (closeButton != null)
                {
                    SkinImage(closeButton.GetComponent<Image>(), editorField);
                    RemoveOldBevel(close);
                    Text closeLabel = closeButton.GetComponentInChildren<Text>(true);
                    if (closeLabel != null)
                    {
                        SetTypeface(closeLabel, true, 21, true);
                        closeLabel.alignment = TextAnchor.MiddleCenter;
                    }
                }
            }
        }

        foreach (InputField field in panel.GetComponentsInChildren<InputField>(true))
        {
            SkinImage(field.GetComponent<Image>(), editorField);
            if (field.textComponent != null) SetTypeface(field.textComponent, false, 16);
            if (field.placeholder is Text hint)
            {
                SetTypeface(hint, false, 14);
                hint.color = new Color32(139, 108, 78, 255);
            }
        }

        foreach (Image image in panel.GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            string n = image.name;
            if (n == "CategoryValuePlate" || n == "ModeValuePlate" ||
                n == "OrderValuePlate" || n == "CostValuePlate" ||
                n == "MarginValuePlate")
            {
                SkinImage(image, editorField);
            }
            else if (n == "DishPhotoFrame")
            {
                SkinImage(image, editorCard);
            }
        }

        DecorateSearchField(panel);
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/Price",
            "", "field-euro");
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/CategoryValuePlate",
            "", "field-category");
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/ModeValuePlate",
            "", "field-cutlery");
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/PreparationTime",
            "", "field-clock");
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/OrderValuePlate",
            "", "field-order");
        Transform orderPlate = panel.Find(
            "Body/Detail/DetailCanvas/OrderValuePlate");
        if (orderPlate != null && orderPlate.Find("ReferenceOrderStepperDivider") == null)
        {
            RectTransform divider = Rect("ReferenceOrderStepperDivider",
                orderPlate, new Vector2(.86f, .10f), new Vector2(.86f, .90f),
                new Vector2(0f, 0f), new Vector2(1f, 0f));
            Image line = BistroBuilderMenuEditorUiFactory.AddImage(
                divider, new Color32(197, 165, 128, 150));
            line.raycastTarget = false;
        }
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/CostValuePlate",
            "", "field-coins");
        DecorateEditorValue(panel, "Body/Detail/DetailCanvas/MarginValuePlate",
            "", "field-margin");
        DecorateEditorAvailability(panel.Find(
            "Body/Detail/DetailCanvas/AvailableToggle")?.GetComponent<Toggle>());

        foreach (Text text in panel.GetComponentsInChildren<Text>(true))
        {
            if (text == null) continue;
            switch (text.name)
            {
                case "DetailHeading":
                case "DishTitle":
                    SetTypeface(text, true, text.name == "DishTitle" ? 24 : 23, true);
                    break;
                case "DishSubtitle":
                case "EditingCaption":
                    SetTypeface(text, false, text.name == "DishSubtitle" ? 14 : 11);
                    text.color = Muted;
                    break;
                case "Columns":
                    SetTypeface(text, false, 9, true);
                    text.color = Muted;
                    break;
                case "OrderLabel":
                case "Count":
                    SetTypeface(text, false, 9);
                    text.color = Muted;
                    break;
                case "CategoryValue":
                case "ModeValue":
                case "OrderValue":
                case "CostValue":
                case "MarginValue":
                    SetTypeface(text, false, 16);
                    break;
                default:
                    if (text.name.EndsWith("Caption", StringComparison.Ordinal))
                    {
                        SetTypeface(text, false, 16);
                        text.color = Muted;
                    }
                    break;
            }
        }

        Transform detailCanvas = panel.Find("Body/Detail/DetailCanvas");
        if (detailCanvas != null)
        {
            // Prevent long dish titles from painting over the status badge.
            // The approved reference keeps the title and its badge in separate
            // anchored columns. Bounds must be restored after SetTypeface().
            Text dishTitle = detailCanvas.Find("DishTitle")?.GetComponent<Text>();
            if (dishTitle != null)
            {
                dishTitle.resizeTextForBestFit = true;
                dishTitle.resizeTextMinSize = 15;
                dishTitle.resizeTextMaxSize = 26;
                dishTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                dishTitle.verticalOverflow = VerticalWrapMode.Truncate;
            }
            Text dishSubtitle = detailCanvas.Find("DishSubtitle")?.GetComponent<Text>();
            if (dishSubtitle != null)
            {
                dishSubtitle.resizeTextForBestFit = true;
                dishSubtitle.resizeTextMinSize = 11;
                dishSubtitle.resizeTextMaxSize = 16;
                dishSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                dishSubtitle.verticalOverflow = VerticalWrapMode.Truncate;
            }
            // SetTypeface defaults to Overflow; reapply bounded wrapping
            // AFTER theme styling to avoid the long field captions bleeding
            // into their editable controls at 1280x720.
            foreach (Text label in detailCanvas.GetComponentsInChildren<Text>(true))
            {
                if (!label.name.EndsWith("Caption", StringComparison.Ordinal))
                    continue;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 16;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (detailCanvas.Find("ReferenceDetailHeadingIcon") == null)
            {
                Image headingIcon = AddReferenceIcon(
                    "ReferenceDetailHeadingIcon", detailCanvas, "file",
                    0f, .946f, 0f, .946f,
                    new Vector2(2f, -12f), new Vector2(24f, 12f));
                if (headingIcon != null) headingIcon.color = Color.white;
            }
            Text detailHeading = detailCanvas.Find("DetailHeading")?.GetComponent<Text>();
            if (detailHeading != null)
                Anchor(detailHeading.rectTransform, 0f, .915f, .75f, 1f,
                    new Vector2(30f, 0f), Vector2.zero);
            if (detailCanvas.Find("ReferenceDetailRule") == null)
                Line(detailCanvas, "ReferenceDetailRule", 0f, 1f, .91f);
        }
        Button photoEdit = panel.Find(
            "Body/Detail/DetailCanvas/DishPhotoFrame/EditDishPhoto"
        )?.GetComponent<Button>();
        if (photoEdit != null)
        {
            Image tile = photoEdit.GetComponent<Image>();
            if (tile != null)
            {
                tile.sprite = null;
                tile.type = Image.Type.Simple;
                tile.color = new Color32(52, 39, 28, 236);
                tile.raycastTarget = true;
            }
            RemoveOldBevel(photoEdit.transform);
            Text editLabel = photoEdit.transform.Find("Label")?.GetComponent<Text>();
            // The reference shows a white pencil icon within the dark photo
            // corner tile. Use the existing raster art instead of the ✎
            // Unicode glyph, which varies by font and platform.
            Transform graphic = photoEdit.transform.Find("ReferencePhotoEditIcon");
            Image photoEditIcon = graphic?.GetComponent<Image>();
            if (photoEditIcon == null)
            {
                photoEditIcon = AddReferenceIcon(
                    "ReferencePhotoEditIcon", photoEdit.transform, "edit",
                    .15f, .15f, .85f, .85f, Vector2.zero, Vector2.zero);
            }
            if (photoEditIcon != null)
            {
                photoEditIcon.enabled = true;
                photoEditIcon.color = Color.white;
                photoEditIcon.type = Image.Type.Simple;
                photoEditIcon.preserveAspect = true;
                photoEditIcon.raycastTarget = false;
            }
            if (editLabel != null)
            {
                // Only retain the text fallback if the resource cannot load.
                editLabel.enabled = photoEditIcon == null;
                if (photoEditIcon == null)
                {
                    SetTypeface(editLabel, false, 20, true);
                    editLabel.color = Color.white;
                    editLabel.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.name.StartsWith("Category_", StringComparison.Ordinal) ||
                button.name.StartsWith("Filter_", StringComparison.Ordinal) ||
                button.name.StartsWith("CartaTab", StringComparison.Ordinal) ||
                button.name == "Close" ||
                button.name == "EditDishPhoto")
                continue;
            SkinEditorButton(button);
        }

        foreach (ScrollRect scroll in panel.GetComponentsInChildren<ScrollRect>(true))
        {
            Image background = scroll.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.clear;
                background.raycastTarget = false;
            }
        }

        // Title is explicitly rendered last within this header, above the
        // inherited management/header graphics. The earlier Title remains
        // as semantic/UI-test metadata but is not the visible text renderer.
        Transform editorHeader = panel.Find("Header");
        if (editorHeader != null)
        {
            Text legacyTitle = editorHeader.Find("Title")?.GetComponent<Text>();
            if (legacyTitle != null) legacyTitle.enabled = false;
            Transform overlayNode = editorHeader.Find("ReferenceHeaderTitle");
            Text overlayTitle = overlayNode != null
                ? overlayNode.GetComponent<Text>()
                : BistroBuilderMenuEditorUiFactory.CreateText(
                    "ReferenceHeaderTitle", editorHeader, "CARTA", 32,
                    TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            if (overlayTitle != null)
            {
                overlayTitle.text = "CARTA";
                SetTypeface(overlayTitle, true, 32, true);
                overlayTitle.color = Ink;
                overlayTitle.alignment = TextAnchor.MiddleLeft;
                overlayTitle.raycastTarget = false;
                Anchor(overlayTitle.rectTransform, .074f, 0f, .38f, 1f,
                    Vector2.zero, Vector2.zero);
                // The legacy uGUI renderer reports healthy metadata yet
                // draws no uppercase header on the actual Windows build.
                // Keep it as metadata and render the approved Recoleta SDF
                // font through TMP for reliable on-screen output.
                overlayTitle.enabled = false;
                overlayTitle.gameObject.SetActive(true);
            }
            Transform tmpNode = editorHeader.Find("ReferenceHeaderTitleTMP");
            RectTransform tmpRect = tmpNode as RectTransform;
            if (tmpRect == null)
                tmpRect = Rect("ReferenceHeaderTitleTMP", editorHeader,
                    new Vector2(.074f, 0f), new Vector2(.38f, 1f),
                    Vector2.zero, Vector2.zero);
            TextMeshProUGUI visibleTitle = tmpRect.GetComponent<TextMeshProUGUI>();
            if (visibleTitle == null)
                visibleTitle = tmpRect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset approvedFont = Resources.Load<TMP_FontAsset>(
                "BistroBuilder/UI/Typography/Recoleta-SDF");
            if (approvedFont != null) visibleTitle.font = approvedFont;
            visibleTitle.text = "CARTA";
            visibleTitle.fontSize = 45f;
            visibleTitle.fontStyle = FontStyles.Bold;
            visibleTitle.color = Ink;
            visibleTitle.alignment = TextAlignmentOptions.MidlineLeft;
            visibleTitle.enableAutoSizing = false;
            visibleTitle.enableWordWrapping = false;
            visibleTitle.overflowMode = TextOverflowModes.Overflow;
            visibleTitle.raycastTarget = false;
            tmpRect.SetAsLastSibling();
        }

        if (panel.Find("ReferenceBrassRivet") == null)
            AddCorners(panel);

        BistroBuilderCartaReferenceV3Responsive responsive =
            panel.GetComponent<BistroBuilderCartaReferenceV3Responsive>();
        if (responsive == null)
            responsive = panel.gameObject.AddComponent<BistroBuilderCartaReferenceV3Responsive>();
        responsive.ApplyImmediate(true);
    }

    public static void StyleNavigationTab(Button button, bool selected)
    {
        if (button == null) return;
        EnsureEditorSurfaces();
        bool editor = button.GetComponentInParent<BistroBuilderMenuEditorRuntimeView>() != null;
        Image background = button.GetComponent<Image>();
        SkinImage(background, editor
            ? (selected ? editorSelected : editorField)
            : (selected ? selectedRow : input));
        if (background != null) background.raycastTarget = true;
        if (editor) RemoveOldBevel(button.transform);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.03f, 1.03f, 1.03f, 1f);
        colors.pressedColor = new Color(.91f, .89f, .87f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Text label = button.transform.Find("Label")?.GetComponent<Text>();
        if (label != null)
        {
            SetTypeface(label, true, editor ? 18 : 12, true);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(editor ? 34f : 26f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            MixedTypefaceRuns(label);
        }

        Transform existing = button.transform.Find("ReferenceTabIcon");
        if (existing == null)
        {
            if (button.name == "CartaTabManagement")
            {
                Image icon = AddReferenceIcon(
                    "ReferenceTabIcon", button.transform, "book",
                    0, .5f, 0, .5f,
                    new Vector2(9, -13), new Vector2(35, 13));
                if (icon != null) icon.color = Color.white;
            }
            else if (button.name == "CartaTabDishes")
            {
                // Replace the old placeholder arch glyph with the same
                // authored plate/food icon used by BistroBuilder's category art.
                Sprite plate = CategorySprite("Principales");
                RectTransform rect = Rect(
                    "ReferenceTabIcon", button.transform,
                    new Vector2(0f, .5f), new Vector2(0f, .5f),
                    new Vector2(8f, -14f), new Vector2(36f, 14f));
                Image icon = BistroBuilderMenuEditorUiFactory.AddImage(
                    rect, Brass);
                icon.sprite = plate;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
        }
    }

    private static Sprite CategoryGrid()
    {
        if (categoryGrid != null) return categoryGrid;
        Texture2D texture = new Texture2D(40, 40, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "CartaV3_CategoriesGrid"
        };
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 40; x++)
        {
            int bx = x >= 22 ? 22 : 7;
            int by = y >= 22 ? 22 : 7;
            bool inSquare = x >= bx && x <= bx + 11 &&
                y >= by && y <= by + 11;
            bool edge = inSquare &&
                (x < bx + 3 || x > bx + 8 ||
                 y < by + 3 || y > by + 8);
            texture.SetPixel(x, y, edge ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        categoryGrid = Sprite.Create(texture, new Rect(0, 0, 40, 40),
            new Vector2(.5f, .5f), 100f);
        categoryGrid.name = "CartaV3_CategoriesGrid";
        return categoryGrid;
    }

    private static Sprite CategorySprite(string label)
    {
        // The six approved Carta categories use dedicated in-game icons.
        // Unlike BBIconCatalog SVG imports, these six raster assets are
        // guaranteed to be visible through the runtime uGUI Image renderer.
        string category = (label ?? string.Empty).ToLowerInvariant();
        string rasterName = category.Contains("todo") ? "category-all" :
            category.Contains("entr") ? "category-starters" :
            category.Contains("principal") ? "category-mains" :
            category.Contains("postre") ? "category-desserts" :
            category.Contains("bebida") ? "category-drinks" :
            category.Contains("especial") ? "category-specials" : null;
        Sprite authored = rasterName != null ? ReferenceIcon(rasterName) : null;
        if (authored != null) return authored;
        if (categoryIcons == null) categoryIcons = BBIconCatalog.LoadDefault();
        if (categoryIcons == null) return CategoryGrid();
        BBIconId id = category.Contains("entr") ? BBIconId.FoodStarter :
            category.Contains("principal") ? BBIconId.FoodMain :
            category.Contains("postre") ? BBIconId.FoodDessert :
            category.Contains("bebida") ? BBIconId.FoodDrink :
            BBIconId.ObjectDish;
        return categoryIcons.GetSprite(id) ?? CategoryGrid();
    }

    public static void StyleSelectorButton(Button button, bool selected)
    {
        if (button == null) return;
        StyleNavigationTab(button, selected);
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            // Unselected categories in the reference are text rows, not
            // outlined buttons. The transparent Image retains hit testing.
            background.color = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;
        }

        Text label = button.transform.Find("Label")?.GetComponent<Text>();
        if (label == null) return;
        SetTypeface(label, true, 20, selected);
        // The shared tab helper emits a static 12px mixed-typeface overlay.
        // Category rows must instead render their live, correctly sized text.
        Transform staleRuns = button.transform.Find("ReferenceTypefaceRuns");
        if (staleRuns != null) staleRuns.gameObject.SetActive(false);
        label.enabled = true;
        label.alignment = TextAnchor.MiddleLeft;
        label.rectTransform.offsetMin = new Vector2(57f, 1f);
        label.rectTransform.offsetMax = new Vector2(-7f, -1f);

        Transform separator = button.transform.Find("ReferenceCategorySeparator");
        if (separator == null)
        {
            RectTransform line = Rect("ReferenceCategorySeparator",
                button.transform, new Vector2(.07f, 0),
                new Vector2(.93f, 0), new Vector2(0f, 0f),
                new Vector2(0f, 1f));
            Image lineImage = BistroBuilderMenuEditorUiFactory.AddImage(
                line, new Color32(216, 190, 156, 160));
            lineImage.raycastTarget = false;
            separator = line;
        }
        separator.gameObject.SetActive(!selected);

        Sprite sprite = CategorySprite(label.text);
        Transform oldIcon = button.transform.Find("ReferenceCategoryIcon");
        if (oldIcon == null)
        {
            RectTransform iconRect = Rect("ReferenceCategoryIcon",
                button.transform,
                new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(10f, -20f), new Vector2(50f, 20f));
            Image categoryIcon = BistroBuilderMenuEditorUiFactory.AddImage(
                iconRect, Brass);
            categoryIcon.sprite = sprite;
            categoryIcon.enabled = true;
            categoryIcon.color = Brass;
            categoryIcon.type = Image.Type.Simple;
            categoryIcon.preserveAspect = true;
            categoryIcon.raycastTarget = false;
        }
        else
        {
            Image categoryIcon = oldIcon.GetComponent<Image>();
            if (categoryIcon != null)
            {
                categoryIcon.sprite = sprite;
                categoryIcon.enabled = true;  // legacy styling may disable child Images
                categoryIcon.color = Brass;
                categoryIcon.type = Image.Type.Simple;
                categoryIcon.preserveAspect = true;
                categoryIcon.raycastTarget = false;
            }
        }
    }

    public static void StyleEditorInclusionButton(Button button, bool destructive)
    {
        if (button == null) return;
        EnsureEditorSurfaces();
        SkinImage(button.GetComponent<Image>(),
            destructive ? editorDanger : editorGold);
        RemoveOldBevel(button.transform);
        Image background = button.GetComponent<Image>();
        if (background != null) background.raycastTarget = true;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            SetTypeface(label, true, 12, true);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }
    }

    public static void StyleDetailStatus(Text status, bool included)
    {
        if (status == null) return;
        EnsureEditorSurfaces();
        Image plate = status.transform.parent.GetComponent<Image>();
        if (plate != null)
        {
            plate.sprite = null;
            plate.type = Image.Type.Simple;
            plate.color = included
                ? new Color32(223, 237, 212, 255)
                : new Color32(234, 225, 211, 255);
            plate.raycastTarget = false;
        }
        status.color = included ? new Color32(45, 103, 40, 255) : Muted;
        status.alignment = TextAnchor.MiddleCenter;
        status.resizeTextForBestFit = true;
        status.resizeTextMinSize = 9;
        status.resizeTextMaxSize = 12;
        status.horizontalOverflow = HorizontalWrapMode.Wrap;
        status.verticalOverflow = VerticalWrapMode.Truncate;
        status.rectTransform.offsetMin = new Vector2(18f, 0f);
        status.rectTransform.offsetMax = new Vector2(-3f, 0f);
        Transform dot = status.transform.parent.Find("StatusDot");
        if (dot == null)
        {
            RectTransform marker = Rect("StatusDot", status.transform.parent,
                new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(9f, -5f), new Vector2(19f, 5f));
            Image icon = BistroBuilderMenuEditorUiFactory.AddImage(marker, Color.white);
            icon.raycastTarget = false;
            dot = marker;
        }
        Image dotImage = dot.GetComponent<Image>();
        if (dotImage != null)
        {
            dotImage.sprite = included ? dotGreen : dotGrey;
            dotImage.color = Color.white;
        }
    }

    public static void StyleAvailabilitySwitch(
        Image track, Image knob, bool available)
    {
        EnsureEditorSurfaces();
        if (track != null)
        {
            SkinImage(track, available ? editorSwitchOn : editorSwitchOff);
            track.enabled = true;
            track.color = Color.white;
            track.raycastTarget = false; // row click still selects its dish
        }
        if (knob != null)
        {
            knob.sprite = editorSwitchKnob;
            knob.enabled = true;
            knob.type = Image.Type.Simple;
            knob.preserveAspect = true;
            knob.color = Color.white;
            knob.raycastTarget = false;
            RectTransform rect = knob.rectTransform;
            rect.anchorMin = new Vector2(available ? 1f : 0f, .5f);
            rect.anchorMax = rect.anchorMin;
            rect.sizeDelta = new Vector2(16f, 16f);
            rect.anchoredPosition = new Vector2(available ? -10f : 10f, 0f);
        }
    }

    public static void StyleDishPreparationClock(Image clock)
    {
        if (clock == null) return;
        Sprite icon = ReferenceIcon("field-clock");
        clock.sprite = icon;
        clock.enabled = true;
        clock.type = Image.Type.Simple;
        clock.preserveAspect = true;
        clock.color = new Color32(143, 103, 57, 255);
        clock.raycastTarget = false;
    }

    public static void StyleDishRow(Button button, bool selected)
    {
        if (button == null) return;
        EnsureEditorSurfaces();
        SkinImage(button.GetComponent<Image>(), selected ? editorSelected : editorRow);
        Image background = button.GetComponent<Image>();
        if (background != null) background.raycastTarget = true;

        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            if (text == null) continue;
            switch (text.name)
            {
                case "Name":
                    SetTypeface(text, true, 19, true);
                    break;
                case "Subtitle":
                    SetTypeface(text, false, 14);
                    text.color = Muted;
                    break;
                case "Price":
                    SetTypeface(text, false, 16, true);
                    break;
                case "Available":
                    SetTypeface(text, false, 15, true);
                    break;
                case "Margin":
                    SetTypeface(text, false, 16, true);
                    break;
                default:
                    SetTypeface(text, false, 12);
                    break;
            }
        }
    }

    private static string CategoryGlyph(string label)
    {
        string value = (label ?? string.Empty).ToLowerInvariant();
        if (value.Contains("todo")) return "▦";
        if (value.Contains("entr")) return "⌁";
        if (value.Contains("principal")) return "⌒";
        if (value.Contains("postre")) return "▤";
        if (value.Contains("bebida")) return "◫";
        if (value.Contains("especial")) return "★";
        return "•";
    }

    public static void StyleRow(Button button,bool selected,bool rule,bool baseMenu,bool active)
    {
        if(button==null)return;
        EnsureSurfaces();
        Image background=button.GetComponent<Image>();
        SkinImage(background,selected?selectedRow:plainRow);
        if(background!=null)background.raycastTarget=true;
        Text label=button.transform.Find("Label")?.GetComponent<Text>();
        if(label==null)return;
        string[] parts=(label.text??string.Empty).Split(new[]{'\n'},2);
        label.text=parts.Length>0?parts[0]:string.Empty;
        SetTypeface(label,true,14,true);
        label.alignment=TextAnchor.MiddleLeft;
        Anchor(label.rectTransform,0,0,1,1,new Vector2(50,29),new Vector2(-42,-5));
        MixedTypefaceRuns(label);
        Text desc=BistroBuilderMenuEditorUiFactory.CreateText(
            "ReferenceRowDescription",button.transform,parts.Length>1?parts[1]:"",
            11,TextAnchor.MiddleLeft,Muted);
        SetTypeface(desc,false,11);desc.color=Muted;
        Anchor(desc.rectTransform,0,0,1,1,new Vector2(50,6),new Vector2(-44,-30));

        RectTransform plate=Rect("ReferenceRowEmblem",button.transform,
            new Vector2(0,.5f),new Vector2(0,.5f),
            new Vector2(8,-17),new Vector2(42,17));
        SkinImage(BistroBuilderMenuEditorUiFactory.AddImage(plate,Color.white),
            rule?input:emblem);
        // Behind label and the small icon already provided by the row builder.
        plate.SetAsFirstSibling();
        Transform oldIcon=button.transform.Find("EntryIcon");
        if(oldIcon!=null)
        {
            Image symbol=oldIcon.GetComponent<Image>();
            Sprite sprite=ReferenceIcon(rule?"clock":baseMenu?"star":"book");
            if(symbol!=null && sprite!=null)
            {
                symbol.sprite=sprite;symbol.color=Color.white;
                symbol.type=Image.Type.Simple;symbol.preserveAspect=true;
                symbol.raycastTarget=false;
            }
            Anchor(oldIcon.GetComponent<RectTransform>(),0,.5f,0,.5f,
                new Vector2(14,-11),new Vector2(36,11));
        }
        RectTransform status=Rect("ReferenceRowStatus",button.transform,
            new Vector2(1,.5f),new Vector2(1,.5f),
            new Vector2(-43,-5),new Vector2(-33,5));
        Image dot=BistroBuilderMenuEditorUiFactory.AddImage(status,Color.white);
        // Active/inactive state comes from the real snapshot, never inferred from text.
        dot.sprite=active?dotGreen:dotGrey;
        dot.raycastTarget=false;
        Text arrow=BistroBuilderMenuEditorUiFactory.CreateText("ReferenceRowChevron",
            button.transform,"›",17,TextAnchor.MiddleCenter,Ink);
        Anchor(arrow.rectTransform,1,.5f,1,.5f,new Vector2(-25,-15),
            new Vector2(-8,15));
    }
}
