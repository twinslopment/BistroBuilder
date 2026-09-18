using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishDraftGenerator
    {
        private const string DraftRoot = "Assets/Generated/FurnitureFinishes/GeneratedDrafts";

        public static int EnsureCandidatesForUnresolved(
            FurnitureFinishProfile profile,
            FurnitureFinishLibrary library,
            IReadOnlyList<FurnitureFinishIssue> issues,
            IReadOnlyList<FurnitureFinishProposal> currentProposals)
        {
            if (profile == null || library == null || issues == null)
                return 0;

            var alreadyResolved = new HashSet<string>(StringComparer.Ordinal);
            if (currentProposals != null)
            {
                foreach (var proposal in currentProposals)
                {
                    if (proposal != null)
                        alreadyResolved.Add(proposal.ZoneId);
                }
            }

            var created = 0;
            foreach (var zone in profile.Zones)
            {
                if (zone == null
                    || !zone.ClassificationConfirmed
                    || alreadyResolved.Contains(zone.Id))
                    continue;

                var family = ResolveFamily(zone);
                if (family == FurnitureSurfaceFamily.Unknown)
                    continue;

                Material source = null;
                var missingChannels = FurnitureFinishChannel.None;
                var actionable = false;
                foreach (var issue in issues)
                {
                    if (!string.Equals(issue.ZoneId, zone.Id, StringComparison.Ordinal))
                        continue;

                    if (issue.Kind == FurnitureFinishIssueKind.MissingMaterial
                        || issue.Kind == FurnitureFinishIssueKind.BrokenShader
                        || issue.Kind == FurnitureFinishIssueKind.PlaceholderMaterial)
                    {
                        actionable = true;
                    }
                    else if (issue.Kind == FurnitureFinishIssueKind.MissingChannel)
                    {
                        actionable = true;
                        missingChannels |= issue.Channel;
                        if (source == null && issue.SourceMaterial != null)
                            source = issue.SourceMaterial;
                    }
                }

                if (!actionable)
                    continue;

                var finish = CreateDraft(
                    profile,
                    zone,
                    family,
                    source,
                    missingChannels);
                RegisterFinish(library, finish);
                created++;
            }

            if (created > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return created;
        }

        private static FurnitureFinishDefinition CreateDraft(
            FurnitureFinishProfile profile,
            FurnitureFinishProfile.ZoneDefinition zone,
            FurnitureSurfaceFamily family,
            Material source,
            FurnitureFinishChannel missingChannels)
        {
            var folder = $"{DraftRoot}/{profile.FurnitureId}";
            FurnitureFinishAssetUtility.EnsureFolder(folder);

            var stem = FurnitureFinishAssetUtility.StableId(
                $"{profile.FurnitureId}_{zone.Id}_{family}_draft",
                "finish_draft");
            var materialPath = $"{folder}/{stem}.mat";
            var finishPath = $"{folder}/{stem}_Finish.asset";

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = source != null
                    ? source.shader
                    : Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("HDRP/Lit")
                        ?? Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("No existe un shader Lit compatible.");

                material = source != null
                    ? new Material(source)
                    : new Material(shader);
                material.name = stem + "_MAT";
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (source != null)
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            ApplyFamilyDefaults(material, family, source == null);

            var required = zone.RequiredChannels | missingChannels;
            if (source == null)
            {
                required |= FurnitureFinishChannel.BaseColorMap
                    | FurnitureFinishChannel.NormalMap
                    | FurnitureFinishChannel.MaskMap
                    | FurnitureFinishChannel.OcclusionMap;
            }
            EnsureRequiredMaps(folder, stem, material, required, family);

            var finish = AssetDatabase.LoadAssetAtPath<FurnitureFinishDefinition>(finishPath);
            if (finish == null)
            {
                finish = ScriptableObject.CreateInstance<FurnitureFinishDefinition>();
                AssetDatabase.CreateAsset(finish, finishPath);
            }

            finish.EditorConfigure(
                "draft_" + stem,
                $"Borrador automático · {zone.DisplayName}",
                family,
                material,
                FurnitureFinishAssetUtility.DetectProvidedChannels(material),
                new[] { "auto_generated", "technical_draft", "requires_review" });
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(finish);
            return finish;
        }

        private static void EnsureRequiredMaps(
            string folder,
            string stem,
            Material material,
            FurnitureFinishChannel required,
            FurnitureSurfaceFamily family)
        {
            foreach (var channel in new[]
            {
                FurnitureFinishChannel.BaseColorMap,
                FurnitureFinishChannel.NormalMap,
                FurnitureFinishChannel.MaskMap,
                FurnitureFinishChannel.OcclusionMap
            })
            {
                if ((required & channel) == 0)
                    continue;
                if (FurnitureFinishAssetUtility.GetTexture(material, channel) != null)
                    continue;

                var texture = CreateProceduralTexture(folder, stem, channel, family);
                AssignTexture(material, channel, texture);
            }
        }

        private static Texture2D CreateProceduralTexture(
            string folder,
            string stem,
            FurnitureFinishChannel channel,
            FurnitureSurfaceFamily family)
        {
            const int size = 128;
            var suffix = channel.ToString();
            var pngPath = $"{folder}/{stem}_{suffix}.png";
            var absolute = Path.GetFullPath(pngPath);

            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                channel != FurnitureFinishChannel.BaseColorMap);
            var pixels = new Color[size * size];
            var step = 1f / size;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = x / (float)(size - 1);
                    var v = y / (float)(size - 1);
                    pixels[y * size + x] = ProceduralPixel(
                        channel,
                        family,
                        u,
                        v,
                        step);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(
                pngPath,
                ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = size;

                if (channel == FurnitureFinishChannel.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                }
                else if (channel == FurnitureFinishChannel.MaskMap
                    || channel == FurnitureFinishChannel.OcclusionMap)
                {
                    importer.sRGBTexture = false;
                }

                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        private static Color ProceduralPixel(
            FurnitureFinishChannel channel,
            FurnitureSurfaceFamily family,
            float u,
            float v,
            float step)
        {
            if (channel == FurnitureFinishChannel.BaseColorMap)
                return ProceduralBaseColor(family, u, v);

            if (channel == FurnitureFinishChannel.NormalMap)
            {
                var left = SurfaceHeight(family, Mathf.Repeat(u - step, 1f), v);
                var right = SurfaceHeight(family, Mathf.Repeat(u + step, 1f), v);
                var down = SurfaceHeight(family, u, Mathf.Repeat(v - step, 1f));
                var up = SurfaceHeight(family, u, Mathf.Repeat(v + step, 1f));
                var strength = family == FurnitureSurfaceFamily.Fabric ? 2.5f : 1.5f;
                var normal = new Vector3(
                    (left - right) * strength,
                    (down - up) * strength,
                    1f).normalized;
                return new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f);
            }

            if (channel == FurnitureFinishChannel.MaskMap)
            {
                var noise = HashNoise(u * 71f, v * 71f);
                var metallic = family == FurnitureSurfaceFamily.Metal
                    ? Mathf.Lerp(0.75f, 0.95f, noise)
                    : 0f;
                var smoothness = Mathf.Clamp01(
                    DefaultSmoothness(family)
                    + (noise - 0.5f) * 0.08f);
                return new Color(metallic, 1f, 0f, smoothness);
            }

            if (channel == FurnitureFinishChannel.OcclusionMap)
            {
                var height = SurfaceHeight(family, u, v);
                var ao = Mathf.Lerp(0.88f, 1f, height);
                return new Color(ao, ao, ao, 1f);
            }

            return ChannelColor(channel, family);
        }

        private static Color ProceduralBaseColor(
            FurnitureSurfaceFamily family,
            float u,
            float v)
        {
            var baseColor = DefaultColor(family);
            var height = SurfaceHeight(family, u, v);
            var factor = Mathf.Lerp(0.78f, 1.12f, height);

            if (family == FurnitureSurfaceFamily.Wood)
            {
                var grain = 0.5f
                    + 0.5f * Mathf.Sin(
                        (v * 42f + HashNoise(u * 4f, v * 4f) * 3f)
                        * Mathf.PI * 2f);
                factor *= Mathf.Lerp(0.82f, 1.08f, grain);
            }
            else if (family == FurnitureSurfaceFamily.Stone)
            {
                var vein = Mathf.Abs(
                    Mathf.Sin(
                        (u * 5f + v * 8f + HashNoise(u * 8f, v * 8f))
                        * Mathf.PI));
                factor *= vein > 0.92f ? 0.72f : 1f;
            }
            else if (family == FurnitureSurfaceFamily.Fabric)
            {
                var weave = 0.5f
                    + 0.25f * Mathf.Sin(u * Mathf.PI * 96f)
                    + 0.25f * Mathf.Sin(v * Mathf.PI * 96f);
                factor *= Mathf.Lerp(0.9f, 1.06f, Mathf.Clamp01(weave));
            }

            return new Color(
                Mathf.Clamp01(baseColor.r * factor),
                Mathf.Clamp01(baseColor.g * factor),
                Mathf.Clamp01(baseColor.b * factor),
                baseColor.a);
        }

        private static float SurfaceHeight(
            FurnitureSurfaceFamily family,
            float u,
            float v)
        {
            var noiseA = Mathf.PerlinNoise(
                u * FamilyScale(family) + 13.17f,
                v * FamilyScale(family) + 7.31f);
            var noiseB = HashNoise(u * 127f, v * 131f);

            switch (family)
            {
                case FurnitureSurfaceFamily.Wood:
                    var grain = 0.5f
                        + 0.5f * Mathf.Sin(
                            (v * 36f + noiseA * 2.5f) * Mathf.PI * 2f);
                    return Mathf.Clamp01(noiseA * 0.35f + grain * 0.65f);

                case FurnitureSurfaceFamily.Fabric:
                    var warp = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 128f);
                    var weft = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 128f);
                    return Mathf.Clamp01(warp * 0.45f + weft * 0.45f + noiseB * 0.1f);

                case FurnitureSurfaceFamily.Leather:
                    return Mathf.Clamp01(noiseA * 0.7f + noiseB * 0.3f);

                case FurnitureSurfaceFamily.Stone:
                    var vein = 0.5f
                        + 0.5f * Mathf.Sin(
                            (u * 7f + v * 11f + noiseA * 2f) * Mathf.PI);
                    return Mathf.Clamp01(noiseA * 0.7f + vein * 0.3f);

                case FurnitureSurfaceFamily.Metal:
                case FurnitureSurfaceFamily.Glass:
                case FurnitureSurfaceFamily.Paint:
                case FurnitureSurfaceFamily.Plastic:
                case FurnitureSurfaceFamily.Ceramic:
                    return Mathf.Lerp(0.45f, 0.55f, noiseA);

                default:
                    return Mathf.Clamp01(noiseA * 0.8f + noiseB * 0.2f);
            }
        }

        private static float FamilyScale(FurnitureSurfaceFamily family)
        {
            switch (family)
            {
                case FurnitureSurfaceFamily.Fabric:
                    return 18f;
                case FurnitureSurfaceFamily.Leather:
                    return 12f;
                case FurnitureSurfaceFamily.Wood:
                    return 5f;
                case FurnitureSurfaceFamily.Stone:
                    return 7f;
                default:
                    return 10f;
            }
        }

        private static float HashNoise(float x, float y)
        {
            var value = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private static void AssignTexture(
            Material material,
            FurnitureFinishChannel channel,
            Texture texture)
        {
            if (material == null || texture == null)
                return;
            foreach (var property in FurnitureFinishAssetUtility.Properties(channel))
            {
                if (!material.HasProperty(property))
                    continue;
                material.SetTexture(property, texture);
                break;
            }

            if (channel == FurnitureFinishChannel.NormalMap)
                material.EnableKeyword("_NORMALMAP");
            if (channel == FurnitureFinishChannel.OcclusionMap)
                material.EnableKeyword("_OCCLUSIONMAP");
            if (channel == FurnitureFinishChannel.MaskMap)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        private static void ApplyFamilyDefaults(
            Material material,
            FurnitureSurfaceFamily family,
            bool setColor)
        {
            if (material == null)
                return;

            var color = DefaultColor(family);
            if (setColor)
            {
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Metallic"))
                material.SetFloat(
                    "_Metallic",
                    family == FurnitureSurfaceFamily.Metal ? 0.85f : 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", DefaultSmoothness(family));
        }

        private static Color ChannelColor(
            FurnitureFinishChannel channel,
            FurnitureSurfaceFamily family)
        {
            switch (channel)
            {
                case FurnitureFinishChannel.NormalMap:
                    return new Color(0.5f, 0.5f, 1f, 1f);
                case FurnitureFinishChannel.MaskMap:
                    var metallic = family == FurnitureSurfaceFamily.Metal ? 0.85f : 0f;
                    return new Color(metallic, 1f, 0f, DefaultSmoothness(family));
                case FurnitureFinishChannel.OcclusionMap:
                    return Color.white;
                default:
                    return DefaultColor(family);
            }
        }

        private static Color DefaultColor(FurnitureSurfaceFamily family)
        {
            switch (family)
            {
                case FurnitureSurfaceFamily.Wood:
                    return new Color(0.42f, 0.27f, 0.15f, 1f);
                case FurnitureSurfaceFamily.Fabric:
                    return new Color(0.52f, 0.54f, 0.49f, 1f);
                case FurnitureSurfaceFamily.Leather:
                    return new Color(0.25f, 0.14f, 0.09f, 1f);
                case FurnitureSurfaceFamily.Metal:
                    return new Color(0.38f, 0.39f, 0.40f, 1f);
                case FurnitureSurfaceFamily.Stone:
                    return new Color(0.63f, 0.62f, 0.59f, 1f);
                case FurnitureSurfaceFamily.Glass:
                    return new Color(0.75f, 0.82f, 0.84f, 0.35f);
                case FurnitureSurfaceFamily.Paint:
                    return new Color(0.82f, 0.80f, 0.74f, 1f);
                case FurnitureSurfaceFamily.Ceramic:
                    return new Color(0.86f, 0.84f, 0.80f, 1f);
                default:
                    return new Color(0.48f, 0.48f, 0.48f, 1f);
            }
        }

        private static float DefaultSmoothness(FurnitureSurfaceFamily family)
        {
            switch (family)
            {
                case FurnitureSurfaceFamily.Glass:
                    return 0.9f;
                case FurnitureSurfaceFamily.Metal:
                    return 0.65f;
                case FurnitureSurfaceFamily.Ceramic:
                    return 0.6f;
                case FurnitureSurfaceFamily.Paint:
                    return 0.45f;
                case FurnitureSurfaceFamily.Fabric:
                    return 0.2f;
                case FurnitureSurfaceFamily.Wood:
                    return 0.3f;
                default:
                    return 0.35f;
            }
        }

        private static FurnitureSurfaceFamily ResolveFamily(
            FurnitureFinishProfile.ZoneDefinition zone)
        {
            if (zone.Family != FurnitureSurfaceFamily.Unknown)
                return zone.Family;
            foreach (var family in zone.CompatibleFamilies)
            {
                if (family != FurnitureSurfaceFamily.Unknown)
                    return family;
            }
            return FurnitureSurfaceFamily.Unknown;
        }

        private static void RegisterFinish(
            FurnitureFinishLibrary library,
            FurnitureFinishDefinition finish)
        {
            var entries = new List<FurnitureFinishDefinition>(library.Finishes);
            if (!entries.Contains(finish))
                entries.Add(finish);
            library.EditorConfigure(entries.ToArray());
        }
    }
}