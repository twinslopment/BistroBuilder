using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicContentClassifier
    {
        internal const string Version = "1.0.0";

        private static readonly HashSet<string> TableTokens =
            new HashSet<string>(
                new[]
                {
                    "table",
                    "tables",
                    "mesa",
                    "mesas"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> TableContextTokens =
            new HashSet<string>(
                new[]
                {
                    "bistro",
                    "dining",
                    "restaurant",
                    "wooden",
                    "wood",
                    "round",
                    "rectangular",
                    "square"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ConflictingFurnitureTokens =
            new HashSet<string>(
                new[]
                {
                    "chair",
                    "chairs",
                    "silla",
                    "sillas",
                    "stool",
                    "stools",
                    "taburete",
                    "taburetes",
                    "bench",
                    "benches",
                    "banco",
                    "bancos",
                    "sofa",
                    "sofas"
                },
                StringComparer.OrdinalIgnoreCase);

        internal static SavicClassificationRecord Classify(
            SavicManifest manifest)
        {
            if (manifest?.source == null)
                throw new ArgumentNullException(nameof(manifest));

            SavicClassificationRecord result =
                new SavicClassificationRecord
                {
                    classified = true,
                    classifierVersion = Version,
                    classifiedUtc = DateTime.UtcNow.ToString("O")
                };

            if (!string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.Model3D.ToString(),
                    StringComparison.Ordinal))
            {
                result.evidence =
                    "Source kind is not a 3D model.";
                return result;
            }

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (analysis == null ||
                !analysis.analyzed ||
                !analysis.hasUsableBounds)
            {
                result.evidence =
                    "3D analysis is incomplete or has unusable bounds.";
                return result;
            }

            string sourceName =
                Path.GetFileNameWithoutExtension(
                    manifest.source.originalFileName ?? string.Empty);

            HashSet<string> tokens =
                Tokenize(sourceName);

            float score = 0f;
            List<string> evidence =
                new List<string>(6);

            bool explicitTableToken =
                ContainsAny(tokens, TableTokens);

            bool conflictingToken =
                ContainsAny(tokens, ConflictingFurnitureTokens);

            if (explicitTableToken)
            {
                score += 0.62f;
                evidence.Add("name contains an explicit table token");
            }

            int contextMatches =
                CountMatches(tokens, TableContextTokens);

            if (contextMatches > 0)
            {
                float contextScore =
                    Math.Min(0.12f, contextMatches * 0.04f);

                score += contextScore;
                evidence.Add(
                    contextMatches +
                    " supporting furniture/table context token(s)");
            }

            if (HasPlausibleFurnitureDimensions(analysis))
            {
                score += 0.14f;
                evidence.Add("bounds are plausible for furniture");
            }

            if (HasPlausibleTableProportions(analysis))
            {
                score += 0.12f;
                evidence.Add("proportions are compatible with a table");
            }

            if (analysis.hasSkinnedMeshes)
            {
                score -= 0.20f;
                evidence.Add("skinned meshes reduce table confidence");
            }

            if (conflictingToken)
            {
                score -= 0.65f;
                evidence.Add("name contains a conflicting furniture token");
            }

            score = Clamp01(score);
            result.score = score;

            if (explicitTableToken &&
                !conflictingToken &&
                score >= 0.78f)
            {
                result.family = "Furniture";
                result.type = "Table";
                result.category = "Furniture";
                result.confidence =
                    score >= 0.92f
                        ? "HIGH"
                        : "MEDIUM";
            }
            else
            {
                result.family = "Unknown";
                result.type = "Unknown";
                result.category = "Unknown";
                result.confidence =
                    score >= 0.55f
                        ? "LOW"
                        : "UNKNOWN";
            }

            result.evidence =
                evidence.Count > 0
                    ? string.Join("; ", evidence)
                    : "No positive classification evidence.";

            return result;
        }

        private static bool HasPlausibleFurnitureDimensions(
            SavicModelAnalysisRecord analysis)
        {
            float width = analysis.widthMeters;
            float height = analysis.heightMeters;
            float depth = analysis.depthMeters;

            return width >= 0.15f &&
                   width <= 8f &&
                   height >= 0.15f &&
                   height <= 5f &&
                   depth >= 0.15f &&
                   depth <= 8f;
        }

        private static bool HasPlausibleTableProportions(
            SavicModelAnalysisRecord analysis)
        {
            float horizontalMax =
                Math.Max(
                    analysis.widthMeters,
                    analysis.depthMeters);

            float horizontalMin =
                Math.Min(
                    analysis.widthMeters,
                    analysis.depthMeters);

            float height =
                analysis.heightMeters;

            if (horizontalMin <= 0.0001f ||
                height <= 0.0001f)
            {
                return false;
            }

            float scaleToNominalHeight =
                0.75f / height;

            float normalizedMax =
                horizontalMax * scaleToNominalHeight;

            float normalizedMin =
                horizontalMin * scaleToNominalHeight;

            return normalizedMax >= 0.55f &&
                   normalizedMax <= 2.50f &&
                   normalizedMin >= 0.40f &&
                   normalizedMin <= 1.80f;
        }

        private static HashSet<string> Tokenize(string raw)
        {
            HashSet<string> tokens =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return tokens;

            StringBuilder current =
                new StringBuilder();

            for (int index = 0;
                 index < raw.Length;
                 index++)
            {
                char value = raw[index];

                if (char.IsLetter(value))
                {
                    current.Append(
                        char.ToLowerInvariant(value));

                    continue;
                }

                FlushToken(current, tokens);
            }

            FlushToken(current, tokens);
            return tokens;
        }

        private static void FlushToken(
            StringBuilder current,
            ISet<string> target)
        {
            if (current.Length == 0)
                return;

            target.Add(current.ToString());
            current.Clear();
        }

        private static bool ContainsAny(
            IEnumerable<string> source,
            ISet<string> candidates)
        {
            foreach (string value in source)
            {
                if (candidates.Contains(value))
                    return true;
            }

            return false;
        }

        private static int CountMatches(
            IEnumerable<string> source,
            ISet<string> candidates)
        {
            int count = 0;

            foreach (string value in source)
            {
                if (candidates.Contains(value))
                    count++;
            }

            return count;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            if (value >= 1f)
                return 1f;

            return value;
        }
    }
}
