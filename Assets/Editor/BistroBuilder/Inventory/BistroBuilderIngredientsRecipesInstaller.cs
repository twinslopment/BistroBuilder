using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Instalador acumulativo e idempotente de 368A.
///
/// Instala:
/// - unidades normalizadas,
/// - 22 ingredientes canónicos,
/// - recetas y escandallos para los 8 platos actuales,
/// - servicio runtime de catálogos,
/// - dos sillas visuales y operativas en cada mesa existente.
///
/// La autoría futura se realiza con assets ScriptableObject mediante el
/// estudio 368A; añadir un plato no requiere modificar código.
/// </summary>
public static class BistroBuilderIngredientsRecipesInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Install or Repair 368A Ingredients, Recipes & Visual Chairs";

    private readonly struct IngredientSeed
    {
        public readonly string Id;
        public readonly string Name;
        public readonly BistroBuilderIngredientCategory Category;
        public readonly BistroBuilderIngredientStorageType Storage;
        public readonly BistroBuilderMeasurementUnit BaseUnit;
        public readonly double PackAmount;
        public readonly BistroBuilderMeasurementUnit PackUnit;
        public readonly int PackPriceCents;
        public readonly int ShelfLifeDays;
        public readonly bool Perishable;

        public IngredientSeed(
            string id,
            string name,
            BistroBuilderIngredientCategory category,
            BistroBuilderIngredientStorageType storage,
            BistroBuilderMeasurementUnit baseUnit,
            double packAmount,
            BistroBuilderMeasurementUnit packUnit,
            int packPriceCents,
            int shelfLifeDays,
            bool perishable
        )
        {
            Id = id;
            Name = name;
            Category = category;
            Storage = storage;
            BaseUnit = baseUnit;
            PackAmount = packAmount;
            PackUnit = packUnit;
            PackPriceCents = packPriceCents;
            ShelfLifeDays = shelfLifeDays;
            Perishable = perishable;
        }
    }

    private readonly struct RecipeLineSeed
    {
        public readonly string IngredientId;
        public readonly double Amount;
        public readonly BistroBuilderMeasurementUnit Unit;

        public RecipeLineSeed(
            string ingredientId,
            double amount,
            BistroBuilderMeasurementUnit unit
        )
        {
            IngredientId = ingredientId;
            Amount = amount;
            Unit = unit;
        }
    }

    private sealed class RecipeSeed
    {
        public string RecipeId { get; }
        public string DishId { get; }
        public int YieldPortions { get; }
        public int WasteBasisPoints { get; }
        public string Notes { get; }
        public RecipeLineSeed[] Lines { get; }

        public RecipeSeed(
            string recipeId,
            string dishId,
            int yieldPortions,
            int wasteBasisPoints,
            string notes,
            params RecipeLineSeed[] lines
        )
        {
            RecipeId = recipeId;
            DishId = dishId;
            YieldPortions = yieldPortions;
            WasteBasisPoints = wasteBasisPoints;
            Notes = notes;
            Lines = lines ?? Array.Empty<RecipeLineSeed>();
        }
    }

    private static readonly IngredientSeed[] IngredientSeeds =
    {
        new IngredientSeed(
            "ingredient_fabes",
            "Fabes",
            BistroBuilderIngredientCategory.DryGoods,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            650,
            365,
            false
        ),
        new IngredientSeed(
            "ingredient_chorizo",
            "Chorizo",
            BistroBuilderIngredientCategory.Meat,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            1050,
            30,
            true
        ),
        new IngredientSeed(
            "ingredient_morcilla",
            "Morcilla",
            BistroBuilderIngredientCategory.Meat,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            950,
            21,
            true
        ),
        new IngredientSeed(
            "ingredient_panceta",
            "Panceta",
            BistroBuilderIngredientCategory.Meat,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            850,
            20,
            true
        ),
        new IngredientSeed(
            "ingredient_cebolla",
            "Cebolla",
            BistroBuilderIngredientCategory.Produce,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            180,
            30,
            true
        ),
        new IngredientSeed(
            "ingredient_ajo",
            "Ajo",
            BistroBuilderIngredientCategory.Produce,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            600,
            45,
            true
        ),
        new IngredientSeed(
            "ingredient_aceite_oliva",
            "Aceite de oliva",
            BistroBuilderIngredientCategory.Condiment,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Milliliter,
            1d,
            BistroBuilderMeasurementUnit.Liter,
            900,
            365,
            false
        ),
        new IngredientSeed(
            "ingredient_sal",
            "Sal",
            BistroBuilderIngredientCategory.Condiment,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            80,
            730,
            false
        ),
        new IngredientSeed(
            "ingredient_agua_cocina",
            "Agua de cocina",
            BistroBuilderIngredientCategory.Other,
            BistroBuilderIngredientStorageType.Ambient,
            BistroBuilderMeasurementUnit.Milliliter,
            1d,
            BistroBuilderMeasurementUnit.Liter,
            1,
            0,
            false
        ),
        new IngredientSeed(
            "ingredient_merluza",
            "Merluza",
            BistroBuilderIngredientCategory.FishAndSeafood,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            2200,
            4,
            true
        ),
        new IngredientSeed(
            "ingredient_limon",
            "Limón",
            BistroBuilderIngredientCategory.Produce,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            250,
            14,
            true
        ),
        new IngredientSeed(
            "ingredient_queso_crema",
            "Queso crema",
            BistroBuilderIngredientCategory.DairyAndEggs,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            800,
            15,
            true
        ),
        new IngredientSeed(
            "ingredient_nata",
            "Nata",
            BistroBuilderIngredientCategory.DairyAndEggs,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Milliliter,
            1d,
            BistroBuilderMeasurementUnit.Liter,
            450,
            10,
            true
        ),
        new IngredientSeed(
            "ingredient_huevo",
            "Huevo",
            BistroBuilderIngredientCategory.DairyAndEggs,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Unit,
            12d,
            BistroBuilderMeasurementUnit.Unit,
            300,
            28,
            true
        ),
        new IngredientSeed(
            "ingredient_azucar",
            "Azúcar",
            BistroBuilderIngredientCategory.DryGoods,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            150,
            730,
            false
        ),
        new IngredientSeed(
            "ingredient_galleta",
            "Galleta",
            BistroBuilderIngredientCategory.DryGoods,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            400,
            180,
            false
        ),
        new IngredientSeed(
            "ingredient_mantequilla",
            "Mantequilla",
            BistroBuilderIngredientCategory.DairyAndEggs,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            900,
            45,
            true
        ),
        new IngredientSeed(
            "ingredient_botella_agua_mineral",
            "Botella de agua mineral",
            BistroBuilderIngredientCategory.Beverage,
            BistroBuilderIngredientStorageType.BeverageCellar,
            BistroBuilderMeasurementUnit.Unit,
            24d,
            BistroBuilderMeasurementUnit.Unit,
            960,
            730,
            false
        ),
        new IngredientSeed(
            "ingredient_refresco_lata",
            "Refresco en lata",
            BistroBuilderIngredientCategory.Beverage,
            BistroBuilderIngredientStorageType.BeverageCellar,
            BistroBuilderMeasurementUnit.Unit,
            24d,
            BistroBuilderMeasurementUnit.Unit,
            1440,
            365,
            false
        ),
        new IngredientSeed(
            "ingredient_vino_casa",
            "Vino de la casa",
            BistroBuilderIngredientCategory.Beverage,
            BistroBuilderIngredientStorageType.BeverageCellar,
            BistroBuilderMeasurementUnit.Milliliter,
            750d,
            BistroBuilderMeasurementUnit.Milliliter,
            500,
            5,
            true
        ),
        new IngredientSeed(
            "ingredient_aceitunas_alinadas",
            "Aceitunas aliñadas",
            BistroBuilderIngredientCategory.PreparedProduct,
            BistroBuilderIngredientStorageType.Refrigerated,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            550,
            30,
            true
        ),
        new IngredientSeed(
            "ingredient_patata",
            "Patata",
            BistroBuilderIngredientCategory.Produce,
            BistroBuilderIngredientStorageType.DryStorage,
            BistroBuilderMeasurementUnit.Gram,
            1d,
            BistroBuilderMeasurementUnit.Kilogram,
            140,
            45,
            true
        )
    };

    private static readonly RecipeSeed[] RecipeSeeds =
    {
        new RecipeSeed(
            "recipe_fabada_asturiana",
            "dish_fabada_asturiana",
            1,
            800,
            "Escandallo inicial del prototipo; ajustable por datos.",
            new RecipeLineSeed("ingredient_fabes", 180d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_chorizo", 45d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_morcilla", 35d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_panceta", 40d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_cebolla", 30d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_ajo", 5d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_aceite_oliva", 10d, BistroBuilderMeasurementUnit.Milliliter),
            new RecipeLineSeed("ingredient_sal", 2d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_agua_cocina", 250d, BistroBuilderMeasurementUnit.Milliliter)
        ),
        new RecipeSeed(
            "recipe_merluza_plancha",
            "dish_merluza_plancha",
            1,
            600,
            "Ración individual de merluza a la plancha.",
            new RecipeLineSeed("ingredient_merluza", 220d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_aceite_oliva", 12d, BistroBuilderMeasurementUnit.Milliliter),
            new RecipeLineSeed("ingredient_ajo", 4d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_limon", 25d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_sal", 2d, BistroBuilderMeasurementUnit.Gram)
        ),
        new RecipeSeed(
            "recipe_tarta_queso",
            "dish_tarta_queso",
            1,
            700,
            "Coste por porción de tarta de queso.",
            new RecipeLineSeed("ingredient_queso_crema", 120d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_nata", 60d, BistroBuilderMeasurementUnit.Milliliter),
            new RecipeLineSeed("ingredient_huevo", 1d, BistroBuilderMeasurementUnit.Unit),
            new RecipeLineSeed("ingredient_azucar", 35d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_galleta", 40d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_mantequilla", 20d, BistroBuilderMeasurementUnit.Gram)
        ),
        new RecipeSeed(
            "recipe_agua_mineral",
            "dish_agua_mineral",
            1,
            0,
            "Artículo directo de barra.",
            new RecipeLineSeed("ingredient_botella_agua_mineral", 1d, BistroBuilderMeasurementUnit.Unit)
        ),
        new RecipeSeed(
            "recipe_refresco",
            "dish_refresco",
            1,
            0,
            "Artículo directo de barra.",
            new RecipeLineSeed("ingredient_refresco_lata", 1d, BistroBuilderMeasurementUnit.Unit)
        ),
        new RecipeSeed(
            "recipe_copa_vino",
            "dish_copa_vino",
            1,
            300,
            "Incluye merma orientativa por servicio de botella.",
            new RecipeLineSeed("ingredient_vino_casa", 150d, BistroBuilderMeasurementUnit.Milliliter)
        ),
        new RecipeSeed(
            "recipe_aceitunas_alinadas",
            "dish_aceitunas_alinadas",
            1,
            500,
            "Ración rápida de barra.",
            new RecipeLineSeed("ingredient_aceitunas_alinadas", 90d, BistroBuilderMeasurementUnit.Gram)
        ),
        new RecipeSeed(
            "recipe_pincho_tortilla",
            "dish_pincho_tortilla",
            1,
            1000,
            "Pincho preparado desde ingredientes base; admite huevo fraccionado.",
            new RecipeLineSeed("ingredient_patata", 180d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_huevo", 1.5d, BistroBuilderMeasurementUnit.Unit),
            new RecipeLineSeed("ingredient_cebolla", 35d, BistroBuilderMeasurementUnit.Gram),
            new RecipeLineSeed("ingredient_aceite_oliva", 15d, BistroBuilderMeasurementUnit.Milliliter),
            new RecipeLineSeed("ingredient_sal", 1d, BistroBuilderMeasurementUnit.Gram)
        )
    };

    [MenuItem(MenuPath, false, 300)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 368A.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant.unity antes de " +
                "instalar 368A.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador 368A.",
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();
        InstallationBackup backup = CaptureInstallationBackup(scene);

        try
        {
            ValidatePrerequisites(scene);
            BistroBuilderIngredientsRecipesEditorUtility.EnsureDataFolders();

            var ingredients =
                new Dictionary<string, BistroBuilderIngredientDefinition>(
                    StringComparer.Ordinal
                );

            for (int index = 0; index < IngredientSeeds.Length; index++)
            {
                BistroBuilderIngredientDefinition definition =
                    EnsureIngredient(IngredientSeeds[index]);
                ingredients.Add(definition.IngredientId, definition);
            }

            var dishes =
                new Dictionary<string, BistroBuilderDishDefinition>(
                    StringComparer.Ordinal
                );

            for (int index = 0; index < RecipeSeeds.Length; index++)
            {
                RecipeSeed seed = RecipeSeeds[index];
                BistroBuilderDishDefinition dish =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .FindDishById(seed.DishId);

                if (dish == null)
                {
                    throw new InvalidOperationException(
                        "No existe el plato canónico " + seed.DishId + "."
                    );
                }

                AssignRecipeIdToDish(dish, seed.RecipeId);
                dishes[seed.DishId] = dish;
            }

            for (int index = 0; index < RecipeSeeds.Length; index++)
            {
                EnsureRecipe(
                    RecipeSeeds[index],
                    dishes,
                    ingredients
                );
            }

            BistroBuilderIngredientCatalog ingredientCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .LoadOrCreateIngredientCatalog();
            BistroBuilderRecipeCatalog recipeCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .LoadOrCreateRecipeCatalog();
            BistroBuilderDishCatalog dishCatalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .RequireDishCatalog();

            BistroBuilderIngredientsRecipesEditorUtility
                .RebuildAllCatalogs(
                    ingredientCatalog,
                    recipeCatalog,
                    dishCatalog
                );

            ConfigureRuntimeService(
                scene,
                ingredientCatalog,
                recipeCatalog
            );

            EnsureVisualChairs(scene);

            EditorUtility.SetDirty(ingredientCatalog);
            EditorUtility.SetDirty(recipeCatalog);
            EditorUtility.SetDirty(dishCatalog);
            EditorSceneManager.MarkSceneDirty(scene);

            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 368A."
                );
            }

            AssetDatabase.Refresh();

            BistroBuilderIngredientsRecipesValidationResult result =
                BistroBuilderIngredientsRecipesValidator
                    .ValidateCurrentProject();

            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(result.BuildReport());

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Ingredientes, recetas y sillas visuales 368A instalados.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount +
                "\n\nEjecuta ahora el autotest 368A.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            try
            {
                backup.Restore();
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(
                    scene.path,
                    OpenSceneMode.Single
                );
            }
            catch (Exception rollbackException)
            {
                Debug.LogException(rollbackException);
            }

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 368A ha fallado y se ha restaurado el " +
                "estado anterior.\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static void ValidatePrerequisites(Scene scene)
    {
        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);

        if (gameSystems == null)
        {
            throw new InvalidOperationException(
                "No se encontró GameSystems."
            );
        }

        BistroBuilderDishCatalogService dishCatalogService =
            gameSystems.GetComponent<BistroBuilderDishCatalogService>();

        if (dishCatalogService == null)
        {
            throw new InvalidOperationException(
                "367A no está correctamente instalado: falta " +
                nameof(BistroBuilderDishCatalogService) + "."
            );
        }

        if (!dishCatalogService.ValidateConfiguration(out string error))
        {
            throw new InvalidOperationException(
                "367A no está correctamente instalado: " + error
            );
        }

        RestaurantSeatRegistry seatRegistry =
            gameSystems.GetComponent<RestaurantSeatRegistry>();

        if (seatRegistry == null)
        {
            throw new InvalidOperationException(
                "La base universal de asientos 365 no está instalada."
            );
        }

        GameObject chairPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                BistroBuilderIngredientsRecipesEditorUtility.ChairPrefabPath
            );

        if (chairPrefab == null ||
            chairPrefab.GetComponent<RestaurantSeat>() == null)
        {
            throw new InvalidOperationException(
                "No existe una silla operativa válida en " +
                BistroBuilderIngredientsRecipesEditorUtility.ChairPrefabPath +
                "."
            );
        }
    }

    private static BistroBuilderIngredientDefinition EnsureIngredient(
        IngredientSeed seed
    )
    {
        string path =
            BistroBuilderIngredientsRecipesEditorUtility
                .GetIngredientAssetPath(seed.Id);
        BistroBuilderIngredientDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderIngredientDefinition
            >(path);

        if (definition == null)
        {
            if (File.Exists(Path.GetFullPath(path)))
            {
                throw new InvalidOperationException(
                    "Existe un asset incompatible en " + path + "."
                );
            }

            definition = ScriptableObject.CreateInstance<
                BistroBuilderIngredientDefinition
            >();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject serialized = new SerializedObject(definition);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "ingredientId").stringValue =
                seed.Id;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "displayName").stringValue =
                seed.Name;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "category").enumValueIndex =
                (int)seed.Category;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "storageType").enumValueIndex =
                (int)seed.Storage;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "baseUnit").enumValueIndex =
                (int)seed.BaseUnit;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "referencePackAmount")
            .doubleValue = seed.PackAmount;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "referencePackUnit")
            .enumValueIndex = (int)seed.PackUnit;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "referencePackPriceCents")
            .intValue = seed.PackPriceCents;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "defaultShelfLifeDays")
            .intValue = seed.ShelfLifeDays;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "perishable").boolValue =
                seed.Perishable;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        if (!definition.TryValidate(out string error))
        {
            throw new InvalidOperationException(path + ": " + error);
        }

        return definition;
    }

    private static void AssignRecipeIdToDish(
        BistroBuilderDishDefinition dish,
        string recipeId
    )
    {
        SerializedObject serialized = new SerializedObject(dish);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "recipeId").stringValue = recipeId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dish);

        if (!dish.TryValidate(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static BistroBuilderRecipeDefinition EnsureRecipe(
        RecipeSeed seed,
        Dictionary<string, BistroBuilderDishDefinition> dishes,
        Dictionary<string, BistroBuilderIngredientDefinition> ingredients
    )
    {
        string path =
            BistroBuilderIngredientsRecipesEditorUtility
                .GetRecipeAssetPath(seed.RecipeId);
        BistroBuilderRecipeDefinition definition =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderRecipeDefinition
            >(path);

        if (definition == null)
        {
            if (File.Exists(Path.GetFullPath(path)))
            {
                throw new InvalidOperationException(
                    "Existe un asset incompatible en " + path + "."
                );
            }

            definition = ScriptableObject.CreateInstance<
                BistroBuilderRecipeDefinition
            >();
            AssetDatabase.CreateAsset(definition, path);
        }

        if (!dishes.TryGetValue(
                seed.DishId,
                out BistroBuilderDishDefinition dish
            ))
        {
            throw new InvalidOperationException(
                "Falta el plato " + seed.DishId + "."
            );
        }

        SerializedObject serialized = new SerializedObject(definition);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "recipeId").stringValue =
                seed.RecipeId;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "dish").objectReferenceValue = dish;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "yieldPortions").intValue =
                seed.YieldPortions;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "wasteBasisPoints").intValue =
                seed.WasteBasisPoints;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "notes").stringValue = seed.Notes;

        SerializedProperty lines =
            BistroBuilderIngredientsRecipesEditorUtility
                .RequireProperty(serialized, "ingredients");
        lines.arraySize = seed.Lines.Length;

        for (int index = 0; index < seed.Lines.Length; index++)
        {
            RecipeLineSeed lineSeed = seed.Lines[index];

            if (!ingredients.TryGetValue(
                    lineSeed.IngredientId,
                    out BistroBuilderIngredientDefinition ingredient
                ))
            {
                throw new InvalidOperationException(
                    "Falta el ingrediente " + lineSeed.IngredientId + "."
                );
            }

            SerializedProperty line = lines.GetArrayElementAtIndex(index);
            RequireRelative(line, "ingredient").objectReferenceValue =
                ingredient;
            RequireRelative(line, "amount").doubleValue = lineSeed.Amount;
            RequireRelative(line, "unit").enumValueIndex =
                (int)lineSeed.Unit;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        if (!definition.TryValidate(out string error))
        {
            throw new InvalidOperationException(path + ": " + error);
        }

        return definition;
    }

    private static void ConfigureRuntimeService(
        Scene scene,
        BistroBuilderIngredientCatalog ingredientCatalog,
        BistroBuilderRecipeCatalog recipeCatalog
    )
    {
        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);
        BistroBuilderDishCatalogService dishCatalogService =
            gameSystems.GetComponent<BistroBuilderDishCatalogService>();
        BistroBuilderRecipeCatalogService service =
            BistroBuilderIngredientsRecipesEditorUtility
                .GetOrAddComponent<BistroBuilderRecipeCatalogService>(
                    gameSystems
                );

        SerializedObject serialized = new SerializedObject(service);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "ingredientCatalog")
            .objectReferenceValue = ingredientCatalog;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "recipeCatalog")
            .objectReferenceValue = recipeCatalog;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "dishCatalogService")
            .objectReferenceValue = dishCatalogService;
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "logInitialization")
            .boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(service);
    }

    private static void EnsureVisualChairs(Scene scene)
    {
        GameObject chairPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                BistroBuilderIngredientsRecipesEditorUtility.ChairPrefabPath
            );
        RestaurantTable[] tables =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<RestaurantTable>(scene);

        Array.Sort(
            tables,
            (first, second) => first.TableId.CompareTo(second.TableId)
        );

        if (tables.Length == 0)
        {
            throw new InvalidOperationException(
                "La escena no contiene mesas operativas."
            );
        }

        BistroBuilder368AInstalledChair[] installed =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<BistroBuilder368AInstalledChair>(scene);
        var existing =
            new Dictionary<string, BistroBuilder368AInstalledChair>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < installed.Length; index++)
        {
            BistroBuilder368AInstalledChair marker = installed[index];
            string key = BuildChairKey(marker.TableId, marker.SlotIndex);

            if (!existing.ContainsKey(key))
            {
                existing.Add(key, marker);
            }
            else
            {
                Undo.DestroyObjectImmediate(marker.gameObject);
            }
        }

        var desiredKeys = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<RestaurantTableSeatSlot>(8);

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++)
        {
            RestaurantTable table = tables[tableIndex];
            RestaurantTableSeatingConfiguration configuration =
                table.GetComponent<RestaurantTableSeatingConfiguration>();

            if (configuration == null)
            {
                throw new InvalidOperationException(
                    table.name +
                    " no contiene RestaurantTableSeatingConfiguration."
                );
            }

            if (!configuration.ValidateConfiguration(out string error))
            {
                throw new InvalidOperationException(
                    table.name + ": " + error
                );
            }

            int slotCount = configuration.WriteCurrentSlots(slots);

            if (slotCount != table.Capacity)
            {
                throw new InvalidOperationException(
                    table.name + " declara Capacity=" + table.Capacity +
                    " pero genera " + slotCount + " plazas."
                );
            }

            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                RestaurantTableSeatSlot slot = slots[slotIndex];
                string key = BuildChairKey(table.TableId, slot.SlotIndex);
                desiredKeys.Add(key);

                BistroBuilder368AInstalledChair marker;

                if (!existing.TryGetValue(key, out marker) || marker == null)
                {
                    GameObject chair = (GameObject)PrefabUtility
                        .InstantiatePrefab(chairPrefab, scene);

                    if (chair == null)
                    {
                        throw new InvalidOperationException(
                            "No se pudo instanciar la silla operativa."
                        );
                    }

                    Undo.RegisterCreatedObjectUndo(
                        chair,
                        "Crear silla visual 368A"
                    );
                    marker = chair.GetComponent<
                        BistroBuilder368AInstalledChair
                    >();

                    if (marker == null)
                    {
                        marker = Undo.AddComponent<
                            BistroBuilder368AInstalledChair
                        >(chair);
                    }
                }

                GameObject chairObject = marker.gameObject;
                chairObject.SetActive(true);
                chairObject.name =
                    "BB_368A_Chair_T" + table.TableId.ToString("D2") +
                    "_S" + slot.SlotIndex.ToString("D2");

                if (chairObject.transform.parent != table.transform.parent)
                {
                    Undo.SetTransformParent(
                        chairObject.transform,
                        table.transform.parent,
                        "Agrupar silla visual 368A"
                    );
                }

                marker.EditorAssign(table.TableId, slot.SlotIndex);
                EditorUtility.SetDirty(marker);

                RestaurantSeat seat =
                    chairObject.GetComponent<RestaurantSeat>();

                if (seat == null)
                {
                    throw new InvalidOperationException(
                        chairObject.name + " no contiene RestaurantSeat."
                    );
                }

                if (!seat.ValidateConfiguration(out string seatError))
                {
                    throw new InvalidOperationException(
                        chairObject.name + ": " + seatError
                    );
                }

                Quaternion rotation =
                    seat.CalculateRootRotationForFacingDirection(
                        slot.FacingDirection
                    );
                Vector3 position =
                    seat.CalculateRootPositionForAssociationAtPose(
                        slot.AssociationPosition,
                        rotation
                    );

                Undo.RecordObject(
                    chairObject.transform,
                    "Colocar silla visual 368A"
                );
                chairObject.transform.SetPositionAndRotation(
                    position,
                    rotation
                );

                RestaurantPlaceableObject placeable =
                    chairObject.GetComponent<RestaurantPlaceableObject>();
                placeable?.AssignInstanceId(
                    "placeable_368a_chair_t" +
                    table.TableId.ToString("D2") +
                    "_s" + slot.SlotIndex.ToString("D2")
                );

                RestaurantAreaMember tableAreaMember =
                    table.GetComponent<RestaurantAreaMember>();
                RestaurantAreaMember chairAreaMember =
                    chairObject.GetComponent<RestaurantAreaMember>();

                if (tableAreaMember == null ||
                    tableAreaMember.AssignedArea == null ||
                    chairAreaMember == null)
                {
                    throw new InvalidOperationException(
                        chairObject.name +
                        " no puede heredar el área de su mesa."
                    );
                }

                Undo.RecordObject(
                    chairAreaMember,
                    "Asignar área de silla visual 368A"
                );
                chairAreaMember.SetArea(tableAreaMember.AssignedArea);
                EditorUtility.SetDirty(chairAreaMember);
                EditorUtility.SetDirty(chairObject);
            }
        }

        foreach (KeyValuePair<string, BistroBuilder368AInstalledChair> pair
                 in existing)
        {
            if (!desiredKeys.Contains(pair.Key) && pair.Value != null)
            {
                Undo.DestroyObjectImmediate(pair.Value.gameObject);
            }
        }
    }

    private static InstallationBackup CaptureInstallationBackup(Scene scene)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal)
        {
            scene.path,
            BistroBuilderIngredientsRecipesEditorUtility
                .IngredientCatalogPath,
            BistroBuilderIngredientsRecipesEditorUtility.RecipeCatalogPath,
            BistroBuilderIngredientsRecipesEditorUtility.DishCatalogPath
        };

        for (int index = 0; index < IngredientSeeds.Length; index++)
        {
            paths.Add(
                BistroBuilderIngredientsRecipesEditorUtility
                    .GetIngredientAssetPath(IngredientSeeds[index].Id)
            );
        }

        for (int index = 0; index < RecipeSeeds.Length; index++)
        {
            RecipeSeed seed = RecipeSeeds[index];
            paths.Add(
                BistroBuilderIngredientsRecipesEditorUtility
                    .GetRecipeAssetPath(seed.RecipeId)
            );

            BistroBuilderDishDefinition dish =
                BistroBuilderIngredientsRecipesEditorUtility
                    .FindDishById(seed.DishId);

            if (dish != null)
            {
                paths.Add(AssetDatabase.GetAssetPath(dish));
            }
        }

        return InstallationBackup.Capture(paths);
    }

    private static string BuildChairKey(int tableId, int slotIndex)
    {
        return tableId + ":" + slotIndex;
    }

    private static SerializedProperty RequireRelative(
        SerializedProperty parent,
        string propertyName
    )
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "La línea de receta no contiene " + propertyName + "."
            );
        }

        return property;
    }

    private sealed class InstallationBackup
    {
        private readonly List<FileRecord> records;

        private InstallationBackup(List<FileRecord> records)
        {
            this.records = records;
        }

        public static InstallationBackup Capture(
            IEnumerable<string> assetPaths
        )
        {
            var records = new List<FileRecord>();

            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string absolutePath = Path.GetFullPath(assetPath);
                string metaPath = absolutePath + ".meta";

                records.Add(
                    new FileRecord
                    {
                        AssetPath = assetPath,
                        Existed = File.Exists(absolutePath),
                        AssetBytes = File.Exists(absolutePath)
                            ? File.ReadAllBytes(absolutePath)
                            : null,
                        MetaExisted = File.Exists(metaPath),
                        MetaBytes = File.Exists(metaPath)
                            ? File.ReadAllBytes(metaPath)
                            : null
                    }
                );
            }

            return new InstallationBackup(records);
        }

        public void Restore()
        {
            for (int index = 0; index < records.Count; index++)
            {
                FileRecord record = records[index];
                string absolutePath = Path.GetFullPath(record.AssetPath);
                string metaPath = absolutePath + ".meta";

                if (!record.Existed)
                {
                    AssetDatabase.DeleteAsset(record.AssetPath);
                    continue;
                }

                string directory = Path.GetDirectoryName(absolutePath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(absolutePath, record.AssetBytes);

                if (record.MetaExisted)
                {
                    File.WriteAllBytes(metaPath, record.MetaBytes);
                }
                else if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
        }
    }

    private sealed class FileRecord
    {
        public string AssetPath;
        public bool Existed;
        public byte[] AssetBytes;
        public bool MetaExisted;
        public byte[] MetaBytes;
    }
}
