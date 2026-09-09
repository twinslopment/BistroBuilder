using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cerebro operativo del Bloque 13. Mantiene una cartera breve de tareas por
/// camarero, calcula saturación, responsabilidad, apoyo entre zonas, conflictos
/// y afinidad de ruta. Las tareas siguen siendo autoridad de WaiterTaskQueue.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedWaiterService : MonoBehaviour
{
    [SerializeField] private BistroBuilderWaiterRoutingService routingService;
    [SerializeField] private BistroBuilderCustomerExperienceTrackingService experienceTrackingService;
    [SerializeField] private bool enableRouteAwareDispatch = true;
    [SerializeField] private bool enableContextActions = true;
    [SerializeField, Range(2, 6)] private int defaultPlanCapacity = 3;

    private readonly Dictionary<WaiterTask, float> firstSeenRealtime = new();
    private readonly Dictionary<WaiterTask, Waiter> plannedOwner = new();
    private readonly Dictionary<Waiter, List<BistroBuilderAdvancedWaiterTaskPlan>> plansByWaiter = new();
    private readonly Dictionary<string, Waiter> destinationReservations =
        new(StringComparer.Ordinal);
    private readonly List<WaiterTask> taskScratch = new(64);
    private readonly List<Waiter> waiterScratch = new(16);

    public event Action<Waiter, BistroBuilderWaiterContextActionKind> ContextActionSuggested;
    public event Action Changed;

    public bool IsOperational => isActiveAndEnabled && routingService != null;
    public BistroBuilderWaiterRoutingService RoutingService => routingService;

    private void Awake()
    {
        ResolveDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (routingService == null)
        {
            error = "Bloque 13 necesita BistroBuilderWaiterRoutingService.";
            return false;
        }
        if (experienceTrackingService == null)
        {
            error = "Bloque 13 necesita Customer Experience para acciones contextuales reales.";
            return false;
        }
        if (defaultPlanCapacity < 2 || defaultPlanCapacity > 6)
        {
            error = "La capacidad de planificación de camareros es inválida.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void RegisterWaiter(Waiter waiter)
    {
        if (waiter == null) return;
        EnsureProfile(waiter);
        if (!plansByWaiter.ContainsKey(waiter))
            plansByWaiter.Add(waiter, new List<BistroBuilderAdvancedWaiterTaskPlan>(6));
    }

    public void UnregisterWaiter(Waiter waiter)
    {
        if (waiter == null) return;
        plansByWaiter.Remove(waiter);
        waiterScratch.Clear();
        foreach (KeyValuePair<WaiterTask, Waiter> pair in plannedOwner)
            if (ReferenceEquals(pair.Value, waiter)) waiterScratch.Add(waiter);
        List<WaiterTask> removeTasks = new();
        foreach (KeyValuePair<WaiterTask, Waiter> pair in plannedOwner)
            if (ReferenceEquals(pair.Value, waiter)) removeTasks.Add(pair.Key);
        for (int i = 0; i < removeTasks.Count; i++) plannedOwner.Remove(removeTasks[i]);
        ReleaseReservations(waiter);
    }

    public void ResetForRuntimeLoad()
    {
        firstSeenRealtime.Clear();
        plannedOwner.Clear();
        destinationReservations.Clear();
        foreach (List<BistroBuilderAdvancedWaiterTaskPlan> plans in plansByWaiter.Values)
            plans.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Reconstruye de forma determinista la cartera de trabajo. Las tareas
    /// pendientes permanecen Pending: la planificación secundaria no roba la
    /// autoridad a la cola ni marca trabajos como iniciados.
    /// </summary>
    public void RebuildPlans(
        IReadOnlyList<WaiterTask> activeTasks,
        IEnumerable<Waiter> registeredWaiters,
        Func<WaiterTask, Vector3> destinationResolver)
    {
        if (activeTasks == null || registeredWaiters == null || destinationResolver == null)
            return;

        waiterScratch.Clear();
        foreach (Waiter waiter in registeredWaiters)
        {
            if (waiter == null) continue;
            RegisterWaiter(waiter);
            waiterScratch.Add(waiter);
            plansByWaiter[waiter].Clear();
        }

        plannedOwner.Clear();
        destinationReservations.Clear();
        taskScratch.Clear();
        var alive = new HashSet<WaiterTask>();
        float now = Time.unscaledTime;

        for (int i = 0; i < activeTasks.Count; i++)
        {
            WaiterTask task = activeTasks[i];
            if (task == null) continue;
            alive.Add(task);
            if (!firstSeenRealtime.ContainsKey(task)) firstSeenRealtime[task] = now;

            if (task.AssignedWaiter != null)
            {
                Waiter assigned = task.AssignedWaiter;
                RegisterWaiter(assigned);
                AddPlan(assigned, task, destinationResolver(task), true);
                ReserveDestination(task, assigned);
            }
            else if (task.IsPending)
            {
                taskScratch.Add(task);
            }
        }

        List<WaiterTask> stale = new();
        foreach (WaiterTask key in firstSeenRealtime.Keys)
            if (!alive.Contains(key)) stale.Add(key);
        for (int i = 0; i < stale.Count; i++) firstSeenRealtime.Remove(stale[i]);

        taskScratch.Sort((a, b) => ResolveDispatchScore(b).CompareTo(ResolveDispatchScore(a)));
        for (int i = 0; i < taskScratch.Count; i++)
        {
            WaiterTask task = taskScratch[i];
            Waiter best = FindBestPlanningWaiter(task, destinationResolver(task));
            if (best == null) continue;
            plannedOwner[task] = best;
            AddPlan(best, task, destinationResolver(task), false);
        }
        Changed?.Invoke();
    }

    public float ResolveDispatchScore(WaiterTask task)
    {
        if (task == null) return float.MinValue;
        float age = GetWaitSeconds(task);
        BistroBuilderWaiterContextActionKind action =
            BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(task, age, 0f);
        return BistroBuilderAdvancedWaiterPolicy.PriorityScore(task.Priority) +
               Mathf.Min(900f, age * 32f) +
               BistroBuilderAdvancedWaiterPolicy.ContextUrgencyBonus(action);
    }

    public Waiter FindBestAvailableWaiter(
        WaiterTask task,
        IEnumerable<Waiter> candidates,
        Vector3 destinationPosition,
        Waiter responsibleWaiter,
        out BistroBuilderWaiterTaskEvaluation bestEvaluation)
    {
        bestEvaluation = default;
        if (task == null || candidates == null) return null;

        Waiter best = null;
        float bestScore = float.MinValue;
        plannedOwner.TryGetValue(task, out Waiter planned);

        foreach (Waiter waiter in candidates)
        {
            if (waiter == null || !waiter.IsAvailable) continue;
            if (!CanUseDestination(task, waiter)) continue;

            BistroBuilderWaiterTaskEvaluation evaluation = Evaluate(
                waiter, task, destinationPosition, responsibleWaiter);
            float score = evaluation.Score;
            if (ReferenceEquals(waiter, planned)) score += 1200f;

            bool wins = best == null || score > bestScore ||
                (Mathf.Approximately(score, bestScore) && waiter.WaiterId < best.WaiterId);
            if (!wins) continue;
            best = waiter;
            bestScore = score;
            bestEvaluation = new BistroBuilderWaiterTaskEvaluation(
                score, evaluation.RouteMeters, evaluation.Responsibility, evaluation.ContextAction);
        }
        return best;
    }

    public void NotifyTaskAssigned(Waiter waiter, WaiterTask task)
    {
        if (waiter == null || task == null) return;
        plannedOwner[task] = waiter;
        ReserveDestination(task, waiter);
        BistroBuilderWaiterContextActionKind action =
            BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(
                task, GetWaitSeconds(task), ResolveSaturation01(waiter));
        if (enableContextActions && action != BistroBuilderWaiterContextActionKind.None)
        {
            CustomerGroup group = ResolveTaskGroup(task);
            if (group != null && experienceTrackingService != null)
                experienceTrackingService.TryApplyWaiterContextAction(
                    group, action, out _);
            ContextActionSuggested?.Invoke(waiter, action);
        }
        Changed?.Invoke();
    }

    public void NotifyTaskEnded(WaiterTask task)
    {
        if (task == null) return;
        plannedOwner.Remove(task);
        firstSeenRealtime.Remove(task);
        string key = DestinationKey(task);
        if (!string.IsNullOrEmpty(key)) destinationReservations.Remove(key);
        Changed?.Invoke();
    }

    public bool TryBuildSnapshot(Waiter waiter, out BistroBuilderAdvancedWaiterSnapshot snapshot)
    {
        snapshot = null;
        if (waiter == null) return false;
        RegisterWaiter(waiter);
        BistroBuilderAdvancedWaiterProfile profile = EnsureProfile(waiter);
        List<BistroBuilderAdvancedWaiterTaskPlan> plans = plansByWaiter[waiter];
        float saturation = ResolveSaturation01(waiter);
        snapshot = new BistroBuilderAdvancedWaiterSnapshot
        {
            waiterId = waiter.WaiterId,
            primaryZoneId = profile.PrimaryZoneId,
            saturation = BistroBuilderAdvancedWaiterPolicy.ResolveSaturation(saturation),
            plannedTaskCount = plans.Count,
            planCapacity = profile.SimultaneousPlanCapacity,
            saturation01 = saturation
        };
        foreach (KeyValuePair<string, Waiter> pair in destinationReservations)
            if (ReferenceEquals(pair.Value, waiter)) snapshot.currentDestinationReservation = pair.Key;
        for (int i = 0; i < plans.Count; i++) snapshot.plans.Add(plans[i]);
        return true;
    }

    private Waiter FindBestPlanningWaiter(WaiterTask task, Vector3 destination)
    {
        Waiter best = null;
        float bestScore = float.MinValue;
        Waiter responsible = task.Order != null ? task.Order.AssignedWaiter : null;
        for (int i = 0; i < waiterScratch.Count; i++)
        {
            Waiter waiter = waiterScratch[i];
            BistroBuilderAdvancedWaiterProfile profile = EnsureProfile(waiter);
            if (plansByWaiter[waiter].Count >= profile.SimultaneousPlanCapacity) continue;
            BistroBuilderWaiterTaskEvaluation evaluation = Evaluate(waiter, task, destination, responsible);
            if (best == null || evaluation.Score > bestScore ||
                (Mathf.Approximately(evaluation.Score, bestScore) && waiter.WaiterId < best.WaiterId))
            {
                best = waiter;
                bestScore = evaluation.Score;
            }
        }
        return best;
    }

    private BistroBuilderWaiterTaskEvaluation Evaluate(
        Waiter waiter,
        WaiterTask task,
        Vector3 destination,
        Waiter responsibleWaiter)
    {
        BistroBuilderAdvancedWaiterProfile profile = EnsureProfile(waiter);
        string zone = ResolveTaskZone(task);
        profile.SupportsZone(zone, out BistroBuilderWaiterResponsibilityKind responsibility);
        if (ReferenceEquals(waiter, responsibleWaiter)) responsibility = BistroBuilderWaiterResponsibilityKind.Primary;
        float saturation = ResolveSaturation01(waiter);
        float routeMeters = enableRouteAwareDispatch && routingService != null
            ? routingService.EstimateRouteMeters(waiter.transform.position, destination)
            : Vector3.Distance(waiter.transform.position, destination);
        float age = GetWaitSeconds(task);
        BistroBuilderWaiterContextActionKind action = enableContextActions
            ? BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(task, age, saturation)
            : BistroBuilderWaiterContextActionKind.None;
        float score = BistroBuilderAdvancedWaiterPolicy.PriorityScore(task.Priority) +
            Mathf.Min(900f, age * 32f) +
            BistroBuilderAdvancedWaiterPolicy.ResponsibilityBonus(responsibility) +
            BistroBuilderAdvancedWaiterPolicy.ContextUrgencyBonus(action) -
            routeMeters * 42f - saturation * 950f;
        score *= profile.ServiceEfficiency;
        return new BistroBuilderWaiterTaskEvaluation(score, routeMeters, responsibility, action);
    }

    private void AddPlan(Waiter waiter, WaiterTask task, Vector3 destination, bool active)
    {
        BistroBuilderAdvancedWaiterProfile profile = EnsureProfile(waiter);
        float saturation = ResolveSaturation01(waiter);
        BistroBuilderWaiterTaskEvaluation evaluation = Evaluate(
            waiter, task, destination, task.Order != null ? task.Order.AssignedWaiter : null);
        plansByWaiter[waiter].Add(new BistroBuilderAdvancedWaiterTaskPlan
        {
            taskId = task.TaskId,
            taskType = task.Type,
            destinationReferenceId = task.DestinationReferenceId,
            responsibility = evaluation.Responsibility,
            contextualAction = evaluation.ContextAction,
            score = evaluation.Score,
            estimatedRouteMeters = evaluation.RouteMeters,
            activeAssignment = active
        });
    }

    private float ResolveSaturation01(Waiter waiter)
    {
        BistroBuilderAdvancedWaiterProfile profile = EnsureProfile(waiter);
        int planned = plansByWaiter.TryGetValue(waiter, out List<BistroBuilderAdvancedWaiterTaskPlan> plans)
            ? plans.Count : 0;
        int active = waiter.IsAvailable ? 0 : 1;
        return BistroBuilderAdvancedWaiterPolicy.ResolveSaturation01(active, planned, profile.SimultaneousPlanCapacity);
    }

    private float GetWaitSeconds(WaiterTask task)
    {
        return task != null && firstSeenRealtime.TryGetValue(task, out float seen)
            ? Mathf.Max(0f, Time.unscaledTime - seen)
            : 0f;
    }

    private static CustomerGroup ResolveTaskGroup(WaiterTask task)
    {
        if (task == null) return null;
        if (task.Order != null && task.Order.CustomerGroup != null)
            return task.Order.CustomerGroup;
        if (task.Table != null) return task.Table.AssignedCustomerGroup;
        return task.BarSpot != null ? task.BarSpot.AssignedCustomerGroup : null;
    }
    private static string ResolveTaskZone(WaiterTask task)
    {
        if (task?.BarSpot != null) return "bar";
        return "dining";
    }

    private bool CanUseDestination(WaiterTask task, Waiter waiter)
    {
        string key = DestinationKey(task);
        return string.IsNullOrEmpty(key) ||
               !destinationReservations.TryGetValue(key, out Waiter owner) ||
               owner == null || ReferenceEquals(owner, waiter);
    }

    private void ReserveDestination(WaiterTask task, Waiter waiter)
    {
        string key = DestinationKey(task);
        if (!string.IsNullOrEmpty(key)) destinationReservations[key] = waiter;
    }

    private void ReleaseReservations(Waiter waiter)
    {
        List<string> keys = new();
        foreach (KeyValuePair<string, Waiter> pair in destinationReservations)
            if (ReferenceEquals(pair.Value, waiter)) keys.Add(pair.Key);
        for (int i = 0; i < keys.Count; i++) destinationReservations.Remove(keys[i]);
    }

    private static string DestinationKey(WaiterTask task)
    {
        return task == null ? string.Empty : task.DestinationReferenceId;
    }

    private BistroBuilderAdvancedWaiterProfile EnsureProfile(Waiter waiter)
    {
        BistroBuilderAdvancedWaiterProfile profile = waiter.GetComponent<BistroBuilderAdvancedWaiterProfile>();
        if (profile == null) profile = waiter.gameObject.AddComponent<BistroBuilderAdvancedWaiterProfile>();
        return profile;
    }

    private void ResolveDependencies()
    {
        if (routingService == null) TryGetComponent(out routingService);
        if (routingService == null) routingService = FindFirstObjectByType<BistroBuilderWaiterRoutingService>();
        if (experienceTrackingService == null) TryGetComponent(out experienceTrackingService);
        if (experienceTrackingService == null)
            experienceTrackingService = FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>();
    }
}
