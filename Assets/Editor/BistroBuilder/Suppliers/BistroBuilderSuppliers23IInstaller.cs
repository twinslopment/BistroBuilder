#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class BistroBuilderSuppliers23IInstaller
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3I - Instalar progresión y desbloqueos")]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("2.3I debe instalarse fuera de Play Mode.");
            return;
        }

        BistroBuilderSuppliers23IPaths.EnsureFolders();
        BistroBuilderSupplierProgressionSettings settings = BistroBuilderSuppliers23IPaths.LoadSettings();
        bool createdSettings = false;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BistroBuilderSupplierProgressionSettings>();
            settings.EditorEnsureSchemaAndDefaults();
            AssetDatabase.CreateAsset(settings, BistroBuilderSuppliers23IPaths.SettingsAssetPath);
            createdSettings = true;
        }
        else
        {
            settings.EditorEnsureSchemaAndDefaults();
            EditorUtility.SetDirty(settings);
        }

        BistroBuilderSupplierAuthoringDatabase suppliers = BistroBuilderSuppliers23IPaths.LoadSuppliers();
        if (suppliers == null)
        {
            Debug.LogError("2.3I: falta supplier.authoring. No se han sembrado reglas de desbloqueo.");
            AssetDatabase.SaveAssets();
            return;
        }

        Undo.RecordObject(suppliers, "Instalar progresión de proveedores 2.3I");
        int seededProfiles = 0;
        seededProfiles += SeedIfEmpty(suppliers, "supplier_distribuciones_norte", new[]
        {
            Condition(BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos, 30000L, null)
        });
        seededProfiles += SeedIfEmpty(suppliers, "supplier_huerta_clara", new[]
        {
            Condition(BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 3L, null)
        });
        seededProfiles += SeedIfEmpty(suppliers, "supplier_carnes_selectas", new[]
        {
            Condition(BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 7L, null),
            Condition(BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos, 50000L, null)
        });
        seededProfiles += SeedIfEmpty(suppliers, "supplier_costa_fresca", new[]
        {
            Condition(BistroBuilderSupplierUnlockRuleKind.DiasAbierto, 10L, null),
            Condition(BistroBuilderSupplierUnlockRuleKind.VolumenComprasCentimos, 75000L, null)
        });

        if (seededProfiles > 0)
        {
            suppliers.EditorTouchRevision();
            EditorUtility.SetDirty(suppliers);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int active = 0;
        int fromStart = 0;
        int progressive = 0;
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier == null || !supplier.isActive) continue;
            active++;
            if (supplier.unlockProfile != null && supplier.unlockProfile.availableFromStart) fromStart++;
            else if (supplier.unlockProfile != null && supplier.unlockProfile.conditions != null && supplier.unlockProfile.conditions.Count > 0) progressive++;
        }

        Debug.Log(
            "2.3I instalado/actualizado. supplier.progression.settings: " + (createdSettings ? "creado" : "actualizado") +
            ", proveedores activos: " + active +
            ", disponibles desde inicio: " + fromStart +
            ", progresivos: " + progressive +
            ", perfiles sembrados ahora: " + seededProfiles +
            ". 2.3I usa AND entre condiciones, no modifica supplier.catalog y 2.3F queda filtrado por proveedores desbloqueados. " +
            "No se ha modificado mercado, promociones, PurchaseOrder, logística, entrega física, Inventario ni Recepciones.");
    }

    private static int SeedIfEmpty(
        BistroBuilderSupplierAuthoringDatabase database,
        string supplierId,
        BistroBuilderSupplierUnlockConditionAuthoring[] conditions)
    {
        BistroBuilderSupplierAuthoringRecord supplier;
        if (!database.TryGetSupplier(supplierId, out supplier) || supplier == null || supplier.unlockProfile == null)
        {
            return 0;
        }
        if (supplier.unlockProfile.availableFromStart)
        {
            return 0;
        }
        if (supplier.unlockProfile.conditions == null)
        {
            supplier.unlockProfile.conditions = new List<BistroBuilderSupplierUnlockConditionAuthoring>();
        }
        if (supplier.unlockProfile.conditions.Count > 0)
        {
            return 0;
        }

        for (int index = 0; index < conditions.Length; index++)
        {
            supplier.unlockProfile.conditions.Add(conditions[index]);
        }
        return 1;
    }

    private static BistroBuilderSupplierUnlockConditionAuthoring Condition(
        BistroBuilderSupplierUnlockRuleKind kind,
        long numericThreshold,
        string stringThreshold)
    {
        return new BistroBuilderSupplierUnlockConditionAuthoring
        {
            kind = kind,
            numericThreshold = numericThreshold,
            stringThreshold = stringThreshold
        };
    }
}
#endif
