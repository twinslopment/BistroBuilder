using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4G. Comprueba que el Queen Test final usa las autoridades
/// existentes y conserva rollback/Save/Load real sin fabricar gameplay.
/// </summary>
public static class BistroBuilderStaff4GStaticSelfTest
{
    private const string RunnerPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff4GQueenTestWindow.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - Autotest estático",
        false,
        3262)]
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
            report = "4G — AUTOTEST ESTÁTICO\n[ERROR] Falta el runner Queen Test.";
            failed = 1;
            return false;
        }

        string source = File.ReadAllText(RunnerPath);

        Require(source, "save.TrySaveSlot(", "Usa SaveGame real para checkpoints.", lines, ref passed, ref failed);
        Require(source, "save.TryLoadSlot(", "Usa SaveGame real para rollback/Load.", lines, ref passed, ref failed);
        Require(source, "save.TryDeleteSlot(", "Elimina slots diagnósticos.", lines, ref passed, ref failed);
        Require(source, "facade.TryHireCandidate(", "Contrata mediante fachada 4F/4B.", lines, ref passed, ref failed);
        Require(source, "facade.TrySetAvailability(", "Disponibilidad pasa por autoridad canónica.", lines, ref passed, ref failed);
        Require(source, "facade.TryTrainEmployee(", "Formación pasa por 4C mediante fachada.", lines, ref passed, ref failed);
        Require(source, "serviceState.TryBeginPreparation()", "Abre flujo de servicio real.", lines, ref passed, ref failed);
        Require(source, "serviceState.TryOpenService()", "Usa transición Open real.", lines, ref passed, ref failed);
        Require(source, "view.completedTasks > 0", "Exige trabajo real observado por 4D.", lines, ref passed, ref failed);
        Require(source, "waiterCoordinator.ActiveTaskCount == 0", "Cierre espera cola real vacía.", lines, ref passed, ref failed);
        Require(source, "session.TryFinalizeClosedSession", "Comprueba idempotencia del cierre 4D.", lines, ref passed, ref failed);
        Require(source, "ValidateRollbackRestored", "Verifica restauración integral.", lines, ref passed, ref failed);

        Forbid(source, "new Waiter(", "No fabrica agentes Waiter.", lines, ref passed, ref failed);
        Forbid(source, "new WaiterTask(", "No fabrica tareas de camarero.", lines, ref passed, ref failed);
        Forbid(source, "new WaiterTaskQueue(", "No crea una cola paralela.", lines, ref passed, ref failed);
        Forbid(source, ".TryApplyServiceResult(", "No inyecta XP/rendimiento directamente.", lines, ref passed, ref failed);
        Forbid(source, ".TryRestoreState(", "No altera el servicio mediante restauración diagnóstica directa.", lines, ref passed, ref failed);
        Forbid(source, "AddComponent<Waiter", "No crea camareros en escena.", lines, ref passed, ref failed);

        bool schemas =
            string.Equals(
                BistroBuilderStaffSnapshot.CurrentSchemaId,
                "staff.state",
                StringComparison.Ordinal) &&
            string.Equals(
                BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId,
                "staff.recruitment",
                StringComparison.Ordinal) &&
            string.Equals(
                BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                "staff.session.runtime",
                StringComparison.Ordinal);
        Check(
            schemas,
            "4G sigue apuntando a las tres identidades persistentes canónicas.",
            lines,
            ref passed,
            ref failed);

        report =
            "4G — AUTOTEST ESTÁTICO\n" +
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
