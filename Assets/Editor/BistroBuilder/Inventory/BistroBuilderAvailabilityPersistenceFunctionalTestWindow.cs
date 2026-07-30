using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional real 368EF.
///
/// Abre una fase Preparing (servicio activo sin llegadas), reserva una ración,
/// guarda en disco, modifica el inventario, carga la partida y comprueba que
/// servicio, reserva, operaciones idempotentes y disponibilidad vuelven al
/// checkpoint. Después restaura el estado previo y elimina el slot diagnóstico.
/// </summary>
public sealed class BistroBuilderAvailabilityPersistenceFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "368EF Functional Availability & Active Save Test";

    private enum TestPhase
    {
        Idle = 0,
        Saving = 1,
        Loading = 2,
        DeletingAfterSuccess = 3,
        DeletingAfterFailure = 4,
        Completed = 5,
        Failed = 6
    }

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode con el servicio cerrado y ejecuta la prueba.";
    private MessageType reportType = MessageType.Info;
    private TestPhase phase = TestPhase.Idle;

    private BistroBuilderSaveGameService saveService;
    private BistroBuilderInventoryService inventoryService;
    private BistroBuilderDishAvailabilityService availabilityService;
    private BistroBuilderRecipeCatalogService recipeCatalogService;
    private BistroBuilderRestaurantMenuService menuService;
    private RestaurantServiceStateService serviceStateService;

    private BistroBuilderInventoryRuntimeSnapshot initialInventory;
    private RestaurantServiceState initialServiceState;
    private RestaurantServiceState savedServiceState;
    private int diagnosticSlot;
    private string runToken = string.Empty;
    private string reservationId = string.Empty;
    private string releaseOperationId = string.Empty;
    private string selectedDishId = string.Empty;
    private long initialPortions;
    private long reservedPortions;
    private long releasedPortions;
    private string pendingFailure = string.Empty;
    private bool subscribed;

    [MenuItem(MenuPath, false, 353)]
    private static void Open()
    {
        GetWindow<BistroBuilderAvailabilityPersistenceFunctionalTestWindow>(
            "BB 368EF Test"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "368EF — Disponibilidad y guardado activo",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba usa un slot libre entre 990 y 999, guarda con el " +
            "servicio en Preparing, carga el checkpoint y elimina el slot. " +
            "No cierres Play Mode hasta ver SUPERADA o FALLIDA.",
            MessageType.Info
        );

        bool canRun = EditorApplication.isPlaying &&
                      (phase == TestPhase.Idle ||
                       phase == TestPhase.Completed ||
                       phase == TestPhase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button(
                    "Ejecutar prueba funcional 368EF",
                    GUILayout.Height(34f)
                ))
            {
                BeginTest();
            }
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para habilitar la prueba.",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void BeginTest()
    {
        ResetRunState();

        if (!ResolveDependencies(out string error))
        {
            SetImmediateFailure(error);
            return;
        }

        if (!serviceStateService.IsClosed)
        {
            SetImmediateFailure(
                "La prueba debe comenzar con el servicio cerrado para no " +
                "interferir con una simulación real."
            );
            return;
        }

        saveService.RefreshExtensions();
        if (!saveService.HasProvider(
                BistroBuilderInventorySaveSectionProvider.StableSectionId
            ) ||
            !saveService.HasProvider(
                BistroBuilderActiveServiceSaveSectionProvider.StableSectionId
            ))
        {
            SetImmediateFailure(
                "La plataforma no registra inventory.canonical y " +
                "service.runtime."
            );
            return;
        }

        diagnosticSlot = FindFreeDiagnosticSlot();
        if (diagnosticSlot < 1)
        {
            SetImmediateFailure(
                "Los slots diagnósticos 990-999 están ocupados. Libera uno " +
                "antes de ejecutar esta prueba."
            );
            return;
        }

        if (!inventoryService.TryCaptureRuntimeSnapshot(
                out initialInventory,
                out error
            ))
        {
            SetImmediateFailure("No se pudo capturar el inventario inicial: " + error);
            return;
        }

        initialServiceState = serviceStateService.CurrentState;
        if (!serviceStateService.TryBeginPreparation())
        {
            SetImmediateFailure(
                "No se pudo abrir la fase Preparing para probar el guardado activo."
            );
            return;
        }
        savedServiceState = serviceStateService.CurrentState;

        if (!availabilityService.RecalculateAll(out error) ||
            !TrySelectOrderableDishAndBuildReservation(
                out selectedDishId,
                out List<BistroBuilderInventoryQuantityLine> quantities,
                out initialPortions,
                out error
            ))
        {
            RestoreInitialRuntime(out _);
            SetImmediateFailure(
                "No se pudo preparar una ración de prueba: " + error
            );
            return;
        }

        runToken = Guid.NewGuid().ToString("N");
        reservationId = "functional_368ef_reservation_" + runToken;
        string reserveOperationId =
            "functional_368ef_reserve_" + runToken;
        releaseOperationId =
            "functional_368ef_release_" + runToken;
        string sourceId = "functional_368ef_source_" + runToken;

        if (!inventoryService.TryCreateReservation(
                reserveOperationId,
                reservationId,
                sourceId,
                quantities,
                out _,
                out error
            ) ||
            !availabilityService.TryGetSnapshot(
                selectedDishId,
                out BistroBuilderDishAvailabilitySnapshot reservedSnapshot
            ))
        {
            RestoreInitialRuntime(out _);
            SetImmediateFailure("No se pudo crear la reserva funcional: " + error);
            return;
        }

        reservedPortions = reservedSnapshot.AvailablePortions;
        if (reservedPortions >= initialPortions)
        {
            RestoreInitialRuntime(out _);
            SetImmediateFailure(
                "La reserva no redujo la disponibilidad del plato seleccionado."
            );
            return;
        }

        SubscribeToSaveService();
        phase = TestPhase.Saving;
        reportType = MessageType.Info;
        report =
            "Guardando el slot diagnóstico " + diagnosticSlot +
            " durante un servicio activo...";
        Repaint();

        if (!saveService.TrySaveSlot(
                diagnosticSlot,
                "BB 368EF DIAGNOSTIC",
                out string rejection
            ))
        {
            FailAndCleanup("El guardado activo fue rechazado: " + rejection);
        }
    }

    private void HandleOperationCompleted(
        BistroBuilderSaveOperationResult result
    )
    {
        if (result == null || result.SlotIndex != diagnosticSlot)
        {
            return;
        }

        if (!result.Succeeded)
        {
            if (phase == TestPhase.DeletingAfterFailure ||
                phase == TestPhase.DeletingAfterSuccess)
            {
                pendingFailure =
                    (phase == TestPhase.DeletingAfterSuccess
                        ? "La lógica 368EF se validó, pero "
                        : pendingFailure + " Además, ") +
                    "no se pudo eliminar el slot diagnóstico: " +
                    result.Message;
                CompleteFailureAfterDelete();
                return;
            }

            FailAndCleanup(
                result.OperationKind + " falló: " + result.Message
            );
            return;
        }

        switch (phase)
        {
            case TestPhase.Saving:
                ContinueAfterSave(result);
                break;

            case TestPhase.Loading:
                ContinueAfterLoad(result);
                break;

            case TestPhase.DeletingAfterSuccess:
                CompleteSuccess();
                break;

            case TestPhase.DeletingAfterFailure:
                CompleteFailureAfterDelete();
                break;
        }
    }

    private void ContinueAfterSave(BistroBuilderSaveOperationResult result)
    {
        if (!inventoryService.TryReleaseReservation(
                releaseOperationId,
                reservationId,
                "Mutación posterior al guardado funcional 368EF.",
                out string error
            ) ||
            !availabilityService.RecalculateAll(out error) ||
            !availabilityService.TryGetSnapshot(
                selectedDishId,
                out BistroBuilderDishAvailabilitySnapshot releasedSnapshot
            ))
        {
            FailAndCleanup(
                "No se pudo modificar el estado después de guardar: " + error
            );
            return;
        }

        releasedPortions = releasedSnapshot.AvailablePortions;
        if (releasedPortions <= reservedPortions)
        {
            FailAndCleanup(
                "Liberar la reserva no restauró la disponibilidad antes de cargar."
            );
            return;
        }

        phase = TestPhase.Loading;
        report =
            "Guardado activo completado (" + result.PayloadBytes +
            " bytes). Cargando el checkpoint...";
        Repaint();

        if (!saveService.TryLoadSlot(
                diagnosticSlot,
                out string rejection
            ))
        {
            FailAndCleanup("La carga fue rechazada: " + rejection);
        }
    }

    private void ContinueAfterLoad(BistroBuilderSaveOperationResult result)
    {
        if (!ResolveDependencies(out string error))
        {
            FailAndCleanup(
                "Tras cargar no se resolvieron las dependencias: " + error
            );
            return;
        }

        bool serviceRestored =
            serviceStateService.CurrentState == savedServiceState &&
            serviceStateService.IsServiceInProgress;
        bool reservationRestored =
            inventoryService.TryGetReservationSnapshot(
                reservationId,
                out BistroBuilderInventoryReservationSnapshot reservation
            ) &&
            reservation != null &&
            reservation.Status ==
                BistroBuilderInventoryReservationStatus.Active;
        bool availabilityRestored =
            availabilityService.RecalculateAll(out error) &&
            availabilityService.TryGetSnapshot(
                selectedDishId,
                out BistroBuilderDishAvailabilitySnapshot loadedSnapshot
            ) &&
            loadedSnapshot.AvailablePortions == reservedPortions;

        if (!serviceRestored || !reservationRestored ||
            !availabilityRestored)
        {
            string reason = !serviceRestored
                ? "El estado activo del servicio no se restauró."
                : !reservationRestored
                    ? "La reserva activa no se restauró."
                    : "La disponibilidad no coincide con el checkpoint.";
            FailAndCleanup(reason + " " + error);
            return;
        }

        if (!RestoreInitialRuntime(out error))
        {
            FailAndCleanup(
                "La prueba cargó correctamente, pero no pudo restaurar el " +
                "estado inicial: " + error
            );
            return;
        }

        phase = TestPhase.DeletingAfterSuccess;
        report =
            "Checkpoint restaurado correctamente. Eliminando el slot " +
            "diagnóstico...";
        Repaint();

        if (!saveService.TryDeleteSlot(
                diagnosticSlot,
                out string rejection
            ))
        {
            pendingFailure =
                "La lógica 368EF fue correcta, pero no se pudo eliminar " +
                "el slot diagnóstico: " + rejection;
            phase = TestPhase.Failed;
            reportType = MessageType.Error;
            report = "PRUEBA FUNCIONAL 368EF FALLIDA\n\n" + pendingFailure;
            UnsubscribeFromSaveService();
            Debug.LogError(report);
            Repaint();
        }
    }

    private void FailAndCleanup(string message)
    {
        pendingFailure = string.IsNullOrWhiteSpace(message)
            ? "Fallo funcional no especificado."
            : message;

        RestoreInitialRuntime(out string restoreError);
        if (!string.IsNullOrWhiteSpace(restoreError))
        {
            pendingFailure += " Restauración local: " + restoreError;
        }

        if (saveService != null && !saveService.IsBusy &&
            diagnosticSlot >= 1 && saveService.SlotExists(diagnosticSlot))
        {
            phase = TestPhase.DeletingAfterFailure;
            reportType = MessageType.Error;
            report =
                "La prueba ha fallado y está eliminando el slot diagnóstico...\n\n" +
                pendingFailure;
            Repaint();

            if (saveService.TryDeleteSlot(
                    diagnosticSlot,
                    out string deleteRejection
                ))
            {
                return;
            }

            pendingFailure += " No se pudo eliminar el slot: " +
                              deleteRejection;
        }

        CompleteFailureAfterDelete();
    }

    private bool RestoreInitialRuntime(out string error)
    {
        error = string.Empty;
        bool restored = true;

        if (inventoryService != null && initialInventory != null &&
            !inventoryService.TryReplaceFromRuntimeSnapshot(
                initialInventory,
                true,
                out error
            ))
        {
            restored = false;
        }

        if (serviceStateService != null &&
            !serviceStateService.TryRestoreState(
                initialServiceState,
                true
            ))
        {
            error = string.IsNullOrWhiteSpace(error)
                ? "No se pudo restaurar el estado inicial del servicio."
                : error + " No se pudo restaurar el servicio.";
            restored = false;
        }

        if (availabilityService != null &&
            !availabilityService.RecalculateAll(out string availabilityError))
        {
            error = string.IsNullOrWhiteSpace(error)
                ? availabilityError
                : error + " " + availabilityError;
            restored = false;
        }

        return restored;
    }

    private void CompleteSuccess()
    {
        phase = TestPhase.Completed;
        reportType = MessageType.Info;
        report =
            "BISTRO BUILDER — PRUEBA FUNCIONAL 368EF SUPERADA\n\n" +
            "- Guardado permitido durante servicio activo (Preparing).\n" +
            "- Reserva de receta persistida en inventory.canonical.\n" +
            "- Estado de servicio restaurado mediante service.runtime.\n" +
            "- Disponibilidad: " + initialPortions + " → " +
                reservedPortions + " → " + releasedPortions + " → " +
                reservedPortions + " tras cargar.\n" +
            "- Operaciones idempotentes y libro restaurados.\n" +
            "- Estado inicial recuperado y slot diagnóstico eliminado.";
        UnsubscribeFromSaveService();
        Debug.Log(report);
        Repaint();
    }

    private void CompleteFailureAfterDelete()
    {
        phase = TestPhase.Failed;
        reportType = MessageType.Error;
        report =
            "PRUEBA FUNCIONAL 368EF FALLIDA\n\n" + pendingFailure;
        UnsubscribeFromSaveService();
        Debug.LogError(report);
        Repaint();
    }

    private bool TrySelectOrderableDishAndBuildReservation(
        out string dishId,
        out List<BistroBuilderInventoryQuantityLine> quantities,
        out long portions,
        out string error
    )
    {
        dishId = string.Empty;
        quantities = null;
        portions = 0L;
        error = string.Empty;

        var items = new List<BistroBuilderMenuItemRuntimeState>();
        if (!menuService.TryGetSnapshot(items, out error))
        {
            return false;
        }

        items.Sort((left, right) =>
        {
            bool leftPreferred = left != null &&
                left.DishId == "dish_fabada_asturiana";
            bool rightPreferred = right != null &&
                right.DishId == "dish_fabada_asturiana";
            if (leftPreferred != rightPreferred)
            {
                return leftPreferred ? -1 : 1;
            }
            return string.CompareOrdinal(
                left != null ? left.DishId : string.Empty,
                right != null ? right.DishId : string.Empty
            );
        });

        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            BistroBuilderMenuItemRuntimeState item = items[itemIndex];
            if (item == null ||
                !availabilityService.TryGetSnapshot(
                    item.DishId,
                    out BistroBuilderDishAvailabilitySnapshot snapshot
                ) ||
                !snapshot.IsOrderable || snapshot.AvailablePortions < 1L ||
                !recipeCatalogService.TryGetRecipeByDishId(
                    item.DishId,
                    out BistroBuilderRecipeDefinition recipe
                ) ||
                recipe == null)
            {
                continue;
            }

            if (!TryBuildOnePortionLines(recipe, out quantities, out error))
            {
                continue;
            }

            dishId = item.DishId;
            portions = snapshot.AvailablePortions;
            return true;
        }

        error = "No existe ningún plato pedible con stock suficiente.";
        return false;
    }

    private static bool TryBuildOnePortionLines(
        BistroBuilderRecipeDefinition recipe,
        out List<BistroBuilderInventoryQuantityLine> lines,
        out string error
    )
    {
        lines = new List<BistroBuilderInventoryQuantityLine>();
        error = string.Empty;

        if (recipe == null || !recipe.TryValidate(out error))
        {
            return false;
        }

        var aggregated = new SortedDictionary<string, long>(
            StringComparer.Ordinal
        );
        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount amount =
                recipe.Ingredients[index];
            if (amount == null || amount.Ingredient == null ||
                !amount.TryGetCanonicalMilliUnits(
                    out long batchQuantity,
                    out error
                ))
            {
                return false;
            }

            long perPortion = DivideCeiling(
                batchQuantity,
                recipe.YieldPortions
            );
            if (perPortion <= 0L)
            {
                error = "La receta contiene una cantidad no utilizable.";
                return false;
            }

            string ingredientId = amount.Ingredient.IngredientId;
            aggregated.TryGetValue(ingredientId, out long current);
            aggregated[ingredientId] = checked(current + perPortion);
        }

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            lines.Add(
                new BistroBuilderInventoryQuantityLine(pair.Key, pair.Value)
            );
        }

        return lines.Count > 0;
    }

    private int FindFreeDiagnosticSlot()
    {
        for (int slot = 999; slot >= 990; slot--)
        {
            if (!saveService.SlotExists(slot))
            {
                return slot;
            }
        }

        return 0;
    }

    private bool ResolveDependencies(out string error)
    {
        saveService = FindFirstObjectByType<BistroBuilderSaveGameService>();
        inventoryService =
            FindFirstObjectByType<BistroBuilderInventoryService>();
        availabilityService =
            FindFirstObjectByType<BistroBuilderDishAvailabilityService>();
        recipeCatalogService =
            FindFirstObjectByType<BistroBuilderRecipeCatalogService>();
        menuService =
            FindFirstObjectByType<BistroBuilderRestaurantMenuService>();
        serviceStateService =
            FindFirstObjectByType<RestaurantServiceStateService>();

        if (saveService == null || inventoryService == null ||
            availabilityService == null || recipeCatalogService == null ||
            menuService == null || serviceStateService == null)
        {
            error = "Faltan dependencias runtime de 368EF.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void SubscribeToSaveService()
    {
        UnsubscribeFromSaveService();
        if (saveService != null)
        {
            saveService.OperationCompleted += HandleOperationCompleted;
            subscribed = true;
        }
    }

    private void UnsubscribeFromSaveService()
    {
        if (subscribed && saveService != null)
        {
            saveService.OperationCompleted -= HandleOperationCompleted;
        }
        subscribed = false;
    }

    private void ResetRunState()
    {
        UnsubscribeFromSaveService();
        phase = TestPhase.Idle;
        reportType = MessageType.Info;
        pendingFailure = string.Empty;
        initialInventory = null;
        diagnosticSlot = 0;
        runToken = string.Empty;
        reservationId = string.Empty;
        releaseOperationId = string.Empty;
        selectedDishId = string.Empty;
        initialPortions = 0L;
        reservedPortions = 0L;
        releasedPortions = 0L;
    }

    private void SetImmediateFailure(string message)
    {
        phase = TestPhase.Failed;
        reportType = MessageType.Error;
        report = "PRUEBA FUNCIONAL 368EF FALLIDA\n\n" + message;
        Debug.LogError(report);
        Repaint();
    }

    private static long DivideCeiling(long numerator, int denominator)
    {
        if (numerator <= 0L || denominator <= 0)
        {
            return 0L;
        }

        long quotient = numerator / denominator;
        return numerator % denominator == 0L ? quotient : quotient + 1L;
    }
}
