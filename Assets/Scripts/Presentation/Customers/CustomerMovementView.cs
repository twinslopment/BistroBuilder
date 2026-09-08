using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el desplazamiento visual de un grupo de clientes.
///
/// Puede mover al grupo hacia:
/// - Una posición de espera.
/// - La mesa asignada.
/// - La salida del restaurante.
///
/// La lógica del grupo permanece en CustomerGroup. Esta clase solamente
/// representa físicamente sus desplazamientos por la escena.
/// </summary>
public sealed class CustomerMovementView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private CustomerGroup customerGroup;

    [SerializeField]
    private Transform restaurantExitPoint;

    [SerializeField]
    private BistroBuilderNavigationService navigationService;

    [Header("Movimiento")]
    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.05f;

    [SerializeField, Min(0.1f)]
    private float circulationRadius = 0.32f;

/// <summary>
    /// Se ejecuta cuando el grupo llega al destino actual.
    ///
    /// Los distintos flujos comprueban el estado del grupo para saber
    /// si ha llegado a una mesa, a una posición de espera o a la salida.
    /// </summary>
    public event Action<CustomerMovementView> DestinationReached;

    public bool HasReachedDestination { get; private set; }

    private Transform currentDestination;
    private bool isMoving;
    private readonly List<Vector3> routePoints = new List<Vector3>(24);
    private int currentRouteIndex;
    private Vector3 reservedArrivalPosition;
    private bool hasReservedArrival;
    private float blockedSince = -1f;
    private int plannedNavigationRevision;

    private void Awake()
    {
        if (customerGroup == null)
        {
            customerGroup =
                GetComponent<CustomerGroup>();
        }
        if (navigationService == null)
        {
            navigationService = FindFirstObjectByType<BistroBuilderNavigationService>();
        }
    }

    private void OnEnable()
    {
        if (customerGroup == null)
        {
            Debug.LogError(
                "CustomerMovementView necesita una referencia " +
                "a CustomerGroup.",
                this
            );

            enabled = false;
            return;
        }

        customerGroup.StateChanged +=
            HandleStateChanged;
    }

    private void OnDisable()
    {
        if (customerGroup != null)
        {
            customerGroup.StateChanged -=
                HandleStateChanged;
        }


        navigationService?.CancelNavigation(GetNavigationOwnerId());
        navigationService?.RemoveAgentPresence(GetNavigationOwnerId());
        isMoving = false;
        currentDestination = null;
        hasReservedArrival = false;
    }

    private void Update()
    {
        string ownerId = GetNavigationOwnerId();
        if (navigationService != null)
        {
            navigationService.UpdateAgentPresence(
                ownerId,
                BistroBuilderNavigationAgentMask.Customer,
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

        Vector3 target = currentRouteIndex < routePoints.Count
            ? routePoints[currentRouteIndex]
            : GetArrivalPosition();

        Vector3 proposed = transform.position;
        BistroBuilderNavigationLocalMoveDecision decision = default;
        bool canMove = navigationService == null ||
            navigationService.TryResolveMovementStep(
                ownerId,
                BistroBuilderNavigationAgentMask.Customer,
                transform.position,
                target,
                movementSpeed,
                circulationRadius,
                0,
                Time.deltaTime,
                out proposed,
                out decision);

        if (navigationService == null)
        {
            proposed = Vector3.MoveTowards(
                transform.position, target, movementSpeed * Time.deltaTime);
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

        if ((transform.position - target).sqrMagnitude <=
            arrivalDistance * arrivalDistance &&
            currentRouteIndex < routePoints.Count - 1)
            currentRouteIndex++;

        if ((transform.position - GetArrivalPosition()).sqrMagnitude <=
            arrivalDistance * arrivalDistance)
            CompleteMovement();
    }

    /// <summary>
    /// Configura el punto de salida después de crear el grupo desde
    /// un prefab.
    ///
    /// El prefab no puede guardar directamente una referencia
    /// a un objeto perteneciente a una escena concreta.
    /// </summary>
    public void ConfigureExitPoint(
        Transform exitPoint
    )
    {
        if (exitPoint == null)
        {
            Debug.LogError(
                "No se puede configurar un punto de salida nulo.",
                this
            );

            return;
        }

        restaurantExitPoint = exitPoint;
    }

    /// <summary>
    /// Envía al grupo hacia una posición de la zona de espera.
    ///
    /// Solamente puede utilizarse mientras el grupo se encuentra
    /// esperando una mesa.
    /// </summary>
    public bool MoveToWaitingPoint(
        Transform waitingPoint
    )
    {
        if (waitingPoint == null)
        {
            Debug.LogError(
                "No se puede mover un grupo hacia un punto de espera nulo.",
                this
            );

            return false;
        }

        if (customerGroup == null ||
            customerGroup.CurrentState !=
                CustomerGroupState.WaitingForTable)
        {
            return false;
        }

        BeginMovement(waitingPoint);
        return true;
    }

    /// <summary>
    /// Envía al grupo a su plaza de barra sin convertirla en una mesa.
    /// WaitingAtBar puede conservar WaitingForTable mientras se desplaza.
    /// </summary>
    public bool MoveToBarPoint(BistroBuilderBarServiceSpot barSpot)
    {
        if (barSpot == null || customerGroup == null ||
            !ReferenceEquals(customerGroup.AssignedBarSpot, barSpot))
        {
            return false;
        }

        Transform destination = barSpot.CustomerPoint;

        if (destination == null)
        {
            return false;
        }

        BeginMovement(destination);
        return true;
    }

    /// <summary>
    /// Reacciona a los estados que implican un desplazamiento automático.
    /// </summary>
    private void HandleStateChanged(
        CustomerGroup group,
        CustomerGroupState newState
    )
    {
        Transform destination = newState switch
        {
            CustomerGroupState.WalkingToTable =>
                GetTableDestination(group),

            CustomerGroupState.WalkingToBar =>
                GetBarDestination(group),

            CustomerGroupState.Leaving =>
                GetExitDestination(),

            _ => null
        };

        if (destination == null)
            return;

        // Si el grupo estaba caminando hacia una posición de espera,
        // el nuevo destino sustituye inmediatamente al anterior.
        BeginMovement(destination);
    }

    /// <summary>
    /// Obtiene el punto de aproximación de la mesa asignada.
    /// </summary>
    private Transform GetTableDestination(
        CustomerGroup group
    )
    {
        RestaurantTable assignedTable =
            group.AssignedTable;

        if (assignedTable == null)
        {
            Debug.LogError(
                $"El grupo {group.GroupId} no tiene " +
                "una mesa asignada.",
                this
            );

            return null;
        }

        if (assignedTable.CustomerApproachPoint == null)
        {
            Debug.LogError(
                $"La mesa {assignedTable.TableId} no tiene " +
                "CustomerApproachPoint.",
                assignedTable
            );

            return null;
        }

        return assignedTable.CustomerApproachPoint;
    }

    private Transform GetBarDestination(CustomerGroup group)
    {
        if (group == null || group.AssignedBarSpot == null)
        {
            Debug.LogError(
                "El grupo no tiene una plaza de barra asignada.",
                this
            );
            return null;
        }

        return group.AssignedBarSpot.CustomerPoint;
    }

    /// <summary>
    /// Obtiene el punto por el que los clientes abandonan el restaurante.
    /// </summary>
    private Transform GetExitDestination()
    {
        if (restaurantExitPoint == null)
        {
            Debug.LogError(
                "CustomerMovementView necesita RestaurantExitPoint.",
                this
            );

            return null;
        }

        return restaurantExitPoint;
    }

    /// <summary>
    /// Inicia un desplazamiento hacia el destino indicado.
    /// </summary>
    private void BeginMovement(
        Transform destination
    )
    {
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
                BistroBuilderNavigationAgentMask.Customer,
                target,
                circulationRadius,
                0,
                out reservedArrivalPosition);
            if (hasReservedArrival) target = reservedArrivalPosition;
        }
        RebuildRouteTo(target);
    }

    /// <summary>
    /// Finaliza el desplazamiento y avisa a los flujos interesados.
    /// </summary>
    private string GetNavigationOwnerId()
    {
        return customerGroup != null
            ? "customer-group:" + customerGroup.GroupId
            : "customer-view:" + GetInstanceID();
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
                    agentMask = BistroBuilderNavigationAgentMask.Customer,
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
    private void CompleteMovement()
    {
        transform.position = GetArrivalPosition();
        navigationService?.CompleteNavigation(GetNavigationOwnerId());
        navigationService?.ReleaseDestination(GetNavigationOwnerId());
        currentDestination = null;
        isMoving = false;
        hasReservedArrival = false;
        HasReachedDestination = true;
        Debug.Log(
            $"Grupo {customerGroup.GroupId} ha llegado a su destino.",
            this);
        DestinationReached?.Invoke(this);
    }
}