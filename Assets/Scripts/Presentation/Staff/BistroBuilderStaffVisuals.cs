using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BistroBuilder.UI.Iconography;

/// <summary>
/// Shared authored look for Personal / Candidatos / Horarios.
/// No game state, no extra Canvas and no scene-wide animation runner.
/// Coordinates are normalized within the existing management safe area.
/// </summary>
public static class BistroBuilderStaffVisuals
{
    // One coherent palette for 4F/5E, tuned against the approved Personal mock-up.
    public static readonly Color Ivory = new Color32(245, 230, 207, 255);
    public static readonly Color Paper = new Color32(251, 240, 220, 255);
    public static readonly Color Inset = new Color32(233, 208, 173, 255);
    public static readonly Color Amber = new Color32(232, 186, 115, 255);
    public static readonly Color Ink = new Color32(53, 35, 23, 255);
    public static readonly Color Muted = new Color32(110, 82, 52, 255);
    public static readonly Color Brass = new Color32(160, 110, 54, 255);
    public static readonly Color Border = new Color32(168, 125, 73, 255);
    public static readonly Color Danger = new Color32(146, 57, 44, 255);
    public static readonly Color Green = new Color32(51, 112, 52, 255);
    public static readonly Color Empty = new Color32(236, 223, 202, 255);

    private static BBIconCatalog icons;
    private static TMP_FontAsset staffRegularFont, staffBoldFont;
    private static bool regularResolved, boldResolved;

    // Personal is deliberately independent of the global Inter/Recoleta shell.
    // Resolve the OS Georgia faces at runtime; preserve a serif fallback when
    // Georgia is unavailable. Do not change the top bar or other modules.
    public static TMP_FontAsset StaffRegular => ResolveStaffFont(
        ref staffRegularFont, ref regularResolved, "Regular");
    public static TMP_FontAsset StaffBold => ResolveStaffFont(
        ref staffBoldFont, ref boldResolved, "Bold");

    private static TMP_FontAsset ResolveStaffFont(
        ref TMP_FontAsset cached, ref bool resolved, string style)
    {
        if (!resolved)
        {
            resolved = true;
            cached = TMP_FontAsset.CreateFontAsset("Georgia", style);
            if (cached != null) cached.name = "BB Staff Georgia " + style;
        }
        return cached != null ? cached : BistroBuilderTypography.Title;
    }

    // The simulated first names are curated by StaffRecruitmentProfile. These
    // sets choose the appropriate portrait-art collection, not an employee rule.
    private static readonly HashSet<string> PortraitFamilyF = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
        { "Lucía", "Marta", "Claudia", "Irene", "Paula", "Sara",
          "Nerea", "Aitana", "Carmen" };
    private static readonly HashSet<string> PortraitFamilyM = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
        { "Daniel", "Álvaro", "Marcos", "Hugo", "Javier", "Diego",
          "Adrián", "Pablo", "Bruno" };

    public static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static RectTransform Node(string name, Transform parent,
        float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, x0, y0, x1, y1);
        return rect;
    }

    public static RectTransform Panel(string name, Transform parent,
        float x0, float y0, float x1, float y1, Color fill, bool outline = true)
    {
        RectTransform r = Node(name, parent, x0, y0, x1, y1);
        Image bg = r.gameObject.AddComponent<Image>();
        bg.color = fill;
        bg.raycastTarget = false;
        if (outline)
        {
            Outline o = r.gameObject.AddComponent<Outline>();
            o.effectColor = Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;
        }
        return r;
    }

    public static TMP_Text Label(string name, Transform parent, string value,
        float size, float x0, float y0, float x1, float y1,
        bool heading = false, TextAlignmentOptions alignment =
            TextAlignmentOptions.MidlineLeft)
    {
        RectTransform r = Node(name, parent, x0, y0, x1, y1);
        TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(10f, size - 5f);
        TMP_FontAsset font = heading ? StaffBold : StaffRegular;
        if (font != null) text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.characterSpacing = heading ? -.35f : -.10f;
        text.color = Ink;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    public static void TextStyle(TMP_Text target, float size, bool heading = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
        Color? overrideColor = null)
    {
        if (target == null) return;
        TMP_FontAsset font = heading ? StaffBold : StaffRegular;
        if (font != null) target.font = font;
        target.fontStyle = FontStyles.Normal;
        target.characterSpacing = heading ? -.35f : -.10f;
        target.color = overrideColor ?? Ink;
        target.fontSize = size;
        target.enableAutoSizing = true;
        target.fontSizeMax = size;
        target.fontSizeMin = Mathf.Max(10f, size - 6f);
        target.alignment = alignment;
        target.overflowMode = TextOverflowModes.Ellipsis;
        target.raycastTarget = false;
    }

    public static void ButtonStyle(Button button, bool destructive = false,
        bool primary = false)
    {
        if (button == null) return;
        Image bg = button.targetGraphic as Image;
        if (bg == null) bg = button.GetComponent<Image>();
        if (bg == null) bg = button.gameObject.AddComponent<Image>();
        button.targetGraphic = bg;
        bg.color = destructive ? Danger : primary ? Amber : Inset;
        Outline outline = button.GetComponent<Outline>();
        if (outline == null) outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = destructive ? new Color32(97, 37, 28, 255) : Border;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        Shadow shadow = button.GetComponent<Shadow>();
        if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(73, 42, 18, 75);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
        // The subtly raised plate matches the approved ivory/brass controls.
        button.transition = Selectable.Transition.ColorTint;
        // Unity ColorTint writes Graphic.color directly; tinting from
        // white would erase the parchment/amber state on the first hover.
        Color baseColor = destructive ? Danger : primary ? Amber : Inset;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Paper,
            destructive ? .10f : .20f);
        colors.pressedColor = Color.Lerp(baseColor, Ink, .14f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g,
            baseColor.b, .42f);
        colors.fadeDuration = .16f;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        TextStyle(label, 17f, false, TextAlignmentOptions.Center,
            destructive ? Paper : Ink);
    }

    public static Button NewButton(string name, Transform parent,
        string label, float x0, float y0, float x1, float y1,
        bool primary = false, bool destructive = false)
    {
        RectTransform rect = Node(name, parent, x0, y0, x1, y1);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(name + "_Label", rect, label, 16f, .03f, 0f, .97f, 1f,
            false, TextAlignmentOptions.Center);
        ButtonStyle(button, destructive, primary);
        return button;
    }

    public static Image Bar(string name, Transform parent,
        float x0, float y0, float x1, float y1, out Image fill)
    {
        RectTransform track = Panel(name, parent, x0, y0, x1, y1, Empty);
        RectTransform progress = Panel(name + "_Fill", track, 0f, 0f, 0f, 1f,
            Brass, false);
        fill = progress.GetComponent<Image>();
        return track.GetComponent<Image>();
    }

    public static void SetBar(Image fill, float value01)
    {
        if (fill == null) return;
        RectTransform r = fill.rectTransform;
        float value = Mathf.Clamp01(value01);
        r.anchorMin = Vector2.zero;
        r.anchorMax = new Vector2(value, 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    public static void Separator(string name, Transform parent,
        float x0, float y, float x1)
    {
        Panel(name, parent, x0, y, x1, y + .002f,
            new Color32(194, 158, 111, 255), false);
    }

    public static void Frame(Transform parent, string name)
    {
        RectTransform frame = Node(name, parent, 0f, 0f, 1f, 1f);
        BistroBuilderTopBarPlate plate =
            frame.gameObject.AddComponent<BistroBuilderTopBarPlate>();
        plate.Cell = false;
        plate.raycastTarget = false;
        frame.SetAsFirstSibling();
    }

    public static Sprite RoleIcon(string roleId)
    {
        BBIconId icon = string.Equals(roleId, "waiter", StringComparison.Ordinal)
            ? BBIconId.ObjectWaiter
            : string.Equals(roleId, "cook", StringComparison.Ordinal)
                ? BBIconId.ObjectCook : BBIconId.NavStaff;
        return Icon(icon);
    }

    public static Sprite Icon(BBIconId id)
    {
        if (icons == null) icons = BBIconCatalog.LoadDefault();
        return icons != null ? icons.GetSprite(id) : null;
    }

    public static Sprite StaffFactIcon(int index)
    {
        string[] paths = { "role", "salary", "assignment", "state" };
        if (index < 0 || index >= paths.Length) return null;
        Sprite authored = Resources.Load<Sprite>(
            "BistroBuilder/UI/StaffIcons/" + paths[index]);
        if (authored != null) return authored;
        BBIconId[] fallbacks = { BBIconId.ObjectEquipment,
            BBIconId.EconomyIncome, BBIconId.AreaDining,
            BBIconId.StatusCorrect };
        return Icon(fallbacks[index]);
    }

    public static void DecorativeIcon(string name, Transform parent,
        BBIconId id, float x0, float y0, float x1, float y1)
    {
        Sprite sprite = Icon(id);
        if (sprite == null) return;
        RectTransform root = Node(name, parent, x0, y0, x1, y1);
        Image image = root.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Ink;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Uses authored portraits if supplied, with a stable identity derived
    /// from name and role (a candidate retains the same portrait after hire).
    /// No screenshot people or unrelated stock faces are assigned as real staff.
    /// Role icon is the explicit fallback when no portrait art is installed.
    /// </summary>
    public static Sprite Portrait(string roleId, string fullName)
    {
        string prefix = string.Equals(roleId, "waiter", StringComparison.Ordinal)
            ? "waiter" : string.Equals(roleId, "cook", StringComparison.Ordinal)
                ? "cook" : roleId;
        if (string.IsNullOrWhiteSpace(prefix)) return null;
        Sprite[] variants = Resources.LoadAll<Sprite>(
            "BistroBuilder/UI/StaffPortraits/" + prefix);
        if (variants == null || variants.Length == 0) return null;

        string firstName = (fullName ?? string.Empty).Trim();
        int separator = firstName.IndexOf(' ');
        if (separator >= 0) firstName = firstName.Substring(0, separator);
        string family = PortraitFamilyF.Contains(firstName) ? "_f_"
            : PortraitFamilyM.Contains(firstName) ? "_m_" : string.Empty;
        var available = new List<Sprite>(variants.Length);
        for (int i = 0; i < variants.Length; i++)
            if (variants[i] != null &&
                (family.Length == 0 || variants[i].name.StartsWith(
                    prefix + family, StringComparison.OrdinalIgnoreCase)))
                available.Add(variants[i]);
        if (available.Count == 0) available.AddRange(variants);
        available.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // Stable across a candidate being hired; never use Unity random or
        // depend on unspecified Resources.LoadAll ordering.
        uint hash = 2166136261u;
        string key = prefix + ":" + (fullName ?? string.Empty) + ":1";
        unchecked
        {
            for (int i = 0; i < key.Length; i++)
                hash = (hash ^ key[i]) * 16777619u;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
        }
        return available[(int)(hash % (uint)available.Count)];
    }

    public static void PortraitFrame(string name, Transform parent,
        float x0, float y0, float x1, float y1, string roleId,
        string fullName, bool large)
    {
        RectTransform outer = Panel(name, parent, x0, y0, x1, y1,
            new Color32(118, 85, 53, 255));
        RectTransform inner = Panel("PortraitMat", outer,
            .035f, .035f, .965f, .965f, Inset, false);
        Image image = Node("Portrait", inner, .035f, .035f, .965f, .965f)
            .gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        Sprite portrait = Portrait(roleId, fullName);
        Sprite role = RoleIcon(roleId);
        image.sprite = portrait != null ? portrait : role;
        image.preserveAspect = true;
        image.color = portrait != null ? Color.white : Muted;
        image.enabled = image.sprite != null;
        if (portrait == null && large)
        {
            var caption = Label("PortraitRole", inner,
                string.Equals(roleId, "cook", StringComparison.Ordinal)
                    ? "COCINA" : string.Equals(roleId, "waiter",
                        StringComparison.Ordinal) ? "SALA" : "PERSONAL",
                14f, .09f, .035f, .91f, .18f, true,
                TextAlignmentOptions.Center);
            caption.color = Brass;
        }
    }
}
