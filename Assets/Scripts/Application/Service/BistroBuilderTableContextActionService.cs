using UnityEngine;

public readonly struct BistroBuilderServiceWaitContextActionSnapshot
{
    public readonly BistroBuilderServiceTimingPhase Phase;
    public readonly float WaitSeconds;
    public readonly float ExpectedFoodSeconds;
    public readonly BistroBuilderServiceTimingState TimingState;
    public readonly bool CanExplainDelay;
    public readonly bool IsDelayExplained;
    public readonly bool HasTimingIncident;
    public readonly bool IsTimingIncidentApologized;

    public BistroBuilderServiceWaitContextActionSnapshot(
        BistroBuilderServiceTimingPhase phase,
        float waitSeconds,
        float expectedFoodSeconds,
        BistroBuilderServiceTimingState timingState,
        bool canExplainDelay,
        bool isDelayExplained,
        bool hasTimingIncident,
        bool isTimingIncidentApologized)
    {
        Phase = phase;
        WaitSeconds = Mathf.Max(0f, waitSeconds);
        ExpectedFoodSeconds = Mathf.Max(0f, expectedFoodSeconds);
        TimingState = timingState;
        CanExplainDelay = canExplainDelay;
        IsDelayExplained = isDelayExplained;
        HasTimingIncident = hasTimingIncident;
        IsTimingIncidentApologized = isTimingIncidentApologized;
    }
}

public readonly struct BistroBuilderBillContextActionSnapshot
{
    public readonly bool IsBillRequested;
    public readonly bool HasActiveTask;
    public readonly bool IsTaskPending;
    public readonly bool IsBeingHandled;
    public readonly bool CanAccelerate;
    public readonly bool IsAlreadyAccelerated;
    public readonly bool CanExplainDelay;
    public readonly bool IsDelayExplained;
    public readonly float WaitSeconds;
    public readonly BistroBuilderServiceTimingState TimingState;
    public readonly WaiterTaskPriority TaskPriority;

    public BistroBuilderBillContextActionSnapshot(
        bool isBillRequested,
        bool hasActiveTask,
        bool isTaskPending,
        bool isBeingHandled,
        bool canAccelerate,
        bool isAlreadyAccelerated,
        bool canExplainDelay,
        bool isDelayExplained,
        float waitSeconds,
        BistroBuilderServiceTimingState timingState,
        WaiterTaskPriority taskPriority)
    {
        IsBillRequested = isBillRequested;
        HasActiveTask = hasActiveTask;
        IsTaskPending = isTaskPending;
        IsBeingHandled = isBeingHandled;
        CanAccelerate = canAccelerate;
        IsAlreadyAccelerated = isAlreadyAccelerated;
        CanExplainDelay = canExplainDelay;
        IsDelayExplained = isDelayExplained;
        WaitSeconds = Mathf.Max(0f, waitSeconds);
        TimingState = timingState;
        TaskPriority = taskPriority;
    }
}

public readonly struct BistroBuilderApologyContextActionSnapshot
{
    public readonly bool HasActiveTimingPhase;
    public readonly BistroBuilderServiceTimingPhase TimingPhase;
    public readonly bool HasTimingIncident;
    public readonly bool TimingIncidentAlreadyApologized;
    public readonly bool HasExplicitServiceIncident;
    public readonly int RecoverableServiceIncidentCount;
    public readonly int ApologizedServiceIncidentCount;
    public readonly bool CanApologize;
    public readonly BistroBuilderServiceTimingState TimingState;

    public bool HasBillTimingIncident =>
        HasActiveTimingPhase &&
        TimingPhase == BistroBuilderServiceTimingPhase.BillDelivery &&
        HasTimingIncident;

    public bool BillIncidentAlreadyApologized =>
        HasActiveTimingPhase &&
        TimingPhase == BistroBuilderServiceTimingPhase.BillDelivery &&
        TimingIncidentAlreadyApologized;

    public BistroBuilderServiceTimingState BillTimingState =>
        HasActiveTimingPhase &&
        TimingPhase == BistroBuilderServiceTimingPhase.BillDelivery
            ? TimingState
            : BistroBuilderServiceTimingState.Normal;

    public int PendingExplicitIncidentCount =>
        Mathf.Max(
            0,
            RecoverableServiceIncidentCount -
            ApologizedServiceIncidentCount
        );

    public BistroBuilderApologyContextActionSnapshot(
        bool hasActiveTimingPhase,
        BistroBuilderServiceTimingPhase timingPhase,
        bool hasTimingIncident,
        bool timingIncidentAlreadyApologized,
        bool hasExplicitServiceIncident,
        int recoverableServiceIncidentCount,
        int apologizedServiceIncidentCount,
        bool canApologize,
        BistroBuilderServiceTimingState timingState)
    {
        HasActiveTimingPhase = hasActiveTimingPhase;
        TimingPhase = timingPhase;
        HasTimingIncident = hasTimingIncident;
        TimingIncidentAlreadyApologized =
            timingIncidentAlreadyApologized;
        HasExplicitServiceIncident = hasExplicitServiceIncident;
        RecoverableServiceIncidentCount =
            Mathf.Max(0, recoverableServiceIncidentCount);
        ApologizedServiceIncidentCount =
            Mathf.Max(
                0,
                Mathf.Min(
                    RecoverableServiceIncidentCount,
                    apologizedServiceIncidentCount
                )
            );
        CanApologize = canApologize;
        TimingState = timingState;
    }
}

/// <summary>
/// Fachada de aplicación para acciones contextuales de mesa.
/// Lee timers/visitas/tareas autoritativas y emite comandos a sus autoridades.
/// No mantiene cronómetros ni estado paralelo.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Table Context Action Service")]
public sealed class BistroBuilderTableContextActionService : MonoBehaviour
{
    [SerializeField] private BistroBuilderServiceTimingCatalog timingCatalog;
    [SerializeField] private WaiterTaskCoordinator taskCoordinator;
    [SerializeField]
    private BistroBuilderCustomerExperienceTrackingService
        experienceTrackingService;

    public BistroBuilderServiceTimingCatalog TimingCatalog =>
        timingCatalog;

    private void Awake()
    {
        ResolveDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (timingCatalog == null)
        {
            error = "Falta el catálogo canónico de tiempos de servicio.";
            return false;
        }

        if (!timingCatalog.Validate(out error))
            return false;

        if (taskCoordinator == null)
        {
            error = "Falta WaiterTaskCoordinator.";
            return false;
        }

        if (experienceTrackingService == null)
        {
            error =
                "Falta Customer Experience Tracking para leer las esperas canónicas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetActiveWaitSnapshot(
        RestaurantTable table,
        out BistroBuilderServiceWaitContextActionSnapshot snapshot)
    {
        snapshot = default;
        ResolveDependencies();

        if (table == null || table.AssignedCustomerGroup == null)
            return false;

        CustomerGroup group = table.AssignedCustomerGroup;
        if (!TryResolveActivePhase(
                table,
                group,
                out BistroBuilderServiceTimingPhase phase
            ))
        {
            return false;
        }

        BistroBuilderReputationVisitRuntimeRecord visit =
            ResolveVisit(group);
        if (visit == null || timingCatalog == null)
            return false;

        float waitSeconds = ResolveWaitSeconds(visit, phase);
        float expectedFoodSeconds =
            phase == BistroBuilderServiceTimingPhase.FoodDelivery
                ? Mathf.Max(0f, visit.expectedFoodSeconds)
                : 0f;

        if (!timingCatalog.TryEvaluate(
                phase,
                waitSeconds,
                expectedFoodSeconds,
                out BistroBuilderServiceTimingState timingState
            ))
        {
            return false;
        }

        bool explained =
            GetDelayExplanationMitigation(visit, phase) > 0;
        bool apologized =
            GetIncidentApologyMitigation(visit, phase) > 0;
        bool incident = IsIncidentOrWorse(timingState);
        bool canExplain =
            !explained &&
            BistroBuilderServiceTimingEvaluator.IsDelayOrWorse(
                timingState
            );

        snapshot = new BistroBuilderServiceWaitContextActionSnapshot(
            phase,
            waitSeconds,
            expectedFoodSeconds,
            timingState,
            canExplain,
            explained,
            incident,
            apologized
        );
        return true;
    }

    public bool TryGetBillSnapshot(
        RestaurantTable table,
        out BistroBuilderBillContextActionSnapshot snapshot)
    {
        snapshot = default;
        ResolveDependencies();

        if (table == null || table.AssignedCustomerGroup == null)
            return false;

        bool requested =
            table.CurrentState == TableState.WaitingForBill ||
            table.CurrentState == TableState.Paying;

        BistroBuilderReputationVisitRuntimeRecord visit =
            ResolveVisit(table.AssignedCustomerGroup);
        float waitSeconds = visit != null
            ? Mathf.Max(0f, visit.billWaitSeconds)
            : 0f;

        BistroBuilderServiceTimingState timingState =
            BistroBuilderServiceTimingState.Normal;

        if (timingCatalog != null)
        {
            timingCatalog.TryEvaluate(
                BistroBuilderServiceTimingPhase.BillDelivery,
                waitSeconds,
                out timingState
            );
        }

        WaiterTask task = null;
        bool hasTask =
            taskCoordinator != null &&
            taskCoordinator.TryGetActiveTableTask(
                WaiterTaskType.DeliverBill,
                table,
                out task
            ) &&
            task != null;

        bool pending = hasTask && task.IsPending;
        bool beingHandled =
            hasTask &&
            (task.State == WaiterTaskState.Assigned ||
             task.State == WaiterTaskState.InProgress);

        WaiterTaskPriority priority = hasTask
            ? task.Priority
            : WaiterTaskPriority.High;

        bool alreadyAccelerated =
            hasTask &&
            priority >= WaiterTaskPriority.Urgent;

        bool isWaitingForBill =
            table.CurrentState == TableState.WaitingForBill &&
            table.AssignedCustomerGroup.CurrentState ==
                CustomerGroupState.WaitingForBill;

        bool canAccelerate = CanAccelerateBill(
            isWaitingForBill,
            pending,
            beingHandled,
            priority,
            timingState
        );

        bool explained =
            visit != null &&
            visit.billDelayExplanationMitigationBasisPoints > 0;

        bool canExplainDelay = CanExplainBillDelay(
            isWaitingForBill,
            explained,
            timingState
        );

        snapshot = new BistroBuilderBillContextActionSnapshot(
            isWaitingForBill,
            hasTask,
            pending,
            beingHandled,
            canAccelerate,
            alreadyAccelerated,
            canExplainDelay,
            explained,
            waitSeconds,
            timingState,
            priority
        );

        return requested || hasTask;
    }

    public bool TryAccelerateBill(
        RestaurantTable table,
        out string error)
    {
        error = string.Empty;
        ResolveDependencies();

        if (!TryGetBillSnapshot(
                table,
                out BistroBuilderBillContextActionSnapshot snapshot))
        {
            error = "La mesa no tiene una espera de cuenta activa.";
            return false;
        }

        if (snapshot.IsBeingHandled)
        {
            error = "La cuenta ya está siendo atendida.";
            return false;
        }

        if (snapshot.IsAlreadyAccelerated)
        {
            error = "La cuenta ya está agilizada.";
            return false;
        }

        if (!snapshot.CanAccelerate)
        {
            error =
                snapshot.TimingState ==
                BistroBuilderServiceTimingState.Normal
                    ? "La cuenta todavía está dentro del tiempo normal de servicio."
                    : "Agilizar cuenta no está disponible en este momento.";
            return false;
        }

        if (taskCoordinator == null ||
            !taskCoordinator.TryChangePendingTableTaskPriority(
                WaiterTaskType.DeliverBill,
                table,
                WaiterTaskPriority.Urgent,
                out _))
        {
            error = "No se pudo elevar la prioridad de la cuenta.";
            return false;
        }

        return true;
    }

    public bool TryExplainDelay(
        RestaurantTable table,
        out string error)
    {
        error = string.Empty;
        ResolveDependencies();

        if (!TryGetActiveWaitSnapshot(
                table,
                out BistroBuilderServiceWaitContextActionSnapshot snapshot))
        {
            error = "La mesa no tiene una espera contextual activa.";
            return false;
        }

        if (snapshot.IsDelayExplained)
        {
            error = "La demora de esta necesidad ya fue explicada.";
            return false;
        }

        if (!snapshot.CanExplainDelay)
        {
            error = "Explicar demora no está disponible en este momento.";
            return false;
        }

        if (timingCatalog == null ||
            !timingCatalog.TryGetRecoveryTuning(
                snapshot.Phase,
                out int explanationMitigationBasisPoints,
                out _
            ) ||
            explanationMitigationBasisPoints <= 0)
        {
            error = "Falta el ajuste de impacto de Explicar demora.";
            return false;
        }

        if (experienceTrackingService == null)
        {
            error = "Customer Experience Tracking no está disponible.";
            return false;
        }

        CustomerGroup group =
            table != null ? table.AssignedCustomerGroup : null;

        return experienceTrackingService.TryExplainServiceDelay(
            group,
            snapshot.Phase,
            explanationMitigationBasisPoints,
            out error
        );
    }

    public bool TryExplainBillDelay(
        RestaurantTable table,
        out string error)
    {
        if (!TryGetActiveWaitSnapshot(
                table,
                out BistroBuilderServiceWaitContextActionSnapshot snapshot
            ) ||
            snapshot.Phase !=
                BistroBuilderServiceTimingPhase.BillDelivery)
        {
            error = "La mesa no tiene una espera de cuenta activa.";
            return false;
        }

        return TryExplainDelay(table, out error);
    }

    public bool TryGetApologySnapshot(
        RestaurantTable table,
        out BistroBuilderApologyContextActionSnapshot snapshot)
    {
        snapshot = default;
        ResolveDependencies();

        if (table == null || table.AssignedCustomerGroup == null)
            return false;

        CustomerGroup group = table.AssignedCustomerGroup;
        BistroBuilderReputationVisitRuntimeRecord visit =
            ResolveVisit(group);
        if (visit == null)
            return false;

        bool hasActiveTimingPhase = TryGetActiveWaitSnapshot(
            table,
            out BistroBuilderServiceWaitContextActionSnapshot wait
        );

        BistroBuilderServiceTimingPhase phase =
            hasActiveTimingPhase
                ? wait.Phase
                : BistroBuilderServiceTimingPhase.BillDelivery;
        BistroBuilderServiceTimingState state =
            hasActiveTimingPhase
                ? wait.TimingState
                : BistroBuilderServiceTimingState.Normal;
        bool timingIncident =
            hasActiveTimingPhase && wait.HasTimingIncident;
        bool timingApologized =
            hasActiveTimingPhase &&
            wait.IsTimingIncidentApologized;

        int pendingExplicit = Mathf.Max(
            0,
            visit.recoverableServiceIncidentCount -
            visit.apologizedServiceIncidentCount
        );
        bool explicitIncident = pendingExplicit > 0;
        bool canApologize = CanApologizeTimingIncident(
            timingIncident,
            timingApologized,
            pendingExplicit
        );

        snapshot = new BistroBuilderApologyContextActionSnapshot(
            hasActiveTimingPhase,
            phase,
            timingIncident,
            timingApologized,
            explicitIncident,
            visit.recoverableServiceIncidentCount,
            visit.apologizedServiceIncidentCount,
            canApologize,
            state
        );
        return true;
    }

    public bool TryApologize(
        RestaurantTable table,
        out string error)
    {
        error = string.Empty;
        ResolveDependencies();

        if (!TryGetApologySnapshot(
                table,
                out BistroBuilderApologyContextActionSnapshot snapshot
            ) ||
            !snapshot.CanApologize)
        {
            error = "Disculpa no está disponible en este momento.";
            return false;
        }

        bool applyTimingIncidentRecovery =
            snapshot.HasActiveTimingPhase &&
            snapshot.HasTimingIncident &&
            !snapshot.TimingIncidentAlreadyApologized;

        int timingMitigationBasisPoints = 0;
        if (applyTimingIncidentRecovery)
        {
            if (timingCatalog == null ||
                !timingCatalog.TryGetRecoveryTuning(
                    snapshot.TimingPhase,
                    out _,
                    out timingMitigationBasisPoints
                ) ||
                timingMitigationBasisPoints <= 0)
            {
                error = "Falta el ajuste de recuperación de Disculpa.";
                return false;
            }
        }

        if (experienceTrackingService == null)
        {
            error = "Customer Experience Tracking no está disponible.";
            return false;
        }

        CustomerGroup group =
            table != null ? table.AssignedCustomerGroup : null;

        return experienceTrackingService.TryApologizeForServiceIncident(
            group,
            applyTimingIncidentRecovery,
            snapshot.TimingPhase,
            timingMitigationBasisPoints,
            snapshot.PendingExplicitIncidentCount > 0,
            out error
        );
    }

    public static bool CanApologize(
        bool hasBillTimingIncident,
        bool billIncidentAlreadyApologized,
        int pendingExplicitIncidentCount)
    {
        return CanApologizeTimingIncident(
            hasBillTimingIncident,
            billIncidentAlreadyApologized,
            pendingExplicitIncidentCount
        );
    }

    public static bool CanApologizeTimingIncident(
        bool hasTimingIncident,
        bool timingIncidentAlreadyApologized,
        int pendingExplicitIncidentCount)
    {
        return (hasTimingIncident &&
                !timingIncidentAlreadyApologized) ||
               pendingExplicitIncidentCount > 0;
    }

    public static bool CanExplainBillDelay(
        bool isWaitingForBill,
        bool isAlreadyExplained,
        BistroBuilderServiceTimingState timingState)
    {
        return isWaitingForBill &&
               !isAlreadyExplained &&
               BistroBuilderServiceTimingEvaluator.IsDelayOrWorse(
                   timingState
               );
    }

    public static bool CanAccelerateBill(
        bool isWaitingForBill,
        bool isTaskPending,
        bool isBeingHandled,
        WaiterTaskPriority priority,
        BistroBuilderServiceTimingState timingState)
    {
        if (!isWaitingForBill ||
            !isTaskPending ||
            isBeingHandled ||
            priority >= WaiterTaskPriority.Urgent)
        {
            return false;
        }

        return BistroBuilderServiceTimingEvaluator.IsActionableWait(
            timingState
        );
    }

    private static bool TryResolveActivePhase(
        RestaurantTable table,
        CustomerGroup group,
        out BistroBuilderServiceTimingPhase phase)
    {
        phase = BistroBuilderServiceTimingPhase.BillDelivery;
        if (table == null || group == null)
            return false;

        if (table.CurrentState == TableState.WaitingForWaiter &&
            group.CurrentState == CustomerGroupState.WaitingForWaiter)
        {
            phase = BistroBuilderServiceTimingPhase.TakeOrder;
            return true;
        }

        if (table.CurrentState == TableState.WaitingForFood &&
            group.CurrentState == CustomerGroupState.WaitingForFood)
        {
            phase = BistroBuilderServiceTimingPhase.FoodDelivery;
            return true;
        }

        if (table.CurrentState == TableState.WaitingForBill &&
            group.CurrentState == CustomerGroupState.WaitingForBill)
        {
            phase = BistroBuilderServiceTimingPhase.BillDelivery;
            return true;
        }

        return false;
    }

    private static float ResolveWaitSeconds(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderServiceTimingPhase phase)
    {
        if (visit == null)
            return 0f;

        switch (phase)
        {
            case BistroBuilderServiceTimingPhase.TakeOrder:
                return Mathf.Max(0f, visit.waiterWaitSeconds);
            case BistroBuilderServiceTimingPhase.FoodDelivery:
                return Mathf.Max(0f, visit.foodWaitSeconds);
            case BistroBuilderServiceTimingPhase.BillDelivery:
                return Mathf.Max(0f, visit.billWaitSeconds);
            default:
                return 0f;
        }
    }

    private static int GetDelayExplanationMitigation(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderServiceTimingPhase phase)
    {
        if (visit == null)
            return 0;

        switch (phase)
        {
            case BistroBuilderServiceTimingPhase.TakeOrder:
                return visit
                    .waiterDelayExplanationMitigationBasisPoints;
            case BistroBuilderServiceTimingPhase.FoodDelivery:
                return visit
                    .foodDelayExplanationMitigationBasisPoints;
            case BistroBuilderServiceTimingPhase.BillDelivery:
                return visit
                    .billDelayExplanationMitigationBasisPoints;
            default:
                return 0;
        }
    }

    private static int GetIncidentApologyMitigation(
        BistroBuilderReputationVisitRuntimeRecord visit,
        BistroBuilderServiceTimingPhase phase)
    {
        if (visit == null)
            return 0;

        switch (phase)
        {
            case BistroBuilderServiceTimingPhase.TakeOrder:
                return visit
                    .waiterIncidentApologyMitigationBasisPoints;
            case BistroBuilderServiceTimingPhase.FoodDelivery:
                return visit
                    .foodIncidentApologyMitigationBasisPoints;
            case BistroBuilderServiceTimingPhase.BillDelivery:
                return visit
                    .billIncidentApologyMitigationBasisPoints;
            default:
                return 0;
        }
    }

    private static bool IsIncidentOrWorse(
        BistroBuilderServiceTimingState state)
    {
        return state == BistroBuilderServiceTimingState.Incident ||
               state == BistroBuilderServiceTimingState.Critical;
    }

    private BistroBuilderReputationVisitRuntimeRecord ResolveVisit(
        CustomerGroup group)
    {
        if (group == null ||
            experienceTrackingService == null ||
            !experienceTrackingService.TryGetRuntimeVisit(
                group.GroupId,
                out BistroBuilderReputationVisitRuntimeRecord visit
            ))
        {
            return null;
        }

        return visit;
    }

    private void ResolveDependencies()
    {
        if (timingCatalog == null)
        {
            timingCatalog =
                Resources.Load<BistroBuilderServiceTimingCatalog>(
                    BistroBuilderServiceTimingCatalog.ResourcesPath
                );
        }

        if (taskCoordinator == null)
        {
            taskCoordinator =
                FindFirstObjectByType<WaiterTaskCoordinator>(
                    FindObjectsInactive.Include
                );
        }

        if (experienceTrackingService == null)
        {
            experienceTrackingService =
                FindFirstObjectByType<
                    BistroBuilderCustomerExperienceTrackingService>(
                    FindObjectsInactive.Include
                );
        }
    }
}
