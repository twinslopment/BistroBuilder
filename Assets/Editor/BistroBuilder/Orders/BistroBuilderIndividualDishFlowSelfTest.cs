using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de BistroBuilder 367D.
///
/// Utiliza objetos temporales HideAndDontSave y no modifica escenas, assets ni
/// partidas. Prueba el ciclo por línea sin ejecutar corrutinas de Play Mode.
/// </summary>
public static class BistroBuilderIndividualDishFlowSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();

    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Run 367D1 Individual Dish Flow Self-Test",
        false,
        222
    )]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        messages.Clear();

        GameObject root = null;
        GameObject tableObject = null;
        GameObject groupObject = null;
        GameObject waiterObject = null;

        try
        {
            RunKitchenExecutionGuardTests();
            RunContractTests();
            RunTaskQueueTests();
            RunKitchenSnapshotTests();

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

            root = CreateHiddenObject("__BB_367D_SELF_TEST__");
            BistroBuilderCanonicalOrderService canonical =
                root.AddComponent<BistroBuilderCanonicalOrderService>();
            BistroBuilderCanonicalOrderIntegrationService integration =
                root.AddComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            BistroBuilderOrderLineExecutionService execution =
                root.AddComponent<BistroBuilderOrderLineExecutionService>();

            SetObjectReference(canonical, "menuService", sceneMenu);
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

            Check(
                canonical.RebuildRuntimeIndex(out _),
                "La autoridad canónica temporal se inicializa."
            );
            Check(
                integration.ValidateConfiguration(out _),
                "La integración individual temporal se valida."
            );
            Check(
                execution.ValidateConfiguration(out _),
                "El ejecutor individual temporal se valida."
            );

            tableObject = CreateHiddenObject("__BB_367D_TABLE__");
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            SetInteger(table, "capacity", 3);
            table.AssignTableId(42);

            groupObject = CreateHiddenObject("__BB_367D_GROUP__");
            CustomerGroup group =
                groupObject.AddComponent<CustomerGroup>();
            Check(group.Initialize(77, 3), "El grupo temporal se inicializa.");
            Check(group.AssignTable(table), "El grupo ocupa la mesa temporal.");

            waiterObject = CreateHiddenObject("__BB_367D_WAITER__");
            Waiter waiter = waiterObject.AddComponent<Waiter>();
            SetInteger(waiter, "waiterId", 9);

            RunIndividualLifecycle(
                canonical,
                integration,
                execution,
                table,
                group,
                waiter
            );
        }
        catch (Exception exception)
        {
            Check(false, "Excepción no controlada: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
            DestroyImmediateSafe(root);
        }

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();
        report.AppendLine("BISTRO BUILDER - AUTOTEST 367D1");
        report.AppendLine("Pruebas superadas: " + passed);
        report.AppendLine("Pruebas fallidas: " + failed);

        for (int index = 0; index < messages.Count; index++)
            report.AppendLine(messages[index]);

        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            report.ToString(),
            "Aceptar"
        );
    }


    private static void RunKitchenExecutionGuardTests()
    {
        GameObject kitchenObject = null;

        try
        {
            kitchenObject = CreateHiddenObject(
                "__BB_367D1_KITCHEN_GUARD__"
            );
            KitchenSystem kitchen =
                kitchenObject.AddComponent<KitchenSystem>();

            Check(
                string.Equals(
                    KitchenSystem.RuntimeRevision,
                    "367D1",
                    StringComparison.Ordinal
                ),
                "La revisión runtime 367D1 está instalada."
            );

            MethodInfo claimMethod = typeof(KitchenSystem).GetMethod(
                "TryClaimProcessingLoop",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            MethodInfo releaseMethod = typeof(KitchenSystem).GetMethod(
                "ReleaseProcessingLoopClaim",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Check(
                claimMethod != null && releaseMethod != null,
                "La guardia anti-reentrada de cocina está disponible."
            );

            if (claimMethod == null || releaseMethod == null)
            {
                return;
            }

            bool firstClaim = (bool)claimMethod.Invoke(kitchen, null);
            bool nestedClaim = (bool)claimMethod.Invoke(kitchen, null);

            Check(
                firstClaim,
                "El primer consumidor reclama el bucle de cocina."
            );
            Check(
                !nestedClaim,
                "Una reentrada síncrona no crea un segundo consumidor."
            );

            releaseMethod.Invoke(kitchen, null);
            bool claimAfterRelease =
                (bool)claimMethod.Invoke(kitchen, null);

            Check(
                claimAfterRelease,
                "La guardia puede reutilizarse después de liberar el bucle."
            );

            releaseMethod.Invoke(kitchen, null);
        }
        finally
        {
            DestroyImmediateSafe(kitchenObject);
        }
    }

    private static void RunIndividualLifecycle(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderCanonicalOrderIntegrationService integration,
        BistroBuilderOrderLineExecutionService execution,
        RestaurantTable table,
        CustomerGroup group,
        Waiter waiter
    )
    {
        Check(
            integration.TryCreateCanonicalOrder(
                table,
                group,
                waiter,
                1,
                out string canonicalId,
                out _
            ),
            "Se crea una comanda canónica de tres clientes."
        );

        RestaurantOrder legacy = new RestaurantOrder(
            1,
            table,
            group,
            waiter,
            canonicalId,
            integration
        );

        Check(
            integration.TryRegisterLegacyOrder(legacy, out _),
            "La fachada legacy queda enlazada."
        );
        Check(
            legacy.TrySetState(OrderState.SentToKitchen),
            "La comanda entra en cocina."
        );

        Check(
            canonical.TryGetOrderSnapshot(
                canonicalId,
                out BistroBuilderCanonicalOrder initial
            ) && initial.Lines.Count == 3,
            "La comanda contiene una línea física por cliente."
        );

        List<string> lineIds = new List<string>();
        for (int index = 0; index < initial.Lines.Count; index++)
        {
            lineIds.Add(initial.Lines[index].LineId);
            Check(
                initial.Lines[index].State ==
                    BistroBuilderCanonicalOrderLineState.Queued,
                "La línea " + (index + 1) + " comienza en cola."
            );
        }

        string first = lineIds[0];
        string second = lineIds[1];
        string third = lineIds[2];

        Check(
            execution.TryResolvePreparationDurationSeconds(
                legacy,
                first,
                0.01f,
                0.1f,
                30f,
                out float duration,
                out _
            ) && duration > 0f,
            "La duración se resuelve desde la definición del plato."
        );

        Check(
            execution.TryBeginPreparation(
                legacy,
                first,
                "kitchen_selftest",
                out _
            ),
            "La primera línea comienza a prepararse."
        );
        Check(
            legacy.CurrentState == OrderState.Preparing,
            "La fachada coarse pasa a Preparing con la primera línea."
        );
        Check(
            execution.TryInterruptPreparation(
                legacy,
                first,
                "kitchen_selftest",
                out _
            ),
            "Una preparación interrumpida vuelve a la cola."
        );
        CheckLineState(
            canonical,
            canonicalId,
            first,
            BistroBuilderCanonicalOrderLineState.Queued,
            "El rollback de cocina conserva Queued."
        );
        Check(
            execution.TryBeginPreparation(
                legacy,
                first,
                "kitchen_selftest",
                out _
            ),
            "La primera línea puede reanudarse."
        );
        Check(
            execution.TryCompletePreparation(
                legacy,
                first,
                "kitchen_selftest",
                out bool firstProductionComplete,
                out _
            ) && !firstProductionComplete,
            "Terminar un plato no completa el resto de cocina."
        );
        CheckLineState(
            canonical,
            canonicalId,
            first,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            "El primer plato queda en el pase."
        );
        Check(
            legacy.CurrentState == OrderState.Preparing,
            "La comanda sigue Preparing mientras quedan líneas en cocina."
        );

        Check(
            execution.TryAssignLineForDelivery(
                legacy,
                first,
                waiter,
                out _
            ),
            "El primer plato se reserva para un camarero."
        );
        Check(
            waiter.AssignedOrderLineId == first,
            "El camarero conserva el LineId asignado."
        );
        Check(
            execution.TryMarkLineInTransit(
                legacy,
                first,
                waiter,
                out _
            ),
            "El primer plato abandona el pase."
        );
        Check(
            execution.TryReturnLineToPickup(
                legacy,
                first,
                waiter,
                out _
            ),
            "Un transporte interrumpido puede regresar al pase."
        );
        waiter.ClearAssignment();
        Check(
            execution.TryAssignLineForDelivery(
                legacy,
                first,
                waiter,
                out _
            ) &&
            execution.TryMarkLineInTransit(
                legacy,
                first,
                waiter,
                out _
            ),
            "El primer plato puede reasignarse tras el rollback."
        );
        Check(
            execution.TryMarkLineServed(
                legacy,
                first,
                waiter,
                out bool firstAllServed,
                out _
            ) && !firstAllServed,
            "Servir el primer plato no completa los demás."
        );
        waiter.ClearAssignment();
        Check(
            legacy.CurrentState == OrderState.Preparing,
            "El coarse permanece Preparing tras una entrega parcial."
        );

        PrepareAndServeLine(
            canonical,
            execution,
            legacy,
            second,
            waiter,
            false,
            "segunda"
        );
        Check(
            legacy.CurrentState == OrderState.Preparing,
            "Dos platos servidos no adelantan el tercero."
        );

        Check(
            execution.TryBeginPreparation(
                legacy,
                third,
                "kitchen_selftest",
                out _
            ),
            "La tercera línea comienza a prepararse."
        );
        Check(
            execution.TryCompletePreparation(
                legacy,
                third,
                "kitchen_selftest",
                out bool allProductionComplete,
                out _
            ) && allProductionComplete,
            "La última línea completa la producción de cocina."
        );
        Check(
            legacy.CurrentState == OrderState.ReadyForPickup,
            "La fachada pasa a ReadyForPickup al salir todo de cocina."
        );

        Check(
            execution.TryAssignLineForDelivery(
                legacy,
                third,
                waiter,
                out _
            ) &&
            execution.TryMarkLineInTransit(
                legacy,
                third,
                waiter,
                out _
            ),
            "La última línea entra en transporte."
        );
        Check(
            execution.TryMarkLineServed(
                legacy,
                third,
                waiter,
                out bool allServed,
                out _
            ) && allServed,
            "La última entrega detecta todos los platos servidos."
        );
        waiter.ClearAssignment();
        Check(
            legacy.CurrentState == OrderState.Served,
            "La fachada coarse pasa a Served una sola vez."
        );
        Check(
            legacy.TrySetState(OrderState.Completed),
            "El pago completa la fachada legacy."
        );
        Check(
            canonical.TryGetOrderSnapshot(
                canonicalId,
                out BistroBuilderCanonicalOrder completed
            ) &&
            completed.State == BistroBuilderCanonicalOrderState.Completed,
            "La comanda canónica alcanza Completed."
        );

        bool allConsumed = true;
        for (int index = 0; index < completed.Lines.Count; index++)
        {
            allConsumed &= completed.Lines[index].State ==
                BistroBuilderCanonicalOrderLineState.Consumed;
        }
        Check(allConsumed, "Todas las líneas se consumen atómicamente.");
        Check(
            !execution.TryBeginPreparation(
                legacy,
                first,
                "kitchen_selftest",
                out _
            ),
            "Una comanda terminal no reabre cocina."
        );
    }

    private static void PrepareAndServeLine(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderOrderLineExecutionService execution,
        RestaurantOrder legacy,
        string lineId,
        Waiter waiter,
        bool expectedAllServed,
        string label
    )
    {
        Check(
            execution.TryBeginPreparation(
                legacy,
                lineId,
                "kitchen_selftest",
                out _
            ),
            "La " + label + " línea comienza a prepararse."
        );
        Check(
            execution.TryCompletePreparation(
                legacy,
                lineId,
                "kitchen_selftest",
                out _,
                out _
            ),
            "La " + label + " línea termina su preparación."
        );
        Check(
            execution.TryAssignLineForDelivery(
                legacy,
                lineId,
                waiter,
                out _
            ),
            "La " + label + " línea se asigna a reparto."
        );
        Check(
            execution.TryMarkLineInTransit(
                legacy,
                lineId,
                waiter,
                out _
            ),
            "La " + label + " línea entra en tránsito."
        );
        Check(
            execution.TryMarkLineServed(
                legacy,
                lineId,
                waiter,
                out bool allServed,
                out _
            ) && allServed == expectedAllServed,
            "La " + label + " línea se sirve con agregado correcto."
        );
        waiter.ClearAssignment();
        CheckLineState(
            canonical,
            legacy.CanonicalOrderId,
            lineId,
            BistroBuilderCanonicalOrderLineState.Served,
            "La " + label + " línea conserva Served."
        );
    }

    private static void RunContractTests()
    {
        Check(
            BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.Preparing,
                BistroBuilderCanonicalOrderLineState.Queued
            ),
            "Preparing puede volver a Queued para rollback."
        );
        Check(
            BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup
            ),
            "Una asignación puede volver al pase."
        );
        Check(
            BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.InTransit,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup
            ),
            "Un transporte puede volver al pase."
        );
        Check(
            !BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.Served,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup
            ),
            "Un plato servido no puede duplicarse."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildKitchenReference("KITCHEN_MAIN") == "kitchen_main",
            "La referencia de cocina se normaliza."
        );
    }

    private static void RunTaskQueueTests()
    {
        GameObject tableObject = CreateHiddenObject("__BB_367D_TASK_TABLE__");
        GameObject groupObject = CreateHiddenObject("__BB_367D_TASK_GROUP__");
        GameObject waiterObject = CreateHiddenObject("__BB_367D_TASK_WAITER__");

        try
        {
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            CustomerGroup group =
                groupObject.AddComponent<CustomerGroup>();
            Waiter waiter = waiterObject.AddComponent<Waiter>();
            RestaurantOrder order = new RestaurantOrder(
                5,
                table,
                group,
                waiter
            );
            WaiterTaskQueue queue = new WaiterTaskQueue();

            string lineA = "line_task_000001";
            string lineB = "line_task_000002";

            Check(
                queue.TryCreateTask(
                    WaiterTaskType.DeliverFood,
                    WaiterTaskPriority.Urgent,
                    table,
                    order,
                    lineA,
                    out WaiterTask taskA
                ),
                "Se crea una tarea para la primera línea."
            );
            Check(
                queue.TryCreateTask(
                    WaiterTaskType.DeliverFood,
                    WaiterTaskPriority.Urgent,
                    table,
                    order,
                    lineB,
                    out WaiterTask taskB
                ),
                "La misma comanda admite otra línea de reparto."
            );
            Check(queue.Count == 2, "La cola conserva dos LineId distintos.");
            Check(
                !queue.TryCreateTask(
                    WaiterTaskType.DeliverFood,
                    WaiterTaskPriority.Urgent,
                    table,
                    order,
                    lineA,
                    out WaiterTask duplicate
                ) && ReferenceEquals(duplicate, taskA),
                "El mismo LineId no se duplica."
            );
            Check(
                taskA.OrderLineId == lineA && taskB.OrderLineId == lineB,
                "Cada tarea conserva su OrderLineId."
            );
            Check(
                queue.TryGetActiveTask(
                    WaiterTaskType.DeliverFood,
                    table,
                    order,
                    lineB,
                    out WaiterTask resolved
                ) && ReferenceEquals(resolved, taskB),
                "La búsqueda por LineId es determinista."
            );
            Check(
                queue.TryCancelTask(taskA) && queue.Count == 1,
                "Cancelar una línea no elimina otra."
            );
            Check(
                queue.TryCancelTask(taskB) && queue.Count == 0,
                "La cola se limpia sin referencias residuales."
            );
        }
        finally
        {
            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
        }
    }

    private static void RunKitchenSnapshotTests()
    {
        BistroBuilderKitchenRuntimeSnapshot snapshot =
            new BistroBuilderKitchenRuntimeSnapshot
            {
                kitchenId = "kitchen_selftest",
                nextSequence = 2
            };
        snapshot.workItems.Add(
            new BistroBuilderKitchenLineWorkSaveData
            {
                canonicalOrderId = "order_snapshot_001",
                orderLineId = "line_snapshot_001",
                dishId = "dish_snapshot_001",
                legacyOrderId = 1,
                sequence = 0,
                totalDurationSeconds = 4f,
                remainingDurationSeconds = 1.5f,
                wasActive = true
            }
        );
        snapshot.workItems.Add(
            new BistroBuilderKitchenLineWorkSaveData
            {
                canonicalOrderId = "order_snapshot_001",
                orderLineId = "line_snapshot_002",
                dishId = "dish_snapshot_002",
                legacyOrderId = 1,
                sequence = 1,
                totalDurationSeconds = 5f,
                remainingDurationSeconds = 5f,
                wasActive = false
            }
        );

        Check(snapshot.TryValidate(out _), "El snapshot de cocina es válido.");
        Check(
            snapshot.version == BistroBuilderKitchenRuntimeSnapshot.CurrentVersion,
            "El snapshot está versionado."
        );
        BistroBuilderKitchenRuntimeSnapshot clone = snapshot.Clone();
        Check(clone.TryValidate(out _), "El clon de cocina es válido.");
        Check(
            !ReferenceEquals(clone.workItems, snapshot.workItems),
            "La colección de cocina se clona profundamente."
        );
        Check(
            !ReferenceEquals(clone.workItems[0], snapshot.workItems[0]),
            "Los trabajos se clonan profundamente."
        );
        clone.workItems[0].remainingDurationSeconds = 0.25f;
        Check(
            Math.Abs(snapshot.workItems[0].remainingDurationSeconds - 1.5f) <
                0.001f,
            "Mutar el clon no altera el snapshot original."
        );

        BistroBuilderKitchenRuntimeSnapshot invalidActive = snapshot.Clone();
        invalidActive.workItems[1].wasActive = true;
        Check(
            !invalidActive.TryValidate(out _),
            "Dos líneas activas en una cocina se rechazan."
        );

        BistroBuilderKitchenRuntimeSnapshot duplicate = snapshot.Clone();
        duplicate.workItems[1].orderLineId =
            duplicate.workItems[0].orderLineId;
        Check(
            !duplicate.TryValidate(out _),
            "Un LineId duplicado en cocina se rechaza."
        );

        BistroBuilderKitchenRuntimeSnapshot invalidTime = snapshot.Clone();
        invalidTime.workItems[0].remainingDurationSeconds = 10f;
        Check(
            !invalidTime.TryValidate(out _),
            "Un tiempo restante mayor que el total se rechaza."
        );

        BistroBuilderKitchenRuntimeSnapshot zeroDuration = snapshot.Clone();
        zeroDuration.workItems[0].totalDurationSeconds = 0f;
        zeroDuration.workItems[0].remainingDurationSeconds = 0f;
        Check(
            !zeroDuration.TryValidate(out _),
            "Un trabajo de cocina con duración total cero se rechaza."
        );

        BistroBuilderKitchenRuntimeSnapshot duplicateSequence =
            snapshot.Clone();
        duplicateSequence.workItems[1].sequence =
            duplicateSequence.workItems[0].sequence;
        Check(
            !duplicateSequence.TryValidate(out _),
            "Una secuencia de cocina duplicada se rechaza."
        );

        BistroBuilderKitchenRuntimeSnapshot invalidNextSequence =
            snapshot.Clone();
        invalidNextSequence.nextSequence = 1;
        Check(
            !invalidNextSequence.TryValidate(out _),
            "La siguiente secuencia debe quedar después de la cola."
        );

        BistroBuilderKitchenRuntimeSnapshot invalidActiveOrder =
            snapshot.Clone();
        invalidActiveOrder.workItems[0].wasActive = false;
        invalidActiveOrder.workItems[1].wasActive = true;
        Check(
            !invalidActiveOrder.TryValidate(out _),
            "La línea activa debe ser el trabajo más antiguo."
        );
    }

    private static void CheckLineState(
        BistroBuilderCanonicalOrderService canonical,
        string orderId,
        string lineId,
        BistroBuilderCanonicalOrderLineState expected,
        string message
    )
    {
        Check(
            canonical.TryGetOrderAndLineSnapshot(
                orderId,
                lineId,
                out _,
                out BistroBuilderCanonicalOrderLine line
            ) && line.State == expected,
            message
        );
    }

    private static GameObject CreateHiddenObject(string name)
    {
        GameObject target = new GameObject(name);
        target.hideFlags = HideFlags.HideAndDontSave;
        target.SetActive(false);
        return target;
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property = RequireProperty(target, propertyName);
        property.objectReferenceValue = value;
        property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInteger(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SerializedProperty property = RequireProperty(target, propertyName);
        property.intValue = value;
        property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumValue(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SetInteger(target, propertyName, value);
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedProperty property = RequireProperty(target, propertyName);
        property.boolValue = value;
        property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty RequireProperty(
        UnityEngine.Object target,
        string propertyName
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + "."
            );

        return property;
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target != null)
            UnityEngine.Object.DestroyImmediate(target);
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
            messages.Add("- FALLO: " + message);
        }
    }
}
