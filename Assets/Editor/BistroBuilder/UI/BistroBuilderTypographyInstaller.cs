using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public sealed class BistroBuilderTypographyInstaller : IPreprocessBuildWithReport
{
    public int callbackOrder => -80;
    public void OnPreprocessBuild(BuildReport report) => Prepare();
    [MenuItem("Tools/Bistro Builder/UI/Prepare typography")]
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        Create("Inter-Regular"); Create("Inter-SemiBold");
        if (File.Exists("Assets/Resources/BistroBuilder/UI/Typography/Recoleta.otf") || File.Exists("Assets/Resources/BistroBuilder/UI/Typography/Recoleta.ttf")) Create("Recoleta");
        var title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/BistroBuilder/UI/Typography/Recoleta-SDF.asset");
        if (title != null && title.faceInfo.styleName.IndexOf("DEMO", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // The supplied demo maps accented glyphs to its publisher mark. Use a normal fallback.
            title.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            string ascii = ""; for (int i = 32; i < 127; i++) ascii += (char)i;
            title.TryAddCharacters(ascii, out _);
            title.characterTable.RemoveAll(character => character.unicode > 126);
            title.atlasPopulationMode = AtlasPopulationMode.Static;
            title.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/BistroBuilder/UI/Typography/Inter-Regular-SDF.asset") };
            title.ReadFontAssetDefinition(); EditorUtility.SetDirty(title);
        }
        AssetDatabase.SaveAssets();
    }
    private static void Create(string name)
    {
        string root="Assets/Resources/BistroBuilder/UI/Typography/";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+name+"-SDF.asset") != null) return;
        var source=AssetDatabase.LoadAssetAtPath<Font>(root+name+".ttf") ?? AssetDatabase.LoadAssetAtPath<Font>(root+name+".otf");
        if (source==null) throw new InvalidOperationException("Missing font: "+name);
        var font=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        font.name=name+"-SDF"; AssetDatabase.CreateAsset(font,root+name+"-SDF.asset");
        foreach(var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture,font);
        AssetDatabase.AddObjectToAsset(font.material,font);
        string chars="";for(int c=32;c<256;c++)chars+=(char)c;chars+="€–—•…✓×↑↓←→¡¿";
        font.TryAddCharacters(chars,out _);EditorUtility.SetDirty(font);
    }
}
