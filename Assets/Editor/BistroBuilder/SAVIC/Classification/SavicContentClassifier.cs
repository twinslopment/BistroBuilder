using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicContentClassifier
    {
        internal const string Version = "4.1.0";

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

        private static readonly HashSet<string> ChairTokens =
            new HashSet<string>(
                new[]
                {
                    "chair",
                    "chairs",
                    "silla",
                    "sillas",
                    "armchair",
                    "armchairs"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ChairContextTokens =
            new HashSet<string>(
                new[]
                {
                    "bistro",
                    "dining",
                    "restaurant",
                    "wooden",
                    "wood",
                    "metal",
                    "upholstered",
                    "contemporary",
                    "nordic"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ChairConflictingTokens =
            new HashSet<string>(
                new[]
                {
                    "table",
                    "tables",
                    "mesa",
                    "mesas",
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

        private static readonly HashSet<string> DecorationTokens =
            new HashSet<string>(
                new[]
                {
                    "decor", "decoration", "decorative", "deco",
                    "mirror", "plant", "planter", "pedestal",
                    "sculpture", "statue", "ornament", "ornamental"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> KitchenEquipmentStrongTokens =
            new HashSet<string>(
                new[]
                {
                    "oven", "stove", "range", "grill",
                    "cooler", "fridge", "refrigerator",
                    "freezer", "dishwasher", "extractor",
                    "hood", "sink", "fryer"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> KitchenEquipmentContextTokens =
            new HashSet<string>(
                new[]
                {
                    "kitchen", "commercial", "cabinet",
                    "equipment", "storage", "rack", "shelf"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ServiceEquipmentStrongTokens =
            new HashSet<string>(
                new[]
                {
                    "counter", "register", "pos", "cash",
                    "pass"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ServiceEquipmentContextTokens =
            new HashSet<string>(
                new[]
                {
                    "bar", "service", "station", "cabinet",
                    "storage", "rack", "shelf"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> GenericPlaceableConflicts =
            new HashSet<string>(
                new[]
                {
                    "chair", "chairs", "silla", "sillas",
                    "stool", "stools", "taburete", "taburetes",
                    "bench", "benches", "banco", "bancos",
                    "sofa", "sofas", "table", "tables",
                    "mesa", "mesas"
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

            if (TryClassifyChair(
                    tokens,
                    analysis,
                    result))
            {
                return result;
            }

            if (TryClassifyGenericPlaceable(
                    tokens,
                    analysis,
                    result))
            {
                return result;
            }

            float score = 0f;
            List<string> evidence =
                new List<string>(6);

            bool explicitTableToken =
                ContainsAny(tokens, TableTokens);

            bool conflictingToken =
                ContainsAny(tokens, ConflictingFurnitureTokens);

            if (explicitTableToken)
            {
                score += 0.40f;
                evidence.Add("name contains an explicit table token");
            }

            int contextMatches =
                CountMatches(tokens, TableContextTokens);

            if (contextMatches > 0)
            {
                float contextScore =
                    Math.Min(0.08f, contextMatches * 0.03f);

                score += contextScore;
                evidence.Add(
                    contextMatches +
                    " supporting furniture/table context token(s)");
            }

            bool plausibleFurnitureDimensions =
                HasPlausibleFurnitureDimensions(analysis);

            if (plausibleFurnitureDimensions)
            {
                score += 0.10f;
                evidence.Add("bounds are plausible for furniture");
            }

            bool plausibleTableProportions =
                HasPlausibleTableProportions(analysis);

            if (plausibleTableProportions)
            {
                score += 0.14f;
                evidence.Add("proportions are compatible with a table");
            }

            bool strongGeometry =
                HasStrongTableGeometry(
                    analysis.geometry);

            bool moderateGeometry =
                !strongGeometry &&
                HasModerateTableGeometry(
                    analysis.geometry);

            if (strongGeometry)
            {
                score += 0.55f;
                evidence.Add(
                    "mesh surface distribution strongly matches a tabletop-over-support structure");
            }
            else if (moderateGeometry)
            {
                score += 0.22f;
                evidence.Add(
                    "mesh surface distribution moderately supports a table profile");
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

            bool nameBackedTable =
                explicitTableToken &&
                plausibleTableProportions &&
                score >= 0.62f;

            bool geometryBackedTable =
                strongGeometry &&
                plausibleFurnitureDimensions &&
                plausibleTableProportions &&
                score >= 0.76f;

            result.explicitTypeToken =
                explicitTableToken;

            result.nameBacked =
                nameBackedTable &&
                !conflictingToken;

            result.geometryBacked =
                geometryBackedTable &&
                !conflictingToken;

            if (!conflictingToken &&
                (nameBackedTable ||
                 geometryBackedTable))
            {
                result.family = "Furniture";
                result.type = "Table";
                result.category = "Furniture";
                result.confidence =
                    geometryBackedTable &&
                    score >= 0.90f
                        ? "HIGH"
                        : score >= 0.78f
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

        private static bool TryClassifyGenericPlaceable(
            ISet<string> tokens,
            SavicModelAnalysisRecord analysis,
            SavicClassificationRecord result)
        {
            if (tokens == null ||
                analysis == null ||
                result == null ||
                ContainsAny(
                    tokens,
                    GenericPlaceableConflicts))
            {
                return false;
            }

            bool plausibleDimensions =
                HasPlausibleGenericPlaceableDimensions(
                    analysis);

            if (!plausibleDimensions)
                return false;

            bool explicitDecoration =
                ContainsAny(
                    tokens,
                    DecorationTokens);

            bool framedFloorDecoration =
                tokens.Contains("framed") &&
                tokens.Contains("floor");

            bool decoration =
                explicitDecoration ||
                framedFloorDecoration;

            bool kitchenStrong =
                ContainsAny(
                    tokens,
                    KitchenEquipmentStrongTokens);

            int kitchenContext =
                CountMatches(
                    tokens,
                    KitchenEquipmentContextTokens);

            bool kitchen =
                kitchenStrong ||
                kitchenContext >= 2;

            bool serviceStrong =
                ContainsAny(
                    tokens,
                    ServiceEquipmentStrongTokens);

            int serviceContext =
                CountMatches(
                    tokens,
                    ServiceEquipmentContextTokens);

            bool service =
                serviceStrong ||
                serviceContext >= 2;

            string type =
                string.Empty;

            string category =
                string.Empty;

            float score =
                0f;

            List<string> evidence =
                new List<string>(6);

            if (kitchen)
            {
                type =
                    "KitchenEquipment";

                category =
                    "KitchenEquipment";

                score +=
                    kitchenStrong
                        ? 0.78f
                        : 0.66f;

                evidence.Add(
                    kitchenStrong
                        ? "name contains explicit kitchen-equipment token"
                        : kitchenContext +
                          " kitchen-equipment context tokens");
            }
            else if (service)
            {
                type =
                    "ServiceEquipment";

                category =
                    "ServiceEquipment";

                score +=
                    serviceStrong
                        ? 0.76f
                        : 0.64f;

                evidence.Add(
                    serviceStrong
                        ? "name contains explicit service-equipment token"
                        : serviceContext +
                          " service-equipment context tokens");
            }
            else if (decoration)
            {
                type =
                    "Decoration";

                category =
                    "Decoration";

                score +=
                    framedFloorDecoration
                        ? 0.82f
                        : 0.74f;

                evidence.Add(
                    framedFloorDecoration
                        ? "name contains framed + floor evidence consistent with a freestanding decorative object"
                        : "name contains explicit decoration token");
            }
            else
            {
                return false;
            }

            if (plausibleDimensions)
            {
                score +=
                    0.10f;

                evidence.Add(
                    "bounds are plausible for a static placeable");
            }

            if (analysis.hasSkinnedMeshes)
            {
                score -=
                    0.20f;

                evidence.Add(
                    "skinned meshes reduce generic static confidence");
            }

            score =
                Clamp01(
                    score);

            if (score < 0.62f)
                return false;

            result.family =
                "Placeable";

            result.type =
                type;

            result.category =
                category;

            result.score =
                score;

            result.explicitTypeToken =
                true;

            result.nameBacked =
                true;

            result.geometryBacked =
                false;

            result.confidence =
                score >= 0.80f
                    ? "HIGH"
                    : "MEDIUM";

            result.evidence =
                string.Join(
                    "; ",
                    evidence);

            return true;
        }

        private static bool TryClassifyChair(
            ISet<string> tokens,
            SavicModelAnalysisRecord analysis,
            SavicClassificationRecord result)
        {
            bool explicitChairToken =
                ContainsAny(
                    tokens,
                    ChairTokens);

            bool conflictingToken =
                ContainsAny(
                    tokens,
                    ChairConflictingTokens);

            float score = 0f;

            List<string> evidence =
                new List<string>(8);

            if (explicitChairToken)
            {
                score += 0.40f;
                evidence.Add(
                    "name contains an explicit chair token");
            }

            int contextMatches =
                CountMatches(
                    tokens,
                    ChairContextTokens);

            if (contextMatches > 0)
            {
                float contextScore =
                    Math.Min(
                        0.08f,
                        contextMatches *
                        0.03f);

                score +=
                    contextScore;

                evidence.Add(
                    contextMatches +
                    " supporting furniture/chair context token(s)");
            }

            bool plausibleFurnitureDimensions =
                HasPlausibleFurnitureDimensions(
                    analysis);

            if (plausibleFurnitureDimensions)
            {
                score += 0.08f;
                evidence.Add(
                    "absolute bounds are plausible for furniture");
            }

            bool plausibleChairShapeRatios =
                HasPlausibleChairShapeRatios(
                    analysis);

            if (plausibleChairShapeRatios)
            {
                score += 0.16f;
                evidence.Add(
                    "scale-independent proportions are compatible with a chair");
            }

            SavicChairGeometryProfileRecord chairGeometry =
                analysis.chairGeometry;

            bool strongChairGeometry =
                chairGeometry != null &&
                chairGeometry.analyzed &&
                chairGeometry.usable &&
                chairGeometry.confidenceScore >= 0.78f &&
                chairGeometry.backEdgeBias >= 0.20f;

            bool moderateChairGeometry =
                !strongChairGeometry &&
                chairGeometry != null &&
                chairGeometry.analyzed &&
                chairGeometry.usable &&
                chairGeometry.confidenceScore >= 0.58f;

            if (strongChairGeometry)
            {
                score +=
                    0.58f *
                    Mathf.Clamp01(
                        chairGeometry.confidenceScore);

                evidence.Add(
                    "mesh geometry strongly matches seat + backrest + lower-support chair structure");

                evidence.Add(
                    chairGeometry.evidence);
            }
            else if (moderateChairGeometry)
            {
                score +=
                    0.24f *
                    Mathf.Clamp01(
                        chairGeometry.confidenceScore);

                evidence.Add(
                    "mesh geometry moderately supports a chair profile");

                evidence.Add(
                    chairGeometry.evidence);
            }

            if (analysis.hasSkinnedMeshes)
            {
                score -= 0.15f;
                evidence.Add(
                    "skinned meshes reduce chair confidence");
            }

            if (conflictingToken)
            {
                score -= 0.65f;
                evidence.Add(
                    "name contains a conflicting furniture token");
            }

            score =
                Clamp01(score);

            bool nameBackedChair =
                explicitChairToken &&
                plausibleChairShapeRatios &&
                score >= 0.56f;

            bool geometryBackedChair =
                strongChairGeometry &&
                plausibleChairShapeRatios &&
                score >= 0.74f;

            if (conflictingToken ||
                (!nameBackedChair &&
                 !geometryBackedChair))
            {
                return false;
            }

            result.family =
                "Furniture";

            result.type =
                "Chair";

            result.category =
                "Seating";

            result.score =
                score;

            result.explicitTypeToken =
                explicitChairToken;

            result.nameBacked =
                nameBackedChair;

            result.geometryBacked =
                geometryBackedChair;

            result.confidence =
                geometryBackedChair &&
                score >= 0.86f
                    ? "HIGH"
                    : score >= 0.74f
                        ? "HIGH"
                        : "MEDIUM";

            result.evidence =
                evidence.Count > 0
                    ? string.Join(
                        "; ",
                        evidence)
                    : "No positive chair classification evidence.";

            return true;
        }

        private static bool HasPlausibleChairShapeRatios(
            SavicModelAnalysisRecord analysis)
        {
            if (analysis == null)
                return false;

            float width =
                analysis.widthMeters;

            float height =
                analysis.heightMeters;

            float depth =
                analysis.depthMeters;

            if (width <= 0.0001f ||
                height <= 0.0001f ||
                depth <= 0.0001f)
            {
                return false;
            }

            float widthToHeight =
                width /
                height;

            float depthToHeight =
                depth /
                height;

            float horizontalAspect =
                Math.Max(
                    width,
                    depth) /
                Math.Max(
                    0.0001f,
                    Math.Min(
                        width,
                        depth));

            // Classification answers "what is this?", not "is it already
            // authored at production scale?". Absolute scale belongs to the
            // authoring planner and quality gate.
            return
                widthToHeight >= 0.22f &&
                widthToHeight <= 1.15f &&
                depthToHeight >= 0.22f &&
                depthToHeight <= 1.20f &&
                horizontalAspect <= 2.20f;
        }

        private static bool HasPlausibleGenericPlaceableDimensions(
            SavicModelAnalysisRecord analysis)
        {
            if (analysis == null)
                return false;

            float width =
                analysis.widthMeters;

            float height =
                analysis.heightMeters;

            float depth =
                analysis.depthMeters;

            if (width < 0.01f ||
                height < 0.01f ||
                depth < 0.01f ||
                width > 5f ||
                height > 5f ||
                depth > 5f)
            {
                return false;
            }

            int substantialAxes = 0;

            if (width >= 0.10f)
                substantialAxes++;

            if (height >= 0.10f)
                substantialAxes++;

            if (depth >= 0.10f)
                substantialAxes++;

            // Thin placeables (for example framed floor mirrors)
            // are legitimate provided two dimensions are substantial.
            return substantialAxes >= 2;
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

        private static bool HasStrongTableGeometry(
            SavicGeometryProfileRecord geometry)
        {
            if (geometry == null ||
                !geometry.analyzed ||
                !geometry.usable)
            {
                return false;
            }

            return geometry.upwardFacingAreaRatio >= 0.23f &&
                   geometry.upperBandAreaRatio >= 0.58f &&
                   geometry.lowerBandAreaRatio >= 0.05f &&
                   geometry.lowerBandAreaRatio <= 0.38f &&
                   geometry.surfaceAreaCentroidHeight01 >= 0.62f &&
                   geometry.upperUpwardProjectedCoverage >= 0.55f &&
                   geometry.verticalAreaRatio <= 0.55f;
        }

        private static bool HasModerateTableGeometry(
            SavicGeometryProfileRecord geometry)
        {
            if (geometry == null ||
                !geometry.analyzed ||
                !geometry.usable)
            {
                return false;
            }

            return geometry.upwardFacingAreaRatio >= 0.16f &&
                   geometry.upperBandAreaRatio >= 0.45f &&
                   geometry.lowerBandAreaRatio <= 0.48f &&
                   geometry.surfaceAreaCentroidHeight01 >= 0.55f &&
                   geometry.upperUpwardProjectedCoverage >= 0.35f;
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
