#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23EInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3E - Instalar pedidos de compra")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("2.3E debe instalarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSuppliers23EPaths.EnsureFolders();
        BistroBuilderSupplierPurchaseOrderSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierPurchaseOrderSettings>(
                BistroBuilderSuppliers23EPaths.PurchaseOrderSettingsPath);
        bool created = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierPurchaseOrderSettings>();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23EPaths.PurchaseOrderSettingsPath);
            created = true;
        }

        Undo.RecordObject(settings, "Instalar 2.3E Pedidos de compra");
        settings.EditorEnsureSchemaAndDefaults();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23EPaths.SupplierDatabasePath);
        int activeSuppliers = 0;
        int activeOffers = 0;
        if (suppliers != null)
        {
            for (int index = 0; index < suppliers.Suppliers.Count; index++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
                if (supplier == null || !supplier.isActive) continue;
                activeSuppliers++;
                if (supplier.baseOffers == null) continue;
                for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                    if (offer != null && offer.isActive) activeOffers++;
                }
            }
        }

        Debug.Log(
            "2.3E instalado/actualizado. supplier.orders.settings: " +
            (created ? "creado" : "actualizado") +
            ", proveedores activos: " + activeSuppliers +
            ", ofertas activas: " + activeOffers +
            ". No se ha modificado supplier.authoring, supplier.catalog, mercado, promociones, Inventario ni Recepciones.");
    }
}
#endif
