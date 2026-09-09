using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4G del ciclo de servicio usado por el Queen Test.
///
/// No ejecuta gameplay ni altera escenas. Protege que la prueba final atraviese
/// el ciclo canónico Closed -> Preparing -> Open -> Closing -> Closed mediante
/// RestaurantServiceStateService y que 4D se limite al binding/finalización
/// de Personal, sin crear una segunda autoridad de servicio o de tareas.
/// </summary>
public static class BistroBuilderStaff4GServiceLifecycleSelfTest
{
    private const string RunnerPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4GQueenTestWindow.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Autotest ciclo de servicio",
        false,
        3265)]
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
            report =
                "4G — CICLO DE SERVICIO\n[ERROR] Falta el runner Queen Test.";
            failed = 1;
            return false;
        }

        string source = File.ReadAllText(RunnerPath);

        Check(
            source.Contains("RestaurantServiceStateService serviceState") &&
            source.Contains("WaiterTaskCoordinator waiterCoordinator") &&
            source.Contains("BistroBuilderStaffSessionService session"),
            "El runner conserva las autoridades canónicas de servicio, tareas y sesión.",
            lines, ref passed, ref failed);

        Check(
            Ordered(
                source,
                "serviceState.TryBeginPreparation()",
                "session.TryEnsureSessionStarted(out error)",
                "session.TryGetAssignmentView(",
                "serviceState.TryOpenService()"),
            "Preparing inicia antes del binding 4D y Open solo después de confirmar asignación real.",
            lines, ref passed, ref failed);

        Check(
            source.Contains("phase = Phase.WaitingRealWork") &&
            source.Contains("view.completedTasks > 0") &&
            Ordered(
                source,
                "view.completedTasks > 0",
                "CaptureActiveState();",
                "phase = Phase.SavingActiveCheckpoint"),
            "El checkpoint Open solo se guarda tras observar trabajo real de 4D.",
            lines, ref passed, ref failed);

        Check(
            Ordered(
                source,
                "ValidateActiveLoadAndBeginClosing()",
                "serviceState.TryBeginClosing()",
                "phase = Phase.WaitingCloseReady"),
            "Tras Load Open, el cierre comienza por RestaurantServiceStateService.",
            lines, ref passed, ref failed);

        Check(
            source.Contains("waiterCoordinator.ActiveTaskCount == 0") &&
            source.Contains("AreBoundWaitersIdle()") &&
            Ordered(
                source,
                "waiterCoordinator.ActiveTaskCount == 0 && AreBoundWaitersIdle()",
                "serviceState.TryCompleteClosing()",
                "ValidateClosedSessionAndSave();"),
            "Closing solo completa cuando no quedan tareas activas y los camareros ligados están Idle.",
            lines, ref passed, ref failed);

        Check(
            source.Contains("if (session.HasActiveSession)") &&
            source.Contains("session.TryFinalizeClosedSession(out replayError)") &&
            source.Contains("beforeReplay") &&
            source.Contains("JsonUtility.ToJson(staff.CreateSnapshot())"),
            "Closed exige sesión 4D inactiva y comprueba finalización idempotente.",
            lines, ref passed, ref failed);

        Check(
            !source.Contains("new RestaurantServiceStateService") &&
            !source.Contains("new WaiterTaskCoordinator") &&
            !source.Contains("new Waiter(") &&
            !source.Contains("new WaiterTask("),
            "El Queen Test no instancia autoridades, camareros ni tareas paralelas.",
            lines, ref passed, ref failed);

        Check(
            !System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bCurrentState\s*=(?!=)\s*RestaurantServiceState") &&
            !System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bActiveTaskCount\s*=(?!=)") &&
            !System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bcompletedTasks\s*\+\+") &&
            !System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bexperiencePoints\s*\+="),
            "El runner no fuerza estado de servicio, tareas, métricas ni XP.",
            lines, ref passed, ref failed);

        report =
            "4G — AUTOTEST CICLO DE SERVICIO\n" +
            string.Join("\n", lines) +
            "\n\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static bool Ordered(string source, params string[] tokens)
    {
        int cursor = 0;
        for (int index = 0; index < tokens.Length; index++)
        {
            int found = source.IndexOf(tokens[index], cursor, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }
            cursor = found + tokens[index].Length;
        }
        return true;
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
