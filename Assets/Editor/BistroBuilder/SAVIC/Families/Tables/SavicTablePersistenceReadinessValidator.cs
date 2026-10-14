using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicTablePersistenceReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        internal static SavicTablePersistenceReadinessRecord Validate(
            SavicManifest manifest,
            string prefabPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                throw new InvalidOperationException(
                    "Persistence validation requires a canonical ContentId.");
            }

            RestaurantPlaceableCatalogDefinition mainCatalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (mainCatalog == null)
            {
                throw new InvalidOperationException(
                    "Canonical placeable catalog is missing.");
            }

            if (!mainCatalog.TryGetItem(
                    manifest.canonicalContentId,
                    out RestaurantPlaceableItemDefinition item) ||
                item == null)
            {
                throw new InvalidOperationException(
                    "Canonical catalog cannot resolve published SAVIC table.");
            }

            if (item.Prefab == null)
            {
                throw new InvalidOperationException(
                    "Published SAVIC table has no loadable prefab.");
            }

            string itemPrefabPath =
                AssetDatabase.GetAssetPath(
                    item.Prefab);

            if (!string.Equals(
                    itemPrefabPath,
                    prefabPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Published SAVIC table prefab differs from canonical catalog prefab.");
            }

            GameObject host =
                new GameObject(
                    "SAVIC_PersistenceReadinessValidation");

            try
            {
                BistroBuilderSaveDefinitionCatalog saveCatalog =
                    host.AddComponent
                        <BistroBuilderSaveDefinitionCatalog>();

                SavicPersistenceCatalogIntegration
                    .EnsureCatalogBinding(
                        saveCatalog,
                        mainCatalog);

                if (!saveCatalog.ValidateConfiguration(
                        out string configurationError))
                {
                    throw new InvalidOperationException(
                        "Persistence catalog configuration is invalid: " +
                        configurationError);
                }

                if (!saveCatalog.TryGetDefinition(
                        manifest.canonicalContentId,
                        out RestaurantPlaceableItemDefinition resolved) ||
                    !ReferenceEquals(
                        resolved,
                        item))
                {
                    throw new InvalidOperationException(
                        "Save/Load catalog cannot resolve the published SAVIC table.");
                }

                RestaurantPlaceableObject placeable =
                    item.Prefab.GetComponent
                        <RestaurantPlaceableObject>();

                if (placeable == null)
                {
                    throw new InvalidOperationException(
                        "Published table prefab has no RestaurantPlaceableObject.");
                }

                RestaurantTable table =
                    item.Prefab.GetComponent
                        <RestaurantTable>();

                if (table == null ||
                    table.TableId < 1)
                {
                    throw new InvalidOperationException(
                        "Published table prefab has no valid persistent functional TableId.");
                }

                return new SavicTablePersistenceReadinessRecord
                {
                    validated = true,
                    validatorVersion = Version,
                    sourceCatalogAssetPath =
                        MainCatalogPath,
                    canonicalContentId =
                        manifest.canonicalContentId,
                    itemDefinitionAssetPath =
                        AssetDatabase.GetAssetPath(item),
                    prefabAssetPath =
                        prefabPath ?? string.Empty,
                    catalogResolvable = true,
                    prefabResolvable = true,
                    functionalTableIdValid = true,
                    evidence =
                        "Canonical placeable catalog resolves the SAVIC table; " +
                        "BistroBuilderSaveDefinitionCatalog consumes the canonical " +
                        "catalog without duplicating definitions; prefab and " +
                        "functional table identity are loadable.",
                    validatedUtc =
                        DateTime.UtcNow.ToString("O")
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    host);
            }
        }
    }
}
