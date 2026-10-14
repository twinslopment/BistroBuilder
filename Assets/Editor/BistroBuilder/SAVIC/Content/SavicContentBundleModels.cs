using System;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicContentBundleDocument
    {
        public string schemaId = SavicContentBundlePlanner.SchemaId;
        public int schemaVersion = SavicContentBundlePlanner.SchemaVersion;
        public string bundleId = string.Empty;
        public SavicIngredientContentRecord[] ingredients;
        public SavicDishContentRecord[] dishes;
        public SavicRecipeContentRecord[] recipes;
        public SavicSupplierContentRecord[] suppliers;
    }

    [Serializable]
    internal sealed class SavicIngredientContentRecord
    {
        public string ingredientId = string.Empty;
        public string displayName = string.Empty;
        public string category = "Other";
        public string storageType = "DryStorage";
        public string baseUnit = "Gram";
        public double referencePackAmount = 1d;
        public string referencePackUnit = "Kilogram";
        public int referencePackPriceCents;
        public int defaultShelfLifeDays;
        public bool perishable;
        public string imageContentId = string.Empty;
        public SavicCommercialPackageContentRecord[] commercialPackages;
    }

    [Serializable]
    internal sealed class SavicCommercialPackageContentRecord
    {
        public string packageFormatId = string.Empty;
        public string displayName = string.Empty;
        public string packageType = "Caja";
        public long netQuantityMicrounits = 1000000L;
        public string logisticSize = "Medio";
        public string imageContentId = string.Empty;
        public bool isActive = true;
    }

    [Serializable]
    internal sealed class SavicDishContentRecord
    {
        public string dishId = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string categoryId = BistroBuilderDishCategoryIdUtility.MainCourse;
        public string course = "Main";
        public string defaultAvailability = "Lunch,Dinner";
        public string allowedServiceModes = "TableService";
        public string requiredStation = "HotKitchen";
        public int basePreparationSeconds = 300;
        public int complexity = 1;
        public string recipeId = string.Empty;
        public int basePriceCents = 1000;
        public bool shareable;
        public int minimumConsumers = 1;
        public int maximumConsumers = 1;
        public string imageContentId = string.Empty;
    }

    [Serializable]
    internal sealed class SavicRecipeContentRecord
    {
        public string recipeId = string.Empty;
        public string dishId = string.Empty;
        public int yieldPortions = 1;
        public int wasteBasisPoints;
        public string notes = string.Empty;
        public SavicRecipeIngredientContentRecord[] ingredients;
    }

    [Serializable]
    internal sealed class SavicRecipeIngredientContentRecord
    {
        public string ingredientId = string.Empty;
        public double amount = 1d;
        public string unit = "Gram";
    }

    [Serializable]
    internal sealed class SavicSupplierContentRecord
    {
        public string supplierId = string.Empty;
        public string displayName = string.Empty;
        public string shortName = string.Empty;
        public string description = string.Empty;
        public string logoContentId = string.Empty;
        public string primaryBrandColor = "#335943";
        public string secondaryBrandColor = "#E6D6A3";
        public string textContrastColor = "#FFFFFF";
        public string catalogFlags = "Generalista";
        public string commercialModelFlags = "Generalista";
        public string scopeFlags = "Regional";
        public string positioningFlags = "Equilibrado";
        public string reliabilityTier = "Alta";
        public float reliabilityValue = 0.97f;
        public long minimumOrderValueCents = 3000;
        public long shippingCostCents = 800;
        public bool freeShippingEnabled = true;
        public long freeShippingThresholdCents = 15000;
        public float defaultLeadTimeGameHours = 24f;
        public bool isActive = true;
        public string[] customTags;
        public SavicSupplierDeliveryWindowContentRecord[] deliveryWindows;
        public SavicSupplierOfferContentRecord[] baseOffers;
    }

    [Serializable]
    internal sealed class SavicSupplierDeliveryWindowContentRecord
    {
        public int startMinuteOfDay = 480;
        public int endMinuteOfDay = 720;
        public bool monday = true;
        public bool tuesday = true;
        public bool wednesday = true;
        public bool thursday = true;
        public bool friday = true;
        public bool saturday = true;
        public bool sunday;
    }

    [Serializable]
    internal sealed class SavicSupplierOfferContentRecord
    {
        public string supplierOfferId = string.Empty;
        public string ingredientId = string.Empty;
        public string packageFormatId = string.Empty;
        public long basePriceCents = 100;
        public int minimumPackageCount = 1;
        public int orderIncrement = 1;
        public string initialAvailability = "Disponible";
        public bool promotionEligible = true;
        public bool overrideLeadTime;
        public float leadTimeOverrideGameHours = 24f;
        public float minimumMarketVariationPercent = -10f;
        public float maximumMarketVariationPercent = 15f;
        public int sortOrder;
        public bool isActive = true;
    }

    internal static class SavicContentBundlePlanner
    {
        internal const string Version = "1.0.0";
        internal const string SchemaId = "savic.content-bundle";
        internal const int SchemaVersion = 1;

        internal static bool TryParseAndPlan(
            SavicManifest manifest,
            string json,
            out SavicContentBundleDocument document,
            out SavicContentBundleAuthoringRecord plan,
            out string reasonCode,
            out string error)
        {
            document = null;
            plan =
                new SavicContentBundleAuthoringRecord
                {
                    plannerVersion = Version
                };
            reasonCode = string.Empty;
            error = string.Empty;

            if (manifest?.source == null ||
                !string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.StructuredData.ToString(),
                    StringComparison.Ordinal))
            {
                reasonCode = "CONTENT_BUNDLE_INVALID_MANIFEST";
                error = "Content bundle planning requires a StructuredData source.";
                return false;
            }

            if (!string.Equals(
                    manifest.source.extension,
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                reasonCode = "STRUCTURED_FORMAT_UNSUPPORTED";
                error = "SAVIC content bundle V1 accepts JSON only; CSV/TSV remain archived for explicit conversion.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                reasonCode = "CONTENT_BUNDLE_EMPTY";
                error = "Content bundle JSON is empty.";
                return false;
            }

            try
            {
                document =
                    JsonUtility.FromJson<SavicContentBundleDocument>(
                        json);
            }
            catch (Exception exception)
            {
                reasonCode = "CONTENT_BUNDLE_JSON_INVALID";
                error = "Content bundle JSON could not be parsed: " + exception.Message;
                return false;
            }

            if (document == null)
            {
                reasonCode = "CONTENT_BUNDLE_JSON_INVALID";
                error = "Content bundle JSON produced no document.";
                return false;
            }

            if (!string.Equals(
                    document.schemaId,
                    SchemaId,
                    StringComparison.Ordinal) ||
                document.schemaVersion != SchemaVersion)
            {
                reasonCode = "CONTENT_BUNDLE_SCHEMA_UNSUPPORTED";
                error =
                    "Content bundle must declare " +
                    SchemaId +
                    " schemaVersion " +
                    SchemaVersion +
                    ".";
                return false;
            }

            document.bundleId =
                BistroBuilderMenuIdUtility.NormalizeStableId(
                    document.bundleId);

            if (!BistroBuilderMenuIdUtility.IsValidStableId(
                    document.bundleId))
            {
                reasonCode = "CONTENT_BUNDLE_ID_INVALID";
                error = "Content bundle requires a stable bundleId.";
                return false;
            }

            int ingredientCount =
                document.ingredients?.Length ?? 0;
            int dishCount =
                document.dishes?.Length ?? 0;
            int recipeCount =
                document.recipes?.Length ?? 0;
            int supplierCount =
                document.suppliers?.Length ?? 0;

            if (ingredientCount +
                dishCount +
                recipeCount +
                supplierCount == 0)
            {
                reasonCode = "CONTENT_BUNDLE_NO_RECORDS";
                error = "Content bundle contains no publishable records.";
                return false;
            }

            plan.planned = true;
            plan.schemaId = SchemaId;
            plan.schemaVersion = SchemaVersion;
            plan.bundleId = document.bundleId;
            plan.ingredientCount = ingredientCount;
            plan.dishCount = dishCount;
            plan.recipeCount = recipeCount;
            plan.supplierCount = supplierCount;
            plan.planReason =
                "Explicit SAVIC content bundle mapped to canonical ingredient, dish, recipe and supplier authorities.";
            plan.plannedUtc = DateTime.UtcNow.ToString("O");

            if (string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                manifest.canonicalContentId =
                    "bb_content_bundle_" +
                    document.bundleId;
            }

            return true;
        }
    }
}
