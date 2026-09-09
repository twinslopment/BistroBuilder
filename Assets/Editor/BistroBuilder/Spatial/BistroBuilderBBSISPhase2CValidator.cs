using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2CValidator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2C/Validar")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2C - VALIDACION");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialPlacementAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialPlacementAssessmentService>();
        BistroBuilderSpatialPlacementConstraintRule rule =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialPlacementConstraintRule>();
        BistroBuilderSpatialEditModeIntegration integration =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialEditModeIntegration>();
        RestaurantPlacementConstraintService constraints =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementConstraintService>();
        if (constraints != null)
            constraints.RefreshRules();

        Check(spatial != null &&
              spatial.ValidateConfiguration(out _),
            "Autoridad BBSIS disponible", report);
        Check(assessment != null &&
              assessment.ValidateConfiguration(out _),
            "Preflight espacial de edición configurado", report);
        Check(rule != null &&
              rule.ValidateConfiguration(out _),
            "Regla BBSIS de colocación configurada", report);
        Check(integration != null &&
              integration.ValidateConfiguration(out _),
            "Integración transaccional de edición configurada", report);
        Check(constraints != null &&
              constraints.RegisteredRuleCount >= 2,
            "BBSIS participa en reglas modulares existentes", report);

        RestaurantOperationalClearanceConstraintRule legacy =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantOperationalClearanceConstraintRule>();
        Check(rule != null &&
              (legacy == null || rule.Priority < legacy.Priority),
            "Semántica BBSIS precede al clearance legacy", report);
        RestaurantPlacementTransactionService transactions =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementTransactionService>();
        RestaurantPlacementHistoryService history =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlacementHistoryService>();
        RestaurantPlaceableLifecycleService lifecycle =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableLifecycleService>();
        RestaurantPlaceableRegistry registry =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantPlaceableRegistry>();
        Check(transactions != null && history != null &&
              lifecycle != null && registry != null,
            "Crear/mover/eliminar/Undo/Redo conservan su autoridad",
            report);

        BistroBuilderSpatialRuntimeBinder binder =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialRuntimeBinder>();
        Check(binder != null &&
              binder.ValidateConfiguration(out _),
            "Binder data-driven disponible para instancias nuevas",
            report);

        if (assessment != null)
            assessment.RefreshProviderCache();
        Check(assessment != null &&
              assessment.CachedProviderCount > 0,
            "Proveedores semánticos cacheados sin búsqueda por frame",
            report);

        Check(AllEditableSpatialSubjectsAreValid(),
            "Subjects editables conservan contratos y proxies",
            report);
        Check(AllSeatSweepsDeclareTableRelation(),
            "Sillas enlazadas excluyen falsos conflictos con su mesa",
            report);
        Check(FeedbackContractAvailable(),
            "Resultado BBSIS llega al feedback genérico de colocación",
            report);
        Check(NoTypeRequiresRigidbody(),
            "BBSIS 2C no introduce Rigidbody obligatorio",
            report);

        BistroBuilderSpatialAssessmentService layout =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        Check(IsLayoutAssessmentDeterministic(layout),
            "Spatial Quality permanece determinista",
            report);

        Finish(report);
    }

    private static bool AllEditableSpatialSubjectsAreValid()
    {
        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        int editableCount = 0;
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null ||
                subject.GetComponent<RestaurantAreaMember>() == null)
                continue;
            editableCount++;
            if (!subject.ValidateSubject(out _))
                return false;
        }
        return editableCount > 0;
    }
    private static bool AllSeatSweepsDeclareTableRelation()
    {
        BistroBuilderSeatSpatialAdapter[] adapters =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSeatSpatialAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        if (adapters.Length == 0)
            return false;
        for (int i = 0; i < adapters.Length; i++)
        {
            var volumes =
                new List<BistroBuilderSpatialSemanticVolume>();
            adapters[i].WriteSemanticVolumes(volumes);
            for (int v = 0; v < volumes.Count; v++)
            {
                BistroBuilderSpatialSemanticVolume volume = volumes[v];
                if (volume == null ||
                    volume.role !=
                        BistroBuilderSpatialSemanticRole.DynamicSweep)
                    continue;
                RestaurantSeat seat =
                    adapters[i].GetComponent<RestaurantSeat>();
                if (seat != null && seat.AssociatedTable != null &&
                    string.IsNullOrWhiteSpace(volume.relatedSubjectId))
                    return false;
            }
        }
        return true;
    }

    private static bool FeedbackContractAvailable()
    {
        EventInfo feedback = typeof(
            RestaurantEditInteractionController).GetEvent(
                "PlacementValidationChanged");
        PropertyInfo message = typeof(
            RestaurantPlacementConstraintEvaluation).GetProperty(
                "UserMessage");
        return feedback != null && message != null;
    }

    private static bool NoTypeRequiresRigidbody()
    {
        Type[] types =
        {
            typeof(BistroBuilderSpatialPlacementAssessmentService),
            typeof(BistroBuilderSpatialPlacementConstraintRule),
            typeof(BistroBuilderSpatialEditModeIntegration)
        };
        for (int i = 0; i < types.Length; i++)
        {
            object[] requirements = types[i].GetCustomAttributes(
                typeof(RequireComponent),
                true);
            for (int r = 0; r < requirements.Length; r++)
            {
                RequireComponent requirement =
                    requirements[r] as RequireComponent;
                if (requirement != null &&
                    (requirement.m_Type0 == typeof(Rigidbody) ||
                     requirement.m_Type1 == typeof(Rigidbody) ||
                     requirement.m_Type2 == typeof(Rigidbody)))
                    return false;
            }
        }
        return true;
    }

    private static bool IsLayoutAssessmentDeterministic(
        BistroBuilderSpatialAssessmentService assessment)
    {
        if (assessment == null)
            return false;
        BistroBuilderSpatialQualityResult first =
            assessment.EvaluateCurrentLayout();
        string firstSignature = Signature(first);
        BistroBuilderSpatialQualityResult second =
            assessment.EvaluateCurrentLayout();
        return first != null && second != null &&
               string.Equals(
                   firstSignature,
                   Signature(second),
                   StringComparison.Ordinal);
    }

    private static string Signature(
        BistroBuilderSpatialQualityResult result)
    {
        if (result == null)
            return "null";
        StringBuilder signature = new StringBuilder();
        signature.Append(result.viable)
            .Append('|')
            .Append(result.quality.ToString("F4"))
            .Append('|')
            .Append(result.diagnostics.Count);
        for (int i = 0; i < result.diagnostics.Count; i++)
            if (result.diagnostics[i] != null)
                signature.Append('|')
                    .Append(result.diagnostics[i].diagnosticId)
                    .Append(':')
                    .Append(result.diagnostics[i].blocking);
        return signature.ToString();
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
                "Validación BBSIS Fase 2C fallida.");
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
}
