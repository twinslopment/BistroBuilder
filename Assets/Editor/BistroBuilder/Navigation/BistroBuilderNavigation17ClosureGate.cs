using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderNavigation17ClosureGate
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string PlayReport = "Navigation17PlayModeReport.txt";
    private const string SaveLoadReport = "Navigation17SaveLoadReport.txt";
    private const string WaitersReport = "AdvancedWaiters13PlayModeReport.txt";
    private const string FrontReport = "AdvancedFrontOfHouse14PlayModeReport.txt";
    private const string Stress50Report = "Navigation17StressSoakReport.txt";
    private const string Stress100Report = "Navigation17StressSoak100Report.txt";
    private const string ClosureReport = "Navigation17ClosureGateReport.txt";

    [MenuItem("Bistro Builder/17 Navegacion/Closure gate")]
    private static void RunFromMenu()
    {
        try { Debug.Log(Run()); }
        catch (Exception exception) { Debug.LogError(exception.Message); }
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Debug.Log(Run());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("No pudo abrirse la escena canonica del Bloque 17.");

        BistroBuilderNavigation17Validator.Run();
        int validationPassed = BistroBuilderNavigation17Validator.LastPassed;
        int validationFailed = BistroBuilderNavigation17Validator.LastFailed;

        BistroBuilderNavigation17SelfTest.Run();
        int selfPassed = BistroBuilderNavigation17SelfTest.LastPassed;
        int selfFailed = BistroBuilderNavigation17SelfTest.LastFailed;

        BistroBuilderNavigationV1CoreSelfTest.Run();
        int corePassed = BistroBuilderNavigationV1CoreSelfTest.LastPassed;
        int coreFailed = BistroBuilderNavigationV1CoreSelfTest.LastFailed;

        int reportPassed = 0;
        StringBuilder reportChecks = new StringBuilder();
        CheckReport(PlayReport, "Play Mode real 17", ref reportPassed, reportChecks);
        CheckReport(SaveLoadReport, "Save/Load real especifico 17", ref reportPassed, reportChecks);
        CheckReport(WaitersReport, "Regresion Camareros 13", ref reportPassed, reportChecks);
        CheckReport(FrontReport, "Regresion Sala 14", ref reportPassed, reportChecks);
        CheckReport(Stress50Report, "Stress/Soak 50 NPC", ref reportPassed, reportChecks);
        CheckReport(Stress100Report, "Stress/Soak 100 NPC", ref reportPassed, reportChecks);
        float stress50P95 = ReadStress50P95(Stress50Report);
        bool performanceOk = stress50P95 <= 2f;
        reportChecks.AppendLine((performanceOk ? "OK - " : "FAIL - ") +
            "Stress/Soak 50 NPC p95 <= 2 ms (" + stress50P95.ToString("0.000") + " ms)");
        if (!performanceOk) throw new InvalidOperationException("Navigation p95 supera 2 ms con 50 NPC.");

        if (validationFailed > 0 || selfFailed > 0 || coreFailed > 0 || reportPassed != 6)
            throw new InvalidOperationException("Closure gate 17 fallido.");

        int accumulated = validationPassed + selfPassed + corePassed + reportPassed + 1;
        string result = "=== BISTRO BUILDER - BLOQUE 17 / CLOSURE GATE ===\n" +
                        BistroBuilderNavigation17Validator.LastReport + "\n" +
                        BistroBuilderNavigation17SelfTest.LastReport + "\n" +
                        BistroBuilderNavigationV1CoreSelfTest.LastReport + "\n" +
                        reportChecks +
                        "Resultado acumulado: " + accumulated + " OK / 0 fallos.";
        File.WriteAllText(Path.GetFullPath(ClosureReport), result);
        return result;
    }

    private static float ReadStress50P95(string relativePath)
    {
        string text = File.ReadAllText(Path.GetFullPath(relativePath));
        var match = System.Text.RegularExpressions.Regex.Match(text, @"navP95Ms=([0-9]+[\.,][0-9]+)");
        if (!match.Success) throw new InvalidOperationException("Stress 50 no contiene navP95Ms.");
        string value = match.Groups[1].Value.Replace(',', '.');
        if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float p95))
            throw new InvalidOperationException("navP95Ms de Stress 50 no es valido.");
        return p95;
    }
    private static void CheckReport(
        string relativePath,
        string label,
        ref int passed,
        StringBuilder report)
    {
        string path = Path.GetFullPath(relativePath);
        bool ok = File.Exists(path) &&
                  File.ReadAllText(path).IndexOf("[PASS]", StringComparison.Ordinal) >= 0;
        report.AppendLine((ok ? "OK - " : "FAIL - ") + label);
        if (!ok)
            throw new InvalidOperationException("Falta evidencia PASS actual para " + label + ".");
        passed++;
    }
}


