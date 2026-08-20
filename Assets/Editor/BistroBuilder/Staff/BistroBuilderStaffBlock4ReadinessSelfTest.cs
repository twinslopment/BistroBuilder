using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate acumulativo no destructivo del Bloque 4 — Personal.
///
/// Ejecuta en una sola pasada los endurecimientos puros/estáticos de 4D–4G.
/// No instala componentes, no modifica escenas, no crea autoridad de gameplay
/// y no sustituye las pruebas reales de compilación/Play Mode en Unity.
/// </summary>
public static class BistroBuilderStaffBlock4ReadinessSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/Bloque 4 - Gate acumulativo 4D-4G",
        false,
        3265)]
    private static void RunFromMenu()
    {
        bool success = Run(out int passed, out int failed, out string report);
        if (success)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder — Personal / Gate acumulativo",
            passed + " gates OK / " + failed + " gates con fallo",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        RunGate(
            "4D endurecimiento",
            () => BistroBuilderStaff4DHardeningSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4D preflight restore",
            () => BistroBuilderStaff4DRestorePreflightSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4D restore elegibilidad transaccional",
            () => BistroBuilderStaff4DRestoreEligibilitySelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4D PrepareForLoad commit-safe",
            () => BistroBuilderStaff4DPrepareForLoadSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4D ResumeAfterLoad commit-safe",
            () => BistroBuilderStaff4DResumeAfterLoadSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4E JSON round-trip",
            () => BistroBuilderStaff4EJsonRoundTripSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4F frontera Presentation",
            () => BistroBuilderStaff4FStaticSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4G arquitectura estática",
            () => BistroBuilderStaff4GStaticSelfTest.Run(
                out _,
                out _,
                out _),
            lines,
            ref passed,
            ref failed);

        RunGate(
            "4G mutación observable",
            () => BistroBuilderStaff4GNaturalMutationSelfTest.Run(out _),
            lines,
            ref passed,
            ref failed);

        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — BLOQUE 4 / GATE ACUMULATIVO ===");
        for (int index = 0; index < lines.Count; index++)
        {
            builder.AppendLine(lines[index]);
        }
        builder.AppendLine();
        builder.AppendLine(
            "Resultado: " + passed + " gates OK / " + failed + " gates con fallo.");
        builder.AppendLine(
            "Este resultado no valida compilación, instalación ni Play Mode real.");

        report = builder.ToString();
        return failed == 0;
    }

    private static void RunGate(
        string name,
        System.Func<bool> gate,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        bool ok;
        try
        {
            ok = gate();
        }
        catch (System.Exception exception)
        {
            ok = false;
            lines.Add("[ERROR] " + name + ": excepción " + exception.GetType().Name + ".");
        }

        if (ok)
        {
            passed++;
            lines.Add("[OK] " + name + ".");
        }
        else
        {
            failed++;
            if (lines.Count == 0 || !lines[lines.Count - 1].StartsWith("[ERROR] " + name))
            {
                lines.Add("[ERROR] " + name + ".");
            }
        }
    }
}
