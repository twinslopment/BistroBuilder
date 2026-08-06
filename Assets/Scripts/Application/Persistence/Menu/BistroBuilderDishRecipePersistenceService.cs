using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de persistencia de las capas runtime de platos y recetas.
///
/// No crea una sección de guardado nueva: menu.state sigue siendo la única
/// fuente persistente. Este servicio convierte las capas runtime a DTO, vuelve
/// a resolverlas contra categorías e ingredientes canónicos y conserva sin
/// pérdida los pares temporalmente no resolubles.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Dish Recipe Persistence Service"
)]
public sealed class BistroBuilderDishRecipePersistenceService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1G3";
    [SerializeField]
    private BistroBuilderDishCatalogService dishCatalogService;

    [SerializeField]
    private BistroBuilderRecipeCatalogService recipeCatalogService;

    [SerializeField]
    private BistroBuilderDishCategoryCatalogService categoryCatalogService;

    private readonly List<BistroBuilderDishRecipeSaveData>
        unresolvedPairs = new List<BistroBuilderDishRecipeSaveData>();

    private readonly List<BistroBuilderDishDefinition> dishBuffer =
        new List<BistroBuilderDishDefinition>(32);

    private readonly List<BistroBuilderRecipeDefinition> recipeBuffer =
        new List<BistroBuilderRecipeDefinition>(32);

    private readonly List<string> suppressedDishIdBuffer =
        new List<string>(16);

    public BistroBuilderDishCatalogService DishCatalogService =>
        dishCatalogService;

    public BistroBuilderRecipeCatalogService RecipeCatalogService =>
        recipeCatalogService;

    public BistroBuilderDishCategoryCatalogService CategoryCatalogService =>
        categoryCatalogService;

    public int UnresolvedPairCount => unresolvedPairs.Count;

    public int UnresolvedCanonicalOverrideCount
    {
        get
        {
            int count = 0;

            for (int index = 0; index < unresolvedPairs.Count; index++)
            {
                BistroBuilderDishRecipeSaveData pair = unresolvedPairs[index];

                if (pair != null && pair.dish != null &&
                    dishCatalogService != null &&
                    dishCatalogService.Catalog != null &&
                    dishCatalogService.Catalog.Contains(pair.dish.dishId))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Reserva la identidad de una autoría no resuelta. Aunque el plato no sea
    /// visible ni operativo, un nuevo plato no puede reutilizar su DishId.
    /// </summary>
    public bool IsDishIdReserved(string dishId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId
        );

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            return false;
        }

        for (int index = 0; index < unresolvedPairs.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = unresolvedPairs[index];

            if (pair != null && pair.dish != null &&
                string.Equals(
                    pair.dish.dishId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reserva también el RecipeId de una autoría no resuelta para impedir que
    /// otra definición se apropie de la receta mientras falta contenido.
    /// </summary>
    public bool IsRecipeIdReserved(string recipeId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            recipeId
        );

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            return false;
        }

        for (int index = 0; index < unresolvedPairs.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = unresolvedPairs[index];

            if (pair != null && pair.recipe != null &&
                string.Equals(
                    pair.recipe.recipeId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (dishCatalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        if (categoryCatalogService == null)
        {
            error = "Falta BistroBuilderDishCategoryCatalogService.";
            return false;
        }

        // La validación profunda de los catálogos se realiza en el validador
        // 2.1G3 y al resolver cada par. Aquí solo exigimos cableado estable para
        // conservar compatibilidad con pruebas históricas que no construyen
        // catálogos canónicos completos y no contienen autoría runtime.
        if (!ReferenceEquals(
                recipeCatalogService.DishCatalogService,
                dishCatalogService
            ))
        {
            error = "La persistencia no comparte los catálogos efectivos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Valida el contrato estructural y las identidades frente a los catálogos
    /// canónicos incluso cuando un par no puede resolverse por contenido
    /// ausente. De este modo una autoría no resuelta no puede apropiarse del
    /// RecipeId de otro plato ni cambiar el RecipeId de un plato canónico.
    /// </summary>
    public bool TryValidatePersistentCollections(
        IList<BistroBuilderDishRecipeSaveData> resolved,
        IList<BistroBuilderDishRecipeSaveData> unresolved,
        out string error
    )
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderDishRecipeSaveDataUtility
                .TryValidatePairCollections(resolved, unresolved, out error))
        {
            return false;
        }

        return TryValidateCanonicalIdentities(resolved, out error) &&
               TryValidateCanonicalIdentities(unresolved, out error);
    }

    /// <summary>
    /// Captura pares runtime completos y preserva por separado los pares que
    /// una carga anterior no pudo resolver. La operación falla si las capas
    /// de plato y receta están descompensadas.
    /// </summary>
    public bool TryCapture(
        out List<BistroBuilderDishRecipeSaveData> resolved,
        out List<BistroBuilderDishRecipeSaveData> unresolved,
        out string error
    )
    {
        resolved = null;
        unresolved = null;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        dishBuffer.Clear();
        recipeBuffer.Clear();
        dishCatalogService.CopyRuntimeDefinitionsTo(dishBuffer);
        recipeCatalogService.CopyRuntimeRecipesTo(recipeBuffer);

        Dictionary<string, BistroBuilderRecipeDefinition> recipesByDish =
            new Dictionary<string, BistroBuilderRecipeDefinition>(
                StringComparer.Ordinal
            );

        for (int index = 0; index < recipeBuffer.Count; index++)
        {
            BistroBuilderRecipeDefinition recipe = recipeBuffer[index];

            if (recipe == null ||
                recipesByDish.ContainsKey(recipe.DishId))
            {
                error = "La capa runtime de recetas está descompensada o " +
                        "contiene DishId duplicados.";
                return false;
            }

            recipesByDish.Add(recipe.DishId, recipe);
        }

        resolved = new List<BistroBuilderDishRecipeSaveData>(
            dishBuffer.Count
        );
        HashSet<string> capturedIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> capturedRecipeIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < dishBuffer.Count; index++)
        {
            BistroBuilderDishDefinition dish = dishBuffer[index];

            if (dish == null || !capturedIds.Add(dish.DishId) ||
                !recipesByDish.TryGetValue(
                    dish.DishId,
                    out BistroBuilderRecipeDefinition recipe
                ) ||
                recipe == null ||
                !ReferenceEquals(recipe.Dish, dish) ||
                !string.Equals(
                    recipe.RecipeId,
                    dish.RecipeId,
                    StringComparison.Ordinal
                ) ||
                !capturedRecipeIds.Add(recipe.RecipeId))
            {
                error = "La capa runtime no contiene un par plato/receta " +
                        "coherente para " +
                        (dish != null ? dish.DishId : "<nulo>") + ".";
                return false;
            }

            BistroBuilderDishRecipeSaveData pair =
                BistroBuilderDishRecipeSaveDataUtility.FromRuntime(
                    dish,
                    recipe
                );

            if (!BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(pair, out error))
            {
                return false;
            }

            resolved.Add(pair);
        }

        if (recipesByDish.Count != capturedIds.Count)
        {
            error = "La capa runtime contiene recetas sin definición de plato.";
            return false;
        }

        suppressedDishIdBuffer.Clear();
        dishCatalogService.CopySuppressedCanonicalDishIdsTo(
            suppressedDishIdBuffer
        );

        HashSet<string> expectedSuppressedDishIds =
            new HashSet<string>(StringComparer.Ordinal);
        unresolved = new List<BistroBuilderDishRecipeSaveData>(
            unresolvedPairs.Count
        );

        for (int index = 0; index < unresolvedPairs.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = unresolvedPairs[index];

            if (!BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(pair, out error))
            {
                return false;
            }

            if (!capturedIds.Add(pair.dish.dishId))
            {
                error = "La autoría persistente repite el DishId " +
                        pair.dish.dishId + ".";
                return false;
            }

            if (!capturedRecipeIds.Add(pair.recipe.recipeId))
            {
                error = "La autoría persistente repite el RecipeId " +
                        pair.recipe.recipeId + ".";
                return false;
            }

            if (dishCatalogService.Catalog != null &&
                dishCatalogService.Catalog.Contains(pair.dish.dishId))
            {
                expectedSuppressedDishIds.Add(pair.dish.dishId);
            }

            unresolved.Add(
                BistroBuilderDishRecipeSaveDataUtility.Clone(pair)
            );
        }

        if (suppressedDishIdBuffer.Count !=
                expectedSuppressedDishIds.Count)
        {
            error = "La supresión canónica no coincide con las " +
                    "sobrescrituras no resueltas.";
            return false;
        }

        for (int index = 0; index < suppressedDishIdBuffer.Count; index++)
        {
            if (!expectedSuppressedDishIds.Contains(
                    suppressedDishIdBuffer[index]
                ))
            {
                error = "La supresión canónica contiene una sombra sin " +
                        "autoría no resuelta.";
                return false;
            }
        }

        if (!TryValidatePersistentCollections(
                resolved,
                unresolved,
                out error
            ))
        {
            return false;
        }

        resolved.Sort(ComparePairs);
        unresolved.Sort(ComparePairs);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Resuelve y sustituye atómicamente ambas capas runtime. Los pares cuyo
    /// contenido canónico falta no abortan la carga: se conservan íntegros en
    /// unresolvedPairs y se reintentarán en una carga futura.
    /// </summary>
    public bool TryApply(
        IList<BistroBuilderDishRecipeSaveData> resolvedSource,
        IList<BistroBuilderDishRecipeSaveData> unresolvedSource,
        out RollbackState rollback,
        out string error
    )
    {
        rollback = null;

        if (!TryValidatePersistentCollections(
                resolvedSource,
                unresolvedSource,
                out error
            ))
        {
            return false;
        }

        rollback = new RollbackState();
        dishCatalogService.CopyRuntimeDefinitionsTo(
            rollback.PreviousDishes
        );
        recipeCatalogService.CopyRuntimeRecipesTo(
            rollback.PreviousRecipes
        );
        dishCatalogService.CopySuppressedCanonicalDishIdsTo(
            rollback.PreviousSuppressedDishIds
        );
        CopyPairs(unresolvedPairs, rollback.PreviousUnresolvedPairs);

        List<BistroBuilderDishDefinition> nextDishes =
            new List<BistroBuilderDishDefinition>();
        List<BistroBuilderRecipeDefinition> nextRecipes =
            new List<BistroBuilderRecipeDefinition>();
        List<BistroBuilderDishRecipeSaveData> nextUnresolved =
            new List<BistroBuilderDishRecipeSaveData>();
        List<string> nextSuppressedDishIds = new List<string>();
        if (!TryResolveSource(
                resolvedSource,
                nextDishes,
                nextRecipes,
                nextUnresolved,
                out error
            ) ||
            !TryResolveSource(
                unresolvedSource,
                nextDishes,
                nextRecipes,
                nextUnresolved,
                out error
            ))
        {
            DestroyRuntimeLists(nextDishes, nextRecipes);
            return false;
        }

        if (!dishCatalogService.TryReplaceRuntimeDefinitions(
                nextDishes,
                out error,
                false
            ))
        {
            DestroyRuntimeLists(nextDishes, nextRecipes);
            return false;
        }

        if (!recipeCatalogService.TryReplaceRuntimeRecipes(
                nextRecipes,
                out error,
                false
            ))
        {
            dishCatalogService.TryReplaceRuntimeDefinitions(
                rollback.PreviousDishes,
                out _,
                false
            );
            DestroyRuntimeLists(nextDishes, nextRecipes);
            return false;
        }

        for (int index = 0; index < nextUnresolved.Count; index++)
        {
            string unresolvedDishId = nextUnresolved[index].dish.dishId;

            if (dishCatalogService.Catalog != null &&
                dishCatalogService.Catalog.Contains(unresolvedDishId))
            {
                nextSuppressedDishIds.Add(unresolvedDishId);
            }
        }

        if (!dishCatalogService.TryReplaceSuppressedCanonicalDishIds(
                nextSuppressedDishIds,
                out error,
                false
            ))
        {
            dishCatalogService.TryReplaceRuntimeDefinitions(
                rollback.PreviousDishes,
                out _,
                false
            );
            recipeCatalogService.TryReplaceRuntimeRecipes(
                rollback.PreviousRecipes,
                out _,
                false
            );
            dishCatalogService.TryReplaceSuppressedCanonicalDishIds(
                rollback.PreviousSuppressedDishIds,
                out _,
                false
            );
            DestroyRuntimeLists(nextDishes, nextRecipes);
            return false;
        }

        unresolvedPairs.Clear();
        CopyPairs(nextUnresolved, unresolvedPairs);
        rollback.AppliedDishes.AddRange(nextDishes);
        rollback.AppliedRecipes.AddRange(nextRecipes);
        rollback.Applied = true;
        error = string.Empty;
        return true;
    }

    public void Rollback(RollbackState rollback)
    {
        if (rollback == null || !rollback.Applied)
        {
            return;
        }

        dishCatalogService.TryReplaceRuntimeDefinitions(
            rollback.PreviousDishes,
            out _,
            false
        );
        recipeCatalogService.TryReplaceRuntimeRecipes(
            rollback.PreviousRecipes,
            out _,
            false
        );
        dishCatalogService.TryReplaceSuppressedCanonicalDishIds(
            rollback.PreviousSuppressedDishIds,
            out _,
            false
        );
        unresolvedPairs.Clear();
        CopyPairs(
            rollback.PreviousUnresolvedPairs,
            unresolvedPairs
        );
        DestroyRuntimeLists(
            rollback.AppliedDishes,
            rollback.AppliedRecipes
        );
        rollback.AppliedDishes.Clear();
        rollback.AppliedRecipes.Clear();
        rollback.Applied = false;
    }

    public void CompleteApply(RollbackState rollback)
    {
        if (rollback == null || !rollback.Applied)
        {
            return;
        }

        dishCatalogService.PublishChanged();
        recipeCatalogService.PublishChanged();

        // Las capas anteriores ya no son fuente de verdad. Se destruyen tras
        // completar la sustitución para evitar acumular ScriptableObjects
        // DontSave en cargas sucesivas. Las comandas y trabajos de cocina
        // conservan identidades y snapshots, no referencias a estas instancias.
        DestroyRuntimeLists(
            rollback.PreviousDishes,
            rollback.PreviousRecipes
        );
        rollback.PreviousDishes.Clear();
        rollback.PreviousRecipes.Clear();
        rollback.AppliedDishes.Clear();
        rollback.AppliedRecipes.Clear();
        rollback.Applied = false;
    }

    private bool TryResolveSource(
        IList<BistroBuilderDishRecipeSaveData> source,
        List<BistroBuilderDishDefinition> nextDishes,
        List<BistroBuilderRecipeDefinition> nextRecipes,
        List<BistroBuilderDishRecipeSaveData> nextUnresolved,
        out string error
    )
    {
        if (source == null)
        {
            error = "La colección persistente de autoría es nula.";
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderDishRecipeSaveData pair = source[index];

            if (!BistroBuilderDishRecipeSaveDataUtility
                    .TryValidatePairStructure(pair, out error))
            {
                return false;
            }

            if (BistroBuilderDishRecipeSaveDataUtility.TryCreateRuntimePair(
                    pair,
                    categoryCatalogService,
                    recipeCatalogService,
                    out BistroBuilderDishDefinition dish,
                    out BistroBuilderRecipeDefinition recipe,
                    out _
                ))
            {
                nextDishes.Add(dish);
                nextRecipes.Add(recipe);
            }
            else
            {
                nextUnresolved.Add(
                    BistroBuilderDishRecipeSaveDataUtility.Clone(pair)
                );
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateCanonicalIdentities(
        IList<BistroBuilderDishRecipeSaveData> source,
        out string error
    )
    {
        if (source == null)
        {
            error = "La colección persistente de autoría es nula.";
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (!TryValidateCanonicalIdentity(source[index], out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateCanonicalIdentity(
        BistroBuilderDishRecipeSaveData pair,
        out string error
    )
    {
        if (pair == null || pair.dish == null || pair.recipe == null)
        {
            error = "No puede validarse la identidad de un par nulo.";
            return false;
        }

        BistroBuilderDishCatalog canonicalDishes =
            dishCatalogService != null ? dishCatalogService.Catalog : null;
        BistroBuilderRecipeCatalog canonicalRecipes =
            recipeCatalogService != null
                ? recipeCatalogService.RecipeCatalog
                : null;

        if (canonicalDishes != null &&
            canonicalDishes.TryGetDefinition(
                pair.dish.dishId,
                out BistroBuilderDishDefinition canonicalDish
            ) &&
            canonicalDish != null &&
            !string.Equals(
                canonicalDish.RecipeId,
                pair.recipe.recipeId,
                StringComparison.Ordinal
            ))
        {
            error = "La sobrescritura persistente de " +
                    pair.dish.dishId +
                    " cambia indebidamente su RecipeId canónico.";
            return false;
        }

        if (canonicalRecipes != null &&
            canonicalRecipes.TryGetByRecipeId(
                pair.recipe.recipeId,
                out BistroBuilderRecipeDefinition canonicalRecipe
            ) &&
            canonicalRecipe != null &&
            !string.Equals(
                canonicalRecipe.DishId,
                pair.dish.dishId,
                StringComparison.Ordinal
            ))
        {
            error = "El RecipeId persistente " + pair.recipe.recipeId +
                    " pertenece al plato canónico " +
                    canonicalRecipe.DishId + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void CopyPairs(
        IList<BistroBuilderDishRecipeSaveData> source,
        List<BistroBuilderDishRecipeSaveData> destination
    )
    {
        for (int index = 0; index < source.Count; index++)
        {
            destination.Add(
                BistroBuilderDishRecipeSaveDataUtility.Clone(source[index])
            );
        }
    }

    private static void DestroyRuntimeLists(
        IList<BistroBuilderDishDefinition> dishes,
        IList<BistroBuilderRecipeDefinition> recipes
    )
    {
        for (int index = 0; index < recipes.Count; index++)
        {
            BistroBuilderDishRecipeSaveDataUtility.DestroyRuntimeObject(
                recipes[index]
            );
        }

        for (int index = 0; index < dishes.Count; index++)
        {
            BistroBuilderDishRecipeSaveDataUtility.DestroyRuntimeObject(
                dishes[index]
            );
        }
    }

    private static int ComparePairs(
        BistroBuilderDishRecipeSaveData first,
        BistroBuilderDishRecipeSaveData second
    )
    {
        return string.Compare(
            first != null && first.dish != null
                ? first.dish.dishId
                : string.Empty,
            second != null && second.dish != null
                ? second.dish.dishId
                : string.Empty,
            StringComparison.Ordinal
        );
    }

    private void CacheDependenciesIfNeeded()
    {
        if (dishCatalogService == null)
        {
            TryGetComponent(out dishCatalogService);
        }

        if (recipeCatalogService == null)
        {
            TryGetComponent(out recipeCatalogService);
        }

        if (categoryCatalogService == null)
        {
            TryGetComponent(out categoryCatalogService);
        }
    }

    public sealed class RollbackState
    {
        internal readonly List<BistroBuilderDishDefinition> PreviousDishes =
            new List<BistroBuilderDishDefinition>();
        internal readonly List<BistroBuilderRecipeDefinition> PreviousRecipes =
            new List<BistroBuilderRecipeDefinition>();
        internal readonly List<BistroBuilderDishRecipeSaveData>
            PreviousUnresolvedPairs =
                new List<BistroBuilderDishRecipeSaveData>();
        internal readonly List<string> PreviousSuppressedDishIds =
            new List<string>();
        internal readonly List<BistroBuilderDishDefinition> AppliedDishes =
            new List<BistroBuilderDishDefinition>();
        internal readonly List<BistroBuilderRecipeDefinition> AppliedRecipes =
            new List<BistroBuilderRecipeDefinition>();
        internal bool Applied;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
