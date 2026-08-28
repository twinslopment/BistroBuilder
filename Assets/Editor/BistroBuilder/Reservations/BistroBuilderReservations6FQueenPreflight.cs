using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 6F — Preflight agregado del Bloque 6 antes del Queen Test reversible.
/// Reutiliza los gates ya aprobados; no crea datos ni modifica la escena.
/// </summary>
public static class BistroBuilderReservations6FQueenPreflight
{
    [MenuItem("Tools/Bistro Builder/Reservations/6F - Queen preflight", false, 660)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — 6F / QUEEN PREFLIGHT ===");
        BistroBuilderBlock6CapacityValidation capacity =
            BistroBuilderBlock6CapacityValidator.ValidateCurrentScene();
        Check(capacity.Errors == 0,
            "Capacidad ampliada: " + capacity.Correct + " OK / " + capacity.Errors + " errores.",
            ref passed, ref failed, builder);

        BistroBuilderReservations6AValidationResult gate6A =
            BistroBuilderReservations6AValidator.ValidateCurrentScene();
        Check(gate6A.Errors == 0,
            "6A Fundación: " + gate6A.Correct + " OK / " + gate6A.Errors + " errores.",
            ref passed, ref failed, builder);

        BistroBuilderReservations6BValidationResult gate6B =
            BistroBuilderReservations6BValidator.ValidateCurrentScene();
        Check(gate6B.Errors == 0,
            "6B Disponibilidad: " + gate6B.Correct + " OK / " + gate6B.Errors + " errores.",
            ref passed, ref failed, builder);

        BistroBuilderReservations6CValidationResult gate6C =
            BistroBuilderReservations6CValidator.ValidateCurrentScene();
        Check(gate6C.Errors == 0,
            "6C Servicio real: " + gate6C.Correct + " OK / " + gate6C.Errors + " errores.",
            ref passed, ref failed, builder);
        BistroBuilderReservations6DValidationResult gate6D =
            BistroBuilderReservations6DValidator.ValidateCurrentScene();
        Check(gate6D.Errors == 0,
            "6D Persistencia: " + gate6D.Correct + " OK / " + gate6D.Errors + " errores.",
            ref passed, ref failed, builder);

        BistroBuilderReservations6EValidation gate6E =
            BistroBuilderReservations6EValidator.ValidateCurrentScene();
        Check(gate6E.Errors == 0,
            "6E UI jugable: " + gate6E.Correct + " OK / " + gate6E.Errors + " errores.",
            ref passed, ref failed, builder);

        bool pure6A = BistroBuilderReservations6AFoundationSelfTest.Run(
            out int passed6A, out int failed6A, out _);
        Check(pure6A && failed6A == 0,
            "Autotest 6A: " + passed6A + " OK / " + failed6A + " fallos.",
            ref passed, ref failed, builder);

        bool pure6B = BistroBuilderReservations6BAvailabilitySelfTest.Run(
            out int passed6B, out int failed6B, out _);
        Check(pure6B && failed6B == 0,
            "Autotest 6B: " + passed6B + " OK / " + failed6B + " fallos.",
            ref passed, ref failed, builder);
        bool pure6D = BistroBuilderReservations6DSelfTest.Run(
            out int passed6D, out int failed6D, out _);
        Check(pure6D && failed6D == 0,
            "Autotest 6D: " + passed6D + " OK / " + failed6D + " fallos.",
            ref passed, ref failed, builder);

        builder.Append("Resultado: ")
            .Append(passed).Append(" OK / ")
            .Append(failed).Append(" fallos.");
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
