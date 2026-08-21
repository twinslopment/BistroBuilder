using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

public static class LivingArchitectureLA11QueenTest
{
    [MenuItem("Bistro Builder/Living Architecture/LA11/Run Queen Test")]
    public static void RunQueen()
    {
        var failures = ArchitectureV1QueenSelfTest.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA11] QUEEN PASS 12/12 — pendiente validación integral en Unity real.");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][LA11] " + failure);
        Debug.LogError("[BB Living Architecture][LA11] QUEEN FAIL " + failures.Count + "/12");
    }

    [MenuItem("Bistro Builder/Living Architecture/LA11/Run Accumulated LA2-LA11")]
    public static void RunAccumulated()
    {
        var failures = ArchitectureV1SelfTestSuite.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA11] ACCUMULATED PASS 107/107 (LA2-LA11). Ejecutar también LA1 y gates runtime antes de cerrar V1.");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][ACCUMULATED] " + failure);
        Debug.LogError("[BB Living Architecture][LA11] ACCUMULATED FAIL — " + failures.Count + " casos fallidos de 107.");
    }
}
