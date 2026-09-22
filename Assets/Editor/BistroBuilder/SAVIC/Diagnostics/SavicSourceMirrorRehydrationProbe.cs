using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicSourceMirrorRehydrationProbe
    {
        private const string SourceMirrorRole =
            "unity.source_mirror";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Source Mirror Rehydration Probe",
            false,
            114)]
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

            SavicManifest manifest =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                manifest != null,
                "No published SAVIC table exists for mirror rehydration.");

            SavicArtifactRecord mirrorArtifact =
                FindArtifact(
                    manifest,
                    SourceMirrorRole);

            Require(
                mirrorArtifact != null &&
                !string.IsNullOrWhiteSpace(
                    mirrorArtifact.projectRelativePath),
                "Published manifest has no Unity source-mirror artifact.");

            string mirrorAssetPath =
                mirrorArtifact.projectRelativePath
                    .Replace('\\', '/');

            string mirrorAbsolutePath =
                ToAbsoluteProjectPath(
                    mirrorAssetPath);

            string archivedAbsolutePath =
                context.Layout.FromProjectRelativePath(
                    manifest.source.archivedRelativePath);

            Require(
                File.Exists(mirrorAbsolutePath),
                "Unity source-mirror payload is missing before probe.");

            Require(
                File.Exists(archivedAbsolutePath),
                "Immutable archived source is missing before probe.");

            string expectedHash =
                manifest.source.sourceHash;

            Require(
                string.Equals(
                    SavicHashService.ComputeSha256(
                        archivedAbsolutePath),
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase),
                "Immutable archived source failed integrity validation.");

            Require(
                string.Equals(
                    SavicHashService.ComputeSha256(
                        mirrorAbsolutePath),
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase),
                "Unity source mirror does not match immutable source.");

            string guidBefore =
                AssetDatabase.AssetPathToGUID(
                    mirrorAssetPath);

            Require(
                !string.IsNullOrWhiteSpace(guidBefore),
                "Unity source mirror has no stable GUID before probe.");

            try
            {
                File.Delete(
                    mirrorAbsolutePath);

                Require(
                    !File.Exists(mirrorAbsolutePath),
                    "Probe could not remove the rebuildable mirror payload.");

                SavicSourceProcessingOutcome outcome =
                    context.SourceProcessing.Process(
                        manifest);

                Require(
                    outcome.Succeeded,
                    "Source mirror rehydration failed: " +
                    outcome.Message);

                Require(
                    File.Exists(mirrorAbsolutePath),
                    "SAVIC did not recreate the missing source-mirror payload.");

                Require(
                    string.Equals(
                        SavicHashService.ComputeSha256(
                            mirrorAbsolutePath),
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase),
                    "Rehydrated mirror payload does not match immutable source.");

                string guidAfter =
                    AssetDatabase.AssetPathToGUID(
                        mirrorAssetPath);

                Require(
                    string.Equals(
                        guidBefore,
                        guidAfter,
                        StringComparison.Ordinal),
                    "Source-mirror GUID changed during deterministic rehydration.");

                ValidatePublishedPrefabReferences(
                    outcome.Manifest);

                Debug.Log(
                    "[SAVIC] SOURCE MIRROR REHYDRATION PROBE - PASS\n" +
                    "SavicId: " +
                    outcome.Manifest.savicId +
                    "\nMirror GUID preserved: " +
                    guidAfter +
                    "\nSourceHash: " +
                    expectedHash);
            }
            finally
            {
                if (!File.Exists(mirrorAbsolutePath) &&
                    File.Exists(archivedAbsolutePath))
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            mirrorAbsolutePath)
                        ?? throw new InvalidOperationException(
                            "Mirror directory could not be resolved."));

                    File.Copy(
                        archivedAbsolutePath,
                        mirrorAbsolutePath,
                        false);

                    AssetDatabase.ImportAsset(
                        mirrorAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static SavicManifest FindPublishedTable(
            IReadOnlyList<SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    candidate.tableAuthoring != null &&
                    candidate.tableAuthoring.planned)
                {
                    return candidate;
                }
            }

            return null;
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

        private static void ValidatePublishedPrefabReferences(
            SavicManifest manifest)
        {
            string prefabPath =
                manifest?.tableAuthoring?
                    .prefabAssetPath;

            Require(
                !string.IsNullOrWhiteSpace(prefabPath),
                "Published table manifest has no prefab path.");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            Require(
                prefab != null,
                "Published table prefab could not be loaded after mirror rehydration.");

            MeshFilter[] filters =
                prefab.GetComponentsInChildren
                    <MeshFilter>(true);

            Require(
                filters.Length > 0,
                "Published table prefab has no mesh filters.");

            for (int index = 0;
                 index < filters.Length;
                 index++)
            {
                Require(
                    filters[index].sharedMesh != null,
                    "Published table prefab lost a mesh reference after mirror rehydration.");
            }

            Renderer[] renderers =
                prefab.GetComponentsInChildren
                    <Renderer>(true);

            Require(
                renderers.Length > 0,
                "Published table prefab has no renderers.");

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Material[] materials =
                    renderers[rendererIndex]
                        .sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Require(
                        materials[materialIndex] != null,
                        "Published table prefab lost a material reference after mirror rehydration.");
                }
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

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
