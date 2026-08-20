using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila visual reutilizable de un empleado. No guarda estado de Personal:
/// recibe una proyección 4F y devuelve únicamente el EmployeeId seleccionado.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderStaffPlayerEmployeeRowView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text salaryText;

    private string employeeId = string.Empty;
    private Action<string> selected;

    private void OnEnable()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
            selectButton.onClick.AddListener(HandleSelected);
        }
    }

    private void OnDisable()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (selectButton == null || nameText == null || roleText == null ||
            statusText == null || levelText == null || salaryText == null)
        {
            error = "La fila de empleado 4F tiene referencias visuales incompletas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        BistroBuilderStaffPlayerEmployeeRow row,
        Action<string> selectionHandler)
    {
        selected = selectionHandler;
        employeeId = row != null ? row.employeeId : string.Empty;

        if (row == null)
        {
            nameText.text = "—";
            roleText.text = string.Empty;
            statusText.text = string.Empty;
            levelText.text = string.Empty;
            salaryText.text = string.Empty;
            if (selectButton != null) selectButton.interactable = false;
            return;
        }

        nameText.text = row.fullName;
        roleText.text = row.roleDisplayName;
        statusText.text = BuildStatus(row);
        levelText.text = "Nivel " + Math.Max(1, row.level);
        salaryText.text = FormatMoney(row.salaryCentsPerService) + " / servicio";
        if (selectButton != null) selectButton.interactable = true;
    }

    private void HandleSelected()
    {
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            selected?.Invoke(employeeId);
        }
    }

    private static string BuildStatus(BistroBuilderStaffPlayerEmployeeRow row)
    {
        if (row.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            return "Inactivo";
        }

        if (row.hasServiceAssignment)
        {
            return row.sessionStatus == BistroBuilderEmployeeSessionStatus.Working
                ? "Trabajando"
                : "Asignado";
        }

        return row.availability == BistroBuilderEmployeeAvailability.Available
            ? "Disponible"
            : "No disponible";
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0L ? "-" : string.Empty) +
               (absolute / 100L) + "," +
               (absolute % 100L).ToString("00") + " €";
    }
}

/// <summary>
/// Fila visual reutilizable de una oferta del mercado de candidatos.
/// CandidateId identifica la oferta y nunca se convierte en EmployeeId.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderStaffPlayerCandidateRowView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text salaryText;

    private string candidateId = string.Empty;
    private Action<string> selected;

    private void OnEnable()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
            selectButton.onClick.AddListener(HandleSelected);
        }
    }

    private void OnDisable()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (selectButton == null || nameText == null || roleText == null ||
            profileText == null || salaryText == null)
        {
            error = "La fila de candidato 4F tiene referencias visuales incompletas.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Bind(
        BistroBuilderStaffPlayerCandidateRow row,
        Action<string> selectionHandler)
    {
        selected = selectionHandler;
        candidateId = row != null ? row.candidateId : string.Empty;

        if (row == null)
        {
            nameText.text = "—";
            roleText.text = string.Empty;
            profileText.text = string.Empty;
            salaryText.text = string.Empty;
            if (selectButton != null) selectButton.interactable = false;
            return;
        }

        nameText.text = row.fullName;
        roleText.text = row.roleDisplayName;
        profileText.text = row.profile.ToString();
        salaryText.text = FormatMoney(row.expectedSalaryCentsPerService) +
                          " / servicio";
        if (selectButton != null) selectButton.interactable = true;
    }

    private void HandleSelected()
    {
        if (!string.IsNullOrWhiteSpace(candidateId))
        {
            selected?.Invoke(candidateId);
        }
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0L ? "-" : string.Empty) +
               (absolute / 100L) + "," +
               (absolute % 100L).ToString("00") + " €";
    }
}
