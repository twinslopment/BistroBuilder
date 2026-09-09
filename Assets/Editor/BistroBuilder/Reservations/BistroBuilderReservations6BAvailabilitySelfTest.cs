using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro de 6B — disponibilidad y asignación de mesas.
/// No toca escena ni ocupación runtime.
/// </summary>
public static class BistroBuilderReservations6BAvailabilitySelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Reservations/6B - Autotest disponibilidad",
        false,
        620)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report);
        else Debug.LogError(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        var request = new BistroBuilderReservationDraft
        {
            guestName = "Mesa prueba",
            partySize = 2,
            dayIndex = 3,
            arrivalMinute = 780,
            durationMinutes = 120
        };
        var candidates = new List<BistroBuilderReservationTableCandidate>
        {
            new BistroBuilderReservationTableCandidate
            {
                tableId = 10,
                capacity = 4,
                associatedSeatCount = 4
            },
            new BistroBuilderReservationTableCandidate
            {
                tableId = 7,
                capacity = 2,
                associatedSeatCount = 2
            },
            new BistroBuilderReservationTableCandidate
            {
                tableId = 8,
                capacity = 2,
                associatedSeatCount = 2
            }
        };
        BistroBuilderReservationsSnapshot empty =
            BistroBuilderReservationEngine.CreateEmptySnapshot();
        Check(
            BistroBuilderReservationAvailabilityEngine.TrySelectBestTable(
                request,
                empty,
                candidates,
                15,
                string.Empty,
                out BistroBuilderReservationTableCandidate best,
                out _),
            "Existe mesa compatible para 2 clientes.",
            ref passed,
            ref failed,
            lines);
        Check(
            best != null && best.tableId == 7,
            "La selección minimiza capacidad sobrante y desempata por TableId.",
            ref passed,
            ref failed,
            lines);

        Check(
            !BistroBuilderReservationAvailabilityEngine.IntervalsConflict(
                600,
                660,
                660,
                720,
                0),
            "Franjas contiguas sin margen no colisionan.",
            ref passed,
            ref failed,
            lines);
        Check(
            BistroBuilderReservationAvailabilityEngine.IntervalsConflict(
                600,
                660,
                660,
                720,
                15),
            "El margen de rotación impide reservas pegadas.",
            ref passed,
            ref failed,
            lines);

        var blocking = new BistroBuilderReservationRecord
        {
            reservationId = "reservation_blocking",
            guestName = "Bloqueo",
            partySize = 2,
            dayIndex = 3,
            arrivalMinute = 780,
            durationMinutes = 120,
            tableId = 7,
            status = BistroBuilderReservationStatus.Booked,
            revision = 1
        };
        Check(
            BistroBuilderReservationEngine.TryAddReservation(
                empty,
                blocking,
                out BistroBuilderReservationsSnapshot withBlocking,
                out _),
            "Fixture de reserva bloqueante válido.",
            ref passed,
            ref failed,
            lines);
        Check(
            BistroBuilderReservationAvailabilityEngine.TrySelectBestTable(
                request,
                withBlocking,
                candidates,
                15,
                string.Empty,
                out BistroBuilderReservationTableCandidate fallback,
                out _),
            "Un conflicto no invalida otras mesas compatibles.",
            ref passed,
            ref failed,
            lines);
        Check(
            fallback != null && fallback.tableId == 8,
            "La mesa conflictiva queda excluida y se elige la siguiente óptima.",
            ref passed,
            ref failed,
            lines);

        Check(
            BistroBuilderReservationAvailabilityEngine.IsTableAvailable(
                request,
                withBlocking,
                candidates[1],
                15,
                blocking.reservationId,
                out _),
            "Editar una reserva excluye su propio ReservationId del conflicto.",
            ref passed,
            ref failed,
            lines);
        withBlocking.reservations[0].status =
            BistroBuilderReservationStatus.Completed;
        Check(
            BistroBuilderReservationAvailabilityEngine.IsTableAvailable(
                request,
                withBlocking,
                candidates[1],
                15,
                string.Empty,
                out _),
            "Reservas terminales no bloquean la mesa.",
            ref passed,
            ref failed,
            lines);

        var fourGuests = request.DeepClone();
        fourGuests.partySize = 4;
        Check(
            BistroBuilderReservationAvailabilityEngine.TrySelectBestTable(
                fourGuests,
                empty,
                candidates,
                15,
                string.Empty,
                out BistroBuilderReservationTableCandidate fourSeat,
                out _),
            "Grupo de 4 encuentra una mesa física de 4 plazas.",
            ref passed,
            ref failed,
            lines);
        Check(
            fourSeat != null && fourSeat.tableId == 10,
            "Un grupo de 4 nunca se asigna a mesa de 2.",
            ref passed,
            ref failed,
            lines);
        var brokenSeats = new BistroBuilderReservationTableCandidate
        {
            tableId = 99,
            capacity = 4,
            associatedSeatCount = 2
        };
        Check(
            !BistroBuilderReservationAvailabilityEngine.IsTableAvailable(
                fourGuests,
                empty,
                brokenSeats,
                15,
                string.Empty,
                out _),
            "Capacidad declarada sin sillas físicas suficientes se rechaza.",
            ref passed,
            ref failed,
            lines);
        Check(
            !BistroBuilderReservationAvailabilityEngine.TrySelectBestTable(
                request,
                empty,
                candidates,
                121,
                string.Empty,
                out _,
                out _),
            "Margen de rotación fuera de rango se rechaza.",
            ref passed,
            ref failed,
            lines);

        report = "=== BISTRO BUILDER — 6B / DISPONIBILIDAD ===\n" +
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
            return;
        }

        failed++;
        lines.Add("[FALLO] " + text);
    }
}
