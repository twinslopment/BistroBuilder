using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de la integración 367C.
///
/// Utiliza objetos temporales HideAndDontSave, comparte únicamente la carta
/// validada de la escena en modo lectura y no modifica escenas, assets ni
/// partidas guardadas.
/// </summary>
public static class BistroBuilderCanonicalOrderIntegrationSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> messages =
        new List<string>();

    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Run 367C Service Integration Self-Test",
        false,
        212
    )]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        messages.Clear();

        GameObject temporaryRoot = null;
        GameObject tableObject = null;
        GameObject groupObject = null;
        GameObject waiterObject = null;

        try
        {
            RunIdentityTests();
            RunStateMapTests();
            RunTransitionGateTests();

            BistroBuilderRestaurantMenuService sceneMenu =
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderRestaurantMenuService
                >();

            Check(
                sceneMenu != null,
                "La carta 367A de la escena está disponible."
            );

            if (sceneMenu == null)
            {
                throw new InvalidOperationException(
                    "No se puede ejecutar el test integrado. " +
                    "No se encontró la carta runtime 367A."
                );
            }

            if (!sceneMenu.ValidateConfiguration(out string menuError))
            {
                throw new InvalidOperationException(
                    "No se puede ejecutar el test integrado. " +
                    menuError
                );
            }

            temporaryRoot = new GameObject(
                "__BB_367C_SELF_TEST__"
            );
            temporaryRoot.hideFlags = HideFlags.HideAndDontSave;
            temporaryRoot.SetActive(false);

            BistroBuilderCanonicalOrderService canonical =
                temporaryRoot.AddComponent<
                    BistroBuilderCanonicalOrderService
                >();
            BistroBuilderCanonicalOrderIntegrationService integration =
                temporaryRoot.AddComponent<
                    BistroBuilderCanonicalOrderIntegrationService
                >();
            OrderSystem orderSystem =
                temporaryRoot.AddComponent<OrderSystem>();

            SetObjectReference(
                canonical,
                "menuService",
                sceneMenu
            );
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
            SetInteger(
                integration,
                "defaultCourseIndex",
                1
            );
            SetBoolean(
                integration,
                "logSynchronization",
                false
            );
            SetObjectReference(
                orderSystem,
                "canonicalIntegrationService",
                integration
            );

            Check(
                canonical.RebuildRuntimeIndex(out _),
                "La autoridad canónica temporal se inicializa."
            );
            Check(
                integration.ValidateConfiguration(out _),
                "La integración temporal se valida."
            );
            Check(
                orderSystem.ValidateConfiguration(out _),
                "OrderSystem temporal se valida."
            );

            tableObject = new GameObject(
                "__BB_367C_TABLE__"
            );
            tableObject.hideFlags = HideFlags.HideAndDontSave;
            tableObject.SetActive(false);
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            SetInteger(table, "capacity", 3);
            table.AssignTableId(42);

            groupObject = new GameObject(
                "__BB_367C_GROUP__"
            );
            groupObject.hideFlags = HideFlags.HideAndDontSave;
            groupObject.SetActive(false);
            CustomerGroup group =
                groupObject.AddComponent<CustomerGroup>();

            Check(
                group.Initialize(77, 3),
                "El grupo temporal se inicializa."
            );
            Check(
                group.AssignTable(table),
                "El grupo temporal ocupa la mesa."
            );

            waiterObject = new GameObject(
                "__BB_367C_WAITER__"
            );
            waiterObject.hideFlags = HideFlags.HideAndDontSave;
            waiterObject.SetActive(false);
            Waiter waiter = waiterObject.AddComponent<Waiter>();
            SetInteger(waiter, "waiterId", 9);
            table.SetState(TableState.WaitingForWaiter);

            Check(
                waiter.AssignTable(table),
                "El camarero temporal recibe la mesa."
            );

            RunIntegratedLifecycleTests(
                canonical,
                integration,
                table,
                group,
                waiter
            );

            RunOrderSystemTests(
                canonical,
                integration,
                orderSystem,
                table,
                waiter
            );
        }
        catch (Exception exception)
        {
            Check(
                false,
                "Excepción no controlada: " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
            DestroyImmediateSafe(temporaryRoot);
        }

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();

        report.AppendLine("BISTRO BUILDER - AUTOTEST 367C");
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

    private static void RunIdentityTests()
    {
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildLegacyOrderReference(12) ==
            "legacy_order_000012",
            "La referencia legacy es determinista."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildTableReference(42) ==
            "table_000042",
            "La referencia de mesa es determinista."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildGroupReference(77) ==
            "group_000077",
            "La referencia de grupo es determinista."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildCustomerReference(77, 2) ==
            "customer_g000077_p002",
            "La referencia de cliente es determinista."
        );
        Check(
            BistroBuilderServiceOrderIdentityUtility
                .BuildWaiterReference(9) ==
            "waiter_000009",
            "La referencia de camarero es determinista."
        );

        List<string> customers = new List<string>();

        Check(
            BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    77,
                    3,
                    customers,
                    out _
                ),
            "Se generan identidades de clientes válidas."
        );
        Check(
            customers.Count == 3,
            "Se genera una identidad por miembro del grupo."
        );
        Check(
            customers[0] != customers[1] &&
            customers[1] != customers[2],
            "Las identidades de clientes son únicas."
        );
        Check(
            BistroBuilderOrderIdUtility.IsValid(customers[2]),
            "La identidad generada cumple el contrato canónico."
        );
        Check(
            !BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    0,
                    3,
                    customers,
                    out _
                ),
            "Un GroupId inválido se rechaza."
        );
        Check(
            !BistroBuilderServiceOrderIdentityUtility
                .TryBuildCustomerReferences(
                    1,
                    0,
                    customers,
                    out _
                ),
            "Un grupo vacío se rechaza."
        );
    }

    private static void RunStateMapTests()
    {
        CheckMap(
            OrderState.Created,
            BistroBuilderCanonicalOrderLineState.Draft,
            false
        );
        CheckMap(
            OrderState.SentToKitchen,
            BistroBuilderCanonicalOrderLineState.Queued,
            false
        );
        CheckMap(
            OrderState.Preparing,
            BistroBuilderCanonicalOrderLineState.Preparing,
            false
        );
        CheckMap(
            OrderState.ReadyForPickup,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            false
        );
        CheckMap(
            OrderState.Served,
            BistroBuilderCanonicalOrderLineState.Served,
            false
        );
        CheckMap(
            OrderState.Completed,
            BistroBuilderCanonicalOrderLineState.Consumed,
            false
        );
        CheckMap(
            OrderState.Cancelled,
            BistroBuilderCanonicalOrderLineState.Cancelled,
            true
        );

        Check(
            BistroBuilderLegacyCanonicalOrderStateMap
                .IsAggregateCompatible(
                    OrderState.ReadyForPickup,
                    BistroBuilderCanonicalOrderState.ReadyForPickup
                ),
            "El estado agregado listo es compatible."
        );
        Check(
            !BistroBuilderLegacyCanonicalOrderStateMap
                .IsAggregateCompatible(
                    OrderState.ReadyForPickup,
                    BistroBuilderCanonicalOrderState.InProgress
                ),
            "Un agregado incompatible se rechaza."
        );
    }

    private static void RunTransitionGateTests()
    {
        GameObject tableObject = new GameObject(
            "__BB_367C_GATE_TABLE__"
        );
        GameObject groupObject = new GameObject(
            "__BB_367C_GATE_GROUP__"
        );
        GameObject waiterObject = new GameObject(
            "__BB_367C_GATE_WAITER__"
        );

        tableObject.hideFlags = HideFlags.HideAndDontSave;
        groupObject.hideFlags = HideFlags.HideAndDontSave;
        waiterObject.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();
            CustomerGroup group =
                groupObject.AddComponent<CustomerGroup>();
            Waiter waiter =
                waiterObject.AddComponent<Waiter>();

            FakeTransitionGate rejectingGate =
                new FakeTransitionGate(false);

            RestaurantOrder rejected = new RestaurantOrder(
                1,
                table,
                group,
                waiter,
                "order_test_gate_001",
                rejectingGate
            );

            Check(
                rejected.HasCanonicalOrder,
                "RestaurantOrder conserva CanonicalOrderId."
            );
            Check(
                !rejected.TrySetState(OrderState.SentToKitchen),
                "La puerta puede rechazar una transición."
            );
            Check(
                rejected.CurrentState == OrderState.Created,
                "Un rechazo no cambia el estado legacy."
            );
            Check(
                !string.IsNullOrEmpty(
                    rejected.LastTransitionError
                ),
                "El rechazo conserva un diagnóstico."
            );

            FakeTransitionGate acceptingGate =
                new FakeTransitionGate(true);

            RestaurantOrder accepted = new RestaurantOrder(
                2,
                table,
                group,
                waiter,
                "order_test_gate_002",
                acceptingGate
            );

            Check(
                accepted.TrySetState(OrderState.SentToKitchen),
                "La puerta puede aprobar una transición."
            );
            Check(
                accepted.CurrentState == OrderState.SentToKitchen,
                "La aprobación cambia el estado legacy."
            );
            Check(
                acceptingGate.CallCount == 1,
                "La puerta se consulta una sola vez."
            );
            Check(
                !accepted.TrySetState(OrderState.ReadyForPickup),
                "La máquina legacy impide saltos antes de la puerta."
            );
            Check(
                acceptingGate.CallCount == 1,
                "Un salto legacy inválido no alcanza la puerta."
            );

            RestaurantOrder legacyOnly = new RestaurantOrder(
                3,
                table,
                group,
                waiter
            );

            Check(
                !legacyOnly.HasCanonicalOrder,
                "El constructor anterior permanece compatible."
            );
        }
        finally
        {
            DestroyImmediateSafe(waiterObject);
            DestroyImmediateSafe(groupObject);
            DestroyImmediateSafe(tableObject);
        }
    }

    private static void RunIntegratedLifecycleTests(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderCanonicalOrderIntegrationService integration,
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
                10,
                out string canonicalOrderId,
                out _
            ),
            "La integración crea una comanda canónica."
        );

        RestaurantOrder order = new RestaurantOrder(
            10,
            table,
            group,
            waiter,
            canonicalOrderId,
            integration
        );

        Check(
            integration.TryRegisterLegacyOrder(order, out _),
            "El enlace legacy-canónico se registra."
        );
        Check(
            integration.ActiveLinkCount == 1,
            "El enlace activo queda indexado."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.Draft,
            BistroBuilderCanonicalOrderLineState.Draft,
            3,
            "La comanda enlazada comienza en Draft."
        );

        Check(
            order.TrySetState(OrderState.SentToKitchen),
            "La comanda legacy se envía a cocina."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.InProgress,
            BistroBuilderCanonicalOrderLineState.Queued,
            3,
            "SentToKitchen deja todas las líneas en Queued."
        );

        Check(
            order.TrySetState(OrderState.Preparing),
            "La preparación legacy se sincroniza."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.InProgress,
            BistroBuilderCanonicalOrderLineState.Preparing,
            3,
            "Preparing deja todas las líneas en preparación."
        );

        BistroBuilderCanonicalOrderOperationResult backwards =
            canonical.TryAdvanceAllLinesToState(
                canonicalOrderId,
                BistroBuilderCanonicalOrderLineState.Queued,
                "self_test"
            );

        Check(
            !backwards.Succeeded,
            "El avance atómico no permite retroceder."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.InProgress,
            BistroBuilderCanonicalOrderLineState.Preparing,
            3,
            "Un retroceso fallido no altera ninguna línea."
        );

        Check(
            order.TrySetState(OrderState.ReadyForPickup),
            "La disponibilidad en pase se sincroniza."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.ReadyForPickup,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            3,
            "ReadyForPickup se refleja en todas las líneas."
        );

        Check(
            order.TrySetState(OrderState.Served),
            "El servicio legacy se sincroniza."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.Served,
            BistroBuilderCanonicalOrderLineState.Served,
            3,
            "Served se refleja en todas las líneas."
        );

        Check(
            order.TrySetState(OrderState.Completed),
            "La finalización legacy se sincroniza."
        );
        CheckSnapshot(
            canonical,
            canonicalOrderId,
            BistroBuilderCanonicalOrderState.Completed,
            BistroBuilderCanonicalOrderLineState.Consumed,
            3,
            "Completed consume todas las líneas."
        );
        Check(
            !order.TrySetState(OrderState.Cancelled),
            "Una comanda completada permanece terminal."
        );

        integration.NotifyLegacyOrderRemoved(order);

        Check(
            integration.ActiveLinkCount == 0,
            "El enlace se retira al terminar la fachada legacy."
        );

        Check(
            integration.TryCreateCanonicalOrder(
                table,
                group,
                waiter,
                11,
                out string cancellableId,
                out _
            ),
            "Se crea una segunda comanda para cancelar."
        );

        RestaurantOrder cancellable = new RestaurantOrder(
            11,
            table,
            group,
            waiter,
            cancellableId,
            integration
        );

        Check(
            integration.TryRegisterLegacyOrder(cancellable, out _),
            "La segunda comanda se enlaza."
        );
        Check(
            cancellable.TrySetState(OrderState.Cancelled),
            "La cancelación legacy se sincroniza."
        );
        CheckSnapshot(
            canonical,
            cancellableId,
            BistroBuilderCanonicalOrderState.Cancelled,
            BistroBuilderCanonicalOrderLineState.Cancelled,
            3,
            "Cancelar la fachada cancela todas las líneas."
        );

        integration.NotifyLegacyOrderRemoved(cancellable);

        Check(
            integration.TryCreateCanonicalOrder(
                table,
                group,
                waiter,
                12,
                out string rollbackId,
                out _
            ),
            "Se crea una comanda provisional para rollback."
        );
        Check(
            integration.TryRollbackUnregisteredCanonicalOrder(
                rollbackId,
                out _
            ),
            "Una creación no registrada puede revertirse."
        );
        Check(
            !canonical.TryGetOrderSnapshot(rollbackId, out _),
            "El rollback no deja un agregado huérfano."
        );
    }

    private static void RunOrderSystemTests(
        BistroBuilderCanonicalOrderService canonical,
        BistroBuilderCanonicalOrderIntegrationService integration,
        OrderSystem orderSystem,
        RestaurantTable table,
        Waiter waiter
    )
    {
        int previousCanonicalCount = canonical.OrderCount;

        RestaurantOrder created =
            orderSystem.CreateOrder(table, waiter);

        Check(
            created != null,
            "OrderSystem crea una comanda integrada."
        );
        Check(
            created != null && created.HasCanonicalOrder,
            "La comanda de OrderSystem queda enlazada."
        );
        Check(
            canonical.OrderCount == previousCanonicalCount + 1,
            "La creación integrada añade un solo agregado canónico."
        );
        Check(
            orderSystem.ActiveOrders.Count == 1,
            "OrderSystem conserva una sola fachada activa."
        );

        if (created != null)
        {
            Check(
                orderSystem.CancelOrder(created),
                "OrderSystem cancela de forma coordinada."
            );
            Check(
                orderSystem.ActiveOrders.Count == 0,
                "La fachada cancelada se retira."
            );
            Check(
                integration.ActiveLinkCount == 0,
                "La cancelación retira el enlace activo."
            );
        }
    }

    private static void CheckMap(
        OrderState legacy,
        BistroBuilderCanonicalOrderLineState expected,
        bool expectedCancellation
    )
    {
        bool mapped =
            BistroBuilderLegacyCanonicalOrderStateMap.TryGetLineTarget(
                legacy,
                out BistroBuilderCanonicalOrderLineState target,
                out bool cancellation
            );

        Check(
            mapped &&
            target == expected &&
            cancellation == expectedCancellation,
            "Mapa válido para " + legacy + "."
        );
    }

    private static void CheckSnapshot(
        BistroBuilderCanonicalOrderService canonical,
        string orderId,
        BistroBuilderCanonicalOrderState expectedOrderState,
        BistroBuilderCanonicalOrderLineState expectedLineState,
        int expectedLineCount,
        string message
    )
    {
        bool valid =
            canonical.TryGetOrderSnapshot(
                orderId,
                out BistroBuilderCanonicalOrder snapshot
            ) &&
            snapshot != null &&
            snapshot.State == expectedOrderState &&
            snapshot.Lines.Count == expectedLineCount;

        if (valid)
        {
            for (int index = 0;
                 index < snapshot.Lines.Count;
                 index++)
            {
                if (snapshot.Lines[index].State != expectedLineState)
                {
                    valid = false;
                    break;
                }
            }
        }

        Check(valid, message);
    }

    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + "."
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInteger(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + "."
            );
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumValue(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + "."
            );
        }

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + "."
            );
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DestroyImmediateSafe(
        UnityEngine.Object target
    )
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
            messages.Add("- FALLO: " + message);
        }
    }

    private sealed class FakeTransitionGate :
        IRestaurantOrderTransitionGate
    {
        private readonly bool approve;

        public int CallCount { get; private set; }

        public FakeTransitionGate(bool approve)
        {
            this.approve = approve;
        }

        public bool TryApproveTransition(
            RestaurantOrder order,
            OrderState currentState,
            OrderState targetState,
            out string error
        )
        {
            CallCount++;

            if (approve)
            {
                error = string.Empty;
                return true;
            }

            error = "Rechazo controlado del autotest.";
            return false;
        }
    }
}
