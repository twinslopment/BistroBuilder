using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime v1 de Navigation & Crowd Flow.
/// Mantiene viajes, telemetría, watchdog y coordinación local sin invadir BBSIS.
/// </summary>
public sealed partial class BistroBuilderNavigationService
{
    [Header("Navigation v1 - Watchdog")]
    [SerializeField, Range(1f, 20f)] private float watchdogFrequencyHz = 5f;
    [SerializeField, Min(0.2f)] private float delayedThresholdSeconds = 1.5f;
    [SerializeField, Min(0.5f)] private float stalledThresholdSeconds = 3f;
    [SerializeField, Min(1f)] private float recoveryThresholdSeconds = 4.5f;
    [SerializeField, Min(1f)] private float fullReplanThresholdSeconds = 7f;
    [SerializeField, Min(2f)] private float explicitFailureThresholdSeconds = 12f;
    [SerializeField, Min(0.005f)] private float meaningfulProgressMeters = 0.03f;

    [Header("Navigation v1 - Traffic Heat")]
    [SerializeField, Range(1f, 10f)] private float trafficHeatFrequencyHz = 3f;

    private BistroBuilderNavigationTrafficCoordinator v1Traffic;
    private BistroBuilderNavigationTrafficHeatField v1TrafficHeat;
    private BistroBuilderNavigationRouteCache v1RouteCache;
    private BistroBuilderNavigationRouteGraph v1RouteGraph;
    private BistroBuilderNavigationControlledPassageCoordinator v1Passages;
    private BistroBuilderNavigationBlockDependencyGraph v1BlockGraph;
    private BistroBuilderSpatialAssessmentService v1SpatialAssessment;
    private readonly Dictionary<string, NavigationTripRuntime> v1Trips =
        new Dictionary<string, NavigationTripRuntime>(StringComparer.Ordinal);
    private readonly Dictionary<string, PresenceKinematics> v1Kinematics =
        new Dictionary<string, PresenceKinematics>(StringComparer.Ordinal);
    private readonly List<string> v1ScratchIds = new List<string>(64);
    private readonly List<string> v1CycleScratch = new List<string>(16);
    private readonly List<Vector3> v1RouteAnchorScratch = new List<Vector3>(16);
    private readonly List<Vector3> v1RouteLegScratch = new List<Vector3>(32);
    private readonly List<BistroBuilderNavigationGateDescriptor> v1GateDescriptors =
        new List<BistroBuilderNavigationGateDescriptor>(32);
    private readonly Dictionary<string, float> v1DeadlockLastSeen =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, BackoffRuntime> v1Backoffs =
        new Dictionary<string, BackoffRuntime>(StringComparer.Ordinal);
    private readonly BistroBuilderNavigationMetricsSnapshot v1Metrics =
        new BistroBuilderNavigationMetricsSnapshot();

    private float nextWatchdogAt;
    private float nextTrafficHeatAt;
    private int trafficEpoch = 1;
    private long requestSequence = 1;

    public event Action<BistroBuilderNavigationResult> NavigationTripFinished;
    public int TrafficEpoch => trafficEpoch;
    public int ActiveTripCount => v1Trips.Count;
    public int ActiveEncounterCount => v1Traffic != null ? v1Traffic.ActiveEncounterCount : 0;
    public int ControlledPassageCount => v1Passages != null ? v1Passages.GateCount : 0;
    public int BlockDependencyCount => v1BlockGraph != null ? v1BlockGraph.EdgeCount : 0;
    public int ActiveDeadlockCount => v1DeadlockLastSeen.Count;
    public int RouteGraphNodeCount => v1RouteGraph != null ? v1RouteGraph.NodeCount : 0;
    public int RouteGraphEdgeCount => v1RouteGraph != null ? v1RouteGraph.EdgeCount : 0;
    public int TrafficHeatCellCount => v1TrafficHeat != null ? v1TrafficHeat.ActiveCellCount : 0;
    public int RouteCachePositiveCount => v1RouteCache != null ? v1RouteCache.PositiveCount : 0;
    public int RouteCacheNegativeCount => v1RouteCache != null ? v1RouteCache.NegativeCount : 0;

    private void InitializeV1()
    {
        v1Traffic ??= new BistroBuilderNavigationTrafficCoordinator();
        v1TrafficHeat ??= new BistroBuilderNavigationTrafficHeatField();
        v1RouteCache ??= new BistroBuilderNavigationRouteCache();
        v1RouteGraph ??= new BistroBuilderNavigationRouteGraph();
        v1Passages ??= new BistroBuilderNavigationControlledPassageCoordinator();
        v1BlockGraph ??= new BistroBuilderNavigationBlockDependencyGraph();
        if (v1SpatialAssessment == null)
            v1SpatialAssessment = FindFirstObjectByType<BistroBuilderSpatialAssessmentService>();
    }

    private void TickV1()
    {
        InitializeV1();
        float now = Time.unscaledTime;
        v1Traffic.Cleanup(now);
        if (now >= nextTrafficHeatAt)
        {
            nextTrafficHeatAt = now + 1f / Mathf.Max(1f, trafficHeatFrequencyHz);
            if (v1TrafficHeat.Rebuild(now))
                IncrementTrafficEpoch();
        }
        v1Passages.Cleanup(now);
        v1BlockGraph.Cleanup(now);
        CleanupBackoffs(now);
        CleanupDeadlockEpisodes(now);
        v1Metrics.controlledPassageSwitchCount = v1Passages.SwitchCount;
        v1Metrics.controlledPassageWaitCount = v1Passages.WaitCount;
        if (now < nextWatchdogAt) return;
        nextWatchdogAt = now + 1f / Mathf.Max(1f, watchdogFrequencyHz);
        TickProgressWatchdog(now);
    }

    private void HandleTopologyRebuiltV1()
    {
        InitializeV1();
        IncrementTrafficEpoch();
        v1Metrics.topologyChangeCount++;
        if (v1SpatialAssessment != null)
            v1SpatialAssessment.EvaluateCurrentLayout();
        RebuildControlledPassagesV1();
        v1RouteGraph.Rebuild(areas, accessZones, v1GateDescriptors);
        BistroBuilderNavigationTopologySnapshot cacheTopology = CaptureTopologySnapshot();
        v1RouteCache.InvalidateStructuralState(
            cacheTopology.spatialRevision,
            cacheTopology.navigationRevision);

        BistroBuilderNavigationTopologySnapshot topology = CaptureTopologySnapshot();
        foreach (NavigationTripRuntime trip in v1Trips.Values)
        {
            if (trip == null) continue;
            trip.trace.topology = topology;
            trip.trace.lastDecision = "Topology rebuilt; route marked for structural verification.";
        }
    }

    private void NotifyTransientSpaceChangedV1()
    {
        IncrementTrafficEpoch();
        v1Metrics.transientChangeCount++;
    }

    private void IncrementTrafficEpoch()
    {
        trafficEpoch = trafficEpoch == int.MaxValue ? 1 : trafficEpoch + 1;
    }

    public BistroBuilderNavigationTopologySnapshot CaptureTopologySnapshot()
    {
        InitializeV1();
        int spatialRevision = spatialService != null ? spatialService.Revision : 0;
        float flowQuality = 1f;

        if (v1SpatialAssessment != null && v1SpatialAssessment.LastLedger != null)
        {
            if (v1SpatialAssessment.LastLedger.topologyRevision > 0)
                spatialRevision = v1SpatialAssessment.LastLedger.topologyRevision;
            flowQuality = Mathf.Clamp01(v1SpatialAssessment.LastLedger.flowQuality);
        }

        return new BistroBuilderNavigationTopologySnapshot
        {
            spatialRevision = spatialRevision,
            navigationRevision = Revision,
            trafficEpoch = trafficEpoch,
            flowQuality = flowQuality
        };
    }

    public bool TryStartNavigation(
        BistroBuilderNavigationRequest request,
        out BistroBuilderNavigationPlan plan,
        out BistroBuilderNavigationResult failure)
    {
        InitializeV1();
        plan = null;
        failure = null;

        if (request == null || !request.Validate(out _))
        {
            failure = BuildFailureResult(
                request,
                BistroBuilderNavigationFailureReason.NavigationAborted);
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.requestId))
            request.requestId = "nav:" + requestSequence++.ToString("D12");

        CancelNavigation(request.ownerId);

        if (!TryBuildRouteV1Cached(
                request.ownerId,
                request.agentMask,
                request.origin,
                request.destination,
                request.mobilityRadius,
                out BistroBuilderNavigationRoute route))
        {
            failure = BuildFailureResult(
                request,
                BistroBuilderNavigationFailureReason.RouteResolutionFailed);
            v1Metrics.tripsFailed++;
            return false;
        }

        BistroBuilderNavigationTopologySnapshot topology = CaptureTopologySnapshot();
        BistroBuilderNavigationRouteCostBreakdown cost =
            BuildInitialCost(request, route, topology);

        plan = new BistroBuilderNavigationPlan
        {
            requestId = request.requestId,
            ownerId = request.ownerId,
            topology = topology,
            route = route.DeepClone(),
            cost = cost,
            structurallyValid = true
        };

        var trace = new BistroBuilderNavigationDecisionTrace
        {
            ownerId = request.ownerId,
            requestId = request.requestId,
            state = BistroBuilderNavigationTravelState.FollowingRoute,
            waitingReason = BistroBuilderNavigationWaitingReason.None,
            recoveryStage = BistroBuilderNavigationRecoveryStage.None,
            routeLengthMeters = route.lengthMeters,
            topology = topology,
            lastDecision = "Initial route accepted."
        };

        v1Trips[request.ownerId] = new NavigationTripRuntime
        {
            request = request,
            plan = plan,
            trace = trace,
            startedAt = Time.unscaledTime,
            lastObservedAt = Time.unscaledTime,
            lastProgressAt = Time.unscaledTime,
            lastPosition = request.origin,
            lastProgressMeters = 0f
        };

        v1Metrics.tripsStarted++;
        return true;
    }

    public void ReportNavigationProgress(
        string ownerId,
        Vector3 position,
        float routeProgressMeters,
        BistroBuilderNavigationWaitingReason waitingReason =
            BistroBuilderNavigationWaitingReason.None,
        string blockerId = "")
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
            trip == null)
            return;

        float now = Time.unscaledTime;
        float dt = Mathf.Max(0f, now - trip.lastObservedAt);
        trip.actualDistanceMeters += Vector3.Distance(trip.lastPosition, position);

        if (routeProgressMeters > trip.lastProgressMeters + meaningfulProgressMeters)
        {
            trip.lastProgressMeters = routeProgressMeters;
            trip.lastProgressAt = now;
            trip.trace.secondsWithoutProgress = 0f;
            trip.inheritedUrgency = 0;
            trip.recoveryDebt = Mathf.Max(0f, trip.recoveryDebt - 0.2f);
            v1BlockGraph?.ClearDependency(ownerId);
            if (trip.trace.state == BistroBuilderNavigationTravelState.Recovering ||
                trip.trace.state == BistroBuilderNavigationTravelState.Replanning ||
                trip.trace.state == BistroBuilderNavigationTravelState.Yielding ||
                trip.trace.state == BistroBuilderNavigationTravelState.WaitingTransient ||
                trip.trace.state == BistroBuilderNavigationTravelState.ControlledPassage)
            {
                trip.trace.state = BistroBuilderNavigationTravelState.FollowingRoute;
                trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.None;
            }
        }

        if (waitingReason != BistroBuilderNavigationWaitingReason.None)
            v1Metrics.accumulatedWaitingSeconds += dt;

        trip.trace.routeProgressMeters =
            Mathf.Max(trip.trace.routeProgressMeters, routeProgressMeters);
        trip.trace.waitingReason = waitingReason;
        trip.trace.blockerId = blockerId ?? string.Empty;
        trip.trace.secondsWithoutProgress = Mathf.Max(0f, now - trip.lastProgressAt);
        trip.lastPosition = position;
        trip.lastObservedAt = now;
    }

    public BistroBuilderNavigationLocalMoveDecision SolveLocalVelocity(
        BistroBuilderNavigationLocalMoveInput input)
    {
        InitializeV1();
        BistroBuilderNavigationLocalMoveDecision decision =
            v1Traffic.Solve(input, Time.unscaledTime);

        if (decision.shouldYield)
        {
            v1Metrics.yieldCount++;
            if (v1Trips.TryGetValue(input.ownerId, out NavigationTripRuntime trip) &&
                trip != null)
            {
                trip.trace.state = BistroBuilderNavigationTravelState.Yielding;
                trip.trace.waitingReason = decision.waitingReason;
                trip.trace.yieldingTo = decision.yieldingTo ?? string.Empty;
                trip.trace.lastDecision = "Yielding to " + trip.trace.yieldingTo + ".";
            }
        }

        v1Metrics.encounterCount = v1Traffic.EncounterCreatedCount;
        return decision;
    }

    public bool TryGetNavigationTrace(
        string ownerId,
        out BistroBuilderNavigationDecisionTrace trace)
    {
        trace = null;
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
            trip == null)
            return false;

        trace = trip.trace.DeepClone();
        return true;
    }

    public bool TryGetControlledPassageSnapshot(
        string gateId,
        out BistroBuilderNavigationControlledPassageSnapshot snapshot)
    {
        InitializeV1();
        return v1Passages.TryGetSnapshot(gateId, out snapshot);
    }

    public BistroBuilderNavigationMetricsSnapshot CaptureNavigationMetrics()
    {
        v1Metrics.encounterCount =
            v1Traffic != null ? v1Traffic.EncounterCreatedCount : 0;
        if (v1Passages != null)
        {
            v1Metrics.controlledPassageSwitchCount = v1Passages.SwitchCount;
            v1Metrics.controlledPassageWaitCount = v1Passages.WaitCount;
        }
        return v1Metrics.DeepClone();
    }

    public bool CompleteNavigation(string ownerId)
    {
        if (!TryRemoveTrip(ownerId, out NavigationTripRuntime trip))
            return false;

        BistroBuilderNavigationResult result = BuildResult(
            trip,
            BistroBuilderNavigationTravelState.Arrived,
            BistroBuilderNavigationFailureReason.None);
        v1Metrics.tripsCompleted++;
        AccumulateTripMetrics(result);
        NavigationTripFinished?.Invoke(result);
        return true;
    }

    public bool FailNavigation(
        string ownerId,
        BistroBuilderNavigationFailureReason reason)
    {
        if (!TryRemoveTrip(ownerId, out NavigationTripRuntime trip))
            return false;

        trip.trace.state = BistroBuilderNavigationTravelState.Failed;
        trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.ExplicitFailure;
        BistroBuilderNavigationResult result = BuildResult(
            trip,
            BistroBuilderNavigationTravelState.Failed,
            reason);
        v1Metrics.tripsFailed++;
        AccumulateTripMetrics(result);
        NavigationTripFinished?.Invoke(result);
        return true;
    }

    public bool CancelNavigation(string ownerId)
    {
        if (!TryRemoveTrip(ownerId, out NavigationTripRuntime trip))
            return false;

        BistroBuilderNavigationResult result = BuildResult(
            trip,
            BistroBuilderNavigationTravelState.Cancelled,
            BistroBuilderNavigationFailureReason.NavigationAborted);
        NavigationTripFinished?.Invoke(result);
        return true;
    }

    private void UpdateAgentPresenceV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 position,
        float radius,
        int externalUrgency)
    {
        InitializeV1();
        if (string.IsNullOrWhiteSpace(ownerId)) return;

        float now = Time.unscaledTime;
        Vector3 velocity = Vector3.zero;
        if (v1Kinematics.TryGetValue(ownerId, out PresenceKinematics previous) &&
            previous != null)
        {
            float dt = now - previous.seenAt;
            if (dt > 0.0001f)
                velocity = (position - previous.position) / dt;
        }

        v1Kinematics[ownerId] = new PresenceKinematics
        {
            position = position,
            seenAt = now
        };

        float waitingAge = 0f;
        float recoveryDebt = 0f;
        float commitment = 0f;
        int effectiveExternalUrgency = externalUrgency;
        if (v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) &&
            trip != null)
        {
            waitingAge = Mathf.Max(0f, now - trip.lastProgressAt);
            recoveryDebt = trip.recoveryDebt;
            commitment = trip.commitment;
            effectiveExternalUrgency = Math.Max(
                effectiveExternalUrgency,
                Math.Max(trip.request.externalUrgency, trip.inheritedUrgency));
        }

        v1Traffic.UpsertPresence(
            ownerId,
            agent,
            position,
            velocity,
            radius,
            effectiveExternalUrgency,
            waitingAge,
            recoveryDebt,
            commitment,
            now);
        v1TrafficHeat.UpsertAgent(ownerId, position, velocity, radius, now);
        v1Passages.UpdateAgentPosition(ownerId, position, now);
    }

    private void RemoveAgentPresenceV1(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;
        v1Kinematics.Remove(ownerId);
        v1Traffic?.RemovePresence(ownerId);
        v1TrafficHeat?.RemoveAgent(ownerId);
        v1Passages?.RemoveAgent(ownerId);
        v1BlockGraph?.RemoveOwner(ownerId);
        v1Backoffs.Remove(ownerId);
    }

    private bool CanAdvanceV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 current,
        Vector3 proposed,
        float radius,
        int externalUrgency)
    {
        Vector3 delta = proposed - current;
        if (delta.sqrMagnitude <= 0.0000001f) return true;

        var input = new BistroBuilderNavigationLocalMoveInput
        {
            ownerId = ownerId,
            agentMask = agent,
            position = current,
            currentVelocity = Vector3.zero,
            preferredVelocity = delta / Mathf.Max(Time.deltaTime, 0.001f),
            radius = radius,
            externalUrgency = externalUrgency,
            waitingAgeSeconds = 0f,
            recoveryDebt = 0f,
            commitment = 0f
        };

        BistroBuilderNavigationLocalMoveDecision decision = SolveLocalVelocity(input);
        return !decision.shouldYield || decision.velocity.sqrMagnitude > 0.0001f;
    }

    public bool TryReplanNavigation(
        string ownerId,
        Vector3 origin,
        Vector3 destination,
        BistroBuilderNavigationReplanLevel level,
        out BistroBuilderNavigationRoute route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
            trip == null)
            return false;

        if (!TryBuildRouteV1Cached(
                ownerId,
                trip.request.agentMask,
                origin,
                destination,
                trip.request.mobilityRadius,
                out BistroBuilderNavigationRoute rebuilt))
            return false;

        trip.request.origin = origin;
        trip.request.destination = destination;
        trip.plan.route = rebuilt.DeepClone();
        trip.plan.topology = CaptureTopologySnapshot();
        trip.plan.cost = BuildInitialCost(trip.request, rebuilt, trip.plan.topology);
        trip.plan.structurallyValid = true;
        trip.trace.topology = trip.plan.topology;
        trip.trace.routeLengthMeters = rebuilt.lengthMeters;
        trip.trace.state = BistroBuilderNavigationTravelState.FollowingRoute;
        trip.trace.replanCount++;
        trip.trace.lastDecision = "Route replanned at level " + level + ".";
        if (level == BistroBuilderNavigationReplanLevel.FullReplan)
            v1Metrics.fullReplanCount++;

        route = rebuilt.DeepClone();
        return true;
    }

    public void ReportNavigationPosition(
        string ownerId,
        Vector3 position,
        BistroBuilderNavigationWaitingReason waitingReason =
            BistroBuilderNavigationWaitingReason.None,
        string blockerId = "")
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
            trip == null ||
            trip.plan == null ||
            trip.plan.route == null)
            return;

        float progress = ProjectProgressOnRoute(
            trip.request.origin,
            trip.plan.route.points,
            position);
        ReportNavigationProgress(
            ownerId,
            position,
            progress,
            waitingReason,
            blockerId);
    }

    public bool TryResolveMovementStep(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 current,
        Vector3 target,
        float nominalSpeed,
        float radius,
        int externalUrgency,
        float deltaTime,
        out Vector3 proposed,
        out BistroBuilderNavigationLocalMoveDecision decision)
    {
        InitializeV1();
        proposed = current;
        Vector3 delta = target - current;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            decision = new BistroBuilderNavigationLocalMoveDecision
            {
                velocity = Vector3.zero,
                waitingReason = BistroBuilderNavigationWaitingReason.None
            };
            proposed = target;
            v1BlockGraph.ClearDependency(ownerId);
            return true;
        }

        float now = Time.unscaledTime;
        float speed = Mathf.Max(0.05f, nominalSpeed);
        float dt = Mathf.Max(0.001f, deltaTime);
        float effectiveRadius = Mathf.Max(0.05f, radius);
        Vector3 preferredDirection = delta / distance;
        Vector3 preferredVelocity = preferredDirection * speed;

        float waitingAge = 0f;
        float recoveryDebt = 0f;
        float commitment = 0f;
        int effectiveExternalUrgency = externalUrgency;
        NavigationTripRuntime trip = null;
        if (v1Trips.TryGetValue(ownerId, out trip) && trip != null)
        {
            waitingAge = Mathf.Max(0f, now - trip.lastProgressAt);
            recoveryDebt = trip.recoveryDebt;
            commitment = trip.commitment;
            effectiveExternalUrgency = Math.Max(
                effectiveExternalUrgency,
                Math.Max(trip.request.externalUrgency, trip.inheritedUrgency));
            trip.lastPreferredDirection = preferredDirection;
        }

        if (TryApplyActiveBackoffV1(
                ownerId,
                agent,
                current,
                speed,
                effectiveRadius,
                dt,
                now,
                out proposed,
                out decision))
            return true;

        Vector3 intendedStep = preferredVelocity * dt;
        if (intendedStep.magnitude > distance)
            intendedStep = delta;
        Vector3 intendedPosition = current + intendedStep;
        intendedPosition.y = Mathf.MoveTowards(current.y, target.y, speed * dt);

        if (!v1Passages.TryAuthorizeStep(
                ownerId,
                current,
                intendedPosition,
                effectiveRadius,
                effectiveExternalUrgency,
                waitingAge,
                now,
                out BistroBuilderNavigationControlledPassageDecision passage))
        {
            decision = new BistroBuilderNavigationLocalMoveDecision
            {
                velocity = Vector3.zero,
                shouldYield = true,
                yieldingTo = string.Empty,
                effectivePriority = effectiveExternalUrgency + waitingAge * 4f,
                waitingReason = BistroBuilderNavigationWaitingReason.ControlledPassage
            };
            v1BlockGraph.SetDependency(ownerId, passage.blockerId, now);
            if (trip != null)
            {
                trip.trace.state = BistroBuilderNavigationTravelState.ControlledPassage;
                trip.trace.waitingReason = BistroBuilderNavigationWaitingReason.ControlledPassage;
                trip.trace.blockerId = passage.blockerId ?? string.Empty;
                trip.trace.lastDecision = "Waiting for controlled passage " + passage.gateId + ".";
            }
            return false;
        }

        if (trip != null)
        {
            trip.commitment = !string.IsNullOrEmpty(passage.gateId)
                ? 1f
                : Mathf.MoveTowards(trip.commitment, 0f, dt * 2f);
            commitment = trip.commitment;
        }

        var input = new BistroBuilderNavigationLocalMoveInput
        {
            ownerId = ownerId,
            agentMask = agent,
            position = current,
            currentVelocity = Vector3.zero,
            preferredVelocity = preferredVelocity,
            radius = effectiveRadius,
            externalUrgency = effectiveExternalUrgency,
            waitingAgeSeconds = waitingAge,
            recoveryDebt = recoveryDebt,
            commitment = commitment
        };

        decision = SolveLocalVelocity(input);
        if (decision.shouldYield && !string.IsNullOrWhiteSpace(decision.yieldingTo))
        {
            v1BlockGraph.SetDependency(ownerId, decision.yieldingTo, now);
            ProcessPotentialDeadlockV1(ownerId, now);
        }
        else if (decision.velocity.sqrMagnitude > 0.000001f)
        {
            v1BlockGraph.ClearDependency(ownerId);
        }

        if (decision.velocity.sqrMagnitude <= 0.000001f)
        {
            if (trip != null)
            {
                trip.trace.state = BistroBuilderNavigationTravelState.Yielding;
                trip.trace.waitingReason =
                    decision.waitingReason == BistroBuilderNavigationWaitingReason.None
                        ? BistroBuilderNavigationWaitingReason.Yield
                        : decision.waitingReason;
                trip.trace.blockerId = decision.yieldingTo ?? string.Empty;
            }
            ProcessPotentialDeadlockV1(ownerId, now);
            return false;
        }

        Vector3 step = decision.velocity * dt;
        if (step.magnitude > distance)
            step = delta;
        proposed = current + step;
        proposed.y = Mathf.MoveTowards(current.y, target.y, speed * dt);

        if (!IsHardMovementStepClear(ownerId, agent, proposed, effectiveRadius))
        {
            proposed = current;
            decision.velocity = Vector3.zero;
            decision.shouldYield = true;
            decision.waitingReason = BistroBuilderNavigationWaitingReason.TransientSweep;
            v1BlockGraph.SetDependency(ownerId, "spatial:hard", now);
            if (trip != null)
            {
                trip.trace.state = BistroBuilderNavigationTravelState.WaitingTransient;
                trip.trace.waitingReason = BistroBuilderNavigationWaitingReason.TransientSweep;
                trip.trace.blockerId = "spatial:hard";
                trip.trace.lastDecision =
                    "Hard spatial/transient constraint blocks next step.";
            }
            return false;
        }

        v1BlockGraph.ClearDependency(ownerId);
        if (trip != null)
            trip.inheritedUrgency = 0;
        return true;
    }
    private bool IsHardMovementStepClear(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 proposed,
        float radius)
    {
        float r = Mathf.Max(0.05f, radius);
        for (int i = 0; i < staticShapes.Count; i++)
            if (PointInsideShape(proposed, staticShapes[i], r + staticClearance))
                return false;

        if (spatialService != null &&
            spatialService.BlocksTraversalPoint(proposed, r, ownerId))
            return false;

        for (int i = 0; i < dynamicEnvelopes.Count; i++)
        {
            BistroBuilderDynamicCirculationEnvelope envelope = dynamicEnvelopes[i];
            if (envelope == null ||
                (spatialService != null && envelope.HasSpatialLease))
                continue;
            if (envelope.BlocksPoint(proposed, r, agent, ownerId))
                return false;
        }

        return true;
    }

    private bool TryBuildRouteV1Cached(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        out BistroBuilderNavigationRoute route)
    {
        InitializeV1();
        BistroBuilderNavigationTopologySnapshot topology = CaptureTopologySnapshot();
        float radius = Mathf.Max(0.05f, mobilityRadius);

        if (v1RouteCache.IsKnownUnavailable(
                topology, agent, origin, destination, radius))
        {
            SyncRouteCacheMetricsV1();
            route = null;
            return false;
        }

        if (v1RouteCache.TryGet(
                topology, agent, origin, destination, radius, out route) &&
            PrepareCachedRouteForEndpointsV1(
                ownerId, agent, origin, destination, radius, route))
        {
            route.navigationRevision = Revision;
            route.congestionCost = MeasureCongestionAlongRoute(route.points, ownerId);
            route.totalScore = route.lengthMeters +
                               route.congestionCost * congestionWeight;
            SyncRouteCacheMetricsV1();
            return true;
        }

        if (!TryBuildHierarchicalRouteV1(
                ownerId,
                agent,
                origin,
                destination,
                radius,
                out route))
        {
            v1Metrics.topologicalFallbackCount++;
            if (!TryBuildRouteDetailed(
                    ownerId,
                    agent,
                    origin,
                    destination,
                    out route))
            {
                v1RouteCache.StoreUnavailable(
                    topology, agent, origin, destination, radius);
                SyncRouteCacheMetricsV1();
                return false;
            }
        }
        else
        {
            v1Metrics.topologicalPlanCount++;
        }

        if (route.kind == BistroBuilderNavigationRouteKind.NavMesh ||
            route.kind == BistroBuilderNavigationRouteKind.OperationalDock)
        {
            v1RouteCache.Store(
                topology, agent, origin, destination, radius, route);
        }
        SyncRouteCacheMetricsV1();
        return true;
    }

    private bool TryBuildHierarchicalRouteV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float mobilityRadius,
        out BistroBuilderNavigationRoute route)
    {
        route = null;
        if (v1RouteGraph == null || v1RouteGraph.NodeCount == 0)
            return false;

        v1RouteAnchorScratch.Clear();
        if (!v1RouteGraph.TryFindGateAnchors(
                origin,
                destination,
                agent,
                mobilityRadius,
                GetTrafficCongestionV1,
                v1RouteAnchorScratch,
                out _))
            return false;

        // Sin gates intermedios la ruta directa de NavMesh es más barata y suficiente.
        if (v1RouteAnchorScratch.Count == 0)
            return false;

        var points = new List<Vector3>(32);
        float totalLength = 0f;
        float totalCongestion = 0f;
        Vector3 legOrigin = origin;

        for (int i = 0; i <= v1RouteAnchorScratch.Count; i++)
        {
            Vector3 legDestination = i < v1RouteAnchorScratch.Count
                ? v1RouteAnchorScratch[i]
                : destination;
            v1RouteLegScratch.Clear();
            if (!TryBuildNavMeshRoute(
                    ownerId,
                    agent,
                    legOrigin,
                    legDestination,
                    v1RouteLegScratch,
                    out float legLength,
                    out float legCongestion))
                return false;

            for (int pointIndex = 0; pointIndex < v1RouteLegScratch.Count; pointIndex++)
            {
                Vector3 point = v1RouteLegScratch[pointIndex];
                if (points.Count > 0 &&
                    (points[points.Count - 1] - point).sqrMagnitude <= 0.0001f)
                    continue;
                points.Add(point);
            }
            totalLength += legLength;
            totalCongestion += legCongestion;
            legOrigin = legDestination;
        }

        if (points.Count == 0)
            return false;

        route = new BistroBuilderNavigationRoute
        {
            kind = BistroBuilderNavigationRouteKind.NavMesh,
            points = points,
            lengthMeters = totalLength,
            congestionCost = totalCongestion,
            totalScore = totalLength + totalCongestion * congestionWeight,
            navigationRevision = Revision,
            isComplete = true
        };
        return true;
    }
    private bool PrepareCachedRouteForEndpointsV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 origin,
        Vector3 destination,
        float radius,
        BistroBuilderNavigationRoute route)
    {
        if (route == null || route.points == null || route.points.Count == 0)
            return false;

        Vector3 first = route.points[0];
        if (!SegmentAllowedForNavMesh(
                origin,
                first,
                radius,
                agent,
                ownerId,
                origin,
                destination))
            return false;

        int lastIndex = route.points.Count - 1;
        Vector3 previous = lastIndex > 0 ? route.points[lastIndex - 1] : origin;
        if (!SegmentAllowedForNavMesh(
                previous,
                destination,
                radius,
                agent,
                ownerId,
                origin,
                destination))
            return false;

        route.points[lastIndex] = destination;
        float length = 0f;
        Vector3 cursor = origin;
        for (int i = 0; i < route.points.Count; i++)
        {
            length += Vector3.Distance(cursor, route.points[i]);
            cursor = route.points[i];
        }
        route.lengthMeters = length;
        return true;
    }

    private void SyncRouteCacheMetricsV1()
    {
        if (v1RouteCache == null) return;
        v1Metrics.routeCacheHitCount = v1RouteCache.HitCount;
        v1Metrics.routeCacheMissCount = v1RouteCache.MissCount;
        v1Metrics.negativeRouteCacheHitCount = v1RouteCache.NegativeHitCount;
    }
    private float GetTrafficCongestionV1(Vector3 point)
    {
        InitializeV1();
        return v1TrafficHeat != null ? v1TrafficHeat.QueryPenalty(point) : 0f;
    }

    public int WriteTrafficHeatSnapshots(
        List<BistroBuilderNavigationTrafficCellSnapshot> results)
    {
        InitializeV1();
        return v1TrafficHeat != null && results != null
            ? v1TrafficHeat.WriteSnapshots(results)
            : 0;
    }
    private void RebuildControlledPassagesV1()
    {
        v1GateDescriptors.Clear();
        BistroBuilderSpatialSubject[] subjects =
            FindObjectsByType<BistroBuilderSpatialSubject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.InstanceID);

        Array.Sort(subjects, (first, second) =>
        {
            if (ReferenceEquals(first, second)) return 0;
            if (first == null) return 1;
            if (second == null) return -1;
            return string.CompareOrdinal(first.SubjectId, second.SubjectId);
        });

        for (int subjectIndex = 0; subjectIndex < subjects.Length; subjectIndex++)
        {
            BistroBuilderSpatialSubject subject = subjects[subjectIndex];
            if (subject == null || subject.Contract == null) continue;
            IReadOnlyList<BistroBuilderSpatialGateDefinition> gates =
                subject.Contract.Gates;
            for (int gateIndex = 0; gateIndex < gates.Count; gateIndex++)
            {
                BistroBuilderSpatialGateDefinition gate = gates[gateIndex];
                if (gate == null || string.IsNullOrWhiteSpace(gate.gateId))
                    continue;

                float scale = Mathf.Max(
                    Mathf.Abs(subject.transform.lossyScale.x),
                    Mathf.Abs(subject.transform.lossyScale.z));
                v1GateDescriptors.Add(new BistroBuilderNavigationGateDescriptor
                {
                    gateId = subject.SubjectId + ":" + gate.gateId,
                    subjectId = subject.SubjectId,
                    start = subject.transform.TransformPoint(gate.localStart),
                    end = subject.transform.TransformPoint(gate.localEnd),
                    minimumWidth = Mathf.Max(0.1f, gate.minimumWidth * scale),
                    criticalRoute = gate.criticalRoute
                });
            }
        }

        v1Passages.Configure(v1GateDescriptors);
    }

    private bool TryApplyActiveBackoffV1(
        string ownerId,
        BistroBuilderNavigationAgentMask agent,
        Vector3 current,
        float speed,
        float radius,
        float dt,
        float now,
        out Vector3 proposed,
        out BistroBuilderNavigationLocalMoveDecision decision)
    {
        proposed = current;
        decision = default;
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Backoffs.TryGetValue(ownerId, out BackoffRuntime backoff) ||
            backoff == null || now >= backoff.until)
            return false;

        Vector3 velocity = backoff.direction * Mathf.Max(0.12f, speed * 0.35f);
        Vector3 candidate = current + velocity * dt;
        candidate.y = current.y;
        if (!IsHardMovementStepClear(ownerId, agent, candidate, radius))
        {
            v1Backoffs.Remove(ownerId);
            return false;
        }

        proposed = candidate;
        decision = new BistroBuilderNavigationLocalMoveDecision
        {
            velocity = velocity,
            shouldYield = false,
            yieldingTo = string.Empty,
            passingSide = 0,
            effectivePriority = ResolveEffectivePriorityV1(ownerId),
            waitingReason = BistroBuilderNavigationWaitingReason.Recovery
        };
        v1BlockGraph.ClearDependency(ownerId);

        if (v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) && trip != null)
        {
            trip.trace.state = BistroBuilderNavigationTravelState.Recovering;
            trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.LocalBackoff;
            trip.trace.waitingReason = BistroBuilderNavigationWaitingReason.Recovery;
            trip.trace.lastDecision = "Deadlock recovery: local backoff.";
        }
        return true;
    }

    private void ProcessPotentialDeadlockV1(string ownerId, float now)
    {
        v1CycleScratch.Clear();
        if (!v1BlockGraph.TryFindCycle(
                ownerId,
                v1CycleScratch,
                out string signature))
            return;

        bool newEpisode =
            !v1DeadlockLastSeen.TryGetValue(signature, out float previousSeen) ||
            now - previousSeen > 1.5f;
        v1DeadlockLastSeen[signature] = now;
        if (newEpisode)
            v1Metrics.deadlockCount++;

        int inheritedUrgency = 0;
        for (int i = 0; i < v1CycleScratch.Count; i++)
        {
            if (!v1Trips.TryGetValue(
                    v1CycleScratch[i],
                    out NavigationTripRuntime participant) ||
                participant == null)
                continue;
            inheritedUrgency = Math.Max(
                inheritedUrgency,
                participant.request.externalUrgency);
        }

        for (int i = 0; i < v1CycleScratch.Count; i++)
        {
            if (!v1Trips.TryGetValue(
                    v1CycleScratch[i],
                    out NavigationTripRuntime participant) ||
                participant == null)
                continue;
            participant.inheritedUrgency = Math.Max(
                participant.inheritedUrgency,
                inheritedUrgency);
            participant.trace.state = BistroBuilderNavigationTravelState.Recovering;
            participant.trace.recoveryStage =
                BistroBuilderNavigationRecoveryStage.PriorityInheritance;
            participant.trace.lastDecision =
                "Deadlock cycle detected: " + signature + ".";
        }

        string candidate = v1BlockGraph.SelectRecoveryCandidate(
            v1CycleScratch,
            ResolveEffectivePriorityV1);
        if (string.IsNullOrWhiteSpace(candidate) ||
            !v1Trips.TryGetValue(candidate, out NavigationTripRuntime candidateTrip) ||
            candidateTrip == null ||
            candidateTrip.lastPreferredDirection.sqrMagnitude <= 0.0001f)
            return;

        if (v1Backoffs.TryGetValue(candidate, out BackoffRuntime existing) &&
            existing != null && now < existing.until)
            return;

        v1Backoffs[candidate] = new BackoffRuntime
        {
            direction = -candidateTrip.lastPreferredDirection.normalized,
            until = now + 0.65f,
            signature = signature
        };
        candidateTrip.recoveryDebt += 0.75f;
        candidateTrip.trace.recoveryStage =
            BistroBuilderNavigationRecoveryStage.LocalBackoff;
        candidateTrip.trace.recoveryCount++;
        candidateTrip.trace.lastDecision =
            "Deadlock recovery selected this agent for local backoff.";
        v1Metrics.localBackoffCount++;
        v1Metrics.recoveryCount++;
    }

    private float ResolveEffectivePriorityV1(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
            trip == null)
            return 0f;

        float waitingAge = Mathf.Max(0f, Time.unscaledTime - trip.lastProgressAt);
        return Math.Max(trip.request.externalUrgency, trip.inheritedUrgency) +
               waitingAge * 4f +
               trip.recoveryDebt * 10f +
               trip.commitment * 20f;
    }

    private void CleanupBackoffs(float now)
    {
        v1ScratchIds.Clear();
        foreach (KeyValuePair<string, BackoffRuntime> pair in v1Backoffs)
            if (pair.Value == null || now >= pair.Value.until)
                v1ScratchIds.Add(pair.Key);
        for (int i = 0; i < v1ScratchIds.Count; i++)
            v1Backoffs.Remove(v1ScratchIds[i]);
    }

    private void CleanupDeadlockEpisodes(float now)
    {
        v1ScratchIds.Clear();
        foreach (KeyValuePair<string, float> pair in v1DeadlockLastSeen)
            if (now - pair.Value > 2f)
                v1ScratchIds.Add(pair.Key);
        for (int i = 0; i < v1ScratchIds.Count; i++)
            v1DeadlockLastSeen.Remove(v1ScratchIds[i]);
    }
    private static float ProjectProgressOnRoute(
        Vector3 origin,
        List<Vector3> points,
        Vector3 position)
    {
        if (points == null || points.Count == 0)
            return 0f;

        Vector3 segmentStart = origin;
        float cumulative = 0f;
        float bestProgress = 0f;
        float bestDistanceSq = float.PositiveInfinity;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 segmentEnd = points[i];
            Vector3 segment = segmentEnd - segmentStart;
            segment.y = 0f;
            float length = segment.magnitude;
            if (length > 0.0001f)
            {
                Vector3 fromStart = position - segmentStart;
                fromStart.y = 0f;
                float t = Mathf.Clamp01(
                    Vector3.Dot(fromStart, segment) / (length * length));
                Vector3 projected = segmentStart + segment * t;
                float distanceSq = HorizontalDistanceSquared(position, projected);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestProgress = cumulative + length * t;
                }
                cumulative += length;
            }
            segmentStart = segmentEnd;
        }

        return bestProgress;
    }
    private void TickProgressWatchdog(float now)
    {
        v1ScratchIds.Clear();
        foreach (string ownerId in v1Trips.Keys)
            v1ScratchIds.Add(ownerId);
        v1ScratchIds.Sort(StringComparer.Ordinal);

        for (int i = 0; i < v1ScratchIds.Count; i++)
        {
            string ownerId = v1ScratchIds[i];
            if (!v1Trips.TryGetValue(ownerId, out NavigationTripRuntime trip) ||
                trip == null)
                continue;

            float stalledFor = Mathf.Max(0f, now - trip.lastProgressAt);
            trip.trace.secondsWithoutProgress = stalledFor;

            if (stalledFor < delayedThresholdSeconds)
                continue;

            if (stalledFor >= explicitFailureThresholdSeconds)
            {
                trip.trace.lastDecision = "Progress watchdog escalated to explicit failure.";
                FailNavigation(ownerId, BistroBuilderNavigationFailureReason.TemporarilyBlocked);
                continue;
            }

            if (stalledFor >= fullReplanThresholdSeconds)
            {
                if (trip.trace.recoveryStage < BistroBuilderNavigationRecoveryStage.FullReplan)
                {
                    trip.trace.state = BistroBuilderNavigationTravelState.Replanning;
                    trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.FullReplan;
                    trip.trace.replanCount++;
                    trip.trace.recoveryCount++;
                    trip.recoveryDebt += 1f;
                    v1Metrics.fullReplanCount++;
                    v1Metrics.recoveryCount++;
                    trip.trace.lastDecision = "Watchdog requested full replan.";
                }
                continue;
            }

            if (stalledFor >= recoveryThresholdSeconds)
            {
                if (trip.trace.recoveryStage < BistroBuilderNavigationRecoveryStage.CorridorRepair)
                {
                    trip.trace.state = BistroBuilderNavigationTravelState.Recovering;
                    trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.CorridorRepair;
                    trip.trace.recoveryCount++;
                    trip.recoveryDebt += 0.5f;
                    v1Metrics.recoveryCount++;
                    trip.trace.lastDecision = "Watchdog requested corridor repair.";
                }
                continue;
            }

            if (stalledFor >= stalledThresholdSeconds)
            {
                if (trip.trace.recoveryStage < BistroBuilderNavigationRecoveryStage.Yield)
                {
                    trip.trace.state = BistroBuilderNavigationTravelState.Recovering;
                    trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.Yield;
                    trip.trace.recoveryCount++;
                    v1Metrics.stallCount++;
                    v1Metrics.recoveryCount++;
                    trip.trace.lastDecision = "Watchdog escalated stalled trip to yield recovery.";
                }
                continue;
            }

            trip.trace.state = BistroBuilderNavigationTravelState.WaitingTransient;
            trip.trace.recoveryStage = BistroBuilderNavigationRecoveryStage.ReciprocalCorrection;
            trip.trace.waitingReason =
                trip.trace.waitingReason == BistroBuilderNavigationWaitingReason.None
                    ? BistroBuilderNavigationWaitingReason.AwaitingCorridor
                    : trip.trace.waitingReason;
            trip.trace.lastDecision = "Trip delayed; watchdog observing.";
        }
    }

    private BistroBuilderNavigationRouteCostBreakdown BuildInitialCost(
        BistroBuilderNavigationRequest request,
        BistroBuilderNavigationRoute route,
        BistroBuilderNavigationTopologySnapshot topology)
    {
        float speed = Mathf.Max(0.1f, request.nominalSpeed);
        float freeFlow = route.lengthMeters / speed;
        return new BistroBuilderNavigationRouteCostBreakdown
        {
            freeFlowTime = freeFlow,
            flowQualityPenalty = freeFlow * (1f - topology.flowQuality) * 0.35f,
            congestionDelay = Mathf.Max(0f, route.congestionCost) * 0.15f
        };
    }

    private bool TryRemoveTrip(
        string ownerId,
        out NavigationTripRuntime trip)
    {
        trip = null;
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !v1Trips.TryGetValue(ownerId, out trip) ||
            trip == null)
            return false;

        v1Trips.Remove(ownerId);
        v1BlockGraph?.ClearDependency(ownerId);
        v1Backoffs.Remove(ownerId);
        return true;
    }

    private BistroBuilderNavigationResult BuildResult(
        NavigationTripRuntime trip,
        BistroBuilderNavigationTravelState state,
        BistroBuilderNavigationFailureReason reason)
    {
        float actual = Mathf.Max(0f, Time.unscaledTime - trip.startedAt);
        return new BistroBuilderNavigationResult
        {
            requestId = trip.request.requestId,
            ownerId = trip.request.ownerId,
            finalState = state,
            failureReason = reason,
            actualTravelSeconds = actual,
            actualDistanceMeters = trip.actualDistanceMeters,
            idealTravelSeconds = trip.plan != null && trip.plan.cost != null
                ? trip.plan.cost.freeFlowTime
                : 0f
        };
    }

    private static BistroBuilderNavigationResult BuildFailureResult(
        BistroBuilderNavigationRequest request,
        BistroBuilderNavigationFailureReason reason)
    {
        return new BistroBuilderNavigationResult
        {
            requestId = request != null ? request.requestId : string.Empty,
            ownerId = request != null ? request.ownerId : string.Empty,
            finalState = BistroBuilderNavigationTravelState.Failed,
            failureReason = reason
        };
    }

    private void AccumulateTripMetrics(BistroBuilderNavigationResult result)
    {
        if (result == null) return;
        v1Metrics.accumulatedActualTravelSeconds += result.actualTravelSeconds;
        v1Metrics.accumulatedIdealTravelSeconds += result.idealTravelSeconds;
    }

    private sealed class NavigationTripRuntime
    {
        public BistroBuilderNavigationRequest request;
        public BistroBuilderNavigationPlan plan;
        public BistroBuilderNavigationDecisionTrace trace;
        public float startedAt;
        public float lastObservedAt;
        public float lastProgressAt;
        public Vector3 lastPosition;
        public float lastProgressMeters;
        public float actualDistanceMeters;
        public float recoveryDebt;
        public float commitment;
        public int inheritedUrgency;
        public Vector3 lastPreferredDirection;
    }

    private sealed class BackoffRuntime
    {
        public Vector3 direction;
        public float until;
        public string signature;
    }

    private sealed class PresenceKinematics
    {
        public Vector3 position;
        public float seenAt;
    }
}
