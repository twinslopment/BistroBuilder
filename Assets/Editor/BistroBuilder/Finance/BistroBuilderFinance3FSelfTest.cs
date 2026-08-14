using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3FSelfTest
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3F - Autotest", false, 3052)]
    public static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3F",
            "Autotest: " + passed + " correctos, " + failed + " fallos.",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("3F — El autotest ha fallado.");
        }
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        int capturedErrors = 0;
        var builder = new StringBuilder();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                capturedErrors++;
            }
        };

        Application.logMessageReceived += handler;
        try
        {
            RunPlaceablePolicyTests(ref passed, ref failed, builder);
            RunAtomicLedgerTests(ref passed, ref failed, builder);
            RunDiscretionaryContractTests(ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("[ERROR] Excepción inesperada: " + exception.Message);
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        Check(capturedErrors == 0,
            "Console sin Error/Exception/Assert durante autotest.",
            ref passed, ref failed, builder);

        builder.Insert(0,
            "3F — AUTOTEST MARKETING, OBRAS Y MEJORAS\nCorrectos: " + passed +
            "  Fallos: " + failed +
            "  Error/Exception/Assert: " + capturedErrors + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunPlaceablePolicyTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        RestaurantPlaceableItemDefinition definition =
            ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
        try
        {
            ConfigureDefinition(
                definition,
                RestaurantPlaceableItemCategory.Furniture,
                500,
                RestaurantPlaceableDisposalMode.Automatic,
                5000,
                0,
                1500);

            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCents(definition) == 50000L,
                "Compra de 500 € se normaliza a 50.000 céntimos.",
                ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.ResolveEffectiveDisposalMode(definition) ==
                  RestaurantPlaceableDisposalMode.Resale,
                "Mobiliario automático se revende.",
                ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
                    definition, 50000L, out var furniture, out _),
                "Vista previa de reventa válida.", ref passed, ref failed, builder);
            Check(furniture.ResaleCents == 25000L &&
                  furniture.RemovalCostCents == 0L &&
                  furniture.NetCashCents == 25000L,
                "Reventa base recupera exactamente el 50 %.",
                ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCategory(definition) ==
                  "investment.furniture",
                "Mobiliario usa investment.furniture.", ref passed, ref failed, builder);

            ConfigureDefinition(
                definition,
                RestaurantPlaceableItemCategory.Structural,
                500,
                RestaurantPlaceableDisposalMode.Automatic,
                5000,
                0,
                1500);
            Check(BistroBuilderPlaceableFinancePolicy.ResolveEffectiveDisposalMode(definition) ==
                  RestaurantPlaceableDisposalMode.Demolition,
                "Estructura automática se demuele.", ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
                    definition, 50000L, out var structural, out _),
                "Vista previa de demolición válida.", ref passed, ref failed, builder);
            Check(structural.RemovalCostCents == 7500L &&
                  structural.ResaleCents == 0L &&
                  structural.NetCashCents == -7500L,
                "Demolición base cuesta exactamente el 15 %.",
                ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCategory(definition) ==
                  "investment.renovation",
                "Estructura usa investment.renovation.", ref passed, ref failed, builder);

            ConfigureDefinition(
                definition,
                RestaurantPlaceableItemCategory.Structural,
                500,
                RestaurantPlaceableDisposalMode.Demolition,
                5000,
                120,
                1500);
            BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
                definition, 50000L, out var fixedDemolition, out _);
            Check(fixedDemolition.RemovalCostCents == 12000L,
                "Coste fijo de demolición prevalece sobre porcentaje.",
                ref passed, ref failed, builder);

            ConfigureDefinition(
                definition,
                RestaurantPlaceableItemCategory.KitchenEquipment,
                500,
                RestaurantPlaceableDisposalMode.ResaleWithRemovalCost,
                5000,
                40,
                1500);
            BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
                definition, 50000L, out var equipment, out _);
            Check(equipment.ResaleCents == 25000L &&
                  equipment.RemovalCostCents == 4000L &&
                  equipment.NetCashCents == 21000L,
                "Reventa con retirada conserva ambas patas y neto.",
                ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCategory(definition) ==
                  "investment.equipment",
                "Equipamiento usa investment.equipment.", ref passed, ref failed, builder);

            ConfigureDefinition(
                definition,
                RestaurantPlaceableItemCategory.Other,
                0,
                RestaurantPlaceableDisposalMode.None,
                0,
                0,
                0);
            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCents(definition) == 0L,
                "Artículo gratuito no inventa coste.", ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.TryBuildDisposalPreview(
                    definition, 0L, out var none, out _) && !none.HasFinancialEffect,
                "Retirada None no inventa ingreso ni gasto.", ref passed, ref failed, builder);
            Check(BistroBuilderPlaceableFinancePolicy.ResolvePurchaseCategory(definition) ==
                  "investment.improvement",
                "Otros colocables usan investment.improvement.", ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void RunAtomicLedgerTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject go = new GameObject("BB_3F_AtomicFinanceSelfTest");
        try
        {
            BistroBuilderFinanceService finance = go.AddComponent<BistroBuilderFinanceService>();
            Check(finance.TryInitializeFresh(out _),
                "Finance temporal inicializa.", ref passed, ref failed, builder);

            var batch = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("batch_credit", "test_3f", "asset_test", "income.asset_resale",
                    BistroBuilderFinanceTransactionKind.Credit, 1000L),
                Request("batch_debit", "test_3f", "asset_test", "expense.asset_removal",
                    BistroBuilderFinanceTransactionKind.Debit, 400L)
            };
            Check(finance.TryPostTransactions(batch, out var posted, out _),
                "Lote financiero de dos patas se publica atómicamente.",
                ref passed, ref failed, builder);
            Check(posted.Count == 2 && finance.TransactionCount == 2,
                "Lote crea exactamente dos movimientos.", ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5000600L,
                "Crédito 10 € y retirada 4 € producen neto +6 €.",
                ref passed, ref failed, builder);

            long replayRevision = finance.Revision;
            Check(finance.TryPostTransactions(batch, out _, out _) &&
                  finance.TransactionCount == 2 &&
                  finance.Revision == replayRevision,
                "Reintento íntegro del lote es idempotente.",
                ref passed, ref failed, builder);

            long balanceBeforeInvalid = finance.CurrentBalanceCents;
            int countBeforeInvalid = finance.TransactionCount;
            var invalid = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("atomic_new", "test_3f", "atomic_source", "investment.furniture",
                    BistroBuilderFinanceTransactionKind.Debit, 100L),
                Request("atomic_bad", "test_3f", "atomic_source", "investment.furniture",
                    BistroBuilderFinanceTransactionKind.Debit, 0L)
            };
            Check(!finance.TryPostTransactions(invalid, out _, out _),
                "Una pata inválida rechaza el lote completo.",
                ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == balanceBeforeInvalid &&
                  finance.TransactionCount == countBeforeInvalid,
                "Lote rechazado no deja estado financiero parcial.",
                ref passed, ref failed, builder);

            var duplicateIds = new List<BistroBuilderFinanceTransactionRequest>
            {
                Request("duplicate_leg", "test_3f", "dup_source", "test.category",
                    BistroBuilderFinanceTransactionKind.Credit, 100L),
                Request("duplicate_leg", "test_3f", "dup_source", "test.category",
                    BistroBuilderFinanceTransactionKind.Debit, 100L)
            };
            Check(!finance.TryPostTransactions(duplicateIds, out _, out _),
                "Un lote no admite OperationId duplicados.",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void RunDiscretionaryContractTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject go = new GameObject("BB_3F_DiscretionarySelfTest");
        try
        {
            GameClock clock = go.AddComponent<GameClock>();
            BistroBuilderFinanceService finance = go.AddComponent<BistroBuilderFinanceService>();
            BistroBuilderGeneralGameStateService general =
                go.AddComponent<BistroBuilderGeneralGameStateService>();
            BistroBuilderDiscretionaryFinanceService discretionary =
                go.AddComponent<BistroBuilderDiscretionaryFinanceService>();
            SetReference(discretionary, "financeService", finance);
            SetReference(discretionary, "generalGameStateService", general);
            SetReference(discretionary, "gameClock", clock);

            var marketing = new BistroBuilderDiscretionaryExpenseRequest
            {
                operationId = "marketing_test_3f",
                sourceSystemId = "marketing_test",
                sourceReferenceId = "campaign_local_test",
                categoryId = "expense.marketing.local",
                amountCents = 12345L,
                description = "Campaña local de prueba."
            };

            Check(BistroBuilderDiscretionaryFinancePolicy.TryValidateExpense(marketing, out _),
                "Contrato acepta gasto de Marketing.", ref passed, ref failed, builder);
            Check(discretionary.TryPostExpense(marketing, out var posted, out _),
                "Marketing publica un débito canónico.", ref passed, ref failed, builder);
            Check(posted != null &&
                  posted.kind == BistroBuilderFinanceTransactionKind.Debit &&
                  posted.categoryId == "expense.marketing.local" &&
                  posted.amountCents == 12345L,
                "Movimiento de Marketing conserva categoría e importe.",
                ref passed, ref failed, builder);

            int countAfterMarketing = finance.TransactionCount;
            Check(discretionary.TryPostExpense(marketing, out _, out _) &&
                  finance.TransactionCount == countAfterMarketing,
                "Reintento de campaña es idempotente.", ref passed, ref failed, builder);

            var invalidCategory = new BistroBuilderDiscretionaryExpenseRequest
            {
                operationId = "invalid_category_3f",
                sourceSystemId = "test_3f",
                sourceReferenceId = "invalid_source_3f",
                categoryId = "sales.invalid",
                amountCents = 100L
            };
            Check(!discretionary.TryPostExpense(invalidCategory, out _, out _),
                "Contrato rechaza categorías ajenas a 3F.", ref passed, ref failed, builder);

            long balanceBeforeInsufficient = finance.CurrentBalanceCents;
            var tooExpensive = new BistroBuilderDiscretionaryExpenseRequest
            {
                operationId = "too_expensive_3f",
                sourceSystemId = "marketing_test",
                sourceReferenceId = "campaign_impossible_test",
                categoryId = "expense.marketing",
                amountCents = balanceBeforeInsufficient + 1L
            };
            Check(!discretionary.TryPostExpense(tooExpensive, out _, out _),
                "Gasto discrecional sin fondos se bloquea antes del ledger.",
                ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == balanceBeforeInsufficient &&
                  finance.TransactionCount == countAfterMarketing,
                "Rechazo por fondos insuficientes no modifica caja ni ledger.",
                ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static BistroBuilderFinanceTransactionRequest Request(
        string operationId,
        string sourceSystemId,
        string sourceReferenceId,
        string categoryId,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            categoryId = categoryId,
            kind = kind,
            amountCents = amountCents,
            dayIndex = 1,
            minuteOfDay = 720,
            description = "3F autotest"
        };
    }

    private static void ConfigureDefinition(
        RestaurantPlaceableItemDefinition definition,
        RestaurantPlaceableItemCategory category,
        int purchasePrice,
        RestaurantPlaceableDisposalMode disposalMode,
        int resaleBasisPoints,
        int removalCost,
        int demolitionBasisPoints)
    {
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("purchasePrice").intValue = purchasePrice;
        serialized.FindProperty("disposalMode").enumValueIndex = (int)disposalMode;
        serialized.FindProperty("resaleBasisPoints").intValue = resaleBasisPoints;
        serialized.FindProperty("removalCost").intValue = removalCost;
        serialized.FindProperty("demolitionBasisPoints").intValue = demolitionBasisPoints;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
