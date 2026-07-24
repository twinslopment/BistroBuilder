using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana temporal de diagnóstico para comprobar la integración real 367C.
///
/// No modifica comandas ni estados. Muestra la fachada legacy y su agregado
/// canónico asociado durante Play Mode.
/// </summary>
public sealed class BistroBuilderCanonicalOrderIntegrationDebugWindow :
    EditorWindow
{
    private OrderSystem orderSystem;
    private BistroBuilderCanonicalOrderService canonicalOrders;
    private BistroBuilderCanonicalOrderIntegrationService integration;

    private Vector2 scroll;
    private readonly List<BistroBuilderCanonicalOrder> canonicalBuffer =
        new List<BistroBuilderCanonicalOrder>(32);

    [MenuItem(
        "Tools/Bistro Builder/Orders/Service Integration Debug",
        false,
        213
    )]
    private static void Open()
    {
        GetWindow<
            BistroBuilderCanonicalOrderIntegrationDebugWindow
        >("BB Service Orders");
    }

    private void OnEnable()
    {
        RefreshReferences();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367C — Integración de servicio",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Ventana de solo lectura. Las comandas deben crearse mediante " +
            "el servicio real del restaurante.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para observar comandas reales.",
                MessageType.Warning
            );

            if (GUILayout.Button("Actualizar referencias"))
            {
                RefreshReferences();
            }

            return;
        }

        if (orderSystem == null ||
            canonicalOrders == null ||
            integration == null)
        {
            RefreshReferences();
        }

        if (orderSystem == null ||
            canonicalOrders == null ||
            integration == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontraron todos los servicios 367C.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Comandas legacy activas",
            orderSystem.ActiveOrders.Count.ToString()
        );
        EditorGUILayout.LabelField(
            "Comandas canónicas runtime",
            canonicalOrders.OrderCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Enlaces activos",
            integration.ActiveLinkCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Servicio de carta",
            integration.CurrentMealService.ToString()
        );

        if (GUILayout.Button("Actualizar fotografía"))
        {
            Repaint();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        IReadOnlyList<RestaurantOrder> active =
            orderSystem.ActiveOrders;

        for (int index = 0; index < active.Count; index++)
        {
            RestaurantOrder legacy = active[index];

            if (legacy == null)
            {
                continue;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Legacy " + legacy.OrderId +
                " — " + legacy.CurrentState,
                EditorStyles.boldLabel
            );
            EditorGUILayout.LabelField(
                "CanonicalOrderId",
                legacy.CanonicalOrderId
            );
            EditorGUILayout.LabelField(
                "Mesa",
                legacy.Table != null
                    ? legacy.Table.TableId.ToString()
                    : "-"
            );
            EditorGUILayout.LabelField(
                "Grupo",
                legacy.CustomerGroup != null
                    ? legacy.CustomerGroup.GroupId.ToString()
                    : "-"
            );

            if (!canonicalOrders.TryGetOrderSnapshot(
                    legacy.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder canonical
                ))
            {
                EditorGUILayout.HelpBox(
                    "No existe el agregado canónico enlazado.",
                    MessageType.Error
                );
                continue;
            }

            EditorGUILayout.LabelField(
                "Estado canónico",
                canonical.State.ToString()
            );
            EditorGUILayout.LabelField(
                "Líneas",
                canonical.Lines.Count.ToString()
            );
            EditorGUILayout.LabelField(
                "Total congelado",
                (canonical.CalculateTotalPriceCents() / 100f)
                    .ToString("0.00") + " €"
            );

            for (int lineIndex = 0;
                 lineIndex < canonical.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line =
                    canonical.Lines[lineIndex];

                EditorGUILayout.LabelField(
                    "  " + (lineIndex + 1) + ". " +
                    line.PrimaryCustomerId + " → " +
                    line.DishId + " — " +
                    line.State
                );
            }
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField(
            "Fotografía canónica completa",
            EditorStyles.boldLabel
        );

        canonicalOrders.CopyOrderSnapshotsTo(canonicalBuffer);

        if (canonicalBuffer.Count == 0)
        {
            EditorGUILayout.LabelField(
                "Todavía no existen comandas canónicas."
            );
        }

        for (int orderIndex = 0;
             orderIndex < canonicalBuffer.Count;
             orderIndex++)
        {
            BistroBuilderCanonicalOrder order =
                canonicalBuffer[orderIndex];

            EditorGUILayout.LabelField(
                order.SequenceNumber + " — " +
                order.OrderId + " — " +
                order.State
            );
            EditorGUILayout.LabelField(
                "  Legacy: " +
                (string.IsNullOrEmpty(order.ExternalReferenceId)
                    ? "-"
                    : order.ExternalReferenceId) +
                " | Mesa: " + order.TableReferenceId +
                " | Grupo: " + order.CustomerGroupReferenceId
            );
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshReferences()
    {
        orderSystem =
            Object.FindFirstObjectByType<OrderSystem>();
        canonicalOrders =
            Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderService
            >();
        integration =
            Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderIntegrationService
            >();
    }
}
