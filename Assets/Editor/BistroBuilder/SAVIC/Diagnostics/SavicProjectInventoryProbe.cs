using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicProjectInventoryProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Project Inventory Probe",
            false,
            116)]
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
            SavicEditorContext context =
                SavicEditorContext.Instance;

            SavicProjectInventorySnapshot snapshot =
                context.ProjectInventory.ScanAndPersist();

            Require(
                snapshot != null,
                "Project inventory scanner returned no snapshot.");

            Require(
                snapshot.totalItems > 0,
                "Project inventory scanner found no placeable item definitions.");

            string inventoryPath =
                Path.Combine(
                    context.Layout.CacheRoot,
                    "Inventory",
                    "project-inventory.json");

            Require(
                File.Exists(inventoryPath),
                "Project inventory snapshot was not persisted.");

            SavicProjectInventorySnapshot persisted =
                SavicAtomicFile.ReadJson
                    <SavicProjectInventorySnapshot>(
                        inventoryPath);

            Require(
                persisted != null &&
                persisted.totalItems ==
                    snapshot.totalItems,
                "Persisted project inventory does not match scan result.");

            SavicManifest publishedTable =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                publishedTable != null,
                "No published SAVIC table exists for inventory validation.");

            int managedMatches = 0;

            for (int index = 0;
                 index < snapshot.items.Count;
                 index++)
            {
                SavicProjectInventoryItemRecord item =
                    snapshot.items[index];

                if (item == null ||
                    !string.Equals(
                        item.itemId,
                        publishedTable.canonicalContentId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                managedMatches++;

                Require(
                    item.managedBySavic,
                    "Published SAVIC table is not marked as SAVIC-managed.");

                Require(
                    item.inMainCatalog,
                    "Published SAVIC table is not in the canonical catalog.");

                Require(
                    string.Equals(
                        item.adoptionState,
                        "MANAGED_BY_SAVIC",
                        StringComparison.Ordinal),
                    "Published SAVIC table has the wrong inventory adoption state.");

                Require(
                    string.Equals(
                        item.savicId,
                        publishedTable.savicId,
                        StringComparison.Ordinal),
                    "Published SAVIC table is linked to the wrong SAVIC identity.");

                Require(
                    !string.IsNullOrWhiteSpace(
                        item.prefabAssetPath) &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        item.prefabAssetPath) != null,
                    "Published SAVIC table inventory record has no usable prefab.");
            }

            Require(
                managedMatches == 1,
                "Project inventory resolved " +
                managedMatches +
                " records for the same published SAVIC table.");

            Debug.Log(
                "[SAVIC] PROJECT INVENTORY PROBE - PASS\n" +
                "Total items: " +
                snapshot.totalItems +
                "\nManaged by SAVIC: " +
                snapshot.managedBySavic +
                "\nLegacy pending adoption: " +
                snapshot.legacyPendingAdoption +
                "\nUnmanaged: " +
                snapshot.unmanaged +
                "\nInventory issues detected: " +
                snapshot.issueCount +
                "\nSnapshot: " +
                inventoryPath);
        }

        private static SavicManifest FindPublishedTable(
            System.Collections.Generic.IReadOnlyList
                <SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(
                        candidate.canonicalContentId))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
