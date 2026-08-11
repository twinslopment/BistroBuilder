#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Instalador idempotente conjunto de 2.3B1 y 2.3B2.
///
/// B1: formatos comerciales reutilizables de todos los ingredientes canónicos.
/// B2: catálogo proveedor→formato y ofertas base editables.
///
/// No publica todavía en supplier.catalog runtime: esa convergencia se valida
/// y ejecuta en 2.3B3 para no introducir una segunda autoridad operacional.
/// </summary>
internal static class BistroBuilderSuppliers23B12Installer
{
    [MenuItem(
        "Tools/Bistro Builder/Proveedores/2.3B1+B2 - Instalar formatos y catálogo base",
        priority = 2)]
    public static void InstallOrUpdate()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (suppliers == null || ingredients == null)
        {
            Debug.LogWarning(
                "2.3B1+B2 necesita 2.3A instalado. Se ejecutará primero " +
                "el instalador de autoría 2.3A.");

            BistroBuilderSuppliers23A2Installer.InstallOrUpdate();
            suppliers = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
            ingredients = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();
        }

        if (suppliers == null || ingredients == null)
        {
            Debug.LogError(
                "2.3B1+B2 no pudo localizar las dos bases de autoría después de instalar 2.3A.");
            return;
        }

        Undo.RecordObject(suppliers, "Instalar 2.3B2 catálogo base");
        Undo.RecordObject(ingredients, "Instalar 2.3B1 formatos comerciales");

        bool supplierSchemaChanged =
            suppliers.SchemaVersion != BistroBuilderSupplierAuthoringDatabase.CurrentSchemaVersion ||
            !string.Equals(
                suppliers.SchemaId,
                BistroBuilderSupplierAuthoringDatabase.CurrentSchemaId,
                StringComparison.Ordinal);

        bool ingredientSchemaChanged =
            ingredients.SchemaVersion != BistroBuilderIngredientAuthoringDatabase.CurrentSchemaVersion ||
            !string.Equals(
                ingredients.SchemaId,
                BistroBuilderIngredientAuthoringDatabase.CurrentSchemaId,
                StringComparison.Ordinal);

        suppliers.EditorEnsureSchema();
        ingredients.EditorEnsureSchema();

        int synchronized =
            BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.TrySynchronizeIntoDatabase(
                ingredients,
                false,
                out string source);

        int addedPackages = 0;
        for (int index = 0; index < ingredients.EditorIngredients.Count; index++)
        {
            addedPackages +=
                BistroBuilderSuppliers23B12ContentSeed.EnsureFormatsForIngredient(
                    ingredients.EditorIngredients[index]);
        }

        int addedOffers =
            BistroBuilderSuppliers23B12ContentSeed.EnsureBaseOffers(
                suppliers,
                ingredients);

        bool supplierChanged = supplierSchemaChanged || addedOffers > 0;
        bool ingredientChanged = ingredientSchemaChanged || addedPackages > 0;

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

        int totalPackages = CountPackages(ingredients);
        int totalOffers = CountOffers(suppliers);

        Debug.Log(
            "2.3B1+B2 instalado/actualizado. " +
            "Ingredientes sincronizados: " + synchronized +
            ", formatos añadidos: " + addedPackages +
            ", formatos totales: " + totalPackages +
            ", ofertas base añadidas: " + addedOffers +
            ", ofertas base totales: " + totalOffers +
            (string.IsNullOrWhiteSpace(source)
                ? "."
                : ", fuente: " + source + "."));

        Selection.activeObject = suppliers;
        BistroBuilderSupplierEditor23A2Window.OpenWindow();
    }

    private static int CountPackages(
        BistroBuilderIngredientAuthoringDatabase database)
    {
        int count = 0;
        for (int index = 0; index < database.Ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient =
                database.Ingredients[index];
            if (ingredient != null && ingredient.commercialPackages != null)
            {
                count += ingredient.commercialPackages.Count;
            }
        }

        return count;
    }

    private static int CountOffers(
        BistroBuilderSupplierAuthoringDatabase database)
    {
        int count = 0;
        for (int index = 0; index < database.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                database.Suppliers[index];
            if (supplier != null && supplier.baseOffers != null)
            {
                count += supplier.baseOffers.Count;
            }
        }

        return count;
    }
}
#endif
