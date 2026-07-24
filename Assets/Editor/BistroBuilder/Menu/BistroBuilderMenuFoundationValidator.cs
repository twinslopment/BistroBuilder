using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado acumulado de la validación de BistroBuilder 367A.
/// </summary>
public sealed class BistroBuilderMenuValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void AddCorrect(string message)
    {
        correct.Add(message);
    }

    public void AddWarning(string message)
    {
        warnings.Add(message);
    }

    public void AddError(string message)
    {
        errors.Add(message);
    }

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine("BISTRO BUILDER - CARTA Y PLATOS 367A");
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);

        AppendGroup(builder, "OK", correct);
        AppendGroup(builder, "ADVERTENCIA", warnings);
        AppendGroup(builder, "ERROR", errors);

        return builder.ToString().TrimEnd();
    }

    private static void AppendGroup(
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
/// Validador no destructivo del catálogo canónico, carta runtime y
/// proveedor menu.state.
/// </summary>
public static class BistroBuilderMenuFoundationValidator
{
    public const string CatalogAssetPath =
        "Assets/Data/BistroBuilder/Menu/BistroBuilderDishCatalog.asset";

    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 367A Dish & Menu Foundation";

    [MenuItem(MenuPath, false, 110)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuValidationResult result = ValidateCurrentProject();

        Debug.Log(result.BuildReport());

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderMenuValidationResult ValidateCurrentProject()
    {
        BistroBuilderMenuValidationResult result =
            new BistroBuilderMenuValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No existe una escena activa válida.");
            return result;
        }

        GameObject gameSystems = FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.AddError("No se encontró GameSystems en la escena activa.");
            return result;
        }

        result.AddCorrect("GameSystems localizado en la escena activa.");

        BistroBuilderDishCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderDishCatalog>(
                CatalogAssetPath
            );

        if (catalog == null)
        {
            result.AddError(
                "No existe el catálogo en " + CatalogAssetPath + "."
            );
        }
        else if (!catalog.TryRebuildIndex(out string catalogError))
        {
            result.AddError(catalogError);
        }
        else
        {
            result.AddCorrect(
                "Catálogo canónico válido con " +
                catalog.DefinitionCount + " plato(s)."
            );
        }

        string[] definitionGuids = AssetDatabase.FindAssets(
            "t:BistroBuilderDishDefinition"
        );

        if (definitionGuids.Length == 0)
        {
            result.AddError("No existen definiciones canónicas de platos.");
        }
        else
        {
            ValidateAllDefinitions(
                definitionGuids,
                catalog,
                result
            );
        }

        BistroBuilderDishCatalogService catalogService =
            gameSystems.GetComponent<BistroBuilderDishCatalogService>();
        BistroBuilderRestaurantMenuService menuService =
            gameSystems.GetComponent<BistroBuilderRestaurantMenuService>();
        BistroBuilderMenuSaveSectionProvider menuProvider =
            gameSystems.GetComponent<BistroBuilderMenuSaveSectionProvider>();
        BistroBuilderSaveGameService saveGameService =
            gameSystems.GetComponent<BistroBuilderSaveGameService>();

        ValidateComponent(
            catalogService,
            "BistroBuilderDishCatalogService instalado.",
            result
        );
        ValidateComponent(
            menuService,
            "BistroBuilderRestaurantMenuService instalado.",
            result
        );
        ValidateComponent(
            menuProvider,
            "BistroBuilderMenuSaveSectionProvider instalado.",
            result
        );
        ValidateComponent(
            saveGameService,
            "BistroBuilderSaveGameService disponible.",
            result
        );

        if (catalogService != null)
        {
            if (catalogService.Catalog != catalog)
            {
                result.AddError(
                    "Dish Catalog Service no referencia el catálogo oficial."
                );
            }
            else if (!catalogService.ValidateConfiguration(
                         out string serviceError
                     ))
            {
                result.AddError(serviceError);
            }
            else
            {
                result.AddCorrect(
                    "Servicio de catálogo indexado y preparado."
                );
            }
        }

        if (menuService != null)
        {
            if (menuService.CatalogService != catalogService)
            {
                result.AddError(
                    "Restaurant Menu Service no referencia el servicio de catálogo."
                );
            }
            else if (!menuService.ValidateConfiguration(
                         out string menuError
                     ))
            {
                // En Edit Mode la carta puede estar vacía antes de Awake,
                // pero su configuración debe seguir siendo válida.
                if (menuService.ItemCount == 0 &&
                    catalogService != null &&
                    catalogService.ValidateConfiguration(out _))
                {
                    result.AddCorrect(
                        "Carta runtime preparada para inicializarse desde el catálogo."
                    );
                }
                else
                {
                    result.AddError(menuError);
                }
            }
            else
            {
                result.AddCorrect(
                    "Carta runtime válida con " + menuService.ItemCount +
                    " entrada(s) serializada(s)."
                );
            }
        }

        if (menuProvider != null)
        {
            if (menuProvider.MenuService != menuService ||
                menuProvider.CatalogService != catalogService)
            {
                result.AddError(
                    "menu.state no tiene sus dependencias correctamente asignadas."
                );
            }
            else if (menuService != null && menuService.ItemCount == 0)
            {
                result.AddCorrect(
                    "menu.state está preparado para la carta inicial runtime."
                );
            }
            else if (!menuProvider.ValidateConfiguration(
                         out string providerError
                     ))
            {
                result.AddError(providerError);
            }
            else
            {
                result.AddCorrect("La sección menu.state está preparada.");
            }
        }

        if (saveGameService != null)
        {
            saveGameService.RefreshExtensions();

            if (!saveGameService.HasProvider(
                    BistroBuilderMenuSaveSectionProvider.StableSectionId
                ))
            {
                result.AddError(
                    "La plataforma 366 no ha registrado menu.state."
                );
            }
            else
            {
                result.AddCorrect(
                    "menu.state registrada en la plataforma universal de guardado."
                );
            }

            if (saveGameService.RegisteredProviderCount < 3)
            {
                result.AddError(
                    "Se esperaban al menos tres proveedores de persistencia."
                );
            }
            else
            {
                result.AddCorrect(
                    "Proveedores de persistencia registrados: " +
                    saveGameService.RegisteredProviderCount + "."
                );
            }
        }

        if (result.ErrorCount == 0)
        {
            result.AddCorrect(
                "BistroBuilder 367A está completo y listo para pruebas runtime."
            );
        }

        return result;
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
            GameObject root = roots[index];

            if (root != null &&
                string.Equals(
                    root.name,
                    "GameSystems",
                    StringComparison.Ordinal
                ))
            {
                return root;
            }
        }

        return null;
    }

    private static void ValidateAllDefinitions(
        string[] guids,
        BistroBuilderDishCatalog catalog,
        BistroBuilderMenuValidationResult result
    )
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int validCount = 0;
        int cataloguedCount = 0;

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            BistroBuilderDishDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderDishDefinition
                >(path);

            if (definition == null)
            {
                result.AddError(
                    "No se pudo cargar una definición localizada en " + path + "."
                );
                continue;
            }

            if (!definition.TryValidate(out string error))
            {
                result.AddError(path + ": " + error);
                continue;
            }

            if (!ids.Add(definition.DishId))
            {
                result.AddError(
                    "DishId duplicado en el proyecto: " +
                    definition.DishId + "."
                );
                continue;
            }

            validCount++;

            if (catalog != null && catalog.Contains(definition.DishId))
            {
                cataloguedCount++;
            }
        }

        result.AddCorrect(
            "Definiciones de plato válidas y con ID único: " +
            validCount + "."
        );

        if (catalog != null && cataloguedCount != validCount)
        {
            result.AddError(
                "El catálogo no incluye todas las definiciones válidas del proyecto."
            );
        }
        else if (catalog != null)
        {
            result.AddCorrect(
                "Todas las definiciones válidas están incluidas en el catálogo."
            );
        }
    }

    private static void ValidateComponent<T>(
        T component,
        string successMessage,
        BistroBuilderMenuValidationResult result
    ) where T : Component
    {
        if (component == null)
        {
            result.AddError("Falta " + typeof(T).Name + ".");
        }
        else
        {
            result.AddCorrect(successMessage);
        }
    }
}
