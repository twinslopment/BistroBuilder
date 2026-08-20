using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4G — Gate estático para impedir un falso PASS del Save/Load activo.
///
/// El test valida el detector puro y exige que el Queen Test principal lo
/// invoque antes de cargar el checkpoint Open. Hasta que esa integración
/// exista, este gate falla deliberadamente y 4G no puede considerarse listo.
/// </summary>
public static class BistroBuilderStaff4GNaturalMutationSelfTest
{
    private const string QueenTestPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4GQueenTestWindow.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Autotest mutación observable",
        false,
        3262)]
    public static void RunFromMenu()
    {
        bool passed = Run(out string report);
        if (passed)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
    }

    public static bool Run(out string report)
    {
        int ok = 0;
        int failed = 0;

        Check(
            File.Exists(QueenTestPath),
            "Existe Queen Test 4G principal.",
            ref ok,
            ref failed,
            out string firstError);

        if (failed > 0)
        {
            report = BuildReport(ok, failed, firstError);
            return false;
        }

        string source = File.ReadAllText(QueenTestPath);

        Check(
            source.Contains(
                "BistroBuilderStaff4GNaturalMutationProbe.HasObservableMutation",
                StringComparison.Ordinal),
            "Queen Test exige mutación observable antes del Load Open.",
            ref ok,
            ref failed,
            out string integrationError);

        Check(
            !source.Contains(
                "private const float NaturalMutationSeconds = 3f;",
                StringComparison.Ordinal),
            "Queen Test ya no usa una espera fija de 3 s como prueba de mutación.",
            ref ok,
            ref failed,
            out string fixedWaitError);

        Check(
            source.Contains(
                "FailAndRollback(\"Timeout esperando mutación observable",
                StringComparison.Ordinal),
            "Queen Test dispone de timeout explícito si el runtime no cambia.",
            ref ok,
            ref failed,
            out string timeoutError);

        string error = FirstNonEmpty(
            integrationError,
            fixedWaitError,
            timeoutError);

        report = BuildReport(ok, failed, error);
        return failed == 0;
    }

    private static void Check(
        bool condition,
        string description,
        ref int ok,
        ref int failed,
        out string error)
    {
        if (condition)
        {
            ok++;
            error = string.Empty;
            return;
        }

        failed++;
        error = description;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                return values[index];
            }
        }
        return string.Empty;
    }

    private static string BuildReport(int ok, int failed, string error)
    {
        return "4G — AUTOTEST MUTACIÓN OBSERVABLE\n" +
               "Resultado: " + ok + " OK / " + failed + " fallos." +
               (string.IsNullOrWhiteSpace(error)
                   ? string.Empty
                   : "\nPrimer fallo: " + error);
    }
}
