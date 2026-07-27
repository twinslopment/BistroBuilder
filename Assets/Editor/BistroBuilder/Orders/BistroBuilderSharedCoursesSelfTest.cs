using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de BistroBuilder 367F.
///
/// Usa objetos HideAndDontSave, una comanda de dos clientes, un entrante
/// compartido y dos principales individuales. No modifica escenas ni assets.
/// </summary>
public static class BistroBuilderSharedCoursesSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();
    private static readonly List<BistroBuilderCustomerDiningChangedEvent>
        diningEvents = new List<BistroBuilderCustomerDiningChangedEvent>();
    private static readonly List<BistroBuilderCourseAndSharingChangedEvent>
        courseEvents = new List<BistroBuilderCourseAndSharingChangedEvent>();

    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Run 367F Shared Dishes and Courses Self-Test",
        false,
        242
    )]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        messages.Clear();
        diningEvents.Clear();
        courseEvents.Clear();

        GameObject root = null;
        GameObject tableObject = null;
        GameObject groupObject = null;
        GameObject waiterObject = null;
        BistroBuilderOrderCompositionProfile profile = null;

        try
        {
            RunPurePolicyTests();

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

            string menuError;
            if (!sceneMenu.ValidateConfiguration(out menuError))
            {
                throw new InvalidOperationException(menuError);
            }

            profile = CreateTestProfile();
            Check(profile != null, "Se crea el perfil temporal 367F.");
            Check(
                profile != null && profile.TryValidate(out _),
                "El perfil temporal se valida."
            );
            Check(
                profile != null && profile.Rules.Count == 2,
                "El perfil contiene dos reglas."
            );
            Check(
                profile != null &&
                profile.CoordinationPolicy ==
                    BistroBuilderCourseCoordinationPolicy.PerTable,
                "La política temporal coordina por mesa."
            );

            root = CreateHiddenObject("__BB_367F_SELF_TEST__");

            BistroBuilderCanonicalOrderService canonical =
                root.AddComponent<BistroBuilderCanonicalOrderService>();
            SetObjectReference(canonical, "menuService", sceneMenu);
            SetBoolean(canonical, "logChanges", false);

            BistroBuilderOrderCompositionService composition =
                root.AddComponent<BistroBuilderOrderCompositionService>();
            SetObjectReference(composition, "menuService", sceneMenu);
            SetObjectReference(
                composition,
                "compositionProfile",
                profile
            );
            SetBoolean(composition, "logComposition", false);

            BistroBuilderCanonicalOrderIntegrationService integration =
                root.AddComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            SetObjectReference(
                integration,
                "canonicalOrderService",
                canonical
            );
            SetObjectReference(
                integration,
                "orderCompositionService",
                composition
            );
            SetEnumValue(
                integration,
                "currentMealService",
                (int)BistroBuilderMealServiceAvailability.Lunch
            );
            SetInteger(integration, "defaultCourseIndex", 1);
            SetBoolean(integration, "individualLineExecutionEnabled", true);
            SetBoolean(
                integration,
                "courseAndSharingExecutionEnabled",
                true
            );
            SetBoolean(integration, "logSynchronization", false);

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
            SetFloat(dining, "defaultEatingDurationSeconds", 4f);
            SetFloat(
                dining,
                "perCustomerEatingDurationOffsetSeconds",
                1f
            );
            SetBoolean(dining, "logTransitions", false);

            BistroBuilderCourseAndSharingService courses =
                root.AddComponent<BistroBuilderCourseAndSharingService>();
            SetObjectReference(courses, "orderSystem", orderSystem);
            SetObjectReference(
                courses,
                "canonicalOrderService",
                canonical
            );
            SetObjectReference(
                courses,
                "compositionService",
                composition
            );
            SetObjectReference(
                courses,
                "customerDiningService",
                dining
            );
            SetBoolean(courses, "logTransitions", false);

            SetObjectReference(
                integration,
                "courseAndSharingService",
                courses
            );

            dining.DiningChanged += CaptureDiningEvent;
            courses.CourseAndSharingChanged += CaptureCourseEvent;

            Check(
                canonical.RebuildRuntimeIndex(out _),
                "La autoridad canónica temporal se inicializa."
            );
            Check(
                composition.ValidateConfiguration(out _),
                "El compositor temporal se valida."
            );
            Check(
                dining.RebuildRuntimeIndex(out _),
                "El consumo temporal se inicializa."
            );
            Check(
                courses.RebuildRuntimeIndex(out _),
                "El runtime temporal de pases se inicializa."
            );
            Check(
                courses.ValidateConfiguration(out _),
                "El coordinador temporal se valida."
            );
            Check(
                integration.ValidateConfiguration(out _),
                "La integración temporal 367F se valida."
            );
            Check(
                execution.ValidateConfiguration(out _),
                "La ejecución individual temporal se valida."
            );
            Check(
                orderSystem.ValidateConfiguration(out _),
                "OrderSystem temporal se valida."
            );
            Check(
                dining.ValidateConfiguration(out _),
                "El consumo individual temporal se valida."
            );
            Check(
                InvokePrivateVoid(dining, "Subscribe"),
                "El consumo temporal se suscribe."
            );
            Check(
                InvokePrivateVoid(courses, "Subscribe"),
                "El coordinador temporal se suscribe."
            );

            tableObject = CreateHiddenObject("__BB_367F_TABLE__");
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            SetInteger(table, "capacity", 2);
            table.AssignTableId(52);

            groupObject = CreateHiddenObject("__BB_367F_GROUP__");
            CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
            Check(group.Initialize(88, 2), "El grupo temporal se inicializa.");
            Check(group.AssignTable(table), "El grupo ocupa la mesa temporal.");
            table.SetState(TableState.WaitingForWaiter);

            waiterObject = CreateHiddenObject("__BB_367F_WAITER__");
            Waiter waiter = waiterObject.AddComponent<Waiter>();
            SetInteger(waiter, "waiterId", 12);
            Check(waiter.AssignTable(table), "El camarero acepta la mesa.");

            RestaurantOrder order = orderSystem.CreateOrder(table, waiter);
            Check(order != null, "Se crea una comanda 367F jugable.");
            Check(
                order != null && order.HasCanonicalOrder,
                "La comanda conserva CanonicalOrderId."
            );
            Check(
                dining.ActiveOrderCount == 1,
                "367E registra la sesión de consumo."
            );
            Check(
                courses.ActiveOrderCount == 1,
                "367F registra la sesión de pases."
            );

            if (order == null)
            {
                throw new InvalidOperationException(
                    "No se pudo crear la comanda temporal."
                );
            }

            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder initial
                ),
                "Se obtiene la comanda canónica compuesta."
            );
            Check(
                initial != null && initial.Lines.Count == 3,
                "La comanda contiene un compartido y dos individuales."
            );
            Check(
                initial != null && CountDistinctCourses(initial) == 2,
                "La comanda contiene dos pases reales."
            );

            if (initial == null || initial.Lines.Count != 3)
            {
                throw new InvalidOperationException(
                    "La composición 367F no generó tres líneas."
                );
            }

            FindCourseLines(
                initial,
                out BistroBuilderCanonicalOrderLine sharedStarter,
                out BistroBuilderCanonicalOrderLine firstMain,
                out BistroBuilderCanonicalOrderLine secondMain
            );

            Check(sharedStarter != null, "Se localiza el entrante compartido.");
            Check(
                sharedStarter != null && sharedStarter.IsShared,
                "El entrante tiene varios consumidores."
            );
            Check(
                sharedStarter != null &&
                sharedStarter.ConsumerCustomerIds.Count == 2,
                "El compartido pertenece a los dos clientes."
            );
            Check(firstMain != null && secondMain != null,
                "Se localizan los dos principales individuales.");
            Check(
                firstMain != null && !firstMain.IsShared &&
                secondMain != null && !secondMain.IsShared,
                "Los principales siguen siendo líneas individuales."
            );
            Check(
                firstMain != null && secondMain != null &&
                !string.Equals(
                    firstMain.PrimaryCustomerId,
                    secondMain.PrimaryCustomerId,
                    StringComparison.Ordinal
                ),
                "Cada principal pertenece a un cliente distinto."
            );

            Check(
                order.TrySetState(OrderState.SentToKitchen),
                "La comanda se envía a cocina mediante 367F."
            );
            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder submitted
                ),
                "Se fotografía la liberación inicial."
            );
            Check(
                sharedStarter != null &&
                GetLineState(submitted, sharedStarter.LineId) ==
                    BistroBuilderCanonicalOrderLineState.Queued,
                "Solo el pase 1 queda Queued."
            );
            Check(
                firstMain != null &&
                GetLineState(submitted, firstMain.LineId) ==
                    BistroBuilderCanonicalOrderLineState.Submitted,
                "El primer principal queda retenido en Submitted."
            );
            Check(
                secondMain != null &&
                GetLineState(submitted, secondMain.LineId) ==
                    BistroBuilderCanonicalOrderLineState.Submitted,
                "El segundo principal queda retenido en Submitted."
            );
            Check(
                courses.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCourseOrderRuntime initialCourseRuntime
                ),
                "Se obtiene el runtime inicial de pases."
            );
            Check(
                initialCourseRuntime != null &&
                initialCourseRuntime.IsCourseReleased(1) &&
                !initialCourseRuntime.IsCourseReleased(2),
                "El runtime registra únicamente el primer pase."
            );

            BistroBuilderCanonicalOrderOperationResult duplicateRelease =
                canonical.TryReleaseSubmittedLines(
                    order.CanonicalOrderId,
                    new[] { firstMain.LineId, firstMain.LineId },
                    "selftest_duplicate"
                );
            Check(
                !duplicateRelease.Succeeded,
                "Una liberación con LineId duplicado se rechaza."
            );
            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder afterDuplicate
                ) &&
                GetLineState(afterDuplicate, firstMain.LineId) ==
                    BistroBuilderCanonicalOrderLineState.Submitted,
                "El rechazo duplicado no modifica el pase retenido."
            );

            Check(
                AdvanceLineToServed(
                    canonical,
                    order.CanonicalOrderId,
                    sharedStarter.LineId,
                    "selftest_shared",
                    out string sharedServeError
                ),
                "El compartido llega a Served. " + sharedServeError
            );
            Check(
                dining.TryNotifyLineServed(
                    order,
                    sharedStarter.LineId,
                    out BistroBuilderCustomerDiningNotificationResult sharedNotice,
                    out string sharedNoticeError
                ),
                "El compartido se notifica al consumo. " + sharedNoticeError
            );
            Check(
                sharedNotice.StartedCustomerCount == 2,
                "El compartido inicia a sus dos consumidores."
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime sharedEating
                ) &&
                CountCustomersInState(
                    sharedEating,
                    BistroBuilderCustomerDiningCustomerState.Eating
                ) == 2,
                "Los dos clientes comen el pase compartido."
            );

            Check(
                dining.AdvanceDiningTime(4.1f, out string firstAdvanceError),
                "Avanza el primer tiempo compartido. " + firstAdvanceError
            );
            Check(
                dining.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCustomerDiningOrderRuntime partialShared
                ),
                "Se obtiene el progreso parcial compartido."
            );
            Check(
                CountCustomersInState(
                    partialShared,
                    BistroBuilderCustomerDiningCustomerState.WaitingForDish
                ) == 1,
                "Un consumidor espera el siguiente pase tras terminar."
            );
            Check(
                CountCustomersInState(
                    partialShared,
                    BistroBuilderCustomerDiningCustomerState.Eating
                ) == 1,
                "El segundo consumidor continúa comiendo."
            );
            Check(
                canonical.TryGetOrderAndLineSnapshot(
                    order.CanonicalOrderId,
                    sharedStarter.LineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine partialSharedLine
                ) &&
                partialSharedLine.State ==
                    BistroBuilderCanonicalOrderLineState.Served,
                "El compartido permanece Served con consumo parcial."
            );
            Check(
                CountDiningEvents(
                    BistroBuilderCustomerDiningChangeType.SharedLineProgressed
                ) == 1,
                "Se publica un progreso compartido parcial."
            );
            Check(
                GetLastSharedProgress().CompletedConsumerCount == 1 &&
                GetLastSharedProgress().TotalConsumerCount == 2,
                "El progreso compartido registra 1/2 consumidores."
            );
            Check(
                GetLineStateFromService(
                    canonical,
                    order.CanonicalOrderId,
                    firstMain.LineId
                ) == BistroBuilderCanonicalOrderLineState.Submitted,
                "El pase 2 continúa retenido con un consumidor activo."
            );

            Check(
                dining.AdvanceDiningTime(1f, out string secondAdvanceError),
                "Termina el segundo consumidor compartido. " +
                secondAdvanceError
            );
            Check(
                GetLineStateFromService(
                    canonical,
                    order.CanonicalOrderId,
                    sharedStarter.LineId
                ) == BistroBuilderCanonicalOrderLineState.Consumed,
                "El compartido pasa a Consumed tras completar 2/2."
            );
            Check(
                courses.TryEvaluateOrderNow(
                    order.CanonicalOrderId,
                    out string releaseError
                ),
                "Se evalúa la liberación del pase 2. " + releaseError
            );
            Check(
                GetLineStateFromService(
                    canonical,
                    order.CanonicalOrderId,
                    firstMain.LineId
                ) == BistroBuilderCanonicalOrderLineState.Queued,
                "El primer principal se libera a Queued."
            );
            Check(
                GetLineStateFromService(
                    canonical,
                    order.CanonicalOrderId,
                    secondMain.LineId
                ) == BistroBuilderCanonicalOrderLineState.Queued,
                "El segundo principal se libera a Queued."
            );
            Check(
                CountCourseEvents(
                    BistroBuilderCourseAndSharingChangeType.CourseReleased
                ) == 1,
                "367F publica una única liberación del pase 2."
            );
            Check(
                courses.TryGetOrderRuntimeSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCourseOrderRuntime releasedRuntime
                ) &&
                releasedRuntime.IsCourseReleased(2),
                "El runtime registra el pase 2 liberado."
            );

            Check(
                AdvanceLineToServed(
                    canonical,
                    order.CanonicalOrderId,
                    firstMain.LineId,
                    "selftest_main_1",
                    out string mainOneError
                ),
                "El primer principal llega a Served. " + mainOneError
            );
            Check(
                dining.TryNotifyLineServed(
                    order,
                    firstMain.LineId,
                    out BistroBuilderCustomerDiningNotificationResult mainOneNotice,
                    out string mainOneNoticeError
                ),
                "El primer principal se notifica. " + mainOneNoticeError
            );
            Check(
                mainOneNotice.StartedCustomerCount == 1,
                "El primer principal inicia solo a su cliente."
            );
            Check(
                dining.AdvanceDiningTime(4.1f, out string mainOneAdvanceError),
                "El primer cliente termina el principal. " +
                mainOneAdvanceError
            );
            Check(
                GetLineStateFromService(
                    canonical,
                    order.CanonicalOrderId,
                    firstMain.LineId
                ) == BistroBuilderCanonicalOrderLineState.Consumed,
                "El primer principal termina en Consumed."
            );
            Check(
                !dining.TryValidateBillReady(order, out _),
                "La cuenta sigue bloqueada con el segundo principal pendiente."
            );

            Check(
                AdvanceLineToServed(
                    canonical,
                    order.CanonicalOrderId,
                    secondMain.LineId,
                    "selftest_main_2",
                    out string mainTwoError
                ),
                "El segundo principal llega a Served. " + mainTwoError
            );
            Check(
                dining.TryNotifyLineServed(
                    order,
                    secondMain.LineId,
                    out BistroBuilderCustomerDiningNotificationResult mainTwoNotice,
                    out string mainTwoNoticeError
                ),
                "El segundo principal se notifica. " + mainTwoNoticeError
            );
            Check(
                mainTwoNotice.StartedCustomerCount == 1,
                "El segundo principal inicia solo a su cliente."
            );
            Check(
                dining.AdvanceDiningTime(5.1f, out string finalAdvanceError),
                "El segundo cliente termina el principal. " +
                finalAdvanceError
            );
            Check(
                canonical.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder completed
                ),
                "Se obtiene la comanda final."
            );
            Check(
                completed != null &&
                completed.State == BistroBuilderCanonicalOrderState.Completed,
                "La comanda canónica alcanza Completed."
            );
            Check(
                completed != null &&
                CountLinesInState(
                    completed,
                    BistroBuilderCanonicalOrderLineState.Consumed
                ) == 3,
                "Las tres líneas terminan en Consumed."
            );
            Check(
                dining.TryValidateBillReady(order, out _),
                "La cuenta queda habilitada al terminar todos los pases."
            );
            Check(
                table.CurrentState == TableState.WaitingForBill,
                "La mesa pasa a WaitingForBill."
            );
            Check(
                group.CurrentState == CustomerGroupState.WaitingForBill,
                "El grupo pasa a WaitingForBill."
            );
            Check(
                order.CurrentState == OrderState.Served,
                "La fachada legacy queda en Served antes del pago."
            );

            Check(
                courses.TryCaptureRuntimeSnapshot(
                    out BistroBuilderCourseAndSharingRuntimeSnapshot snapshot,
                    out string snapshotError
                ),
                "Se captura el snapshot 367F. " + snapshotError
            );
            Check(
                snapshot != null && snapshot.schemaVersion == 1,
                "El snapshot 367F utiliza esquema 1."
            );
            Check(
                snapshot != null && snapshot.TryValidate(out _),
                "El snapshot 367F se valida."
            );
            Check(
                courses.TryReplaceFromRuntimeSnapshot(
                    snapshot,
                    out string restoreError
                ),
                "El snapshot 367F se restaura atómicamente. " + restoreError
            );

            Check(
                orderSystem.CompleteOrder(order),
                "OrderSystem completa y retira la comanda."
            );
            Check(
                dining.ActiveOrderCount == 0,
                "El consumo retira su runtime al completar."
            );
            Check(
                courses.ActiveOrderCount == 0,
                "367F retira su runtime al completar."
            );
            Check(
                CountCourseEvents(
                    BistroBuilderCourseAndSharingChangeType.OrderRemoved
                ) == 1,
                "367F publica una única retirada de runtime."
            );
            Check(
                CountDiningEvents(
                    BistroBuilderCustomerDiningChangeType.BillReady
                ) == 1,
                "La cuenta se habilita una sola vez."
            );
        }
        catch (Exception exception)
        {
            failed++;
            messages.Add("- ERROR NO CONTROLADO: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
            DestroyImmediateSafe(root);
            DestroyImmediateSafe(profile);
        }

        string report =
            "BISTRO BUILDER - AUTOTEST 367F\n" +
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

    private static void RunPurePolicyTests()
    {
        Check(
            BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(0),
            "El pase 0 es válido."
        );
        Check(
            BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(20),
            "El pase 20 es válido."
        );
        Check(
            !BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(-1),
            "Un pase negativo se rechaza."
        );
        Check(
            !BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(21),
            "Un pase superior a 20 se rechaza."
        );
        Check(
            Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                BistroBuilderCourseCoordinationPolicy.PerTable
            ),
            "PerTable es una política publicada."
        );
        Check(
            Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                BistroBuilderCourseCoordinationPolicy.PerCustomer
            ),
            "PerCustomer es una política publicada."
        );
        Check(
            Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                BistroBuilderCourseCoordinationPolicy.Hybrid
            ),
            "Hybrid es una política publicada."
        );
        Check(
            Enum.IsDefined(
                typeof(BistroBuilderCourseCoordinationPolicy),
                BistroBuilderCourseCoordinationPolicy.Manual
            ),
            "Manual es una política publicada."
        );
        Check(
            string.Equals(
                BistroBuilderCourseAndSharingService.RuntimeRevision,
                "367F",
                StringComparison.Ordinal
            ),
            "El coordinador declara revisión 367F."
        );
        Check(
            string.Equals(
                BistroBuilderOrderCompositionService.RuntimeRevision,
                "367F",
                StringComparison.Ordinal
            ),
            "El compositor declara revisión 367F."
        );
        Check(
            string.Equals(
                BistroBuilderCustomerDiningService.RuntimeRevision,
                "367F",
                StringComparison.Ordinal
            ),
            "El consumo declara revisión 367F."
        );
        Check(
            string.Equals(
                KitchenSystem.RuntimeRevision,
                "367F",
                StringComparison.Ordinal
            ),
            "La cocina declara revisión 367F."
        );
    }

    private static BistroBuilderOrderCompositionProfile CreateTestProfile()
    {
        BistroBuilderOrderCompositionProfile profile =
            ScriptableObject.CreateInstance<
                BistroBuilderOrderCompositionProfile
            >();
        profile.hideFlags = HideFlags.HideAndDontSave;

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("coordinationPolicy").enumValueIndex =
            (int)BistroBuilderCourseCoordinationPolicy.PerTable;
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 2;

        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            1,
            BistroBuilderOrderLineCompositionMode.SharedAllCustomers,
            0
        );
        ConfigureRule(
            rules.GetArrayElementAtIndex(1),
            2,
            BistroBuilderOrderLineCompositionMode.IndividualPerCustomer,
            1
        );

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    private static void ConfigureRule(
        SerializedProperty rule,
        int courseIndex,
        BistroBuilderOrderLineCompositionMode mode,
        int menuOffset
    )
    {
        rule.FindPropertyRelative("enabled").boolValue = true;
        rule.FindPropertyRelative("courseIndex").intValue = courseIndex;
        rule.FindPropertyRelative("compositionMode").enumValueIndex =
            (int)mode;
        rule.FindPropertyRelative("menuDisplayOffset").intValue = menuOffset;
        rule.FindPropertyRelative("sharedGroupSize").intValue = 2;
    }

    private static void FindCourseLines(
        BistroBuilderCanonicalOrder order,
        out BistroBuilderCanonicalOrderLine sharedStarter,
        out BistroBuilderCanonicalOrderLine firstMain,
        out BistroBuilderCanonicalOrderLine secondMain
    )
    {
        sharedStarter = null;
        firstMain = null;
        secondMain = null;

        for (int index = 0; index < order.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[index];

            if (line.CourseIndex == 1 && line.IsShared)
            {
                sharedStarter = line;
            }
            else if (line.CourseIndex == 2 && firstMain == null)
            {
                firstMain = line;
            }
            else if (line.CourseIndex == 2)
            {
                secondMain = line;
            }
        }
    }

    private static bool AdvanceLineToServed(
        BistroBuilderCanonicalOrderService canonical,
        string orderId,
        string lineId,
        string actor,
        out string error
    )
    {
        int safety = 0;

        while (safety < 16)
        {
            safety++;

            if (!canonical.TryGetOrderAndLineSnapshot(
                    orderId,
                    lineId,
                    out _,
                    out BistroBuilderCanonicalOrderLine line
                ) ||
                line == null)
            {
                error = "No se encontró la línea de prueba.";
                return false;
            }

            if (line.State == BistroBuilderCanonicalOrderLineState.Served)
            {
                error = string.Empty;
                return true;
            }

            if (!BistroBuilderCanonicalOrderTransitionPolicy
                    .TryGetNormalNextState(
                        line.State,
                        out BistroBuilderCanonicalOrderLineState next
                    ) ||
                (int)next >
                    (int)BistroBuilderCanonicalOrderLineState.Served)
            {
                error = "La línea no tiene ruta normal hasta Served.";
                return false;
            }

            BistroBuilderCanonicalOrderOperationResult result =
                canonical.TryTransitionLine(lineId, next, actor);

            if (!result.Succeeded)
            {
                error = result.Message;
                return false;
            }
        }

        error = "Se superó el límite de transición de la línea.";
        return false;
    }

    private static BistroBuilderCanonicalOrderLineState GetLineState(
        BistroBuilderCanonicalOrder order,
        string lineId
    )
    {
        return order != null && order.TryGetLine(lineId, out var line)
            ? line.State
            : BistroBuilderCanonicalOrderLineState.Failed;
    }

    private static BistroBuilderCanonicalOrderLineState GetLineStateFromService(
        BistroBuilderCanonicalOrderService canonical,
        string orderId,
        string lineId
    )
    {
        return canonical.TryGetOrderAndLineSnapshot(
                   orderId,
                   lineId,
                   out _,
                   out BistroBuilderCanonicalOrderLine line
               ) && line != null
            ? line.State
            : BistroBuilderCanonicalOrderLineState.Failed;
    }

    private static int CountDistinctCourses(
        BistroBuilderCanonicalOrder order
    )
    {
        HashSet<int> courses = new HashSet<int>();

        for (int index = 0; index < order.Lines.Count; index++)
        {
            courses.Add(order.Lines[index].CourseIndex);
        }

        return courses.Count;
    }

    private static int CountLinesInState(
        BistroBuilderCanonicalOrder order,
        BistroBuilderCanonicalOrderLineState state
    )
    {
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

    private static int CountCustomersInState(
        BistroBuilderCustomerDiningOrderRuntime runtime,
        BistroBuilderCustomerDiningCustomerState state
    )
    {
        int count = 0;

        if (runtime == null)
        {
            return count;
        }

        for (int index = 0; index < runtime.Customers.Count; index++)
        {
            if (runtime.Customers[index].State == state)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountDiningEvents(
        BistroBuilderCustomerDiningChangeType type
    )
    {
        int count = 0;

        for (int index = 0; index < diningEvents.Count; index++)
        {
            if (diningEvents[index].ChangeType == type)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountCourseEvents(
        BistroBuilderCourseAndSharingChangeType type
    )
    {
        int count = 0;

        for (int index = 0; index < courseEvents.Count; index++)
        {
            if (courseEvents[index].ChangeType == type)
            {
                count++;
            }
        }

        return count;
    }

    private static BistroBuilderCustomerDiningChangedEvent
        GetLastSharedProgress()
    {
        for (int index = diningEvents.Count - 1; index >= 0; index--)
        {
            if (diningEvents[index].ChangeType ==
                BistroBuilderCustomerDiningChangeType.SharedLineProgressed)
            {
                return diningEvents[index];
            }
        }

        return default;
    }

    private static void CaptureDiningEvent(
        BistroBuilderCustomerDiningChangedEvent change
    )
    {
        diningEvents.Add(change);
    }

    private static void CaptureCourseEvent(
        BistroBuilderCourseAndSharingChangedEvent change
    )
    {
        courseEvents.Add(change);
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

    private static bool InvokePrivateVoid(object target, string methodName)
    {
        MethodInfo method = target?.GetType().GetMethod(
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
            throw new MissingFieldException(target.GetType().Name, fieldName);
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
            throw new MissingFieldException(target.GetType().Name, fieldName);
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
            throw new MissingFieldException(target.GetType().Name, fieldName);
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
            throw new MissingFieldException(target.GetType().Name, fieldName);
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
            throw new MissingFieldException(target.GetType().Name, fieldName);
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
