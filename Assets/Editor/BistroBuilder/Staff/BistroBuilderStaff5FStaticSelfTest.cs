using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5F — Gate estático del Queen Test de Horarios.
/// Vigila que la prueba siga usando autoridades reales, mutación observable,
/// Save/Load universal y rollback, sin fabricar agentes ni tareas.
/// </summary>
public static class BistroBuilderStaff5FStaticSelfTest
{
    private const string QueenPath =
        "Assets/Editor/BistroBuilder/Staff/BistroBuilderStaff5FQueenTestWindow.cs";

    [MenuItem("Tools/Bistro Builder/Personal/5F - Autotest arquitectura Queen", false, 3293)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5F arquitectura",
            passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5F / ARQUITECTURA QUEEN ===");

        try
        {
            Check(typeof(BistroBuilderStaff5FQueenTestWindow).IsSubclassOf(typeof(EditorWindow)),
                "El Queen runner es una herramienta Editor y no autoridad runtime.",
                ref passed, ref failed, log);
            Check(typeof(BistroBuilderStaff5FQueenPreflightWindow).IsSubclassOf(typeof(EditorWindow)),
                "El preflight es read-only y separado del runner.",
                ref passed, ref failed, log);

            string source = File.ReadAllText(QueenPath);
            Require(source, "BistroBuilderStaffScheduleSessionBridge",
                "Queen usa el bridge 5C.", ref passed, ref failed, log);
            Require(source, "BistroBuilderStaffSessionService",
                "Queen delega el binding operativo en 4D.", ref passed, ref failed, log);
            Require(source, "BistroBuilderStaffRecruitmentService",
                "Queen reutiliza Recruitment 4B para bootstrap reversible.",
                ref passed, ref failed, log);
            Require(source, "BistroBuilderStaffPlayerFacade",
                "La contratación de bootstrap usa la fachada canónica 4F.",
                ref passed, ref failed, log);
            Require(source, "TryHireCandidate",
                "Queen puede contratar un camarero real solo después del rollback.",
                ref passed, ref failed, log);
            Require(source, "initialMarketJson",
                "Rollback verifica también staff.recruitment.",
                ref passed, ref failed, log);
            Require(source, "BistroBuilderStaff4GNaturalMutationProbe.HasObservableMutation",
                "Save/Load Open exige mutación operativa observable real.",
                ref passed, ref failed, log);
            Require(source, "TryReplaceServiceAssignments",
                "El servicio se inicia desde un plan real de staff.schedule.",
                ref passed, ref failed, log);
            Require(source, "TrySetScheduled(",
                "El round-trip Closed crea un estado B real de staff.schedule.",
                ref passed, ref failed, log);
            Require(source, "TrySaveSlot", "Queen usa SaveGame universal para Save.",
                ref passed, ref failed, log);
            Require(source, "TryLoadSlot", "Queen usa SaveGame universal para Load.",
                ref passed, ref failed, log);
            Require(source, "TryDeleteSlot", "Queen limpia slots temporales.",
                ref passed, ref failed, log);
            Require(source, "ValidateRollbackRestored",
                "Queen verifica restauración integral antes de dar PASS.",
                ref passed, ref failed, log);
            Require(source, "AreBoundWaitersIdle",
                "Queen espera camareros ligados realmente libres antes de Closed.",
                ref passed, ref failed, log);

            Forbid(source, "new Waiter(",
                "Queen no fabrica agentes Waiter.", ref passed, ref failed, log);
            Forbid(source, "new WaiterTask(",
                "Queen no fabrica tareas WaiterTask.", ref passed, ref failed, log);
            Forbid(source, "TrySetStaffServiceEligibility",
                "Queen no manipula elegibilidad operativa directamente.",
                ref passed, ref failed, log);
            Forbid(source, "BistroBuilderJsonSaveSerializer",
                "Queen no evita SaveGame mediante serialización directa.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static void Require(string source, string token, string text,
        ref int passed, ref int failed, StringBuilder log)
    {
        Check(source.IndexOf(token, StringComparison.Ordinal) >= 0,
            text, ref passed, ref failed, log);
    }

    private static void Forbid(string source, string token, string text,
        ref int passed, ref int failed, StringBuilder log)
    {
        Check(source.IndexOf(token, StringComparison.Ordinal) < 0,
            text, ref passed, ref failed, log);
    }

    private static void Check(bool condition, string text,
        ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
