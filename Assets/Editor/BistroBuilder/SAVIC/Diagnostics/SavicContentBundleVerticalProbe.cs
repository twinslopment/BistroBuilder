using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicContentBundleVerticalProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Content Bundle Vertical Probe",
            false,
            134)]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            SavicStorageLayout currentLayout =
                SavicStorageLayout.ForCurrentProject();

            currentLayout.EnsureInfrastructure();

            string suffix =
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 10);

            string ingredientId =
                "ingredient_savic_probe_" +
                suffix;

            string packageId =
                "package_savic_probe_" +
                suffix;

            string dishId =
                "dish_savic_probe_" +
                suffix;

            string recipeId =
                "recipe_savic_probe_" +
                suffix;

            string supplierId =
                "supplier_savic_probe_" +
                suffix;

            string offerId =
                "offer_savic_probe_" +
                suffix;

            string bundleId =
                "bundle_savic_probe_" +
                suffix;

            SavicContentBundleDocument document =
                BuildDocument(
                    bundleId,
                    ingredientId,
                    packageId,
                    dishId,
                    recipeId,
                    supplierId,
                    offerId);

            string json =
                JsonUtility.ToJson(
                    document,
                    true);

            string incomingFileName =
                "savic_content_bundle_probe_" +
                suffix +
                ".json";

            string incomingPath =
                Path.Combine(
                    currentLayout.DropHereRoot,
                    incomingFileName);

            if (File.Exists(incomingPath))
                File.Delete(incomingPath);

            File.WriteAllText(
                incomingPath,
                json);

            string sourceHash =
                SavicHashService.ComputeSha256(
                    incomingPath);

            string archivePath =
                currentLayout.GetArchivedSourcePath(
                    sourceHash,
                    incomingFileName);

            bool archiveExisted =
                File.Exists(archivePath);

            string diagnosticRoot =
                Path.Combine(
                    currentLayout.RuntimeRoot,
                    "Diagnostics",
                    "ContentBundleVertical",
                    suffix);

            SavicStorageLayout queueLayout =
                new SavicStorageLayout(
                    diagnosticRoot);

            queueLayout.EnsureInfrastructure();

            SavicManifestRepository manifests =
                new SavicManifestRepository(
                    currentLayout,
                    Path.Combine(
                        diagnosticRoot,
                        "Manifests"));

            SavicJobStore jobs =
                new SavicJobStore(
                    queueLayout);

            SavicIntakeService intake =
                new SavicIntakeService(
                    currentLayout,
                    manifests,
                    jobs);

            SavicSourceProcessingService processing =
                new SavicSourceProcessingService(
                    currentLayout,
                    manifests);

            string ingredientAssetPath =
                BistroBuilderIngredientsRecipesEditorUtility
                    .GetIngredientAssetPath(
                        ingredientId);

            string dishAssetPath =
                BistroBuilderIngredientsRecipesEditorUtility
                    .GetDishAssetPath(
                        dishId);

            string recipeAssetPath =
                BistroBuilderIngredientsRecipesEditorUtility
                    .GetRecipeAssetPath(
                        recipeId);

            using SavicAssetMutationScope rollback =
                new SavicAssetMutationScope(
                    currentLayout,
                    "content_bundle_vertical_probe");

            CaptureCanonicalAssets(
                rollback,
                ingredientAssetPath,
                dishAssetPath,
                recipeAssetPath);

            try
            {
                SavicIntakeOutcome intakeOutcome =
                    intake.IngestSynchronously(
                        incomingPath);

                Require(
                    intakeOutcome.Succeeded &&
                    !intakeOutcome.DuplicateExact,
                    "Content bundle intake failed or unexpectedly deduplicated.");

                SavicSourceProcessingOutcome processingOutcome =
                    processing.ProcessBySavicId(
                        intakeOutcome.ManifestSavicId);

                Require(
                    processingOutcome.Succeeded,
                    "Content bundle processing failed: " +
                    processingOutcome.Message);

                Require(
                    manifests.TryGetBySavicId(
                        intakeOutcome.ManifestSavicId,
                        out SavicManifest manifest),
                    "Published content bundle manifest is missing.");

                Require(
                    manifest.contentBundleReadiness != null &&
                    manifest.contentBundleReadiness.validated &&
                    manifest.contentBundleReadiness.ingredientsPublished == 1 &&
                    manifest.contentBundleReadiness.dishesPublished == 1 &&
                    manifest.contentBundleReadiness.recipesPublished == 1 &&
                    manifest.contentBundleReadiness.suppliersPublished == 1,
                    "Content bundle readiness/counts are invalid.");

                ValidateIngredient(
                    ingredientId,
                    packageId);

                ValidateDishRecipe(
                    dishId,
                    recipeId,
                    ingredientId);

                ValidateSupplier(
                    supplierId,
                    offerId,
                    ingredientId,
                    packageId);

                ValidateRejectedStructuredFormat();

                Debug.Log(
                    "[SAVIC] CONTENT BUNDLE VERTICAL PROBE - PASS\n" +
                    "Ingredient publication/catalog: PASS\n" +
                    "Commercial package authoring: PASS\n" +
                    "Dish publication/catalog: PASS\n" +
                    "Recipe cross-reference/cost contract: PASS\n" +
                    "Supplier authoring/base-offer contract: PASS\n" +
                    "Atomic canonical catalogs: PASS\n" +
                    "Unsupported CSV/TSV routing: PASS");
            }
            finally
            {
                try
                {
                    if (File.Exists(incomingPath))
                        File.Delete(incomingPath);

                    if (!archiveExisted &&
                        File.Exists(archivePath))
                    {
                        File.Delete(archivePath);
                    }

                    if (Directory.Exists(diagnosticRoot))
                    {
                        Directory.Delete(
                            diagnosticRoot,
                            true);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[SAVIC] Content bundle probe cleanup warning: " +
                        exception.Message);
                }
            }
        }

        private static SavicContentBundleDocument BuildDocument(
            string bundleId,
            string ingredientId,
            string packageId,
            string dishId,
            string recipeId,
            string supplierId,
            string offerId)
        {
            return new SavicContentBundleDocument
            {
                schemaId =
                    SavicContentBundlePlanner.SchemaId,
                schemaVersion =
                    SavicContentBundlePlanner.SchemaVersion,
                bundleId =
                    bundleId,
                ingredients =
                    new[]
                    {
                        new SavicIngredientContentRecord
                        {
                            ingredientId = ingredientId,
                            displayName = "Harina SAVIC Probe",
                            category = "DryGoods",
                            storageType = "DryStorage",
                            baseUnit = "Gram",
                            referencePackAmount = 1d,
                            referencePackUnit = "Kilogram",
                            referencePackPriceCents = 220,
                            defaultShelfLifeDays = 365,
                            perishable = false,
                            commercialPackages =
                                new[]
                                {
                                    new SavicCommercialPackageContentRecord
                                    {
                                        packageFormatId = packageId,
                                        displayName = "Saco 1 kg",
                                        packageType = "Saco",
                                        netQuantityMicrounits = 1000000000L,
                                        logisticSize = "Pequeno",
                                        isActive = true
                                    }
                                }
                        }
                    },
                dishes =
                    new[]
                    {
                        new SavicDishContentRecord
                        {
                            dishId = dishId,
                            displayName = "Plato SAVIC Probe",
                            description =
                                "Contenido temporal para validar publicación SAVIC.",
                            categoryId =
                                BistroBuilderDishCategoryIdUtility.MainCourse,
                            course = "Main",
                            defaultAvailability = "Lunch,Dinner",
                            allowedServiceModes = "TableService",
                            requiredStation = "HotKitchen",
                            basePreparationSeconds = 180,
                            complexity = 2,
                            recipeId = recipeId,
                            basePriceCents = 1290,
                            shareable = false,
                            minimumConsumers = 1,
                            maximumConsumers = 1
                        }
                    },
                recipes =
                    new[]
                    {
                        new SavicRecipeContentRecord
                        {
                            recipeId = recipeId,
                            dishId = dishId,
                            yieldPortions = 1,
                            wasteBasisPoints = 500,
                            notes = "SAVIC vertical probe.",
                            ingredients =
                                new[]
                                {
                                    new SavicRecipeIngredientContentRecord
                                    {
                                        ingredientId = ingredientId,
                                        amount = 120d,
                                        unit = "Gram"
                                    }
                                }
                        }
                    },
                suppliers =
                    new[]
                    {
                        new SavicSupplierContentRecord
                        {
                            supplierId = supplierId,
                            displayName = "Proveedor SAVIC Probe",
                            shortName = "SAVIC Probe",
                            description =
                                "Proveedor temporal para validar el bundle.",
                            catalogFlags = "Secos",
                            commercialModelFlags = "Distribuidor",
                            scopeFlags = "Regional",
                            positioningFlags = "Equilibrado",
                            reliabilityTier = "Alta",
                            reliabilityValue = 0.97f,
                            minimumOrderValueCents = 3000,
                            shippingCostCents = 500,
                            freeShippingEnabled = true,
                            freeShippingThresholdCents = 12000,
                            defaultLeadTimeGameHours = 24f,
                            isActive = true,
                            deliveryWindows =
                                new[]
                                {
                                    new SavicSupplierDeliveryWindowContentRecord
                                    {
                                        startMinuteOfDay = 480,
                                        endMinuteOfDay = 720,
                                        monday = true,
                                        tuesday = true,
                                        wednesday = true,
                                        thursday = true,
                                        friday = true,
                                        saturday = false,
                                        sunday = false
                                    }
                                },
                            baseOffers =
                                new[]
                                {
                                    new SavicSupplierOfferContentRecord
                                    {
                                        supplierOfferId = offerId,
                                        ingredientId = ingredientId,
                                        packageFormatId = packageId,
                                        basePriceCents = 210,
                                        minimumPackageCount = 1,
                                        orderIncrement = 1,
                                        initialAvailability = "Disponible",
                                        promotionEligible = true,
                                        overrideLeadTime = false,
                                        leadTimeOverrideGameHours = 24f,
                                        minimumMarketVariationPercent = -5f,
                                        maximumMarketVariationPercent = 10f,
                                        sortOrder = 0,
                                        isActive = true
                                    }
                                }
                        }
                    }
            };
        }

        private static void CaptureCanonicalAssets(
            SavicAssetMutationScope rollback,
            string ingredientAssetPath,
            string dishAssetPath,
            string recipeAssetPath)
        {
            rollback.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .IngredientCatalogPath);
            rollback.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .DishCatalogPath);
            rollback.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .RecipeCatalogPath);
            rollback.CaptureAsset(
                SavicContentBundlePublisher
                    .IngredientAuthoringDatabasePath);
            rollback.CaptureAsset(
                SavicContentBundlePublisher
                    .SupplierAuthoringDatabasePath);
            rollback.CaptureAsset(
                SavicContentBundlePublisher
                    .DishImageCatalogPath);
            rollback.CaptureAsset(
                ingredientAssetPath);
            rollback.CaptureAsset(
                dishAssetPath);
            rollback.CaptureAsset(
                recipeAssetPath);
        }

        private static void ValidateIngredient(
            string ingredientId,
            string packageId)
        {
            BistroBuilderIngredientCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientCatalog>(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .IngredientCatalogPath);

            Require(
                catalog != null &&
                catalog.TryGetDefinition(
                    ingredientId,
                    out BistroBuilderIngredientDefinition definition) &&
                definition != null &&
                definition.TryValidate(
                    out _),
                "Published ingredient is missing/invalid.");

            BistroBuilderIngredientAuthoringDatabase authoring =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientAuthoringDatabase>(
                    SavicContentBundlePublisher
                        .IngredientAuthoringDatabasePath);

            Require(
                authoring != null &&
                authoring.TryGetIngredient(
                    ingredientId,
                    out BistroBuilderIngredientAuthoringRecord record) &&
                record != null,
                "Ingredient authoring record is missing.");

            bool packageFound = false;

            for (int index = 0;
                 index < record.commercialPackages.Count;
                 index++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package =
                    record.commercialPackages[index];

                if (package != null &&
                    string.Equals(
                        package.PackageFormatId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    packageFound =
                        package.netQuantityMicrounits > 0;
                    break;
                }
            }

            Require(
                packageFound,
                "Commercial package was not published.");
        }

        private static void ValidateDishRecipe(
            string dishId,
            string recipeId,
            string ingredientId)
        {
            BistroBuilderDishCatalog dishCatalog =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishCatalog>(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .DishCatalogPath);

            BistroBuilderRecipeCatalog recipeCatalog =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderRecipeCatalog>(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .RecipeCatalogPath);

            Require(
                dishCatalog != null &&
                dishCatalog.TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition dish) &&
                dish != null &&
                dish.TryValidate(
                    out _) &&
                string.Equals(
                    dish.RecipeId,
                    recipeId,
                    StringComparison.Ordinal),
                "Published dish is missing/invalid.");

            Require(
                recipeCatalog != null &&
                recipeCatalog.TryGetByRecipeId(
                    recipeId,
                    out BistroBuilderRecipeDefinition recipe) &&
                recipe != null &&
                recipe.TryValidate(
                    out _) &&
                string.Equals(
                    recipe.DishId,
                    dishId,
                    StringComparison.Ordinal) &&
                recipe.Ingredients.Count == 1 &&
                string.Equals(
                    recipe.Ingredients[0]
                        .Ingredient
                        .IngredientId,
                    ingredientId,
                    StringComparison.Ordinal) &&
                recipe.TryCalculateCostPerPortionCents(
                    out int costCents,
                    out _) &&
                costCents > 0,
                "Published recipe/cost contract is invalid.");
        }

        private static void ValidateSupplier(
            string supplierId,
            string offerId,
            string ingredientId,
            string packageId)
        {
            BistroBuilderSupplierAuthoringDatabase supplierDatabase =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderSupplierAuthoringDatabase>(
                    SavicContentBundlePublisher
                        .SupplierAuthoringDatabasePath);

            BistroBuilderIngredientAuthoringDatabase ingredientDatabase =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientAuthoringDatabase>(
                    SavicContentBundlePublisher
                        .IngredientAuthoringDatabasePath);

            Require(
                supplierDatabase != null &&
                supplierDatabase.TryGetSupplier(
                    supplierId,
                    out BistroBuilderSupplierAuthoringRecord supplier) &&
                supplier != null,
                "Published supplier is missing.");

            bool offerFound = false;

            for (int index = 0;
                 index < supplier.baseOffers.Count;
                 index++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[index];

                if (offer != null &&
                    string.Equals(
                        offer.SupplierOfferId,
                        offerId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        offer.ingredientId,
                        ingredientId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        offer.packageFormatId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    offerFound = true;
                    break;
                }
            }

            Require(
                offerFound,
                "Published supplier base offer is missing/invalid.");

            BistroBuilderAuthoringValidationReport report =
                BistroBuilderSupplierAuthoringValidator.Validate(
                    supplierDatabase,
                    ingredientDatabase);

            Require(
                report.IsStructurallyValid,
                "Supplier/ingredient authoring validator reported errors.");
        }

        private static void ValidateRejectedStructuredFormat()
        {
            SavicManifest csvManifest =
                new SavicManifest
                {
                    savicId =
                        "structured_csv_probe",
                    source =
                        new SavicSourceRecord
                        {
                            sourceHash =
                                "structured-csv-probe",
                            originalFileName =
                                "content.csv",
                            extension =
                                ".csv",
                            sourceKind =
                                SavicSourceKind
                                    .StructuredData
                                    .ToString()
                        }
                };

            bool planned =
                SavicContentBundlePlanner.TryParseAndPlan(
                    csvManifest,
                    "id,name",
                    out _,
                    out _,
                    out string reasonCode,
                    out _);

            Require(
                !planned &&
                string.Equals(
                    reasonCode,
                    "STRUCTURED_FORMAT_UNSUPPORTED",
                    StringComparison.Ordinal),
                "CSV structured data did not route to explicit review.");
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
