using UnityEngine;

/// <summary>
/// Política V1 de planificación de Reservas.
/// No define horarios de apertura: solo horizonte y margen entre reservas.
/// </summary>
[CreateAssetMenu(
    fileName = "ReservationAvailabilityProfile",
    menuName = "Bistro Builder/Reservations/Availability Profile")]
public sealed class BistroBuilderReservationAvailabilityProfile :
    ScriptableObject
{
    [SerializeField, Min(1)]
    private int planningHorizonDays = 14;

    [SerializeField, Range(0, 120)]
    private int turnoverBufferMinutes = 15;

    public int PlanningHorizonDays => planningHorizonDays;
    public int TurnoverBufferMinutes => turnoverBufferMinutes;

    public bool TryValidate(out string error)
    {
        if (planningHorizonDays < 1 || planningHorizonDays > 60)
        {
            error = "El horizonte de Reservas debe estar entre 1 y 60 días.";
            return false;
        }
        if (turnoverBufferMinutes < 0 ||
            turnoverBufferMinutes >
                BistroBuilderReservationAvailabilityEngine
                    .MaximumTurnoverBufferMinutes)
        {
            error = "El margen de rotación de Reservas queda fuera de rango.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        planningHorizonDays = Mathf.Clamp(planningHorizonDays, 1, 60);
        turnoverBufferMinutes = Mathf.Clamp(
            turnoverBufferMinutes,
            0,
            BistroBuilderReservationAvailabilityEngine
                .MaximumTurnoverBufferMinutes);
    }
#endif
}
