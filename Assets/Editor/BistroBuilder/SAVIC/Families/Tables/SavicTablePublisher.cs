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
    internal readonly struct SavicTablePublicationOutcome
    {
        internal SavicTablePublicationOutcome(
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

    internal sealed class SavicTablePublisher
    {
        internal const string Version = "1.2.0";
        private const string LegacyCompatibleVersion = "1.1.0";
        private const string PublicationFingerprintSchema =
            "table-publication-input-v3";

        private const string GeneratedTablesRoot =
            "Assets/Generated/BistroBuilder/SAVIC/Published/Tables";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string TableEditableDefinitionPath =
            "Assets/Data/Restaurant/EditMode/EditableDefinitions/" +
            "EditableObjectDefinition_Table.asset";

        private const string SourceMirrorArtifactRole =
            "unity.source_mirror";

        private const string PrefabArtifactRole =
            "published.table.prefab";

        private const string ItemArtifactRole =
            "catalog.item_definition";

        private const string LargePreviewArtifactRole =
            "preview.large";

        private const string CatalogPreviewArtifactRole =
            "preview.catalog";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicTablePublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicTablePublicationOutcome Publish(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicTableAuthoringRecord plan =
                manifest.tableAuthoring;

            if (plan == null || !plan.planned)
            {
                return Fail(
                    "Table publication requires a valid authoring plan.");
            }

            if (string.IsNullOrWhiteSpace(
                    plan.templatePrefabAssetPath) ||
                string.IsNullOrWhiteSpace(
                    plan.seatingDefinitionAssetPath))
            {
                return Fail(
                    "Table authoring plan has no canonical template references.");
            }

            EnsureCanonicalContentId(manifest);

            string publicationFingerprint =
                BuildPublicationFingerprint(
                    manifest,
                    plan);

            string contentFolder =
                GeneratedTablesRoot +
                "/" +
                manifest.canonicalContentId;

            EnsureAssetFolder(contentFolder);

            string itemPath =
                contentFolder +
                "/PlaceableItem_" +
                manifest.canonicalContentId +
                ".asset";

            string prefabPath =
                contentFolder +
                "/Table_" +
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
                        TableEditableDefinitionPath);

            RestaurantTableSeatingConfigurationDefinition seatingDefinition =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantTableSeatingConfigurationDefinition>(
                        plan.seatingDefinitionAssetPath);

            GameObject template =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    plan.templatePrefabAssetPath);

            if (catalog == null ||
                editableDefinition == null ||
                seatingDefinition == null ||
                template == null)
            {
                return Fail(
                    "One or more canonical Bistro Builder table dependencies are missing.");
            }

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_table_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(MainCatalogPath);
            transaction.CaptureAsset(itemPath);
            transaction.CaptureAsset(prefabPath);
            transaction.CaptureAsset(largePreviewPath);
            transaction.CaptureAsset(catalogPreviewPath);

            GameObject workingRoot = null;

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
                        prefabPath,
                        publicationFingerprint);

                if (!reusePublishedPrefab)
                {
                    ClearItemPrefabReference(item);
                    AssetDatabase.SaveAssets();

                    workingRoot =
                        BuildWorkingTable(
                            template,
                            sourceModelAsset,
                            item,
                            seatingDefinition,
                            editableDefinition,
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
                            "Unity did not confirm the generated table prefab save.");
                    }
                }

                RestaurantPlaceableObject savedPlaceable =
                    savedPrefab.GetComponent<RestaurantPlaceableObject>();

                if (savedPlaceable == null)
                {
                    throw new InvalidOperationException(
                        "Generated prefab has no RestaurantPlaceableObject.");
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
                    "SAVIC.Table",
                    "SAVIC.CatalogItem");

                GameObject reloadedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                if (reloadedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Generated table prefab could not be reloaded.");
                }

                StampManagedAsset(
                    reloadedPrefab,
                    "SAVIC.Managed",
                    "SAVIC.Table",
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
                        "Preview generation failed: " +
                        previews.Message);
                }

                AssetDatabase.SaveAssets();

                manifest.tableSpatial =
                    SavicTableSpatialReadinessValidator.Validate(
                        manifest,
                        plan,
                        prefabPath);

                manifest.tableNavigation =
                    SavicTableNavigationReadinessValidator.Validate(
                        manifest,
                        plan,
                        prefabPath);

                manifest.tablePersistence =
                    SavicTablePersistenceReadinessValidator.Validate(
                        manifest,
                        prefabPath);

                ValidatePublishedTable(
                    reloadedPrefab,
                    item,
                    catalog,
                    manifest,
                    plan);

                plan.prefabAssetPath = prefabPath;
                plan.itemDefinitionAssetPath = itemPath;
                manifest.tableAuthoring = plan;
                manifest.status = "PUBLISHED";

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    prefabPath,
                    "savic.table-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    itemPath,
                    "savic.table-publisher",
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
                    "Navigation.TableReadiness",
                    "PASS",
                    "INFO",
                    manifest.tableNavigation.evidence,
                    SavicTableNavigationReadinessValidator.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "SaveLoad.TableCatalogReadiness",
                    "PASS",
                    "INFO",
                    manifest.tablePersistence.evidence,
                    SavicTablePersistenceReadinessValidator.Version);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Publication.TablePrefab",
                    "PASS",
                    "INFO",
                    "Table prefab, catalog item and canonical integrations validated.",
                    Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "table.capacity",
                    plan.capacity.ToString(
                        CultureInfo.InvariantCulture),
                    "HIGH",
                    "Capacity selected from normalized dimensions and canonical seating constraints.",
                    "table.authoring.capacity.v1");

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "table.uniformScale",
                    plan.uniformScale.ToString(
                        "0.######",
                        CultureInfo.InvariantCulture),
                    plan.scaleCorrectionApplied
                        ? "HIGH"
                        : "CERTAIN",
                    plan.planReason,
                    "table.authoring.scale.v1");

                manifests.Save(manifest);
                transaction.Commit();

                return new SavicTablePublicationOutcome(
                    true,
                    itemWasCreated
                        ? "Table published and catalog item created."
                        : "Table republished while preserving manual catalog values.",
                    prefabPath,
                    itemPath);
            }
            catch (Exception exception)
            {
                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Publication.TablePrefab",
                    "FAIL",
                    "ERROR",
                    exception.Message,
                    Version);

                manifest.status = "NEEDS_REVIEW";

                try
                {
                    manifests.Save(manifest);
                }
                catch (Exception saveException)
                {
                    Debug.LogError(
                        "[SAVIC] Could not persist table publication failure: " +
                        saveException);
                }

                return Fail(
                    "Table publication failed: " +
                    exception.Message,
                    prefabPath,
                    itemPath);
            }
            finally
            {
                if (workingRoot != null)
                    Object.DestroyImmediate(workingRoot);
            }
        }

        internal SavicTablePublicationOutcome RefreshAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicTableAuthoringRecord plan =
                manifest.tableAuthoring;

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
                    "Visual/SourceModel") == null)
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
                    "Table appearance refresh could not resolve content folder.",
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
                    "refresh_table_appearance_" +
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
                        "Visual");

                if (visualRoot == null)
                {
                    throw new InvalidOperationException(
                        "Published table prefab has no Visual root.");
                }

                Transform previousSource =
                    visualRoot.Find(
                        "SourceModel");

                if (previousSource == null)
                {
                    throw new InvalidOperationException(
                        "Published table prefab has no SourceModel child.");
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

                RemoveUnsupportedStaticFurnitureComponents(
                    sourceInstance);

                Renderer stateRenderer =
                    sourceInstance.GetComponentInChildren
                        <Renderer>(true);

                if (stateRenderer == null)
                {
                    throw new InvalidOperationException(
                        "Refreshed table source contains no renderer.");
                }

                TableStateView stateView =
                    prefabContents.GetComponent<TableStateView>();

                if (stateView != null)
                {
                    SerializedObject stateViewSerialized =
                        new SerializedObject(
                            stateView);

                    RequireProperty(
                        stateViewSerialized,
                        "tableRenderer").objectReferenceValue =
                            stateRenderer;

                    stateViewSerialized
                        .ApplyModifiedPropertiesWithoutUndo();
                }

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        prefabContents,
                        prefabPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not confirm table appearance-only prefab save.");
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
                        "Appearance-refreshed table prefab could not be reloaded.");
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
                        "Preview refresh failed: " +
                        previews.Message);
                }

                AssetDatabase.SaveAssets();

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    prefabPath,
                    "savic.table-publisher",
                    Version,
                    publicationFingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    itemPath,
                    "savic.table-publisher",
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
                    "Table visual source and previews refreshed without rebuilding colliders, spatial, navigation or persistence topology.",
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

                return new SavicTablePublicationOutcome(
                    true,
                    "Table appearance refreshed incrementally; geometry/colliders were reused.",
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
                    "Incremental table appearance refresh failed: " +
                    exception.Message,
                    prefabPath,
                    itemPath);
            }
        }

        private static string BuildPublicationFingerprint(
            SavicManifest manifest,
            SavicTableAuthoringRecord plan)
        {
            if (manifest?.source == null)
            {
                throw new InvalidOperationException(
                    "Cannot fingerprint a table publication without source metadata.");
            }

            string templateHash =
                RequireDependencyHash(
                    plan.templatePrefabAssetPath);

            string seatingHash =
                RequireDependencyHash(
                    plan.seatingDefinitionAssetPath);

            string editableHash =
                RequireDependencyHash(
                    TableEditableDefinitionPath);

            string canonical =
                string.Join(
                    "|",
                    new[]
                    {
                        "savic.table.publication",
                        PublicationFingerprintSchema,
                        Version,
                        manifest.source.sourceHash ?? string.Empty,
                        manifest.incremental?.geometryFingerprint ?? string.Empty,
                        manifest.incremental?.appearanceFingerprint ?? string.Empty,
                        plan.plannerVersion ?? string.Empty,
                        plan.uniformScale.ToString("R", CultureInfo.InvariantCulture),
                        plan.visualYawDegrees.ToString("R", CultureInfo.InvariantCulture),
                        plan.finalWidthMeters.ToString("R", CultureInfo.InvariantCulture),
                        plan.finalHeightMeters.ToString("R", CultureInfo.InvariantCulture),
                        plan.finalDepthMeters.ToString("R", CultureInfo.InvariantCulture),
                        plan.capacity.ToString(CultureInfo.InvariantCulture),
                        plan.templatePrefabAssetPath ?? string.Empty,
                        templateHash,
                        plan.seatingDefinitionAssetPath ?? string.Empty,
                        seatingHash,
                        TableEditableDefinitionPath,
                        editableHash,
                        SavicSemanticPartAnalyzer.Version,
                        SavicTableColliderBuilder.Version
                    });

            return SavicHashService.ComputeSha256Text(
                canonical);
        }

        private static string RequireDependencyHash(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                throw new InvalidOperationException(
                    "Cannot fingerprint missing dependency: " +
                    assetPath);
            }

            return AssetDatabase
                .GetAssetDependencyHash(assetPath)
                .ToString();
        }

        private static bool CanReusePublishedPrefab(
            SavicManifest manifest,
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
                    "savic.table-publisher",
                    StringComparison.Ordinal))
            {
                return false;
            }

            bool currentBuilder =
                string.Equals(
                    artifact.builderVersion,
                    Version,
                    StringComparison.Ordinal);

            bool legacyCompatibleBuilder =
                string.Equals(
                    artifact.builderVersion,
                    LegacyCompatibleVersion,
                    StringComparison.Ordinal);

            if (!currentBuilder &&
                !legacyCompatibleBuilder)
            {
                return false;
            }

            if (currentBuilder &&
                string.Equals(
                    artifact.inputFingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }

            // Safe migration/revalidation path: analyzer/classifier upgrades
            // must not force a prefab rebuild when the actual visual source,
            // geometry plan and canonical runtime contract are unchanged.
            return ExistingPrefabMatchesPlan(
                       prefabPath,
                       manifest,
                       manifest.tableAuthoring) &&
                   ExistingPrefabUsesCurrentSource(
                       manifest,
                       prefabPath);
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
                    StringComparison.Ordinal))
            {
                return false;
            }

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    expectedPath);

            if (sprite == null)
                return false;

            if (string.Equals(
                    artifact.inputFingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }

            // One-time adoption of previews generated by this exact renderer
            // version before artifact fingerprints were introduced.
            return string.IsNullOrWhiteSpace(
                artifact.inputFingerprint);
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

        private static bool ExistingPrefabMatchesPlan(
            string prefabPath,
            SavicManifest manifest,
            SavicTableAuthoringRecord plan)
        {
            if (plan == null ||
                !plan.planned)
            {
                return false;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null)
                return false;

            RestaurantTable table =
                prefab.GetComponent<RestaurantTable>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            RestaurantPlaceableObject placeable =
                prefab.GetComponent<RestaurantPlaceableObject>();

            RestaurantTableSeatingConfiguration seating =
                prefab.GetComponent
                    <RestaurantTableSeatingConfiguration>();

            Renderer[] renderers =
                prefab.GetComponentsInChildren
                    <Renderer>(true);

            if (table == null ||
                footprint == null ||
                placeable == null ||
                seating == null ||
                renderers.Length == 0 ||
                table.Capacity != plan.capacity)
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

            return SavicTableColliderBuilder.Validate(
                prefab,
                manifest,
                plan,
                manifest.tableColliders,
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

        private static GameObject SavePrefabWithBoundedRetry(
            GameObject workingRoot,
            string prefabPath,
            out bool savedSuccessfully)
        {
            savedSuccessfully = false;

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

                return
                    savedSuccessfully
                        ? created
                        : null;
            }

            // Updating an existing prefab through Unity's direct temp-file move
            // can fail on Windows while the old asset has a transient read
            // handle. Generate a complete prefab at a unique staging path first,
            // then replace only the YAML payload. The target .meta is untouched,
            // so its stable GUID is preserved.
            string directory =
                Path.GetDirectoryName(
                    prefabPath)
                ?.Replace('\\', '/')
                ?? throw new InvalidOperationException(
                    "Prefab directory could not be resolved.");

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

                const int MaximumReplacementAttempts = 3;

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

                return
                    AssetDatabase.LoadAssetAtPath<GameObject>(
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
                new SerializedObject(item);

            SerializedProperty property =
                RequireProperty(
                    serialized,
                    "prefab");

            if (property.objectReferenceValue == null)
                return;

            property.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static RestaurantPlaceableItemDefinition
            GetOrCreateItemDefinition(
                string itemPath,
                SavicManifest manifest,
                SavicTableAuthoringRecord plan,
                RestaurantEditableObjectDefinition editableDefinition,
                out bool wasCreated)
        {
            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        itemPath);

            wasCreated = item == null;

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
                new SerializedObject(item);

            RequireProperty(
                serialized,
                "itemId").stringValue =
                    manifest.canonicalContentId;

            RequireProperty(
                serialized,
                "category").enumValueIndex =
                    (int)RestaurantPlaceableItemCategory.Furniture;

            RequireProperty(
                serialized,
                "placementScope").enumValueIndex =
                    (int)RestaurantPlaceableEnvironmentScope
                        .InteriorAndExterior;

            RequireProperty(
                serialized,
                "dimensionsCentimeters").vector3Value =
                    new Vector3(
                        plan.finalWidthMeters * 100f,
                        plan.finalHeightMeters * 100f,
                        plan.finalDepthMeters * 100f);

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
                        "Mesa integrada automáticamente por SAVIC.";

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
            EditorUtility.SetDirty(item);
            return item;
        }

        private static GameObject BuildWorkingTable(
            GameObject template,
            GameObject sourceModelAsset,
            RestaurantPlaceableItemDefinition item,
            RestaurantTableSeatingConfigurationDefinition
                seatingDefinition,
            RestaurantEditableObjectDefinition editableDefinition,
            SavicManifest manifest,
            SavicTableAuthoringRecord plan)
        {
            GameObject root =
                PrefabUtility.InstantiatePrefab(
                    template) as GameObject;

            if (root == null)
            {
                throw new InvalidOperationException(
                    "Canonical table template could not be instantiated.");
            }

            PrefabUtility.UnpackPrefabInstance(
                root,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            root.name =
                "SAVIC_Table_" +
                ShortIdentity(
                    manifest.canonicalContentId);

            root.transform.position =
                Vector3.zero;
            root.transform.rotation =
                Quaternion.identity;
            root.transform.localScale =
                Vector3.one;

            RemoveTemplateVisual(root);

            Transform placementAnchor =
                EnsureDirectChild(
                    root.transform,
                    "PlacementAnchor");

            Transform customerApproach =
                EnsureDirectChild(
                    root.transform,
                    "CustomerApproachPoint");

            Transform waiterService =
                EnsureDirectChild(
                    root.transform,
                    "WaiterServicePoint");

            placementAnchor.localPosition =
                Vector3.zero;
            placementAnchor.localRotation =
                Quaternion.identity;
            placementAnchor.localScale =
                Vector3.one;

            float interactionOffset =
                Math.Max(
                    0.65f,
                    plan.finalWidthMeters * 0.25f);

            customerApproach.localPosition =
                new Vector3(
                    -plan.finalWidthMeters * 0.5f -
                    interactionOffset,
                    0f,
                    0f);

            waiterService.localPosition =
                new Vector3(
                    plan.finalWidthMeters * 0.5f +
                    interactionOffset,
                    0f,
                    0f);

            Transform visualRoot =
                EnsureDirectChild(
                    root.transform,
                    "Visual");

            visualRoot.localPosition =
                Vector3.zero;
            visualRoot.localRotation =
                Quaternion.identity;
            visualRoot.localScale =
                Vector3.one;

            GameObject sourceInstance =
                InstantiateSourceModel(
                    sourceModelAsset,
                    visualRoot);

            NormalizeSourceVisual(
                sourceInstance.transform,
                manifest.model3D,
                plan);

            RemoveUnsupportedStaticFurnitureComponents(
                sourceInstance);

            Renderer stateRenderer =
                sourceInstance.GetComponentInChildren
                    <Renderer>(true);

            if (stateRenderer == null)
            {
                throw new InvalidOperationException(
                    "Imported table source contains no renderer.");
            }

            manifest.tableColliders =
                SavicTableColliderBuilder.Build(
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
                0.05f,
                true,
                0.02f);

            RestaurantEditableObject editable =
                RequireComponent
                    <RestaurantEditableObject>(
                        root);

            editable.SetDefinition(
                editableDefinition);

            RestaurantTable table =
                RequireComponent<RestaurantTable>(
                    root);

            ConfigureTableComponent(
                table,
                plan.capacity,
                customerApproach,
                waiterService);

            RestaurantTableSeatingConfiguration seating =
                RequireComponent
                    <RestaurantTableSeatingConfiguration>(
                        root);

            ConfigureSeatingComponent(
                seating,
                table,
                footprint,
                seatingDefinition,
                placementAnchor);

            RestaurantPlaceableObject placeable =
                RequireComponent
                    <RestaurantPlaceableObject>(
                        root);

            ConfigurePlaceableComponent(
                placeable,
                item,
                placementAnchor);

            RestaurantAreaMember areaMember =
                RequireComponent
                    <RestaurantAreaMember>(
                        root);

            if (areaMember.RequiredCapabilityCount <= 0)
            {
                throw new InvalidOperationException(
                    "Canonical table template has no area capability requirement.");
            }

            TableStateView tableStateView =
                root.GetComponent<TableStateView>();

            if (tableStateView != null)
            {
                SerializedObject stateView =
                    new SerializedObject(
                        tableStateView);

                RequireProperty(
                    stateView,
                    "restaurantTable").objectReferenceValue =
                        table;

                RequireProperty(
                    stateView,
                    "tableRenderer").objectReferenceValue =
                        stateRenderer;

                stateView.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static void RemoveTemplateVisual(
            GameObject root)
        {
            MeshRenderer renderer =
                root.GetComponent<MeshRenderer>();

            if (renderer != null)
                Object.DestroyImmediate(renderer);

            MeshFilter filter =
                root.GetComponent<MeshFilter>();

            if (filter != null)
                Object.DestroyImmediate(filter);

            Transform existingVisual =
                root.transform.Find("Visual");

            if (existingVisual != null)
                Object.DestroyImmediate(
                    existingVisual.gameObject);
        }

        private static GameObject InstantiateSourceModel(
            GameObject sourceModelAsset,
            Transform parent)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    sourceModelAsset) as GameObject;

            if (instance == null)
                instance = Object.Instantiate(sourceModelAsset);

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Imported source model could not be instantiated.");
            }

            if (PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            instance.name = "SourceModel";
            instance.transform.SetParent(
                parent,
                false);

            return instance;
        }

        private static void NormalizeSourceVisual(
            Transform source,
            SavicModelAnalysisRecord analysis,
            SavicTableAuthoringRecord plan)
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
                yaw * scaledCenter;

            float sourceBottom =
                (analysis.boundsCenterY -
                 analysis.heightMeters * 0.5f) *
                plan.uniformScale;

            source.localRotation = yaw;
            source.localScale =
                Vector3.one *
                plan.uniformScale;

            source.localPosition =
                new Vector3(
                    -rotatedCenter.x,
                    -sourceBottom,
                    -rotatedCenter.z);
        }

        private static void RemoveUnsupportedStaticFurnitureComponents(
            GameObject root)
        {
            foreach (Camera camera in
                     root.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(camera);
            }

            foreach (Light light in
                     root.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light);
            }

            foreach (AudioSource audio in
                     root.GetComponentsInChildren<AudioSource>(true))
            {
                Object.DestroyImmediate(audio);
            }

            foreach (Animator animator in
                     root.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(animator);
            }

            foreach (Animation animation in
                     root.GetComponentsInChildren<Animation>(true))
            {
                Object.DestroyImmediate(animation);
            }

            foreach (Collider collider in
                     root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ConfigureTableComponent(
            RestaurantTable table,
            int capacity,
            Transform customerApproach,
            Transform waiterService)
        {
            SerializedObject serialized =
                new SerializedObject(table);

            RequireProperty(
                serialized,
                "tableId").intValue =
                    1;

            RequireProperty(
                serialized,
                "capacity").intValue =
                    Math.Max(1, capacity);

            RequireProperty(
                serialized,
                "customerApproachPoint").objectReferenceValue =
                    customerApproach;

            RequireProperty(
                serialized,
                "waiterServicePoint").objectReferenceValue =
                    waiterService;

            RequireProperty(
                serialized,
                "assignedCustomerGroup").objectReferenceValue =
                    null;

            RequireProperty(
                serialized,
                "currentState").enumValueIndex =
                    0;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSeatingComponent(
            RestaurantTableSeatingConfiguration seating,
            RestaurantTable table,
            RestaurantPlacementFootprint footprint,
            RestaurantTableSeatingConfigurationDefinition definition,
            Transform seatingCenter)
        {
            SerializedObject serialized =
                new SerializedObject(seating);

            RequireProperty(
                serialized,
                "table").objectReferenceValue =
                    table;

            RequireProperty(
                serialized,
                "placementFootprint").objectReferenceValue =
                    footprint;

            RequireProperty(
                serialized,
                "definition").objectReferenceValue =
                    definition;

            RequireProperty(
                serialized,
                "seatingCenter").objectReferenceValue =
                    seatingCenter;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlaceableComponent(
            RestaurantPlaceableObject placeable,
            RestaurantPlaceableItemDefinition item,
            Transform placementAnchor)
        {
            placeable.SetItemDefinition(item);

            SerializedObject serialized =
                new SerializedObject(placeable);

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

        private static void LinkItemToPrefab(
            RestaurantPlaceableItemDefinition item,
            RestaurantPlaceableObject prefabPlaceable)
        {
            SerializedObject serialized =
                new SerializedObject(item);

            RequireProperty(
                serialized,
                "prefab").objectReferenceValue =
                    prefabPlaceable;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void RegisterInCatalog(
            RestaurantPlaceableCatalogDefinition catalog,
            RestaurantPlaceableItemDefinition item)
        {
            SerializedObject serialized =
                new SerializedObject(catalog);

            SerializedProperty items =
                RequireProperty(
                    serialized,
                    "items");

            for (int index = 0;
                 index < items.arraySize;
                 index++)
            {
                SerializedProperty element =
                    items.GetArrayElementAtIndex(index);

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
                    element.objectReferenceValue = item;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalog);
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
            EditorUtility.SetDirty(catalog);
        }

        private static void ValidatePublishedTable(
            GameObject prefab,
            RestaurantPlaceableItemDefinition item,
            RestaurantPlaceableCatalogDefinition catalog,
            SavicManifest manifest,
            SavicTableAuthoringRecord plan)
        {
            RestaurantPlaceableObject placeable =
                prefab.GetComponent<RestaurantPlaceableObject>();

            RestaurantTable table =
                prefab.GetComponent<RestaurantTable>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            RestaurantTableSeatingConfiguration seating =
                prefab.GetComponent
                    <RestaurantTableSeatingConfiguration>();

            Renderer[] renderers =
                prefab.GetComponentsInChildren
                    <Renderer>(true);

            if (placeable == null ||
                table == null ||
                footprint == null ||
                seating == null ||
                renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Generated table prefab is missing required canonical components.");
            }

            if (item.CatalogIcon == null ||
                item.InspectorPreview == null)
            {
                throw new InvalidOperationException(
                    "Generated catalog item has no usable SAVIC previews.");
            }

            if (!placeable.ValidateConfiguration(
                    out string placeableError))
            {
                throw new InvalidOperationException(
                    "Placeable validation failed: " +
                    placeableError);
            }

            if (!seating.ValidateConfiguration(
                    out string seatingError))
            {
                throw new InvalidOperationException(
                    "Seating validation failed: " +
                    seatingError);
            }

            if (table.Capacity != plan.capacity)
            {
                throw new InvalidOperationException(
                    "Published table capacity does not match the authoring plan.");
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
                    "Published placement footprint does not match normalized dimensions.");
            }

            if (!SavicTableColliderBuilder.Validate(
                    prefab,
                    manifest,
                    plan,
                    manifest.tableColliders,
                    out string colliderError))
            {
                throw new InvalidOperationException(
                    "Published semantic collider validation failed: " +
                    colliderError);
            }

            if (!ReferenceEquals(
                    placeable.ItemDefinition,
                    item))
            {
                throw new InvalidOperationException(
                    "Published prefab does not reference its catalog item.");
            }

            if (!catalog.TryGetItem(
                    manifest.canonicalContentId,
                    out RestaurantPlaceableItemDefinition
                        catalogItem) ||
                !ReferenceEquals(
                    catalogItem,
                    item))
            {
                throw new InvalidOperationException(
                    "Main placeable catalog does not resolve the generated item.");
            }
        }

        private static void StampManagedAsset(
            Object asset,
            params string[] labels)
        {
            if (asset == null)
                return;

            HashSet<string> merged =
                new HashSet<string>(
                    AssetDatabase.GetLabels(asset),
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                string label =
                    labels[index];

                if (!string.IsNullOrWhiteSpace(label))
                    merged.Add(label);
            }

            string[] finalLabels =
                new string[merged.Count];

            merged.CopyTo(finalLabels);
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
                    "Canonical table template is missing " +
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
                    serialized.targetObject.GetType().Name +
                    " no longer exposes serialized property '" +
                    propertyName +
                    "'. SAVIC integration requires migration.");
            }

            return property;
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string name)
        {
            Transform child =
                parent.Find(name);

            if (child != null &&
                child.parent == parent)
            {
                return child;
            }

            GameObject created =
                new GameObject(name);

            created.transform.SetParent(
                parent,
                false);

            return created.transform;
        }

        private static void EnsureCanonicalContentId(
            SavicManifest manifest)
        {
            if (!string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    manifest.savicId))
            {
                throw new InvalidOperationException(
                    "Manifest has no SAVIC identity.");
            }

            manifest.canonicalContentId =
                "bb_table_" +
                manifest.savicId
                    .Trim()
                    .ToLowerInvariant();
        }

        private static string BuildDisplayName(
            string originalFileName)
        {
            string stem =
                Path.GetFileNameWithoutExtension(
                    originalFileName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(stem))
                return "Mesa SAVIC";

            string normalized =
                stem.Replace('_', ' ')
                    .Replace('-', ' ');

            string[] rawTokens =
                normalized.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            List<string> kept =
                new List<string>(
                    rawTokens.Length);

            for (int index = 0;
                 index < rawTokens.Length;
                 index++)
            {
                string token =
                    rawTokens[index].Trim();

                if (ShouldDiscardDisplayToken(token))
                    continue;

                kept.Add(token);
            }

            if (kept.Count == 0)
                return "Mesa SAVIC";

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
            if (string.IsNullOrWhiteSpace(token))
                return true;

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

            int digitCount = 0;

            for (int index = 0;
                 index < token.Length;
                 index++)
            {
                if (char.IsDigit(token[index]))
                    digitCount++;
            }

            return digitCount >= 6 &&
                   digitCount == token.Length;
        }

        private static string ShortIdentity(
            string canonicalContentId)
        {
            if (string.IsNullOrWhiteSpace(
                    canonicalContentId))
            {
                return "Unknown";
            }

            const int maximumLength = 20;

            return canonicalContentId.Length <=
                   maximumLength
                ? canonicalContentId
                : canonicalContentId.Substring(
                    canonicalContentId.Length -
                    maximumLength,
                    maximumLength);
        }

        private static bool Approximately(
            float first,
            float second,
            float tolerance)
        {
            return Math.Abs(first - second) <=
                   Math.Max(0.000001f, tolerance);
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

                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid =
                        AssetDatabase.CreateFolder(
                            current,
                            segments[index]);

                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        throw new IOException(
                            "Unity could not create asset folder: " +
                            next);
                    }
                }

                current = next;
            }
        }

        private static SavicTablePublicationOutcome Fail(
            string message,
            string prefabPath = "",
            string itemPath = "")
        {
            return new SavicTablePublicationOutcome(
                false,
                message,
                prefabPath,
                itemPath);
        }
    }
}
