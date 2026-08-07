using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidades pequeñas para que el Taller de Objetos 3D pueda mostrar
/// el estado real del catálogo sin obligar al usuario a buscar el asset
/// manualmente en Project.
/// </summary>
internal static class BistroBuilderAssetWorkshopCatalogService
{
    internal const string MainCatalogPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    internal sealed class Health
    {
        public RestaurantPlaceableCatalogDefinition Catalog;
        public int ItemCount;
        public int NullReferences;
        public int DuplicateItemIds;
        public int DuplicateDisplayNames;
        public int MissingPrefabs;

        public int ProblemCount =>
            NullReferences +
            DuplicateItemIds +
            DuplicateDisplayNames +
            MissingPrefabs;
    }

    internal static Health Inspect()
    {
        Health health = new Health();

        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        health.Catalog = catalog;

        if (catalog == null)
        {
            return health;
        }

        HashSet<string> ids =
            new HashSet<string>(StringComparer.Ordinal);

        HashSet<string> names =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<RestaurantPlaceableItemDefinition> items =
            catalog.Items;

        health.ItemCount = items != null ? items.Count : 0;

        if (items == null)
        {
            return health;
        }

        for (int index = 0;
             index < items.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition item = items[index];

            if (item == null)
            {
                health.NullReferences++;
                continue;
            }

            string itemId = item.ItemId;
            if (string.IsNullOrWhiteSpace(itemId) ||
                !ids.Add(itemId))
            {
                health.DuplicateItemIds++;
            }

            string displayName = item.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName) &&
                !names.Add(displayName.Trim()))
            {
                health.DuplicateDisplayNames++;
            }

            if (!item.HasValidPrefab)
            {
                health.MissingPrefabs++;
            }
        }

        return health;
    }

    internal static bool RepairSafeIssues(out string message)
    {
        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        if (catalog == null)
        {
            message =
                "No se encuentra el catálogo principal en " +
                MainCatalogPath + ".";
            return false;
        }

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty items = serialized.FindProperty("items");

        if (items == null || !items.isArray)
        {
            message =
                "El catálogo ha cambiado y no contiene la lista 'items'.";
            return false;
        }

        HashSet<string> knownIds =
            new HashSet<string>(StringComparer.Ordinal);

        int removed = 0;

        for (int index = items.arraySize - 1;
             index >= 0;
             index--)
        {
            SerializedProperty element =
                items.GetArrayElementAtIndex(index);

            RestaurantPlaceableItemDefinition definition =
                element.objectReferenceValue as
                    RestaurantPlaceableItemDefinition;

            bool remove = definition == null;

            if (!remove)
            {
                string itemId = definition.ItemId;
                remove =
                    string.IsNullOrWhiteSpace(itemId) ||
                    !knownIds.Add(itemId);
            }

            if (!remove)
            {
                continue;
            }

            items.DeleteArrayElementAtIndex(index);

            // En arrays de referencias Unity puede requerir un segundo
            // borrado si el primer Delete deja una referencia nula.
            if (index < items.arraySize)
            {
                SerializedProperty afterDelete =
                    items.GetArrayElementAtIndex(index);

                if (afterDelete.objectReferenceValue == null)
                {
                    items.DeleteArrayElementAtIndex(index);
                }
            }

            removed++;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        message = removed > 0
            ? "Catálogo limpiado. Referencias retiradas: " + removed + "."
            : "El catálogo no tenía referencias nulas ni ItemId duplicados.";

        return true;
    }

    internal static void SelectMainCatalog()
    {
        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(MainCatalogPath);

        if (catalog == null)
        {
            EditorUtility.DisplayDialog(
                "Taller de Objetos 3D",
                "No se encuentra el catálogo principal.",
                "Cerrar"
            );
            return;
        }

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }
}
