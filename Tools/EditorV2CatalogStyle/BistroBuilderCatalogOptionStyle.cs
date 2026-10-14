using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Original catalog vectors. Decorations never own selection or consume a click.</summary>
public static class BistroBuilderCatalogOptionStyle
{
    private static TMP_FontAsset font;
    private static readonly string[] Keys = {
        "tab-all", "tab-favorites", "tab-recent", "category-all", "category-tables",
        "category-seating", "category-bar", "category-kitchen", "category-storage",
        "category-lighting", "category-decoration", "category-exterior"
    };

    public static TMP_FontAsset Font
    {
        get
        {
            if (font != null) return font;
            var source = Resources.Load<Font>("BistroBuilder/UI/GalleryViva/CatalogOptionsR8");
            if (source == null) { Debug.LogError("Missing CatalogOptionsR8 vector font."); return null; }
            font = TMP_FontAsset.CreateFontAsset(source, 128, 12, GlyphRenderMode.SDFAA, 1024, 1024);
            if (font == null) return null;
            font.name = "BB catalog original vectors R8";
            font.boldStyle = 0; font.boldSpacing = 0;
            string characters = "";
            for (int i = 0; i < Keys.Length; i++) characters += (char)(0xe100 + i);
            if (!font.TryAddCharacters(characters)) Debug.LogError("Incomplete CatalogOptionsR8 glyph atlas.");
            return font;
        }
    }

    public static BistroBuilderCatalogOptionRelief Create(Transform parent, string key,
        float x, float y, float width, float height)
    {
        int index = Array.IndexOf(Keys, key);
        if (index < 0 || Font == null) return null;
        var rect = BistroBuilderGalleryViva.Node("Icon", parent, x, y, width, height);
        var relief = rect.gameObject.AddComponent<BistroBuilderCatalogOptionRelief>();
        relief.Configure(Font, ((char)(0xe100 + index)).ToString());
        // Events belong to the button, while each glyph layer remains transparent to raycasts.
        var relay = parent.gameObject.AddComponent<BistroBuilderCatalogOptionEventRelay>();
        relay.Configure(relief);
        return relief;
    }

    public static void SetSelected(Transform button, bool selected)
    {
        var icon = button.Find("Icon");
        if (icon != null) icon.GetComponent<BistroBuilderCatalogOptionRelief>()?.SetSelected(selected);
    }

    /// <summary>Give all labels 110 units and retain four genuine B9 columns.</summary>
    public static void ApplyContentGeometry(RectTransform expanded)
    {
        if (expanded.GetComponent<BistroBuilderCatalogGeometryApplied>() != null) return;
        expanded.gameObject.AddComponent<BistroBuilderCatalogGeometryApplied>();
        const float horizontalScale = 366f / 399f;
        string[] names = { "Featured", "FeaturedPrevious", "FeaturedNext", "RelatedHeading",
            "RelatedSpark", "SeeAll", "RelatedPrevious", "RelatedNext", "Separator", "AllSpark",
            "AllHeading", "Order", "ItemsScroll", "EmptyState", "Related_0", "Related_1",
            "Related_2", "Related_3" };
        foreach (string name in names)
        {
            var rect = expanded.Find(name) as RectTransform;
            if (rect == null) continue;
            var p = rect.anchoredPosition; p.x = 179 + (p.x - 146) * horizontalScale;
            rect.anchoredPosition = p;
            bool circular = name.EndsWith("Previous", StringComparison.Ordinal) || name.EndsWith("Next", StringComparison.Ordinal);
            if (circular) continue; // Arrows retain their round shape and click area.
            var size = rect.sizeDelta; size.x *= horizontalScale; rect.sizeDelta = size;
            if (name == "Featured" || name == "ItemsScroll" || name.StartsWith("Related_", StringComparison.Ordinal))
                ScaleChildren(rect, horizontalScale);
        }
        var items = expanded.Find("ItemsScroll/Viewport/Items");
        if (items != null)
        {
            var grid = items.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.cellSize = new Vector2(86, grid.cellSize.y);
        }
    }

    private static void ScaleChildren(Transform parent, float scale)
    {
        foreach (Transform child in parent)
        {
            var rect = child as RectTransform;
            if (rect == null) continue;
            var p = rect.anchoredPosition; p.x *= scale; rect.anchoredPosition = p;
            var size = rect.sizeDelta; size.x *= scale; rect.sizeDelta = size;
            ScaleChildren(child, scale);
        }
    }

    // Native QA checks rendered content rather than assuming success from compilation.
    public static bool Validate(RectTransform expanded, out string report)
    {
        var failures = new List<string>();
        var glyphs = new HashSet<string>();
        foreach (Transform child in expanded)
        {
            if (!child.name.StartsWith("Category_", StringComparison.Ordinal) && !child.name.StartsWith("Tab_", StringComparison.Ordinal)) continue;
            var caption = child.Find("Caption")?.GetComponent<TMP_Text>();
            var icon = child.Find("Icon")?.GetComponent<BistroBuilderCatalogOptionRelief>();
            if (child.GetComponent<Button>() == null) failures.Add(child.name + ": no native button");
            if (caption == null || caption.font == null || !caption.font.faceInfo.familyName.StartsWith("Inter", StringComparison.Ordinal))
                failures.Add(child.name + ": incorrect control font");
            if (caption != null)
            {
                caption.ForceMeshUpdate(true);
                if (caption.isTextOverflowing || caption.GetPreferredValues(caption.text).x > caption.rectTransform.rect.width + .5f)
                    failures.Add(child.name + ": label does not fit");
                if (caption.enableAutoSizing) failures.Add(child.name + ": variable type size");
            }
            if (icon == null || !glyphs.Add(icon.Character)) failures.Add(child.name + ": missing or repeated glyph");
            foreach (var graphic in child.GetComponentsInChildren<TMP_Text>(true))
                if (graphic.raycastTarget) failures.Add(child.name + ": decoration consumes pointer");
        }
        if (glyphs.Count != 12) failures.Add("Expected 12 distinct option vectors.");
        var grid = expanded.Find("ItemsScroll/Viewport/Items")?.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            float available = ((RectTransform)grid.transform).rect.width - grid.padding.horizontal;
            int columns = Mathf.FloorToInt((available + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x));
            if (columns != 4) failures.Add("B9 grid must retain four columns.");
        }
        report = failures.Count == 0 ? "12 vectors; full Inter labels; four B9 columns; decorative raycasts disabled." : string.Join("\n", failures);
        return failures.Count == 0;
    }
}

public sealed class BistroBuilderCatalogGeometryApplied : MonoBehaviour { }

public sealed class BistroBuilderCatalogOptionRelief : MonoBehaviour
{
    private TMP_Text front, glint, shadow;
    private bool selected, hovered, pressed, focused;
    public string Character { get; private set; }

    public void Configure(TMP_FontAsset font, string character)
    {
        Character = character;
        shadow = Layer("CastShadow", font, character, new Vector2(.35f, -.85f));
        glint = Layer("EdgeLight", font, character, new Vector2(-.25f, .55f));
        front = Layer("BrassFace", font, character, Vector2.zero);
        front.enableVertexGradient = true;
        Refresh();
    }

    private TMP_Text Layer(string name, TMP_FontAsset font, string character, Vector2 offset)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = offset; rect.offsetMax = offset;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = character;
        text.fontSize = Mathf.Min(((RectTransform)transform).sizeDelta.x, ((RectTransform)transform).sizeDelta.y);
        text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false; text.raycastTarget = false;
        return text;
    }

    public void SetSelected(bool value) { selected = value; Refresh(); }
    public void SetHovered(bool value) { hovered = value; if (!value) pressed = false; Refresh(); }
    public void SetPressed(bool value) { pressed = value; Refresh(); }
    public void SetFocused(bool value) { focused = value; Refresh(); }
    private void Refresh()
    {
        if (front == null) return;
        bool lit = selected || hovered || focused;
        Color light = pressed ? new Color32(177, 116, 47, 255) : lit ? new Color32(236, 189, 108, 255) : new Color32(211, 167, 100, 255);
        Color dark = pressed ? new Color32(115, 65, 30, 255) : lit ? new Color32(133, 78, 32, 255) : new Color32(133, 84, 43, 255);
        front.color = Color.white;
        front.colorGradient = new VertexGradient(light, light, dark, dark);
        glint.color = pressed ? new Color32(251, 226, 171, 65) : new Color32(255, 244, 211, 205);
        shadow.color = pressed ? new Color32(83, 48, 22, 105) : new Color32(83, 48, 22, 75);
    }
}

public sealed class BistroBuilderCatalogOptionEventRelay : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private BistroBuilderCatalogOptionRelief relief;
    public void Configure(BistroBuilderCatalogOptionRelief value) { relief = value; }
    public void OnPointerEnter(PointerEventData _) { relief?.SetHovered(true); }
    public void OnPointerExit(PointerEventData _) { relief?.SetHovered(false); }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) relief?.SetPressed(true); }
    public void OnPointerUp(PointerEventData _) { relief?.SetPressed(false); }
    public void OnSelect(BaseEventData _) { relief?.SetFocused(true); }
    public void OnDeselect(BaseEventData _) { relief?.SetFocused(false); }
    private void OnDisable() { relief?.SetHovered(false); relief?.SetPressed(false); relief?.SetFocused(false); }
}
