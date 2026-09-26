using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal enum SavicEquipmentIntegrationMode
    {
        NotEquipment = 0,
        PassiveAreaPlaceable = 1,
        RequiresDedicatedGameplayAdapter = 2,
        Ambiguous = 3
    }

    internal readonly struct SavicEquipmentIntegrationDecision
    {
        internal SavicEquipmentIntegrationDecision(
            SavicEquipmentIntegrationMode mode,
            string typeId,
            string requiredAreaCapabilityId,
            string reasonCode,
            string evidence)
        {
            Mode = mode;
            TypeId = typeId ?? string.Empty;
            RequiredAreaCapabilityId =
                requiredAreaCapabilityId ?? string.Empty;
            ReasonCode = reasonCode ?? string.Empty;
            Evidence = evidence ?? string.Empty;
        }

        internal SavicEquipmentIntegrationMode Mode { get; }
        internal string TypeId { get; }
        internal string RequiredAreaCapabilityId { get; }
        internal string ReasonCode { get; }
        internal string Evidence { get; }

        internal bool IsEquipment =>
            Mode != SavicEquipmentIntegrationMode.NotEquipment;

        internal bool IsPassive =>
            Mode == SavicEquipmentIntegrationMode.PassiveAreaPlaceable;

        internal bool RequiresGameplayAdapter =>
            Mode ==
            SavicEquipmentIntegrationMode.RequiresDedicatedGameplayAdapter;
    }

    /// <summary>
    /// Maps equipment identity to an existing Bistro Builder contract.
    /// It never creates appliance gameplay. Equipment with no simulated
    /// runtime function can be published as a passive placeable constrained
    /// by the canonical area capability; equipment that corresponds to an
    /// interactive gameplay station/end-point remains review-gated until an
    /// approved adapter exists.
    /// </summary>
    internal static class SavicEquipmentIntegrationPolicy
    {
        internal const string Version = "1.0.0";

        internal const string PassiveAreaPlaceableMode =
            "PASSIVE_AREA_PLACEABLE";

        internal const string FoodProductionCapabilityId =
            "food_production";

        internal const string OrderPickupCapabilityId =
            "order_pickup";

        private static readonly HashSet<string>
            PassiveKitchenEquipmentTokens =
                new HashSet<string>(
                    new[]
                    {
                        // Storage/refrigeration has no dedicated gameplay
                        // authority in the current canonical systems.
                        "cooler", "fridge", "refrigerator", "freezer",
                        "cabinet", "cupboard", "shelf", "shelving",
                        "rack", "storage", "locker", "stand",

                        // D-003: water/extraction/ventilation are not
                        // simulated gameplay, therefore these remain static.
                        "dishwasher", "sink", "tap", "faucet",
                        "extractor", "hood"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string>
            InteractiveKitchenEquipmentTokens =
                new HashSet<string>(
                    new[]
                    {
                        // These correspond to real cooking/station concepts
                        // already present in the kitchen domain. A placeable
                        // must not impersonate them without an approved bridge.
                        "oven", "stove", "range", "grill", "fryer",
                        "coffee", "machine"
                    },
                    StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string>
            InteractiveServiceEquipmentTokens =
                new HashSet<string>(
                    new[]
                    {
                        // Existing service/bar/pass systems own these
                        // interactions; static publication would duplicate
                        // gameplay authority.
                        "counter", "bar", "pass",
                        "register", "pos", "cash"
                    },
                    StringComparer.OrdinalIgnoreCase);

        internal static SavicEquipmentIntegrationDecision Resolve(
            string sourceFileName,
            string classifiedType = "")
        {
            HashSet<string> tokens =
                Tokenize(
                    Path.GetFileNameWithoutExtension(
                        sourceFileName ??
                        string.Empty));

            bool classifiedKitchen =
                string.Equals(
                    classifiedType,
                    "KitchenEquipment",
                    StringComparison.Ordinal);

            bool classifiedService =
                string.Equals(
                    classifiedType,
                    "ServiceEquipment",
                    StringComparison.Ordinal);

            bool serviceInteractive =
                ContainsAny(
                    tokens,
                    InteractiveServiceEquipmentTokens);

            if (serviceInteractive)
            {
                return new SavicEquipmentIntegrationDecision(
                    SavicEquipmentIntegrationMode
                        .RequiresDedicatedGameplayAdapter,
                    "ServiceEquipment",
                    OrderPickupCapabilityId,
                    "FUNCTIONAL_ADAPTER_REQUIRED",
                    "The source maps to an existing interactive service/bar/pass concept. SAVIC will not duplicate that gameplay authority with a generic static prefab.");
            }

            bool kitchenInteractive =
                ContainsAny(
                    tokens,
                    InteractiveKitchenEquipmentTokens);

            if (kitchenInteractive)
            {
                return new SavicEquipmentIntegrationDecision(
                    SavicEquipmentIntegrationMode
                        .RequiresDedicatedGameplayAdapter,
                    "KitchenEquipment",
                    FoodProductionCapabilityId,
                    "FUNCTIONAL_ADAPTER_REQUIRED",
                    "The source maps to an existing interactive kitchen-station concept. A dedicated integration adapter is required before the placeable may provide gameplay.");
            }

            bool kitchenPassive =
                ContainsAny(
                    tokens,
                    PassiveKitchenEquipmentTokens);

            if (kitchenPassive)
            {
                return new SavicEquipmentIntegrationDecision(
                    SavicEquipmentIntegrationMode
                        .PassiveAreaPlaceable,
                    "KitchenEquipment",
                    FoodProductionCapabilityId,
                    "PASSIVE_EQUIPMENT_CANONICAL",
                    "The equipment has no dedicated simulated appliance behavior in the current product contract. It may be published as a passive KitchenEquipment placeable constrained to areas with food_production capability.");
            }

            if (classifiedKitchen)
            {
                return new SavicEquipmentIntegrationDecision(
                    SavicEquipmentIntegrationMode.Ambiguous,
                    "KitchenEquipment",
                    FoodProductionCapabilityId,
                    "EQUIPMENT_FUNCTION_AMBIGUOUS",
                    "Kitchen equipment was classified, but SAVIC cannot prove whether it is passive or requires an interactive gameplay adapter.");
            }

            if (classifiedService)
            {
                return new SavicEquipmentIntegrationDecision(
                    SavicEquipmentIntegrationMode.Ambiguous,
                    "ServiceEquipment",
                    OrderPickupCapabilityId,
                    "EQUIPMENT_FUNCTION_AMBIGUOUS",
                    "Service equipment was classified, but SAVIC cannot prove whether it is passive or requires an interactive gameplay adapter.");
            }

            return new SavicEquipmentIntegrationDecision(
                SavicEquipmentIntegrationMode.NotEquipment,
                string.Empty,
                string.Empty,
                string.Empty,
                "No equipment integration evidence.");
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

                if (char.IsLetterOrDigit(character))
                {
                    current.Append(
                        char.ToLowerInvariant(character));
                }
                else
                {
                    FlushToken(
                        current,
                        tokens);
                }
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
    }
}
