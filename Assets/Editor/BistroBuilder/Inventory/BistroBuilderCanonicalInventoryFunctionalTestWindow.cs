using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional real de 368B sobre el servicio instalado en Play Mode.
///
/// Las operaciones se realizan únicamente en memoria y se descartan al salir
/// de Play Mode. Verifica el componente real de la escena, no una instancia
/// aislada del autotest.
/// </summary>
public sealed class BistroBuilderCanonicalInventoryFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "368B Functional Inventory Test";

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode y ejecuta la prueba funcional.";
    private MessageType reportType = MessageType.Info;

    [MenuItem(MenuPath, false, 360)]
    private static void Open()
    {
        GetWindow<BistroBuilderCanonicalInventoryFunctionalTestWindow>(
            "BB 368B Test"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "368B — Inventario canónico",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba añade una recepción, reserva dos ingredientes, " +
            "consume la reserva y registra una merma. Salir de Play Mode " +
            "restaura el estado de la escena.",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Ejecutar prueba funcional 368B", GUILayout.Height(32f)))
            {
                RunFunctionalTest();
            }
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void RunFunctionalTest()
    {
        BistroBuilderInventoryService service =
            FindFirstObjectByType<BistroBuilderInventoryService>();

        if (service == null || !service.IsInitialized)
        {
            SetFailure(
                "No existe un BistroBuilderInventoryService inicializado " +
                "en la escena activa."
            );
            return;
        }

        if (!Convert(500d, BistroBuilderMeasurementUnit.Gram, out long grams500) ||
            !Convert(200d, BistroBuilderMeasurementUnit.Gram, out long grams200) ||
            !Convert(50d, BistroBuilderMeasurementUnit.Gram, out long grams50))
        {
            SetFailure("No se pudieron preparar las cantidades de prueba.");
            return;
        }

        if (!service.TryGetStockSnapshot(
                "ingredient_fabes",
                out BistroBuilderInventoryStockSnapshot initialFabes
            ) ||
            !service.TryGetStockSnapshot(
                "ingredient_cebolla",
                out BistroBuilderInventoryStockSnapshot initialOnion
            ))
        {
            SetFailure("Faltan ingredientes canónicos de la prueba.");
            return;
        }

        int transactionsBefore = service.TransactionCount;
        string runId = Guid.NewGuid().ToString("N");

        if (!service.TryAddStock(
                "functional_368b_purchase_" + runId,
                "functional_supplier",
                "ingredient_fabes",
                grams500,
                BistroBuilderInventoryTransactionType.Purchase,
                "Recepción funcional 368B.",
                out string error
            ))
        {
            SetFailure("Falló la recepción: " + error);
            return;
        }

        var lines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(
                "ingredient_fabes",
                grams200
            ),
            new BistroBuilderInventoryQuantityLine(
                "ingredient_cebolla",
                grams50
            )
        };

        if (!service.TryCreateReservation(
                "functional_368b_reserve_" + runId,
                "functional_368b_reservation_" + runId,
                "functional_order_" + runId,
                lines,
                out _,
                out error
            ))
        {
            SetFailure("Falló la reserva atómica: " + error);
            return;
        }

        if (!service.TryConsumeReservation(
                "functional_368b_consume_" + runId,
                "functional_368b_reservation_" + runId,
                "Consumo funcional 368B.",
                out error
            ))
        {
            SetFailure("Falló el consumo: " + error);
            return;
        }

        if (!service.TryRegisterWaste(
                "functional_368b_waste_" + runId,
                "functional_inventory_count_" + runId,
                "ingredient_fabes",
                grams50,
                "Merma funcional 368B.",
                out error
            ))
        {
            SetFailure("Falló la merma: " + error);
            return;
        }

        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot finalFabes
        );
        service.TryGetStockSnapshot(
            "ingredient_cebolla",
            out BistroBuilderInventoryStockSnapshot finalOnion
        );

        bool balancesAreExact =
            finalFabes.OnHandCanonicalMilliUnits ==
                initialFabes.OnHandCanonicalMilliUnits +
                grams500 - grams200 - grams50 &&
            finalFabes.ReservedCanonicalMilliUnits == 0L &&
            finalFabes.ConsumedCanonicalMilliUnits ==
                initialFabes.ConsumedCanonicalMilliUnits + grams200 &&
            finalFabes.WastedCanonicalMilliUnits ==
                initialFabes.WastedCanonicalMilliUnits + grams50 &&
            finalOnion.OnHandCanonicalMilliUnits ==
                initialOnion.OnHandCanonicalMilliUnits - grams50 &&
            finalOnion.ReservedCanonicalMilliUnits == 0L &&
            finalOnion.ConsumedCanonicalMilliUnits ==
                initialOnion.ConsumedCanonicalMilliUnits + grams50;

        if (!balancesAreExact)
        {
            SetFailure(
                "Los balances finales no coinciden con las operaciones."
            );
            return;
        }

        if (service.TransactionCount != transactionsBefore + 6)
        {
            SetFailure(
                "El libro no registró los seis movimientos esperados."
            );
            return;
        }

        if (!service.ValidateRuntimeState(out error))
        {
            SetFailure("La auditoría final falló: " + error);
            return;
        }

        report =
            "BISTRO BUILDER — PRUEBA FUNCIONAL 368B SUPERADA\n\n" +
            "- Recepción física: +500 g de fabes.\n" +
            "- Reserva atómica: 200 g de fabes y 50 g de cebolla.\n" +
            "- Consumo: reserva cerrada una sola vez.\n" +
            "- Merma: 50 g de fabes.\n" +
            "- Reservado final: 0.\n" +
            "- Libro: 6 movimientos nuevos y balances reconstruibles.";
        reportType = MessageType.Info;
        Debug.Log(report);
        Repaint();
    }

    private void SetFailure(string message)
    {
        report = "PRUEBA FUNCIONAL 368B FALLIDA\n\n" + message;
        reportType = MessageType.Error;
        Debug.LogError(report);
        Repaint();
    }

    private static bool Convert(
        double amount,
        BistroBuilderMeasurementUnit unit,
        out long canonicalMilliUnits
    )
    {
        return BistroBuilderMeasurementUtility
            .TryConvertToCanonicalMilliUnits(
                amount,
                unit,
                out canonicalMilliUnits,
                out _
            );
    }
}
