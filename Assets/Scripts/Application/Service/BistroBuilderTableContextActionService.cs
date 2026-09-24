using UnityEngine;

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

/// <summary>
/// Fachada de aplicación para las acciones contextuales de una mesa.
/// No posee tareas ni cronómetros: lee las autoridades existentes y emite
/// comandos al WaiterTaskCoordinator.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Table Context Action Service")]
public sealed class BistroBuilderTableContextActionService : MonoBehaviour
{
    [SerializeField] private BistroBuilderServiceTimingCatalog timingCatalog;
    [SerializeField] private WaiterTaskCoordinator taskCoordinator;
    [SerializeField]
    private BistroBuilderCustomerExperienceTrackingService experienceTrackingService;

    public BistroBuilderServiceTimingCatalog TimingCatalog => timingCatalog;

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

    public bool TryGetBillSnapshot(
        RestaurantTable table,
        out BistroBuilderBillContextActionSnapshot snapshot)
    {
        snapshot = default;
        ResolveDependencies();

        if (table == null || table.AssignedCustomerGroup == null)
            return false;

        bool requested = table.CurrentState == TableState.WaitingForBill ||
                         table.CurrentState == TableState.Paying;

        BistroBuilderReputationVisitRuntimeRecord visit =
            ResolveBillVisit(table.AssignedCustomerGroup);
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
        bool hasTask = taskCoordinator != null &&
            taskCoordinator.TryGetActiveTableTask(
                WaiterTaskType.DeliverBill,
                table,
                out task
            ) &&
            task != null;

        bool pending = hasTask && task.IsPending;
        bool beingHandled = hasTask &&
            (task.State == WaiterTaskState.Assigned ||
             task.State == WaiterTaskState.InProgress);

        WaiterTaskPriority priority = hasTask
            ? task.Priority
            : WaiterTaskPriority.High;

        bool alreadyAccelerated =
            hasTask && priority >= WaiterTaskPriority.Urgent;

        bool isWaitingForBill = table.CurrentState == TableState.WaitingForBill;
        bool canAccelerate = CanAccelerateBill(
            isWaitingForBill,
            pending,
            beingHandled,
            priority,
            timingState
        );
        bool explained = visit != null &&
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
            error = snapshot.TimingState == BistroBuilderServiceTimingState.Normal
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

    public bool TryExplainBillDelay(
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

        if (snapshot.IsDelayExplained)
        {
            error = "La demora de esta cuenta ya fue explicada.";
            return false;
        }

        if (!snapshot.CanExplainDelay)
        {
            error = "Explicar demora no está disponible en este momento.";
            return false;
        }

        if (timingCatalog == null ||
            !timingCatalog.TryGetProfile(
                BistroBuilderServiceTimingPhase.BillDelivery,
                out BistroBuilderServiceTimingProfile profile) ||
            profile == null ||
            profile.ExplanationPenaltyMitigationBasisPoints <= 0)
        {
            error = "Falta el ajuste de impacto de Explicar demora.";
            return false;
        }

        if (experienceTrackingService == null)
        {
            error = "Customer Experience Tracking no está disponible.";
            return false;
        }

        CustomerGroup group = table != null ? table.AssignedCustomerGroup : null;
        return experienceTrackingService.TryExplainBillDelay(
            group,
            profile.ExplanationPenaltyMitigationBasisPoints,
            out error
        );
    }

    public static bool CanExplainBillDelay(
        bool isWaitingForBill,
        bool isAlreadyExplained,
        BistroBuilderServiceTimingState timingState)
    {
        return isWaitingForBill &&
               !isAlreadyExplained &&
               BistroBuilderServiceTimingEvaluator.IsDelayOrWorse(timingState);
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

        return BistroBuilderServiceTimingEvaluator.IsActionableWait(timingState);
    }

    private BistroBuilderReputationVisitRuntimeRecord ResolveBillVisit(
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
                FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>(
                    FindObjectsInactive.Include
                );
        }
    }
}
