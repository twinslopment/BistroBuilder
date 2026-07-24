using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Valida la instalación de la plataforma universal de persistencia.
///
/// No modifica assets ni escena. Su informe sirve tanto al instalador
/// como a Project Health y a las pruebas manuales.
/// </summary>
public static class BistroBuilderPersistenceFoundationValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Persistence/Validate Save Foundation";

    [MenuItem(MenuPath, false, 110)]
    private static void ValidateFromMenu()
    {
        BistroBuilderPersistenceValidationResult result =
            ValidateCurrentProject();

        Debug.Log(
            "BISTRO BUILDER - VALIDACIÓN DE PERSISTENCIA 366B\n" +
            result.BuildReport()
        );

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderPersistenceValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderPersistenceValidationResult result =
            new BistroBuilderPersistenceValidationResult();

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No hay una escena activa válida.");
            return result;
        }

        GameObject gameSystems = FindGameSystems(scene);

        if (gameSystems == null)
        {
            result.AddError("No existe el objeto GameSystems.");
            return result;
        }

        BistroBuilderSaveGameService service =
            gameSystems.GetComponent<BistroBuilderSaveGameService>();
        BistroBuilderSaveDefinitionCatalog catalog =
            gameSystems.GetComponent<BistroBuilderSaveDefinitionCatalog>();
        RestaurantStructureSaveSectionProvider provider =
            gameSystems.GetComponent<RestaurantStructureSaveSectionProvider>();
        BistroBuilderPlacementOperationSaveGuard guard =
            gameSystems.GetComponent<BistroBuilderPlacementOperationSaveGuard>();
        BistroBuilderEditInteractionSaveParticipant participant =
            gameSystems.GetComponent<
                BistroBuilderEditInteractionSaveParticipant
            >();

        BistroBuilderGeneralGameStateService generalState =
            gameSystems.GetComponent<
                BistroBuilderGeneralGameStateService
            >();
        BistroBuilderGeneralGameSaveSectionProvider generalProvider =
            gameSystems.GetComponent<
                BistroBuilderGeneralGameSaveSectionProvider
            >();
        BistroBuilderSimulationSaveParticipant simulationParticipant =
            gameSystems.GetComponent<
                BistroBuilderSimulationSaveParticipant
            >();
        BistroBuilderActiveServiceSaveGuard activeServiceGuard =
            gameSystems.GetComponent<
                BistroBuilderActiveServiceSaveGuard
            >();

        ValidateSingleComponent(
            gameSystems,
            service,
            nameof(BistroBuilderSaveGameService),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            catalog,
            nameof(BistroBuilderSaveDefinitionCatalog),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            provider,
            nameof(RestaurantStructureSaveSectionProvider),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            guard,
            nameof(BistroBuilderPlacementOperationSaveGuard),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            participant,
            nameof(BistroBuilderEditInteractionSaveParticipant),
            result
        );

        ValidateSingleComponent(
            gameSystems,
            generalState,
            nameof(BistroBuilderGeneralGameStateService),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            generalProvider,
            nameof(BistroBuilderGeneralGameSaveSectionProvider),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            simulationParticipant,
            nameof(BistroBuilderSimulationSaveParticipant),
            result
        );
        ValidateSingleComponent(
            gameSystems,
            activeServiceGuard,
            nameof(BistroBuilderActiveServiceSaveGuard),
            result
        );

        if (service != null)
        {
            service.RefreshExtensions();

            if (!service.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderSaveGameService) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "El orquestador de guardado y carga está configurado."
                );
            }

            if (service.RegisteredProviderCount < 2)
            {
                result.AddError(
                    "El orquestador no ha registrado proveedores."
                );
            }
            else
            {
                result.AddOk(
                    "Proveedores registrados: " +
                    service.RegisteredProviderCount + "."
                );
            }
        }

        if (catalog != null)
        {
            catalog.RebuildIndex();

            if (!catalog.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderSaveDefinitionCatalog) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "Catálogo persistente válido con " +
                    catalog.Count + " definición(es)."
                );
            }

            ValidateCatalogCoverage(catalog, result);
        }

        if (provider != null)
        {
            if (!provider.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(RestaurantStructureSaveSectionProvider) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "La sección restaurant.structure está preparada."
                );
            }

            if (!string.Equals(
                    provider.SectionId,
                    RestaurantStructureSaveSectionProvider.StableSectionId,
                    StringComparison.Ordinal
                ) ||
                provider.SectionVersion !=
                    RestaurantStructureSaveSectionProvider
                        .StableSectionVersion)
            {
                result.AddError(
                    "La identidad o versión de restaurant.structure " +
                    "no es estable."
                );
            }
        }

        if (guard != null)
        {
            if (!guard.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderPlacementOperationSaveGuard) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "La protección contra operaciones de colocación " +
                    "incompletas está activa."
                );
            }
        }

        if (participant != null)
        {
            if (!participant.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderEditInteractionSaveParticipant) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "La interacción de edición se bloquea durante " +
                    "snapshots y cargas."
                );
            }
        }

        if (generalState != null)
        {
            if (!generalState.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderGeneralGameStateService) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "La identidad y el calendario general están " +
                    "preparados."
                );
            }
        }

        if (generalProvider != null)
        {
            if (!generalProvider.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderGeneralGameSaveSectionProvider) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk("La sección game.general está preparada.");
            }

            if (!string.Equals(
                    generalProvider.SectionId,
                    BistroBuilderGeneralGameSaveSectionProvider
                        .StableSectionId,
                    StringComparison.Ordinal
                ) ||
                generalProvider.SectionVersion !=
                    BistroBuilderGeneralGameSaveSectionProvider
                        .StableSectionVersion)
            {
                result.AddError(
                    "La identidad o versión de game.general no es estable."
                );
            }

            if (generalProvider.PrepareOrder <=
                    generalProvider.ApplyOrder ||
                generalProvider.FinalizeOrder <=
                    generalProvider.ApplyOrder)
            {
                result.AddError(
                    "El orden por fases de game.general no garantiza " +
                    "cierre temprano y reactivación tardía."
                );
            }
        }

        if (simulationParticipant != null)
        {
            if (!simulationParticipant.ValidateConfiguration(
                    out string error
                ))
            {
                result.AddError(
                    nameof(BistroBuilderSimulationSaveParticipant) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "El reloj se congela mediante un bloqueo apilable " +
                    "durante snapshots y cargas."
                );
            }
        }

        if (activeServiceGuard != null)
        {
            if (!activeServiceGuard.ValidateConfiguration(out string error))
            {
                result.AddError(
                    nameof(BistroBuilderActiveServiceSaveGuard) +
                    ": " + error
                );
            }
            else
            {
                result.AddOk(
                    "El guardado de servicio activo queda reservado a " +
                    "service.runtime sin acoplar el núcleo."
                );
            }
        }

        ValidateRequiredExistingServices(gameSystems, result);

        if (result.ErrorCount == 0)
        {
            result.AddOk(
                "La persistencia general 366B está completa y preparada " +
                "para snapshots de servicio activo."
            );
        }

        return result;
    }

    private static void ValidateCatalogCoverage(
        BistroBuilderSaveDefinitionCatalog catalog,
        BistroBuilderPersistenceValidationResult result
    )
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:RestaurantPlaceableItemDefinition"
        );

        Dictionary<string, RestaurantPlaceableItemDefinition>
            projectDefinitions =
                new Dictionary<
                    string,
                    RestaurantPlaceableItemDefinition
                >(StringComparer.Ordinal);

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(path);

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            string itemId = NormalizeId(definition.ItemId);

            if (!projectDefinitions.ContainsKey(itemId))
            {
                projectDefinitions.Add(itemId, definition);
            }
        }

        for (int index = 0;
             index < catalog.Definitions.Count;
             index++)
        {
            RestaurantPlaceableItemDefinition definition =
                catalog.Definitions[index];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            projectDefinitions.Remove(
                NormalizeId(definition.ItemId)
            );
        }

        if (projectDefinitions.Count > 0)
        {
            foreach (KeyValuePair<
                         string,
                         RestaurantPlaceableItemDefinition
                     > missing in projectDefinitions)
            {
                result.AddError(
                    "La definición " + missing.Key +
                    " no está incluida en el catálogo de carga."
                );
            }

            return;
        }

        result.AddOk(
            "Todas las definiciones colocables del proyecto están " +
            "incluidas en el catálogo de carga."
        );
    }

    private static void ValidateRequiredExistingServices(
        GameObject gameSystems,
        BistroBuilderPersistenceValidationResult result
    )
    {
        Type[] requiredTypes =
        {
            typeof(RestaurantPlaceableRegistry),
            typeof(RestaurantPlaceableLifecycleService),
            typeof(RestaurantPlacementValidationService),
            typeof(RestaurantPlacementHistoryService),
            typeof(RestaurantSeatingTopologyService),
            typeof(RestaurantPlacementTransactionService),
            typeof(RestaurantPlaceableCreationService),
            typeof(RestaurantEditInteractionController),
            typeof(GameClock),
            typeof(RestaurantServiceStateService)
        };

        for (int index = 0; index < requiredTypes.Length; index++)
        {
            Type requiredType = requiredTypes[index];

            if (gameSystems.GetComponent(requiredType) == null)
            {
                result.AddError(
                    "GameSystems no contiene " +
                    requiredType.Name + "."
                );
            }
        }
    }

    private static void ValidateSingleComponent<T>(
        GameObject gameSystems,
        T component,
        string displayName,
        BistroBuilderPersistenceValidationResult result
    ) where T : Component
    {
        T[] components = gameSystems.GetComponents<T>();

        if (component == null || components.Length == 0)
        {
            result.AddError("Falta " + displayName + ".");
            return;
        }

        if (components.Length > 1)
        {
            result.AddError(
                displayName + " está duplicado en GameSystems."
            );
            return;
        }

        result.AddOk(displayName + " instalado.");
    }

    internal static GameObject FindGameSystems(Scene scene)
    {
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

        for (int index = 0; index < roots.Length; index++)
        {
            GameObject root = roots[index];

            if (root != null &&
                root.GetComponentInChildren<
                    RestaurantPlaceableRegistry
                >(true) != null)
            {
                return root;
            }
        }

        return null;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Informe acumulado del validador de persistencia.
/// </summary>
public sealed class BistroBuilderPersistenceValidationResult
{
    private readonly List<string> okMessages =
        new List<string>();
    private readonly List<string> warningMessages =
        new List<string>();
    private readonly List<string> errorMessages =
        new List<string>();

    public int OkCount => okMessages.Count;
    public int WarningCount => warningMessages.Count;
    public int ErrorCount => errorMessages.Count;

    public void AddOk(string message)
    {
        okMessages.Add(message ?? string.Empty);
    }

    public void AddWarning(string message)
    {
        warningMessages.Add(message ?? string.Empty);
    }

    public void AddError(string message)
    {
        errorMessages.Add(message ?? string.Empty);
    }

    public string BuildReport()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder(1024);

        builder.AppendLine("BISTRO BUILDER - PERSISTENCIA");
        builder.AppendLine("Correctos: " + OkCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);

        AppendMessages(builder, "OK", okMessages);
        AppendMessages(builder, "AVISO", warningMessages);
        AppendMessages(builder, "ERROR", errorMessages);

        return builder.ToString();
    }

    private static void AppendMessages(
        System.Text.StringBuilder builder,
        string prefix,
        IReadOnlyList<string> messages
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
