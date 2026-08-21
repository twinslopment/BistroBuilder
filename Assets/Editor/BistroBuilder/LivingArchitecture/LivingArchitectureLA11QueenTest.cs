using System.Collections.Generic;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

public static class LivingArchitectureLA11QueenTest
{
    [MenuItem("Bistro Builder/Living Architecture/LA11/Run Queen Test")]
    public static void RunQueen()
    {
        var failures = new List<string>();
        Append(failures, "QUEEN", ArchitectureV1QueenSelfTest.Run());
        Append(failures, "HARDENING", ArchitectureV1QueenHardeningSelfTest.Run());

        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA11] QUEEN PASS 14/14 — pendiente validación integral en Unity real.");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][LA11] " + failure);
        Debug.LogError("[BB Living Architecture][LA11] QUEEN FAIL " + failures.Count + "/14");
    }

    [MenuItem("Bistro Builder/Living Architecture/LA11/Run Accumulated LA2-LA11")]
    public static void RunAccumulated()
    {
        var failures = ArchitectureV1SelfTestSuite.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA11] ACCUMULATED PASS 109/109 (LA2-LA11 + hardening). Ejecutar también LA1 y gates runtime antes de cerrar V1.");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][ACCUMULATED] " + failure);
        Debug.LogError("[BB Living Architecture][LA11] ACCUMULATED FAIL — " + failures.Count + " casos fallidos de 109.");
    }

    private static void Append(ICollection<string> target, string group, IReadOnlyList<string> source)
    {
        if (source == null)
        {
            target.Add(group + ": self-test returned null");
            return;
        }

        for (var i = 0; i < source.Count; i++)
            target.Add(group + "/" + source[i]);
    }
}
