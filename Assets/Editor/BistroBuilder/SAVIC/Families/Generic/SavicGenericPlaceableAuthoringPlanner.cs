using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicGenericPlaceableAuthoringPlanner
    {
        internal const string Version = "2.0.0";

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

        internal static bool IsHighConfidenceStaticGenericCandidate(
            string sourceFileName)
        {
            HashSet<string> tokens =
                Tokenize(
                    Path.GetFileNameWithoutExtension(
                        sourceFileName ??
                        string.Empty));

            if (tokens.Count == 0 ||
                ContainsAny(
                    tokens,
                    new[]
                    {
                        "chair", "chairs", "silla", "sillas",
                        "stool", "stools", "taburete", "taburetes",
                        "bench", "benches", "banco", "bancos",
                        "table", "tables", "mesa", "mesas"
                    }) ||
                ContainsAny(
                    tokens,
                    WallEvidence) ||
                ContainsAny(
                    tokens,
                    CeilingEvidence) ||
                ContainsAny(
                    tokens,
                    SurfaceEvidence))
            {
                return false;
            }

            bool floorDecoration =
                (tokens.Contains("mirror") ||
                 tokens.Contains("plant") ||
                 tokens.Contains("planter") ||
                 tokens.Contains("pedestal") ||
                 tokens.Contains("sculpture") ||
                 tokens.Contains("statue") ||
                 tokens.Contains("ornament") ||
                 tokens.Contains("ornamental") ||
                 tokens.Contains("decor") ||
                 tokens.Contains("decoration") ||
                 tokens.Contains("decorative") ||
                 tokens.Contains("deco") ||
                 (tokens.Contains("framed") &&
                  tokens.Contains("floor"))) &&
                ContainsAny(
                    tokens,
                    FloorEvidence);

            SavicEquipmentIntegrationDecision
                equipmentDecision =
                    SavicEquipmentIntegrationPolicy.Resolve(
                        sourceFileName);

            return floorDecoration ||
                   equipmentDecision.IsPassive;
        }

        internal static bool TryResolvePreImportReview(
            string sourceFileName,
            out string type,
            out string category,
            out string reasonCode,
            out string message)
        {
            type = string.Empty;
            category = string.Empty;
            reasonCode = string.Empty;
            message = string.Empty;

            HashSet<string> tokens =
                Tokenize(
                    Path.GetFileNameWithoutExtension(
                        sourceFileName ??
                        string.Empty));

            if (tokens.Count == 0 ||
                ContainsAny(
                    tokens,
                    new[]
                    {
                        "chair", "chairs", "silla", "sillas",
                        "stool", "stools", "taburete", "taburetes",
                        "bench", "benches", "banco", "bancos",
                        "table", "tables", "mesa", "mesas"
                    }))
            {
                return false;
            }

            SavicEquipmentIntegrationDecision
                equipmentDecision =
                    SavicEquipmentIntegrationPolicy.Resolve(
                        sourceFileName);

            if (equipmentDecision.RequiresGameplayAdapter)
            {
                type =
                    equipmentDecision.TypeId;

                category =
                    equipmentDecision.TypeId;

                reasonCode =
                    equipmentDecision.ReasonCode;

                message =
                    equipmentDecision.Evidence +
                    " Expensive 3D import/analysis is intentionally skipped until the approved adapter exists.";

                return true;
            }

            bool decoration =
                tokens.Contains("decor") ||
                tokens.Contains("decoration") ||
                tokens.Contains("decorative") ||
                tokens.Contains("deco") ||
                tokens.Contains("mirror") ||
                tokens.Contains("plant") ||
                tokens.Contains("planter") ||
                tokens.Contains("pedestal") ||
                tokens.Contains("sculpture") ||
                tokens.Contains("statue") ||
                tokens.Contains("ornament") ||
                tokens.Contains("ornamental") ||
                (tokens.Contains("framed") &&
                 tokens.Contains("floor"));

            if (!decoration)
                return false;

            if (ContainsAny(
                    tokens,
                    WallEvidence))
            {
                type = "Decoration";
                category = "Decoration";
                reasonCode =
                    "PLACEMENT_WALL_REQUIRES_ADAPTER";
                message =
                    "Wall-mounted decoration was identified before Unity model import. " +
                    "The wall-placement adapter is not available yet, so expensive import/analysis is skipped.";
                return true;
            }

            if (ContainsAny(
                    tokens,
                    CeilingEvidence))
            {
                type = "Decoration";
                category = "Decoration";
                reasonCode =
                    "PLACEMENT_CEILING_REQUIRES_ADAPTER";
                message =
                    "Ceiling-mounted decoration was identified before Unity model import. " +
                    "The ceiling-placement adapter is not available yet, so expensive import/analysis is skipped.";
                return true;
            }

            if (ContainsAny(
                    tokens,
                    SurfaceEvidence))
            {
                type = "Decoration";
                category = "Decoration";
                reasonCode =
                    "PLACEMENT_SURFACE_REQUIRES_ADAPTER";
                message =
                    "Surface decoration was identified before Unity model import. " +
                    "The surface-placement adapter is not available yet, so expensive import/analysis is skipped.";
                return true;
            }

            return false;
        }

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

            SavicEquipmentIntegrationDecision
                equipmentDecision =
                    SavicEquipmentIntegrationPolicy.Resolve(
                        manifest.source.originalFileName,
                        type);

            if (equipment)
            {
                if (equipmentDecision.RequiresGameplayAdapter)
                {
                    plan.requiresFunctionalAdapter =
                        true;

                    reasonCode =
                        equipmentDecision.ReasonCode;

                    error =
                        equipmentDecision.Evidence;

                    return false;
                }

                if (!equipmentDecision.IsPassive)
                {
                    plan.requiresFunctionalAdapter =
                        equipmentDecision.Mode ==
                        SavicEquipmentIntegrationMode
                            .RequiresDedicatedGameplayAdapter;

                    reasonCode =
                        string.IsNullOrWhiteSpace(
                            equipmentDecision.ReasonCode)
                            ? "EQUIPMENT_FUNCTION_AMBIGUOUS"
                            : equipmentDecision.ReasonCode;

                    error =
                        string.IsNullOrWhiteSpace(
                            equipmentDecision.Evidence)
                            ? "Equipment function is ambiguous; SAVIC will not publish it as passive content."
                            : equipmentDecision.Evidence;

                    return false;
                }

                if (!string.Equals(
                        equipmentDecision.TypeId,
                        type,
                        StringComparison.Ordinal))
                {
                    reasonCode =
                        "EQUIPMENT_TYPE_CONFLICT";

                    error =
                        "Equipment integration policy conflicts with the classified equipment type.";

                    return false;
                }

                plan.integrationMode =
                    SavicEquipmentIntegrationPolicy
                        .PassiveAreaPlaceableMode;

                plan.requiredAreaCapabilityId =
                    equipmentDecision
                        .RequiredAreaCapabilityId;
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
                    : equipmentDecision.Evidence;

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
