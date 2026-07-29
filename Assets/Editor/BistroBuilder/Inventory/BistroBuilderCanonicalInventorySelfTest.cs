using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Autotest determinista del inventario transaccional 368B y su instalación.
/// </summary>
public static class BistroBuilderCanonicalInventorySelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "Run 368B Canonical Inventory Self-Test";

    private sealed class TestResult
    {
        public readonly List<string> Passed = new List<string>();
        public readonly List<string> Failed = new List<string>();

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed.Add(message);
            }
            else
            {
                Failed.Add(message);
            }
        }

        public string BuildReport()
        {
            var builder = new StringBuilder(8192);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 368B");
            builder.AppendLine("Pruebas superadas: " + Passed.Count);
            builder.AppendLine("Pruebas fallidas: " + Failed.Count);

            for (int index = 0; index < Passed.Count; index++)
            {
                builder.AppendLine("- OK: " + Passed[index]);
            }

            for (int index = 0; index < Failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + Failed[index]);
            }

            return builder.ToString().TrimEnd();
        }
    }

    [MenuItem(MenuPath, false, 350)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 368B.",
                "Aceptar"
            );
            return;
        }

        var result = new TestResult();
        GameObject temporaryObject = null;

        try
        {
            RunInstalledStructureTests(result);
            temporaryObject = RunRuntimeTransactionTests(result);
        }
        catch (Exception exception)
        {
            result.Failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            if (temporaryObject != null)
            {
                Object.DestroyImmediate(temporaryObject);
            }
        }

        string report = result.BuildReport();

        if (result.Failed.Count > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    private static void RunInstalledStructureTests(TestResult result)
    {
        BistroBuilderCanonicalInventoryValidationResult validation =
            BistroBuilderCanonicalInventoryValidator
                .ValidateCurrentProject();

        result.Check(
            validation.ErrorCount == 0,
            "La instalación 368B supera el validador estructural."
        );
        result.Check(
            validation.WarningCount == 0,
            "La instalación 368B no deja advertencias estructurales."
        );

        Scene scene = SceneManager.GetActiveScene();
        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);
        BistroBuilderInventoryService installed = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderInventoryService>()
            : null;

        result.Check(
            installed != null,
            "Existe un único servicio canónico de inventario."
        );
        result.Check(
            installed != null && installed.OpeningStockProfile != null,
            "El servicio tiene un perfil de existencias de apertura."
        );

        BistroBuilder368BInstalledHudDock[] docks =
            FindSceneObjects<BistroBuilder368BInstalledHudDock>(scene);

        result.Check(
            docks.Length == 1 &&
            docks[0].ValidateConfiguration(out _),
            "El dock compacto de tiempo y velocidad está instalado."
        );
    }

    private static GameObject RunRuntimeTransactionTests(TestResult result)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility
                .FindGameSystems(scene);
        BistroBuilderRecipeCatalogService recipeService = gameSystems != null
            ? gameSystems.GetComponent<
                BistroBuilderRecipeCatalogService
            >()
            : null;
        BistroBuilderOpeningStockProfile profile =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderOpeningStockProfile
            >(
                BistroBuilderCanonicalInventoryInstaller
                    .OpeningStockProfilePath
            );

        var temporary = new GameObject("BB_368B_Inventory_SelfTest");
        BistroBuilderInventoryService service =
            temporary.AddComponent<BistroBuilderInventoryService>();
        SerializedObject serialized = new SerializedObject(service);
        serialized.FindProperty("recipeCatalogService")
            .objectReferenceValue = recipeService;
        serialized.FindProperty("openingStockProfile")
            .objectReferenceValue = profile;
        serialized.FindProperty("logInitialization").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        bool initialized = service.TryInitialize(out string error);
        result.Check(initialized, "El inventario se inicializa desde datos.");
        result.Check(
            initialized && service.StockEntryCount ==
                recipeService.IngredientCount,
            "Se crea un balance por cada ingrediente canónico."
        );
        result.Check(
            initialized && service.TransactionCount == profile.LineCount,
            "Cada existencia inicial deja un movimiento trazable."
        );
        result.Check(
            initialized && service.ReservationCount == 0,
            "La partida comienza sin reservas activas."
        );

        if (!initialized)
        {
            result.Failed.Add("Inicialización: " + error);
            return temporary;
        }

        Convert(500d, BistroBuilderMeasurementUnit.Gram, out long grams500);
        Convert(300d, BistroBuilderMeasurementUnit.Gram, out long grams300);
        Convert(200d, BistroBuilderMeasurementUnit.Gram, out long grams200);
        Convert(100d, BistroBuilderMeasurementUnit.Gram, out long grams100);
        Convert(50d, BistroBuilderMeasurementUnit.Gram, out long grams50);

        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot initialFabes
        );
        int transactionsBeforePurchase = service.TransactionCount;
        bool purchase = service.TryAddStock(
            "op_purchase_fabes_01",
            "supplier_selftest",
            "ingredient_fabes",
            grams500,
            BistroBuilderInventoryTransactionType.Purchase,
            "Compra de prueba.",
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot purchasedFabes
        );

        result.Check(purchase, "Una compra válida añade stock.");
        result.Check(
            purchasedFabes.OnHandCanonicalMilliUnits ==
                initialFabes.OnHandCanonicalMilliUnits + grams500,
            "La compra incrementa exactamente la existencia física."
        );
        result.Check(
            service.TransactionCount == transactionsBeforePurchase + 1,
            "La compra crea un único movimiento."
        );

        bool purchaseReplay = service.TryAddStock(
            "op_purchase_fabes_01",
            "supplier_selftest",
            "ingredient_fabes",
            grams500,
            BistroBuilderInventoryTransactionType.Purchase,
            "Compra de prueba.",
            out _
        );
        result.Check(
            purchaseReplay &&
            service.TransactionCount == transactionsBeforePurchase + 1,
            "Repetir el mismo OperationId no aplica la compra dos veces."
        );

        bool conflictingReplay = service.TryAddStock(
            "op_purchase_fabes_01",
            "supplier_selftest",
            "ingredient_fabes",
            grams300,
            BistroBuilderInventoryTransactionType.Purchase,
            "Operación conflictiva.",
            out _
        );
        result.Check(
            !conflictingReplay,
            "Un OperationId reutilizado para otros datos se rechaza."
        );

        result.Check(
            !service.TryAddStock(
                "op_unknown_ingredient",
                "supplier_selftest",
                "ingredient_inexistente",
                grams100,
                BistroBuilderInventoryTransactionType.Purchase,
                string.Empty,
                out _
            ),
            "Los ingredientes desconocidos se rechazan."
        );
        result.Check(
            !service.TryAddStock(
                "op_negative_stock",
                "supplier_selftest",
                "ingredient_fabes",
                -1L,
                BistroBuilderInventoryTransactionType.Purchase,
                string.Empty,
                out _
            ),
            "Las cantidades negativas se rechazan."
        );

        var reservationLines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(
                "ingredient_fabes",
                grams300
            ),
            new BistroBuilderInventoryQuantityLine(
                "ingredient_cebolla",
                grams100
            )
        };
        bool reserved = service.TryCreateReservation(
            "op_reserve_01",
            "reservation_01",
            "order_selftest_01",
            reservationLines,
            out BistroBuilderInventoryReservationSnapshot reservation,
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot reservedFabes
        );
        service.TryGetStockSnapshot(
            "ingredient_cebolla",
            out BistroBuilderInventoryStockSnapshot reservedOnion
        );

        result.Check(
            reserved && reservation != null && reservation.Lines.Count == 2,
            "Una reserva multiplaza de ingredientes se completa."
        );
        result.Check(
            reservedFabes.ReservedCanonicalMilliUnits == grams300 &&
            reservedOnion.ReservedCanonicalMilliUnits == grams100,
            "La reserva aplica exactamente ambas cantidades."
        );
        result.Check(
            reservedFabes.AvailableCanonicalMilliUnits ==
                reservedFabes.OnHandCanonicalMilliUnits - grams300,
            "Disponible distingue existencia física y reserva."
        );

        long onionBeforeFailedReservation =
            reservedOnion.ReservedCanonicalMilliUnits;
        long fabesBeforeFailedReservation =
            reservedFabes.ReservedCanonicalMilliUnits;
        var impossibleLines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(
                "ingredient_fabes",
                reservedFabes.AvailableCanonicalMilliUnits + 1L
            ),
            new BistroBuilderInventoryQuantityLine(
                "ingredient_cebolla",
                grams50
            )
        };
        bool impossible = service.TryCreateReservation(
            "op_reserve_impossible",
            "reservation_impossible",
            "order_selftest_impossible",
            impossibleLines,
            out _,
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot afterFailedFabes
        );
        service.TryGetStockSnapshot(
            "ingredient_cebolla",
            out BistroBuilderInventoryStockSnapshot afterFailedOnion
        );

        result.Check(
            !impossible,
            "Una reserva con un ingrediente insuficiente se rechaza."
        );
        result.Check(
            afterFailedFabes.ReservedCanonicalMilliUnits ==
                fabesBeforeFailedReservation &&
            afterFailedOnion.ReservedCanonicalMilliUnits ==
                onionBeforeFailedReservation,
            "La reserva fallida no modifica ninguna línea."
        );

        long fabesOnHandBeforeRelease = afterFailedFabes.OnHandCanonicalMilliUnits;
        bool released = service.TryReleaseReservation(
            "op_release_01",
            "reservation_01",
            "Cancelación de prueba.",
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot releasedFabes
        );
        service.TryGetStockSnapshot(
            "ingredient_cebolla",
            out BistroBuilderInventoryStockSnapshot releasedOnion
        );

        result.Check(released, "Una reserva activa puede liberarse.");
        result.Check(
            releasedFabes.ReservedCanonicalMilliUnits == 0L &&
            releasedOnion.ReservedCanonicalMilliUnits == 0L,
            "Liberar devuelve exactamente toda la disponibilidad."
        );
        result.Check(
            releasedFabes.OnHandCanonicalMilliUnits ==
                fabesOnHandBeforeRelease,
            "Liberar una reserva no consume existencia física."
        );

        var consumptionLines = new List<BistroBuilderInventoryQuantityLine>
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
        bool reservedForConsumption = service.TryCreateReservation(
            "op_reserve_02",
            "reservation_02",
            "order_selftest_02",
            consumptionLines,
            out _,
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot beforeConsumeFabes
        );
        int transactionsBeforeConsume = service.TransactionCount;
        bool consumed = service.TryConsumeReservation(
            "op_consume_02",
            "reservation_02",
            "Preparación completada.",
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot consumedFabes
        );

        result.Check(
            reservedForConsumption && consumed,
            "Una reserva puede consumirse atómicamente."
        );
        result.Check(
            consumedFabes.OnHandCanonicalMilliUnits ==
                beforeConsumeFabes.OnHandCanonicalMilliUnits - grams200 &&
            consumedFabes.ReservedCanonicalMilliUnits == 0L,
            "Consumir reduce existencia física y reserva."
        );
        result.Check(
            consumedFabes.ConsumedCanonicalMilliUnits == grams200,
            "El consumo acumulado queda trazado."
        );

        bool consumeReplay = service.TryConsumeReservation(
            "op_consume_02",
            "reservation_02",
            "Preparación completada.",
            out _
        );
        result.Check(
            consumeReplay &&
            service.TransactionCount == transactionsBeforeConsume + 2,
            "Repetir el mismo consumo no descuenta dos veces."
        );
        result.Check(
            !service.TryConsumeReservation(
                "op_consume_02_second",
                "reservation_02",
                string.Empty,
                out _
            ),
            "Una reserva ya consumida rechaza un segundo cierre."
        );

        long onHandBeforeWaste = consumedFabes.OnHandCanonicalMilliUnits;
        bool waste = service.TryRegisterWaste(
            "op_waste_01",
            "inventory_count_selftest",
            "ingredient_fabes",
            grams100,
            "Merma de prueba.",
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot wastedFabes
        );

        result.Check(waste, "La merma libre puede registrarse.");
        result.Check(
            wastedFabes.OnHandCanonicalMilliUnits ==
                onHandBeforeWaste - grams100 &&
            wastedFabes.WastedCanonicalMilliUnits == grams100,
            "La merma reduce stock y aumenta su acumulado."
        );

        var protectedLines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(
                "ingredient_fabes",
                grams300
            )
        };
        service.TryCreateReservation(
            "op_reserve_protected",
            "reservation_protected",
            "order_protected",
            protectedLines,
            out _,
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot protectedFabes
        );
        bool invalidWaste = service.TryRegisterWaste(
            "op_waste_reserved",
            "inventory_count_selftest",
            "ingredient_fabes",
            protectedFabes.AvailableCanonicalMilliUnits + 1L,
            string.Empty,
            out _
        );
        result.Check(
            !invalidWaste,
            "La merma no puede invadir cantidades reservadas."
        );
        result.Check(
            !service.TryCorrectOnHand(
                "op_correction_below_reserved",
                "inventory_count_selftest",
                "ingredient_fabes",
                protectedFabes.ReservedCanonicalMilliUnits - 1L,
                string.Empty,
                out _
            ),
            "Una corrección no puede quedar por debajo de la reserva."
        );
        service.TryReleaseReservation(
            "op_release_protected",
            "reservation_protected",
            string.Empty,
            out _
        );

        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot beforeCorrection
        );
        bool correction = service.TryCorrectOnHand(
            "op_correction_valid",
            "inventory_count_selftest",
            "ingredient_fabes",
            beforeCorrection.OnHandCanonicalMilliUnits + grams100,
            "Conteo físico corregido.",
            out _
        );
        service.TryGetStockSnapshot(
            "ingredient_fabes",
            out BistroBuilderInventoryStockSnapshot afterCorrection
        );
        result.Check(correction, "Una corrección física válida se aplica.");
        result.Check(
            afterCorrection.OnHandCanonicalMilliUnits ==
                beforeCorrection.OnHandCanonicalMilliUnits + grams100,
            "La corrección fija exactamente el balance contado."
        );

        result.Check(
            !service.TryReleaseReservation(
                "op_release_unknown",
                "reservation_unknown",
                string.Empty,
                out _
            ),
            "Las reservas desconocidas se rechazan."
        );

        var ledger = new List<BistroBuilderInventoryTransactionSnapshot>();
        service.CopyTransactionsTo(ledger);
        bool sequenceValid = true;

        for (int index = 0; index < ledger.Count; index++)
        {
            if (ledger[index].Sequence != index + 1L)
            {
                sequenceValid = false;
                break;
            }
        }

        result.Check(sequenceValid, "El libro mantiene secuencia continua.");
        result.Check(
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.Purchase
            ) &&
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.Reservation
            ) &&
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.ReservationRelease
            ) &&
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.Consumption
            ) &&
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.Waste
            ) &&
            ContainsTransactionType(
                ledger,
                BistroBuilderInventoryTransactionType.Correction
            ),
            "El libro cubre compras, reservas, consumo, merma y corrección."
        );
        result.Check(
            service.ValidateRuntimeState(out _),
            "Los balances se reconstruyen exactamente desde el libro."
        );

        return temporary;
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

    private static T[] FindSceneObjects<T>(Scene scene)
        where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            results.AddRange(
                roots[rootIndex].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
    }

    private static bool ContainsTransactionType(
        List<BistroBuilderInventoryTransactionSnapshot> ledger,
        BistroBuilderInventoryTransactionType type
    )
    {
        for (int index = 0; index < ledger.Count; index++)
        {
            if (ledger[index].TransactionType == type)
            {
                return true;
            }
        }

        return false;
    }
}
