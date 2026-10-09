using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>B11: known-bad deterministic projection, dormant-budget gate,
/// real Unity scene read-only and repeatable authority scans.</summary>
public static class BistroBuilderEditorV2B11DiagnosisSelfTest
{
    private const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string Report = "EditorV2_B11_Diagnosis_Report.txt";
    private static readonly List<string> lines = new List<string>(96);
    private static int passed, failed;

    [MenuItem("Bistro Builder/QA/Editor V2/B11 Diagnosis Self Test")]
    public static void RunFromMenu() => Run();

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        lines.Clear(); passed = 0; failed = 0;
        lines.Add("B11 - DIAGNOSTICO DEL RESTAURANTE");
        try
        {
            TestProjection();
            TestRealScene();
        }
        catch (Exception ex) { Check(false, "Excepcion: " + ex); }
        finally
        {
            lines.Add("Resultado: " + passed + " OK / " + failed + " fallos.");
            File.WriteAllLines(Path.Combine(
                Directory.GetCurrentDirectory(), Report), lines);
            foreach (string line in lines)
                UnityEngine.Debug.Log("[B11] " + line);
        }
        return failed == 0;
    }

    private static void TestProjection()
    {
        var r = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = BistroBuilderEditorV2DiagnosisLayer.All
        };
        var warning = Finding("nav:blocked", BistroBuilderEditorV2DiagnosisLayer.Circulation,
            BistroBuilderEditorV2DiagnosisSeverity.Blocking, "No se puede atravesar la sala");
        var less = Finding("seat:unassociated", BistroBuilderEditorV2DiagnosisLayer.Capacity,
            BistroBuilderEditorV2DiagnosisSeverity.Warning, "Silla sin mesa");
        var advisory = Finding("layout:note", BistroBuilderEditorV2DiagnosisLayer.Layout,
            BistroBuilderEditorV2DiagnosisSeverity.Information, "Dato");
        Check(BistroBuilderEditorV2DiagnosisComposer.Add(r, less) &&
              BistroBuilderEditorV2DiagnosisComposer.Add(r, warning) &&
              BistroBuilderEditorV2DiagnosisComposer.Add(r, advisory),
              "B11 acepta problemas conocidos de rutas, capacidad y distribucion");
        Check(!BistroBuilderEditorV2DiagnosisComposer.Add(r, warning) &&
              r.findings.Count == 3, "identidad estable evita avisos duplicados");

        BistroBuilderEditorV2DiagnosisComposer.Finish(r);
        Check(r.blocking == 1 && r.warnings == 1,
            "cuenta bloqueos y advertencias, no informativos");
        Check(r.findings[0] == warning && r.findings[1] == less,
            "ordena por severidad con resultados reproducibles");
        var filter = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = BistroBuilderEditorV2DiagnosisLayer.Capacity
        };
        Check(!BistroBuilderEditorV2DiagnosisComposer.Add(filter, warning) &&
              BistroBuilderEditorV2DiagnosisComposer.Add(filter, less),
            "filtro por capa no incorpora problemas de otras capas");
        Check(BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.MobilityEnvelope) ==
                  BistroBuilderEditorV2DiagnosisLayer.Circulation &&
              BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.TraversalGate) ==
                  BistroBuilderEditorV2DiagnosisLayer.Circulation,
              "BBSIS identifica movilidad y portales en circulacion");
        Check(BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.SeatBay) ==
                  BistroBuilderEditorV2DiagnosisLayer.Accessibility &&
              BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.Approach) ==
                  BistroBuilderEditorV2DiagnosisLayer.Accessibility,
              "BBSIS identifica entrada a mesa y plazas");
        Check(BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.WorkZone) ==
                  BistroBuilderEditorV2DiagnosisLayer.Interaction &&
              BistroBuilderEditorV2DiagnosisComposer.ForSpatialRole(
                  BistroBuilderSpatialSemanticRole.StaticBody) ==
                  BistroBuilderEditorV2DiagnosisLayer.Layout,
              "BBSIS separa espacio funcional de incidencias generales");
        Check(!string.IsNullOrWhiteSpace(
              BistroBuilderEditorV2DiagnosisComposer.RecommendationForSpatialRole(
                  BistroBuilderSpatialSemanticRole.MobilityEnvelope)) &&
              !string.IsNullOrWhiteSpace(
              BistroBuilderEditorV2DiagnosisComposer.RecommendationForSpatialRole(
                  BistroBuilderSpatialSemanticRole.WorkZone)),
              "propuestas de correccion accionables por tipo de conflicto");
        // Real authority DTOs describe known-bad fixtures. The same adapters
        // are used by B11 in the scene, rather than manually inventing alerts.
        var authorityReport = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = BistroBuilderEditorV2DiagnosisLayer.All
        };
        var bottleneck = new BistroBuilderSpatialBottleneckRecord
        {
            bottleneckId = "known-door-conflict",
            subjectId = "table-1",
            otherSubjectId = "door-1",
            role = BistroBuilderSpatialSemanticRole.TraversalGate,
            blocking = true, severity = 0.96f,
            evidence = "Mesa invade el paso de entrada."
        };
        Vector3 blockedAt = new Vector3(3f, 0f, 2f);
        Check(BistroBuilderEditorV2DiagnosisComposer.AddSpatialBottleneck(
                  authorityReport, bottleneck, true, blockedAt),
            "BBSIS real DTO: mesa obstruye un paso, se convierte en aviso");
        Check(authorityReport.findings[0].layer ==
                  BistroBuilderEditorV2DiagnosisLayer.Circulation &&
              authorityReport.findings[0].severity ==
                  BistroBuilderEditorV2DiagnosisSeverity.Blocking &&
              authorityReport.findings[0].hasWorldPosition &&
              authorityReport.findings[0].worldPosition == blockedAt,
            "BBSIS localiza conflicto del pasillo y lo considera bloqueo");
        var navIssue = new BistroBuilderCirculationIssue
        {
            issueId = "no-route-kitchen",
            severity = BistroBuilderCirculationIssueSeverity.Blocking,
            sourceName = "Cocina -> mesa",
            message = "No se puede circular hasta la mesa."
        };
        Check(BistroBuilderEditorV2DiagnosisComposer.AddNavigationIssue(
                  authorityReport, navIssue) &&
              authorityReport.findings[1].severity ==
                  BistroBuilderEditorV2DiagnosisSeverity.Blocking,
            "Navigation DTO: trayecto cocina-mesa bloqueado");
        var advisoryIssue = new BistroBuilderCirculationIssue
        {
            issueId = "nav-info",
            severity = BistroBuilderCirculationIssueSeverity.Info,
            sourceName = "Zona norte", message = "Sin incidencias."
        };
        Check(BistroBuilderEditorV2DiagnosisComposer.AddNavigationIssue(
                  authorityReport, advisoryIssue) &&
              authorityReport.findings[2].severity ==
                  BistroBuilderEditorV2DiagnosisSeverity.Information,
            "Navigation distingue informacion de advertencia");
        BistroBuilderEditorV2DiagnosisComposer.AddSeatingIssues(
            authorityReport, 6, 2);
        Check(authorityReport.findings.Exists(x =>
                  x.id == "capacity:unassociated-seats" &&
                  x.severity == BistroBuilderEditorV2DiagnosisSeverity.Warning),
            "Seating DTO: dos sillas sin mesa activan problema de capacidad");
        BistroBuilderEditorV2DiagnosisComposer.Finish(authorityReport);
        Check(authorityReport.blocking == 2 &&
              authorityReport.warnings == 1,
            "cuentas de los tres proveedores sin falsas alarmas");
        var noSeats = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = BistroBuilderEditorV2DiagnosisLayer.Capacity
        };
        BistroBuilderEditorV2DiagnosisComposer.AddSeatingIssues(
            noSeats, 0, 0);
        Check(noSeats.findings.Count == 1 &&
              noSeats.findings[0].id == "capacity:no-seats",
            "aforo sin asientos denuncia la capacidad inexistente");
        var capacityOnly = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = BistroBuilderEditorV2DiagnosisLayer.Capacity
        };
        Check(!BistroBuilderEditorV2DiagnosisComposer.AddSpatialBottleneck(
                  capacityOnly, bottleneck, true, blockedAt) &&
              capacityOnly.findings.Count == 0,
              "desactivar circulacion evita mostrar hallazgos BBSIS ajenos");
    }

    private static BistroBuilderEditorV2DiagnosticFinding Finding(
        string id, BistroBuilderEditorV2DiagnosisLayer layer,
        BistroBuilderEditorV2DiagnosisSeverity severity, string description)
        => new BistroBuilderEditorV2DiagnosticFinding
        {
            id = id, layer = layer, severity = severity,
            title = description, explanation = description,
            recommendation = "Corrige la distribucion."
        };

    private static void TestRealScene()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        Bootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));
        var edit = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var diagnosis = Object.FindFirstObjectByType<
            BistroBuilderEditorV2DiagnosisService>(FindObjectsInactive.Include);
        var overlay = Object.FindFirstObjectByType<
            BistroBuilderEditorV2DiagnosisOverlay>(FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlacementRegistry>(
            FindObjectsInactive.Include);
        var history = Object.FindFirstObjectByType<RestaurantPlacementHistoryService>(
            FindObjectsInactive.Include);
        Check(edit != null && diagnosis != null && overlay != null &&
              registry != null && history != null,
            "bootstrap instala servicio y overlay B11 de forma idempotente");
        if (diagnosis == null || edit == null || overlay == null ||
            registry == null || history == null) return;

        int starting = diagnosis.ScanCount;
        Check(!diagnosis.TryScan(BistroBuilderEditorV2DiagnosisLayer.All,
                  out _, out _) && diagnosis.ScanCount == starting,
              "sin modo edicion no diagnostica ni incrementa contador");
        Check(!diagnosis.TryScan(BistroBuilderEditorV2DiagnosisLayer.None,
                  out _, out _) && diagnosis.ScanCount == starting,
              "filtro vacio se rechaza antes de ejecutar consultas");

        bool entered = edit.IsEditModeActive ||
            edit.TryEnterEditMode(out _, out string _);
        Check(entered, "entra en modo edicion con guardas reales");
        if (!entered) return;

        int beforeCount = registry.RegisteredFootprintCount;
        int beforeUndo = history.UndoCount;
        var poses = new Dictionary<int, Vector3>();
        foreach (var footprint in registry.RegisteredFootprints)
            if (footprint != null)
                poses[footprint.GetInstanceID()] =
                    footprint.transform.position;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 50000; i++)
        {
            int still = diagnosis.ScanCount;
            if (still != starting)
            { Check(false, "B11 hizo trabajo autonomo estando desactivado"); break; }
        }
        sw.Stop();
        Check(diagnosis.ScanCount == starting && diagnosis.LastReport == null,
            "cero scans, refrescos o asignaciones de informes cuando esta inactivo");
        lines.Add("METRIC - B11_DORMANT_50000_READS_MS=" +
                  sw.Elapsed.TotalMilliseconds.ToString("F2"));

        bool scanned = diagnosis.TryScan(BistroBuilderEditorV2DiagnosisLayer.All,
            out var report, out string error);
        Check(scanned && report != null && diagnosis.ScanCount == starting + 1,
            "analisis explicito consulta las autoridades existentes: " + error);
        if (!scanned || report == null) return;
        Check(report.scannedLayers == BistroBuilderEditorV2DiagnosisLayer.All &&
              report.evaluations == diagnosis.ScanCount,
            "informe identifica capas y revision de evaluacion");
        Check(report.scannedObjects >= 0 && report.scannedRoutes >= 0 &&
              report.blocking >= 0 && report.warnings >= 0,
            "contadores reales no negativos");
        bool validFindings = true;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in report.findings)
        {
            validFindings &= !string.IsNullOrWhiteSpace(finding.id) &&
                !string.IsNullOrWhiteSpace(finding.title) &&
                !string.IsNullOrWhiteSpace(finding.recommendation) &&
                ids.Add(finding.id);
        }
        Check(validFindings, "avisos unicos y con solucion para el jugador");
        Check(registry.RegisteredFootprintCount == beforeCount &&
              history.UndoCount == beforeUndo && PosesEqual(registry, poses),
            "analizar BBSIS y Navigation no altera posicion, numero ni historial");

        overlay.SelectLayers(BistroBuilderEditorV2DiagnosisLayer.Capacity);
        overlay.SetVisible(true);
        Check(overlay.IsVisible && diagnosis.ScanCount == starting + 2,
            "overlay accesible bajo demanda, escanea una vez al abrir");
        Check(diagnosis.LastReport != null &&
              diagnosis.LastReport.scannedLayers ==
                  BistroBuilderEditorV2DiagnosisLayer.Capacity,
            "filtro capacidad no escanea otras capas");
        overlay.SetVisible(false);
        int dormant = diagnosis.ScanCount;
        for (int i = 0; i < 5000; i++)
        {
            bool hidden = !overlay.IsVisible;
            if (!hidden) break;
        }
        Check(diagnosis.ScanCount == dormant,
            "al cerrar no quedan analisis en segundo plano");

        bool invalid = diagnosis.TryScan(
            BistroBuilderEditorV2DiagnosisLayer.None, out _, out _);
        Check(!invalid && diagnosis.ScanCount == dormant,
            "no se permite solicitar diagnostico vacio");
        Check(registry.RegisteredFootprintCount == beforeCount &&
              history.UndoCount == beforeUndo && PosesEqual(registry, poses),
            "tras filtros y overlay el restaurante sigue intacto");
        Check(diagnosis.TryScan(
                  BistroBuilderEditorV2DiagnosisLayer.Circulation,
                  out var routesOnly, out _) &&
              routesOnly.scannedLayers ==
                  BistroBuilderEditorV2DiagnosisLayer.Circulation,
            "ejecucion independiente de capa circulacion");

        lines.Add("METRIC - B11_SCANS=" + diagnosis.ScanCount +
                  " FINDINGS=" + report.findings.Count +
                  " OBJECTS=" + report.scannedObjects +
                  " ROUTES=" + report.scannedRoutes +
                  " COMPLETE=" + report.complete);
    }

    private static bool PosesEqual(
        RestaurantPlacementRegistry registry, Dictionary<int, Vector3> poses)
    {
        int count = 0;
        foreach (var footprint in registry.RegisteredFootprints)
        {
            if (footprint == null) continue;
            count++;
            if (!poses.TryGetValue(footprint.GetInstanceID(), out var point) ||
                footprint.transform.position != point)
                return false;
        }
        return count == poses.Count;
    }

    private static void Bootstrap(Type type)
    {
        MethodInfo method = type.GetMethod("Install",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("No bootstrap method: " + type.Name);
        method.Invoke(null, null);
    }

    private static void Check(bool condition, string description)
    {
        if (condition) passed++; else failed++;
        lines.Add((condition ? "OK - " : "FAIL - ") + description);
    }
}
