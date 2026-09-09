using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fila visual reutilizable de la agenda de Reservas 6E.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderReservationRowView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text guestText;
    [SerializeField] private TMP_Text partyText;
    [SerializeField] private TMP_Text tableText;
    [SerializeField] private TMP_Text statusText;

    private string reservationId = string.Empty;
    private Action<string> selectAction;

    public void Bind(
        BistroBuilderReservationPlayerRow row,
        bool selected,
        Action<string> onSelect)
    {
        reservationId = row != null ? row.reservationId : string.Empty;
        selectAction = onSelect;
        if (row == null) return;
        if (timeText != null)
            timeText.text = FormatMinute(row.arrivalMinute);
        if (guestText != null)
            guestText.text = row.guestName;
        if (partyText != null)
            partyText.text = row.partySize + " pers.";
        if (tableText != null)
            tableText.text = row.tableId > 0 ? "Mesa " + row.tableId : "Sin mesa";
        if (statusText != null)
        {
            statusText.text = StatusLabel(row.status);
            statusText.color = StatusColor(row.status);
        }

        if (background != null)
            background.color = selected
                ? new Color(0.17f, 0.25f, 0.20f, 1f)
                : new Color(0.085f, 0.10f, 0.115f, 0.98f);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelect);
            selectButton.onClick.AddListener(HandleSelect);
        }
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleSelect);
    }

    private void HandleSelect()
    {
        if (!string.IsNullOrWhiteSpace(reservationId))
            selectAction?.Invoke(reservationId);
    }

    private static string FormatMinute(int minute)
    {
        int hour = Mathf.Clamp(minute / 60, 0, 23);
        int rest = Mathf.Clamp(minute % 60, 0, 59);
        return hour.ToString("00") + ":" + rest.ToString("00");
    }

    private static string StatusLabel(BistroBuilderReservationStatus status)
    {
        switch (status)
        {
            case BistroBuilderReservationStatus.Booked: return "RESERVADA";
            case BistroBuilderReservationStatus.Due: return "EN HORA";
            case BistroBuilderReservationStatus.Arrived: return "LLEGADA";
            case BistroBuilderReservationStatus.Seated: return "SENTADOS";
            case BistroBuilderReservationStatus.Completed: return "COMPLETADA";
            case BistroBuilderReservationStatus.Cancelled: return "CANCELADA";
            case BistroBuilderReservationStatus.NoShow: return "NO SHOW";
            default: return status.ToString().ToUpperInvariant();
        }
    }

    private static Color StatusColor(BistroBuilderReservationStatus status)
    {
        switch (status)
        {
            case BistroBuilderReservationStatus.Booked:
                return new Color(0.76f, 0.82f, 0.66f, 1f);
            case BistroBuilderReservationStatus.Due:
            case BistroBuilderReservationStatus.Arrived:
                return new Color(0.95f, 0.72f, 0.38f, 1f);
            case BistroBuilderReservationStatus.Seated:
            case BistroBuilderReservationStatus.Completed:
                return new Color(0.55f, 0.82f, 0.68f, 1f);
            default:
                return new Color(0.68f, 0.68f, 0.66f, 1f);
        }
    }
}
