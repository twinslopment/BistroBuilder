using System;
using System.Collections.Generic;
using System.IO;
using BistroBuilder.UI.Iconography;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicImagePublicationOutcome
    {
        internal SavicImagePublicationOutcome(
            bool succeeded,
            string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
    }

    internal sealed class SavicImagePublisher
    {
        internal const string Version = "1.0.0";

        private const string IconCatalogPath =
            "Assets/Resources/BistroBuilder/UI/BBIconCatalog.asset";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicImagePublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicImagePublicationOutcome Publish(
            SavicManifest manifest,
            string sourceAssetPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            SavicImageAuthoringRecord plan =
                manifest.imageAuthoring;

            if (plan == null || !plan.planned)
            {
                return Failure(
                    "Image publication requires a valid authoring plan.");
            }

            if (string.IsNullOrWhiteSpace(sourceAssetPath))
                return Failure("Imported image source path is empty.");

            string sourceAbsolute =
                ToAbsoluteProjectPath(sourceAssetPath);

            if (!File.Exists(sourceAbsolute))
                return Failure("Imported image source mirror is missing.");

            string destinationFolder =
                Path.GetDirectoryName(plan.publishedAssetPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(destinationFolder))
                return Failure("Published image folder could not be resolved.");

            EnsureAssetFolder(destinationFolder);

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_image_" +
                    manifest.canonicalContentId);

            transaction.CaptureAsset(
                plan.publishedAssetPath);

            bool uiIcon =
                string.Equals(
                    plan.role,
                    SavicImageAuthoringPlanner.UiIconRole,
                    StringComparison.Ordinal);

            if (uiIcon)
                transaction.CaptureAsset(IconCatalogPath);

            try
            {
                CopySourceBytes(
                    sourceAbsolute,
                    plan.publishedAssetPath);

                AssetDatabase.ImportAsset(
                    plan.publishedAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                TextureImporter importer =
                    AssetImporter.GetAtPath(
                        plan.publishedAssetPath) as TextureImporter;

                if (importer == null)
                {
                    throw new InvalidOperationException(
                        "Published image has no TextureImporter.");
                }

                bool sourceHasAlpha =
                    importer.DoesSourceTextureHaveAlpha();

                ApplyImporterProfile(
                    importer,
                    plan,
                    sourceHasAlpha);

                importer.SaveAndReimport();

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        plan.publishedAssetPath);

                if (texture == null)
                {
                    throw new InvalidOperationException(
                        "Published image did not resolve as Texture2D.");
                }

                if (!HasSafeDimensions(texture))
                {
                    throw new InvalidOperationException(
                        "Published image dimensions are invalid or exceed the SAVIC V1 safety limit.");
                }

                Sprite sprite =
                    plan.importAsSprite
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(
                            plan.publishedAssetPath)
                        : null;

                if (plan.importAsSprite && sprite == null)
                {
                    throw new InvalidOperationException(
                        "Published image was planned as Sprite but no Sprite was imported.");
                }

                StampManagedLabels(
                    plan.importAsSprite
                        ? (UnityEngine.Object)sprite
                        : texture,
                    plan.role);

                bool catalogBindingValid = true;

                if (uiIcon)
                {
                    catalogBindingValid =
                        BindUiIcon(
                            manifest,
                            plan,
                            sprite);

                    if (!catalogBindingValid)
                    {
                        throw new InvalidOperationException(
                            "Published UI icon could not be bound to BBIconCatalog.");
                    }
                }

                bool importerProfileValid =
                    ValidateImporterProfile(
                        importer,
                        plan);

                if (!importerProfileValid)
                {
                    throw new InvalidOperationException(
                        "Published image importer profile does not match its SAVIC role.");
                }

                manifest.imageReadiness =
                    new SavicImageReadinessRecord
                    {
                        validated = true,
                        validatorVersion = Version,
                        textureResolvable = true,
                        spriteResolvable =
                            !plan.importAsSprite ||
                            sprite != null,
                        importerProfileValid = true,
                        catalogBindingValid =
                            catalogBindingValid,
                        widthPixels = texture.width,
                        heightPixels = texture.height,
                        sourceHasAlpha = sourceHasAlpha,
                        role = plan.role,
                        mapType = plan.mapType,
                        targetIconId = plan.targetIconId,
                        publishedAssetPath =
                            plan.publishedAssetPath,
                        evidence =
                            "Image published with deterministic importer profile" +
                            (uiIcon
                                ? " and canonical BBIconCatalog binding."
                                : "."),
                        validatedUtc =
                            DateTime.UtcNow.ToString("O")
                    };

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    "published.image",
                    plan.publishedAssetPath,
                    "savic.image-publisher",
                    Version,
                    BuildFingerprint(
                        manifest,
                        plan));

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "Image.Publication",
                    "PASS",
                    "INFO",
                    manifest.imageReadiness.evidence,
                    Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "image.role",
                    plan.role,
                    "HIGH",
                    plan.planReason,
                    "image.plan.v1");

                if (!string.IsNullOrWhiteSpace(plan.mapType))
                {
                    SavicManifestMutations.UpsertDecision(
                        manifest,
                        "image.mapType",
                        plan.mapType,
                        "HIGH",
                        plan.planReason,
                        "image.plan.v1");
                }

                manifest.family =
                    uiIcon ? "UI" : "Image";
                manifest.type =
                    uiIcon
                        ? "Icon"
                        : string.Equals(
                            plan.role,
                            SavicImageAuthoringPlanner.MaterialTextureRole,
                            StringComparison.Ordinal)
                            ? "MaterialTexture"
                            : "ContentImage";
                manifest.category =
                    uiIcon
                        ? "UI"
                        : string.Equals(
                            plan.role,
                            SavicImageAuthoringPlanner.MaterialTextureRole,
                            StringComparison.Ordinal)
                            ? "Materials"
                            : "Images";
                manifest.status = "PUBLISHED";

                manifests.Save(manifest);
                AssetDatabase.SaveAssets();
                transaction.Commit();

                return new SavicImagePublicationOutcome(
                    true,
                    "Image published and validated.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Image publication failed: " +
                    exception.Message);
            }
        }

        private static void ApplyImporterProfile(
            TextureImporter importer,
            SavicImageAuthoringRecord plan,
            bool sourceHasAlpha)
        {
            importer.npotScale =
                TextureImporterNPOTScale.None;
            importer.maxTextureSize =
                Mathf.Clamp(
                    plan.maximumTextureSize,
                    32,
                    8192);
            importer.mipmapEnabled =
                plan.mipmaps;
            importer.sRGBTexture =
                plan.sRgb;
            importer.filterMode =
                FilterMode.Bilinear;

            if (plan.importAsSprite)
            {
                importer.textureType =
                    TextureImporterType.Sprite;
                importer.spriteImportMode =
                    SpriteImportMode.Single;
                importer.wrapMode =
                    TextureWrapMode.Clamp;
                importer.alphaIsTransparency =
                    sourceHasAlpha;
                importer.textureCompression =
                    string.Equals(
                        plan.role,
                        SavicImageAuthoringPlanner.UiIconRole,
                        StringComparison.Ordinal)
                        ? TextureImporterCompression.Uncompressed
                        : TextureImporterCompression.Compressed;
                importer.spritePixelsPerUnit = 100f;
                return;
            }

            importer.textureType =
                string.Equals(
                    plan.mapType,
                    "NORMAL",
                    StringComparison.Ordinal)
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;

            importer.wrapMode =
                TextureWrapMode.Repeat;
            importer.alphaIsTransparency = false;
            importer.textureCompression =
                TextureImporterCompression.Compressed;
        }

        private static bool ValidateImporterProfile(
            TextureImporter importer,
            SavicImageAuthoringRecord plan)
        {
            if (importer == null)
                return false;

            if (importer.mipmapEnabled != plan.mipmaps ||
                importer.sRGBTexture != plan.sRgb)
            {
                return false;
            }

            if (plan.importAsSprite)
            {
                return importer.textureType ==
                           TextureImporterType.Sprite &&
                       importer.spriteImportMode ==
                           SpriteImportMode.Single &&
                       importer.wrapMode ==
                           TextureWrapMode.Clamp;
            }

            TextureImporterType expected =
                string.Equals(
                    plan.mapType,
                    "NORMAL",
                    StringComparison.Ordinal)
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;

            return importer.textureType == expected &&
                   importer.wrapMode ==
                       TextureWrapMode.Repeat;
        }

        private static bool BindUiIcon(
            SavicManifest manifest,
            SavicImageAuthoringRecord plan,
            Sprite sprite)
        {
            if (sprite == null ||
                !Enum.TryParse(
                    plan.targetIconId,
                    true,
                    out BBIconId targetId))
            {
                return false;
            }

            BBIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<BBIconCatalog>(
                    IconCatalogPath);

            if (catalog == null)
                return false;

            List<BBIconDefinition> entries =
                new List<BBIconDefinition>(
                    catalog.Entries.Count);

            bool replaced = false;

            for (int i = 0;
                 i < catalog.Entries.Count;
                 i++)
            {
                BBIconDefinition entry =
                    catalog.Entries[i];

                if (entry.id == targetId)
                {
                    entries.Add(
                        new BBIconDefinition(
                            entry.id,
                            sprite,
                            entry.semanticRole,
                            "SAVIC:" +
                            manifest.canonicalContentId));

                    replaced = true;
                }
                else
                {
                    entries.Add(entry);
                }
            }

            if (!replaced)
                return false;

            catalog.EditorSetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            return catalog.TryGet(
                       targetId,
                       out BBIconDefinition resolved) &&
                   resolved.sprite == sprite;
        }

        private static void StampManagedLabels(
            UnityEngine.Object asset,
            string role)
        {
            if (asset == null)
                return;

            HashSet<string> labels =
                new HashSet<string>(
                    AssetDatabase.GetLabels(asset),
                    StringComparer.Ordinal)
                {
                    "SAVIC.Managed",
                    "SAVIC.Image"
                };

            if (string.Equals(
                    role,
                    SavicImageAuthoringPlanner.UiIconRole,
                    StringComparison.Ordinal))
            {
                labels.Add("SAVIC.UIIcon");
            }
            else if (string.Equals(
                         role,
                         SavicImageAuthoringPlanner.MaterialTextureRole,
                         StringComparison.Ordinal))
            {
                labels.Add("SAVIC.MaterialTexture");
            }
            else
            {
                labels.Add("SAVIC.ContentImage");
            }

            string[] values =
                new string[labels.Count];

            labels.CopyTo(values);
            Array.Sort(
                values,
                StringComparer.Ordinal);

            AssetDatabase.SetLabels(
                asset,
                values);
        }

        private static bool HasSafeDimensions(
            Texture2D texture)
        {
            return texture != null &&
                   texture.width > 0 &&
                   texture.height > 0 &&
                   texture.width <= 8192 &&
                   texture.height <= 8192;
        }

        private static void CopySourceBytes(
            string sourceAbsolute,
            string destinationAssetPath)
        {
            string destinationAbsolute =
                ToAbsoluteProjectPath(
                    destinationAssetPath);

            string directory =
                Path.GetDirectoryName(
                    destinationAbsolute)
                ?? throw new InvalidOperationException(
                    "Published image directory could not be resolved.");

            Directory.CreateDirectory(directory);

            if (File.Exists(destinationAbsolute) &&
                FilesEqual(
                    sourceAbsolute,
                    destinationAbsolute))
            {
                return;
            }

            string temporary =
                destinationAbsolute +
                ".tmp." +
                Guid.NewGuid().ToString("N");

            try
            {
                File.Copy(
                    sourceAbsolute,
                    temporary,
                    false);

                if (File.Exists(destinationAbsolute))
                {
                    File.Copy(
                        temporary,
                        destinationAbsolute,
                        true);
                    File.Delete(temporary);
                }
                else
                {
                    File.Move(
                        temporary,
                        destinationAbsolute);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static bool FilesEqual(
            string first,
            string second)
        {
            FileInfo a = new FileInfo(first);
            FileInfo b = new FileInfo(second);

            if (a.Length != b.Length)
                return false;

            return string.Equals(
                SavicHashService.ComputeSha256(first),
                SavicHashService.ComputeSha256(second),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFingerprint(
            SavicManifest manifest,
            SavicImageAuthoringRecord plan)
        {
            return string.Join(
                "|",
                Version,
                manifest.source?.sourceHash ?? string.Empty,
                plan.plannerVersion ?? string.Empty,
                plan.role ?? string.Empty,
                plan.mapType ?? string.Empty,
                plan.targetIconId ?? string.Empty,
                plan.importAsSprite,
                plan.sRgb,
                plan.mipmaps,
                plan.maximumTextureSize);
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

        private static void EnsureAssetFolder(
            string folder)
        {
            string normalized =
                folder.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts =
                normalized.Split('/');

            string current = "Assets";
            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static SavicImagePublicationOutcome Failure(
            string message)
        {
            return new SavicImagePublicationOutcome(
                false,
                message);
        }
    }
}
