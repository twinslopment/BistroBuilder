using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicGenericPlaceableAuthoringPlanner
    {
        internal const string Version = "1.0.0";

        private static readonly HashSet<string> FloorEvidence =
            new HashSet<string>(
                new[]
                {
                    "floor", "freestanding", "standing",
                    "plant", "planter", "pedestal",
                    "statue", "sculpture", "urn"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WallEvidence =
            new HashSet<string>(
                new[]
                {
                    "wall", "wallmounted", "mounted",
                    "picture", "painting", "frame",
                    "applique", "aplique", "sconce"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> CeilingEvidence =
            new HashSet<string>(
                new[]
                {
                    "ceiling", "pendant", "chandelier",
                    "hanging", "suspended"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SurfaceEvidence =
            new HashSet<string>(
                new[]
                {
                    "vase", "bottle", "candle",
                    "centerpiece", "centrepiece",
                    "tabletop", "countertop",
                    "desktop", "plate"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> FunctionalEquipment =
            new HashSet<string>(
                new[]
                {
                    "oven", "stove", "range", "grill",
                    "cooler", "fridge", "refrigerator",
                    "freezer", "dishwasher", "extractor",
                    "hood", "coffee", "machine",
                    "fryer", "sink", "tap", "faucet",
                    "counter", "bar", "pass", "register",
                    "pos", "cash"
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> PassiveEquipment =
            new HashSet<string>(
                new[]
                {
                    "cabinet", "cupboard", "shelf",
                    "shelving", "rack", "storage",
                    "locker", "stand"
                },
                StringComparer.OrdinalIgnoreCase);

        internal static bool TryPlan(
            SavicManifest manifest,
            out SavicGenericPlaceableAuthoringRecord plan,
            out string reasonCode,
            out string error)
        {
            plan =
                new SavicGenericPlaceableAuthoringRecord
                {
                    plannerVersion = Version
                };

            reasonCode =
                string.Empty;

            error =
                string.Empty;

            if (manifest?.source == null ||
                manifest.model3D == null ||
                manifest.classification == null)
            {
                reasonCode =
                    "GENERIC_INVALID_MANIFEST";

                error =
                    "Generic placeable planning requires source, analysis and classification.";

                return false;
            }

            EnsureCanonicalContentId(
                manifest);

            SavicModelAnalysisRecord analysis =
                manifest.model3D;

            if (!analysis.analyzed ||
                !analysis.hasUsableBounds)
            {
                reasonCode =
                    "GENERIC_GEOMETRY_UNUSABLE";

                error =
                    "Generic placeable has no usable analyzed bounds.";

                return false;
            }

            if (analysis.hasSkinnedMeshes)
            {
                reasonCode =
                    "GENERIC_SKINNED_UNSUPPORTED";

                error =
                    "Skinned meshes are not safe for generic static publication.";

                return false;
            }

            if (!HasSafeDimensions(
                    analysis))
            {
                reasonCode =
                    "GENERIC_DIMENSIONS_OUT_OF_RANGE";

                error =
                    "Generic placeable dimensions are outside the safe V1 automatic range.";

                return false;
            }

            string type =
                manifest.classification.type ??
                string.Empty;

            if (!string.Equals(
                    type,
                    "Decoration",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    type,
                    "KitchenEquipment",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    type,
                    "ServiceEquipment",
                    StringComparison.Ordinal))
            {
                reasonCode =
                    "GENERIC_TYPE_UNSUPPORTED";

                error =
                    "Classification is not a supported generic placeable type.";

                return false;
            }

            HashSet<string> tokens =
                Tokenize(
                    Path.GetFileNameWithoutExtension(
                        manifest.source.originalFileName ??
                        string.Empty));

            if (ContainsAny(
                    tokens,
                    WallEvidence))
            {
                reasonCode =
                    "PLACEMENT_WALL_REQUIRES_ADAPTER";

                error =
                    "Wall-mounted content requires the dedicated wall-placement adapter.";

                return false;
            }

            if (ContainsAny(
                    tokens,
                    CeilingEvidence))
            {
                reasonCode =
                    "PLACEMENT_CEILING_REQUIRES_ADAPTER";

                error =
                    "Ceiling-mounted content requires the dedicated ceiling-placement adapter.";

                return false;
            }

            if (ContainsAny(
                    tokens,
                    SurfaceEvidence))
            {
                reasonCode =
                    "PLACEMENT_SURFACE_REQUIRES_ADAPTER";

                error =
                    "Surface decoration requires the dedicated surface-placement adapter.";

                return false;
            }

            bool equipment =
                string.Equals(
                    type,
                    "KitchenEquipment",
                    StringComparison.Ordinal) ||
                string.Equals(
                    type,
                    "ServiceEquipment",
                    StringComparison.Ordinal);

            bool requiresFunctionalAdapter =
                equipment &&
                ContainsAny(
                    tokens,
                    FunctionalEquipment);

            if (requiresFunctionalAdapter)
            {
                plan.requiresFunctionalAdapter =
                    true;

                reasonCode =
                    "FUNCTIONAL_ADAPTER_REQUIRED";

                error =
                    "The asset is recognized as equipment, but its gameplay function requires a dedicated adapter before publication.";

                return false;
            }

            if (equipment &&
                !ContainsAny(
                    tokens,
                    PassiveEquipment))
            {
                plan.requiresFunctionalAdapter =
                    true;

                reasonCode =
                    "EQUIPMENT_FUNCTION_AMBIGUOUS";

                error =
                    "Equipment function is ambiguous; SAVIC will not publish it as passive decoration.";

                return false;
            }

            if (string.Equals(
                    type,
                    "Decoration",
                    StringComparison.Ordinal) &&
                !ContainsAny(
                    tokens,
                    FloorEvidence))
            {
                reasonCode =
                    "PLACEMENT_MODE_AMBIGUOUS";

                error =
                    "Decoration lacks reliable floor-placement evidence.";

                return false;
            }

            RestaurantPlaceableItemCategory category =
                ResolveCategory(
                    type);

            string contentFolder =
                "Assets/Generated/BistroBuilder/SAVIC/Published/Generic/" +
                manifest.canonicalContentId;

            plan.planned =
                true;

            plan.placementMode =
                "FLOOR";

            plan.category =
                category.ToString();

            plan.finalWidthMeters =
                analysis.widthMeters;

            plan.finalHeightMeters =
                analysis.heightMeters;

            plan.finalDepthMeters =
                analysis.depthMeters;

            plan.rotationStepDegrees =
                category ==
                RestaurantPlaceableItemCategory.Decoration
                    ? 15f
                    : 90f;

            plan.minimumClearanceMeters =
                category ==
                RestaurantPlaceableItemCategory.Decoration
                    ? 0.02f
                    : 0.05f;

            plan.suggestedPurchasePriceEuro =
                ResolvePrice(
                    category,
                    analysis);

            plan.prefabAssetPath =
                contentFolder +
                "/Placeable_" +
                manifest.canonicalContentId +
                ".prefab";

            plan.editableDefinitionAssetPath =
                contentFolder +
                "/Editable_" +
                manifest.canonicalContentId +
                ".asset";

            plan.itemDefinitionAssetPath =
                contentFolder +
                "/PlaceableItem_" +
                manifest.canonicalContentId +
                ".asset";

            plan.planReason =
                category ==
                RestaurantPlaceableItemCategory.Decoration
                    ? "High-confidence static floor decoration; generic non-interactive placeable is safe."
                    : "Passive floor equipment with no functional token; generic placeable publication is safe.";

            plan.plannedUtc =
                DateTime.UtcNow.ToString("O");

            return true;
        }

        internal static void EnsureCanonicalContentId(
            SavicManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (!string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                return;
            }

            string type =
                manifest.classification?.type ??
                "generic";

            string normalizedType =
                NormalizeToken(
                    type);

            string id =
                manifest.savicId ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    "Generic placeable requires a SAVIC identity.");
            }

            manifest.canonicalContentId =
                "bb_" +
                normalizedType +
                "_" +
                id.ToLowerInvariant();
        }

        private static bool HasSafeDimensions(
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
                width > 5.0f ||
                height > 5.0f ||
                depth > 5.0f)
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

            return substantialAxes >= 2;
        }

        private static RestaurantPlaceableItemCategory ResolveCategory(
            string type)
        {
            if (string.Equals(
                    type,
                    "KitchenEquipment",
                    StringComparison.Ordinal))
            {
                return RestaurantPlaceableItemCategory.KitchenEquipment;
            }

            if (string.Equals(
                    type,
                    "ServiceEquipment",
                    StringComparison.Ordinal))
            {
                return RestaurantPlaceableItemCategory.ServiceEquipment;
            }

            return RestaurantPlaceableItemCategory.Decoration;
        }

        private static int ResolvePrice(
            RestaurantPlaceableItemCategory category,
            SavicModelAnalysisRecord analysis)
        {
            float volume =
                Math.Max(
                    0.001f,
                    analysis.widthMeters *
                    analysis.heightMeters *
                    analysis.depthMeters);

            switch (category)
            {
                case RestaurantPlaceableItemCategory.KitchenEquipment:
                    return Math.Max(
                        250,
                        (int)Math.Round(
                            550f +
                            volume * 250f));

                case RestaurantPlaceableItemCategory.ServiceEquipment:
                    return Math.Max(
                        180,
                        (int)Math.Round(
                            350f +
                            volume * 180f));

                default:
                    return Math.Max(
                        25,
                        (int)Math.Round(
                            55f +
                            volume * 65f));
            }
        }

        private static HashSet<string> Tokenize(
            string value)
        {
            HashSet<string> tokens =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(value))
                return tokens;

            StringBuilder current =
                new StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char character =
                    value[index];

                if (char.IsLetterOrDigit(
                        character))
                {
                    current.Append(
                        char.ToLowerInvariant(
                            character));

                    continue;
                }

                FlushToken(
                    current,
                    tokens);
            }

            FlushToken(
                current,
                tokens);

            return tokens;
        }

        private static void FlushToken(
            StringBuilder current,
            ISet<string> tokens)
        {
            if (current.Length == 0)
                return;

            tokens.Add(
                current.ToString());

            current.Length = 0;
        }

        private static bool ContainsAny(
            ISet<string> tokens,
            IEnumerable<string> candidates)
        {
            foreach (string candidate in candidates)
            {
                if (tokens.Contains(candidate))
                    return true;
            }

            return false;
        }

        private static string NormalizeToken(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "generic";

            StringBuilder builder =
                new StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char character =
                    char.ToLowerInvariant(
                        value[index]);

                if (char.IsLetterOrDigit(
                        character))
                {
                    builder.Append(
                        character);
                }
                else if (builder.Length > 0 &&
                         builder[
                             builder.Length - 1] !=
                         '_')
                {
                    builder.Append('_');
                }
            }

            return builder
                .ToString()
                .Trim('_');
        }
    }
}
