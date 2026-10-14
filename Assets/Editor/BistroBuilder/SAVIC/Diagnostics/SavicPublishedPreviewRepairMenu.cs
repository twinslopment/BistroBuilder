using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicPublishedPreviewRepairMenu
    {
        private const string CatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string PublishedRoot =
            "Assets/Generated/BistroBuilder/SAVIC/Published/";

        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Repair Published Previews", false, 131)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Canonical placeable catalog is missing.");

            int repaired = 0;
            int current = 0;
            int rejected = 0;
            foreach (SavicManifest manifest in context.Manifests.GetAll()
                         .Where(candidate => candidate != null &&
                             string.Equals(candidate.status, "PUBLISHED", StringComparison.Ordinal))
                         .OrderBy(candidate => candidate.canonicalContentId, StringComparer.Ordinal))
            {
                SavicArtifactRecord large = Find(manifest, "preview.large");
                SavicArtifactRecord small = Find(manifest, "preview.catalog");
                if (large == null || small == null)
                    continue;
                if (string.Equals(large.builderVersion, SavicPreviewRenderer.Version,
                        StringComparison.Ordinal) &&
                    string.Equals(small.builderVersion, SavicPreviewRenderer.Version,
                        StringComparison.Ordinal))
                {
                    current++;
                    continue;
                }

                if (!TryRepair(context, catalog, manifest, large, small, out string reason))
                {
                    rejected++;
                    Debug.LogWarning("[SAVIC] Preview repair skipped " +
                                     manifest.canonicalContentId + ": " + reason);
                    continue;
                }
                repaired++;
            }

            Debug.Log("[SAVIC] Published preview repair: repaired=" + repaired +
                      ", already current=" + current + ", skipped=" + rejected + ".");
        }

        private static bool TryRepair(
            SavicEditorContext context,
            RestaurantPlaceableCatalogDefinition catalog,
            SavicManifest manifest,
            SavicArtifactRecord large,
            SavicArtifactRecord small,
            out string reason)
        {
            reason = string.Empty;
            SavicArtifactRecord itemRecord = Find(manifest, "catalog.item_definition");
            SavicArtifactRecord[] prefabs = manifest.artifacts
                .Where(record => record != null &&
                    record.role.StartsWith("published.", StringComparison.Ordinal) &&
                    record.role.EndsWith(".prefab", StringComparison.Ordinal))
                .ToArray();
            if (itemRecord == null || prefabs.Length != 1 ||
                !itemRecord.projectRelativePath.StartsWith(PublishedRoot,
                    StringComparison.Ordinal) ||
                !prefabs[0].projectRelativePath.StartsWith(PublishedRoot,
                    StringComparison.Ordinal))
            {
                reason = "Published item/prefab identity is incomplete.";
                return false;
            }

            string folder = Path.GetDirectoryName(itemRecord.projectRelativePath)
                ?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder) ||
                !string.Equals(Path.GetDirectoryName(prefabs[0].projectRelativePath)
                        ?.Replace('\\', '/'), folder, StringComparison.Ordinal) ||
                !string.Equals(large.projectRelativePath,
                    folder + "/Preview_Large.png", StringComparison.Ordinal) ||
                !string.Equals(small.projectRelativePath,
                    folder + "/Preview_Catalog.png", StringComparison.Ordinal))
            {
                reason = "Preview paths do not match the published item folder.";
                return false;
            }

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                    itemRecord.projectRelativePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabs[0].projectRelativePath);
            if (item == null || prefab == null ||
                !string.Equals(item.ItemId, manifest.canonicalContentId,
                    StringComparison.Ordinal) ||
                !catalog.TryGetItem(item.ItemId, out RestaurantPlaceableItemDefinition inCatalog) ||
                inCatalog != item || item.Prefab == null ||
                item.Prefab.gameObject != prefab)
            {
                reason = "Catalog item or prefab does not resolve to this SAVIC identity.";
                return false;
            }

            SavicPreviewGenerationResult result =
                SavicPreviewRenderer.GenerateAndAssign(prefab, item, folder);
            if (!result.Succeeded ||
                AssetDatabase.GetAssetPath(item.CatalogIcon) != small.projectRelativePath ||
                AssetDatabase.GetAssetPath(item.InspectorPreview) != large.projectRelativePath)
            {
                reason = "Isolated preview generation failed: " + result.Message;
                return false;
            }

            AssetDatabase.SaveAssets();
            string fingerprint = SavicPreviewRenderer.BuildInputFingerprint(
                prefabs[0].projectRelativePath);
            SavicManifestMutations.UpsertArtifact(manifest, "preview.large",
                large.projectRelativePath, "savic.preview-renderer",
                SavicPreviewRenderer.Version, fingerprint);
            SavicManifestMutations.UpsertArtifact(manifest, "preview.catalog",
                small.projectRelativePath, "savic.preview-renderer",
                SavicPreviewRenderer.Version, fingerprint);
            SavicManifestMutations.UpsertValidation(manifest, "Presentation.Previews",
                "PASS", "INFO", "Isolated published previews regenerated and assigned.",
                SavicPreviewRenderer.Version);
            context.Manifests.Save(manifest);
            return true;
        }

        private static SavicArtifactRecord Find(SavicManifest manifest, string role) =>
            manifest.artifacts?.FirstOrDefault(record => record != null &&
                string.Equals(record.role, role, StringComparison.Ordinal));
    }
}
