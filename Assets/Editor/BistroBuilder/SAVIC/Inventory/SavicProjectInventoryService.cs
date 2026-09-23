using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicProjectInventoryService
    {
        internal const string Version = "1.0.0";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicProjectInventoryService(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal event Action Changed;

        internal SavicProjectInventorySnapshot ScanAndPersist()
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Canonical placeable catalog is missing: " +
                    MainCatalogPath);
            }

            HashSet<RestaurantPlaceableItemDefinition> catalogItems =
                new HashSet<RestaurantPlaceableItemDefinition>();

            IReadOnlyList<RestaurantPlaceableItemDefinition> catalogEntries =
                catalog.Items;

            for (int index = 0;
                 index < catalogEntries.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    catalogEntries[index];

                if (item != null)
                    catalogItems.Add(item);
            }

            Dictionary<string, string> savicIdByContentId =
                BuildSavicIdentityMap();

            string[] itemGuids =
                AssetDatabase.FindAssets(
                    "t:RestaurantPlaceableItemDefinition");

            Array.Sort(
                itemGuids,
                StringComparer.Ordinal);

            SavicProjectInventorySnapshot snapshot =
                new SavicProjectInventorySnapshot
                {
                    generatedUtc =
                        DateTime.UtcNow.ToString("O"),
                    scannerVersion =
                        Version
                };

            Dictionary<string, List<SavicProjectInventoryItemRecord>>
                byItemId =
                    new Dictionary<
                        string,
                        List<SavicProjectInventoryItemRecord>>(
                            StringComparer.Ordinal);

            for (int index = 0;
                 index < itemGuids.Length;
                 index++)
            {
                string assetGuid =
                    itemGuids[index];

                string itemPath =
                    AssetDatabase.GUIDToAssetPath(
                        assetGuid);

                RestaurantPlaceableItemDefinition item =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            itemPath);

                if (item == null)
                    continue;

                SavicProjectInventoryItemRecord record =
                    BuildRecord(
                        item,
                        assetGuid,
                        itemPath,
                        catalogItems.Contains(item),
                        savicIdByContentId);

                snapshot.items.Add(record);

                if (!byItemId.TryGetValue(
                        record.itemId,
                        out List<SavicProjectInventoryItemRecord> sameId))
                {
                    sameId =
                        new List<SavicProjectInventoryItemRecord>();

                    byItemId.Add(
                        record.itemId,
                        sameId);
                }

                sameId.Add(record);

                switch (record.adoptionState)
                {
                    case "MANAGED_BY_SAVIC":
                        snapshot.managedBySavic++;
                        break;

                    case "LEGACY_PENDING_ADOPTION":
                        snapshot.legacyPendingAdoption++;
                        break;

                    default:
                        snapshot.unmanaged++;
                        break;
                }

                AppendPerItemIssues(
                    snapshot,
                    item,
                    record);
            }

            AppendDuplicateIdIssues(
                snapshot,
                byItemId);

            snapshot.totalItems =
                snapshot.items.Count;

            snapshot.issueCount =
                snapshot.issues.Count;

            Persist(snapshot);
            NotifyChanged();
            return snapshot;
        }

        internal bool TryLoadPersisted(
            out SavicProjectInventorySnapshot snapshot)
        {
            try
            {
                snapshot = SavicAtomicFile.ReadJson
                    <SavicProjectInventorySnapshot>(
                        layout.ProjectInventorySnapshotPath);
            }
            catch (Exception exception)
            {
                snapshot = null;
                Debug.LogWarning(
                    "[SAVIC] Persisted inventory could not be read safely: " +
                    exception.Message);
                return false;
            }

            if (snapshot == null)
                return false;

            snapshot.items ??=
                new List<SavicProjectInventoryItemRecord>();

            snapshot.issues ??=
                new List<SavicProjectInventoryIssueRecord>();

            return true;
        }

        private void NotifyChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SAVIC] Inventory change listener failed safely: " +
                    exception);
            }
        }

        private Dictionary<string, string> BuildSavicIdentityMap()
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            IReadOnlyList<SavicManifest> all =
                manifests.GetAll();

            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                SavicManifest manifest =
                    all[index];

                if (manifest == null ||
                    string.IsNullOrWhiteSpace(
                        manifest.canonicalContentId) ||
                    string.IsNullOrWhiteSpace(
                        manifest.savicId))
                {
                    continue;
                }

                result[
                    manifest.canonicalContentId] =
                        manifest.savicId;
            }

            return result;
        }

        private static SavicProjectInventoryItemRecord BuildRecord(
            RestaurantPlaceableItemDefinition item,
            string assetGuid,
            string itemPath,
            bool inMainCatalog,
            IReadOnlyDictionary<string, string> savicIdByContentId)
        {
            string itemId =
                item.ItemId ?? string.Empty;

            string prefabPath =
                item.Prefab == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(
                        item.Prefab);

            string catalogIconPath =
                item.CatalogIcon == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(
                        item.CatalogIcon);

            string inspectorPreviewPath =
                item.InspectorPreview == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(
                        item.InspectorPreview);

            bool managed =
                IsSavicManaged(
                    item,
                    itemPath);

            string savicId =
                string.Empty;

            savicIdByContentId.TryGetValue(
                itemId,
                out savicId);

            if (!string.IsNullOrWhiteSpace(savicId))
                managed = true;

            string adoptionState =
                managed
                    ? "MANAGED_BY_SAVIC"
                    : inMainCatalog
                        ? "LEGACY_PENDING_ADOPTION"
                        : "UNMANAGED";

            return new SavicProjectInventoryItemRecord
            {
                assetGuid =
                    assetGuid ?? string.Empty,
                itemAssetPath =
                    itemPath ?? string.Empty,
                itemId =
                    itemId,
                displayName =
                    item.DisplayName ?? string.Empty,
                category =
                    item.Category.ToString(),
                placementScope =
                    item.PlacementScope.ToString(),
                prefabAssetPath =
                    prefabPath,
                prefabGuid =
                    string.IsNullOrWhiteSpace(prefabPath)
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(
                            prefabPath),
                catalogIconAssetPath =
                    catalogIconPath,
                inspectorPreviewAssetPath =
                    inspectorPreviewPath,
                dependencyHash =
                    AssetDatabase
                        .GetAssetDependencyHash(itemPath)
                        .ToString(),
                inMainCatalog =
                    inMainCatalog,
                managedBySavic =
                    managed,
                adoptionState =
                    adoptionState,
                savicId =
                    savicId ?? string.Empty
            };
        }

        private static bool IsSavicManaged(
            UnityEngine.Object asset,
            string assetPath)
        {
            if (asset != null)
            {
                string[] labels =
                    AssetDatabase.GetLabels(asset);

                for (int index = 0;
                     index < labels.Length;
                     index++)
                {
                    if (string.Equals(
                            labels[index],
                            "SAVIC.Managed",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return !string.IsNullOrWhiteSpace(assetPath) &&
                   assetPath.StartsWith(
                       "Assets/Generated/BistroBuilder/SAVIC/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendPerItemIssues(
            SavicProjectInventorySnapshot snapshot,
            RestaurantPlaceableItemDefinition item,
            SavicProjectInventoryItemRecord record)
        {
            if (string.IsNullOrWhiteSpace(
                    record.itemId))
            {
                AddIssue(
                    snapshot,
                    "ITEM_ID_EMPTY",
                    "ERROR",
                    record,
                    "Placeable item has no canonical ItemId.");
            }

            if (item.Prefab == null)
            {
                AddIssue(
                    snapshot,
                    "PREFAB_MISSING",
                    record.inMainCatalog
                        ? "ERROR"
                        : "WARNING",
                    record,
                    "Placeable item has no prefab reference.");
            }

            if (record.managedBySavic &&
                string.IsNullOrWhiteSpace(
                    record.savicId))
            {
                AddIssue(
                    snapshot,
                    "SAVIC_IDENTITY_UNLINKED",
                    "ERROR",
                    record,
                    "Asset is marked as SAVIC-managed but no manifest owns its ContentId.");
            }

            if (!record.managedBySavic &&
                !string.IsNullOrWhiteSpace(
                    record.savicId))
            {
                AddIssue(
                    snapshot,
                    "SAVIC_IDENTITY_LABEL_DRIFT",
                    "WARNING",
                    record,
                    "Manifest owns the ContentId but the asset is missing its SAVIC management marker.");
            }
        }

        private static void AppendDuplicateIdIssues(
            SavicProjectInventorySnapshot snapshot,
            IReadOnlyDictionary<
                string,
                List<SavicProjectInventoryItemRecord>> byItemId)
        {
            foreach (
                KeyValuePair<
                    string,
                    List<SavicProjectInventoryItemRecord>> pair in byItemId)
            {
                if (string.IsNullOrWhiteSpace(
                        pair.Key) ||
                    pair.Value == null ||
                    pair.Value.Count <= 1)
                {
                    continue;
                }

                for (int index = 0;
                     index < pair.Value.Count;
                     index++)
                {
                    SavicProjectInventoryItemRecord record =
                        pair.Value[index];

                    AddIssue(
                        snapshot,
                        "DUPLICATE_ITEM_ID",
                        "ERROR",
                        record,
                        "Canonical ItemId '" +
                        pair.Key +
                        "' is used by " +
                        pair.Value.Count +
                        " assets.");
                }
            }
        }

        private static void AddIssue(
            SavicProjectInventorySnapshot snapshot,
            string code,
            string severity,
            SavicProjectInventoryItemRecord record,
            string message)
        {
            snapshot.issues.Add(
                new SavicProjectInventoryIssueRecord
                {
                    code =
                        code ?? string.Empty,
                    severity =
                        severity ?? string.Empty,
                    itemId =
                        record?.itemId ?? string.Empty,
                    assetPath =
                        record?.itemAssetPath ?? string.Empty,
                    message =
                        message ?? string.Empty
                });
        }

        private void Persist(
            SavicProjectInventorySnapshot snapshot)
        {
            string directory =
                Path.GetDirectoryName(
                    layout.ProjectInventorySnapshotPath);

            Directory.CreateDirectory(
                directory);

            SavicAtomicFile.WriteJson(
                layout.ProjectInventorySnapshotPath,
                snapshot);
        }
    }
}
