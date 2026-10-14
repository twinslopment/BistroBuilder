using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarStoolPublicationSelfTest
    {
        public static void FinalizeVerifiedFromCommandLine()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = SavicEditorContext.Instance;
            var accepted = context.Manifests.GetAll().Where(m => m?.type == "BarStool" &&
                SavicBarStoolRuntimeAcceptance.Required(m)).ToArray();
            Require(accepted.Length == 3, "Expected the three real stool candidates.");
            foreach (var m in accepted)
            {
                Require(SavicBarStoolRuntimeAcceptance.Matches(m, context.Layout), "Runtime proof is stale: " + m.savicId);
                foreach (var mutate in new Action<SavicBarStoolRuntimeAcceptanceRecord>[] {
                    p => p.sourceHash = "stale", p => p.planFingerprint = "stale", p => p.prefabDependencyHash = "stale",
                    p => p.customerPrefabDependencyHash = "stale", p => p.animationCatalogDependencyHash = "stale",
                    p => p.reportHash = "stale", p => p.creationPassed = false, p => p.associationPassed = false,
                    p => p.routePassed = false, p => p.leasePassed = false, p => p.seatedAnimationPassed = false,
                    p => p.saveLoadPassed = false, p => p.repeatedLoadPassed = false,
                    p => p.cleanupPassed = false, p => p.consoleClean = false })
                {
                    var clone = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(m));
                    mutate(clone.barStoolRuntime);
                    Require(!SavicBarStoolRuntimeAcceptance.Matches(clone, context.Layout), "Incomplete/stale proof accepted.");
                }
            }
            context.CanonicalReconciliation.RetryVerifiedBarStoolRuntimeAcceptances(16);
            int ticks = 0;
            while (ticks++ < 128 && context.Batch.TickOneIgnoringCooldownForDiagnostics()) { }
            foreach (var m in accepted)
            {
                Require(context.Manifests.TryGetBySavicId(m.savicId, out var current) && current.status == "PUBLISHED" &&
                    current.genericPlaceableReadiness.validated && current.genericPlaceableReadiness.catalogResolvable &&
                    SavicBarStoolRuntimeAcceptance.Matches(current, context.Layout),
                    "Canonical publication/catalog/proof failed for " + m.savicId);
            }
            Require(context.CanonicalReconciliation.RetryVerifiedBarStoolRuntimeAcceptances(16) == 0,
                "Completed stool publication was queued repeatedly.");
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] REAL BAR STOOL CANONICAL PUBLICATION - PASS: 3 native queue publications, 45 stale/incomplete proof negatives, actual catalog readiness, no repeated retries.");
        }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }

        public static void RunQueueGuardsFromCommandLine()
        {
            string root = Path.Combine(Path.GetTempPath(), "SAVIC_StoolAcceptance_" + Guid.NewGuid().ToString("N"));
            try
            {
                var layout = new SavicStorageLayout(root); layout.EnsureInfrastructure();
                var job = new SavicJobRecord { jobId = "stool", manifestSavicId = "asset", sourceHash = "hash", batchEligible = true,
                    state = SavicJobState.NeedsReview.ToString(), reasonCode = "BAR_STOOL_RUNTIME_ACCEPTANCE_PENDING", primaryStage = "FAMILY_PUBLICATION" };
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath, new SavicQueueSnapshot { schemaVersion = 1, jobs = new List<SavicJobRecord> { job } });
                var store = new SavicJobStore(layout);
                Require(!store.RetryVerifiedBarStoolRuntimeAcceptance("stool", "wrong", "hash", "proof") &&
                    !store.RetryVerifiedBarStoolRuntimeAcceptance("stool", "asset", "wrong", "proof") &&
                    !store.RetryVerifiedBarStoolRuntimeAcceptance("stool", "asset", "hash", ""), "Unverified identity/proof queued.");
                Require(!store.RetryVerifiedBarRuntimeAcceptance("stool", "asset", "hash", "proof"), "Bar/stool review contracts confused.");
                Require(store.RetryVerifiedBarStoolRuntimeAcceptance("stool", "asset", "hash", "proof") &&
                    store.Jobs[0].state == SavicJobState.Ingested.ToString() && !store.Jobs[0].sourcePrepared &&
                    !store.RetryVerifiedBarStoolRuntimeAcceptance("stool", "asset", "hash", "proof"), "Queue bypassed processing or repeated.");
                Debug.Log("[SAVIC] BAR STOOL ACCEPTANCE QUEUE SELF-TEST - PASS: identity, source, proof, family isolation, ingestion and idempotence.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        public static void VerifyAllFromCommandLine()
        {
            FinalizeVerifiedFromCommandLine();
            var context = SavicEditorContext.Instance;
            var publisher = new SavicGenericPlaceablePublisher(context.Layout, context.Manifests);
            Require(!SavicGenericPlaceablePublisher.CanReplaceGeneratedDescription("Texto manual de autoría", "Nuevo texto"),
                "Manual description would be overwritten.");
            int descriptions = 0;
            foreach (var m in context.Manifests.GetAll().Where(m => m?.status == "PUBLISHED" &&
                (m.type == "FloorLamp" || m.type == "StorageFurniture")))
            {
                string prefabHash = SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(m.genericPlaceable.prefabAssetPath));
                if (publisher.RefreshPublishedGeneratedDescription(m)) descriptions++;
                Require(!publisher.RefreshPublishedGeneratedDescription(m) &&
                    prefabHash == SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(m.genericPlaceable.prefabAssetPath)),
                    "Description repair changed the physical prefab or repeated.");
            }
            Debug.Log("[SAVIC] PUBLISHED GENERATED DESCRIPTION REFRESH - PASS: repaired=" + descriptions + ", repeated=0, physical prefabs unchanged, manual text preserved.");
            BistroBuilderAnimationV1SelfTest.Run();
            Require(BistroBuilderAnimationV1SelfTest.LastFailed == 0, BistroBuilderAnimationV1SelfTest.LastReport);
            BistroBuilderAdvancedCustomers10GSelfTest.RunFromCommandLine();
            SavicBarSeatBindingSelfTest.VerifyNativeAndCanonicalFromCommandLine();
        }
    }
}
