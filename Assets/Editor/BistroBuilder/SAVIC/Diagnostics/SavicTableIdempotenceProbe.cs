using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicTableIdempotenceProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Table Idempotence Probe",
            false,
            113)]
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

            SavicManifest manifest =
                FindPublishedTable(
                    context.Manifests.GetAll());

            if (manifest == null)
            {
                throw new InvalidOperationException(
                    "No published SAVIC table exists for the idempotence probe.");
            }

            SavicSourceProcessingOutcome first =
                context.SourceProcessing.Process(
                    manifest);

            Require(
                first.Succeeded,
                "First idempotence pass failed: " +
                first.Message);

            manifest =
                first.Manifest;

            string itemPath =
                manifest.tableAuthoring
                    .itemDefinitionAssetPath;

            string prefabPath =
                manifest.tableAuthoring
                    .prefabAssetPath;

            string folder =
                Path.GetDirectoryName(itemPath)?
                    .Replace('\\', '/')
                ?? string.Empty;

            string largePreviewPath =
                folder + "/Preview_Large.png";

            string catalogPreviewPath =
                folder + "/Preview_Catalog.png";

            string itemGuid =
                RequireGuid(itemPath);

            string prefabGuid =
                RequireGuid(prefabPath);

            string largeGuid =
                RequireGuid(largePreviewPath);

            string catalogPreviewGuid =
                RequireGuid(catalogPreviewPath);

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        itemPath);

            Require(
                item != null,
                "Generated table item could not be loaded.");

            int originalPrice =
                item.PurchasePrice;

            Sprite originalCatalogIcon =
                item.CatalogIcon;

            Sprite originalInspectorPreview =
                item.InspectorPreview;

            Sprite manualPreviewOverride =
                FindExternalPreviewSprite(
                    manifest.canonicalContentId);

            Require(
                manualPreviewOverride != null,
                "No non-SAVIC preview sprite is available for override preservation testing.");

            int testPrice =
                originalPrice <= 999990
                    ? originalPrice + 7
                    : Math.Max(0, originalPrice - 7);

            try
            {
                SetManualOverrides(
                    item,
                    testPrice,
                    manualPreviewOverride,
                    manualPreviewOverride);

                SavicSourceProcessingOutcome second =
                    context.SourceProcessing.Process(
                        manifest);

                Require(
                    second.Succeeded,
                    "Second idempotence pass failed: " +
                    second.Message);

                context.Manifests.Reload();

                Require(
                    context.Manifests.TryGetBySavicId(
                        manifest.savicId,
                        out SavicManifest reloaded),
                    "Manifest could not be reloaded after second pass.");

                RestaurantPlaceableItemDefinition reloadedItem =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            itemPath);

                Require(
                    reloadedItem != null,
                    "Generated item disappeared after second pass.");

                Require(
                    reloadedItem.PurchasePrice ==
                    testPrice,
                    "Manual purchase-price override was overwritten by reprocessing.");

                Require(
                    string.Equals(
                        itemGuid,
                        RequireGuid(itemPath),
                        StringComparison.Ordinal),
                    "Item GUID changed during reprocessing.");

                Require(
                    string.Equals(
                        prefabGuid,
                        RequireGuid(prefabPath),
                        StringComparison.Ordinal),
                    "Prefab GUID changed during reprocessing.");

                Require(
                    string.Equals(
                        largeGuid,
                        RequireGuid(largePreviewPath),
                        StringComparison.Ordinal),
                    "Large preview GUID changed during reprocessing.");

                Require(
                    string.Equals(
                        catalogPreviewGuid,
                        RequireGuid(catalogPreviewPath),
                        StringComparison.Ordinal),
                    "Catalog preview GUID changed during reprocessing.");

                ValidateSingleCatalogEntry(
                    reloaded.canonicalContentId,
                    itemPath);

                ValidateUniqueManifestRecords(
                    reloaded);

                Require(
                    ReferenceEquals(
                        reloadedItem.CatalogIcon,
                        manualPreviewOverride) &&
                    ReferenceEquals(
                        reloadedItem.InspectorPreview,
                        manualPreviewOverride),
                    "Manual preview overrides were overwritten by reprocessing.");

                Debug.Log(
                    "[SAVIC] TABLE IDEMPOTENCE PROBE — PASS\n" +
                    "ContentId: " +
                    reloaded.canonicalContentId +
                    "\nItem GUID: " +
                    itemGuid +
                    "\nPrefab GUID: " +
                    prefabGuid +
                    "\nLarge preview GUID: " +
                    largeGuid +
                    "\nCatalog preview GUID: " +
                    catalogPreviewGuid +
                    "\nManual price preserved during reprocess: " +
                    testPrice +
                    " EUR");
            }
            finally
            {
                RestaurantPlaceableItemDefinition current =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            itemPath);

                if (current != null)
                {
                    RestoreManualOverrides(
                        current,
                        originalPrice,
                        originalCatalogIcon,
                        originalInspectorPreview);
                }
            }
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
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    candidate.tableAuthoring != null &&
                    candidate.tableAuthoring.planned)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ValidateSingleCatalogEntry(
            string contentId,
            string expectedItemPath)
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            Require(
                catalog != null,
                "Main catalog could not be loaded.");

            int matches = 0;

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition candidate =
                    items[index];

                if (candidate == null ||
                    !string.Equals(
                        candidate.ItemId,
                        contentId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matches++;

                Require(
                    string.Equals(
                        AssetDatabase.GetAssetPath(candidate),
                        expectedItemPath,
                        StringComparison.Ordinal),
                    "Catalog resolves the content id to the wrong item asset.");
            }

            Require(
                matches == 1,
                "Catalog contains " +
                matches +
                " entries for the same SAVIC content id.");
        }

        private static void ValidateUniqueManifestRecords(
            SavicManifest manifest)
        {
            HashSet<string> artifactRoles =
                new HashSet<string>(
                    StringComparer.Ordinal);

            if (manifest.artifacts != null)
            {
                for (int index = 0;
                     index < manifest.artifacts.Count;
                     index++)
                {
                    SavicArtifactRecord record =
                        manifest.artifacts[index];

                    if (record == null)
                        continue;

                    Require(
                        artifactRoles.Add(record.role),
                        "Manifest contains duplicate artifact role: " +
                        record.role);
                }
            }

            HashSet<string> validationIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            if (manifest.validations != null)
            {
                for (int index = 0;
                     index < manifest.validations.Count;
                     index++)
                {
                    SavicValidationRecord record =
                        manifest.validations[index];

                    if (record == null)
                        continue;

                    Require(
                        validationIds.Add(
                            record.validationId),
                        "Manifest contains duplicate validation id: " +
                        record.validationId);
                }
            }

            HashSet<string> decisionKeys =
                new HashSet<string>(
                    StringComparer.Ordinal);

            if (manifest.decisions != null)
            {
                for (int index = 0;
                     index < manifest.decisions.Count;
                     index++)
                {
                    SavicDecisionRecord record =
                        manifest.decisions[index];

                    if (record == null)
                        continue;

                    Require(
                        decisionKeys.Add(record.key),
                        "Manifest contains duplicate decision key: " +
                        record.key);
                }
            }
        }

        private static Sprite FindExternalPreviewSprite(
            string excludedContentId)
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
                return null;

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition candidate =
                    items[index];

                if (candidate == null ||
                    string.Equals(
                        candidate.ItemId,
                        excludedContentId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Sprite sprite =
                    candidate.CatalogIcon ??
                    candidate.InspectorPreview;

                if (sprite == null)
                    continue;

                string assetPath =
                    AssetDatabase.GetAssetPath(sprite);

                if (string.IsNullOrWhiteSpace(assetPath) ||
                    assetPath.StartsWith(
                        "Assets/Generated/BistroBuilder/SAVIC/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return sprite;
            }

            return null;
        }

        private static void SetManualOverrides(
            RestaurantPlaceableItemDefinition item,
            int price,
            Sprite catalogIcon,
            Sprite inspectorPreview)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            SerializedObject serialized =
                new SerializedObject(item);

            SerializedProperty priceProperty =
                serialized.FindProperty(
                    "purchasePrice");

            SerializedProperty catalogProperty =
                serialized.FindProperty(
                    "catalogIcon");

            SerializedProperty inspectorProperty =
                serialized.FindProperty(
                    "inspectorPreview");

            if (priceProperty == null ||
                catalogProperty == null ||
                inspectorProperty == null)
            {
                throw new InvalidOperationException(
                    "RestaurantPlaceableItemDefinition override fields changed.");
            }

            priceProperty.intValue =
                Math.Max(0, price);

            catalogProperty.objectReferenceValue =
                catalogIcon;

            inspectorProperty.objectReferenceValue =
                inspectorPreview;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        private static void RestoreManualOverrides(
            RestaurantPlaceableItemDefinition item,
            int price,
            Sprite catalogIcon,
            Sprite inspectorPreview)
        {
            SetManualOverrides(
                item,
                price,
                catalogIcon,
                inspectorPreview);
        }

        private static void SetPurchasePrice(
            RestaurantPlaceableItemDefinition item,
            int price)
        {
            SerializedObject serialized =
                new SerializedObject(item);

            SerializedProperty property =
                serialized.FindProperty(
                    "purchasePrice");

            if (property == null)
            {
                throw new InvalidOperationException(
                    "RestaurantPlaceableItemDefinition no longer exposes purchasePrice.");
            }

            property.intValue =
                Math.Max(0, price);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        private static string RequireGuid(
            string assetPath)
        {
            string guid =
                AssetDatabase.AssetPathToGUID(
                    assetPath);

            Require(
                !string.IsNullOrWhiteSpace(guid),
                "Asset has no GUID: " +
                assetPath);

            return guid;
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
