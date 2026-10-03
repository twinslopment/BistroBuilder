using System;
using System.IO;
using BistroBuilder.UI.Iconography;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicImageAuthoringPlanner
    {
        internal const string Version = "1.0.0";

        internal const string ContentImageRole = "CONTENT_IMAGE";
        internal const string MaterialTextureRole = "MATERIAL_TEXTURE";
        internal const string UiIconRole = "UI_ICON";

        internal static bool TryPlan(
            SavicManifest manifest,
            out SavicImageAuthoringRecord plan,
            out string reasonCode,
            out string error)
        {
            plan =
                new SavicImageAuthoringRecord
                {
                    plannerVersion = Version
                };

            reasonCode = string.Empty;
            error = string.Empty;

            if (manifest?.source == null ||
                !string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.Image.ToString(),
                    StringComparison.Ordinal))
            {
                reasonCode = "IMAGE_INVALID_MANIFEST";
                error = "Image planning requires an Image source manifest.";
                return false;
            }

            string extension =
                (manifest.source.extension ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            if (extension == ".webp")
            {
                reasonCode = "IMAGE_FORMAT_UNSUPPORTED";
                error =
                    "WebP is archived by SAVIC but is not a native Unity V1 texture source format.";
                return false;
            }

            string sourceName =
                Path.GetFileNameWithoutExtension(
                    manifest.source.originalFileName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(sourceName))
            {
                reasonCode = "IMAGE_NAME_MISSING";
                error = "Image source has no usable filename.";
                return false;
            }

            if (TryResolveUiIcon(
                    sourceName,
                    out BBIconId iconId,
                    out bool uiIntent))
            {
                EnsureCanonicalContentId(
                    manifest,
                    "bb_uiicon_" +
                    iconId.ToString().ToLowerInvariant());

                plan.planned = true;
                plan.role = UiIconRole;
                plan.targetIconId = iconId.ToString();
                plan.mapType = "UI";
                plan.importAsSprite = true;
                plan.sRgb = true;
                plan.mipmaps = false;
                plan.maximumTextureSize = 1024;
                plan.publishedAssetPath =
                    BuildPublishedPath(
                        manifest,
                        manifest.source.originalFileName);
                plan.planReason =
                    "Explicit ui_<BBIconId> filename mapped to the canonical BBIconCatalog contract.";
                plan.plannedUtc = DateTime.UtcNow.ToString("O");
                return true;
            }

            if (uiIntent)
            {
                reasonCode = "UI_ICON_ID_UNKNOWN";
                error =
                    "Filename declares UI icon intent but does not name a valid BBIconId.";
                return false;
            }

            string mapType =
                ResolveMaterialMapType(sourceName);

            if (!string.Equals(
                    mapType,
                    "NONE",
                    StringComparison.Ordinal))
            {
                EnsureCanonicalContentId(
                    manifest,
                    "bb_texture_" +
                    manifest.savicId.ToLowerInvariant());

                bool colorData =
                    mapType == "ALBEDO" ||
                    mapType == "EMISSION";

                plan.planned = true;
                plan.role = MaterialTextureRole;
                plan.mapType = mapType;
                plan.importAsSprite = false;
                plan.sRgb = colorData;
                plan.mipmaps = true;
                plan.maximumTextureSize = 4096;
                plan.publishedAssetPath =
                    BuildPublishedPath(
                        manifest,
                        manifest.source.originalFileName);
                plan.planReason =
                    "Explicit material-map token mapped image to a deterministic texture import profile.";
                plan.plannedUtc = DateTime.UtcNow.ToString("O");
                return true;
            }

            EnsureCanonicalContentId(
                manifest,
                "bb_image_" +
                manifest.savicId.ToLowerInvariant());

            plan.planned = true;
            plan.role = ContentImageRole;
            plan.mapType = "GENERIC";
            plan.importAsSprite = true;
            plan.sRgb = true;
            plan.mipmaps = false;
            plan.maximumTextureSize = 2048;
            plan.publishedAssetPath =
                BuildPublishedPath(
                    manifest,
                    manifest.source.originalFileName);
            plan.planReason =
                "Standalone image without material/UI intent published as a managed content Sprite.";
            plan.plannedUtc = DateTime.UtcNow.ToString("O");
            return true;
        }

        private static bool TryResolveUiIcon(
            string sourceName,
            out BBIconId iconId,
            out bool uiIntent)
        {
            iconId = default;
            uiIntent = false;

            string candidate = sourceName.Trim();

            if (candidate.StartsWith(
                    "ui_",
                    StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(3);
                uiIntent = true;
            }
            else if (candidate.StartsWith(
                         "ui-",
                         StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(3);
                uiIntent = true;
            }
            else
            {
                return false;
            }

            int suffixIndex =
                candidate.IndexOf(
                    "__",
                    StringComparison.Ordinal);

            if (suffixIndex > 0)
                candidate = candidate.Substring(0, suffixIndex);

            return Enum.TryParse(
                candidate,
                true,
                out iconId);
        }

        private static string ResolveMaterialMapType(
            string sourceName)
        {
            string normalized =
                sourceName
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_");

            string[] parts =
                normalized.Split(
                    new[] { '_' },
                    StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i];

                if (token == "normal" ||
                    token == "norm" ||
                    token == "nrm")
                {
                    return "NORMAL";
                }

                if (token == "albedo" ||
                    token == "basecolor" ||
                    token == "basecolour" ||
                    token == "diffuse")
                {
                    return "ALBEDO";
                }

                if (token == "metallic" ||
                    token == "metalness")
                {
                    return "METALLIC";
                }

                if (token == "roughness" ||
                    token == "rough")
                {
                    return "ROUGHNESS";
                }

                if (token == "smoothness" ||
                    token == "smooth")
                {
                    return "SMOOTHNESS";
                }

                if (token == "ao" ||
                    token == "occlusion")
                {
                    return "OCCLUSION";
                }

                if (token == "emission" ||
                    token == "emissive")
                {
                    return "EMISSION";
                }

                if (token == "mask" ||
                    token == "maskmap")
                {
                    return "MASK";
                }
            }

            return "NONE";
        }

        private static void EnsureCanonicalContentId(
            SavicManifest manifest,
            string canonicalContentId)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(manifest.canonicalContentId))
                manifest.canonicalContentId = canonicalContentId;
        }

        private static string BuildPublishedPath(
            SavicManifest manifest,
            string originalFileName)
        {
            string safeName =
                Path.GetFileName(originalFileName);

            return
                "Assets/Generated/BistroBuilder/SAVIC/Published/Images/" +
                manifest.canonicalContentId +
                "/" +
                safeName;
        }
    }
}
