using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B11: explicit, read-only restaurant diagnosis. No Update, no coroutines,
/// no change to the scene, Finance, placement, topology or edit document.
/// Executes the existing authority's queries only when the player asks.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Diagnosis B11")]
public sealed class BistroBuilderEditorV2DiagnosisService : MonoBehaviour
{
    [SerializeField] private RestaurantEditModeService editMode;
    [SerializeField] private BistroBuilderSpatialAssessmentService spatialAssessment;
    [SerializeField] private BistroBuilderSpatialInteractionService spatial;
    [SerializeField] private BistroBuilderNavigationService navigation;
    [SerializeField] private BistroBuilderEditNavigationValidationProvider architectureNavigation;
    [SerializeField] private BistroBuilderEditDocumentRuntimeService document;
    [SerializeField] private RestaurantPlacementRegistry placementRegistry;
    [SerializeField] private RestaurantPlacementValidationService placementValidation;
    [SerializeField] private RestaurantSeatRegistry seatRegistry;
    [SerializeField] private RestaurantSeatingTopologyService seating;

    private BistroBuilderEditorV2DiagnosisReport lastReport;
    private Coroutine navigationRoutine;
    private readonly List<BistroBuilderEditDiagnostic> architectureDiagnostics =
        new List<BistroBuilderEditDiagnostic>(32);

    public int ScanCount { get; private set; }
    public BistroBuilderEditorV2DiagnosisReport LastReport => lastReport;
    public event Action<BistroBuilderEditorV2DiagnosisReport> ReportChanged;

    public void Configure(
        RestaurantEditModeService mode,
        BistroBuilderSpatialAssessmentService assessment,
        BistroBuilderSpatialInteractionService spatialAuthority,
        BistroBuilderNavigationService navigationAuthority,
        RestaurantPlacementRegistry registry,
        RestaurantPlacementValidationService validator,
        RestaurantSeatRegistry seats,
        RestaurantSeatingTopologyService topology,
        BistroBuilderEditNavigationValidationProvider architectureProvider,
        BistroBuilderEditDocumentRuntimeService documentAuthority)
    {
        editMode = mode;
        spatialAssessment = assessment;
        spatial = spatialAuthority;
        navigation = navigationAuthority;
        placementRegistry = registry;
        placementValidation = validator;
        seatRegistry = seats;
        seating = topology;
        architectureNavigation = architectureProvider;
        document = documentAuthority;
    }

    public bool TryScan(
        BistroBuilderEditorV2DiagnosisLayer layers,
        out BistroBuilderEditorV2DiagnosisReport report,
        out string error)
    {
        report = null;
        if ((layers & BistroBuilderEditorV2DiagnosisLayer.All) == 0)
        {
            error = "Elige al menos una capa de diagnóstico.";
            return false;
        }
        if (editMode == null || !editMode.IsEditModeActive)
        {
            error = "El diagnóstico sólo está disponible en modo edición.";
            return false;
        }
        CancelPendingRouteScan();
        ScanCount++;
        report = new BistroBuilderEditorV2DiagnosisReport
        {
            scannedLayers = layers & BistroBuilderEditorV2DiagnosisLayer.All,
            spatialRevision = spatial != null ? spatial.Revision : 0,
            navigationRevision = navigation != null ? navigation.Revision : 0
        };
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CollectSpatial(report);
            report.spatialMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            CollectNavigation(report);
            report.navigationMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            CollectPlacement(report);
            report.placementMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            CollectCapacity(report);
            report.capacityMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            CollectArchitecture(report);
            report.architectureMilliseconds = watch.ElapsedMilliseconds;
            BistroBuilderEditorV2DiagnosisComposer.Finish(report);
            report.evaluations = ScanCount;
            lastReport = report;
            if (report.navigationPending)
            {
                bool authoritiesReady = report.complete;
                report.complete = false;
                if (Application.isPlaying)
                    navigationRoutine = StartCoroutine(
                        CollectNavigationIncrementally(report, authoritiesReady));
                else
                    report.navigationPending = false;
            }
            ReportChanged?.Invoke(report);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            report.complete = false;
            error = "Una autoridad interrumpió el diagnóstico: " +
                    exception.GetType().Name + ": " + exception.Message;
            Debug.LogWarning("[B11] " + error, this);
            BistroBuilderEditorV2DiagnosisComposer.Finish(report);
            lastReport = report;
            return false;
        }
    }

    private void CollectSpatial(BistroBuilderEditorV2DiagnosisReport report)
    {
        const BistroBuilderEditorV2DiagnosisLayer allSpatial =
            BistroBuilderEditorV2DiagnosisLayer.Circulation |
            BistroBuilderEditorV2DiagnosisLayer.Accessibility |
            BistroBuilderEditorV2DiagnosisLayer.Interaction |
            BistroBuilderEditorV2DiagnosisLayer.Layout;
        if ((report.scannedLayers & allSpatial) == 0) return;
        if (spatialAssessment == null || spatial == null ||
            !spatialAssessment.ValidateConfiguration(out string _))
        {
            Unavailable(report, "BBSIS", allSpatial);
            return;
        }
        BistroBuilderSpatialQualityResult quality =
            spatialAssessment.EvaluateCurrentLayout();
        BistroBuilderSpatialBottleneckLedger ledger =
            spatialAssessment.LastLedger;
        if (quality == null || ledger == null)
        {
            Unavailable(report, "BBSIS", allSpatial);
            return;
        }

        foreach (BistroBuilderSpatialBottleneckRecord bottleneck in ledger.records)
        {
            if (bottleneck == null) continue;
            bool found = spatial.TryGetSubject(
                bottleneck.subjectId, out BistroBuilderSpatialSubject subject);
            BistroBuilderEditorV2DiagnosisComposer.AddSpatialBottleneck(
                report, bottleneck, found && subject != null,
                found && subject != null ?
                    subject.transform.position : Vector3.zero);
        }
    }

    private void CollectNavigation(BistroBuilderEditorV2DiagnosisReport report)
    {
        if ((report.scannedLayers &
            BistroBuilderEditorV2DiagnosisLayer.Circulation) == 0) return;
        if (navigation == null ||
            !navigation.ValidateConfiguration(out string _))
        {
            Unavailable(report, "Navigation",
                BistroBuilderEditorV2DiagnosisLayer.Circulation);
            return;
        }
        // The canonical A* fallback can take over a second per destination.
        // Never freeze the UI by evaluating the entire restaurant in one call.
        report.navigationPending = true;
    }

    /// <summary>Cancels unfinished navigation inspection without hiding
    /// partial findings or declaring unchecked routes as operational.</summary>
    public void CancelPendingRouteScan()
    {
        if (navigationRoutine != null)
        {
            StopCoroutine(navigationRoutine);
            navigationRoutine = null;
        }
        if (lastReport != null && lastReport.navigationPending)
        {
            lastReport.navigationPending = false;
            lastReport.complete = false;
            ReportChanged?.Invoke(lastReport);
        }
    }

    private void OnDisable()
    {
        CancelPendingRouteScan();
    }

    private IEnumerator CollectNavigationIncrementally(
        BistroBuilderEditorV2DiagnosisReport report,
        bool authoritiesReady)
    {
        // StartCoroutine normally advances immediately to its first yield.
        // Force the first expensive route to a later frame.
        yield return null;
        var authorityReport = new BistroBuilderCirculationHealthReport
        {
            revision = navigation != null ? navigation.Revision : 0
        };
        IEnumerator steps = navigation.ScanCirculationIncrementally(
            authorityReport);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool successful = true;
        while (true)
        {
            if (this == null || !isActiveAndEnabled || editMode == null ||
                !editMode.IsEditModeActive ||
                !ReferenceEquals(lastReport, report))
            {
                report.navigationPending = false;
                report.complete = false;
                navigationRoutine = null;
                yield break;
            }
            bool hasMore;
            try
            {
                hasMore = steps.MoveNext();
            }
            catch (Exception exception)
            {
                successful = false;
                Debug.LogWarning("[B11] Navigation route inspection: " +
                    exception.Message, this);
                break;
            }
            report.scannedRoutes = authorityReport.checkedConnections;
            foreach (var issue in authorityReport.issues)
                BistroBuilderEditorV2DiagnosisComposer.AddNavigationIssue(
                    report, issue);
            BistroBuilderEditorV2DiagnosisComposer.Finish(report);
            ReportChanged?.Invoke(report);
            if (!hasMore) break;
            yield return steps.Current;
        }
        watch.Stop();
        report.navigationMilliseconds = watch.ElapsedMilliseconds;
        report.navigationPending = false;
        report.complete = authoritiesReady && successful &&
            navigation != null &&
            navigation.Revision == authorityReport.revision;
        BistroBuilderEditorV2DiagnosisComposer.Finish(report);
        ReportChanged?.Invoke(report);
        navigationRoutine = null;
    }

    private void CollectPlacement(BistroBuilderEditorV2DiagnosisReport report)
    {
        if ((report.scannedLayers &
            BistroBuilderEditorV2DiagnosisLayer.Layout) == 0) return;
        if (placementRegistry == null || placementValidation == null)
        {
            Unavailable(report, "Placement",
                BistroBuilderEditorV2DiagnosisLayer.Layout);
            return;
        }

        foreach (RestaurantPlacementFootprint footprint in
                 placementRegistry.RegisteredFootprints)
        {
            if (footprint == null || !footprint.gameObject.activeInHierarchy)
                continue;
            report.scannedObjects++;
            if (!placementRegistry.TryGetMember(footprint, out var member) ||
                member == null)
            {
                AddPlacementIssue(report, "missing:" + footprint.GetInstanceID(),
                    footprint.gameObject, "Objeto sin registro de área.",
                    "Vuelve a integrar el elemento mediante el editor.", true);
                continue;
            }
            var validation = placementValidation.ValidateCurrentPlacement(member);
            if (validation.IsValid) continue;
            bool blocking = validation.Status ==
                RestaurantPlacementValidationStatus.PhysicalOverlap ||
                validation.Status ==
                RestaurantPlacementValidationStatus.OutsideRegisteredAreas ||
                validation.Status ==
                RestaurantPlacementValidationStatus.SystemUnavailable;
            AddPlacementIssue(report, "invalid:" + member.GetInstanceID(),
                member.gameObject,
                string.IsNullOrWhiteSpace(validation.UserMessage)
                    ? "Hay una regla de colocación incumplida."
                    : validation.UserMessage,
                "Selecciona este objeto y ajusta su posición, orientación o separación.",
                blocking);
        }
    }

    private static void AddPlacementIssue(
        BistroBuilderEditorV2DiagnosisReport report,
        string key, GameObject target, string message,
        string recommendation, bool blocking)
    {
        BistroBuilderEditorV2DiagnosisComposer.Add(report,
            new BistroBuilderEditorV2DiagnosticFinding
            {
                id = "placement:" + key,
                layer = BistroBuilderEditorV2DiagnosisLayer.Layout,
                severity = blocking
                    ? BistroBuilderEditorV2DiagnosisSeverity.Blocking
                    : BistroBuilderEditorV2DiagnosisSeverity.Warning,
                targetId = target != null ? target.name : string.Empty,
                title = "Incidencia de distribución",
                explanation = message,
                recommendation = recommendation,
                hasWorldPosition = target != null,
                worldPosition = target != null ? target.transform.position :
                    Vector3.zero
            });
    }

    private void CollectCapacity(BistroBuilderEditorV2DiagnosisReport report)
    {
        if ((report.scannedLayers &
            BistroBuilderEditorV2DiagnosisLayer.Capacity) == 0) return;
        if (seatRegistry == null || seating == null)
        {
            Unavailable(report, "Seating",
                BistroBuilderEditorV2DiagnosisLayer.Capacity);
            return;
        }
        BistroBuilderEditorV2DiagnosisComposer.AddSeatingIssues(
            report, seatRegistry.RegisteredSeatCount,
            seating.UnassociatedSeatCount);
    }

    private void CollectArchitecture(BistroBuilderEditorV2DiagnosisReport report)
    {
        if ((report.scannedLayers &
            BistroBuilderEditorV2DiagnosisLayer.Accessibility) == 0) return;
        if (architectureNavigation == null || document == null)
        {
            Unavailable(report, "ArchitectureNavigation",
                BistroBuilderEditorV2DiagnosisLayer.Accessibility);
            return;
        }
        var snapshot = document.GetCommittedSnapshot();
        architectureDiagnostics.Clear();
        architectureNavigation.Validate(snapshot, snapshot.revision,
            architectureDiagnostics);
        foreach (var issue in architectureDiagnostics)
        {
            if (issue == null) continue;
            BistroBuilderEditorV2DiagnosisComposer.Add(report,
                new BistroBuilderEditorV2DiagnosticFinding
                {
                    id = "architecture:" + issue.code + ":" + issue.targetId,
                    layer = BistroBuilderEditorV2DiagnosisLayer.Accessibility,
                    severity = issue.severity ==
                        BistroBuilderEditDiagnosticSeverity.Blocking
                        ? BistroBuilderEditorV2DiagnosisSeverity.Blocking
                        : BistroBuilderEditorV2DiagnosisSeverity.Warning,
                    targetId = issue.targetId.ToString(),
                    title = "Acceso arquitectónico insuficiente",
                    explanation = issue.message,
                    recommendation =
                        "Amplía el hueco de paso hasta las dimensiones exigidas por Navigation.",
                    hasWorldPosition = true,
                    worldPosition = new Vector3(issue.location.x, 0f,
                        issue.location.y)
                });
        }
    }

    private static void Unavailable(
        BistroBuilderEditorV2DiagnosisReport report,
        string authority,
        BistroBuilderEditorV2DiagnosisLayer layer)
    {
        report.complete = false;
        if ((report.scannedLayers & layer) == 0) return;
        BistroBuilderEditorV2DiagnosisComposer.Add(report,
            new BistroBuilderEditorV2DiagnosticFinding
            {
                id = "missing:" + authority,
                layer = layer & report.scannedLayers,
                severity = BistroBuilderEditorV2DiagnosisSeverity.Information,
                title = "No evaluado: " + authority,
                explanation = "La autoridad no está disponible o configurada.",
                recommendation =
                    "Comprueba el estado de los sistemas antes de abrir el restaurante."
            });
    }
}
