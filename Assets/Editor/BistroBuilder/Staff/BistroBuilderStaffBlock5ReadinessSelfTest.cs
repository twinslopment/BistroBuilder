using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate acumulativo no destructivo del Bloque 5 — Horarios y Turnos.
/// No modifica escena ni sustituye validación real en Unity.
/// </summary>
public static class BistroBuilderStaffBlock5ReadinessSelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/Bloque 5 - Gate acumulativo 5A-5F", false, 3290)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — Bloque 5",
            passed + " gates OK / " + failed + " gates con fallo", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        RunGate("5A fundación", () => BistroBuilderStaff5AFoundationSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);
        RunGate("5B planificación", () => BistroBuilderStaff5BPlanningSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);
        RunGate("5C binding con 4D", () => BistroBuilderStaff5CBindingSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);
        RunGate("5D JSON SaveGame", () => BistroBuilderStaff5DJsonRoundTripSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);
        RunGate("5E frontera Presentation", () => BistroBuilderStaff5EStaticSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);
        RunGate("5F arquitectura Queen", () => BistroBuilderStaff5FStaticSelfTest.Run(out _, out _, out _),
            lines, ref passed, ref failed);

        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — BLOQUE 5 / GATE ACUMULATIVO ===");
        foreach (string line in lines) builder.AppendLine(line);
        builder.AppendLine();
        builder.AppendLine("Resultado: " + passed + " gates OK / " + failed + " gates con fallo.");
        builder.AppendLine("No valida compilación, escena ni Play Mode real.");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunGate(
        string name,
        Func<bool> gate,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        bool ok;
        try { ok = gate(); }
        catch (Exception exception)
        {
            ok = false;
            lines.Add("[ERROR] " + name + ": " + exception.GetType().Name + ".");
        }
        if (ok) { passed++; lines.Add("[OK] " + name + "."); }
        else
        {
            failed++;
            if (lines.Count == 0 || !lines[lines.Count - 1].StartsWith("[ERROR] " + name, StringComparison.Ordinal))
                lines.Add("[ERROR] " + name + ".");
        }
    }
}
