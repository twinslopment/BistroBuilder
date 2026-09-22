using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicPersistenceCatalogProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Persistence Catalog Probe",
            false,
            121)]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            SavicEditorContext context =
                SavicEditorContext.Instance;

            SavicManifest table =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                table != null,
                "No published SAVIC table is available.");

            RestaurantPlaceableCatalogDefinition mainCatalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            Require(
                mainCatalog != null,
                "Canonical placeable catalog is missing.");

            Require(
                mainCatalog.TryGetItem(
                    table.canonicalContentId,
                    out RestaurantPlaceableItemDefinition canonicalItem) &&
                canonicalItem != null,
                "Published SAVIC table is missing from the canonical catalog.");

            GameObject host =
                new GameObject(
                    "SAVIC_PersistenceCatalogProbe");

            try
            {
                BistroBuilderSaveDefinitionCatalog saveCatalog =
                    host.AddComponent
                        <BistroBuilderSaveDefinitionCatalog>();

                bool firstBindingChanged =
                    SavicPersistenceCatalogIntegration
                        .EnsureCatalogBinding(
                            saveCatalog,
                            mainCatalog);

                bool secondBindingChanged =
                    SavicPersistenceCatalogIntegration
                        .EnsureCatalogBinding(
                            saveCatalog,
                            mainCatalog);

                Require(
                    firstBindingChanged,
                    "First canonical catalog binding did not report a change.");

                Require(
                    !secondBindingChanged,
                    "Canonical catalog binding is not idempotent.");

                Require(
                    saveCatalog.SourceCatalogs != null &&
                    saveCatalog.SourceCatalogs.Count == 1 &&
                    ReferenceEquals(
                        saveCatalog.SourceCatalogs[0],
                        mainCatalog),
                    "Persistence catalog does not retain exactly one canonical source.");

                Require(
                    saveCatalog.ValidateConfiguration(
                        out string validationError),
                    "Persistence catalog validation failed: " +
                    validationError);

                Require(
                    saveCatalog.TryGetDefinition(
                        table.canonicalContentId,
                        out RestaurantPlaceableItemDefinition resolved) &&
                    ReferenceEquals(
                        resolved,
                        canonicalItem),
                    "Save/Load catalog cannot resolve the SAVIC table by canonical ItemId.");

                ValidateLegacyPlusCanonicalDeduplication(
                    saveCatalog,
                    mainCatalog,
                    canonicalItem,
                    table.canonicalContentId);

                Debug.Log(
                    "[SAVIC] PERSISTENCE CATALOG PROBE - PASS\n" +
                    "Canonical ItemId: " +
                    table.canonicalContentId +
                    "\nResolved definition: " +
                    AssetDatabase.GetAssetPath(
                        canonicalItem) +
                    "\nSource catalogs: " +
                    saveCatalog.SourceCatalogs.Count +
                    "\nResolved definitions: " +
                    saveCatalog.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    host);
            }
        }

        private static void ValidateLegacyPlusCanonicalDeduplication(
            BistroBuilderSaveDefinitionCatalog saveCatalog,
            RestaurantPlaceableCatalogDefinition mainCatalog,
            RestaurantPlaceableItemDefinition canonicalItem,
            string contentId)
        {
            SerializedObject serialized =
                new SerializedObject(
                    saveCatalog);

            SerializedProperty definitions =
                serialized.FindProperty(
                    "definitions");

            Require(
                definitions != null &&
                definitions.isArray,
                "Legacy persistence definitions contract is unavailable.");

            definitions.arraySize =
                1;

            definitions
                .GetArrayElementAtIndex(0)
                .objectReferenceValue =
                    canonicalItem;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            saveCatalog.RebuildIndex();

            Require(
                saveCatalog.ValidateConfiguration(
                    out string validationError),
                "Legacy + canonical source deduplication failed: " +
                validationError);

            Require(
                saveCatalog.TryGetDefinition(
                    contentId,
                    out RestaurantPlaceableItemDefinition resolved) &&
                ReferenceEquals(
                    resolved,
                    canonicalItem),
                "Same-definition legacy entry changed canonical resolution.");

            int occurrences =
                0;

            IReadOnlyList<RestaurantPlaceableItemDefinition> resolvedItems =
                saveCatalog.Definitions;

            for (int index = 0;
                 index < resolvedItems.Count;
                 index++)
            {
                if (ReferenceEquals(
                        resolvedItems[index],
                        canonicalItem))
                {
                    occurrences++;
                }
            }

            Require(
                occurrences == 1,
                "Same definition is duplicated in resolved persistence catalog.");
        }

        private static SavicManifest FindPublishedTable(
            IReadOnlyList<SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(
                        candidate.canonicalContentId))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    message);
        }
    }
}
