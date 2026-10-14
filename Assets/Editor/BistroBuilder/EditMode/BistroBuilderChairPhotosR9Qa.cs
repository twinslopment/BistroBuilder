using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Generate actual published chair photos through the canonical SAVIC service.</summary>
public static class BistroBuilderChairPhotosR9Qa
{
    public static void Generate()
    {
        const string folder="Logs/ChairPhotosR9";
        Directory.CreateDirectory(folder+"/Before");
        var report=new StringBuilder("REAL UNITY EDITOR GPU CHAIR PHOTOS\nGPU="+SystemInfo.graphicsDeviceName+"\n");
        int checks=0;
        Action<bool,string> check=(ok,text)=>{if(!ok)throw new InvalidOperationException(text);checks++;report.AppendLine("PASS|"+text);};
        try
        {
            var definitions=BistroBuilderCatalogThumbnailService.LoadAllDefinitions();
            var chairs=definitions.Where(x=>x.Category==RestaurantPlaceableItemCategory.Seating && x.HasValidPrefab).ToArray();
            check(chairs.Length>0,"Canonical published chair definitions found: "+chairs.Length);
            var otherIcons=definitions.Where(x=>x.Category!=RestaurantPlaceableItemCategory.Seating && x.CatalogIcon!=null)
                .Select(x=>AssetDatabase.GetAssetPath(x.CatalogIcon)).Distinct().Where(File.Exists).ToDictionary(x=>x,Hash);
            foreach(var item in chairs)
            {
                string png=AssetDatabase.GetAssetPath(item.CatalogIcon);
                check(BistroBuilderCatalogThumbnailService.IsGeneratedIcon(item.CatalogIcon),item.ItemId+" uses a generated derivative");
                string oldGuid=AssetDatabase.AssetPathToGUID(png);
                string definitionPath=AssetDatabase.GetAssetPath(item);
                string originalDefinition=File.ReadAllText(definitionPath);
                string prefabPath=AssetDatabase.GetAssetPath(item.Prefab);
                string originalPrefab=Hash(prefabPath);
                var dependencies=AssetDatabase.GetDependencies(prefabPath,true).Where(x=>x.EndsWith(".mat")||x.EndsWith(".fbx")||x.EndsWith(".obj")||x.EndsWith(".blend"))
                    .Where(File.Exists).ToDictionary(x=>x,Hash);
                if(!File.Exists(folder+"/Before/"+Path.GetFileName(png)))File.Copy(png,folder+"/Before/"+Path.GetFileName(png));
                if(!File.Exists(folder+"/Before/"+Path.GetFileName(png)+".meta"))File.Copy(png+".meta",folder+"/Before/"+Path.GetFileName(png)+".meta");
                if(!File.Exists(folder+"/Before/"+Path.GetFileName(definitionPath)))File.Copy(definitionPath,folder+"/Before/"+Path.GetFileName(definitionPath));
                var result=BistroBuilderCatalogThumbnailService.GenerateAndAssign(item,false,true,1024,false);
                check(result.Succeeded && result.Changed,item.ItemId+" actual GPU render: "+result.Message);
                check(AssetDatabase.AssetPathToGUID(result.AssetPath)==oldGuid,item.ItemId+" preserves sprite GUID");
                check(item.CatalogIcon.texture.width==1024 && item.CatalogIcon.texture.height==1024,item.ItemId+" imports 1024 x 1024 pixels");
                check(AssetDatabase.GetAssetPath(item.InspectorPreview)==result.AssetPath,item.ItemId+" detail preview receives its own new derivative");
                var importer=(TextureImporter)AssetImporter.GetAtPath(result.AssetPath);
                check(importer.textureCompression==TextureImporterCompression.Uncompressed && importer.mipmapEnabled && importer.filterMode==FilterMode.Trilinear,item.ItemId+" keeps thin edges with mipmapped, uncompressed alpha");
                check(Hash(prefabPath)==originalPrefab && dependencies.All(x=>Hash(x.Key)==x.Value),item.ItemId+" modifies no published geometry or materials");
                AssetDatabase.SaveAssets();
                check(CanonicalData(File.ReadAllText(definitionPath))==CanonicalData(originalDefinition),item.ItemId+" preserves all non-presentation canonical definition data");
                if(File.ReadAllText(definitionPath)!=originalDefinition)
                    report.AppendLine("REPAIR|"+item.ItemId+" generated inspector preview corrected; Unity serializes empty schema defaults");
                var quality=BistroBuilderCatalogThumbnailQualityService.AnalyzeSprite(item.CatalogIcon);
                check(quality.Status!=BistroBuilderThumbnailQualityStatus.Error,item.ItemId+" passes canonical image quality: "+quality.Status);
                File.Copy(result.AssetPath,folder+"/"+Path.GetFileName(result.AssetPath),true);
            }
            check(otherIcons.All(x=>Hash(x.Key)==x.Value),"Unrequested catalogue image derivatives remain identical");
            report.AppendLine("CHECKS="+checks+"\nCHAIRS="+chairs.Length+"\nPASS");
            File.WriteAllText(folder+"/generation.txt",report.ToString());
            Debug.Log(report.ToString());
        }
        catch(Exception e)
        {
            report.AppendLine("FAIL\n"+e);File.WriteAllText(folder+"/generation.txt",report.ToString());throw;
        }
    }
    // The intended presentation reference may change. Unity also emits these
    // two empty schema defaults when saving an older definition. All other
    // YAML content must remain identical, including economics and placement.
    private static string CanonicalData(string yaml)
    {
        return Regex.Replace(yaml.Replace("\r\n","\n"),
            @"^  (inspectorPreview:.*|snapProfile: \{fileID: 0\}|catalogSubcategory: ?)\n", "", RegexOptions.Multiline);
    }
    private static string Hash(string path)
    {
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
    }
}
