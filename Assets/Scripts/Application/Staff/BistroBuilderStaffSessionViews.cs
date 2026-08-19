/// <summary>
/// Vista consultiva de una asignación 4D. Puede mencionar WaiterState porque
/// pertenece a Application, nunca se serializa dentro de staff.state.
/// </summary>
public sealed class BistroBuilderEmployeeSessionAssignmentView
{
    public string employeeId = string.Empty;
    public int waiterId;
    public BistroBuilderEmployeeSessionStatus status;
    public WaiterState waiterState;
    public int completedTasks;
    public int failedTasks;
    public int tablesHandled;
    public long totalTaskDurationMilliseconds;
}

public sealed class BistroBuilderStaffCoverageSnapshot
{
    public int operationalWaiterSlots;
    public int activeWaiterEmployees;
    public int availableWaiterEmployees;
    public int boundWaiterEmployees;
    public int unfilledWaiterSlots;
    public int unassignedAvailableWaiterEmployees;
    public bool hasFullCurrentCoverage;
}
