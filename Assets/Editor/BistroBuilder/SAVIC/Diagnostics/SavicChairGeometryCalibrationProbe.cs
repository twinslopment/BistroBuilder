using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairGeometryCalibrationProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Geometry Calibration Probe",
            false,
            123)]
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

            int analyzed = 0;

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

                SavicChairGeometryProfileRecord chair =
                    analysis.chairGeometry;

                Debug.Log(
                    "[SAVIC][CHAIR_CALIBRATION]" +
                    "|id=" + item.ItemId +
                    "|category=" + item.Category +
                    "|size=" +
                    Format(analysis.widthMeters) + "x" +
                    Format(analysis.heightMeters) + "x" +
                    Format(analysis.depthMeters) +
                    "|usable=" + chair.usable +
                    "|seatY=" + Format(chair.seatHeight01) +
                    "|seatM=" + Format(chair.seatHeightMeters) +
                    "|seatCoverage=" + Format(chair.seatProjectedCoverage) +
                    "|seatArea=" + Format(chair.seatUpwardAreaRatio) +
                    "|upperVertical=" + Format(chair.upperVerticalAreaRatio) +
                    "|back=" + chair.backAxis + "/" + chair.backSide +
                    "|backBias=" + Format(chair.backEdgeBias) +
                    "|front=" +
                    Format(chair.frontDirectionLocalX) + "," +
                    Format(chair.frontDirectionLocalZ) +
                    "|lowerSupport=" + Format(chair.lowerSupportAreaRatio) +
                    "|confidence=" + Format(chair.confidenceScore));

                analyzed++;
            }

            Require(
                analyzed > 0,
                "Chair calibration found no canonical prefabs.");

            Debug.Log(
                "[SAVIC] CHAIR GEOMETRY CALIBRATION PROBE - PASS\n" +
                "Catalog prefabs analyzed: " +
                analyzed);
        }

        private static string Format(float value)
        {
            return value.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
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
