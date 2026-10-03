using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicPublishedChairAppearanceGate
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Validate/Repair Published Chair Appearance", false, 133)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Published chair appearance requires a graphics device.");

            SavicEditorContext context = SavicEditorContext.Instance;
            SavicChairPublisher publisher = new SavicChairPublisher(
                context.Layout, context.Manifests);
            int repaired = 0;
            int checkedChairs = 0;

            foreach (SavicManifest manifest in context.Manifests.GetAll())
            {
                if (manifest == null || manifest.status != "PUBLISHED" ||
                    manifest.type != "Chair")
                    continue;

                checkedChairs++;
                string prefabPath = manifest.chairAuthoring?.prefabAssetPath;
                string expectedFolder =
                    "Assets/Generated/BistroBuilder/SAVIC/Published/Chairs/" +
                    manifest.canonicalContentId + "/";
                if (string.IsNullOrWhiteSpace(prefabPath) ||
                    !prefabPath.StartsWith(expectedFolder, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Published chair prefab path is not canonical: " + manifest.savicId);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "Published chair prefab is missing: " + manifest.savicId);
                if (SavicChairPublisher.CountInvalidMaterialSlots(prefab) == 0)
                    continue;

                VerifySource(context.Layout, manifest);
                string mirror = context.Layout.GetUnitySourceMirrorPath(
                    manifest.source.sourceHash,
                    manifest.source.originalFileName);
                if (!File.Exists(mirror) ||
                    !SameSha256(mirror, manifest.source.sourceHash))
                    throw new InvalidOperationException(
                        "Published chair source mirror is absent or invalid: " + manifest.savicId);
                string mirrorAssetPath = context.Layout.ToProjectRelativePath(mirror);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(mirrorAssetPath);
                if (source == null)
                    throw new InvalidOperationException(
                        "Published chair source cannot be loaded by Unity: " + manifest.savicId);

                SavicChairPublicationOutcome outcome = publisher.Publish(manifest, source);
                if (!outcome.Succeeded)
                    throw new InvalidOperationException(
                        "Published chair appearance repair failed for " + manifest.savicId +
                        ": " + outcome.Message);
                GameObject updated = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (updated == null ||
                    SavicChairPublisher.CountInvalidMaterialSlots(updated) != 0)
                    throw new InvalidOperationException(
                        "Published chair still has invalid materials: " + manifest.savicId);
                repaired++;
            }

            if (checkedChairs == 0)
                throw new InvalidOperationException("No published chair exists to validate.");
            Debug.Log("[SAVIC] PUBLISHED CHAIR APPEARANCE GATE - PASS: " +
                      "checked=" + checkedChairs + ", repaired=" + repaired +
                      ", invalid material slots=0.");
        }

        private static void VerifySource(
            SavicStorageLayout layout, SavicManifest manifest)
        {
            if (manifest.source == null ||
                string.IsNullOrWhiteSpace(manifest.source.sourceHash) ||
                string.IsNullOrWhiteSpace(manifest.source.originalFileName))
                throw new InvalidOperationException(
                    "Published chair source identity is incomplete: " + manifest.savicId);
            string archive = layout.GetArchivedSourcePath(
                manifest.source.sourceHash,
                manifest.source.originalFileName);
            string recorded = layout.FromProjectRelativePath(
                manifest.source.archivedRelativePath);
            if (!string.Equals(archive, recorded, StringComparison.OrdinalIgnoreCase) ||
                !SameSha256(archive, manifest.source.sourceHash))
                throw new InvalidOperationException(
                    "Published chair archived source failed canonical SHA-256 verification: " +
                    manifest.savicId);
        }

        private static bool SameSha256(string path, string expected)
        {
            return File.Exists(path) &&
                   string.Equals(SavicHashService.ComputeSha256(path), expected,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
