using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicChairPublicationOutcome
    {
        internal SavicChairPublicationOutcome(
            bool succeeded,
            string message,
            string prefabAssetPath,
            string itemDefinitionAssetPath)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            PrefabAssetPath = prefabAssetPath ?? string.Empty;
            ItemDefinitionAssetPath =
                itemDefinitionAssetPath ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
        internal string PrefabAssetPath { get; }
        internal string ItemDefinitionAssetPath { get; }
    }

    internal sealed class SavicChairPublisher
    {
        internal const string Version = "1.0.0";

        private const string PublicationFingerprintSchema =
            "chair-publication-input-v1";

        private const string GeneratedChairsRoot =
            "Assets/Generated/BistroBuilder/SAVIC/Published/Chairs";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string SourceMirrorArtifactRole =
            "unity.source_mirror";

        private const string PrefabArtifactRole =
            "published.chair.prefab";

        private const string ItemArtifactRole =
            "catalog.item_definition";

        private const string LargePreviewArtifactRole =
            "preview.large";

        private const string CatalogPreviewArtifactRole =
            "preview.catalog";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicChairPublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicChairPublicationOutcome Publish(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicChairAuthoringRecord plan =
                manifest.chairAuthoring;

            if (plan == null ||
                !plan.planned)
            {
                return Fail(
                    "Chair publication requires a valid authoring plan.");
            }

            if (string.IsNullOrWhiteSpace(
                    plan.templatePrefabAssetPath) ||
                string.IsNullOrWhiteSpace(
                    plan.seatUseProfileAssetPath) ||
                string.IsNullOrWhiteSpace(
                    plan.editableDefinitionAssetPath))
            {
                return Fail(
                    "Chair authoring plan has incomplete canonical dependencies.");
            }

            EnsureCanonicalContentId(
                manifest);

            string publicationFingerprint =
                BuildPublicationFingerprint(
                    manifest,
                    plan);

            string contentFolder =
                GeneratedChairsRoot +
                "/" +
                manifest.canonicalContentId;

            EnsureAssetFolder(
                contentFolder);

            string itemPath =
                contentFolder +
                "/PlaceableItem_" +
                manifest.canonicalContentId +
                ".asset";

            string prefabPath =
                contentFolder +
                "/Chair_" +
                manifest.canonicalContentId +
                ".prefab";

            string largePreviewPath =
                contentFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                contentFolder +
                "/Preview_Catalog.png";

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            RestaurantEditableObjectDefinition editableDefinition =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantEditableObjectDefinition>(
                        plan.editableDefinitionAssetPath);

            RestaurantSeatUseProfileDefinition useProfile =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantSeatUseProfileDefinition>(
                        plan.seatUseProfileAssetPath);

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    plan.templatePrefabAssetPath);

            if (catalog == null ||
                editableDefinition == null ||
                useProfile == null ||
                template == null)
            {
                return Fail(
                    "One or more canonical Bistro Builder chair dependencies are missing.");
            }

            if (!useProfile.ValidateConfiguration(
                    out string profileError))
            {
                return Fail(
                    "Canonical chair SeatUseProfile is invalid: " +
                    profileError);
            }

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_chair_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(
                MainCatalogPath);

            transaction.CaptureAsset(
                itemPath);

            transaction.CaptureAsset(
                prefabPath);

            transaction.CaptureAsset(
                largePreviewPath);

            transaction.CaptureAsset(
                catalogPreviewPath);

            GameObject workingRoot =
                null;

            try
            {
                bool itemWasCreated;

                RestaurantPlaceableItemDefinition item =
                    GetOrCreateItemDefinition(
                        itemPath,
                        manifest,
                        plan,
                        editableDefinition,
                        out itemWasCreated);

                GameObject savedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                bool reusePublishedPrefab =
                    savedPrefab != null &&
                    CanReusePublishedPrefab(
                        manifest,
                        plan,
                        prefabPath,
                        publicationFingerprint);

                if (!reusePublishedPrefab)
                {
                    ClearItemPrefabReference(
                        item);

                    AssetDatabase.SaveAssets();

                    workingRoot =
                        BuildWorkingChair(
                            template,
                            sourceModelAsset,
                            item,
                            editableDefinition,
                            useProfile,
                            manifest,
                            plan);

                    savedPrefab =
                        SavePrefabWithBoundedRetry(
                            workingRoot,
                            prefabPath,
                            out bool prefabSaved);

                    if (!prefabSaved ||
                        savedPrefab == null)
                    {
                        throw new InvalidOperationException(
                            "Unity did not confirm the generated chair prefab save.");
                    }
                }

                RestaurantPlaceableObject savedPlaceable =
                    savedPrefab.GetComponent<RestaurantPlaceableObject>();

                if (savedPlaceable == null)
                {
                    throw new InvalidOperationException(
                        "Generated chair prefab has no RestaurantPlaceableObject.");
                }

                LinkItemToPrefab(
                    item,
                    savedPlaceable);

                RegisterInCatalog(
                    catalog,
                    item);

                AssetDatabase.SaveAssets();

                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                AssetDatabase.ImportAsset(
                    itemPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                StampManagedAsset(
                    item,
                    "SAVIC.Managed",
                    "SAVIC.Chair",
                    "SAVIC.CatalogItem");

                GameObject reloadedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                if (reloadedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Generated chair prefab could not be reloaded.");
                }

                StampManagedAsset(
                    reloadedPrefab,
                    "SAVIC.Managed",
                    "SAVIC.Chair",
                    "SAVIC.Prefab");

                string previewFingerprint =
                    SavicPreviewRenderer.BuildInputFingerprint(
                        prefabPath);

                bool reusePreviews =
                    CanReuseManagedPreviews(
                        manifest,
                        previewFingerprint,
                        largePreviewPath,
                        catalogPreviewPath);

                SavicPreviewGenerationResult previews =
                    reusePreviews
                        ? SavicPreviewRenderer.ReuseAndAssign(
                            item,
                            largePreviewPath,
                            catalogPreviewPath)
                        : SavicPreviewRenderer.GenerateAndAssign(
                            reloadedPrefab,
                            item,
                            contentFolder);

                if (!previews.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Chair preview generation failed: " +
                        previews.Message);
                }

                AssetDatabase.SaveAssets();

                manifest.chairSpatial =
                    SavicChairSpatialReadinessValidator.Validate(
                        manifest,
                        plan,
                        prefabPath);

                manifest.chairNavigation =
                    SavicChairNavigationReadinessValidator.Validate(
                        manifest,
                        plan,
                        prefabPath);

                manifest.chairPersistence =
                    SavicChairPersistenceReadinessValidator.Validate(
                        manifest,
                        plan,
                        prefabPath);

                ValidatePublishedChair(
                    reloadedPrefab,
                    item,
                    catalog,
                    useProfile,
                    manifest,
                    plan);

                plan.prefabAssetPath =
                    prefabPath;

                plan.itemDefinitionAssetPath =
                    itemPath;

                manifest.chairAuthoring =
                    plan;

                manifest.status =
                    "PUBLISHED";

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    prefabPath,
                    "savic.chair-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    itemPath,
                    "savic.chair-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    LargePreviewArtifactRole,
                    previews.LargePreviewAssetPath,
                    "savic.preview-renderer",
                    SavicPreviewRenderer.Version,
                    previewFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    CatalogPreviewArtifactRole,
                    previews.CatalogPreviewAssetPath,
                    "savic.preview-renderer",
                    SavicPreviewRenderer.Version,
                    previewFingerprint);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Presentation.Previews",
                    "PASS",
                    "INFO",
                    previews.Message,
                    SavicPreviewRenderer.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Spatial.ChairReadiness",
                    "PASS",
                    "INFO",
                    manifest.chairSpatial.evidence,
                    SavicChairSpatialReadinessValidator.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Navigation.ChairReadiness",
                    "PASS",
                    "INFO",
                    manifest.chairNavigation.evidence,
                    SavicChairNavigationReadinessValidator.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "SaveLoad.ChairCatalogReadiness",
                    "PASS",
                    "INFO",
                    manifest.chairPersistence.evidence,
                    SavicChairPersistenceReadinessValidator.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Publication.ChairPrefab",
                    "PASS",
                    "INFO",
                    "Chair prefab, catalog item and canonical integrations validated.",
                    Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "chair.uniformScale",
                    plan.uniformScale.ToString(
                        "0.######",
                        CultureInfo.InvariantCulture),
                    plan.scaleCorrectionApplied
                        ? "HIGH"
                        : "CERTAIN",
                    plan.planReason,
                    "chair.authoring.scale.v1");

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "chair.frontNormalization",
                    plan.visualYawDegrees.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture),
                    "HIGH",
                    "Detected chair front was normalized to canonical +Z.",
                    "chair.authoring.front.v1");

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "chair.seatHeight",
                    plan.finalSeatHeightMeters.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture),
                    "HIGH",
                    "Functional SeatPoint follows the normalized detected seat height.",
                    "chair.authoring.seat-height.v1");

                manifests.Save(
                    manifest);

                transaction.Commit();

                return new SavicChairPublicationOutcome(
                    true,
                    itemWasCreated
                        ? "Chair published and catalog item created."
                        : "Chair republished while preserving manual catalog values.",
                    prefabPath,
                    itemPath);
            }
            catch (Exception exception)
            {
                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Publication.ChairPrefab",
                    "FAIL",
                    "ERROR",
                    exception.Message,
                    Version);

                manifest.status =
                    "NEEDS_REVIEW";

                try
                {
                    manifests.Save(
                        manifest);
                }
                catch (Exception saveException)
                {
                    Debug.LogError(
                        "[SAVIC] Could not persist chair publication failure: " +
                        saveException);
                }

                return Fail(
                    "Chair publication failed: " +
                    exception.Message,
                    prefabPath,
                    itemPath);
            }
            finally
            {
                if (workingRoot != null)
                {
                    Object.DestroyImmediate(
                        workingRoot);
                }
            }
        }

        internal SavicChairPublicationOutcome RefreshAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicChairAuthoringRecord plan =
                manifest.chairAuthoring;

            if (plan == null ||
                !plan.planned ||
                string.IsNullOrWhiteSpace(plan.prefabAssetPath) ||
                string.IsNullOrWhiteSpace(plan.itemDefinitionAssetPath))
            {
                return Publish(
                    manifest,
                    sourceModelAsset);
            }

            string prefabPath =
                plan.prefabAssetPath;

            string itemPath =
                plan.itemDefinitionAssetPath;

            GameObject existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        itemPath);

            if (existingPrefab == null ||
                item == null ||
                existingPrefab.transform.Find(
                    "OperationalMotionRoot/Visual/SourceModel") == null)
            {
                return Publish(
                    manifest,
                    sourceModelAsset);
            }

            string contentFolder =
                Path.GetDirectoryName(itemPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(contentFolder))
            {
                return Fail(
                    "Chair appearance refresh could not resolve content folder.",
                    prefabPath,
                    itemPath);
            }

            string largePreviewPath =
                contentFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                contentFolder +
                "/Preview_Catalog.png";

            string publicationFingerprint =
                BuildPublicationFingerprint(
                    manifest,
                    plan);

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "refresh_chair_appearance_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(prefabPath);
            transaction.CaptureAsset(itemPath);
            transaction.CaptureAsset(largePreviewPath);
            transaction.CaptureAsset(catalogPreviewPath);

            GameObject prefabContents = null;

            try
            {
                prefabContents =
                    PrefabUtility.LoadPrefabContents(
                        prefabPath);

                Transform visualRoot =
                    prefabContents.transform.Find(
                        "OperationalMotionRoot/Visual");

                if (visualRoot == null)
                {
                    throw new InvalidOperationException(
                        "Published chair prefab has no OperationalMotionRoot/Visual root.");
                }

                Transform previousSource =
                    visualRoot.Find(
                        "SourceModel");

                if (previousSource == null)
                {
                    throw new InvalidOperationException(
                        "Published chair prefab has no SourceModel child.");
                }

                Object.DestroyImmediate(
                    previousSource.gameObject);

                GameObject sourceInstance =
                    InstantiateSourceModel(
                        sourceModelAsset,
                        visualRoot);

                NormalizeSourceVisual(
                    sourceInstance.transform,
                    manifest.model3D,
                    plan);

                RemoveUnsupportedSourceComponents(
                    sourceInstance);

                if (sourceInstance.GetComponentInChildren
                        <Renderer>(true) == null)
                {
                    throw new InvalidOperationException(
                        "Refreshed chair source contains no renderer.");
                }

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        prefabContents,
                        prefabPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not confirm chair appearance-only prefab save.");
                }
            }
            finally
            {
                if (prefabContents != null)
                {
                    PrefabUtility.UnloadPrefabContents(
                        prefabContents);
                }
            }

            try
            {
                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                GameObject reloadedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                if (reloadedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Appearance-refreshed chair prefab could not be reloaded.");
                }

                string previewFingerprint =
                    SavicPreviewRenderer.BuildInputFingerprint(
                        prefabPath);

                SavicPreviewGenerationResult previews =
                    SavicPreviewRenderer.GenerateAndAssign(
                        reloadedPrefab,
                        item,
                        contentFolder);

                if (!previews.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Chair preview refresh failed: " +
                        previews.Message);
                }

                AssetDatabase.SaveAssets();

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    prefabPath,
                    "savic.chair-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    itemPath,
                    "savic.chair-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    LargePreviewArtifactRole,
                    previews.LargePreviewAssetPath,
                    "savic.preview-renderer",
                    SavicPreviewRenderer.Version,
                    previewFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    CatalogPreviewArtifactRole,
                    previews.CatalogPreviewAssetPath,
                    "savic.preview-renderer",
                    SavicPreviewRenderer.Version,
                    previewFingerprint);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Pipeline.IncrementalAppearanceRefresh",
                    "PASS",
                    "INFO",
                    "Chair visual source and previews refreshed without rebuilding colliders, spatial, navigation or persistence topology.",
                    SavicIncrementalInvalidationService.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Presentation.Previews",
                    "PASS",
                    "INFO",
                    previews.Message,
                    SavicPreviewRenderer.Version);

                manifest.status =
                    "PUBLISHED";

                manifests.Save(
                    manifest);

                transaction.Commit();

                return new SavicChairPublicationOutcome(
                    true,
                    "Chair appearance refreshed incrementally; geometry/colliders were reused.",
                    prefabPath,
                    itemPath);
            }
            catch (Exception exception)
            {
                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Pipeline.IncrementalAppearanceRefresh",
                    "FAIL",
                    "ERROR",
                    exception.Message,
                    SavicIncrementalInvalidationService.Version);

                return Fail(
                    "Incremental chair appearance refresh failed: " +
                    exception.Message,
                    prefabPath,
                    itemPath);
            }
        }

        private static string BuildPublicationFingerprint(
            SavicManifest manifest,
            SavicChairAuthoringRecord plan)
        {
            if (manifest?.source == null)
            {
                throw new InvalidOperationException(
                    "Cannot fingerprint a chair publication without source metadata.");
            }

            string templateHash =
                RequireDependencyHash(
                    plan.templatePrefabAssetPath);

            string profileHash =
                RequireDependencyHash(
                    plan.seatUseProfileAssetPath);

            string editableHash =
                RequireDependencyHash(
                    plan.editableDefinitionAssetPath);

            string canonical =
                string.Join(
                    "|",
                    new[]
                    {
                        "savic.chair.publication",
                        PublicationFingerprintSchema,
                        Version,
                        manifest.source.sourceHash ?? string.Empty,
                        plan.plannerVersion ?? string.Empty,
                        plan.uniformScale.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.visualYawDegrees.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.finalWidthMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.finalHeightMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.finalDepthMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.finalSeatHeightMeters.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        plan.templatePrefabAssetPath ?? string.Empty,
                        templateHash,
                        plan.seatUseProfileAssetPath ?? string.Empty,
                        profileHash,
                        plan.editableDefinitionAssetPath ?? string.Empty,
                        editableHash,
                        SavicChairGeometryAnalyzer.Version,
                        SavicChairSemanticPartAnalyzer.Version,
                        SavicChairColliderBuilder.Version
                    });

            return SavicHashService.ComputeSha256Text(
                canonical);
        }

        private static string RequireDependencyHash(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(
                    assetPath) ||
                AssetDatabase.LoadMainAssetAtPath(
                    assetPath) == null)
            {
                throw new InvalidOperationException(
                    "Cannot fingerprint missing dependency: " +
                    assetPath);
            }

            return AssetDatabase
                .GetAssetDependencyHash(
                    assetPath)
                .ToString();
        }

        private static bool CanReusePublishedPrefab(
            SavicManifest manifest,
            SavicChairAuthoringRecord plan,
            string prefabPath,
            string expectedFingerprint)
        {
            SavicArtifactRecord artifact =
                FindArtifact(
                    manifest,
                    PrefabArtifactRole);

            if (artifact == null ||
                !string.Equals(
                    artifact.projectRelativePath,
                    prefabPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.builderId,
                    "savic.chair-publisher",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.builderVersion,
                    Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.inputFingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return ExistingPrefabMatchesPlan(
                       prefabPath,
                       manifest,
                       plan) &&
                   ExistingPrefabUsesCurrentSource(
                       manifest,
                       prefabPath);
        }

        private static bool ExistingPrefabMatchesPlan(
            string prefabPath,
            SavicManifest manifest,
            SavicChairAuthoringRecord plan)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null)
                return false;

            RestaurantSeat seat =
                prefab.GetComponent<RestaurantSeat>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent
                    <RestaurantPlacementFootprint>();

            RestaurantPlaceableObject placeable =
                prefab.GetComponent
                    <RestaurantPlaceableObject>();

            BistroBuilderSeatCirculationEnvelope envelope =
                prefab.GetComponent
                    <BistroBuilderSeatCirculationEnvelope>();

            Renderer[] renderers =
                prefab.GetComponentsInChildren
                    <Renderer>(true);

            if (seat == null ||
                footprint == null ||
                placeable == null ||
                envelope == null ||
                renderers.Length == 0 ||
                seat.SeatPoint == null)
            {
                return false;
            }

            if (!Approximately(
                    footprint.Size.x,
                    plan.finalWidthMeters,
                    0.002f) ||
                !Approximately(
                    footprint.Size.y,
                    plan.finalDepthMeters,
                    0.002f))
            {
                return false;
            }

            float seatHeight =
                prefab.transform
                    .InverseTransformPoint(
                        seat.SeatPoint.position)
                    .y;

            if (!Approximately(
                    seatHeight,
                    plan.finalSeatHeightMeters,
                    0.03f))
            {
                return false;
            }

            return SavicChairColliderBuilder.Validate(
                prefab,
                manifest,
                plan,
                manifest.chairColliders,
                out _);
        }

        private static bool ExistingPrefabUsesCurrentSource(
            SavicManifest manifest,
            string prefabPath)
        {
            SavicArtifactRecord sourceArtifact =
                FindArtifact(
                    manifest,
                    SourceMirrorArtifactRole);

            if (sourceArtifact == null ||
                string.IsNullOrWhiteSpace(
                    sourceArtifact.projectRelativePath))
            {
                return false;
            }

            string expectedSourcePath =
                sourceArtifact.projectRelativePath
                    .Replace('\\', '/');

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null)
                return false;

            MeshFilter[] filters =
                prefab.GetComponentsInChildren
                    <MeshFilter>(true);

            for (int index = 0;
                 index < filters.Length;
                 index++)
            {
                Mesh mesh =
                    filters[index].sharedMesh;

                if (mesh == null)
                    continue;

                string meshPath =
                    AssetDatabase.GetAssetPath(
                        mesh)
                    .Replace('\\', '/');

                if (string.Equals(
                        meshPath,
                        expectedSourcePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            SkinnedMeshRenderer[] skinned =
                prefab.GetComponentsInChildren
                    <SkinnedMeshRenderer>(true);

            for (int index = 0;
                 index < skinned.Length;
                 index++)
            {
                Mesh mesh =
                    skinned[index].sharedMesh;

                if (mesh == null)
                    continue;

                string meshPath =
                    AssetDatabase.GetAssetPath(
                        mesh)
                    .Replace('\\', '/');

                if (string.Equals(
                        meshPath,
                        expectedSourcePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanReuseManagedPreviews(
            SavicManifest manifest,
            string expectedFingerprint,
            string largePreviewPath,
            string catalogPreviewPath)
        {
            return IsReusablePreviewArtifact(
                       manifest,
                       LargePreviewArtifactRole,
                       largePreviewPath,
                       expectedFingerprint) &&
                   IsReusablePreviewArtifact(
                       manifest,
                       CatalogPreviewArtifactRole,
                       catalogPreviewPath,
                       expectedFingerprint);
        }

        private static bool IsReusablePreviewArtifact(
            SavicManifest manifest,
            string role,
            string expectedPath,
            string expectedFingerprint)
        {
            SavicArtifactRecord artifact =
                FindArtifact(
                    manifest,
                    role);

            if (artifact == null ||
                !string.Equals(
                    artifact.projectRelativePath,
                    expectedPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.builderId,
                    "savic.preview-renderer",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.builderVersion,
                    SavicPreviewRenderer.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    artifact.inputFingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(
                       expectedPath) != null;
        }

        private static SavicArtifactRecord FindArtifact(
            SavicManifest manifest,
            string role)
        {
            if (manifest?.artifacts == null)
                return null;

            for (int index = 0;
                 index < manifest.artifacts.Count;
                 index++)
            {
                SavicArtifactRecord candidate =
                    manifest.artifacts[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.role,
                        role,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static GameObject BuildWorkingChair(
            GameObject template,
            GameObject sourceModelAsset,
            RestaurantPlaceableItemDefinition item,
            RestaurantEditableObjectDefinition editableDefinition,
            RestaurantSeatUseProfileDefinition useProfile,
            SavicManifest manifest,
            SavicChairAuthoringRecord plan)
        {
            GameObject root =
                PrefabUtility.InstantiatePrefab(
                    template) as GameObject;

            if (root == null)
            {
                throw new InvalidOperationException(
                    "Canonical chair template could not be instantiated.");
            }

            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            root.name =
                "SAVIC_Chair_" +
                ShortIdentity(
                    manifest.canonicalContentId);

            root.transform.position =
                Vector3.zero;

            root.transform.rotation =
                Quaternion.identity;

            root.transform.localScale =
                Vector3.one;

            Transform placementAnchor =
                EnsureDirectChild(
                    root.transform,
                    "PlacementAnchor");

            Transform associationPoint =
                EnsureDirectChild(
                    root.transform,
                    "AssociationPoint");

            Transform customerApproach =
                EnsureDirectChild(
                    root.transform,
                    "CustomerApproachPoint");

            Transform operationalMotionRoot =
                EnsureDirectChild(
                    root.transform,
                    "OperationalMotionRoot");

            RemoveTemplateVisual(
                operationalMotionRoot);

            Transform seatPoint =
                EnsureDirectChild(
                    operationalMotionRoot,
                    "SeatPoint");

            Transform visualRoot =
                EnsureDirectChild(
                    operationalMotionRoot,
                    "Visual");

            SetIdentity(
                placementAnchor);

            SetIdentity(
                operationalMotionRoot);

            SetIdentity(
                visualRoot);

            float halfDepth =
                plan.finalDepthMeters *
                0.5f;

            placementAnchor.localPosition =
                Vector3.zero;

            associationPoint.localPosition =
                new Vector3(
                    0f,
                    0f,
                    halfDepth);

            associationPoint.localRotation =
                Quaternion.identity;

            associationPoint.localScale =
                Vector3.one;

            float approachOffset =
                useProfile.PullOutDistance +
                useProfile.CustomerApproachDistance;

            customerApproach.localPosition =
                new Vector3(
                    0f,
                    0f,
                    -halfDepth -
                    approachOffset);

            customerApproach.localRotation =
                Quaternion.identity;

            customerApproach.localScale =
                Vector3.one;

            seatPoint.localPosition =
                new Vector3(
                    0f,
                    plan.finalSeatHeightMeters,
                    0f);

            seatPoint.localRotation =
                Quaternion.identity;

            seatPoint.localScale =
                Vector3.one;

            GameObject sourceInstance =
                InstantiateSourceModel(
                    sourceModelAsset,
                    visualRoot);

            NormalizeSourceVisual(
                sourceInstance.transform,
                manifest.model3D,
                plan);

            RemoveUnsupportedSourceComponents(
                sourceInstance);

            if (sourceInstance.GetComponentInChildren
                    <Renderer>(true) == null)
            {
                throw new InvalidOperationException(
                    "Imported chair source contains no renderer.");
            }

            manifest.chairColliders =
                SavicChairColliderBuilder.Build(
                    root,
                    manifest,
                    plan);

            RestaurantPlacementFootprint footprint =
                RequireComponent
                    <RestaurantPlacementFootprint>(
                        root);

            footprint.ConfigureRuntime(
                Vector3.zero,
                new Vector2(
                    plan.finalWidthMeters,
                    plan.finalDepthMeters),
                0f,
                true,
                0.02f);

            RestaurantEditableObject editable =
                RequireComponent
                    <RestaurantEditableObject>(
                        root);

            editable.SetDefinition(
                editableDefinition);

            RestaurantPlaceableObject placeable =
                RequireComponent
                    <RestaurantPlaceableObject>(
                        root);

            ConfigurePlaceableComponent(
                placeable,
                item,
                placementAnchor);

            RestaurantSeat seat =
                RequireComponent<RestaurantSeat>(
                    root);

            ConfigureSeatComponent(
                seat,
                useProfile,
                placeable,
                associationPoint,
                operationalMotionRoot,
                seatPoint,
                customerApproach);

            RestaurantOperationalClearanceSet clearanceSet =
                RequireComponent
                    <RestaurantOperationalClearanceSet>(
                        root);

            ConfigureOperationalClearance(
                clearanceSet,
                plan,
                useProfile);

            if (root.GetComponent
                    <BistroBuilderSeatCirculationEnvelope>() ==
                null)
            {
                root.AddComponent
                    <BistroBuilderSeatCirculationEnvelope>();
            }

            RestaurantAreaMember areaMember =
                RequireComponent
                    <RestaurantAreaMember>(
                        root);

            if (areaMember.RequiredCapabilityCount <=
                0)
            {
                throw new InvalidOperationException(
                    "Canonical chair template has no area capability requirement.");
            }

            if (!seat.ValidateConfiguration(
                    out string seatError))
            {
                throw new InvalidOperationException(
                    "Generated chair failed functional validation: " +
                    seatError);
            }

            return root;
        }

        private static void ConfigureSeatComponent(
            RestaurantSeat seat,
            RestaurantSeatUseProfileDefinition useProfile,
            RestaurantPlaceableObject placeable,
            Transform associationPoint,
            Transform operationalMotionRoot,
            Transform seatPoint,
            Transform customerApproach)
        {
            SerializedObject serialized =
                new SerializedObject(
                    seat);

            RequireProperty(
                serialized,
                "useProfile").objectReferenceValue =
                    useProfile;

            RequireProperty(
                serialized,
                "facingAxis").enumValueIndex =
                    (int)RestaurantSeatFacingAxis.PositiveZ;

            RequireProperty(
                serialized,
                "placeableObject").objectReferenceValue =
                    placeable;

            RequireProperty(
                serialized,
                "associationPoint").objectReferenceValue =
                    associationPoint;

            RequireProperty(
                serialized,
                "operationalMotionRoot").objectReferenceValue =
                    operationalMotionRoot;

            RequireProperty(
                serialized,
                "seatPoint").objectReferenceValue =
                    seatPoint;

            RequireProperty(
                serialized,
                "customerApproachPoint").objectReferenceValue =
                    customerApproach;

            RequireProperty(
                serialized,
                "associatedTable").objectReferenceValue =
                    null;

            RequireProperty(
                serialized,
                "associatedSlotIndex").intValue =
                    -1;

            RequireProperty(
                serialized,
                "topologyStatus").enumValueIndex =
                    0;

            RequireProperty(
                serialized,
                "topologyDiagnostic").stringValue =
                    string.Empty;

            RequireProperty(
                serialized,
                "operationalState").enumValueIndex =
                    0;

            RequireProperty(
                serialized,
                "reservationOwnerId").stringValue =
                    string.Empty;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureOperationalClearance(
            RestaurantOperationalClearanceSet clearanceSet,
            SavicChairAuthoringRecord plan,
            RestaurantSeatUseProfileDefinition useProfile)
        {
            SerializedObject serialized =
                new SerializedObject(
                    clearanceSet);

            SerializedProperty clearances =
                RequireProperty(
                    serialized,
                    "clearances");

            clearances.arraySize =
                1;

            SerializedProperty clearance =
                clearances.GetArrayElementAtIndex(
                    0);

            float clearanceDepth =
                Math.Max(
                    0.50f,
                    useProfile.PullOutDistance +
                    useProfile.CustomerApproachDistance +
                    useProfile.CustomerApproachRadius);

            float clearanceWidth =
                Math.Max(
                    0.10f,
                    plan.finalWidthMeters +
                    0.10f);

            RequireRelative(
                clearance,
                "clearanceId").stringValue =
                    "seat_pullout_and_approach";

            RequireRelative(
                clearance,
                "localCenter").vector3Value =
                    new Vector3(
                        0f,
                        0f,
                        -plan.finalDepthMeters *
                        0.5f -
                        clearanceDepth *
                        0.5f);

            RequireRelative(
                clearance,
                "size").vector2Value =
                    new Vector2(
                        clearanceWidth,
                        clearanceDepth);

            RequireRelative(
                clearance,
                "localYawDegrees").floatValue =
                    0f;

            RequireRelative(
                clearance,
                "blockedUserMessage").stringValue =
                    "La silla no puede retirarse para sentar al cliente.";

            RequireProperty(
                serialized,
                "blocksOtherPlacements").boolValue =
                    true;

            RequireProperty(
                serialized,
                "requiresClearanceForOwner").boolValue =
                    true;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlaceableComponent(
            RestaurantPlaceableObject placeable,
            RestaurantPlaceableItemDefinition item,
            Transform placementAnchor)
        {
            placeable.SetItemDefinition(
                item);

            SerializedObject serialized =
                new SerializedObject(
                    placeable);

            RequireProperty(
                serialized,
                "instanceId").stringValue =
                    string.Empty;

            RequireProperty(
                serialized,
                "placementAnchor").objectReferenceValue =
                    placementAnchor;

            RequireProperty(
                serialized,
                "synchronizeEditableDefinition").boolValue =
                    true;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidatePublishedChair(
            GameObject prefab,
            RestaurantPlaceableItemDefinition item,
            RestaurantPlaceableCatalogDefinition catalog,
            RestaurantSeatUseProfileDefinition useProfile,
            SavicManifest manifest,
            SavicChairAuthoringRecord plan)
        {
            RestaurantPlaceableObject placeable =
                prefab.GetComponent
                    <RestaurantPlaceableObject>();

            RestaurantSeat seat =
                prefab.GetComponent
                    <RestaurantSeat>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent
                    <RestaurantPlacementFootprint>();

            RestaurantOperationalClearanceSet clearance =
                prefab.GetComponent
                    <RestaurantOperationalClearanceSet>();

            BistroBuilderSeatCirculationEnvelope envelope =
                prefab.GetComponent
                    <BistroBuilderSeatCirculationEnvelope>();

            Renderer[] renderers =
                prefab.GetComponentsInChildren
                    <Renderer>(true);

            if (placeable == null ||
                seat == null ||
                footprint == null ||
                clearance == null ||
                envelope == null ||
                renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Generated chair prefab is missing required canonical components.");
            }

            if (item.CatalogIcon == null ||
                item.InspectorPreview == null)
            {
                throw new InvalidOperationException(
                    "Generated chair catalog item has no usable SAVIC previews.");
            }

            if (!placeable.ValidateConfiguration(
                    out string placeableError))
            {
                throw new InvalidOperationException(
                    "Chair placeable validation failed: " +
                    placeableError);
            }

            if (!seat.ValidateConfiguration(
                    out string seatError))
            {
                throw new InvalidOperationException(
                    "Chair seat validation failed: " +
                    seatError);
            }

            if (!ReferenceEquals(
                    seat.UseProfile,
                    useProfile))
            {
                throw new InvalidOperationException(
                    "Published chair does not use its planned SeatUseProfile.");
            }

            if (!Approximately(
                    footprint.Size.x,
                    plan.finalWidthMeters,
                    0.002f) ||
                !Approximately(
                    footprint.Size.y,
                    plan.finalDepthMeters,
                    0.002f))
            {
                throw new InvalidOperationException(
                    "Published chair footprint does not match normalized dimensions.");
            }

            if (seat.OperationalMotionRoot == null ||
                seat.SeatPoint == null ||
                !seat.SeatPoint.IsChildOf(
                    seat.OperationalMotionRoot))
            {
                throw new InvalidOperationException(
                    "Published chair has invalid operational motion hierarchy.");
            }

            float seatHeight =
                prefab.transform
                    .InverseTransformPoint(
                        seat.SeatPoint.position)
                    .y;

            if (!Approximately(
                    seatHeight,
                    plan.finalSeatHeightMeters,
                    0.03f))
            {
                throw new InvalidOperationException(
                    "Published chair SeatPoint height differs from authoring plan.");
            }

            Vector3 facing =
                seat.CalculateFacingDirectionAtPose(
                    prefab.transform.rotation);

            facing.y =
                0f;

            if (facing.sqrMagnitude <
                    0.9f ||
                Vector3.Dot(
                    facing.normalized,
                    prefab.transform.forward) <
                    0.999f)
            {
                throw new InvalidOperationException(
                    "Published chair functional facing is not canonical +Z.");
            }

            if (!SavicChairColliderBuilder.Validate(
                    prefab,
                    manifest,
                    plan,
                    manifest.chairColliders,
                    out string colliderError))
            {
                throw new InvalidOperationException(
                    "Published semantic chair collider validation failed: " +
                    colliderError);
            }

            if (!ReferenceEquals(
                    placeable.ItemDefinition,
                    item))
            {
                throw new InvalidOperationException(
                    "Published chair prefab does not reference its catalog item.");
            }

            if (!catalog.TryGetItem(
                    manifest.canonicalContentId,
                    out RestaurantPlaceableItemDefinition catalogItem) ||
                !ReferenceEquals(
                    catalogItem,
                    item))
            {
                throw new InvalidOperationException(
                    "Main placeable catalog does not resolve the generated chair.");
            }
        }

        private static RestaurantPlaceableItemDefinition
            GetOrCreateItemDefinition(
                string itemPath,
                SavicManifest manifest,
                SavicChairAuthoringRecord plan,
                RestaurantEditableObjectDefinition editableDefinition,
                out bool wasCreated)
        {
            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        itemPath);

            wasCreated =
                item == null;

            if (item == null)
            {
                item =
                    ScriptableObject.CreateInstance
                        <RestaurantPlaceableItemDefinition>();

                item.name =
                    "PlaceableItem_" +
                    manifest.canonicalContentId;

                AssetDatabase.CreateAsset(
                    item,
                    itemPath);
            }

            SerializedObject serialized =
                new SerializedObject(
                    item);

            RequireProperty(
                serialized,
                "itemId").stringValue =
                    manifest.canonicalContentId;

            RequireProperty(
                serialized,
                "category").enumValueIndex =
                    (int)RestaurantPlaceableItemCategory.Seating;

            RequireProperty(
                serialized,
                "placementScope").enumValueIndex =
                    (int)RestaurantPlaceableEnvironmentScope
                        .InteriorAndExterior;

            RequireProperty(
                serialized,
                "dimensionsCentimeters").vector3Value =
                    new Vector3(
                        plan.finalWidthMeters *
                        100f,
                        plan.finalHeightMeters *
                        100f,
                        plan.finalDepthMeters *
                        100f);

            RequireProperty(
                serialized,
                "editableDefinition").objectReferenceValue =
                    editableDefinition;

            RequireProperty(
                serialized,
                "inspectorRules").intValue =
                    (int)(
                        RestaurantPlaceableInspectorRuleFlags
                            .FloorSurface |
                        RestaurantPlaceableInspectorRuleFlags
                            .RequiresClearance);

            if (wasCreated)
            {
                RequireProperty(
                    serialized,
                    "displayName").stringValue =
                        BuildDisplayName(
                            manifest.source?.originalFileName);

                RequireProperty(
                    serialized,
                    "description").stringValue =
                        "Silla integrada automáticamente por SAVIC.";

                RequireProperty(
                    serialized,
                    "purchasePrice").intValue =
                        Math.Max(
                            0,
                            plan.suggestedPurchasePriceEuro);

                RequireProperty(
                    serialized,
                    "disposalMode").enumValueIndex =
                        (int)RestaurantPlaceableDisposalMode
                            .Automatic;

                RequireProperty(
                    serialized,
                    "resaleBasisPoints").intValue =
                        5000;

                RequireProperty(
                    serialized,
                    "removalCost").intValue =
                        0;

                RequireProperty(
                    serialized,
                    "demolitionBasisPoints").intValue =
                        1500;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);

            return item;
        }

        private static GameObject InstantiateSourceModel(
            GameObject sourceModelAsset,
            Transform parent)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    sourceModelAsset) as GameObject;

            if (instance == null)
            {
                instance =
                    Object.Instantiate(
                        sourceModelAsset);
            }

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Imported chair source model could not be instantiated.");
            }

            if (PrefabUtility.IsPartOfPrefabInstance(
                    instance))
            {
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            instance.name =
                "SourceModel";

            instance.transform.SetParent(
                parent,
                false);

            return instance;
        }

        private static void NormalizeSourceVisual(
            Transform source,
            SavicModelAnalysisRecord analysis,
            SavicChairAuthoringRecord plan)
        {
            Quaternion yaw =
                Quaternion.Euler(
                    0f,
                    plan.visualYawDegrees,
                    0f);

            Vector3 scaledCenter =
                new Vector3(
                    analysis.boundsCenterX,
                    analysis.boundsCenterY,
                    analysis.boundsCenterZ) *
                plan.uniformScale;

            Vector3 rotatedCenter =
                yaw *
                scaledCenter;

            float sourceBottom =
                (analysis.boundsCenterY -
                 analysis.heightMeters *
                 0.5f) *
                plan.uniformScale;

            source.localRotation =
                yaw;

            source.localScale =
                Vector3.one *
                plan.uniformScale;

            source.localPosition =
                new Vector3(
                    -rotatedCenter.x,
                    -sourceBottom,
                    -rotatedCenter.z);
        }

        private static void RemoveTemplateVisual(
            Transform operationalMotionRoot)
        {
            if (operationalMotionRoot == null)
                return;

            Transform existingVisual =
                operationalMotionRoot.Find(
                    "Visual");

            if (existingVisual != null &&
                existingVisual.parent ==
                    operationalMotionRoot)
            {
                Object.DestroyImmediate(
                    existingVisual.gameObject);
            }
        }

        private static void RemoveUnsupportedSourceComponents(
            GameObject root)
        {
            foreach (Camera camera in
                     root.GetComponentsInChildren
                         <Camera>(true))
            {
                Object.DestroyImmediate(
                    camera);
            }

            foreach (Light light in
                     root.GetComponentsInChildren
                         <Light>(true))
            {
                Object.DestroyImmediate(
                    light);
            }

            foreach (AudioSource audio in
                     root.GetComponentsInChildren
                         <AudioSource>(true))
            {
                Object.DestroyImmediate(
                    audio);
            }

            foreach (Animator animator in
                     root.GetComponentsInChildren
                         <Animator>(true))
            {
                Object.DestroyImmediate(
                    animator);
            }

            foreach (Animation animation in
                     root.GetComponentsInChildren
                         <Animation>(true))
            {
                Object.DestroyImmediate(
                    animation);
            }
        }

        private static GameObject SavePrefabWithBoundedRetry(
            GameObject workingRoot,
            string prefabPath,
            out bool savedSuccessfully)
        {
            savedSuccessfully =
                false;

            string absoluteTarget =
                ToAbsoluteProjectPath(
                    prefabPath);

            if (!File.Exists(
                    absoluteTarget))
            {
                GameObject created =
                    PrefabUtility.SaveAsPrefabAsset(
                        workingRoot,
                        prefabPath,
                        out bool createdSuccessfully);

                savedSuccessfully =
                    createdSuccessfully &&
                    created != null;

                return savedSuccessfully
                    ? created
                    : null;
            }

            string directory =
                Path.GetDirectoryName(
                    prefabPath)
                ?.Replace('\\', '/')
                ?? throw new InvalidOperationException(
                    "Chair prefab directory could not be resolved.");

            string stagingPath =
                directory +
                "/SAVIC_STAGE_" +
                Guid.NewGuid().ToString("N") +
                ".prefab";

            try
            {
                GameObject staged =
                    PrefabUtility.SaveAsPrefabAsset(
                        workingRoot,
                        stagingPath,
                        out bool stagingSaved);

                if (!stagingSaved ||
                    staged == null)
                {
                    return null;
                }

                AssetDatabase.SaveAssets();

                string absoluteStaging =
                    ToAbsoluteProjectPath(
                        stagingPath);

                if (!File.Exists(
                        absoluteStaging))
                {
                    return null;
                }

                const int MaximumReplacementAttempts =
                    3;

                for (int attempt = 1;
                     attempt <= MaximumReplacementAttempts;
                     attempt++)
                {
                    try
                    {
                        File.Copy(
                            absoluteStaging,
                            absoluteTarget,
                            true);

                        savedSuccessfully =
                            true;

                        break;
                    }
                    catch (IOException)
                    {
                        if (attempt >=
                            MaximumReplacementAttempts)
                        {
                            throw;
                        }

                        AssetDatabase.ReleaseCachedFileHandles();

                        Thread.Sleep(
                            attempt == 1
                                ? 120
                                : 240);
                    }
                }

                if (!savedSuccessfully)
                    return null;

                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                return AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
            }
            finally
            {
                AssetDatabase.DeleteAsset(
                    stagingPath);
            }
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static void ClearItemPrefabReference(
            RestaurantPlaceableItemDefinition item)
        {
            if (item == null)
                return;

            SerializedObject serialized =
                new SerializedObject(
                    item);

            SerializedProperty property =
                RequireProperty(
                    serialized,
                    "prefab");

            if (property.objectReferenceValue ==
                null)
            {
                return;
            }

            property.objectReferenceValue =
                null;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);
        }

        private static void LinkItemToPrefab(
            RestaurantPlaceableItemDefinition item,
            RestaurantPlaceableObject prefabPlaceable)
        {
            SerializedObject serialized =
                new SerializedObject(
                    item);

            RequireProperty(
                serialized,
                "prefab").objectReferenceValue =
                    prefabPlaceable;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);
        }

        private static void RegisterInCatalog(
            RestaurantPlaceableCatalogDefinition catalog,
            RestaurantPlaceableItemDefinition item)
        {
            SerializedObject serialized =
                new SerializedObject(
                    catalog);

            SerializedProperty items =
                RequireProperty(
                    serialized,
                    "items");

            for (int index = 0;
                 index < items.arraySize;
                 index++)
            {
                SerializedProperty element =
                    items.GetArrayElementAtIndex(
                        index);

                if (ReferenceEquals(
                        element.objectReferenceValue,
                        item))
                {
                    return;
                }

                RestaurantPlaceableItemDefinition candidate =
                    element.objectReferenceValue as
                        RestaurantPlaceableItemDefinition;

                if (candidate != null &&
                    string.Equals(
                        candidate.ItemId,
                        item.ItemId,
                        StringComparison.Ordinal))
                {
                    element.objectReferenceValue =
                        item;

                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    EditorUtility.SetDirty(
                        catalog);

                    return;
                }
            }

            int newIndex =
                items.arraySize;

            items.InsertArrayElementAtIndex(
                newIndex);

            items.GetArrayElementAtIndex(
                newIndex).objectReferenceValue =
                    item;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                catalog);
        }

        private static void StampManagedAsset(
            Object asset,
            params string[] labels)
        {
            if (asset == null)
                return;

            HashSet<string> merged =
                new HashSet<string>(
                    AssetDatabase.GetLabels(
                        asset),
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                string label =
                    labels[index];

                if (!string.IsNullOrWhiteSpace(
                        label))
                {
                    merged.Add(
                        label);
                }
            }

            string[] finalLabels =
                new string[
                    merged.Count];

            merged.CopyTo(
                finalLabels);

            Array.Sort(
                finalLabels,
                StringComparer.Ordinal);

            AssetDatabase.SetLabels(
                asset,
                finalLabels);
        }

        private static T RequireComponent<T>(
            GameObject root)
            where T : Component
        {
            T component =
                root.GetComponent<T>();

            if (component == null)
            {
                throw new InvalidOperationException(
                    "Canonical chair template is missing " +
                    typeof(T).Name +
                    ".");
            }

            return component;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject
                        .GetType().Name +
                    " no longer exposes serialized property '" +
                    propertyName +
                    "'. SAVIC integration requires migration.");
            }

            return property;
        }

        private static SerializedProperty RequireRelative(
            SerializedProperty parent,
            string propertyName)
        {
            SerializedProperty property =
                parent.FindPropertyRelative(
                    propertyName);

            if (property == null)
            {
                throw new InvalidOperationException(
                    "Chair operational-clearance schema no longer exposes '" +
                    propertyName +
                    "'.");
            }

            return property;
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string name)
        {
            Transform child =
                parent.Find(
                    name);

            if (child != null &&
                child.parent ==
                    parent)
            {
                return child;
            }

            GameObject created =
                new GameObject(
                    name);

            created.transform.SetParent(
                parent,
                false);

            return created.transform;
        }

        private static void SetIdentity(
            Transform transform)
        {
            transform.localPosition =
                Vector3.zero;

            transform.localRotation =
                Quaternion.identity;

            transform.localScale =
                Vector3.one;
        }

        private static void EnsureCanonicalContentId(
            SavicManifest manifest)
        {
            if (!string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                if (!manifest.canonicalContentId.StartsWith(
                        "bb_chair_",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Existing CanonicalContentId belongs to another content family.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    manifest.savicId))
            {
                throw new InvalidOperationException(
                    "Manifest has no SAVIC identity.");
            }

            manifest.canonicalContentId =
                "bb_chair_" +
                manifest.savicId
                    .Trim()
                    .ToLowerInvariant();
        }

        private static string BuildDisplayName(
            string originalFileName)
        {
            string stem =
                Path.GetFileNameWithoutExtension(
                    originalFileName ??
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    stem))
            {
                return "Silla SAVIC";
            }

            string normalized =
                stem.Replace(
                        '_',
                        ' ')
                    .Replace(
                        '-',
                        ' ');

            string[] rawTokens =
                normalized.Split(
                    new[] { ' ' },
                    StringSplitOptions
                        .RemoveEmptyEntries);

            List<string> kept =
                new List<string>(
                    rawTokens.Length);

            for (int index = 0;
                 index < rawTokens.Length;
                 index++)
            {
                string token =
                    rawTokens[index].Trim();

                if (ShouldDiscardDisplayToken(
                        token))
                {
                    continue;
                }

                kept.Add(
                    token);
            }

            if (kept.Count == 0)
                return "Silla SAVIC";

            string display =
                string.Join(
                    " ",
                    kept);

            return CultureInfo
                .InvariantCulture
                .TextInfo
                .ToTitleCase(
                    display.ToLowerInvariant());
        }

        private static bool ShouldDiscardDisplayToken(
            string token)
        {
            if (string.IsNullOrWhiteSpace(
                    token))
            {
                return true;
            }

            if (string.Equals(
                    token,
                    "meshy",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    token,
                    "ai",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    token,
                    "generate",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    token,
                    "generated",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int digitCount =
                0;

            for (int index = 0;
                 index < token.Length;
                 index++)
            {
                if (char.IsDigit(
                        token[index]))
                {
                    digitCount++;
                }
            }

            return digitCount >= 6 &&
                   digitCount ==
                   token.Length;
        }

        private static string ShortIdentity(
            string canonicalContentId)
        {
            if (string.IsNullOrWhiteSpace(
                    canonicalContentId))
            {
                return "Unknown";
            }

            const int MaximumLength =
                20;

            return canonicalContentId.Length <=
                   MaximumLength
                ? canonicalContentId
                : canonicalContentId.Substring(
                    canonicalContentId.Length -
                    MaximumLength,
                    MaximumLength);
        }

        private static bool Approximately(
            float first,
            float second,
            float tolerance)
        {
            return Math.Abs(
                       first -
                       second) <=
                   Math.Max(
                       0.000001f,
                       tolerance);
        }

        private static void EnsureAssetFolder(
            string assetFolder)
        {
            string normalized =
                assetFolder.Replace(
                    '\\',
                    '/');

            if (!normalized.StartsWith(
                    "Assets",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generated asset folder must live under Assets.");
            }

            string[] segments =
                normalized.Split('/');

            string current =
                segments[0];

            for (int index = 1;
                 index < segments.Length;
                 index++)
            {
                string next =
                    current +
                    "/" +
                    segments[index];

                if (!AssetDatabase.IsValidFolder(
                        next))
                {
                    string guid =
                        AssetDatabase.CreateFolder(
                            current,
                            segments[index]);

                    if (string.IsNullOrWhiteSpace(
                            guid))
                    {
                        throw new IOException(
                            "Unity could not create asset folder: " +
                            next);
                    }
                }

                current =
                    next;
            }
        }

        private static SavicChairPublicationOutcome Fail(
            string message,
            string prefabPath = "",
            string itemPath = "")
        {
            return new SavicChairPublicationOutcome(
                false,
                message,
                prefabPath,
                itemPath);
        }
    }
}
