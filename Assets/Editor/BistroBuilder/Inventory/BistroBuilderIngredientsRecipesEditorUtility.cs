using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Utilidades editoriales compartidas por instalador y estudio de autoría
/// 368A. Todas las rutas son datos, no dependencias de escena.
/// </summary>
public static class BistroBuilderIngredientsRecipesEditorUtility
{
    public const string IngredientDefinitionsFolder =
        "Assets/Data/BistroBuilder/Ingredients/Definitions";

    public const string IngredientCatalogPath =
        "Assets/Data/BistroBuilder/Ingredients/" +
        "BistroBuilderIngredientCatalog.asset";

    public const string RecipeDefinitionsFolder =
        "Assets/Data/BistroBuilder/Recipes/Definitions";

    public const string RecipeCatalogPath =
        "Assets/Data/BistroBuilder/Recipes/" +
        "BistroBuilderRecipeCatalog.asset";

    public const string DishDefinitionsFolder =
        "Assets/Data/BistroBuilder/Menu/Definitions";

    public const string DishCatalogPath =
        "Assets/Data/BistroBuilder/Menu/BistroBuilderDishCatalog.asset";

    public const string ChairPrefabPath =
        "Assets/Prefabs/Restaurant/Generated/Furniture/" +
        "SillaBistroDeMadera.prefab";

    public static string GetIngredientAssetPath(string ingredientId)
    {
        return IngredientDefinitionsFolder + "/" +
               BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId) +
               ".asset";
    }

    public static string GetRecipeAssetPath(string recipeId)
    {
        return RecipeDefinitionsFolder + "/" +
               BistroBuilderMenuIdUtility.NormalizeStableId(recipeId) +
               ".asset";
    }

    public static string GetDishAssetPath(string dishId)
    {
        return DishDefinitionsFolder + "/" +
               BistroBuilderMenuIdUtility.NormalizeStableId(dishId) +
               ".asset";
    }

    public static void EnsureDataFolders()
    {
        EnsureFolder(IngredientDefinitionsFolder);
        EnsureFolder(RecipeDefinitionsFolder);
        EnsureFolder(DishDefinitionsFolder);
    }

    public static void EnsureFolder(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/').TrimEnd('/');

        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string parent = Path.GetDirectoryName(normalized)
            ?.Replace('\\', '/');
        string name = Path.GetFileName(normalized);

        if (string.IsNullOrWhiteSpace(parent) ||
            string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Ruta de carpeta inválida: " + folderPath + "."
            );
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static BistroBuilderIngredientCatalog
        LoadOrCreateIngredientCatalog()
    {
        BistroBuilderIngredientCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientCatalog>(
                IngredientCatalogPath
            );

        if (catalog != null)
        {
            return catalog;
        }

        EnsureFolder(Path.GetDirectoryName(IngredientCatalogPath)
            ?.Replace('\\', '/'));

        catalog = ScriptableObject.CreateInstance<
            BistroBuilderIngredientCatalog
        >();
        AssetDatabase.CreateAsset(catalog, IngredientCatalogPath);
        return catalog;
    }

    public static BistroBuilderRecipeCatalog LoadOrCreateRecipeCatalog()
    {
        BistroBuilderRecipeCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderRecipeCatalog>(
                RecipeCatalogPath
            );

        if (catalog != null)
        {
            return catalog;
        }

        EnsureFolder(Path.GetDirectoryName(RecipeCatalogPath)
            ?.Replace('\\', '/'));

        catalog = ScriptableObject.CreateInstance<
            BistroBuilderRecipeCatalog
        >();
        AssetDatabase.CreateAsset(catalog, RecipeCatalogPath);
        return catalog;
    }

    public static BistroBuilderDishCatalog RequireDishCatalog()
    {
        BistroBuilderDishCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderDishCatalog>(
                DishCatalogPath
            );

        if (catalog == null)
        {
            throw new InvalidOperationException(
                "No existe el catálogo de platos 367A en " +
                DishCatalogPath + "."
            );
        }

        return catalog;
    }

    public static void RebuildAllCatalogs(
        BistroBuilderIngredientCatalog ingredientCatalog,
        BistroBuilderRecipeCatalog recipeCatalog,
        BistroBuilderDishCatalog dishCatalog
    )
    {
        RebuildIngredientCatalog(ingredientCatalog);
        RebuildDishCatalog(dishCatalog);
        RebuildRecipeCatalog(recipeCatalog);
    }

    public static void RebuildIngredientCatalog(
        BistroBuilderIngredientCatalog catalog
    )
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderIngredientDefinition"
        );
        var definitions =
            new List<BistroBuilderIngredientDefinition>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderIngredientDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            (first, second) => string.Compare(
                first != null ? first.IngredientId : string.Empty,
                second != null ? second.IngredientId : string.Empty,
                StringComparison.Ordinal
            )
        );

        AssignObjectList(catalog, "definitions", definitions);

        if (!catalog.TryRebuildIndex(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public static void RebuildRecipeCatalog(
        BistroBuilderRecipeCatalog catalog
    )
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderRecipeDefinition"
        );
        var definitions =
            new List<BistroBuilderRecipeDefinition>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderRecipeDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderRecipeDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            (first, second) => string.Compare(
                first != null ? first.RecipeId : string.Empty,
                second != null ? second.RecipeId : string.Empty,
                StringComparison.Ordinal
            )
        );

        AssignObjectList(catalog, "definitions", definitions);

        if (!catalog.TryRebuildIndex(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public static void RebuildDishCatalog(BistroBuilderDishCatalog catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );
        var definitions =
            new List<BistroBuilderDishDefinition>(guids.Length);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            (first, second) => string.Compare(
                first != null ? first.DishId : string.Empty,
                second != null ? second.DishId : string.Empty,
                StringComparison.Ordinal
            )
        );

        AssignObjectList(catalog, "definitions", definitions);

        if (!catalog.TryRebuildIndex(out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public static BistroBuilderIngredientDefinition FindIngredientById(
        string ingredientId
    )
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderIngredientDefinition"
        );

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderIngredientDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderIngredientDefinition
                >(path);

            if (definition != null &&
                string.Equals(
                    definition.IngredientId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return definition;
            }
        }

        return null;
    }

    public static BistroBuilderRecipeDefinition FindRecipeById(
        string recipeId
    )
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(recipeId);
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderRecipeDefinition"
        );

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderRecipeDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderRecipeDefinition
                >(path);

            if (definition != null &&
                string.Equals(
                    definition.RecipeId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return definition;
            }
        }

        return null;
    }

    public static BistroBuilderDishDefinition FindDishById(string dishId)
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition != null &&
                string.Equals(
                    definition.DishId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                return definition;
            }
        }

        return null;
    }

    public static GameObject FindGameSystems(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal
                ))
            {
                return roots[index];
            }
        }

        return null;
    }

    public static T[] FindSceneObjects<T>(Scene scene)
        where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        var result = new List<T>(all.Length);

        for (int index = 0; index < all.Length; index++)
        {
            T item = all[index];

            if (item != null &&
                !EditorUtility.IsPersistent(item) &&
                item.gameObject.scene == scene)
            {
                result.Add(item);
            }
        }

        return result.ToArray();
    }

    public static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    public static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                serialized.targetObject.GetType().Name +
                " no contiene la propiedad " + propertyName + "."
            );
        }

        return property;
    }

    public static double CentsToEuros(int cents)
    {
        if (cents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cents));
        }

        return cents / 100d;
    }

    public static int EurosToCents(double euros)
    {
        if (double.IsNaN(euros) ||
            double.IsInfinity(euros) ||
            euros < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(euros));
        }

        decimal cents = decimal.Round(
            (decimal)euros * 100m,
            0,
            MidpointRounding.AwayFromZero
        );

        if (cents > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(euros));
        }

        return (int)cents;
    }

    private static void AssignObjectList<T>(
        UnityEngine.Object target,
        string propertyName,
        List<T> objects
    ) where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty list = RequireProperty(serialized, propertyName);
        list.arraySize = objects.Count;

        for (int index = 0; index < objects.Count; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                objects[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
