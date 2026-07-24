using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Validador del hito 367B sin modificar escenas ni assets.
/// </summary>
public static class BistroBuilderCanonicalOrderFoundationValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Orders/" +
        "Validate 367B Canonical Orders Foundation";

    [MenuItem(MenuPath, false, 201)]
    private static void ValidateFromMenu()
    {
        BistroBuilderCanonicalOrderValidationResult result =
            ValidateCurrentProject();

        Debug.Log(
            "BISTRO BUILDER - VALIDACIÓN 367B\n" +
            result.BuildReport()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderCanonicalOrderValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderCanonicalOrderValidationResult result =
            new BistroBuilderCanonicalOrderValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No existe una escena activa cargada.");
            return result;
        }

        GameObject gameSystems = FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.Error("No se encontró GameSystems.");
            return result;
        }

        result.Ok("GameSystems localizado.");

        BistroBuilderDishCatalogService catalog =
            gameSystems.GetComponent<BistroBuilderDishCatalogService>();
        BistroBuilderRestaurantMenuService menu =
            gameSystems.GetComponent<BistroBuilderRestaurantMenuService>();
        BistroBuilderCanonicalOrderService orders =
            gameSystems.GetComponent<BistroBuilderCanonicalOrderService>();

        if (catalog == null)
        {
            result.Error("Falta BistroBuilderDishCatalogService.");
        }
        else if (!catalog.ValidateConfiguration(out string catalogError))
        {
            result.Error(catalogError);
        }
        else
        {
            result.Ok("Catálogo canónico de platos preparado.");
        }

        if (menu == null)
        {
            result.Error("Falta BistroBuilderRestaurantMenuService.");
        }
        else if (!menu.ValidateConfiguration(out string menuError))
        {
            result.Error(menuError);
        }
        else
        {
            result.Ok("Carta runtime 367A preparada.");
        }

        if (orders == null)
        {
            result.Error("Falta BistroBuilderCanonicalOrderService.");
        }
        else
        {
            SerializedObject serialized = new SerializedObject(orders);
            SerializedProperty menuReference =
                serialized.FindProperty("menuService");

            if (menuReference == null ||
                menuReference.objectReferenceValue == null)
            {
                result.Error(
                    "CanonicalOrderService no tiene asignada la carta."
                );
            }
            else if (!ReferenceEquals(
                         menuReference.objectReferenceValue,
                         menu
                     ))
            {
                result.Error(
                    "CanonicalOrderService apunta a otra carta runtime."
                );
            }
            else
            {
                result.Ok("Dependencia de carta asignada correctamente.");
            }

            if (!orders.ValidateConfiguration(out string orderError))
            {
                result.Error(orderError);
            }
            else
            {
                result.Ok("Servicio canónico de comandas preparado.");
                result.Ok("Índices OrderId y OrderLineId válidos.");
                result.Ok("Snapshot service.runtime preparado.");
            }
        }

        if (gameSystems.GetComponents<
                BistroBuilderCanonicalOrderService
            >().Length > 1)
        {
            result.Error("Existe más de un servicio canónico de comandas.");
        }
        else
        {
            result.Ok("No existen autoridades duplicadas de comandas.");
        }

        if (result.ErrorCount == 0)
        {
            result.Ok(
                "La base 367B está completa sin sustituir todavía el " +
                "flujo legacy de servicio."
            );
        }

        return result;
    }

    public static GameObject FindGameSystems(Scene scene)
    {
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

        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index].GetComponent<
                    BistroBuilderRestaurantMenuService
                >() != null)
            {
                return roots[index];
            }
        }

        return null;
    }
}

public sealed class BistroBuilderCanonicalOrderValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void Ok(string message)
    {
        correct.Add(message);
    }

    public void Warning(string message)
    {
        warnings.Add(message);
    }

    public void Error(string message)
    {
        errors.Add(message);
    }

    public string BuildReport()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        builder.AppendLine("BISTRO BUILDER - COMANDAS CANÓNICAS 367B");
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);

        Append(builder, "OK", correct);
        Append(builder, "AVISO", warnings);
        Append(builder, "ERROR", errors);

        return builder.ToString();
    }

    private static void Append(
        System.Text.StringBuilder builder,
        string prefix,
        List<string> messages
    )
    {
        for (int index = 0; index < messages.Count; index++)
        {
            builder.AppendLine("- " + prefix + ": " + messages[index]);
        }
    }
}
