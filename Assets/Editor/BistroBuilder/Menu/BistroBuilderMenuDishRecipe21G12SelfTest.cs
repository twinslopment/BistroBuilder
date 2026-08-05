using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderMenuDishRecipe21G12SelfTestResult
{
    private readonly List<string> passed = new List<string>();
    private readonly List<string> failed = new List<string>();

    public int PassedCount => passed.Count;
    public int FailedCount => failed.Count;
    public void Pass(string value) => passed.Add(value);
    public void Fail(string value) => failed.Add(value);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.1G1/2");
        builder.AppendLine("Pruebas superadas: " + PassedCount);
        builder.AppendLine("Pruebas fallidas: " + FailedCount);
        Append(builder, "OK", passed);
        Append(builder, "FALLO", failed);
        return builder.ToString().TrimEnd();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(values[index]);
        }
    }
}

/// <summary>
/// Autotest no destructivo de contratos de dominio y clonación 2.1G1/2.
/// </summary>
public static class BistroBuilderMenuDishRecipe21G12SelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1G1-2 Dish and Recipe Self-Test";

    [MenuItem(MenuPath, false, 192)]
    private static void RunFromMenu()
    {
        BistroBuilderMenuDishRecipe21G12SelfTestResult result = Run();
        string report = result.BuildReport();

        if (result.FailedCount > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderMenuDishRecipe21G12SelfTestResult Run()
    {
        BistroBuilderMenuDishRecipe21G12SelfTestResult result =
            new BistroBuilderMenuDishRecipe21G12SelfTestResult();
        BistroBuilderIngredientDefinition ingredient = FindIngredient();

        Check(
            result,
            ingredient != null,
            "Existe al menos un ingrediente canónico para construir recetas."
        );

        if (ingredient == null)
        {
            return result;
        }

        BistroBuilderDishDefinition dish = null;
        BistroBuilderRecipeDefinition recipe = null;
        BistroBuilderDishDefinition clonedDish = null;
        BistroBuilderRecipeDefinition clonedRecipe = null;

        try
        {
            dish = BistroBuilderDishDefinition.CreateRuntime(
                "dish_autotest_g12",
                "Plato autotest",
                "Definición runtime no persistente.",
                BistroBuilderDishCategoryIdUtility.MainCourse,
                BistroBuilderDishCourse.Main,
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderDishServiceModeAvailability.TableService,
                BistroBuilderKitchenStationType.HotKitchen,
                420,
                6,
                "recipe_autotest_g12",
                1950
            );
            Check(
                result,
                dish.TryValidate(out _),
                "La fábrica runtime crea un plato válido."
            );
            Check(
                result,
                dish.hideFlags == HideFlags.DontSave,
                "El plato runtime no puede guardarse como asset accidentalmente."
            );

            BistroBuilderRecipeIngredientAmount line =
                new BistroBuilderRecipeIngredientAmount(
                    ingredient,
                    125d,
                    ingredient.BaseUnit
                );
            Check(
                result,
                line.TryGetCanonicalMilliUnits(out long milliUnits, out _) &&
                milliUnits > 0L,
                "La línea editable convierte cantidades a unidades canónicas."
            );

            recipe = BistroBuilderRecipeDefinition.CreateRuntime(
                "recipe_autotest_g12",
                dish,
                2,
                500,
                new List<BistroBuilderRecipeIngredientAmount> { line },
                "Autotest"
            );
            Check(
                result,
                recipe.TryValidate(out _),
                "La fábrica runtime crea una receta válida y costeable."
            );
            Check(
                result,
                recipe.hideFlags == HideFlags.DontSave,
                "La receta runtime no puede guardarse como asset accidentalmente."
            );

            clonedDish = dish.CloneRuntime();
            clonedRecipe = recipe.CloneRuntime(clonedDish);
            Check(
                result,
                !ReferenceEquals(dish, clonedDish) &&
                !ReferenceEquals(recipe, clonedRecipe) &&
                ReferenceEquals(clonedRecipe.Dish, clonedDish) &&
                clonedRecipe.TryValidate(out _),
                "La clonación runtime conserva identidad y referencias coherentes."
            );

            BistroBuilderRecipeDefinition duplicateRecipe =
                BistroBuilderRecipeDefinition.CreateRuntime(
                    "recipe_autotest_g12",
                    dish,
                    1,
                    0,
                    new List<BistroBuilderRecipeIngredientAmount>
                    {
                        line,
                        line.Clone()
                    },
                    string.Empty
                );
            Check(
                result,
                !duplicateRecipe.TryValidate(out _),
                "Una receta rechaza ingredientes duplicados."
            );
            UnityEngine.Object.DestroyImmediate(duplicateRecipe);

            BistroBuilderDishRecipeAuthoringRequest request =
                new BistroBuilderDishRecipeAuthoringRequest
                {
                    DishId = dish.DishId,
                    DisplayName = dish.DisplayName,
                    CategoryId = dish.CategoryId,
                    BasePriceCents = dish.BasePriceCents
                };
            request.Ingredients.Add(
                new BistroBuilderRecipeIngredientDraft(
                    ingredient.IngredientId,
                    50d,
                    ingredient.BaseUnit
                )
            );
            BistroBuilderDishRecipeAuthoringRequest clone = request.Clone();
            clone.Ingredients[0].Amount = 75d;
            Check(
                result,
                request.Ingredients[0].Amount == 50d &&
                clone.Ingredients[0].Amount == 75d,
                "El formulario de autoría realiza una copia profunda de la receta."
            );

            Check(
                result,
                BistroBuilderMenuIdUtility.IsValidStableId(
                    BistroBuilderMenuIdUtility.NormalizeStableId(
                        "Plato Nuevo de Prueba"
                    )
                ),
                "Los nombres de jugador pueden convertirse en identidades estables."
            );

            MethodInfo apply = typeof(BistroBuilderDishRecipeAuthoringService)
                .GetMethod("TryApplyRuntime");
            MethodInfo rollback = typeof(BistroBuilderDishRecipeAuthoringService)
                .GetMethod("Rollback");
            Check(
                result,
                apply != null && rollback != null,
                "La aplicación de autoría publica rollback explícito."
            );

            Check(
                result,
                BistroBuilderMenuSaveData.CurrentSchemaVersion == 3,
                "G1/2 mantiene menu.state v3 hasta implementar G3."
            );
        }
        catch (Exception exception)
        {
            result.Fail("Excepción inesperada: " + exception.Message);
        }
        finally
        {
            Destroy(clonedRecipe);
            Destroy(clonedDish);
            Destroy(recipe);
            Destroy(dish);
        }

        return result;
    }

    private static BistroBuilderIngredientDefinition FindIngredient()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderIngredientDefinition"
        );

        for (int index = 0; index < guids.Length; index++)
        {
            BistroBuilderIngredientDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientDefinition
                >(AssetDatabase.GUIDToAssetPath(guids[index]));

            if (definition != null && definition.TryValidate(out _))
            {
                return definition;
            }
        }

        return null;
    }

    private static void Check(
        BistroBuilderMenuDishRecipe21G12SelfTestResult result,
        bool condition,
        string message
    )
    {
        if (condition)
        {
            result.Pass(message);
        }
        else
        {
            result.Fail(message);
        }
    }

    private static void Destroy(UnityEngine.Object value)
    {
        if (value != null)
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
