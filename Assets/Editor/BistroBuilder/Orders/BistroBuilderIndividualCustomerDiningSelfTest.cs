using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de BistroBuilder 367E.
///
/// Utiliza objetos temporales HideAndDontSave. No modifica escenas, prefabs,
/// partidas ni assets. El tiempo de consumo se avanza de forma determinista.
/// </summary>
public static class BistroBuilderIndividualCustomerDiningSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();
    private static readonly List<BistroBuilderCustomerDiningChangedEvent>
        diningEvents = new List<BistroBuilderCustomerDiningChangedEvent>();

    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Run 367E Individual Customer Dining Self-Test",
        false,
        232
    )]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        messages.Clear();
        diningEvents.Clear();

        GameObject root = null;
        GameObject tableObject = null;
        GameObject groupObject = null;
        GameObject waiterObject = null;

        try
        {
            RunPureContractTests();
            RunRuntimeModelTests();

            BistroBuilderRestaurantMenuService sceneMenu =
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderRestaurantMenuService
                >();

            Check(sceneMenu != null, "La carta 367A está disponible.");

            if (sceneMenu == null)
            {
                throw new InvalidOperationException(
                    "No se encontró una carta válida."
                );
            }

            if (!sceneMenu.ValidateConfiguration(out string menuError))
            {
                throw new InvalidOperationException(menuError);
            }

            root = CreateHiddenObject("__BB_367E_SELF_TEST__");

            BistroBuilderCanonicalOrderService canonical =
                root.AddComponent<BistroBuilderCanonicalOrderService>();
            SetObjectReference(canonical, "menuService", sceneMenu);
            SetBoolean(canonical, "logChanges", false);

            BistroBuilderCanonicalOrderIntegrationService integration =
                root.AddComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            SetObjectReference(
                integration,
                "canonicalOrderService",
                canonical
            );
            SetEnumValue(
                integration,
                "currentMealService",
                (int)BistroBuilderMealServiceAvailability.Lunch
            );
            SetInteger(integration, "defaultCourseIndex", 1);
            SetBoolean(integration, "logSynchronization", false);
            SetBoolean(
                integration,
                "individualLineExecutionEnabled",
                true
            );

            BistroBuilderOrderLineExecutionService execution =
                root.AddComponent<BistroBuilderOrderLineExecutionService>();
            SetObjectReference(
                execution,
                "canonicalOrderService",
                canonical
            );
            SetObjectReference(
                execution,
                "integrationService",
                integration
            );
            SetBoolean(execution, "logTransitions", false);

            OrderSystem orderSystem = root.AddComponent<OrderSystem>();
            SetObjectReference(
                orderSystem,
                "canonicalIntegrationService",
                integration
            );

            BistroBuilderCustomerDiningService dining =
                root.AddComponent<BistroBuilderCustomerDiningService>();
            SetObjectReference(dining, "orderSystem", orderSystem);
            SetObjectReference(
                dining,
                "canonicalOrderService",
                canonical
            );
            SetObjectReference(
                dining,
                "lineExecutionService",
                execution
            );
            SetFloat(dining, "defaultEatingDurationSeconds", 10f);
            SetBoolean(dining, "logTransitions", false);
            dining.DiningChanged += CaptureDiningEvent;

            Check(
                canonical.RebuildRuntimeIndex(out _),
                "La autoridad canónica temporal se inicializa."
            );
            Check(
                integration.ValidateConfiguration(out _),
                "La integración 367D temporal se valida."
            );
            Check(
                execution.ValidateConfiguration(out _),
                "El ejecutor de líneas temporal se valida."
            );
            Check(
                orderSystem.ValidateConfiguration(out _),
                "OrderSystem temporal se valida."
            );
            Check(
                dining.RebuildRuntimeIndex(out _),
                "El índice de consumo temporal se inicializa."
            );
            Check(
                dining.ValidateConfiguration(out _),
                "La autoridad de consumo temporal se valida."
            );
            Check(
                InvokePrivateVoid(dining, "Subscribe"),
                "La autoridad de consumo se suscribe a OrderSystem."
            );

            tableObject = CreateHiddenObject("__BB_367E_TABLE__");
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            SetInteger(table, "capacity", 2);
            table.AssignTableId(42);

            groupObject = CreateHiddenObject("__BB_367E_GROUP__");
            CustomerGroup group =
                groupObject.AddComponent<CustomerGroup>();
            Check(group.Initialize(77, 2), "El grupo temporal se inicializa.");
            Check(group.AssignTable(table), "El grupo ocupa la mesa temporal.");
            table.SetState(TableState.WaitingForWaiter);

            waiterObject = CreateHiddenObject("__BB_367E_WAITER__");
            Waiter waiter = waiterObject.AddComponent<Waiter>();
            SetInteger(waiter, "waiterId", 9);
            Check(
                waiter.AssignTable(table),
                "El camarero acepta la mesa temporal."
            );

            Check(
                ReferenceEquals(waiter.AssignedTable, table),
                "El camarero queda asignado a la mesa."
            );

            RestaurantOrder order = orderSystem.CreateOrder(table, waiter);

            Check(order != null, "Se crea la comanda jugable temporal.");
            Check(
                order != null && order.HasCanonicalOrder,
                "La comanda temporal conserva CanonicalOrderId."
            );
            Check(
                dining.ActiveOrderCount == 1,
                "OrderCreated registra una sesión de consumo."
            );

            if (order == null)
            {
                throw new InvalidOperationException(
                    "No se pudo crear la comanda de prueba."
                );
            }

            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder initialOrder
                ),
                "Se obtiene la comanda canónica inicial."
            );
            Check(
                initialOrder != null && initialOrder.Lines.Count == 2,
                "La comanda contiene una línea por cliente."
            );

            if (initialOrder == null || initialOrder.Lines.Count != 2)
            {
                throw new InvalidOperationException(
                    "La comanda canónica no contiene dos líneas."
                );
            }

            BistroBuilderCanonicalOrderLine firstLine = initialOrder.Lines[0];
            BistroBuilderCanonicalOrderLine secondLine = initialOrder.Lines[1];

            Check(
                !string.Equals(
                    firstLine.LineId,
                    secondLine.LineId,
                    StringComparison.Ordinal
                ),
                "Las líneas conservan LineId distintos."
            );
            Check(
                !string.Equals(
                    firstLine.PrimaryCustomerId,
                    secondLine.PrimaryCustomerId,
                    StringComparison.Ordinal
                ),
                "Cada línea pertenece a un CustomerId distinto."
            );

            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime initialRuntime
                ),
                "Se obtiene la sesión individual inicial."
            );
            Check(
                initialRuntime != null && initialRuntime.Customers.Count == 2,
                "La sesión contiene dos clientes individuales."
            );
            Check(
                CountCustomersInState(
                    initialRuntime,
                    BistroBuilderCustomerDiningCustomerState.WaitingForDish
                ) == 2,
                "Los dos clientes comienzan esperando plato."
            );

            group.SetState(CustomerGroupState.WaitingForFood);
            table.SetState(TableState.WaitingForFood);
            Check(
                order.TrySetState(OrderState.SentToKitchen),
                "La comanda entra en la cola de cocina."
            );

            string firstServeError = string.Empty;
            bool firstServed = AdvanceLineToServed(
                canonical,
                execution,
                order,
                firstLine.LineId,
                "selftest_367e_first",
                out firstServeError
            );
            Check(
                firstServed,
                "La primera línea llega a Served. " + firstServeError
            );

            BistroBuilderCanonicalOrderOperationResult duplicateConsume =
                canonical.TryConsumeServedLines(
                    order.CanonicalOrderId,
                    new List<string>
                    {
                        firstLine.LineId,
                        firstLine.LineId
                    },
                    "selftest_367e_duplicate"
                );
            Check(
                !duplicateConsume.Succeeded &&
                duplicateConsume.FailureReason ==
                    BistroBuilderCanonicalOrderFailureReason.DuplicateLineId,
                "El consumo atómico rechaza LineId duplicados."
            );
            Check(
                canonical.TryGetOrderAndLineSnapshot(
                    order.CanonicalOrderId,
                    firstLine.LineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine lineAfterRollback
                ) &&
                lineAfterRollback.State ==
                    BistroBuilderCanonicalOrderLineState.Served,
                "El rechazo atómico no consume parcialmente la línea."
            );

            BistroBuilderCustomerDiningNotificationResult firstNotice;
            string firstNoticeError = string.Empty;
            bool firstNotified = dining.TryNotifyLineServed(
                order,
                firstLine.LineId,
                out firstNotice,
                out firstNoticeError
            );
            Check(
                firstNotified,
                "La primera línea se reconcilia. " + firstNoticeError
            );
            Check(
                firstNotice.StartedCustomerCount == 1,
                "Solo un cliente comienza a comer con el primer plato."
            );

            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime afterFirstServe
                ),
                "Se fotografía el consumo tras el primer plato."
            );
            Check(
                CountCustomersInState(
                    afterFirstServe,
                    BistroBuilderCustomerDiningCustomerState.Eating
                ) == 1,
                "Existe exactamente un cliente Eating."
            );
            Check(
                CountCustomersInState(
                    afterFirstServe,
                    BistroBuilderCustomerDiningCustomerState.WaitingForDish
                ) == 1,
                "El segundo cliente continúa WaitingForDish."
            );
            Check(
                group.CurrentState == CustomerGroupState.WaitingForFood,
                "La fachada de grupo sigue esperando mientras falta un plato."
            );
            Check(
                table.CurrentState == TableState.WaitingForFood,
                "La mesa sigue esperando mientras falta un plato."
            );
            Check(
                !dining.TryValidateBillReady(order, out _),
                "La cuenta está bloqueada tras servir solo un plato."
            );

            BistroBuilderCustomerDiningRuntimeSnapshot partialSnapshot;
            string partialSnapshotError = string.Empty;
            bool partialCaptured = dining.TryCaptureRuntimeSnapshot(
                out partialSnapshot,
                out partialSnapshotError
            );
            Check(
                partialCaptured,
                "Se captura un snapshot parcial. " + partialSnapshotError
            );
            Check(
                partialSnapshot != null &&
                partialSnapshot.TryValidate(out _),
                "El snapshot parcial es válido."
            );
            Check(
                partialSnapshot != null &&
                partialSnapshot.Clone().TryValidate(out _),
                "El clon del snapshot parcial es válido."
            );

            string firstAdvanceError = string.Empty;
            bool firstAdvance = dining.AdvanceDiningTime(
                4f,
                out firstAdvanceError
            );
            Check(
                firstAdvance,
                "Avanzan cuatro segundos individuales. " + firstAdvanceError
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime afterFourSeconds
                ),
                "Se fotografía el consumo tras cuatro segundos."
            );
            Check(
                TryGetCustomer(
                    afterFourSeconds,
                    firstLine.PrimaryCustomerId,
                    out BistroBuilderCustomerDiningCustomerRuntime firstCustomer
                ),
                "Se localiza al primer cliente."
            );
            Check(
                firstCustomer != null &&
                Approximately(firstCustomer.RemainingEatingSeconds, 6f),
                "El primer cliente conserva seis segundos."
            );
            string restoreError = string.Empty;
            bool restored = dining.TryReplaceFromRuntimeSnapshot(
                partialSnapshot != null ? partialSnapshot.Clone() : null,
                false,
                out restoreError
            );
            Check(
                restored,
                "El snapshot parcial se restaura atómicamente. " +
                restoreError
            );
            BistroBuilderCustomerDiningOrderRuntime restoredRuntime = null;
            BistroBuilderCustomerDiningCustomerRuntime restoredCustomer = null;
            bool restoredRuntimeFound = dining.TryGetOrderRuntimeSnapshot(
                order.CanonicalOrderId,
                out restoredRuntime
            );
            bool restoredCustomerFound = restoredRuntimeFound &&
                TryGetCustomer(
                    restoredRuntime,
                    firstLine.PrimaryCustomerId,
                    out restoredCustomer
                );
            Check(
                restoredCustomerFound,
                "Se localiza al cliente tras restaurar el snapshot."
            );
            Check(
                restoredCustomer != null &&
                Approximately(restoredCustomer.RemainingEatingSeconds, 10f),
                "La restauración recupera el tiempo individual exacto."
            );

            string replayAdvanceError = string.Empty;
            bool replayAdvance = dining.AdvanceDiningTime(
                4f,
                out replayAdvanceError
            );
            Check(
                replayAdvance,
                "El tiempo restaurado vuelve a avanzar. " +
                replayAdvanceError
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime replayRuntime
                ) &&
                TryGetCustomer(
                    replayRuntime,
                    firstLine.PrimaryCustomerId,
                    out BistroBuilderCustomerDiningCustomerRuntime replayCustomer
                ) &&
                replayCustomer != null &&
                Approximately(replayCustomer.RemainingEatingSeconds, 6f),
                "El temporizador restaurado conserva avance determinista."
            );
            Check(
                !dining.TryValidateBillReady(order, out _),
                "La cuenta continúa bloqueada durante el consumo parcial."
            );

            string secondServeError = string.Empty;
            bool secondServed = AdvanceLineToServed(
                canonical,
                execution,
                order,
                secondLine.LineId,
                "selftest_367e_second",
                out secondServeError
            );
            Check(
                secondServed,
                "La segunda línea llega a Served. " + secondServeError
            );
            BistroBuilderCustomerDiningNotificationResult secondNotice;
            string secondNoticeError = string.Empty;
            bool secondNotified = dining.TryNotifyLineServed(
                order,
                secondLine.LineId,
                out secondNotice,
                out secondNoticeError
            );
            Check(
                secondNotified,
                "La segunda línea se reconcilia. " + secondNoticeError
            );
            Check(
                secondNotice.StartedCustomerCount == 1,
                "El segundo plato inicia únicamente al segundo cliente."
            );
            Check(
                secondNotice.AllCustomersStartedOrCompleted,
                "Todos los clientes ya han empezado o terminado."
            );
            Check(
                group.CurrentState == CustomerGroupState.Eating,
                "La fachada de grupo pasa a Eating al empezar todos."
            );
            Check(
                table.CurrentState == TableState.Eating,
                "La fachada de mesa pasa a Eating al empezar todos."
            );
            Check(
                order.CurrentState == OrderState.Served,
                "La fachada legacy alcanza Served."
            );

            string secondAdvanceError = string.Empty;
            bool secondAdvance = dining.AdvanceDiningTime(
                6f,
                out secondAdvanceError
            );
            Check(
                secondAdvance,
                "Avanzan seis segundos adicionales. " + secondAdvanceError
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime staggeredRuntime
                ),
                "Se fotografía el consumo escalonado."
            );
            Check(
                TryGetCustomer(
                    staggeredRuntime,
                    firstLine.PrimaryCustomerId,
                    out firstCustomer
                ) &&
                firstCustomer.State ==
                    BistroBuilderCustomerDiningCustomerState.Completed,
                "El primer cliente termina antes que el segundo."
            );
            Check(
                TryGetCustomer(
                    staggeredRuntime,
                    secondLine.PrimaryCustomerId,
                    out BistroBuilderCustomerDiningCustomerRuntime secondCustomer
                ) &&
                secondCustomer.State ==
                    BistroBuilderCustomerDiningCustomerState.Eating,
                "El segundo cliente continúa comiendo."
            );
            Check(
                secondCustomer != null &&
                Approximately(secondCustomer.RemainingEatingSeconds, 4f),
                "El segundo cliente conserva cuatro segundos."
            );
            Check(
                canonical.TryGetOrderAndLineSnapshot(
                    order.CanonicalOrderId,
                    firstLine.LineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine firstConsumedLine
                ) &&
                firstConsumedLine.State ==
                    BistroBuilderCanonicalOrderLineState.Consumed,
                "La primera línea pasa individualmente a Consumed."
            );
            Check(
                canonical.TryGetOrderAndLineSnapshot(
                    order.CanonicalOrderId,
                    secondLine.LineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine secondEatingLine
                ) &&
                secondEatingLine.State ==
                    BistroBuilderCanonicalOrderLineState.Served,
                "La segunda línea permanece Served mientras se consume."
            );
            Check(
                !dining.TryValidateBillReady(order, out _),
                "La cuenta sigue bloqueada con un cliente comiendo."
            );

            string finalAdvanceError = string.Empty;
            bool finalAdvance = dining.AdvanceDiningTime(
                4f,
                out finalAdvanceError
            );
            Check(
                finalAdvance,
                "El segundo cliente termina su tiempo. " + finalAdvanceError
            );
            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder completedCanonical
                ),
                "Se obtiene la comanda tras el consumo."
            );
            Check(
                completedCanonical != null &&
                completedCanonical.State ==
                    BistroBuilderCanonicalOrderState.Completed,
                "La comanda canónica alcanza Completed."
            );
            Check(
                completedCanonical != null &&
                CountLinesInState(
                    completedCanonical,
                    BistroBuilderCanonicalOrderLineState.Consumed
                ) == 2,
                "Las dos líneas terminan en Consumed."
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime completedRuntime
                ),
                "Se obtiene el runtime individual completado."
            );
            Check(
                completedRuntime != null &&
                completedRuntime.AllCustomersCompleted,
                "Todos los clientes individuales están Completed."
            );
            Check(
                completedRuntime != null && completedRuntime.BillRequested,
                "La cuenta queda solicitada una sola vez."
            );
            Check(
                group.CurrentState == CustomerGroupState.WaitingForBill,
                "El grupo solicita la cuenta tras terminar todos."
            );
            Check(
                table.CurrentState == TableState.WaitingForBill,
                "La mesa solicita la cuenta tras terminar todos."
            );
            string billReadyError = string.Empty;
            bool billReady = dining.TryValidateBillReady(
                order,
                out billReadyError
            );
            Check(
                billReady,
                "La guardia autoriza la cuenta final. " + billReadyError
            );

            Check(
                CountEvents(
                    BistroBuilderCustomerDiningChangeType
                        .CustomerStartedCourse
                ) == 2,
                "Cada cliente inicia el pase exactamente una vez."
            );
            Check(
                CountEvents(
                    BistroBuilderCustomerDiningChangeType
                        .CustomerCompletedCourse
                ) == 2,
                "Cada cliente completa el pase exactamente una vez."
            );
            Check(
                CountEvents(
                    BistroBuilderCustomerDiningChangeType.LineConsumed
                ) == 2,
                "Cada línea se consume exactamente una vez."
            );
            Check(
                CountEvents(
                    BistroBuilderCustomerDiningChangeType.BillReady
                ) == 1,
                "La cuenta se solicita exactamente una vez."
            );

            BistroBuilderCustomerDiningRuntimeSnapshot finalSnapshot;
            string finalSnapshotError = string.Empty;
            bool finalCaptured = dining.TryCaptureRuntimeSnapshot(
                out finalSnapshot,
                out finalSnapshotError
            );
            Check(
                finalCaptured,
                "Se captura el snapshot final. " + finalSnapshotError
            );
            Check(
                finalSnapshot != null &&
                finalSnapshot.Orders.Count == 1,
                "El snapshot final conserva la sesión activa."
            );
            Check(
                finalSnapshot != null &&
                finalSnapshot.TryValidate(out _),
                "El snapshot final es válido."
            );

            Check(
                orderSystem.CompleteOrder(order),
                "OrderSystem completa la fachada legacy."
            );
            Check(
                dining.ActiveOrderCount == 0,
                "OrderCompleted retira la sesión de consumo activa."
            );
            Check(
                CountEvents(
                    BistroBuilderCustomerDiningChangeType.OrderRemoved
                ) == 1,
                "La sesión se retira exactamente una vez."
            );
        }
        catch (Exception exception)
        {
            Check(false, "Excepción no controlada: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            BistroBuilderCustomerDiningService dining =
                root != null
                    ? root.GetComponent<BistroBuilderCustomerDiningService>()
                    : null;

            if (dining != null)
            {
                dining.DiningChanged -= CaptureDiningEvent;
            }

            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
            DestroyImmediateSafe(root);
        }

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();
        report.AppendLine("BISTRO BUILDER - AUTOTEST 367E");
        report.AppendLine("Pruebas superadas: " + passed);
        report.AppendLine("Pruebas fallidas: " + failed);

        for (int index = 0; index < messages.Count; index++)
        {
            report.AppendLine(messages[index]);
        }

        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            report.ToString(),
            "Aceptar"
        );
    }

    private static void RunPureContractTests()
    {
        Check(
            BistroBuilderCustomerDiningPolicy.IsTerminal(
                BistroBuilderCustomerDiningCustomerState.Completed
            ),
            "Completed es terminal."
        );
        Check(
            BistroBuilderCustomerDiningPolicy.IsTerminal(
                BistroBuilderCustomerDiningCustomerState.Cancelled
            ),
            "Cancelled es terminal."
        );
        Check(
            BistroBuilderCustomerDiningPolicy.IsTerminal(
                BistroBuilderCustomerDiningCustomerState.Failed
            ),
            "Failed es terminal."
        );
        Check(
            !BistroBuilderCustomerDiningPolicy.IsTerminal(
                BistroBuilderCustomerDiningCustomerState.WaitingForDish
            ),
            "WaitingForDish no es terminal."
        );
        Check(
            !BistroBuilderCustomerDiningPolicy.IsTerminal(
                BistroBuilderCustomerDiningCustomerState.Eating
            ),
            "Eating no es terminal."
        );
        Check(
            string.Equals(
                BistroBuilderCustomerDiningService.RuntimeRevision,
                "367E",
                StringComparison.Ordinal
            ),
            "La autoridad runtime declara la revisión 367E."
        );
        Check(
            BistroBuilderCustomerDiningRuntimeSnapshot.CurrentSchemaVersion == 1,
            "El snapshot 367E comienza en esquema 1."
        );
        Check(
            typeof(BistroBuilderCanonicalOrderService).GetMethod(
                "TryConsumeServedLines",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "La autoridad canónica expone consumo atómico."
        );
        Check(
            typeof(CustomerDiningFlow).GetField(
                "activeRoutine",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) == null,
            "CustomerDiningFlow no conserva corrutina grupal."
        );
        Check(
            typeof(CustomerDiningFlow).GetMethod(
                "EatingRoutine",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) == null,
            "CustomerDiningFlow no conserva EatingRoutine."
        );
    }

    private static void RunRuntimeModelTests()
    {
        BistroBuilderCustomerDiningCustomerRuntime customer =
            new BistroBuilderCustomerDiningCustomerRuntime(
                "customer_test_001",
                1
            );

        Check(customer.TryValidate(out _), "El runtime de cliente inicial valida.");
        Check(
            customer.State ==
                BistroBuilderCustomerDiningCustomerState.WaitingForDish,
            "El runtime de cliente comienza WaitingForDish."
        );
        Check(
            customer.TryStartCourse(1, 5f, out _),
            "El runtime inicia un pase válido."
        );
        Check(
            !customer.TryStartCourse(1, 5f, out _),
            "No puede iniciar dos veces el mismo temporizador."
        );
        Check(
            !customer.AdvanceTime(2f),
            "El pase no termina antes de tiempo."
        );
        Check(
            Approximately(customer.RemainingEatingSeconds, 3f),
            "El temporizador individual descuenta tiempo."
        );
        Check(customer.AdvanceTime(3f), "El pase informa su finalización.");
        Check(
            customer.AddConsumedLineClaim("order_line_test_001"),
            "Se registra una reclamación de línea."
        );
        Check(
            !customer.AddConsumedLineClaim("order_line_test_001"),
            "No se duplican reclamaciones de línea."
        );
        customer.SetCompleted();
        Check(customer.TryValidate(out _), "El runtime completado valida.");
        Check(customer.IsTerminal, "El runtime completado es terminal.");

        List<BistroBuilderCustomerDiningCustomerRuntime> customers =
            new List<BistroBuilderCustomerDiningCustomerRuntime> { customer };
        BistroBuilderCustomerDiningOrderRuntime order =
            new BistroBuilderCustomerDiningOrderRuntime(
                "order_test_001",
                1,
                "group_test_001",
                "table_test_001",
                customers
            );

        Check(order.TryValidate(out _), "El runtime de comanda valida.");
        Check(order.AllCustomersCompleted, "La comanda detecta clientes completos.");
        order.MarkBillRequested();
        Check(order.BillRequested, "La comanda registra la cuenta solicitada.");
        Check(order.TryValidate(out _), "La comanda con cuenta solicitada valida.");

        BistroBuilderCustomerDiningRuntimeSnapshot snapshot =
            new BistroBuilderCustomerDiningRuntimeSnapshot(
                new List<BistroBuilderCustomerDiningOrderRuntime> { order }
            );

        Check(snapshot.TryValidate(out _), "El snapshot unitario valida.");
        Check(snapshot.Clone().TryValidate(out _), "El clon unitario valida.");
    }

    private static bool AdvanceLineToServed(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderOrderLineExecutionService execution,
        RestaurantOrder order,
        string lineId,
        string actor,
        out string error
    )
    {
        error = string.Empty;

        BistroBuilderCanonicalOrderLineState[] states =
        {
            BistroBuilderCanonicalOrderLineState.Preparing,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
            BistroBuilderCanonicalOrderLineState.InTransit,
            BistroBuilderCanonicalOrderLineState.Served
        };

        for (int index = 0; index < states.Length; index++)
        {
            BistroBuilderCanonicalOrderOperationResult result =
                canonical.TryTransitionLine(lineId, states[index], actor);

            if (!result.Succeeded)
            {
                error = result.Message;
                return false;
            }

            if (!execution.TrySynchronizeLegacyOrder(
                    order,
                    out _,
                    out _,
                    out error
                ))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountCustomersInState(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCustomerDiningCustomerState state
    )
    {
        if (runtime == null)
        {
            return 0;
        }

        int count = 0;

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            if (runtime.Customers[index].State == state)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLinesInState(
        BistroBuilderCanonicalOrder order,
        BistroBuilderCanonicalOrderLineState state
    )
    {
        if (order == null)
        {
            return 0;
        }

        int count = 0;

        for (int index = 0; index < order.Lines.Count; index++)
        {
            if (order.Lines[index].State == state)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryGetCustomer(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        string customerId,
        out BistroBuilderCustomerDiningCustomerRuntime customer
    )
    {
        customer = null;
        return runtime != null &&
               runtime.TryGetCustomer(customerId, out customer);
    }

    private static int CountEvents(
        BistroBuilderCustomerDiningChangeType changeType
    )
    {
        int count = 0;

        for (int index = 0; index < diningEvents.Count; index++)
        {
            if (diningEvents[index].ChangeType == changeType)
            {
                count++;
            }
        }

        return count;
    }

    private static void CaptureDiningEvent(
        BistroBuilderCustomerDiningChangedEvent change
    )
    {
        diningEvents.Add(change);
    }

    private static bool Approximately(float first, float second)
    {
        return Mathf.Abs(first - second) <= 0.01f;
    }

    private static GameObject CreateHiddenObject(string name)
    {
        GameObject gameObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        gameObject.SetActive(false);

        return gameObject;
    }

    private static bool InvokePrivateVoid(
        object target,
        string methodName
    )
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (method == null || method.GetParameters().Length != 0)
        {
            return false;
        }

        method.Invoke(target, null);
        return true;
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.GetType().Name,
                fieldName
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string fieldName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.GetType().Name,
                fieldName
            );
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInteger(
        UnityEngine.Object target,
        string fieldName,
        int value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.GetType().Name,
                fieldName
            );
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumValue(
        UnityEngine.Object target,
        string fieldName,
        int value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.GetType().Name,
                fieldName
            );
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        UnityEngine.Object target,
        string fieldName,
        float value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new MissingFieldException(
                target.GetType().Name,
                fieldName
            );
        }

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
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
