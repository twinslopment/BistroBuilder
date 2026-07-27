using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana de diagnóstico de platos compartidos y varios pases.
/// Solo consulta snapshots; no modifica la simulación.
/// </summary>
public sealed class BistroBuilderSharedCoursesDebugWindow : EditorWindow
{
    private readonly List<BistroBuilderCourseOrderRuntime> courseOrders =
        new List<BistroBuilderCourseOrderRuntime>();

    private readonly List<BistroBuilderCustomerDiningOrderRuntime>
        diningOrders = new List<BistroBuilderCustomerDiningOrderRuntime>();

    private Vector2 scroll;
    private string status = "Pulsa Actualizar fotografía.";

    [MenuItem(
        "Tools/Bistro Builder/Orders/Shared Dishes and Courses Debug",
        false,
        243
    )]
    private static void OpenWindow()
    {
        BistroBuilderSharedCoursesDebugWindow window =
            GetWindow<BistroBuilderSharedCoursesDebugWindow>();
        window.titleContent = new GUIContent("BB Shared Courses");
        window.minSize = new Vector2(620f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367F — Platos compartidos y pases",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);

        if (GUILayout.Button("Actualizar fotografía", GUILayout.Height(28f)))
        {
            RefreshSnapshot();
        }

        EditorGUILayout.HelpBox(status, MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (courseOrders.Count == 0)
        {
            EditorGUILayout.LabelField("No hay comandas 367F activas.");
        }

        for (int orderIndex = 0;
             orderIndex < courseOrders.Count;
             orderIndex++)
        {
            DrawOrder(courseOrders[orderIndex]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshSnapshot()
    {
        courseOrders.Clear();
        diningOrders.Clear();

        BistroBuilderCourseAndSharingService courses =
            Object.FindFirstObjectByType<BistroBuilderCourseAndSharingService>();
        BistroBuilderCustomerDiningService dining =
            Object.FindFirstObjectByType<BistroBuilderCustomerDiningService>();

        if (courses == null || dining == null)
        {
            status = "No se encontraron las autoridades 367F en la escena.";
            Repaint();
            return;
        }

        courses.CopyOrderRuntimeSnapshotsTo(courseOrders);
        dining.CopyOrderRuntimeSnapshotsTo(diningOrders);
        status = "Fotografía actualizada. Comandas activas: " +
                 courseOrders.Count + ".";
        Repaint();
    }

    private void DrawOrder(BistroBuilderCourseOrderRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "Comanda " + runtime.LegacyOrderId + " — " + runtime.OrderId,
            EditorStyles.boldLabel
        );
        EditorGUILayout.LabelField(
            "Política",
            runtime.CoordinationPolicy.ToString()
        );
        EditorGUILayout.LabelField(
            "Pase inicial",
            runtime.InitialCourseIndex.ToString()
        );
        EditorGUILayout.LabelField(
            "Pases liberados",
            JoinCourses(runtime.ReleasedCourseIndices)
        );
        EditorGUILayout.LabelField(
            "Líneas liberadas",
            runtime.ReleasedLineIds.Count.ToString()
        );

        BistroBuilderCanonicalOrderService canonical =
            Object.FindFirstObjectByType<BistroBuilderCanonicalOrderService>();

        if (canonical != null &&
            canonical.TryGetOrderSnapshot(
                runtime.OrderId,
                out BistroBuilderCanonicalOrder order
            ) &&
            order != null)
        {
            BistroBuilderCustomerDiningOrderRuntime dining =
                FindDiningRuntime(runtime.OrderId);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Líneas", EditorStyles.boldLabel);

            for (int lineIndex = 0;
                 lineIndex < order.Lines.Count;
                 lineIndex++)
            {
                DrawLine(order.Lines[lineIndex], dining);
            }

            if (dining != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Clientes", EditorStyles.boldLabel);

                for (int customerIndex = 0;
                     customerIndex < dining.Customers.Count;
                     customerIndex++)
                {
                    BistroBuilderCustomerDiningCustomerRuntime customer =
                        dining.Customers[customerIndex];

                    EditorGUILayout.LabelField(
                        customer.CustomerId,
                        customer.State + " · pase " +
                        customer.CurrentCourseIndex + " · " +
                        customer.RemainingEatingSeconds.ToString("0.00") + " s"
                    );
                }

                EditorGUILayout.LabelField(
                    "Guardia de cuenta",
                    dining.BillRequested ? "Lista" : "Bloqueada"
                );
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private static void DrawLine(
        BistroBuilderCanonicalOrderLine line,
        BistroBuilderCustomerDiningOrderRuntime dining
    )
    {
        if (line == null)
        {
            return;
        }

        int completed = 0;

        if (dining != null)
        {
            for (int index = 0;
                 index < line.ConsumerCustomerIds.Count;
                 index++)
            {
                if (dining.TryGetCustomer(
                        line.ConsumerCustomerIds[index],
                        out BistroBuilderCustomerDiningCustomerRuntime customer
                    ) &&
                    customer != null &&
                    customer.HasConsumedLine(line.LineId))
                {
                    completed++;
                }
            }
        }

        string sharing = line.IsShared
            ? "Compartido " + completed + "/" +
              line.ConsumerCustomerIds.Count
            : "Individual";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            "P" + line.CourseIndex + " · " + line.DishId,
            GUILayout.Width(260f)
        );
        EditorGUILayout.LabelField(
            line.State.ToString(),
            GUILayout.Width(130f)
        );
        EditorGUILayout.LabelField(sharing);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.SelectableLabel(
            line.LineId,
            EditorStyles.miniLabel,
            GUILayout.Height(16f)
        );
    }

    private BistroBuilderCustomerDiningOrderRuntime FindDiningRuntime(
        string orderId
    )
    {
        for (int index = 0; index < diningOrders.Count; index++)
        {
            if (diningOrders[index] != null &&
                string.Equals(
                    diningOrders[index].OrderId,
                    orderId,
                    System.StringComparison.Ordinal
                ))
            {
                return diningOrders[index];
            }
        }

        return null;
    }

    private static string JoinCourses(IReadOnlyList<int> courses)
    {
        if (courses == null || courses.Count == 0)
        {
            return "Ninguno";
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int index = 0; index < courses.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(courses[index]);
        }

        return builder.ToString();
    }
}
