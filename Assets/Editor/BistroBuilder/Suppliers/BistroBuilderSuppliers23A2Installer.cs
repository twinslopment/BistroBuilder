#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Instalador idempotente de 2.3A. Solo crea/actualiza datos de autoría.
/// No toca Inventario, Recepciones, pedidos ni el catálogo runtime existente.
/// </summary>
internal static class BistroBuilderSuppliers23A2Installer
{
    [MenuItem("Tools/Bistro Builder/Proveedores/2.3A2 - Instalar o actualizar base de autoría", priority = 1)]
    public static void InstallOrUpdate()
    {
        int created = 0;
        int preserved = 0;
        bool supplierChanged = false;
        bool ingredientChanged = false;

        BistroBuilderSuppliers23A2Paths.EnsureFolders();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();

        if (suppliers == null)
        {
            suppliers = ScriptableObject.CreateInstance<BistroBuilderSupplierAuthoringDatabase>();
            AssetDatabase.CreateAsset(
                suppliers,
                BistroBuilderSuppliers23A2Paths.SupplierDatabasePath);
            created++;
            supplierChanged = true;
        }

        Undo.RecordObject(suppliers, "Instalar 2.3A proveedores");

        if (!string.Equals(
                suppliers.SchemaId,
                BistroBuilderSupplierAuthoringDatabase.CurrentSchemaId,
                StringComparison.Ordinal) ||
            suppliers.SchemaVersion !=
                BistroBuilderSupplierAuthoringDatabase.CurrentSchemaVersion)
        {
            supplierChanged = true;
        }

        suppliers.EditorEnsureSchema();

        List<BistroBuilderSupplierAuthoringRecord> seed =
            BistroBuilderSuppliers23A2SeedFactory.CreateSixProvisionalSuppliers();

        for (int index = 0; index < seed.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord candidate = seed[index];
            if (suppliers.TryGetSupplier(candidate.SupplierId, out _))
            {
                preserved++;
                continue;
            }

            suppliers.EditorSuppliers.Add(candidate);
            created++;
            supplierChanged = true;
        }

        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (ingredients == null)
        {
            ingredients = ScriptableObject.CreateInstance<BistroBuilderIngredientAuthoringDatabase>();
            AssetDatabase.CreateAsset(
                ingredients,
                BistroBuilderSuppliers23A2Paths.IngredientDatabasePath);
            created++;
            ingredientChanged = true;
        }

        Undo.RecordObject(ingredients, "Instalar 2.3A ingredientes");

        if (!string.Equals(
                ingredients.SchemaId,
                BistroBuilderIngredientAuthoringDatabase.CurrentSchemaId,
                StringComparison.Ordinal) ||
            ingredients.SchemaVersion !=
                BistroBuilderIngredientAuthoringDatabase.CurrentSchemaVersion)
        {
            ingredientChanged = true;
        }

        ingredients.EditorEnsureSchema();

        if (supplierChanged)
        {
            suppliers.EditorTouchRevision();
            EditorUtility.SetDirty(suppliers);
        }

        if (ingredientChanged)
        {
            ingredients.EditorTouchRevision();
            EditorUtility.SetDirty(ingredients);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int synced =
            BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.TrySynchronizeIntoDatabase(
                ingredients,
                false,
                out string source);

        Debug.Log(
            "2.3A instalado/actualizado. " +
            "Registros/activos creados: " + created +
            ", semillas preservadas: " + preserved +
            ", ingredientes sincronizados: " + synced +
            (string.IsNullOrWhiteSpace(source)
                ? "."
                : ", fuente: " + source + "."));

        Selection.activeObject = suppliers;
        BistroBuilderSupplierEditor23A2Window.OpenWindow();
    }
}
#endif
