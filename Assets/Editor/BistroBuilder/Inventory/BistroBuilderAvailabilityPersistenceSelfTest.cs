using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest estructural y de dominio de 368EF.
///
/// No altera stock, carta, servicio ni archivos de guardado. Comprueba el
/// cálculo derivado, la serialización de inventario y los contratos necesarios
/// para guardar durante un servicio activo.
/// </summary>
public static class BistroBuilderAvailabilityPersistenceSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Run 368EF Availability & Persistence Self-Test";

    [MenuItem(MenuPath, false, 352)]
    private static void Run()
    {
        int passed = 0;
        int failed = 0;
        var lines = new List<string>();

        Action<bool, string> check = (condition, description) =>
        {
            if (condition)
            {
                passed++;
                lines.Add("- OK: " + description);
            }
            else
            {
                failed++;
                lines.Add("- ERROR: " + description);
            }
        };

        BistroBuilderAvailabilityPersistenceValidationResult validation =
            BistroBuilderAvailabilityPersistenceValidator
                .ValidateCurrentProject();
        check(
            validation.ErrorCount == 0,
            "La instalación 368EF supera el validador estructural."
        );

        BistroBuilderDishAvailabilityService availability =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderDishAvailabilityService
            >();
        BistroBuilderRestaurantMenuService menu =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRestaurantMenuService
            >();
        BistroBuilderRecipeCatalogService recipes =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRecipeCatalogService
            >();
        BistroBuilderInventoryService inventory =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderInventoryService
            >();
        BistroBuilderCanonicalOrderIntegrationService integration =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderIntegrationService
            >();

        check(availability != null, "Existe el motor de disponibilidad.");
        check(menu != null, "Existe la carta runtime.");
        check(recipes != null, "Existe el catálogo runtime de recetas.");
        check(inventory != null, "Existe el inventario canónico.");
        check(
            integration != null &&
            IsConcreteMealService(integration.CurrentMealService),
            "La integración conserva desayuno, comida o cena como " +
            "servicio actual concreto."
        );

        string error = string.Empty;
        GameObject runtimePreviewRoot = null;
        BistroBuilderInventoryService runtimeInventory = null;
        BistroBuilderDishAvailabilityService runtimeAvailability = null;

        bool previewCreated = TryCreateRuntimePreview(
            inventory,
            availability,
            recipes,
            menu,
            integration,
            out runtimePreviewRoot,
            out runtimeInventory,
            out runtimeAvailability,
            out error
        );

        try
        {
            bool availabilityValid = previewCreated &&
                runtimeAvailability.ValidateConfiguration(out error) &&
                runtimeAvailability.RecalculateAll(out error);
            check(
                availabilityValid,
                string.IsNullOrWhiteSpace(error)
                    ? "La disponibilidad se recalcula sin errores."
                    : "La disponibilidad se recalcula: " + error
            );

            if (availabilityValid)
            {
                check(
                    runtimeAvailability.DishCount == 8,
                    "Los 8 platos canónicos tienen disponibilidad derivada."
                );

                var menuItems = new List<BistroBuilderMenuItemRuntimeState>();
                bool menuSnapshotValid = menu != null &&
                    menu.TryGetSnapshot(menuItems, out error);
                check(
                    menuSnapshotValid && menuItems.Count == 8,
                    "La carta expone 8 entradas evaluables."
                );

                if (menuSnapshotValid && recipes != null &&
                    runtimeInventory != null)
                {
                    BistroBuilderMealServiceAvailability mealService =
                        integration != null
                            ? integration.CurrentMealService
                            : BistroBuilderMealServiceAvailability.Lunch;

                    for (int index = 0; index < menuItems.Count; index++)
                    {
                        BistroBuilderMenuItemRuntimeState item =
                            menuItems[index];
                        bool found = runtimeAvailability.TryGetSnapshot(
                            item.DishId,
                            out BistroBuilderDishAvailabilitySnapshot snapshot
                        );
                        check(
                            found,
                            "Existe disponibilidad para " + item.DishId + "."
                        );

                        if (!found)
                        {
                            continue;
                        }

                        check(
                            snapshot.AvailablePortions >= 0L &&
                            BistroBuilderMenuIdUtility.IsValidStableId(
                                snapshot.DishId
                            ),
                            "La fotografía de " + item.DishId + " es válida."
                        );

                        if (TryGetTerminalState(
                                item,
                                mealService,
                                out BistroBuilderDishAvailabilityState
                                    expectedTerminal
                            ))
                        {
                            check(
                                snapshot.State == expectedTerminal,
                                "La prioridad comercial de " + item.DishId +
                                " prevalece sobre el stock."
                            );
                            continue;
                        }

                        if (!recipes.TryGetRecipeByDishId(
                                item.DishId,
                                out BistroBuilderRecipeDefinition recipe
                            ) ||
                            recipe == null ||
                            !TryCalculateExpectedAvailability(
                                recipe,
                                runtimeInventory,
                                out long expectedPortions,
                                out string expectedLimitingIngredient,
                                out error
                            ))
                        {
                            check(
                                false,
                                "No se pudo auditar " + item.DishId + "."
                            );
                            continue;
                        }

                        check(
                            snapshot.AvailablePortions == expectedPortions,
                            "Las raciones de " + item.DishId +
                            " coinciden con el ingrediente limitante."
                        );
                        check(
                            string.Equals(
                                snapshot.LimitingIngredientId,
                                expectedLimitingIngredient,
                                StringComparison.Ordinal
                            ),
                            "El ingrediente limitante de " + item.DishId +
                            " es determinista."
                        );

                        BistroBuilderDishAvailabilityState expectedState =
                            expectedPortions <= 0L
                                ? BistroBuilderDishAvailabilityState.OutOfStock
                                : expectedPortions <=
                                    runtimeAvailability
                                        .LowStockPortionThreshold
                                    ? BistroBuilderDishAvailabilityState
                                        .LowStock
                                    : BistroBuilderDishAvailabilityState
                                        .Available;
                        check(
                            snapshot.State == expectedState,
                            "El estado de stock de " + item.DishId +
                            " coincide con sus raciones."
                        );
                    }
                }
            }

            BistroBuilderInventoryRuntimeSnapshot capturedInventory = null;
            bool inventoryCaptured = previewCreated &&
                runtimeInventory.TryCaptureRuntimeSnapshot(
                    out capturedInventory,
                    out error
                );
            check(
                inventoryCaptured,
                "El inventario captura un snapshot completo."
            );

            if (inventoryCaptured)
            {
                string json = JsonUtility.ToJson(capturedInventory, false);
                BistroBuilderInventoryRuntimeSnapshot roundTrip =
                    JsonUtility.FromJson<
                        BistroBuilderInventoryRuntimeSnapshot
                    >(json);
                bool roundTripValid = roundTrip != null &&
                    roundTrip.TryValidateBasic(out error);
                check(
                    roundTripValid,
                    "El snapshot de inventario supera un ciclo JSON completo."
                );
                check(
                    roundTripValid &&
                    roundTrip.stock.Count == capturedInventory.stock.Count &&
                    roundTrip.lots.Count == capturedInventory.lots.Count &&
                    roundTrip.reservations.Count ==
                        capturedInventory.reservations.Count &&
                    roundTrip.operations.Count ==
                        capturedInventory.operations.Count &&
                    roundTrip.ledger.Count == capturedInventory.ledger.Count,
                    "El ciclo JSON conserva balances, lotes, reservas, operaciones y libro."
                );

                BistroBuilderInventorySaveSectionProvider inventoryProvider =
                    UnityEngine.Object.FindFirstObjectByType<
                        BistroBuilderInventorySaveSectionProvider
                    >();
                check(
                    inventoryProvider != null &&
                    inventoryProvider.ValidateState(roundTrip, out error),
                    "inventory.canonical acepta el snapshot reconstruido."
                );
            }
        }
        finally
        {
            if (runtimePreviewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimePreviewRoot);
            }
        }

        string runtimeOrderId =
            "order_" + Guid.NewGuid().ToString("N");
        string runtimeLineId =
            "order_line_" + Guid.NewGuid().ToString("N");
        string longReservationId =
            "inventory_reservation_" + runtimeOrderId + "_" +
            runtimeLineId;
        string longSourceId =
            "order_line_" + runtimeOrderId + "_" + runtimeLineId;
        string longOperationId =
            "inventory_reserve_" + runtimeOrderId + "_" + runtimeLineId;

        var longReservationRecord =
            new BistroBuilderInventoryReservationSaveRecord
            {
                reservationId = longReservationId,
                sourceId = longSourceId,
                status = (int)
                    BistroBuilderInventoryReservationStatus.Active,
                revision = 1L
            };
        longReservationRecord.lines.Add(
            new BistroBuilderInventoryReservationLineSaveRecord
            {
                ingredientId = "ingredient_runtime_id_test",
                canonicalMilliUnits = 1000L
            }
        );
        check(
            longReservationId.Length > 96 &&
            longReservationRecord.TryValidate(out error),
            "La persistencia admite ReservationId runtime superiores a " +
            "96 caracteres y dentro del contrato de 160."
        );

        var longOperationRecord =
            new BistroBuilderInventoryOperationSaveRecord
            {
                operationId = longOperationId,
                fingerprint = "reserve|ingredient_runtime_id_test|1000"
            };
        check(
            longOperationId.Length > 96 &&
            longOperationRecord.TryValidate(out error),
            "La persistencia admite OperationId idempotentes compuestos por " +
            "OrderId y OrderLineId."
        );

        var longTransactionRecord =
            new BistroBuilderInventoryTransactionSaveRecord
            {
                sequence = 1L,
                transactionId = "inventory_tx_00000001",
                operationId = longOperationId,
                ingredientId = "ingredient_runtime_id_test",
                transactionType = (int)
                    BistroBuilderInventoryTransactionType.Reservation,
                quantityCanonicalMilliUnits = 1000L,
                onHandDeltaCanonicalMilliUnits = 0L,
                reservedDeltaCanonicalMilliUnits = 1000L,
                previousOnHandCanonicalMilliUnits = 5000L,
                newOnHandCanonicalMilliUnits = 5000L,
                previousReservedCanonicalMilliUnits = 0L,
                newReservedCanonicalMilliUnits = 1000L,
                sourceId = longSourceId,
                reason = "Prueba del contrato runtime persistido.",
                timestampUtcTicks = DateTime.UtcNow.Ticks
            };
        check(
            longTransactionRecord.TryValidate(out error),
            "El libro persistido acepta OperationId y SourceId runtime largos."
        );

        var excessiveRuntimeOperation =
            new BistroBuilderInventoryOperationSaveRecord
            {
                operationId = new string('a', 161),
                fingerprint = "contract_limit"
            };
        check(
            !excessiveRuntimeOperation.TryValidate(out error),
            "La persistencia sigue rechazando identidades runtime mayores " +
            "de 160 caracteres."
        );

        string maximumOrderId =
            "order_" + new string('a', 90);
        string maximumLineId =
            "line_" + new string('b', 91);
        string boundedReservationId =
            BistroBuilderOrderInventoryLifecycleService.BuildReservationId(
                maximumOrderId,
                maximumLineId
            );
        string boundedOperationId =
            BistroBuilderOrderInventoryLifecycleService.BuildOperationId(
                "waste",
                maximumOrderId,
                maximumLineId,
                new string('c', 96)
            );
        check(
            boundedReservationId.Length <= 160 &&
            boundedOperationId.Length <= 160,
            "Los generadores 368CD limitan cualquier identidad compuesta a " +
            "160 caracteres."
        );
        check(
            boundedOperationId ==
                BistroBuilderOrderInventoryLifecycleService.BuildOperationId(
                    "waste",
                    maximumOrderId,
                    maximumLineId,
                    new string('c', 96)
                ),
            "La compactación de IDs largos es determinista."
        );
        check(
            boundedOperationId !=
                BistroBuilderOrderInventoryLifecycleService.BuildOperationId(
                    "waste",
                    maximumOrderId,
                    maximumLineId,
                    new string('d', 96)
                ),
            "La huella compactada distingue operaciones largas diferentes."
        );

        BistroBuilderCanonicalOrderService canonicalOrders =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderService
            >();
        BistroBuilderCanonicalOrderRuntimeSnapshot canonical = null;
        bool canonicalCaptured = canonicalOrders != null &&
            canonicalOrders.TryCaptureRuntimeSnapshot(
                out canonical,
                out error
            );
        check(
            canonicalCaptured,
            "Las comandas canónicas pueden crear un checkpoint."
        );

        if (canonicalCaptured)
        {
            BistroBuilderCanonicalOrderRuntimeSnapshot checkpoint =
                canonical.CreateActiveServiceCheckpointSnapshot();
            check(
                checkpoint != null && checkpoint.TryValidate(out error),
                "El checkpoint activo de comandas es válido."
            );
            check(
                !ContainsInFlightDeliveryState(checkpoint),
                "Las entregas en tránsito se normalizan a un punto reanudable."
            );
        }

        var generalMealServiceData = new BistroBuilderGeneralGameSaveData
        {
            currentMealService =
                (int)BistroBuilderMealServiceAvailability.Dinner
        };
        string generalMealServiceJson = JsonUtility.ToJson(
            generalMealServiceData,
            false
        );
        BistroBuilderGeneralGameSaveData generalMealServiceRoundTrip =
            JsonUtility.FromJson<BistroBuilderGeneralGameSaveData>(
                generalMealServiceJson
            );
        check(
            generalMealServiceRoundTrip != null &&
            generalMealServiceRoundTrip.currentMealService ==
                (int)BistroBuilderMealServiceAvailability.Dinner,
            "game.general conserva el servicio de comida en un ciclo JSON."
        );

        var legacyGeneralData = new BistroBuilderGeneralGameSaveData();
        check(
            legacyGeneralData.currentMealService ==
                (int)BistroBuilderMealServiceAvailability.None,
            "Una partida 366 sin servicio explícito conserva el marcador " +
            "None para migrar determinísticamente a comida."
        );

        var closedServiceData = new BistroBuilderActiveServiceSaveData
        {
            wasActiveService = false,
            nextGroupId = 1,
            nextLegacyOrderId = 1
        };
        check(
            closedServiceData.TryValidate(out error),
            "service.runtime admite un restaurante cerrado sin entidades."
        );
        check(
            IsConcreteMealService(
                (BistroBuilderMealServiceAvailability)
                    closedServiceData.currentMealService
            ),
            "service.runtime cerrado conserva un servicio concreto."
        );

        var structureTableRecord = new RestaurantPlaceableSaveRecord
        {
            instanceId = "table_identity_test",
            itemId = "table_basic",
            functionalTableId = 17,
            worldPosition = new BistroBuilderSaveVector3(Vector3.zero),
            worldRotation = new BistroBuilderSaveQuaternion(
                Quaternion.identity
            ),
            localScale = new BistroBuilderSaveVector3(Vector3.one)
        };
        string structureTableJson = JsonUtility.ToJson(
            structureTableRecord,
            false
        );
        RestaurantPlaceableSaveRecord structureTableRoundTrip =
            JsonUtility.FromJson<RestaurantPlaceableSaveRecord>(
                structureTableJson
            );
        check(
            structureTableRoundTrip != null &&
            structureTableRoundTrip.functionalTableId == 17,
            "restaurant.structure conserva la identidad funcional TableId."
        );

        var legacyStructureTableRecord =
            new RestaurantPlaceableSaveRecord();
        check(
            legacyStructureTableRecord.functionalTableId == 0,
            "Una partida estructural antigua usa el fallback TableId cero."
        );

        var pendingTableReservation =
            new BistroBuilderPendingBarTableReservationSaveRecord
            {
                groupId = 17,
                tableId = 4
            };
        check(
            pendingTableReservation.TryValidate(out error),
            "La reserva WaitingAtBar→mesa conserva identidades válidas."
        );

        string pendingJson = JsonUtility.ToJson(
            pendingTableReservation,
            false
        );
        BistroBuilderPendingBarTableReservationSaveRecord pendingRoundTrip =
            JsonUtility.FromJson<
                BistroBuilderPendingBarTableReservationSaveRecord
            >(pendingJson);
        check(
            pendingRoundTrip != null &&
            pendingRoundTrip.TryValidate(out error) &&
            pendingRoundTrip.groupId == 17 &&
            pendingRoundTrip.tableId == 4,
            "La reserva temporal de mesa supera un ciclo JSON completo."
        );

        check(
            closedServiceData.pendingBarTableReservations != null,
            "service.runtime incluye la colección de reservas temporales."
        );

        var freeTableWithoutGroup = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 1,
            state = (int)TableState.Free,
            groupId = 0
        };
        check(
            freeTableWithoutGroup.TryValidate(out error),
            "Una mesa libre sin grupo es un checkpoint válido."
        );

        var dirtyTableWithoutGroup = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 2,
            state = (int)TableState.Dirty,
            groupId = 0
        };
        check(
            dirtyTableWithoutGroup.TryValidate(out error),
            "Una mesa sucia sin grupo es un checkpoint válido."
        );

        var waitingTableWithoutGroup =
            new BistroBuilderTableRuntimeSaveRecord
            {
                tableId = 3,
                state = (int)TableState.WaitingForFood,
                groupId = 0
            };
        check(
            !waitingTableWithoutGroup.TryValidate(out error),
            "Una mesa WaitingForFood sin grupo se rechaza antes de guardar."
        );

        var waitingTableWithGroup = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 4,
            state = (int)TableState.WaitingForFood,
            groupId = 21
        };
        check(
            waitingTableWithGroup.TryValidate(out error),
            "Una mesa WaitingForFood con grupo es persistible."
        );

        var freeTableWithGroup = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 5,
            state = (int)TableState.Free,
            groupId = 21
        };
        check(
            !freeTableWithGroup.TryValidate(out error),
            "Una mesa Free con grupo se rechaza antes de guardar."
        );

        CustomerGroupSpawner spawner =
            UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();
        BistroBuilderCustomerSpawnerRuntimeSaveRecord currentSpawnState = null;
        bool currentSpawnStateValid = spawner != null &&
            spawner.TryCaptureRuntimeSpawnState(
                out currentSpawnState,
                out error
            ) &&
            currentSpawnState != null &&
            currentSpawnState.TryValidate(out error);
        check(
            currentSpawnStateValid,
            "El generador actual expone un calendario de llegadas persistible."
        );

        var pendingArrivalPlan =
            new BistroBuilderCustomerSpawnerRuntimeSaveRecord
            {
                scheduleInitialized = true,
                scheduleCompleted = false,
                secondsUntilNextArrival = 3.5f
            };
        pendingArrivalPlan.pendingArrivals.Add(
            new BistroBuilderCustomerArrivalPlanSaveRecord
            {
                groupSize = 1,
                serviceMode = (int)BistroBuilderServiceMode.BarService
            }
        );
        pendingArrivalPlan.pendingArrivals.Add(
            new BistroBuilderCustomerArrivalPlanSaveRecord
            {
                groupSize = 2,
                serviceMode = (int)BistroBuilderServiceMode.TableService
            }
        );
        check(
            pendingArrivalPlan.TryValidate(out error),
            "El calendario pendiente de llegadas es persistible."
        );

        string arrivalJson = JsonUtility.ToJson(pendingArrivalPlan, false);
        BistroBuilderCustomerSpawnerRuntimeSaveRecord arrivalRoundTrip =
            JsonUtility.FromJson<
                BistroBuilderCustomerSpawnerRuntimeSaveRecord
            >(arrivalJson);
        check(
            arrivalRoundTrip != null &&
            arrivalRoundTrip.TryValidate(out error) &&
            arrivalRoundTrip.pendingArrivals.Count == 2 &&
            Mathf.Approximately(
                arrivalRoundTrip.secondsUntilNextArrival,
                3.5f
            ),
            "La carga conserva próximas llegadas, modalidades y espera."
        );

        var completedArrivalPlan =
            new BistroBuilderCustomerSpawnerRuntimeSaveRecord
            {
                scheduleInitialized = true,
                scheduleCompleted = true,
                secondsUntilNextArrival = 0f
            };
        check(
            completedArrivalPlan.TryValidate(out error),
            "Un calendario completado impide duplicar clientes al cargar."
        );

        BistroBuilderBarSessionPhase preparingWithReservedTable =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.ClosingForTable,
                BistroBuilderServiceMode.WaitingAtBar,
                true,
                OrderState.Preparing,
                false,
                false,
                true
            );
        check(
            preparingWithReservedTable ==
                BistroBuilderBarSessionPhase.WaitingForItems,
            "Una mesa reservada no cierra una comanda todavía Preparing."
        );

        BistroBuilderBarSessionPhase servedPendingConsumption =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.ClosingForTable,
                BistroBuilderServiceMode.WaitingAtBar,
                true,
                OrderState.Served,
                true,
                false,
                true
            );
        check(
            servedPendingConsumption ==
                BistroBuilderBarSessionPhase.Consuming,
            "Una comanda servida reanuda el consumo antes del cierre."
        );

        BistroBuilderBarSessionPhase consumedWithReservedTable =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.WaitingForItems,
                BistroBuilderServiceMode.WaitingAtBar,
                true,
                OrderState.Served,
                true,
                true,
                true
            );
        check(
            consumedWithReservedTable ==
                BistroBuilderBarSessionPhase.ClosingForTable,
            "Una comanda consumida y con mesa reservada puede cerrarse."
        );

        BistroBuilderBarSessionPhase consumedWithoutTable =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.Consuming,
                BistroBuilderServiceMode.WaitingAtBar,
                true,
                OrderState.Served,
                true,
                true,
                false
            );
        check(
            consumedWithoutTable ==
                BistroBuilderBarSessionPhase
                    .WaitingForTableAfterConsumption,
            "Una comanda consumida sin mesa continúa esperando desde barra."
        );

        check(
            !BistroBuilderBarSessionRecoveryPolicy
                .CanCompleteWaitingAtBarOrder(
                    OrderState.Preparing,
                    true
                ),
            "Preparing nunca puede saltar directamente a Completed."
        );

        check(
            BistroBuilderBarSessionRecoveryPolicy
                .CanCompleteWaitingAtBarOrder(
                    OrderState.Served,
                    true
                ),
            "Solo Served con todas las líneas consumidas puede completarse."
        );

        BistroBuilderBarSessionPhase walkingWithoutOrder =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.WalkingToBar,
                BistroBuilderServiceMode.WaitingAtBar,
                false,
                OrderState.Created,
                false,
                false,
                false
            );
        check(
            walkingWithoutOrder ==
                BistroBuilderBarSessionPhase.WalkingToBar,
            "Una sesión sin comanda conserva el trayecto hacia barra."
        );

        BistroBuilderBarSessionPhase directBarConsumedButPreparing =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.WaitingForPayment,
                BistroBuilderServiceMode.BarService,
                true,
                OrderState.Preparing,
                true,
                true,
                false
            );
        check(
            directBarConsumedButPreparing ==
                BistroBuilderBarSessionPhase.WaitingForItems,
            "BarService no cobra mientras la fachada siga Preparing."
        );

        BistroBuilderBarSessionPhase directBarReadyForPayment =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.WaitingForItems,
                BistroBuilderServiceMode.BarService,
                true,
                OrderState.Served,
                true,
                true,
                false
            );
        check(
            directBarReadyForPayment ==
                BistroBuilderBarSessionPhase.WaitingForPayment,
            "BarService cobra solo cuando la comanda ya está Served."
        );

        BistroBuilderBarSessionPhase invalidNonBarMode =
            BistroBuilderBarSessionRecoveryPolicy.ResolveRestoredPhase(
                BistroBuilderBarSessionPhase.WaitingForItems,
                BistroBuilderServiceMode.TableService,
                true,
                OrderState.Preparing,
                false,
                false,
                false
            );
        check(
            invalidNonBarMode == BistroBuilderBarSessionPhase.Cancelled,
            "La recuperación rechaza modalidades que no sean de barra."
        );

        var validClosingSession =
            new BistroBuilderBarSessionSaveRecord
            {
                groupId = 31,
                anchorBarSpotId = "bar_spot_policy_01",
                serviceMode =
                    (int)BistroBuilderServiceMode.WaitingAtBar,
                phase =
                    (int)BistroBuilderBarSessionPhase.ClosingForTable,
                tableRequested = true
            };
        validClosingSession.occupiedBarSpotIds.Add(
            "bar_spot_policy_01"
        );
        check(
            validClosingSession.TryValidate(out error),
            "ClosingForTable exige y acepta WaitingAtBar con mesa solicitada."
        );

        var closingWithoutRequestedTable =
            new BistroBuilderBarSessionSaveRecord
            {
                groupId = 32,
                anchorBarSpotId = "bar_spot_policy_02",
                serviceMode =
                    (int)BistroBuilderServiceMode.WaitingAtBar,
                phase =
                    (int)BistroBuilderBarSessionPhase.ClosingForTable,
                tableRequested = false
            };
        closingWithoutRequestedTable.occupiedBarSpotIds.Add(
            "bar_spot_policy_02"
        );
        check(
            !closingWithoutRequestedTable.TryValidate(out error),
            "ClosingForTable sin mesa solicitada se rechaza."
        );

        var closingInDirectBarService =
            new BistroBuilderBarSessionSaveRecord
            {
                groupId = 33,
                anchorBarSpotId = "bar_spot_policy_03",
                serviceMode =
                    (int)BistroBuilderServiceMode.BarService,
                phase =
                    (int)BistroBuilderBarSessionPhase.ClosingForTable,
                tableRequested = true
            };
        closingInDirectBarService.occupiedBarSpotIds.Add(
            "bar_spot_policy_03"
        );
        check(
            !closingInDirectBarService.TryValidate(out error),
            "BarService no puede persistir ClosingForTable."
        );

        var waitingForTableInDirectBarService =
            new BistroBuilderBarSessionSaveRecord
            {
                groupId = 34,
                anchorBarSpotId = "bar_spot_policy_04",
                serviceMode =
                    (int)BistroBuilderServiceMode.BarService,
                phase = (int)BistroBuilderBarSessionPhase
                    .WaitingForTableAfterConsumption,
                tableRequested = false
            };
        waitingForTableInDirectBarService.occupiedBarSpotIds.Add(
            "bar_spot_policy_04"
        );
        check(
            !waitingForTableInDirectBarService.TryValidate(out error),
            "BarService no puede esperar una transición posterior a mesa."
        );

        string checkpointId =
            "service_checkpoint_" + Guid.NewGuid().ToString("N");
        check(
            BistroBuilderMenuIdUtility.IsValidStableId(checkpointId),
            "La identidad del checkpoint activo siempre empieza por letra."
        );

        BistroBuilderSaveGameService saveService =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSaveGameService
            >();
        if (saveService != null)
        {
            saveService.RefreshExtensions();
        }
        check(
            saveService != null &&
            saveService.HasProvider(
                BistroBuilderInventorySaveSectionProvider.StableSectionId
            ),
            "La plataforma registra inventory.canonical."
        );
        check(
            saveService != null &&
            saveService.HasProvider(
                BistroBuilderActiveServiceSaveSectionProvider.StableSectionId
            ),
            "La plataforma registra service.runtime."
        );

        BistroBuilderActiveServiceSaveGuard guard =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderActiveServiceSaveGuard
            >();
        check(
            guard != null && guard.ValidateConfiguration(out error),
            "La regla de guardado activo está configurada."
        );

        BistroBuilderSimulationSaveParticipant participant =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSimulationSaveParticipant
            >();
        check(
            participant != null && participant.ValidateConfiguration(out error),
            "El guardado congela la simulación de forma coordinada."
        );

        BistroBuilderOrderInventoryLifecycleService lifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOrderInventoryLifecycleService
            >();
        check(
            lifecycle != null && lifecycle.ValidateConfiguration(out error),
            "368CD puede reconstruir enlaces de reservas después de cargar."
        );

        check(
            typeof(BistroBuilderInventoryRuntimeSnapshot) ==
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderInventorySaveSectionProvider
                >()?.StateType,
            "La disponibilidad derivada no se guarda como segunda autoridad."
        );

        string report =
            "BISTRO BUILDER - AUTOTEST 368EF\n" +
            "Pruebas superadas: " + passed + "\n" +
            "Pruebas fallidas: " + failed + "\n" +
            string.Join("\n", lines);

        if (failed > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    private static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability service
    )
    {
        return service == BistroBuilderMealServiceAvailability.Breakfast ||
               service == BistroBuilderMealServiceAvailability.Lunch ||
               service == BistroBuilderMealServiceAvailability.Dinner;
    }

    private static bool TryCreateRuntimePreview(
        BistroBuilderInventoryService sourceInventory,
        BistroBuilderDishAvailabilityService sourceAvailability,
        BistroBuilderRecipeCatalogService recipes,
        BistroBuilderRestaurantMenuService menu,
        BistroBuilderCanonicalOrderIntegrationService integration,
        out GameObject root,
        out BistroBuilderInventoryService runtimeInventory,
        out BistroBuilderDishAvailabilityService runtimeAvailability,
        out string error
    )
    {
        root = null;
        runtimeInventory = null;
        runtimeAvailability = null;
        error = string.Empty;

        if (sourceInventory == null || sourceAvailability == null ||
            recipes == null || menu == null || integration == null)
        {
            error = "No están disponibles todas las dependencias para la " +
                    "previsualización runtime aislada de 368EF.";
            return false;
        }

        if (sourceInventory.OpeningStockProfile == null)
        {
            error = "Falta el perfil de stock inicial de 368EF.";
            return false;
        }

        try
        {
            root = new GameObject("BB_368EF_RuntimePreview");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            runtimeInventory =
                root.AddComponent<BistroBuilderInventoryService>();

            BistroBuilderGeneralGameStateService generalGameState =
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderGeneralGameStateService
                >();

            if (!TrySetObjectReference(
                    runtimeInventory,
                    "recipeCatalogService",
                    recipes,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeInventory,
                    "openingStockProfile",
                    sourceInventory.OpeningStockProfile,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeInventory,
                    "generalGameStateService",
                    generalGameState,
                    out error
                ) ||
                !TrySetBoolean(
                    runtimeInventory,
                    "logInitialization",
                    false,
                    out error
                ))
            {
                return false;
            }

            if (!runtimeInventory.TryInitialize(out error))
            {
                return false;
            }

            runtimeAvailability =
                root.AddComponent<BistroBuilderDishAvailabilityService>();

            if (!TrySetObjectReference(
                    runtimeAvailability,
                    "recipeCatalogService",
                    recipes,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "inventoryService",
                    runtimeInventory,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "menuService",
                    menu,
                    out error
                ) ||
                !TrySetObjectReference(
                    runtimeAvailability,
                    "orderIntegration",
                    integration,
                    out error
                ) ||
                !TrySetInteger(
                    runtimeAvailability,
                    "lowStockPortionThreshold",
                    sourceAvailability.LowStockPortionThreshold,
                    out error
                ) ||
                !TrySetBoolean(
                    runtimeAvailability,
                    "logChanges",
                    false,
                    out error
                ))
            {
                return false;
            }

            return runtimeAvailability.ValidateConfiguration(out error);
        }
        catch (Exception exception)
        {
            error = "No se pudo construir la previsualización aislada de " +
                    "368EF: " + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(error) && root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                runtimeInventory = null;
                runtimeAvailability = null;
            }
        }
    }

    private static bool TrySetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.ObjectReference)
        {
            error = "No existe la referencia serializada '" + propertyName +
                    "'.";
            return false;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static bool TrySetInteger(
        UnityEngine.Object target,
        string propertyName,
        int value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.Integer)
        {
            error = "No existe el entero serializado '" + propertyName +
                    "'.";
            return false;
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static bool TrySetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value,
        out string error
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.propertyType != SerializedPropertyType.Boolean)
        {
            error = "No existe el booleano serializado '" + propertyName +
                    "'.";
            return false;
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        error = string.Empty;
        return true;
    }

    private static bool TryGetTerminalState(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderDishAvailabilityState state
    )
    {
        if (!item.Unlocked)
        {
            state = BistroBuilderDishAvailabilityState.Locked;
            return true;
        }

        if (!item.Enabled)
        {
            state = BistroBuilderDishAvailabilityState.Disabled;
            return true;
        }

        if (item.ManuallySoldOut)
        {
            state = BistroBuilderDishAvailabilityState.ManuallyPaused;
            return true;
        }

        if ((item.AvailableServices & mealService) == 0)
        {
            state =
                BistroBuilderDishAvailabilityState.UnavailableForService;
            return true;
        }

        state = BistroBuilderDishAvailabilityState.Available;
        return false;
    }

    private static bool TryCalculateExpectedAvailability(
        BistroBuilderRecipeDefinition recipe,
        BistroBuilderInventoryService inventory,
        out long portions,
        out string limitingIngredientId,
        out string error
    )
    {
        portions = long.MaxValue;
        limitingIngredientId = string.Empty;
        error = string.Empty;

        if (recipe == null || inventory == null ||
            !recipe.TryValidate(out error))
        {
            return false;
        }

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

            long required = DivideCeiling(
                batchQuantity,
                recipe.YieldPortions
            );
            if (required <= 0L ||
                !inventory.TryGetStockSnapshot(
                    amount.Ingredient.IngredientId,
                    out BistroBuilderInventoryStockSnapshot stock
                ))
            {
                error = "Falta un balance de la receta.";
                return false;
            }

            long ingredientPortions =
                stock.AvailableCanonicalMilliUnits / required;
            if (ingredientPortions < portions ||
                ingredientPortions == portions &&
                string.CompareOrdinal(
                    stock.IngredientId,
                    limitingIngredientId
                ) < 0)
            {
                portions = ingredientPortions;
                limitingIngredientId = stock.IngredientId;
            }
        }

        return portions != long.MaxValue;
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

    private static bool ContainsInFlightDeliveryState(
        BistroBuilderCanonicalOrderRuntimeSnapshot snapshot
    )
    {
        if (snapshot == null)
        {
            return false;
        }

        for (int orderIndex = 0;
             orderIndex < snapshot.Orders.Count;
             orderIndex++)
        {
            BistroBuilderCanonicalOrder order = snapshot.Orders[orderIndex];
            for (int lineIndex = 0;
                 lineIndex < order.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLineState state =
                    order.Lines[lineIndex].State;
                if (state ==
                        BistroBuilderCanonicalOrderLineState.AssignedForDelivery ||
                    state == BistroBuilderCanonicalOrderLineState.InTransit)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
