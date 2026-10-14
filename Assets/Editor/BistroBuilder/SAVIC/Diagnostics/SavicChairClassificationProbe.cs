using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairClassificationProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Classification Probe",
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
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            Require(
                catalog != null,
                "Canonical placeable catalog could not be loaded.");

            int chairsChecked = 0;
            int negativesChecked = 0;

            List<string> failures =
                new List<string>();

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item == null ||
                    item.Prefab == null)
                {
                    continue;
                }

                SavicModelAnalysisRecord analysis =
                    SavicModelAnalyzer.Analyze(
                        item.Prefab.gameObject);

                bool expectedChair =
                    item.Category ==
                    RestaurantPlaceableItemCategory.Seating;

                SavicManifest generic =
                    BuildSyntheticManifest(
                        analysis,
                        "asset_" +
                        index.ToString(
                            CultureInfo.InvariantCulture) +
                        ".glb");

                SavicClassificationRecord genericClassification =
                    SavicContentClassifier.Classify(
                        generic);

                if (expectedChair)
                {
                    chairsChecked++;

                    if (!string.Equals(
                            genericClassification.type,
                            "Chair",
                            StringComparison.Ordinal) ||
                        !genericClassification.geometryBacked)
                    {
                        failures.Add(
                            item.ItemId +
                            " geometry-only => " +
                            genericClassification.type +
                            " score " +
                            genericClassification.score.ToString(
                                "0.000",
                                CultureInfo.InvariantCulture) +
                            "; " +
                            genericClassification.evidence);
                    }

                    SavicManifest explicitName =
                        BuildSyntheticManifest(
                            analysis,
                            "dining_chair_" +
                            index.ToString(
                                CultureInfo.InvariantCulture) +
                            ".glb");

                    SavicClassificationRecord explicitClassification =
                        SavicContentClassifier.Classify(
                            explicitName);

                    if (!string.Equals(
                            explicitClassification.type,
                            "Chair",
                            StringComparison.Ordinal))
                    {
                        failures.Add(
                            item.ItemId +
                            " explicit-name => " +
                            explicitClassification.type +
                            " score " +
                            explicitClassification.score.ToString(
                                "0.000",
                                CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    negativesChecked++;

                    if (string.Equals(
                            genericClassification.type,
                            "Chair",
                            StringComparison.Ordinal))
                    {
                        failures.Add(
                            item.ItemId +
                            " false positive => Chair score " +
                            genericClassification.score.ToString(
                                "0.000",
                                CultureInfo.InvariantCulture) +
                            "; " +
                            genericClassification.evidence);
                    }
                }
            }

            Require(
                chairsChecked >= 5,
                "Too few canonical chair assets were available for calibration.");

            Require(
                negativesChecked >= 3,
                "Too few non-chair canonical assets were available for calibration.");

            Require(
                failures.Count == 0,
                "Chair classifier regression(s): " +
                string.Join(
                    " | ",
                    failures));

            ValidateConflictingName(
                catalog,
                failures);

            Debug.Log(
                "[SAVIC] CHAIR CLASSIFICATION PROBE - PASS\n" +
                "Canonical chairs recognized geometry-only: " +
                chairsChecked +
                "\nCanonical non-chairs rejected: " +
                negativesChecked +
                "\nConflicting table/chair filename held for review: PASS");
        }

        private static void ValidateConflictingName(
            RestaurantPlaceableCatalogDefinition catalog,
            ICollection<string> failures)
        {
            RestaurantPlaceableItemDefinition chair =
                null;

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition candidate =
                    items[index];

                if (candidate != null &&
                    candidate.Prefab != null &&
                    candidate.Category ==
                    RestaurantPlaceableItemCategory.Seating)
                {
                    chair =
                        candidate;
                    break;
                }
            }

            Require(
                chair != null,
                "No chair exists for conflicting-name validation.");

            SavicModelAnalysisRecord analysis =
                SavicModelAnalyzer.Analyze(
                    chair.Prefab.gameObject);

            SavicClassificationRecord classification =
                SavicContentClassifier.Classify(
                    BuildSyntheticManifest(
                        analysis,
                        "chair_table_conflict.glb"));

            Require(
                !string.Equals(
                    classification.type,
                    "Chair",
                    StringComparison.Ordinal),
                "Conflicting chair/table filename was silently accepted as Chair.");

            Require(
                !string.Equals(
                    classification.type,
                    "Table",
                    StringComparison.Ordinal),
                "Conflicting chair/table filename was silently accepted as Table.");
        }

        private static SavicManifest BuildSyntheticManifest(
            SavicModelAnalysisRecord analysis,
            string fileName)
        {
            return new SavicManifest
            {
                source =
                    new SavicSourceRecord
                    {
                        sourceKind =
                            SavicSourceKind.Model3D.ToString(),
                        originalFileName =
                            fileName ?? string.Empty
                    },
                model3D =
                    analysis
            };
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
