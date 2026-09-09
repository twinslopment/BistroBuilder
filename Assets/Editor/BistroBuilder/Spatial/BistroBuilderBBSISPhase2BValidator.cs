using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2BValidator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } =
        string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2B/Validar")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2B - VALIDACION");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderOperationalSpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOperationalSpatialCoordinator>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialAssessmentService>();
        Check(spatial != null &&
              spatial.ValidateConfiguration(out _),
            "Autoridad BBSIS disponible", report);
        Check(coordinator != null &&
              coordinator.ValidateConfiguration(out _),
            "Coordinador operacional configurado", report);

        KitchenSystem[] kitchens =
            UnityEngine.Object.FindObjectsByType<KitchenSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        Check(kitchens.Length > 0,
            "Cocina real detectada", report);
        Check(AllKitchensBound(kitchens),
            "Cocina publica Subject/Contract/Proxy/Adapter",
            report);
        Check(AllKitchenPassAnchorsResolve(kitchens),
            "Pass sigue el PickupPoint real", report);

        BistroBuilderKitchenStationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderKitchenStationCatalog>(
                "Assets/Data/Kitchen/" +
                "BB_Kitchen_Station_Catalog.asset");
        Check(catalog != null && catalog.TryValidate(out _),
            "Catálogo de estaciones válido", report);
        Check(KitchenContractMatchesCatalog(kitchens, catalog),
            "Work Ports y Work Edges cubren estaciones/capacidad",
            report);

        BistroBuilderBarServiceSpot[] spots =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderBarServiceSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        Check(spots.Length > 0,
            "Plazas reales de barra detectadas", report);
        Check(AllBarSpotsBound(spots),
            "Todas las plazas publican contrato y proxy Layered",
            report);
        Check(AllBarAnchorsResolve(spots),
            "Puertos de cliente/camarero resuelven anclajes reales",
            report);

        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderSpatialSubject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        Check(AllSubjectsValidAndUnique(subjects),
            "Subjects BBSIS siguen válidos y únicos", report);
        Check(OperationalContractsAreVersionThree(subjects),
            "Contratos operacionales versionados", report);
        Check(NoOperationalTypeRequiresRigidbody(),
            "BBSIS 2B no exige Rigidbody", report);

        MethodInfo gateMethod = typeof(
            IBistroBuilderOperationalSpatialGate).GetMethod(
                "TryAcquireKitchenWork");
        FieldInfo integrationField = typeof(
            BistroBuilderAdvancedKitchenService).GetField(
                "spatialGate",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
        Check(gateMethod != null && integrationField != null,
            "Cocina avanzada consume el gate BBSIS desacoplado",
            report);

        if (assessment != null)
        {
            BistroBuilderSpatialQualityResult first =
                assessment.EvaluateCurrentLayout();
            string firstSignature = Signature(first);
            BistroBuilderSpatialQualityResult second =
                assessment.EvaluateCurrentLayout();
            Check(first != null && second != null &&
                  string.Equals(
                      firstSignature,
                      Signature(second),
                      StringComparison.Ordinal),
                "Spatial Quality operacional es determinista",
                report);
            Check(assessment.LastLedger != null &&
                  assessment.LastLedger.flowQuality >= 0f &&
                  assessment.LastLedger.flowQuality <= 1f,
                "Flow Quality y Bottleneck Ledger operativos",
                report);
        }
        else
        {
            Check(false,
                "Spatial Quality operacional es determinista",
                report);
            Check(false,
                "Flow Quality y Bottleneck Ledger operativos",
                report);
        }

        report.AppendLine(
            "Resultado: " + LastPassed + " OK / " +
            LastFailed + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Validación BBSIS Fase 2B fallida.");
    }

    private static bool AllKitchensBound(
        KitchenSystem[] kitchens)
    {
        for (int i = 0; i < kitchens.Length; i++)
        {
            KitchenSystem kitchen = kitchens[i];
            if (kitchen == null)
                continue;
            BistroBuilderSpatialSubject subject =
                kitchen.GetComponent<
                    BistroBuilderSpatialSubject>();
            BistroBuilderKitchenSpatialAdapter adapter =
                kitchen.GetComponent<
                    BistroBuilderKitchenSpatialAdapter>();
            if (subject == null || adapter == null ||
                subject.Contract == null ||
                subject.Contract.FamilyId != "work.kitchen" ||
                subject.Proxy == null ||
                subject.Proxy.Mode !=
                    BistroBuilderAdaptiveSpatialProxyMode.Layered ||
                !adapter.ValidateConfiguration(out _))
                return false;
        }
        return true;
    }

    private static bool AllKitchenPassAnchorsResolve(
        KitchenSystem[] kitchens)
    {
        for (int i = 0; i < kitchens.Length; i++)
        {
            KitchenSystem kitchen = kitchens[i];
            if (kitchen == null)
                continue;
            BistroBuilderSpatialSubject subject =
                kitchen.GetComponent<
                    BistroBuilderSpatialSubject>();
            if (subject == null ||
                kitchen.PickupPoint == null ||
                !subject.TryGetPortWorld(
                    BistroBuilderKitchenSpatialAdapter.PassPortId,
                    out Vector3 position,
                    out _,
                    out _,
                    out _) ||
                Vector3.Distance(
                    position,
                    kitchen.PickupPoint.position) > 0.01f)
                return false;
        }
        return true;
    }

    private static bool KitchenContractMatchesCatalog(
        KitchenSystem[] kitchens,
        BistroBuilderKitchenStationCatalog catalog)
    {
        if (catalog == null)
            return false;
        int expectedPorts = 1;
        int expectedEdges = 0;
        for (int i = 0; i < catalog.Stations.Count; i++)
        {
            BistroBuilderKitchenStationDefinition station =
                catalog.Stations[i];
            if (station == null)
                continue;
            expectedPorts += Mathf.Max(1, station.baseCapacity);
            expectedEdges++;
        }

        for (int i = 0; i < kitchens.Length; i++)
        {
            BistroBuilderSpatialSubject subject =
                kitchens[i] != null
                    ? kitchens[i].GetComponent<
                        BistroBuilderSpatialSubject>()
                    : null;
            if (subject == null ||
                subject.Contract == null ||
                subject.Contract.Ports.Count != expectedPorts ||
                subject.Contract.WorkEdges.Count != expectedEdges)
                return false;
        }
        return true;
    }

    private static bool AllBarSpotsBound(
        BistroBuilderBarServiceSpot[] spots)
    {
        for (int i = 0; i < spots.Length; i++)
        {
            BistroBuilderBarServiceSpot spot = spots[i];
            if (spot == null)
                continue;
            BistroBuilderSpatialSubject subject =
                spot.GetComponent<
                    BistroBuilderSpatialSubject>();
            BistroBuilderBarSpatialAdapter adapter =
                spot.GetComponent<
                    BistroBuilderBarSpatialAdapter>();
            if (subject == null || adapter == null ||
                subject.Contract == null ||
                subject.Contract.FamilyId != "work.bar" ||
                subject.Proxy == null ||
                subject.Proxy.Mode !=
                    BistroBuilderAdaptiveSpatialProxyMode.Layered ||
                !adapter.ValidateConfiguration(out _))
                return false;
        }
        return true;
    }

    private static bool AllBarAnchorsResolve(
        BistroBuilderBarServiceSpot[] spots)
    {
        for (int i = 0; i < spots.Length; i++)
        {
            BistroBuilderBarServiceSpot spot = spots[i];
            if (spot == null)
                continue;
            BistroBuilderSpatialSubject subject =
                spot.GetComponent<
                    BistroBuilderSpatialSubject>();
            if (subject == null ||
                !subject.TryGetPortWorld(
                    BistroBuilderBarSpatialAdapter.CustomerPortId,
                    out Vector3 customer,
                    out _,
                    out _,
                    out _) ||
                !subject.TryGetPortWorld(
                    BistroBuilderBarSpatialAdapter.ServicePortId,
                    out Vector3 service,
                    out _,
                    out _,
                    out _) ||
                Vector3.Distance(
                    customer,
                    spot.CustomerPoint.position) > 0.01f ||
                Vector3.Distance(
                    service,
                    spot.WaiterServicePoint.position) > 0.01f)
                return false;
        }
        return true;
    }

    private static bool AllSubjectsValidAndUnique(
        BistroBuilderSpatialSubject[] subjects)
    {
        var ids = new HashSet<string>(
            StringComparer.Ordinal);
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null)
                continue;
            if (!subject.ValidateSubject(out _) ||
                !ids.Add(subject.SubjectId))
                return false;
        }
        return true;
    }

    private static bool OperationalContractsAreVersionThree(
        BistroBuilderSpatialSubject[] subjects)
    {
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null || subject.Contract == null)
                continue;
            string family = subject.Contract.FamilyId;
            if ((family == "work.kitchen" ||
                 family == "work.bar") &&
                subject.Contract.ContractVersion < 3)
                return false;
        }
        return true;
    }

    private static bool NoOperationalTypeRequiresRigidbody()
    {
        Type[] types =
        {
            typeof(BistroBuilderKitchenSpatialAdapter),
            typeof(BistroBuilderBarSpatialAdapter),
            typeof(BistroBuilderOperationalSpatialCoordinator)
        };
        for (int i = 0; i < types.Length; i++)
        {
            object[] attributes = types[i].GetCustomAttributes(
                typeof(RequireComponent), true);
            for (int a = 0; a < attributes.Length; a++)
                if (attributes[a] is RequireComponent require &&
                    RequiresRigidbody(require))
                    return false;
        }
        return true;
    }

    private static bool RequiresRigidbody(
        RequireComponent require)
    {
        FieldInfo[] fields = typeof(RequireComponent).GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].FieldType == typeof(Type) &&
                ReferenceEquals(
                    fields[i].GetValue(require),
                    typeof(Rigidbody)))
                return true;
        return false;
    }

    private static string Signature(
        BistroBuilderSpatialQualityResult result)
    {
        if (result == null)
            return "null";
        StringBuilder builder = new StringBuilder();
        builder.Append(result.viable ? "1" : "0")
            .Append('|')
            .Append(result.quality.ToString("0.000000"));
        for (int i = 0; i < result.diagnostics.Count; i++)
        {
            BistroBuilderSpatialDiagnostic diagnostic =
                result.diagnostics[i];
            if (diagnostic != null)
                builder.Append('|')
                    .Append(diagnostic.diagnosticId)
                    .Append(':')
                    .Append(diagnostic.blocking ? '1' : '0');
        }
        return builder.ToString();
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
