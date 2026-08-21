using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

public static class LivingArchitectureLA9SelfTest
{
    [MenuItem("Bistro Builder/Living Architecture/LA9/Run Self Test")]
    public static void Run()
    {
        var failures = ArchitectureEditSessionSelfTest.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA9] PASS 12/12");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][LA9] " + failure);
        Debug.LogError("[BB Living Architecture][LA9] FAIL " + failures.Count + "/12");
    }
}
