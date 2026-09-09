using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate 4G de frontera de autoridad.
///
/// El Queen Test puede leer directamente las autoridades canónicas para validar
/// snapshots y wiring, pero las mutaciones de jugador deben atravesar la fachada
/// 4F. El flujo operativo del servicio debe seguir usando ServiceState/Session y
/// nunca fabricar camareros, tareas, colas ni resultados de rendimiento.
/// </summary>
public static class BistroBuilderStaff4GAuthorityBoundarySelfTest
{
    private const string RunnerPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4GQueenTestWindow.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Gate frontera de autoridad",
        false,
        3264)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        if (!File.Exists(RunnerPath))
        {
            failed = 1;
            report =
                "4G — FRONTERA DE AUTORIDAD\n" +
                "[ERROR] Falta BistroBuilderStaff4GQueenTestWindow.cs.";
            return false;
        }

        string source = File.ReadAllText(RunnerPath);

        Require(
            source,
            "facade.TryHireCandidate(",
            "La contratación del Queen Test atraviesa la fachada 4F.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "facade.TrySetAvailability(",
            "Los cambios de disponibilidad atraviesan la fachada 4F.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "facade.TryTrainEmployee(",
            "La formación atraviesa la fachada 4F/4C.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "session.TryEnsureSessionStarted(",
            "El binding operativo se inicia mediante StaffSession 4D.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "serviceState.TryBeginPreparation()",
            "Preparing usa la autoridad canónica de estado de servicio.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "serviceState.TryOpenService()",
            "Open usa la autoridad canónica de estado de servicio.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "serviceState.TryBeginClosing()",
            "Closing usa la autoridad canónica de estado de servicio.",
            lines,
            ref passed,
            ref failed);
        Require(
            source,
            "serviceState.TryCompleteClosing()",
            "Closed usa la autoridad canónica de estado de servicio.",
            lines,
            ref passed,
            ref failed);

        Forbid(
            source,
            "recruitment.TryHireCandidate(",
            "El runner no salta la fachada para contratar.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "recruitment.TryDismissEmployee(",
            "El runner no salta la fachada para despedir.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "staff.TrySetAvailability(",
            "El runner no salta la fachada para disponibilidad.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "development.TryTrainEmployee(",
            "El runner no salta la fachada para formación.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "AddComponent<Waiter",
            "El runner no crea un segundo sistema de camareros.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "new WaiterTask(",
            "El runner no fabrica trabajo operativo.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            "new WaiterTaskQueue(",
            "El runner no duplica WaiterTaskCoordinator ni su cola.",
            lines,
            ref passed,
            ref failed);
        Forbid(
            source,
            ".TryApplyServiceResult(",
            "El runner no inyecta XP/rendimiento.",
            lines,
            ref passed,
            ref failed);

        report =
            "4G — FRONTERA DE AUTORIDAD\n" +
            string.Join("\n", lines) +
            "\n\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void Require(
        string source,
        string token,
        string text,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        Check(
            source.IndexOf(token, StringComparison.Ordinal) >= 0,
            text,
            lines,
            ref passed,
            ref failed);
    }

    private static void Forbid(
        string source,
        string token,
        string text,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        Check(
            source.IndexOf(token, StringComparison.Ordinal) < 0,
            text,
            lines,
            ref passed,
            ref failed);
    }

    private static void Check(
        bool condition,
        string text,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[ERROR] " + text);
        }
    }
}
