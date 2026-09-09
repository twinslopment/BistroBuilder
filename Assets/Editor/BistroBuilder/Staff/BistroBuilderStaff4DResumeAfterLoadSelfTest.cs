using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4D — Gate estático de seguridad para TryResumeAfterRuntimeLoad.
///
/// Protege la regla de commit tardío: la reanudación mantiene Personal
/// suspendido mientras valida configuración, coherencia service/session y
/// rehidratación. Solo libera la suspensión después de completar todo con éxito.
/// </summary>
public static class BistroBuilderStaff4DResumeAfterLoadSelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest ResumeAfterLoad seguro",
        false,
        3235)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D ResumeAfterLoad",
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
        log.AppendLine(
            "=== BISTRO BUILDER — 4D RESUME AFTER LOAD / AUTOTEST ===");

        string absolutePath = Path.GetFullPath(SessionServicePath);
        string source = File.Exists(absolutePath)
            ? File.ReadAllText(absolutePath)
            : string.Empty;

        int resumeIndex = source.IndexOf(
            "public bool TryResumeAfterRuntimeLoad",
            StringComparison.Ordinal);
        int finalizeIndex = source.IndexOf(
            "public bool TryFinalizeClosedSession",
            resumeIndex >= 0 ? resumeIndex : 0,
            StringComparison.Ordinal);
        string body = Slice(source, resumeIndex, finalizeIndex);

        Check(
            resumeIndex >= 0 && !string.IsNullOrEmpty(body),
            "Existe TryResumeAfterRuntimeLoad y puede inspeccionarse.",
            ref passed,
            ref failed,
            log);

        int suspendIndex = body.IndexOf(
            "suspendedForRuntimeLoad = true",
            StringComparison.Ordinal);
        int validateIndex = body.IndexOf(
            "ValidateConfiguration(out error)",
            StringComparison.Ordinal);
        int coherenceIndex = body.IndexOf(
            "serviceActive != HasActiveSession",
            StringComparison.Ordinal);
        int rehydrateIndex = body.IndexOf(
            "TryRehydrateRuntimeFromCurrentState(out error)",
            StringComparison.Ordinal);
        int releaseIndex = body.LastIndexOf(
            "suspendedForRuntimeLoad = false",
            StringComparison.Ordinal);

        Check(
            suspendIndex >= 0 &&
            validateIndex > suspendIndex &&
            coherenceIndex > validateIndex &&
            rehydrateIndex > coherenceIndex,
            "La reanudación mantiene suspensión durante todos los preflights.",
            ref passed,
            ref failed,
            log);

        Check(
            releaseIndex > rehydrateIndex,
            "La suspensión solo se libera después de rehidratar correctamente.",
            ref passed,
            ref failed,
            log);

        string beforeRehydrate = rehydrateIndex > 0
            ? body.Substring(0, rehydrateIndex)
            : body;
        Check(
            !beforeRehydrate.Contains("suspendedForRuntimeLoad = false"),
            "Ningún fallo previo puede reactivar Personal prematuramente.",
            ref passed,
            ref failed,
            log);

        string afterRehydrate = rehydrateIndex >= 0
            ? body.Substring(rehydrateIndex)
            : string.Empty;
        Check(
            afterRehydrate.Contains("return false;") &&
            afterRehydrate.Contains("suspendedForRuntimeLoad = false") &&
            afterRehydrate.Contains("error = string.Empty;") &&
            afterRehydrate.Contains("return true;"),
            "El commit exitoso es explícito y el fallo conserva suspensión.",
            ref passed,
            ref failed,
            log);

        log.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Slice(string source, int start, int end)
    {
        if (string.IsNullOrEmpty(source) || start < 0)
        {
            return string.Empty;
        }

        int safeEnd = end > start && end <= source.Length
            ? end
            : source.Length;
        return source.Substring(start, safeEnd - start);
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
