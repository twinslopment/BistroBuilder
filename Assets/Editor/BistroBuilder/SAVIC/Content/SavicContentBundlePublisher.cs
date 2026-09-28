using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicContentBundlePublicationOutcome
    {
        internal SavicContentBundlePublicationOutcome(
            bool succeeded,
            string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
    }

    internal sealed class SavicContentBundlePublisher
    {
        internal const string Version = "1.0.0";

        internal const string IngredientAuthoringDatabasePath =
            "Assets/Resources/BistroBuilder/Suppliers/Authoring/" +
            "BistroBuilderIngredientAuthoringDatabase.asset";

        internal const string SupplierAuthoringDatabasePath =
            "Assets/Resources/BistroBuilder/Suppliers/Authoring/" +
            "BistroBuilderSupplierAuthoringDatabase.asset";

        internal const string DishImageCatalogPath =
            "Assets/Resources/BistroBuilder/Menu/DishImageCatalog.asset";

        internal const string DishCategoryCatalogPath =
            "Assets/Data/BistroBuilder/Menu/Categories/" +
            "BistroBuilderDishCategoryCatalog.asset";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;

        internal SavicContentBundlePublisher(
            SavicStorageLayout layout,
            SavicManifestRepository manifests)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests =
                manifests ?? throw new ArgumentNullException(nameof(manifests));
        }

        internal SavicContentBundlePublicationOutcome Publish(
            SavicManifest manifest,
            SavicContentBundleDocument document)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            BistroBuilderIngredientsRecipesEditorUtility.EnsureDataFolders();

            BistroBuilderIngredientCatalog ingredientCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .LoadOrCreateIngredientCatalog();

            BistroBuilderDishCatalog dishCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .RequireDishCatalog();

            BistroBuilderRecipeCatalog recipeCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .LoadOrCreateRecipeCatalog();

            BistroBuilderDishCategoryCatalog categoryCatalog =
                AssetDatabase.LoadAssetAtPath<BistroBuilderDishCategoryCatalog>(
                    DishCategoryCatalogPath);

            BistroBuilderIngredientAuthoringDatabase ingredientAuthoring =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientAuthoringDatabase>(
                    IngredientAuthoringDatabasePath);

            BistroBuilderSupplierAuthoringDatabase supplierDatabase =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderSupplierAuthoringDatabase>(
                    SupplierAuthoringDatabasePath);

            BistroBuilderDishImageCatalog dishImageCatalog =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishImageCatalog>(
                    DishImageCatalogPath);

            if (ingredientCatalog == null ||
                dishCatalog == null ||
                recipeCatalog == null ||
                categoryCatalog == null ||
                ingredientAuthoring == null ||
                supplierDatabase == null ||
                dishImageCatalog == null)
            {
                return Failure(
                    "At least one canonical Bistro Builder content authority is missing.");
            }

            Dictionary<string, SavicIngredientContentRecord> ingredientInput =
                IndexIngredients(document.ingredients);

            Dictionary<string, SavicDishContentRecord> dishInput =
                IndexDishes(document.dishes);

            Dictionary<string, SavicRecipeContentRecord> recipeInput =
                IndexRecipes(document.recipes);

            Dictionary<string, SavicSupplierContentRecord> supplierInput =
                IndexSuppliers(document.suppliers);

            ValidateBundleReferences(
                ingredientInput,
                dishInput,
                recipeInput,
                supplierInput,
                ingredientCatalog,
                dishCatalog,
                categoryCatalog);

            using SavicAssetMutationScope transaction =
                new SavicAssetMutationScope(
                    layout,
                    "publish_content_bundle_" +
                    manifest.contentBundle.bundleId);

            CaptureMasterAssets(transaction);

            CaptureDefinitionAssets(
                transaction,
                ingredientInput,
                dishInput,
                recipeInput);

            try
            {
                Dictionary<string, BistroBuilderIngredientDefinition>
                    resolvedIngredients =
                        BuildIngredientIndex(
                            ingredientCatalog);

                Dictionary<string, BistroBuilderDishDefinition>
                    resolvedDishes =
                        BuildDishIndex(
                            dishCatalog);

                int ingredientsPublished =
                    PublishIngredients(
                        ingredientInput,
                        ingredientCatalog,
                        ingredientAuthoring,
                        resolvedIngredients);

                int dishesPublished =
                    PublishDishes(
                        dishInput,
                        dishCatalog,
                        categoryCatalog,
                        dishImageCatalog,
                        resolvedDishes);

                int recipesPublished =
                    PublishRecipes(
                        recipeInput,
                        recipeCatalog,
                        resolvedDishes,
                        resolvedIngredients);

                int suppliersPublished =
                    PublishSuppliers(
                        supplierInput,
                        supplierDatabase,
                        ingredientAuthoring,
                        resolvedIngredients);

                EditorUtility.SetDirty(ingredientCatalog);
                EditorUtility.SetDirty(dishCatalog);
                EditorUtility.SetDirty(recipeCatalog);
                EditorUtility.SetDirty(ingredientAuthoring);
                EditorUtility.SetDirty(supplierDatabase);
                EditorUtility.SetDirty(dishImageCatalog);

                AssetDatabase.SaveAssets();

                if (!ingredientCatalog.TryRebuildIndex(
                        out string ingredientCatalogError))
                {
                    throw new InvalidOperationException(
                        "IngredientCatalog: " +
                        ingredientCatalogError);
                }

                if (!dishCatalog.TryRebuildIndex(
                        out string dishCatalogError))
                {
                    throw new InvalidOperationException(
                        "DishCatalog: " +
                        dishCatalogError);
                }

                if (!recipeCatalog.TryRebuildIndex(
                        out string recipeCatalogError))
                {
                    throw new InvalidOperationException(
                        "RecipeCatalog: " +
                        recipeCatalogError);
                }

                ValidateDishRecipeReferences(
                    dishInput,
                    dishCatalog,
                    recipeCatalog);

                ValidateSupplierReferences(
                    supplierDatabase,
                    ingredientAuthoring,
                    ingredientCatalog);

                BistroBuilderAuthoringValidationReport supplierReport =
                    BistroBuilderSupplierAuthoringValidator.Validate(
                        supplierDatabase,
                        ingredientAuthoring);

                if (!supplierReport.IsStructurallyValid)
                {
                    throw new InvalidOperationException(
                        "Supplier/ingredient authoring validation reported " +
                        supplierReport.ErrorCount +
                        " errors.");
                }

                manifest.contentBundleReadiness =
                    new SavicContentBundleReadinessRecord
                    {
                        validated = true,
                        validatorVersion = Version,
                        ingredientCatalogValid = true,
                        dishCatalogValid = true,
                        recipeCatalogValid = true,
                        supplierAuthoringValid = true,
                        ingredientsPublished = ingredientsPublished,
                        dishesPublished = dishesPublished,
                        recipesPublished = recipesPublished,
                        suppliersPublished = suppliersPublished,
                        evidence =
                            "Transactional content bundle resolved and validated against canonical ingredient, dish, recipe and supplier authorities.",
                        validatedUtc =
                            DateTime.UtcNow.ToString("O")
                    };

                SavicManifestMutations.UpsertArtifact(
                    manifest,
                    "published.content-bundle",
                    BistroBuilderIngredientsRecipesEditorUtility
                        .DishCatalogPath,
                    "savic.content-bundle-publisher",
                    Version,
                    BuildFingerprint(
                        manifest,
                        document));

                SavicManifestMutations.UpsertValidation(
                    manifest,
                    "ContentBundle.Publication",
                    "PASS",
                    "INFO",
                    manifest.contentBundleReadiness.evidence,
                    Version);

                SavicManifestMutations.UpsertDecision(
                    manifest,
                    "content.bundleId",
                    manifest.contentBundle.bundleId,
                    "HIGH",
                    manifest.contentBundle.planReason,
                    "content.bundle.v1");

                manifest.family = "Content";
                manifest.type = "ContentBundle";
                manifest.category = "GameData";
                manifest.status = "PUBLISHED";

                manifests.Save(manifest);
                AssetDatabase.SaveAssets();
                transaction.Commit();

                return new SavicContentBundlePublicationOutcome(
                    true,
                    "Content bundle published atomically.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Content bundle publication failed: " +
                    exception.Message);
            }
        }

        private void CaptureMasterAssets(
            SavicAssetMutationScope transaction)
        {
            transaction.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .IngredientCatalogPath);
            transaction.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .DishCatalogPath);
            transaction.CaptureAsset(
                BistroBuilderIngredientsRecipesEditorUtility
                    .RecipeCatalogPath);
            transaction.CaptureAsset(
                IngredientAuthoringDatabasePath);
            transaction.CaptureAsset(
                SupplierAuthoringDatabasePath);
            transaction.CaptureAsset(
                DishImageCatalogPath);
        }

        private static void CaptureDefinitionAssets(
            SavicAssetMutationScope transaction,
            Dictionary<string, SavicIngredientContentRecord> ingredients,
            Dictionary<string, SavicDishContentRecord> dishes,
            Dictionary<string, SavicRecipeContentRecord> recipes)
        {
            foreach (string ingredientId in ingredients.Keys)
            {
                transaction.CaptureAsset(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetIngredientAssetPath(
                            ingredientId));
            }

            foreach (string dishId in dishes.Keys)
            {
                transaction.CaptureAsset(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetDishAssetPath(
                            dishId));
            }

            foreach (string recipeId in recipes.Keys)
            {
                transaction.CaptureAsset(
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetRecipeAssetPath(
                            recipeId));
            }
        }

        private int PublishIngredients(
            Dictionary<string, SavicIngredientContentRecord> input,
            BistroBuilderIngredientCatalog catalog,
            BistroBuilderIngredientAuthoringDatabase authoring,
            Dictionary<string, BistroBuilderIngredientDefinition> resolved)
        {
            int published = 0;

            foreach (KeyValuePair<string, SavicIngredientContentRecord> pair
                     in input)
            {
                SavicIngredientContentRecord record = pair.Value;

                BistroBuilderIngredientCategory category =
                    ParseEnum<BistroBuilderIngredientCategory>(
                        record.category,
                        "ingredient category");

                BistroBuilderIngredientStorageType storage =
                    ParseEnum<BistroBuilderIngredientStorageType>(
                        record.storageType,
                        "ingredient storage type");

                BistroBuilderMeasurementUnit baseUnit =
                    ParseEnum<BistroBuilderMeasurementUnit>(
                        record.baseUnit,
                        "ingredient base unit");

                BistroBuilderMeasurementUnit packUnit =
                    ParseEnum<BistroBuilderMeasurementUnit>(
                        record.referencePackUnit,
                        "reference pack unit");

                string path =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetIngredientAssetPath(pair.Key);

                BistroBuilderIngredientDefinition definition =
                    AssetDatabase.LoadAssetAtPath<
                        BistroBuilderIngredientDefinition>(
                        path);

                if (definition == null)
                {
                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        throw new InvalidOperationException(
                            "Incompatible asset already exists at " +
                            path +
                            ".");
                    }

                    definition =
                        ScriptableObject.CreateInstance<
                            BistroBuilderIngredientDefinition>();

                    AssetDatabase.CreateAsset(
                        definition,
                        path);
                }

                SerializedObject serialized =
                    new SerializedObject(definition);

                SetString(
                    serialized,
                    "ingredientId",
                    pair.Key);
                SetString(
                    serialized,
                    "displayName",
                    record.displayName);
                SetEnum(
                    serialized,
                    "category",
                    (int)category);
                SetEnum(
                    serialized,
                    "storageType",
                    (int)storage);
                SetEnum(
                    serialized,
                    "baseUnit",
                    (int)baseUnit);
                SetDouble(
                    serialized,
                    "referencePackAmount",
                    record.referencePackAmount);
                SetEnum(
                    serialized,
                    "referencePackUnit",
                    (int)packUnit);
                SetInt(
                    serialized,
                    "referencePackPriceCents",
                    record.referencePackPriceCents);
                SetInt(
                    serialized,
                    "defaultShelfLifeDays",
                    record.defaultShelfLifeDays);
                SetBool(
                    serialized,
                    "perishable",
                    record.perishable);

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);

                if (!definition.TryValidate(
                        out string error))
                {
                    throw new InvalidOperationException(
                        path +
                        ": " +
                        error);
                }

                catalog.EditorUpsert(
                    definition);

                resolved[pair.Key] =
                    definition;

                UpsertIngredientAuthoring(
                    authoring,
                    definition,
                    record);

                published++;
            }

            return published;
        }

        private int PublishDishes(
            Dictionary<string, SavicDishContentRecord> input,
            BistroBuilderDishCatalog catalog,
            BistroBuilderDishCategoryCatalog categoryCatalog,
            BistroBuilderDishImageCatalog imageCatalog,
            Dictionary<string, BistroBuilderDishDefinition> resolved)
        {
            int published = 0;

            foreach (KeyValuePair<string, SavicDishContentRecord> pair
                     in input)
            {
                SavicDishContentRecord record = pair.Value;

                string categoryId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        record.categoryId);

                if (!categoryCatalog.Contains(categoryId))
                {
                    throw new InvalidOperationException(
                        "Dish " +
                        pair.Key +
                        " references unknown category " +
                        categoryId +
                        ".");
                }

                BistroBuilderDishCourse course =
                    ParseEnum<BistroBuilderDishCourse>(
                        record.course,
                        "dish course");

                BistroBuilderMealServiceAvailability availability =
                    ParseFlags<BistroBuilderMealServiceAvailability>(
                        record.defaultAvailability,
                        "meal-service availability");

                BistroBuilderDishServiceModeAvailability serviceModes =
                    ParseFlags<BistroBuilderDishServiceModeAvailability>(
                        record.allowedServiceModes,
                        "dish service-mode availability");

                BistroBuilderKitchenStationType station =
                    ParseEnum<BistroBuilderKitchenStationType>(
                        record.requiredStation,
                        "kitchen station");

                string recipeId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        record.recipeId);

                string path =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetDishAssetPath(pair.Key);

                BistroBuilderDishDefinition definition =
                    AssetDatabase.LoadAssetAtPath<
                        BistroBuilderDishDefinition>(
                        path);

                if (definition == null)
                {
                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        throw new InvalidOperationException(
                            "Incompatible asset already exists at " +
                            path +
                            ".");
                    }

                    definition =
                        ScriptableObject.CreateInstance<
                            BistroBuilderDishDefinition>();

                    AssetDatabase.CreateAsset(
                        definition,
                        path);
                }

                definition.InitializeRuntime(
                    pair.Key,
                    record.displayName,
                    record.description,
                    categoryId,
                    course,
                    availability,
                    serviceModes,
                    station,
                    record.basePreparationSeconds,
                    record.complexity,
                    recipeId,
                    record.basePriceCents,
                    record.shareable,
                    record.minimumConsumers,
                    record.maximumConsumers);

                EditorUtility.SetDirty(definition);

                if (!definition.TryValidate(
                        out string error))
                {
                    throw new InvalidOperationException(
                        path +
                        ": " +
                        error);
                }

                catalog.EditorUpsert(
                    definition);

                resolved[pair.Key] =
                    definition;

                if (!string.IsNullOrWhiteSpace(
                        record.imageContentId))
                {
                    Sprite image =
                        ResolveImageContent(
                            record.imageContentId);

                    imageCatalog.EditorUpsert(
                        pair.Key,
                        image,
                        record.imageContentId);
                }

                published++;
            }

            return published;
        }

        private static int PublishRecipes(
            Dictionary<string, SavicRecipeContentRecord> input,
            BistroBuilderRecipeCatalog catalog,
            Dictionary<string, BistroBuilderDishDefinition> dishes,
            Dictionary<string, BistroBuilderIngredientDefinition> ingredients)
        {
            int published = 0;

            foreach (KeyValuePair<string, SavicRecipeContentRecord> pair
                     in input)
            {
                SavicRecipeContentRecord record =
                    pair.Value;

                string dishId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        record.dishId);

                if (!dishes.TryGetValue(
                        dishId,
                        out BistroBuilderDishDefinition dish))
                {
                    throw new InvalidOperationException(
                        "Recipe " +
                        pair.Key +
                        " references missing dish " +
                        dishId +
                        ".");
                }

                List<BistroBuilderRecipeIngredientAmount> lines =
                    new List<BistroBuilderRecipeIngredientAmount>();

                SavicRecipeIngredientContentRecord[] sourceLines =
                    record.ingredients ??
                    Array.Empty<SavicRecipeIngredientContentRecord>();

                if (sourceLines.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Recipe " +
                        pair.Key +
                        " contains no ingredients.");
                }

                HashSet<string> seenIngredients =
                    new HashSet<string>(
                        StringComparer.Ordinal);

                for (int index = 0;
                     index < sourceLines.Length;
                     index++)
                {
                    SavicRecipeIngredientContentRecord source =
                        sourceLines[index];

                    if (source == null)
                    {
                        throw new InvalidOperationException(
                            "Recipe " +
                            pair.Key +
                            " contains a null ingredient line.");
                    }

                    string ingredientId =
                        BistroBuilderMenuIdUtility.NormalizeStableId(
                            source.ingredientId);

                    if (!seenIngredients.Add(
                            ingredientId))
                    {
                        throw new InvalidOperationException(
                            "Recipe " +
                            pair.Key +
                            " repeats ingredient " +
                            ingredientId +
                            ".");
                    }

                    if (!ingredients.TryGetValue(
                            ingredientId,
                            out BistroBuilderIngredientDefinition ingredient))
                    {
                        throw new InvalidOperationException(
                            "Recipe " +
                            pair.Key +
                            " references missing ingredient " +
                            ingredientId +
                            ".");
                    }

                    BistroBuilderMeasurementUnit unit =
                        ParseEnum<BistroBuilderMeasurementUnit>(
                            source.unit,
                            "recipe measurement unit");

                    lines.Add(
                        new BistroBuilderRecipeIngredientAmount(
                            ingredient,
                            source.amount,
                            unit));
                }

                string path =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetRecipeAssetPath(pair.Key);

                BistroBuilderRecipeDefinition definition =
                    AssetDatabase.LoadAssetAtPath<
                        BistroBuilderRecipeDefinition>(
                        path);

                if (definition == null)
                {
                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        throw new InvalidOperationException(
                            "Incompatible asset already exists at " +
                            path +
                            ".");
                    }

                    definition =
                        ScriptableObject.CreateInstance<
                            BistroBuilderRecipeDefinition>();

                    AssetDatabase.CreateAsset(
                        definition,
                        path);
                }

                definition.InitializeRuntime(
                    pair.Key,
                    dish,
                    record.yieldPortions,
                    record.wasteBasisPoints,
                    lines,
                    record.notes);

                EditorUtility.SetDirty(definition);

                if (!definition.TryValidate(
                        out string error))
                {
                    throw new InvalidOperationException(
                        path +
                        ": " +
                        error);
                }

                catalog.EditorUpsert(
                    definition);

                published++;
            }

            return published;
        }

        private int PublishSuppliers(
            Dictionary<string, SavicSupplierContentRecord> input,
            BistroBuilderSupplierAuthoringDatabase supplierDatabase,
            BistroBuilderIngredientAuthoringDatabase ingredientAuthoring,
            Dictionary<string, BistroBuilderIngredientDefinition> ingredients)
        {
            int published = 0;

            supplierDatabase.EditorEnsureSchema();
            ingredientAuthoring.EditorEnsureSchema();

            foreach (KeyValuePair<string, SavicSupplierContentRecord> pair
                     in input)
            {
                SavicSupplierContentRecord source =
                    pair.Value;

                BistroBuilderSupplierAuthoringRecord target = null;

                for (int index = 0;
                     index < supplierDatabase.EditorSuppliers.Count;
                     index++)
                {
                    BistroBuilderSupplierAuthoringRecord candidate =
                        supplierDatabase.EditorSuppliers[index];

                    if (candidate != null &&
                        string.Equals(
                            candidate.SupplierId,
                            pair.Key,
                            StringComparison.Ordinal))
                    {
                        target = candidate;
                        break;
                    }
                }

                if (target == null)
                {
                    target =
                        new BistroBuilderSupplierAuthoringRecord();

                    target.AssignStableIdOnce(
                        pair.Key);

                    supplierDatabase.EditorSuppliers.Add(
                        target);
                }

                target.displayName =
                    RequireText(
                        source.displayName,
                        "supplier displayName");

                target.shortName =
                    string.IsNullOrWhiteSpace(source.shortName)
                        ? target.displayName
                        : source.shortName.Trim();

                target.description =
                    source.description?.Trim() ??
                    string.Empty;

                target.primaryBrandColor =
                    ParseColor(
                        source.primaryBrandColor,
                        "primaryBrandColor");

                target.secondaryBrandColor =
                    ParseColor(
                        source.secondaryBrandColor,
                        "secondaryBrandColor");

                target.textContrastColor =
                    ParseColor(
                        source.textContrastColor,
                        "textContrastColor");

                target.catalogFlags =
                    ParseFlags<BistroBuilderSupplierCatalogFlags>(
                        source.catalogFlags,
                        "supplier catalog flags");

                target.commercialModelFlags =
                    ParseFlags<BistroBuilderSupplierCommercialModelFlags>(
                        source.commercialModelFlags,
                        "supplier commercial-model flags");

                target.scopeFlags =
                    ParseFlags<BistroBuilderSupplierScopeFlags>(
                        source.scopeFlags,
                        "supplier scope flags");

                target.positioningFlags =
                    ParseFlags<BistroBuilderSupplierPositioningFlags>(
                        source.positioningFlags,
                        "supplier positioning flags");

                target.reliabilityTier =
                    ParseEnum<BistroBuilderSupplierReliabilityTier>(
                        source.reliabilityTier,
                        "supplier reliability tier");

                target.reliabilityValue =
                    source.reliabilityValue;
                target.minimumOrderValueCents =
                    source.minimumOrderValueCents;
                target.shippingCostCents =
                    source.shippingCostCents;
                target.freeShippingEnabled =
                    source.freeShippingEnabled;
                target.freeShippingThresholdCents =
                    source.freeShippingThresholdCents;
                target.defaultLeadTimeGameHours =
                    source.defaultLeadTimeGameHours;
                target.isActive =
                    source.isActive;

                target.logo =
                    string.IsNullOrWhiteSpace(
                        source.logoContentId)
                        ? target.logo
                        : ResolveImageContent(
                            source.logoContentId);

                target.customTags =
                    new List<string>();

                if (source.customTags != null)
                {
                    for (int index = 0;
                         index < source.customTags.Length;
                         index++)
                    {
                        string tag =
                            source.customTags[index]
                                ?.Trim();

                        if (!string.IsNullOrWhiteSpace(tag) &&
                            !target.customTags.Contains(tag))
                        {
                            target.customTags.Add(tag);
                        }
                    }
                }

                if (source.deliveryWindows != null)
                {
                    target.deliveryWindows =
                        new List<
                            BistroBuilderSupplierDeliveryWindowAuthoring>();

                    for (int index = 0;
                         index < source.deliveryWindows.Length;
                         index++)
                    {
                        SavicSupplierDeliveryWindowContentRecord window =
                            source.deliveryWindows[index];

                        if (window == null)
                            continue;

                        target.deliveryWindows.Add(
                            new BistroBuilderSupplierDeliveryWindowAuthoring
                            {
                                startMinuteOfDay =
                                    window.startMinuteOfDay,
                                endMinuteOfDay =
                                    window.endMinuteOfDay,
                                monday = window.monday,
                                tuesday = window.tuesday,
                                wednesday = window.wednesday,
                                thursday = window.thursday,
                                friday = window.friday,
                                saturday = window.saturday,
                                sunday = window.sunday
                            });
                    }
                }

                if (source.baseOffers != null)
                {
                    UpsertSupplierOffers(
                        target,
                        source.baseOffers,
                        ingredientAuthoring,
                        ingredients);
                }

                published++;
            }

            supplierDatabase.EditorTouchRevision();
            ingredientAuthoring.EditorTouchRevision();

            return published;
        }

        private void UpsertIngredientAuthoring(
            BistroBuilderIngredientAuthoringDatabase database,
            BistroBuilderIngredientDefinition canonical,
            SavicIngredientContentRecord source)
        {
            BistroBuilderIngredientAuthoringRecord target = null;

            for (int index = 0;
                 index < database.EditorIngredients.Count;
                 index++)
            {
                BistroBuilderIngredientAuthoringRecord candidate =
                    database.EditorIngredients[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.IngredientId,
                        canonical.IngredientId,
                        StringComparison.Ordinal))
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                target =
                    new BistroBuilderIngredientAuthoringRecord();

                target.AssignStableIdOnce(
                    canonical.IngredientId);

                database.EditorIngredients.Add(
                    target);
            }

            target.RefreshCanonicalSnapshot(
                canonical.DisplayName,
                BistroBuilderMeasurementUtility.GetSymbol(
                    canonical.BaseUnit),
                canonical.Category.ToString());

            target.displayImage =
                string.IsNullOrWhiteSpace(
                    source.imageContentId)
                    ? target.displayImage
                    : ResolveImageContent(
                        source.imageContentId);

            target.isActive = true;

            if (source.commercialPackages != null)
            {
                for (int index = 0;
                     index < source.commercialPackages.Length;
                     index++)
                {
                    SavicCommercialPackageContentRecord packageSource =
                        source.commercialPackages[index];

                    if (packageSource == null)
                        continue;

                    string packageId =
                        NormalizePrefixedId(
                            packageSource.packageFormatId,
                            "package");

                    BistroBuilderCommercialPackageAuthoringRecord package =
                        FindPackage(
                            target,
                            packageId);

                    if (package == null)
                    {
                        package =
                            new BistroBuilderCommercialPackageAuthoringRecord();

                        package.AssignStableIdOnce(
                            packageId);

                        target.commercialPackages.Add(
                            package);
                    }

                    package.displayName =
                        RequireText(
                            packageSource.displayName,
                            "package displayName");
                    package.packageType =
                        RequireText(
                            packageSource.packageType,
                            "package type");
                    package.netQuantityMicrounits =
                        packageSource.netQuantityMicrounits;
                    package.logisticSize =
                        ParseEnum<
                            BistroBuilderCommercialPackageLogisticSize>(
                            packageSource.logisticSize,
                            "package logistic size");
                    package.packageImage =
                        string.IsNullOrWhiteSpace(
                            packageSource.imageContentId)
                            ? package.packageImage
                            : ResolveImageContent(
                                packageSource.imageContentId);
                    package.isActive =
                        packageSource.isActive;
                }
            }

            database.EditorTouchRevision();
        }

        private static void UpsertSupplierOffers(
            BistroBuilderSupplierAuthoringRecord supplier,
            SavicSupplierOfferContentRecord[] sources,
            BistroBuilderIngredientAuthoringDatabase ingredientAuthoring,
            Dictionary<string, BistroBuilderIngredientDefinition> ingredients)
        {
            if (supplier.baseOffers == null)
            {
                supplier.baseOffers =
                    new List<
                        BistroBuilderSupplierBaseOfferAuthoringRecord>();
            }

            for (int index = 0;
                 index < sources.Length;
                 index++)
            {
                SavicSupplierOfferContentRecord source =
                    sources[index];

                if (source == null)
                    continue;

                string ingredientId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        source.ingredientId);

                if (!ingredients.ContainsKey(
                        ingredientId))
                {
                    throw new InvalidOperationException(
                        "Supplier offer references unknown ingredient " +
                        ingredientId +
                        ".");
                }

                string packageId =
                    NormalizePrefixedId(
                        source.packageFormatId,
                        "package");

                if (!ingredientAuthoring.TryGetIngredient(
                        ingredientId,
                        out BistroBuilderIngredientAuthoringRecord
                            ingredientAuthoringRecord) ||
                    FindPackage(
                        ingredientAuthoringRecord,
                        packageId) == null)
                {
                    throw new InvalidOperationException(
                        "Supplier offer references unknown package " +
                        packageId +
                        " for ingredient " +
                        ingredientId +
                        ".");
                }

                string offerId =
                    NormalizePrefixedId(
                        source.supplierOfferId,
                        "offer");

                BistroBuilderSupplierBaseOfferAuthoringRecord target =
                    null;

                for (int currentIndex = 0;
                     currentIndex < supplier.baseOffers.Count;
                     currentIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord candidate =
                        supplier.baseOffers[currentIndex];

                    if (candidate != null &&
                        string.Equals(
                            candidate.SupplierOfferId,
                            offerId,
                            StringComparison.Ordinal))
                    {
                        target = candidate;
                        break;
                    }
                }

                if (target == null)
                {
                    target =
                        new BistroBuilderSupplierBaseOfferAuthoringRecord();

                    target.AssignStableIdOnce(
                        offerId);

                    supplier.baseOffers.Add(
                        target);
                }

                target.ingredientId =
                    ingredientId;
                target.packageFormatId =
                    packageId;
                target.basePriceCents =
                    source.basePriceCents;
                target.minimumPackageCount =
                    source.minimumPackageCount;
                target.orderIncrement =
                    source.orderIncrement;
                target.initialAvailability =
                    ParseEnum<
                        BistroBuilderSupplierOfferAvailability>(
                        source.initialAvailability,
                        "supplier offer availability");
                target.promotionEligible =
                    source.promotionEligible;
                target.overrideLeadTime =
                    source.overrideLeadTime;
                target.leadTimeOverrideGameHours =
                    source.leadTimeOverrideGameHours;
                target.minimumMarketVariationPercent =
                    source.minimumMarketVariationPercent;
                target.maximumMarketVariationPercent =
                    source.maximumMarketVariationPercent;
                target.sortOrder =
                    source.sortOrder;
                target.isActive =
                    source.isActive;
            }
        }

        private Sprite ResolveImageContent(
            string canonicalContentId)
        {
            IReadOnlyList<SavicManifest> all =
                manifests.GetAll();

            for (int index = 0;
                 index < all.Count;
                 index++)
            {
                SavicManifest candidate =
                    all[index];

                if (candidate == null ||
                    !string.Equals(
                        candidate.canonicalContentId,
                        canonicalContentId,
                        StringComparison.Ordinal) ||
                    candidate.imageReadiness == null ||
                    !candidate.imageReadiness.validated ||
                    candidate.imageAuthoring == null ||
                    string.IsNullOrWhiteSpace(
                        candidate.imageAuthoring
                            .publishedAssetPath))
                {
                    continue;
                }

                Sprite sprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        candidate.imageAuthoring
                            .publishedAssetPath);

                if (sprite != null)
                    return sprite;
            }

            throw new InvalidOperationException(
                "Referenced SAVIC image content is not published/readable: " +
                canonicalContentId +
                ".");
        }

        private static void ValidateBundleReferences(
            Dictionary<string, SavicIngredientContentRecord> ingredients,
            Dictionary<string, SavicDishContentRecord> dishes,
            Dictionary<string, SavicRecipeContentRecord> recipes,
            Dictionary<string, SavicSupplierContentRecord> suppliers,
            BistroBuilderIngredientCatalog ingredientCatalog,
            BistroBuilderDishCatalog dishCatalog,
            BistroBuilderDishCategoryCatalog categoryCatalog)
        {
            foreach (KeyValuePair<string, SavicDishContentRecord> pair
                     in dishes)
            {
                string categoryId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        pair.Value.categoryId);

                if (!categoryCatalog.Contains(
                        categoryId))
                {
                    throw new InvalidOperationException(
                        "Unknown dish category: " +
                        categoryId +
                        ".");
                }

                string recipeId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        pair.Value.recipeId);

                if (!string.IsNullOrWhiteSpace(recipeId) &&
                    !recipes.ContainsKey(recipeId))
                {
                    throw new InvalidOperationException(
                        "Dish " +
                        pair.Key +
                        " references recipe " +
                        recipeId +
                        " that is not part of the same transactional bundle.");
                }
            }

            foreach (KeyValuePair<string, SavicRecipeContentRecord> pair
                     in recipes)
            {
                string dishId =
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        pair.Value.dishId);

                if (!dishes.ContainsKey(dishId) &&
                    !dishCatalog.Contains(dishId))
                {
                    throw new InvalidOperationException(
                        "Recipe " +
                        pair.Key +
                        " references unknown dish " +
                        dishId +
                        ".");
                }

                SavicRecipeIngredientContentRecord[] lines =
                    pair.Value.ingredients ??
                    Array.Empty<
                        SavicRecipeIngredientContentRecord>();

                for (int index = 0;
                     index < lines.Length;
                     index++)
                {
                    string ingredientId =
                        BistroBuilderMenuIdUtility.NormalizeStableId(
                            lines[index]?.ingredientId);

                    if (!ingredients.ContainsKey(ingredientId) &&
                        !ingredientCatalog.Contains(ingredientId))
                    {
                        throw new InvalidOperationException(
                            "Recipe " +
                            pair.Key +
                            " references unknown ingredient " +
                            ingredientId +
                            ".");
                    }
                }
            }

            foreach (KeyValuePair<string, SavicSupplierContentRecord> pair
                     in suppliers)
            {
                if (pair.Value.baseOffers == null)
                    continue;

                for (int index = 0;
                     index < pair.Value.baseOffers.Length;
                     index++)
                {
                    SavicSupplierOfferContentRecord offer =
                        pair.Value.baseOffers[index];

                    if (offer == null)
                        continue;

                    string ingredientId =
                        BistroBuilderMenuIdUtility.NormalizeStableId(
                            offer.ingredientId);

                    if (!ingredients.ContainsKey(ingredientId) &&
                        !ingredientCatalog.Contains(ingredientId))
                    {
                        throw new InvalidOperationException(
                            "Supplier " +
                            pair.Key +
                            " references unknown ingredient " +
                            ingredientId +
                            ".");
                    }
                }
            }
        }

        private static void ValidateDishRecipeReferences(
            Dictionary<string, SavicDishContentRecord> publishedDishInput,
            BistroBuilderDishCatalog dishCatalog,
            BistroBuilderRecipeCatalog recipeCatalog)
        {
            foreach (string dishId in publishedDishInput.Keys)
            {
                if (!dishCatalog.TryGetDefinition(
                        dishId,
                        out BistroBuilderDishDefinition dish))
                {
                    throw new InvalidOperationException(
                        "Published dish is missing from DishCatalog: " +
                        dishId +
                        ".");
                }

                if (string.IsNullOrWhiteSpace(
                        dish.RecipeId))
                    continue;

                if (!recipeCatalog.TryGetByRecipeId(
                        dish.RecipeId,
                        out BistroBuilderRecipeDefinition recipe) ||
                    recipe == null ||
                    !string.Equals(
                        recipe.DishId,
                        dish.DishId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Dish/recipe cross-reference is invalid for " +
                        dishId +
                        ".");
                }
            }
        }

        private static void ValidateSupplierReferences(
            BistroBuilderSupplierAuthoringDatabase supplierDatabase,
            BistroBuilderIngredientAuthoringDatabase ingredientAuthoring,
            BistroBuilderIngredientCatalog ingredientCatalog)
        {
            IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers =
                supplierDatabase.Suppliers;

            for (int supplierIndex = 0;
                 supplierIndex < suppliers.Count;
                 supplierIndex++)
            {
                BistroBuilderSupplierAuthoringRecord supplier =
                    suppliers[supplierIndex];

                if (supplier?.baseOffers == null)
                    continue;

                for (int offerIndex = 0;
                     offerIndex < supplier.baseOffers.Count;
                     offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                        supplier.baseOffers[offerIndex];

                    if (offer == null ||
                        !offer.isActive)
                        continue;

                    if (!ingredientCatalog.Contains(
                            offer.ingredientId))
                    {
                        throw new InvalidOperationException(
                            "Supplier offer references missing canonical ingredient " +
                            offer.ingredientId +
                            ".");
                    }

                    if (!ingredientAuthoring.TryGetIngredient(
                            offer.ingredientId,
                            out BistroBuilderIngredientAuthoringRecord
                                ingredientRecord) ||
                        FindPackage(
                            ingredientRecord,
                            offer.packageFormatId) == null)
                    {
                        throw new InvalidOperationException(
                            "Supplier offer references missing package " +
                            offer.packageFormatId +
                            ".");
                    }
                }
            }
        }

        private static Dictionary<string, BistroBuilderIngredientDefinition>
            BuildIngredientIndex(
                BistroBuilderIngredientCatalog catalog)
        {
            Dictionary<string, BistroBuilderIngredientDefinition> result =
                new Dictionary<string, BistroBuilderIngredientDefinition>(
                    StringComparer.Ordinal);

            IReadOnlyList<BistroBuilderIngredientDefinition> definitions =
                catalog.Definitions;

            for (int index = 0;
                 index < definitions.Count;
                 index++)
            {
                BistroBuilderIngredientDefinition definition =
                    definitions[index];

                if (definition != null)
                {
                    result[definition.IngredientId] =
                        definition;
                }
            }

            return result;
        }

        private static Dictionary<string, BistroBuilderDishDefinition>
            BuildDishIndex(
                BistroBuilderDishCatalog catalog)
        {
            Dictionary<string, BistroBuilderDishDefinition> result =
                new Dictionary<string, BistroBuilderDishDefinition>(
                    StringComparer.Ordinal);

            List<BistroBuilderDishDefinition> definitions =
                new List<BistroBuilderDishDefinition>();

            catalog.CopyDefinitionsTo(
                definitions);

            for (int index = 0;
                 index < definitions.Count;
                 index++)
            {
                BistroBuilderDishDefinition definition =
                    definitions[index];

                if (definition != null)
                {
                    result[definition.DishId] =
                        definition;
                }
            }

            return result;
        }

        private static Dictionary<string, SavicIngredientContentRecord>
            IndexIngredients(
                SavicIngredientContentRecord[] source)
        {
            Dictionary<string, SavicIngredientContentRecord> result =
                new Dictionary<string, SavicIngredientContentRecord>(
                    StringComparer.Ordinal);

            if (source == null)
                return result;

            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                SavicIngredientContentRecord record =
                    source[index];

                if (record == null)
                    throw new InvalidOperationException(
                        "Bundle contains a null ingredient record.");

                string id =
                    NormalizeStableIdRequired(
                        record.ingredientId,
                        "ingredientId");

                record.ingredientId = id;

                if (!result.TryAdd(
                        id,
                        record))
                {
                    throw new InvalidOperationException(
                        "Duplicate ingredientId in bundle: " +
                        id +
                        ".");
                }
            }

            return result;
        }

        private static Dictionary<string, SavicDishContentRecord>
            IndexDishes(
                SavicDishContentRecord[] source)
        {
            Dictionary<string, SavicDishContentRecord> result =
                new Dictionary<string, SavicDishContentRecord>(
                    StringComparer.Ordinal);

            if (source == null)
                return result;

            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                SavicDishContentRecord record =
                    source[index];

                if (record == null)
                    throw new InvalidOperationException(
                        "Bundle contains a null dish record.");

                string id =
                    NormalizeStableIdRequired(
                        record.dishId,
                        "dishId");

                record.dishId = id;

                if (!result.TryAdd(
                        id,
                        record))
                {
                    throw new InvalidOperationException(
                        "Duplicate dishId in bundle: " +
                        id +
                        ".");
                }
            }

            return result;
        }

        private static Dictionary<string, SavicRecipeContentRecord>
            IndexRecipes(
                SavicRecipeContentRecord[] source)
        {
            Dictionary<string, SavicRecipeContentRecord> result =
                new Dictionary<string, SavicRecipeContentRecord>(
                    StringComparer.Ordinal);

            if (source == null)
                return result;

            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                SavicRecipeContentRecord record =
                    source[index];

                if (record == null)
                    throw new InvalidOperationException(
                        "Bundle contains a null recipe record.");

                string id =
                    NormalizeStableIdRequired(
                        record.recipeId,
                        "recipeId");

                record.recipeId = id;

                if (!result.TryAdd(
                        id,
                        record))
                {
                    throw new InvalidOperationException(
                        "Duplicate recipeId in bundle: " +
                        id +
                        ".");
                }
            }

            return result;
        }

        private static Dictionary<string, SavicSupplierContentRecord>
            IndexSuppliers(
                SavicSupplierContentRecord[] source)
        {
            Dictionary<string, SavicSupplierContentRecord> result =
                new Dictionary<string, SavicSupplierContentRecord>(
                    StringComparer.Ordinal);

            if (source == null)
                return result;

            for (int index = 0;
                 index < source.Length;
                 index++)
            {
                SavicSupplierContentRecord record =
                    source[index];

                if (record == null)
                    throw new InvalidOperationException(
                        "Bundle contains a null supplier record.");

                string id =
                    NormalizePrefixedId(
                        record.supplierId,
                        "supplier");

                record.supplierId = id;

                if (!result.TryAdd(
                        id,
                        record))
                {
                    throw new InvalidOperationException(
                        "Duplicate supplierId in bundle: " +
                        id +
                        ".");
                }
            }

            return result;
        }

        private static BistroBuilderCommercialPackageAuthoringRecord
            FindPackage(
                BistroBuilderIngredientAuthoringRecord ingredient,
                string packageId)
        {
            if (ingredient?.commercialPackages == null)
                return null;

            for (int index = 0;
                 index < ingredient.commercialPackages.Count;
                 index++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package =
                    ingredient.commercialPackages[index];

                if (package != null &&
                    string.Equals(
                        package.PackageFormatId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    return package;
                }
            }

            return null;
        }

        private static T ParseEnum<T>(
            string value,
            string label)
            where T : struct, Enum
        {
            if (!Enum.TryParse(
                    value?.Trim(),
                    true,
                    out T parsed) ||
                !Enum.IsDefined(
                    typeof(T),
                    parsed))
            {
                throw new InvalidOperationException(
                    "Invalid " +
                    label +
                    ": " +
                    value +
                    ".");
            }

            return parsed;
        }

        private static T ParseFlags<T>(
            string value,
            string label)
            where T : struct, Enum
        {
            string normalized =
                (value ?? string.Empty)
                    .Replace("|", ",")
                    .Trim();

            if (!Enum.TryParse(
                    normalized,
                    true,
                    out T parsed))
            {
                throw new InvalidOperationException(
                    "Invalid " +
                    label +
                    ": " +
                    value +
                    ".");
            }

            ulong raw =
                Convert.ToUInt64(parsed);

            ulong known = 0UL;

            Array values =
                Enum.GetValues(
                    typeof(T));

            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                known |=
                    Convert.ToUInt64(
                        values.GetValue(index));
            }

            if (raw == 0UL ||
                (raw & ~known) != 0UL)
            {
                throw new InvalidOperationException(
                    "Invalid " +
                    label +
                    " mask: " +
                    value +
                    ".");
            }

            return parsed;
        }

        private static Color ParseColor(
            string value,
            string label)
        {
            if (!ColorUtility.TryParseHtmlString(
                    value,
                    out Color color))
            {
                throw new InvalidOperationException(
                    "Invalid " +
                    label +
                    ": " +
                    value +
                    ".");
            }

            return color;
        }

        private static string NormalizeStableIdRequired(
            string value,
            string label)
        {
            string normalized =
                BistroBuilderMenuIdUtility.NormalizeStableId(
                    value);

            if (!BistroBuilderMenuIdUtility.IsValidStableId(
                    normalized))
            {
                throw new InvalidOperationException(
                    "Invalid stable " +
                    label +
                    ": " +
                    value +
                    ".");
            }

            return normalized;
        }

        private static string NormalizePrefixedId(
            string value,
            string prefix)
        {
            string normalized =
                BistroBuilderSupplierAuthoringRecord.NormalizeId(
                    value,
                    prefix);

            if (string.IsNullOrWhiteSpace(
                    normalized))
            {
                throw new InvalidOperationException(
                    "Invalid " +
                    prefix +
                    " id: " +
                    value +
                    ".");
            }

            return normalized;
        }

        private static string RequireText(
            string value,
            string label)
        {
            string trimmed =
                value?.Trim() ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(
                    trimmed))
            {
                throw new InvalidOperationException(
                    "Missing " +
                    label +
                    ".");
            }

            return trimmed;
        }

        private static void SetString(
            SerializedObject target,
            string name,
            string value)
        {
            RequireProperty(target, name).stringValue =
                value?.Trim() ??
                string.Empty;
        }

        private static void SetInt(
            SerializedObject target,
            string name,
            int value)
        {
            RequireProperty(target, name).intValue =
                value;
        }

        private static void SetDouble(
            SerializedObject target,
            string name,
            double value)
        {
            RequireProperty(target, name).doubleValue =
                value;
        }

        private static void SetBool(
            SerializedObject target,
            string name,
            bool value)
        {
            RequireProperty(target, name).boolValue =
                value;
        }

        private static void SetEnum(
            SerializedObject target,
            string name,
            int value)
        {
            RequireProperty(target, name).enumValueIndex =
                value;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject target,
            string name)
        {
            SerializedProperty property =
                target.FindProperty(name);

            if (property == null)
            {
                throw new InvalidOperationException(
                    target.targetObject.GetType().Name +
                    " has no serialized field " +
                    name +
                    ".");
            }

            return property;
        }

        private static string BuildFingerprint(
            SavicManifest manifest,
            SavicContentBundleDocument document)
        {
            return string.Join(
                "|",
                Version,
                manifest.source?.sourceHash ?? string.Empty,
                document.schemaId ?? string.Empty,
                document.schemaVersion,
                document.bundleId ?? string.Empty,
                manifest.contentBundle.ingredientCount,
                manifest.contentBundle.dishCount,
                manifest.contentBundle.recipeCount,
                manifest.contentBundle.supplierCount);
        }

        private static SavicContentBundlePublicationOutcome Failure(
            string message)
        {
            return new SavicContentBundlePublicationOutcome(
                false,
                message);
        }
    }
}
