using System;
using UnityEditor;
using UnityEngine;
public static class BistroBuilderEditClarityBuild
{
    public static void Run()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition"))
        {
            var item=AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if(item.Prefab==null||!BistroBuilderCatalogThumbnailService.IsGeneratedIcon(item.CatalogIcon))continue;
            var result=BistroBuilderCatalogThumbnailService.GenerateAndAssign(item,false,true,512,false);
            if(!result.Succeeded)throw new Exception(result.Message);
        }
        AssetDatabase.SaveAssets();
        BistroBuilderEditSectionsPlayTest.RunBatchAndBuild();
    }
}
