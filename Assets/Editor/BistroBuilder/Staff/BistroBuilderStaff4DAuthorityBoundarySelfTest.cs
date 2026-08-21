using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4D que protege la frontera entre Personal persistente y los
/// agentes operativos legacy. 4D puede enlazar, observar y controlar la
/// elegibilidad de Waiter existentes, pero nunca debe convertirse en una
/// segunda autoridad de camareros ni de reparto de tareas.
/// </summary>
public static class BistroBuilderStaff4DAuthorityBoundarySelfTest
{
    private const string SessionServicePath =
        "Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Gate frontera autoridad operativa",
        false,
        3234)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D / Frontera de autoridad",
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
        log.AppendLine("=== BISTRO BUILDER — 4D / FRONTERA DE AUTORIDAD ===");

        string source = ReadSource(SessionServicePath);
        Check(
            !string.IsNullOrWhiteSpace(source),
            "Existe la autoridad de binding 4D.",
            ref passed, ref failed, log);

        Check(
            source.Contains("FindObjectsByType<Waiter>(") &&
            source.Contains("FindObjectsSortMode.InstanceID") &&
            source.Contains("La escena contiene WaiterId inválidos o duplicados."),
            "4D indexa Waiter ya existentes y rechaza identidades operativas duplicadas.",
            ref passed, ref failed, log);

        bool createsOrDestroysWaiters =
            source.Contains("new GameObject(") ||
            source.Contains("Instantiate(") ||
            source.Contains("AddComponent<Waiter>") ||
            source.Contains("Destroy(") ||
            source.Contains("DestroyImmediate(");
        Check(
            !createsOrDestroysWaiters,
            "4D no crea, instancia ni destruye agentes Waiter.",
            ref passed, ref failed, log);

        Check(
            source.Contains(
                "[SerializeField] private WaiterTaskCoordinator waiterTaskCoordinator;") &&
            source.Contains("waiterTaskCoordinator.ActiveTasks"),
            "4D consume el WaiterTaskCoordinator canónico como fuente observable.",
            ref passed, ref failed, log);

        string coordinatorWithoutAllowedRead = source.Replace(
            "waiterTaskCoordinator.ActiveTasks",
            string.Empty);
        Check(
            !coordinatorWithoutAllowedRead.Contains("waiterTaskCoordinator."),
            "4D no invoca comandos sobre WaiterTaskCoordinator; solo lee ActiveTasks.",
            ref passed, ref failed, log);

        bool ownsParallelTaskQueue =
            source.Contains("Queue<WaiterTask>") ||
            source.Contains("List<WaiterTask>") ||
            source.Contains("new WaiterTaskCoordinator") ||
            source.Contains("AddComponent<WaiterTaskCoordinator>");
        Check(
            !ownsParallelTaskQueue,
            "4D no declara una cola de tareas ni un coordinador operativo paralelo.",
            ref passed, ref failed, log);

        bool mutatesOperationalState =
            source.Contains(".SetState(") ||
            source.Contains(".AssignTask(") ||
            source.Contains(".TryAssignTask(") ||
            source.Contains(".CompleteTask(") ||
            source.Contains(".FailTask(");
        Check(
            !mutatesOperationalState,
            "4D no fuerza estados ni completa/asigna tareas de Waiter.",
            ref passed, ref failed, log);

        Check(
            source.Contains("BistroBuilderStaffEligibilityBatch.TryApply(") &&
            source.Contains("TrySetStaffServiceEligibility("),
            "La única mutación operativa permitida es la elegibilidad controlada de servicio.",
            ref passed, ref failed, log);

        Check(
            source.Contains("ReferenceEquals(task.AssignedWaiter, binding.waiter)") &&
            source.Contains("task.State != WaiterTaskState.Completed"),
            "El rendimiento 4D se deriva de tareas observadas del sistema operativo real.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        log.AppendLine(
            "Este gate no sustituye compilación, Play Mode ni Queen Test real.");
        report = log.ToString();
        return failed == 0;
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
