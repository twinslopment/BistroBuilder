using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicChairVerticalProbe
    {
        private const string SourceModelPath =
            "Assets/Art/Blender/Placeables/Furniture/Chairs/" +
            "BB_Chair_Master_002/Models/BB_Chair_Master_002.fbx";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string DiagnosticSavicId =
            "diagnosticchairverticalprobe";

        private const string DiagnosticContentId =
            "bb_chair_diagnosticchairverticalprobe";

        private const string DiagnosticContentFolder =
            "Assets/Generated/BistroBuilder/SAVIC/Published/Chairs/" +
            DiagnosticContentId;

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Chair Vertical Probe",
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
            SavicEditorContext context =
                SavicEditorContext.Instance;

            CleanupDiagnosticResidue(
                context);

            string itemPath =
                DiagnosticContentFolder +
                "/PlaceableItem_" +
                DiagnosticContentId +
                ".asset";

            string prefabPath =
                DiagnosticContentFolder +
                "/Chair_" +
                DiagnosticContentId +
                ".prefab";

            string largePreviewPath =
                DiagnosticContentFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                DiagnosticContentFolder +
                "/Preview_Catalog.png";

            using SavicAssetMutationScope rollback =
                new SavicAssetMutationScope(
                    context.Layout,
                    "chair_vertical_probe");

            rollback.CaptureAsset(
                MainCatalogPath);

            rollback.CaptureAsset(
                itemPath);

            rollback.CaptureAsset(
                prefabPath);

            rollback.CaptureAsset(
                largePreviewPath);

            rollback.CaptureAsset(
                catalogPreviewPath);

            try
            {
                GameObject sourceModel =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        SourceModelPath);

                Require(
                    sourceModel != null,
                    "Canonical raw chair FBX could not be loaded.");

                SavicManifest manifest =
                    BuildManifest(
                        sourceModel);

                Require(
                    string.Equals(
                        manifest.classification.type,
                        "Chair",
                        StringComparison.Ordinal),
                    "Geometry-backed classifier did not identify the neutral-named chair.");

                Require(
                    manifest.classification.geometryBacked,
                    "Chair classification is not geometry-backed.");

                Require(
                    manifest.model3D.semanticParts != null &&
                    manifest.model3D.semanticParts.automationReady,
                    "Chair semantic parts are not automation-ready.");

                Require(
                    SavicChairAuthoringPlanner.TryPlan(
                        manifest,
                        out SavicChairAuthoringRecord plan,
                        out string planError),
                    "Chair authoring planner rejected canonical source: " +
                    planError);

                manifest.chairAuthoring =
                    plan;

                manifest.status =
                    "PLANNED";

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    "unity.source_mirror",
                    SourceModelPath,
                    "savic.chair-vertical-probe",
                    "1.0.0",
                    AssetDatabase
                        .GetAssetDependencyHash(
                            SourceModelPath)
                        .ToString());

                SavicChairPublisher publisher =
                    new SavicChairPublisher(
                        context.Layout,
                        context.Manifests);

                SavicChairPublicationOutcome first =
                    publisher.Publish(
                        manifest,
                        sourceModel);

                Require(
                    first.Succeeded,
                    "First chair publication failed: " +
                    first.Message);

                ValidatePublishedState(
                    manifest,
                    first,
                    out string prefabGuid,
                    out string itemGuid,
                    out int originalPrice);

                RestaurantPlaceableItemDefinition item =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            first.ItemDefinitionAssetPath);

                Require(
                    item != null,
                    "Published chair item disappeared before idempotence validation.");

                int manualPrice =
                    originalPrice +
                    17;

                SetPurchasePrice(
                    item,
                    manualPrice);

                SavicChairPublicationOutcome second =
                    publisher.Publish(
                        manifest,
                        sourceModel);

                Require(
                    second.Succeeded,
                    "Second chair publication failed: " +
                    second.Message);

                Require(
                    string.Equals(
                        prefabGuid,
                        AssetDatabase.AssetPathToGUID(
                            second.PrefabAssetPath),
                        StringComparison.Ordinal),
                    "Chair prefab GUID changed during idempotent reprocessing.");

                Require(
                    string.Equals(
                        itemGuid,
                        AssetDatabase.AssetPathToGUID(
                            second.ItemDefinitionAssetPath),
                        StringComparison.Ordinal),
                    "Chair item GUID changed during idempotent reprocessing.");

                RestaurantPlaceableItemDefinition reloadedItem =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            second.ItemDefinitionAssetPath);

                Require(
                    reloadedItem != null &&
                    reloadedItem.PurchasePrice ==
                        manualPrice,
                    "Manual chair price override was overwritten by reprocessing.");

                Require(
                    CountCatalogEntries(
                        DiagnosticContentId) ==
                    1,
                    "Chair publication created duplicate canonical catalog entries.");

                Require(
                    manifest.chairSpatial != null &&
                    manifest.chairSpatial.validated &&
                    manifest.chairSpatial.runtimeBindingValidated,
                    "Chair BBSIS readiness did not pass.");

                Require(
                    manifest.chairNavigation != null &&
                    manifest.chairNavigation.validated &&
                    manifest.chairNavigation.canonicalFrontPositiveZ,
                    "Chair navigation readiness did not pass.");

                Require(
                    manifest.chairPersistence != null &&
                    manifest.chairPersistence.validated &&
                    manifest.chairPersistence.catalogResolvable &&
                    manifest.chairPersistence.prefabResolvable,
                    "Chair persistence readiness did not pass.");

                Debug.Log(
                    "[SAVIC] CHAIR VERTICAL PROBE - PASS\n" +
                    "Classification: " +
                    manifest.classification.type +
                    " / " +
                    manifest.classification.score.ToString("0.000") +
                    "\nSemantic parts: " +
                    manifest.model3D.semanticParts.semanticPartCount +
                    "\nColliders: " +
                    manifest.chairColliders.colliderCount +
                    "\nBBSIS volumes S/O/D: " +
                    manifest.chairSpatial.staticVolumeCount +
                    "/" +
                    manifest.chairSpatial.operationalVolumeCount +
                    "/" +
                    manifest.chairSpatial.dynamicVolumeCount +
                    "\nCanonical front +Z: " +
                    manifest.chairNavigation.canonicalFrontPositiveZ +
                    "\nSave/Load catalog: PASS" +
                    "\nIdempotence: PASS" +
                    "\nManual price override preserved: PASS");
            }
            finally
            {
                CleanupDiagnosticResidue(
                    context);
            }
        }

        private static SavicManifest BuildManifest(
            GameObject sourceModel)
        {
            SavicModelAnalysisRecord analysis =
                SavicModelAnalyzer.Analyze(
                    sourceModel);

            Require(
                analysis != null &&
                analysis.analyzed &&
                analysis.hasUsableBounds,
                "Canonical chair source analysis failed.");

            string dependencyHash =
                AssetDatabase
                    .GetAssetDependencyHash(
                        SourceModelPath)
                    .ToString();

            string now =
                DateTime.UtcNow.ToString("O");

            SavicManifest manifest =
                new SavicManifest
                {
                    savicId =
                        DiagnosticSavicId,
                    createdUtc =
                        now,
                    updatedUtc =
                        now,
                    status =
                        "ANALYZED",
                    source =
                        new SavicSourceRecord
                        {
                            sourceHash =
                                SavicHashService.ComputeSha256Text(
                                    "savic-chair-vertical-probe|" +
                                    dependencyHash),
                            originalFileName =
                                "neutral_asset_0042.fbx",
                            extension =
                                ".fbx",
                            sourceKind =
                                SavicSourceKind.Model3D.ToString(),
                            archivedRelativePath =
                                string.Empty,
                            byteLength =
                                0,
                            originalLastWriteUtcTicks =
                                0,
                            ingestedUtc =
                                now
                        },
                    model3D =
                        analysis
                };

            SavicClassificationRecord classification =
                SavicContentClassifier.Classify(
                    manifest);

            manifest.classification =
                classification;

            manifest.family =
                classification.family;

            manifest.type =
                classification.type;

            manifest.category =
                classification.category;

            SavicSemanticPartAnalysisRecord semantic =
                SavicSemanticPartAnalyzer.Analyze(
                    sourceModel,
                    analysis,
                    classification);

            analysis.semanticParts =
                semantic;

            return manifest;
        }

        private static void ValidatePublishedState(
            SavicManifest manifest,
            SavicChairPublicationOutcome publication,
            out string prefabGuid,
            out string itemGuid,
            out int originalPrice)
        {
            Require(
                string.Equals(
                    manifest.status,
                    "PUBLISHED",
                    StringComparison.Ordinal),
                "Published chair manifest is not PUBLISHED.");

            Require(
                string.Equals(
                    manifest.canonicalContentId,
                    DiagnosticContentId,
                    StringComparison.Ordinal),
                "Chair publisher assigned an unexpected CanonicalContentId.");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    publication.PrefabAssetPath);

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        publication.ItemDefinitionAssetPath);

            Require(
                prefab != null &&
                item != null,
                "Published chair artifacts cannot be loaded.");

            Require(
                item.Category ==
                    RestaurantPlaceableItemCategory.Seating,
                "Published chair is not registered in Seating.");

            Require(
                item.CatalogIcon != null &&
                item.InspectorPreview != null,
                "Published chair previews are missing.");

            RestaurantSeat seat =
                prefab.GetComponent<RestaurantSeat>();

            RestaurantPlaceableObject placeable =
                prefab.GetComponent<RestaurantPlaceableObject>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            Require(
                seat != null &&
                placeable != null &&
                footprint != null &&
                prefab.GetComponent
                    <BistroBuilderSeatCirculationEnvelope>() != null,
                "Published chair is missing canonical runtime components.");

            Require(
                seat.ValidateConfiguration(
                    out string seatError),
                "Published chair seat validation failed: " +
                seatError);

            Require(
                placeable.ValidateConfiguration(
                    out string placeableError),
                "Published chair placeable validation failed: " +
                placeableError);

            Require(
                manifest.chairColliders != null &&
                manifest.chairColliders.generated &&
                manifest.chairColliders.semanticBacked &&
                manifest.chairColliders.colliderCount >=
                    3,
                "Published chair has no semantic compound collider set.");

            prefabGuid =
                AssetDatabase.AssetPathToGUID(
                    publication.PrefabAssetPath);

            itemGuid =
                AssetDatabase.AssetPathToGUID(
                    publication.ItemDefinitionAssetPath);

            Require(
                !string.IsNullOrWhiteSpace(
                    prefabGuid) &&
                !string.IsNullOrWhiteSpace(
                    itemGuid),
                "Published chair artifacts do not have stable Unity GUIDs.");

            originalPrice =
                item.PurchasePrice;

            Require(
                CountCatalogEntries(
                    DiagnosticContentId) ==
                1,
                "Published chair does not resolve exactly once in the canonical catalog.");
        }

        private static int CountCatalogEntries(
            string itemId)
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
                return 0;

            int count =
                0;

            IReadOnlyList<RestaurantPlaceableItemDefinition> items =
                catalog.Items;

            for (int index = 0;
                 index < items.Count;
                 index++)
            {
                RestaurantPlaceableItemDefinition item =
                    items[index];

                if (item != null &&
                    string.Equals(
                        item.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void SetPurchasePrice(
            RestaurantPlaceableItemDefinition item,
            int price)
        {
            SerializedObject serialized =
                new SerializedObject(
                    item);

            SerializedProperty property =
                serialized.FindProperty(
                    "purchasePrice");

            Require(
                property != null,
                "RestaurantPlaceableItemDefinition purchasePrice field changed.");

            property.intValue =
                Math.Max(
                    0,
                    price);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);

            AssetDatabase.SaveAssets();
        }

        private static void CleanupDiagnosticResidue(
            SavicEditorContext context)
        {
            if (context == null)
                return;

            RemoveCatalogEntry();

            if (AssetDatabase.IsValidFolder(
                    DiagnosticContentFolder))
            {
                AssetDatabase.DeleteAsset(
                    DiagnosticContentFolder);
            }

            string manifestPath =
                Path.Combine(
                    context.Layout.ManifestsRoot,
                    DiagnosticSavicId +
                    ".json");

            if (File.Exists(
                    manifestPath))
            {
                File.Delete(
                    manifestPath);
            }

            context.Manifests.Reload();

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RemoveCatalogEntry()
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
                return;

            SerializedObject serialized =
                new SerializedObject(
                    catalog);

            SerializedProperty items =
                serialized.FindProperty(
                    "items");

            if (items == null ||
                !items.isArray)
            {
                return;
            }

            bool changed =
                false;

            for (int index =
                     items.arraySize - 1;
                 index >= 0;
                 index--)
            {
                SerializedProperty element =
                    items.GetArrayElementAtIndex(
                        index);

                RestaurantPlaceableItemDefinition item =
                    element.objectReferenceValue as
                        RestaurantPlaceableItemDefinition;

                if (item == null ||
                    !string.Equals(
                        item.ItemId,
                        DiagnosticContentId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                element.objectReferenceValue =
                    null;

                items.DeleteArrayElementAtIndex(
                    index);

                changed =
                    true;
            }

            if (!changed)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                catalog);

            AssetDatabase.SaveAssets();
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    message);
            }
        }
    }
}
