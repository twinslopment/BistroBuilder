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
        {
            scheduledText.text = row != null && row.scheduled ? "EN TURNO" : "Libre";
            scheduledText.color = row != null && row.scheduled
                ? new Color(0.70f, 0.88f, 0.72f, 1f)
                : new Color(0.82f, 0.82f, 0.80f, 1f);
        }
        if (availabilityText != null)
            availabilityText.color = row != null && row.available
                ? new Color(0.72f, 0.86f, 0.74f, 1f)
                : new Color(0.72f, 0.68f, 0.62f, 1f);
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(HandleToggle);
            toggleButton.onClick.AddListener(HandleToggle);
            toggleButton.interactable = row != null && row.available;
            if (toggleButton.targetGraphic != null)
                toggleButton.targetGraphic.color = row == null
                    ? new Color(0.09f, 0.105f, 0.12f, 0.98f)
                    : !row.available
                        ? new Color(0.075f, 0.08f, 0.085f, 0.92f)
                        : row.scheduled
                            ? new Color(0.13f, 0.20f, 0.16f, 0.98f)
                            : new Color(0.09f, 0.105f, 0.12f, 0.98f);
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
