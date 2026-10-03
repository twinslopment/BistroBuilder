using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicResolvedMaterialSemantic
    {
        internal SavicResolvedMaterialSemantic(
            string semantic,
            string confidence,
            float score,
            string evidence,
            string source)
        {
            Semantic =
                string.IsNullOrWhiteSpace(semantic)
                    ? "Unknown"
                    : semantic;

            Confidence =
                string.IsNullOrWhiteSpace(confidence)
                    ? "UNKNOWN"
                    : confidence;

            Score = score;
            Evidence = evidence ?? string.Empty;
            Source = source ?? string.Empty;
        }

        internal string Semantic { get; }
        internal string Confidence { get; }
        internal float Score { get; }
        internal string Evidence { get; }
        internal string Source { get; }
        internal bool IsKnown =>
            !string.Equals(
                Semantic,
                "Unknown",
                StringComparison.Ordinal);
    }

    internal static class SavicMaterialSemanticResolver
    {
        internal const string Version = "1.0.0";

        private static readonly Dictionary<string, string> SourceTokenSemantics =
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
                { "metal", "Metal" },
                { "steel", "Metal" },
                { "iron", "Metal" },
                { "brass", "Metal" },
                { "chrome", "Metal" },
                { "aluminium", "Metal" },
                { "aluminum", "Metal" },
                { "copper", "Metal" },
                { "glass", "Glass" },
                { "marble", "Stone" },
                { "granite", "Stone" },
                { "stone", "Stone" },
                { "ceramic", "Ceramic" },
                { "concrete", "Concrete" },
                { "plastic", "Plastic" },
                { "acrylic", "Plastic" },
                { "fabric", "Fabric" },
                { "linen", "Fabric" },
                { "velvet", "Fabric" },
                { "leather", "Fabric" }
            };

        internal static SavicResolvedMaterialSemantic Resolve(
            SavicManifest manifest)
        {
            if (manifest?.model3D == null)
            {
                return Unknown(
                    "No model analysis is available.");
            }

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (!string.IsNullOrWhiteSpace(
                    analysis.dominantMaterialSemantic) &&
                !string.Equals(
                    analysis.dominantMaterialSemantic,
                    "Unknown",
                    StringComparison.Ordinal))
            {
                return new SavicResolvedMaterialSemantic(
                    analysis.dominantMaterialSemantic,
                    analysis.dominantMaterialConfidence,
                    ResolveRawScore(analysis),
                    BuildRawEvidence(analysis),
                    "MATERIAL_DATA");
            }

            if (manifest.classification == null ||
                !string.Equals(
                    manifest.classification.family,
                    "Furniture",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.classification.type,
                    "Table",
                    StringComparison.Ordinal) ||
                !manifest.classification.geometryBacked)
            {
                return Unknown(
                    "No reliable material semantic exists and source-name fallback is restricted to geometry-backed furniture classification.");
            }

            string sourceName =
                Path.GetFileNameWithoutExtension(
                    manifest.source?.originalFileName ??
                    string.Empty);

            HashSet<string> tokens =
                Tokenize(
                    sourceName);

            Dictionary<string, int> semanticMatches =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string token in tokens)
            {
                if (!SourceTokenSemantics.TryGetValue(
                        token,
                        out string semantic))
                {
                    continue;
                }

                semanticMatches.TryGetValue(
                    semantic,
                    out int count);

                semanticMatches[semantic] =
                    count + 1;
            }

            if (semanticMatches.Count != 1)
            {
                return Unknown(
                    semanticMatches.Count == 0
                        ? "No explicit material token is present in source metadata."
                        : "Source metadata contains conflicting material semantics.");
            }

            foreach (
                KeyValuePair<string, int> pair in semanticMatches)
            {
                string appearanceContext =
                    string.Equals(
                        analysis.appearanceDataCompleteness,
                        "NONE",
                        StringComparison.Ordinal)
                        ? "the source carries no texture, vertex-color or non-default base-color appearance data"
                        : "raw material appearance exists but contains no conservative semantic label";

                return new SavicResolvedMaterialSemantic(
                    pair.Key,
                    "MEDIUM",
                    pair.Value >= 2
                        ? 0.78f
                        : 0.72f,
                    pair.Value +
                    " explicit source material token(s); " +
                    "object type was independently confirmed as Table by geometry-backed classification; " +
                    appearanceContext +
                    ".",
                    "SOURCE_METADATA_FALLBACK");
            }

            return Unknown(
                "No material semantic could be resolved.");
        }

        private static float ResolveRawScore(
            SavicModelAnalysisRecord analysis)
        {
            if (analysis?.materials == null)
                return 0f;

            float best =
                0f;

            for (int index = 0;
                 index < analysis.materials.Count;
                 index++)
            {
                SavicMaterialAnalysisRecord material =
                    analysis.materials[index];

                if (material == null ||
                    !string.Equals(
                        material.semantic,
                        analysis.dominantMaterialSemantic,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                best =
                    Math.Max(
                        best,
                        material.semanticScore);
            }

            return best;
        }

        private static string BuildRawEvidence(
            SavicModelAnalysisRecord analysis)
        {
            if (analysis?.materials == null)
                return "Material semantic inferred directly from raw material data.";

            for (int index = 0;
                 index < analysis.materials.Count;
                 index++)
            {
                SavicMaterialAnalysisRecord material =
                    analysis.materials[index];

                if (material != null &&
                    string.Equals(
                        material.semantic,
                        analysis.dominantMaterialSemantic,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return material.evidence ??
                           "Material semantic inferred directly from raw material data.";
                }
            }

            return "Material semantic inferred directly from raw material data.";
        }

        private static SavicResolvedMaterialSemantic Unknown(
            string evidence)
        {
            return new SavicResolvedMaterialSemantic(
                "Unknown",
                "UNKNOWN",
                0f,
                evidence,
                "NONE");
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
