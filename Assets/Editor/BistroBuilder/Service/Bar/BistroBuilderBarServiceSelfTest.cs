using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest de modelo y de integración estructural para 367H.
/// No inicia un servicio ni modifica la escena guardada.
/// </summary>
public static class BistroBuilderBarServiceSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Service/Run 367H Bar Service Self-Test";

    private sealed class TestDishResolver : IBistroBuilderOrderDishResolver
    {
        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish dish,
            out string rejectionReason
        )
        {
            string normalized =
                BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

            if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
            {
                dish = default;
                rejectionReason = "DishId inválido.";
                return false;
            }

            dish = new BistroBuilderResolvedOrderDish(normalized, 250, 0);
            rejectionReason = string.Empty;
            return true;
        }
    }

    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();
    private static readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [MenuItem(MenuPath, false, 267)]
    private static void Run()
    {
        passed = 0;
        failed = 0;
        messages.Clear();
        temporaryObjects.Clear();

        try
        {
            RunServiceModeTests();
            RunBarReservationTests();
            RunOrderAndDeliveryTests();
            RunCanonicalOrderTests();
            RunInstalledSceneTests();
        }
        catch (Exception exception)
        {
            failed++;
            messages.Add("- ERROR NO CONTROLADO: " + exception);
        }
        finally
        {
            for (int index = temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (temporaryObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryObjects[index]);
                }
            }
        }

        string report =
            "BISTRO BUILDER - AUTOTEST 367H\n" +
            "Pruebas superadas: " + passed + "\n" +
            "Pruebas fallidas: " + failed + "\n" +
            string.Join("\n", messages);

        if (failed == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    private static void RunServiceModeTests()
    {
        Check(
            BistroBuilderServiceModeUtility.IsDefined(
                BistroBuilderServiceMode.TableService
            ),
            "TableService es una modalidad válida."
        );
        Check(
            BistroBuilderServiceModeUtility.IsBarMode(
                BistroBuilderServiceMode.BarService
            ),
            "BarService se reconoce como modalidad de barra."
        );
        Check(
            BistroBuilderServiceModeUtility.IsBarMode(
                BistroBuilderServiceMode.WaitingAtBar
            ),
            "WaitingAtBar se reconoce como modalidad de barra."
        );
        Check(
            !BistroBuilderServiceModeUtility.IsBarMode(
                BistroBuilderServiceMode.TableService
            ),
            "TableService no se confunde con una modalidad de barra."
        );
        Check(
            BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                BistroBuilderDishServiceModeAvailability.All,
                false
            ),
            "La máscara completa de modalidades es válida."
        );
        Check(
            !BistroBuilderServiceModeUtility.IsValidAvailabilityMask(
                (BistroBuilderDishServiceModeAvailability)128,
                true
            ),
            "Los bits desconocidos de modalidad se rechazan."
        );
        Check(
            BistroBuilderServiceModeUtility.ToAvailability(
                BistroBuilderServiceMode.WaitingAtBar
            ) == BistroBuilderDishServiceModeAvailability.WaitingAtBar,
            "La conversión de WaitingAtBar a máscara es exacta."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility.BuildBarSpotReference(
                "bar_spot_01"
            ) == "bar_spot_01",
            "La identidad estable de plaza de barra se conserva."
        );
    }

    private static void RunBarReservationTests()
    {
        GameObject root = CreateHiddenObject("BB367H_BarReservationTest");
        root.SetActive(false);
        BistroBuilderBarServiceRegistry registry =
            root.AddComponent<BistroBuilderBarServiceRegistry>();

        BistroBuilderBarServiceSpot first = CreateSpot(root.transform, 1, 0f);
        BistroBuilderBarServiceSpot second = CreateSpot(root.transform, 2, 1f);
        BistroBuilderBarServiceSpot third = CreateSpot(root.transform, 3, 2f);

        Check(registry.RegisterSpot(first), "La primera plaza se registra.");
        Check(registry.RegisterSpot(second), "La segunda plaza se registra.");
        Check(registry.RegisterSpot(third), "La tercera plaza se registra.");
        Check(registry.RegisteredSpotCount == 3, "El registro contiene 3 plazas.");
        Check(registry.FreeCapacity == 3, "La capacidad inicial libre es 3.");

        GameObject groupObject = CreateHiddenObject("BB367H_GroupTwo");
        CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
        Check(
            group.Initialize(
                9101,
                2,
                BistroBuilderServiceMode.WaitingAtBar
            ),
            "El grupo de dos clientes se inicializa para WaitingAtBar."
        );

        Check(
            registry.TryAllocateSpot(
                group,
                BistroBuilderServiceMode.WaitingAtBar,
                out BistroBuilderBarServiceSpot anchor
            ),
            "La reserva multiplaza se completa atómicamente."
        );
        Check(anchor != null, "La reserva devuelve una plaza ancla.");
        Check(group.HasAssignedBarSpot, "El grupo conserva su plaza ancla.");
        Check(
            group.CurrentServiceMode == BistroBuilderServiceMode.WaitingAtBar,
            "El grupo adopta la modalidad WaitingAtBar."
        );
        Check(
            registry.GetReservedCapacity(group) == 2,
            "La reserva cubre exactamente a los dos clientes."
        );

        List<BistroBuilderBarServiceSpot> occupied =
            new List<BistroBuilderBarServiceSpot>();
        Check(
            registry.GetOccupiedSpots(group, occupied) == 2,
            "El grupo ocupa dos plazas físicas."
        );
        Check(registry.FreeCapacity == 1, "Queda una plaza libre.");
        Check(
            registry.ValidateConfiguration(out _),
            "El registro valida una ocupación multiplaza coherente."
        );
        Check(registry.ReleaseGroup(group), "La liberación conjunta tiene éxito.");
        Check(!group.HasAssignedBarSpot, "La referencia ancla queda limpia.");
        Check(registry.FreeCapacity == 3, "Toda la capacidad vuelve a estar libre.");

        // La plaza duplicada debe reutilizar exactamente la identidad ya
        // registrada. Antes se empleaba "bar_spot_01", mientras que las
        // plazas del escenario usan "bar_spot_selftest_01..03"; por tanto,
        // el supuesto duplicado era en realidad una identidad nueva y el
        // registro actuaba correctamente al aceptarla.
        GameObject duplicateRoot = CreateHiddenObject("BB367H_DuplicateSpot");
        duplicateRoot.transform.SetParent(root.transform, false);
        BistroBuilderBarServiceSpot duplicate =
            ConfigureSpot(duplicateRoot, first.BarSpotId);
        bool duplicateRejected = !registry.TryRegisterSpot(
            duplicate,
            out string duplicateRejectionReason
        );
        Check(
            duplicateRejected &&
            string.Equals(
                duplicateRejectionReason,
                "BarSpotId duplicado: " + first.BarSpotId + ".",
                StringComparison.Ordinal
            ),
            "El registro rechaza BarSpotId duplicados sin contaminar Console."
        );
        Check(
            registry.RegisteredSpotCount == 3,
            "Rechazar un BarSpotId duplicado no altera el registro."
        );
    }

    private static void RunOrderAndDeliveryTests()
    {
        GameObject waiterObject = CreateHiddenObject("BB367H_Waiter");
        Waiter waiter = waiterObject.AddComponent<Waiter>();

        GameObject tableObject = CreateHiddenObject("BB367H_Table");
        RestaurantTable table = tableObject.AddComponent<RestaurantTable>();
        table.AssignTableId(701);

        GameObject tableGroupObject = CreateHiddenObject("BB367H_TableGroup");
        CustomerGroup tableGroup = tableGroupObject.AddComponent<CustomerGroup>();
        tableGroup.Initialize(9201, 2, BistroBuilderServiceMode.TableService);

        RestaurantOrder tableOrder = new RestaurantOrder(
            1,
            table,
            tableGroup,
            waiter
        );

        GameObject barGroupObject = CreateHiddenObject("BB367H_BarGroup");
        CustomerGroup barGroup = barGroupObject.AddComponent<CustomerGroup>();
        barGroup.Initialize(9202, 1, BistroBuilderServiceMode.BarService);

        GameObject spotObject = CreateHiddenObject("BB367H_OrderSpot");
        BistroBuilderBarServiceSpot spot =
            ConfigureSpot(spotObject, "bar_spot_order_test");
        Check(spot.TryOccupy(barGroup), "La plaza de prueba se ocupa.");
        Check(
            barGroup.TryAssignBarSpot(
                spot,
                BistroBuilderServiceMode.BarService
            ),
            "El grupo enlaza la plaza de prueba."
        );

        RestaurantOrder barOrder = new RestaurantOrder(
            2,
            spot,
            barGroup,
            waiter,
            BistroBuilderServiceMode.BarService
        );

        Check(tableOrder.HasTableDestination, "La comanda de mesa conserva mesa.");
        Check(!tableOrder.HasBarDestination, "La comanda de mesa no tiene barra.");
        Check(barOrder.HasBarDestination, "La comanda de barra conserva plaza.");
        Check(!barOrder.HasTableDestination, "La comanda de barra no crea mesa proxy.");
        Check(
            barOrder.DestinationKind ==
                BistroBuilderServiceDestinationKind.BarSpot,
            "La comanda declara destino BarSpot."
        );
        Check(
            barOrder.ServiceMode == BistroBuilderServiceMode.BarService,
            "La modalidad de la comanda de barra es explícita."
        );

        WaiterTask tableTaskOne = new WaiterTask(
            1,
            WaiterTaskType.DeliverFood,
            WaiterTaskPriority.Urgent,
            table,
            tableOrder,
            "order_line_table_367h_01",
            1
        );
        WaiterTask tableTaskTwo = new WaiterTask(
            2,
            WaiterTaskType.DeliverFood,
            WaiterTaskPriority.Urgent,
            table,
            tableOrder,
            "order_line_table_367h_02",
            2
        );
        WaiterTask barTask = new WaiterTask(
            3,
            WaiterTaskType.DeliverFood,
            WaiterTaskPriority.Urgent,
            spot,
            barOrder,
            "order_line_bar_367h_01",
            3
        );

        Check(tableTaskOne.HasValidDestination, "La tarea de mesa tiene destino.");
        Check(barTask.HasValidDestination, "La tarea de barra tiene destino.");
        Check(
            barTask.DestinationKind ==
                BistroBuilderServiceDestinationKind.BarSpot,
            "La tarea de barra conserva su tipo de destino."
        );

        GameObject kitchenObject = CreateHiddenObject("BB367H_Kitchen");
        kitchenObject.SetActive(false);
        KitchenSystem kitchen = kitchenObject.AddComponent<KitchenSystem>();
        BistroBuilderDeliveryRun run = new BistroBuilderDeliveryRun(
            1,
            kitchen,
            3,
            new[] { tableTaskOne, tableTaskTwo, barTask }
        );

        Check(run.Items.Count == 3, "La ronda mixta contiene tres platos.");
        Check(run.Stops.Count == 2, "La ronda mixta crea dos paradas.");
        Check(
            run.Stops[0].DestinationKind ==
                BistroBuilderServiceDestinationKind.Table,
            "La primera parada pertenece a la mesa."
        );
        Check(
            run.Stops[1].DestinationKind ==
                BistroBuilderServiceDestinationKind.BarSpot,
            "La segunda parada pertenece a la barra."
        );
        Check(run.TryAssignWaiter(waiter), "La ronda acepta un camarero.");
        Check(run.TryBeginPickup(), "La ronda comienza una única recogida.");
        Check(
            run.TryMarkLineInTransit(tableOrder, tableTaskOne.OrderLineId) &&
            run.TryMarkLineInTransit(tableOrder, tableTaskTwo.OrderLineId) &&
            run.TryMarkLineInTransit(barOrder, barTask.OrderLineId),
            "Los tres platos pasan a tránsito."
        );
        Check(run.TryBeginDelivery(), "La ronda inicia la ruta de entrega.");
        Check(
            run.TryMarkLineServed(tableOrder, tableTaskOne.OrderLineId) &&
            run.TryMarkLineServed(tableOrder, tableTaskTwo.OrderLineId),
            "Los dos platos de mesa se sirven en la primera parada."
        );
        Check(run.TryAdvanceStop(), "La ronda avanza de mesa a barra.");
        Check(
            run.TryMarkLineServed(barOrder, barTask.OrderLineId),
            "El plato de barra se sirve en la segunda parada."
        );
        Check(run.TryComplete(), "La ronda mixta termina sin volver a cocina.");
        Check(
            run.State == BistroBuilderDeliveryRunState.Completed &&
            run.RemainingLineCount == 0,
            "La ronda mixta queda Completed y sin pendientes."
        );
    }

    private static void RunCanonicalOrderTests()
    {
        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                externalReferenceId = "legacy_order_367h_test",
                tableReferenceId = "bar_spot_canonical_367h",
                customerGroupReferenceId = "group_367h_test",
                serviceMode = BistroBuilderServiceMode.BarService,
                mealService = BistroBuilderMealServiceAvailability.Lunch
            };
        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_agua_mineral",
                "customer_367h_01",
                new[] { "customer_367h_01" },
                1
            )
        );

        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            new TestDishResolver(),
            1,
            out BistroBuilderCanonicalOrder order,
            out BistroBuilderCanonicalOrderOperationResult result
        );

        Check(created && result.Succeeded, "La fábrica acepta una comanda de barra.");
        Check(order != null, "La fábrica devuelve el agregado canónico.");
        Check(
            order != null &&
            order.ServiceMode == BistroBuilderServiceMode.BarService,
            "El agregado canónico conserva BarService."
        );
        Check(
            order != null &&
            order.ServiceDestinationReferenceId ==
                "bar_spot_canonical_367h",
            "El agregado conserva la identidad de plaza."
        );
        Check(
            order != null && order.TryValidate(out _),
            "La comanda canónica de barra se valida."
        );

        request.serviceMode = (BistroBuilderServiceMode)99;
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                request,
                new TestDishResolver(),
                2,
                out _,
                out _
            ),
            "La fábrica rechaza modalidades desconocidas."
        );
    }

    private static void RunInstalledSceneTests()
    {
        BistroBuilderBarServiceValidationResult result =
            BistroBuilderBarServiceValidator.ValidateCurrentScene();
        Check(
            result.ErrorCount == 0,
            "La escena instalada supera el validador 367H."
        );
        Check(
            result.CorrectCount >= 10,
            "La validación estructural cubre los subsistemas principales."
        );
    }

    private static BistroBuilderBarServiceSpot CreateSpot(
        Transform parent,
        int index,
        float x
    )
    {
        GameObject gameObject = CreateHiddenObject(
            "BB367H_Spot_" + index.ToString("D2")
        );
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = new Vector3(x, 0f, 0f);
        return ConfigureSpot(
            gameObject,
            "bar_spot_selftest_" + index.ToString("D2")
        );
    }

    private static BistroBuilderBarServiceSpot ConfigureSpot(
        GameObject gameObject,
        string spotId
    )
    {
        BistroBuilderBarServiceSpot spot =
            gameObject.GetComponent<BistroBuilderBarServiceSpot>();

        if (spot == null)
        {
            spot = gameObject.AddComponent<BistroBuilderBarServiceSpot>();
        }

        Transform customer = new GameObject("CustomerPoint").transform;
        customer.SetParent(gameObject.transform, false);
        Transform waiter = new GameObject("WaiterPoint").transform;
        waiter.SetParent(gameObject.transform, false);

        SerializedObject serialized = new SerializedObject(spot);
        RequireProperty(serialized, "barSpotId").stringValue = spotId;
        RequireProperty(serialized, "customerPoint").objectReferenceValue =
            customer;
        RequireProperty(serialized, "waiterServicePoint").objectReferenceValue =
            waiter;
        RequireProperty(serialized, "capacity").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spot;
    }

    private static GameObject CreateHiddenObject(string name)
    {
        GameObject gameObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName + "."
            );
        }

        return property;
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            messages.Add("- OK: " + message);
        }
        else
        {
            failed++;
            messages.Add("- ERROR: " + message);
        }
    }
}
