using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate estático 4F. Comprueba que la fachada jugable sigue siendo una capa de
/// Presentation y no ha absorbido autoridades de Save, camareros o finanzas.
/// </summary>
public static class BistroBuilderStaff4FStaticSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Autotest estático Presentation",
        false,
        3240)]
    private static void RunFromMenu()
    {
        bool success = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4F Personal",
            report,
            "Aceptar");
        if (!success)
        {
            Debug.LogError(
                "4F Presentation no supera el gate estático: " + failed +
                " fallo(s).");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        Check(
            typeof(BistroBuilderStaffPlayerFacade).IsSubclassOf(typeof(MonoBehaviour)),
            "La fachada 4F es un componente de Presentation.",
            ref passed,
            ref failed,
            lines);

        FieldInfo[] fields = typeof(BistroBuilderStaffPlayerFacade).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        bool forbiddenAuthority = false;
        for (int index = 0; index < fields.Length; index++)
        {
            string typeName = fields[index].FieldType.FullName ??
                              fields[index].FieldType.Name;
            if (typeName.IndexOf("SaveGameService", StringComparison.Ordinal) >= 0 ||
                typeName.IndexOf("WaiterTaskCoordinator", StringComparison.Ordinal) >= 0 ||
                typeName.IndexOf("Finance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                forbiddenAuthority = true;
                break;
            }
        }
        Check(
            !forbiddenAuthority,
            "La fachada no referencia directamente Save, WaiterTaskCoordinator ni Finanzas.",
            ref passed,
            ref failed,
            lines);

        CheckMethod(
            "TryBuildSnapshot",
            typeof(BistroBuilderStaffPlayerFacade),
            ref passed,
            ref failed,
            lines);
        CheckMethod(
            "TryHireCandidate",
            typeof(BistroBuilderStaffPlayerFacade),
            ref passed,
            ref failed,
            lines);
        CheckMethod(
            "TryDismissEmployee",
            typeof(BistroBuilderStaffPlayerFacade),
            ref passed,
            ref failed,
            lines);
        CheckMethod(
            "TrySetAvailability",
            typeof(BistroBuilderStaffPlayerFacade),
            ref passed,
            ref failed,
            lines);
        CheckMethod(
            "TryTrainEmployee",
            typeof(BistroBuilderStaffPlayerFacade),
            ref passed,
            ref failed,
            lines);

        Check(
            !typeof(BistroBuilderStaffPlayerUiSnapshot).IsSubclassOf(typeof(UnityEngine.Object)),
            "El snapshot de UI no es un Unity Object autoritativo.",
            ref passed,
            ref failed,
            lines);
        Check(
            !typeof(BistroBuilderStaffPlayerEmployeeRow).IsSubclassOf(typeof(UnityEngine.Object)),
            "Las filas de empleado son DTO de lectura.",
            ref passed,
            ref failed,
            lines);
        Check(
            !typeof(BistroBuilderStaffPlayerCandidateRow).IsSubclassOf(typeof(UnityEngine.Object)),
            "Las filas de candidato son DTO de lectura.",
            ref passed,
            ref failed,
            lines);

        report =
            "4F — AUTOTEST ESTÁTICO PRESENTATION\n" +
            "Correctos: " + passed + "\n" +
            "Fallos: " + failed + "\n\n" +
            string.Join("\n", lines);
        return failed == 0;
    }

    private static void CheckMethod(
        string methodName,
        Type type,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        bool exists = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public) != null;
        Check(
            exists,
            "Contrato público disponible: " + methodName + ".",
            ref passed,
            ref failed,
            lines);
    }

    private static void Check(
        bool condition,
        string description,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + description);
            return;
        }

        failed++;
        lines.Add("[ERROR] " + description);
    }
}
