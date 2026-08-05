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
    public static readonly Color Overlay = new Color(0.02f, 0.025f, 0.023f, 0.82f);
    public static readonly Color Surface = new Color(0.085f, 0.095f, 0.09f, 0.985f);
    public static readonly Color SurfaceRaised = new Color(0.12f, 0.135f, 0.125f, 1f);
    public static readonly Color SurfaceSelected = new Color(0.20f, 0.28f, 0.23f, 1f);
    public static readonly Color Border = new Color(0.27f, 0.30f, 0.28f, 1f);
    public static readonly Color Accent = new Color(0.74f, 0.58f, 0.25f, 1f);
    public static readonly Color Positive = new Color(0.29f, 0.57f, 0.39f, 1f);
    public static readonly Color Warning = new Color(0.82f, 0.56f, 0.17f, 1f);
    public static readonly Color Negative = new Color(0.72f, 0.25f, 0.23f, 1f);
    public static readonly Color TextPrimary = new Color(0.94f, 0.94f, 0.90f, 1f);
    public static readonly Color TextSecondary = new Color(0.69f, 0.72f, 0.69f, 1f);

    private static Font cachedFont;

    public static Font Font
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf"
                );
            }

            return cachedFont;
        }
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
        text.font = Font;
        text.text = value ?? string.Empty;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
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
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            normalColor.r,
            normalColor.g,
            normalColor.b,
            0.35f
        );
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

        RectTransform check = CreateRect(
            "Check",
            box,
            Vector2.zero,
            Vector2.one,
            new Vector2(4f, 4f),
            new Vector2(-4f, -4f)
        );
        Image checkImage = AddImage(check, Accent);
        checkImage.raycastTarget = false;
        toggle.graphic = checkImage;

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
        AddImage(root, new Color(0.04f, 0.045f, 0.042f, 0.45f));
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
