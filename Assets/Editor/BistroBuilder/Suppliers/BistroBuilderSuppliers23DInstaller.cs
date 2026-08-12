#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23DInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3D - Instalar Motor Comercial Inteligente")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("2.3D debe instalarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSuppliers23DPaths.EnsureFolders();
        BistroBuilderSupplierCommercialIntelligenceSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(
                BistroBuilderSuppliers23DPaths.CommercialSettingsPath);
        bool created = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierCommercialIntelligenceSettings>();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23DPaths.CommercialSettingsPath);
            created = true;
        }

        Undo.RecordObject(settings, "Instalar 2.3D Motor Comercial Inteligente");
        settings.EditorEnsureSchemaAndDefaults();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23DPaths.SupplierDatabasePath);
        int activeSuppliers = 0;
        int eligibleOffers = 0;
        if (suppliers != null)
        {
            for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
                if (supplier == null || !supplier.isActive)
                {
                    continue;
                }
                activeSuppliers++;
                if (supplier.baseOffers == null)
                {
                    continue;
                }
                for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                    if (offer != null && offer.isActive && offer.promotionEligible)
                    {
                        eligibleOffers++;
                    }
                }
            }
        }

        Debug.Log(
            "2.3D instalado/actualizado. commercial.settings: " +
            (created ? "creado" : "actualizado") +
            ", proveedores activos: " + activeSuppliers +
            ", ofertas elegibles para promoción: " + eligibleOffers +
            ". No se ha modificado supplier.authoring, supplier.catalog, mercado, Inventario ni Recepciones.");
    }
}
#endif
