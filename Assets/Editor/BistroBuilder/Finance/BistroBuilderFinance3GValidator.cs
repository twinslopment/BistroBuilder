using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3GValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3G - Validar",
        false,
        3061)]
    private static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(
            out int passed,
            out int failed,
            out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3G",
            "Validación: " + passed + " correctos, " + failed + " errores.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3G — La validación ha fallado.");
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

        BistroBuilderFinancialResultsService results =
            FindSingle<BistroBuilderFinancialResultsService>(
                scene,
                "BistroBuilderFinancialResultsService",
                ref passed,
                ref failed,
                builder);
        BistroBuilderFinanceService finance =
            FindSingle<BistroBuilderFinanceService>(
                scene,
                "BistroBuilderFinanceService",
                ref passed,
                ref failed,
                builder);
        BistroBuilderProductCostService productCost =
            FindSingle<BistroBuilderProductCostService>(
                scene,
                "BistroBuilderProductCostService",
                ref passed,
                ref failed,
                builder);
        BistroBuilderGeneralGameStateService general =
            FindSingle<BistroBuilderGeneralGameStateService>(
                scene,
                "BistroBuilderGeneralGameStateService",
                ref passed,
                ref failed,
                builder);
        BistroBuilderMenuOfferService menu =
            FindSingle<BistroBuilderMenuOfferService>(
                scene,
                "BistroBuilderMenuOfferService",
                ref passed,
                ref failed,
                builder);
        RestaurantServiceStateService serviceState =
            FindSingle<RestaurantServiceStateService>(
                scene,
                "RestaurantServiceStateService",
                ref passed,
                ref failed,
                builder);

        Check(
            results != null && ReferenceEquals(results.FinanceService, finance),
            "3G referencia la autoridad financiera 3A.",
            ref passed,
            ref failed,
            builder);
        Check(
            results != null && ReferenceEquals(results.ProductCostService, productCost),
            "3G referencia la autoridad de costes 3D.",
            ref passed,
            ref failed,
            builder);
        Check(
            results != null && ReferenceEquals(results.GeneralGameStateService, general),
            "3G comparte el calendario canónico.",
            ref passed,
            ref failed,
            builder);
        Check(
            results != null && ReferenceEquals(results.MenuOfferService, menu),
            "3G comparte el servicio de comida canónico.",
            ref passed,
            ref failed,
            builder);
        Check(
            results != null && ReferenceEquals(results.ServiceStateService, serviceState),
            "3G comparte el estado operativo del restaurante.",
            ref passed,
            ref failed,
            builder);

        string configurationError = string.Empty;
        Check(
            results != null && results.ValidateConfiguration(out configurationError),
            "Configuración completa de 3G válida" +
            (string.IsNullOrWhiteSpace(configurationError)
                ? "."
                : ": " + configurationError),
            ref passed,
            ref failed,
            builder);

        builder.Insert(
            0,
            "3G — VALIDACIÓN RESULTADOS POR SERVICIO Y DÍA\n" +
            "Correctos: " + passed + "  Errores: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static T FindSingle<T>(
        Scene scene,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
        where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        T found = null;
        int count = 0;

        for (int index = 0; index < all.Length; index++)
        {
            T candidate = all[index];
            if (candidate == null || candidate.gameObject.scene != scene)
            {
                continue;
            }
            count++;
            found = candidate;
        }

        Check(
            count == 1,
            label + " único en escena.",
            ref passed,
            ref failed,
            builder);
        return count == 1 ? found : null;
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
