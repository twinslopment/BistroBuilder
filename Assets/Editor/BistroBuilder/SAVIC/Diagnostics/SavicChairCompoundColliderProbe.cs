using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairCompoundColliderProbe
    {
        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Compound Collider Probe",
            false,
            127)]
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

            int checkedCount =
                0;

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

                checkedCount++;

                GameObject instance =
                    UnityEngine.Object.Instantiate(
                        item.Prefab.gameObject);

                try
                {
                    instance.name =
                        "SAVIC_ChairColliderProbe_" +
                        index.ToString(
                            CultureInfo.InvariantCulture);

                    SavicManifest manifest =
                        AnalyzeChair(
                            instance,
                            "asset_" +
                            index.ToString(
                                CultureInfo.InvariantCulture) +
                            ".glb");

                    Require(
                        SavicChairAuthoringPlanner.TryPlan(
                            manifest,
                            out SavicChairAuthoringRecord plan,
                            out string rejection),
                        item.ItemId +
                        " authoring plan rejected: " +
                        rejection);

                    manifest.chairAuthoring =
                        plan;

                    SavicChairColliderAuthoringRecord record =
                        SavicChairColliderBuilder.Build(
                            instance,
                            manifest,
                            plan);

                    Require(
                        record.generated,
                        item.ItemId +
                        " collider generation did not complete.");

                    Require(
                        SavicChairColliderBuilder.Validate(
                            instance,
                            manifest,
                            plan,
                            record,
                            out string error),
                        item.ItemId +
                        " collider validation failed: " +
                        error);

                    Require(
                        record.seatColliderCount == 1 &&
                        record.backColliderCount == 1 &&
                        record.supportColliderCount >= 1,
                        item.ItemId +
                        " missing seat/back/support collider composition.");

                    Require(
                        instance
                            .GetComponentsInChildren<Collider>(true)
                            .Length ==
                        record.colliderCount,
                        item.ItemId +
                        " retained a legacy or untracked collider.");

                    Debug.Log(
                        "[SAVIC][CHAIR_COLLIDER]" +
                        "|id=" + item.ItemId +
                        "|strategy=" + record.strategy +
                        "|total=" + record.colliderCount +
                        "|support=" + record.supportColliderCount +
                        "|arms=" + record.armColliderCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(
                        instance);
                }
            }

            Require(
                checkedCount >= 5,
                "Too few canonical chairs were available for collider calibration.");

            Debug.Log(
                "[SAVIC] CHAIR COMPOUND COLLIDER PROBE - PASS\n" +
                "Canonical chairs validated: " +
                checkedCount +
                "\nLegacy full-body colliders removed: PASS\n" +
                "Seat/back/support semantic collider composition: PASS");
        }

        private static SavicManifest AnalyzeChair(
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
