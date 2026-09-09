using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gate acumulativo de cierre del Bloque 10. Ejecuta los motores puros 10A-10G
/// y la cadena estructural final sin crear una segunda autoridad de clientes.
/// </summary>
public static class BistroBuilderAdvancedCustomersBlock10ReadinessSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/Bloque 10 - Readiness", false, 10090)]
    private static void RunFromMenu()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool ok = Run(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!Run(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }
    public static bool Run(out string report)
    {
        int passed = 0;
        int failed = 0;
        var lines = new System.Collections.Generic.List<string>();

        Accumulate("10A", BistroBuilderAdvancedCustomers10ASelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10B", BistroBuilderAdvancedCustomers10BSelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10C", BistroBuilderAdvancedCustomers10CSelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10D", BistroBuilderAdvancedCustomers10DSelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10E", BistroBuilderAdvancedCustomers10ESelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10F", BistroBuilderAdvancedCustomers10FSelfTest.Run,
            ref passed, ref failed, lines);
        Accumulate("10G", BistroBuilderAdvancedCustomers10GSelfTest.Run,
            ref passed, ref failed, lines);

        BistroBuilderAdvancedCustomers10GValidationResult structural =
            BistroBuilderAdvancedCustomers10GValidator.ValidateCurrentScene();
        bool structuralOk = structural.Errors == 0;
        if (structuralOk) passed++; else failed++;
        lines.Add(structuralOk
            ? "[OK] Cadena estructural 10A-10G verde."
            : "[FAIL] La cadena estructural 10A-10G contiene errores.");
        report = "=== BISTRO BUILDER — BLOQUE 10 / READINESS ===\n" +
            string.Join("\n", lines) + "\nResultado acumulado: " +
            passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private delegate bool PureSelfTest(
        out int passed,
        out int failed,
        out string report);

    private static void Accumulate(
        string label,
        PureSelfTest test,
        ref int totalPassed,
        ref int totalFailed,
        System.Collections.Generic.List<string> lines)
    {
        bool ok = test(out int passed, out int failed, out _);
        totalPassed += passed;
        totalFailed += failed;
        lines.Add((ok ? "[OK] " : "[FAIL] ") + label + ": " +
            passed + " OK / " + failed + " fallos.");
    }
}
