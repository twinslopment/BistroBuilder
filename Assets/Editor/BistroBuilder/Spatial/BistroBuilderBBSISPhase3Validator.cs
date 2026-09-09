using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Valida el hardening de producción y determinismo de BBSIS.
/// </summary>
public static class BistroBuilderBBSISPhase3Validator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 3/Validar")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 3 - VALIDACION");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();

        Check(spatial != null && spatial.ValidateConfiguration(out _),
            "Autoridad BBSIS configurada", report);
        Check(assessment != null && assessment.ValidateConfiguration(out _),
            "Spatial Assessment configurado", report);
        Check(typeof(BistroBuilderSpatialInteractionService)
                .GetProperty("GrantedLeaseCount") != null &&
              typeof(BistroBuilderSpatialInteractionService)
                .GetProperty("RejectedLeaseCount") != null,
            "Telemetria de concesiones y rechazos disponible", report);
        Check(typeof(BistroBuilderSpatialInteractionService)
                .GetMethod("ResetTransientRuntimeStateAfterLoad") != null,
            "Reset transitorio post-Load disponible", report);
        Check(typeof(BistroBuilderSpatialInteractionService)
                .GetMethod("TryGetSubject") != null,
            "Limpieza de subjects obsoletos disponible", report);
        Check(typeof(BistroBuilderSpatialInteractionService)
                .GetMethod("TryAcquireLease") != null,
            "Arbitraje determinista de leases disponible", report);
        Check(spatial == null || spatial.ActiveLeaseCount >= 0,
            "Ledger de leases consistente", report);
        Check(spatial == null || spatial.ActiveEpisodeCount >= 0,
            "Ledger de episodios consistente", report);
        Check(assessment == null ||
              assessment.LastLedger != null,
            "Bottleneck Ledger disponible", report);
        Check(assessment == null ||
              assessment.LastResult != null,
            "Spatial Quality disponible", report);

        Finish(report);
    }

    private static void Check(
        bool condition,
        string label,
        StringBuilder report)
    {
        if (condition)
        {
            LastPassed++;
            report.AppendLine("OK - " + label);
        }
        else
        {
            LastFailed++;
            report.AppendLine("FAIL - " + label);
        }
    }
    private static void Finish(StringBuilder report)
    {
        report.AppendLine(
            "Resultado: " + LastPassed + " OK / " +
            LastFailed + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Validacion BBSIS Fase 3 fallida.");
    }
}
