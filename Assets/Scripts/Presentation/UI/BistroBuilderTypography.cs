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
                title = CloneForRuntime(title);
                title.name = "Recoleta runtime";
                var accentFallback = TMP_FontAsset.CreateFontAsset("Georgia", "Regular");
                if (accentFallback != null && !title.fallbackFontAssetTable.Contains(accentFallback)) title.fallbackFontAssetTable.Insert(0, accentFallback);
            }
            if (title == null && Application.isPlaying) title = TMP_FontAsset.CreateFontAsset("Georgia", "Bold");
            return title != null ? title : Body;
        }
    }
    public static bool HasRecoleta => Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Recoleta-SDF") != null;
    // TMP owns and destroys a font's atlas/material. A runtime copy must not
    // retain these owned references to persistent subassets of the source font.
    public static TMP_FontAsset CloneForRuntime(TMP_FontAsset source)
    {
        if (source == null) return null;
        var copy = Object.Instantiate(source);
        var textures = source.atlasTextures;
        var owned = new Texture2D[textures != null ? textures.Length : 0];
        for (int i = 0; i < owned.Length; i++)
            if (textures[i] != null) owned[i] = Object.Instantiate(textures[i]);
        copy.atlasTextures = owned;
        if (source.material != null)
        {
            copy.material = new Material(source.material);
            if (owned.Length > 0) copy.material.mainTexture = owned[0];
        }
        return copy;
    }
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
