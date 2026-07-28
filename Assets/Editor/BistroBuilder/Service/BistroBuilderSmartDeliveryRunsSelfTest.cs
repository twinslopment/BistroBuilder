using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista del modelo de rondas inteligentes 367G1.
/// No modifica escenas ni assets del proyecto.
/// </summary>
public static class BistroBuilderSmartDeliveryRunsSelfTest
{
    private static int passedCount;
    private static int failedCount;
    private static readonly List<string> messages = new List<string>();

    [MenuItem(
        "Tools/Bistro Builder/Service/" +
        "Run 367G1 Smart Delivery Runs Self-Test",
        false,
        262
    )]
    private static void RunFromMenu()
    {
        passedCount = 0;
        failedCount = 0;
        messages.Clear();

        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            ExecuteModelTests(temporaryObjects);
        }
        catch (Exception exception)
        {
            failedCount++;
            messages.Add("- FALLO INESPERADO: " + exception);
        }
        finally
        {
            for (int index = temporaryObjects.Count - 1;
                 index >= 0;
                 index--)
            {
                if (temporaryObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(temporaryObjects[index]);
            }
        }

        string report =
            "BISTRO BUILDER - AUTOTEST 367G1\n" +
            "Pruebas superadas: " + passedCount + "\n" +
            "Pruebas fallidas: " + failedCount + "\n" +
            string.Join("\n", messages);

        if (failedCount == 0)
            Debug.Log(report);
        else
            Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            report,
            "Aceptar"
        );
    }

    private static void ExecuteModelTests(List<GameObject> temporaryObjects)
    {
        Check(
            IsCompatibleRuntimeRevision(
                WaiterTaskCoordinator.RuntimeRevision
            ),
            "El coordinador declara 367G1 o una revisión acumulativa posterior."
        );
        Check(
            Mathf.Approximately(
                WaiterTaskCoordinator
                    .CalculateConsolidationRemainingSeconds(10f, 10f, 0.8f),
                0.8f
            ),
            "La consolidación conserva toda la ventana al comenzar."
        );
        Check(
            Mathf.Approximately(
                WaiterTaskCoordinator
                    .CalculateConsolidationRemainingSeconds(10f, 10.5f, 0.8f),
                0.3f
            ),
            "La consolidación descuenta tiempo real transcurrido."
        );
        Check(
            Mathf.Approximately(
                WaiterTaskCoordinator
                    .CalculateConsolidationRemainingSeconds(10f, 11f, 0.8f),
                0f
            ),
            "La línea madura al agotar la ventana breve."
        );
        Check(
            Mathf.Approximately(
                WaiterTaskCoordinator
                    .CalculateConsolidationRemainingSeconds(10f, 10f, -4f),
                0f
            ),
            "Una ventana negativa se normaliza sin bloquear repartos."
        );
        Check(
            Mathf.Approximately(
                WaiterTaskCoordinator
                    .CalculateConsolidationRemainingSeconds(
                        float.NaN,
                        10f,
                        0.8f
                    ),
                0f
            ),
            "Los valores temporales inválidos fallan de forma segura."
        );
        GameObject waiterObject = CreateHiddenObject(
            "__BB_367G_WAITER__",
            temporaryObjects
        );
        Waiter waiter = waiterObject.AddComponent<Waiter>();

        GameObject kitchenObject = CreateHiddenObject(
            "__BB_367G_KITCHEN__",
            temporaryObjects
        );
        KitchenSystem kitchen = kitchenObject.AddComponent<KitchenSystem>();

        RestaurantTable tableA = CreateTable(
            "__BB_367G_TABLE_A__",
            101,
            temporaryObjects
        );
        RestaurantTable tableB = CreateTable(
            "__BB_367G_TABLE_B__",
            102,
            temporaryObjects
        );

        CustomerGroup groupA = CreateGroup(
            "__BB_367G_GROUP_A__",
            201,
            temporaryObjects
        );
        CustomerGroup groupB = CreateGroup(
            "__BB_367G_GROUP_B__",
            202,
            temporaryObjects
        );

        RestaurantOrder orderA = new RestaurantOrder(
            301,
            tableA,
            groupA,
            waiter
        );
        RestaurantOrder orderB = new RestaurantOrder(
            302,
            tableB,
            groupB,
            waiter
        );

        WaiterTask taskA1 = CreateDeliveryTask(
            1,
            orderA,
            "line_367g_a1",
            0
        );
        WaiterTask taskA2 = CreateDeliveryTask(
            2,
            orderA,
            "line_367g_a2",
            1
        );
        WaiterTask taskB1 = CreateDeliveryTask(
            3,
            orderB,
            "line_367g_b1",
            2
        );

        List<WaiterTask> orderedTasks = new List<WaiterTask>
        {
            taskA1,
            taskA2,
            taskB1
        };

        BistroBuilderDeliveryRun run = new BistroBuilderDeliveryRun(
            1,
            kitchen,
            3,
            orderedTasks
        );

        Check(run.RunId == 1, "La ronda conserva su identidad.");
        Check(run.Capacity == 3, "La ronda conserva su capacidad.");
        Check(run.Items.Count == 3, "La ronda contiene tres platos.");
        Check(run.Stops.Count == 2, "Tres platos se agrupan en dos mesas.");
        Check(
            ReferenceEquals(run.Stops[0].Table, tableA),
            "La primera parada corresponde a la mesa ancla."
        );
        Check(
            run.Stops[0].Items.Count == 2,
            "Los dos platos de la primera mesa viajan juntos."
        );
        Check(
            ReferenceEquals(run.Stops[1].Table, tableB),
            "La segunda parada corresponde a la otra mesa."
        );
        Check(
            run.ContainsLine(orderA, "line_367g_a1"),
            "La ronda localiza su primera línea."
        );
        Check(
            run.ContainsLine(orderB, "LINE_367G_B1"),
            "La identidad de línea se normaliza."
        );
        Check(
            !run.ContainsLine(orderB, "line_inexistente"),
            "La ronda rechaza una línea ajena."
        );

        Check(
            waiter.FoodDeliveryCapacity >= 3,
            "El camarero tiene capacidad inicial para tres platos."
        );
        Check(
            waiter.AssignDeliveryRun(run),
            "El camarero acepta la ronda completa."
        );
        Check(
            waiter.CurrentState == WaiterState.WalkingToKitchen,
            "La ronda comienza con un único viaje a cocina."
        );
        Check(
            ReferenceEquals(waiter.AssignedDeliveryRun, run),
            "El camarero conserva la ronda activa."
        );
        Check(
            waiter.HasDeliveryLine(orderA, "line_367g_a2"),
            "El camarero reconoce líneas adicionales a la representativa."
        );

        Check(run.TryBeginPickup(), "La ronda entra en recogida.");
        Check(
            run.TryMarkLineInTransit(orderA, "line_367g_a1"),
            "La primera línea entra en tránsito."
        );
        Check(
            run.TryMarkLineInTransit(orderA, "line_367g_a2"),
            "La segunda línea entra en tránsito."
        );
        Check(
            run.TryMarkLineInTransit(orderB, "line_367g_b1"),
            "La tercera línea entra en tránsito."
        );
        Check(
            waiter.TryBeginDeliveryRunStops(),
            "La ronda prepara la primera parada."
        );
        Check(
            ReferenceEquals(waiter.AssignedTable, tableA),
            "La compatibilidad legacy apunta a la mesa actual."
        );

        Check(
            waiter.TrySelectDeliveryLine(orderA, "line_367g_a1"),
            "Se selecciona el primer plato de la parada."
        );
        Check(
            run.TryMarkLineServed(orderA, "line_367g_a1"),
            "El primer plato queda servido."
        );
        Check(
            waiter.TrySelectDeliveryLine(orderA, "line_367g_a2"),
            "Se selecciona el segundo plato de la misma mesa."
        );
        Check(
            run.TryMarkLineServed(orderA, "line_367g_a2"),
            "El segundo plato queda servido."
        );
        Check(
            waiter.TryAdvanceDeliveryRunStop(),
            "El camarero avanza sin volver a cocina."
        );
        Check(
            ReferenceEquals(waiter.AssignedTable, tableB),
            "La asignación cambia a la segunda mesa."
        );
        Check(
            run.TryMarkLineServed(orderB, "line_367g_b1"),
            "El plato de la segunda mesa queda servido."
        );
        Check(run.RemainingLineCount == 0, "No quedan platos pendientes.");
        Check(run.TryComplete(), "La ronda se completa de forma explícita.");
        Check(
            run.State == BistroBuilderDeliveryRunState.Completed,
            "La ronda termina en Completed."
        );

        waiter.ClearAssignment();
        Check(waiter.IsAvailable, "El camarero vuelve a estar disponible.");

        bool capacityRejected = false;

        try
        {
            _ = new BistroBuilderDeliveryRun(
                2,
                kitchen,
                2,
                orderedTasks
            );
        }
        catch (ArgumentException)
        {
            capacityRejected = true;
        }

        Check(
            capacityRejected,
            "Una ronda que supera capacidad se rechaza."
        );

        WaiterTask duplicatedLineTask = CreateDeliveryTask(
            4,
            orderB,
            "line_367g_a1",
            3
        );
        bool duplicateRejected = false;

        try
        {
            _ = new BistroBuilderDeliveryRun(
                3,
                kitchen,
                3,
                new List<WaiterTask>
                {
                    taskA1,
                    duplicatedLineTask
                }
            );
        }
        catch (ArgumentException)
        {
            duplicateRejected = true;
        }

        Check(
            duplicateRejected,
            "Una ronda con LineId duplicado se rechaza."
        );
    }

    private static WaiterTask CreateDeliveryTask(
        int taskId,
        RestaurantOrder order,
        string lineId,
        long creationSequence
    )
    {
        return new WaiterTask(
            taskId,
            WaiterTaskType.DeliverFood,
            WaiterTaskPriority.Urgent,
            order.Table,
            order,
            lineId,
            creationSequence
        );
    }

    private static RestaurantTable CreateTable(
        string name,
        int tableId,
        List<GameObject> temporaryObjects
    )
    {
        GameObject tableObject = CreateHiddenObject(name, temporaryObjects);
        RestaurantTable table = tableObject.AddComponent<RestaurantTable>();
        table.AssignTableId(tableId);
        return table;
    }

    private static CustomerGroup CreateGroup(
        string name,
        int groupId,
        List<GameObject> temporaryObjects
    )
    {
        GameObject groupObject = CreateHiddenObject(name, temporaryObjects);
        CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
        Check(group.Initialize(groupId, 2), "Grupo temporal inicializado.");
        return group;
    }

    private static GameObject CreateHiddenObject(
        string name,
        List<GameObject> temporaryObjects
    )
    {
        GameObject gameObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private static bool IsCompatibleRuntimeRevision(string revision)
    {
        return string.Equals(revision, "367G1", StringComparison.Ordinal) ||
               string.Equals(revision, "367H", StringComparison.Ordinal);
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passedCount++;
            messages.Add("- OK: " + message);
        }
        else
        {
            failedCount++;
            messages.Add("- ERROR: " + message);
        }
    }
}
