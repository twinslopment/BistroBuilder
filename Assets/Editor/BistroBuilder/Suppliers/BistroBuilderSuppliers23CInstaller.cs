#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BistroBuilderSuppliers23CInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C - Instalar mercado y ciclo de 5 días")]
    public static void InstallOrUpdate()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder(BistroBuilderSuppliers23CPaths.ResourcesFolder, "BistroBuilder");
        EnsureFolder(BistroBuilderSuppliers23CPaths.BistroBuilderFolder, "Suppliers");

        BistroBuilderSupplierMarketSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23CPaths.MarketSettingsAsset);

        bool created = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierMarketSettings>();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23CPaths.MarketSettingsAsset);
            created = true;
        }

        Undo.RecordObject(settings, "2.3C actualizar ajustes de mercado");
        settings.EditorEnsureSchemaAndDefaults();
        EditorUtility.SetDirty(settings);

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23CPaths.SupplierAuthoringAsset);

        int supplierProfilesAdjusted = 0;
        if (suppliers != null)
        {
            Undo.RecordObject(suppliers, "2.3C fijar revisión de mercado cada 5 días");
            for (int index = 0; index < suppliers.EditorSuppliers.Count; index++)
            {
                BistroBuilderSupplierAuthoringRecord supplier = suppliers.EditorSuppliers[index];
                if (supplier == null || supplier.priceEvolutionProfile == null)
                {
                    continue;
                }

                if (supplier.priceEvolutionProfile.reviewEveryGameDays != 5)
                {
                    supplier.priceEvolutionProfile.reviewEveryGameDays = 5;
                    supplierProfilesAdjusted++;
                }
            }

            if (supplierProfilesAdjusted > 0)
            {
                suppliers.EditorTouchRevision();
                EditorUtility.SetDirty(suppliers);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "2.3C instalado/actualizado. market.settings: " +
            (created ? "creado" : "existente") +
            ", ciclo: 5 días, perfiles de proveedor ajustados: " +
            supplierProfilesAdjusted +
            ". La autoridad runtime se crea automáticamente en Play Mode.");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
