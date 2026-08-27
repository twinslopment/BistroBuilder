using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila visual reutilizable de una oferta del mercado de candidatos.
/// CandidateId identifica la oferta y nunca se convierte en EmployeeId.
/// Se mantiene en un MonoScript propio para que Unity serialice de forma
/// estable la referencia del componente en escena y Play Mode.
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
        profileText.text = FormatProfile(row.profile);
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

    private static string FormatProfile(
        BistroBuilderStaffCandidateProfile profile)
    {
        switch (profile)
        {
            case BistroBuilderStaffCandidateProfile.Fast:
                return "Rápido";
            case BistroBuilderStaffCandidateProfile.Attentive:
                return "Atento";
            case BistroBuilderStaffCandidateProfile.Organized:
                return "Organizado";
            case BistroBuilderStaffCandidateProfile.Hospitable:
                return "Hospitalario";
            default:
                return "Equilibrado";
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
