using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderBarServiceValidationResult
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
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("BISTRO BUILDER - SERVICIO DE BARRA 367H");
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
            builder.AppendLine("- " + prefix + ": " + messages[index]);
        }
    }
}

/// <summary>
/// Verifica código, escena, destinos, catálogo y conexiones 367H sin alterar
/// el proyecto salvo la reconstrucción no serializada de índices runtime.
/// </summary>
public static class BistroBuilderBarServiceValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Service/" +
        "Validate 367H Bar Service";

    private static readonly string[] RequiredDishIds =
    {
        "dish_agua_mineral",
        "dish_refresco",
        "dish_copa_vino",
        "dish_aceitunas_alinadas",
        "dish_pincho_tortilla"
    };

    [MenuItem(MenuPath, false, 266)]
    private static void ValidateFromMenu()
    {
        BistroBuilderBarServiceValidationResult result =
            ValidateCurrentScene();

        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            result.BuildReport(),
            "Aceptar"
        );
    }

    public static BistroBuilderBarServiceValidationResult
        ValidateCurrentScene()
    {
        BistroBuilderBarServiceValidationResult result =
            new BistroBuilderBarServiceValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.AddError("No hay una escena activa cargada.");
            return result;
        }

        ValidateRuntimeRevisions(result);
        ValidateBarFixtures(scene, result);
        ValidateTables(scene, result);
        ValidateMenu(scene, result);
        ValidateSystems(scene, result);
        return result;
    }

    private static void ValidateRuntimeRevisions(
        BistroBuilderBarServiceValidationResult result
    )
    {
        if (string.Equals(
                Waiter.RuntimeRevision,
                "367H",
                StringComparison.Ordinal
            ) &&
            string.Equals(
                WaiterTaskCoordinator.RuntimeRevision,
                "367H",
                StringComparison.Ordinal
            ) &&
            string.Equals(
                FoodDeliveryServiceFlow.RuntimeRevision,
                "367H",
                StringComparison.Ordinal
            ))
        {
            result.AddCorrect("Las revisiones runtime 367H están activas.");
        }
        else
        {
            result.AddError("Las revisiones runtime no corresponden a 367H.");
        }
    }

    private static void ValidateBarFixtures(
        Scene scene,
        BistroBuilderBarServiceValidationResult result
    )
    {
        BistroBuilder367HInstalledFixture[] fixtures =
            BistroBuilderBarServiceInstaller
                .FindSceneObjects<BistroBuilder367HInstalledFixture>(scene);
        int barFixtureCount = 0;

        for (int index = 0; index < fixtures.Length; index++)
        {
            if (fixtures[index] != null &&
                string.Equals(
                    fixtures[index].FixtureId,
                    "fixture_367h_bar",
                    StringComparison.Ordinal
                ))
            {
                barFixtureCount++;
            }
        }

        if (barFixtureCount == 1)
        {
            result.AddCorrect("Existe una única barra fija 367H.");
        }
        else
        {
            result.AddError(
                "Deben existir exactamente una barra fija; encontradas " +
                barFixtureCount + "."
            );
        }

        BistroBuilderBarServiceSpot[] spots =
            BistroBuilderBarServiceInstaller
                .FindSceneObjects<BistroBuilderBarServiceSpot>(scene);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        if (spots.Length == 4)
        {
            result.AddCorrect("La barra contiene cuatro plazas operativas.");
        }
        else
        {
            result.AddError(
                "Se esperaban cuatro plazas de barra y hay " +
                spots.Length + "."
            );
        }

        int validSpots = 0;

        for (int index = 0; index < spots.Length; index++)
        {
            BistroBuilderBarServiceSpot spot = spots[index];
            string error = string.Empty;
            bool valid = spot != null &&
                spot.ValidateConfiguration(out error) &&
                ids.Add(spot.BarSpotId);

            if (valid)
            {
                validSpots++;
            }
            else
            {
                result.AddError(
                    spot == null
                        ? "Existe una plaza de barra nula."
                        : string.IsNullOrWhiteSpace(error)
                            ? "BarSpotId duplicado: " + spot.BarSpotId + "."
                            : error
                );
            }
        }

        if (validSpots == spots.Length && spots.Length == 4)
        {
            result.AddCorrect(
                "Las cuatro plazas tienen identidad y puntos válidos."
            );
        }

        int stools = 0;

        for (int index = 0; index < fixtures.Length; index++)
        {
            if (fixtures[index] == null ||
                !string.Equals(
                    fixtures[index].FixtureId,
                    "fixture_367h_bar",
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            Transform[] children = fixtures[index]
                .GetComponentsInChildren<Transform>(true);

            for (int child = 0; child < children.Length; child++)
            {
                if (children[child].name == "ProvisionalStool")
                {
                    stools++;
                }
            }
        }

        if (stools == 4)
        {
            result.AddCorrect("La barra incorpora cuatro taburetes provisionales.");
        }
        else
        {
            result.AddError(
                "La barra debe contener cuatro taburetes; detectados " +
                stools + "."
            );
        }

        RestaurantPlacementObstacle[] obstacles =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                RestaurantPlacementObstacle
            >(scene);
        int barObstacleCount = 0;

        for (int index = 0; index < obstacles.Length; index++)
        {
            RestaurantPlacementObstacle obstacle = obstacles[index];

            if (obstacle != null &&
                string.Equals(
                    obstacle.ObstacleId,
                    "placement_obstacle_367h_bar",
                    StringComparison.Ordinal
                ) &&
                obstacle.BlocksPlacement &&
                obstacle.Operational)
            {
                barObstacleCount++;
            }
        }

        if (barObstacleCount == 1)
        {
            result.AddCorrect(
                "La barra bloquea colocaciones incompatibles mediante un " +
                "obstáculo fijo único."
            );
        }
        else
        {
            result.AddError(
                "La barra necesita un único obstáculo de colocación activo; " +
                "detectados " + barObstacleCount + "."
            );
        }
    }

    private static void ValidateTables(
        Scene scene,
        BistroBuilderBarServiceValidationResult result
    )
    {
        RestaurantTable[] tables = BistroBuilderBarServiceInstaller
            .FindSceneObjects<RestaurantTable>(scene);
        HashSet<int> ids = new HashSet<int>();
        int fixedCount = 0;

        for (int index = 0; index < tables.Length; index++)
        {
            RestaurantTable table = tables[index];

            if (table == null || !ids.Add(table.TableId))
            {
                result.AddError(
                    table == null
                        ? "Existe una mesa nula."
                        : "TableId duplicado: " + table.TableId + "."
                );
                continue;
            }

            BistroBuilder367HInstalledFixture marker =
                table.GetComponent<BistroBuilder367HInstalledFixture>();

            if (marker != null &&
                (marker.FixtureId == "fixture_367h_table_03" ||
                 marker.FixtureId == "fixture_367h_table_04"))
            {
                fixedCount++;
            }
        }

        if (tables.Length >= 4)
        {
            result.AddCorrect(
                "La escena contiene al menos cuatro mesas operativas."
            );
        }
        else
        {
            result.AddError(
                "367H necesita cuatro mesas y solo hay " + tables.Length + "."
            );
        }

        if (fixedCount == 2)
        {
            result.AddCorrect(
                "Las dos mesas adicionales están marcadas como fixtures fijos."
            );
        }
        else
        {
            result.AddError(
                "Se esperaban dos mesas fijas 367H y se detectaron " +
                fixedCount + "."
            );
        }

        if (ids.Count == tables.Length)
        {
            result.AddCorrect("Todos los TableId son únicos.");
        }
    }

    private static void ValidateMenu(
        Scene scene,
        BistroBuilderBarServiceValidationResult result
    )
    {
        BistroBuilderDishCatalogService[] catalogs =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                BistroBuilderDishCatalogService
            >(scene);
        BistroBuilderRestaurantMenuService[] menus =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                BistroBuilderRestaurantMenuService
            >(scene);

        if (catalogs.Length != 1 || menus.Length != 1)
        {
            result.AddError(
                "Debe existir un catálogo y una carta runtime únicos."
            );
            return;
        }

        if (!catalogs[0].RebuildIndex(out string catalogError))
        {
            result.AddError(catalogError);
            return;
        }

        int availableDefinitions = 0;
        int activeMenuItems = 0;

        for (int index = 0; index < RequiredDishIds.Length; index++)
        {
            string dishId = RequiredDishIds[index];

            if (catalogs[0].TryGetDefinition(
                    dishId,
                    out BistroBuilderDishDefinition definition
                ) &&
                definition.IsAvailableForServiceMode(
                    BistroBuilderServiceMode.BarService
                ) &&
                definition.IsAvailableForServiceMode(
                    BistroBuilderServiceMode.WaitingAtBar
                ))
            {
                availableDefinitions++;
            }

            if (menus[0].TryGetItemSnapshot(
                    dishId,
                    out BistroBuilderMenuItemRuntimeState item
                ) &&
                item.Enabled && item.Unlocked && !item.ManuallySoldOut)
            {
                activeMenuItems++;
            }
        }

        if (availableDefinitions == RequiredDishIds.Length)
        {
            result.AddCorrect(
                "Los cinco artículos rápidos admiten BarService y " +
                "WaitingAtBar."
            );
        }
        else
        {
            result.AddError(
                "Solo " + availableDefinitions + "/" +
                RequiredDishIds.Length +
                " artículos están configurados para barra."
            );
        }

        if (activeMenuItems == RequiredDishIds.Length)
        {
            result.AddCorrect(
                "Los cinco artículos de barra están activos en la carta."
            );
        }
        else
        {
            result.AddError(
                "Solo " + activeMenuItems + "/" +
                RequiredDishIds.Length +
                " artículos están activos en la carta."
            );
        }
    }

    private static void ValidateSystems(
        Scene scene,
        BistroBuilderBarServiceValidationResult result
    )
    {
        BistroBuilderBarServiceRegistry[] registries =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                BistroBuilderBarServiceRegistry
            >(scene);
        BistroBuilderBarServiceSystem[] systems =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                BistroBuilderBarServiceSystem
            >(scene);

        if (registries.Length != 1 || systems.Length != 1)
        {
            result.AddError(
                "Debe existir un registro y una autoridad de barra únicos."
            );
            return;
        }

        registries[0].RebuildRegistryFromScene();

        if (registries[0].ValidateConfiguration(out string registryError))
        {
            result.AddCorrect("El registro de plazas de barra es válido.");
        }
        else
        {
            result.AddError(registryError);
        }

        if (systems[0].ValidateConfiguration(out string systemError))
        {
            result.AddCorrect("La autoridad de servicio de barra es válida.");
        }
        else
        {
            result.AddError(systemError);
        }

        CustomerGroupSpawner[] spawners =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                CustomerGroupSpawner
            >(scene);
        FoodDeliveryServiceFlow[] deliveryFlows =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                FoodDeliveryServiceFlow
            >(scene);
        BillServiceFlow[] billFlows =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                BillServiceFlow
            >(scene);

        int connectedSpawners = CountReference(
            spawners,
            "barServiceSystem",
            systems[0]
        );
        int connectedDelivery = CountReference(
            deliveryFlows,
            "barServiceSystem",
            systems[0]
        );
        int connectedBills = CountReference(
            billFlows,
            "barServiceSystem",
            systems[0]
        );
        TableAssignmentSystem[] tableAssignments =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                TableAssignmentSystem
            >(scene);
        int connectedTableAssignments = CountReference(
            tableAssignments,
            "barServiceSystem",
            systems[0]
        );

        if (connectedSpawners == spawners.Length && spawners.Length > 0)
        {
            result.AddCorrect("Todos los generadores registran clientes en barra.");
        }
        else
        {
            result.AddError("Hay generadores sin autoridad de barra conectada.");
        }

        if (connectedDelivery == deliveryFlows.Length &&
            deliveryFlows.Length > 0)
        {
            result.AddCorrect("Todos los flujos de reparto admiten barra.");
        }
        else
        {
            result.AddError("Hay flujos de reparto sin barra conectada.");
        }

        if (connectedBills == billFlows.Length && billFlows.Length > 0)
        {
            result.AddCorrect(
                "Todas las cuentas pueden liquidar cargos transferidos."
            );
        }
        else
        {
            result.AddError("Hay flujos de cuenta sin barra conectada.");
        }

        if (tableAssignments.Length == 1 &&
            connectedTableAssignments == 1)
        {
            result.AddCorrect(
                "La asignación de mesas puede cerrar WaitingAtBar de forma " +
                "transaccional."
            );
        }
        else
        {
            result.AddError(
                "TableAssignmentSystem no está conectado de forma única " +
                "con la autoridad de barra."
            );
        }

        WaiterTaskCoordinator[] coordinators =
            BistroBuilderBarServiceInstaller.FindSceneObjects<
                WaiterTaskCoordinator
            >(scene);

        if (coordinators.Length == 1 &&
            coordinators[0].MultiTableDeliveryRunsEnabled &&
            coordinators[0].MaximumDeliveryRunSize >= 3)
        {
            result.AddCorrect(
                "Las rondas inteligentes pueden combinar mesa y barra."
            );
        }
        else
        {
            result.AddError(
                "El coordinador de rondas 367G1/367H no es válido."
            );
        }
    }

    private static int CountReference<T>(
        T[] objects,
        string propertyName,
        UnityEngine.Object expected
    ) where T : UnityEngine.Object
    {
        int count = 0;

        for (int index = 0; index < objects.Length; index++)
        {
            SerializedObject serialized = new SerializedObject(objects[index]);
            SerializedProperty property =
                serialized.FindProperty(propertyName);

            if (property != null &&
                ReferenceEquals(property.objectReferenceValue, expected))
            {
                count++;
            }
        }

        return count;
    }
}
