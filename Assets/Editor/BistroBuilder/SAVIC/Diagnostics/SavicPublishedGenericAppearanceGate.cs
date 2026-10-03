using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicPublishedGenericAppearanceGate
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Validate/Repair Published Generic Appearance", false, 134)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Appearance verification requires a graphics device.");
            SavicEditorContext context = SavicEditorContext.Instance;
            SavicGenericPlaceablePublisher publisher = new SavicGenericPlaceablePublisher(context.Layout, context.Manifests);
            int checkedCount = 0, repaired = 0;
            foreach (SavicManifest manifest in context.Manifests.GetAll().Where(candidate =>
                         candidate != null && candidate.status == "PUBLISHED" && candidate.genericPlaceable?.planned == true))
            {
                checkedCount++;
                string path = manifest.genericPlaceable.prefabAssetPath;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Published generic prefab is absent: " + manifest.savicId);
                if (SavicSourceMaterialFallback.CountInvalidSlots(prefab) == 0) continue;
                if (manifest.source == null) throw new InvalidOperationException("Source identity is absent.");
                string archive = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
                if (context.Layout.ToProjectRelativePath(archive) != manifest.source.archivedRelativePath ||
                    !HasHash(archive, manifest.source.sourceHash) || !HasHash(mirror, manifest.source.sourceHash))
                    throw new InvalidOperationException("Generic source failed canonical SHA-256 verification: " + manifest.savicId);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                if (source == null) throw new InvalidOperationException("Generic Unity source cannot be loaded: " + manifest.savicId);
                SavicGenericPlaceablePublicationOutcome outcome = publisher.Publish(manifest, source);
                if (!outcome.Succeeded || SavicSourceMaterialFallback.CountInvalidSlots(
                        AssetDatabase.LoadAssetAtPath<GameObject>(path)) != 0)
                    throw new InvalidOperationException("Generic appearance repair failed: " + outcome.Message);
                repaired++;
            }
            if (checkedCount == 0) throw new InvalidOperationException("No published generic placeables were available.");
            Debug.Log("[SAVIC] PUBLISHED GENERIC APPEARANCE GATE - PASS: checked=" + checkedCount +
                      ", repaired=" + repaired + ", invalid material slots=0.");
        }

        public static void VerifyRegressionAndRepairFromCommandLine()
        {
            // RunFromMenu preserves this process so the real published outputs can then be verified.
            SavicV1ClosureGate.RunFromMenu();
            SavicPublishedChairAppearanceGate.RunFromCommandLine();
            RunFromCommandLine();
            RunFromCommandLine();
            SavicAutonomousClassificationAudit.ReconcileAndAuditFromCommandLine();
        }

        private static bool HasHash(string path, string expected) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), expected, StringComparison.OrdinalIgnoreCase);
    }
}
