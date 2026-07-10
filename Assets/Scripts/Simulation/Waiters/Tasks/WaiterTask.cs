using System;

/// <summary>
/// Representa una tarea individual que debe realizar un camarero.
///
/// Esta clase almacena la información y el estado de la tarea,
/// pero no ejecuta directamente movimientos ni modifica mesas.
///
/// El futuro coordinador utilizará esta clase para distribuir
/// trabajo entre cualquier número de camareros.
/// </summary>
public sealed class WaiterTask
{
    /// <summary>
    /// Identificador único de la tarea.
    /// </summary>
    public int TaskId { get; }

    /// <summary>
    /// Tipo de trabajo que debe realizarse.
    /// </summary>
    public WaiterTaskType Type { get; }

    /// <summary>
    /// Prioridad actual de la tarea.
    /// </summary>
    public WaiterTaskPriority Priority { get; private set; }

    /// <summary>
    /// Estado actual de la tarea.
    /// </summary>
    public WaiterTaskState State { get; private set; }

    /// <summary>
    /// Mesa relacionada con la tarea.
    /// </summary>
    public RestaurantTable Table { get; }

    /// <summary>
    /// Comanda relacionada con la tarea.
    ///
    /// Solo es obligatoria para tareas DeliverFood.
    /// </summary>
    public RestaurantOrder Order { get; }

    /// <summary>
    /// Camarero que tiene reservada o asignada la tarea.
    ///
    /// Será null mientras la tarea permanezca pendiente.
    /// </summary>
    public Waiter AssignedWaiter { get; private set; }

    /// <summary>
    /// Secuencia global de creación.
    ///
    /// Se utiliza para mantener el orden de llegada cuando varias
    /// tareas tienen la misma prioridad.
    /// </summary>
    public long CreationSequence { get; }

    /// <summary>
    /// Indica si la tarea continúa esperando asignación.
    /// </summary>
    public bool IsPending =>
        State == WaiterTaskState.Pending;

    /// <summary>
    /// Indica si la tarea puede recibir un camarero.
    /// </summary>
    public bool CanBeAssigned =>
        State == WaiterTaskState.Pending &&
        AssignedWaiter == null;

    /// <summary>
    /// Crea una nueva tarea de camarero.
    /// </summary>
    /// <param name="taskId">
    /// Identificador único generado por la cola central.
    /// </param>
    /// <param name="type">
    /// Tipo de trabajo que debe realizarse.
    /// </param>
    /// <param name="priority">
    /// Prioridad inicial de la tarea.
    /// </param>
    /// <param name="table">
    /// Mesa relacionada con la tarea.
    /// </param>
    /// <param name="order">
    /// Comanda relacionada. Puede ser null salvo en DeliverFood.
    /// </param>
    /// <param name="creationSequence">
    /// Orden global de creación de la tarea.
    /// </param>
    public WaiterTask(
        int taskId,
        WaiterTaskType type,
        WaiterTaskPriority priority,
        RestaurantTable table,
        RestaurantOrder order,
        long creationSequence
    )
    {
        if (taskId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taskId),
                "El identificador de tarea debe ser mayor que cero."
            );
        }

        if (table == null)
        {
            throw new ArgumentNullException(
                nameof(table),
                "Una tarea de camarero necesita una mesa."
            );
        }

        if (type == WaiterTaskType.DeliverFood &&
            order == null)
        {
            throw new ArgumentNullException(
                nameof(order),
                "Una tarea DeliverFood necesita una comanda."
            );
        }

        if (creationSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(creationSequence),
                "La secuencia de creación no puede ser negativa."
            );
        }

        TaskId = taskId;
        Type = type;
        Priority = priority;
        Table = table;
        Order = order;
        CreationSequence = creationSequence;

        State = WaiterTaskState.Pending;
        AssignedWaiter = null;
    }

    /// <summary>
    /// Cambia la prioridad mientras la tarea siga pendiente.
    ///
    /// Esto permitirá aumentar la urgencia de tareas que llevan
    /// demasiado tiempo esperando.
    /// </summary>
    public bool TryChangePriority(
        WaiterTaskPriority newPriority
    )
    {
        if (State != WaiterTaskState.Pending)
            return false;

        Priority = newPriority;

        return true;
    }

    /// <summary>
    /// Reserva la tarea para un camarero disponible.
    ///
    /// Esta operación todavía no inicia el trabajo. El coordinador
    /// deberá confirmar después que el camarero ha aceptado realmente
    /// la tarea.
    /// </summary>
    public bool TryAssignWaiter(Waiter waiter)
    {
        if (waiter == null)
            return false;

        if (!CanBeAssigned)
            return false;

        if (!waiter.IsAvailable)
            return false;

        AssignedWaiter = waiter;
        State = WaiterTaskState.Assigned;

        return true;
    }

    /// <summary>
    /// Libera una asignación que no pudo confirmarse.
    ///
    /// Solo puede ejecutarse antes de que la tarea haya comenzado.
    /// Permite mantener la asignación como una operación transaccional:
    /// si el camarero no acepta el trabajo, la tarea vuelve a Pending.
    /// </summary>
    public bool TryReleaseAssignment()
    {
        if (State != WaiterTaskState.Assigned)
            return false;

        AssignedWaiter = null;
        State = WaiterTaskState.Pending;

        return true;
    }

    /// <summary>
    /// Marca que el camarero ha comenzado a ejecutar la tarea.
    /// </summary>
    public bool TryStart()
    {
        if (State != WaiterTaskState.Assigned)
            return false;

        State = WaiterTaskState.InProgress;

        return true;
    }

    /// <summary>
    /// Marca la tarea como completada.
    /// </summary>
    public bool TryComplete()
    {
        if (State != WaiterTaskState.Assigned &&
            State != WaiterTaskState.InProgress)
        {
            return false;
        }

        State = WaiterTaskState.Completed;

        return true;
    }

    /// <summary>
    /// Cancela una tarea que ya no es necesaria.
    ///
    /// Puede cancelarse mientras esté pendiente, asignada o
    /// en ejecución.
    /// </summary>
    public bool TryCancel()
    {
        if (State == WaiterTaskState.Completed ||
            State == WaiterTaskState.Cancelled)
        {
            return false;
        }

        State = WaiterTaskState.Cancelled;
        AssignedWaiter = null;

        return true;
    }
}