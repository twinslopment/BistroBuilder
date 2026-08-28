using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enlace persistible entre una reserva activa y su CustomerGroup canónico.
/// Solo guarda identidades lógicas; nunca referencias Unity.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationRuntimeBindingSaveRecord
{
    public string reservationId = string.Empty;
    public int groupId;

    public BistroBuilderReservationRuntimeBindingSaveRecord DeepClone()
    {
        return (BistroBuilderReservationRuntimeBindingSaveRecord)MemberwiseClone();
    }
}

/// <summary>
/// Payload de SaveGame de Reservas.
/// Contiene el estado planificado y los enlaces runtime necesarios para
/// reconectar grupos restaurados por service.runtime durante un servicio.
/// </summary>
[Serializable]
public sealed class BistroBuilderReservationsSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public BistroBuilderReservationsSnapshot state =
        new BistroBuilderReservationsSnapshot();
    public List<BistroBuilderReservationRuntimeBindingSaveRecord> activeBindings =
        new List<BistroBuilderReservationRuntimeBindingSaveRecord>();

    public BistroBuilderReservationsSaveData DeepClone()
    {
        var clone = new BistroBuilderReservationsSaveData
        {
            version = version,
            state = state != null ? state.DeepClone() : null,
            activeBindings = new List<BistroBuilderReservationRuntimeBindingSaveRecord>()
        };

        if (activeBindings != null)
        {
            for (int index = 0; index < activeBindings.Count; index++)
            {
                BistroBuilderReservationRuntimeBindingSaveRecord binding =
                    activeBindings[index];
                if (binding != null)
                    clone.activeBindings.Add(binding.DeepClone());
            }
        }

        return clone;
    }
}
