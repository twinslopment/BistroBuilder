using System;
using System.Collections.Generic;

/// <summary>
/// Estado persistente V1 de horarios y turnos de Personal.
/// No contiene GameObjects ni referencias a agentes operativos.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffScheduleSnapshot
{
    public const string CurrentSchemaId = "staff.schedule";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision;
    public List<BistroBuilderStaffShiftRecord> shifts =
        new List<BistroBuilderStaffShiftRecord>();

    public BistroBuilderStaffScheduleSnapshot DeepClone()
    {
        var clone = new BistroBuilderStaffScheduleSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            shifts = new List<BistroBuilderStaffShiftRecord>()
        };

        if (shifts != null)
        {
            for (int index = 0; index < shifts.Count; index++)
            {
                clone.shifts.Add(shifts[index]?.DeepClone());
            }
        }
        return clone;
    }
}

/// <summary>
/// Turno planificado de un EmployeeId para un servicio concreto.
/// El rango horario es informativo/planificador; la autoridad de apertura sigue
/// siendo RestaurantServiceStateService.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffShiftRecord
{
    public string employeeId = string.Empty;
    public int dayIndex = 1;
    public BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    public int startMinute = 720;
    public int endMinute = 960;

    public BistroBuilderStaffShiftRecord DeepClone()
    {
        return (BistroBuilderStaffShiftRecord)MemberwiseClone();
    }
}

/// <summary>
/// Resultado consultivo de cobertura para un servicio.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffScheduleCoverage
{
    public int dayIndex;
    public BistroBuilderMealServiceAvailability mealService;
    public int scheduledWaiters;
    public int minimumRecommendedWaiters;
    public long projectedSalaryCents;
    public bool isSufficient;
}
