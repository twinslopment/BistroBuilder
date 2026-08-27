using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Autotest puro de 6A — Fundación de Reservas.
/// No toca escena, SaveGame, mesas ni clientes runtime.
/// </summary>
public static class BistroBuilderReservations6AFoundationSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Reservations/6A - Autotest fundación",
        false,
        610)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) UnityEngine.Debug.Log(report);
        else UnityEngine.Debug.LogError(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var lines = new List<string>();
        BistroBuilderReservationsSnapshot empty =
            BistroBuilderReservationEngine.CreateEmptySnapshot();
        Check(
            BistroBuilderReservationEngine.TryValidateSnapshot(
                empty,
                out _),
            "Snapshot reservations.state vacío válido.",
            ref passed,
            ref failed,
            lines);

        var draft = new BistroBuilderReservationDraft
        {
            guestName = "  Carmen Ortega  ",
            partySize = 4,
            dayIndex = 3,
            arrivalMinute = 780,
            durationMinutes = 120,
            notes = "  Ventana  "
        };
        Check(
            BistroBuilderReservationEngine.TryValidateDraft(
                draft,
                out _),
            "Borrador V1 válido.",
            ref passed,
            ref failed,
            lines);
        var record = new BistroBuilderReservationRecord
        {
            reservationId = "reservation_test_001",
            guestName = draft.guestName,
            partySize = draft.partySize,
            dayIndex = draft.dayIndex,
            arrivalMinute = draft.arrivalMinute,
            durationMinutes = draft.durationMinutes,
            notes = draft.notes,
            revision = 1L
        };
        Check(
            BistroBuilderReservationEngine.TryAddReservation(
                empty,
                record,
                out BistroBuilderReservationsSnapshot added,
                out _),
            "Alta pura de reserva incrementa el snapshot.",
            ref passed,
            ref failed,
            lines);
        Check(
            added != null &&
            added.revision == 1L &&
            added.reservations.Count == 1 &&
            added.reservations[0].guestName == "Carmen Ortega",
            "Alta normaliza texto y revisión.",
            ref passed,
            ref failed,
            lines);
        Check(
            !BistroBuilderReservationEngine.TryAddReservation(
                added,
                record,
                out _,
                out _),
            "ReservationId duplicado se rechaza.",
            ref passed,
            ref failed,
            lines);

        Check(
            BistroBuilderReservationEngine.TryAssignTable(
                added,
                record.reservationId,
                5,
                out BistroBuilderReservationsSnapshot assigned,
                out _),
            "6A persiste únicamente TableId lógico.",
            ref passed,
            ref failed,
            lines);
        Check(
            assigned != null &&
            assigned.reservations[0].tableId == 5 &&
            assigned.revision == 2L,
            "Asignación lógica incrementa revisiones.",
            ref passed,
            ref failed,
            lines);
        var editedDraft = draft.DeepClone();
        editedDraft.guestName = "Carmen O.";
        Check(
            BistroBuilderReservationEngine.TryReplaceDraft(
                assigned,
                record.reservationId,
                editedDraft,
                out BistroBuilderReservationsSnapshot edited,
                out _),
            "Reserva planificada admite edición.",
            ref passed,
            ref failed,
            lines);
        Check(
            edited != null &&
            edited.reservations[0].tableId == 0 &&
            edited.reservations[0].revision == 3L,
            "Editar planificación invalida TableId previo.",
            ref passed,
            ref failed,
            lines);

        Check(
            BistroBuilderReservationEngine.TryTransition(
                edited,
                record.reservationId,
                BistroBuilderReservationStatus.Due,
                out BistroBuilderReservationsSnapshot due,
                out _),
            "Booked -> Due permitido.",
            ref passed,
            ref failed,
            lines);
        Check(
            !BistroBuilderReservationEngine.TryTransition(
                due,
                record.reservationId,
                BistroBuilderReservationStatus.Completed,
                out _,
                out _),
            "Due -> Completed directo se rechaza.",
            ref passed,
            ref failed,
            lines);

        BistroBuilderReservationsSnapshot clone = due.DeepClone();
        clone.reservations[0].guestName = "MUTADO";
        Check(
            due.reservations[0].guestName != "MUTADO",
            "DeepClone aísla el estado persistente.",
            ref passed,
            ref failed,
            lines);

        Check(
            PersistentModelsContainNoUnityObjectReferences(),
            "Modelos persistentes no contienen UnityEngine.Object.",
            ref passed,
            ref failed,
            lines);

        report = "=== BISTRO BUILDER — 6A / FUNDACIÓN RESERVAS ===\n" +
                 string.Join("\n", lines) +
                 "\nResultado: " + passed + " OK / " + failed + " fallos.";
        return failed == 0;
    }
    private static bool PersistentModelsContainNoUnityObjectReferences()
    {
        Type[] types =
        {
            typeof(BistroBuilderReservationDraft),
            typeof(BistroBuilderReservationRecord),
            typeof(BistroBuilderReservationsSnapshot)
        };

        for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
            System.Reflection.FieldInfo[] fields =
                types[typeIndex].GetFields();
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                if (ContainsUnityObject(fields[fieldIndex].FieldType))
                    return false;
            }
        }

        return true;
    }

    private static bool ContainsUnityObject(Type type)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return true;

        if (!type.IsGenericType)
            return false;
        Type[] arguments = type.GetGenericArguments();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (ContainsUnityObject(arguments[index]))
                return true;
        }

        return false;
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
