using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3EValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3E - Validar gastos y nóminas",
        false,
        3041)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(
            out int passed,
            out int failed,
            out string report);

        Debug.Log(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 3E",
            "Validación: " + passed + " correctos, " +
            failed + " errores.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError(
                "3E — La validación de gastos y nóminas ha fallado.");
        }
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();

        Scene scene = SceneManager.GetActiveScene();

        Check(
            scene.IsValid() && scene.isLoaded,
            "Escena activa válida.",
            ref passed,
            ref failed,
            builder);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        bool finance3DValid =
            BistroBuilderFinance3DValidator.ValidateCurrentScene(
                out _,
                out int finance3DErrors,
                out _);

        Check(
            finance3DValid && finance3DErrors == 0,
            "3A/3B/3C/3D permanecen íntegros y válidos.",
            ref passed,
            ref failed,
            builder);

        GameObject gameSystems = FindGameSystems(scene);

        Check(
            gameSystems != null,
            "Existe GameSystems canónico.",
            ref passed,
            ref failed,
            builder);

        if (gameSystems == null)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        BistroBuilderOperatingExpenseService[] services =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderOperatingExpenseService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        Check(
            services.Length == 1,
            "Existe un único servicio 3E.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderOperatingExpenseService service =
            services.Length == 1 ? services[0] : null;

        Check(
            service != null && service.gameObject == gameSystems,
            "3E pertenece a GameSystems.",
            ref passed,
            ref failed,
            builder);

        bool referencesValid =
            service != null &&
            GetReference(service, "financeService")
                is BistroBuilderFinanceService &&
            GetReference(service, "generalGameStateService")
                is BistroBuilderGeneralGameStateService &&
            GetReference(service, "gameClock")
                is GameClock &&
            GetReference(service, "saveGameService")
                is BistroBuilderSaveGameService;

        Check(
            referencesValid,
            "3E referencia únicamente autoridades canónicas existentes.",
            ref passed,
            ref failed,
            builder);

        BistroBuilderOperatingExpenseProfile profile =
            service != null
                ? GetReference(service, "expenseProfile")
                    as BistroBuilderOperatingExpenseProfile
                : null;

        Check(
            profile != null &&
            string.Equals(
                AssetDatabase.GetAssetPath(profile),
                BistroBuilderFinance3EInstaller.ProfileAssetPath,
                StringComparison.Ordinal),
            "El perfil de gastos 3E es el asset canónico.",
            ref passed,
            ref failed,
            builder);

        string profileError = string.Empty;

        Check(
            profile != null &&
            profile.TryValidate(out profileError),
            "Perfil de gastos válido" +
            FormatError(profileError) + ".",
            ref passed,
            ref failed,
            builder);

        string serviceError = string.Empty;

        Check(
            service != null &&
            service.ValidateConfiguration(out serviceError),
            "Configuración de 3E válida" +
            FormatError(serviceError) + ".",
            ref passed,
            ref failed,
            builder);

        var payroll = new BistroBuilderPayrollBatchRequest
        {
            payrollRunId = "validator_payroll",
            periodStartDayIndex = 1,
            periodEndDayIndex = 7,
            employeeCount = 1,
            totalCents = 100L
        };

        bool payrollContractValid =
            BistroBuilderOperatingExpensePolicy
                .TryBuildPayrollTransactionRequest(
                    payroll,
                    7,
                    480,
                    out BistroBuilderFinanceTransactionRequest
                        payrollTransaction,
                    out _) &&
            payrollTransaction != null &&
            payrollTransaction.kind ==
                BistroBuilderFinanceTransactionKind.Debit &&
            payrollTransaction.categoryId ==
                BistroBuilderOperatingExpensePolicy
                    .PayrollCategoryId;

        Check(
            payrollContractValid,
            "El contrato de nómina produce un débito financiero válido.",
            ref passed,
            ref failed,
            builder);

        report = BuildReport(passed, failed, builder);
        return failed == 0;
    }

    private static UnityEngine.Object GetReference(
        UnityEngine.Object target,
        string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        var serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(fieldName);

        return property != null
            ? property.objectReferenceValue
            : null;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal))
            {
                return roots[index];
            }
        }

        return null;
    }

    private static string BuildReport(
        int passed,
        int failed,
        StringBuilder builder)
    {
        builder.Insert(
            0,
            "3E — VALIDACIÓN GASTOS OPERATIVOS Y NÓMINAS\n" +
            "Correctos: " + passed +
            "  Errores: " + failed + "\n\n");

        return builder.ToString();
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

    private static string FormatError(string error)
    {
        return string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : ": " + error;
    }
}
