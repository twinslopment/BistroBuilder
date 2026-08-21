using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

public static class LivingArchitectureLA8SelfTest
{
    [MenuItem("Bistro Builder/Living Architecture/LA8/Run Self Test")]
    public static void Run()
    {
        var failures = ArchitecturePersistenceSelfTest.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA8] PASS 10/10");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][LA8] " + failure);
        Debug.LogError("[BB Living Architecture][LA8] FAIL " + failures.Count + "/10");
    }
}
