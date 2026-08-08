using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Autotest aislado de 2.2B: recepción atómica, agregación, idempotencia,
/// creación de lotes y desacoplamiento entre inventario y presentación.
/// No modifica el inventario real de la escena.
/// </summary>
public static class BistroBuilderGoodsReceiving22BSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Run 2.2B Goods Receiving and Basic Delivery Visual Self-Test";

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
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.2B");
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

    [MenuItem(MenuPath, false, 372)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 2.2B.",
                "Aceptar"
            );
            return;
        }

        var result = new TestResult();
        GameObject root = null;

        try
        {
            BistroBuilderGoodsReceiving22BValidationResult validation =
                BistroBuilderGoodsReceiving22BValidator.ValidateCurrentProject();
            result.Check(
                validation.ErrorCount == 0,
                "La instalación 2.2B supera el validador estructural."
            );
            result.Check(
                validation.WarningCount == 0,
                "La instalación 2.2B no deja advertencias estructurales."
            );

            result.Check(
                BistroBuilderGoodsReceivingIds.PrimaryWarehouse ==
                    "warehouse_primary" &&
                BistroBuilderGoodsReceivingIds.PrimarySupplyAccess ==
                    "supply_access_primary",
                "El contrato fija un almacén y un acceso de suministros únicos."
            );

            Scene scene = SceneManager.GetActiveScene();
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            BistroBuilderInventoryService installedInventory = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderInventoryService>()
                : null;
            BistroBuilderRecipeCatalogService recipes = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
                : null;
            BistroBuilderGeneralGameStateService generalState = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderGeneralGameStateService>()
                : null;

            root = new GameObject("BB_22B_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderInventoryService inventory =
                root.AddComponent<BistroBuilderInventoryService>();
            SetReference(inventory, "recipeCatalogService", recipes);
            SetReference(
                inventory,
                "openingStockProfile",
                installedInventory != null
                    ? installedInventory.OpeningStockProfile
                    : null
            );
            SetReference(
                inventory,
                "generalGameStateService",
                generalState
            );
            SetBoolean(inventory, "logInitialization", false);

            string error = string.Empty;
            bool initialized = inventory.TryInitialize(out error);
            result.Check(
                initialized && inventory.ValidateRuntimeState(out error),
                "Un inventario aislado arranca válido sobre la base 2.2A."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaBefore
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patataBefore
            );
            int lotsBefore = inventory.LotCount;
            int transactionsBefore = inventory.TransactionCount;
            long revisionBefore = inventory.RuntimeRevision;

            bool wasReplayed = false;
            bool batchReceived = false;
            if (initialized)
            {
                batchReceived = inventory.TryReceivePurchaseBatch(
                    "receipt_22b_selftest_batch",
                    "supplier_22b_selftest",
                    new List<BistroBuilderInventoryQuantityLine>
                    {
                        new BistroBuilderInventoryQuantityLine(
                            "ingredient_merluza",
                            500L
                        ),
                        new BistroBuilderInventoryQuantityLine(
                            "ingredient_patata",
                            1000L
                        ),
                        new BistroBuilderInventoryQuantityLine(
                            "ingredient_merluza",
                            250L
                        )
                    },
                    "Recepción atómica de autotest 2.2B.",
                    out wasReplayed,
                    out error
                );
            }
            result.Check(
                batchReceived && !wasReplayed,
                "Una recepción nueva se acepta una sola vez."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaAfter
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patataAfter
            );
            result.Check(
                batchReceived &&
                merluzaAfter.OnHandCanonicalMilliUnits ==
                    merluzaBefore.OnHandCanonicalMilliUnits + 750L &&
                patataAfter.OnHandCanonicalMilliUnits ==
                    patataBefore.OnHandCanonicalMilliUnits + 1000L,
                "La recepción agrega líneas duplicadas y actualiza ambos ingredientes."
            );

            result.Check(
                inventory.LotCount == lotsBefore + 2,
                "La recepción crea un lote interno por ingrediente agregado, no por fila repetida."
            );

            List<BistroBuilderInventoryLotSnapshot> lots =
                new List<BistroBuilderInventoryLotSnapshot>();
            inventory.CopyLotSnapshotsTo(lots);
            int sourceLots = 0;
            for (int index = 0; index < lots.Count; index++)
            {
                if (lots[index].SourceId == "supplier_22b_selftest")
                {
                    sourceLots++;
                }
            }
            result.Check(
                sourceLots == 2,
                "Los lotes recibidos conservan la procedencia para futuras compras/proveedores."
            );

            List<BistroBuilderInventoryTransactionSnapshot> transactions =
                new List<BistroBuilderInventoryTransactionSnapshot>();
            inventory.CopyTransactionsTo(transactions);
            int receiptTransactions = 0;
            for (int index = 0; index < transactions.Count; index++)
            {
                BistroBuilderInventoryTransactionSnapshot transaction =
                    transactions[index];
                if (transaction.OperationId == "receipt_22b_selftest_batch" &&
                    transaction.TransactionType ==
                        BistroBuilderInventoryTransactionType.Purchase)
                {
                    receiptTransactions++;
                }
            }
            result.Check(
                receiptTransactions == 2 &&
                inventory.TransactionCount == transactionsBefore + 2,
                "El libro registra una compra por ingrediente dentro del mismo ReceiptId."
            );

            long revisionAfterBatch = inventory.RuntimeRevision;
            int lotsAfterBatch = inventory.LotCount;
            int transactionsAfterBatch = inventory.TransactionCount;
            bool replayed = inventory.TryReceivePurchaseBatch(
                "receipt_22b_selftest_batch",
                "supplier_22b_selftest",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        750L
                    ),
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        1000L
                    )
                },
                "Repetición idempotente.",
                out bool replayFlag,
                out error
            );
            result.Check(
                replayed && replayFlag &&
                inventory.RuntimeRevision == revisionAfterBatch &&
                inventory.LotCount == lotsAfterBatch &&
                inventory.TransactionCount == transactionsAfterBatch,
                "Repetir el mismo ReceiptId no duplica stock, lotes ni movimientos."
            );

            bool conflictRejected = !inventory.TryReceivePurchaseBatch(
                "receipt_22b_selftest_batch",
                "supplier_22b_selftest",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        751L
                    ),
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        1000L
                    )
                },
                "Conflicto esperado.",
                out _,
                out error
            );
            result.Check(
                conflictRejected &&
                inventory.RuntimeRevision == revisionAfterBatch,
                "Un ReceiptId reutilizado con contenido distinto se rechaza sin mutación."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot beforeInvalid
            );
            long revisionBeforeInvalid = inventory.RuntimeRevision;
            bool invalidRejected = !inventory.TryReceivePurchaseBatch(
                "receipt_22b_selftest_invalid",
                "supplier_22b_selftest",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        300L
                    ),
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_nonexistent_22b",
                        100L
                    )
                },
                "Recepción inválida de autotest.",
                out _,
                out error
            );
            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot afterInvalid
            );
            result.Check(
                invalidRejected &&
                beforeInvalid.OnHandCanonicalMilliUnits ==
                    afterInvalid.OnHandCanonicalMilliUnits &&
                inventory.RuntimeRevision == revisionBeforeInvalid,
                "Una línea inválida aborta la recepción completa de forma atómica."
            );

            BistroBuilderGoodsReceivingService receiving =
                root.AddComponent<BistroBuilderGoodsReceivingService>();
            SetReference(receiving, "inventoryService", inventory);
            SetReference(
                receiving,
                "generalGameStateService",
                generalState
            );
            SetBoolean(receiving, "logReceipts", false);

            int acceptedEvents = 0;
            receiving.ReceiptAccepted += _ => acceptedEvents++;
            bool serviceAccepted = receiving.TryReceiveGoods(
                "receipt_22b_selftest_service",
                "supplier_22b_service",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        400L
                    )
                },
                "Recepción mediante capa de aplicación.",
                out BistroBuilderGoodsReceiptSnapshot receipt,
                out error
            );
            result.Check(
                serviceAccepted && receipt != null && !receipt.WasReplayed &&
                receipt.WarehouseId ==
                    BistroBuilderGoodsReceivingIds.PrimaryWarehouse &&
                receipt.Lines.Count == 1,
                "La capa de aplicación emite una recepción dirigida al único almacén genérico."
            );

            bool serviceReplay = receiving.TryReceiveGoods(
                "receipt_22b_selftest_service",
                "supplier_22b_service",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        400L
                    )
                },
                "Repetición mediante capa de aplicación.",
                out BistroBuilderGoodsReceiptSnapshot replayReceipt,
                out error
            );
            result.Check(
                serviceReplay && replayReceipt != null &&
                replayReceipt.WasReplayed && acceptedEvents == 1,
                "Una recepción repetida no vuelve a disparar al repartidor visual."
            );

            result.Check(
                inventory.RuntimeRevision > revisionBefore &&
                inventory.ValidateRuntimeState(out error),
                "La auditoría final conserva balances, lotes y libro coherentes."
            );
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
            if (root != null)
            {
                Object.DestroyImmediate(root);
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

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
