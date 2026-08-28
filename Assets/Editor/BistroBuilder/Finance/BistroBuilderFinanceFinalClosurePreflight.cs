using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate acumulativo final del Bloque 3. Reúne la base endurecida 3A-3I y
/// los contratos estructurales/puros de la UI 3J sobre la escena actual.
/// </summary>
public static class BistroBuilderFinanceFinalClosurePreflight
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ReportPath = "Block3FinanceFinalPreflight.txt";

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - CIERRE FINAL 3A-3J / Preflight",
        false,
        3110)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — Finanzas",
            "Cierre 3A-3J: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool ok = Run(out _, out _, out string report);
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine(
            "=== BISTRO BUILDER — BLOQUE 3 / PREFLIGHT FINAL 3A-3J ===");

        bool hardeningValidation =
            BistroBuilderFinanceHardeningValidator.ValidateCurrentScene(
                out int hardeningValidationPassed,
                out int hardeningValidationFailed,
                out string hardeningValidationReport);
        Check(hardeningValidation && hardeningValidationFailed == 0,
            "3A-3I validación: " + hardeningValidationPassed +
            " OK / " + hardeningValidationFailed + " errores",
            ref passed, ref failed, builder);
        if (!hardeningValidation)
            builder.AppendLine(hardeningValidationReport);

        bool hardeningSelfTest = BistroBuilderFinanceHardeningSelfTest.Run(
            out int hardeningTestPassed,
            out int hardeningTestFailed,
            out string hardeningTestReport);
        Check(hardeningSelfTest && hardeningTestFailed == 0,
            "3A-3I autotest: " + hardeningTestPassed +
            " OK / " + hardeningTestFailed + " fallos",
            ref passed, ref failed, builder);
        if (!hardeningSelfTest)
            builder.AppendLine(hardeningTestReport);

        bool uiValidation = BistroBuilderFinance3JValidator.ValidateCurrentScene(
            out int uiValidationPassed,
            out int uiValidationFailed,
            out string uiValidationReport);
        Check(uiValidation && uiValidationFailed == 0,
            "3J validación: " + uiValidationPassed +
            " OK / " + uiValidationFailed + " errores",
            ref passed, ref failed, builder);
        if (!uiValidation)
            builder.AppendLine(uiValidationReport);

        bool uiSelfTest = BistroBuilderFinance3JSelfTest.Run(
            out int uiTestPassed,
            out int uiTestFailed,
            out string uiTestReport);
        Check(uiSelfTest && uiTestFailed == 0,
            "3J autotest: " + uiTestPassed +
            " OK / " + uiTestFailed + " fallos",
            ref passed, ref failed, builder);
        if (!uiSelfTest)
            builder.AppendLine(uiTestReport);

        BistroBuilderStaffPayrollFinanceBridge[] payrollBridges =
            UnityEngine.Object.FindObjectsByType<BistroBuilderStaffPayrollFinanceBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        bool payrollWiring = payrollBridges.Length == 1 &&
            payrollBridges[0] != null &&
            payrollBridges[0].ValidateConfiguration(out _) &&
            payrollBridges[0].OperatingExpenseService != null &&
            object.ReferenceEquals(
                payrollBridges[0].OperatingExpenseService.StaffPayrollFinanceBridge,
                payrollBridges[0]);
        Check(payrollWiring,
            "3E/4/5 nómina real y proyección salarial: wiring canónico único",
            ref passed, ref failed, builder);

        bool payrollIds =
            BistroBuilderStaffPayrollFinanceBridge.BuildScheduledPayrollRunId(
                1, BistroBuilderMealServiceAvailability.Breakfast) ==
                "staffpay_day_00000001_breakfast" &&
            BistroBuilderStaffPayrollFinanceBridge.BuildScheduledPayrollRunId(
                1, BistroBuilderMealServiceAvailability.Lunch) ==
                "staffpay_day_00000001_lunch" &&
            BistroBuilderStaffPayrollFinanceBridge.BuildScheduledPayrollRunId(
                1, BistroBuilderMealServiceAvailability.Dinner) ==
                "staffpay_day_00000001_dinner";
        Check(payrollIds,
            "Nómina Staff usa identidades estables por día/servicio",
            ref passed, ref failed, builder);
        builder.AppendLine();
        builder.AppendLine(
            "Resultado final: " + passed + " OK / " + failed + " fallos.");
        report = builder.ToString();
        return failed == 0;
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
            builder.Append("[OK] ").AppendLine(message);
        }
        else
        {
            failed++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
