using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana temporal de validación de 367B.
/// No forma parte de la UI final del juego.
/// </summary>
public sealed class BistroBuilderCanonicalOrderDebugWindow : EditorWindow
{
    private readonly List<BistroBuilderCanonicalOrder> orders =
        new List<BistroBuilderCanonicalOrder>();

    private BistroBuilderCanonicalOrderService service;
    private Vector2 scroll;
    private int customerCount = 3;
    private int courseIndex = 1;
    private BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    private int selectedOrderIndex;
    private int selectedLineIndex;
    private string lastMessage = string.Empty;

    [MenuItem(
        "Tools/Bistro Builder/Orders/Canonical Orders Debug",
        false,
        203
    )]
    private static void Open()
    {
        GetWindow<BistroBuilderCanonicalOrderDebugWindow>(
            "BB Canonical Orders"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367B — Comandas canónicas",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Ventana temporal. Crea comandas sintéticas por cliente sin " +
            "reemplazar aún el flujo legacy del servicio.",
            MessageType.Info
        );

        ResolveService();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            customerCount = EditorGUILayout.IntSlider(
                "Clientes",
                customerCount,
                1,
                10
            );
            courseIndex = EditorGUILayout.IntSlider(
                "Pase",
                courseIndex,
                0,
                5
            );
            mealService =
                (BistroBuilderMealServiceAvailability)
                EditorGUILayout.EnumPopup("Servicio", mealService);

            if (GUILayout.Button("Crear comanda individual de prueba"))
            {
                CreateTestOrder();
            }

            if (GUILayout.Button("Actualizar fotografía"))
            {
                RefreshOrders();
            }
        }

        if (!string.IsNullOrEmpty(lastMessage))
        {
            EditorGUILayout.HelpBox(lastMessage, MessageType.None);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para utilizar la herramienta.",
                MessageType.Warning
            );
            return;
        }

        RefreshOrders();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Comandas runtime: " + orders.Count,
            EditorStyles.boldLabel
        );

        if (orders.Count == 0)
        {
            return;
        }

        string[] orderLabels = new string[orders.Count];

        for (int index = 0; index < orders.Count; index++)
        {
            orderLabels[index] =
                orders[index].SequenceNumber + " — " +
                orders[index].OrderId + " — " +
                orders[index].State;
        }

        selectedOrderIndex = Mathf.Clamp(
            selectedOrderIndex,
            0,
            orders.Count - 1
        );
        selectedOrderIndex = EditorGUILayout.Popup(
            "Comanda",
            selectedOrderIndex,
            orderLabels
        );

        BistroBuilderCanonicalOrder order = orders[selectedOrderIndex];

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("OrderId", order.OrderId);
        EditorGUILayout.LabelField(
            "Mesa",
            order.TableReferenceId
        );
        EditorGUILayout.LabelField(
            "Grupo",
            order.CustomerGroupReferenceId
        );
        EditorGUILayout.LabelField("Estado", order.State.ToString());
        EditorGUILayout.LabelField(
            "Total",
            (order.CalculateTotalPriceCents() / 100f).ToString("0.00") +
            " €"
        );
        EditorGUILayout.LabelField("Líneas", order.Lines.Count.ToString());

        string[] lineLabels = new string[order.Lines.Count];

        for (int index = 0; index < order.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[index];
            lineLabels[index] =
                index + " — " + line.DishId + " — " + line.State;
        }

        selectedLineIndex = Mathf.Clamp(
            selectedLineIndex,
            0,
            order.Lines.Count - 1
        );
        selectedLineIndex = EditorGUILayout.Popup(
            "Línea",
            selectedLineIndex,
            lineLabels
        );

        BistroBuilderCanonicalOrderLine selectedLine =
            order.Lines[selectedLineIndex];

        EditorGUILayout.LabelField("LineId", selectedLine.LineId);
        EditorGUILayout.LabelField("Plato", selectedLine.DishId);
        EditorGUILayout.LabelField(
            "Cliente principal",
            selectedLine.PrimaryCustomerId
        );
        EditorGUILayout.LabelField(
            "Consumidores",
            selectedLine.ConsumerCustomerIds.Count.ToString()
        );
        EditorGUILayout.LabelField(
            "Precio congelado",
            (selectedLine.PriceCentsAtOrder / 100f).ToString("0.00") +
            " €"
        );
        EditorGUILayout.LabelField("Pase", selectedLine.CourseIndex.ToString());
        EditorGUILayout.LabelField("Estado", selectedLine.State.ToString());
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Avanzar línea"))
            {
                AdvanceSelectedLine(selectedLine);
            }

            if (GUILayout.Button("Cancelar comanda"))
            {
                ApplyResult(
                    service.TryCancelOrder(
                        order.OrderId,
                        "debug_operator"
                    )
                );
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Eliminar terminal seleccionada"))
            {
                ApplyResult(
                    service.TryRemoveTerminalOrder(order.OrderId)
                );
            }

            if (GUILayout.Button("Vaciar runtime"))
            {
                service.ClearAllOrders(true);
                lastMessage = "Runtime de comandas vaciado.";
            }
        }
    }

    private void ResolveService()
    {
        if (service == null && Application.isPlaying)
        {
            service = Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderService
            >();
        }
    }

    private void CreateTestOrder()
    {
        ResolveService();

        if (service == null)
        {
            lastMessage = "No se encontró CanonicalOrderService.";
            return;
        }

        List<string> customers = new List<string>(customerCount);

        for (int index = 0; index < customerCount; index++)
        {
            customers.Add(
                "customer_debug_" + (index + 1).ToString("000")
            );
        }

        BistroBuilderCanonicalOrderOperationResult result =
            service.TryCreateIndividualOrder(
                "table_debug_001",
                "group_debug_001",
                customers,
                mealService,
                courseIndex,
                out _
            );

        ApplyResult(result);
    }

    private void AdvanceSelectedLine(
        BistroBuilderCanonicalOrderLine line
    )
    {
        if (service == null || line == null)
        {
            return;
        }

        if (!BistroBuilderCanonicalOrderTransitionPolicy
                .TryGetNormalNextState(
                    line.State,
                    out BistroBuilderCanonicalOrderLineState next
                ))
        {
            lastMessage = "La línea ya no tiene un siguiente estado normal.";
            return;
        }

        ApplyResult(
            service.TryTransitionLine(
                line.LineId,
                next,
                "debug_operator"
            )
        );
    }

    private void ApplyResult(
        BistroBuilderCanonicalOrderOperationResult result
    )
    {
        lastMessage = result.Message;
        RefreshOrders();
        Repaint();
    }

    private void RefreshOrders()
    {
        ResolveService();

        if (service != null)
        {
            service.CopyOrderSnapshotsTo(orders);
        }
        else
        {
            orders.Clear();
        }
    }
}
