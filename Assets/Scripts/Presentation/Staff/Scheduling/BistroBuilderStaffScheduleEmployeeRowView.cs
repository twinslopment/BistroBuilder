using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila reutilizable de un camarero dentro del planificador 5E.
/// </summary>
public sealed class BistroBuilderStaffScheduleEmployeeRowView : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text availabilityText;
    [SerializeField] private TMP_Text salaryText;
    [SerializeField] private TMP_Text scheduledText;

    private string employeeId = string.Empty;
    private Action<string> onToggle;

    public void Bind(BistroBuilderStaffSchedulePlayerRow row, Action<string> toggle)
    {
        employeeId = row != null ? row.employeeId : string.Empty;
        onToggle = toggle;
        if (nameText != null) nameText.text = row != null ? row.displayName : string.Empty;
        if (availabilityText != null)
            availabilityText.text = row != null && row.available ? "Disponible" : "No disponible";
        if (salaryText != null)
            salaryText.text = row != null ? FormatMoney(row.salaryCentsPerService) : string.Empty;
        if (scheduledText != null)
            scheduledText.text = row != null && row.scheduled ? "EN TURNO" : "Libre";
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(HandleToggle);
            toggleButton.onClick.AddListener(HandleToggle);
            toggleButton.interactable = row != null && row.available;
        }
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggle);
    }

    private void HandleToggle()
    {
        if (!string.IsNullOrWhiteSpace(employeeId)) onToggle?.Invoke(employeeId);
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("0.00") + " € / servicio";
    }
}
