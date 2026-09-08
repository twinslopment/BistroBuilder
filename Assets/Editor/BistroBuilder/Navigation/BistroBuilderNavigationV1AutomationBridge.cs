using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puente Tool-First para lanzar los autotests de Navigation v1
/// desde automatización externa sin cerrar el Editor activo.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderNavigationV1AutomationBridge
{
    private const string RequestFile = "NavigationV1CoreTest.request";
    private const string ReportFile = "NavigationV1CoreTest_AutoReport.txt";
    private static bool running;

    static BistroBuilderNavigationV1AutomationBridge()
    {
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (running || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string root = Directory.GetParent(Application.dataPath).FullName;
        string request = Path.Combine(root, RequestFile);
        if (!File.Exists(request)) return;

        running = true;
        try
        {
            File.Delete(request);
            BistroBuilderNavigationV1CoreSelfTest.Run();
            WriteReport(root, true, null);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteReport(root, false, exception);
        }
        finally
        {
            running = false;
        }
    }
    private static void WriteReport(string root, bool pass, Exception exception)
    {
        string text =
            "=== BISTRO BUILDER - NAVIGATION & CROWD FLOW V1 CORE ===\n" +
            (pass ? "[PASS]\n" : "[FAIL]\n") +
            BistroBuilderNavigationV1CoreSelfTest.LastReport;
        if (exception != null)
            text += "\nEXCEPTION:\n" + exception;

        File.WriteAllText(Path.Combine(root, ReportFile), text);
    }
}
