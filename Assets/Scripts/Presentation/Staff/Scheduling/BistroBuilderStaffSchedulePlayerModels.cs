using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderStaffSchedulePlayerRow
{
    public string employeeId = string.Empty;
    public string displayName = string.Empty;
    public string roleName = string.Empty;
    public string departmentId = string.Empty;
    public string departmentDisplayName = string.Empty;
    public int departmentSortOrder = 100;
    public string operationalAdapterId = string.Empty;
    public long salaryCentsPerService;
    public bool available;
    public bool scheduled;
}

[Serializable]
public sealed class BistroBuilderStaffSchedulePlayerSnapshot
{
    public int dayIndex;
    public BistroBuilderMealServiceAvailability mealService;
    public int horizonDays;
    public BistroBuilderStaffScheduleCoverage coverage;
    public int scheduledWaiters;
    public int scheduledCooks;
    public long projectedWaiterSalaryCents;
    public long projectedCookSalaryCents;
    public long projectedTotalSalaryCents;
    public List<BistroBuilderStaffSchedulePlayerRow> employees =
        new List<BistroBuilderStaffSchedulePlayerRow>();
}
