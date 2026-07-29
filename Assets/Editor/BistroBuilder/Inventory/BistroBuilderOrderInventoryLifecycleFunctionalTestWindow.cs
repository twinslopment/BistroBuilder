using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderOrderInventoryLifecycleFunctionalTestWindow : EditorWindow
{
    private string result = "Entra en Play Mode y ejecuta la prueba funcional.";

    [MenuItem("Tools/Bistro Builder/Inventory/368CD Functional Order Inventory Test", false, 343)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderOrderInventoryLifecycleFunctionalTestWindow>("BB 368CD Test");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("368CD — Ciclo de ingredientes por comanda", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Comprueba de forma aislada reserva, idempotencia, consumo y liberación sin alterar la escena al salir de Play Mode.",
            MessageType.Info);
        GUI.enabled = EditorApplication.isPlaying;
        if (GUILayout.Button("Ejecutar prueba funcional 368CD", GUILayout.Height(32))) RunTest();
        GUI.enabled = true;
        EditorGUILayout.HelpBox(result, MessageType.None);
    }

    private void RunTest()
    {
        BistroBuilderInventoryService inventory = Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        if (inventory == null)
        {
            result = "FALLO: no existe BistroBuilderInventoryService.";
            return;
        }

        const string ingredientA = "ingredient_fabes";
        const string ingredientB = "ingredient_cebolla";
        string token = System.Guid.NewGuid().ToString("N");
        string reservationId = "functional_368cd_res_" + token;
        var lines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(ingredientA, 120000L),
            new BistroBuilderInventoryQuantityLine(ingredientB, 40000L)
        };

        if (!inventory.TryGetStockSnapshot(ingredientA, out BistroBuilderInventoryStockSnapshot beforeA) ||
            !inventory.TryGetStockSnapshot(ingredientB, out BistroBuilderInventoryStockSnapshot beforeB))
        {
            result = "FALLO: no se pudieron leer balances iniciales.";
            return;
        }

        if (!inventory.TryCreateReservation("functional_368cd_reserve_" + token, reservationId,
                "functional_order_line_" + token, lines, out _, out string error))
        {
            result = "FALLO al reservar: " + error;
            return;
        }

        // Repetición exacta: debe ser idempotente y no duplicar stock reservado.
        if (!inventory.TryCreateReservation("functional_368cd_reserve_" + token, reservationId,
                "functional_order_line_" + token, lines, out _, out error))
        {
            result = "FALLO en replay idempotente: " + error;
            return;
        }

        if (!inventory.TryConsumeReservation("functional_368cd_consume_" + token,
                reservationId, "Inicio de preparación funcional.", out error))
        {
            result = "FALLO al consumir: " + error;
            return;
        }

        // Repetición exacta de consumo: no debe descontar dos veces.
        if (!inventory.TryConsumeReservation("functional_368cd_consume_" + token,
                reservationId, "Inicio de preparación funcional.", out error))
        {
            result = "FALLO en consumo idempotente: " + error;
            return;
        }

        inventory.TryGetStockSnapshot(ingredientA, out BistroBuilderInventoryStockSnapshot afterA);
        inventory.TryGetStockSnapshot(ingredientB, out BistroBuilderInventoryStockSnapshot afterB);

        bool correct =
            afterA.OnHandCanonicalMilliUnits == beforeA.OnHandCanonicalMilliUnits - 120000L &&
            afterB.OnHandCanonicalMilliUnits == beforeB.OnHandCanonicalMilliUnits - 40000L &&
            afterA.ReservedCanonicalMilliUnits == beforeA.ReservedCanonicalMilliUnits &&
            afterB.ReservedCanonicalMilliUnits == beforeB.ReservedCanonicalMilliUnits;

        result = correct
            ? "BISTRO BUILDER — PRUEBA FUNCIONAL 368CD SUPERADA\n\n" +
              "- Reserva atómica de dos ingredientes.\n" +
              "- Replay de reserva sin duplicación.\n" +
              "- Consumo al iniciar preparación.\n" +
              "- Replay de consumo sin doble descuento.\n" +
              "- Reservado final restaurado al valor previo."
            : "FALLO: los balances finales no coinciden con un único consumo.";

        if (correct) Debug.Log(result); else Debug.LogError(result);
    }
}
