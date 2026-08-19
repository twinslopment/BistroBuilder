using System;
using System.Collections.Generic;
using System.Globalization;

public enum BistroBuilderEmployeeSessionStatus
{
    Unassigned = 0,
    Assigned = 1,
    Working = 2
}

/// <summary>
/// Métricas acumuladas del binding actual. Solo conserva hechos observados del
/// runtime central de tareas; no duplica la cola ni el estado del Waiter.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffSessionBindingRecord
{
    public string employeeId = string.Empty;
    public int waiterId;
    public int completedTasks;
    public int failedTasks;
    public long totalTaskDurationMilliseconds;
    public List<int> handledTableIds = new List<int>();

    public BistroBuilderStaffSessionBindingRecord DeepClone()
    {
        return new BistroBuilderStaffSessionBindingRecord
        {
            employeeId = employeeId,
            waiterId = waiterId,
            completedTasks = completedTasks,
            failedTasks = failedTasks,
            totalTaskDurationMilliseconds = totalTaskDurationMilliseconds,
            handledTableIds = handledTableIds != null
                ? new List<int>(handledTableIds)
                : null
        };
    }
}

/// <summary>
/// Runtime persistible del binding de Personal. 4E lo guardará coordinado con
/// service.runtime. No contiene GameObjects, Transform ni referencias Waiter.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffSessionSnapshot
{
    public const string CurrentSchemaId = "staff.session.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision = 1L;
    public bool active;
    public string sessionId = string.Empty;
    public int dayIndex;
    public List<BistroBuilderStaffSessionBindingRecord> bindings =
        new List<BistroBuilderStaffSessionBindingRecord>();

    public BistroBuilderStaffSessionSnapshot DeepClone()
    {
        var clone = new BistroBuilderStaffSessionSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            active = active,
            sessionId = sessionId,
            dayIndex = dayIndex,
            bindings = new List<BistroBuilderStaffSessionBindingRecord>()
        };

        if (bindings != null)
        {
            for (int index = 0; index < bindings.Count; index++)
            {
                clone.bindings.Add(
                    bindings[index] != null
                        ? bindings[index].DeepClone()
                        : null);
            }
        }
        return clone;
    }
}

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

public static class BistroBuilderStaffSessionIdUtility
{
    private const string Prefix = "staffsession_";

    public static string CreateNew()
    {
        return Prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);
        if (!normalized.StartsWith(Prefix, StringComparison.Ordinal) ||
            normalized.Length != Prefix.Length + 32)
        {
            return false;
        }

        bool anyNonZero = false;
        for (int index = Prefix.Length; index < normalized.Length; index++)
        {
            char c = normalized[index];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!hex)
            {
                return false;
            }
            anyNonZero |= c != '0';
        }
        return anyNonZero;
    }
}
