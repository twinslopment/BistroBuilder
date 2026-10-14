using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicGeometryFalsePositiveProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Geometry False Positive Probe",
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
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            Require(
                catalog != null,
                "Canonical placeable catalog could not be loaded.");

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            int seatingChecked = 0;
            List<string> failures =
                new List<string>();

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item == null ||
                    item.Category !=
                        RestaurantPlaceableItemCategory.Seating ||
                    item.Prefab == null)
                {
                    continue;
                }

                seatingChecked++;

                SavicModelAnalysisRecord analysis =
                    SavicModelAnalyzer.Analyze(
                        item.Prefab.gameObject);

                SavicManifest synthetic =
                    new SavicManifest
                    {
                        source =
                            new SavicSourceRecord
                            {
                                sourceKind =
                                    SavicSourceKind.Model3D.ToString(),
                                originalFileName =
                                    "generic_asset_" +
                                    index +
                                    ".glb"
                            },
                        model3D =
                            analysis
                    };

                SavicClassificationRecord classification =
                    SavicContentClassifier.Classify(
                        synthetic);

                if (string.Equals(
                        classification.type,
                        "Table",
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        item.ItemId +
                        " => score " +
                        classification.score.ToString("0.000") +
                        "; " +
                        classification.evidence);
                }
            }

            Require(
                seatingChecked >= 3,
                "Too few canonical seating assets were available for the false-positive regression.");

            Require(
                failures.Count == 0,
                "Geometry-only classifier produced table false positives on seating assets: " +
                string.Join(" | ", failures));

            Debug.Log(
                "[SAVIC] GEOMETRY FALSE POSITIVE PROBE - PASS\n" +
                "Canonical seating assets checked: " +
                seatingChecked +
                "\nTable false positives: 0");
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
