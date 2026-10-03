using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicOverheadEquipmentPublicationSelfTest
    {
        public static void RunQueueGuardsFromCommandLine()
        {
            string tempDirectory = Path.GetFullPath(Path.GetTempPath());
            string root = Path.GetFullPath(Path.Combine(tempDirectory, "SAVIC_OverheadQueue_" + Guid.NewGuid().ToString("N")));
            Require(root.StartsWith(tempDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), "Fixture must remain within its explicit temporary directory.");
            try
            {
                var layout = new SavicStorageLayout(root); layout.EnsureInfrastructure();
                var pending = new SavicJobRecord { jobId = "plan", manifestSavicId = "asset", sourceHash = "hash", batchEligible = true,
                    state = SavicJobState.NeedsReview.ToString(), reasonCode = "EQUIPMENT_FUNCTION_AMBIGUOUS", primaryStage = "FAMILY_PUBLICATION" };
                var accepted = new SavicJobRecord { jobId = "runtime", manifestSavicId = "accepted", sourceHash = "hash2", batchEligible = true,
                    state = SavicJobState.NeedsReview.ToString(), reasonCode = "OVERHEAD_RUNTIME_ACCEPTANCE_PENDING", primaryStage = "FAMILY_PUBLICATION" };
                var cancelled = new SavicJobRecord { jobId = "cancelled", manifestSavicId = "cancelled", sourceHash = "hash3", batchEligible = true,
                    cancelRequested = true, state = SavicJobState.NeedsReview.ToString(), reasonCode = "OVERHEAD_RUNTIME_ACCEPTANCE_PENDING", primaryStage = "FAMILY_PUBLICATION" };
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath, new SavicQueueSnapshot { schemaVersion = 1,
                    jobs = new List<SavicJobRecord> { pending, accepted, cancelled } });
                var store = new SavicJobStore(layout);
                Require(!store.RetryVerifiedOverheadReview("plan", "wrong", "hash", "plan", false) &&
                    !store.RetryVerifiedOverheadReview("plan", "asset", "wrong", "plan", false) &&
                    !store.RetryVerifiedOverheadReview("plan", "asset", "hash", "", false) &&
                    !store.RetryVerifiedOverheadReview("plan", "asset", "hash", "proof", true), "Queue accepted mismatched identity/source/phase or absent fingerprint.");
                Require(store.RetryVerifiedOverheadReview("plan", "asset", "hash", "plan", false) &&
                    !store.RetryVerifiedOverheadReview("plan", "asset", "hash", "plan", false), "Planner phase skipped canonical ingestion or repeated.");
                Require(!store.RetryVerifiedOverheadReview("runtime", "accepted", "hash2", "plan", false) &&
                    !store.RetryVerifiedOverheadReview("cancelled", "cancelled", "hash3", "proof", true) &&
                    !store.RetryVerifiedBarRuntimeAcceptance("runtime", "accepted", "hash2", "proof"), "Review phases/families/cancellation were confused.");
                Require(store.RetryVerifiedOverheadReview("runtime", "accepted", "hash2", "proof", true) &&
                    !store.RetryVerifiedOverheadReview("runtime", "accepted", "hash2", "proof", true) &&
                    store.Jobs.Where(j => j.jobId != "cancelled").All(j => j.state == SavicJobState.Ingested.ToString() && !j.sourcePrepared),
                    "Acceptance bypassed real processing or repeated.");
                var unknown = new SavicManifest { genericPlaceable = new SavicGenericPlaceableAuthoringRecord {
                    integrationMode = SavicOverheadEquipmentFunctionAdapter.Mode } };
                Require(SavicFunctionalRuntimeAcceptance.Required(unknown) && !SavicFunctionalRuntimeAcceptance.Matches(unknown, layout),
                    "Missing overhead runtime acceptance was accepted.");
                Debug.Log("[SAVIC] OVERHEAD QUEUE GUARDS - PASS: identity/source/phase/cancellation, no other-family bypass, ingestion and idempotence, missing proof rejected.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        public static void PublishVerifiedFromCommandLine()
        {
            RunQueueGuardsFromCommandLine();
            var context = SavicEditorContext.Instance;
            var records = context.Manifests.GetAll().Where(m => SavicOverheadEquipmentRuntimeAcceptance.Required(m)).ToArray();
            Require(records.Length > 0, "No accepted real overhead candidate.");
            foreach (var m in records)
            {
                Require(SavicOverheadEquipmentRuntimeAcceptance.Matches(m, context.Layout), "Real overhead acceptance is stale.");
                foreach (var change in new Action<SavicOverheadEquipmentRuntimeAcceptanceRecord>[] {
                    p => p.verifierVersion = "old", p => p.sourceHash = "wrong", p => p.planFingerprint = "wrong",
                    p => p.prefabDependencyHash = "wrong", p => p.reportHash = "wrong", p => p.reportRelativePath = "wrong",
                    p => p.catalogMode = "unknown", p => p.verifiedUtc = "", p => p.creationPassed = false,
                    p => p.provisionalIsolationPassed = false, p => p.nativeBindingPassed = false, p => p.areaCapabilityPassed = false,
                    p => p.routePassed = false, p => p.leasePassed = false, p => p.saveLoadPassed = false,
                    p => p.repeatedLoadPassed = false, p => p.cleanupPassed = false, p => p.consoleClean = false })
                {
                    var clone = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(m)); change(clone.overheadEquipmentRuntime);
                    Require(!SavicOverheadEquipmentRuntimeAcceptance.Matches(clone, context.Layout), "Stale/incomplete proof was accepted.");
                }
                var changedPlan = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(m)); changedPlan.overheadEquipment.installationBottomMeters -= 1f;
                Require(!SavicOverheadEquipmentRuntimeAcceptance.Matches(changedPlan, context.Layout), "Altered physical installation was accepted.");
            }
            SavicOverheadEquipmentRuntimePlaytest.PublishVerifiedFromCommandLine();
            Require(context.CanonicalReconciliation.RetryVerifiedOverheadReviews(4) == 0, "Published overhead candidate queued repeatedly.");
            Debug.Log("[SAVIC] OVERHEAD REAL ACCEPTANCE NEGATIVES - PASS: 18 stale/incomplete proofs, altered installation and completed queue idempotence.");
        }
        public static void VerifyFinalFromCommandLine()
        {
            SavicOverheadEquipmentNativeProbe.VerifyFromCommandLine();
            PublishVerifiedFromCommandLine();
            var context = SavicEditorContext.Instance;
            Require(context.Manifests.GetAll().Where(m => SavicOverheadEquipmentRuntimeAcceptance.Required(m)).All(m =>
                m.status == "PUBLISHED" && m.overheadEquipmentRuntime.catalogMode == "main" &&
                m.genericPlaceableReadiness.validated && m.genericPlaceableReadiness.catalogResolvable),
                "Published overhead item lacks strict main-catalog acceptance or canonical readiness.");
            SavicElevatedSpatialSelfTest.VerifyCanonicalFromCommandLine();
        }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
