using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro 6D: contrato JSON, validación y compatibilidad estructural.
/// </summary>
public static class BistroBuilderReservations6DSelfTest
{
    [MenuItem("Tools/Bistro Builder/Reservations/6D - Autotest persistencia", false, 640)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();

        BistroBuilderReservationsSnapshot snapshot =
            BistroBuilderReservationEngine.CreateEmptySnapshot();
        var future = new BistroBuilderReservationRecord
        {
            reservationId = "reservation_6d_future",
            guestName = "Lucía Ramos",
            partySize = 2,
            dayIndex = 5,
            arrivalMinute = 780,
            durationMinutes = 90,
            tableId = 2,
            status = BistroBuilderReservationStatus.Booked,
            revision = 1L
        };
        var active = new BistroBuilderReservationRecord
        {
            reservationId = "reservation_6d_active",
            guestName = "Álvaro Martín",
            partySize = 4,
            dayIndex = 3,
            arrivalMinute = 840,
            durationMinutes = 120,
            tableId = 5,
            status = BistroBuilderReservationStatus.Due,
            revision = 2L
        };

        Check(
            BistroBuilderReservationEngine.TryAddReservation(
                snapshot, future,
                out BistroBuilderReservationsSnapshot withFuture,
                out _),
            "Reserva futura añadida al snapshot.",
            ref passed, ref failed, lines);
        Check(
            BistroBuilderReservationEngine.TryAddReservation(
                withFuture, active,
                out BistroBuilderReservationsSnapshot complete,
                out _),
            "Reserva activa añadida al snapshot.",
            ref passed, ref failed, lines);

        var data = new BistroBuilderReservationsSaveData
        {
            version = BistroBuilderReservationsSaveData.CurrentVersion,
            state = complete,
            activeBindings = new List<BistroBuilderReservationRuntimeBindingSaveRecord>
            {
                new BistroBuilderReservationRuntimeBindingSaveRecord
                {
                    reservationId = active.reservationId,
                    groupId = 42
                }
            }
        };

        Check(
            BistroBuilderReservationsSaveSectionProvider.TryValidateSaveData(
                data,
                out string validationError),
            "Payload 6D válido. " + validationError,
            ref passed, ref failed, lines);

        string json = JsonUtility.ToJson(data, false);
        BistroBuilderReservationsSaveData roundTrip =
            JsonUtility.FromJson<BistroBuilderReservationsSaveData>(json);
        string roundTripError = string.Empty;
        Check(
            roundTrip != null &&
            BistroBuilderReservationsSaveSectionProvider.TryValidateSaveData(
                roundTrip,
                out roundTripError),
            "Round-trip JSON de reservations.state válido. " + roundTripError,
            ref passed, ref failed, lines);
        Check(
            roundTrip != null &&
            roundTrip.state.reservations.Count == 2 &&
            roundTrip.activeBindings.Count == 1 &&
            roundTrip.activeBindings[0].groupId == 42,
            "Round-trip conserva reservas futuras y enlace activo.",
            ref passed, ref failed, lines);

        BistroBuilderReservationsSaveData duplicate = data.DeepClone();
        duplicate.activeBindings.Add(
            new BistroBuilderReservationRuntimeBindingSaveRecord
            {
                reservationId = active.reservationId,
                groupId = 43
            });
        Check(
            !BistroBuilderReservationsSaveSectionProvider.TryValidateSaveData(
                duplicate,
                out _),
            "ReservationId runtime duplicado se rechaza.",
            ref passed, ref failed, lines);

        BistroBuilderReservationsSaveData terminal = data.DeepClone();
        for (int index = 0; index < terminal.state.reservations.Count; index++)
        {
            if (terminal.state.reservations[index].reservationId == active.reservationId)
                terminal.state.reservations[index].status =
                    BistroBuilderReservationStatus.Completed;
        }
        Check(
            !BistroBuilderReservationsSaveSectionProvider.TryValidateSaveData(
                terminal,
                out _),
            "Una reserva terminal no puede conservar enlace runtime.",
            ref passed, ref failed, lines);

        var empty = new BistroBuilderReservationsSaveData
        {
            version = BistroBuilderReservationsSaveData.CurrentVersion,
            state = BistroBuilderReservationEngine.CreateEmptySnapshot(),
            activeBindings = new List<BistroBuilderReservationRuntimeBindingSaveRecord>()
        };
        Check(
            BistroBuilderReservationsSaveSectionProvider.TryValidateSaveData(
                empty,
                out _),
            "Estado vacío válido para carga legacy/reset.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — 6D / AUTOTEST PERSISTENCIA ===\n" +
                 string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        List<string> lines)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + text);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + text);
        }
    }
}
