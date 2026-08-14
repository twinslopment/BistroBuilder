using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinance3BRuntimeTest
{
    private const string ArmedKey = "BB.Finance.3B.Runtime.Armed";
    private const string ResultKey = "BB.Finance.3B.Runtime.Result";

    private static BistroBuilderFinanceService finance;
    private static long baselineBalance;
    private static int baselineTransactions;
    private static int capturedErrors;

    static BistroBuilderFinance3BRuntimeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Bistro Builder/Finanzas/3B - Prueba runtime real", false, 3013)]
    private static void Run()
    {
        if (SessionState.GetBool(ArmedKey, false))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3B",
                "La prueba runtime 3B ya está esperando un cobro real.",
                "Aceptar");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            SessionState.SetBool(ArmedKey, true);
            SessionState.EraseString(ResultKey);
            ArmInPlayMode();
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3B",
                "No se pudo guardar la escena antes de iniciar la prueba.",
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
            EditorApplication.delayCall += ArmInPlayMode;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            Cleanup();
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetString(
                ResultKey,
                "Prueba cancelada antes de observar un cobro real.");
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string result = SessionState.GetString(ResultKey, string.Empty);
            if (!string.IsNullOrEmpty(result))
            {
                SessionState.EraseString(ResultKey);
                EditorUtility.DisplayDialog(
                    "Bistro Builder — 3B",
                    result,
                    "Aceptar");
            }
        }
    }

    private static void ArmInPlayMode()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false))
        {
            return;
        }

        BistroBuilderFinanceService[] services =
            UnityEngine.Object.FindObjectsByType<BistroBuilderFinanceService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (services.Length != 1)
        {
            Fail("La prueba necesita una única autoridad financiera runtime.");
            return;
        }

        finance = services[0];
        if (!finance.ValidateConfiguration(out string error))
        {
            Fail("La autoridad financiera no es válida. " + error);
            return;
        }

        baselineBalance = finance.CurrentBalanceCents;
        baselineTransactions = finance.TransactionCount;
        capturedErrors = 0;

        finance.TransactionPosted -= HandleTransactionPosted;
        finance.TransactionPosted += HandleTransactionPosted;
        finance.StateRestored -= HandleStateRestored;
        finance.StateRestored += HandleStateRestored;
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        Debug.Log(
            "3B — Prueba runtime armada. Esperando el próximo cobro real de mesa o barra.");
    }

    private static void HandleStateRestored()
    {
        if (finance == null)
        {
            return;
        }

        baselineBalance = finance.CurrentBalanceCents;
        baselineTransactions = finance.TransactionCount;
    }

    private static void HandleTransactionPosted(
        BistroBuilderFinanceTransactionRecord transaction)
    {
        if (transaction == null ||
            !string.Equals(
                transaction.sourceSystemId,
                BistroBuilderSalesRevenuePolicy.SourceSystemId,
                StringComparison.Ordinal) ||
            string.IsNullOrEmpty(transaction.categoryId) ||
            !transaction.categoryId.StartsWith("sales.", StringComparison.Ordinal))
        {
            return;
        }

        if (transaction.kind != BistroBuilderFinanceTransactionKind.Credit ||
            transaction.amountCents <= 0L)
        {
            Fail("El movimiento de venta observado no es un ingreso positivo.");
            return;
        }

        long expectedBalance = baselineBalance + transaction.amountCents;
        bool ledgerOk = finance.TransactionCount == baselineTransactions + 1;
        bool balanceOk = finance.CurrentBalanceCents == expectedBalance;

        if (!ledgerOk || !balanceOk || capturedErrors != 0)
        {
            Fail(
                "El cobro llegó a Finanzas, pero la verificación no fue limpia." +
                "\nTransacciones: " + baselineTransactions + " -> " +
                finance.TransactionCount +
                "\nCaja: " + FormatMoney(baselineBalance) + " -> " +
                FormatMoney(finance.CurrentBalanceCents) +
                "\nError/Exception/Assert: " + capturedErrors);
            return;
        }

        Complete(
            "PRUEBA RUNTIME 3B SUPERADA" +
            "\n\nCobro real: " + FormatMoney(transaction.amountCents) +
            "\nCategoría: " + transaction.categoryId +
            "\nCaja: " + FormatMoney(baselineBalance) + " -> " +
            FormatMoney(finance.CurrentBalanceCents) +
            "\nMovimientos: " + baselineTransactions + " -> " +
            finance.TransactionCount +
            "\nError/Exception/Assert: 0");
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
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
        Complete("PRUEBA RUNTIME 3B NO SUPERADA\n\n" + message);
    }

    private static void Complete(string result)
    {
        Cleanup();
        SessionState.SetBool(ArmedKey, false);
        SessionState.SetString(ResultKey, result);

        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
    }

    private static void Cleanup()
    {
        if (finance != null)
        {
            finance.TransactionPosted -= HandleTransactionPosted;
            finance.StateRestored -= HandleStateRestored;
        }

        Application.logMessageReceived -= HandleLog;
        finance = null;
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
