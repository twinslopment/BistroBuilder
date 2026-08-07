using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista de contratos y migraciones 2.1G3. No modifica assets
/// ni la escena activa y destruye sus objetos temporales al finalizar.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G3SelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1G3 Dish Recipe Persistence Self-Test";

    [MenuItem(MenuPath, false, 196)]
    private static void RunFromMenu()
    {
        TestReport report = Run();
        string text = report.BuildReport();

        if (report.Failed > 0)
        {
            Debug.LogError(text);
        }
        else
        {
            Debug.Log(text);
        }

        EditorUtility.DisplayDialog("Bistro Builder", text, "Aceptar");
    }

    public static TestReport Run()
    {
        TestReport report = new TestReport();
        GameObject root = null;

        try
        {
            report.Check(
                BistroBuilderMenuSaveData.CurrentSchemaVersion >= 4 &&
                BistroBuilderMenuSaveSectionProvider.StableSectionVersion >= 4 &&
                BistroBuilderMenuSaveData.CurrentSchemaVersion ==
                    BistroBuilderMenuSaveSectionProvider.StableSectionVersion,
                "menu.state conserva el contrato de autoría v4 dentro de la versión actual."
            );

            BistroBuilderDishRecipeSaveData pair = CreateValidPair();
            report.Check(
                BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(pair, out _),
                "Un par plato/receta completo cumple el contrato v4."
            );

            BistroBuilderDishRecipeSaveData clone =
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair);
            clone.dish.displayName = "Nombre independiente";
            clone.recipe.ingredients[0].amount = 99d;
            report.Check(
                pair.dish.displayName != clone.dish.displayName &&
                Math.Abs(pair.recipe.ingredients[0].amount - 99d) > 0.001d,
                "El clonado de autoría es profundo e independiente."
            );

            BistroBuilderDishRecipeSaveData mismatch =
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair);
            mismatch.recipe.dishId = "dish_other";
            report.Check(
                !BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(mismatch, out _),
                "Una receta enlazada a otro DishId se rechaza."
            );

            BistroBuilderDishRecipeSaveData duplicateIngredient =
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair);
            duplicateIngredient.recipe.ingredients.Add(
                new BistroBuilderRecipeIngredientSaveData
                {
                    ingredientId =
                        duplicateIngredient.recipe.ingredients[0].ingredientId,
                    amount = 2d,
                    unit = duplicateIngredient.recipe.ingredients[0].unit
                }
            );
            report.Check(
                !BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(duplicateIngredient, out _),
                "Una receta con ingredientes duplicados se rechaza."
            );

            BistroBuilderDishRecipeSaveData duplicateRecipeId =
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair);
            duplicateRecipeId.dish.dishId = "dish_g3_second";
            duplicateRecipeId.recipe.dishId = "dish_g3_second";
            report.Check(
                !BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairCollections(
                        new List<BistroBuilderDishRecipeSaveData> { pair },
                        new List<BistroBuilderDishRecipeSaveData>
                        {
                            duplicateRecipeId
                        },
                        out _
                    ),
                "Dos pares con el mismo RecipeId se rechazan conjuntamente."
            );

            BistroBuilderDishRecipeSaveData excessiveAmount =
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair);
            excessiveAmount.recipe.ingredients[0].amount = double.MaxValue;
            report.Check(
                !BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(excessiveAmount, out _),
                "Una cantidad que desborda las unidades canónicas se rechaza."
            );

            string json = JsonUtility.ToJson(pair, true);
            BistroBuilderDishRecipeSaveData roundTrip =
                JsonUtility.FromJson<BistroBuilderDishRecipeSaveData>(json);
            report.Check(
                roundTrip != null &&
                roundTrip.dish.dishId == pair.dish.dishId &&
                roundTrip.recipe.recipeId == pair.recipe.recipeId &&
                roundTrip.recipe.ingredients.Count == 1 &&
                Math.Abs(
                    roundTrip.recipe.ingredients[0].amount -
                    pair.recipe.ingredients[0].amount
                ) < 0.001d,
                "El round-trip JSON conserva identidad, receta y cantidades."
            );

            root = new GameObject("BB_2.1G3_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            BistroBuilderMenuStateV3ToV4Migration migration =
                root.AddComponent<BistroBuilderMenuStateV3ToV4Migration>();
            BistroBuilderMenuSaveDataV3 legacy = CreateValidV3();
            bool migrated = migration.TryMigrate(
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy)),
                out byte[] payload,
                out _
            );
            BistroBuilderMenuSaveData current = migrated
                ? JsonUtility.FromJson<BistroBuilderMenuSaveData>(
                    Encoding.UTF8.GetString(payload)
                )
                : null;

            report.Check(
                migrated && current != null && current.schemaVersion == 4,
                "La migración consecutiva v3 -> v4 se completa."
            );
            report.Check(
                current != null &&
                current.activeRestaurantId == legacy.activeRestaurantId &&
                current.restaurants.Count == legacy.restaurants.Count &&
                current.restaurants[0].revision ==
                    legacy.restaurants[0].revision,
                "La migración conserva restaurante activo y revisión."
            );
            report.Check(
                current != null &&
                current.restaurants[0].items[0].dishId ==
                    legacy.restaurants[0].items[0].dishId &&
                current.restaurants[0].items[0].currentPriceCents ==
                    legacy.restaurants[0].items[0].currentPriceCents &&
                current.restaurants[0].items[0].basePreparationSeconds ==
                    legacy.restaurants[0].items[0].basePreparationSeconds,
                "La migración conserva carta, precio y preparación."
            );
            report.Check(
                current != null &&
                current.restaurants[0].unresolvedItems.Count == 1 &&
                current.restaurants[0].unresolvedItems[0].dishId ==
                    legacy.restaurants[0].unresolvedItems[0].dishId &&
                current.restaurants[0].unresolvedItems[0]
                    .preparationDifficulty == 0 &&
                current.restaurants[0].unresolvedItems[0]
                    .basePreparationSeconds == 0,
                "La migración conserva íntegramente entradas históricas no resueltas."
            );

            report.Check(
                current != null &&
                current.authoredDishRecipes != null &&
                current.unresolvedAuthoredDishRecipes != null &&
                current.authoredDishRecipes.Count == 0 &&
                current.unresolvedAuthoredDishRecipes.Count == 0,
                "La migración inicializa la autoría vacía sin inventar definiciones."
            );

            BistroBuilderMenuSaveDataV3 invalidLegacy = CreateValidV3();
            invalidLegacy.restaurants[0].items[0].preparationDifficulty = 5;
            invalidLegacy.restaurants[0].items[0].basePreparationSeconds = 0;
            report.Check(
                !migration.TryMigrate(
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(invalidLegacy)),
                    out _,
                    out _
                ),
                "La migración rechaza preparación histórica incoherente."
            );

            BistroBuilderMenuSaveData save = new BistroBuilderMenuSaveData
            {
                authoredDishRecipes =
                    new List<BistroBuilderDishRecipeSaveData> { pair },
                unresolvedAuthoredDishRecipes =
                    new List<BistroBuilderDishRecipeSaveData>
                    {
                        BistroBuilderDishRecipeSaveDataUtility.Clone(pair)
                    }
            };
            save.unresolvedAuthoredDishRecipes[0].dish.dishId =
                "dish_g3_unresolved";
            save.unresolvedAuthoredDishRecipes[0].dish.recipeId =
                "recipe_g3_unresolved";
            save.unresolvedAuthoredDishRecipes[0].recipe.dishId =
                "dish_g3_unresolved";
            save.unresolvedAuthoredDishRecipes[0].recipe.recipeId =
                "recipe_g3_unresolved";
            report.Check(
                BistroBuilderDishRecipeSaveDataUtility.TryValidatePairStructure(
                    save.unresolvedAuthoredDishRecipes[0],
                    out _
                ),
                "La autoría no resuelta conserva un contrato completo y reintentable."
            );

            report.Check(
                typeof(BistroBuilderMenuSaveDataV3) !=
                    typeof(BistroBuilderMenuSaveData) &&
                typeof(BistroBuilderMenuSaveDataV2) !=
                    typeof(BistroBuilderMenuSaveData),
                "Los DTO históricos permanecen separados del contrato actual."
            );
        }
        catch (Exception exception)
        {
            report.Fail("Excepción inesperada: " + exception.Message);
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        return report;
    }

    private static BistroBuilderDishRecipeSaveData CreateValidPair()
    {
        return new BistroBuilderDishRecipeSaveData
        {
            dish = new BistroBuilderDishDefinitionSaveData
            {
                definitionVersion = 1,
                dishId = "dish_g3_selftest",
                displayName = "Plato persistente G3",
                description = "Definición temporal para autotest.",
                categoryId = BistroBuilderDishCategoryIdUtility.MainCourse,
                course = (int)BistroBuilderDishCourse.Main,
                defaultAvailability =
                    (int)BistroBuilderMealServiceAvailability.All,
                allowedServiceModes =
                    (int)BistroBuilderDishServiceModeAvailability.TableService,
                requiredStation =
                    (int)BistroBuilderKitchenStationType.HotKitchen,
                basePreparationSeconds = 420,
                complexity = 6,
                recipeId = "recipe_g3_selftest",
                basePriceCents = 1895,
                shareable = false,
                minimumConsumers = 1,
                maximumConsumers = 1
            },
            recipe = new BistroBuilderRecipeDefinitionSaveData
            {
                definitionVersion = 1,
                recipeId = "recipe_g3_selftest",
                dishId = "dish_g3_selftest",
                yieldPortions = 2,
                wasteBasisPoints = 250,
                notes = "Autotest 2.1G3",
                ingredients = new List<BistroBuilderRecipeIngredientSaveData>
                {
                    new BistroBuilderRecipeIngredientSaveData
                    {
                        ingredientId = "ingredient_g3_test",
                        amount = 125.5d,
                        unit = (int)BistroBuilderMeasurementUnit.Gram
                    }
                }
            }
        };
    }

    private static BistroBuilderMenuSaveDataV3 CreateValidV3()
    {
        return new BistroBuilderMenuSaveDataV3
        {
            schemaVersion = 3,
            activeRestaurantId =
                BistroBuilderRestaurantMenuCollectionService
                    .DefaultRestaurantId,
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>
            {
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId =
                        BistroBuilderRestaurantMenuCollectionService
                            .DefaultRestaurantId,
                    revision = 7,
                    items = new List<BistroBuilderMenuItemSaveData>
                    {
                        new BistroBuilderMenuItemSaveData
                        {
                            dishId = "dish_g3_legacy",
                            currentPriceCents = 1495,
                            unlocked = true,
                            enabled = true,
                            availableServices =
                                (int)BistroBuilderMealServiceAvailability.All,
                            displayOrder = 0,
                            preparationDifficulty = 4,
                            basePreparationSeconds = 360
                        }
                    },
                    unresolvedItems =
                        new List<BistroBuilderMenuItemSaveData>
                        {
                            new BistroBuilderMenuItemSaveData
                            {
                                dishId = "dish_g3_legacy_missing",
                                currentPriceCents = 995,
                                unlocked = true,
                                enabled = true,
                                availableServices =
                                    (int)BistroBuilderMealServiceAvailability.All,
                                displayOrder = 1,
                                preparationDifficulty = 0,
                                basePreparationSeconds = 0
                            }
                        }
                }
            }
        };
    }

    public sealed class TestReport
    {
        private readonly List<string> lines = new List<string>();
        public int Passed { get; private set; }
        public int Failed { get; private set; }

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                lines.Add("- OK: " + message);
            }
            else
            {
                Fail(message);
            }
        }

        public void Fail(string message)
        {
            Failed++;
            lines.Add("- FALLO: " + message);
        }

        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.1G3");
            builder.AppendLine("Pruebas superadas: " + Passed);
            builder.AppendLine("Pruebas fallidas: " + Failed);

            for (int index = 0; index < lines.Count; index++)
            {
                builder.AppendLine(lines[index]);
            }

            return builder.ToString().TrimEnd();
        }
    }
}
