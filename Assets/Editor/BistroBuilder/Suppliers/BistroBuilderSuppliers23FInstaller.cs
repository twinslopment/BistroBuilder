#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23FInstaller
{
    [MenuItem(BistroBuilderSuppliers23FPaths.MenuRoot + "2.3F - Instalar Motor de Compra Inteligente")]
    public static void Install()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/BistroBuilder");
        EnsureFolder("Assets/Resources/BistroBuilder/Suppliers");
        BistroBuilderSupplierSmartPurchaseSettings settings = AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierSmartPurchaseSettings>(BistroBuilderSuppliers23FPaths.SettingsAssetPath);
        bool created=false;
        if(settings==null)
        {
            settings=ScriptableObject.CreateInstance<BistroBuilderSupplierSmartPurchaseSettings>();
            AssetDatabase.CreateAsset(settings,BistroBuilderSuppliers23FPaths.SettingsAssetPath);
            created=true;
        }
        settings.EditorEnsureSchema();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        BistroBuilderSupplierAuthoringDatabase suppliers=Resources.Load<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath);
        BistroBuilderIngredientAuthoringDatabase ingredients=Resources.Load<BistroBuilderIngredientAuthoringDatabase>(BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath);
        int sc=0,oc=0,ic=0;
        if(suppliers!=null) foreach(var s in suppliers.Suppliers) if(s!=null&&s.isActive){sc++; if(s.baseOffers!=null) foreach(var o in s.baseOffers) if(o!=null&&o.isActive) oc++;}
        if(ingredients!=null) foreach(var i in ingredients.Ingredients) if(i!=null&&i.isActive) ic++;
        Debug.Log("2.3F instalado/actualizado. supplier.smart_purchase.settings: "+(created?"creado":"actualizado")+", proveedores activos: "+sc+", ofertas activas: "+oc+", ingredientes: "+ic+". No se ha modificado Inventario, 2.2C, pedidos, mercado ni promociones.");
    }
    private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; int p=path.LastIndexOf('/'); string parent=path.Substring(0,p); string name=path.Substring(p+1); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,name); }
}
#endif
