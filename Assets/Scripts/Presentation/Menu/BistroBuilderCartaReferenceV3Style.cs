using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        editorSwitchOn, editorSwitchOff, editorSwitchKnob;
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
        editorSwitchOn = Surface(new Color32(220, 161, 45, 255),
            new Color32(204, 139, 27, 255),
            new Color32(173, 115, 22, 255), 12, 1);
        editorSwitchOff = Surface(new Color32(222, 211, 195, 255),
            new Color32(219, 207, 190, 255),
            new Color32(199, 181, 159, 255), 12, 1);
        editorSwitchKnob = Dot(new Color32(255, 255, 255, 255),
            new Color32(245, 239, 231, 255));
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
        Image background = button.GetComponent<Image>();
        SkinImage(background, destructive ? editorDanger :
            plain ? editorField : editorButton);
        if (background != null) background.raycastTarget = true;
        RemoveOldBevel(button.transform);
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
                    new Vector2(13, 8), new Vector2(51, -8));
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
            if (field.textComponent != null) SetTypeface(field.textComponent, false, 13);
            if (field.placeholder is Text hint)
            {
                SetTypeface(hint, false, 12);
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

        foreach (Text text in panel.GetComponentsInChildren<Text>(true))
        {
            if (text == null) continue;
            switch (text.name)
            {
                case "DetailHeading":
                case "DishTitle":
                    SetTypeface(text, true, text.name == "DishTitle" ? 18 : 17, true);
                    break;
                case "DishSubtitle":
                case "EditingCaption":
                    SetTypeface(text, false, text.name == "DishSubtitle" ? 10 : 9);
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
                    SetTypeface(text, false, 10);
                    break;
                default:
                    if (text.name.EndsWith("Caption", StringComparison.Ordinal))
                    {
                        SetTypeface(text, false, 10);
                        text.color = Muted;
                    }
                    break;
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
            SetTypeface(label, true, 12, true);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(26f, 2f);
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
                    new Vector2(10, -9), new Vector2(28, 9));
                if (icon != null) icon.color = Color.white;
            }
            else if (button.name == "CartaTabDishes")
            {
                Text icon = BistroBuilderMenuEditorUiFactory.CreateText(
                    "ReferenceTabIcon", button.transform, "⌒", 16,
                    TextAnchor.MiddleCenter, Brass, FontStyle.Bold);
                Anchor(icon.rectTransform, 0, 0, 0, 1,
                    new Vector2(8, 0), new Vector2(30, 0));
                SetTypeface(icon, false, 16, true);
                icon.color = Brass;
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
        string category = (label ?? string.Empty).ToLowerInvariant();
        if (category.Contains("todo")) return CategoryGrid();
        if (category.Contains("especial")) return ReferenceIcon("star");
        if (categoryIcons == null) categoryIcons = BBIconCatalog.LoadDefault();
        if (categoryIcons == null) return null;
        BBIconId id = category.Contains("entr") ? BBIconId.FoodStarter :
            category.Contains("principal") ? BBIconId.FoodMain :
            category.Contains("postre") ? BBIconId.FoodDessert :
            category.Contains("bebida") ? BBIconId.FoodDrink :
            BBIconId.ObjectDish;
        return categoryIcons.GetSprite(id);
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
        SetTypeface(label, true, 14, selected);
        label.alignment = TextAnchor.MiddleLeft;
        label.rectTransform.offsetMin = new Vector2(43f, 1f);
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
                new Vector2(10f, -12f), new Vector2(35f, 12f));
            Image categoryIcon = BistroBuilderMenuEditorUiFactory.AddImage(
                iconRect, Brass);
            categoryIcon.sprite = sprite;
            categoryIcon.type = Image.Type.Simple;
            categoryIcon.preserveAspect = true;
            categoryIcon.raycastTarget = false;
        }
        else
        {
            Image categoryIcon = oldIcon.GetComponent<Image>();
            if (categoryIcon != null) categoryIcon.sprite = sprite;
        }
    }

    public static void StyleEditorInclusionButton(Button button, bool destructive)
    {
        if (button == null) return;
        EnsureEditorSurfaces();
        SkinImage(button.GetComponent<Image>(),
            destructive ? editorDanger : editorButton);
        RemoveOldBevel(button.transform);
        Image background = button.GetComponent<Image>();
        if (background != null) background.raycastTarget = true;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            SetTypeface(label, true, 12, true);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = destructive ? Color.white : Ink;
        }
    }

    public static void StyleAvailabilitySwitch(
        Image track, Image knob, bool available)
    {
        EnsureEditorSurfaces();
        if (track != null)
        {
            SkinImage(track, available ? editorSwitchOn : editorSwitchOff);
            track.raycastTarget = false; // row click still selects its dish
        }
        if (knob != null)
        {
            knob.sprite = editorSwitchKnob;
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
                    SetTypeface(text, true, 15, true);
                    break;
                case "Subtitle":
                    SetTypeface(text, false, 11);
                    text.color = Muted;
                    break;
                case "Price":
                    SetTypeface(text, false, 13, true);
                    break;
                case "Available":
                    SetTypeface(text, false, 15, true);
                    break;
                case "Margin":
                    SetTypeface(text, false, 12, true);
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
