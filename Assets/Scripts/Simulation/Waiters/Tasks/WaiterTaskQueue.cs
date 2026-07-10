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
    /// <summary>
    /// Clave interna utilizada para detectar tareas duplicadas.
    ///
    /// Las tareas DeliverFood se identifican mediante la comanda.
    /// El resto se identifican mediante la mesa.
    /// </summary>
    private readonly struct TaskKey :
        IEquatable<TaskKey>
    {
        private readonly WaiterTaskType type;
        private readonly object target;

        public TaskKey(
            WaiterTaskType type,
            RestaurantTable table,
            RestaurantOrder order
        )
        {
            this.type = type;

            target = type == WaiterTaskType.DeliverFood
                ? order
                : table;
        }

        public bool Equals(TaskKey other)
        {
            return type == other.type &&
                   ReferenceEquals(
                       target,
                       other.target
                   );
        }

        public override bool Equals(object obj)
        {
            return obj is TaskKey other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int targetHash = target != null
                    ? RuntimeHelpers.GetHashCode(target)
                    : 0;

                return ((int)type * 397) ^
                       targetHash;
            }
        }
    }

    /// <summary>
    /// Lista interna de tareas activas.
    ///
    /// Solo contiene tareas pendientes, asignadas o en ejecución.
    /// Las tareas completadas y canceladas se eliminan.
    /// </summary>
    private readonly List<WaiterTask> activeTasks;

    /// <summary>
    /// Vista de solo lectura de la lista de tareas.
    ///
    /// Se crea una sola vez para evitar asignaciones de memoria
    /// cada vez que otro sistema consulta las tareas activas.
    /// </summary>
    private readonly ReadOnlyCollection<WaiterTask>
        activeTasksView;

    /// <summary>
    /// Índice de búsqueda rápida para impedir duplicados.
    /// </summary>
    private readonly Dictionary<TaskKey, WaiterTask>
        activeTasksByKey;

    private int nextTaskId = 1;
    private long nextCreationSequence;

    /// <summary>
    /// Se ejecuta cuando una nueva tarea entra en la cola.
    /// </summary>
    public event Action<WaiterTask> TaskCreated;

    /// <summary>
    /// Se ejecuta después de completar y retirar una tarea.
    /// </summary>
    public event Action<WaiterTask> TaskCompleted;

    /// <summary>
    /// Se ejecuta después de cancelar y retirar una tarea.
    /// </summary>
    public event Action<WaiterTask> TaskCancelled;

    /// <summary>
    /// Número actual de tareas activas.
    /// </summary>
    public int Count => activeTasks.Count;

    /// <summary>
    /// Vista de solo lectura de las tareas activas.
    /// </summary>
    public IReadOnlyList<WaiterTask> ActiveTasks =>
        activeTasksView;

    /// <summary>
    /// Inicializa la cola y sus estructuras internas.
    /// </summary>
    public WaiterTaskQueue()
    {
        activeTasks = new List<WaiterTask>();
        activeTasksView = activeTasks.AsReadOnly();

        activeTasksByKey =
            new Dictionary<TaskKey, WaiterTask>();
    }

    /// <summary>
    /// Crea una tarea si no existe otra activa con el mismo tipo
    /// y el mismo objetivo.
    ///
    /// Cuando ya existe una tarea equivalente, devuelve false
    /// y entrega la tarea existente mediante createdTask.
    /// </summary>
    public bool TryCreateTask(
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        out WaiterTask createdTask
    )
    {
        createdTask = null;

        if (table == null)
            return false;

        if (type == WaiterTaskType.DeliverFood &&
            order == null)
        {
            return false;
        }

        TaskKey key = new TaskKey(
            type,
            table,
            order
        );

        if (activeTasksByKey.TryGetValue(
                key,
                out WaiterTask existingTask
            ))
        {
            createdTask = existingTask;

            return false;
        }

        EnsureTaskIdIsAvailable();
        EnsureCreationSequenceIsAvailable();

        createdTask = new WaiterTask(
            nextTaskId,
            type,
            priority,
            table,
            order,
            nextCreationSequence
        );

        nextTaskId++;
        nextCreationSequence++;

        activeTasks.Add(createdTask);
        activeTasksByKey.Add(
            key,
            createdTask
        );

        TaskCreated?.Invoke(createdTask);

        return true;
    }

    /// <summary>
    /// Busca una tarea activa por tipo y objetivo.
    /// </summary>
    public bool TryGetActiveTask(
        WaiterTaskType type,
        RestaurantTable table,
        RestaurantOrder order,
        out WaiterTask task
    )
    {
        task = null;

        if (table == null)
            return false;

        if (type == WaiterTaskType.DeliverFood &&
            order == null)
        {
            return false;
        }

        TaskKey key = new TaskKey(
            type,
            table,
            order
        );

        return activeTasksByKey.TryGetValue(
            key,
            out task
        );
    }

    /// <summary>
    /// Devuelve la siguiente tarea pendiente aplicando:
    ///
    /// 1. Mayor prioridad.
    /// 2. En igualdad de prioridad, mayor antigüedad.
    ///
    /// Devuelve null cuando no existen tareas pendientes.
    /// </summary>
    public WaiterTask GetNextPendingTask()
    {
        WaiterTask selectedTask = null;

        foreach (WaiterTask task in activeTasks)
        {
            if (task == null ||
                !task.IsPending)
            {
                continue;
            }

            if (selectedTask == null)
            {
                selectedTask = task;
                continue;
            }

            bool hasHigherPriority =
                task.Priority >
                selectedTask.Priority;

            bool hasSamePriorityButIsOlder =
                task.Priority ==
                selectedTask.Priority &&
                task.CreationSequence <
                selectedTask.CreationSequence;

            if (hasHigherPriority ||
                hasSamePriorityButIsOlder)
            {
                selectedTask = task;
            }
        }

        return selectedTask;
    }

    /// <summary>
    /// Reserva una tarea pendiente para un camarero.
    /// </summary>
    public bool TryAssignTask(
        WaiterTask task,
        Waiter waiter
    )
    {
        if (!IsActiveTask(task))
            return false;

        return task.TryAssignWaiter(waiter);
    }

    /// <summary>
    /// Revierte la reserva de una tarea cuando el camarero
    /// no ha podido aceptar realmente el trabajo.
    ///
    /// La tarea vuelve a Pending y podrá asignarse de nuevo.
    /// </summary>
    public bool TryReleaseTaskAssignment(
        WaiterTask task
    )
    {
        if (!IsActiveTask(task))
            return false;

        return task.TryReleaseAssignment();
    }

    /// <summary>
    /// Marca una tarea asignada como iniciada.
    /// </summary>
    public bool TryStartTask(
        WaiterTask task
    )
    {
        if (!IsActiveTask(task))
            return false;

        return task.TryStart();
    }

    /// <summary>
    /// Cambia la prioridad de una tarea que continúa pendiente.
    /// </summary>
    public bool TryChangePriority(
        WaiterTask task,
        WaiterTaskPriority newPriority
    )
    {
        if (!IsActiveTask(task))
            return false;

        return task.TryChangePriority(
            newPriority
        );
    }

    /// <summary>
    /// Completa una tarea y la elimina de las estructuras activas.
    /// </summary>
    public bool TryCompleteTask(
        WaiterTask task
    )
    {
        if (!IsActiveTask(task))
            return false;

        if (!task.TryComplete())
            return false;

        RemoveActiveTask(task);

        TaskCompleted?.Invoke(task);

        return true;
    }

    /// <summary>
    /// Cancela una tarea y la elimina de las estructuras activas.
    /// </summary>
    public bool TryCancelTask(
        WaiterTask task
    )
    {
        if (!IsActiveTask(task))
            return false;

        if (!task.TryCancel())
            return false;

        RemoveActiveTask(task);

        TaskCancelled?.Invoke(task);

        return true;
    }

    /// <summary>
    /// Cancela todas las tareas relacionadas con una mesa.
    ///
    /// Se utilizará cuando una mesa sea eliminada, desactivada
    /// o retirada del servicio.
    /// </summary>
    public int CancelTasksForTable(
        RestaurantTable table
    )
    {
        if (table == null)
            return 0;

        int cancelledCount = 0;

        // Se recorre hacia atrás porque las tareas se eliminan
        // de la lista durante el proceso.
        for (int index = activeTasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                activeTasks[index];

            if (task == null ||
                !ReferenceEquals(
                    task.Table,
                    table
                ))
            {
                continue;
            }

            if (TryCancelTask(task))
                cancelledCount++;
        }

        return cancelledCount;
    }

    /// <summary>
    /// Cancela todas las tareas relacionadas con una comanda.
    /// </summary>
    public int CancelTasksForOrder(
        RestaurantOrder order
    )
    {
        if (order == null)
            return 0;

        int cancelledCount = 0;

        for (int index = activeTasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                activeTasks[index];

            if (task == null ||
                !ReferenceEquals(
                    task.Order,
                    order
                ))
            {
                continue;
            }

            if (TryCancelTask(task))
                cancelledCount++;
        }

        return cancelledCount;
    }

    /// <summary>
    /// Cancela y retira todas las tareas activas.
    ///
    /// Será utilizado al finalizar un servicio o reiniciar
    /// la simulación.
    /// </summary>
    public void Clear()
    {
        for (int index = activeTasks.Count - 1;
             index >= 0;
             index--)
        {
            WaiterTask task =
                activeTasks[index];

            if (task == null)
                continue;

            if (task.TryCancel())
            {
                TaskCancelled?.Invoke(task);
            }
        }

        activeTasks.Clear();
        activeTasksByKey.Clear();
    }

    /// <summary>
    /// Comprueba que una tarea pertenece realmente a esta cola
    /// y continúa registrada como activa.
    /// </summary>
    private bool IsActiveTask(
        WaiterTask task
    )
    {
        if (task == null)
            return false;

        TaskKey key = new TaskKey(
            task.Type,
            task.Table,
            task.Order
        );

        return activeTasksByKey.TryGetValue(
                   key,
                   out WaiterTask registeredTask
               ) &&
               ReferenceEquals(
                   registeredTask,
                   task
               );
    }

    /// <summary>
    /// Retira una tarea de la lista y del índice de búsqueda.
    /// </summary>
    private void RemoveActiveTask(
        WaiterTask task
    )
    {
        TaskKey key = new TaskKey(
            task.Type,
            task.Table,
            task.Order
        );

        activeTasksByKey.Remove(key);
        activeTasks.Remove(task);
    }

    /// <summary>
    /// Evita reutilizar identificadores al alcanzar el límite
    /// de un número entero.
    /// </summary>
    private void EnsureTaskIdIsAvailable()
    {
        if (nextTaskId == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Se ha alcanzado el límite de identificadores " +
                "de tareas de camarero."
            );
        }
    }

    /// <summary>
    /// Evita que la secuencia de creación se desborde.
    /// </summary>
    private void EnsureCreationSequenceIsAvailable()
    {
        if (nextCreationSequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Se ha alcanzado el límite de secuencias " +
                "de tareas de camarero."
            );
        }
    }
}