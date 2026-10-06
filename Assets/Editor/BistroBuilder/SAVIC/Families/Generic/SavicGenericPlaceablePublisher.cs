using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicGenericPlaceablePublicationOutcome
    {
        internal SavicGenericPlaceablePublicationOutcome(
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

    internal sealed class SavicGenericPlaceablePublisher
    {
        internal const string Version = "2.3.0";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string PrefabArtifactRole =
            "published.generic.prefab";

        private const string ItemArtifactRole =
            "catalog.item_definition";

        private const string EditableArtifactRole =
            "editmode.editable_definition";

        private const string LargePreviewArtifactRole =
            "preview.large";

        private const string CatalogPreviewArtifactRole =
            "preview.catalog";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicGenericPlaceablePublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));

            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicGenericPlaceablePublicationOutcome Publish(
            SavicManifest manifest,
            GameObject sourceModelAsset, bool prepareCandidateOnly = false)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicGenericPlaceableAuthoringRecord plan =
                manifest.genericPlaceable;

            if (plan == null ||
                !plan.planned)
            {
                return Fail(
                    "Generic publication requires a valid authoring plan.");
            }

            string contentFolder =
                Path.GetDirectoryName(
                    plan.prefabAssetPath)
                ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(contentFolder))
            {
                return Fail(
                    "Generic content folder could not be resolved.");
            }

            EnsureAssetFolder(
                contentFolder);

            string largePreviewPath =
                contentFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                contentFolder +
                "/Preview_Catalog.png";

            string fingerprint =
                BuildPublicationFingerprint(
                    manifest,
                    plan);

            if (prepareCandidateOnly && !SavicFunctionalRuntimeAcceptance.Required(manifest))
                return Fail("Candidate preparation is restricted to content requiring real runtime acceptance.");
            if (!prepareCandidateOnly && !SavicFunctionalRuntimeAcceptance.Matches(manifest, layout))
                return Fail("Functional publication requires current canonical runtime and SaveGame acceptance.");

            if (CanReuseExisting(
                    manifest,
                    plan,
                    fingerprint))
            {
                RestaurantPlaceableItemDefinition reusedItem =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantPlaceableItemDefinition>(
                            plan.itemDefinitionAssetPath);

                GameObject reusedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        plan.prefabAssetPath);

                if (reusedItem != null &&
                    reusedPrefab != null)
                {
                    using SavicAssetMutationScope reuseTransaction = new SavicAssetMutationScope(layout, "reuse_generic_" + manifest.canonicalContentId);
                    reuseTransaction.CaptureAsset(MainCatalogPath);
                    reuseTransaction.CaptureAsset(plan.itemDefinitionAssetPath);
                    if (manifest.assets4All?.delivery != null)
                    {
                        reuseTransaction.CaptureAsset(plan.prefabAssetPath);
                        reuseTransaction.CaptureAsset(largePreviewPath);
                        reuseTransaction.CaptureAsset(catalogPreviewPath);
                        SavicAssets4AllService.ApplyOverrides(manifest);
                        reusedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(plan.prefabAssetPath);
                    }
                    RefreshGeneratedDescription(reusedItem, plan.integrationMode == SavicBarCounterFunctionAdapter.Mode);
                    SavicPreviewGenerationResult reusedPreviews =
                        manifest.assets4All?.delivery != null && manifest.developerOverrides.Any(o => o.field.StartsWith("material:"))
                        ? SavicPreviewRenderer.GenerateAndAssign(reusedPrefab, reusedItem, contentFolder)
                        : SavicPreviewRenderer.ReuseAndAssign(
                            reusedItem,
                            largePreviewPath,
                            catalogPreviewPath);

                    if (reusedPreviews.Succeeded)
                    {
                        if (manifest.assets4All?.delivery != null)
                        {
                            string currentPreviewFingerprint = SavicPreviewRenderer.BuildInputFingerprint(plan.prefabAssetPath);
                            SavicManifestMutations.UpsertArtifact(manifest, LargePreviewArtifactRole, reusedPreviews.LargePreviewAssetPath,
                                "savic.preview-renderer", SavicPreviewRenderer.Version, currentPreviewFingerprint);
                            SavicManifestMutations.UpsertArtifact(manifest, CatalogPreviewArtifactRole, reusedPreviews.CatalogPreviewAssetPath,
                                "savic.preview-renderer", SavicPreviewRenderer.Version, currentPreviewFingerprint);
                        }
                        if (!prepareCandidateOnly) EnsureCatalogEntry(reusedItem);
                        AssetDatabase.SaveAssets();
                        if (!ValidateAndStampReadiness(
                                manifest,
                                plan,
                                out string readinessError, prepareCandidateOnly))
                        {
                            return Fail(
                                readinessError,
                                plan.prefabAssetPath,
                                plan.itemDefinitionAssetPath);
                        }

                        manifest.status = prepareCandidateOnly ? "NEEDS_REVIEW" : "PUBLISHED";

                        manifests.Save(
                            manifest);
                        reuseTransaction.Commit();

                        return new SavicGenericPlaceablePublicationOutcome(
                            true,
                            prepareCandidateOnly ? "Functional candidate reused; real runtime acceptance pending." : "Generic placeable reused unchanged accepted artifacts.",
                            plan.prefabAssetPath,
                            plan.itemDefinitionAssetPath);
                    }
                }
            }

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_generic_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(
                plan.editableDefinitionAssetPath);

            transaction.CaptureAsset(
                plan.itemDefinitionAssetPath);

            transaction.CaptureAsset(
                plan.prefabAssetPath);

            transaction.CaptureAsset(
                largePreviewPath);

            transaction.CaptureAsset(
                catalogPreviewPath);

            transaction.CaptureAsset(
                MainCatalogPath);
            string neutralMaterialPath = contentFolder + "/Material_NeutralFallback.mat";
            transaction.CaptureAsset(neutralMaterialPath);

            try
            {
                RestaurantEditableObjectDefinition editable =
                    LoadOrCreateEditable(
                        manifest,
                        plan);

                RestaurantPlaceableItemDefinition item =
                    LoadOrCreateItem(
                        manifest,
                        plan,
                        editable);

                BuildOrReplacePrefab(
                    manifest,
                    plan,
                    item,
                    editable,
                    sourceModelAsset);

                if (manifest.assets4All?.delivery != null)
                    SavicAssets4AllService.ApplyOverrides(manifest);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        plan.prefabAssetPath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Published generic prefab could not be loaded.");
                }

                if (SavicSourceMaterialFallback.CountInvalidSlots(prefab) != 0)
                    throw new InvalidOperationException("Published generic prefab contains invalid material slots.");
                Material neutral = AssetDatabase.LoadAssetAtPath<Material>(neutralMaterialPath);
                int neutralSlots = neutral == null ? 0 : prefab.GetComponentsInChildren<Renderer>(true)
                    .Sum(renderer => renderer.sharedMaterials.Count(material => material == neutral));
                SavicManifestMutations.UpsertValidation(manifest, "Presentation.GenericMaterials",
                    neutralSlots > 0 ? "WARNING" : "PASS", neutralSlots > 0 ? "WARNING" : "INFO",
                    neutralSlots > 0 ? neutralSlots + " missing or unsupported material slot(s) use a managed neutral material; source textures are unavailable."
                        : "Source materials are usable.", Version);
                if (neutralSlots > 0)
                    SavicManifestMutations.UpsertArtifact(manifest, "published.generic.neutral_material",
                        neutralMaterialPath, "savic.generic-placeable-publisher", Version,
                        SavicHashService.ComputeSha256(layout.FromProjectRelativePath(neutralMaterialPath)));

                RestaurantPlaceableObject placeable =
                    prefab.GetComponent<RestaurantPlaceableObject>();

                if (placeable == null)
                {
                    throw new InvalidOperationException(
                        "Published generic prefab has no RestaurantPlaceableObject.");
                }

                AssignPrefabToItem(
                    item,
                    placeable,
                    manifest,
                    plan);

                if (!prepareCandidateOnly) EnsureCatalogEntry(item);

                string previewFingerprint =
                    SavicPreviewRenderer.BuildInputFingerprint(
                        plan.prefabAssetPath);

                SavicPreviewGenerationResult previews =
                    SavicPreviewRenderer.GenerateAndAssign(
                        prefab,
                        item,
                        contentFolder);

                if (!previews.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Generic preview generation failed: " +
                        previews.Message);
                }

                AssetDatabase.SaveAssets();

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    plan.prefabAssetPath,
                    "savic.generic-placeable-publisher",
                    Version,
                    fingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    plan.itemDefinitionAssetPath,
                    "savic.generic-placeable-publisher",
                    Version,
                    fingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    EditableArtifactRole,
                    plan.editableDefinitionAssetPath,
                    "savic.generic-placeable-publisher",
                    Version,
                    fingerprint);

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

                if (!ValidateAndStampReadiness(
                        manifest,
                        plan,
                        out string readinessError, prepareCandidateOnly))
                {
                    throw new InvalidOperationException(
                        readinessError);
                }

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Presentation.Previews",
                    "PASS",
                    "INFO",
                    previews.Message,
                    SavicPreviewRenderer.Version);

                manifest.status = prepareCandidateOnly ? "NEEDS_REVIEW" : "PUBLISHED";

                manifests.Save(
                    manifest);

                transaction.Commit();

                return new SavicGenericPlaceablePublicationOutcome(
                    true,
                    prepareCandidateOnly ? "Functional candidate prepared atomically without main catalog publication." : "Generic floor placeable published atomically.",
                    plan.prefabAssetPath,
                    plan.itemDefinitionAssetPath);
            }
            catch (Exception exception)
            {
                return Fail(
                    "Generic publication failed: " +
                    exception.Message,
                    plan.prefabAssetPath,
                    plan.itemDefinitionAssetPath);
            }
        }

        internal SavicGenericPlaceablePublicationOutcome
            RefreshAppearanceOnly(
                SavicManifest manifest,
                GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicGenericPlaceableAuthoringRecord plan =
                manifest.genericPlaceable;

            if (plan == null ||
                !plan.planned)
            {
                return Publish(
                    manifest,
                    sourceModelAsset);
            }

            GameObject existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    plan.prefabAssetPath);

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        plan.itemDefinitionAssetPath);

            if (existingPrefab == null ||
                SavicSourceMaterialFallback.CountInvalidSlots(existingPrefab) > 0 ||
                SavicSourceMaterialFallback.CountInvalidSlots(sourceModelAsset) > 0 ||
                item == null ||
                existingPrefab.transform.Find(
                    "Visual/SourceModel") == null ||
                !HasRequiredAreaCapability(
                    existingPrefab,
                    plan))
            {
                return Publish(
                    manifest,
                    sourceModelAsset);
            }

            string contentFolder =
                Path.GetDirectoryName(
                    plan.prefabAssetPath)
                ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(contentFolder))
            {
                return Fail(
                    "Generic appearance refresh could not resolve content folder.");
            }

            string largePreviewPath =
                contentFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                contentFolder +
                "/Preview_Catalog.png";

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "refresh_generic_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(
                plan.prefabAssetPath);

            transaction.CaptureAsset(
                plan.itemDefinitionAssetPath);

            transaction.CaptureAsset(
                largePreviewPath);

            transaction.CaptureAsset(
                catalogPreviewPath);

            GameObject prefabContents =
                null;

            try
            {
                prefabContents =
                    PrefabUtility.LoadPrefabContents(
                        plan.prefabAssetPath);

                Transform visualRoot =
                    prefabContents.transform.Find(
                        "Visual");

                Transform previousSource =
                    prefabContents.transform.Find(
                        "Visual/SourceModel");

                if (visualRoot == null ||
                    previousSource == null)
                {
                    throw new InvalidOperationException(
                        "Published generic prefab has no canonical Visual/SourceModel hierarchy.");
                }

                Object.DestroyImmediate(
                    previousSource.gameObject);

                GameObject sourceInstance =
                    InstantiateSource(
                        sourceModelAsset,
                        prefabContents.scene);

                sourceInstance.name =
                    "SourceModel";

                sourceInstance.transform.SetParent(
                    visualRoot,
                    false);

                NormalizeSourceVisual(
                    sourceInstance.transform,
                    manifest.model3D);

                RemoveSourceColliders(
                    sourceInstance);

                SavicPlaceableFunctionAdapters.Apply(prefabContents, manifest);
                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        prefabContents,
                        plan.prefabAssetPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not confirm generic appearance-only prefab save.");
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
                    plan.prefabAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                if (manifest.assets4All?.delivery != null)
                    SavicAssets4AllService.ApplyOverrides(manifest);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        plan.prefabAssetPath);

                string previewFingerprint =
                    SavicPreviewRenderer.BuildInputFingerprint(
                        plan.prefabAssetPath);

                SavicPreviewGenerationResult previews =
                    SavicPreviewRenderer.GenerateAndAssign(
                        prefab,
                        item,
                        contentFolder);

                if (!previews.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Generic appearance preview refresh failed: " +
                        previews.Message);
                }

                string fingerprint =
                    BuildPublicationFingerprint(
                        manifest,
                        plan);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    plan.prefabAssetPath,
                    "savic.generic-placeable-publisher",
                    Version,
                    fingerprint);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    ItemArtifactRole,
                    plan.itemDefinitionAssetPath,
                    "savic.generic-placeable-publisher",
                    Version,
                    fingerprint);

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

                if (!ValidateAndStampReadiness(
                        manifest,
                        plan,
                        out string readinessError))
                {
                    throw new InvalidOperationException(
                        readinessError);
                }

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Pipeline.IncrementalAppearanceRefresh",
                    "PASS",
                    "INFO",
                    "Generic visual and previews refreshed without rebuilding collider, footprint or runtime identity.",
                    SavicIncrementalInvalidationService.Version);

                manifest.status =
                    "PUBLISHED";

                manifests.Save(
                    manifest);

                transaction.Commit();

                return new SavicGenericPlaceablePublicationOutcome(
                    true,
                    "Generic appearance refreshed incrementally.",
                    plan.prefabAssetPath,
                    plan.itemDefinitionAssetPath);
            }
            catch (Exception exception)
            {
                return Fail(
                    "Generic appearance refresh failed: " +
                    exception.Message,
                    plan.prefabAssetPath,
                    plan.itemDefinitionAssetPath);
            }
        }

        private RestaurantEditableObjectDefinition LoadOrCreateEditable(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            RestaurantEditableObjectDefinition editable =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantEditableObjectDefinition>(
                        plan.editableDefinitionAssetPath);

            if (editable == null)
            {
                Object existing =
                    AssetDatabase.LoadMainAssetAtPath(
                        plan.editableDefinitionAssetPath);

                if (existing != null)
                {
                    throw new InvalidOperationException(
                        "Editable definition path is occupied by another asset type.");
                }

                editable =
                    ScriptableObject.CreateInstance
                        <RestaurantEditableObjectDefinition>();

                AssetDatabase.CreateAsset(
                    editable,
                    plan.editableDefinitionAssetPath);
            }

            SerializedObject serialized =
                new SerializedObject(
                    editable);

            SetString(
                serialized,
                "definitionId",
                ExtractContentId(
                    plan.itemDefinitionAssetPath));

            SetString(
                serialized,
                "displayName",
                HumanizeSourceName(
                    SavicProviderMetadataService.ResolveSemanticName(manifest, layout)));

            SetString(
                serialized,
                "description",
                "Contenido gestionado automáticamente por SAVIC.");

            SetBool(
                serialized,
                "canMove",
                true);

            SetBool(
                serialized,
                "canRotate",
                true);

            SetBool(
                serialized,
                "useCustomGridSize",
                true);

            SetFloat(
                serialized,
                "customGridSize",
                0.05f);

            SetBool(
                serialized,
                "useCustomRotationStep",
                true);

            SetFloat(
                serialized,
                "customRotationStepDegrees",
                Mathf.Clamp(
                    plan.rotationStepDegrees,
                    1f,
                    180f));

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                editable);

            return editable;
        }

        private RestaurantPlaceableItemDefinition LoadOrCreateItem(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan,
            RestaurantEditableObjectDefinition editable)
        {
            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        plan.itemDefinitionAssetPath);

            if (item == null)
            {
                Object existing =
                    AssetDatabase.LoadMainAssetAtPath(
                        plan.itemDefinitionAssetPath);

                if (existing != null)
                {
                    throw new InvalidOperationException(
                        "Item definition path is occupied by another asset type.");
                }

                item =
                    ScriptableObject.CreateInstance
                        <RestaurantPlaceableItemDefinition>();

                AssetDatabase.CreateAsset(
                    item,
                    plan.itemDefinitionAssetPath);
            }

            RestaurantPlaceableItemCategory category =
                ParseCategory(
                    plan.category);

            SerializedObject serialized =
                new SerializedObject(
                    item);

            SetString(
                serialized,
                "itemId",
                manifest.canonicalContentId);

            SetString(
                serialized,
                "displayName",
                HumanizeSourceName(
                    SavicProviderMetadataService.ResolveSemanticName(manifest, layout)));

            SetEnumIndex(
                serialized,
                "category",
                (int)category);

            SetEnumIndex(
                serialized,
                "placementScope",
                (int)RestaurantPlaceableEnvironmentScope
                    .InteriorAndExterior);

            string existingDescription = serialized.FindProperty("description").stringValue;
            string desiredDescription = BuildDescription(category, plan.integrationMode == SavicBarCounterFunctionAdapter.Mode);
            if (CanReplaceGeneratedDescription(existingDescription, desiredDescription))
                SetString(serialized, "description", desiredDescription);

            SetObjectReference(
                serialized,
                "editableDefinition",
                editable);

            SetInteger(
                serialized,
                "purchasePrice",
                Math.Max(
                    0,
                    plan.suggestedPurchasePriceEuro));

            SetVector3(
                serialized,
                "dimensionsCentimeters",
                new Vector3(
                    plan.finalWidthMeters * 100f,
                    plan.finalHeightMeters * 100f,
                    plan.finalDepthMeters * 100f));

            SerializedProperty rules =
                RequireProperty(
                    serialized,
                    "inspectorRules");

            rules.intValue =
                (int)(
                    RestaurantPlaceableInspectorRuleFlags
                        .FloorSurface |
                    RestaurantPlaceableInspectorRuleFlags
                        .RequiresClearance);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);

            return item;
        }

        private void BuildOrReplacePrefab(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan,
            RestaurantPlaceableItemDefinition item,
            RestaurantEditableObjectDefinition editable,
            GameObject sourceModelAsset)
        {
            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            GameObject root =
                null;

            try
            {
                root =
                    new GameObject(
                        manifest.canonicalContentId);

                SceneManager.MoveGameObjectToScene(
                    root,
                    previewScene);

                GameObject visualRoot =
                    new GameObject(
                        "Visual");

                SceneManager.MoveGameObjectToScene(
                    visualRoot,
                    previewScene);

                visualRoot.transform.SetParent(
                    root.transform,
                    false);

                GameObject sourceInstance =
                    InstantiateSource(
                        sourceModelAsset,
                        previewScene);

                sourceInstance.name =
                    "SourceModel";

                sourceInstance.transform.SetParent(
                    visualRoot.transform,
                    false);

                NormalizeSourceVisual(
                    sourceInstance.transform,
                    manifest.model3D);

                RemoveSourceColliders(
                    sourceInstance);

                BoxCollider collider =
                    root.AddComponent<BoxCollider>();

                if (SavicSourceMaterialFallback.CountInvalidSlots(sourceInstance) > 0)
                {
                    string neutralPath = Path.GetDirectoryName(plan.prefabAssetPath).Replace('\\', '/') + "/Material_NeutralFallback.mat";
                    SavicSourceMaterialFallback.ReplaceInvalidSlots(sourceInstance,
                        SavicSourceMaterialFallback.GetOrCreateNeutral(neutralPath, "SAVIC Neutral Placeable Appearance"));
                }

                collider.center =
                    new Vector3(
                        0f,
                        plan.finalHeightMeters * 0.5f,
                        0f);

                collider.size =
                    new Vector3(
                        Mathf.Max(
                            0.01f,
                            plan.finalWidthMeters),
                        Mathf.Max(
                            0.01f,
                            plan.finalHeightMeters),
                        Mathf.Max(
                            0.01f,
                            plan.finalDepthMeters));

                RestaurantAreaMember areaMember =
                    root.AddComponent
                        <RestaurantAreaMember>();

                RestaurantPlacementFootprint footprint =
                    root.AddComponent
                        <RestaurantPlacementFootprint>();

                RestaurantEditableObject editableObject =
                    root.AddComponent
                        <RestaurantEditableObject>();

                RestaurantPlaceableObject placeable =
                    root.AddComponent
                        <RestaurantPlaceableObject>();

                GameObject anchorObject =
                    new GameObject(
                        "PlacementAnchor");

                SceneManager.MoveGameObjectToScene(
                    anchorObject,
                    previewScene);

                anchorObject.transform.SetParent(
                    root.transform,
                    false);

                anchorObject.transform.localPosition =
                    Vector3.zero;

                ConfigureAreaMember(
                    areaMember,
                    plan);

                ConfigureFootprint(
                    footprint,
                    plan);

                ConfigureEditableObject(
                    editableObject,
                    editable);

                ConfigurePlaceable(
                    placeable,
                    item,
                    anchorObject.transform);

                SavicPlaceableFunctionAdapters.Apply(root, manifest);

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        plan.prefabAssetPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not confirm generic prefab save.");
                }
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(
                        root);
                }

                EditorSceneManager.ClosePreviewScene(
                    previewScene);
            }

            AssetDatabase.ImportAsset(
                plan.prefabAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static GameObject InstantiateSource(
            GameObject sourceModelAsset,
            Scene scene)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    sourceModelAsset,
                    scene) as GameObject;

            if (instance != null)
                return instance;

            instance =
                Object.Instantiate(
                    sourceModelAsset);

            SceneManager.MoveGameObjectToScene(
                instance,
                scene);

            return instance;
        }

        internal static void NormalizeSourceVisual(
            Transform source,
            SavicModelAnalysisRecord analysis)
        {
            if (source == null ||
                analysis == null)
            {
                return;
            }

            // Match SavicMetricSpace: preserve the importer's rotation and unit scale.

            float minY =
                analysis.boundsCenterY -
                analysis.heightMeters * 0.5f;

            source.localPosition =
                new Vector3(
                    -analysis.boundsCenterX,
                    -minY,
                    -analysis.boundsCenterZ);
        }

        private static void RemoveSourceColliders(
            GameObject source)
        {
            Collider[] colliders =
                source.GetComponentsInChildren
                    <Collider>(true);

            for (int index =
                     colliders.Length - 1;
                 index >= 0;
                 index--)
            {
                if (colliders[index] != null)
                {
                    Object.DestroyImmediate(
                        colliders[index]);
                }
            }
        }

        private static void ConfigureAreaMember(
            RestaurantAreaMember areaMember,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            if (areaMember == null)
                throw new ArgumentNullException(nameof(areaMember));

            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            SerializedObject serialized =
                new SerializedObject(
                    areaMember);

            SetObjectReference(
                serialized,
                "assignedArea",
                null);

            SetObjectReference(
                serialized,
                "positionReference",
                areaMember.transform);

            SerializedProperty requirements =
                RequireProperty(
                    serialized,
                    "requiredCapabilities");

            if (string.IsNullOrWhiteSpace(
                    plan.requiredAreaCapabilityId))
            {
                requirements.arraySize =
                    0;
            }
            else
            {
                RestaurantAreaCapabilityDefinition capability =
                    ResolveAreaCapability(
                        plan.requiredAreaCapabilityId);

                requirements.arraySize =
                    1;

                requirements
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue =
                        capability;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool HasRequiredAreaCapability(
            GameObject prefab,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            if (prefab == null ||
                plan == null)
            {
                return false;
            }

            RestaurantAreaMember member =
                prefab.GetComponent<RestaurantAreaMember>();

            if (member == null)
                return false;

            if (string.IsNullOrWhiteSpace(
                    plan.requiredAreaCapabilityId))
            {
                return member.RequiredCapabilityCount ==
                       0;
            }

            string requiredId =
                plan.requiredAreaCapabilityId.Trim();

            int matches =
                0;

            IReadOnlyList<RestaurantAreaCapabilityDefinition>
                requirements =
                    member.RequiredCapabilities;

            if (requirements != null)
            {
                for (int index = 0;
                     index < requirements.Count;
                     index++)
                {
                    RestaurantAreaCapabilityDefinition capability =
                        requirements[index];

                    if (capability != null &&
                        string.Equals(
                            capability.CapabilityId,
                            requiredId,
                            StringComparison.Ordinal))
                    {
                        matches++;
                    }
                }
            }

            return matches == 1 &&
                   member.RequiredCapabilityCount == 1;
        }

        private static RestaurantAreaCapabilityDefinition
            ResolveAreaCapability(
                string capabilityId)
        {
            if (string.IsNullOrWhiteSpace(
                    capabilityId))
            {
                throw new ArgumentException(
                    "Area capability id is required.",
                    nameof(capabilityId));
            }

            string normalized =
                capabilityId.Trim();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:RestaurantAreaCapabilityDefinition");

            RestaurantAreaCapabilityDefinition match =
                null;

            int matchCount =
                0;

            for (int index = 0;
                 index < guids.Length;
                 index++)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(
                        guids[index]);

                RestaurantAreaCapabilityDefinition candidate =
                    AssetDatabase.LoadAssetAtPath
                        <RestaurantAreaCapabilityDefinition>(
                            assetPath);

                if (candidate == null ||
                    !string.Equals(
                        candidate.CapabilityId,
                        normalized,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                match =
                    candidate;

                matchCount++;
            }

            if (matchCount != 1 ||
                match == null)
            {
                throw new InvalidOperationException(
                    "Expected exactly one RestaurantAreaCapabilityDefinition for '" +
                    normalized +
                    "', found " +
                    matchCount +
                    ".");
            }

            return match;
        }

        private static void ConfigureFootprint(
            RestaurantPlacementFootprint footprint,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            SerializedObject serialized =
                new SerializedObject(
                    footprint);

            SetVector3(
                serialized,
                "localCenter",
                new Vector3(
                    0f,
                    plan.finalHeightMeters * 0.5f,
                    0f));

            SetVector2(
                serialized,
                "size",
                new Vector2(
                    Mathf.Max(
                        0.1f,
                        plan.finalWidthMeters),
                    Mathf.Max(
                        0.1f,
                        plan.finalDepthMeters)));

            SetFloat(
                serialized,
                "boundaryInset",
                0.02f);

            SetFloat(
                serialized,
                "minimumClearance",
                Mathf.Max(
                    0f,
                    plan.minimumClearanceMeters));

            SetBool(
                serialized,
                "blocksOtherPlacements",
                true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEditableObject(
            RestaurantEditableObject editableObject,
            RestaurantEditableObjectDefinition editable)
        {
            SerializedObject serialized =
                new SerializedObject(
                    editableObject);

            SetObjectReference(
                serialized,
                "definition",
                editable);

            SetBool(
                serialized,
                "editingEnabled",
                true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlaceable(
            RestaurantPlaceableObject placeable,
            RestaurantPlaceableItemDefinition item,
            Transform anchor)
        {
            SerializedObject serialized =
                new SerializedObject(
                    placeable);

            SetObjectReference(
                serialized,
                "itemDefinition",
                item);

            SetString(
                serialized,
                "instanceId",
                string.Empty);

            SetObjectReference(
                serialized,
                "placementAnchor",
                anchor);

            SetBool(
                serialized,
                "synchronizeEditableDefinition",
                true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignPrefabToItem(
            RestaurantPlaceableItemDefinition item,
            RestaurantPlaceableObject prefab,
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            SerializedObject serialized =
                new SerializedObject(
                    item);

            SetObjectReference(
                serialized,
                "prefab",
                prefab);

            SetString(
                serialized,
                "itemId",
                manifest.canonicalContentId);

            SetVector3(
                serialized,
                "dimensionsCentimeters",
                new Vector3(
                    plan.finalWidthMeters * 100f,
                    plan.finalHeightMeters * 100f,
                    plan.finalDepthMeters * 100f));

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                item);
        }

        private static void EnsureCatalogEntry(
            RestaurantPlaceableItemDefinition item)
        {
            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Canonical placeable catalog is missing.");
            }

            SerializedObject serialized =
                new SerializedObject(
                    catalog);

            SerializedProperty items =
                RequireProperty(
                    serialized,
                    "items");

            List<RestaurantPlaceableItemDefinition> merged =
                new List<RestaurantPlaceableItemDefinition>();

            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < items.arraySize;
                 index++)
            {
                RestaurantPlaceableItemDefinition existing =
                    items
                        .GetArrayElementAtIndex(
                            index)
                        .objectReferenceValue as
                        RestaurantPlaceableItemDefinition;

                if (existing == null ||
                    string.IsNullOrWhiteSpace(
                        existing.ItemId))
                {
                    continue;
                }

                if (string.Equals(
                        existing.ItemId,
                        item.ItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (ids.Add(
                        existing.ItemId))
                {
                    merged.Add(
                        existing);
                }
            }

            ids.Add(
                item.ItemId);

            merged.Add(
                item);

            merged.Sort(
                (left, right) =>
                    string.Compare(
                        left?.ItemId,
                        right?.ItemId,
                        StringComparison.Ordinal));

            items.arraySize =
                merged.Count;

            for (int index = 0;
                 index < merged.Count;
                 index++)
            {
                items
                    .GetArrayElementAtIndex(
                        index)
                    .objectReferenceValue =
                        merged[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                catalog);
        }

        private bool ValidateAndStampReadiness(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan,
            out string error, bool candidateOnly = false)
        {
            error =
                string.Empty;

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    plan.prefabAssetPath);

            RestaurantPlaceableItemDefinition item =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        plan.itemDefinitionAssetPath);

            RestaurantEditableObjectDefinition editable =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantEditableObjectDefinition>(
                        plan.editableDefinitionAssetPath);

            if (prefab == null ||
                item == null ||
                editable == null)
            {
                error =
                    "Generic published asset set is incomplete.";
                return false;
            }

            if (SavicSourceMaterialFallback.CountInvalidSlots(prefab) > 0)
            {
                error = "Generic published prefab has invalid material slots.";
                return false;
            }

            RestaurantPlaceableObject placeable =
                prefab.GetComponent
                    <RestaurantPlaceableObject>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent
                    <RestaurantPlacementFootprint>();

            BoxCollider collider =
                prefab.GetComponent
                    <BoxCollider>();

            RestaurantAreaMember areaMember =
                prefab.GetComponent
                    <RestaurantAreaMember>();

            if (placeable == null ||
                footprint == null ||
                (collider == null && !SavicBarCounterRuntimeAcceptance.Required(manifest)) ||
                areaMember == null)
            {
                error =
                    "Generic prefab is missing placeable, area-member, footprint or collider components.";
                return false;
            }

            if (!placeable.ValidateConfiguration(
                    out string placeableError))
            {
                error =
                    "Generic prefab validation failed: " +
                    placeableError;
                return false;
            }

            bool footprintReady =
                footprint.Size.x >=
                    Math.Max(
                        0.1f,
                        plan.finalWidthMeters) -
                    0.001f &&
                footprint.Size.y >=
                    Math.Max(
                        0.1f,
                        plan.finalDepthMeters) -
                    0.001f &&
                footprint.BlocksOtherPlacements;

            bool colliderReady = SavicBarCounterRuntimeAcceptance.Required(manifest)
                ? collider == null && prefab.GetComponentsInChildren<BoxCollider>(true).Length == manifest.barCounter.physicalBoxes.Count
                : collider != null && collider.size.x > 0.01f && collider.size.y > 0.01f && collider.size.z > 0.01f;

            int catalogCount =
                CountCatalogEntries(
                    manifest.canonicalContentId);

            bool catalogReady =
                catalogCount == 1 &&
                item.HasValidPrefab &&
                item.EditableDefinition != null;

            bool persistenceReady =
                catalogReady &&
                string.Equals(
                    item.ItemId,
                    manifest.canonicalContentId,
                    StringComparison.Ordinal);

            bool runtimeRequired = SavicFunctionalRuntimeAcceptance.Required(manifest);
            bool runtimeAccepted = !runtimeRequired || SavicFunctionalRuntimeAcceptance.Matches(manifest, layout);
            bool navigationReady = footprintReady && runtimeAccepted;

            bool areaCapabilityReady =
                HasRequiredAreaCapability(
                    prefab,
                    plan);

            bool functionReady = SavicPlaceableFunctionAdapters.Validate(prefab, manifest, out string functionError);
            SavicManifestMutations.UpsertValidation(manifest, "GenericPlaceable.FunctionAdapter",
                functionReady ? "PASS" : "FAIL", functionReady ? "INFO" : "ERROR",
                functionReady ? "Required native function adapter matches the canonical authoring plan."
                    : functionError, Version);

            bool ready =
                footprintReady &&
                colliderReady &&
                catalogReady &&
                persistenceReady &&
                navigationReady &&
                areaCapabilityReady && functionReady && runtimeAccepted;

            manifest.genericPlaceableReadiness =
                new SavicGenericPlaceableReadinessRecord
                {
                    validated =
                        ready,
                    validatorVersion =
                        Version,
                    floorPlacementReady =
                        footprintReady,
                    colliderReady =
                        colliderReady,
                    footprintReady =
                        footprintReady,
                    catalogResolvable =
                        catalogReady,
                    persistenceReady =
                        persistenceReady,
                    navigationReady =
                        navigationReady,
                    spatialContractRequired =
                        runtimeRequired,
                    areaCapabilityReady =
                        areaCapabilityReady,
                    functionalAdapterReady = functionReady,
                    requiredAreaCapabilityId =
                        plan.requiredAreaCapabilityId ??
                        string.Empty,
                    integrationMode =
                        plan.integrationMode ??
                        "NONE",
                    prefabAssetPath =
                        plan.prefabAssetPath,
                    itemDefinitionAssetPath =
                        plan.itemDefinitionAssetPath,
                    evidence =
                        runtimeRequired ? (ready
                            ? "Native physical/function contract, canonical catalog identity and verified creation/navigation/lease/SaveGame acceptance match the prefab."
                            : "Functional candidate requires current runtime acceptance and catalog publication.") : ready
                            ? string.IsNullOrWhiteSpace(
                                  plan.requiredAreaCapabilityId)
                                ? "Static generic placeable has canonical floor anchor, footprint, collider, catalog identity and persistence-ready prefab. No interactive BBSIS contract is required."
                                : "Passive equipment has canonical floor anchor, footprint, collider, catalog identity, persistence-ready prefab and required area capability '" +
                                  plan.requiredAreaCapabilityId +
                                  "'. No interactive appliance gameplay or BBSIS work contract was invented."
                            : "Generic readiness validation failed.",
                    validatedUtc =
                        DateTime.UtcNow.ToString("O")
                };

            UpsertReadinessValidations(
                manifest,
                plan,
                footprintReady,
                colliderReady,
                catalogReady,
                persistenceReady,
                navigationReady,
                areaCapabilityReady, candidateOnly);

            if (candidateOnly)
            {
                // Prepared geometry is not catalog/persistence/navigation acceptance.
                SavicManifestMutations.UpsertValidation(manifest, "GenericPlaceable.RuntimeAcceptance", "REVIEW", "WARNING",
                    "Candidate prepared without main catalog entry; real Play Mode acceptance remains required.", Version);
                return footprintReady && colliderReady && areaCapabilityReady && functionReady;
            }
            if (runtimeRequired)
                SavicManifestMutations.UpsertValidation(manifest, "GenericPlaceable.RuntimeAcceptance",
                    runtimeAccepted ? "PASS" : "REVIEW", runtimeAccepted ? "INFO" : "WARNING",
                    runtimeAccepted ? "Current prefab passed canonical runtime creation, native binding/lease/route and SaveGame save/load/cleanup."
                        : "Runtime acceptance is absent or stale.", Version);
            if (!ready)
            {
                error =
                    "Generic placeable readiness gate failed.";
            }

            return ready;
        }

        private static void UpsertReadinessValidations(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan,
            bool footprintReady,
            bool colliderReady,
            bool catalogReady,
            bool persistenceReady,
            bool navigationReady,
            bool areaCapabilityReady, bool candidateOnly = false)
        {
            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.FloorPlacement",
                footprintReady ? "PASS" : "FAIL",
                footprintReady ? "INFO" : "ERROR",
                footprintReady
                    ? "Floor anchor and canonical footprint are valid."
                    : "Floor placement footprint is invalid.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.Collider",
                colliderReady ? "PASS" : "FAIL",
                colliderReady ? "INFO" : "ERROR",
                colliderReady
                    ? "Canonical static collider geometry is valid."
                    : "Generic collider is invalid.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.Catalog",
                catalogReady ? "PASS" : candidateOnly ? "REVIEW" : "FAIL",
                catalogReady ? "INFO" : candidateOnly ? "WARNING" : "ERROR",
                catalogReady
                    ? "Canonical catalog resolves exactly one item definition."
                    : candidateOnly ? "Candidate is not yet published to the main catalog." : "Catalog registration is missing or duplicated.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.Persistence",
                persistenceReady ? "PASS" : candidateOnly ? "REVIEW" : "FAIL",
                persistenceReady ? "INFO" : candidateOnly ? "WARNING" : "ERROR",
                persistenceReady
                    ? "Canonical ItemId and prefab are persistence-resolvable."
                    : "Persistence identity is not ready.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.Navigation",
                navigationReady ? "PASS" : candidateOnly ? "REVIEW" : "FAIL",
                navigationReady ? "INFO" : candidateOnly ? "WARNING" : "ERROR",
                navigationReady
                    ? "Canonical blocking footprint supplies static navigation topology."
                    : "Navigation footprint is not ready.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.AreaCapability",
                areaCapabilityReady ? "PASS" : "FAIL",
                areaCapabilityReady ? "INFO" : "ERROR",
                areaCapabilityReady
                    ? string.IsNullOrWhiteSpace(
                          plan.requiredAreaCapabilityId)
                        ? "No area capability is required for this generic placeable."
                        : "Placement requires the existing canonical area capability '" +
                          plan.requiredAreaCapabilityId +
                          "'."
                    : "Required area capability is missing or duplicated on the published prefab.",
                Version);

            SavicManifestMutations.UpsertValidation(
                manifest,
                "GenericPlaceable.BBSIS",
                "PASS",
                "INFO",
                plan.integrationMode == SavicBarCounterFunctionAdapter.Mode
                    ? "Compound root body and native bar ports are validated against the existing work.bar contract."
                    : string.Equals(
                    plan.integrationMode,
                    SavicEquipmentIntegrationPolicy.PassiveAreaPlaceableMode,
                    StringComparison.Ordinal)
                    ? "Passive equipment uses the existing area-capability placement contract; no interactive appliance BBSIS contract is created."
                    : "No interactive BBSIS contract is required for this passive generic placeable.",
                Version);
        }

        private static bool CanReuseExisting(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan,
            string fingerprint)
        {
            SavicArtifactRecord artifact =
                manifest.artifacts?
                    .FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            string.Equals(
                                candidate.role,
                                PrefabArtifactRole,
                                StringComparison.Ordinal));

            if (artifact == null ||
                !string.Equals(
                    artifact.inputFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plan.prefabAssetPath);
            return prefab != null && SavicSourceMaterialFallback.CountInvalidSlots(prefab) == 0 &&
                   AssetDatabase.LoadAssetAtPath
                       <RestaurantPlaceableItemDefinition>(
                           plan.itemDefinitionAssetPath) != null &&
                   AssetDatabase.LoadAssetAtPath
                       <RestaurantEditableObjectDefinition>(
                           plan.editableDefinitionAssetPath) != null;
        }

        private static string BuildPublicationFingerprint(
            SavicManifest manifest,
            SavicGenericPlaceableAuthoringRecord plan)
        {
            return SavicHashService.ComputeSha256Text(
                string.Join(
                    "|",
                    "savic.generic-publication.v1",
                    Version,
                    manifest.canonicalContentId ??
                        string.Empty,
                    manifest.source?.sourceHash ??
                        string.Empty,
                    manifest.source?.providerMetadataHash ?? string.Empty,
                    manifest.incremental?.geometryFingerprint ??
                        string.Empty,
                    manifest.incremental?.appearanceFingerprint ??
                        string.Empty,
                    plan.plannerVersion ??
                        string.Empty,
                    plan.placementMode ??
                        string.Empty,
                    plan.category ??
                        string.Empty,
                    plan.integrationMode ??
                        string.Empty,
                    SavicPlaceableFunctionAdapters.Resolve(manifest)?.Fingerprint(manifest) ?? string.Empty,
                    plan.requiredAreaCapabilityId ??
                        string.Empty,
                    F(plan.finalWidthMeters),
                    F(plan.finalHeightMeters),
                    F(plan.finalDepthMeters),
                    F(plan.rotationStepDegrees),
                    F(plan.minimumClearanceMeters),
                    plan.suggestedPurchasePriceEuro.ToString(
                        CultureInfo.InvariantCulture)));
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

            return catalog.Items.Count(
                item =>
                    item != null &&
                    string.Equals(
                        item.ItemId,
                        itemId,
                        StringComparison.Ordinal));
        }

        private static RestaurantPlaceableItemCategory ParseCategory(
            string value)
        {
            if (Enum.TryParse(
                    value,
                    true,
                    out RestaurantPlaceableItemCategory category))
            {
                return category;
            }

            return RestaurantPlaceableItemCategory.Other;
        }

        internal bool RefreshPublishedGeneratedDescription(SavicManifest manifest)
        {
            var plan = manifest?.genericPlaceable;
            if (manifest?.status != "PUBLISHED" || plan == null || !plan.planned) return false;
            var item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(plan.itemDefinitionAssetPath);
            if (item == null || item.ItemId != manifest.canonicalContentId || item.Category != ParseCategory(plan.category))
                throw new InvalidOperationException("Published description repair requires the canonical item and category.");
            string desired = BuildDescription(item.Category, plan.integrationMode == SavicBarCounterFunctionAdapter.Mode);
            if (item.Description == desired || !CanReplaceGeneratedDescription(item.Description, desired)) return false;
            using var transaction = new SavicAssetMutationScope(layout, "description_" + manifest.canonicalContentId);
            transaction.CaptureAsset(plan.itemDefinitionAssetPath);
            RefreshGeneratedDescription(item, plan.integrationMode == SavicBarCounterFunctionAdapter.Mode);
            AssetDatabase.SaveAssets();
            transaction.Commit();
            return true;
        }

        internal static bool CanReplaceGeneratedDescription(string existing, string desired) =>
            string.IsNullOrWhiteSpace(existing) || existing == desired ||
            existing == "Elemento decorativo preparado automáticamente por SAVIC." ||
            existing == "Equipamiento pasivo de cocina preparado automáticamente por SAVIC." ||
            existing == "Equipamiento pasivo de servicio preparado automáticamente por SAVIC.";

        private static void RefreshGeneratedDescription(RestaurantPlaceableItemDefinition item, bool bar)
        {
            string desired = BuildDescription(item.Category, bar);
            if (item.Description == desired || !CanReplaceGeneratedDescription(item.Description, desired)) return;
            var serialized = new SerializedObject(item);
            SetString(serialized, "description", desired);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
        }

        private static string BuildDescription(
            RestaurantPlaceableItemCategory category, bool bar = false)
        {
            if (bar) return "Mostrador de barra con plazas de servicio y contrato espacial canónicos, preparado por SAVIC.";
            switch (category)
            {
                case RestaurantPlaceableItemCategory.Seating:
                    return "Asiento de barra preparado por SAVIC para una plaza compatible y ocupación canónica.";
                case RestaurantPlaceableItemCategory.KitchenEquipment:
                    return "Equipamiento pasivo de cocina preparado automáticamente por SAVIC.";

                case RestaurantPlaceableItemCategory.ServiceEquipment:
                    return "Equipamiento pasivo de servicio preparado automáticamente por SAVIC.";

                case RestaurantPlaceableItemCategory.Lighting:
                    return "Iluminación funcional preparada automáticamente por SAVIC.";
                case RestaurantPlaceableItemCategory.Furniture:
                    return "Mobiliario preparado automáticamente por SAVIC.";

                default:
                    return "Elemento decorativo preparado automáticamente por SAVIC.";
            }
        }

        private static string ExtractContentId(
            string itemPath)
        {
            string file =
                Path.GetFileNameWithoutExtension(
                    itemPath) ??
                string.Empty;

            const string prefix =
                "PlaceableItem_";

            return file.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                ? file.Substring(
                    prefix.Length)
                : file;
        }

        private static string HumanizeSourceName(
            string raw)
        {
            string value =
                Path.GetFileNameWithoutExtension(
                    raw ?? string.Empty) ??
                string.Empty;

            value =
                value
                    .Replace(
                        "Meshy_AI_",
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase)
                    .Replace(
                        "_generate",
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase)
                    .Replace(
                        "_texture",
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase)
                    .Replace(
                        '_',
                        ' ')
                    .Trim();

            return string.IsNullOrWhiteSpace(
                    value)
                ? "Artículo SAVIC"
                : value;
        }

        private static void EnsureAssetFolder(
            string assetFolder)
        {
            string normalized =
                assetFolder
                    .Replace('\\', '/')
                    .TrimEnd('/');

            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generic content folder must live under Assets.");
            }

            string[] parts =
                normalized.Split('/');

            string current =
                "Assets";

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    current +
                    "/" +
                    parts[index];

                if (!AssetDatabase.IsValidFolder(
                        next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current =
                    next;
            }
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string name)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    name);

            if (property == null)
            {
                throw new InvalidOperationException(
                    "Serialized property not found: " +
                    name);
            }

            return property;
        }

        private static void SetString(
            SerializedObject serialized,
            string name,
            string value)
        {
            RequireProperty(
                serialized,
                name).stringValue =
                    value ?? string.Empty;
        }

        private static void SetBool(
            SerializedObject serialized,
            string name,
            bool value)
        {
            RequireProperty(
                serialized,
                name).boolValue =
                    value;
        }

        private static void SetFloat(
            SerializedObject serialized,
            string name,
            float value)
        {
            RequireProperty(
                serialized,
                name).floatValue =
                    value;
        }

        private static void SetInteger(
            SerializedObject serialized,
            string name,
            int value)
        {
            RequireProperty(
                serialized,
                name).intValue =
                    value;
        }

        private static void SetEnumIndex(
            SerializedObject serialized,
            string name,
            int value)
        {
            RequireProperty(
                serialized,
                name).enumValueIndex =
                    value;
        }

        private static void SetVector2(
            SerializedObject serialized,
            string name,
            Vector2 value)
        {
            RequireProperty(
                serialized,
                name).vector2Value =
                    value;
        }

        private static void SetVector3(
            SerializedObject serialized,
            string name,
            Vector3 value)
        {
            RequireProperty(
                serialized,
                name).vector3Value =
                    value;
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            string name,
            Object value)
        {
            RequireProperty(
                serialized,
                name).objectReferenceValue =
                    value;
        }

        private static string F(
            float value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private static SavicGenericPlaceablePublicationOutcome Fail(
            string message,
            string prefabPath = "",
            string itemPath = "")
        {
            return new SavicGenericPlaceablePublicationOutcome(
                false,
                message,
                prefabPath,
                itemPath);
        }
    }
}
