using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicGeometryClassificationProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Geometry Classification Probe",
            false,
            118)]
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

            SavicManifest publishedTable =
                FindPublishedOrKnownTable(
                    context.Manifests.GetAll());

            Require(
                publishedTable != null &&
                publishedTable.model3D != null &&
                publishedTable.model3D.geometry != null &&
                publishedTable.model3D.geometry.usable,
                "No analyzed SAVIC table geometry is available.");

            SavicManifest genericTable =
                BuildClassificationManifest(
                    "asset_000001.glb",
                    publishedTable.model3D);

            SavicClassificationRecord tableResult =
                SavicContentClassifier.Classify(
                    genericTable);

            Require(
                string.Equals(
                    tableResult.type,
                    "Table",
                    StringComparison.Ordinal),
                "Geometry-only table recognition failed.");

            Require(
                tableResult.score >= 0.78f,
                "Geometry-only table confidence is below the automatic authoring threshold.");

            genericTable.classification =
                tableResult;

            genericTable.family =
                tableResult.family;

            genericTable.type =
                tableResult.type;

            genericTable.category =
                tableResult.category;

            Require(
                SavicTableAuthoringPlanner.TryPlan(
                    genericTable,
                    out SavicTableAuthoringRecord genericPlan,
                    out string genericPlanRejection),
                "Geometry-only table did not reach automatic authoring: " +
                genericPlanRejection);

            Require(
                genericPlan.planned &&
                genericPlan.capacity > 0,
                "Geometry-only table authoring plan is incomplete.");

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            Require(
                catalog != null,
                "Main placeable catalog could not be loaded.");

            RestaurantPlaceableItemDefinition chair =
                FindItemByCategory(
                    catalog.Items,
                    RestaurantPlaceableItemCategory.Seating);

            RestaurantPlaceableItemDefinition decoration =
                FindItemByCategory(
                    catalog.Items,
                    RestaurantPlaceableItemCategory.Decoration);

            Require(
                chair != null &&
                chair.Prefab != null,
                "Calibration catalog has no usable seating prefab.");

            Require(
                decoration != null &&
                decoration.Prefab != null,
                "Calibration catalog has no usable decoration prefab.");

            SavicClassificationRecord genericChairResult =
                ClassifyPrefabWithGenericName(
                    chair,
                    "asset_000002.glb");

            SavicClassificationRecord misleadingChairResult =
                ClassifyPrefabWithGenericName(
                    chair,
                    "chair_table_000003.glb");

            SavicClassificationRecord decorationResult =
                ClassifyPrefabWithGenericName(
                    decoration,
                    "asset_000004.glb");

            Require(
                !string.Equals(
                    genericChairResult.type,
                    "Table",
                    StringComparison.Ordinal),
                "Generic chair produced a false-positive table classification.");

            Require(
                !string.Equals(
                    misleadingChairResult.type,
                    "Table",
                    StringComparison.Ordinal),
                "Conflicting chair/table filename bypassed geometry safety.");

            Require(
                !string.Equals(
                    decorationResult.type,
                    "Table",
                    StringComparison.Ordinal),
                "Decoration produced a false-positive table classification.");

            Debug.Log(
                "[SAVIC] GEOMETRY CLASSIFICATION PROBE - PASS\n" +
                "Generic table => " +
                tableResult.type +
                " / " +
                tableResult.score.ToString("0.###") +
                " / authoring capacity " +
                genericPlan.capacity +
                "\nGeneric chair => " +
                genericChairResult.type +
                " / " +
                genericChairResult.score.ToString("0.###") +
                "\nMisleading chair_table => " +
                misleadingChairResult.type +
                " / " +
                misleadingChairResult.score.ToString("0.###") +
                "\nGeneric decoration => " +
                decorationResult.type +
                " / " +
                decorationResult.score.ToString("0.###"));
        }

        private static SavicClassificationRecord
            ClassifyPrefabWithGenericName(
                RestaurantPlaceableItemDefinition item,
                string fileName)
        {
            SavicModelAnalysisRecord analysis =
                SavicModelAnalyzer.Analyze(
                    item.Prefab.gameObject);

            SavicManifest manifest =
                BuildClassificationManifest(
                    fileName,
                    analysis);

            return SavicContentClassifier.Classify(
                manifest);
        }

        private static SavicManifest BuildClassificationManifest(
            string fileName,
            SavicModelAnalysisRecord analysis)
        {
            return new SavicManifest
            {
                source =
                    new SavicSourceRecord
                    {
                        sourceKind =
                            SavicSourceKind.Model3D.ToString(),
                        originalFileName =
                            fileName
                    },
                model3D =
                    analysis
            };
        }

        private static RestaurantPlaceableItemDefinition
            FindItemByCategory(
                IReadOnlyList<RestaurantPlaceableItemDefinition> items,
                RestaurantPlaceableItemCategory category)
        {
            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item != null &&
                    item.Category == category &&
                    item.Prefab != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static SavicManifest FindPublishedOrKnownTable(
            IReadOnlyList<SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    candidate.model3D != null &&
                    candidate.model3D.geometry != null &&
                    candidate.model3D.geometry.usable)
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
