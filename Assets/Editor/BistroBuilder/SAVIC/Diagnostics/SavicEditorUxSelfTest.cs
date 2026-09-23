using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicEditorUxSelfTest
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Editor UX Self-Test",
            false,
            124)]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            ValidateReadModel();
            ValidateRepositorySignalsAndPersistedInventory();
            ValidateVirtualizationContract();

            Debug.Log(
                "[SAVIC] EDITOR UX SELF-TEST - PASS\n" +
                "Deterministic read model, filters, repository refresh " +
                "signals, persisted inventory and fixed-height " +
                "virtualization validated.");
        }

        private static void ValidateReadModel()
        {
            SavicEditorSnapshot empty =
                SavicEditorReadModel.Build(null, null, null);

            Require(empty.Assets.Count == 0, "Null manifest input is not safe.");
            Require(empty.Jobs.Count == 0, "Null job input is not safe.");
            Require(empty.Validations.Count == 0, "Null inventory input is not safe.");

            List<SavicManifest> manifests =
                new List<SavicManifest>
                {
                    CreateManifest(
                        "asset-published",
                        "table_alpha.glb",
                        "PUBLISHED",
                        "Furniture",
                        "Table",
                        "Furniture",
                        "2026-09-23T10:00:00Z",
                        new SavicValidationRecord
                        {
                            validationId = "Publication.TablePrefab",
                            result = "PASS",
                            severity = "INFO",
                            message = "Published",
                            validatorVersion = "1.0.0"
                        }),
                    CreateManifest(
                        "asset-review",
                        "chair_beta.fbx",
                        "NEEDS_REVIEW",
                        "Furniture",
                        "Chair",
                        "Furniture",
                        "2026-09-23T11:00:00Z",
                        new SavicValidationRecord
                        {
                            validationId = "Geometry.Chair",
                            result = "FAIL",
                            severity = "ERROR",
                            message = "Seat evidence is ambiguous.",
                            validatorVersion = "1.0.0"
                        }),
                    CreateManifest(
                        "asset-failed",
                        "broken_source.glb",
                        "FAILED_PROCESSING",
                        "Unknown",
                        "Unknown",
                        "Unknown",
                        "2026-09-23T12:00:00Z"),
                    CreateManifest(
                        "asset-stale",
                        "old_decoration.glb",
                        "STALE",
                        "Decoration",
                        "Decoration",
                        "Decoration",
                        "2026-09-22T09:00:00Z"),
                    CreateManifest(
                        "asset-autocorrected",
                        "chair_gamma.glb",
                        "AUTO_CORRECTED",
                        "Furniture",
                        "Chair",
                        "Furniture",
                        "2026-09-23T13:00:00Z")
                };

            List<SavicJobRecord> jobs =
                new List<SavicJobRecord>
                {
                    new SavicJobRecord
                    {
                        jobId = "job-waiting",
                        originalFileName = "incoming.glb",
                        state = SavicJobState.Waiting.ToString(),
                        createdUtc = "2026-09-23T14:00:00Z",
                        updatedUtc = "2026-09-23T14:00:00Z"
                    },
                    new SavicJobRecord
                    {
                        jobId = "job-failed",
                        originalFileName = "invalid.txt",
                        state = SavicJobState.FailedSource.ToString(),
                        message = "Unsupported source",
                        createdUtc = "2026-09-23T15:00:00Z",
                        updatedUtc = "2026-09-23T15:00:00Z"
                    }
                };

            SavicProjectInventorySnapshot inventory =
                new SavicProjectInventorySnapshot
                {
                    generatedUtc = "2026-09-23T16:00:00Z",
                    scannerVersion = "1.0.0",
                    totalItems = 3,
                    managedBySavic = 1,
                    legacyPendingAdoption = 2,
                    issueCount = 1,
                    issues = new List<SavicProjectInventoryIssueRecord>
                    {
                        new SavicProjectInventoryIssueRecord
                        {
                            code = "PREFAB_MISSING",
                            severity = "ERROR",
                            itemId = "legacy_missing_prefab",
                            assetPath = "Assets/Test/Legacy.asset",
                            message = "Placeable item has no prefab reference."
                        }
                    }
                };

            SavicEditorSnapshot first =
                SavicEditorReadModel.Build(
                    manifests,
                    jobs,
                    inventory);

            SavicEditorSnapshot second =
                SavicEditorReadModel.Build(
                    manifests.AsEnumerable().Reverse(),
                    jobs.AsEnumerable().Reverse(),
                    inventory);

            Require(first.Summary.TotalManaged == 5, "Managed total is incorrect.");
            Require(first.Summary.Passed == 2, "PASS total is incorrect.");
            Require(first.Summary.AutoCorrected == 1, "Autocorrect total is incorrect.");
            Require(first.Summary.Errors == 2, "Error total is incorrect.");
            Require(first.Summary.Stale == 1, "Stale total is incorrect.");
            Require(first.Summary.QueuedOrActive == 1, "Queue total is incorrect.");
            Require(first.Summary.InventoryIssues == 1, "Inventory issue total is incorrect.");
            Require(first.Summary.LegacyPendingAdoption == 2, "Legacy total is incorrect.");
            Require(first.Summary.NeedsReview == 3, "Review total is incorrect.");

            Require(
                first.Assets.Select(row => row.SavicId).SequenceEqual(
                    second.Assets.Select(row => row.SavicId)),
                "Asset ordering depends on input order.");

            Require(
                first.Jobs.Select(row => row.JobId).SequenceEqual(
                    second.Jobs.Select(row => row.JobId)),
                "Job ordering depends on input order.");

            Require(
                first.Reviews.Select(row => row.StableKey).SequenceEqual(
                    second.Reviews.Select(row => row.StableKey)),
                "Review ordering depends on input order.");

            List<SavicEditorAssetRow> chairs =
                SavicEditorReadModel.FilterAssets(
                    first.Assets,
                    string.Empty,
                    "Furniture",
                    "Furniture",
                    "Todos",
                    "Todos",
                    "Todos")
                .Where(row => row.Type == "Chair")
                .ToList();

            Require(chairs.Count == 2, "Library family/category filtering failed.");

            Require(
                SavicEditorReadModel.FilterAssets(
                    first.Assets,
                    string.Empty,
                    "Todos",
                    "Todos",
                    "PUBLISHED",
                    "Model3D · .glb",
                    SavicVersion.PipelineVersion).Count == 1,
                "Library status/origin/version filtering failed.");

            Require(
                SavicEditorReadModel.FilterAssets(
                    first.Assets,
                    "gamma",
                    "Todos",
                    "Todos",
                    "Todos",
                    "Todos",
                    "Todos").Count == 1,
                "Library text search failed.");

            Require(
                SavicEditorReadModel.FilterReviews(
                    first.Reviews,
                    "prefab",
                    "ERROR",
                    "INVENTARIO").Count == 1,
                "Review filters failed.");

            Require(
                SavicEditorReadModel.FilterJobs(
                    first.Jobs,
                    "incoming",
                    "Waiting").Count == 1,
                "Queue filters failed.");

            Require(
                first.Validations.Count == 3,
                "Manifest and inventory validations were not flattened.");

            Require(
                SavicEditorReadModel.FilterValidations(
                    first.Validations,
                    "chair",
                    "FAIL",
                    "ERROR",
                    "MANIFEST").Count == 1,
                "Validation filters failed.");

            Require(
                string.Equals(
                    first.Validations[0].Severity,
                    "ERROR",
                    StringComparison.Ordinal),
                "Validation severity ordering is incorrect.");

            Require(
                first.History.Count == manifests.Count + jobs.Count,
                "Unified history is incomplete.");

            Require(
                SavicEditorReadModel.FilterHistory(
                    first.History,
                    "incoming",
                    "JOB").Count == 1,
                "History filters failed.");
        }

        private static void ValidateRepositorySignalsAndPersistedInventory()
        {
            string sandboxRoot = Path.Combine(
                Path.GetTempPath(),
                "BistroBuilder_SAVIC_EditorUX_" +
                Guid.NewGuid().ToString("N"));

            try
            {
                SavicStorageLayout layout =
                    new SavicStorageLayout(sandboxRoot);

                layout.EnsureInfrastructure();

                SavicManifestRepository manifests =
                    new SavicManifestRepository(layout);

                SavicJobStore jobs =
                    new SavicJobStore(layout);

                int manifestChanges = 0;
                int jobChanges = 0;
                manifests.Changed += () => manifestChanges++;
                jobs.Changed += () => jobChanges++;

                SavicManifest manifest =
                    manifests.CreateIngested(
                        new string('a', 64),
                        "fixture.glb",
                        layout.GetArchivedSourcePath(
                            new string('a', 64),
                            "fixture.glb"),
                        128,
                        1,
                        SavicSourceKind.Model3D);

                Require(manifestChanges == 1, "Manifest save signal was not emitted once.");

                Require(
                    manifests.TryGetManifestPath(
                        manifest.savicId,
                        out string manifestPath) &&
                    File.Exists(manifestPath),
                    "Manifest path lookup failed.");

                jobs.RecordIngested(manifest, false, "fixture");
                Require(jobChanges == 1, "Job change signal was not emitted once.");

                SavicProjectInventorySnapshot persisted =
                    new SavicProjectInventorySnapshot
                    {
                        generatedUtc = "2026-09-23T16:00:00Z",
                        scannerVersion = "fixture",
                        totalItems = 7,
                        items = null,
                        issues = null
                    };

                SavicAtomicFile.WriteJson(
                    layout.ProjectInventorySnapshotPath,
                    persisted);

                SavicProjectInventoryService inventoryService =
                    new SavicProjectInventoryService(
                        layout,
                        manifests);

                Require(
                    inventoryService.TryLoadPersisted(
                        out SavicProjectInventorySnapshot loaded),
                    "Persisted inventory could not be loaded.");

                Require(loaded.totalItems == 7, "Persisted inventory data changed.");
                Require(loaded.items != null, "Persisted inventory items were not normalized.");
                Require(loaded.issues != null, "Persisted inventory issues were not normalized.");
            }
            finally
            {
                TryDeleteDirectory(sandboxRoot);
            }
        }

        private static void ValidateVirtualizationContract()
        {
            ListView list = new ListView();
            SavicEditorWindow.ConfigureVirtualizedList(list, 54f);
            SavicEditorWindow.ConfigureStandardRowUnbinding(list);

            Require(
                list.virtualizationMethod ==
                    CollectionVirtualizationMethod.FixedHeight,
                "SAVIC lists are not configured for fixed-height virtualization.");

            Require(
                Mathf.Approximately(list.fixedItemHeight, 54f),
                "SAVIC list item height is not deterministic.");

            Require(
                list.selectionType == SelectionType.Single,
                "SAVIC detail lists must use single selection.");

            Require(
                list.unbindItem != null,
                "SAVIC virtualized lists must explicitly unbind recycled rows.");
        }

        private static SavicManifest CreateManifest(
            string savicId,
            string sourceName,
            string status,
            string family,
            string type,
            string category,
            string updatedUtc,
            params SavicValidationRecord[] validations)
        {
            return new SavicManifest
            {
                savicId = savicId,
                canonicalContentId = "bb_" + savicId,
                status = status,
                family = family,
                type = type,
                category = category,
                createdUtc = updatedUtc,
                updatedUtc = updatedUtc,
                source = new SavicSourceRecord
                {
                    originalFileName = sourceName,
                    extension = Path.GetExtension(sourceName),
                    sourceKind = SavicSourceKind.Model3D.ToString(),
                    sourceHash = savicId
                },
                validations = validations?.ToList() ??
                              new List<SavicValidationRecord>()
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SAVIC] Editor UX self-test cleanup warning: " +
                    exception.Message);
            }
        }
    }
}
