using UnityEngine;

public readonly struct BistroBuilderBillContextActionSnapshot
{
    public readonly bool IsBillRequested;
    public readonly bool HasActiveTask;
    public readonly bool IsTaskPending;
    public readonly bool IsBeingHandled;
    public readonly bool CanAccelerate;
    public readonly bool IsAlreadyAccelerated;
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

        float waitSeconds =
            ResolveBillWaitSeconds(table.AssignedCustomerGroup);

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

        bool canAccelerate = CanAccelerateBill(
            table.CurrentState == TableState.WaitingForBill,
            pending,
            beingHandled,
            priority,
            timingState
        );

        snapshot = new BistroBuilderBillContextActionSnapshot(
            table.CurrentState == TableState.WaitingForBill,
            hasTask,
            pending,
            beingHandled,
            canAccelerate,
            alreadyAccelerated,
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

    private float ResolveBillWaitSeconds(CustomerGroup group)
    {
        if (group == null ||
            experienceTrackingService == null ||
            !experienceTrackingService.TryGetRuntimeVisit(
                group.GroupId,
                out BistroBuilderReputationVisitRuntimeRecord visit
            ) ||
            visit == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, visit.billWaitSeconds);
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
