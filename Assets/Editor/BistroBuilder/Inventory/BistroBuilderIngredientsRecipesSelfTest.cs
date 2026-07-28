using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Autotest determinista de conversiones, costes, recetas, catálogos y
/// estructura instalada de 368A.
/// </summary>
public static class BistroBuilderIngredientsRecipesSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Run 368A Ingredients & Recipes Self-Test";

    private sealed class TestResult
    {
        public readonly List<string> Passed = new List<string>();
        public readonly List<string> Failed = new List<string>();

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed.Add(message);
            }
            else
            {
                Failed.Add(message);
            }
        }

        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 368A");
            builder.AppendLine("Pruebas superadas: " + Passed.Count);
            builder.AppendLine("Pruebas fallidas: " + Failed.Count);

            for (int index = 0; index < Passed.Count; index++)
            {
                builder.AppendLine("- OK: " + Passed[index]);
            }

            for (int index = 0; index < Failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + Failed[index]);
            }

            return builder.ToString().TrimEnd();
        }
    }

    [MenuItem(MenuPath, false, 320)]
    public static void Run()
    {
        var result = new TestResult();
        var temporaryObjects = new List<Object>();

        try
        {
            RunMeasurementTests(result);
            RunIngredientAndRecipeTests(result, temporaryObjects);
            RunInstalledProjectTests(result);
        }
        catch (Exception exception)
        {
            result.Failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            for (int index = temporaryObjects.Count - 1;
                 index >= 0;
                 index--)
            {
                if (temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(temporaryObjects[index]);
                }
            }
        }

        string report = result.BuildReport();

        if (result.Failed.Count > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            report,
            "Aceptar"
        );
    }

    private static void RunMeasurementTests(TestResult result)
    {
        bool kilogramConverted =
            BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    1d,
                    BistroBuilderMeasurementUnit.Kilogram,
                    out long kilogram,
                    out _
                );
        bool gramsConverted =
            BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    1000d,
                    BistroBuilderMeasurementUnit.Gram,
                    out long grams,
                    out _
                );

        result.Check(
            kilogramConverted && gramsConverted,
            "Kilogramos y gramos se convierten."
        );
        result.Check(
            kilogram == 1000000L && kilogram == grams,
            "1 kg equivale exactamente a 1000 g."
        );

        bool literConverted =
            BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    1d,
                    BistroBuilderMeasurementUnit.Liter,
                    out long liter,
                    out _
                );
        bool millilitersConverted =
            BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    1000d,
                    BistroBuilderMeasurementUnit.Milliliter,
                    out long milliliters,
                    out _
                );

        result.Check(
            literConverted && millilitersConverted,
            "Litros y mililitros se convierten."
        );
        result.Check(
            liter == 1000000L && liter == milliliters,
            "1 l equivale exactamente a 1000 ml."
        );

        bool fractionalUnitConverted =
            BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    1.5d,
                    BistroBuilderMeasurementUnit.Unit,
                    out long fractionalUnit,
                    out _
                );

        result.Check(
            fractionalUnitConverted && fractionalUnit == 1500L,
            "Las recetas admiten 1,5 unidades sin float autoritativo."
        );
        result.Check(
            !BistroBuilderMeasurementUtility.AreCompatible(
                BistroBuilderMeasurementUnit.Gram,
                BistroBuilderMeasurementUnit.Liter
            ),
            "Masa y volumen se rechazan como unidades incompatibles."
        );
        result.Check(
            !BistroBuilderMeasurementUtility
                .TryConvertToCanonicalMilliUnits(
                    0d,
                    BistroBuilderMeasurementUnit.Gram,
                    out _,
                    out _
                ),
            "Las cantidades nulas se rechazan."
        );
    }

    private static void RunIngredientAndRecipeTests(
        TestResult result,
        List<Object> temporaryObjects
    )
    {
        BistroBuilderIngredientDefinition ingredient =
            ScriptableObject.CreateInstance<
                BistroBuilderIngredientDefinition
            >();
        temporaryObjects.Add(ingredient);
        ConfigureIngredientForTest(ingredient);

        result.Check(
            ingredient.TryValidate(out _),
            "Una definición canónica de ingrediente válida se acepta."
        );

        BistroBuilderMeasurementUtility
            .TryConvertToCanonicalMilliUnits(
                200d,
                BistroBuilderMeasurementUnit.Gram,
                out long required,
                out _
            );
        bool costCalculated = ingredient.TryCalculateCostMicroCents(
            required,
            out long microCents,
            out _
        );

        result.Check(
            costCalculated,
            "El coste proporcional del ingrediente se calcula."
        );
        result.Check(
            microCents == 1000000L,
            "200 g de un envase de 1 kg por 5,00 € cuestan 1,00 €."
        );

        BistroBuilderDishDefinition dish =
            ScriptableObject.CreateInstance<BistroBuilderDishDefinition>();
        temporaryObjects.Add(dish);
        ConfigureDishForTest(dish);

        BistroBuilderRecipeDefinition recipe =
            ScriptableObject.CreateInstance<
                BistroBuilderRecipeDefinition
            >();
        temporaryObjects.Add(recipe);
        ConfigureRecipeForTest(recipe, dish, ingredient);

        result.Check(
            dish.TryValidate(out _),
            "El plato de prueba con RecipeId se valida."
        );
        result.Check(
            recipe.TryValidate(out _),
            "La receta enlazada al plato se valida."
        );
        result.Check(
            recipe.TryCalculateCostPerPortionCents(
                out int costCents,
                out _
            ) && costCents == 100,
            "La receta calcula 1,00 € de coste por ración."
        );

        bool snapshotBuilt =
            BistroBuilderRecipeEconomics.TryBuildSnapshot(
                dish,
                recipe,
                out BistroBuilderRecipeEconomicsSnapshot snapshot,
                out _
            );

        result.Check(
            snapshotBuilt,
            "El escandallo económico se construye."
        );
        result.Check(
            snapshot.GrossMarginCents == 900 &&
            snapshot.GrossMarginBasisPoints == 9000,
            "El margen de 10,00 € sobre 1,00 € se calcula al 90 %."
        );
        result.Check(
            snapshot.MarginBand ==
                BistroBuilderRecipeMarginBand.Excellent,
            "El margen de prueba se clasifica como excelente."
        );

        BistroBuilderIngredientCatalog ingredientCatalog =
            ScriptableObject.CreateInstance<
                BistroBuilderIngredientCatalog
            >();
        temporaryObjects.Add(ingredientCatalog);
        AssignCatalogDefinitions(
            ingredientCatalog,
            "definitions",
            ingredient,
            ingredient
        );

        result.Check(
            !ingredientCatalog.TryRebuildIndex(out _),
            "El catálogo rechaza IngredientId duplicados."
        );

        BistroBuilderRecipeCatalog recipeCatalog =
            ScriptableObject.CreateInstance<BistroBuilderRecipeCatalog>();
        temporaryObjects.Add(recipeCatalog);
        AssignCatalogDefinitions(
            recipeCatalog,
            "definitions",
            recipe,
            recipe
        );

        result.Check(
            !recipeCatalog.TryRebuildIndex(out _),
            "El catálogo rechaza RecipeId duplicados."
        );
    }

    private static void RunInstalledProjectTests(TestResult result)
    {
        BistroBuilderIngredientsRecipesValidationResult validation =
            BistroBuilderIngredientsRecipesValidator
                .ValidateCurrentProject();

        result.Check(
            validation.ErrorCount == 0,
            "La instalación 368A supera el validador estructural."
        );
        result.Check(
            validation.WarningCount == 0,
            "La instalación inicial no genera advertencias económicas."
        );

        BistroBuilderIngredientCatalog ingredientCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientCatalog>(
                BistroBuilderIngredientsRecipesEditorUtility
                    .IngredientCatalogPath
            );
        BistroBuilderRecipeCatalog recipeCatalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderRecipeCatalog>(
                BistroBuilderIngredientsRecipesEditorUtility
                    .RecipeCatalogPath
            );

        result.Check(
            ingredientCatalog != null &&
            ingredientCatalog.DefinitionCount >= 22,
            "El proyecto contiene los 22 ingredientes iniciales."
        );
        result.Check(
            recipeCatalog != null &&
            recipeCatalog.DefinitionCount >= 8,
            "El proyecto contiene recetas para los 8 platos actuales."
        );
    }

    private static void ConfigureIngredientForTest(
        BistroBuilderIngredientDefinition ingredient
    )
    {
        SerializedObject serialized = new SerializedObject(ingredient);
        SetString(serialized, "ingredientId", "ingredient_selftest");
        SetString(serialized, "displayName", "Ingrediente de prueba");
        SetEnum(
            serialized,
            "category",
            (int)BistroBuilderIngredientCategory.DryGoods
        );
        SetEnum(
            serialized,
            "storageType",
            (int)BistroBuilderIngredientStorageType.DryStorage
        );
        SetEnum(
            serialized,
            "baseUnit",
            (int)BistroBuilderMeasurementUnit.Gram
        );
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "referencePackAmount")
            .doubleValue = 1d;
        SetEnum(
            serialized,
            "referencePackUnit",
            (int)BistroBuilderMeasurementUnit.Kilogram
        );
        SetInteger(serialized, "referencePackPriceCents", 500);
        SetInteger(serialized, "defaultShelfLifeDays", 365);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "perishable").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureDishForTest(
        BistroBuilderDishDefinition dish
    )
    {
        SerializedObject serialized = new SerializedObject(dish);
        SetString(serialized, "dishId", "dish_selftest");
        SetString(serialized, "displayName", "Plato de prueba");
        SetString(serialized, "description", "Plato temporal del autotest.");
        SetEnum(
            serialized,
            "category",
            (int)BistroBuilderDishCategory.MainCourse
        );
        SetEnum(
            serialized,
            "course",
            (int)BistroBuilderDishCourse.Main
        );
        SetInteger(
            serialized,
            "defaultAvailability",
            (int)BistroBuilderMealServiceAvailability.Lunch
        );
        SetInteger(
            serialized,
            "allowedServiceModes",
            (int)BistroBuilderDishServiceModeAvailability.TableService
        );
        SetEnum(
            serialized,
            "requiredStation",
            (int)BistroBuilderKitchenStationType.HotKitchen
        );
        SetInteger(serialized, "basePreparationSeconds", 60);
        SetInteger(serialized, "complexity", 1);
        SetString(serialized, "recipeId", "recipe_selftest");
        SetInteger(serialized, "basePriceCents", 1000);
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "shareable").boolValue = false;
        SetInteger(serialized, "minimumConsumers", 1);
        SetInteger(serialized, "maximumConsumers", 1);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRecipeForTest(
        BistroBuilderRecipeDefinition recipe,
        BistroBuilderDishDefinition dish,
        BistroBuilderIngredientDefinition ingredient
    )
    {
        SerializedObject serialized = new SerializedObject(recipe);
        SetString(serialized, "recipeId", "recipe_selftest");
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, "dish").objectReferenceValue = dish;
        SetInteger(serialized, "yieldPortions", 1);
        SetInteger(serialized, "wasteBasisPoints", 0);

        SerializedProperty lines =
            BistroBuilderIngredientsRecipesEditorUtility
                .RequireProperty(serialized, "ingredients");
        lines.arraySize = 1;
        SerializedProperty line = lines.GetArrayElementAtIndex(0);
        line.FindPropertyRelative("ingredient").objectReferenceValue =
            ingredient;
        line.FindPropertyRelative("amount").doubleValue = 200d;
        line.FindPropertyRelative("unit").enumValueIndex =
            (int)BistroBuilderMeasurementUnit.Gram;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignCatalogDefinitions<T>(
        T catalog,
        string propertyName,
        params Object[] definitions
    ) where T : Object
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty list =
            BistroBuilderIngredientsRecipesEditorUtility
                .RequireProperty(serialized, propertyName);
        list.arraySize = definitions.Length;

        for (int index = 0; index < definitions.Length; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(
        SerializedObject serialized,
        string propertyName,
        string value
    )
    {
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, propertyName).stringValue = value;
    }

    private static void SetInteger(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, propertyName).intValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string propertyName,
        int value
    )
    {
        BistroBuilderIngredientsRecipesEditorUtility
            .RequireProperty(serialized, propertyName).enumValueIndex = value;
    }
}
