using TMPro;
using UnityEngine;

public static class BistroBuilderTypography
{
    private static TMP_FontAsset body, emphasis, title;
    private static Font legacyBody, legacyTitle;
    private static bool titleResolved;
    public static TMP_FontAsset Body => body != null ? body : body = Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Inter-Regular-SDF");
    public static TMP_FontAsset Emphasis => emphasis != null ? emphasis : emphasis = Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Inter-SemiBold-SDF");
    public static TMP_FontAsset Title
    {
        get
        {
            if (titleResolved) return title != null ? title : Body;
            titleResolved = true;
            title = Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Recoleta-SDF");
            if (title != null && title.faceInfo.styleName.Contains("DEMO") && Application.isPlaying)
            {
                title = Object.Instantiate(title);
                title.name = "Recoleta runtime";
                var accentFallback = TMP_FontAsset.CreateFontAsset("Georgia", "Regular");
                if (accentFallback != null && !title.fallbackFontAssetTable.Contains(accentFallback)) title.fallbackFontAssetTable.Insert(0, accentFallback);
            }
            if (title == null && Application.isPlaying) title = TMP_FontAsset.CreateFontAsset("Georgia", "Bold");
            return title != null ? title : Body;
        }
    }
    public static bool HasRecoleta => Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Recoleta-SDF") != null;
    public static Font LegacyBody => legacyBody != null ? legacyBody : legacyBody = Resources.Load<Font>("BistroBuilder/UI/Typography/Inter-Regular");
    public static Font LegacyTitle
    {
        get
        {
            if (legacyTitle != null) return legacyTitle;
            var supplied = Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Recoleta-SDF");
            // IMGUI cannot fall back individual glyphs; keep accented opening-screen headings readable.
            if (supplied != null && !supplied.faceInfo.styleName.Contains("DEMO")) legacyTitle = Resources.Load<Font>("BistroBuilder/UI/Typography/Recoleta");
            return legacyTitle != null ? legacyTitle : legacyTitle = Font.CreateDynamicFontFromOSFont("Georgia", 32);
        }
    }
    public static void Apply(TMP_Text text, BistroBuilderUiStyleRole role, bool preserveSize = false)
    {
        bool heading = role == BistroBuilderUiStyleRole.Title || role == BistroBuilderUiStyleRole.Heading;
        var font = heading ? Title : role == BistroBuilderUiStyleRole.Subheading || role == BistroBuilderUiStyleRole.Label || role == BistroBuilderUiStyleRole.Kpi ? Emphasis : Body;
        if (font != null) text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.characterSpacing = 0;
        text.lineSpacing = role == BistroBuilderUiStyleRole.Body ? 5 : 1;
        if (preserveSize) return;
        float size = Size(role);
        text.fontSize = size;
        text.enableAutoSizing = true; text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Min(size, role == BistroBuilderUiStyleRole.Title ? 22 : role == BistroBuilderUiStyleRole.Heading ? 18 : 12);
    }
    public static float Size(BistroBuilderUiStyleRole role)
    {
        switch(role)
        {
            case BistroBuilderUiStyleRole.Title: return BistroBuilderUiTokens.FontH1;
            case BistroBuilderUiStyleRole.Heading: return BistroBuilderUiTokens.FontH2;
            case BistroBuilderUiStyleRole.Subheading: return BistroBuilderUiTokens.FontH3;
            case BistroBuilderUiStyleRole.Label: return BistroBuilderUiTokens.FontLabel;
            case BistroBuilderUiStyleRole.Caption: return BistroBuilderUiTokens.FontCaption;
            case BistroBuilderUiStyleRole.Kpi: return BistroBuilderUiTokens.FontKpi;
            default: return BistroBuilderUiTokens.FontBody;
        }
    }
}
