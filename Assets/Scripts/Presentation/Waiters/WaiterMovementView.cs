using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa visualmente el movimiento de un camarero.
///
/// Traduce los estados operativos del camarero en desplazamientos
/// hacia mesas o cocina y notifica cuándo se alcanza el destino.
///
/// También resuelve correctamente las asignaciones cuyo destino
/// ya está alcanzado, evitando que una tarea quede bloqueada
/// esperando un movimiento de distancia cero.
/// </summary>
public sealed class WaiterMovementView : MonoBehaviour
{
    [Header("Referencias")]

    [SerializeField]
    private Waiter waiter;

    [SerializeField]
    private KitchenSystem kitchenSystem;

    [SerializeField]
    private BistroBuilderWaiterRoutingService routingService;

    [SerializeField]
    private BistroBuilderNavigationService navigationService;

    [SerializeField, Min(0.1f)]
    private float circulationRadius = 0.28f;

[Header("Movimiento")]

    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2.5f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    /// <summary>
    /// Se emite una sola vez cuando el camarero alcanza
    /// el destino correspondiente a su tarea actual.
    /// </summary>
    public event Action<WaiterMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    /// <summary>
    /// Destino de la solicitud de movimiento actualmente activa.
    /// </summary>
    private Transform currentDestination;

    private bool isMoving;
    private readonly List<Vector3> routePoints = new List<Vector3>(16);
    private int currentRouteIndex;
    private Vector3 reservedArrivalPosition;
    private bool hasReservedArrival;
    private float blockedSince = -1f;
    private int plannedNavigationRevision;
    public BistroBuilderWaiterRouteKind CurrentRouteKind =>
        routingService != null ? routingService.LastRouteKind : BistroBuilderWaiterRouteKind.DirectFallback;

    /// <summary>
    /// Corrutina utilizada cuando el camarero ya está dentro
    /// de la distancia de llegada al recibir una nueva tarea.
    ///
    /// La notificación se retrasa un frame para que el sistema
    /// de tareas termine primero de registrar la asignación.
    /// </summary>
    private Coroutine deferredArrivalRoutine;

    /// <summary>
    /// Identificador interno de la solicitud de movimiento.
    ///
    /// Permite descartar una llegada diferida si, antes de ejecutarse,
    /// el camarero ha recibido otro destino.
    /// </summary>
    private uint movementRequestVersion;

    private void Awake()
    {
        if (waiter == null)
        {
            waiter = GetComponent<Waiter>();
        }
        if (routingService == null)
        {
            routingService = FindFirstObjectByType<BistroBuilderWaiterRoutingService>();
        }
        if (navigationService == null)
        {
            navigationService = FindFirstObjectByType<BistroBuilderNavigationService>();
        }
    }

    private void OnEnable()
    {
        if (waiter == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una referencia a Waiter.",
                this
            );

            enabled = false;
            return;
        }

        waiter.StateChanged += HandleWaiterStateChanged;
    }

    private void OnDisable()
    {
        if (waiter != null)
        {
            waiter.StateChanged -= HandleWaiterStateChanged;
        }

        CancelDeferredArrival();

        currentDestination = null;
        isMoving = false;
        HasReachedDestination = false;

        movementRequestVersion++;
        navigationService?.CancelNavigation(GetNavigationOwnerId());
        navigationService?.RemoveAgentPresence(GetNavigationOwnerId());
        hasReservedArrival = false;
        blockedSince = -1f;
    }

    private void Update()
    {
        string ownerId = GetNavigationOwnerId();
        if (navigationService != null)
        {
            navigationService.UpdateAgentPresence(
                ownerId,
                BistroBuilderNavigationAgentMask.Waiter,
                transform.position,
                circulationRadius,
                0);
            navigationService.ReportNavigationPosition(ownerId, transform.position);
        }

        if (!isMoving || currentDestination == null) return;

        if (!EnsureScheduledRouteReady(ownerId)) return;

        if (navigationService != null &&
            plannedNavigationRevision != navigationService.Revision)
            ReplanCurrentRoute(BistroBuilderNavigationReplanLevel.RouteSuffixRepair);

        Vector3 movementTarget = currentRouteIndex < routePoints.Count
            ? routePoints[currentRouteIndex]
            : GetArrivalPosition();

        Vector3 proposed = transform.position;
        BistroBuilderNavigationLocalMoveDecision decision = default;
        bool canMove = navigationService == null ||
            navigationService.TryResolveMovementStep(
                ownerId,
                BistroBuilderNavigationAgentMask.Waiter,
                transform.position,
                movementTarget,
                movementSpeed,
                circulationRadius,
                0,
                Time.deltaTime,
                out proposed,
                out decision);

        if (navigationService == null)
        {
            proposed = Vector3.MoveTowards(
                transform.position,
                movementTarget,
                movementSpeed * Time.deltaTime);
            decision = default;
            canMove = true;
        }

        if (!canMove)
        {
            if (blockedSince < 0f) blockedSince = Time.unscaledTime;
            BistroBuilderNavigationWaitingReason reason =
                decision.waitingReason == BistroBuilderNavigationWaitingReason.None
                    ? BistroBuilderNavigationWaitingReason.Yield
                    : decision.waitingReason;
            navigationService?.ReportNavigationPosition(
                ownerId,
                transform.position,
                reason,
                decision.yieldingTo);

            if (navigationService != null &&
                navigationService.TryGetNavigationTrace(
                    ownerId,
                    out BistroBuilderNavigationDecisionTrace trace))
            {
                if (trace.recoveryStage ==
                    BistroBuilderNavigationRecoveryStage.FullReplan)
                    ReplanCurrentRoute(BistroBuilderNavigationReplanLevel.FullReplan);
                else if (trace.recoveryStage ==
                         BistroBuilderNavigationRecoveryStage.CorridorRepair)
                    ReplanCurrentRoute(BistroBuilderNavigationReplanLevel.CorridorRepair);
            }

            navigationService?.RefreshDestination(ownerId);
            return;
        }

        blockedSince = -1f;
        transform.position = proposed;
        navigationService?.ReportNavigationPosition(ownerId, transform.position);
        navigationService?.RefreshDestination(ownerId);

        if ((transform.position - movementTarget).sqrMagnitude <=
            arrivalDistance * arrivalDistance &&
            currentRouteIndex < routePoints.Count - 1)
            currentRouteIndex++;

        TryCompleteCurrentMovement();
    }
    /// <summary>
    /// Traduce los estados de desplazamiento del camarero
    /// en destinos físicos de la escena.
    ///
    /// Los estados que no implican movimiento se ignoran.
    /// No se cancela aquí un movimiento previo porque los eventos
    /// de estado pueden producir asignaciones anidadas durante
    /// el cambio del camarero a Idle.
    /// </summary>
    private void HandleWaiterStateChanged(
        Waiter changedWaiter,
        WaiterState newState
    )
    {
        Transform destination = newState switch
        {
            WaiterState.WalkingToTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToKitchen =>
                GetKitchenPickupPoint(),

            WaiterState.WalkingToServeTable =>
                GetAssignedServicePoint(changedWaiter),

            WaiterState.WalkingToBill =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToCleanTable =>
                GetTableServicePoint(changedWaiter),

            WaiterState.WalkingToBar =>
                GetAssignedServicePoint(changedWaiter),

            WaiterState.WalkingToBarBill =>
                GetAssignedServicePoint(changedWaiter),

            _ => null
        };

        if (destination == null)
        {
            return;
        }

        BeginMovement(destination);
    }

    /// <summary>
    /// Obtiene el punto de servicio del destino operativo asignado. Puede ser
    /// una mesa o una plaza de barra y nunca depende de una mesa proxy.
    /// </summary>
    private Transform GetAssignedServicePoint(Waiter changedWaiter)
    {
        if (changedWaiter == null)
        {
            return null;
        }

        Transform destination = changedWaiter.AssignedWaiterServicePoint;

        if (destination != null)
        {
            return destination;
        }

        Debug.LogError(
            "El camarero " + changedWaiter.WaiterId +
            " no tiene un punto de servicio operativo asignado.",
            this
        );

        return null;
    }

    /// <summary>
    /// Obtiene el punto de servicio de la mesa
    /// actualmente asignada al camarero.
    /// </summary>
    private Transform GetTableServicePoint(
        Waiter changedWaiter
    )
    {
        RestaurantTable assignedTable =
            changedWaiter.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El camarero {changedWaiter.WaiterId} " +
                "no tiene mesa asignada.",
                this
            );

            return null;
        }

        if (assignedTable.WaiterServicePoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} " +
                "no tiene WaiterServicePoint.",
                assignedTable
            );

            return null;
        }

        return assignedTable.WaiterServicePoint;
    }

    /// <summary>
    /// Obtiene el punto de recogida de comandas de cocina.
    /// </summary>
    private Transform GetKitchenPickupPoint()
    {
        // Una ronda 367G puede proceder de una cocina distinta de la
        // referencia provisional instalada en este componente. La ronda
        // activa es la autoridad del punto de recogida.
        KitchenSystem activeKitchen =
            waiter != null && waiter.AssignedDeliveryRun != null
                ? waiter.AssignedDeliveryRun.SourceKitchen
                : kitchenSystem;

        if (activeKitchen == null)
        {
            Debug.LogError(
                "WaiterMovementView necesita una cocina de origen " +
                "para ir al punto de recogida.",
                this
            );

            return null;
        }

        if (activeKitchen.PickupPoint == null)
        {
            Debug.LogError(
                "KitchenSystem no tiene PickupPoint asignado.",
                activeKitchen
            );

            return null;
        }

        return activeKitchen.PickupPoint;
    }

    /// <summary>
    /// Inicia una nueva solicitud de movimiento.
    ///
    /// Si el camarero ya está en el destino, programa la llegada
    /// para el siguiente frame en lugar de dejarla depender
    /// exclusivamente del desplazamiento visual.
    /// </summary>
    private void BeginMovement(
        Transform destination
    )
    {
        CancelDeferredArrival();
        currentDestination = destination;
        HasReachedDestination = false;
        isMoving = true;
        blockedSince = -1f;
        hasReservedArrival = false;

        Vector3 target = destination.position;
        if (navigationService != null)
        {
            hasReservedArrival = navigationService.TryReserveDestination(
                GetNavigationOwnerId(),
                BistroBuilderNavigationAgentMask.Waiter,
                target,
                circulationRadius,
                0,
                out reservedArrivalPosition);
            if (hasReservedArrival) target = reservedArrivalPosition;
        }
        RebuildRouteTo(target);
        movementRequestVersion++;

        if (!IsWithinArrivalDistance(destination)) return;
        uint requestedVersion = movementRequestVersion;
        deferredArrivalRoutine = StartCoroutine(
            CompleteArrivalNextFrame(requestedVersion, destination));
    }

    /// <summary>
    /// Completa una llegada de distancia cero en el siguiente frame.
    ///
    /// El retraso evita que DestinationReached se emita dentro
    /// de la propia llamada que todavía está asignando la tarea.
    /// </summary>
    private IEnumerator CompleteArrivalNextFrame(
        uint requestedVersion,
        Transform expectedDestination
    )
    {
        yield return null;

        deferredArrivalRoutine = null;

        bool requestIsStillValid =
            isMoving &&
            requestedVersion == movementRequestVersion &&
            currentDestination == expectedDestination &&
            expectedDestination != null;

        if (!requestIsStillValid)
        {
            yield break;
        }

        // El destino podría haberse desplazado durante el frame.
        // En ese caso, el movimiento normal continuará en Update.
        if (!IsWithinArrivalDistance(expectedDestination))
        {
            yield break;
        }

        CompleteMovement();
    }

    /// <summary>
    /// Comprueba en cada frame si el destino activo
    /// ya se encuentra dentro del margen de llegada.
    /// </summary>
    private void TryCompleteCurrentMovement()
    {
        if (currentDestination == null)
        {
            return;
        }

        if (!IsWithinArrivalDistance(currentDestination))
        {
            return;
        }

        CompleteMovement();
    }

    /// <summary>
    /// Comprueba la distancia utilizando magnitud al cuadrado
    /// para evitar calcular una raíz cuadrada en cada frame.
    /// </summary>
    private bool IsWithinArrivalDistance(
        Transform destination
    )
    {
        if (destination == null)
        {
            return false;
        }

        float distanceSquared =
            (transform.position - GetArrivalPosition())
            .sqrMagnitude;

        float arrivalDistanceSquared =
            arrivalDistance * arrivalDistance;

        return distanceSquared <= arrivalDistanceSquared;
    }

    /// <summary>
    /// Finaliza la solicitud actual y notifica a los flujos
    /// operativos que el camarero ha llegado.
    ///
    /// El movimiento se cierra antes de emitir el evento para
    /// impedir notificaciones duplicadas.
    /// </summary>
    private void CompleteMovement()
    {
        if (!isMoving ||
            currentDestination == null)
        {
            return;
        }

        CancelDeferredArrival();

        Transform reachedDestination =
            currentDestination;

        transform.position = GetArrivalPosition();
        navigationService?.CompleteNavigation(GetNavigationOwnerId());
        navigationService?.ReleaseDestination(GetNavigationOwnerId());
        hasReservedArrival = false;

        currentDestination = null;
        isMoving = false;
        HasReachedDestination = true;

        movementRequestVersion++;

        Debug.Log(
            $"Camarero {waiter.WaiterId} ha llegado a su destino.",
            this
        );

        DestinationReached?.Invoke(this);
    }

    /// <summary>
    /// Cancela cualquier desplazamiento transitorio antes de reconstruir
    /// un servicio cargado. No emite DestinationReached ni modifica la
    /// posición restaurada posteriormente por el proveedor de guardado.
    /// </summary>
    public void ResetForRuntimeLoad()
    {
        CancelDeferredArrival();
        currentDestination = null;
        isMoving = false;
        HasReachedDestination = false;
        navigationService?.CancelNavigation(GetNavigationOwnerId());
        navigationService?.RemoveAgentPresence(GetNavigationOwnerId());
        hasReservedArrival = false;
        blockedSince = -1f;
        movementRequestVersion++;
    }

    /// <summary>
    /// Cancela únicamente una llegada diferida pendiente.
    /// No modifica una solicitud de movimiento válida.
    /// </summary>
    private string GetNavigationOwnerId()
    {
        return waiter != null
            ? "waiter:" + waiter.WaiterId
            : "waiter-view:" + GetInstanceID();
    }

    private Vector3 GetArrivalPosition()
    {
        return hasReservedArrival
            ? reservedArrivalPosition
            : currentDestination != null
                ? currentDestination.position
                : transform.position;
    }

    private void ReplanCurrentRoute(
        BistroBuilderNavigationReplanLevel level =
            BistroBuilderNavigationReplanLevel.RouteSuffixRepair)
    {
        if (!isMoving || currentDestination == null) return;
        RebuildRouteTo(GetArrivalPosition(), level);
    }

    private void RebuildRouteTo(
        Vector3 target,
        BistroBuilderNavigationReplanLevel level =
            BistroBuilderNavigationReplanLevel.Steering)
    {
        currentRouteIndex = 0;

        if (navigationService != null)
        {
            string ownerId = GetNavigationOwnerId();
            if (navigationService.TryGetNavigationTrace(ownerId, out _))
            {
                if (navigationService.TryReplanNavigation(
                        ownerId,
                        transform.position,
                        target,
                        level,
                        out BistroBuilderNavigationRoute rebuilt) &&
                    rebuilt != null && rebuilt.points != null)
                {
                    routePoints.Clear();
                    routePoints.AddRange(rebuilt.points);
                }
            }
            else
            {
                routePoints.Clear();
                var request = new BistroBuilderNavigationRequest
                {
                    ownerId = ownerId,
                    agentMask = BistroBuilderNavigationAgentMask.Waiter,
                    origin = transform.position,
                    destination = target,
                    nominalSpeed = movementSpeed,
                    mobilityRadius = circulationRadius,
                    externalUrgency = 0
                };

                if (navigationService.TryStartNavigation(
                        request,
                        out BistroBuilderNavigationPlan plan,
                        out _) &&
                    plan != null && plan.route != null && plan.route.points != null)
                    routePoints.AddRange(plan.route.points);
            }

            plannedNavigationRevision = navigationService.Revision;
            return;
        }

        routePoints.Clear();
        if (routingService != null)
            routingService.TryBuildRoute(transform.position, target, routePoints, out _);
        if (routePoints.Count == 0)
            routePoints.Add(target);
    }

    private bool EnsureScheduledRouteReady(string ownerId)
    {
        if (navigationService == null || routePoints.Count > 0)
            return true;

        if (navigationService.TryGetNavigationPlan(
                ownerId,
                out BistroBuilderNavigationPlan plan) &&
            plan != null && plan.route != null && plan.route.points != null &&
            plan.route.points.Count > 0)
        {
            routePoints.AddRange(plan.route.points);
            currentRouteIndex = 0;
            plannedNavigationRevision = navigationService.Revision;
            return true;
        }

        if (navigationService.TryGetNavigationTrace(
                ownerId,
                out BistroBuilderNavigationDecisionTrace trace) &&
            trace.state == BistroBuilderNavigationTravelState.RequestingRoute)
        {
            navigationService.ReportNavigationPosition(
                ownerId,
                transform.position,
                BistroBuilderNavigationWaitingReason.AwaitingCorridor);
            navigationService.RefreshDestination(ownerId);
            return false;
        }

        navigationService.ReleaseDestination(ownerId);
        hasReservedArrival = false;
        isMoving = false;
        currentDestination = null;
        return false;
    }
    private void CancelDeferredArrival()
    {
        if (deferredArrivalRoutine == null)
        {
            return;
        }

        StopCoroutine(deferredArrivalRoutine);
        deferredArrivalRoutine = null;
    }
}