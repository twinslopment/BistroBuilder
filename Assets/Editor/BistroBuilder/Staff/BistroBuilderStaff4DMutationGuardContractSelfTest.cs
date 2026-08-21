using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4D que congela el contrato de guardia de mutación runtime.
///
/// StaffService sigue siendo la única autoridad del roster persistente. 4D
/// únicamente registra una guardia por inversión de control para impedir que
/// despido/disponibilidad invaliden un binding operativo activo.
/// </summary>
public static class BistroBuilderStaff4DMutationGuardContractSelfTest
{
    private const string StaffServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffService.cs";

    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Gate contrato guardia runtime",
        false,
        3235)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D / Guardia runtime",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4D / GUARDIA DE MUTACIÓN RUNTIME ===");

        string staffSource = ReadSource(StaffServicePath);
        string sessionSource = ReadSource(SessionServicePath);

        Check(
            !string.IsNullOrWhiteSpace(staffSource) &&
            !string.IsNullOrWhiteSpace(sessionSource),
            "Existen StaffService y StaffSessionService.",
            ref passed, ref failed, log);

        Check(
            staffSource.Contains(
                "private IBistroBuilderStaffRuntimeMutationGuard runtimeMutationGuard;") &&
            staffSource.Contains(
                "public bool TryRegisterRuntimeMutationGuard(") &&
            staffSource.Contains(
                "public void UnregisterRuntimeMutationGuard("),
            "StaffService expone una única guardia runtime desacoplada de Waiter.",
            ref passed, ref failed, log);

        Check(
            staffSource.Contains(
                "runtimeMutationGuard != null &&\n            !ReferenceEquals(runtimeMutationGuard, guard)") &&
            staffSource.Contains(
                "Personal ya tiene otra guardia runtime registrada."),
            "El registro rechaza una segunda autoridad runtime distinta.",
            ref passed, ref failed, log);

        string dismissBody = SliceMethod(
            staffSource,
            "public bool TryDismissEmployee(",
            "public bool TryGetEmployee(");
        Check(
            dismissBody.Contains("runtimeMutationGuard != null") &&
            dismissBody.Contains("runtimeMutationGuard.CanDismissEmployee(") &&
            dismissBody.IndexOf(
                "runtimeMutationGuard.CanDismissEmployee(",
                StringComparison.Ordinal) <
            dismissBody.IndexOf(
                "BistroBuilderStaffEmploymentEngine.TryDismissEmployee(",
                StringComparison.Ordinal),
            "El despido consulta la guardia antes de construir/comprometer la mutación.",
            ref passed, ref failed, log);

        string availabilityBody = SliceMethod(
            staffSource,
            "public bool TrySetAvailability(",
            "public bool TryCalculateTotalActiveSalaryCentsPerService(");
        Check(
            availabilityBody.Contains("runtimeMutationGuard != null") &&
            availabilityBody.Contains(
                "runtimeMutationGuard.CanChangeAvailability(") &&
            availabilityBody.IndexOf(
                "runtimeMutationGuard.CanChangeAvailability(",
                StringComparison.Ordinal) <
            availabilityBody.IndexOf(
                "BistroBuilderStaffEngine.TrySetAvailability(",
                StringComparison.Ordinal),
            "La disponibilidad consulta la guardia antes de mutar staff.state.",
            ref passed, ref failed, log);

        Check(
            sessionSource.Contains(
                "IBistroBuilderStaffRuntimeMutationGuard") &&
            sessionSource.Contains("TryRegisterMutationGuard();") &&
            sessionSource.Contains(
                "staffService?.UnregisterRuntimeMutationGuard(this);") &&
            sessionSource.Contains("public bool CanDismissEmployee(") &&
            sessionSource.Contains("public bool CanChangeAvailability("),
            "4D implementa, registra y libera la guardia durante su lifecycle.",
            ref passed, ref failed, log);

        string dismissGuardBody = SliceMethod(
            sessionSource,
            "public bool CanDismissEmployee(",
            "public bool CanChangeAvailability(");
        Check(
            dismissGuardBody.Contains("TryGetActiveAssignment(") &&
            dismissGuardBody.Contains("no puede despedirse durante esa sesión"),
            "La guardia bloquea despido mientras existe binding activo.",
            ref passed, ref failed, log);

        string availabilityGuardBody = SliceMethod(
            sessionSource,
            "public bool CanChangeAvailability(",
            "public bool TryEnsureSessionStarted(");
        Check(
            availabilityGuardBody.Contains("TryGetActiveAssignment(") &&
            availabilityGuardBody.Contains(
                "La disponibilidad no puede cambiar mientras el empleado"),
            "La guardia bloquea cambios reales de disponibilidad durante binding.",
            ref passed, ref failed, log);

        Check(
            !staffSource.Contains("Waiter ") &&
            !staffSource.Contains("WaiterTask") &&
            !staffSource.Contains("WaiterTaskCoordinator"),
            "StaffService no adquiere dependencias operativas al aplicar la guardia.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        log.AppendLine(
            "Este gate no sustituye compilación, Play Mode ni Queen Test real.");
        report = log.ToString();
        return failed == 0;
    }

    private static string SliceMethod(string source, string startToken, string endToken)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int end = source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
        return end > start
            ? source.Substring(start, end - start)
            : source.Substring(start);
    }

    private static string ReadSource(string assetPath)
    {
        string absolutePath = Path.GetFullPath(assetPath);
        return File.Exists(absolutePath)
            ? File.ReadAllText(absolutePath)
            : string.Empty;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + text);
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
