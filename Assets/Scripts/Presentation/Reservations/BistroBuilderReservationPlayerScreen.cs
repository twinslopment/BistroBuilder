using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla jugable 6E: agenda diaria y formulario de creación/edición.
/// Presentation no decide disponibilidad ni servicio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reservations/Reservation Player Screen")]
public sealed class BistroBuilderReservationPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderReservationPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform agendaContent;
    [SerializeField] private BistroBuilderReservationRowView rowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousDayButton;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button newReservationButton;
    [SerializeField] private Button saveReservationButton;
    [SerializeField] private Button cancelReservationButton;
    [SerializeField] private Button partyMinusButton;
    [SerializeField] private Button partyPlusButton;
    [SerializeField] private Button timeMinusButton;
    [SerializeField] private Button timePlusButton;
    [SerializeField] private Button durationMinusButton;
    [SerializeField] private Button durationPlusButton;
    [SerializeField] private TMP_InputField guestInput;
    [SerializeField] private TMP_InputField notesInput;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text dayHeaderText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private TMP_Text formModeText;
    [SerializeField] private TMP_Text partyValueText;
    [SerializeField] private TMP_Text timeValueText;
    [SerializeField] private TMP_Text durationValueText;
    [SerializeField] private TMP_Text tableValueText;

    private int selectedDayIndex = 1;
    private string selectedReservationId = string.Empty;
    private int partySize = 2;
    private int arrivalMinute = 780;
    private int durationMinutes = 90;
    private bool cancelArmed;
    private bool bound;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public int SelectedDayIndex => selectedDayIndex;
    public string SelectedReservationId => selectedReservationId;

    private void Awake()
    {
        CacheDependencies();
        Bind();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
        if (facade != null) facade.ViewInvalidated += HandleInvalidated;
    }

    private void OnDisable()
    {
        if (facade != null) facade.ViewInvalidated -= HandleInvalidated;
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || agendaContent == null ||
            rowPrefab == null || closeButton == null || previousDayButton == null ||
            nextDayButton == null || newReservationButton == null ||
            saveReservationButton == null || cancelReservationButton == null)
        {
            error = "La pantalla 6E tiene referencias principales incompletas.";
            return false;
        }
        if (partyMinusButton == null || partyPlusButton == null ||
            timeMinusButton == null || timePlusButton == null ||
            durationMinusButton == null || durationPlusButton == null ||
            guestInput == null || notesInput == null || titleText == null ||
            dayHeaderText == null || summaryText == null || feedbackText == null ||
            emptyStateText == null || formModeText == null || partyValueText == null ||
            timeValueText == null || durationValueText == null || tableValueText == null)
        {
            error = "La pantalla 6E tiene referencias de formulario incompletas.";
            return false;
        }

        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (!ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }

        selectedDayIndex = facade.CurrentDayIndex;
        panelRoot.SetActive(true);
        BeginNewReservation();
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        cancelArmed = false;
        ShowFeedback(string.Empty);
    }

    public void BeginNewReservation()
    {
        selectedReservationId = string.Empty;
        partySize = 2;
        arrivalMinute = 780;
        durationMinutes = 90;
        cancelArmed = false;
        if (guestInput != null) guestInput.text = string.Empty;
        if (notesInput != null) notesInput.text = string.Empty;
        UpdateFormLabels(null);
        ShowFeedback(string.Empty);
        if (IsVisible) RefreshRowsOnly();
    }

    public void Refresh()
    {
        if (!IsVisible || facade == null) return;
        if (!facade.TryBuildDaySnapshot(
                selectedDayIndex,
                out BistroBuilderReservationPlayerSnapshot snapshot,
                out string error))
        {
            ShowFeedback(error);
            return;
        }
        bool selectionStillVisible = false;
        for (int index = 0; index < snapshot.rows.Count; index++)
        {
            BistroBuilderReservationPlayerRow row = snapshot.rows[index];
            if (row != null && string.Equals(
                    row.reservationId,
                    selectedReservationId,
                    StringComparison.Ordinal))
            {
                selectionStillVisible = true;
                break;
            }
        }
        if (!string.IsNullOrWhiteSpace(selectedReservationId) && !selectionStillVisible)
            selectedReservationId = string.Empty;

        RenderSnapshot(snapshot);
        previousDayButton.interactable = selectedDayIndex > facade.CurrentDayIndex;
        nextDayButton.interactable =
            selectedDayIndex < facade.CurrentDayIndex + facade.PlanningHorizonDays - 1;
        ShowFeedback(string.Empty);
    }

    private void RenderSnapshot(BistroBuilderReservationPlayerSnapshot snapshot)
    {
        ClearRows();
        for (int index = 0; index < snapshot.rows.Count; index++)
        {
            BistroBuilderReservationPlayerRow row = snapshot.rows[index];
            BistroBuilderReservationRowView view = Instantiate(rowPrefab, agendaContent);
            view.gameObject.SetActive(true);
            view.Bind(
                row,
                string.Equals(row.reservationId, selectedReservationId, StringComparison.Ordinal),
                HandleSelectReservation);
        }

        dayHeaderText.text = "Día " + selectedDayIndex;
        summaryText.text = snapshot.rows.Count + " reservas · " +
            snapshot.ActiveCount + " activas";
        bool empty = snapshot.rows.Count == 0;
        emptyStateText.gameObject.SetActive(empty);
        emptyStateText.text = empty
            ? "No hay reservas para este día. Crea la primera desde el panel derecho."
            : string.Empty;
        titleText.text = "RESERVAS";

        UpdateFormLabels(FindSelectedRow(snapshot));
    }

    private BistroBuilderReservationPlayerRow FindSelectedRow(
        BistroBuilderReservationPlayerSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(selectedReservationId))
            return null;
        for (int index = 0; index < snapshot.rows.Count; index++)
            if (snapshot.rows[index] != null && string.Equals(
                    snapshot.rows[index].reservationId,
                    selectedReservationId,
                    StringComparison.Ordinal))
                return snapshot.rows[index];
        return null;
    }

    public bool TrySelectReservation(string reservationId, out string error)
    {
        error = string.Empty;
        if (facade == null || !facade.TryGetReservation(
                reservationId,
                out BistroBuilderReservationRecord record) ||
            record == null || record.dayIndex != selectedDayIndex)
        {
            error = "La reserva seleccionada ya no pertenece a la agenda visible.";
            return false;
        }

        selectedReservationId = record.reservationId;
        partySize = record.partySize;
        arrivalMinute = record.arrivalMinute;
        durationMinutes = record.durationMinutes;
        cancelArmed = false;
        guestInput.text = record.guestName;
        notesInput.text = record.notes;
        UpdateFormLabels(ToPlayerRow(record));
        Refresh();
        return true;
    }

    private void HandleSelectReservation(string reservationId)
    {
        if (!TrySelectReservation(reservationId, out string error))
            ShowFeedback(error);
    }

    public bool TrySubmitCurrentForm(out string error)
    {
        error = string.Empty;
        if (facade == null)
        {
            error = "La fachada de Reservas no está disponible.";
            return false;
        }

        var draft = new BistroBuilderReservationDraft
        {
            guestName = guestInput != null ? guestInput.text : string.Empty,
            partySize = partySize,
            dayIndex = selectedDayIndex,
            arrivalMinute = arrivalMinute,
            durationMinutes = durationMinutes,
            notes = notesInput != null ? notesInput.text : string.Empty
        };

        BistroBuilderReservationRecord result;
        bool success = string.IsNullOrWhiteSpace(selectedReservationId)
            ? facade.TryCreateAndAssign(draft, out result, out error)
            : facade.TryEditAndReassign(
                selectedReservationId,
                draft,
                out result,
                out error);
        if (!success || result == null)
            return false;

        selectedReservationId = result.reservationId;
        cancelArmed = false;
        Refresh();
        ShowFeedback("Reserva guardada y mesa " + result.tableId + " asignada.");
        return true;
    }

    private void HandleSaveReservation()
    {
        if (!TrySubmitCurrentForm(out string error))
            ShowFeedback(error);
    }

    public bool TryCancelSelected(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(selectedReservationId))
        {
            error = "Selecciona una reserva antes de cancelar.";
            return false;
        }

        if (!facade.TryCancel(selectedReservationId, out error))
            return false;

        selectedReservationId = string.Empty;
        cancelArmed = false;
        Refresh();
        ShowFeedback("Reserva cancelada.");
        return true;
    }

    private void HandleCancelReservation()
    {
        if (string.IsNullOrWhiteSpace(selectedReservationId))
        {
            ShowFeedback("Selecciona una reserva antes de cancelar.");
            return;
        }

        if (!cancelArmed)
        {
            cancelArmed = true;
            ShowFeedback("Pulsa de nuevo para confirmar la cancelación.");
            UpdateFormLabels(null);
            return;
        }

        if (!TryCancelSelected(out string error))
            ShowFeedback(error);
    }

    private void HandlePreviousDay()
    {
        selectedDayIndex = Mathf.Max(facade.CurrentDayIndex, selectedDayIndex - 1);
        BeginNewReservation();
        Refresh();
    }

    private void HandleNextDay()
    {
        int last = facade.CurrentDayIndex + facade.PlanningHorizonDays - 1;
        selectedDayIndex = Mathf.Min(last, selectedDayIndex + 1);
        BeginNewReservation();
        Refresh();
    }

    private void HandlePartyMinus()
    {
        partySize = Mathf.Max(BistroBuilderReservationEngine.MinimumPartySize, partySize - 1);
        cancelArmed = false;
        UpdateFormLabels(null);
    }

    private void HandlePartyPlus()
    {
        partySize = Mathf.Min(BistroBuilderReservationEngine.MaximumPartySize, partySize + 1);
        cancelArmed = false;
        UpdateFormLabels(null);
    }

    private void HandleTimeMinus()
    {
        arrivalMinute = Mathf.Max(0, arrivalMinute - 30);
        cancelArmed = false;
        ClampDurationToDay();
        UpdateFormLabels(null);
    }

    private void HandleTimePlus()
    {
        int latest = 1440 - durationMinutes;
        arrivalMinute = Mathf.Min(latest, arrivalMinute + 30);
        cancelArmed = false;
        UpdateFormLabels(null);
    }

    private void HandleDurationMinus()
    {
        durationMinutes = Mathf.Max(
            BistroBuilderReservationEngine.MinimumDurationMinutes,
            durationMinutes - 30);
        cancelArmed = false;
        UpdateFormLabels(null);
    }

    private void HandleDurationPlus()
    {
        int maximum = Mathf.Min(
            BistroBuilderReservationEngine.MaximumDurationMinutes,
            1440 - arrivalMinute);
        durationMinutes = Mathf.Min(maximum, durationMinutes + 30);
        cancelArmed = false;
        UpdateFormLabels(null);
    }

    private void ClampDurationToDay()
    {
        durationMinutes = Mathf.Min(durationMinutes, 1440 - arrivalMinute);
        durationMinutes = Mathf.Max(
            BistroBuilderReservationEngine.MinimumDurationMinutes,
            durationMinutes);
    }

    private void UpdateFormLabels(BistroBuilderReservationPlayerRow row)
    {
        if (row == null && !string.IsNullOrWhiteSpace(selectedReservationId) &&
            facade != null && facade.TryGetReservation(
                selectedReservationId,
                out BistroBuilderReservationRecord record) && record != null)
            row = ToPlayerRow(record);

        bool editing = row != null;
        formModeText.text = editing ? "EDITAR RESERVA" : "NUEVA RESERVA";
        partyValueText.text = partySize + " personas";
        timeValueText.text = FormatMinute(arrivalMinute);
        durationValueText.text = durationMinutes + " min";
        tableValueText.text = editing && row.tableId > 0
            ? "Mesa " + row.tableId + " · " + StatusLabel(row.status)
            : "Mesa automática al guardar";

        bool canEdit = !editing || row.CanEdit;
        saveReservationButton.interactable = canEdit;
        cancelReservationButton.interactable = editing && row.CanCancel;
        SetButtonLabel(
            saveReservationButton,
            editing ? "Guardar cambios" : "Crear reserva");
        SetButtonLabel(
            cancelReservationButton,
            cancelArmed ? "Confirmar cancelación" : "Cancelar reserva");
    }

    private static BistroBuilderReservationPlayerRow ToPlayerRow(
        BistroBuilderReservationRecord record)
    {
        return new BistroBuilderReservationPlayerRow
        {
            reservationId = record.reservationId,
            guestName = record.guestName,
            partySize = record.partySize,
            dayIndex = record.dayIndex,
            arrivalMinute = record.arrivalMinute,
            durationMinutes = record.durationMinutes,
            tableId = record.tableId,
            status = record.status,
            notes = record.notes
        };
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
            case BistroBuilderReservationStatus.Booked: return "Reservada";
            case BistroBuilderReservationStatus.Due: return "En hora";
            case BistroBuilderReservationStatus.Arrived: return "Llegada";
            case BistroBuilderReservationStatus.Seated: return "Sentados";
            case BistroBuilderReservationStatus.Completed: return "Completada";
            case BistroBuilderReservationStatus.Cancelled: return "Cancelada";
            case BistroBuilderReservationStatus.NoShow: return "No show";
            default: return status.ToString();
        }
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    private void ClearRows()
    {
        if (agendaContent == null) return;
        for (int index = agendaContent.childCount - 1; index >= 0; index--)
            Destroy(agendaContent.GetChild(index).gameObject);
    }

    private void RefreshRowsOnly() => Refresh();

    private void HandleInvalidated()
    {
        if (IsVisible) Refresh();
    }

    private void Bind()
    {
        if (bound) return;
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (previousDayButton != null) previousDayButton.onClick.AddListener(HandlePreviousDay);
        if (nextDayButton != null) nextDayButton.onClick.AddListener(HandleNextDay);
        if (newReservationButton != null) newReservationButton.onClick.AddListener(BeginNewReservation);
        if (saveReservationButton != null) saveReservationButton.onClick.AddListener(HandleSaveReservation);
        if (cancelReservationButton != null) cancelReservationButton.onClick.AddListener(HandleCancelReservation);
        if (partyMinusButton != null) partyMinusButton.onClick.AddListener(HandlePartyMinus);
        if (partyPlusButton != null) partyPlusButton.onClick.AddListener(HandlePartyPlus);
        if (timeMinusButton != null) timeMinusButton.onClick.AddListener(HandleTimeMinus);
        if (timePlusButton != null) timePlusButton.onClick.AddListener(HandleTimePlus);
        if (durationMinusButton != null) durationMinusButton.onClick.AddListener(HandleDurationMinus);
        if (durationPlusButton != null) durationPlusButton.onClick.AddListener(HandleDurationPlus);
        bound = true;
    }

    private void Unbind()
    {
        if (!bound) return;
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
        if (previousDayButton != null) previousDayButton.onClick.RemoveListener(HandlePreviousDay);
        if (nextDayButton != null) nextDayButton.onClick.RemoveListener(HandleNextDay);
        if (newReservationButton != null) newReservationButton.onClick.RemoveListener(BeginNewReservation);
        if (saveReservationButton != null) saveReservationButton.onClick.RemoveListener(HandleSaveReservation);
        if (cancelReservationButton != null) cancelReservationButton.onClick.RemoveListener(HandleCancelReservation);
        if (partyMinusButton != null) partyMinusButton.onClick.RemoveListener(HandlePartyMinus);
        if (partyPlusButton != null) partyPlusButton.onClick.RemoveListener(HandlePartyPlus);
        if (timeMinusButton != null) timeMinusButton.onClick.RemoveListener(HandleTimeMinus);
        if (timePlusButton != null) timePlusButton.onClick.RemoveListener(HandleTimePlus);
        if (durationMinusButton != null) durationMinusButton.onClick.RemoveListener(HandleDurationMinus);
        if (durationPlusButton != null) durationPlusButton.onClick.RemoveListener(HandleDurationPlus);
        bound = false;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
