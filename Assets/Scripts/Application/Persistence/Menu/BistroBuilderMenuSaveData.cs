using System;
using System.Collections.Generic;

/// <summary>
/// Estado persistente de un plato dentro de una carta.
/// Usa céntimos enteros y valores primitivos estables para que el formato
/// pueda migrarse sin depender de objetos Unity ni de nombres visibles.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuItemSaveData
{
    public string dishId = string.Empty;
    public int currentPriceCents;
    public bool unlocked;
    public bool enabled;
    public bool manuallySoldOut;
    public bool signatureDish;
    public int availableServices;
    public int displayOrder;

    // 0/0 significa heredar de la definición efectiva. Es el estado que
    // produce la migración V2 -> V3 para no inventar valores históricos.
    public int preparationDifficulty;
    public int basePreparationSeconds;
}

/// <summary>
/// Carta persistente de un restaurante concreto.
///
/// unresolvedItems conserva íntegramente las entradas cuyo DishId no existe
/// temporalmente en el catálogo actual. No se ofrecen al jugador, pero tampoco
/// se destruyen durante una carga o una actualización de contenido.
/// </summary>
[Serializable]
public sealed class BistroBuilderRestaurantMenuSaveData
{
    public string restaurantId = string.Empty;
    public int revision;

    public List<BistroBuilderMenuItemSaveData> items =
        new List<BistroBuilderMenuItemSaveData>();

    public List<BistroBuilderMenuItemSaveData> unresolvedItems =
        new List<BistroBuilderMenuItemSaveData>();
}

/// <summary>
/// Definición persistente de un plato creado o sobrescrito por el jugador.
/// Solo usa primitivos y enums serializados como enteros. Nunca guarda una
/// referencia a ScriptableObject ni modifica los assets canónicos.
/// </summary>
[Serializable]
public sealed class BistroBuilderDishDefinitionSaveData
{
    public int definitionVersion =
        BistroBuilderDishDefinition.CurrentDefinitionVersion;
    public string dishId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string categoryId = string.Empty;
    public int course;
    public int defaultAvailability;
    public int allowedServiceModes;
    public int requiredStation;
    public int basePreparationSeconds;
    public int complexity;
    public string recipeId = string.Empty;
    public int basePriceCents;
    public bool shareable;
    public int minimumConsumers = 1;
    public int maximumConsumers = 1;
}

/// <summary>
/// Línea persistente de ingrediente. ingredientId enlaza exclusivamente con
/// el catálogo canónico de ingredientes; cantidad y unidad conservan la
/// autoría visible y se vuelven a validar al cargar.
/// </summary>
[Serializable]
public sealed class BistroBuilderRecipeIngredientSaveData
{
    public string ingredientId = string.Empty;
    public double amount;
    public int unit;
}

/// <summary>
/// Receta persistente correspondiente a una definición runtime de plato.
/// </summary>
[Serializable]
public sealed class BistroBuilderRecipeDefinitionSaveData
{
    public int definitionVersion =
        BistroBuilderRecipeDefinition.CurrentDefinitionVersion;
    public string recipeId = string.Empty;
    public string dishId = string.Empty;
    public int yieldPortions = 1;
    public int wasteBasisPoints;
    public string notes = string.Empty;

    public List<BistroBuilderRecipeIngredientSaveData> ingredients =
        new List<BistroBuilderRecipeIngredientSaveData>();
}

/// <summary>
/// Unidad atómica persistente de autoría: una definición y su receta. El par
/// evita guardar capas descompensadas y permite restaurarlas o conservarlas
/// como no resueltas sin perder datos.
/// </summary>
[Serializable]
public sealed class BistroBuilderDishRecipeSaveData
{
    public BistroBuilderDishDefinitionSaveData dish =
        new BistroBuilderDishDefinitionSaveData();
    public BistroBuilderRecipeDefinitionSaveData recipe =
        new BistroBuilderRecipeDefinitionSaveData();
}

/// <summary>
/// Formato actual de la sección menu.state.
///
/// V4 conserva la arquitectura multirrestaurante de V3 y añade una única capa
/// global de autoría runtime para platos y recetas. Las parejas que no pueden
/// resolverse temporalmente por contenido ausente se conservan aparte y se
/// reintentan en cargas futuras. No guarda disponibilidad derivada del
/// inventario ni duplica assets canónicos.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveData
{
    public const int CurrentSchemaVersion = 4;

    public int schemaVersion = CurrentSchemaVersion;
    public string activeRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public List<BistroBuilderRestaurantMenuSaveData> restaurants =
        new List<BistroBuilderRestaurantMenuSaveData>();

    public List<BistroBuilderDishRecipeSaveData> authoredDishRecipes =
        new List<BistroBuilderDishRecipeSaveData>();

    public List<BistroBuilderDishRecipeSaveData>
        unresolvedAuthoredDishRecipes =
            new List<BistroBuilderDishRecipeSaveData>();
}

/// <summary>
/// Contrato exacto de menu.state v3. Se conserva para la migración pura
/// v3 -> v4 y para que las migraciones anteriores no dependan del DTO actual.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveDataV3
{
    public int schemaVersion = 3;
    public string activeRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public List<BistroBuilderRestaurantMenuSaveData> restaurants =
        new List<BistroBuilderRestaurantMenuSaveData>();
}

/// <summary>
/// Contrato exacto de menu.state v2. La forma multirrestaurante coincide con
/// V3, pero sus entradas no tenían preparación explícita y JsonUtility deja
/// esos campos en 0/0 para expresar herencia al migrar.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveDataV2
{
    public int schemaVersion = 2;
    public string activeRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public List<BistroBuilderRestaurantMenuSaveData> restaurants =
        new List<BistroBuilderRestaurantMenuSaveData>();
}

/// <summary>
/// Contrato exacto de menu.state v1. Se conserva exclusivamente para la
/// migración pura v1 -> v2; no debe volver a usarse como estado runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveDataV1
{
    public int schemaVersion = 1;

    public List<BistroBuilderMenuItemSaveData> items =
        new List<BistroBuilderMenuItemSaveData>();
}
