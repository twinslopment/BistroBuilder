using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCompactSquareTableProbe
    {
        private const string ContractPath =
            "Assets/Resources/BistroBuilder/Spatial/Contracts/" +
            "BB_SpatialContract_Table_table_compact_2_rectangular.asset";

        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Compact Square Table Probe", false, 133)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicManifest candidate = SavicEditorContext.Instance.Manifests.GetAll()
                .FirstOrDefault(manifest => manifest?.model3D != null &&
                    (manifest.status == "NEEDS_REVIEW" ||
                     manifest.status == "PUBLISHED") &&
                    manifest.classification?.type == "Table" &&
                    SavicTableAuthoringPlanner.TryPlan(manifest,
                        out SavicTableAuthoringRecord plan, out _) &&
                    plan.seatingDefinitionAssetPath ==
                    SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath);
            Require(candidate != null,
                "No verified compact table candidate satisfies the calibrated plan.");
            Require(SavicTableAuthoringPlanner.TryPlan(candidate,
                    out SavicTableAuthoringRecord accepted, out string rejection),
                "Compact table plan failed: " + rejection);
            Require(accepted.capacity == 2 &&
                    accepted.finalWidthMeters >= 0.59f &&
                    accepted.finalDepthMeters >= 0.59f &&
                    accepted.finalHeightMeters >= 0.60f &&
                    accepted.finalHeightMeters <= 0.90f,
                "Compact table escaped physical seating bounds.");

            RestaurantTableSeatingConfigurationDefinition seating =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantTableSeatingConfigurationDefinition>(
                        accepted.seatingDefinitionAssetPath);
            string seatingError = "Seating definition is missing.";
            Require(seating != null &&
                    seating.Shape == RestaurantTableSeatingShape.Rectangular &&
                    seating.MaximumCustomers == 2 &&
                    seating.RectangularSeatCount == 2 &&
                    seating.ValidateConfiguration(accepted.finalWidthMeters,
                        accepted.finalDepthMeters, out seatingError),
                "Canonical opposite-side seating configuration rejected the table: " +
                seatingError);

            BistroBuilderSpatialContractDefinition contract =
                AssetDatabase.LoadAssetAtPath
                    <BistroBuilderSpatialContractDefinition>(ContractPath);
            string contractError = "BBSIS contract is missing.";
            Require(contract != null && contract.ValidateDefinition(out contractError) &&
                    contract.FamilyId == "seating.table" &&
                    contract.Ports.Count == 2,
                "Compact square table BBSIS contract is invalid: " + contractError);

            SavicManifest tooSmall = Clone(candidate);
            tooSmall.model3D.widthMeters = 1.20f;
            tooSmall.model3D.depthMeters = 1.20f;
            Require(!SavicTableAuthoringPlanner.TryPlan(tooSmall, out _, out _),
                "Undersized tabletop passed compact square authoring.");

            SavicManifest elongated = Clone(candidate);
            elongated.model3D.depthMeters = 1.00f;
            Require(!SavicTableAuthoringPlanner.TryPlan(elongated, out _, out _),
                "Elongated tabletop passed the compact square profile.");

            SavicManifest weak = Clone(candidate);
            weak.model3D.geometry.upperBandAreaRatio = 0.30f;
            Require(!SavicTableAuthoringPlanner.TryPlan(weak, out _, out _),
                "Weak tabletop geometry passed compact square authoring.");

            TestVerifiedRetry(candidate);

            Debug.Log("[SAVIC] COMPACT SQUARE TABLE PROBE - PASS: " +
                      "real candidate, two-seat opposite-side contract, BBSIS definition, " +
                      "undersize, elongation and weak-geometry rejection. " +
                      "SavicId=" + candidate.savicId +
                      ", width=" + accepted.finalWidthMeters.ToString("0.###") +
                      ", depth=" + accepted.finalDepthMeters.ToString("0.###") + ".");
        }

        private static void TestVerifiedRetry(SavicManifest candidate)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "BistroBuilder_SAVIC_CompactTable_" + Guid.NewGuid().ToString("N"));
            try
            {
                SavicStorageLayout layout = new SavicStorageLayout(root);
                layout.EnsureInfrastructure();
                const string name = "compact_square_table.glb";
                string original = Path.Combine(root, name);
                File.WriteAllBytes(original, new byte[] { 1, 2, 3, 4, 5, 6 });
                string hash = SavicHashService.ComputeSha256(original);
                string archive = layout.GetArchivedSourcePath(hash, name);
                Directory.CreateDirectory(Path.GetDirectoryName(archive));
                File.Copy(original, archive);

                SavicManifest manifest = Clone(candidate);
                manifest.savicId = Guid.NewGuid().ToString("N");
                manifest.status = "NEEDS_REVIEW";
                manifest.source.originalFileName = name;
                manifest.source.sourceHash = hash;
                manifest.source.sourceKind = SavicSourceKind.Model3D.ToString();
                manifest.source.archivedRelativePath =
                    layout.ToProjectRelativePath(archive);
                SavicManifestRepository repository =
                    new SavicManifestRepository(layout);
                repository.Save(manifest);

                SavicJobRecord review = new SavicJobRecord
                {
                    jobId = Guid.NewGuid().ToString("N"),
                    manifestSavicId = manifest.savicId,
                    sourceHash = hash,
                    originalFileName = name,
                    archivedRelativePath = manifest.source.archivedRelativePath,
                    state = SavicJobState.NeedsReview.ToString(),
                    reasonCode = "FAMILY_PUBLICATION_FAILED",
                    primaryStage = "FAMILY_PUBLICATION",
                    message = "Normalized dimensions are not safe for an automatic table profile.",
                    batchEligible = true
                };
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { review }
                    });
                SavicJobStore jobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService continuity =
                    new SavicCanonicalReconciliationService(layout, repository, jobs);

                File.WriteAllBytes(archive, new byte[] { 9, 9 });
                Require(continuity.RetryVerifiedCompactSquareTableReviews(4) == 0,
                    "Corrupt original was queued for table publication.");
                File.Copy(original, archive, true);
                Require(continuity.RetryVerifiedCompactSquareTableReviews(4) == 1 &&
                        jobs.Jobs[0].state == SavicJobState.Ingested.ToString() &&
                        jobs.Jobs[0].lastAutomaticTablePlannerRetryVersion ==
                        SavicTableAuthoringPlanner.Version,
                    "Verified compact square table was not queued once.");

                SavicJobRecord attempted = jobs.Jobs[0];
                attempted.state = SavicJobState.NeedsReview.ToString();
                attempted.reasonCode = "FAMILY_PUBLICATION_FAILED";
                attempted.message =
                    "Normalized dimensions are not safe for an automatic table profile.";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { attempted }
                    });
                SavicJobStore reloaded = new SavicJobStore(layout);
                Require(new SavicCanonicalReconciliationService(layout,
                            repository, reloaded)
                        .RetryVerifiedCompactSquareTableReviews(4) == 0,
                    "Planner revision retried the same review indefinitely.");

                manifest.status = "PUBLISHED";
                manifest.tableAuthoring = new SavicTableAuthoringRecord
                {
                    planned = true,
                    plannerVersion = "1.1.0",
                    seatingDefinitionAssetPath =
                        "Assets/Data/Restaurant/Seating/TableConfigurations/" +
                        "TableSeatingConfiguration_TableCompactRound2.asset"
                };
                repository.Save(manifest);
                attempted.state = SavicJobState.Done.ToString();
                attempted.reasonCode = "PUBLISHED";
                attempted.lastAutomaticTablePlannerRetryVersion = "1.1.0";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { attempted }
                    });
                SavicJobStore publishedJobs = new SavicJobStore(layout);
                SavicCanonicalReconciliationService update =
                    new SavicCanonicalReconciliationService(layout,
                        repository, publishedJobs);
                Require(update.RetryVerifiedCompactSquareTableReviews(4) == 1 &&
                        publishedJobs.Jobs[0].state == SavicJobState.Ingested.ToString() &&
                        publishedJobs.Jobs[0].lastAutomaticTablePlannerRetryVersion ==
                        SavicTableAuthoringPlanner.Version,
                    "Published table did not receive the corrected seating profile once.");

                SavicJobRecord updated = publishedJobs.Jobs[0];
                updated.state = SavicJobState.Done.ToString();
                updated.reasonCode = "PUBLISHED";
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath,
                    new SavicQueueSnapshot
                    {
                        schemaVersion = 1,
                        jobs = new List<SavicJobRecord> { updated }
                    });
                SavicJobStore completedJobs = new SavicJobStore(layout);
                Require(new SavicCanonicalReconciliationService(layout,
                            repository, completedJobs)
                        .RetryVerifiedCompactSquareTableReviews(4) == 0,
                    "Published table profile update repeated on reload.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static SavicManifest Clone(SavicManifest source)
        {
            return JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(source));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
