#if UNITY_EDITOR
using System;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.LivingArchitecture.Editor
{
    /// <summary>Runner de Editor para el self-test puro LA7.</summary>
    public static class LivingArchitectureLA7SelfTest
    {
        [MenuItem("Bistro Builder/Living Architecture/LA7/Run Self Test")]
        public static void Run()
        {
            var result = ArchitectureMesherSelfTest.Run();
            if (!result.Success)
                throw new InvalidOperationException("[BB Living Architecture][LA7] SELF TEST FAIL — " + string.Join(" | ", result.Failures));

            Debug.Log($"[BB Living Architecture][LA7] SELF TEST PASS — {result.Passed} correctos, {result.Failed} fallidos. Pendiente validación real acumulativa en Unity.");
        }
    }
}
#endif
