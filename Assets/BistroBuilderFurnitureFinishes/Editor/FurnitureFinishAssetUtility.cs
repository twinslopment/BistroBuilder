using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishAssetUtility
    {
        private const string RolePrefix = "ASBB_role_";

        public static string StableId(string raw, string fallback)
        {
            var source = (string.IsNullOrWhiteSpace(raw) ? fallback : raw)
                .Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            var previousUnderscore = false;
            foreach (var character in source.ToLowerInvariant())
            {
                var valid = (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9');
                if (valid)
                {
                    builder.Append(character);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore && builder.Length > 0)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            var result = builder.ToString().Trim('_');
            if (string.IsNullOrWhiteSpace(result))
                result = fallback;
            if (result.Length > 0 && char.IsDigit(result[0]))
                result = "f_" + result;
            return result;
        }

        public static string TransformPath(Transform root, Transform target)
        {
            if (root == target)
                return string.Empty;
            var parts = new Stack<string>();
            var cursor = target;
            while (cursor != null && cursor != root)
            {
                parts.Push(cursor.name);
                cursor = cursor.parent;
            }
            return string.Join("/", parts);
        }

        public static Renderer FindRenderer(GameObject root, string path)
        {
            if (root == null)
                return null;
            var transform = string.IsNullOrWhiteSpace(path)
                ? root.transform
                : root.transform.Find(path);
            return transform != null ? transform.GetComponent<Renderer>() : null;
        }

        public static string SemanticRole(Material material, Renderer renderer, int materialIndex)
        {
            if (material != null)
            {
                var name = material.name ?? string.Empty;
                if (name.StartsWith(RolePrefix, StringComparison.Ordinal))
                {
                    var role = name.Substring(RolePrefix.Length);
                    var separator = role.IndexOfAny(new[] { ' ', '.', '_', '-' });
                    return separator > 0 ? role.Substring(0, separator) : role;
                }
                if (!string.IsNullOrWhiteSpace(name))
                    return StableId(name, "surface");
            }
            return StableId($"{renderer.name}_slot_{materialIndex}", "surface");
        }

        public static FurnitureSurfaceFamily InferFamily(string semanticName, out bool confident)
        {
            var value = (semanticName ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(value, "fabric", "cloth", "textile", "upholstery", "cushion", "tela", "tapizado"))
                return Confirm(FurnitureSurfaceFamily.Fabric, out confident);
            if (ContainsAny(value, "wood", "timber", "walnut", "oak", "roble", "nogal", "madera"))
                return Confirm(FurnitureSurfaceFamily.Wood, out confident);
            if (ContainsAny(value, "metal", "steel", "iron", "brass", "aluminium", "aluminum", "acero", "laton"))
                return Confirm(FurnitureSurfaceFamily.Metal, out confident);
            if (ContainsAny(value, "leather", "cuero"))
                return Confirm(FurnitureSurfaceFamily.Leather, out confident);
            if (ContainsAny(value, "glass", "crystal", "vidrio", "cristal"))
                return Confirm(FurnitureSurfaceFamily.Glass, out confident);
            if (ContainsAny(value, "stone", "marble", "granite", "piedra", "marmol"))
                return Confirm(FurnitureSurfaceFamily.Stone, out confident);
            if (ContainsAny(value, "ceramic", "ceramica"))
                return Confirm(FurnitureSurfaceFamily.Ceramic, out confident);
            if (ContainsAny(value, "plastic", "plastico"))
                return Confirm(FurnitureSurfaceFamily.Plastic, out confident);
            if (ContainsAny(value, "paint", "lacquer", "pintura", "lacado"))
                return Confirm(FurnitureSurfaceFamily.Paint, out confident);

            confident = false;
            return FurnitureSurfaceFamily.Unknown;
        }

        public static bool IsPlaceholder(Material material)
        {
            if (material == null)
                return false;
            var name = (material.name ?? string.Empty).ToLowerInvariant();
            return ContainsAny(name, "placeholder", "missing", "temp_material", "temporary", "default-material", "defaultmaterial");
        }

        public static bool IsShaderBroken(Material material)
        {
            return material == null
                || material.shader == null
                || string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal);
        }

        public static Texture GetTexture(Material material, FurnitureFinishChannel channel)
        {
            if (material == null)
                return null;
            foreach (var property in Properties(channel))
            {
                if (material.HasProperty(property))
                    return material.GetTexture(property);
            }
            return null;
        }

        public static string[] Properties(FurnitureFinishChannel channel)
        {
            switch (channel)
            {
                case FurnitureFinishChannel.BaseColorMap:
                    return new[] { "_BaseMap", "_MainTex", "_BaseColorMap" };
                case FurnitureFinishChannel.NormalMap:
                    return new[] { "_BumpMap", "_NormalMap" };
                case FurnitureFinishChannel.MaskMap:
                    return new[] { "_MaskMap", "_MetallicGlossMap" };
                case FurnitureFinishChannel.OcclusionMap:
                    return new[] { "_OcclusionMap" };
                default:
                    return Array.Empty<string>();
            }
        }

        public static FurnitureFinishChannel DetectProvidedChannels(Material material)
        {
            var result = FurnitureFinishChannel.None;
            foreach (FurnitureFinishChannel channel in new[]
            {
                FurnitureFinishChannel.BaseColorMap,
                FurnitureFinishChannel.NormalMap,
                FurnitureFinishChannel.MaskMap,
                FurnitureFinishChannel.OcclusionMap
            })
            {
                if (GetTexture(material, channel) != null)
                    result |= channel;
            }
            return result;
        }

        public static string MaterialFingerprint(Material material)
        {
            if (material == null)
                return "<null>";

            var builder = new StringBuilder();
            builder.Append(material.shader != null ? material.shader.name : "<shader-null>");
            AppendColor(builder, material, "_BaseColor");
            AppendColor(builder, material, "_Color");
            AppendFloat(builder, material, "_Metallic");
            AppendFloat(builder, material, "_Smoothness");

            foreach (var channel in new[]
            {
                FurnitureFinishChannel.BaseColorMap,
                FurnitureFinishChannel.NormalMap,
                FurnitureFinishChannel.MaskMap,
                FurnitureFinishChannel.OcclusionMap
            })
            {
                var texture = GetTexture(material, channel);
                var path = texture != null ? AssetDatabase.GetAssetPath(texture) : string.Empty;
                var guid = !string.IsNullOrWhiteSpace(path)
                    ? AssetDatabase.AssetPathToGUID(path)
                    : string.Empty;
                builder.Append('|').Append(channel).Append('=').Append(guid);
            }

            return builder.ToString();
        }

        public static FurnitureSurfaceFamily InferFamily(
            Material material,
            string semanticHint,
            out bool confident)
        {
            var tokens = new StringBuilder(semanticHint ?? string.Empty);
            if (material != null)
            {
                tokens.Append(' ').Append(material.name);
                foreach (var channel in new[]
                {
                    FurnitureFinishChannel.BaseColorMap,
                    FurnitureFinishChannel.NormalMap,
                    FurnitureFinishChannel.MaskMap,
                    FurnitureFinishChannel.OcclusionMap
                })
                {
                    var texture = GetTexture(material, channel);
                    if (texture != null)
                        tokens.Append(' ').Append(texture.name);
                }
            }

            var inferred = InferFamily(tokens.ToString(), out confident);
            if (confident)
                return inferred;

            if (material != null
                && material.HasProperty("_Metallic")
                && material.GetFloat("_Metallic") >= 0.65f)
            {
                confident = true;
                return FurnitureSurfaceFamily.Metal;
            }

            confident = false;
            return FurnitureSurfaceFamily.Unknown;
        }

        public static void EnsureFolder(string assetFolder)
        {
            var normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;
            var parts = normalized.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void AppendColor(StringBuilder builder, Material material, string property)
        {
            if (material == null || !material.HasProperty(property))
                return;
            var color = material.GetColor(property);
            builder.Append('|').Append(property).Append('=')
                .Append(color.r.ToString("R")).Append(',')
                .Append(color.g.ToString("R")).Append(',')
                .Append(color.b.ToString("R")).Append(',')
                .Append(color.a.ToString("R"));
        }

        private static void AppendFloat(StringBuilder builder, Material material, string property)
        {
            if (material == null || !material.HasProperty(property))
                return;
            builder.Append('|').Append(property).Append('=')
                .Append(material.GetFloat(property).ToString("R"));
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (var term in terms)
            {
                if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static FurnitureSurfaceFamily Confirm(FurnitureSurfaceFamily family, out bool confident)
        {
            confident = true;
            return family;
        }
    }
}