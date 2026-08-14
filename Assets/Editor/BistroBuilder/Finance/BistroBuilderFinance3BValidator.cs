using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3BValidator
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3B - Validar ingresos por ventas", false, 3011)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3B",
            "Validación de ventas: " + passed + " correctos, " + failed + " errores.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3B — La validación de ingresos por ventas ha fallado.");
        }
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        StringBuilder builder = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida.", ref passed, ref failed, builder);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        bool financeCoreValid = BistroBuilderFinance3AValidator.ValidateCurrentScene(
            out _, out int financeCoreErrors, out _);
        Check(financeCoreValid && financeCoreErrors == 0,
            "3A permanece íntegro y válido.", ref passed, ref failed, builder);

        GameObject gameSystems = FindGameSystems(scene);
        Check(gameSystems != null,
            "Existe GameSystems canónico.", ref passed, ref failed, builder);
        if (gameSystems == null)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        BistroBuilderSalesRevenueBridge[] bridges =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSalesRevenueBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(bridges.Length == 1,
            "Existe un único bridge de ingresos 3B.", ref passed, ref failed, builder);

        BistroBuilderSalesRevenueBridge bridge =
            bridges.Length == 1 ? bridges[0] : null;
        Check(bridge != null && bridge.gameObject == gameSystems,
            "El bridge 3B pertenece a GameSystems.", ref passed, ref failed, builder);

        BistroBuilderFinanceService finance =
            gameSystems.GetComponent<BistroBuilderFinanceService>();
        Check(bridge != null && finance != null &&
              ReferenceEquals(bridge.FinanceService, finance),
            "3B escribe exclusivamente en la autoridad financiera 3A.",
            ref passed, ref failed, builder);

        Check(bridge != null &&
              bridge.OrderSystem != null &&
              bridge.BarServiceSystem != null &&
              bridge.GeneralGameStateService != null &&
              bridge.GameClock != null,
            "3B tiene enlazadas sus fuentes canónicas de servicio y tiempo.",
            ref passed, ref failed, builder);

        string bridgeError = string.Empty;
        bool bridgeValid = bridge != null &&
            bridge.ValidateConfiguration(out bridgeError);
        Check(bridgeValid,
            "Configuración de 3B válida" + FormatError(bridgeError) + ".",
            ref passed, ref failed, builder);

        report = BuildReport(passed, failed, builder);
        return failed == 0;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            GameObject root = roots[index];
            if (root != null && string.Equals(root.name, "GameSystems", StringComparison.Ordinal))
            {
                return root;
            }
        }
        return null;
    }

    private static string BuildReport(int passed, int failed, StringBuilder builder)
    {
        builder.Insert(0,
            "3B — VALIDACIÓN INGRESOS POR VENTAS\n" +
            "Correctos: " + passed + "  Errores: " + failed + "\n\n");
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
        return string.IsNullOrWhiteSpace(error) ? string.Empty : ": " + error;
    }
}
