using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4G del protocolo reversible del Queen Test.
///
/// No ejecuta Save/Load ni modifica la partida. Protege la secuencia de
/// rollback, verificación y limpieza de slots diagnósticos para impedir que
/// una futura edición del runner deje mutaciones o slots huérfanos.
/// </summary>
public static class BistroBuilderStaff4GRollbackSafetySelfTest
{
    private const string RunnerPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4GQueenTestWindow.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Autotest seguridad rollback",
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
                "4G — SEGURIDAD ROLLBACK\n[ERROR] Falta el runner Queen Test.";
            failed = 1;
            return false;
        }

        string source = File.ReadAllText(RunnerPath);

        Check(
            source.Contains("for (int slot = 980; slot <= 989; slot++)"),
            "Los slots diagnósticos quedan confinados al rango 980–989.",
            lines, ref passed, ref failed);
        Check(
            source.Contains("if (save.SlotExists(slot))") &&
            source.Contains("FindTwoFreeSlots(out rollbackSlot, out checkpointSlot)"),
            "Solo usa dos slots diagnósticos previamente libres.",
            lines, ref passed, ref failed);
        Check(
            Ordered(
                source,
                "FindTwoFreeSlots(out rollbackSlot, out checkpointSlot)",
                "CaptureInitialState();",
                "phase = Phase.SavingRollback;",
                "save.TrySaveSlot("),
            "Captura estado y guarda rollback antes de mutar gameplay.",
            lines, ref passed, ref failed);
        Check(
            Ordered(
                source,
                "if (!result.Succeeded)",
                "ExecutePreServiceMutations();") &&
            source.Contains("phase == Phase.SavingRollback"),
            "Las mutaciones comienzan únicamente tras confirmar el rollback.",
            lines, ref passed, ref failed);
        Check(
            source.Contains("FailAndRollback(") &&
            source.Contains("BeginRollback(true)"),
            "Los fallos posteriores al rollback vuelven por la ruta reversible.",
            lines, ref passed, ref failed);
        Check(
            Ordered(
                source,
                "ValidateClosedLoad();",
                "BeginRollback(false);") &&
            source.Contains("Phase.LoadingRollbackSuccess"),
            "El éxito también restaura el rollback antes de finalizar.",
            lines, ref passed, ref failed);
        Check(
            source.Contains("ValidateRollbackRestored(out restoreError)") &&
            source.Contains("CurrentWaiterCount() != initialWaiterCount") &&
            source.Contains("JsonEquals(initialStaffJson, staff.CreateSnapshot())") &&
            source.Contains("JsonEquals(initialMarketJson, recruitment.CreateMarketSnapshot())") &&
            source.Contains("JsonEquals(initialSessionJson, session.CreateSessionSnapshot())"),
            "Verifica restauración exacta de Personal, sesión y Waiter count.",
            lines, ref passed, ref failed);
        Check(
            Ordered(
                source,
                "ValidateRollbackRestored(out restoreError)",
                "DeleteCheckpoint(failure);") &&
            source.Contains("save.TryDeleteSlot(checkpointSlot") &&
            source.Contains("save.TryDeleteSlot(rollbackSlot"),
            "Solo limpia slots después de validar el rollback restaurado.",
            lines, ref passed, ref failed);
        Check(
            source.Contains("if (!save.SlotExists(checkpointSlot))") &&
            source.Contains("if (!save.SlotExists(rollbackSlot))"),
            "La limpieza es tolerante a slots ya ausentes.",
            lines, ref passed, ref failed);
        Check(
            !source.Contains("PlayerPrefs.DeleteAll") &&
            !source.Contains("Directory.Delete") &&
            !source.Contains("File.Delete"),
            "El Queen Test no usa borrados globales o externos a SaveGame.",
            lines, ref passed, ref failed);

        report =
            "4G — AUTOTEST SEGURIDAD ROLLBACK\n" +
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
