using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairAuthoringPlannerProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Authoring Planner Probe",
            false,
            126)]
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

            int chairsChecked =
                0;

            List<string> failures =
                new List<string>();

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            RestaurantPlaceableItemDefinition rotationSource =
                null;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item == null ||
                    item.Prefab == null ||
                    item.Category !=
                        RestaurantPlaceableItemCategory.Seating)
                {
                    continue;
                }

                rotationSource ??=
                    item;

                chairsChecked++;

                SavicManifest manifest =
                    AnalyzeAsGenericChair(
                        item.Prefab.gameObject,
                        "asset_" +
                        index.ToString(
                            CultureInfo.InvariantCulture) +
                        ".glb");

                bool planned =
                    SavicChairAuthoringPlanner.TryPlan(
                        manifest,
                        out SavicChairAuthoringRecord plan,
                        out string rejection);

                if (!planned)
                {
                    failures.Add(
                        item.ItemId +
                        " rejected: " +
                        rejection);
                    continue;
                }

                if (Math.Abs(
                        plan.visualYawDegrees) >
                        0.001f)
                {
                    failures.Add(
                        item.ItemId +
                        " canonical +Z chair unexpectedly requires yaw " +
                        plan.visualYawDegrees.ToString(
                            "0.#",
                            CultureInfo.InvariantCulture));
                }

                if (plan.finalSeatHeightMeters <
                        0.40f ||
                    plan.finalSeatHeightMeters >
                        0.52f)
                {
                    failures.Add(
                        item.ItemId +
                        " final seat height outside safe dining range.");
                }

                Debug.Log(
                    "[SAVIC][CHAIR_PLAN]" +
                    "|id=" + item.ItemId +
                    "|scale=" +
                    plan.uniformScale.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "|yaw=" +
                    plan.visualYawDegrees.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture) +
                    "|size=" +
                    plan.finalWidthMeters.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "x" +
                    plan.finalHeightMeters.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "x" +
                    plan.finalDepthMeters.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "|seat=" +
                    plan.finalSeatHeightMeters.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture));
            }

            Require(
                chairsChecked >= 5,
                "Too few canonical chairs were available for authoring calibration.");

            Require(
                failures.Count == 0,
                "Chair authoring planner regression(s): " +
                string.Join(
                    " | ",
                    failures));

            Require(
                rotationSource != null,
                "No chair exists for orientation normalization validation.");

            ValidateRotatedSource(
                rotationSource.Prefab.gameObject,
                90f,
                -90f);

            ValidateRotatedSource(
                rotationSource.Prefab.gameObject,
                -90f,
                90f);

            ValidateRotatedSource(
                rotationSource.Prefab.gameObject,
                180f,
                180f);

            Debug.Log(
                "[SAVIC] CHAIR AUTHORING PLANNER PROBE - PASS\n" +
                "Canonical chairs planned: " +
                chairsChecked +
                "\nCanonical +Z orientation: PASS" +
                "\n+X/-X/-Z source front normalization: PASS");
        }

        private static void ValidateRotatedSource(
            GameObject sourcePrefab,
            float visualRotationDegrees,
            float expectedPlannerYaw)
        {
            GameObject root =
                new GameObject(
                    "SAVIC_ChairPlannerRotationProbe");

            GameObject source =
                null;

            try
            {
                source =
                    UnityEngine.Object.Instantiate(
                        sourcePrefab);

                source.name =
                    "RotatedSource";

                source.transform.SetParent(
                    root.transform,
                    false);

                source.transform.localPosition =
                    Vector3.zero;

                source.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        visualRotationDegrees,
                        0f);

                source.transform.localScale =
                    Vector3.one;

                SavicManifest manifest =
                    AnalyzeAsGenericChair(
                        root,
                        "asset_rotated.glb");

                Require(
                    SavicChairAuthoringPlanner.TryPlan(
                        manifest,
                        out SavicChairAuthoringRecord plan,
                        out string rejection),
                    "Rotated chair source was rejected: " +
                    rejection);

                Require(
                    Math.Abs(
                        Mathf.DeltaAngle(
                            plan.visualYawDegrees,
                            expectedPlannerYaw)) <=
                    0.01f,
                    "Rotated chair yaw mismatch. Source rotation " +
                    visualRotationDegrees +
                    " expected planner yaw " +
                    expectedPlannerYaw +
                    " but got " +
                    plan.visualYawDegrees +
                    ".");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        private static SavicManifest AnalyzeAsGenericChair(
            GameObject root,
            string sourceName)
        {
            SavicModelAnalysisRecord model =
                SavicModelAnalyzer.Analyze(
                    root);

            SavicManifest manifest =
                new SavicManifest
                {
                    source =
                        new SavicSourceRecord
                        {
                            sourceKind =
                                SavicSourceKind.Model3D.ToString(),
                            originalFileName =
                                sourceName ?? string.Empty
                        },
                    model3D =
                        model
                };

            SavicClassificationRecord classification =
                SavicContentClassifier.Classify(
                    manifest);

            manifest.classification =
                classification;

            model.semanticParts =
                SavicSemanticPartAnalyzer.Analyze(
                    root,
                    model,
                    classification);

            return manifest;
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
