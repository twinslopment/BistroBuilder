using BistroBuilder.UI.Iconography;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Fábrica visual privada de 2.1E. Centraliza tipografía, colores y creación
/// de controles para impedir que la vista duplique estilos o configuración
/// de navegación.
/// </summary>
internal static class BistroBuilderMenuEditorUiFactory
{
    // CARTA V1: one ivory / brass palette for Portfolio, Editor, Authoring,
    // their lists and popovers. Same visual language as approved Personal V8.
    public static readonly Color Overlay = new Color32(19, 16, 13, 222);
    public static readonly Color Surface = new Color32(247, 230, 206, 255);
    public static readonly Color SurfaceRaised = new Color32(255, 242, 222, 255);
    public static readonly Color SurfaceSelected = new Color32(239, 202, 149, 255);
    public static readonly Color Border = new Color32(184, 142, 90, 255);
    public static readonly Color Accent = new Color32(171, 111, 47, 255);
    public static readonly Color Positive = new Color32(210, 159, 95, 255);
    public static readonly Color Warning = new Color32(162, 103, 38, 255);
    public static readonly Color Negative = new Color32(150, 42, 31, 255);
    public static readonly Color TextPrimary = new Color32(56, 36, 21, 255);
    public static readonly Color TextSecondary = new Color32(111, 80, 50, 255);

    private static BBIconCatalog icons;
    private static Font cachedFont;
    private static Font displayFont;
    private static Font emphasisFont;
    private static readonly Dictionary<string, Sprite> themedHeaderSprites =
        new Dictionary<string, Sprite>();

    public static Font Font
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.Load<Font>(
                    "BistroBuilder/UI/Typography/Inter-Regular")
                    ?? BistroBuilderTypography.LegacyBody;
            return cachedFont;
        }
    }

    // These are the project's own imported fonts. The installed Recoleta.otf
    // is a DEMO, so only ASCII-only display strings may safely use it; for
    // any accented heading the official Inter SemiBold is the fallback.
    private static Font HeadingFont(string value)
    {
        if (emphasisFont == null)
            emphasisFont = Resources.Load<Font>(
                "BistroBuilder/UI/Typography/Inter-SemiBold") ?? Font;
        bool cleanAscii = true;
        if (!string.IsNullOrEmpty(value))
            foreach (char glyph in value)
                if (glyph < 32 || glyph > 126) { cleanAscii = false; break; }
        if (!cleanAscii) return emphasisFont;
        if (displayFont == null)
            displayFont = Resources.Load<Font>(
                "BistroBuilder/UI/Typography/Recoleta");
        return displayFont != null ? displayFont : emphasisFont;
    }

    public static void SetButtonDisplay(Text text, string value)
    {
        if (text == null) return;
        text.text = value ?? string.Empty;
        Font face = HeadingFont(text.text);
        if (face != null) text.font = face;
        text.fontStyle = face == displayFont ? FontStyle.Bold : FontStyle.Normal;
    }

    private static bool DisplayLabel(string name) => name == "Title" ||
        name == "Subheading" || name == "Heading" || name == "SectionTitle";

    public static void StylePlate(RectTransform rect)
    {
        if (rect == null) return;
        Image panel = rect.GetComponent<Image>();
        if (panel == null) panel = AddImage(rect, Surface);
        panel.color = Surface;
        Outline line = rect.GetComponent<Outline>();
        if (line == null) line = rect.gameObject.AddComponent<Outline>();
        line.effectColor = Border;
        line.effectDistance = new Vector2(1.7f, -1.7f);
        line.useGraphicAlpha = false;
        Shadow shadow = rect.GetComponent<Shadow>();
        if (shadow == null) shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(65, 39, 19, 70);
        shadow.effectDistance = new Vector2(0, -2);
        shadow.useGraphicAlpha = true;
    }

    public static RectTransform AddIcon(string name, Transform parent,
        BBIconId id, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        if (icons == null) icons = BBIconCatalog.LoadDefault();
        Sprite sprite = icons != null ? icons.GetSprite(id) : null;
        if (sprite == null) return null;
        RectTransform rect = CreateRect(name, parent, min, max,
            offsetMin, offsetMax);
        Image image = AddImage(rect, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    // V4: four independent HEADER sprites. The list-row icons deliberately
    // remain BBIconCatalog sprites and are never replaced by these assets.
    private static Sprite ThemedHeaderSprite(string key)
    {
        if (themedHeaderSprites.TryGetValue(key, out Sprite cached))
            return cached;
        string path = "BistroBuilder/UI/MenuHeaderIcons/" + key;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            // Works with PNG assets imported as either Sprite or Texture2D;
            // avoids a global importer or changes to unrelated .meta files.
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = "BB Carta " + key;
            }
        }
        if (sprite != null) themedHeaderSprites[key] = sprite;
        return sprite;
    }

    private static RectTransform AddThemedHeaderIcon(string name,
        Transform parent, string key, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        Sprite sprite = ThemedHeaderSprite(key);
        if (sprite == null) return null;
        RectTransform root = CreateRect(name, parent, min, max,
            offsetMin, offsetMax);
        Image image = AddImage(root, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return root;
    }

    public static void AddMenuHeaderIcon(Transform parent)
    {
        if (AddThemedHeaderIcon("CartaHeaderIcon", parent, "carta_main",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(9f, 3f), new Vector2(53f, -3f)) == null)
            AddIcon("CartaHeaderIcon", parent, BBIconId.NavMenu,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(14f, 9f), new Vector2(45f, -9f));
    }

    public static void AddPortfolioSectionIcon(Transform parent,
        string title, float minY, float maxY)
    {
        string key = title == "Mis cartas" ? "mis_cartas" :
            title == "Reglas de activación" ? "reglas_activacion" :
            "detalle_regla";
        if (AddThemedHeaderIcon("SectionIcon", parent, key,
            new Vector2(.022f, minY), new Vector2(.127f, maxY),
            new Vector2(0f, 1f), new Vector2(0f, -1f)) != null) return;
        BBIconId fallback = title == "Mis cartas" ? BBIconId.NavMenu :
            title == "Reglas de activación" ? BBIconId.ActionSave :
            BBIconId.ActionEdit;
        AddIcon("SectionIcon", parent, fallback,
            new Vector2(.037f, minY), new Vector2(.113f, maxY),
            new Vector2(0f, 5f), new Vector2(0f, -5f));
    }

    public static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform)
        );
        gameObject.layer = parent != null
            ? parent.gameObject.layer
            : 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    public static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    public static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment,
        Color color,
        FontStyle style = FontStyle.Normal
    )
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Text text = rect.gameObject.AddComponent<Text>();
        // Legacy uGUI Text is retained so no service/UI bindings are replaced.
        // The official Recoleta is used for clean ASCII display labels only;
        // Inter Regular/SemiBold handles text with Spanish diacritics and euro.
        bool isTitle = DisplayLabel(name);
        text.font = isTitle ? HeadingFont(value) :
            style == FontStyle.Bold ?
            (emphasisFont ?? (emphasisFont = Resources.Load<Font>(
                "BistroBuilder/UI/Typography/Inter-SemiBold") ?? Font)) : Font;
        text.text = value ?? string.Empty;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = isTitle && text.font == displayFont
            ? FontStyle.Bold : FontStyle.Normal;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        UnityAction callback,
        Color normalColor,
        int fontSize = 15
    )
    {
        RectTransform rect = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Image image = AddImage(rect, normalColor);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        // ColorTint multiplies image.color: tint ONCE, never twice (the prior
        // dark menu accidentally used normalColor both places).
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 251, 237, 255);
        colors.pressedColor = new Color32(214, 186, 148, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Automatic
        };

        Text text = CreateText(
            "Label",
            rect,
            label,
            fontSize,
            TextAnchor.MiddleCenter,
            TextPrimary,
            FontStyle.Bold
        );
        Font buttonFace = HeadingFont(label);
        if (buttonFace != null) text.font = buttonFace;
        text.fontStyle = buttonFace == displayFont ? FontStyle.Bold : FontStyle.Normal;
        Outline trim = rect.gameObject.AddComponent<Outline>();
        trim.effectColor = Border;
        trim.effectDistance = new Vector2(1f, -1f);
        trim.useGraphicAlpha = false;
        text.rectTransform.offsetMin = new Vector2(8f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);

        if (callback != null)
        {
            button.onClick.AddListener(callback);
        }

        return button;
    }

    public static Toggle CreateToggle(
        string name,
        Transform parent,
        string label,
        UnityAction<bool> callback
    )
    {
        RectTransform root = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        // The checked/unchecked background has its own deterministic palette;
        // disable Selectable's automatic tint so it cannot shift the tick box.
        toggle.transition = Selectable.Transition.None;
        toggle.navigation = new Navigation
        {
            mode = Navigation.Mode.Automatic
        };

        RectTransform box = CreateRect(
            "Box",
            root,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, -10f),
            new Vector2(20f, 10f)
        );
        Image boxImage = AddImage(box, SurfaceRaised);
        toggle.targetGraphic = boxImage;

        // Use the project's own Inter check glyph, geometrically centred in
        // the 20x20 box. The previous filled inner square did not show a tick,
        // and the HTML prototype's text-baseline check was visibly displaced.
        RectTransform check = CreateRect(
            "Check",
            box,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Text checkGlyph = check.gameObject.AddComponent<Text>();
        checkGlyph.font = Font; // Inter Regular includes U+2713.
        checkGlyph.text = "\u2713";
        checkGlyph.fontSize = 15;
        checkGlyph.fontStyle = FontStyle.Bold;
        checkGlyph.alignment = TextAnchor.MiddleCenter;
        checkGlyph.color = Color.white;
        checkGlyph.raycastTarget = false;
        checkGlyph.supportRichText = false;
        checkGlyph.horizontalOverflow = HorizontalWrapMode.Overflow;
        checkGlyph.verticalOverflow = VerticalWrapMode.Overflow;
        toggle.graphic = checkGlyph;
        boxImage.color = toggle.isOn ? Accent : SurfaceRaised;
        toggle.onValueChanged.AddListener(value =>
            boxImage.color = value ? Accent : SurfaceRaised);

        Text text = CreateText(
            "Label",
            root,
            label,
            14,
            TextAnchor.MiddleLeft,
            TextPrimary
        );
        text.rectTransform.offsetMin = new Vector2(30f, 0f);
        text.rectTransform.offsetMax = Vector2.zero;

        if (callback != null)
        {
            toggle.onValueChanged.AddListener(callback);
        }

        return toggle;
    }

    public static InputField CreateInputField(
        string name,
        Transform parent,
        string placeholder,
        UnityAction<string> onValueChanged,
        UnityAction<string> onEndEdit
    )
    {
        RectTransform root = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Image image = AddImage(root, SurfaceRaised);
        InputField input = root.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.characterValidation = InputField.CharacterValidation.None;
        input.navigation = new Navigation
        {
            mode = Navigation.Mode.Automatic
        };

        Text valueText = CreateText(
            "Text",
            root,
            string.Empty,
            15,
            TextAnchor.MiddleLeft,
            TextPrimary
        );
        valueText.supportRichText = false;
        valueText.rectTransform.offsetMin = new Vector2(10f, 4f);
        valueText.rectTransform.offsetMax = new Vector2(-10f, -4f);
        input.textComponent = valueText;

        Text placeholderText = CreateText(
            "Placeholder",
            root,
            placeholder,
            15,
            TextAnchor.MiddleLeft,
            new Color(
                TextSecondary.r,
                TextSecondary.g,
                TextSecondary.b,
                0.7f
            ),
            FontStyle.Italic
        );
        placeholderText.rectTransform.offsetMin = new Vector2(10f, 4f);
        placeholderText.rectTransform.offsetMax = new Vector2(-10f, -4f);
        input.placeholder = placeholderText;

        if (onValueChanged != null)
        {
            input.onValueChanged.AddListener(onValueChanged);
        }

        if (onEndEdit != null)
        {
            input.onEndEdit.AddListener(onEndEdit);
        }

        return input;
    }

    public static ScrollRect CreateScrollView(
        string name,
        Transform parent,
        out RectTransform content
    )
    {
        RectTransform root = CreateRect(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        AddImage(root, SurfaceRaised);
        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        RectTransform viewport = CreateRect(
            "Viewport",
            root,
            Vector2.zero,
            Vector2.one,
            new Vector2(4f, 4f),
            new Vector2(-4f, -4f)
        );
        Image viewportImage = AddImage(viewport, Color.clear);
        viewportImage.raycastTarget = true;

        // RectMask2D recorta por el rectángulo del viewport y no depende de
        // la transparencia del Graphic. Un Mask clásico con Image totalmente
        // transparente puede ocultar todo el contenido en determinadas
        // versiones/configuraciones de uGUI.
        RectMask2D rectMask = viewport.gameObject.AddComponent<RectMask2D>();
        rectMask.padding = Vector4.zero;

        content = CreateRect(
            "Content",
            viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero
        );
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    public static void SetLayoutHeight(Component component, float height)
    {
        LayoutElement layout = component.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout = component.gameObject.AddComponent<LayoutElement>();
        }

        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    public static void SetLayoutWidth(Component component, float width)
    {
        LayoutElement layout = component.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout = component.gameObject.AddComponent<LayoutElement>();
        }

        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
    }
}
