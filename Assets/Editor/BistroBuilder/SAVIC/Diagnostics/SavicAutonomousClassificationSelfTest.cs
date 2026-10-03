using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicAutonomousClassificationSelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Autonomous Classification Self-Test", false, 131)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicModelAnalysisRecord analysis = Analysis();
            Require(Classify("restaurant_bar_stool.glb", analysis).type == "BarStool",
                "Explicit bar stool was not recognized.");
            Require(Classify("restaurant_bar_stoo_123_generate.glb", analysis).type ==
                    "BarStool",
                "Truncated bar stool identity was not recognized.");
            Require(Classify("restaurant_stool.glb", analysis).type == "Stool",
                "Non-bar stool was not recognized.");
            Require(Classify("restaurant_bar_station.glb", analysis).type != "BarStool",
                "Bar context alone fabricated a stool identity.");
            Require(Classify("curved_bar.glb", analysis).type == "BarCounter" &&
                Classify("bar_floor_lamp.glb", analysis).type != "BarCounter" &&
                Classify("bar_cabinet.glb", analysis).type != "BarCounter",
                "Bar counter identity ignored a conflicting function.");
            Require(Classify("stool_table.glb", analysis).type != "Stool",
                "Conflicting table identity was ignored.");
            Require(Classify("stoo_123.glb", analysis).type != "Stool",
                "A truncated fragment without bar context was accepted.");

            string root = Path.Combine(Path.GetTempPath(),
                "BistroBuilder_SAVIC_AutonomousClass_" + Guid.NewGuid().ToString("N"));
            try
            {
                string legacyRoot = Path.Combine(root, "legacy");
                string doorFolder = Path.Combine(legacyRoot, "KitchenDoors");
                Directory.CreateDirectory(doorFolder);
                string doorPath = Path.Combine(doorFolder,
                    "restaurant_kitchen_sw.glb");
                File.WriteAllBytes(doorPath, new byte[] { 12, 34, 56 });
                SavicSourceRecord doorSource = new SavicSourceRecord
                {
                    originalFileName = Path.GetFileName(doorPath),
                    sourceHash = SavicHashService.ComputeSha256(doorPath)
                };
                Require(SavicVerifiedProjectSourceContext.TryResolveUnderRoot(
                        doorSource, legacyRoot, "Assets/Legacy", out string folder,
                        out string relativePath) && folder == "KitchenDoors" &&
                        relativePath.EndsWith("/restaurant_kitchen_sw.glb",
                            StringComparison.Ordinal),
                    "Byte-verified source folder provenance was not resolved.");
                SavicModelAnalysisRecord doorAnalysis = Analysis();
                doorAnalysis.widthMeters = 0.82f;
                doorAnalysis.heightMeters = 1.90f;
                doorAnalysis.depthMeters = 0.17f;
                doorAnalysis.geometry = new SavicGeometryProfileRecord
                {
                    analyzed = true,
                    verticalAreaRatio = 0.91f,
                    upwardFacingAreaRatio = 0.04f
                };
                HashSet<string> swingTokens = new HashSet<string>(
                    new[] { "restaurant", "kitchen", "sw" },
                    StringComparer.OrdinalIgnoreCase);
                Require(SavicContentClassifier.TryClassifyVerifiedProjectDoor(
                        swingTokens, doorAnalysis, folder, relativePath,
                        new SavicClassificationRecord()) &&
                    !SavicContentClassifier.TryClassifyVerifiedProjectDoor(
                        swingTokens, doorAnalysis, "KitchenWindows", relativePath,
                        new SavicClassificationRecord()),
                    "Verified folder and door geometry did not gate identity.");
                Require(!SavicContentClassifier.TryClassifyVerifiedProjectDoor(
                        new HashSet<string>(new[] { "restaurant", "kitchen" }),
                        doorAnalysis, folder, relativePath,
                        new SavicClassificationRecord()),
                    "Folder context alone fabricated a door identity.");
                doorAnalysis.geometry.verticalAreaRatio = 0.2f;
                Require(!SavicContentClassifier.TryClassifyVerifiedProjectDoor(
                        swingTokens, doorAnalysis, folder, relativePath,
                        new SavicClassificationRecord()),
                    "Non-door geometry was accepted as a door.");
                string conflictingFolder = Path.Combine(legacyRoot, "Windows");
                Directory.CreateDirectory(conflictingFolder);
                string conflictingCopy = Path.Combine(conflictingFolder,
                    doorSource.originalFileName);
                File.Copy(doorPath, conflictingCopy);
                Require(!SavicVerifiedProjectSourceContext.TryResolveUnderRoot(
                        doorSource, legacyRoot, "Assets/Legacy", out _, out _),
                    "Conflicting byte-identical source folders supplied identity.");
                File.Delete(conflictingCopy);
                File.WriteAllBytes(doorPath, new byte[] { 99 });
                Require(!SavicVerifiedProjectSourceContext.TryResolveUnderRoot(
                        doorSource, legacyRoot, "Assets/Legacy", out _, out _),
                    "A changed project copy supplied semantic provenance.");

                SavicStorageLayout layout = new SavicStorageLayout(root);
                layout.EnsureInfrastructure();
                SavicManifestRepository manifests = new SavicManifestRepository(layout);
                List<SavicJobRecord> records = new List<SavicJobRecord>
                {
                    CreateReview(layout, manifests, "bar_stool.glb", 1),
                    CreateReview(layout, manifests, "dining_chair.glb", 2),
                    CreateReview(layout, manifests, "other_stool.glb", 3),
                    CreateReview(layout, manifests, "commercial_kitchen_exhaust_hood.glb", 4)
                };
                records[3].reasonCode = "EQUIPMENT_FUNCTION_AMBIGUOUS";
                records[3].primaryStage = "FAMILY_PUBLICATION";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot { schemaVersion = 1, jobs = records });
                SavicJobStore jobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService reconciliation =
                    new SavicCanonicalReconciliationService(layout, manifests, jobs);

                string corruptArchive = layout.GetArchivedSourcePath(
                    records[2].sourceHash, records[2].originalFileName);
                File.WriteAllBytes(corruptArchive, new byte[] { 99 });
                SavicClassificationRefreshResult first =
                    reconciliation.RefreshReviewedClassifications(8,
                        type => type == "Chair" || type == "KitchenEquipment");
                Require(first.Updated == 3 && first.Queued == 1 && first.Skipped == 1,
                    "Verified refresh did not distinguish unsupported, publishable and corrupt sources.");
                Require(jobs.Jobs[0].state == SavicJobState.NeedsReview.ToString() &&
                        jobs.Jobs[1].state == SavicJobState.Ingested.ToString() &&
                        jobs.Jobs[2].state == SavicJobState.NeedsReview.ToString(),
                    "Classification refresh changed the wrong queue states.");
                Require(jobs.Jobs[3].state == SavicJobState.NeedsReview.ToString() &&
                        jobs.Jobs[3].reasonCode == "EQUIPMENT_FUNCTION_AMBIGUOUS" &&
                        manifests.TryGetBySavicId(records[3].manifestSavicId, out SavicManifest hood) &&
                        hood.classification.type == "KitchenEquipment",
                    "Refreshing provider identity cleared a functional publication review.");
                Require(manifests.TryGetBySavicId(records[0].manifestSavicId,
                            out SavicManifest stool) &&
                        stool.classification.type == "BarStool" &&
                        stool.status == "NEEDS_REVIEW",
                    "Stool identity was not retained separately from publication readiness.");
                SavicClassificationRefreshResult second =
                    reconciliation.RefreshReviewedClassifications(8,
                        type => type == "Chair" || type == "BarStool");
                Require(second.Updated == 0 && second.Queued == 1 &&
                        jobs.Jobs[0].state == SavicJobState.Ingested.ToString(),
                    "A newly registered family did not retry a current verified classification.");
                SavicClassificationRefreshResult third =
                    reconciliation.RefreshReviewedClassifications(8,
                        type => type == "Chair" || type == "BarStool");
                Require(third.Updated == 0 && third.Queued == 0,
                    "The same classifier revision repeated a retry.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }

            Debug.Log("[SAVIC] AUTONOMOUS CLASSIFICATION SELF-TEST - PASS: " +
                      "stool identity, verified project door context, conflicts, " +
                      "verified refresh, idempotence and source integrity.");
        }

        private static SavicClassificationRecord Classify(
            string name, SavicModelAnalysisRecord analysis)
        {
            return SavicContentClassifier.Classify(new SavicManifest
            {
                source = new SavicSourceRecord
                {
                    sourceKind = SavicSourceKind.Model3D.ToString(),
                    originalFileName = name
                },
                model3D = analysis
            });
        }

        private static SavicJobRecord CreateReview(
            SavicStorageLayout layout, SavicManifestRepository manifests,
            string name, byte marker)
        {
            string source = Path.Combine(layout.ProjectRoot, name);
            File.WriteAllBytes(source, new[] { marker });
            string hash = SavicHashService.ComputeSha256(source);
            string archive = layout.GetArchivedSourcePath(hash, name);
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            File.Copy(source, archive);
            SavicManifest manifest = new SavicManifest
            {
                savicId = Guid.NewGuid().ToString("N"),
                status = "NEEDS_REVIEW",
                source = new SavicSourceRecord
                {
                    sourceHash = hash,
                    sourceKind = SavicSourceKind.Model3D.ToString(),
                    originalFileName = name,
                    archivedRelativePath = layout.ToProjectRelativePath(archive)
                },
                classification = new SavicClassificationRecord
                {
                    classified = true,
                    classifierVersion = "previous",
                    type = "Unknown"
                },
                model3D = Analysis()
            };
            manifests.Save(manifest);
            return new SavicJobRecord
            {
                jobId = Guid.NewGuid().ToString("N"),
                manifestSavicId = manifest.savicId,
                sourceHash = hash,
                originalFileName = name,
                archivedRelativePath = manifest.source.archivedRelativePath,
                state = SavicJobState.NeedsReview.ToString(),
                reasonCode = "UNSUPPORTED_PUBLICATION_FAMILY",
                primaryStage = "CLASSIFICATION",
                batchEligible = true
            };
        }

        private static SavicModelAnalysisRecord Analysis()
        {
            return new SavicModelAnalysisRecord
            {
                analyzed = true,
                hasUsableBounds = true,
                widthMeters = 0.55f,
                heightMeters = 0.95f,
                depthMeters = 0.55f,
                chairGeometry = new SavicChairGeometryProfileRecord
                {
                    analyzed = true,
                    usable = true,
                    confidenceScore = 0.83f
                }
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
