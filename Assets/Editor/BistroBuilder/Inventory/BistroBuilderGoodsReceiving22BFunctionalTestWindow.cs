using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional real de 2.2B sobre la escena jugable.
///
/// Ejecuta dos recepciones consecutivas para comprobar que el inventario se
/// actualiza de forma autoritativa y que la presentación usa siempre un solo
/// repartidor temporal, con cajas al entrar y sin ellas al salir. Al finalizar
/// restaura exactamente el snapshot original del inventario.
/// </summary>
public sealed class BistroBuilderGoodsReceiving22BFunctionalTestWindow :
    EditorWindow
{
    private const float SafetyTimeoutSeconds = 20f;

    private readonly List<string> passed = new List<string>();
    private readonly List<string> failed = new List<string>();
    private readonly Dictionary<
        string,
        HashSet<BistroBuilderGoodsReceivingVisualState>
    > statesByReceipt = new Dictionary<
        string,
        HashSet<BistroBuilderGoodsReceivingVisualState>
    >(StringComparer.Ordinal);

    private BistroBuilderInventoryService inventory;
    private BistroBuilderGoodsReceivingService receiving;
    private BistroBuilderGoodsReceivingPresentation presentation;
    private BistroBuilderGoodsReceivingRoute route;
    private BistroBuilderInventoryRuntimeSnapshot originalSnapshot;

    private string receiptA;
    private string receiptB;
    private string status = "Entra en Play Mode para ejecutar la prueba.";
    private bool running;
    private bool completionLogged;
    private double startedAt;
    private int completedVisuals;
    private int maxActiveVisualCount;
    private bool sawBoxesWhileCarrying;
    private bool sawNoBoxesAfterUnload;

    private long merluzaBefore;
    private long patataBefore;
    private long salBefore;
    private int lotsBefore;
    private int transactionsBefore;
    private int functionalReceivedDayIndex;

    private float originalMovementSpeed;
    private float originalUnloadDuration;
    private float originalExteriorDistance;
    private float originalArrivalDistance;
    private bool originalLogVisualFlow;
    private bool presentationSettingsCaptured;

    [MenuItem(
        "Tools/Bistro Builder/Inventory/2.2B Goods Receiving and Basic Delivery Visual Functional Test",
        false,
        373
    )]
    private static void OpenWindow()
    {
        BistroBuilderGoodsReceiving22BFunctionalTestWindow window =
            GetWindow<BistroBuilderGoodsReceiving22BFunctionalTestWindow>();
        window.titleContent = new GUIContent("BB 2.2B Test");
        window.minSize = new Vector2(680f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshReferences();
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorTick;
        UnsubscribePresentation();
        if (running)
        {
            AbortAndRestore("La ventana se cerró durante la prueba.");
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Bistro Builder 2.2B — Recepción y reparto visual básico",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "La prueba acepta mercancía mediante el inventario canónico y " +
            "muestra dos entregas consecutivas usando siempre un único " +
            "repartidor temporal. El personaje entra por suministros con " +
            "cajas, llega al almacén, descarga y sale. El inventario original " +
            "se restaura al terminar.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode y vuelve a esta ventana.",
                MessageType.Warning
            );
            if (GUILayout.Button("Actualizar referencias", GUILayout.Height(28f)))
            {
                RefreshReferences();
            }
            return;
        }

        if (inventory == null || receiving == null || presentation == null ||
            route == null)
        {
            RefreshReferences();
        }

        bool referencesReady = inventory != null && receiving != null &&
            presentation != null && route != null;
        if (!referencesReady)
        {
            EditorGUILayout.HelpBox(
                "Faltan componentes 2.2B. Ejecuta primero el instalador.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Estado visual",
            presentation.CurrentState.ToString()
        );
        EditorGUILayout.LabelField(
            "Repartidores activos",
            presentation.ActiveVisualCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Representaciones pendientes",
            presentation.PendingVisualCount.ToString()
        );

        EditorGUI.BeginDisabledGroup(running);
        if (GUILayout.Button(
                "Ejecutar prueba funcional 2.2B",
                GUILayout.Height(36f)
            ))
        {
            StartFunctionalTest();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Seguimiento", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.None);

        if (passed.Count > 0 || failed.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Correctos: " + passed.Count + "    Fallos: " + failed.Count,
                EditorStyles.boldLabel
            );
        }
    }

    private void StartFunctionalTest()
    {
        passed.Clear();
        failed.Clear();
        statesByReceipt.Clear();
        running = false;
        completionLogged = false;
        completedVisuals = 0;
        maxActiveVisualCount = 0;
        sawBoxesWhileCarrying = false;
        sawNoBoxesAfterUnload = false;
        presentationSettingsCaptured = false;

        try
        {
            RefreshReferences();
            if (inventory == null || receiving == null || presentation == null ||
                route == null)
            {
                throw new InvalidOperationException(
                    "No se localizaron todos los componentes 2.2B."
                );
            }

            if (presentation.IsBusy)
            {
                throw new InvalidOperationException(
                    "Hay una representación de reparto en curso. Espera a que " +
                    "termine antes de ejecutar la prueba funcional 2.2B."
                );
            }

            string error = string.Empty;
            Check(
                inventory.ValidateConfiguration(out error) &&
                receiving.ValidateConfiguration(out error) &&
                route.ValidateConfiguration(out error) &&
                presentation.ValidateConfiguration(out error),
                "Inventario, recepción, ruta y presentación están configurados."
            );
            if (failed.Count > 0)
            {
                throw new InvalidOperationException(error);
            }

            Check(
                inventory.TryCaptureRuntimeSnapshot(
                    out originalSnapshot,
                    out error
                ) && originalSnapshot != null,
                "Se captura el inventario original antes de la prueba."
            );
            if (originalSnapshot == null)
            {
                throw new InvalidOperationException(error);
            }

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluza
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patata
            );
            inventory.TryGetStockSnapshot(
                "ingredient_sal",
                out BistroBuilderInventoryStockSnapshot sal
            );
            merluzaBefore = merluza.OnHandCanonicalMilliUnits;
            patataBefore = patata.OnHandCanonicalMilliUnits;
            salBefore = sal.OnHandCanonicalMilliUnits;
            lotsBefore = inventory.LotCount;
            transactionsBefore = inventory.TransactionCount;

            CaptureAndAcceleratePresentation();
            SubscribePresentation();

            string token = DateTime.UtcNow.Ticks.ToString();
            receiptA = "receipt_22b_functional_" + token + "_a";
            receiptB = "receipt_22b_functional_" + token + "_b";
            statesByReceipt.Add(
                receiptA,
                new HashSet<BistroBuilderGoodsReceivingVisualState>()
            );
            statesByReceipt.Add(
                receiptB,
                new HashSet<BistroBuilderGoodsReceivingVisualState>()
            );

            bool acceptedA = receiving.TryReceiveGoods(
                receiptA,
                "supplier_22b_functional",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        600L
                    ),
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        800L
                    )
                },
                "Primera recepción funcional 2.2B.",
                out BistroBuilderGoodsReceiptSnapshot snapshotA,
                out error
            );
            Check(
                acceptedA && snapshotA != null && !snapshotA.WasReplayed &&
                snapshotA.WarehouseId ==
                    BistroBuilderGoodsReceivingIds.PrimaryWarehouse,
                "La primera recepción se acepta para el almacén genérico."
            );
            functionalReceivedDayIndex = snapshotA != null
                ? snapshotA.ReceivedDayIndex
                : 0;

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaAfterA
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patataAfterA
            );
            Check(
                merluzaAfterA.OnHandCanonicalMilliUnits ==
                    merluzaBefore + 600L &&
                patataAfterA.OnHandCanonicalMilliUnits ==
                    patataBefore + 800L,
                "La recepción autoritativa actualiza el stock sin depender de la animación."
            );

            bool acceptedB = receiving.TryReceiveGoods(
                receiptB,
                "supplier_22b_functional",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_sal",
                        500L
                    )
                },
                "Segunda recepción funcional 2.2B.",
                out BistroBuilderGoodsReceiptSnapshot snapshotB,
                out error
            );
            Check(
                acceptedB && snapshotB != null && !snapshotB.WasReplayed,
                "Una segunda recepción puede aceptarse mientras la anterior se representa."
            );

            Check(
                presentation.ActiveVisualCount == 1 &&
                presentation.PendingVisualCount == 1,
                "Dos recepciones se representan en serie con un único repartidor."
            );

            running = true;
            startedAt = EditorApplication.timeSinceStartup;
            status =
                "Recepciones aceptadas. Observando entrada, descarga y salida " +
                "de los dos repartos visuales...";
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            AbortAndRestore("ERROR: " + exception.Message);
        }
    }

    private void EditorTick()
    {
        if (!running || !Application.isPlaying)
        {
            return;
        }

        if (presentation == null)
        {
            AbortAndRestore("ERROR: se perdió la presentación 2.2B.");
            return;
        }

        maxActiveVisualCount = Math.Max(
            maxActiveVisualCount,
            presentation.ActiveVisualCount
        );

        if (presentation.ActiveVisual != null)
        {
            if (presentation.CurrentState ==
                    BistroBuilderGoodsReceivingVisualState.Entering ||
                presentation.CurrentState ==
                    BistroBuilderGoodsReceivingVisualState.GoingToWarehouse ||
                presentation.CurrentState ==
                    BistroBuilderGoodsReceivingVisualState.Unloading)
            {
                sawBoxesWhileCarrying |= presentation.ActiveVisual.HasBoxes;
            }

            if (presentation.CurrentState ==
                    BistroBuilderGoodsReceivingVisualState.ReturningToSupplyAccess ||
                presentation.CurrentState ==
                    BistroBuilderGoodsReceivingVisualState.Exiting)
            {
                sawNoBoxesAfterUnload |= !presentation.ActiveVisual.HasBoxes;
            }
        }

        if (EditorApplication.timeSinceStartup - startedAt >
            SafetyTimeoutSeconds)
        {
            AbortAndRestore(
                "ERROR: la representación visual excedió el tiempo de seguridad."
            );
            return;
        }

        if (completedVisuals >= 2 && !presentation.IsBusy &&
            presentation.ActiveVisualCount == 0)
        {
            CompleteFunctionalTest();
        }
    }

    private void HandleVisualStateChanged(
        BistroBuilderGoodsReceivingPresentation source,
        BistroBuilderGoodsReceiptSnapshot receipt,
        BistroBuilderGoodsReceivingVisualState state
    )
    {
        if (!running && string.IsNullOrWhiteSpace(receiptA))
        {
            return;
        }

        if (receipt == null ||
            !statesByReceipt.TryGetValue(
                receipt.ReceiptId,
                out HashSet<BistroBuilderGoodsReceivingVisualState> states
            ))
        {
            return;
        }

        states.Add(state);

        if ((state == BistroBuilderGoodsReceivingVisualState.Entering ||
             state == BistroBuilderGoodsReceivingVisualState.GoingToWarehouse ||
             state == BistroBuilderGoodsReceivingVisualState.Unloading) &&
            source.ActiveVisual != null)
        {
            sawBoxesWhileCarrying |= source.ActiveVisual.HasBoxes;
        }

        if ((state ==
                BistroBuilderGoodsReceivingVisualState.ReturningToSupplyAccess ||
             state == BistroBuilderGoodsReceivingVisualState.Exiting) &&
            source.ActiveVisual != null)
        {
            sawNoBoxesAfterUnload |= !source.ActiveVisual.HasBoxes;
        }

        if (state == BistroBuilderGoodsReceivingVisualState.Completed)
        {
            completedVisuals++;
        }
    }

    private void CompleteFunctionalTest()
    {
        running = false;
        string error = string.Empty;

        try
        {
            Check(
                HasFullVisualSequence(receiptA),
                "El primer repartidor entra, llega al almacén, descarga y sale."
            );
            Check(
                HasFullVisualSequence(receiptB),
                "El segundo reparto repite la secuencia completa sin solaparse."
            );
            Check(
                maxActiveVisualCount == 1,
                "Nunca existe más de un repartidor temporal simultáneo."
            );
            Check(
                sawBoxesWhileCarrying,
                "El repartidor transporta cajas durante entrada y llegada al almacén."
            );
            Check(
                sawNoBoxesAfterUnload,
                "Las cajas desaparecen al descargar antes de la salida."
            );
            Check(
                completedVisuals == 2 &&
                presentation.ActiveVisualCount == 0 &&
                presentation.PendingVisualCount == 0,
                "Los repartidores temporales se destruyen al abandonar suministros."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaAfter
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patataAfter
            );
            inventory.TryGetStockSnapshot(
                "ingredient_sal",
                out BistroBuilderInventoryStockSnapshot salAfter
            );
            Check(
                merluzaAfter.OnHandCanonicalMilliUnits == merluzaBefore + 600L &&
                patataAfter.OnHandCanonicalMilliUnits == patataBefore + 800L &&
                salAfter.OnHandCanonicalMilliUnits == salBefore + 500L,
                "Las dos recepciones conservan exactamente las cantidades aceptadas."
            );

            var transactions =
                new List<BistroBuilderInventoryTransactionSnapshot>();
            inventory.CopyTransactionsTo(transactions);
            bool receiptAInLedger = HasPurchaseOperation(transactions, receiptA, 2);
            bool receiptBInLedger = HasPurchaseOperation(transactions, receiptB, 1);
            Check(
                receiptAInLedger && receiptBInLedger,
                "El libro registra las dos recepciones como movimientos Purchase."
            );

            var lots = new List<BistroBuilderInventoryLotSnapshot>();
            inventory.CopyLotSnapshotsTo(lots);
            int functionalLots = 0;
            bool receivedToday = true;
            int receivedDay = Math.Max(1, functionalReceivedDayIndex);
            for (int index = 0; index < lots.Count; index++)
            {
                if (lots[index].SourceId == "supplier_22b_functional")
                {
                    functionalLots++;
                    receivedToday &= lots[index].ReceivedDayIndex == receivedDay;
                }
            }
            Check(
                functionalLots == 3 && receivedToday &&
                inventory.LotCount == lotsBefore + 3,
                "Cada ingrediente recibido crea un lote interno fechado en el día actual."
            );

            Check(
                inventory.TransactionCount == transactionsBefore + 3 &&
                inventory.ValidateRuntimeState(out error),
                "Inventario, lotes y libro permanecen coherentes tras las recepciones."
            );

            long revisionBeforeReplay = inventory.RuntimeRevision;
            int lotsBeforeReplay = inventory.LotCount;
            bool replayed = receiving.TryReceiveGoods(
                receiptA,
                "supplier_22b_functional",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        600L
                    ),
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_patata",
                        800L
                    )
                },
                "Repetición funcional idempotente.",
                out BistroBuilderGoodsReceiptSnapshot replayReceipt,
                out error
            );
            Check(
                replayed && replayReceipt != null && replayReceipt.WasReplayed &&
                inventory.RuntimeRevision == revisionBeforeReplay &&
                inventory.LotCount == lotsBeforeReplay &&
                !presentation.IsBusy,
                "Repetir una recepción no duplica inventario ni vuelve a crear repartidor."
            );

            bool restored = originalSnapshot != null &&
                inventory.TryReplaceFromRuntimeSnapshot(
                    originalSnapshot,
                    true,
                    out error
                );
            Check(
                restored,
                "La prueba restaura el snapshot original del inventario."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaRestored
            );
            inventory.TryGetStockSnapshot(
                "ingredient_patata",
                out BistroBuilderInventoryStockSnapshot patataRestored
            );
            inventory.TryGetStockSnapshot(
                "ingredient_sal",
                out BistroBuilderInventoryStockSnapshot salRestored
            );
            Check(
                restored &&
                merluzaRestored.OnHandCanonicalMilliUnits == merluzaBefore &&
                patataRestored.OnHandCanonicalMilliUnits == patataBefore &&
                salRestored.OnHandCanonicalMilliUnits == salBefore,
                "Las existencias reales quedan exactamente como antes de la prueba."
            );
            Check(
                restored && inventory.LotCount == lotsBefore &&
                inventory.TransactionCount == transactionsBefore &&
                inventory.ValidateRuntimeState(out error),
                "Lotes, movimientos y auditoría final quedan restaurados sin residuos."
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "Excepción al completar: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            UnsubscribePresentation();
            RestorePresentationSettings();
        }

        status = failed.Count == 0
            ? "PRUEBA FUNCIONAL 2.2B SUPERADA"
            : "PRUEBA FUNCIONAL 2.2B CON FALLOS";
        LogFinalReport();
        Repaint();
    }

    private bool HasFullVisualSequence(string receiptId)
    {
        if (string.IsNullOrWhiteSpace(receiptId) ||
            !statesByReceipt.TryGetValue(
                receiptId,
                out HashSet<BistroBuilderGoodsReceivingVisualState> states
            ))
        {
            return false;
        }

        return states.Contains(BistroBuilderGoodsReceivingVisualState.Entering) &&
               states.Contains(
                   BistroBuilderGoodsReceivingVisualState.GoingToWarehouse
               ) &&
               states.Contains(BistroBuilderGoodsReceivingVisualState.Unloading) &&
               states.Contains(
                   BistroBuilderGoodsReceivingVisualState.ReturningToSupplyAccess
               ) &&
               states.Contains(BistroBuilderGoodsReceivingVisualState.Exiting) &&
               states.Contains(BistroBuilderGoodsReceivingVisualState.Completed);
    }

    private static bool HasPurchaseOperation(
        List<BistroBuilderInventoryTransactionSnapshot> transactions,
        string operationId,
        int expectedCount
    )
    {
        int count = 0;
        for (int index = 0; index < transactions.Count; index++)
        {
            BistroBuilderInventoryTransactionSnapshot transaction =
                transactions[index];
            if (transaction.OperationId == operationId &&
                transaction.TransactionType ==
                    BistroBuilderInventoryTransactionType.Purchase)
            {
                count++;
            }
        }
        return count == expectedCount;
    }

    private void AbortAndRestore(string message)
    {
        running = false;
        status = message;
        UnsubscribePresentation();

        // La representación es puramente visual. Si la prueba se aborta a
        // mitad de una entrega, reiniciar el componente cancela la coroutine,
        // vacía su cola y destruye el actor temporal sin tocar inventario.
        if (presentation != null && presentation.IsBusy && presentation.enabled)
        {
            presentation.enabled = false;
            presentation.enabled = true;
        }

        RestorePresentationSettings();

        if (inventory != null && originalSnapshot != null)
        {
            if (!inventory.TryReplaceFromRuntimeSnapshot(
                    originalSnapshot,
                    true,
                    out string restoreError
                ))
            {
                failed.Add("No se pudo restaurar el inventario: " + restoreError);
            }
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            failed.Add(message);
        }
        LogFinalReport();
        Repaint();
    }

    private void CaptureAndAcceleratePresentation()
    {
        SerializedObject serialized = new SerializedObject(presentation);
        originalMovementSpeed =
            RequireProperty(serialized, "movementSpeed").floatValue;
        originalUnloadDuration =
            RequireProperty(serialized, "unloadDurationSeconds").floatValue;
        originalExteriorDistance =
            RequireProperty(serialized, "exteriorSpawnDistance").floatValue;
        originalArrivalDistance =
            RequireProperty(serialized, "arrivalDistance").floatValue;
        originalLogVisualFlow =
            RequireProperty(serialized, "logVisualFlow").boolValue;

        RequireProperty(serialized, "movementSpeed").floatValue = 18f;
        RequireProperty(serialized, "unloadDurationSeconds").floatValue = 0.12f;
        RequireProperty(serialized, "exteriorSpawnDistance").floatValue = 0.65f;
        RequireProperty(serialized, "arrivalDistance").floatValue = 0.04f;
        RequireProperty(serialized, "logVisualFlow").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        presentationSettingsCaptured = true;
    }

    private void RestorePresentationSettings()
    {
        if (!presentationSettingsCaptured || presentation == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(presentation);
        RequireProperty(serialized, "movementSpeed").floatValue =
            originalMovementSpeed;
        RequireProperty(serialized, "unloadDurationSeconds").floatValue =
            originalUnloadDuration;
        RequireProperty(serialized, "exteriorSpawnDistance").floatValue =
            originalExteriorDistance;
        RequireProperty(serialized, "arrivalDistance").floatValue =
            originalArrivalDistance;
        RequireProperty(serialized, "logVisualFlow").boolValue =
            originalLogVisualFlow;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        presentationSettingsCaptured = false;
    }

    private void SubscribePresentation()
    {
        UnsubscribePresentation();
        if (presentation != null)
        {
            presentation.VisualStateChanged += HandleVisualStateChanged;
        }
    }

    private void UnsubscribePresentation()
    {
        if (presentation != null)
        {
            presentation.VisualStateChanged -= HandleVisualStateChanged;
        }
    }

    private void RefreshReferences()
    {
        inventory = FindFirstObjectByType<BistroBuilderInventoryService>();
        receiving = FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
        presentation =
            FindFirstObjectByType<BistroBuilderGoodsReceivingPresentation>();
        route = FindFirstObjectByType<BistroBuilderGoodsReceivingRoute>();
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed.Add(message);
        }
        else
        {
            failed.Add(message);
        }
    }

    private void LogFinalReport()
    {
        if (completionLogged)
        {
            return;
        }
        completionLogged = true;

        var builder = new StringBuilder(8192);
        builder.AppendLine(
            failed.Count == 0
                ? "PRUEBA FUNCIONAL 2.2B SUPERADA"
                : "PRUEBA FUNCIONAL 2.2B CON FALLOS"
        );
        builder.AppendLine("Correctos: " + passed.Count);
        builder.AppendLine("Fallos: " + failed.Count);
        for (int index = 0; index < passed.Count; index++)
        {
            builder.AppendLine("- OK: " + passed[index]);
        }
        for (int index = 0; index < failed.Count; index++)
        {
            builder.AppendLine("- ERROR: " + failed[index]);
        }

        string report = builder.ToString().TrimEnd();
        if (failed.Count > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name
    )
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + name + "."
            );
        }
        return property;
    }
}
