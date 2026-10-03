using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicProviderMetadataSelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Provider Metadata Self-Test", false, 134)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "savic-provider-metadata-self-test-" + Guid.NewGuid().ToString("N")));
            try
            {
                SavicStorageLayout layout = new SavicStorageLayout(root);
                layout.EnsureInfrastructure();
                string download = Path.Combine(root, "Meshy_AI_Realistic_freestandin_0101000000_generate.glb");
                File.WriteAllBytes(download, new byte[] { 103, 108, 84, 70, 2, 0, 0, 0 });
                string hash = SavicHashService.ComputeSha256(download);
                string task = Guid.NewGuid().ToString("D");
                string originalUrl = "https://assets.meshy.ai/uploads/converted/" + task + "/" + Path.GetFileName(download);
                Require(SavicProviderMetadataService.TryResolveMeshyDownloadTask(originalUrl + "?Expires=123", out string parsed) &&
                    parsed == task, "Valid download task was not extracted without signed URL parameters.");
                Require(!SavicProviderMetadataService.TryResolveMeshyDownloadTask(originalUrl.Replace("assets.meshy.ai", "assets.meshy.ai.example.org"), out _) &&
                    !SavicProviderMetadataService.TryResolveMeshyDownloadTask(originalUrl.Replace("https:", "http:"), out _),
                    "A lookalike host or insecure URL supplied source provenance.");
                using (FileStream stream = SavicDownloadProvenance.OpenZoneIdentifier(download, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(stream))
                    writer.Write("[ZoneTransfer]\nHostUrl=" + originalUrl + "\n");
                string archive = layout.GetArchivedSourcePath(hash, Path.GetFileName(download));
                Directory.CreateDirectory(Path.GetDirectoryName(archive));
                File.Copy(download, archive);
                SavicManifest manifest = new SavicManifest
                {
                    savicId = Guid.NewGuid().ToString("N"), status = "NEEDS_REVIEW",
                    source = new SavicSourceRecord
                    {
                        originalFileName = Path.GetFileName(download), sourceHash = hash,
                        sourceKind = SavicSourceKind.Model3D.ToString(),
                        archivedRelativePath = layout.ToProjectRelativePath(archive)
                    },
                    model3D = new SavicModelAnalysisRecord
                    {
                        analyzed = true, hasUsableBounds = true,
                        widthMeters = 1.2f, heightMeters = 1.9f, depthMeters = 0.8f
                    }
                };
                SavicManifestRepository repository = new SavicManifestRepository(layout);
                repository.Save(manifest);
                SavicProviderMetadataInput input = new SavicProviderMetadataInput
                {
                    downloadedOriginalPath = download, taskId = task,
                    caption = "Realistic freestanding contemporary storage cabinet.", captionKind = "PROMPT_SUBJECT",
                    description = "Realistic freestanding contemporary storage cabinet. No table, no wall, no background scene.",
                    observedPreviewUrl = "https://api.meshy.ai/misc/cdn-images/test/tasks/" + task + "/output/preview.png"
                };
                input.taskId = Guid.NewGuid().ToString("D");
                ExpectRejected(() => SavicProviderMetadataService.AttachVerified(layout, repository, input),
                    "Metadata from another provider task was attached.");
                Require(string.IsNullOrEmpty(manifest.source.providerMetadataRelativePath) && manifest.status == "NEEDS_REVIEW",
                    "Rejected metadata modified source evidence or lifecycle.");
                input.taskId = task;
                Require(SavicProviderMetadataService.AttachVerified(layout, repository, input), "Verified metadata was not attached.");
                Require(SavicProviderMetadataService.TryReadVerified(layout, manifest.source, out _), "Attached evidence was not readable.");
                manifest.classification = SavicContentClassifier.Classify(manifest, layout);
                Require(manifest.classification.type == "StorageFurniture", "Verified cabinet became kitchen equipment, architecture or a table.");
                Require(SavicGenericPlaceableAuthoringPlanner.TryPlan(manifest, out SavicGenericPlaceableAuthoringRecord plan,
                    out _, out _, layout) && plan.category == "Furniture" && plan.integrationMode == "STATIC_FURNITURE" &&
                    string.IsNullOrEmpty(plan.requiredAreaCapabilityId), "Storage furniture was assigned a kitchen gameplay or area authority.");
                Require(!SavicProviderMetadataService.AttachVerified(layout, repository, input) &&
                    manifest.classification.classifierVersion == SavicContentClassifier.Version,
                    "Unchanged provider evidence repeatedly invalidated classification.");
                string metadataPath = layout.FromProjectRelativePath(manifest.source.providerMetadataRelativePath);
                File.AppendAllText(metadataPath, "\n ");
                Require(!SavicProviderMetadataService.TryReadVerified(layout, manifest.source, out _) &&
                    SavicContentClassifier.Classify(manifest, layout).type == "Unknown",
                    "Changed metadata continued to provide semantic identity.");
                Require(SavicProviderMetadataService.AttachVerified(layout, repository, input), "Verified evidence could not be restored.");
                string validPath = manifest.source.providerMetadataRelativePath;
                manifest.source.providerMetadataRelativePath = "../outside.json";
                Require(!SavicProviderMetadataService.TryReadVerified(layout, manifest.source, out _), "Outside-path metadata was accepted.");
                manifest.source.providerMetadataRelativePath = validPath;
                File.WriteAllBytes(archive, new byte[] { 99 });
                ExpectRejected(() => SavicProviderMetadataService.AttachVerified(layout, repository, input),
                    "Corrupt canonical original accepted provider metadata.");

                SavicManifest hood = new SavicManifest
                {
                    savicId = Guid.NewGuid().ToString("N"),
                    source = new SavicSourceRecord { originalFileName = "commercial_kitchen_exhaust_hood.glb",
                        sourceKind = SavicSourceKind.Model3D.ToString() },
                    model3D = new SavicModelAnalysisRecord
                    { analyzed = true, hasUsableBounds = true, widthMeters = 1.9f, heightMeters = 0.49f, depthMeters = 0.93f }
                };
                hood.classification = SavicContentClassifier.Classify(hood, layout);
                Require(hood.classification.type == "KitchenEquipment", "Explicit hood identity was not recognized.");
                Require(!SavicGenericPlaceableAuthoringPlanner.TryPlan(hood, out _, out string reason, out _, layout) &&
                    reason == "PLACEMENT_OVERHEAD_REQUIRES_ADAPTER", "An exhaust hood did not retain the overhead placement gate.");
                Debug.Log("[SAVIC] PROVIDER METADATA SELF-TEST - PASS: download task, SHA-256, subject isolation, cabinet contract, idempotence, corruption, path confinement and overhead hood gate.");
            }
            finally
            {
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(root).StartsWith("savic-provider-metadata-self-test-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Self-test cleanup target is outside its temporary scope.");
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ExpectRejected(Action action, string error)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException(error);
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }
    }
}
