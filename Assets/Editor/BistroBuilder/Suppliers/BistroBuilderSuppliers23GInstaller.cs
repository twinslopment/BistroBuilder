#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23GInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3G - Instalar planificación logística")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("2.3G debe instalarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSuppliers23GPaths.EnsureFolders();
        BistroBuilderSupplierLogisticsPlanningSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierLogisticsPlanningSettings>(BistroBuilderSuppliers23GPaths.LogisticsSettingsPath);
        bool created = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierLogisticsPlanningSettings>();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23GPaths.LogisticsSettingsPath);
            created = true;
        }
        Undo.RecordObject(settings, "Instalar 2.3G Planificación logística");
        settings.EditorEnsureSchemaAndDefaults();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(BistroBuilderSuppliers23GPaths.SupplierDatabasePath);
        int active = 0;
        int withWindows = 0;
        int validReliability = 0;
        if (suppliers != null)
        {
            for (int index = 0; index < suppliers.Suppliers.Count; index++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
                if (supplier == null || !supplier.isActive) continue;
                active++;
                if (supplier.deliveryWindows != null && supplier.deliveryWindows.Count > 0) withWindows++;
                if (supplier.reliabilityValue >= 0f && supplier.reliabilityValue <= 1f) validReliability++;
            }
        }

        Debug.Log(
            "2.3G instalado/actualizado. supplier.logistics.settings: " + (created ? "creado" : "actualizado") +
            ", proveedores activos: " + active +
            ", con ventanas: " + withWindows +
            ", fiabilidad válida: " + validReliability +
            ". No se ha modificado PurchaseOrder, supplier.authoring, Inventario ni Recepciones.");
    }
}
#endif
