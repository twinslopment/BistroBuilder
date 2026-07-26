using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Diagnóstico de solo lectura del flujo por plato físico.
/// </summary>
public sealed class BistroBuilderIndividualDishFlowDebugWindow : EditorWindow
{
    private OrderSystem orderSystem;
    private BistroBuilderCanonicalOrderService canonical;
    private WaiterTaskCoordinator coordinator;
    private KitchenSystem[] kitchens;
    private Vector2 scroll;

    [MenuItem(
        "Tools/Bistro Builder/Orders/Individual Dish Flow Debug",
        false,
        223
    )]
    private static void Open()
    {
        GetWindow<BistroBuilderIndividualDishFlowDebugWindow>(
            "BB Individual Dishes"
        );
    }

    private void OnEnable() => RefreshReferences();

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367D — Platos individuales",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Ventana de solo lectura. Muestra cocina, tareas y líneas " +
            "canónicas durante el servicio real.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para observar el flujo.",
                MessageType.Warning
            );

            if (GUILayout.Button("Actualizar referencias"))
                RefreshReferences();

            return;
        }

        if (orderSystem == null || canonical == null || coordinator == null)
            RefreshReferences();

        if (orderSystem == null || canonical == null || coordinator == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontraron todos los servicios 367D.",
                MessageType.Error
            );
            return;
        }

        if (GUILayout.Button("Actualizar fotografía"))
        {
            RefreshReferences();
            Repaint();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Cocinas", EditorStyles.boldLabel);
        for (int index = 0; index < kitchens.Length; index++)
        {
            KitchenSystem kitchen = kitchens[index];
            if (kitchen == null) continue;

            EditorGUILayout.LabelField(
                kitchen.KitchenId + " — " + kitchen.CurrentState
            );
            EditorGUILayout.LabelField(
                "  Línea activa",
                string.IsNullOrEmpty(kitchen.ActiveOrderLineId)
                    ? "-"
                    : kitchen.ActiveOrderLineId
            );
            EditorGUILayout.LabelField(
                "  Tiempo restante",
                kitchen.ActiveRemainingPreparationSeconds.ToString("0.00") +
                " s"
            );
            EditorGUILayout.LabelField(
                "  Líneas pendientes",
                kitchen.PendingLineCount.ToString()
            );
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Tareas de reparto activas",
            EditorStyles.boldLabel
        );

        IReadOnlyList<WaiterTask> tasks = coordinator.ActiveTasks;
        int foodTaskCount = 0;

        for (int index = 0; index < tasks.Count; index++)
        {
            WaiterTask task = tasks[index];
            if (task == null || task.Type != WaiterTaskType.DeliverFood)
                continue;

            foodTaskCount++;
            EditorGUILayout.LabelField(
                "Tarea " + task.TaskId + " — " + task.State
            );
            EditorGUILayout.LabelField(
                "  LineId",
                task.OrderLineId
            );
            EditorGUILayout.LabelField(
                "  Comanda legacy",
                task.Order != null ? task.Order.OrderId.ToString() : "-"
            );
            EditorGUILayout.LabelField(
                "  Camarero",
                task.AssignedWaiter != null
                    ? task.AssignedWaiter.WaiterId.ToString()
                    : "-"
            );
        }

        if (foodTaskCount == 0)
            EditorGUILayout.LabelField("No hay repartos activos.");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Comandas activas y líneas",
            EditorStyles.boldLabel
        );

        IReadOnlyList<RestaurantOrder> activeOrders = orderSystem.ActiveOrders;

        for (int index = 0; index < activeOrders.Count; index++)
        {
            RestaurantOrder legacy = activeOrders[index];
            if (legacy == null) continue;

            EditorGUILayout.LabelField(
                "Legacy " + legacy.OrderId + " — " + legacy.CurrentState,
                EditorStyles.boldLabel
            );

            if (!canonical.TryGetOrderSnapshot(
                    legacy.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder snapshot
                ))
            {
                EditorGUILayout.HelpBox(
                    "No se encontró la comanda canónica.",
                    MessageType.Error
                );
                continue;
            }

            for (int lineIndex = 0;
                 lineIndex < snapshot.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line =
                    snapshot.Lines[lineIndex];
                EditorGUILayout.LabelField(
                    "  " + (lineIndex + 1) + ". " +
                    line.PrimaryCustomerId + " → " + line.DishId +
                    " — " + line.State
                );
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshReferences()
    {
        orderSystem = Object.FindFirstObjectByType<OrderSystem>();
        canonical = Object.FindFirstObjectByType<
            BistroBuilderCanonicalOrderService
        >();
        coordinator = Object.FindFirstObjectByType<WaiterTaskCoordinator>();
        kitchens = Object.FindObjectsByType<KitchenSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
    }
}
