using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fine-grained 9-slice-independent ornamentation for the approved Carta V3.
/// Procedurally drawn from geometry, not a screenshot of the design. Every
/// ornament is click-through and constrained to the existing Unity layout.
/// </summary>
internal static class BistroBuilderCartaV3Ornamentation
{
    private static Sprite cornerSeal;
    private static Texture2D headerLace;
    private static readonly Color32 DeepGold = new Color32(114, 68, 25, 255);
    private static readonly Color32 Gold = new Color32(183, 131, 64, 255);
    private static readonly Color32 Bright = new Color32(255, 229, 169, 255);

    public static void Decorate(RectTransform panel)
    {
        if (panel == null || panel.Find("V3LuxuryRimTop") != null) return;
        DecorateParchmentHeader(panel);
        // Layered edge catches approximate the reference's metal-inlaid ivory
        // surround without stealing real estate from the editing form.
        Stroke(panel, "V3LuxuryRimTop", 0f, 1f, 1f, 1f,
            4f, -3f, -4f, -1f, DeepGold);
        Stroke(panel, "V3LuxuryRimGoldTop", 0f, 1f, 1f, 1f,
            8f, -7f, -8f, -6f, Gold);
        Stroke(panel, "V3LuxuryRimHighlightTop", 0f, 1f, 1f, 1f,
            9f, -9f, -9f, -8f, Bright);
        Stroke(panel, "V3LuxuryRimBottom", 0f, 0f, 1f, 0f,
            4f, 1f, -4f, 3f, DeepGold);
        Stroke(panel, "V3LuxuryRimGoldBottom", 0f, 0f, 1f, 0f,
            8f, 6f, -8f, 7f, Gold);
        Stroke(panel, "V3LuxuryRimHighlightBottom", 0f, 0f, 1f, 0f,
            9f, 8f, -9f, 9f, Bright);
        Stroke(panel, "V3LuxuryRimLeft", 0f, 0f, 0f, 1f,
            1f, 7f, 3f, -7f, DeepGold);
        Stroke(panel, "V3LuxuryRimGoldLeft", 0f, 0f, 0f, 1f,
            6f, 8f, 7f, -8f, Gold);
        Stroke(panel, "V3LuxuryRimRight", 1f, 0f, 1f, 1f,
            -3f, 7f, -1f, -7f, DeepGold);
        Stroke(panel, "V3LuxuryRimGoldRight", 1f, 0f, 1f, 1f,
            -7f, 8f, -6f, -8f, Gold);

        Seal(panel, "V3LuxuryCornerBL", 0f, 0f, 3f, 3f);
        Seal(panel, "V3LuxuryCornerBR", 1f, 0f, -3f, 3f);
        Seal(panel, "V3LuxuryCornerTL", 0f, 1f, 3f, -3f);
        Seal(panel, "V3LuxuryCornerTR", 1f, 1f, -3f, -3f);

        foreach (string action in new[] {
            "AddDish", "RemoveDish", "DuplicateDish", "Apply", "Discard"
        })
        {
            Transform button = panel.Find("Footer/" + action);
            if (button == null) continue;
            Stroke(button, "V3ButtonGlint", 0f, 1f, 1f, 1f,
                4f, -4f, -4f, -2f, new Color32(255, 248, 224, 182));
            Stroke(button, "V3ButtonInsetBottom", 0f, 0f, 1f, 0f,
                4f, 3f, -4f, 5f, new Color32(90, 45, 13, 105));
            Stroke(button, "V3ButtonInsetLeft", 0f, 0f, 0f, 1f,
                3f, 5f, 4f, -5f, new Color32(255, 229, 182, 116));
        }

        foreach (string panePath in new[] { "Body/Sidebar", "Body/List", "Body/Detail" })
        {
            Transform pane = panel.Find(panePath);
            if (pane == null) continue;
            Stroke(pane, "V3PaneHighlightTop", 0f, 1f, 1f, 1f,
                9f, -9f, -9f, -8f, new Color32(255, 252, 246, 228));
            Stroke(pane, "V3PaneInsetBottom", 0f, 0f, 1f, 0f,
                9f, 7f, -9f, 8f, new Color32(166, 118, 64, 120));
        }

        // The legacy uGUI × often disappears on the Windows build due to its
        // installed font's missing glyph. This real vector-shaped X is stable.
        Transform close = panel.Find("Header/Close");
        if (close != null && close.Find("V3CloseStrokeA") == null)
        {
            Text label = close.Find("Label")?.GetComponent<Text>();
            if (label != null) label.enabled = false;
            CrossStroke(close, "V3CloseStrokeA", 44f);
            CrossStroke(close, "V3CloseStrokeB", -44f);
        }

        // Detail values open the real extended recipe/category editor, and
        // their visible dropdown affordances should match the approved design.
        foreach (string field in new[] { "CategoryValuePlate", "ModeValuePlate" })
        {
            Transform control = panel.Find("Body/Detail/DetailCanvas/" + field);
            if (control == null || control.Find("V3DropdownChevron") != null) continue;
            RectTransform chevron = BistroBuilderMenuEditorUiFactory.CreateRect(
                "V3DropdownChevron", control, new Vector2(1f, .5f),
                new Vector2(1f, .5f), new Vector2(-27f, -10f),
                new Vector2(-10f, 10f));
            Image glyph = BistroBuilderMenuEditorUiFactory.AddImage(
                chevron, new Color32(89, 57, 27, 255));
            glyph.sprite = BistroBuilderCartaReferenceV3Style.ReferenceIcon("chevron");
            glyph.type = Image.Type.Simple;
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            chevron.localEulerAngles = new Vector3(0, 0, -90f);
            Button action = control.GetComponent<Button>();
            Image basePlate = control.GetComponent<Image>();
            if (action != null && basePlate != null)
            {
                basePlate.raycastTarget = true;
                action.targetGraphic = basePlate;
            }
        }
    }

    private static void DecorateParchmentHeader(RectTransform panel)
    {
        Transform header = panel.Find("Header");
        if (header == null || header.Find("V3ParchmentLace") != null) return;
        RectTransform rect = BistroBuilderMenuEditorUiFactory.CreateRect(
            "V3ParchmentLace", header, Vector2.zero, Vector2.one,
            new Vector2(0f, 0f), new Vector2(0f, 0f));
        RawImage lace = rect.gameObject.AddComponent<RawImage>();
        lace.texture = GetParchmentLace();
        lace.color = Color.white;
        lace.raycastTarget = false;
        lace.uvRect = new Rect(0f, 0f, 6f, 1f);
        rect.SetAsFirstSibling(); // Under every live header button and title.
    }

    private static Texture2D GetParchmentLace()
    {
        if (headerLace != null) return headerLace;
        const int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Carta V3 Procedural Parchment Watermark",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };
        for (int y = 0; y < h; ++y)
        for (int x = 0; x < w; ++x)
        {
            Vector2 pixel = new Vector2(x + .5f, y + .5f);
            float grain = Mathf.PerlinNoise((x + 37f) * .085f,
                (y + 73f) * .085f) - .42f;
            float pattern = 0f;
            for (int motif = 0; motif < 2; ++motif)
            {
                Vector2 center = motif == 0
                    ? new Vector2(52f, 61f) : new Vector2(183f, 81f);
                float radius = Vector2.Distance(pixel, center);
                float outer = Mathf.Abs(radius - 43f);
                float inner = Mathf.Abs(radius - 25f);
                pattern += .9f * Mathf.Clamp01(1.1f - outer) +
                    .4f * Mathf.Clamp01(1f - inner);
            }
            // Under 7% opacity: a subtle engraved relief, not a UI bitmap.
            float opacity = Mathf.Clamp01(grain * .085f + pattern * .045f);
            Color ink = new Color(.68f, .48f, .29f, opacity);
            tex.SetPixel(x, y, ink);
        }
        tex.Apply(false, true);
        headerLace = tex;
        return tex;
    }

    private static void CrossStroke(Transform parent, string name, float angle)
    {
        RectTransform line = BistroBuilderMenuEditorUiFactory.CreateRect(
            name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(-12f, -2f), new Vector2(12f, 2f));
        Image image = BistroBuilderMenuEditorUiFactory.AddImage(
            line, new Color32(64, 38, 17, 255));
        image.raycastTarget = false;
        line.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private static void Stroke(Transform parent, string name,
        float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top, Color color)
    {
        RectTransform rect = BistroBuilderMenuEditorUiFactory.CreateRect(
            name, parent, new Vector2(minX, minY), new Vector2(maxX, maxY),
            new Vector2(left, bottom), new Vector2(right, top));
        Image image = BistroBuilderMenuEditorUiFactory.AddImage(rect, color);
        image.raycastTarget = false;
    }

    private static void Seal(Transform parent, string name,
        float x, float y, float offsetX, float offsetY)
    {
        RectTransform rect = BistroBuilderMenuEditorUiFactory.CreateRect(
            name, parent, new Vector2(x, y), new Vector2(x, y),
            new Vector2(offsetX - 13f, offsetY - 13f),
            new Vector2(offsetX + 13f, offsetY + 13f));
        Image image = BistroBuilderMenuEditorUiFactory.AddImage(rect, Color.white);
        image.sprite = CornerSprite();
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static Sprite CornerSprite()
    {
        if (cornerSeal != null) return cornerSeal;
        const int side = 128;
        Texture2D texture = new Texture2D(side, side,
            TextureFormat.RGBA32, false, false);
        texture.name = "Carta V3 Raised Brass Corner Seal";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float dx = (x + .5f - side * .5f) / (side * .5f);
            float dy = (y + .5f - side * .5f) / (side * .5f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float diamond = Mathf.Abs(dx) + Mathf.Abs(dy);
            Color fill = Color.clear;
            float lighting = Mathf.Clamp01(.65f + (.36f * (dy - dx)));
            if (distance < .95f)
            {
                fill = Color.Lerp(new Color32(105, 59, 21, 255),
                    new Color32(237, 179, 95, 255), lighting);
                if (distance > .79f && distance < .91f)
                    fill = Color.Lerp(new Color32(96, 50, 17, 255),
                        new Color32(255, 227, 157, 255), lighting);
                if (distance < .72f)
                    fill = Color.Lerp(new Color32(166, 103, 38, 255),
                        new Color32(255, 220, 139, 255), lighting);
                if (diamond < .67f && diamond > .53f)
                    fill = new Color32(101, 57, 22, 255);
                if (diamond < .51f)
                    fill = Color.Lerp(new Color32(169, 107, 40, 255),
                        new Color32(250, 215, 135, 255), lighting);
                if (distance < .18f)
                    fill = new Color32(91, 49, 18, 255);
                // Rounded antialiased silhouette.
                fill.a *= Mathf.Clamp01((.96f - distance) * 90f);
            }
            texture.SetPixel(x, y, fill);
        }
        texture.Apply(false, true);
        cornerSeal = Sprite.Create(texture, new Rect(0, 0, side, side),
            new Vector2(.5f, .5f), 100f);
        return cornerSeal;
    }
}
