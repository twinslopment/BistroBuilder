using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicSemanticPartCalibrationProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Semantic Part Calibration Probe",
            false,
            122)]
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

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            int positiveCount = 0;
            int negativeCount = 0;
            List<string> failures =
                new List<string>();
            List<string> observations =
                new List<string>();

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

                // Legacy table_basic prefabs are intentionally coarse
                // placeholder geometry (1 m cubes) and are not golden semantic
                // references for SAVIC's production-content gate.
                bool positive =
                    item.ItemId.StartsWith(
                        "bb_table_",
                        StringComparison.Ordinal);

                bool negative =
                    item.Category ==
                        RestaurantPlaceableItemCategory.Seating ||
                    item.Category ==
                        RestaurantPlaceableItemCategory.Decoration;

                if (!positive &&
                    !negative)
                {
                    continue;
                }

                SavicModelAnalysisRecord model =
                    SavicModelAnalyzer.Analyze(
                        item.Prefab.gameObject);

                SavicClassificationRecord forcedTable =
                    new SavicClassificationRecord
                    {
                        classified = true,
                        classifierVersion =
                            SavicContentClassifier.Version,
                        family = "Furniture",
                        type = "Table",
                        category = "Tables",
                        confidence = "HIGH",
                        score = 1f,
                        geometryBacked = true,
                        evidence =
                            "Semantic calibration force-table contract."
                    };

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        item.Prefab.gameObject,
                        model,
                        forcedTable);

                SavicSemanticPartRecord top =
                    FindPart(
                        semantic,
                        "table.top");

                SavicSemanticPartRecord support =
                    FindPart(
                        semantic,
                        "table.support");

                observations.Add(
                    item.ItemId +
                    " => ready=" +
                    semantic.automationReady +
                    ", dims=" +
                    model.widthMeters.ToString("0.000") +
                    "x" +
                    model.heightMeters.ToString("0.000") +
                    "x" +
                    model.depthMeters.ToString("0.000") +
                    ", top=" +
                    (top?.confidenceScore ?? 0f).ToString("0.000") +
                    " [size=" +
                    (top?.normalizedSizeX ?? 0f).ToString("0.000") +
                    "," +
                    (top?.normalizedSizeY ?? 0f).ToString("0.000") +
                    "," +
                    (top?.normalizedSizeZ ?? 0f).ToString("0.000") +
                    "; nY=" +
                    (top?.meanAbsoluteNormalY ?? 0f).ToString("0.000") +
                    "; area=" +
                    (top?.areaFraction ?? 0f).ToString("0.000") +
                    "], support=" +
                    (support?.confidenceScore ?? 0f).ToString("0.000") +
                    ", pattern=" +
                    (semantic.supportPattern?.mode ?? "NONE") +
                    "/" +
                    (semantic.supportPattern?.confidenceScore ?? 0f)
                        .ToString("0.000"));

                if (positive)
                {
                    positiveCount++;

                    if (!semantic.automationReady)
                    {
                        failures.Add(
                            "FALSE_NEGATIVE " +
                            item.ItemId +
                            ": " +
                            semantic.evidence);
                    }
                }

                if (negative)
                {
                    negativeCount++;

                    if (semantic.automationReady)
                    {
                        failures.Add(
                            "FALSE_POSITIVE " +
                            item.ItemId +
                            ": " +
                            semantic.evidence);
                    }
                }
            }

            Require(
                positiveCount >= 1,
                "Semantic calibration has no production-quality SAVIC table positive.");

            Require(
                negativeCount >= 5,
                "Semantic calibration has too few non-table negatives.");

            if (failures.Count > 0)
            {
                Debug.LogWarning(
                    "[SAVIC][SEMANTIC_CALIBRATION]\n" +
                    string.Join(
                        "\n",
                        observations));
            }

            Require(
                failures.Count == 0,
                "Semantic part calibration failed: " +
                string.Join(
                    " | ",
                    failures));

            Debug.Log(
                "[SAVIC] SEMANTIC PART CALIBRATION PROBE - PASS\n" +
                "Known tables accepted: " +
                positiveCount +
                "\nKnown non-tables rejected: " +
                negativeCount +
                "\n" +
                string.Join(
                    "\n",
                    observations));
        }

        private static SavicSemanticPartRecord FindPart(
            SavicSemanticPartAnalysisRecord semantic,
            string partId)
        {
            if (semantic?.parts == null)
                return null;

            for (int index = 0;
                 index < semantic.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord part =
                    semantic.parts[index];

                if (part != null &&
                    string.Equals(
                        part.partId,
                        partId,
                        StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    message);
        }
    }
}
