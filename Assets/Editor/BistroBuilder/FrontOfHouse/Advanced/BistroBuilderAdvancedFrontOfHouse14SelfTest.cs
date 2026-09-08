using System.Collections.Generic;
using System.Text;
using UnityEditor;

public static class BistroBuilderAdvancedFrontOfHouse14SelfTest
{
    [MenuItem("Tools/Bistro Builder/Front Of House/14 - Autotest", false, 14002)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) UnityEngine.Debug.Log(report); else UnityEngine.Debug.LogError(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0; failed = 0;
        var lines = new List<string>();
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveOperationalState(0, 3, 0f, 0.1f) ==
            BistroBuilderFrontOfHouseOperationalState.Fluid, "Estado Fluida", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveOperationalState(2, 0, 0.4f, 0.4f) ==
            BistroBuilderFrontOfHouseOperationalState.Waiting, "Estado Con espera", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveOperationalState(6, 0, 0.9f, 0.4f) ==
            BistroBuilderFrontOfHouseOperationalState.Saturated, "Estado Saturada", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveOperationalState(2, 1, 0.4f, 0.9f) ==
            BistroBuilderFrontOfHouseOperationalState.ReducedPace, "Estado Ritmo reducido", ref passed, ref failed, lines);

        int normal = BistroBuilderAdvancedFrontOfHousePolicy.ComputeQueuePriority(20f, 80f, 2, false, false, false);
        int reservation = BistroBuilderAdvancedFrontOfHousePolicy.ComputeQueuePriority(5f, 80f, 2, true, false, false);
        int vip = BistroBuilderAdvancedFrontOfHousePolicy.ComputeQueuePriority(20f, 80f, 2, false, true, false);
        Check(reservation > normal, "Reservas priorizadas", ref passed, ref failed, lines);
        Check(vip > normal, "VIP priorizado", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveQueueReason(70f, 80f, false, false, false) ==
            BistroBuilderFrontOfHouseQueueReason.PatienceRisk, "Riesgo por paciencia", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ResolveQueueReason(10f, 80f, false, false, true) ==
            BistroBuilderFrontOfHouseQueueReason.WaitingAtBar, "Espera en barra", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ShouldAbandon(95f, 80f, false, false),
            "Abandono por espera excesiva", ref passed, ref failed, lines);
        Check(!BistroBuilderAdvancedFrontOfHousePolicy.ShouldAbandon(95f, 80f, true, false),
            "Reserva tolera demora adicional", ref passed, ref failed, lines);
        Check(!BistroBuilderAdvancedFrontOfHousePolicy.ShouldAbandon(200f, 80f, false, true),
            "Barra evita abandono de cola física", ref passed, ref failed, lines);

        float exact = BistroBuilderAdvancedFrontOfHousePolicy.ComputeTableScore(2, 2, 5f, 0, 0, 0, false);
        float waste = BistroBuilderAdvancedFrontOfHousePolicy.ComputeTableScore(2, 6, 5f, 0, 0, 0, false);
        float preferred = BistroBuilderAdvancedFrontOfHousePolicy.ComputeTableScore(2, 2, 5f, 3000, 0, 0, false);
        Check(exact > waste, "Asignacion eficiente por capacidad", ref passed, ref failed, lines);
        Check(preferred > exact, "Preferencia de zona influye en mesa", ref passed, ref failed, lines);
        Check(BistroBuilderAdvancedFrontOfHousePolicy.ComputeTableScore(2, 2, 1f, 0, 0, 0, true) == float.MinValue,
            "Mesa protegida para reserva excluida", ref passed, ref failed, lines);

        var snapshot = new BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot();
        snapshot.tableRotations.Add(new BistroBuilderFrontOfHouseTableRotationRecord
            { tableId = 10, seatedCount = 2, lastSeatSequence = 2 });
        snapshot.nextSeatSequence = 3;
        Check(snapshot.TryValidate(out _), "Snapshot persistente valido", ref passed, ref failed, lines);
        Check(snapshot.DeepClone().tableRotations.Count == 1, "Snapshot clonable", ref passed, ref failed, lines);
        snapshot.tableRotations.Add(new BistroBuilderFrontOfHouseTableRotationRecord
            { tableId = 10, seatedCount = 1, lastSeatSequence = 1 });
        Check(!snapshot.TryValidate(out _), "Rotacion duplicada rechazada", ref passed, ref failed, lines);

        var b = new StringBuilder("BLOQUE 14 - AUTOTEST\n");
        for (int i = 0; i < lines.Count; i++) b.AppendLine(lines[i]);
        b.Append("Resultado: ").Append(passed).Append(" OK / ").Append(failed).Append(" fallos.");
        report = b.ToString();
        return failed == 0;
    }

    private static void Check(bool ok, string name, ref int passed, ref int failed, List<string> lines)
    {
        if (ok) passed++; else failed++;
        lines.Add((ok ? "OK - " : "ERROR - ") + name);
    }
}
