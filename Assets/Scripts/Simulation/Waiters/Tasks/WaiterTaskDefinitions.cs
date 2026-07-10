/// <summary>
/// Define los tipos de trabajo que puede realizar un camarero.
///
/// Estos tipos serán utilizados por el futuro coordinador central
/// para gestionar cualquier número de mesas y camareros.
/// </summary>
public enum WaiterTaskType
{
    /// <summary>
    /// Ir a una mesa para tomar la comanda.
    /// </summary>
    TakeOrder = 0,

    /// <summary>
    /// Recoger una comanda preparada y servirla en su mesa.
    /// </summary>
    DeliverFood = 1,

    /// <summary>
    /// Llevar la cuenta a una mesa.
    /// </summary>
    DeliverBill = 2,

    /// <summary>
    /// Limpiar una mesa que ha quedado sucia.
    /// </summary>
    CleanTable = 3
}

/// <summary>
/// Define la prioridad general de una tarea.
///
/// Los valores numéricos permitirán ordenar las tareas de mayor
/// a menor prioridad sin depender del orden del Inspector.
/// </summary>
public enum WaiterTaskPriority
{
    Low = 100,
    Normal = 200,
    High = 300,
    Urgent = 400
}

/// <summary>
/// Representa el estado de una tarea dentro de la cola central.
/// </summary>
public enum WaiterTaskState
{
    /// <summary>
    /// La tarea está esperando un camarero disponible.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// La tarea ya tiene un camarero asignado.
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// El camarero está ejecutando la tarea.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// La tarea ha terminado correctamente.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// La tarea fue anulada porque dejó de ser necesaria.
    /// </summary>
    Cancelled = 4
}