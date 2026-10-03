using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicGeometryCalibrationProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Geometry Calibration Probe",
            false,
            117)]
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

            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Main placeable catalog could not be loaded.");
            }

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            int analyzed =
                0;

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

                GameObject root =
                    item.Prefab.gameObject;

                SavicModelAnalysisRecord analysis =
                    SavicModelAnalyzer.Analyze(
                        root);

                SavicGeometryProfileRecord geometry =
                    analysis.geometry;

                Debug.Log(
                    "[SAVIC][GEOMETRY_CALIBRATION]" +
                    "|id=" + item.ItemId +
                    "|name=" + Sanitize(item.DisplayName) +
                    "|category=" + item.Category +
                    "|size=" +
                    analysis.widthMeters.ToString("0.###") +
                    "x" +
                    analysis.heightMeters.ToString("0.###") +
                    "x" +
                    analysis.depthMeters.ToString("0.###") +
                    "|tri=" +
                    analysis.triangleCount +
                    "|up=" +
                    geometry.upwardFacingAreaRatio.ToString("0.###") +
                    "|horizontal=" +
                    geometry.horizontalAreaRatio.ToString("0.###") +
                    "|vertical=" +
                    geometry.verticalAreaRatio.ToString("0.###") +
                    "|upper=" +
                    geometry.upperBandAreaRatio.ToString("0.###") +
                    "|lower=" +
                    geometry.lowerBandAreaRatio.ToString("0.###") +
                    "|centroidY=" +
                    geometry.surfaceAreaCentroidHeight01.ToString("0.###") +
                    "|upperCoverage=" +
                    geometry.upperUpwardProjectedCoverage.ToString("0.###") +
                    "|lowerCoverage=" +
                    geometry.lowerHorizontalProjectedCoverage.ToString("0.###") +
                    "|material=" +
                    analysis.dominantMaterialSemantic +
                    "|materialConfidence=" +
                    analysis.dominantMaterialConfidence);

                analyzed++;
            }

            if (analyzed <= 0)
            {
                throw new InvalidOperationException(
                    "Geometry calibration found no catalog prefab to analyze.");
            }

            Debug.Log(
                "[SAVIC] GEOMETRY CALIBRATION PROBE - PASS\n" +
                "Catalog prefabs analyzed: " +
                analyzed);
        }

        private static string Sanitize(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value
                    .Replace("|", "/")
                    .Replace(
                        Environment.NewLine,
                        " ");
        }
    }
}
