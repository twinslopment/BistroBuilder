using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private static readonly Color32 Brass = new Color32(155, 106, 58, 255);
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static Font recoleta, inter, interBold;
    private static Sprite outer, card, normalButton, selectedRow, plainRow, input, danger, emblem, dotGreen, dotGrey, dotBrass;

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
        outer = Surface(new Color32(249,235,215,255),new Color32(244,219,190,255),
            new Color32(119,80,42,255),14,3);
        card = Surface(new Color32(255,245,230,255),new Color32(245,223,195,255),
            new Color32(171,123,71,255),11,2);
        normalButton = Surface(new Color32(249,233,208,255),new Color32(215,173,119,255),
            new Color32(150,106,62,255),7,2);
        selectedRow = Surface(new Color32(246,221,185,255),new Color32(237,196,142,255),
            new Color32(193,139,76,255),8,2);
        plainRow = Surface(new Color32(250,238,219,255),new Color32(250,238,219,255),
            new Color32(222,194,163,255),3,1);
        input = Surface(new Color32(252,241,223,255),new Color32(250,236,216,255),
            new Color32(196,156,113,255),6,1);
        danger = Surface(new Color32(189,80,59,255),new Color32(151,39,24,255),
            new Color32(113,31,19,255),6,2);
        emblem = Surface(new Color32(241,213,169,255),new Color32(214,169,115,255),
            new Color32(165,117,72,255),7,1);
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
            default:return null;
        }
    }

    private static void SkinButton(Button button)
    {
        bool destructive=button.name=="Eliminar" || button.name=="Eliminarregla" ||
            button.name=="ConfirmDeletion" || button.name=="DiscardAndClose";
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

    public static void ApplyEditor(RectTransform panel)
    {
        if (panel == null) return;
        EnsureSurfaces();
        SkinImage(panel.GetComponent<Image>(), outer);

        foreach (string path in new[] { "Body/Sidebar", "Body/Detail", "Footer" })
        {
            Transform node = panel.Find(path);
            if (node != null) SkinImage(node.GetComponent<Image>(), card);
        }

        Transform listHeader = panel.Find("Body/List/ListHeader");
        if (listHeader != null)
            SkinImage(listHeader.GetComponent<Image>(), input);

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
                title.text = "CARTA Y PLATOS";
                SetTypeface(title, true, 30, true);
                Anchor(title.rectTransform, .067f, 0, .31f, 1,
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
                    new Vector2(14, 13), new Vector2(42, -13));
            }

            Transform close = header.Find("Close");
            if (close != null) close.gameObject.SetActive(false);
        }

        foreach (InputField field in panel.GetComponentsInChildren<InputField>(true))
        {
            SkinImage(field.GetComponent<Image>(), input);
            if (field.textComponent != null) SetTypeface(field.textComponent, false, 13);
            if (field.placeholder is Text hint)
            {
                SetTypeface(hint, false, 12);
                hint.color = new Color32(139, 108, 78, 255);
            }
        }

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.name.StartsWith("Category_", StringComparison.Ordinal) ||
                button.name.StartsWith("Filter_", StringComparison.Ordinal) ||
                button.name.StartsWith("CartaTab", StringComparison.Ordinal) ||
                button.name == "Close")
                continue;
            SkinButton(button);
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
        EnsureSurfaces();
        Image background = button.GetComponent<Image>();
        SkinImage(background, selected ? selectedRow : input);
        if (background != null) background.raycastTarget = true;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.03f, 1.03f, 1.03f, 1f);
        colors.pressedColor = new Color(.91f, .89f, .87f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            SetTypeface(label, true, 13, true);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            MixedTypefaceRuns(label);
        }
    }

    public static void StyleSelectorButton(Button button, bool selected)
    {
        StyleNavigationTab(button, selected);
        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null)
        {
            SetTypeface(label, true, 13, selected);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
        }
    }

    public static void StyleEditorInclusionButton(Button button, bool destructive)
    {
        if (button == null) return;
        EnsureSurfaces();
        SkinImage(button.GetComponent<Image>(), destructive ? danger : normalButton);
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
