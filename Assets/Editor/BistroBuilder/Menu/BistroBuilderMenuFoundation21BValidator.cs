using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado no destructivo de 2.1B.
/// </summary>
public sealed class BistroBuilderMenuFoundation21BValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void AddCorrect(string message) => correct.Add(message);
    public void AddWarning(string message) => warnings.Add(message);
    public void AddError(string message) => errors.Add(message);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1B CATEGORÍAS Y EDICIÓN TRANSACCIONAL"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        Append(builder, "OK", correct);
        Append(builder, "ADVERTENCIA", warnings);
        Append(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> messages
    )
    {
        for (int index = 0; index < messages.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(messages[index]);
        }
    }
}

/// <summary>
/// Validador estructural y no destructivo de 2.1B.
/// </summary>
public static class BistroBuilderMenuFoundation21BValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1B Categories and Transactional Editing";

    public const string CategoryCatalogAssetPath =
        "Assets/Data/BistroBuilder/Menu/Categories/" +
        "BistroBuilderDishCategoryCatalog.asset";

    public const string CommercialPolicyAssetPath =
        "Assets/Data/BistroBuilder/Menu/" +
        "BistroBuilderMenuCommercialPolicy.asset";

    private sealed class CanonicalCategory
    {
        public readonly string CategoryId;
        public readonly BistroBuilderDishCategory LegacyCategory;

        public CanonicalCategory(
            string categoryId,
            BistroBuilderDishCategory legacyCategory
        )
        {
            CategoryId = categoryId;
            LegacyCategory = legacyCategory;
        }
    }

    private static readonly CanonicalCategory[] CanonicalCategories =
    {
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.Starter,
            BistroBuilderDishCategory.Starter
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.MainCourse,
            BistroBuilderDishCategory.MainCourse
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.Dessert,
            BistroBuilderDishCategory.Dessert
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.Beverage,
            BistroBuilderDishCategory.Beverage
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.SideDish,
            BistroBuilderDishCategory.SideDish
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.SharedDish,
            BistroBuilderDishCategory.SharedDish
        ),
        new CanonicalCategory(
            BistroBuilderDishCategoryIdUtility.TastingItem,
            BistroBuilderDishCategory.TastingItem
        )
    };

    [MenuItem(MenuPath, false, 141)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuFoundation21BValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else if (result.WarningCount > 0)
        {
            Debug.LogWarning(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderMenuFoundation21BValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuFoundation21BValidationResult result =
            new BistroBuilderMenuFoundation21BValidationResult();

        BistroBuilderMenuState21AValidationResult prerequisite =
            BistroBuilderMenuState21AValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            result.AddError(
                "2.1A no está validado. Corrige primero su informe."
            );
        }
        else
        {
            result.AddCorrect(
                "La autoridad por restaurante y menu.state v2 de 2.1A " +
                "siguen válidos."
            );

            if (prerequisite.WarningCount > 0)
            {
                result.AddWarning(
                    "2.1A mantiene " + prerequisite.WarningCount +
                    " advertencia(s) previa(s)."
                );
            }
        }

        BistroBuilderDishCategoryCatalog categoryCatalog =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderDishCategoryCatalog
            >(CategoryCatalogAssetPath);
        BistroBuilderMenuCommercialPolicy commercialPolicy =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderMenuCommercialPolicy
            >(CommercialPolicyAssetPath);

        ValidateCategoryCatalog(categoryCatalog, result);
        ValidateCommercialPolicy(commercialPolicy, result);
        ValidateDishDefinitions(categoryCatalog, result);

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No existe una escena activa válida.");
            return result;
        }

        GameObject gameSystems =
            BistroBuilderMenuFoundationValidator.FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.AddError("No se encontró GameSystems en la escena activa.");
            return result;
        }

        result.AddCorrect("GameSystems localizado en la escena activa.");

        BistroBuilderDishCatalogService dishCatalogService =
            RequireUnique<BistroBuilderDishCatalogService>(
                gameSystems,
                result
            );
        BistroBuilderRestaurantMenuService menuService =
            RequireUnique<BistroBuilderRestaurantMenuService>(
                gameSystems,
                result
            );
        BistroBuilderRestaurantMenuCollectionService collectionService =
            RequireUnique<BistroBuilderRestaurantMenuCollectionService>(
                gameSystems,
                result
            );
        BistroBuilderDishCategoryCatalogService categoryService =
            RequireUnique<BistroBuilderDishCategoryCatalogService>(
                gameSystems,
                result
            );
        BistroBuilderMenuEditSessionService editSessionService =
            RequireUnique<BistroBuilderMenuEditSessionService>(
                gameSystems,
                result
            );

        if (categoryService != null)
        {
            if (categoryService.Catalog != categoryCatalog)
            {
                result.AddError(
                    "El servicio de categorías no usa el catálogo canónico."
                );
            }
            else if (!categoryService.ValidateConfiguration(
                         out string categoryServiceError
                     ))
            {
                result.AddError(categoryServiceError);
            }
            else
            {
                result.AddCorrect(
                    "Servicio runtime de categorías válido con " +
                    categoryService.CategoryCount + " categoría(s)."
                );
            }
        }

        if (menuService != null)
        {
            if (menuService.CommercialPolicy != commercialPolicy)
            {
                result.AddError(
                    "La carta activa no usa la política comercial canónica."
                );
            }
            else if (!menuService.ValidateConfiguration(
                         out string menuError
                     ))
            {
                result.AddError(menuError);
            }
            else
            {
                result.AddCorrect(
                    "La carta activa respeta precios, capacidad y platos " +
                    "firma de la política comercial."
                );
            }
        }

        if (collectionService != null &&
            dishCatalogService != null &&
            menuService != null)
        {
            if (collectionService.MenuService != menuService ||
                collectionService.CatalogService != dishCatalogService)
            {
                result.AddError(
                    "La colección 2.1A no comparte las autoridades canónicas."
                );
            }
            else
            {
                ValidateAllRestaurantPolicies(
                    collectionService,
                    commercialPolicy,
                    result
                );
            }
        }

        if (editSessionService != null)
        {
            if (editSessionService.MenuService != menuService ||
                editSessionService.CollectionService != collectionService ||
                editSessionService.CatalogService != dishCatalogService ||
                editSessionService.CategoryCatalogService != categoryService ||
                editSessionService.CommercialPolicy != commercialPolicy)
            {
                result.AddError(
                    "La edición transaccional no comparte todos los servicios " +
                    "y assets canónicos."
                );
            }
            else if (!editSessionService.ValidateConfiguration(
                         out string editError
                     ))
            {
                result.AddError(editError);
            }
            else
            {
                result.AddCorrect(
                    "Servicio de edición transaccional configurado y válido."
                );
            }

            if (editSessionService.HasOpenSession)
            {
                result.AddWarning(
                    "Hay una sesión de edición abierta. Debe aplicarse o " +
                    "descartarse antes de guardar o cambiar de restaurante."
                );
            }
            else
            {
                result.AddCorrect(
                    "No quedan borradores de edición abiertos en la escena."
                );
            }
        }

        return result;
    }

    private static void ValidateCategoryCatalog(
        BistroBuilderDishCategoryCatalog catalog,
        BistroBuilderMenuFoundation21BValidationResult result
    )
    {
        if (catalog == null)
        {
            result.AddError(
                "No existe el catálogo canónico de categorías en " +
                CategoryCatalogAssetPath + "."
            );
            return;
        }

        if (!catalog.TryRebuildIndex(out string error))
        {
            result.AddError(error);
            return;
        }

        result.AddCorrect(
            "Catálogo canónico de categorías válido con " + catalog.Count +
            " definición(es)."
        );

        for (int index = 0; index < CanonicalCategories.Length; index++)
        {
            CanonicalCategory expected = CanonicalCategories[index];

            if (!catalog.TryGetDefinition(
                    expected.CategoryId,
                    out BistroBuilderDishCategoryDefinition byId
                ) ||
                byId == null)
            {
                result.AddError(
                    "Falta la categoría canónica " + expected.CategoryId + "."
                );
                continue;
            }

            if (!byId.HasLegacyMapping ||
                byId.LegacyCategory != expected.LegacyCategory ||
                !catalog.TryGetDefinition(
                    expected.LegacyCategory,
                    out BistroBuilderDishCategoryDefinition byLegacy
                ) ||
                byLegacy != byId)
            {
                result.AddError(
                    "La compatibilidad histórica de " +
                    expected.CategoryId + " es incoherente."
                );
                continue;
            }

            result.AddCorrect(
                "Categoría estable registrada: " + expected.CategoryId + "."
            );
        }
    }

    private static void ValidateCommercialPolicy(
        BistroBuilderMenuCommercialPolicy policy,
        BistroBuilderMenuFoundation21BValidationResult result
    )
    {
        if (policy == null)
        {
            result.AddError(
                "No existe la política comercial canónica en " +
                CommercialPolicyAssetPath + "."
            );
            return;
        }

        if (!policy.TryValidate(out string error))
        {
            result.AddError(error);
            return;
        }

        result.AddCorrect(
            "Política comercial válida: precios " +
            policy.MinimumPriceCents + "-" + policy.MaximumPriceCents +
            " céntimos, " + policy.MaximumMenuItems +
            " platos y " + policy.MaximumSignatureDishes +
            " platos firma como máximo."
        );
        if (!policy.RequireSignatureDishEnabled ||
            !policy.RequireSignatureDishUnlocked ||
            !policy.RequireSignatureDishServiceAvailability)
        {
            result.AddError(
                "La política comercial ha desactivado una regla canónica de " +
                "integridad de platos firma."
            );
        }
        else
        {
            result.AddCorrect(
                "Los platos firma exigen activación, desbloqueo y al menos " +
                "un servicio."
            );
        }
    }

    private static void ValidateDishDefinitions(
        BistroBuilderDishCategoryCatalog categoryCatalog,
        BistroBuilderMenuFoundation21BValidationResult result
    )
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );

        if (guids.Length == 0)
        {
            result.AddError("No existe ninguna definición canónica de plato.");
            return;
        }

        HashSet<string> dishIds = new HashSet<string>(StringComparer.Ordinal);
        int projectDefinitionCount = 0;
        int validCount = 0;

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);

            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                continue;
            }

            projectDefinitionCount++;
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition == null)
            {
                result.AddError(
                    "No se pudo cargar una definición localizada en " +
                    path + "."
                );
                continue;
            }

            if (!definition.TryValidate(out string definitionError))
            {
                result.AddError(path + ": " + definitionError);
                continue;
            }

            if (!definition.HasExplicitCategoryId)
            {
                result.AddError(
                    path + " todavía depende del enum histórico y no tiene " +
                    "CategoryId explícito."
                );
                continue;
            }

            if (definition.DefinitionVersion !=
                BistroBuilderDishDefinition.CurrentDefinitionVersion)
            {
                result.AddError(
                    path + " usa una versión de definición no soportada."
                );
                continue;
            }

            if (categoryCatalog == null ||
                !categoryCatalog.TryGetDefinition(
                    definition.CategoryId,
                    out _
                ))
            {
                result.AddError(
                    path + " referencia la categoría no registrada " +
                    definition.CategoryId + "."
                );
                continue;
            }

            if (!dishIds.Add(definition.DishId))
            {
                result.AddError(
                    "El DishId " + definition.DishId +
                    " aparece en más de una definición."
                );
                continue;
            }

            validCount++;
        }

        if (projectDefinitionCount == 0)
        {
            result.AddError(
                "No existe ninguna definición de plato dentro de Assets."
            );
        }
        else if (validCount == projectDefinitionCount)
        {
            result.AddCorrect(
                "Las " + validCount +
                " definiciones de plato tienen versión y categoría estables."
            );
        }
    }

    private static void ValidateAllRestaurantPolicies(
        BistroBuilderRestaurantMenuCollectionService collectionService,
        BistroBuilderMenuCommercialPolicy commercialPolicy,
        BistroBuilderMenuFoundation21BValidationResult result
    )
    {
        List<BistroBuilderRestaurantMenuRuntimeState> restaurants =
            new List<BistroBuilderRestaurantMenuRuntimeState>();

        if (!collectionService.TryGetAllRestaurantSnapshots(
                restaurants,
                out string error
            ))
        {
            result.AddError(error);
            return;
        }

        for (int restaurantIndex = 0;
             restaurantIndex < restaurants.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuRuntimeState restaurant =
                restaurants[restaurantIndex];
            List<BistroBuilderMenuItemRuntimeState> resolved =
                new List<BistroBuilderMenuItemRuntimeState>(
                    restaurant.ItemCount
                );

            for (int itemIndex = 0;
                 itemIndex < restaurant.Items.Count;
                 itemIndex++)
            {
                BistroBuilderMenuItemRuntimeState item =
                    restaurant.Items[itemIndex];
                resolved.Add(item != null ? item.Clone() : null);
            }

            if (!BistroBuilderMenuPolicyEvaluator.TryValidateMenu(
                    resolved,
                    commercialPolicy,
                    out error
                ))
            {
                result.AddError(
                    "La carta de " + restaurant.RestaurantId + ": " + error
                );
                return;
            }
        }

        result.AddCorrect(
            "Las " + restaurants.Count +
            " carta(s) por restaurante respetan la política comercial."
        );
    }

    private static T RequireUnique<T>(
        GameObject gameSystems,
        BistroBuilderMenuFoundation21BValidationResult result
    ) where T : Component
    {
        T[] components = gameSystems.GetComponents<T>();

        if (components.Length == 0)
        {
            result.AddError("Falta " + typeof(T).Name + " en GameSystems.");
            return null;
        }

        if (components.Length > 1)
        {
            result.AddError(
                "Hay " + components.Length + " componentes " +
                typeof(T).Name + " en GameSystems."
            );
            return null;
        }

        result.AddCorrect("Existe un único " + typeof(T).Name + ".");
        return components[0];
    }
}
