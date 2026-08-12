using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador visual de una entrega física 2.3H.
/// No modifica Inventario ni marca Delivered. Solo representa el reparto y
/// emite un handoff único para que 2.2B/2.3L realice la recepción canónica.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplierDeliveryPresentationController : MonoBehaviour
{
    private readonly List<Vector3> path = new List<Vector3>();
    private readonly List<GameObject> boxes = new List<GameObject>();

    private BistroBuilderSupplierDispatchTicket ticket;
    private BistroBuilderPurchaseOrderRecord order;
    private BistroBuilderSupplierDeliveryPresentationSettings settings;
    private BistroBuilderSupplierDeliverySceneAnchors anchors;
    private BistroBuilderSupplierDeliveryBrandingData branding;
    private BistroBuilderSupplierDeliveryPresentationRecord record;

    private GameObject vehicle;
    private GameObject driver;
    private GameObject trolley;
    private BistroBuilderSupplierDeliveryVehicleView vehicleView;
    private NavMeshAgent driverAgent;

    private int pathIndex;
    private float stateElapsed;
    private float cleanupElapsed;
    private bool handoffEmitted;
    private bool completionEmitted;
    private bool initialized;
    private bool manualTick;
    private bool driverNavMeshActive;
    private Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierDeliveryPresentationRecord> stateChanged;
    private Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierReceivingHandoff> handoffReady;
    private Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierDeliveryPresentationRecord> completed;

    public BistroBuilderSupplierDeliveryPresentationRecord Record => record != null ? record.DeepClone() : null;
    public bool IsInitialized => initialized;
    public bool IsCompleted => record != null && record.state == BistroBuilderSupplierDeliveryPresentationState.Completed;
    public GameObject VehicleObject => vehicle;
    public GameObject DriverObject => driver;
    public GameObject TrolleyObject => trolley;

    public bool Initialize(
        BistroBuilderSupplierDispatchTicket sourceTicket,
        BistroBuilderPurchaseOrderRecord sourceOrder,
        BistroBuilderSupplierDeliveryPresentationSettings sourceSettings,
        BistroBuilderSupplierDeliverySceneAnchors sourceAnchors,
        BistroBuilderSupplierDeliveryBrandingData sourceBranding,
        BistroBuilderSupplierDeliveryPresentationRecord sourceRecord,
        Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierDeliveryPresentationRecord> onStateChanged,
        Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierReceivingHandoff> onHandoffReady,
        Action<BistroBuilderSupplierDeliveryPresentationController, BistroBuilderSupplierDeliveryPresentationRecord> onCompleted,
        out string error)
    {
        error = null;
        if (sourceTicket == null) { error = "DispatchTicket nulo."; return false; }
        if (sourceOrder == null || sourceOrder.confirmedLines == null || sourceOrder.confirmedLines.Count == 0)
        { error = "PurchaseOrder sin líneas confirmadas para la entrega visual."; return false; }
        if (sourceSettings == null) { error = "Falta supplier.delivery.presentation.settings."; return false; }
        if (sourceAnchors == null || !sourceAnchors.IsComplete) { error = "Faltan anclajes completos de escena 2.3H."; return false; }
        if (sourceBranding == null || !sourceBranding.HasReadableIdentity) { error = "Branding de proveedor inválido."; return false; }
        if (sourceRecord == null) { error = "PresentationRecord nulo."; return false; }

        ticket = sourceTicket.DeepClone();
        order = sourceOrder.DeepClone();
        settings = sourceSettings;
        anchors = sourceAnchors;
        branding = sourceBranding;
        record = sourceRecord.DeepClone();
        stateChanged = onStateChanged;
        handoffReady = onHandoffReady;
        completed = onCompleted;

        vehicle = BistroBuilderSupplierDeliveryVisualFactory.CreateVehicle(ticket.vehicle, settings, transform);
        vehicle.transform.position = anchors.vehicleEntry.position;
        vehicle.transform.rotation = anchors.vehicleEntry.rotation;
        string brandingError;
        if (!BistroBuilderSupplierDeliveryVisualFactory.ApplyBranding(vehicle, branding, settings, out brandingError))
        {
            error = brandingError;
            DestroyImmediateSafe(vehicle);
            return false;
        }

        vehicleView = vehicle.GetComponent<BistroBuilderSupplierDeliveryVehicleView>();
        driver = BistroBuilderSupplierDeliveryVisualFactory.CreateDriver(settings, transform);
        driver.transform.position = anchors.driverExitPoint.position;
        driver.transform.rotation = anchors.driverExitPoint.rotation;
        BistroBuilderSupplierDeliveryVisualFactory.ApplyDriverBrandColor(driver, branding);
        driver.SetActive(false);

        trolley = BistroBuilderSupplierDeliveryVisualFactory.CreateTrolley(settings, transform);
        trolley.transform.position = anchors.driverExitPoint.position + anchors.driverExitPoint.TransformVector(settings.DriverTrolleyFollowOffset);
        trolley.transform.rotation = anchors.driverExitPoint.rotation;
        trolley.SetActive(false);

        driverAgent = driver.GetComponent<NavMeshAgent>();
        driverNavMeshActive = false;
        BuildVehicleEntryPath();
        record.state = BistroBuilderSupplierDeliveryPresentationState.VehicleEntering;
        record.currentTrip = Mathf.Clamp(record.currentTrip, 1, Mathf.Max(1, record.totalTrips));
        stateElapsed = 0f;
        initialized = true;
        NotifyState();
        return true;
    }

    private void Update()
    {
        if (!initialized || manualTick) return;
        Tick(Time.unscaledDeltaTime, true);
    }

    /// <summary>
    /// Avance determinista usado por las pruebas runtime 2.3H. No necesita
    /// esperar frames y fuerza el fallback de waypoints para no depender de un bake NavMesh.
    /// </summary>
    public void ManualTick(float deltaTime)
    {
        if (!initialized) return;
        manualTick = true;
        Tick(Mathf.Max(0.001f, deltaTime), false);
    }

    public void DisposeVisuals()
    {
        if (driverAgent != null && driverAgent.isOnNavMesh) driverAgent.ResetPath();
        DestroyImmediateSafe(vehicle);
        DestroyImmediateSafe(driver);
        DestroyImmediateSafe(trolley);
        boxes.Clear();
        vehicle = null;
        driver = null;
        trolley = null;
        initialized = false;
    }

    private void Tick(float dt, bool allowNavMesh)
    {
        if (record == null || record.state == BistroBuilderSupplierDeliveryPresentationState.Cancelled) return;
        stateElapsed += dt;

        switch (record.state)
        {
            case BistroBuilderSupplierDeliveryPresentationState.VehicleEntering:
                if (MoveAlongPath(vehicle, settings.VehicleSpeedMetersPerSecond, settings.VehicleTurnDegreesPerSecond, dt))
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.Parked);
                break;

            case BistroBuilderSupplierDeliveryPresentationState.Parked:
                if (stateElapsed >= settings.ParkPauseSeconds)
                {
                    driver.SetActive(true);
                    driver.transform.position = anchors.driverExitPoint.position;
                    driver.transform.rotation = anchors.driverExitPoint.rotation;
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.DriverExiting);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.DriverExiting:
                if (stateElapsed >= settings.DriverExitSeconds)
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.OpeningRearDoors);
                break;

            case BistroBuilderSupplierDeliveryPresentationState.OpeningRearDoors:
                ApplyDoorProgress(settings.RearDoorSeconds <= 0f ? 1f : stateElapsed / settings.RearDoorSeconds);
                if (stateElapsed >= settings.RearDoorSeconds)
                {
                    ApplyDoorProgress(1f);
                    PrepareTripLoad();
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley:
                if (stateElapsed >= settings.TrolleyPrepareSeconds)
                {
                    trolley.SetActive(true);
                    BuildDriverWarehousePath();
                    BeginDriverMovement(allowNavMesh);
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse:
                if (MoveDriverAndTrolley(dt, allowNavMesh))
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.Unloading);
                break;

            case BistroBuilderSupplierDeliveryPresentationState.Unloading:
                if (stateElapsed >= settings.UnloadSecondsPerTrip)
                {
                    ClearBoxes();
                    if (record.currentTrip >= record.totalTrips && !handoffEmitted)
                    {
                        handoffEmitted = true;
                        record.receivingHandoffEmitted = true;
                        record.handoffId = BuildHandoffId(record.logisticsPlanId);
                        record.stateRevision++;
                        handoffReady?.Invoke(this, BuildHandoff());
                    }
                    BuildDriverReturnPath();
                    BeginDriverMovement(allowNavMesh);
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.ReturningToVehicle);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.ReturningToVehicle:
                if (MoveDriverAndTrolley(dt, allowNavMesh))
                {
                    if (record.currentTrip < record.totalTrips)
                    {
                        record.currentTrip++;
                        record.stateRevision++;
                        PrepareTripLoad();
                        ChangeState(BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley);
                    }
                    else ChangeState(BistroBuilderSupplierDeliveryPresentationState.StowingTrolley);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.StowingTrolley:
                if (stateElapsed >= settings.TrolleyStowSeconds)
                {
                    trolley.SetActive(false);
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.ClosingRearDoors);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.ClosingRearDoors:
                ApplyDoorProgress(settings.RearDoorSeconds <= 0f ? 0f : 1f - (stateElapsed / settings.RearDoorSeconds));
                if (stateElapsed >= settings.RearDoorSeconds)
                {
                    ApplyDoorProgress(0f);
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.DriverEnteringVehicle);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.DriverEnteringVehicle:
                if (stateElapsed >= settings.DriverEnterSeconds)
                {
                    driver.SetActive(false);
                    BuildVehicleExitPath();
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.VehicleExiting);
                }
                break;

            case BistroBuilderSupplierDeliveryPresentationState.VehicleExiting:
                if (MoveAlongPath(vehicle, settings.VehicleSpeedMetersPerSecond, settings.VehicleTurnDegreesPerSecond, dt))
                    ChangeState(BistroBuilderSupplierDeliveryPresentationState.Completed);
                break;

            case BistroBuilderSupplierDeliveryPresentationState.Completed:
                cleanupElapsed += dt;
                if (!completionEmitted && cleanupElapsed >= settings.CleanupDelaySeconds)
                {
                    completionEmitted = true;
                    completed?.Invoke(this, record.DeepClone());
                }
                break;
        }
    }

    private void ChangeState(BistroBuilderSupplierDeliveryPresentationState next)
    {
        if (record.state == next) return;
        record.state = next;
        record.stateRevision++;
        stateElapsed = 0f;
        if (next == BistroBuilderSupplierDeliveryPresentationState.Completed)
        {
            record.completedGameDay = Mathf.Max(record.startedGameDay, record.completedGameDay);
            cleanupElapsed = 0f;
        }
        NotifyState();
    }

    private void NotifyState()
    {
        stateChanged?.Invoke(this, record.DeepClone());
    }

    private void BuildVehicleEntryPath()
    {
        anchors.BuildVehicleEntryPath(path);
        pathIndex = path.Count > 1 ? 1 : path.Count;
    }

    private void BuildVehicleExitPath()
    {
        anchors.BuildVehicleExitPath(path);
        pathIndex = path.Count > 1 ? 1 : path.Count;
    }

    private void BuildDriverWarehousePath()
    {
        anchors.BuildDriverWarehousePath(path);
        pathIndex = path.Count > 1 ? 1 : path.Count;
    }

    private void BuildDriverReturnPath()
    {
        anchors.BuildDriverReturnPath(path);
        pathIndex = path.Count > 1 ? 1 : path.Count;
    }

    private bool MoveAlongPath(GameObject actor, float speed, float turnSpeed, float dt)
    {
        if (actor == null || path.Count == 0 || pathIndex >= path.Count) return true;
        Vector3 target = path[pathIndex];
        Vector3 current = actor.transform.position;
        Vector3 delta = target - current;
        delta.y = target.y - current.y;
        if (delta.sqrMagnitude <= 0.0025f)
        {
            actor.transform.position = target;
            pathIndex++;
            return pathIndex >= path.Count;
        }
        Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
        if (horizontal.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            actor.transform.rotation = Quaternion.RotateTowards(actor.transform.rotation, targetRotation, turnSpeed * dt);
        }
        actor.transform.position = Vector3.MoveTowards(current, target, Mathf.Max(0.01f, speed) * dt);
        return false;
    }

    private void BeginDriverMovement(bool allowNavMesh)
    {
        driverNavMeshActive = false;
        if (!allowNavMesh || !settings.PreferNavMeshWhenAvailable || driverAgent == null || path.Count == 0) return;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(driver.transform.position, out hit, 1.5f, NavMesh.AllAreas)) return;
        if (!driverAgent.isOnNavMesh)
        {
            if (!driverAgent.Warp(hit.position)) return;
        }
        driverAgent.speed = settings.DriverSpeedMetersPerSecond;
        driverAgent.angularSpeed = settings.DriverTurnDegreesPerSecond;
        driverAgent.stoppingDistance = 0.08f;
        driverAgent.SetDestination(path[path.Count - 1]);
        driverNavMeshActive = true;
    }

    private bool MoveDriverAndTrolley(float dt, bool allowNavMesh)
    {
        if (driver == null) return true;
        bool arrived;
        if (allowNavMesh && driverNavMeshActive && driverAgent != null && driverAgent.isOnNavMesh)
        {
            arrived = !driverAgent.pathPending && driverAgent.remainingDistance <= Mathf.Max(0.08f, driverAgent.stoppingDistance + 0.02f);
        }
        else
        {
            driverNavMeshActive = false;
            arrived = MoveAlongPath(driver, settings.DriverSpeedMetersPerSecond, settings.DriverTurnDegreesPerSecond, dt);
        }
        if (trolley != null && trolley.activeSelf)
        {
            Vector3 target = driver.transform.position + driver.transform.TransformVector(settings.DriverTrolleyFollowOffset);
            trolley.transform.position = Vector3.Lerp(trolley.transform.position, target, Mathf.Clamp01(dt * 12f));
            trolley.transform.rotation = Quaternion.RotateTowards(trolley.transform.rotation, driver.transform.rotation, settings.DriverTurnDegreesPerSecond * dt);
        }
        return arrived;
    }

    private void PrepareTripLoad()
    {
        ClearBoxes();
        int totalVisual = Mathf.Max(1, ticket.visualLoadUnits);
        int trips = Mathf.Clamp(record.totalTrips, 1, 3);
        int basePerTrip = totalVisual / trips;
        int remainder = totalVisual % trips;
        int thisTrip = basePerTrip + (record.currentTrip <= remainder ? 1 : 0);
        thisTrip = Mathf.Clamp(thisTrip, 1, settings.MaximumVisibleBoxesPerTrip);
        trolley.SetActive(true);
        for (int i = 0; i < thisTrip; i++)
        {
            GameObject box = BistroBuilderSupplierDeliveryVisualFactory.CreateBox(settings, trolley.transform, i);
            int row = i / 3;
            int column = i % 3;
            box.transform.localPosition = settings.TrolleyLoadLocalOffset +
                                          new Vector3((column - 1) * 0.36f, row * 0.31f, 0f);
            boxes.Add(box);
        }
    }

    private void ClearBoxes()
    {
        for (int i = 0; i < boxes.Count; i++) DestroyImmediateSafe(boxes[i]);
        boxes.Clear();
    }

    private void ApplyDoorProgress(float t)
    {
        if (vehicleView != null) vehicleView.SetRearDoors01(Mathf.Clamp01(t));
    }

    private BistroBuilderSupplierReceivingHandoff BuildHandoff()
    {
        BistroBuilderSupplierReceivingHandoff handoff = new BistroBuilderSupplierReceivingHandoff
        {
            handoffId = BuildHandoffId(record.logisticsPlanId),
            logisticsPlanId = record.logisticsPlanId,
            purchaseOrderId = order.purchaseOrderId,
            orderDisplayCode = order.displayCode,
            supplierId = order.supplierId,
            supplierDisplayName = order.supplierTerms != null ? order.supplierTerms.supplierDisplayName : branding.displayName,
            gameDay = Mathf.Max(1, order.actualDeliveryStartGameDay),
            visualTripsCompleted = record.totalTrips
        };
        for (int i = 0; i < order.confirmedLines.Count; i++)
        {
            BistroBuilderPurchaseOrderConfirmedLineSnapshot line = order.confirmedLines[i];
            if (line == null) continue;
            handoff.lines.Add(new BistroBuilderSupplierDeliveryManifestLine
            {
                purchaseOrderLineId = line.purchaseOrderLineId,
                supplierOfferId = line.supplierOfferId,
                ingredientId = line.ingredientId,
                ingredientDisplayName = line.ingredientDisplayName,
                canonicalUnit = line.canonicalUnit,
                packageFormatId = line.packageFormatId,
                packageDisplayName = line.packageDisplayName,
                packageCount = line.packageCount,
                totalNetQuantityMicrounits = line.totalNetQuantityMicrounits
            });
            handoff.totalPackageCount += Mathf.Max(0, line.packageCount);
            handoff.totalNetQuantityMicrounits += Math.Max(0L, line.totalNetQuantityMicrounits);
        }
        return handoff;
    }

    public static string BuildHandoffId(string logisticsPlanId)
    {
        return "receiving_handoff_" + (string.IsNullOrWhiteSpace(logisticsPlanId) ? "unknown" : logisticsPlanId.Trim());
    }

    private static void DestroyImmediateSafe(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }
}
