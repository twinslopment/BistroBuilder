using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderStaffSchedulePlayerRow
{
    public string employeeId = string.Empty;
    public string displayName = string.Empty;
    public string roleName = string.Empty;
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
    public List<BistroBuilderStaffSchedulePlayerRow> employees =
        new List<BistroBuilderStaffSchedulePlayerRow>();
}
