using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    // Read-only source verification and visual evidence before new service/overhead authoring.
    public static class SavicPendingServiceSourceAudit
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Audit Pending Service Sources", false, 139)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            int audited = 0;
            foreach (SavicManifest manifest in context.Manifests.GetAll().Where(candidate => candidate?.status == "NEEDS_REVIEW" &&
                         (candidate.classification?.type == "Unknown" || candidate.classification?.type == "KitchenEquipment")))
            {
                string archive = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
                Require(context.Layout.ToProjectRelativePath(archive) == manifest.source.archivedRelativePath &&
                    HasHash(archive, manifest.source.sourceHash) && HasHash(mirror, manifest.source.sourceHash),
                    "Pending service source identity failed SHA-256 verification.");
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                Require(source != null, "Pending service source is unavailable in Unity.");
                string directory = "Assets/Generated/BistroBuilder/SAVIC/Diagnostics/SourceAudit/" + manifest.savicId;
                EnsureFolder(directory);
                RestaurantPlaceableItemDefinition probe = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
                try
                {
                    SavicPreviewGenerationResult preview = SavicPreviewRenderer.GenerateAndAssign(source, probe, directory);
                    Require(preview.Succeeded, preview.Message);
                    Debug.Log("[SAVIC] Verified pending service source preview: " + manifest.savicId + ", subject=" +
                        SavicProviderMetadataService.ResolveSemanticName(manifest, context.Layout) + ", source height=" +
                        manifest.model3D.heightMeters + "m, preview=" + preview.LargePreviewAssetPath +
                        ". Original and lifecycle preserved; this is geometry evidence, not publication.");
                    audited++;
                    string[] views = { "Bottom", "Front", "Back", "Right", "BottomFront" };
                    Vector3[] directions = { Vector3.down, Vector3.back, Vector3.forward, Vector3.right, new Vector3(0.5f, -0.8f, -1f) };
                    for (int index = 0; index < views.Length; index++)
                    {
                        string viewDirectory = directory + "/" + views[index];
                        EnsureFolder(viewDirectory);
                        SavicPreviewGenerationResult view = SavicPreviewRenderer.GenerateAndAssign(source, probe, viewDirectory, directions[index]);
                        Require(view.Succeeded, view.Message);
                        Debug.Log("[SAVIC] Verified source view " + views[index] + ": " + view.LargePreviewAssetPath);
                    }
                }
                finally { Object.DestroyImmediate(probe); }
            }
            Require(audited > 0, "No pending service source was available for visual audit.");
            Debug.Log("[SAVIC] PENDING SERVICE SOURCE AUDIT - PASS: " + audited + " verified originals rendered; publication states preserved.");
        }
        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
        private static bool HasHash(string path, string hash) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), hash, StringComparison.OrdinalIgnoreCase);
        private static void Require(bool success, string error) { if (!success) throw new InvalidOperationException(error); }
    }
}
