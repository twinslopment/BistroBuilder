using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5C — Gate estático de integración con 4D.
/// </summary>
public static class BistroBuilderStaff5CBindingSelfTest
{
    private const string BridgePath =
        "Assets/Scripts/Application/Staff/Scheduling/BistroBuilderStaffScheduleSessionBridge.cs";

    [MenuItem("Tools/Bistro Builder/Personal/5C - Autotest binding horario", false, 3274)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5C", passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        string source = Read(BridgePath);

        Check(source.Contains("BistroBuilderStaffSessionService") &&
              source.Contains("TryRestoreSessionSnapshot(candidate, out error)"),
            "5C delega la reconstrucción atómica del binding en 4D.",
            ref passed, ref failed, log);
        Check(source.Contains("SessionStarted += HandleSessionStarted") &&
              source.Contains("ServiceOpeningRequested += HandleServiceOpeningRequested"),
            "5C aplica/verifica el horario antes del servicio Open.",
            ref passed, ref failed, log);
        Check(!source.Contains("new Waiter(") &&
              !source.Contains("AddComponent<Waiter>") &&
              !source.Contains("new WaiterTask") &&
              !source.Contains("WaiterTaskCoordinator"),
            "5C no crea camareros, tareas ni coordinador alternativo.",
            ref passed, ref failed, log);
        Check(!source.Contains("TrySetAvailability(") &&
              !source.Contains("TryCommitDomainMutation(") &&
              !source.Contains("TryRestoreSnapshot("),
            "5C no altera staff.state para simular un turno.",
            ref passed, ref failed, log);
        Check(source.Contains("HasPristineMetrics") &&
              source.Contains("completedTasks != 0"),
            "5C rechaza remapear una sesión que ya observó trabajo real.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Read(string path)
    {
        string full = Path.GetFullPath(path);
        return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
    }

    private static void Check(bool condition, string text, ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
