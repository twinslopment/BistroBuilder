using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    [InitializeOnLoad]
    internal static class SavicEditorBootstrap
    {
        private const double TickIntervalSeconds = 0.35;
        private const double InventoryRefreshDebounceSeconds = 1.0;
        private const double InventoryRetryDelaySeconds = 5.0;

        private static double nextTickTime;
        private static double inventoryRefreshNotBefore;
        private static bool inventoryRefreshPending = true;
        private static bool initialized;
        private static string lastInitializationError = string.Empty;

        static SavicEditorBootstrap()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (initialized)
                return;

            try
            {
                SavicEditorContext.Instance.Layout.EnsureInfrastructure();
                new SavicSourceUpdateService(SavicEditorContext.Instance).RecoverInterrupted();
                SavicEditorContext.Instance.Batch.RecoverAfterDomainReload();

                SavicClassificationRefreshResult classificationRefresh =
                    SavicEditorContext.Instance.CanonicalReconciliation
                        .RefreshReviewedClassifications(16,
                            SavicEditorContext.Instance.SourceProcessing
                                .HasRegisteredFamily);
                if (classificationRefresh.Updated > 0 ||
                    classificationRefresh.Skipped > 0)
                    Debug.Log("[SAVIC] Reviewed classifications refreshed: " +
                              "updated=" + classificationRefresh.Updated +
                              ", queued=" + classificationRefresh.Queued +
                              ", skipped=" + classificationRefresh.Skipped + ".");

                // Preview generation needs a real graphics device. A headless
                // batch must not turn verified reviews into render failures.
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    int chairReviews = SavicEditorContext.Instance
                        .CanonicalReconciliation
                        .RetryVerifiedChairReviewsForCurrentPlanner(8);
                    int previewRetries = SavicEditorContext.Instance
                        .CanonicalReconciliation
                        .RetryVerifiedChairPreviewFailures(8);
                    int tableReviews = SavicEditorContext.Instance
                        .CanonicalReconciliation
                        .RetryVerifiedCompactSquareTableReviews(4);
                    int barAcceptances = SavicEditorContext.Instance.CanonicalReconciliation.RetryVerifiedBarRuntimeAcceptances(4);
                    int stoolAcceptances = SavicEditorContext.Instance.CanonicalReconciliation.RetryVerifiedBarStoolRuntimeAcceptances(4);
                    int overheadReviews = SavicEditorContext.Instance.CanonicalReconciliation.RetryVerifiedOverheadReviews(4);
                    if (overheadReviews > 0) Debug.Log("[SAVIC] Verified passive overhead source queued for canonical processing: " + overheadReviews + ".");
                    if (stoolAcceptances > 0) Debug.Log("[SAVIC] Verified bar stool runtime acceptance queued for canonical publication: " + stoolAcceptances + ".");
                    if (barAcceptances > 0) Debug.Log("[SAVIC] Verified bar runtime acceptance queued for canonical publication: " + barAcceptances + ".");
                    if (chairReviews + previewRetries > 0)
                        Debug.Log("[SAVIC] Verified chair revalidation queued: " +
                                  "planner=" + chairReviews +
                                  ", preview=" + previewRetries + ".");
                    if (tableReviews > 0)
                        Debug.Log("[SAVIC] Verified compact square table revalidation queued: " +
                                  tableReviews + ".");
                }

                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;

                EditorApplication.projectChanged -= OnProjectChanged;
                EditorApplication.projectChanged += OnProjectChanged;

                inventoryRefreshPending = true;
                inventoryRefreshNotBefore =
                    EditorApplication.timeSinceStartup;

                initialized = true;
                lastInitializationError = string.Empty;
            }
            catch (Exception exception)
            {
                lastInitializationError = exception.Message;
                Debug.LogError("[SAVIC] Initialization failed: " + exception);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!initialized ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextTickTime)
                return;

            nextTickTime = now + TickIntervalSeconds;

            try
            {
                SavicEditorContext.Instance.Intake.Tick();
            }
            catch (Exception exception)
            {
                Debug.LogError("[SAVIC] Intake tick failed safely: " + exception);
            }

            try
            {
                SavicEditorContext.Instance.Batch.TickOne();
            }
            catch (Exception exception)
            {
                Debug.LogError("[SAVIC] Batch tick failed safely: " + exception);
            }

            TryRefreshProjectInventory(now);
        }

        private static void OnProjectChanged()
        {
            inventoryRefreshPending = true;
            inventoryRefreshNotBefore =
                EditorApplication.timeSinceStartup +
                InventoryRefreshDebounceSeconds;
        }

        private static void TryRefreshProjectInventory(
            double now)
        {
            if (!inventoryRefreshPending ||
                now < inventoryRefreshNotBefore)
            {
                return;
            }

            try
            {
                SavicEditorContext.Instance
                    .ProjectInventory
                    .ScanAndPersist();

                inventoryRefreshPending = false;
            }
            catch (Exception exception)
            {
                inventoryRefreshNotBefore =
                    now +
                    InventoryRetryDelaySeconds;

                Debug.LogError(
                    "[SAVIC] Project inventory refresh failed safely: " +
                    exception);
            }
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Refresh Project Inventory", false, 2)]
        private static void RefreshProjectInventory()
        {
            Initialize();

            SavicProjectInventorySnapshot snapshot =
                SavicEditorContext.Instance
                    .ProjectInventory
                    .ScanAndPersist();

            inventoryRefreshPending = false;

            Debug.Log(
                "[SAVIC] Project inventory refreshed. " +
                "Items: " +
                snapshot.totalItems +
                ", managed: " +
                snapshot.managedBySavic +
                ", legacy pending adoption: " +
                snapshot.legacyPendingAdoption +
                ", issues: " +
                snapshot.issueCount +
                ".");
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Run Intake Scan", false, 1)]
        private static void RunIntakeScan()
        {
            Initialize();
            SavicEditorContext.Instance.Intake.Tick();

            Debug.Log(
                "[SAVIC] Intake scan requested. Inbox candidates: " +
                SavicEditorContext.Instance.Intake.GetInboxFileCount() + ".");
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Open DropHere Folder", false, 20)]
        private static void OpenDropHere()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            context.Layout.EnsureInfrastructure();
            EditorUtility.RevealInFinder(context.Layout.DropHereRoot);
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Open Source Archive", false, 21)]
        private static void OpenSourceArchive()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            context.Layout.EnsureInfrastructure();
            EditorUtility.RevealInFinder(context.Layout.ContentSourceRoot);
        }

        internal static string LastInitializationError => lastInitializationError;
    }
}
