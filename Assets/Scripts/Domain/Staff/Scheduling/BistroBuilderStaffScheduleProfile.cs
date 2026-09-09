using System;
using UnityEngine;

/// <summary>
/// Balance/configuración V1 del planificador de turnos.
/// No contiene empleados concretos ni referencias de escena.
/// </summary>
[CreateAssetMenu(
    fileName = "StaffScheduleProfile",
    menuName = "Bistro Builder/Staff/Schedule Profile")]
public sealed class BistroBuilderStaffScheduleProfile : ScriptableObject
{
    public const string StableSchemaId = "staff.schedule.profile";
    public const int StableSchemaVersion = 1;

    [SerializeField] private string schemaId = StableSchemaId;
    [SerializeField] private int schemaVersion = StableSchemaVersion;
    [SerializeField, Range(1, 28)] private int planningHorizonDays = 7;
    [SerializeField, Range(1, 12)] private int minimumRecommendedWaiters = 1;

    [Header("Comida")]
    [SerializeField, Range(0, 1439)] private int lunchStartMinute = 720;
    [SerializeField, Range(1, 1440)] private int lunchEndMinute = 960;

    [Header("Cena")]
    [SerializeField, Range(0, 1439)] private int dinnerStartMinute = 1140;
    [SerializeField, Range(1, 1440)] private int dinnerEndMinute = 1380;

    public int PlanningHorizonDays => planningHorizonDays;
    public int MinimumRecommendedWaiters => minimumRecommendedWaiters;

    public bool TryGetDefaultWindow(
        BistroBuilderMealServiceAvailability mealService,
        out int startMinute,
        out int endMinute)
    {
        if (mealService == BistroBuilderMealServiceAvailability.Lunch)
        {
            startMinute = lunchStartMinute;
            endMinute = lunchEndMinute;
            return true;
        }
        if (mealService == BistroBuilderMealServiceAvailability.Dinner)
        {
            startMinute = dinnerStartMinute;
            endMinute = dinnerEndMinute;
            return true;
        }

        startMinute = 0;
        endMinute = 0;
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (!string.Equals(schemaId, StableSchemaId, StringComparison.Ordinal) ||
            schemaVersion != StableSchemaVersion ||
            planningHorizonDays < 1 || planningHorizonDays > 28 ||
            minimumRecommendedWaiters < 1 || minimumRecommendedWaiters > 12 ||
            !IsValidWindow(lunchStartMinute, lunchEndMinute) ||
            !IsValidWindow(dinnerStartMinute, dinnerEndMinute))
        {
            error = "El perfil de turnos contiene configuración inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsValidWindow(int startMinute, int endMinute)
    {
        return startMinute >= 0 && startMinute < 1440 &&
               endMinute > startMinute && endMinute <= 1440;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        schemaId = StableSchemaId;
        schemaVersion = StableSchemaVersion;
        planningHorizonDays = Mathf.Clamp(planningHorizonDays, 1, 28);
        minimumRecommendedWaiters = Mathf.Clamp(minimumRecommendedWaiters, 1, 12);
        lunchStartMinute = Mathf.Clamp(lunchStartMinute, 0, 1439);
        lunchEndMinute = Mathf.Clamp(lunchEndMinute, lunchStartMinute + 1, 1440);
        dinnerStartMinute = Mathf.Clamp(dinnerStartMinute, 0, 1439);
        dinnerEndMinute = Mathf.Clamp(dinnerEndMinute, dinnerStartMinute + 1, 1440);
    }
#endif
}
