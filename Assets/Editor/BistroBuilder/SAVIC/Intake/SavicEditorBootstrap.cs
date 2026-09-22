using System;
using UnityEditor;
using UnityEngine;

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
