using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicMaterialSemanticAnalyzer
    {
        internal const string Version = "1.0.0";

        private static readonly Dictionary<string, string> TokenSemantics =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "wood", "Wood" },
                { "wooden", "Wood" },
                { "oak", "Wood" },
                { "walnut", "Wood" },
                { "pine", "Wood" },
                { "mahogany", "Wood" },
                { "birch", "Wood" },
                { "beech", "Wood" },
                { "teak", "Wood" },
                { "veneer", "Wood" },
                { "metal", "Metal" },
                { "steel", "Metal" },
                { "iron", "Metal" },
                { "brass", "Metal" },
                { "chrome", "Metal" },
                { "aluminium", "Metal" },
                { "aluminum", "Metal" },
                { "copper", "Metal" },
                { "fabric", "Fabric" },
                { "cloth", "Fabric" },
                { "linen", "Fabric" },
                { "velvet", "Fabric" },
                { "leather", "Fabric" },
                { "glass", "Glass" },
                { "crystal", "Glass" },
                { "marble", "Stone" },
                { "granite", "Stone" },
                { "stone", "Stone" },
                { "terrazzo", "Stone" },
                { "ceramic", "Ceramic" },
                { "porcelain", "Ceramic" },
                { "concrete", "Concrete" },
                { "cement", "Concrete" },
                { "plastic", "Plastic" },
                { "polymer", "Plastic" },
                { "acrylic", "Plastic" }
            };

        internal static List<SavicMaterialAnalysisRecord> Analyze(
            GameObject root,
            out string dominantSemantic,
            out string dominantConfidence)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);

            HashSet<Material> unique =
                new HashSet<Material>();

            List<SavicMaterialAnalysisRecord> records =
                new List<SavicMaterialAnalysisRecord>();

            Dictionary<string, int> semanticVotes =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, float> semanticBestConfidence =
                new Dictionary<string, float>(
                    StringComparer.OrdinalIgnoreCase);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Material[] materials =
                    renderers[rendererIndex].sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material =
                        materials[materialIndex];

                    if (material == null ||
                        !unique.Add(material))
                    {
                        continue;
                    }

                    SavicMaterialAnalysisRecord record =
                        AnalyzeMaterial(material);

                    records.Add(record);

                    if (string.Equals(
                            record.semantic,
                            "Unknown",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    semanticVotes.TryGetValue(
                        record.semantic,
                        out int votes);

                    semanticVotes[record.semantic] =
                        votes + 1;

                    semanticBestConfidence.TryGetValue(
                        record.semantic,
                        out float currentBest);

                    semanticBestConfidence[record.semantic] =
                        Math.Max(
                            currentBest,
                            record.semanticScore);
                }
            }

            ResolveDominantSemantic(
                semanticVotes,
                semanticBestConfidence,
                out dominantSemantic,
                out dominantConfidence);

            records.Sort(
                (left, right) =>
                    string.Compare(
                        left.materialName,
                        right.materialName,
                        StringComparison.OrdinalIgnoreCase));

            return records;
        }

        private static SavicMaterialAnalysisRecord AnalyzeMaterial(
            Material material)
        {
            string[] texturePropertyNames =
                material.GetTexturePropertyNames();

            List<string> textureNames =
                new List<string>();

            StringBuilder semanticInput =
                new StringBuilder();

            AppendTokens(
                semanticInput,
                material.name);

            for (int index = 0;
                 index < texturePropertyNames.Length;
                 index++)
            {
                Texture texture =
                    material.GetTexture(
                        texturePropertyNames[index]);

                if (texture == null)
                    continue;

                string textureName =
                    texture.name ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(textureName))
                    textureNames.Add(textureName);

                AppendTokens(
                    semanticInput,
                    textureName);
            }

            float metallic =
                ReadFloat(
                    material,
                    "_Metallic",
                    0f);

            float smoothness =
                material.HasProperty("_Smoothness")
                    ? ReadFloat(material, "_Smoothness", 0f)
                    : ReadFloat(material, "_Glossiness", 0f);

            Color baseColor =
                material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;

            bool transparent =
                IsTransparent(
                    material,
                    baseColor);

            ResolveSemantic(
                semanticInput.ToString(),
                metallic,
                transparent,
                out string semantic,
                out float semanticScore,
                out string confidence,
                out string evidence);

            return new SavicMaterialAnalysisRecord
            {
                materialName =
                    material.name ?? string.Empty,
                shaderName =
                    material.shader == null
                        ? string.Empty
                        : material.shader.name,
                semantic =
                    semantic,
                semanticConfidence =
                    confidence,
                semanticScore =
                    semanticScore,
                metallic =
                    Mathf.Clamp01(metallic),
                smoothness =
                    Mathf.Clamp01(smoothness),
                baseColorR =
                    baseColor.r,
                baseColorG =
                    baseColor.g,
                baseColorB =
                    baseColor.b,
                baseColorA =
                    baseColor.a,
                transparent =
                    transparent,
                textureCount =
                    textureNames.Count,
                textureNames =
                    string.Join(
                        "; ",
                        textureNames),
                evidence =
                    evidence
            };
        }

        private static void ResolveSemantic(
            string semanticInput,
            float metallic,
            bool transparent,
            out string semantic,
            out float score,
            out string confidence,
            out string evidence)
        {
            HashSet<string> tokens =
                Tokenize(
                    semanticInput);

            Dictionary<string, int> matches =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string token in tokens)
            {
                if (!TokenSemantics.TryGetValue(
                        token,
                        out string candidate))
                {
                    continue;
                }

                matches.TryGetValue(
                    candidate,
                    out int count);

                matches[candidate] =
                    count + 1;
            }

            string tokenWinner =
                string.Empty;

            int tokenWinnerCount =
                0;

            foreach (
                KeyValuePair<string, int> pair in matches)
            {
                if (pair.Value >
                    tokenWinnerCount)
                {
                    tokenWinner =
                        pair.Key;

                    tokenWinnerCount =
                        pair.Value;
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    tokenWinner))
            {
                semantic =
                    tokenWinner;

                score =
                    tokenWinnerCount >= 2
                        ? 0.98f
                        : 0.92f;

                confidence =
                    "HIGH";

                evidence =
                    tokenWinnerCount +
                    " semantic token(s) in material or texture names.";

                return;
            }

            if (metallic >= 0.65f &&
                !transparent)
            {
                semantic =
                    "Metal";
                score =
                    0.72f;
                confidence =
                    "MEDIUM";
                evidence =
                    "Material metallic value is strongly metallic.";
                return;
            }

            if (transparent &&
                metallic < 0.35f)
            {
                semantic =
                    "Glass";
                score =
                    0.62f;
                confidence =
                    "LOW";
                evidence =
                    "Material transparency is compatible with glass but has no semantic naming evidence.";
                return;
            }

            semantic =
                "Unknown";
            score =
                0f;
            confidence =
                "UNKNOWN";
            evidence =
                "No conservative material semantic evidence.";
        }

        private static void ResolveDominantSemantic(
            IReadOnlyDictionary<string, int> votes,
            IReadOnlyDictionary<string, float> bestConfidence,
            out string semantic,
            out string confidence)
        {
            semantic =
                "Unknown";
            confidence =
                "UNKNOWN";

            int winnerVotes =
                0;

            float winnerScore =
                0f;

            foreach (
                KeyValuePair<string, int> pair in votes)
            {
                bestConfidence.TryGetValue(
                    pair.Key,
                    out float score);

                if (pair.Value > winnerVotes ||
                    (pair.Value == winnerVotes &&
                     score > winnerScore))
                {
                    semantic =
                        pair.Key;

                    winnerVotes =
                        pair.Value;

                    winnerScore =
                        score;
                }
            }

            if (winnerVotes <= 0)
                return;

            confidence =
                winnerScore >= 0.90f
                    ? "HIGH"
                    : winnerScore >= 0.70f
                        ? "MEDIUM"
                        : "LOW";
        }

        private static bool IsTransparent(
            Material material,
            Color baseColor)
        {
            if (baseColor.a < 0.98f)
                return true;

            if (material.renderQueue >= 3000)
                return true;

            if (material.HasProperty("_Surface") &&
                material.GetFloat("_Surface") > 0.5f)
            {
                return true;
            }

            string renderType =
                material.GetTag(
                    "RenderType",
                    false,
                    string.Empty);

            return renderType.IndexOf(
                       "Transparent",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float ReadFloat(
            Material material,
            string property,
            float fallback)
        {
            return material.HasProperty(property)
                ? material.GetFloat(property)
                : fallback;
        }

        private static void AppendTokens(
            StringBuilder builder,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(value);
        }

        private static HashSet<string> Tokenize(
            string raw)
        {
            HashSet<string> result =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            StringBuilder token =
                new StringBuilder();

            for (int index = 0;
                 index < raw.Length;
                 index++)
            {
                char value =
                    raw[index];

                if (char.IsLetter(value))
                {
                    token.Append(
                        char.ToLowerInvariant(value));
                }
                else
                {
                    FlushToken(
                        token,
                        result);
                }
            }

            FlushToken(
                token,
                result);

            return result;
        }

        private static void FlushToken(
            StringBuilder token,
            ISet<string> target)
        {
            if (token.Length == 0)
                return;

            target.Add(
                token.ToString());

            token.Clear();
        }
    }
}
