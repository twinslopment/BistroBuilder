using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Use project fonts. Exclude the supplied demo font's marker glyphs only in a runtime copy.</summary>
public static class BistroBuilderEditorV2ApprovedTypography
{
    private static TMP_FontAsset heading;
    public static TMP_FontAsset Heading
    {
        get
        {
            if (heading != null) return heading;
            var supplied = Resources.Load<TMP_FontAsset>("BistroBuilder/UI/Typography/Recoleta-SDF");
            if (supplied == null)
            {
                Debug.LogError("Missing approved Recoleta-SDF project resource.");
                return BistroBuilderTypography.Emphasis;
            }
            heading = BistroBuilderTypography.CloneForRuntime(supplied);
            heading.name = "Recoleta approved Editor V2";
            heading.boldStyle = 0; heading.boldSpacing = 0;
            heading.fallbackFontAssetTable = new List<TMP_FontAsset>(supplied.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
            if (supplied.faceInfo.styleName.Contains("DEMO"))
            {
                heading.atlasPopulationMode = AtlasPopulationMode.Static;
                heading.characterTable.RemoveAll(c => BistroBuilderRecoletaDemoCoverage.Characters.Contains(c.unicode));
                heading.ReadFontAssetDefinition();
                // Existing project policy uses Georgia for missing accented Recoleta glyphs.
                var accentFont = TMP_FontAsset.CreateFontAsset("Georgia", "Regular");
                if (accentFont != null)
                {
                    accentFont.name = "Recoleta unavailable glyph fallback";
                    accentFont.boldStyle = 0; accentFont.boldSpacing = 0;
                    heading.fallbackFontAssetTable.Insert(0, accentFont);
                }
            }
            if (BistroBuilderTypography.Body != null) heading.fallbackFontAssetTable.Add(BistroBuilderTypography.Body);
            return heading;
        }
    }
}
