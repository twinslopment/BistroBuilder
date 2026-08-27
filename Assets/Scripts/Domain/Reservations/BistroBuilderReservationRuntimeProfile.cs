using UnityEngine;

/// <summary>
/// Política V1 de integración runtime de Reservas.
/// Mantiene separadas las reglas de llegada del estado persistente.
/// </summary>
[CreateAssetMenu(
    fileName = "ReservationRuntimeProfile",
    menuName = "Bistro Builder/Reservations/Runtime Profile")]
public sealed class BistroBuilderReservationRuntimeProfile : ScriptableObject
{
    [SerializeField, Range(5, 180)]
    private int noShowGraceMinutes = 60;

    public int NoShowGraceMinutes => noShowGraceMinutes;

    public bool TryValidate(out string error)
    {
        if (noShowGraceMinutes < 5 || noShowGraceMinutes > 180)
        {
            error = "La tolerancia de NoShow debe estar entre 5 y 180 minutos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        noShowGraceMinutes = Mathf.Clamp(noShowGraceMinutes, 5, 180);
    }
#endif
}
