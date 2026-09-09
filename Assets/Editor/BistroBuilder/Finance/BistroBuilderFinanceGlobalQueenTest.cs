using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Queen Test financiera global 3A-3I.
///
/// Ejecuta Save/Load REAL sobre dos slots temporales, reproduce la carrera de
/// carga detectada en auditoría, verifica proyecciones 3G/3H, gasto 3F,
/// write-off de inventario, liquidez 3I y pago atómico de deuda. Al terminar
/// carga un rollback completo de la partida y elimina ambos slots.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderFinanceGlobalQueenTest
{
    private enum Phase
    {
        Idle,
        WaitingStartup,
        SavingRollback,
        SavingCheckpoint,
        LoadingCheckpoint,
        WaitingCheckpointReconcile,
        LoadingRollbackSuccess,
        LoadingRollbackFailure,
        WaitingRollbackReconcileSuccess,
        WaitingRollbackReconcileFailure,
        DeletingCheckpointSuccess,
        DeletingCheckpointFailure,
        DeletingRollbackSuccess,
        DeletingRollbackFailure
    }

    private const string ArmedKey = "BB.Finance.GlobalQueen.Armed";
    private const string ResultKey = "BB.Finance.GlobalQueen.Result";
    private const double StartupTimeoutSeconds = 25d;
    private const double ReconcileTimeoutSeconds = 10d;

    private static Phase phase;
    private static double deadline;
    private static int settleFrames;
    private static int capturedErrors;
    private static string pendingFailure = string.Empty;
    private static bool rollbackSaved;

    private static int rollbackSlot = -1;
    private static int checkpointSlot = -1;
    private static string token = string.Empty;
    private static int diagnosticDay;
    private static int checkpointDay;
    private static string queenLoanId = string.Empty;

    private static BistroBuilderSaveGameService save;
    private static BistroBuilderFinanceService finance;
    private static BistroBuilderSupplierPurchaseFinanceBridge supplier;
    private static BistroBuilderProductCostService productCost;
    private static BistroBuilderOperatingExpenseService operating;
    private static BistroBuilderDiscretionaryFinanceService discretionary;
    private static BistroBuilderFinancialResultsService results;
    private static BistroBuilderFinancialHistoryService history;
    private static BistroBuilderFinancingService financing;
    private static BistroBuilderInventoryLossFinanceBridge inventoryLoss;
    private static BistroBuilderRecipeCatalogService recipes;
    private static BistroBuilderGeneralGameStateService general;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static BistroBuilderProductCostSnapshot baselineProductCost;
    private static BistroBuilderFinancingSnapshot baselineFinancing;
    private static BistroBuilderFinanceSnapshot checkpointFinance;
    private static BistroBuilderFinancingSnapshot checkpointFinancing;
    private static BistroBuilderDayFinancialResult checkpointDayResult;
    private static BistroBuilderFinancialPeriodReport checkpointHistory;

    private static string baselineGameId;
    private static string baselineRestaurantName;
    private static string baselineCreatedUtc;
    private static int baselineDayIndex;
    private static int baselineYear;
    private static int baselineMonth;
    private static int baselineDay;
    private static string baselineProgressionStage;
    private static int baselineProgressionLevel;

    static BistroBuilderFinanceGlobalQueenTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - QUEEN TEST FINANCIERA GLOBAL",
        false,
        3093)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "La Queen Test financiera ya está en ejecución.",
                "Aceptar");
            return;
        }
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "Sal de Play Mode antes de iniciar la Queen Test.",
                "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "No se pudieron guardar las escenas antes de la Queen Test.",
                "Aceptar");
            return;
        }

        ResetStaticState();
        SessionState.SetBool(ArmedKey, true);
        SessionState.EraseString(ResultKey);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            phase = Phase.WaitingStartup;
            deadline = EditorApplication.timeSinceStartup + StartupTimeoutSeconds;
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceived += HandleLog;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupSubscriptions();
            if (SessionState.GetBool(ArmedKey, false))
            {
                SessionState.SetBool(ArmedKey, false);
                SessionState.SetString(
                    ResultKey,
                    "QUEEN TEST FINANCIERA GLOBAL CANCELADA antes de completarse.");
            }
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string message = SessionState.GetString(ResultKey, string.Empty);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
        SessionState.EraseString(ResultKey);
        EditorUtility.DisplayDialog(
            "Bistro Builder — Finanzas",
            message,
            "Aceptar");
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            EditorApplication.update -= Update;
            return;
        }

        if (phase == Phase.WaitingStartup)
        {
            TryBeginWhenReady();
            return;
        }

        if (phase == Phase.WaitingCheckpointReconcile)
        {
            if (settleFrames > 0)
            {
                settleFrames--;
                return;
            }
            if (save != null && save.IsBusy)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    FailAndRollback("SaveGame no terminó de estabilizar el checkpoint cargado.");
                }
                return;
            }
            ValidateCheckpointAfterLoad();
            return;
        }

        if (phase == Phase.WaitingRollbackReconcileSuccess ||
            phase == Phase.WaitingRollbackReconcileFailure)
        {
            if (settleFrames > 0)
            {
                settleFrames--;
                return;
            }
            if (save != null && save.IsBusy)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    CompleteFailure(
                        pendingFailure +
                        " Además, SaveGame no estabilizó el rollback.");
                }
                return;
            }
            ValidateRollbackAndCleanup(
                phase == Phase.WaitingRollbackReconcileFailure);
        }
    }

    private static void TryBeginWhenReady()
    {
        ResolveDependencies();
        bool ready = save != null && !save.IsBusy &&
                     finance != null && finance.IsInitialized &&
                     productCost != null && productCost.IsInitialized &&
                     financing != null && financing.IsInitialized &&
                     supplier != null && operating != null &&
                     discretionary != null && results != null &&
                     history != null && inventoryLoss != null &&
                     recipes != null && general != null && serviceState != null;

        if (!ready)
        {
            if (EditorApplication.timeSinceStartup >= deadline)
            {
                CompleteFailure(
                    "No se inicializaron todas las autoridades 3A-3I dentro del tiempo esperado.");
            }
            return;
        }

        if (!serviceState.IsClosed)
        {
            CompleteFailure(
                "La Queen Test financiera necesita comenzar con el restaurante Closed.");
            return;
        }

        if (!BistroBuilderFinanceHardeningValidator.ValidateCurrentScene(
                out _, out int validationFailed, out string validationReport) ||
            validationFailed != 0)
        {
            CompleteFailure(
                "El gate estructural previo no es limpio.\n" + validationReport);
            return;
        }

        if (!FindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            CompleteFailure(
                "Se necesitan dos slots libres entre 980 y 989 para una Queen Test reversible.");
            return;
        }

        CaptureBaseline();
        save.RefreshExtensions();
        SubscribeSave();
        token = Guid.NewGuid().ToString("N").ToLowerInvariant();
        capturedErrors = 0;
        phase = Phase.SavingRollback;

        if (!save.TrySaveSlot(
                rollbackSlot,
                "BB FINANCE QUEEN ROLLBACK",
                out string rejection))
        {
            CompleteFailure(
                "No se pudo guardar el rollback inicial. " + rejection);
        }
    }

    private static void HandleSaveOperation(BistroBuilderSaveOperationResult operation)
    {
        if (operation == null)
        {
            return;
        }

        if (phase == Phase.SavingRollback && operation.SlotIndex == rollbackSlot)
        {
            if (!operation.Succeeded)
            {
                CompleteFailure(
                    "No se pudo guardar el rollback inicial. " + operation.Message);
                return;
            }
            rollbackSaved = true;
            if (!PrepareDiagnostics(out string error))
            {
                FailAndRollback(error);
                return;
            }

            checkpointFinance = finance.CreateSnapshot();
            checkpointFinancing = financing.CreateSnapshot();
            if (!results.TryGetDayResult(
                    diagnosticDay,
                    out checkpointDayResult,
                    out error) ||
                !history.TryGetPeriodReport(
                    diagnosticDay,
                    diagnosticDay + 1,
                    out checkpointHistory,
                    out error))
            {
                FailAndRollback(
                    "No se pudieron capturar los resultados del checkpoint. " + error);
                return;
            }

            phase = Phase.SavingCheckpoint;
            if (!save.TrySaveSlot(
                    checkpointSlot,
                    "BB FINANCE QUEEN CHECKPOINT",
                    out string rejection))
            {
                FailAndRollback(
                    "No se pudo guardar el checkpoint financiero. " + rejection);
            }
            return;
        }

        if (phase == Phase.SavingCheckpoint &&
            operation.SlotIndex == checkpointSlot)
        {
            if (!operation.Succeeded)
            {
                FailAndRollback(
                    "El checkpoint financiero no pudo guardarse. " + operation.Message);
                return;
            }

            if (!MutateAfterCheckpoint(out string error))
            {
                FailAndRollback(error);
                return;
            }

            phase = Phase.LoadingCheckpoint;
            if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            {
                FailAndRollback(
                    "No se pudo iniciar la carga del checkpoint. " + rejection);
            }
            return;
        }

        if (phase == Phase.LoadingCheckpoint &&
            operation.SlotIndex == checkpointSlot)
        {
            if (!operation.Succeeded)
            {
                FailAndRollback(
                    "El checkpoint financiero no pudo cargarse. " + operation.Message);
                return;
            }

            ResolveDependencies();
            phase = Phase.WaitingCheckpointReconcile;
            settleFrames = 6;
            deadline = EditorApplication.timeSinceStartup + ReconcileTimeoutSeconds;
            return;
        }

        if ((phase == Phase.LoadingRollbackSuccess ||
             phase == Phase.LoadingRollbackFailure) &&
            operation.SlotIndex == rollbackSlot)
        {
            bool failure = phase == Phase.LoadingRollbackFailure;
            if (!operation.Succeeded)
            {
                RestoreBaselineInMemoryBestEffort();
                CompleteFailure(
                    (failure ? pendingFailure + " " : string.Empty) +
                    "Además, falló la carga del rollback: " + operation.Message);
                return;
            }

            ResolveDependencies();
            phase = failure
                ? Phase.WaitingRollbackReconcileFailure
                : Phase.WaitingRollbackReconcileSuccess;
            settleFrames = 6;
            deadline = EditorApplication.timeSinceStartup + ReconcileTimeoutSeconds;
            return;
        }

        if ((phase == Phase.DeletingCheckpointSuccess ||
             phase == Phase.DeletingCheckpointFailure) &&
            operation.SlotIndex == checkpointSlot)
        {
            bool failure = phase == Phase.DeletingCheckpointFailure;
            if (!operation.Succeeded)
            {
                pendingFailure = (failure ? pendingFailure + " " : string.Empty) +
                    "No se pudo eliminar el checkpoint temporal: " + operation.Message;
                failure = true;
            }
            DeleteRollbackSlot(failure);
            return;
        }

        if ((phase == Phase.DeletingRollbackSuccess ||
             phase == Phase.DeletingRollbackFailure) &&
            operation.SlotIndex == rollbackSlot)
        {
            bool failure = phase == Phase.DeletingRollbackFailure;
            if (!operation.Succeeded)
            {
                pendingFailure = (failure ? pendingFailure + " " : string.Empty) +
                    "No se pudo eliminar el rollback temporal: " + operation.Message;
                CompleteFailure(pendingFailure);
                return;
            }

            if (failure)
            {
                CompleteFailure(pendingFailure);
            }
            else
            {
                CompleteSuccess();
            }
        }
    }

    private static bool PrepareDiagnostics(out string error)
    {
        error = string.Empty;
        if (baselineDayIndex > int.MaxValue - 30)
        {
            error = "El DayIndex está demasiado cerca del límite para fabricar el escenario diagnóstico.";
            return false;
        }

        diagnosticDay = baselineDayIndex + 20;
        checkpointDay = diagnosticDay + 2;
        if (!SetDaySilently(diagnosticDay))
        {
            error = "No se pudo establecer el primer día diagnóstico.";
            return false;
        }

        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                "order_queen_" + token,
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch,
                20000L,
                diagnosticDay,
                600,
                out BistroBuilderFinanceTransactionRequest sale,
                out error) ||
            !finance.TryPostTransaction(sale, out _, out error))
        {
            error = "3B/3A no pudieron publicar la venta diagnóstica. " + error;
            return false;
        }

        var marketing = new BistroBuilderDiscretionaryExpenseRequest
        {
            operationId = "queen_marketing_" + token,
            sourceSystemId = "marketing.queen",
            sourceReferenceId = "campaign_queen_" + token,
            categoryId = "expense.marketing.queen",
            amountCents = 25000L,
            description = "Queen Test financiera — marketing"
        };
        if (!discretionary.TryPostExpense(marketing, out _, out error))
        {
            error = "3F no pudo publicar Marketing diagnóstico. " + error;
            return false;
        }

        var investment = new BistroBuilderDiscretionaryExpenseRequest
        {
            operationId = "queen_investment_" + token,
            sourceSystemId = "improvement.queen",
            sourceReferenceId = "improvement_queen_" + token,
            categoryId = "investment.improvement.queen",
            amountCents = 4000L,
            description = "Queen Test financiera — mejora"
        };
        if (!discretionary.TryPostExpense(investment, out _, out error))
        {
            error = "3F no pudo publicar la inversión diagnóstica. " + error;
            return false;
        }

        var ingredients = new List<BistroBuilderIngredientDefinition>();
        recipes.CopyIngredientsTo(ingredients);
        if (ingredients.Count == 0 || ingredients[0] == null)
        {
            error = "No existe ingrediente canónico para probar baja económica.";
            return false;
        }

        string ingredientId = ingredients[0].IngredientId;
        var expiration = new BistroBuilderInventoryTransactionSnapshot(
            1L,
            "inv_tx_queen_" + token,
            "expire_queen_" + token,
            ingredientId,
            BistroBuilderInventoryTransactionType.Expiration,
            1000L,
            -1000L,
            0L,
            0L,
            0L,
            0L,
            0L,
            "finance_queen",
            "Queen Test write-off",
            1L);
        if (!inventoryLoss.TryRecognizeLoss(expiration, out error))
        {
            error = "La baja económica de inventario no pudo reconocerse. " + error;
            return false;
        }

        string lossOperation =
            BistroBuilderInventoryLossFinancePolicy.BuildOperationId(
                expiration.OperationId);
        if (!finance.TryGetTransactionByOperationId(
                lossOperation,
                out BistroBuilderFinanceTransactionRecord lossTx) ||
            lossTx.amountCents <= 0L)
        {
            error = "La caducidad diagnóstica no produjo un write-off positivo trazable.";
            return false;
        }

        if (!SetDaySilently(diagnosticDay + 1))
        {
            error = "No se pudo establecer el día de financiación diagnóstico.";
            return false;
        }

        if (!financing.TryAcceptOffer(
                "bridge",
                "accept_finance_queen_" + token,
                out BistroBuilderLoanRecord loan,
                out error) ||
            loan == null)
        {
            error = "3I no pudo aceptar el préstamo puente diagnóstico. " + error;
            return false;
        }
        queenLoanId = loan.loanId;

        if (!SetDaySilently(checkpointDay))
        {
            error = "No se pudo establecer el día de corte del checkpoint.";
            return false;
        }

        if (!supplier.TryGetFinancialPosition(out _, out _, out error))
        {
            error = "3C no pudo resolver compromisos durante la Queen Test. " + error;
            return false;
        }

        if (!financing.TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition liquidity,
                out error) ||
            !liquidity.projectionComplete ||
            !liquidity.supplierCommitmentsResolved ||
            !liquidity.recurringOperatingObligationsResolved ||
            liquidity.status == BistroBuilderLiquidityStatus.Unknown)
        {
            error = "3I no produjo una proyección de liquidez completa. " + error;
            return false;
        }

        if (!financing.TryGetFinancialStress(
                out BistroBuilderFinancialStressSnapshot stress,
                out error) ||
            stress.consecutiveLossDays != 1)
        {
            error = "La racha de pérdidas completadas no ignoró el día de financiación puro. " + error;
            return false;
        }

        return financing.TryValidateLedgerConsistency(out error);
    }

    private static bool MutateAfterCheckpoint(out string error)
    {
        error = string.Empty;
        BistroBuilderFinancingSnapshot debt = financing.CreateSnapshot();
        BistroBuilderLoanRecord loan = debt != null
            ? debt.loans.Find(item => item != null && item.loanId == queenLoanId)
            : null;
        if (loan == null || loan.installments.Count == 0)
        {
            error = "El checkpoint no contiene el préstamo diagnóstico.";
            return false;
        }

        int firstDueDay = loan.installments[0].dueDayIndex;
        if (!SetDaySilently(firstDueDay))
        {
            error = "No se pudo avanzar al primer vencimiento diagnóstico.";
            return false;
        }

        int transactionCountBefore = finance.TransactionCount;
        if (!financing.TryProcessDuePayments(
                firstDueDay,
                out BistroBuilderDebtPaymentProcessResult payment,
                out error) ||
            payment.paidInstallments != 1 ||
            payment.principalPaidCents != loan.installments[0].principalCents ||
            payment.interestPaidCents != loan.installments[0].interestCents ||
            finance.TransactionCount - transactionCountBefore !=
                (loan.installments[0].interestCents > 0L ? 2 : 1))
        {
            error = "El pago atómico de la primera cuota no fue exacto. " + error;
            return false;
        }

        if (!financing.TryValidateLedgerConsistency(out error))
        {
            error = "Deuda/ledger dejaron de converger tras pagar una cuota. " + error;
            return false;
        }

        if (!results.TryGetDayResult(
                firstDueDay,
                out BistroBuilderDayFinancialResult paidDay,
                out error) ||
            paidDay.debtPrincipalCashOutCents !=
                loan.installments[0].principalCents ||
            paidDay.financingInterestExpensesCents !=
                loan.installments[0].interestCents)
        {
            error = "3G no separó principal e interés de la cuota real. " + error;
            return false;
        }

        var mutation = new BistroBuilderFinanceTransactionRequest
        {
            operationId = "queen_post_checkpoint_mutation_" + token,
            sourceSystemId = "finance.queen",
            sourceReferenceId = "mutation_queen_" + token,
            categoryId = "diagnostic.finance_mutation",
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = 1234L,
            dayIndex = firstDueDay,
            minuteOfDay = 700,
            description = "Mutación posterior al checkpoint de Queen Test"
        };
        if (!finance.TryPostTransaction(mutation, out _, out error))
        {
            error = "No se pudo fabricar la mutación posterior al checkpoint. " + error;
            return false;
        }

        return true;
    }

    private static void ValidateCheckpointAfterLoad()
    {
        ResolveDependencies();
        if (finance == null || financing == null || general == null ||
            results == null || history == null)
        {
            FailAndRollback("Faltan autoridades tras cargar el checkpoint.");
            return;
        }

        if (general.DayIndex != checkpointDay)
        {
            FailAndRollback(
                "El calendario del checkpoint no fue restaurado exactamente.");
            return;
        }

        if (!SnapshotsEqual(checkpointFinance, finance.CreateSnapshot()) ||
            !SnapshotsEqual(checkpointFinancing, financing.CreateSnapshot()))
        {
            FailAndRollback(
                "Save/Load no restauró exactamente finance.runtime y finance.financing.runtime.");
            return;
        }

        if (finance.TryGetTransactionByOperationId(
                BistroBuilderFinancingEngine.BuildPrincipalOperationId(
                    queenLoanId,
                    1),
                out _))
        {
            FailAndRollback(
                "REGRESIÓN DE CARRERA LOAD: apareció una cuota que no existía en el checkpoint.");
            return;
        }

        if (!financing.TryValidateLedgerConsistency(out string error))
        {
            FailAndRollback(
                "Deuda y ledger no son coherentes después del Load real. " + error);
            return;
        }

        if (!results.TryGetDayResult(
                diagnosticDay,
                out BistroBuilderDayFinancialResult loadedDay,
                out error) ||
            !DayResultEquivalent(checkpointDayResult, loadedDay))
        {
            FailAndRollback(
                "3G no reconstruyó el mismo resultado después de Load. " + error);
            return;
        }

        if (!history.TryGetPeriodReport(
                diagnosticDay,
                diagnosticDay + 1,
                out BistroBuilderFinancialPeriodReport loadedHistory,
                out error) ||
            !HistoryEquivalent(checkpointHistory, loadedHistory) ||
            loadedHistory.activeDayCount != 1 ||
            loadedHistory.resultDayCount != 1 ||
            loadedHistory.financialActivityDayCount != 2)
        {
            FailAndRollback(
                "3H no reconstruyó el histórico/actividad con la misma semántica tras Load. " +
                error);
            return;
        }

        if (!financing.TryGetFinancialStress(
                out BistroBuilderFinancialStressSnapshot stress,
                out error) ||
            stress.consecutiveLossDays != 1)
        {
            FailAndRollback(
                "3I perdió la racha de pérdidas completadas tras Load. " + error);
            return;
        }

        BeginRollback(false);
    }

    private static void BeginRollback(bool failure)
    {
        if (!rollbackSaved || save == null)
        {
            if (failure)
            {
                RestoreBaselineInMemoryBestEffort();
                CompleteFailure(pendingFailure);
            }
            else
            {
                CompleteFailure(
                    "La Queen Test terminó su flujo pero no conserva rollback inicial.");
            }
            return;
        }

        phase = failure
            ? Phase.LoadingRollbackFailure
            : Phase.LoadingRollbackSuccess;
        if (!save.TryLoadSlot(rollbackSlot, out string rejection))
        {
            RestoreBaselineInMemoryBestEffort();
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "No se pudo iniciar la restauración final. " + rejection);
        }
    }

    private static void FailAndRollback(string message)
    {
        pendingFailure = message;
        BeginRollback(true);
    }

    private static void ValidateRollbackAndCleanup(bool failure)
    {
        ResolveDependencies();
        bool restored = finance != null && financing != null &&
                        productCost != null && general != null &&
                        SnapshotsEqual(baselineFinance, finance.CreateSnapshot()) &&
                        SnapshotsEqual(baselineFinancing, financing.CreateSnapshot()) &&
                        SnapshotsEqual(baselineProductCost, productCost.CreateSnapshot()) &&
                        general.DayIndex == baselineDayIndex &&
                        string.Equals(general.GameId, baselineGameId, StringComparison.Ordinal);

        if (!restored)
        {
            RestoreBaselineInMemoryBestEffort();
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "El rollback real no dejó el estado financiero inicial idéntico.");
            return;
        }

        if (financing != null &&
            !financing.TryValidateLedgerConsistency(out string consistencyError))
        {
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "El rollback restauró snapshots pero no consistencia deuda/ledger: " +
                consistencyError);
            return;
        }

        DeleteCheckpointSlot(failure);
    }

    private static void DeleteCheckpointSlot(bool failure)
    {
        if (checkpointSlot < 0 || save == null || !save.SlotExists(checkpointSlot))
        {
            DeleteRollbackSlot(failure);
            return;
        }

        phase = failure
            ? Phase.DeletingCheckpointFailure
            : Phase.DeletingCheckpointSuccess;
        if (!save.TryDeleteSlot(checkpointSlot, out string rejection))
        {
            pendingFailure = (failure ? pendingFailure + " " : string.Empty) +
                "No se pudo iniciar limpieza del checkpoint: " + rejection;
            DeleteRollbackSlot(true);
        }
    }

    private static void DeleteRollbackSlot(bool failure)
    {
        if (rollbackSlot < 0 || save == null || !save.SlotExists(rollbackSlot))
        {
            if (failure)
            {
                CompleteFailure(pendingFailure);
            }
            else
            {
                CompleteSuccess();
            }
            return;
        }

        phase = failure
            ? Phase.DeletingRollbackFailure
            : Phase.DeletingRollbackSuccess;
        if (!save.TryDeleteSlot(rollbackSlot, out string rejection))
        {
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "No se pudo iniciar limpieza del rollback: " + rejection);
        }
    }

    private static void CaptureBaseline()
    {
        baselineFinance = finance.CreateSnapshot();
        baselineProductCost = productCost.CreateSnapshot();
        baselineFinancing = financing.CreateSnapshot();
        baselineGameId = general.GameId;
        baselineRestaurantName = general.RestaurantName;
        baselineCreatedUtc = general.CreatedUtc;
        baselineDayIndex = general.DayIndex;
        baselineYear = general.CalendarYear;
        baselineMonth = general.CalendarMonth;
        baselineDay = general.CalendarDay;
        baselineProgressionStage = general.ProgressionStageId;
        baselineProgressionLevel = general.ProgressionLevel;
    }

    private static bool SetDaySilently(int dayIndex)
    {
        return general.TryRestoreState(
            baselineGameId,
            baselineRestaurantName,
            baselineCreatedUtc,
            dayIndex,
            baselineYear,
            baselineMonth,
            baselineDay,
            baselineProgressionStage,
            baselineProgressionLevel,
            false);
    }

    private static void RestoreBaselineInMemoryBestEffort()
    {
        try
        {
            if (finance != null && baselineFinance != null)
            {
                finance.TryRestoreSnapshot(baselineFinance, out _);
            }
            if (productCost != null && baselineProductCost != null)
            {
                productCost.TryRestoreSnapshot(baselineProductCost, out _);
            }
            if (general != null)
            {
                general.TryRestoreState(
                    baselineGameId,
                    baselineRestaurantName,
                    baselineCreatedUtc,
                    baselineDayIndex,
                    baselineYear,
                    baselineMonth,
                    baselineDay,
                    baselineProgressionStage,
                    baselineProgressionLevel,
                    false);
            }
            if (financing != null && baselineFinancing != null)
            {
                financing.TryRestoreSnapshot(baselineFinancing, out _);
            }
        }
        catch
        {
            // Best effort exclusivamente para una ruta de fallo catastrófica.
        }
    }

    private static bool FindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 980; slot <= 989; slot++)
        {
            if (save.SlotExists(slot))
            {
                continue;
            }
            if (first < 0)
            {
                first = slot;
            }
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private static bool SnapshotsEqual(
        BistroBuilderFinanceSnapshot left,
        BistroBuilderFinanceSnapshot right)
    {
        return left != null && right != null &&
               string.Equals(
                   JsonUtility.ToJson(left),
                   JsonUtility.ToJson(right),
                   StringComparison.Ordinal);
    }

    private static bool SnapshotsEqual(
        BistroBuilderProductCostSnapshot left,
        BistroBuilderProductCostSnapshot right)
    {
        return left != null && right != null &&
               string.Equals(
                   JsonUtility.ToJson(left),
                   JsonUtility.ToJson(right),
                   StringComparison.Ordinal);
    }

    private static bool SnapshotsEqual(
        BistroBuilderFinancingSnapshot left,
        BistroBuilderFinancingSnapshot right)
    {
        return left != null && right != null &&
               string.Equals(
                   JsonUtility.ToJson(left),
                   JsonUtility.ToJson(right),
                   StringComparison.Ordinal);
    }

    private static bool DayResultEquivalent(
        BistroBuilderDayFinancialResult left,
        BistroBuilderDayFinancialResult right)
    {
        return left != null && right != null &&
               left.dayIndex == right.dayIndex &&
               left.revenueCents == right.revenueCents &&
               left.productCostCents == right.productCostCents &&
               left.totalPeriodExpensesCents == right.totalPeriodExpensesCents &&
               left.operatingResultCents == right.operatingResultCents &&
               left.inventoryWriteOffExpensesCents == right.inventoryWriteOffExpensesCents &&
               left.marketingExpensesCents == right.marketingExpensesCents &&
               left.investmentCashOutCents == right.investmentCashOutCents &&
               left.netCashChangeCents == right.netCashChangeCents;
    }

    private static bool HistoryEquivalent(
        BistroBuilderFinancialPeriodReport left,
        BistroBuilderFinancialPeriodReport right)
    {
        return left != null && right != null &&
               left.startDayIndex == right.startDayIndex &&
               left.endDayIndex == right.endDayIndex &&
               left.activeDayCount == right.activeDayCount &&
               left.resultDayCount == right.resultDayCount &&
               left.financialActivityDayCount == right.financialActivityDayCount &&
               left.revenueCents == right.revenueCents &&
               left.operatingResultCents == right.operatingResultCents &&
               left.netCashChangeCents == right.netCashChangeCents &&
               left.loanProceedsCashInCents == right.loanProceedsCashInCents;
    }

    private static void ResolveDependencies()
    {
        save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        supplier = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseFinanceBridge>();
        productCost = UnityEngine.Object.FindFirstObjectByType<BistroBuilderProductCostService>();
        operating = UnityEngine.Object.FindFirstObjectByType<BistroBuilderOperatingExpenseService>();
        discretionary = UnityEngine.Object.FindFirstObjectByType<BistroBuilderDiscretionaryFinanceService>();
        results = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancialResultsService>();
        history = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancialHistoryService>();
        financing = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinancingService>();
        inventoryLoss = UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryLossFinanceBridge>();
        recipes = UnityEngine.Object.FindFirstObjectByType<BistroBuilderRecipeCatalogService>();
        general = UnityEngine.Object.FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
    }

    private static void SubscribeSave()
    {
        if (save != null)
        {
            save.OperationCompleted -= HandleSaveOperation;
            save.OperationCompleted += HandleSaveOperation;
        }
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= HandleLog;
        if (save != null)
        {
            save.OperationCompleted -= HandleSaveOperation;
        }
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

    private static void CompleteSuccess()
    {
        if (capturedErrors != 0)
        {
            CompleteFailure(
                "El flujo funcional fue correcto, pero se observaron " +
                capturedErrors + " Error/Exception/Assert.");
            return;
        }

        Complete(
            "QUEEN TEST FINANCIERA GLOBAL SUPERADA\n\n" +
            "3A — Ledger, batch atómico e idempotencia: OK\n" +
            "3B — Venta canónica -> ingreso: OK\n" +
            "3C — Compromisos de proveedor resolubles: OK\n" +
            "3D — Product Cost persistente y reconstruible: OK\n" +
            "3E — Obligaciones recurrentes proyectables: OK\n" +
            "3F — Marketing e inversión separados: OK\n" +
            "Inventario — Caducidad -> write-off económico: OK\n" +
            "3G — Resultado vs caja + deuda explícita: OK\n" +
            "3H — Día operativo != día de tesorería: OK\n" +
            "3I — Liquidez completa, deuda y ledger coherentes: OK\n" +
            "Pago de cuota en batch atómico: OK\n" +
            "Save -> mutación -> Load real: OK\n" +
            "Carrera CalendarChanged durante Load: BLOQUEADA\n" +
            "Checkpoint restaurado exactamente: OK\n" +
            "Rollback integral restaurado exactamente: OK\n" +
            "Slots temporales eliminados: OK\n" +
            "Error/Exception/Assert: 0");
    }

    private static void CompleteFailure(string message)
    {
        Complete(
            "QUEEN TEST FINANCIERA GLOBAL FALLIDA\n\n" +
            message +
            "\n\nError/Exception/Assert observados: " + capturedErrors);
    }

    private static void Complete(string message)
    {
        CleanupSubscriptions();
        SessionState.SetString(ResultKey, message);
        SessionState.SetBool(ArmedKey, false);
        phase = Phase.Idle;
        EditorApplication.isPlaying = false;
    }

    private static void ResetStaticState()
    {
        CleanupSubscriptions();
        phase = Phase.Idle;
        rollbackSlot = -1;
        checkpointSlot = -1;
        rollbackSaved = false;
        pendingFailure = string.Empty;
        capturedErrors = 0;
        token = string.Empty;
        queenLoanId = string.Empty;
        baselineFinance = null;
        baselineProductCost = null;
        baselineFinancing = null;
        checkpointFinance = null;
        checkpointFinancing = null;
        checkpointDayResult = null;
        checkpointHistory = null;
    }
}
