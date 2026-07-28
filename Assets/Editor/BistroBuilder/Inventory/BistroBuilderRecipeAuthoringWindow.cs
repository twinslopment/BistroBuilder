using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estudio de autoría 368A para crear ingredientes, platos y recetas sin
/// modificar código. Mantiene IDs estables, dinero en céntimos, cantidades
/// con unidades compatibles y sincroniza la nueva definición con la carta
/// de la escena abierta.
/// </summary>
public sealed class BistroBuilderRecipeAuthoringWindow : EditorWindow
{
    [Serializable]
    private sealed class IngredientRowDraft
    {
        public BistroBuilderIngredientDefinition Ingredient;
        public double Amount = 1d;
        public BistroBuilderMeasurementUnit Unit =
            BistroBuilderMeasurementUnit.Gram;
    }

    private static readonly CultureInfo SpanishCulture =
        CultureInfo.GetCultureInfo("es-ES");

    private readonly List<IngredientRowDraft> ingredientRows =
        new List<IngredientRowDraft>();

    private readonly string[] tabs =
    {
        "Ingredientes",
        "Platos y recetas"
    };

    private int selectedTab;
    private Vector2 scrollPosition;
    private string statusMessage = string.Empty;
    private MessageType statusType = MessageType.Info;

    // Ingrediente.
    private BistroBuilderIngredientDefinition selectedIngredient;
    private string ingredientId = string.Empty;
    private string ingredientDisplayName = string.Empty;
    private BistroBuilderIngredientCategory ingredientCategory =
        BistroBuilderIngredientCategory.Other;
    private BistroBuilderIngredientStorageType ingredientStorage =
        BistroBuilderIngredientStorageType.DryStorage;
    private BistroBuilderMeasurementUnit ingredientBaseUnit =
        BistroBuilderMeasurementUnit.Gram;
    private double referencePackAmount = 1d;
    private BistroBuilderMeasurementUnit referencePackUnit =
        BistroBuilderMeasurementUnit.Kilogram;
    private double referencePackPriceEuros;
    private int shelfLifeDays;
    private bool perishable;

    // Plato y receta.
    private BistroBuilderDishDefinition selectedDish;
    private string dishId = string.Empty;
    private string recipeId = string.Empty;
    private string dishDisplayName = string.Empty;
    private string dishDescription = string.Empty;
    private BistroBuilderDishCategory dishCategory =
        BistroBuilderDishCategory.MainCourse;
    private BistroBuilderDishCourse dishCourse =
        BistroBuilderDishCourse.Main;
    private BistroBuilderMealServiceAvailability mealAvailability =
        BistroBuilderMealServiceAvailability.Lunch |
        BistroBuilderMealServiceAvailability.Dinner;
    private BistroBuilderDishServiceModeAvailability serviceModes =
        BistroBuilderDishServiceModeAvailability.TableService;
    private BistroBuilderKitchenStationType kitchenStation =
        BistroBuilderKitchenStationType.HotKitchen;
    private int preparationSeconds = 300;
    private int complexity = 1;
    private double salePriceEuros = 10d;
    private bool shareable;
    private int minimumConsumers = 1;
    private int maximumConsumers = 1;
    private int yieldPortions = 1;
    private double wastePercent;
    private string recipeNotes = string.Empty;

    [MenuItem(
        "Tools/Bistro Builder/Content/Dish & Recipe Studio 368A",
        false,
        3680
    )]
    public static void Open()
    {
        BistroBuilderRecipeAuthoringWindow window =
            GetWindow<BistroBuilderRecipeAuthoringWindow>();
        window.titleContent = new GUIContent("BB Dish & Recipe Studio");
        window.minSize = new Vector2(650f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        if (ingredientRows.Count == 0)
        {
            ingredientRows.Add(new IngredientRowDraft());
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Bistro Builder — Estudio de ingredientes, platos y recetas",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Crea contenido canónico sin tocar scripts. El precio de un " +
            "ingrediente se introduce como envase de compra (por ejemplo, " +
            "1 kg por 6,00 €). El sistema calcula automáticamente el coste " +
            "por ración y el margen del plato.",
            MessageType.Info
        );

        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        EditorGUILayout.Space(6f);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (selectedTab == 0)
        {
            DrawIngredientTab();
        }
        else
        {
            DrawDishRecipeTab();
        }

        EditorGUILayout.EndScrollView();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    private void DrawIngredientTab()
    {
        EditorGUILayout.LabelField(
            "1. Ingrediente canónico",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();
        BistroBuilderIngredientDefinition nextSelection =
            (BistroBuilderIngredientDefinition)EditorGUILayout.ObjectField(
                "Ingrediente existente",
                selectedIngredient,
                typeof(BistroBuilderIngredientDefinition),
                false
            );

        if (EditorGUI.EndChangeCheck())
        {
            selectedIngredient = nextSelection;

            if (selectedIngredient != null)
            {
                LoadIngredient(selectedIngredient);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Nuevo ingrediente"))
            {
                ResetIngredientDraft();
            }

            using (new EditorGUI.DisabledScope(selectedIngredient == null))
            {
                if (GUILayout.Button("Recargar seleccionado"))
                {
                    LoadIngredient(selectedIngredient);
                }
            }
        }

        EditorGUILayout.Space(8f);
        ingredientId = EditorGUILayout.TextField(
            new GUIContent(
                "IngredientId estable",
                "Ejemplo: ingredient_tomate. No debe cambiar después de " +
                "publicar partidas que lo referencien."
            ),
            ingredientId
        );
        ingredientDisplayName = EditorGUILayout.TextField(
            "Nombre visible",
            ingredientDisplayName
        );
        ingredientCategory =
            (BistroBuilderIngredientCategory)EditorGUILayout.EnumPopup(
                "Categoría",
                ingredientCategory
            );
        ingredientStorage =
            (BistroBuilderIngredientStorageType)EditorGUILayout.EnumPopup(
                "Almacenamiento",
                ingredientStorage
            );
        ingredientBaseUnit =
            (BistroBuilderMeasurementUnit)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Unidad base de inventario",
                    "Debe ser g, ml, unidad o ración."
                ),
                ingredientBaseUnit
            );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Envase de compra de referencia",
            EditorStyles.boldLabel
        );
        referencePackAmount = Math.Max(
            0.001d,
            EditorGUILayout.DoubleField(
                "Cantidad del envase",
                referencePackAmount
            )
        );
        referencePackUnit =
            (BistroBuilderMeasurementUnit)EditorGUILayout.EnumPopup(
                "Unidad del envase",
                referencePackUnit
            );
        referencePackPriceEuros = Math.Max(
            0d,
            EditorGUILayout.DoubleField(
                "Precio del envase (€)",
                referencePackPriceEuros
            )
        );

        EditorGUILayout.Space(8f);
        perishable = EditorGUILayout.Toggle("Perecedero", perishable);
        shelfLifeDays = Mathf.Max(
            0,
            EditorGUILayout.IntField(
                "Vida útil predeterminada (días)",
                shelfLifeDays
            )
        );

        DrawIngredientCompatibilityPreview();

        EditorGUILayout.Space(10f);
        if (GUILayout.Button(
                "Crear o actualizar ingrediente",
                GUILayout.Height(32f)
            ))
        {
            SaveIngredient();
        }
    }

    private void DrawIngredientCompatibilityPreview()
    {
        if (!BistroBuilderMeasurementUtility.IsCanonicalBaseUnit(
                ingredientBaseUnit
            ))
        {
            EditorGUILayout.HelpBox(
                "La unidad base debe ser Gram, Milliliter, Unit o Portion.",
                MessageType.Error
            );
            return;
        }

        if (!BistroBuilderMeasurementUtility.AreCompatible(
                ingredientBaseUnit,
                referencePackUnit
            ))
        {
            EditorGUILayout.HelpBox(
                "La unidad del envase no es compatible con la unidad base.",
                MessageType.Error
            );
            return;
        }

        int cents;

        try
        {
            cents = BistroBuilderIngredientsRecipesEditorUtility
                .EurosToCents(referencePackPriceEuros);
        }
        catch (Exception)
        {
            EditorGUILayout.HelpBox(
                "El precio indicado queda fuera del rango permitido.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.HelpBox(
            "Referencia económica: " +
            referencePackAmount.ToString("0.###", SpanishCulture) + " " +
            referencePackUnit + " por " + FormatEuros(cents) + ".",
            MessageType.None
        );
    }

    private void DrawDishRecipeTab()
    {
        EditorGUILayout.LabelField(
            "2. Plato canónico y receta",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();
        BistroBuilderDishDefinition nextSelection =
            (BistroBuilderDishDefinition)EditorGUILayout.ObjectField(
                "Plato existente",
                selectedDish,
                typeof(BistroBuilderDishDefinition),
                false
            );

        if (EditorGUI.EndChangeCheck())
        {
            selectedDish = nextSelection;

            if (selectedDish != null)
            {
                LoadDishAndRecipe(selectedDish);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Nuevo plato"))
            {
                ResetDishDraft();
            }

            using (new EditorGUI.DisabledScope(selectedDish == null))
            {
                if (GUILayout.Button("Recargar seleccionado"))
                {
                    LoadDishAndRecipe(selectedDish);
                }
            }

            using (new EditorGUI.DisabledScope(
                       string.IsNullOrWhiteSpace(dishDisplayName)
                   ))
            {
                if (GUILayout.Button("Generar IDs desde nombre"))
                {
                    GenerateDishIdsFromName();
                }
            }
        }

        EditorGUILayout.Space(8f);
        dishId = EditorGUILayout.TextField(
            new GUIContent(
                "DishId estable",
                "Ejemplo: dish_cachopo. Es la identidad usada por carta, " +
                "comandas y guardado."
            ),
            dishId
        );
        recipeId = EditorGUILayout.TextField(
            new GUIContent(
                "RecipeId estable",
                "Ejemplo: recipe_cachopo."
            ),
            recipeId
        );
        dishDisplayName = EditorGUILayout.TextField(
            "Nombre visible",
            dishDisplayName
        );
        EditorGUILayout.LabelField("Descripción");
        dishDescription = EditorGUILayout.TextArea(
            dishDescription,
            GUILayout.MinHeight(48f)
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Clasificación", EditorStyles.boldLabel);
        dishCategory =
            (BistroBuilderDishCategory)EditorGUILayout.EnumPopup(
                "Categoría",
                dishCategory
            );
        dishCourse =
            (BistroBuilderDishCourse)EditorGUILayout.EnumPopup(
                "Pase",
                dishCourse
            );
        mealAvailability =
            (BistroBuilderMealServiceAvailability)
            EditorGUILayout.EnumFlagsField(
                "Servicios del día",
                mealAvailability
            );
        serviceModes =
            (BistroBuilderDishServiceModeAvailability)
            EditorGUILayout.EnumFlagsField(
                "Modalidades de servicio",
                serviceModes
            );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Producción", EditorStyles.boldLabel);
        kitchenStation =
            (BistroBuilderKitchenStationType)EditorGUILayout.EnumPopup(
                "Estación principal",
                kitchenStation
            );
        preparationSeconds = Mathf.Clamp(
            EditorGUILayout.IntField(
                "Tiempo base (segundos)",
                preparationSeconds
            ),
            1,
            BistroBuilderDishDefinition.MaximumPreparationSeconds
        );
        complexity = EditorGUILayout.IntSlider(
            "Complejidad",
            complexity,
            1,
            10
        );
        salePriceEuros = Math.Max(
            0d,
            EditorGUILayout.DoubleField(
                "Precio de venta (€)",
                salePriceEuros
            )
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Consumo", EditorStyles.boldLabel);
        shareable = EditorGUILayout.Toggle("Compartible", shareable);

        using (new EditorGUI.DisabledScope(!shareable))
        {
            minimumConsumers = Mathf.Max(
                1,
                EditorGUILayout.IntField(
                    "Consumidores mínimos",
                    minimumConsumers
                )
            );
            maximumConsumers = Mathf.Max(
                minimumConsumers,
                EditorGUILayout.IntField(
                    "Consumidores máximos",
                    maximumConsumers
                )
            );
        }

        if (!shareable)
        {
            minimumConsumers = 1;
            maximumConsumers = 1;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Rendimiento", EditorStyles.boldLabel);
        yieldPortions = Mathf.Clamp(
            EditorGUILayout.IntField(
                "Raciones producidas",
                yieldPortions
            ),
            1,
            BistroBuilderRecipeDefinition.MaximumYieldPortions
        );
        wastePercent = Math.Max(
            0d,
            Math.Min(
                100d,
                EditorGUILayout.DoubleField(
                    "Merma estimada (%)",
                    wastePercent
                )
            )
        );

        EditorGUILayout.Space(8f);
        DrawIngredientRows();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Notas de autoría");
        recipeNotes = EditorGUILayout.TextArea(
            recipeNotes,
            GUILayout.MinHeight(45f)
        );

        DrawEconomicsPreview();

        EditorGUILayout.Space(10f);
        if (GUILayout.Button(
                "Crear o actualizar plato y receta",
                GUILayout.Height(36f)
            ))
        {
            SaveDishAndRecipe();
        }
    }

    private void DrawIngredientRows()
    {
        EditorGUILayout.LabelField(
            "Ingredientes y cantidades",
            EditorStyles.boldLabel
        );

        int removeIndex = -1;

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            IngredientRowDraft row = ingredientRows[index];

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Ingrediente " + (index + 1),
                        EditorStyles.boldLabel
                    );

                    if (GUILayout.Button("Eliminar", GUILayout.Width(75f)))
                    {
                        removeIndex = index;
                    }
                }

                row.Ingredient =
                    (BistroBuilderIngredientDefinition)
                    EditorGUILayout.ObjectField(
                        "Definición",
                        row.Ingredient,
                        typeof(BistroBuilderIngredientDefinition),
                        false
                    );
                row.Amount = Math.Max(
                    0.001d,
                    EditorGUILayout.DoubleField("Cantidad", row.Amount)
                );
                row.Unit =
                    (BistroBuilderMeasurementUnit)EditorGUILayout.EnumPopup(
                        "Unidad",
                        row.Unit
                    );

                if (row.Ingredient != null &&
                    !BistroBuilderMeasurementUtility.AreCompatible(
                        row.Ingredient.BaseUnit,
                        row.Unit
                    ))
                {
                    EditorGUILayout.HelpBox(
                        "La unidad elegida no es compatible con " +
                        row.Ingredient.DisplayName + ".",
                        MessageType.Error
                    );
                }
            }
        }

        if (removeIndex >= 0)
        {
            ingredientRows.RemoveAt(removeIndex);
        }

        if (ingredientRows.Count == 0)
        {
            ingredientRows.Add(new IngredientRowDraft());
        }

        if (GUILayout.Button("Añadir ingrediente"))
        {
            ingredientRows.Add(new IngredientRowDraft());
        }
    }

    private void DrawEconomicsPreview()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Escandallo automático",
            EditorStyles.boldLabel
        );

        if (!TryCalculateDraftEconomics(
                out int costCents,
                out int saleCents,
                out int marginCents,
                out int marginBasisPoints,
                out BistroBuilderRecipeMarginBand band,
                out string error
            ))
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "Coste por ración: " + FormatEuros(costCents) + "\n" +
            "Precio de venta: " + FormatEuros(saleCents) + "\n" +
            "Margen bruto: " + FormatEuros(marginCents) + " (" +
            (marginBasisPoints / 100d).ToString("0.0", SpanishCulture) +
            " %)\n" +
            "Indicador: " + TranslateMarginBand(band),
            marginCents < 0 ? MessageType.Error : MessageType.Info
        );
    }

    private void SaveIngredient()
    {
        ClearStatus();
        string normalizedId =
            BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedId))
        {
            SetStatus(
                "El IngredientId no es válido. Usa un identificador como " +
                "ingredient_tomate.",
                MessageType.Error
            );
            return;
        }

        if (selectedIngredient != null &&
            !string.Equals(
                selectedIngredient.IngredientId,
                normalizedId,
                StringComparison.Ordinal
            ))
        {
            SetStatus(
                "No se puede cambiar el ID estable de un ingrediente " +
                "existente. Crea uno nuevo.",
                MessageType.Error
            );
            return;
        }

        int priceCents;

        try
        {
            priceCents = BistroBuilderIngredientsRecipesEditorUtility
                .EurosToCents(referencePackPriceEuros);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, MessageType.Error);
            return;
        }

        BistroBuilderIngredientsRecipesEditorUtility.EnsureDataFolders();
        BistroBuilderIngredientDefinition existing =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindIngredientById(normalizedId);
        BistroBuilderIngredientDefinition target = existing;
        bool created = false;
        string createdPath = string.Empty;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Autoría de ingrediente 368A");

        try
        {
            if (target == null)
            {
                target = ScriptableObject.CreateInstance<
                    BistroBuilderIngredientDefinition
                >();
                created = true;
            }
            else
            {
                Undo.RecordObject(target, "Actualizar ingrediente 368A");
            }

            WriteIngredientFields(target, normalizedId, priceCents);

            if (!target.TryValidate(out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            if (created)
            {
                createdPath =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetIngredientAssetPath(normalizedId);

                if (AssetDatabase.LoadMainAssetAtPath(createdPath) != null)
                {
                    throw new InvalidOperationException(
                        "La ruta de asset ya está ocupada: " + createdPath
                    );
                }

                AssetDatabase.CreateAsset(target, createdPath);
                Undo.RegisterCreatedObjectUndo(
                    target,
                    "Crear ingrediente 368A"
                );
            }

            BistroBuilderIngredientCatalog catalog =
                BistroBuilderIngredientsRecipesEditorUtility
                    .LoadOrCreateIngredientCatalog();
            Undo.RecordObject(catalog, "Actualizar catálogo 368A");
            BistroBuilderIngredientsRecipesEditorUtility
                .RebuildIngredientCatalog(catalog);

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);

            selectedIngredient = target;
            ingredientId = target.IngredientId;
            Selection.activeObject = target;
            SetStatus(
                "Ingrediente guardado y catálogo reconstruido: " +
                target.DisplayName + ".",
                MessageType.Info
            );
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);

            if (created && !string.IsNullOrWhiteSpace(createdPath))
            {
                AssetDatabase.DeleteAsset(createdPath);
            }
            else if (created && target != null)
            {
                DestroyImmediate(target);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetStatus(exception.Message, MessageType.Error);
        }
    }

    private void SaveDishAndRecipe()
    {
        ClearStatus();

        string normalizedDishId =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        string normalizedRecipeId =
            BistroBuilderMenuIdUtility.NormalizeStableId(recipeId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedDishId))
        {
            SetStatus(
                "El DishId no es válido. Usa un identificador como " +
                "dish_cachopo.",
                MessageType.Error
            );
            return;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedRecipeId))
        {
            SetStatus(
                "El RecipeId no es válido. Usa un identificador como " +
                "recipe_cachopo.",
                MessageType.Error
            );
            return;
        }

        if (selectedDish != null &&
            !string.Equals(
                selectedDish.DishId,
                normalizedDishId,
                StringComparison.Ordinal
            ))
        {
            SetStatus(
                "No se puede cambiar el DishId estable de un plato " +
                "existente. Crea uno nuevo.",
                MessageType.Error
            );
            return;
        }

        if (selectedDish != null &&
            !string.IsNullOrWhiteSpace(selectedDish.RecipeId) &&
            !string.Equals(
                selectedDish.RecipeId,
                normalizedRecipeId,
                StringComparison.Ordinal
            ))
        {
            SetStatus(
                "No se puede sustituir el RecipeId estable de un plato " +
                "existente. Edita su receta actual.",
                MessageType.Error
            );
            return;
        }

        int salePriceCents;

        try
        {
            salePriceCents =
                BistroBuilderIngredientsRecipesEditorUtility
                    .EurosToCents(salePriceEuros);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, MessageType.Error);
            return;
        }

        if (!ValidateDraftIngredientRows(out string rowError))
        {
            SetStatus(rowError, MessageType.Error);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            SetStatus(
                "Debe estar abierta la escena principal del restaurante " +
                "para sincronizar la nueva receta con la carta.",
                MessageType.Error
            );
            return;
        }

        BistroBuilderDishCatalogService[] dishServices =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<BistroBuilderDishCatalogService>(scene);
        BistroBuilderRestaurantMenuService[] menuServices =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindSceneObjects<BistroBuilderRestaurantMenuService>(scene);

        if (dishServices.Length != 1 || menuServices.Length != 1)
        {
            SetStatus(
                "La escena debe contener exactamente un servicio de " +
                "catálogo de platos y una carta runtime. Encontrados: " +
                dishServices.Length + " y " + menuServices.Length + ".",
                MessageType.Error
            );
            return;
        }

        BistroBuilderIngredientsRecipesEditorUtility.EnsureDataFolders();

        BistroBuilderDishDefinition existingDish =
            BistroBuilderIngredientsRecipesEditorUtility.FindDishById(
                normalizedDishId
            );
        BistroBuilderRecipeDefinition existingRecipe =
            BistroBuilderIngredientsRecipesEditorUtility.FindRecipeById(
                normalizedRecipeId
            );

        if (existingRecipe != null &&
            !string.Equals(
                existingRecipe.DishId,
                normalizedDishId,
                StringComparison.Ordinal
            ))
        {
            SetStatus(
                "El RecipeId ya pertenece al plato " +
                existingRecipe.DishId + ".",
                MessageType.Error
            );
            return;
        }

        BistroBuilderDishDefinition dishTarget = existingDish;
        BistroBuilderRecipeDefinition recipeTarget = existingRecipe;
        bool dishCreated = false;
        bool recipeCreated = false;
        string createdDishPath = string.Empty;
        string createdRecipePath = string.Empty;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Autoría de plato y receta 368A");

        try
        {
            if (dishTarget == null)
            {
                dishTarget = ScriptableObject.CreateInstance<
                    BistroBuilderDishDefinition
                >();
                dishCreated = true;
            }
            else
            {
                Undo.RecordObject(dishTarget, "Actualizar plato 368A");
            }

            WriteDishFields(
                dishTarget,
                normalizedDishId,
                normalizedRecipeId,
                salePriceCents
            );

            if (recipeTarget == null)
            {
                recipeTarget = ScriptableObject.CreateInstance<
                    BistroBuilderRecipeDefinition
                >();
                recipeCreated = true;
            }
            else
            {
                Undo.RecordObject(recipeTarget, "Actualizar receta 368A");
            }

            WriteRecipeFields(
                recipeTarget,
                normalizedRecipeId,
                dishTarget
            );

            if (!dishTarget.TryValidate(out string dishError))
            {
                throw new InvalidOperationException(dishError);
            }

            if (!recipeTarget.TryValidate(out string recipeError))
            {
                throw new InvalidOperationException(recipeError);
            }

            if (dishCreated)
            {
                createdDishPath =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetDishAssetPath(normalizedDishId);

                if (AssetDatabase.LoadMainAssetAtPath(createdDishPath) != null)
                {
                    throw new InvalidOperationException(
                        "La ruta de plato ya está ocupada: " +
                        createdDishPath
                    );
                }

                AssetDatabase.CreateAsset(dishTarget, createdDishPath);
                Undo.RegisterCreatedObjectUndo(
                    dishTarget,
                    "Crear plato 368A"
                );
            }

            if (recipeCreated)
            {
                createdRecipePath =
                    BistroBuilderIngredientsRecipesEditorUtility
                        .GetRecipeAssetPath(normalizedRecipeId);

                if (AssetDatabase.LoadMainAssetAtPath(createdRecipePath) != null)
                {
                    throw new InvalidOperationException(
                        "La ruta de receta ya está ocupada: " +
                        createdRecipePath
                    );
                }

                AssetDatabase.CreateAsset(recipeTarget, createdRecipePath);
                Undo.RegisterCreatedObjectUndo(
                    recipeTarget,
                    "Crear receta 368A"
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

            Undo.RecordObject(
                ingredientCatalog,
                "Actualizar catálogo de ingredientes 368A"
            );
            Undo.RecordObject(
                recipeCatalog,
                "Actualizar catálogo de recetas 368A"
            );
            Undo.RecordObject(
                dishCatalog,
                "Actualizar catálogo de platos 368A"
            );

            BistroBuilderIngredientsRecipesEditorUtility.RebuildAllCatalogs(
                ingredientCatalog,
                recipeCatalog,
                dishCatalog
            );

            BistroBuilderDishCatalogService dishService = dishServices[0];
            BistroBuilderRestaurantMenuService menuService = menuServices[0];
            Undo.RecordObject(menuService, "Sincronizar carta 368A");

            if (!dishService.RebuildIndex(out string catalogError))
            {
                throw new InvalidOperationException(catalogError);
            }

            if (!menuService.RebuildRuntimeIndexAndEnsureDefaults(
                    out string menuError
                ))
            {
                throw new InvalidOperationException(menuError);
            }

            if (!menuService.TryGetItemSnapshot(normalizedDishId, out _))
            {
                RequireMenuMutation(
                    menuService.TryAddDish(normalizedDishId),
                    "añadir el plato a la carta"
                );
            }

            RequireMenuMutation(
                menuService.TrySetUnlocked(normalizedDishId, true),
                "desbloquear el plato"
            );
            RequireMenuMutation(
                menuService.TrySetEnabled(normalizedDishId, true),
                "activar el plato"
            );
            RequireMenuMutation(
                menuService.TrySetPriceCents(
                    normalizedDishId,
                    salePriceCents
                ),
                "actualizar el precio"
            );
            RequireMenuMutation(
                menuService.TrySetAvailability(
                    normalizedDishId,
                    mealAvailability
                ),
                "actualizar los servicios del día"
            );

            EditorUtility.SetDirty(dishTarget);
            EditorUtility.SetDirty(recipeTarget);
            EditorUtility.SetDirty(menuService);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena después de sincronizar " +
                    "la carta."
                );
            }

            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);

            selectedDish = dishTarget;
            dishId = dishTarget.DishId;
            recipeId = recipeTarget.RecipeId;
            Selection.activeObject = dishTarget;

            if (!BistroBuilderRecipeEconomics.TryBuildSnapshot(
                    dishTarget,
                    recipeTarget,
                    out BistroBuilderRecipeEconomicsSnapshot economics,
                    out string economicsError
                ))
            {
                throw new InvalidOperationException(economicsError);
            }

            SetStatus(
                "Plato y receta guardados. La carta activa ya contiene " +
                dishTarget.DisplayName + " a " +
                FormatEuros(economics.SalePriceCents) +
                ". Coste estimado: " +
                FormatEuros(economics.CostPerPortionCents) +
                " por ración.",
                MessageType.Info
            );
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);

            if (recipeCreated &&
                !string.IsNullOrWhiteSpace(createdRecipePath))
            {
                AssetDatabase.DeleteAsset(createdRecipePath);
            }
            else if (recipeCreated && recipeTarget != null)
            {
                DestroyImmediate(recipeTarget);
            }

            if (dishCreated && !string.IsNullOrWhiteSpace(createdDishPath))
            {
                AssetDatabase.DeleteAsset(createdDishPath);
            }
            else if (dishCreated && dishTarget != null)
            {
                DestroyImmediate(dishTarget);
            }

            dishServices[0].RebuildIndex(out _);
            menuServices[0].RebuildRuntimeIndexAndEnsureDefaults(out _);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetStatus(exception.Message, MessageType.Error);
        }
    }

    private void WriteIngredientFields(
        BistroBuilderIngredientDefinition target,
        string normalizedId,
        int priceCents
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "ingredientId"
        ).stringValue = normalizedId;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "displayName"
        ).stringValue = ingredientDisplayName != null
            ? ingredientDisplayName.Trim()
            : string.Empty;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "category"
        ).enumValueIndex = (int)ingredientCategory;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "storageType"
        ).enumValueIndex = (int)ingredientStorage;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "baseUnit"
        ).enumValueIndex = (int)ingredientBaseUnit;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "referencePackAmount"
        ).doubleValue = referencePackAmount;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "referencePackUnit"
        ).enumValueIndex = (int)referencePackUnit;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "referencePackPriceCents"
        ).intValue = priceCents;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "defaultShelfLifeDays"
        ).intValue = shelfLifeDays;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "perishable"
        ).boolValue = perishable;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void WriteDishFields(
        BistroBuilderDishDefinition target,
        string normalizedDishId,
        string normalizedRecipeId,
        int priceCents
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "dishId"
        ).stringValue = normalizedDishId;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "displayName"
        ).stringValue = dishDisplayName != null
            ? dishDisplayName.Trim()
            : string.Empty;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "description"
        ).stringValue = dishDescription != null
            ? dishDescription.Trim()
            : string.Empty;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "category"
        ).enumValueIndex = (int)dishCategory;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "course"
        ).enumValueIndex = (int)dishCourse;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "defaultAvailability"
        ).intValue = (int)mealAvailability;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "allowedServiceModes"
        ).intValue = (int)serviceModes;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "requiredStation"
        ).enumValueIndex = (int)kitchenStation;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "basePreparationSeconds"
        ).intValue = preparationSeconds;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "complexity"
        ).intValue = complexity;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "recipeId"
        ).stringValue = normalizedRecipeId;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "basePriceCents"
        ).intValue = priceCents;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "shareable"
        ).boolValue = shareable;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "minimumConsumers"
        ).intValue = shareable ? minimumConsumers : 1;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "maximumConsumers"
        ).intValue = shareable ? maximumConsumers : 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void WriteRecipeFields(
        BistroBuilderRecipeDefinition target,
        string normalizedRecipeId,
        BistroBuilderDishDefinition dish
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "recipeId"
        ).stringValue = normalizedRecipeId;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "dish"
        ).objectReferenceValue = dish;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "yieldPortions"
        ).intValue = yieldPortions;
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "wasteBasisPoints"
        ).intValue = Mathf.Clamp(
            (int)Math.Round(
                wastePercent * 100d,
                MidpointRounding.AwayFromZero
            ),
            0,
            BistroBuilderRecipeDefinition.MaximumWasteBasisPoints
        );
        BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
            serialized,
            "notes"
        ).stringValue = recipeNotes != null
            ? recipeNotes.Trim()
            : string.Empty;

        SerializedProperty ingredients =
            BistroBuilderIngredientsRecipesEditorUtility.RequireProperty(
                serialized,
                "ingredients"
            );
        ingredients.arraySize = ingredientRows.Count;

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            IngredientRowDraft row = ingredientRows[index];
            SerializedProperty element =
                ingredients.GetArrayElementAtIndex(index);
            SerializedProperty ingredient =
                element.FindPropertyRelative("ingredient");
            SerializedProperty amount =
                element.FindPropertyRelative("amount");
            SerializedProperty unit =
                element.FindPropertyRelative("unit");

            if (ingredient == null || amount == null || unit == null)
            {
                throw new InvalidOperationException(
                    "La estructura serializada de una línea de receta no " +
                    "coincide con el contrato 368A."
                );
            }

            ingredient.objectReferenceValue = row.Ingredient;
            amount.doubleValue = row.Amount;
            unit.enumValueIndex = (int)row.Unit;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void LoadIngredient(
        BistroBuilderIngredientDefinition definition
    )
    {
        if (definition == null)
        {
            return;
        }

        ingredientId = definition.IngredientId;
        ingredientDisplayName = definition.DisplayName;
        ingredientCategory = definition.Category;
        ingredientStorage = definition.StorageType;
        ingredientBaseUnit = definition.BaseUnit;
        referencePackAmount = definition.ReferencePackAmount;
        referencePackUnit = definition.ReferencePackUnit;
        referencePackPriceEuros =
            BistroBuilderIngredientsRecipesEditorUtility.CentsToEuros(
                definition.ReferencePackPriceCents
            );
        shelfLifeDays = definition.DefaultShelfLifeDays;
        perishable = definition.Perishable;
        ClearStatus();
        Repaint();
    }

    private void LoadDishAndRecipe(BistroBuilderDishDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        dishId = definition.DishId;
        recipeId = definition.RecipeId;
        dishDisplayName = definition.DisplayName;
        dishDescription = definition.Description;
        dishCategory = definition.Category;
        dishCourse = definition.Course;
        mealAvailability = definition.DefaultAvailability;
        serviceModes = definition.AllowedServiceModes;
        kitchenStation = definition.RequiredStation;
        preparationSeconds = definition.BasePreparationSeconds;
        complexity = definition.Complexity;
        salePriceEuros =
            BistroBuilderIngredientsRecipesEditorUtility.CentsToEuros(
                definition.BasePriceCents
            );
        shareable = definition.Shareable;
        minimumConsumers = definition.MinimumConsumers;
        maximumConsumers = definition.MaximumConsumers;

        ingredientRows.Clear();
        BistroBuilderRecipeDefinition recipe =
            BistroBuilderIngredientsRecipesEditorUtility.FindRecipeById(
                definition.RecipeId
            );

        if (recipe != null)
        {
            yieldPortions = recipe.YieldPortions;
            wastePercent = recipe.WasteBasisPoints / 100d;
            recipeNotes = recipe.Notes;

            for (int index = 0; index < recipe.Ingredients.Count; index++)
            {
                BistroBuilderRecipeIngredientAmount line =
                    recipe.Ingredients[index];
                ingredientRows.Add(new IngredientRowDraft
                {
                    Ingredient = line.Ingredient,
                    Amount = line.Amount,
                    Unit = line.Unit
                });
            }
        }
        else
        {
            yieldPortions = 1;
            wastePercent = 0d;
            recipeNotes = string.Empty;
        }

        if (ingredientRows.Count == 0)
        {
            ingredientRows.Add(new IngredientRowDraft());
        }

        ClearStatus();
        Repaint();
    }

    private void ResetIngredientDraft()
    {
        selectedIngredient = null;
        ingredientId = string.Empty;
        ingredientDisplayName = string.Empty;
        ingredientCategory = BistroBuilderIngredientCategory.Other;
        ingredientStorage =
            BistroBuilderIngredientStorageType.DryStorage;
        ingredientBaseUnit = BistroBuilderMeasurementUnit.Gram;
        referencePackAmount = 1d;
        referencePackUnit = BistroBuilderMeasurementUnit.Kilogram;
        referencePackPriceEuros = 0d;
        shelfLifeDays = 0;
        perishable = false;
        ClearStatus();
        GUI.FocusControl(null);
    }

    private void ResetDishDraft()
    {
        selectedDish = null;
        dishId = string.Empty;
        recipeId = string.Empty;
        dishDisplayName = string.Empty;
        dishDescription = string.Empty;
        dishCategory = BistroBuilderDishCategory.MainCourse;
        dishCourse = BistroBuilderDishCourse.Main;
        mealAvailability =
            BistroBuilderMealServiceAvailability.Lunch |
            BistroBuilderMealServiceAvailability.Dinner;
        serviceModes =
            BistroBuilderDishServiceModeAvailability.TableService;
        kitchenStation = BistroBuilderKitchenStationType.HotKitchen;
        preparationSeconds = 300;
        complexity = 1;
        salePriceEuros = 10d;
        shareable = false;
        minimumConsumers = 1;
        maximumConsumers = 1;
        yieldPortions = 1;
        wastePercent = 0d;
        recipeNotes = string.Empty;
        ingredientRows.Clear();
        ingredientRows.Add(new IngredientRowDraft());
        ClearStatus();
        GUI.FocusControl(null);
    }

    private void GenerateDishIdsFromName()
    {
        string slug = BuildAsciiSlug(dishDisplayName);

        if (string.IsNullOrWhiteSpace(slug))
        {
            SetStatus(
                "No se pudo generar un identificador a partir del nombre.",
                MessageType.Error
            );
            return;
        }

        dishId = "dish_" + slug;
        recipeId = "recipe_" + slug;
        ClearStatus();
        GUI.FocusControl(null);
    }

    private bool ValidateDraftIngredientRows(out string error)
    {
        error = string.Empty;

        if (ingredientRows.Count == 0)
        {
            error = "La receta debe contener al menos un ingrediente.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            IngredientRowDraft row = ingredientRows[index];

            if (row == null || row.Ingredient == null)
            {
                error = "Falta el ingrediente de la línea " +
                        (index + 1) + ".";
                return false;
            }

            if (!row.Ingredient.TryValidate(out error))
            {
                error = "Línea " + (index + 1) + ": " + error;
                return false;
            }

            if (!ids.Add(row.Ingredient.IngredientId))
            {
                error = "El ingrediente " +
                        row.Ingredient.DisplayName +
                        " está repetido. Agrupa la cantidad en una sola " +
                        "línea.";
                return false;
            }

            if (!BistroBuilderMeasurementUtility.AreCompatible(
                    row.Ingredient.BaseUnit,
                    row.Unit
                ))
            {
                error = "La unidad de " + row.Ingredient.DisplayName +
                        " no es compatible con su unidad base.";
                return false;
            }

            if (!BistroBuilderMeasurementUtility
                    .TryConvertToCanonicalMilliUnits(
                        row.Amount,
                        row.Unit,
                        out _,
                        out error
                    ))
            {
                error = "Línea " + (index + 1) + ": " + error;
                return false;
            }
        }

        return true;
    }

    private bool TryCalculateDraftEconomics(
        out int costCents,
        out int saleCents,
        out int marginCents,
        out int marginBasisPoints,
        out BistroBuilderRecipeMarginBand band,
        out string error
    )
    {
        costCents = 0;
        saleCents = 0;
        marginCents = 0;
        marginBasisPoints = 0;
        band = BistroBuilderRecipeMarginBand.Low;
        error = string.Empty;

        if (!ValidateDraftIngredientRows(out error))
        {
            return false;
        }

        if (yieldPortions < 1)
        {
            error = "El rendimiento debe ser al menos una ración.";
            return false;
        }

        decimal totalMicroCents = 0m;

        for (int index = 0; index < ingredientRows.Count; index++)
        {
            IngredientRowDraft row = ingredientRows[index];

            if (!BistroBuilderMeasurementUtility
                    .TryConvertToCanonicalMilliUnits(
                        row.Amount,
                        row.Unit,
                        out long canonical,
                        out error
                    ))
            {
                return false;
            }

            if (!row.Ingredient.TryCalculateCostMicroCents(
                    canonical,
                    out long lineCost,
                    out error
                ))
            {
                return false;
            }

            totalMicroCents += lineCost;
        }

        decimal withWaste =
            totalMicroCents * (100m + (decimal)wastePercent) / 100m;
        decimal perPortionMicroCents = withWaste / yieldPortions;
        decimal cents =
            perPortionMicroCents /
            BistroBuilderIngredientDefinition.MicroCentsPerCent;
        decimal roundedCents = decimal.Round(
            cents,
            0,
            MidpointRounding.AwayFromZero
        );

        if (roundedCents < 0m || roundedCents > int.MaxValue)
        {
            error = "El coste calculado queda fuera de rango.";
            return false;
        }

        try
        {
            saleCents = BistroBuilderIngredientsRecipesEditorUtility
                .EurosToCents(salePriceEuros);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        costCents = (int)roundedCents;
        marginCents = saleCents - costCents;
        marginBasisPoints = saleCents > 0
            ? (int)decimal.Round(
                (decimal)marginCents * 10000m / saleCents,
                0,
                MidpointRounding.AwayFromZero
            )
            : 0;
        band = BistroBuilderRecipeEconomics.ResolveBand(
            marginCents,
            marginBasisPoints
        );
        return true;
    }

    private static void RequireMenuMutation(
        BistroBuilderMenuMutationResult result,
        string operation
    )
    {
        if (result.Succeeded ||
            result.FailureReason ==
            BistroBuilderMenuMutationFailureReason.NoChange)
        {
            return;
        }

        throw new InvalidOperationException(
            "No se pudo " + operation + ": " + result.Message
        );
    }

    private static string BuildAsciiSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Trim().Normalize(
            NormalizationForm.FormD
        );
        StringBuilder builder = new StringBuilder(decomposed.Length);

        for (int index = 0; index < decomposed.Length; index++)
        {
            char character = decomposed[index];
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return BistroBuilderMenuIdUtility.NormalizeStableId(
            builder.ToString().Normalize(NormalizationForm.FormC)
        );
    }

    private static string FormatEuros(int cents)
    {
        return (cents / 100m).ToString("C2", SpanishCulture);
    }

    private static string TranslateMarginBand(
        BistroBuilderRecipeMarginBand band
    )
    {
        switch (band)
        {
            case BistroBuilderRecipeMarginBand.Loss:
                return "pérdida";
            case BistroBuilderRecipeMarginBand.Low:
                return "margen bajo";
            case BistroBuilderRecipeMarginBand.Correct:
                return "margen correcto";
            case BistroBuilderRecipeMarginBand.High:
                return "margen alto";
            case BistroBuilderRecipeMarginBand.Excellent:
                return "margen excelente";
            default:
                return band.ToString();
        }
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message ?? string.Empty;
        statusType = type;
        Repaint();
    }

    private void ClearStatus()
    {
        statusMessage = string.Empty;
        statusType = MessageType.Info;
    }
}
