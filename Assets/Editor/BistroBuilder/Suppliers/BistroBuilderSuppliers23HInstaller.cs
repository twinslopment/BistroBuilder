#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23HInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Instalar entrega física")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("2.3H debe instalarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSuppliers23HPaths.EnsureFolders();
        BistroBuilderSupplierDeliveryPresentationSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierDeliveryPresentationSettings>(BistroBuilderSuppliers23HPaths.DeliveryPresentationSettingsPath);
        bool created = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierDeliveryPresentationSettings>();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23HPaths.DeliveryPresentationSettingsPath);
            created = true;
        }

        Undo.RecordObject(settings, "Instalar 2.3H entrega física");
        settings.EditorEnsureSchemaAndDefaults();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23HPaths.SupplierDatabasePath);
        int active = 0;
        int withLogo = 0;
        int withName = 0;
        if (suppliers != null)
        {
            for (int i = 0; i < suppliers.Suppliers.Count; i++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[i];
                if (supplier == null || !supplier.isActive) continue;
                active++;
                if (supplier.logo != null) withLogo++;
                if (!string.IsNullOrWhiteSpace(supplier.displayName) || !string.IsNullOrWhiteSpace(supplier.shortName)) withName++;
            }
        }

        Debug.Log(
            "2.3H instalado/actualizado. supplier.delivery.presentation.settings: " + (created ? "creado" : "actualizado") +
            ", proveedores activos: " + active +
            ", con nombre visible: " + withName +
            ", con logo asignado: " + withLogo +
            ". Branding lateral obligatorio: si falta logo se usa nombre + colores del proveedor. " +
            "No se ha modificado supplier.authoring, supplier.catalog, PurchaseOrder, logística 2.3G, Inventario ni Recepciones.");
    }
}
#endif
