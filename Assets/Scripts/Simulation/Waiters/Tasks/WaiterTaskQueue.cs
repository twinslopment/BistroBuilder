using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Mantiene el conjunto de tareas activas de los camareros.
///
/// Sus responsabilidades son:
/// - Crear tareas evitando duplicados.
/// - Mantener el orden por prioridad y antigüedad.
/// - Asignar, iniciar, completar y cancelar tareas.
/// - Retirar inmediatamente las tareas terminadas.
/// - Cancelar tareas cuando una mesa o comanda deja de existir.
///
/// Esta clase no hereda de MonoBehaviour. Es una clase lógica
/// reutilizable y preparada para pruebas automatizadas.
/// </summary>
public sealed class WaiterTaskQueue
{
    private readonly struct TaskKey : IEquatable<TaskKey>
    {
        private readonly WaiterTaskType type;
        private readonly object target;
        private readonly string orderLineId;

        public TaskKey(
            WaiterTaskType type,
            RestaurantTable table,
            RestaurantOrder order,
            string orderLineId)
        {
            this.type = type;
            target = type == WaiterTaskType.DeliverFood ? order : table;
            this.orderLineId = type == WaiterTaskType.DeliverFood
                ? BistroBuilderOrderIdUtility.Normalize(orderLineId)
                : string.Empty;
        }

        public bool Equals(TaskKey other)
        {
            return type == other.type &&
                   ReferenceEquals(target, other.target) &&
                   string.Equals(
                       orderLineId,
                       other.orderLineId,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TaskKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int targetHash = target != null
                    ? RuntimeHelpers.GetHashCode(target)
                    : 0;
                int lineHash = orderLineId != null
                    ? StringComparer.Ordinal.GetHashCode(orderLineId)
                    : 0;
                return (((int)type * 397) ^ targetHash) * 397 ^ lineHash;
            }
        }
    }

    private readonly List<WaiterTask> activeTasks;
    private readonly ReadOnlyCollection<WaiterTask> activeTasksView;
    private readonly Dictionary<TaskKey, WaiterTask> activeTasksByKey;

    private int nextTaskId = 1;
    private long nextCreationSequence;

    /// <summary>
    /// Se ejecuta cuando una nueva tarea entra en la cola.
    /// </summary>
    public event Action<WaiterTask> TaskCreated;

    /// <summary>
    /// Se ejecuta cuando una tarea ya asignada pasa realmente a InProgress.
    /// Es una señal puramente observacional añadida para integraciones como
    /// Personal; no cambia la autoridad ni el ciclo de vida de la tarea.
    /// </summary>
    public event Action<WaiterTask> TaskStarted;

    /// <summary>
    /// Se ejecuta después de completar y retirar una tarea.
    /// </summary>
    public event Action<WaiterTask> TaskCompleted;

    /// <summary>
    /// Se ejecuta después de cancelar y retirar una tarea.
    /// </summary>
    public event Action<WaiterTask> TaskCancelled;

    public int Count => activeTasks.Count;
    public IReadOnlyList<WaiterTask> ActiveTasks => activeTasksView;

    public WaiterTaskQueue()
    {
        activeTasks = new List<WaiterTask>();
        activeTasksView = activeTasks.AsReadOnly();
        activeTasksByKey = new Dictionary<TaskKey, WaiterTask>();
    }

    public bool TryCreateTask(
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        out WaiterTask createdTask)
    {
        return TryCreateTask(
            type,
            priority,
            table,
            order,
            string.Empty,
            out createdTask);
    }

    public bool TryCreateTask(
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        string orderLineId,
        out WaiterTask createdTask)
    {
        createdTask = null;

        if (type != WaiterTaskType.DeliverFood && table == null)
        {
            return false;
        }

        if (type == WaiterTaskType.DeliverFood &&
            (order == null ||
             (!order.HasTableDestination && !order.HasBarDestination)))
        {
            return false;
        }

        string normalizedLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);
        if (!string.IsNullOrEmpty(normalizedLineId) &&
            !BistroBuilderOrderIdUtility.IsValid(normalizedLineId))
        {
            return false;
        }

        TaskKey key = new TaskKey(
            type,
            table,
            order,
            normalizedLineId);
        if (activeTasksByKey.TryGetValue(key, out WaiterTask existingTask))
        {
            createdTask = existingTask;
            return false;
        }

        EnsureTaskIdIsAvailable();
        EnsureCreationSequenceIsAvailable();

        createdTask = type == WaiterTaskType.DeliverFood && table == null
            ? new WaiterTask(
                nextTaskId,
                type,
                priority,
                order.BarSpot,
                order,
                normalizedLineId,
                nextCreationSequence)
            : new WaiterTask(
                nextTaskId,
                type,
                priority,
                table,
                order,
                normalizedLineId,
                nextCreationSequence);

        nextTaskId++;
        nextCreationSequence++;
        activeTasks.Add(createdTask);
        activeTasksByKey.Add(key, createdTask);
        TaskCreated?.Invoke(createdTask);
        return true;
    }

    public bool TryGetActiveTask(
        WaiterTaskType type,
        RestaurantTable table,
        RestaurantOrder order,
        out WaiterTask task)
    {
        return TryGetActiveTask(
            type,
            table,
            order,
            string.Empty,
            out task);
    }

    public bool TryGetActiveTask(
        WaiterTaskType type,
        RestaurantTable table,
        RestaurantOrder order,
        string orderLineId,
        out WaiterTask task)
    {
        task = null;
        if (type != WaiterTaskType.DeliverFood && table == null)
        {
            return false;
        }
        if (type == WaiterTaskType.DeliverFood && order == null)
        {
            return false;
        }

        TaskKey key = new TaskKey(type, table, order, orderLineId);
        return activeTasksByKey.TryGetValue(key, out task);
    }

    public WaiterTask GetNextPendingTask()
    {
        WaiterTask selectedTask = null;
        foreach (WaiterTask task in activeTasks)
        {
            if (task == null || !task.IsPending)
            {
                continue;
            }

            if (selectedTask == null)
            {
                selectedTask = task;
                continue;
            }

            bool hasHigherPriority = task.Priority > selectedTask.Priority;
            bool hasSamePriorityButIsOlder =
                task.Priority == selectedTask.Priority &&
                task.CreationSequence < selectedTask.CreationSequence;
            if (hasHigherPriority || hasSamePriorityButIsOlder)
            {
                selectedTask = task;
            }
        }
        return selectedTask;
    }

    public bool TryAssignTask(WaiterTask task, Waiter waiter)
    {
        if (!IsActiveTask(task))
        {
            return false;
        }
        return task.TryAssignWaiter(waiter);
    }

    public bool TryReleaseTaskAssignment(WaiterTask task)
    {
        if (!IsActiveTask(task))
        {
            return false;
        }
        return task.TryReleaseAssignment();
    }

    public bool TryStartTask(WaiterTask task)
    {
        if (!IsActiveTask(task) || !task.TryStart())
        {
            return false;
        }

        TaskStarted?.Invoke(task);
        return true;
    }

    public bool TryChangePriority(
        WaiterTask task,
        WaiterTaskPriority newPriority)
    {
        if (!IsActiveTask(task))
        {
            return false;
        }
        return task.TryChangePriority(newPriority);
    }

    public bool TryCompleteTask(WaiterTask task)
    {
        if (!IsActiveTask(task) || !task.TryComplete())
        {
            return false;
        }

        RemoveActiveTask(task);
        TaskCompleted?.Invoke(task);
        return true;
    }

    public bool TryCancelTask(WaiterTask task)
    {
        if (!IsActiveTask(task) || !task.TryCancel())
        {
            return false;
        }

        RemoveActiveTask(task);
        TaskCancelled?.Invoke(task);
        return true;
    }

    public int CancelTasksForTable(RestaurantTable table)
    {
        if (table == null)
        {
            return 0;
        }

        int cancelledCount = 0;
        for (int index = activeTasks.Count - 1; index >= 0; index--)
        {
            WaiterTask task = activeTasks[index];
            if (task == null || !ReferenceEquals(task.Table, table))
            {
                continue;
            }
            if (TryCancelTask(task))
            {
                cancelledCount++;
            }
        }
        return cancelledCount;
    }

    public int CancelTasksForOrder(RestaurantOrder order)
    {
        if (order == null)
        {
            return 0;
        }

        int cancelledCount = 0;
        for (int index = activeTasks.Count - 1; index >= 0; index--)
        {
            WaiterTask task = activeTasks[index];
            if (task == null || !ReferenceEquals(task.Order, order))
            {
                continue;
            }
            if (TryCancelTask(task))
            {
                cancelledCount++;
            }
        }
        return cancelledCount;
    }

    public void Clear()
    {
        for (int index = activeTasks.Count - 1; index >= 0; index--)
        {
            WaiterTask task = activeTasks[index];
            if (task == null)
            {
                continue;
            }
            if (task.TryCancel())
            {
                TaskCancelled?.Invoke(task);
            }
        }

        activeTasks.Clear();
        activeTasksByKey.Clear();
    }

    private bool IsActiveTask(WaiterTask task)
    {
        if (task == null)
        {
            return false;
        }

        TaskKey key = new TaskKey(
            task.Type,
            task.Table,
            task.Order,
            task.OrderLineId);
        return activeTasksByKey.TryGetValue(
                   key,
                   out WaiterTask registeredTask) &&
               ReferenceEquals(registeredTask, task);
    }

    private void RemoveActiveTask(WaiterTask task)
    {
        TaskKey key = new TaskKey(
            task.Type,
            task.Table,
            task.Order,
            task.OrderLineId);
        activeTasksByKey.Remove(key);
        activeTasks.Remove(task);
    }

    private void EnsureTaskIdIsAvailable()
    {
        if (nextTaskId == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Se ha alcanzado el límite de identificadores de tareas de camarero.");
        }
    }

    private void EnsureCreationSequenceIsAvailable()
    {
        if (nextCreationSequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Se ha alcanzado el límite de secuencias de tareas de camarero.");
        }
    }
}
