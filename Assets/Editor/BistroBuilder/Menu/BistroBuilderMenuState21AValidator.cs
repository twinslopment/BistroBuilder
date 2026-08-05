using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado del cierre técnico 2.1A: autoridad de carta por restaurante y
/// migración compatible de menu.state.
/// </summary>
public sealed class BistroBuilderMenuState21AValidationResult
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
        StringBuilder builder = new StringBuilder(3072);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1A CARTA POR RESTAURANTE Y MIGRACIÓN"
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
/// Validador no destructivo del contrato 2.1A.
/// </summary>
public static class BistroBuilderMenuState21AValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1A Restaurant Menu State";

    public const string AccidentalCopyFolder =
        "Assets/Scripts/Application/Menu - copia";

    [MenuItem(MenuPath, false, 131)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuState21AValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderMenuState21AValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuState21AValidationResult result =
            new BistroBuilderMenuState21AValidationResult();

        if (AssetDatabase.IsValidFolder(AccidentalCopyFolder))
        {
            result.AddError(
                "Existe la copia accidental 'Menu - copia'. Contiene tipos y " +
                "GUID duplicados y debe eliminarse antes de compilar."
            );
        }
        else
        {
            result.AddCorrect("No existen copias duplicadas del módulo Menu.");
        }

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

        result.AddCorrect("GameSystems localizado.");

        BistroBuilderDishCatalogService catalogService =
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
        BistroBuilderMenuSaveSectionProvider provider =
            RequireUnique<BistroBuilderMenuSaveSectionProvider>(
                gameSystems,
                result
            );
        BistroBuilderMenuStateV1ToV2Migration migration =
            RequireUnique<BistroBuilderMenuStateV1ToV2Migration>(
                gameSystems,
                result
            );
        BistroBuilderSaveGameService saveService =
            RequireUnique<BistroBuilderSaveGameService>(
                gameSystems,
                result
            );

        if (catalogService != null)
        {
            if (catalogService.ValidateConfiguration(out string catalogError))
            {
                result.AddCorrect("Catálogo canónico válido.");
            }
            else
            {
                result.AddError(catalogError);
            }
        }

        if (menuService != null)
        {
            if (menuService.ValidateConfiguration(out string menuError))
            {
                result.AddCorrect(
                    "Carta operativa válida con " + menuService.ItemCount +
                    " plato(s)."
                );
            }
            else
            {
                result.AddError(menuError);
            }
        }

        if (collectionService != null)
        {
            if (collectionService.MenuService != menuService ||
                collectionService.CatalogService != catalogService)
            {
                result.AddError(
                    "La colección no comparte la carta y el catálogo canónicos."
                );
            }
            else if (!collectionService.ValidateConfiguration(
                         out string collectionError
                     ))
            {
                result.AddError(collectionError);
            }
            else
            {
                result.AddCorrect(
                    "Autoridad por restaurante válida: " +
                    collectionService.RestaurantCount + " restaurante(s)."
                );
                result.AddCorrect(
                    "RestaurantId activo estable: " +
                    collectionService.ActiveRestaurantId + "."
                );

                if (collectionService.UnresolvedItemCount > 0)
                {
                    result.AddWarning(
                        "Hay " + collectionService.UnresolvedItemCount +
                        " plato(s) no resuelto(s); sus datos se conservan pero " +
                        "no se ofrecen hasta recuperar sus definiciones."
                    );
                }
                else
                {
                    result.AddCorrect("No hay DishId no resueltos.");
                }
            }
        }

        if (provider != null)
        {
            if (provider.SectionId !=
                    BistroBuilderMenuSaveSectionProvider.StableSectionId ||
                provider.SectionVersion < 2 ||
                provider.StateType != typeof(BistroBuilderMenuSaveData))
            {
                result.AddError("menu.state no expone el contrato v2 esperado.");
            }
            else if (provider.MenuService != menuService ||
                     provider.CatalogService != catalogService ||
                     provider.CollectionService != collectionService)
            {
                result.AddError(
                    "menu.state no referencia las autoridades canónicas."
                );
            }
            else if (!provider.ValidateConfiguration(
                         out string providerError
                     ))
            {
                result.AddError(providerError);
            }
            else
            {
                result.AddCorrect(
                    "menu.state v2 o posterior está configurada y preparada."
                );
            }
        }

        if (migration != null)
        {
            if (migration.SectionId !=
                    BistroBuilderMenuSaveSectionProvider.StableSectionId ||
                migration.FromVersion != 1 ||
                migration.ToVersion != 2 ||
                migration.FromSerializerId !=
                    BistroBuilderJsonSaveSerializer.StableSerializerId ||
                migration.ToSerializerId !=
                    BistroBuilderJsonSaveSerializer.StableSerializerId)
            {
                result.AddError(
                    "La migración menu.state v1 -> v2 es inválida."
                );
            }
            else
            {
                result.AddCorrect(
                    "Migración consecutiva menu.state v1 -> v2 registrada."
                );
            }
        }

        if (saveService != null)
        {
            saveService.RefreshExtensions();

            if (!saveService.HasProvider(
                    BistroBuilderMenuSaveSectionProvider.StableSectionId
                ))
            {
                result.AddError("La plataforma 366 no registra menu.state.");
            }
            else if (!saveService.ValidateConfiguration(
                         out string saveError
                     ))
            {
                result.AddError(saveError);
            }
            else
            {
                result.AddCorrect(
                    "La plataforma universal registra proveedor y migración."
                );
            }
        }

        BistroBuilderMenuValidationResult legacy =
            BistroBuilderMenuFoundationValidator.ValidateCurrentProject();

        if (legacy.ErrorCount == 0)
        {
            result.AddCorrect(
                "La base 367A continúa superando su validación de regresión."
            );
        }
        else
        {
            result.AddError(
                "La regresión 367A presenta " + legacy.ErrorCount +
                " error(es)."
            );
        }

        return result;
    }

    private static T RequireUnique<T>(
        GameObject target,
        BistroBuilderMenuState21AValidationResult result
    ) where T : Component
    {
        T[] components = target.GetComponents<T>();

        if (components.Length == 0)
        {
            result.AddError("Falta " + typeof(T).Name + ".");
            return null;
        }

        if (components.Length > 1)
        {
            result.AddError(
                "Existen " + components.Length + " instancias de " +
                typeof(T).Name + "."
            );
            return null;
        }

        result.AddCorrect(typeof(T).Name + " es único.");
        return components[0];
    }
}
