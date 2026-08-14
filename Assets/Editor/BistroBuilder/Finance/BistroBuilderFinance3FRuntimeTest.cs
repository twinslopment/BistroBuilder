using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3FRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3F.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3F.Runtime.Result";
    private const double StartupTimeoutSeconds = 20d;
    private const double PopupCompletionTimeoutSeconds = 3d;

    private static readonly List<long> popupAmounts = new List<long>(16);

    private static double startupDeadline;
    private static double popupCompletionDeadline;
    private static int capturedErrors;
    private static long reportStartBalance;
    private static long reportObservedBalance;
    private static int reportObservedTransactions;
    private static int reportObservedPopups;

    private static BistroBuilderFinanceService finance;
    private static BistroBuilderMoneyPopupService popupService;
    private static RestaurantPlacementHistoryService history;
    private static RestaurantPlaceableLifecycleService lifecycle;
    private static RestaurantEditModeService editMode;
    private static RestaurantPlaceableObject originalPlaceable;
    private static RestaurantPlacementStateSnapshot originalState;
    private static BistroBuilderFinanceSnapshot baselineFinance;

    static BistroBuilderFinance3FRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Bistro Builder/Finanzas/3F - Prueba runtime real", false, 3053)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "La prueba runtime 3F ya está en ejecución.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "Sal de Play Mode antes de iniciar la prueba automática.",
                "Aceptar");
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3F",
                "No se pudo guardar la escena antes de la prueba.",
                "Aceptar");
            return;
        }

        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            startupDeadline =
                EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            EditorApplication.update -= TryRunWhenReady;
            EditorApplication.update += TryRunWhenReady;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            CleanupSubscriptions();
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(
                ResultKey,
                "Prueba cancelada antes de completar 3F.");
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string result = SessionState.GetString(ResultKey, string.Empty);
        if (string.IsNullOrEmpty(result))
        {
            return;
        }

        SessionState.EraseString(ResultKey);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3F",
            result,
            "Aceptar");
    }

    private static void TryRunWhenReady()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= TryRunWhenReady;
            return;
        }

        finance =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        BistroBuilderDiscretionaryFinanceService discretionary =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderDiscretionaryFinanceService>();
        BistroBuilderPlaceableFinanceBridge bridge =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderPlaceableFinanceBridge>();
        popupService =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderMoneyPopupService>();
        RestaurantPlaceableCreationService creation =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableCreationService>();
        RestaurantPlaceableDeletionService deletion =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableDeletionService>();
        history =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementHistoryService>();
        lifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableLifecycleService>();
        editMode =
            UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
        RestaurantPlaceableRegistry registry =
            UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
        BistroBuilderSupplierPurchaseFinanceBridge supplierFinance =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierPurchaseFinanceBridge>();

        bool ready =
            finance != null && finance.IsInitialized &&
            discretionary != null && discretionary.IsInitialized &&
            bridge != null && bridge.IsBound &&
            popupService != null &&
            creation != null &&
            deletion != null &&
            history != null &&
            lifecycle != null &&
            editMode != null &&
            registry != null && registry.RegisteredPlaceableCount > 0 &&
            supplierFinance != null && supplierFinance.IsBound;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= startupDeadline)
            {
                Fail("Las autoridades runtime de 3F no estuvieron listas a tiempo.");
            }
            return;
        }

        EditorApplication.update -= TryRunWhenReady;
        capturedErrors = 0;
        popupAmounts.Clear();

        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;
        popupService.PopupShown -= HandlePopupShown;
        popupService.PopupShown += HandlePopupShown;

        baselineFinance = finance.CreateSnapshot();
        if (baselineFinance == null)
        {
            Fail("No se pudo capturar el estado financiero inicial.");
            return;
        }

        if (!editMode.IsEditModeActive &&
            !editMode.TryEnterEditMode(out _, out string editError))
        {
            Fail("No se pudo abrir automáticamente el modo edición. " + editError);
            return;
        }

        if (!TryReserveRealPlacementSlot(
                registry,
                out RestaurantPlaceableItemDefinition baseDefinition,
                out Vector3 anchorPosition,
                out Quaternion rotation,
                out Transform parent,
                out string slotError))
        {
            Fail(slotError);
            return;
        }

        long startBalance = finance.CurrentBalanceCents;
        int startTransactions = finance.TransactionCount;

        if (!RunFurnitureCycle(
                creation,
                deletion,
                baseDefinition,
                anchorPosition,
                rotation,
                parent,
                startBalance,
                startTransactions,
                out string furnitureError))
        {
            Fail(furnitureError);
            return;
        }

        if (!RunStructuralCycle(
                creation,
                deletion,
                baseDefinition,
                anchorPosition,
                rotation,
                parent,
                startBalance,
                startTransactions,
                out string structuralError))
        {
            Fail(structuralError);
            return;
        }

        var marketing = new BistroBuilderDiscretionaryExpenseRequest
        {
            operationId = "finance_3f_runtime_marketing",
            sourceSystemId = "marketing_runtime_test",
            sourceReferenceId = "campaign_runtime_3f",
            categoryId = "expense.marketing.local",
            amountCents = 12345L,
            description = "Campaña local diagnóstica 3F."
        };

        if (!discretionary.TryPostExpense(
                marketing,
                out _,
                out string marketingError))
        {
            Fail(
                "El contrato real de Marketing rechazó un gasto válido. " +
                marketingError);
            return;
        }

        if (finance.CurrentBalanceCents != startBalance - 69845L ||
            finance.TransactionCount != startTransactions + 9 ||
            popupAmounts.Count != 8)
        {
            Fail(
                "El consolidado de compra, reventa, historial, demolición y Marketing no cuadra.");
            return;
        }

        if (!RunNoChargeGuards(
                creation,
                baseDefinition,
                anchorPosition,
                rotation,
                parent,
                out string guardError))
        {
            Fail(guardError);
            return;
        }

        reportStartBalance = startBalance;
        reportObservedBalance = finance.CurrentBalanceCents;
        reportObservedTransactions =
            finance.TransactionCount - startTransactions;
        reportObservedPopups = popupAmounts.Count;

        popupCompletionDeadline =
            EditorApplication.timeSinceStartup + PopupCompletionTimeoutSeconds;
        EditorApplication.update -= WaitForPopupAnimations;
        EditorApplication.update += WaitForPopupAnimations;
    }

    private static void WaitForPopupAnimations()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= WaitForPopupAnimations;
            return;
        }

        if (popupService == null)
        {
            Fail("El servicio de popup desapareció durante la animación.");
            return;
        }

        if (popupService.ActivePopupCount > 0)
        {
            if (EditorApplication.timeSinceStartup >= popupCompletionDeadline)
            {
                Fail(
                    "Los popups monetarios no completaron su animación y limpieza a tiempo.");
            }
            return;
        }

        EditorApplication.update -= WaitForPopupAnimations;

        if (capturedErrors != 0)
        {
            Fail(
                "La prueba funcional terminó con Error/Exception/Assert: " +
                capturedErrors + ".");
            return;
        }

        if (!RestoreDiagnosticState(out string cleanupError))
        {
            Fail(
                "La prueba fue correcta, pero no pudo limpiar su estado transitorio. " +
                cleanupError);
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3F SUPERADA" +
            "\n\nCompra mobiliario: -500,00 €" +
            "\nVenta al 50 %: +250,00 €" +
            "\nUndo venta: -250,00 €" +
            "\nRedo venta: +250,00 €" +
            "\nUndo venta (2): -250,00 €" +
            "\nUndo compra: +500,00 €" +
            "\nPistas Create/Delete independientes: OK" +
            "\nCompra obra: -500,00 €" +
            "\nDemolición 15 %: -75,00 €" +
            "\nMarketing contractual: -123,45 €" +
            "\nCaja diagnóstica: " + FormatMoney(reportStartBalance) +
            " → " + FormatMoney(reportObservedBalance) +
            "\nMovimientos financieros nuevos: " + reportObservedTransactions +
            "\nPopups monetarios confirmados: " + reportObservedPopups +
            "\nAnimación pop/subida/fade y autolimpieza: OK" +
            "\nPreview cancelado: 0 € / sin popup" +
            "\nFondos insuficientes: bloqueado antes de comprar" +
            "\nEstado inicial restaurado: OK" +
            "\nError/Exception/Assert: 0");
    }

    private static bool RunFurnitureCycle(
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion,
        RestaurantPlaceableItemDefinition baseDefinition,
        Vector3 anchorPosition,
        Quaternion rotation,
        Transform parent,
        long startBalance,
        int startTransactions,
        out string error)
    {
        error = string.Empty;
        RestaurantPlaceableItemDefinition furniture =
            CreateDiagnosticDefinition(
                baseDefinition,
                "finance_3f_runtime_furniture",
                "Mueble diagnóstico 3F",
                RestaurantPlaceableItemCategory.Furniture,
                RestaurantPlaceableDisposalMode.Resale,
                500,
                5000,
                0,
                1500);

        if (!TryCreateAndCommit(
                creation,
                furniture,
                anchorPosition,
                rotation,
                parent,
                out RestaurantPlaceableObject furnitureInstance,
                out error))
        {
            error = "Compra real de mobiliario falló. " + error;
            return false;
        }

        if (!CheckState(
                startBalance - 50000L,
                startTransactions + 1,
                -50000L,
                1))
        {
            error =
                "La compra real de 500 € no produjo caja, ledger y popup esperados.";
            return false;
        }

        if (!deletion.TryDelete(
                furnitureInstance,
                out RestaurantPlaceableDeletionResult saleResult))
        {
            error = "La venta real del mobiliario falló. " + saleResult.Message;
            return false;
        }

        if (!CheckState(
                startBalance - 25000L,
                startTransactions + 2,
                25000L,
                2))
        {
            error = "La reventa al 50 % no produjo +250 € exactamente una vez.";
            return false;
        }

        if (!history.TryUndo(out _, out _, out _) ||
            !CheckState(
                startBalance - 50000L,
                startTransactions + 3,
                -25000L,
                3))
        {
            error = "Undo de venta no restauró mundo y compensación -250 €.";
            return false;
        }

        if (!history.TryRedo(out _, out _, out _) ||
            !CheckState(
                startBalance - 25000L,
                startTransactions + 4,
                25000L,
                4))
        {
            error = "Redo de venta no reaplicó mundo y compensación +250 €.";
            return false;
        }

        if (!history.TryUndo(out _, out _, out _) ||
            !CheckState(
                startBalance - 50000L,
                startTransactions + 5,
                -25000L,
                5))
        {
            error = "El segundo Undo de venta no compensó -250 €.";
            return false;
        }

        if (!history.TryUndo(out _, out _, out _) ||
            !CheckState(
                startBalance,
                startTransactions + 6,
                50000L,
                6))
        {
            error =
                "Undo de compra no devolvió 500 €; las pistas Create/Delete se cruzaron.";
            return false;
        }

        return true;
    }

    private static bool RunStructuralCycle(
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion,
        RestaurantPlaceableItemDefinition baseDefinition,
        Vector3 anchorPosition,
        Quaternion rotation,
        Transform parent,
        long startBalance,
        int startTransactions,
        out string error)
    {
        error = string.Empty;
        RestaurantPlaceableItemDefinition structural =
            CreateDiagnosticDefinition(
                baseDefinition,
                "finance_3f_runtime_structural",
                "Obra diagnóstica 3F",
                RestaurantPlaceableItemCategory.Structural,
                RestaurantPlaceableDisposalMode.Demolition,
                500,
                5000,
                0,
                1500);

        if (!TryCreateAndCommit(
                creation,
                structural,
                anchorPosition,
                rotation,
                parent,
                out RestaurantPlaceableObject structuralInstance,
                out error))
        {
            error = "Compra real de obra falló. " + error;
            return false;
        }

        if (!CheckState(
                startBalance - 50000L,
                startTransactions + 7,
                -50000L,
                7))
        {
            error = "La inversión estructural no descontó 500 € con popup.";
            return false;
        }

        if (!deletion.TryDelete(
                structuralInstance,
                out RestaurantPlaceableDeletionResult demolitionResult))
        {
            error = "La demolición real falló. " + demolitionResult.Message;
            return false;
        }

        if (!CheckState(
                startBalance - 57500L,
                startTransactions + 8,
                -7500L,
                8))
        {
            error = "La demolición no descontó exactamente el 15 % = 75 €.";
            return false;
        }

        return true;
    }

    private static bool RunNoChargeGuards(
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableItemDefinition baseDefinition,
        Vector3 anchorPosition,
        Quaternion rotation,
        Transform parent,
        out string error)
    {
        error = string.Empty;
        int transactionsBefore = finance.TransactionCount;
        int popupsBefore = popupAmounts.Count;

        RestaurantPlaceableItemDefinition cancelDefinition =
            CreateDiagnosticDefinition(
                baseDefinition,
                "finance_3f_runtime_cancel",
                "Compra cancelada 3F",
                RestaurantPlaceableItemCategory.Furniture,
                RestaurantPlaceableDisposalMode.Resale,
                400,
                5000,
                0,
                1500);

        if (!creation.TryBeginCreation(
                cancelDefinition,
                anchorPosition,
                rotation,
                parent,
                out _,
                out RestaurantPlaceableCreationResult beginCancel))
        {
            error = "No se pudo preparar la cancelación. " + beginCancel.Message;
            return false;
        }

        if (!creation.TryCancelActiveCreation(
                out RestaurantPlaceableCreationResult cancelResult))
        {
            error = "No se pudo cancelar la previsualización. " + cancelResult.Message;
            return false;
        }

        if (finance.TransactionCount != transactionsBefore ||
            popupAmounts.Count != popupsBefore)
        {
            error =
                "Cancelar una previsualización generó dinero o popup indebidamente.";
            return false;
        }

        RestaurantPlaceableItemDefinition impossible =
            CreateDiagnosticDefinition(
                baseDefinition,
                "finance_3f_runtime_impossible",
                "Compra sin fondos 3F",
                RestaurantPlaceableItemCategory.Furniture,
                RestaurantPlaceableDisposalMode.Resale,
                1000000,
                5000,
                0,
                1500);

        if (!creation.TryBeginCreation(
                impossible,
                anchorPosition,
                rotation,
                parent,
                out _,
                out RestaurantPlaceableCreationResult beginImpossible))
        {
            error =
                "No se pudo preparar la prueba de fondos insuficientes. " +
                beginImpossible.Message;
            return false;
        }

        bool committed = creation.TryCommitActiveCreation(
            out RestaurantPlaceableCreationResult rejectedPurchase);
        bool cancelledAfterReject = creation.TryCancelActiveCreation(out _);

        if (committed ||
            rejectedPurchase.FailureReason !=
                RestaurantPlaceableCreationFailureReason.EconomyRejected ||
            !cancelledAfterReject ||
            finance.TransactionCount != transactionsBefore ||
            popupAmounts.Count != popupsBefore)
        {
            error =
                "Una compra sin fondos no fue rechazada limpiamente antes de alterar mundo/caja.";
            return false;
        }

        return true;
    }

    private static bool TryReserveRealPlacementSlot(
        RestaurantPlaceableRegistry registry,
        out RestaurantPlaceableItemDefinition definition,
        out Vector3 anchorPosition,
        out Quaternion rotation,
        out Transform parent,
        out string error)
    {
        definition = null;
        anchorPosition = Vector3.zero;
        rotation = Quaternion.identity;
        parent = null;
        error = string.Empty;

        var candidates =
            new List<RestaurantPlaceableObject>(registry.RegisteredPlaceables);
        candidates.Sort((left, right) =>
        {
            bool leftTable =
                left != null && left.GetComponent<RestaurantTable>() != null;
            bool rightTable =
                right != null && right.GetComponent<RestaurantTable>() != null;
            return leftTable.CompareTo(rightTable);
        });

        for (int index = 0; index < candidates.Count; index++)
        {
            RestaurantPlaceableObject candidate = candidates[index];
            if (candidate == null ||
                candidate.ItemDefinition == null ||
                candidate.ItemDefinition.Prefab == null)
            {
                continue;
            }

            Vector3 candidateAnchor = candidate.PlacementAnchor.position;
            Quaternion candidateRotation = candidate.transform.rotation;
            Transform candidateParent = candidate.transform.parent;

            if (!lifecycle.TryDeactivateInstance(
                    candidate,
                    out RestaurantPlacementStateSnapshot state,
                    out _))
            {
                continue;
            }

            originalPlaceable = candidate;
            originalState = state;
            definition = candidate.ItemDefinition;
            anchorPosition = candidateAnchor;
            rotation = candidateRotation;
            parent = candidateParent;
            return true;
        }

        error =
            "No se encontró un colocable real que pudiera ceder temporalmente su posición a 3F.";
        return false;
    }

    private static RestaurantPlaceableItemDefinition CreateDiagnosticDefinition(
        RestaurantPlaceableItemDefinition source,
        string itemId,
        string displayName,
        RestaurantPlaceableItemCategory category,
        RestaurantPlaceableDisposalMode disposalMode,
        int purchasePrice,
        int resaleBasisPoints,
        int removalCost,
        int demolitionBasisPoints)
    {
        RestaurantPlaceableItemDefinition clone =
            UnityEngine.Object.Instantiate(source);
        clone.name = itemId;

        var serialized = new SerializedObject(clone);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("purchasePrice").intValue = purchasePrice;
        serialized.FindProperty("disposalMode").enumValueIndex = (int)disposalMode;
        serialized.FindProperty("resaleBasisPoints").intValue = resaleBasisPoints;
        serialized.FindProperty("removalCost").intValue = removalCost;
        serialized.FindProperty("demolitionBasisPoints").intValue =
            demolitionBasisPoints;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return clone;
    }

    private static bool TryCreateAndCommit(
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableItemDefinition definition,
        Vector3 anchorPosition,
        Quaternion rotation,
        Transform parent,
        out RestaurantPlaceableObject placeable,
        out string error)
    {
        error = string.Empty;
        if (!creation.TryBeginCreation(
                definition,
                anchorPosition,
                rotation,
                parent,
                out placeable,
                out RestaurantPlaceableCreationResult begin))
        {
            error = begin.Message;
            return false;
        }

        if (!creation.TryCommitActiveCreation(
                out RestaurantPlaceableCreationResult commit))
        {
            creation.TryCancelActiveCreation(out _);
            error = commit.Message;
            return false;
        }

        return true;
    }

    private static bool CheckState(
        long expectedBalance,
        int expectedTransactions,
        long expectedLatestPopup,
        int expectedPopupCount)
    {
        return finance.CurrentBalanceCents == expectedBalance &&
               finance.TransactionCount == expectedTransactions &&
               popupAmounts.Count == expectedPopupCount &&
               popupAmounts[popupAmounts.Count - 1] == expectedLatestPopup;
    }

    private static bool RestoreDiagnosticState(out string error)
    {
        error = string.Empty;
        history?.ClearHistory();

        if (originalPlaceable != null &&
            originalState.IsValid &&
            !lifecycle.TryActivateInstance(
                originalPlaceable,
                originalState,
                out RestaurantPlaceableLifecycleResult restoreResult))
        {
            error =
                "No se restauró el colocable original. " + restoreResult.Message;
            return false;
        }

        if (finance != null &&
            baselineFinance != null &&
            !finance.TryRestoreSnapshot(baselineFinance, out error))
        {
            return false;
        }

        if (editMode != null && editMode.IsEditModeActive)
        {
            editMode.TryExitEditMode(true, out _);
        }

        return true;
    }

    private static void HandlePopupShown(long signedCents, Vector3 position)
    {
        popupAmounts.Add(signedCents);
    }

    private static void HandleLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert)
        {
            capturedErrors++;
        }
    }

    private static void Fail(string message)
    {
        EditorApplication.update -= WaitForPopupAnimations;
        RestoreDiagnosticState(out _);
        Complete("PRUEBA RUNTIME 3F NO SUPERADA\n\n" + message);
    }

    private static void Complete(string result)
    {
        CleanupSubscriptions();
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(ResultKey, result);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall +=
                () => EditorApplication.isPlaying = false;
        }
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= TryRunWhenReady;
        EditorApplication.update -= WaitForPopupAnimations;
        Application.logMessageReceived -= HandleLog;

        if (popupService != null)
        {
            popupService.PopupShown -= HandlePopupShown;
        }

        finance = null;
        popupService = null;
        history = null;
        lifecycle = null;
        editMode = null;
        originalPlaceable = null;
        originalState = default;
        baselineFinance = null;
        popupAmounts.Clear();
        reportStartBalance = 0L;
        reportObservedBalance = 0L;
        reportObservedTransactions = 0;
        reportObservedPopups = 0;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
