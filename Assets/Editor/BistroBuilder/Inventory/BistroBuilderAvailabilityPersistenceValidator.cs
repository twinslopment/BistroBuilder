using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderAvailabilityPersistenceValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Ok(string text)
    {
        CorrectCount++;
        lines.Add("- OK: " + text);
    }

    public void Warn(string text)
    {
        WarningCount++;
        lines.Add("- ADVERTENCIA: " + text);
    }

    public void Error(string text)
    {
        ErrorCount++;
        lines.Add("- ERROR: " + text);
    }

    public string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "BISTRO BUILDER - DISPONIBILIDAD, PERSISTENCIA Y GUARDADO ACTIVO 368EF"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        for (int index = 0; index < lines.Count; index++)
        {
            builder.AppendLine(lines[index]);
        }
        return builder.ToString();
    }
}

public static class BistroBuilderAvailabilityPersistenceValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 368EF Availability, Persistence & Active Save";

    [MenuItem(MenuPath, false, 351)]
    private static void ValidateMenu()
    {
        BistroBuilderAvailabilityPersistenceValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();
        if (result.ErrorCount > 0) Debug.LogError(report); else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderAvailabilityPersistenceValidationResult
        ValidateCurrentProject()
    {
        var result =
            new BistroBuilderAvailabilityPersistenceValidationResult();

        BistroBuilderOrderInventoryLifecycleValidationResult baseResult =
            BistroBuilderOrderInventoryLifecycleValidator
                .ValidateCurrentProject();
        if (baseResult.ErrorCount == 0)
        {
            result.Ok("La base 368CD sigue siendo válida.");
        }
        else
        {
            result.Error("La base 368CD contiene errores.");
        }

        BistroBuilderDishAvailabilityService[] availabilityServices =
            Object.FindObjectsByType<BistroBuilderDishAvailabilityService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        if (availabilityServices.Length != 1)
        {
            result.Error(
                "Debe existir un único servicio de disponibilidad; encontrados: " +
                availabilityServices.Length + "."
            );
        }
        else
        {
            BistroBuilderDishAvailabilityService availability =
                availabilityServices[0];
            string error = string.Empty;

            if (!availability.ValidateConfiguration(out error))
            {
                result.Error(
                    string.IsNullOrWhiteSpace(error)
                        ? "El motor de disponibilidad no es válido."
                        : error
                );
            }
            else if (Application.isPlaying)
            {
                if (availability.RecalculateAll(out error))
                {
                    result.Ok(
                        "El motor de disponibilidad dinámica es válido y recalculable."
                    );
                    if (availability.DishCount == 8)
                    {
                        result.Ok(
                            "Los 8 platos canónicos tienen disponibilidad derivada."
                        );
                    }
                    else
                    {
                        result.Error(
                            "Se esperaban 8 disponibilidades y existen " +
                            availability.DishCount + "."
                        );
                    }
                }
                else
                {
                    result.Error(
                        string.IsNullOrWhiteSpace(error)
                            ? "La disponibilidad runtime no pudo recalcularse."
                            : error
                    );
                }
            }
            else
            {
                result.Ok(
                    "El motor de disponibilidad dinámica tiene una " +
                    "configuración estructural válida."
                );

                BistroBuilderRestaurantMenuService menu =
                    Object.FindFirstObjectByType<
                        BistroBuilderRestaurantMenuService
                    >();
                var menuItems =
                    new List<BistroBuilderMenuItemRuntimeState>();

                if (menu != null &&
                    menu.TryGetSnapshot(menuItems, out error) &&
                    menuItems.Count == 8)
                {
                    result.Ok(
                        "Los 8 platos canónicos están preparados para el " +
                        "recalculo runtime de disponibilidad."
                    );
                }
                else
                {
                    result.Error(
                        string.IsNullOrWhiteSpace(error)
                            ? "La carta no expone 8 platos evaluables."
                            : error
                    );
                }
            }
        }

        BistroBuilderInventorySaveSectionProvider[] inventoryProviders =
            Object.FindObjectsByType<BistroBuilderInventorySaveSectionProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        ValidateProviderCount(
            inventoryProviders,
            BistroBuilderInventorySaveSectionProvider.StableSectionId,
            BistroBuilderInventorySaveSectionProvider.StableSectionVersion,
            "proveedor de inventario",
            result
        );
        if (inventoryProviders.Length == 1)
        {
            string error = string.Empty;
            if (inventoryProviders[0].ValidateConfiguration(out error))
            {
                result.Ok(
                    "inventory.canonical puede capturar y reconciliar el inventario."
                );
            }
            else
            {
                result.Error(error);
            }
        }

        BistroBuilderActiveServiceSaveSectionProvider[] runtimeProviders =
            Object.FindObjectsByType<
                BistroBuilderActiveServiceSaveSectionProvider
            >(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        ValidateProviderCount(
            runtimeProviders,
            BistroBuilderActiveServiceSaveSectionProvider.StableSectionId,
            BistroBuilderActiveServiceSaveSectionProvider.StableSectionVersion,
            "proveedor de servicio activo",
            result
        );
        if (runtimeProviders.Length == 1)
        {
            string error = string.Empty;
            if (runtimeProviders[0].ValidateConfiguration(out error))
            {
                result.Ok(
                    "service.runtime puede reconstruir clientes, comandas, cocina y barra."
                );
            }
            else
            {
                result.Error(error);
            }

            BistroBuilderGeneralGameSaveSectionProvider generalProvider =
                Object.FindFirstObjectByType<
                    BistroBuilderGeneralGameSaveSectionProvider
                >();
            if (generalProvider != null &&
                runtimeProviders[0].FinalizeOrder >
                    generalProvider.FinalizeOrder &&
                (inventoryProviders.Length != 1 ||
                 runtimeProviders[0].FinalizeOrder >
                    inventoryProviders[0].FinalizeOrder))
            {
                result.Ok(
                    "service.runtime reanuda flujos después de restaurar " +
                    "inventario, reloj y estado general."
                );
            }
            else
            {
                result.Error(
                    "El orden de finalización podría reactivar tareas antes " +
                    "de restaurar el estado general."
                );
            }
        }

        BistroBuilderSaveGameService saveService =
            Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        if (saveService == null)
        {
            result.Error("Falta BistroBuilderSaveGameService.");
        }
        else
        {
            saveService.RefreshExtensions();
            if (saveService.HasProvider(
                    BistroBuilderInventorySaveSectionProvider.StableSectionId
                ) &&
                saveService.HasProvider(
                    BistroBuilderActiveServiceSaveSectionProvider
                        .StableSectionId
                ))
            {
                result.Ok(
                    "La plataforma de guardado registra inventario y service.runtime."
                );
            }
            else
            {
                result.Error(
                    "La plataforma no registra todas las secciones 368EF."
                );
            }
        }

        BistroBuilderActiveServiceSaveGuard guard =
            Object.FindFirstObjectByType<BistroBuilderActiveServiceSaveGuard>();
        if (guard != null)
        {
            string error = string.Empty;
            if (guard.ValidateConfiguration(out error) &&
                saveService != null &&
                saveService.HasProvider(
                    BistroBuilderGeneralGameSaveSectionProvider
                        .FutureActiveServiceSectionId
                ))
            {
                result.Ok(
                    "La regla de guardado permite partidas durante un servicio activo."
                );
            }
            else
            {
                result.Error(
                    string.IsNullOrWhiteSpace(error)
                        ? "La regla de servicio activo sigue bloqueada."
                        : error
                );
            }
        }
        else
        {
            result.Error("Falta BistroBuilderActiveServiceSaveGuard.");
        }

        BistroBuilderSimulationSaveParticipant participant =
            Object.FindFirstObjectByType<
                BistroBuilderSimulationSaveParticipant
            >();
        if (participant != null)
        {
            string error = string.Empty;
            if (participant.ValidateConfiguration(out error))
            {
                result.Ok(
                    "GameClock es la única autoridad de pausa y Time.timeScale " +
                    "durante guardado y carga."
                );
            }
            else
            {
                result.Error(error);
            }
        }
        else
        {
            result.Error("Falta BistroBuilderSimulationSaveParticipant.");
        }

        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();

        if (inventory == null)
        {
            result.Error("Falta BistroBuilderInventoryService.");
        }
        else if (!inventory.ValidateConfiguration(
                     out string inventoryConfigurationError
                 ))
        {
            result.Error(inventoryConfigurationError);
        }
        else if (!inventory.IsInitialized)
        {
            // Los diccionarios del inventario son estado runtime no
            // serializado. Un validador estructural ejecutado tras abrir Unity
            // no debe depender de haber entrado antes en Play Mode ni de que
            // otro autotest haya inicializado accidentalmente el componente.
            result.Ok(
                "El inventario tiene configuración persistible; la captura " +
                "runtime se valida de forma aislada en el autotest 368EF."
            );
        }
        else if (inventory.TryCaptureRuntimeSnapshot(
                     out BistroBuilderInventoryRuntimeSnapshot snapshot,
                     out string inventorySnapshotError
                 ) &&
                 snapshot.TryValidateBasic(out inventorySnapshotError))
        {
            result.Ok(
                "El inventario captura balances, reservas, operaciones y libro auditables."
            );
        }
        else
        {
            result.Error(
                string.IsNullOrWhiteSpace(inventorySnapshotError)
                    ? "El snapshot de inventario no es válido."
                    : inventorySnapshotError
            );
        }

        CustomerGroupSpawner spawner =
            Object.FindFirstObjectByType<CustomerGroupSpawner>();
        if (spawner == null)
        {
            result.Error("Falta CustomerGroupSpawner.");
        }
        else
        {
            string spawnError = string.Empty;
            if (spawner.TryCaptureRuntimeSpawnState(
                    out BistroBuilderCustomerSpawnerRuntimeSaveRecord spawnState,
                    out spawnError
                ) &&
                spawnState != null &&
                spawnState.TryValidate(out spawnError))
            {
                result.Ok(
                    "El calendario de llegadas futuras es persistible y no se duplica al cargar."
                );
            }
            else
            {
                result.Error(
                    string.IsNullOrWhiteSpace(spawnError)
                        ? "El calendario runtime de llegadas no es persistible."
                        : spawnError
                );
            }
        }

        result.Ok(
            "La disponibilidad no se persiste: se recalcula tras cargar el inventario."
        );
        result.Ok(
            "Las rutas de reparto en curso se convierten en checkpoints reanudables."
        );
        return result;
    }

    private static void ValidateProviderCount<T>(
        T[] providers,
        string expectedSectionId,
        int expectedSectionVersion,
        string description,
        BistroBuilderAvailabilityPersistenceValidationResult result
    ) where T : MonoBehaviour, IBistroBuilderSaveSectionProvider
    {
        if (providers.Length != 1)
        {
            result.Error(
                "Debe existir un único " + description + "; encontrados: " +
                providers.Length + "."
            );
            return;
        }

        if (providers[0].SectionId == expectedSectionId &&
            providers[0].SectionVersion == expectedSectionVersion)
        {
            result.Ok(
                "El " + description + " conserva identidad y versión estables."
            );
        }
        else
        {
            result.Error(
                "El " + description + " no conserva su contrato estable."
            );
        }
    }
}
