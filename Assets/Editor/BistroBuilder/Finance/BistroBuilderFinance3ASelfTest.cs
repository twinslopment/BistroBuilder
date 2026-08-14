using System;
using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3ASelfTest
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3A - Autotest núcleo financiero", false, 3002)]
    public static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3A",
            "Autotest financiero: " + passed + " correctos, " + failed + " fallos.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3A — El autotest del núcleo financiero ha fallado.");
        }
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        StringBuilder builder = new StringBuilder();

        RunServiceTests(ref passed, ref failed, builder);
        RunOverflowTest(ref passed, ref failed, builder);
        RunProviderRoundTrip(ref passed, ref failed, builder);

        builder.Insert(0,
            "3A — AUTOTEST NÚCLEO FINANCIERO\n" +
            "Correctos: " + passed + "  Fallos: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunServiceTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject testObject = new GameObject("BB_3A_FinanceServiceSelfTest");
        try
        {
            BistroBuilderFinanceService finance =
                testObject.AddComponent<BistroBuilderFinanceService>();

            Check(finance.TryInitializeFresh(out _),
                "Servicio financiero inicializa.", ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5000000L,
                "Saldo inicial = 50.000,00 €.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest sale = Request(
                "test_sale_1", "orders", "order_test_1", "sales",
                BistroBuilderFinanceTransactionKind.Credit, 12000L, 1, 720, "Venta de prueba");
            Check(finance.TryPostTransaction(sale, out var saleRecord, out _),
                "Ingreso de 120,00 € aceptado.", ref passed, ref failed, builder);
            Check(saleRecord != null && saleRecord.sequence == 1L,
                "Primera transacción usa secuencia 1.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest expense = Request(
                "test_expense_1", "test", "expense_test_1", "operating_expense",
                BistroBuilderFinanceTransactionKind.Debit, 4500L, 1, 780, "Gasto de prueba");
            Check(finance.TryPostTransaction(expense, out _, out _),
                "Gasto de 45,00 € aceptado.", ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5007500L,
                "Saldo resultante = 50.075,00 €.", ref passed, ref failed, builder);

            long revisionBeforeRetry = finance.Revision;
            int countBeforeRetry = finance.TransactionCount;
            Check(finance.TryPostTransaction(sale, out var retryRecord, out _),
                "Reintento idéntico es idempotente.", ref passed, ref failed, builder);
            Check(finance.Revision == revisionBeforeRetry &&
                  finance.TransactionCount == countBeforeRetry &&
                  retryRecord != null && retryRecord.sequence == 1L,
                "El reintento no duplica movimiento ni revisión.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest conflict = Request(
                "test_sale_1", "orders", "order_test_1", "sales",
                BistroBuilderFinanceTransactionKind.Credit, 13000L, 1, 720, "Venta alterada");
            Check(!finance.TryPostTransaction(conflict, out _, out _),
                "OperationId repetido con otro contenido se rechaza.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest zero = Request(
                "test_zero", "test", "zero_test", "test",
                BistroBuilderFinanceTransactionKind.Debit, 0L, 1, 800, string.Empty);
            Check(!finance.TryPostTransaction(zero, out _, out _),
                "Importe cero se rechaza.", ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot snapshot = finance.CreateSnapshot();
            Check(snapshot != null && BistroBuilderFinanceEngine.TryValidateSnapshot(snapshot, out _),
                "Snapshot del servicio valida contra su ledger.", ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot tampered = snapshot.DeepClone();
            tampered.currentBalanceCents++;
            Check(!BistroBuilderFinanceEngine.TryValidateSnapshot(tampered, out _),
                "Manipulación del saldo se detecta contra el ledger.", ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot clone = snapshot.DeepClone();
            clone.transactions[0].description = "modificado";
            Check(!string.Equals(snapshot.transactions[0].description, clone.transactions[0].description, StringComparison.Ordinal),
                "DeepClone no comparte registros mutables.", ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static void RunOverflowTest(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        BistroBuilderFinanceSnapshot overflow =
            BistroBuilderFinanceEngine.CreateInitialSnapshot(long.MaxValue, "EUR");
        BistroBuilderFinanceTransactionRequest request = Request(
            "test_overflow", "test", "overflow_test", "test",
            BistroBuilderFinanceTransactionKind.Credit, 1L, 1, 900, string.Empty);

        Check(!BistroBuilderFinanceEngine.TryAppendNewTransaction(overflow, request, out _, out _),
            "Overflow monetario se rechaza.", ref passed, ref failed, builder);
        Check(overflow.currentBalanceCents == long.MaxValue &&
              overflow.transactions.Count == 0 &&
              overflow.nextTransactionSequence == 1L &&
              overflow.revision == 1L,
            "El fallo por overflow es atómico.", ref passed, ref failed, builder);
    }

    private static void RunProviderRoundTrip(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        GameObject testObject = new GameObject("BB_3A_FinancePersistenceSelfTest");
        try
        {
            BistroBuilderSaveGameService save = testObject.AddComponent<BistroBuilderSaveGameService>();
            BistroBuilderFinanceService finance = testObject.AddComponent<BistroBuilderFinanceService>();
            BistroBuilderFinanceSaveSectionProvider provider =
                testObject.AddComponent<BistroBuilderFinanceSaveSectionProvider>();

            Check(finance.TryInitializeFresh(out _),
                "Servicio financiero temporal inicializa.", ref passed, ref failed, builder);
            save.RefreshExtensions();
            Check(save.HasProvider(BistroBuilderFinanceSaveSectionProvider.StableSectionId),
                "finance.runtime se registra en SaveGameService.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest first = Request(
                "roundtrip_1", "test", "roundtrip_source_1", "test",
                BistroBuilderFinanceTransactionKind.Debit, 2500L, 2, 600, "Antes del snapshot");
            Check(finance.TryPostTransaction(first, out _, out _),
                "Servicio registra movimiento antes del snapshot.", ref passed, ref failed, builder);

            long expectedBalance = finance.CurrentBalanceCents;
            BistroBuilderSaveCaptureContext capture = new BistroBuilderSaveCaptureContext(99);
            Exhaust(provider.CaptureState(capture));
            Check(!capture.HasFailed && capture.State is BistroBuilderFinanceSnapshot,
                "Proveedor captura finance.runtime.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest second = Request(
                "roundtrip_2", "test", "roundtrip_source_2", "test",
                BistroBuilderFinanceTransactionKind.Credit, 9900L, 2, 660, "Después del snapshot");
            Check(finance.TryPostTransaction(second, out _, out _) &&
                  finance.CurrentBalanceCents != expectedBalance,
                "Estado financiero cambia después de capturar.", ref passed, ref failed, builder);

            BistroBuilderSaveLoadContext load = new BistroBuilderSaveLoadContext(99, false, 32);
            Exhaust(provider.PrepareForLoad(load));
            Exhaust(provider.ApplyState(capture.State, load));
            provider.FinalizeLoad(load);

            Check(!load.HasFailed,
                "Proveedor aplica snapshot sin error.", ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == expectedBalance && finance.TransactionCount == 1,
                "Round-trip restaura exactamente saldo y ledger.", ref passed, ref failed, builder);
            Check(finance.TryPostTransaction(first, out _, out _) && finance.TransactionCount == 1,
                "Idempotencia sigue activa después de restaurar.", ref passed, ref failed, builder);

            BistroBuilderFinanceTransactionRequest transient = Request(
                "roundtrip_transient", "test", "roundtrip_transient_source", "test",
                BistroBuilderFinanceTransactionKind.Debit, 100L, 3, 700, "Estado previo a save legacy");
            Check(finance.TryPostTransaction(transient, out _, out _) && finance.TransactionCount == 2,
                "Estado previo a un save legacy contiene movimientos.", ref passed, ref failed, builder);

            BistroBuilderSaveLoadContext legacyLoad = new BistroBuilderSaveLoadContext(100, false, 32);
            Exhaust(provider.PrepareForLoad(legacyLoad));
            provider.FinalizeLoad(legacyLoad);
            Check(!legacyLoad.HasFailed,
                "Save anterior a 3A inicializa Finanzas sin error.", ref passed, ref failed, builder);
            Check(finance.CurrentBalanceCents == 5000000L && finance.TransactionCount == 0,
                "Save anterior a 3A recibe estado financiero fresco.", ref passed, ref failed, builder);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static BistroBuilderFinanceTransactionRequest Request(
        string operationId,
        string sourceSystemId,
        string sourceReferenceId,
        string categoryId,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents,
        int dayIndex,
        int minuteOfDay,
        string description)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            categoryId = categoryId,
            kind = kind,
            amountCents = amountCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = description
        };
    }

    private static void Exhaust(IEnumerator routine)
    {
        while (routine != null && routine.MoveNext())
        {
        }
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }
}
