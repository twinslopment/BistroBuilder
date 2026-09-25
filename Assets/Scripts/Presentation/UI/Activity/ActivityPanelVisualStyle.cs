using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ActivityPanelVisualStyle
{
    public static readonly Color32 Panel = new Color32(247, 234, 212, 250);
    public static readonly Color32 PanelLight = new Color32(255, 248, 237, 255);
    public static readonly Color32 PanelSoft = new Color32(242, 226, 201, 255);
    public static readonly Color32 Gold = new Color32(198, 139, 44, 255);
    public static readonly Color32 GoldLight = new Color32(240, 184, 76, 255);
    public static readonly Color32 GoldDark = new Color32(117, 71, 19, 255);
    public static readonly Color32 Ink = new Color32(42, 22, 12, 255);
    public static readonly Color32 Brown = new Color32(91, 59, 34, 255);
    public static readonly Color32 Muted = new Color32(120, 105, 91, 255);
    public static readonly Color32 Line = new Color32(195, 161, 117, 255);

    private static Sprite roundedSprite;

    public static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite == null)
                roundedSprite = CreateRoundedSprite();
            return roundedSprite;
        }
    }
    public static void ApplyRounded(Image image, Color color)
    {
        if (image == null)
            return;

        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    public static void ApplyPanelChrome(RectTransform panel, TMP_Text heading)
    {
        if (panel == null)
            return;

        Image background = panel.GetComponent<Image>();
        if (background == null)
            background = panel.gameObject.AddComponent<Image>();
        ApplyRounded(background, Panel);
        background.raycastTarget = true;

        Outline outer = panel.GetComponent<Outline>();
        if (outer == null)
            outer = panel.gameObject.AddComponent<Outline>();
        outer.effectColor = GoldDark;
        outer.effectDistance = new Vector2(2f, -2f);
        outer.useGraphicAlpha = true;

        EnsureInnerFrame(panel);
        EnsureHeadingPlaque(panel, heading);
    }

    public static void ApplyHeading(TMP_Text heading)
    {
        if (heading == null)
            return;

        heading.text = "ACTIVIDAD";
        heading.fontSize = 25f;
        heading.color = Ink;
        heading.alignment = TextAlignmentOptions.Center;
        heading.fontStyle = FontStyles.Normal;
        BistroBuilderTypography.Apply(heading, BistroBuilderUiStyleRole.Heading, true);
    }
    public static void ApplyFilterButton(Button button, bool selected)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
            button.targetGraphic = image;
        }

        Color normal = selected ? GoldLight : PanelLight;
        Color hover = Color.Lerp(normal, Color.white, 0.035f);
        Color pressed = Color.Lerp(normal, GoldDark, 0.08f);

        ApplyRounded(image, normal);
        button.transition = Selectable.Transition.ColorTint;
        button.colors = BistroBuilderUiTokens.ButtonColors(normal, hover, pressed);

        TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.color = Ink;
            label.fontSize = 10.6f;
            label.fontStyle = FontStyles.Normal;
            BistroBuilderTypography.Apply(label, BistroBuilderUiStyleRole.Label, true);
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = selected ? GoldDark : Line;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    public static void ApplyRowBackground(Image background)
    {
        if (background == null)
            return;
        ApplyRounded(background, PanelLight);
    }
    public static void GetSeverityStyle(
        ActivitySeverity severity,
        out Color background,
        out Color foreground,
        out string label)
    {
        switch (severity)
        {
            case ActivitySeverity.Critical:
                background = new Color32(247, 222, 215, 255);
                foreground = new Color32(168, 61, 50, 255);
                label = "CRÍTICO";
                break;
            case ActivitySeverity.Attention:
                background = new Color32(244, 223, 189, 255);
                foreground = new Color32(185, 104, 29, 255);
                label = "ATENCIÓN";
                break;
            case ActivitySeverity.Positive:
                background = new Color32(223, 234, 216, 255);
                foreground = new Color32(77, 125, 71, 255);
                label = "POSITIVO";
                break;
            case ActivitySeverity.Opportunity:
                background = new Color32(231, 222, 240, 255);
                foreground = new Color32(110, 85, 144, 255);
                label = "OPORT.";
                break;
            case ActivitySeverity.Reservation:
                background = new Color32(223, 232, 239, 255);
                foreground = new Color32(55, 107, 149, 255);
                label = "RESERVA";
                break;
            default:
                background = new Color32(234, 223, 206, 255);
                foreground = new Color32(102, 81, 63, 255);
                label = "INFO";
                break;
        }
    }
    private static void EnsureInnerFrame(RectTransform panel)
    {
        RectTransform frame = panel.Find("BB_ActivityInnerFrame") as RectTransform;
        if (frame == null)
        {
            var go = new GameObject("BB_ActivityInnerFrame", typeof(RectTransform), typeof(Image));
            frame = go.GetComponent<RectTransform>();
            frame.SetParent(panel, false);
            frame.SetAsFirstSibling();
        }

        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(7f, 7f);
        frame.offsetMax = new Vector2(-7f, -7f);

        Image image = frame.GetComponent<Image>();
        ApplyRounded(image, new Color(1f, 1f, 1f, 0.01f));
        image.raycastTarget = false;

        Outline outline = frame.GetComponent<Outline>();
        if (outline == null)
            outline = frame.gameObject.AddComponent<Outline>();
        Color gold = Gold;
        gold.a = 0.62f;
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
    }

    private static void EnsureHeadingPlaque(RectTransform panel, TMP_Text heading)
    {
        RectTransform plaque = panel.Find("BB_ActivityHeadingPlaque") as RectTransform;
        if (plaque == null)
        {
            var go = new GameObject("BB_ActivityHeadingPlaque", typeof(RectTransform), typeof(Image));
            plaque = go.GetComponent<RectTransform>();
            plaque.SetParent(panel, false);
            plaque.SetSiblingIndex(1);
        }
        plaque.anchorMin = plaque.anchorMax = new Vector2(0.5f, 1f);
        plaque.pivot = new Vector2(0.5f, 1f);
        plaque.anchoredPosition = new Vector2(0f, -9f);
        plaque.sizeDelta = new Vector2(252f, 50f);

        Image image = plaque.GetComponent<Image>();
        ApplyRounded(image, PanelLight);
        image.raycastTarget = false;

        Outline outline = plaque.GetComponent<Outline>();
        if (outline == null)
            outline = plaque.gameObject.AddComponent<Outline>();
        outline.effectColor = GoldDark;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        if (heading != null)
        {
            ApplyHeading(heading);
            RectTransform rect = heading.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -15f);
            rect.sizeDelta = new Vector2(230f, 38f);
            heading.transform.SetAsLastSibling();
        }
    }

    private static Sprite CreateRoundedSprite()
    {
        const int size = 64;
        const float radius = 14f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "BB_ActivityRoundedRuntime",
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs((x + 0.5f) - half) - inner;
                float py = Mathf.Abs((y + 0.5f) - half) - inner;
                float ox = Mathf.Max(px, 0f);
                float oy = Mathf.Max(py, 0f);
                float outside = Mathf.Sqrt(ox * ox + oy * oy) - radius;
                float alpha = Mathf.Clamp01(0.5f - outside);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(18f, 18f, 18f, 18f));
        sprite.name = "BB_ActivityRoundedRuntimeSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
