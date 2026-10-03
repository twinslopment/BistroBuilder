using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairSemanticPartProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Semantic Part Probe",
            false,
            125)]
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
            int armSetsDetected = 0;

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
                    item.Prefab == null ||
                    item.Category !=
                        RestaurantPlaceableItemCategory.Seating)
                {
                    continue;
                }

                chairsChecked++;

                SavicModelAnalysisRecord model =
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
                                    "asset_" +
                                    index.ToString(
                                        CultureInfo.InvariantCulture) +
                                    ".glb"
                            },
                        model3D =
                            model
                    };

                SavicClassificationRecord classification =
                    SavicContentClassifier.Classify(
                        synthetic);

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        item.Prefab.gameObject,
                        model,
                        classification);

                SavicSemanticPartRecord seat =
                    FindPart(
                        semantic,
                        "chair.seat");

                SavicSemanticPartRecord back =
                    FindPart(
                        semantic,
                        "chair.back");

                SavicSemanticPartRecord support =
                    FindPart(
                        semantic,
                        "chair.support");

                SavicSemanticPartRecord arms =
                    FindPart(
                        semantic,
                        "chair.arms");

                if (arms != null)
                    armSetsDetected++;

                Debug.Log(
                    "[SAVIC][CHAIR_SEMANTIC]" +
                    "|id=" + item.ItemId +
                    "|ready=" + semantic.automationReady +
                    "|coverage=" +
                    semantic.semanticCoverage.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "|seat=" + PartLabel(seat) +
                    "|back=" + PartLabel(back) +
                    "|support=" + PartLabel(support) +
                    "|arms=" + PartLabel(arms) +
                    "|supportPattern=" +
                    (semantic.supportPattern?.mode ??
                     "NONE") +
                    "|zones=" +
                    (semantic.supportPattern?.zoneCount ?? 0) +
                    "|front=" +
                    model.chairGeometry.frontDirectionLocalX.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "," +
                    model.chairGeometry.frontDirectionLocalZ.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture));

                if (!string.Equals(
                        classification.type,
                        "Chair",
                        StringComparison.Ordinal) ||
                    !semantic.analyzed ||
                    !semantic.automationReady ||
                    seat == null ||
                    back == null ||
                    support == null)
                {
                    failures.Add(
                        item.ItemId +
                        " => type " +
                        classification.type +
                        ", ready " +
                        semantic.automationReady +
                        ", coverage " +
                        semantic.semanticCoverage.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture) +
                        "; " +
                        semantic.evidence);
                }

                if (Math.Abs(
                        model.chairGeometry.frontDirectionLocalX) >
                        0.001f ||
                    model.chairGeometry.frontDirectionLocalZ <
                        0.99f)
                {
                    failures.Add(
                        item.ItemId +
                        " => canonical front orientation drifted from +Z.");
                }
            }

            Require(
                chairsChecked >= 5,
                "Too few canonical chairs were available for semantic calibration.");

            Require(
                failures.Count == 0,
                "Chair semantic regression(s): " +
                string.Join(
                    " | ",
                    failures));

            ValidateSyntheticMultiMeshArmchair();

            Debug.Log(
                "[SAVIC] CHAIR SEMANTIC PART PROBE - PASS\n" +
                "Canonical chairs automation-ready: " +
                chairsChecked +
                "\nSeat/back/support recognized: PASS" +
                "\nOptional bilateral arm sets detected in canonical set: " +
                armSetsDetected +
                "\nSynthetic multi-mesh armchair grouping: PASS");
        }

        private static void ValidateSyntheticMultiMeshArmchair()
        {
            GameObject root =
                new GameObject(
                    "SyntheticMultiMeshArmchair");

            try
            {
                AddBox(
                    root.transform,
                    "Seat",
                    new Vector3(
                        0f,
                        0.47f,
                        0f),
                    new Vector3(
                        0.52f,
                        0.08f,
                        0.52f));

                AddBox(
                    root.transform,
                    "Back",
                    new Vector3(
                        0f,
                        0.72f,
                        -0.23f),
                    new Vector3(
                        0.52f,
                        0.48f,
                        0.08f));

                float[] signs =
                    { -1f, 1f };

                for (int xIndex = 0;
                     xIndex < signs.Length;
                     xIndex++)
                {
                    for (int zIndex = 0;
                         zIndex < signs.Length;
                         zIndex++)
                    {
                        AddBox(
                            root.transform,
                            "Leg_" +
                            xIndex +
                            "_" +
                            zIndex,
                            new Vector3(
                                signs[xIndex] *
                                0.20f,
                                0.23f,
                                signs[zIndex] *
                                0.20f),
                            new Vector3(
                                0.06f,
                                0.46f,
                                0.06f));
                    }
                }

                AddBox(
                    root.transform,
                    "Arm_Left",
                    new Vector3(
                        -0.27f,
                        0.61f,
                        0.02f),
                    new Vector3(
                        0.06f,
                        0.16f,
                        0.40f));

                AddBox(
                    root.transform,
                    "Arm_Right",
                    new Vector3(
                        0.27f,
                        0.61f,
                        0.02f),
                    new Vector3(
                        0.06f,
                        0.16f,
                        0.40f));

                SavicModelAnalysisRecord model =
                    SavicModelAnalyzer.Analyze(
                        root);

                SavicManifest synthetic =
                    new SavicManifest
                    {
                        source =
                            new SavicSourceRecord
                            {
                                sourceKind =
                                    SavicSourceKind.Model3D.ToString(),
                                originalFileName =
                                    "asset_synthetic_arm.glb"
                            },
                        model3D =
                            model
                    };

                SavicClassificationRecord classification =
                    SavicContentClassifier.Classify(
                        synthetic);

                Require(
                    string.Equals(
                        classification.type,
                        "Chair",
                        StringComparison.Ordinal),
                    "Synthetic armchair was not recognized as Chair from geometry.");

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        root,
                        model,
                        classification);

                SavicSemanticPartRecord arms =
                    FindPart(
                        semantic,
                        "chair.arms");

                SavicSemanticPartRecord support =
                    FindPart(
                        semantic,
                        "chair.support");

                Require(
                    semantic.automationReady,
                    "Synthetic armchair semantic analysis is not automation-ready: " +
                    semantic.evidence);

                Require(
                    arms != null &&
                    arms.sourceRegionCount >= 2,
                    "Two independent arm meshes were not grouped into one ArmSet.");

                Require(
                    support != null &&
                    support.sourceRegionCount >= 4,
                    "Independent leg meshes were not grouped into one support semantic part.");

                Require(
                    semantic.supportPattern != null &&
                    string.Equals(
                        semantic.supportPattern.mode,
                        "MULTI_CONTACT",
                        StringComparison.Ordinal) &&
                    semantic.supportPattern.zoneCount >= 4,
                    "Synthetic four-leg support pattern was not detected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        private static void AddBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject box =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            box.name =
                name;

            box.transform.SetParent(
                parent,
                false);

            box.transform.localPosition =
                localPosition;

            box.transform.localRotation =
                Quaternion.identity;

            box.transform.localScale =
                localScale;

            Collider collider =
                box.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    collider);
            }
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

        private static string PartLabel(
            SavicSemanticPartRecord part)
        {
            if (part == null)
                return "NONE";

            return
                part.role +
                "/" +
                part.confidence +
                "/" +
                part.areaFraction.ToString(
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
