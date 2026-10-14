using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicConstructionPublicationOutcome
    {
        internal SavicConstructionPublicationOutcome(
            bool succeeded,
            string message,
            string prefabAssetPath)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            PrefabAssetPath = prefabAssetPath ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
        internal string PrefabAssetPath { get; }
    }

    internal sealed class SavicConstructionPublisher
    {
        internal const string Version = "1.0.0";

        private const string ConstructionKitPath =
            "Assets/Resources/BistroBuilder/Construction/ConstructionAssetKit.asset";

        private const string PrefabArtifactRole =
            "published.construction.prefab";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicConstructionPublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicConstructionPublicationOutcome Publish(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (sourceModelAsset == null)
                throw new ArgumentNullException(nameof(sourceModelAsset));

            SavicConstructionAuthoringRecord plan = manifest.construction;
            if (plan == null || !plan.planned)
            {
                return Fail(
                    "Construction publication requires a valid authoring plan.");
            }

            BistroBuilderConstructionAssetKit kit =
                AssetDatabase.LoadAssetAtPath<BistroBuilderConstructionAssetKit>(
                    ConstructionKitPath);

            if (kit == null)
            {
                return Fail(
                    "Canonical ConstructionAssetKit is missing at " +
                    ConstructionKitPath +
                    ".");
            }

            string contentFolder =
                Path.GetDirectoryName(plan.prefabAssetPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(contentFolder))
                return Fail("Construction publication folder could not be resolved.");

            EnsureAssetFolder(contentFolder);

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_construction_" + manifest.canonicalContentId);

            transaction.CaptureAsset(plan.prefabAssetPath);
            transaction.CaptureAsset(ConstructionKitPath);

            try
            {
                BuildOrReplacePrefab(
                    manifest,
                    plan,
                    sourceModelAsset);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        plan.prefabAssetPath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Published construction prefab could not be loaded.");
                }

                RegisterInConstructionKit(
                    kit,
                    plan,
                    prefab);

                EditorUtility.SetDirty(kit);
                AssetDatabase.SaveAssets();

                if (!ValidateAndStampReadiness(
                        manifest,
                        plan,
                        kit,
                        prefab,
                        out string readinessError))
                {
                    throw new InvalidOperationException(readinessError);
                }

                string fingerprint =
                    BuildPublicationFingerprint(
                        manifest,
                        plan);

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    PrefabArtifactRole,
                    plan.prefabAssetPath,
                    "savic.construction-publisher",
                    Version,
                    fingerprint);

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Construction.Publication",
                    "PASS",
                    "INFO",
                    manifest.constructionReadiness.evidence,
                    Version);

                manifest.status = "PUBLISHED";
                manifests.Save(manifest);
                transaction.Commit();

                return new SavicConstructionPublicationOutcome(
                    true,
                    "Construction asset published and registered by definition id.",
                    plan.prefabAssetPath);
            }
            catch (Exception exception)
            {
                return Fail(
                    "Construction publication failed: " +
                    exception.Message,
                    plan.prefabAssetPath);
            }
        }

        internal SavicConstructionPublicationOutcome RefreshAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModelAsset)
        {
            // Construction runtime identity lives in definitionId and the kit registry.
            // Rebuilding the visual prefab is safe while preserving both.
            return Publish(manifest, sourceModelAsset);
        }

        private static void RegisterInConstructionKit(
            BistroBuilderConstructionAssetKit kit,
            SavicConstructionAuthoringRecord plan,
            GameObject prefab)
        {
            Vector3 nominalSize =
                new Vector3(
                    plan.nominalWidthMeters,
                    plan.nominalHeightMeters,
                    plan.nominalDepthMeters);

            if (string.Equals(
                    plan.role,
                    SavicConstructionAuthoringPlanner.WallRole,
                    StringComparison.Ordinal))
            {
                kit.UpsertWallVisual(
                    plan.definitionId,
                    prefab,
                    nominalSize);
                return;
            }

            kit.UpsertOpening(
                plan.definitionId,
                plan.openingType,
                prefab,
                nominalSize);
        }

        private static void BuildOrReplacePrefab(
            SavicManifest manifest,
            SavicConstructionAuthoringRecord plan,
            GameObject sourceModelAsset)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;

            try
            {
                root = new GameObject(plan.definitionId);
                SceneManager.MoveGameObjectToScene(root, scene);

                GameObject visualRoot = new GameObject("Visual");
                SceneManager.MoveGameObjectToScene(visualRoot, scene);
                visualRoot.transform.SetParent(root.transform, false);
                visualRoot.transform.localRotation =
                    Quaternion.Euler(0f, plan.visualYawDegrees, 0f);

                GameObject source =
                    PrefabUtility.InstantiatePrefab(
                        sourceModelAsset,
                        scene) as GameObject;

                if (source == null)
                {
                    source = Object.Instantiate(sourceModelAsset);
                    SceneManager.MoveGameObjectToScene(source, scene);
                }

                source.name = "SourceModel";
                source.transform.SetParent(visualRoot.transform, false);
                source.transform.localRotation = Quaternion.identity;
                source.transform.localScale = Vector3.one;

                SavicModelAnalysisRecord analysis = manifest.model3D;
                float minY =
                    analysis.boundsCenterY -
                    analysis.heightMeters * 0.5f;

                bool wall =
                    string.Equals(
                        plan.role,
                        SavicConstructionAuthoringPlanner.WallRole,
                        StringComparison.Ordinal);

                source.transform.localPosition =
                    new Vector3(
                        -analysis.boundsCenterX,
                        wall
                            ? -analysis.boundsCenterY
                            : -minY,
                        -analysis.boundsCenterZ);

                RemovePhysicalAuthority(source);

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        plan.prefabAssetPath);

                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not confirm construction prefab save.");
                }
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.ImportAsset(
                plan.prefabAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void RemovePhysicalAuthority(
            GameObject source)
        {
            Collider[] colliders =
                source.GetComponentsInChildren<Collider>(true);

            for (int i = colliders.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(colliders[i]);

            Rigidbody[] rigidbodies =
                source.GetComponentsInChildren<Rigidbody>(true);

            for (int i = rigidbodies.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(rigidbodies[i]);
        }

        private static bool ValidateAndStampReadiness(
            SavicManifest manifest,
            SavicConstructionAuthoringRecord plan,
            BistroBuilderConstructionAssetKit kit,
            GameObject prefab,
            out string error)
        {
            error = string.Empty;

            bool prefabResolvable = prefab != null;
            bool kitResolvable = kit != null;
            bool definitionResolvable = false;
            bool passageColliderSafe = true;
            bool wallVisualOnly =
                string.Equals(
                    plan.role,
                    SavicConstructionAuthoringPlanner.WallRole,
                    StringComparison.Ordinal);

            Vector3 resolvedSize = Vector3.zero;
            GameObject resolvedPrefab = null;

            if (wallVisualOnly)
            {
                definitionResolvable =
                    kit.TryResolveWallVisual(
                        plan.definitionId,
                        out resolvedPrefab,
                        out resolvedSize);
            }
            else
            {
                definitionResolvable =
                    kit.TryResolveOpening(
                        plan.openingType,
                        plan.definitionId,
                        out resolvedPrefab,
                        out resolvedSize);

                passageColliderSafe =
                    prefab.GetComponentsInChildren<Collider>(true).Length == 0;
            }

            bool exactPrefab =
                definitionResolvable &&
                resolvedPrefab == prefab;

            bool sizeMatches =
                definitionResolvable &&
                Mathf.Abs(resolvedSize.x - plan.nominalWidthMeters) <= 0.001f &&
                Mathf.Abs(resolvedSize.y - plan.nominalHeightMeters) <= 0.001f &&
                Mathf.Abs(resolvedSize.z - plan.nominalDepthMeters) <= 0.001f;

            bool valid =
                prefabResolvable &&
                kitResolvable &&
                definitionResolvable &&
                exactPrefab &&
                sizeMatches &&
                passageColliderSafe;

            string evidence =
                valid
                    ? "Construction definition resolves exactly to the published prefab with canonical nominal dimensions; opening fills contain no collider authority."
                    : "Construction readiness failed: prefab=" +
                      prefabResolvable +
                      ", kit=" +
                      kitResolvable +
                      ", definition=" +
                      definitionResolvable +
                      ", exactPrefab=" +
                      exactPrefab +
                      ", size=" +
                      sizeMatches +
                      ", passageSafe=" +
                      passageColliderSafe +
                      ".";

            manifest.constructionReadiness =
                new SavicConstructionReadinessRecord
                {
                    validated = valid,
                    validatorVersion = Version,
                    prefabResolvable = prefabResolvable,
                    constructionKitResolvable = kitResolvable,
                    definitionResolvable =
                        definitionResolvable &&
                        exactPrefab &&
                        sizeMatches,
                    passageColliderSafe = passageColliderSafe,
                    wallVisualOnly = wallVisualOnly,
                    definitionId = plan.definitionId,
                    role = plan.role,
                    prefabAssetPath = plan.prefabAssetPath,
                    evidence = evidence,
                    validatedUtc = DateTime.UtcNow.ToString("O")
                };

            if (valid)
                return true;

            error = evidence;
            return false;
        }

        private static string BuildPublicationFingerprint(
            SavicManifest manifest,
            SavicConstructionAuthoringRecord plan)
        {
            return string.Join(
                "|",
                Version,
                manifest.source?.sourceHash ?? string.Empty,
                manifest.classification?.classifierVersion ?? string.Empty,
                plan.plannerVersion ?? string.Empty,
                plan.role ?? string.Empty,
                plan.definitionId ?? string.Empty,
                plan.nominalWidthMeters.ToString("R"),
                plan.nominalHeightMeters.ToString("R"),
                plan.nominalDepthMeters.ToString("R"),
                plan.visualYawDegrees.ToString("R"));
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts = normalized.Split('/');
            if (parts.Length == 0 ||
                !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Construction asset folder must be under Assets.");
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static SavicConstructionPublicationOutcome Fail(
            string message,
            string prefabAssetPath = "")
        {
            return new SavicConstructionPublicationOutcome(
                false,
                message,
                prefabAssetPath);
        }
    }
}
