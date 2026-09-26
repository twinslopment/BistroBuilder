using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicIncrementalRealAssetProbe
    {
        private const string CanonicalSourceModelPath =
            "Assets/Art/Blender/Placeables/Furniture/Chairs/" +
            "BB_Chair_Master_002/Models/BB_Chair_Master_002.fbx";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        private const string DiagnosticRoot =
            "Assets/Generated/BistroBuilder/SAVIC/Diagnostics/" +
            "IncrementalRealAssetProbe";

        private const string DiagnosticSourcePath =
            DiagnosticRoot + "/Source_Chair.prefab";

        private const string DiagnosticMaterialPath =
            DiagnosticRoot + "/ProbeMaterial.mat";

        private const string DiagnosticSavicId =
            "diagnosticincrementalrealchair";

        private const string DiagnosticContentId =
            "bb_chair_diagnosticincrementalrealchair";

        private const string DiagnosticPublishedFolder =
            "Assets/Generated/BistroBuilder/SAVIC/Published/Chairs/" +
            DiagnosticContentId;

        private const float GeometryScaleX = 1.12f;

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/" +
            "Run Incremental Real Asset Probe",
            false,
            130)]
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

            using SavicAssetMutationScope rollback =
                new SavicAssetMutationScope(
                    context.Layout,
                    "incremental_real_asset_probe");

            string itemPath =
                DiagnosticPublishedFolder +
                "/PlaceableItem_" +
                DiagnosticContentId +
                ".asset";

            string prefabPath =
                DiagnosticPublishedFolder +
                "/Chair_" +
                DiagnosticContentId +
                ".prefab";

            string largePreviewPath =
                DiagnosticPublishedFolder +
                "/Preview_Large.png";

            string catalogPreviewPath =
                DiagnosticPublishedFolder +
                "/Preview_Catalog.png";

            rollback.CaptureAsset(MainCatalogPath);
            rollback.CaptureAsset(itemPath);
            rollback.CaptureAsset(prefabPath);
            rollback.CaptureAsset(largePreviewPath);
            rollback.CaptureAsset(catalogPreviewPath);

            try
            {
                EnsureAssetFolder(
                    DiagnosticRoot);

                GameObject canonical =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        CanonicalSourceModelPath);

                Require(
                    canonical != null,
                    "Canonical real chair source could not be loaded.");

                WriteSourceVariant(
                    canonical,
                    false,
                    1f);

                GameObject baselineSource =
                    LoadDiagnosticSource();

                SavicManifest baseline =
                    BuildFullManifest(
                        baselineSource,
                        null);

                SavicIncrementalPlan baselinePlan =
                    SavicIncrementalInvalidationService.Evaluate(
                        null,
                        baseline,
                        baseline.model3D);

                Require(
                    baselinePlan.Action ==
                    SavicIncrementalAction.FullRebuild,
                    "Initial real asset publication did not request a full build.");

                baseline.incremental =
                    SavicIncrementalInvalidationService.Stamp(
                        null,
                        baselinePlan);

                UpsertSourceMirror(
                    baseline);

                SavicChairPublisher publisher =
                    new SavicChairPublisher(
                        context.Layout,
                        context.Manifests);

                SavicChairPublicationOutcome first =
                    publisher.Publish(
                        baseline,
                        baselineSource);

                Require(
                    first.Succeeded,
                    "Initial real chair publication failed: " +
                    first.Message);

                SavicManifest publishedBaseline =
                    CloneManifest(
                        baseline);

                string prefabGuid =
                    RequireGuid(
                        first.PrefabAssetPath,
                        "Published prefab");

                string itemGuid =
                    RequireGuid(
                        first.ItemDefinitionAssetPath,
                        "Published item");

                string baselineColliderSignature =
                    BuildColliderSignature(
                        first.PrefabAssetPath);

                string baselineVisualSignature =
                    BuildVisualMaterialSignature(
                        first.PrefabAssetPath);

                string baselineSemanticSignature =
                    JsonUtility.ToJson(
                        publishedBaseline.model3D.semanticParts,
                        false);

                string baselineColliderRecord =
                    JsonUtility.ToJson(
                        publishedBaseline.chairColliders,
                        false);

                string baselineSpatialRecord =
                    JsonUtility.ToJson(
                        publishedBaseline.chairSpatial,
                        false);

                string baselineNavigationRecord =
                    JsonUtility.ToJson(
                        publishedBaseline.chairNavigation,
                        false);

                string baselinePersistenceRecord =
                    JsonUtility.ToJson(
                        publishedBaseline.chairPersistence,
                        false);

                string baselinePublicationFingerprint =
                    RequireArtifactFingerprint(
                        publishedBaseline,
                        "published.chair.prefab");

                // Real asset, same meshes/transforms, changed project material.
                WriteSourceVariant(
                    canonical,
                    true,
                    1f);

                GameObject materialSource =
                    LoadDiagnosticSource();

                SavicManifest materialRevision =
                    CloneManifest(
                        publishedBaseline);

                SavicModelAnalysisRecord materialAnalysis =
                    SavicModelAnalyzer.Analyze(
                        materialSource);

                materialAnalysis.semanticParts =
                    publishedBaseline.model3D.semanticParts;

                materialRevision.model3D =
                    materialAnalysis;

                materialRevision.classification =
                    publishedBaseline.classification;

                materialRevision.family =
                    publishedBaseline.family;

                materialRevision.type =
                    publishedBaseline.type;

                materialRevision.category =
                    publishedBaseline.category;

                materialRevision.chairAuthoring =
                    publishedBaseline.chairAuthoring;

                SavicIncrementalPlan materialPlan =
                    SavicIncrementalInvalidationService.Evaluate(
                        publishedBaseline,
                        materialRevision,
                        materialAnalysis);

                Require(
                    materialPlan.Action ==
                    SavicIncrementalAction.AppearanceOnly,
                    "Real material-only edit did not select AppearanceOnly.");

                materialRevision.incremental =
                    SavicIncrementalInvalidationService.Stamp(
                        publishedBaseline.incremental,
                        materialPlan);

                UpsertSourceMirror(
                    materialRevision);

                SavicChairPublicationOutcome materialRefresh =
                    publisher.RefreshAppearanceOnly(
                        materialRevision,
                        materialSource);

                Require(
                    materialRefresh.Succeeded,
                    "Real material-only refresh failed: " +
                    materialRefresh.Message);

                RequireStableGuids(
                    prefabGuid,
                    itemGuid,
                    materialRefresh);

                string materialColliderSignature =
                    BuildColliderSignature(
                        materialRefresh.PrefabAssetPath);

                string materialVisualSignature =
                    BuildVisualMaterialSignature(
                        materialRefresh.PrefabAssetPath);

                Require(
                    string.Equals(
                        baselineColliderSignature,
                        materialColliderSignature,
                        StringComparison.Ordinal),
                    "Material-only refresh changed collider topology.");

                Require(
                    !string.Equals(
                        baselineVisualSignature,
                        materialVisualSignature,
                        StringComparison.Ordinal),
                    "Material-only refresh did not change published appearance.");

                Require(
                    materialVisualSignature.Contains(
                        DiagnosticMaterialPath,
                        StringComparison.Ordinal),
                    "Published material-only refresh does not reference the diagnostic material.");

                Require(
                    string.Equals(
                        baselineSemanticSignature,
                        JsonUtility.ToJson(
                            materialRevision.model3D.semanticParts,
                            false),
                        StringComparison.Ordinal),
                    "Material-only refresh rebuilt semantic parts.");

                Require(
                    string.Equals(
                        baselineColliderRecord,
                        JsonUtility.ToJson(
                            materialRevision.chairColliders,
                            false),
                        StringComparison.Ordinal),
                    "Material-only refresh rewrote collider authoring state.");

                Require(
                    string.Equals(
                        baselineSpatialRecord,
                        JsonUtility.ToJson(
                            materialRevision.chairSpatial,
                            false),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        baselineNavigationRecord,
                        JsonUtility.ToJson(
                            materialRevision.chairNavigation,
                            false),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        baselinePersistenceRecord,
                        JsonUtility.ToJson(
                            materialRevision.chairPersistence,
                            false),
                        StringComparison.Ordinal),
                    "Material-only refresh rebuilt spatial/navigation/persistence state.");

                string materialPublicationFingerprint =
                    RequireArtifactFingerprint(
                        materialRevision,
                        "published.chair.prefab");

                Require(
                    !string.Equals(
                        baselinePublicationFingerprint,
                        materialPublicationFingerprint,
                        StringComparison.Ordinal),
                    "Appearance-only publication fingerprint did not advance.");

                SavicManifest publishedMaterial =
                    CloneManifest(
                        materialRevision);

                // Same real asset and material, structural X change.
                WriteSourceVariant(
                    canonical,
                    true,
                    GeometryScaleX);

                GameObject geometrySource =
                    LoadDiagnosticSource();

                SavicManifest geometryRevision =
                    CloneManifest(
                        publishedMaterial);

                SavicModelAnalysisRecord geometryAnalysis =
                    SavicModelAnalyzer.Analyze(
                        geometrySource);

                geometryRevision.model3D =
                    geometryAnalysis;

                SavicIncrementalPlan geometryPlan =
                    SavicIncrementalInvalidationService.Evaluate(
                        publishedMaterial,
                        geometryRevision,
                        geometryAnalysis);

                Require(
                    geometryPlan.Action ==
                    SavicIncrementalAction.FullRebuild,
                    "Real structural edit did not select FullRebuild.");

                geometryRevision.incremental =
                    SavicIncrementalInvalidationService.Stamp(
                        publishedMaterial.incremental,
                        geometryPlan);

                RebuildClassificationAndSemantics(
                    geometryRevision,
                    geometrySource);

                Require(
                    SavicChairAuthoringPlanner.TryPlan(
                        geometryRevision,
                        out SavicChairAuthoringRecord geometryAuthoring,
                        out string planningError),
                    "Real structural edit could not be replanned safely: " +
                    planningError);

                geometryRevision.chairAuthoring =
                    geometryAuthoring;

                geometryRevision.status =
                    "PLANNED";

                UpsertSourceMirror(
                    geometryRevision);

                SavicChairPublicationOutcome geometryRebuild =
                    publisher.Publish(
                        geometryRevision,
                        geometrySource);

                Require(
                    geometryRebuild.Succeeded,
                    "Real geometry rebuild failed: " +
                    geometryRebuild.Message);

                RequireStableGuids(
                    prefabGuid,
                    itemGuid,
                    geometryRebuild);

                string geometryColliderSignature =
                    BuildColliderSignature(
                        geometryRebuild.PrefabAssetPath);

                string geometryPublicationFingerprint =
                    RequireArtifactFingerprint(
                        geometryRevision,
                        "published.chair.prefab");

                Require(
                    !string.Equals(
                        materialPublicationFingerprint,
                        geometryPublicationFingerprint,
                        StringComparison.Ordinal),
                    "Geometry rebuild did not advance publication fingerprint.");

                Require(
                    !string.Equals(
                        materialColliderSignature,
                        geometryColliderSignature,
                        StringComparison.Ordinal),
                    "Structural edit did not rebuild collider topology.");

                Require(
                    geometryRevision.chairSpatial != null &&
                    geometryRevision.chairSpatial.validated &&
                    geometryRevision.chairNavigation != null &&
                    geometryRevision.chairNavigation.validated &&
                    geometryRevision.chairPersistence != null &&
                    geometryRevision.chairPersistence.validated,
                    "Structural rebuild did not revalidate downstream runtime contracts.");

                Require(
                    CountCatalogEntries(
                        DiagnosticContentId) ==
                    1,
                    "Incremental probe created duplicate catalog entries.");

                Debug.Log(
                    "[SAVIC] INCREMENTAL REAL ASSET PROBE - PASS\n" +
                    "Real chair baseline publication: PASS\n" +
                    "Material-only detection: APPEARANCE_ONLY\n" +
                    "Material-only visual refresh: PASS\n" +
                    "Collider preservation on material edit: PASS\n" +
                    "Semantic/BBSIS/navigation/persistence reuse: PASS\n" +
                    "Structural edit detection: FULL_REBUILD\n" +
                    "Collider rebuild after structural edit: PASS\n" +
                    "Prefab/item GUID preservation: PASS\n" +
                    "Catalog duplication guard: PASS");
            }
            finally
            {
                CleanupDiagnosticResidue(
                    context);
            }
        }

        private static SavicManifest BuildFullManifest(
            GameObject source,
            SavicManifest previous)
        {
            SavicManifest manifest =
                previous == null
                    ? CreateManifest()
                    : CloneManifest(previous);

            manifest.model3D =
                SavicModelAnalyzer.Analyze(
                    source);

            RebuildClassificationAndSemantics(
                manifest,
                source);

            Require(
                string.Equals(
                    manifest.classification.type,
                    "Chair",
                    StringComparison.Ordinal),
                "Real diagnostic asset was not classified as Chair.");

            Require(
                manifest.model3D.semanticParts != null &&
                manifest.model3D.semanticParts.automationReady,
                "Real diagnostic chair semantic parts are not automation-ready.");

            Require(
                SavicChairAuthoringPlanner.TryPlan(
                    manifest,
                    out SavicChairAuthoringRecord plan,
                    out string planningError),
                "Real diagnostic chair could not be planned: " +
                planningError);

            manifest.chairAuthoring =
                plan;

            manifest.status =
                "PLANNED";

            return manifest;
        }

        private static SavicManifest CreateManifest()
        {
            string now =
                DateTime.UtcNow.ToString("O");

            return new SavicManifest
            {
                savicId =
                    DiagnosticSavicId,
                canonicalContentId =
                    DiagnosticContentId,
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
                                "savic-incremental-real-asset-probe-v1"),
                        originalFileName =
                            "chair_incremental_real_probe.fbx",
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
                    }
            };
        }

        private static void RebuildClassificationAndSemantics(
            SavicManifest manifest,
            GameObject source)
        {
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
                    source,
                    manifest.model3D,
                    classification);

            manifest.model3D.semanticParts =
                semantic;

            SavicResolvedMaterialSemantic resolved =
                SavicMaterialSemanticResolver.Resolve(
                    manifest);

            manifest.materialSemantic =
                new SavicMaterialSemanticResolutionRecord
                {
                    resolved =
                        resolved.IsKnown,
                    resolverVersion =
                        SavicMaterialSemanticResolver.Version,
                    semantic =
                        resolved.Semantic,
                    confidence =
                        resolved.Confidence,
                    score =
                        resolved.Score,
                    source =
                        resolved.Source,
                    evidence =
                        resolved.Evidence,
                    resolvedUtc =
                        DateTime.UtcNow.ToString("O")
                };
        }

        private static void UpsertSourceMirror(
            SavicManifest manifest)
        {
            SavicManifestMutations.UpsertArtifact(
                manifest,
                "unity.source_mirror",
                DiagnosticSourcePath,
                "savic.incremental-real-asset-probe",
                "1.0.0",
                AssetDatabase
                    .GetAssetDependencyHash(
                        DiagnosticSourcePath)
                    .ToString());
        }

        private static void WriteSourceVariant(
            GameObject canonical,
            bool useProbeMaterial,
            float geometryScaleX)
        {
            if (canonical == null)
                throw new ArgumentNullException(nameof(canonical));

            EnsureAssetFolder(
                DiagnosticRoot);

            if (!useProbeMaterial &&
                AssetDatabase.LoadMainAssetAtPath(
                    DiagnosticMaterialPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    DiagnosticMaterialPath);
            }

            GameObject root =
                new GameObject(
                    "SAVIC_IncrementalRealAsset_Source");

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    canonical) as GameObject;

            if (instance == null)
            {
                instance =
                    Object.Instantiate(
                        canonical);
            }

            Require(
                instance != null,
                "Could not instantiate canonical real chair.");

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(
                        instance))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        instance,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                instance.name =
                    "Model";

                instance.transform.SetParent(
                    root.transform,
                    false);

                instance.transform.localPosition =
                    Vector3.zero;

                instance.transform.localRotation =
                    Quaternion.identity;

                instance.transform.localScale =
                    new Vector3(
                        geometryScaleX,
                        1f,
                        1f);

                if (useProbeMaterial)
                {
                    ApplyProbeMaterial(
                        instance);
                }

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        DiagnosticSourcePath);

                Require(
                    saved != null,
                    "Could not save diagnostic real source prefab.");
            }
            finally
            {
                Object.DestroyImmediate(
                    root);
            }

            AssetDatabase.SaveAssets();

            AssetDatabase.ImportAsset(
                DiagnosticSourcePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void ApplyProbeMaterial(
            GameObject root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren
                    <Renderer>(true);

            Renderer target =
                renderers.FirstOrDefault(
                    renderer =>
                        renderer != null &&
                        renderer.sharedMaterials != null &&
                        renderer.sharedMaterials.Length > 0 &&
                        renderer.sharedMaterials.Any(
                            material => material != null));

            Require(
                target != null,
                "Canonical chair has no material slot for the appearance probe.");

            Material sourceMaterial =
                target.sharedMaterials.First(
                    material => material != null);

            Material probeMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    DiagnosticMaterialPath);

            if (probeMaterial == null)
            {
                probeMaterial =
                    new Material(
                        sourceMaterial)
                    {
                        name =
                            "SAVIC_Incremental_Probe_Material"
                    };

                SetDistinctBaseColor(
                    probeMaterial,
                    sourceMaterial);

                AssetDatabase.CreateAsset(
                    probeMaterial,
                    DiagnosticMaterialPath);
            }

            Material[] materials =
                target.sharedMaterials;

            for (int index = 0;
                 index < materials.Length;
                 index++)
            {
                if (materials[index] != null)
                {
                    materials[index] =
                        probeMaterial;
                    break;
                }
            }

            target.sharedMaterials =
                materials;
        }

        private static void SetDistinctBaseColor(
            Material target,
            Material source)
        {
            Color original =
                Color.white;

            if (source != null)
            {
                if (source.HasProperty("_BaseColor"))
                {
                    original =
                        source.GetColor(
                            "_BaseColor");
                }
                else if (source.HasProperty("_Color"))
                {
                    original =
                        source.GetColor(
                            "_Color");
                }
            }

            Color changed =
                new Color(
                    Mathf.Repeat(
                        original.r + 0.37f,
                        1f),
                    Mathf.Repeat(
                        original.g + 0.19f,
                        1f),
                    Mathf.Repeat(
                        original.b + 0.53f,
                        1f),
                    Math.Max(
                        0.25f,
                        original.a));

            if (target.HasProperty("_BaseColor"))
            {
                target.SetColor(
                    "_BaseColor",
                    changed);
            }

            if (target.HasProperty("_Color"))
            {
                target.SetColor(
                    "_Color",
                    changed);
            }

            EditorUtility.SetDirty(
                target);
        }

        private static GameObject LoadDiagnosticSource()
        {
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DiagnosticSourcePath);

            Require(
                source != null,
                "Diagnostic source prefab could not be loaded.");

            return source;
        }

        private static string BuildVisualMaterialSignature(
            string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Require(
                prefab != null,
                "Published prefab missing while reading material signature.");

            Transform sourceRoot =
                prefab.transform.Find(
                    "OperationalMotionRoot/Visual/SourceModel");

            Require(
                sourceRoot != null,
                "Published chair has no SourceModel visual root.");

            List<string> rows =
                new List<string>();

            Renderer[] renderers =
                sourceRoot.GetComponentsInChildren
                    <Renderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                Material[] materials =
                    renderer.sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material =
                        materials[materialIndex];

                    if (material == null)
                    {
                        rows.Add(
                            "NULL");
                        continue;
                    }

                    Color color =
                        material.HasProperty("_BaseColor")
                            ? material.GetColor("_BaseColor")
                            : material.HasProperty("_Color")
                                ? material.GetColor("_Color")
                                : Color.white;

                    rows.Add(
                        string.Join(
                            "|",
                            AssetDatabase.GetAssetPath(
                                material),
                            material.name,
                            F(color.r),
                            F(color.g),
                            F(color.b),
                            F(color.a)));
                }
            }

            rows.Sort(
                StringComparer.Ordinal);

            return string.Join(
                "\n",
                rows);
        }

        private static string BuildColliderSignature(
            string prefabPath)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Require(
                prefab != null,
                "Published prefab missing while reading collider signature.");

            List<string> rows =
                new List<string>();

            Collider[] colliders =
                prefab.GetComponentsInChildren
                    <Collider>(true);

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider =
                    colliders[index];

                StringBuilder row =
                    new StringBuilder();

                row.Append(
                    GetRelativePath(
                        prefab.transform,
                        collider.transform));

                row.Append('|');
                row.Append(
                    collider.GetType().Name);

                row.Append('|');
                AppendVector(
                    row,
                    collider.transform.localPosition);

                row.Append('|');
                AppendQuaternion(
                    row,
                    collider.transform.localRotation);

                row.Append('|');
                AppendVector(
                    row,
                    collider.transform.localScale);

                switch (collider)
                {
                    case BoxCollider box:
                        row.Append("|B|");
                        AppendVector(
                            row,
                            box.center);
                        row.Append('|');
                        AppendVector(
                            row,
                            box.size);
                        break;

                    case SphereCollider sphere:
                        row.Append("|S|");
                        AppendVector(
                            row,
                            sphere.center);
                        row.Append('|');
                        row.Append(
                            F(sphere.radius));
                        break;

                    case CapsuleCollider capsule:
                        row.Append("|C|");
                        AppendVector(
                            row,
                            capsule.center);
                        row.Append('|');
                        row.Append(
                            F(capsule.radius));
                        row.Append('|');
                        row.Append(
                            F(capsule.height));
                        row.Append('|');
                        row.Append(
                            capsule.direction.ToString(
                                CultureInfo.InvariantCulture));
                        break;

                    case MeshCollider mesh:
                        row.Append("|M|");
                        row.Append(
                            AssetDatabase.GetAssetPath(
                                mesh.sharedMesh));
                        row.Append('|');
                        row.Append(
                            mesh.convex
                                ? "1"
                                : "0");
                        break;
                }

                rows.Add(
                    row.ToString());
            }

            rows.Sort(
                StringComparer.Ordinal);

            return string.Join(
                "\n",
                rows);
        }

        private static string RequireArtifactFingerprint(
            SavicManifest manifest,
            string role)
        {
            SavicArtifactRecord artifact =
                manifest?.artifacts?.FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        string.Equals(
                            candidate.role,
                            role,
                            StringComparison.Ordinal));

            Require(
                artifact != null &&
                !string.IsNullOrWhiteSpace(
                    artifact.inputFingerprint),
                "Required artifact fingerprint missing: " +
                role);

            return artifact.inputFingerprint;
        }

        private static void RequireStableGuids(
            string expectedPrefabGuid,
            string expectedItemGuid,
            SavicChairPublicationOutcome publication)
        {
            Require(
                string.Equals(
                    expectedPrefabGuid,
                    AssetDatabase.AssetPathToGUID(
                        publication.PrefabAssetPath),
                    StringComparison.Ordinal),
                "Published prefab GUID changed during incremental processing.");

            Require(
                string.Equals(
                    expectedItemGuid,
                    AssetDatabase.AssetPathToGUID(
                        publication.ItemDefinitionAssetPath),
                    StringComparison.Ordinal),
                "Published item GUID changed during incremental processing.");
        }

        private static string RequireGuid(
            string assetPath,
            string label)
        {
            string guid =
                AssetDatabase.AssetPathToGUID(
                    assetPath);

            Require(
                !string.IsNullOrWhiteSpace(
                    guid),
                label +
                " has no stable Unity GUID.");

            return guid;
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

        private static SavicManifest CloneManifest(
            SavicManifest manifest)
        {
            string json =
                JsonUtility.ToJson(
                    manifest,
                    false);

            return JsonUtility.FromJson
                <SavicManifest>(
                    json);
        }

        private static string GetRelativePath(
            Transform root,
            Transform child)
        {
            if (root == child)
                return ".";

            List<string> segments =
                new List<string>();

            Transform cursor =
                child;

            while (cursor != null &&
                   cursor != root)
            {
                segments.Add(
                    cursor.name);

                cursor =
                    cursor.parent;
            }

            segments.Reverse();

            return string.Join(
                "/",
                segments);
        }

        private static void AppendVector(
            StringBuilder builder,
            Vector3 value)
        {
            builder.Append(F(value.x));
            builder.Append(',');
            builder.Append(F(value.y));
            builder.Append(',');
            builder.Append(F(value.z));
        }

        private static void AppendQuaternion(
            StringBuilder builder,
            Quaternion value)
        {
            builder.Append(F(value.x));
            builder.Append(',');
            builder.Append(F(value.y));
            builder.Append(',');
            builder.Append(F(value.z));
            builder.Append(',');
            builder.Append(F(value.w));
        }

        private static string F(
            float value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private static void EnsureAssetFolder(
            string assetFolder)
        {
            string normalized =
                assetFolder
                    .Replace('\\', '/')
                    .TrimEnd('/');

            Require(
                normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal),
                "Diagnostic folder must live under Assets.");

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

        private static void CleanupDiagnosticResidue(
            SavicEditorContext context)
        {
            if (context == null)
                return;

            RemoveCatalogEntry();

            if (AssetDatabase.IsValidFolder(
                    DiagnosticPublishedFolder))
            {
                AssetDatabase.DeleteAsset(
                    DiagnosticPublishedFolder);
            }

            if (AssetDatabase.IsValidFolder(
                    DiagnosticRoot))
            {
                AssetDatabase.DeleteAsset(
                    DiagnosticRoot);
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
