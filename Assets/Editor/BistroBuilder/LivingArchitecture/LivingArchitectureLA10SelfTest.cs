using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

public static class LivingArchitectureLA10SelfTest
{
    [MenuItem("Bistro Builder/Living Architecture/LA10/Run Self Test")]
    public static void Run()
    {
        var failures = ArchitectureEditFeedbackSelfTest.Run();
        if (failures.Count == 0)
        {
            Debug.Log("[BB Living Architecture][LA10] PASS 10/10");
            return;
        }

        foreach (var failure in failures) Debug.LogError("[BB Living Architecture][LA10] " + failure);
        Debug.LogError("[BB Living Architecture][LA10] FAIL " + failures.Count + "/10");
    }
}
