using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2AValidator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/Validar")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2A - VALIDACION");

        BistroBuilderSpatialInteractionService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialAssessmentService>();
        BistroBuilderSpatialRuntimeBinder binder =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialRuntimeBinder>();
        Check(service != null, "Autoridad BBSIS disponible", report);
        Check(assessment != null && assessment.ValidateConfiguration(out _),
            "Spatial Assessment configurado", report);
        Check(binder != null && binder.ValidateConfiguration(out _),
            "Runtime Binder data-driven configurado", report);

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(seats.Length > 0, "Sillas reales detectadas", report);
        Check(AllSeatsBound(seats),
            "Todas las sillas reales tienen Subject/Contract/Proxy/Adapter", report);
        Check(AllSeatPortsResolve(seats),
            "Seat y Approach Ports resuelven anclajes reales", report);

        RestaurantTableSeatingConfiguration[] tables =
            UnityEngine.Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(tables.Length > 0, "Mesas reales detectadas", report);
        Check(AllTablesBound(tables),
            "Todas las mesas reales tienen Subject/Contract/Proxy/Adapter", report);
        Check(AllTablePortsMatchCapacity(tables),
            "Seat Bays contractuales coinciden con capacidad real", report);

        BistroBuilderNavigableDoor[] doors =
            UnityEngine.Object.FindObjectsByType<BistroBuilderNavigableDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(AllDoorsBound(doors),
            "Puertas presentes exponen Subject/Contract/Sweep Adapter", report);

        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSpatialSubject>(
                FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        Check(AllSubjectsValidAndUnique(subjects),
            "Subjects espaciales válidos, estables y únicos", report);
        Check(NoSpatialTypeRequiresRigidbody(),
            "BBSIS 2A no introduce Rigidbody obligatorio", report);
        Check(typeof(BistroBuilderSpatialInteractionService).GetMethod(
                "TryFindStaticGeometryConflict") != null,
            "Preflight contra geometría estática disponible", report);

        if (assessment != null)
        {
            BistroBuilderSpatialQualityResult first = assessment.EvaluateCurrentLayout();
            string firstSignature = BuildQualitySignature(first);
            BistroBuilderSpatialQualityResult second = assessment.EvaluateCurrentLayout();
            Check(first != null && second != null,
                "Spatial Quality produce diagnóstico", report);
            Check(string.Equals(firstSignature, BuildQualitySignature(second),
                    StringComparison.Ordinal),
                "Spatial Quality es determinista", report);
            Check(assessment.LastLedger != null &&
                  assessment.LastLedger.flowQuality >= 0f &&
                  assessment.LastLedger.flowQuality <= 1f,
                "Bottleneck Ledger y Flow Quality disponibles", report);
        }
        else
        {
            Check(false, "Spatial Quality produce diagnóstico", report);
            Check(false, "Spatial Quality es determinista", report);
            Check(false, "Bottleneck Ledger y Flow Quality disponibles", report);
        }

        Check(ContractsAreVersionTwoOrNewer(subjects),
            "Contratos reales de Fase 2A versionados", report);

        report.AppendLine("Resultado: " + LastPassed + " OK / " + LastFailed + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException("Validación BBSIS Fase 2A fallida.");
    }

    private static bool AllSeatsBound(RestaurantSeat[] seats)
    {
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat seat = seats[i];
            if (seat == null) continue;
            BistroBuilderSpatialSubject subject = seat.GetComponent<BistroBuilderSpatialSubject>();
            BistroBuilderAdaptiveSpatialProxy proxy = seat.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (subject == null || proxy == null ||
                seat.GetComponent<BistroBuilderSeatSpatialAdapter>() == null ||
                subject.Contract == null ||
                subject.Contract.FamilyId != "seating.chair" ||
                proxy.Mode != BistroBuilderAdaptiveSpatialProxyMode.Articulated)
                return false;
        }
        return true;
    }

    private static bool AllSeatPortsResolve(RestaurantSeat[] seats)
    {
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat seat = seats[i];
            if (seat == null) continue;
            BistroBuilderSpatialSubject subject = seat.GetComponent<BistroBuilderSpatialSubject>();
            if (subject == null ||
                !subject.TryGetPortWorld("seat", out Vector3 seatPosition, out _, out _, out _) ||
                !subject.TryGetPortWorld("approach", out Vector3 approachPosition, out _, out _, out _))
                return false;
            if (seat.SeatPoint == null || seat.CustomerApproachPoint == null ||
                Vector3.Distance(seatPosition, seat.SeatPoint.position) > 0.01f ||
                Vector3.Distance(approachPosition, seat.CustomerApproachPoint.position) > 0.01f)
                return false;
        }
        return true;
    }

    private static bool AllTablesBound(RestaurantTableSeatingConfiguration[] tables)
    {
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTableSeatingConfiguration table = tables[i];
            if (table == null) continue;
            BistroBuilderSpatialSubject subject = table.GetComponent<BistroBuilderSpatialSubject>();
            BistroBuilderAdaptiveSpatialProxy proxy = table.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (subject == null || proxy == null ||
                table.GetComponent<BistroBuilderTableSpatialAdapter>() == null ||
                subject.Contract == null ||
                subject.Contract.FamilyId != "seating.table" ||
                proxy.Mode != BistroBuilderAdaptiveSpatialProxyMode.Layered)
                return false;
        }
        return true;
    }

    private static bool AllTablePortsMatchCapacity(
        RestaurantTableSeatingConfiguration[] tables)
    {
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTableSeatingConfiguration table = tables[i];
            if (table == null || table.Definition == null) continue;
            BistroBuilderSpatialSubject subject = table.GetComponent<BistroBuilderSpatialSubject>();
            if (subject == null || subject.Contract == null ||
                subject.Contract.Ports.Count != table.MaximumCustomers)
                return false;
            for (int portIndex = 0; portIndex < subject.Contract.Ports.Count; portIndex++)
            {
                BistroBuilderSpatialPortDefinition port = subject.Contract.Ports[portIndex];
                if (port == null || port.kind != BistroBuilderSpatialPortKind.SeatBay)
                    return false;
            }
        }
        return true;
    }

    private static bool AllDoorsBound(BistroBuilderNavigableDoor[] doors)
    {
        for (int i = 0; i < doors.Length; i++)
        {
            BistroBuilderNavigableDoor door = doors[i];
            if (door == null) continue;
            BistroBuilderSpatialSubject subject = door.GetComponent<BistroBuilderSpatialSubject>();
            if (subject == null || subject.Contract == null ||
                subject.Contract.FamilyId != "architecture.door" ||
                door.GetComponent<BistroBuilderDoorSpatialAdapter>() == null)
                return false;
        }
        return true;
    }

    private static bool AllSubjectsValidAndUnique(BistroBuilderSpatialSubject[] subjects)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null) continue;
            if (!subject.ValidateSubject(out _) ||
                string.IsNullOrWhiteSpace(subject.SubjectId) ||
                !ids.Add(subject.SubjectId))
                return false;
        }
        return true;
    }

    private static bool NoSpatialTypeRequiresRigidbody()
    {
        Type[] types =
        {
            typeof(BistroBuilderSpatialSubject),
            typeof(BistroBuilderAdaptiveSpatialProxy),
            typeof(BistroBuilderSeatSpatialAdapter),
            typeof(BistroBuilderTableSpatialAdapter),
            typeof(BistroBuilderDoorSpatialAdapter),
            typeof(BistroBuilderSpatialAssessmentService),
            typeof(BistroBuilderSpatialRuntimeBinder)
        };
        for (int i = 0; i < types.Length; i++)
        {
            object[] attributes = types[i].GetCustomAttributes(typeof(RequireComponent), true);
            for (int a = 0; a < attributes.Length; a++)
            {
                RequireComponent require = attributes[a] as RequireComponent;
                if (require == null) continue;
                var fields = typeof(RequireComponent).GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    if (fields[f].FieldType == typeof(Type) &&
                        ReferenceEquals(fields[f].GetValue(require), typeof(Rigidbody)))
                        return false;
                }
            }
        }
        return true;
    }

    private static bool ContractsAreVersionTwoOrNewer(BistroBuilderSpatialSubject[] subjects)
    {
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null || subject.Contract == null) continue;
            string family = subject.Contract.FamilyId;
            if ((family == "seating.chair" || family == "seating.table" ||
                 family == "architecture.door") &&
                subject.Contract.ContractVersion < 2)
                return false;
        }
        return true;
    }

    private static string BuildQualitySignature(BistroBuilderSpatialQualityResult result)
    {
        if (result == null) return "null";
        StringBuilder builder = new StringBuilder();
        builder.Append(result.viable ? "1" : "0");
        builder.Append('|').Append(result.quality.ToString("0.000000"));
        for (int i = 0; i < result.diagnostics.Count; i++)
        {
            BistroBuilderSpatialDiagnostic diagnostic = result.diagnostics[i];
            if (diagnostic == null) continue;
            builder.Append('|').Append(diagnostic.diagnosticId)
                .Append(':').Append(diagnostic.blocking ? '1' : '0');
        }
        return builder.ToString();
    }

    private static void Check(bool condition, string label, StringBuilder report)
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
