using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana de diagnóstico de solo lectura para BistroBuilder 367E.
///
/// No altera estados ni fuerza transiciones. Permite comprobar durante un
/// servicio real qué CustomerId espera, come o ha terminado, junto con las
/// líneas canónicas y la disponibilidad de la cuenta.
/// </summary>
public sealed class BistroBuilderIndividualCustomerDiningDebugWindow
    : EditorWindow
{
    private readonly List<BistroBuilderCustomerDiningOrderRuntime>
        runtimeSnapshots =
            new List<BistroBuilderCustomerDiningOrderRuntime>(16);

    private readonly StringBuilder textBuffer = new StringBuilder(256);

    private BistroBuilderCustomerDiningService diningService;
    private BistroBuilderCanonicalOrderService canonicalOrderService;
    private OrderSystem orderSystem;
    private Vector2 scroll;
    private bool autoRefresh = true;
    private double nextRepaintTime;

    [MenuItem(
        "Tools/Bistro Builder/Orders/Individual Customer Dining Debug",
        false,
        233
    )]
    private static void Open()
    {
        GetWindow<BistroBuilderIndividualCustomerDiningDebugWindow>(
            "BB Customer Dining"
        );
    }

    private void OnEnable()
    {
        RefreshReferences();
        nextRepaintTime = EditorApplication.timeSinceStartup;
    }

    private void Update()
    {
        if (!autoRefresh || !Application.isPlaying)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;

        if (now < nextRepaintTime)
        {
            return;
        }

        nextRepaintTime = now + 0.25d;
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367E — Consumo individual",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Diagnóstico de solo lectura. CustomerId y OrderLineId son la " +
            "autoridad; los estados de grupo y mesa son solo una fachada " +
            "operativa compatible.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();
        autoRefresh = EditorGUILayout.ToggleLeft(
            "Actualización automática",
            autoRefresh,
            GUILayout.Width(170f)
        );

        if (GUILayout.Button("Actualizar fotografía"))
        {
            RefreshReferences();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode y abre el servicio para observar 367E.",
                MessageType.Warning
            );
            return;
        }

        EnsureReferences();

        if (diningService == null ||
            canonicalOrderService == null ||
            orderSystem == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontraron todas las autoridades de 367E.",
                MessageType.Error
            );
            return;
        }

        int runtimeCount = diningService.CopyOrderRuntimeSnapshotsTo(
            runtimeSnapshots
        );

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Revisión de consumo",
            diningService.Revision.ToString()
        );
        EditorGUILayout.LabelField(
            "Sesiones activas",
            runtimeCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Duración provisional por pase",
            diningService.DefaultEatingDurationSeconds.ToString("0.00") +
            " s"
        );

        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (runtimeCount == 0)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "No hay sesiones de consumo activas."
            );
        }

        for (int orderIndex = 0;
             orderIndex < runtimeSnapshots.Count;
             orderIndex++)
        {
            DrawRuntime(runtimeSnapshots[orderIndex]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRuntime(
        BistroBuilderCustomerDiningOrderRuntime runtime
    )
    {
        if (runtime == null)
        {
            EditorGUILayout.HelpBox(
                "La fotografía contiene un runtime nulo.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Comanda legacy " + runtime.LegacyOrderId +
            " — " + runtime.OrderId,
            EditorStyles.boldLabel
        );

        RestaurantOrder legacy = FindLegacyOrder(runtime.OrderId);

        EditorGUILayout.LabelField(
            "Grupo / mesa",
            runtime.CustomerGroupReferenceId + " / " +
            runtime.TableReferenceId
        );
        EditorGUILayout.LabelField(
            "Fachada legacy",
            legacy != null ? legacy.CurrentState.ToString() : "No enlazada"
        );
        EditorGUILayout.LabelField(
            "Estado de grupo",
            legacy != null && legacy.CustomerGroup != null
                ? legacy.CustomerGroup.CurrentState.ToString()
                : "-"
        );
        EditorGUILayout.LabelField(
            "Estado de mesa",
            legacy != null && legacy.Table != null
                ? legacy.Table.CurrentState.ToString()
                : "-"
        );
        EditorGUILayout.LabelField(
            "Todos los clientes terminados",
            runtime.AllCustomersCompleted ? "Sí" : "No"
        );
        EditorGUILayout.LabelField(
            "Cuenta solicitada",
            runtime.BillRequested ? "Sí" : "No"
        );

        if (legacy != null)
        {
            bool billReady = diningService.TryValidateBillReady(
                legacy,
                out string billError
            );

            EditorGUILayout.LabelField(
                "Guardia de cuenta",
                billReady ? "Autorizada" : "Bloqueada"
            );

            if (!billReady && !string.IsNullOrWhiteSpace(billError))
            {
                EditorGUILayout.LabelField("  Motivo", billError);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Clientes", EditorStyles.boldLabel);

        for (int customerIndex = 0;
             customerIndex < runtime.Customers.Count;
             customerIndex++)
        {
            BistroBuilderCustomerDiningCustomerRuntime customer =
                runtime.Customers[customerIndex];

            if (customer == null)
            {
                EditorGUILayout.HelpBox(
                    "Cliente runtime nulo.",
                    MessageType.Error
                );
                continue;
            }

            EditorGUILayout.LabelField(
                "  " + (customerIndex + 1) + ". " +
                customer.CustomerId + " — " + customer.State
            );
            EditorGUILayout.LabelField(
                "     Pase",
                customer.CurrentCourseIndex.ToString()
            );
            EditorGUILayout.LabelField(
                "     Tiempo restante",
                customer.RemainingEatingSeconds.ToString("0.00") + " s"
            );
            EditorGUILayout.LabelField(
                "     Reclamaciones consumidas",
                BuildIdList(customer.ConsumedLineIds)
            );
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Líneas canónicas",
            EditorStyles.boldLabel
        );

        if (!canonicalOrderService.TryGetOrderSnapshot(
                runtime.OrderId,
                out BistroBuilderCanonicalOrder canonical
            ) ||
            canonical == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontró la comanda canónica.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Agregado canónico",
            canonical.State.ToString()
        );

        for (int lineIndex = 0;
             lineIndex < canonical.Lines.Count;
             lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line =
                canonical.Lines[lineIndex];

            if (line == null)
            {
                EditorGUILayout.HelpBox(
                    "Línea canónica nula.",
                    MessageType.Error
                );
                continue;
            }

            EditorGUILayout.LabelField(
                "  " + (lineIndex + 1) + ". " + line.LineId +
                " — " + line.State
            );
            EditorGUILayout.LabelField("     Plato", line.DishId);
            EditorGUILayout.LabelField(
                "     Pase",
                line.CourseIndex.ToString()
            );
            EditorGUILayout.LabelField(
                "     Cliente principal",
                line.PrimaryCustomerId
            );
            EditorGUILayout.LabelField(
                "     Consumidores",
                BuildIdList(line.ConsumerCustomerIds)
            );
        }
    }

    private RestaurantOrder FindLegacyOrder(string canonicalOrderId)
    {
        if (orderSystem == null)
        {
            return null;
        }

        IReadOnlyList<RestaurantOrder> activeOrders =
            orderSystem.ActiveOrders;

        for (int index = 0; index < activeOrders.Count; index++)
        {
            RestaurantOrder order = activeOrders[index];

            if (order != null &&
                string.Equals(
                    order.CanonicalOrderId,
                    canonicalOrderId,
                    System.StringComparison.Ordinal
                ))
            {
                return order;
            }
        }

        return null;
    }

    private string BuildIdList(IReadOnlyList<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return "-";
        }

        textBuffer.Clear();

        for (int index = 0; index < ids.Count; index++)
        {
            if (index > 0)
            {
                textBuffer.Append(", ");
            }

            textBuffer.Append(ids[index]);
        }

        return textBuffer.ToString();
    }

    private void EnsureReferences()
    {
        if (diningService == null ||
            canonicalOrderService == null ||
            orderSystem == null)
        {
            RefreshReferences();
        }
    }

    private void RefreshReferences()
    {
        diningService = Object.FindFirstObjectByType<
            BistroBuilderCustomerDiningService
        >();
        canonicalOrderService = Object.FindFirstObjectByType<
            BistroBuilderCanonicalOrderService
        >();
        orderSystem = Object.FindFirstObjectByType<OrderSystem>();
    }
}
