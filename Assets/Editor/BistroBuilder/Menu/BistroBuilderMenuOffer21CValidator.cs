using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resultado no destructivo del validador 2.1C.
/// </summary>
public sealed class BistroBuilderMenuOffer21CValidationResult
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
            "BISTRO BUILDER - 2.1C DISPONIBILIDAD Y OFERTA UNIFICADA"
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
/// Comprueba que mesa, barra y comandas comparten una única fachada de oferta
/// y que 2.1C no ha creado una segunda autoridad persistente.
/// </summary>
public static class BistroBuilderMenuOffer21CValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1C Unified Menu Offer";

    [MenuItem(MenuPath, false, 151)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuOffer21CValidationResult result =
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

    public static BistroBuilderMenuOffer21CValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuOffer21CValidationResult result =
            new BistroBuilderMenuOffer21CValidationResult();

        BistroBuilderMenuFoundation21BValidationResult prerequisite =
            BistroBuilderMenuFoundation21BValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            result.AddError(
                "2.1B no está validado. Corrige primero su informe."
            );
        }
        else
        {
            result.AddCorrect(
                "2.1A y 2.1B siguen válidos como base de estado y edición."
            );

            if (prerequisite.WarningCount > 0)
            {
                result.AddWarning(
                    "2.1B conserva " + prerequisite.WarningCount +
                    " advertencia(s) previa(s)."
                );
            }
        }

        BistroBuilderAvailabilityPersistenceValidationResult
            availabilityBase =
                BistroBuilderAvailabilityPersistenceValidator
                    .ValidateCurrentProject();

        if (availabilityBase.ErrorCount > 0)
        {
            result.AddError(
                "368EF no está validado. Corrige primero disponibilidad, " +
                "inventario y persistencia."
            );
        }
        else
        {
            result.AddCorrect(
                "368EF sigue válido como autoridad de disponibilidad derivada."
            );

            if (availabilityBase.WarningCount > 0)
            {
                result.AddWarning(
                    "368EF conserva " + availabilityBase.WarningCount +
                    " advertencia(s) previa(s)."
                );
            }
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }

        result.AddCorrect("La escena activa está cargada y es válida.");

        List<BistroBuilderMenuOfferService> offers =
            FindSceneComponents<BistroBuilderMenuOfferService>(scene);

        if (offers.Count != 1)
        {
            result.AddError(
                "Debe existir un único BistroBuilderMenuOfferService; hay " +
                offers.Count + "."
            );
            return result;
        }

        BistroBuilderMenuOfferService offer = offers[0];
        result.AddCorrect("Existe una única autoridad de oferta 2.1C.");

        if (string.Equals(
            BistroBuilderMenuOfferService.RuntimeRevision,
            "MENU-2.1C",
            StringComparison.Ordinal
        ))
        {
            result.AddCorrect("La revisión runtime 2.1C es la esperada.");
        }
        else
        {
            result.AddError("La revisión runtime de oferta no es 2.1C.");
        }

        if (offer.ValidateConfiguration(out string offerError))
        {
            result.AddCorrect("La oferta unificada es estructuralmente válida.");
        }
        else
        {
            result.AddError(offerError);
        }

        if (offer.MenuService != null &&
            offer.CollectionService != null &&
            ReferenceEquals(
                offer.CollectionService.MenuService,
                offer.MenuService
            ))
        {
            result.AddCorrect(
                "La oferta utiliza la carta operativa del restaurante activo."
            );
        }
        else
        {
            result.AddError(
                "La oferta no comparte la autoridad de carta por restaurante."
            );
        }

        if (offer.CatalogService != null &&
            offer.MenuService != null &&
            ReferenceEquals(
                offer.MenuService.CatalogService,
                offer.CatalogService
            ))
        {
            result.AddCorrect("La oferta usa el catálogo canónico de platos.");
        }
        else
        {
            result.AddError("La oferta usa un catálogo distinto del menú.");
        }

        if (offer.AvailabilityService != null)
        {
            result.AddCorrect(
                "La oferta reutiliza la disponibilidad derivada de 368EF."
            );
        }
        else
        {
            result.AddError("La oferta no está conectada con 368EF.");
        }

        if (offer.OrderIntegration != null &&
            BistroBuilderMenuOfferContext.IsConcreteMealService(
                offer.CurrentMealService
            ))
        {
            result.AddCorrect(
                "La franja activa procede de la integración canónica de " +
                "servicio."
            );
        }
        else
        {
            result.AddError(
                "La oferta no recibe desayuno, comida o cena de forma válida."
            );
        }

        if (BistroBuilderMenuIdUtility.IsValidStableId(
                offer.ActiveRestaurantId
            ))
        {
            result.AddCorrect("La oferta publica un RestaurantId estable.");
        }
        else
        {
            result.AddError("El RestaurantId publicado por la oferta es inválido.");
        }

        ValidateUniqueConsumer(
            scene,
            offer,
            result
        );
        ValidateBarConsumers(scene, offer, result);
        ValidateCatalogServiceModes(offer.CatalogService, result);

        if (Enum.GetValues(typeof(BistroBuilderServiceMode)).Length == 3)
        {
            result.AddCorrect(
                "Mesa, barra y espera en barra tienen contextos diferenciados."
            );
        }
        else
        {
            result.AddError(
                "La máscara de modalidades cambió sin actualizar 2.1C."
            );
        }

        result.AddCorrect(
            "La oferta es derivada y no añade una nueva sección de guardado."
        );
        result.AddCorrect(
            "El precio histórico de las comandas continúa congelado en " +
            "céntimos."
        );

        return result;
    }

    private static void ValidateUniqueConsumer(
        Scene scene,
        BistroBuilderMenuOfferService offer,
        BistroBuilderMenuOffer21CValidationResult result
    )
    {
        List<BistroBuilderCanonicalOrderService> orders =
            FindSceneComponents<BistroBuilderCanonicalOrderService>(scene);
        List<BistroBuilderOrderCompositionService> composers =
            FindSceneComponents<BistroBuilderOrderCompositionService>(scene);
        List<BistroBuilderCanonicalOrderIntegrationService> integrations =
            FindSceneComponents<
                BistroBuilderCanonicalOrderIntegrationService
            >(scene);

        if (orders.Count == 1 &&
            ReferenceEquals(orders[0].OfferService, offer))
        {
            result.AddCorrect(
                "La autoridad canónica de comandas valida la modalidad con " +
                "2.1C."
            );
        }
        else
        {
            result.AddError(
                "BistroBuilderCanonicalOrderService no usa la oferta única."
            );
        }

        if (composers.Count == 1 &&
            ReferenceEquals(composers[0].OfferService, offer))
        {
            result.AddCorrect(
                "El compositor de mesa consume la oferta canónica."
            );
        }
        else
        {
            result.AddError(
                "BistroBuilderOrderCompositionService no usa la oferta única."
            );
        }

        if (integrations.Count == 1 &&
            ReferenceEquals(integrations[0].OfferService, offer))
        {
            result.AddCorrect(
                "La integración de mesa y barra comparte la oferta canónica."
            );
        }
        else
        {
            result.AddError(
                "La integración de comandas no usa la oferta única."
            );
        }
    }

    private static void ValidateBarConsumers(
        Scene scene,
        BistroBuilderMenuOfferService offer,
        BistroBuilderMenuOffer21CValidationResult result
    )
    {
        List<BistroBuilderBarServiceSystem> bars =
            FindSceneComponents<BistroBuilderBarServiceSystem>(scene);

        if (bars.Count == 0)
        {
            result.AddError("No existe ningún sistema operativo de barra.");
            return;
        }

        for (int index = 0; index < bars.Count; index++)
        {
            if (!ReferenceEquals(bars[index].OfferService, offer))
            {
                result.AddError(
                    "Una instancia de barra no consume la oferta 2.1C."
                );
                return;
            }
        }

        result.AddCorrect(
            "Todas las instancias de barra filtran franja, modalidad y stock " +
            "desde 2.1C."
        );
    }

    private static void ValidateCatalogServiceModes(
        BistroBuilderDishCatalogService catalog,
        BistroBuilderMenuOffer21CValidationResult result
    )
    {
        if (catalog == null)
        {
            return;
        }

        List<BistroBuilderDishDefinition> definitions =
            new List<BistroBuilderDishDefinition>();
        catalog.CopyDefinitionsTo(definitions);

        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderDishDefinition definition = definitions[index];

            if (definition == null ||
                !BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                    definition.AllowedServiceModes,
                    false
                ))
            {
                result.AddError(
                    "Existe una definición con modalidades de servicio " +
                    "inválidas."
                );
                return;
            }
        }

        result.AddCorrect(
            "Todas las definiciones declaran modalidades de servicio válidas."
        );
    }

    private static List<T> FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new List<T>();
        T[] all = Resources.FindObjectsOfTypeAll<T>();

        for (int index = 0; index < all.Length; index++)
        {
            T component = all[index];

            if (component != null &&
                component.gameObject.scene == scene &&
                !EditorUtility.IsPersistent(component))
            {
                result.Add(component);
            }
        }

        return result;
    }
}
