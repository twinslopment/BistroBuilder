using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla jugable 4F de Personal. Toda mutación se ejecuta a través de
/// BistroBuilderStaffPlayerFacade; este componente solo gestiona selección,
/// renderizado y confirmaciones de UI.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Player Screen")]
public sealed class BistroBuilderStaffPlayerScreen : MonoBehaviour
{
    private enum ViewMode
    {
        Staff = 0,
        Candidates = 1
    }

    private enum PendingConfirmation
    {
        None = 0,
        Hire = 1,
        Dismiss = 2
    }

    [Header("Autoridad de Presentation")]
    [SerializeField] private BistroBuilderStaffPlayerFacade facade;

    [Header("Contenedor")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button staffTabButton;
    [SerializeField] private Button candidatesTabButton;
    [SerializeField] private TMP_Text headerSummaryText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Plantilla")]
    [SerializeField] private GameObject staffPanel;
    [SerializeField] private RectTransform employeeListContent;
    [SerializeField] private BistroBuilderStaffPlayerEmployeeRowView employeeRowPrefab;
    [SerializeField] private TMP_Text employeeNameText;
    [SerializeField] private TMP_Text employeeRoleText;
    [SerializeField] private TMP_Text employeeContractText;
    [SerializeField] private TMP_Text employeeProgressText;
    [SerializeField] private TMP_Text employeeSkillsText;
    [SerializeField] private TMP_Text employeePerformanceText;
    [SerializeField] private TMP_Text employeeSessionText;
    [SerializeField] private Button toggleAvailabilityButton;
    [SerializeField] private TMP_Text toggleAvailabilityButtonText;
    [SerializeField] private Button dismissButton;

    [Header("Candidatos")]
    [SerializeField] private GameObject candidatesPanel;
    [SerializeField] private RectTransform candidateListContent;
    [SerializeField] private BistroBuilderStaffPlayerCandidateRowView candidateRowPrefab;
    [SerializeField] private TMP_Text candidateNameText;
    [SerializeField] private TMP_Text candidateRoleText;
    [SerializeField] private TMP_Text candidateProfileText;
    [SerializeField] private TMP_Text candidateSkillsText;
    [SerializeField] private TMP_Text candidateSalaryText;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button refreshCandidatesButton;

    [Header("Confirmación")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button confirmationAcceptButton;
    [SerializeField] private Button confirmationCancelButton;

    private readonly List<BistroBuilderStaffPlayerEmployeeRowView> employeeRows =
        new List<BistroBuilderStaffPlayerEmployeeRowView>();
    private readonly List<BistroBuilderStaffPlayerCandidateRowView> candidateRows =
        new List<BistroBuilderStaffPlayerCandidateRowView>();

    private BistroBuilderStaffPlayerUiSnapshot currentSnapshot;
    private string selectedEmployeeId = string.Empty;
    private string selectedCandidateId = string.Empty;
    private ViewMode viewMode = ViewMode.Staff;
    private PendingConfirmation pendingConfirmation;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
    public string SelectedEmployeeId => selectedEmployeeId;
    public string SelectedCandidateId => selectedCandidateId;

    private void Awake()
    {
        CacheDependencies();
        BindButtons();
        SetConfirmationVisible(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        BindButtons();
        if (facade != null)
        {
            facade.ViewInvalidated -= HandleViewInvalidated;
            facade.ViewInvalidated += HandleViewInvalidated;
        }

        if (IsVisible)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (facade != null)
        {
            facade.ViewInvalidated -= HandleViewInvalidated;
        }
        UnbindButtons();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || canvasGroup == null ||
            closeButton == null || staffTabButton == null ||
            candidatesTabButton == null || headerSummaryText == null ||
            feedbackText == null)
        {
            error = "La cabecera de la pantalla 4F tiene referencias incompletas.";
            return false;
        }

        if (staffPanel == null || employeeListContent == null ||
            employeeRowPrefab == null || employeeNameText == null ||
            employeeRoleText == null || employeeContractText == null ||
            employeeProgressText == null || employeeSkillsText == null ||
            employeePerformanceText == null || employeeSessionText == null ||
            toggleAvailabilityButton == null ||
            toggleAvailabilityButtonText == null || dismissButton == null)
        {
            error = "La vista de plantilla 4F tiene referencias incompletas.";
            return false;
        }

        if (candidatesPanel == null || candidateListContent == null ||
            candidateRowPrefab == null || candidateNameText == null ||
            candidateRoleText == null || candidateProfileText == null ||
            candidateSkillsText == null || candidateSalaryText == null ||
            hireButton == null || refreshCandidatesButton == null)
        {
            error = "La vista de candidatos 4F tiene referencias incompletas.";
            return false;
        }

        if (confirmationPanel == null || confirmationText == null ||
            confirmationAcceptButton == null || confirmationCancelButton == null)
        {
            error = "La confirmación 4F tiene referencias incompletas.";
            return false;
        }

        if (!facade.ValidateConfiguration(out error) ||
            !employeeRowPrefab.ValidateConfiguration(out error) ||
            !candidateRowPrefab.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Show()
    {
        if (panelRoot == null)
        {
            return;
        }

        panelRoot.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        Refresh();
    }

    public void Hide()
    {
        CancelConfirmation();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Refresh()
    {
        if (!ValidateConfiguration(out string error))
        {
            ShowFeedback(error);
            return;
        }

        if (!facade.TryBuildSnapshot(
                out BistroBuilderStaffPlayerUiSnapshot snapshot,
                out error))
        {
            ShowFeedback(error);
            return;
        }

        currentSnapshot = snapshot;
        PreserveValidSelection();
        RebuildRows();
        RenderHeader();
        RenderSelectedEmployee();
        RenderSelectedCandidate();
        ApplyViewMode();
        ShowFeedback(string.Empty);
    }

    public void ShowStaff()
    {
        viewMode = ViewMode.Staff;
        CancelConfirmation();
        ApplyViewMode();
    }

    public void ShowCandidates()
    {
        viewMode = ViewMode.Candidates;
        CancelConfirmation();
        ApplyViewMode();
    }

    public void RequestHireSelectedCandidate()
    {
        BistroBuilderStaffPlayerCandidateRow candidate = FindSelectedCandidate();
        if (candidate == null)
        {
            ShowFeedback("Selecciona primero un candidato.");
            return;
        }

        pendingConfirmation = PendingConfirmation.Hire;
        confirmationText.text =
            "¿Contratar a " + candidate.fullName + " por " +
            FormatMoney(candidate.expectedSalaryCentsPerService) +
            " por servicio?";
        SetConfirmationVisible(true);
    }

    public void RequestDismissSelectedEmployee()
    {
        BistroBuilderStaffPlayerEmployeeRow employee = FindSelectedEmployee();
        if (employee == null)
        {
            ShowFeedback("Selecciona primero un empleado.");
            return;
        }

        if (employee.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            ShowFeedback("El empleado seleccionado ya no está activo.");
            return;
        }

        pendingConfirmation = PendingConfirmation.Dismiss;
        confirmationText.text =
            "¿Despedir a " + employee.fullName +
            "? Esta acción lo retirará de futuras asignaciones.";
        SetConfirmationVisible(true);
    }

    public void ConfirmPendingAction()
    {
        PendingConfirmation action = pendingConfirmation;
        CancelConfirmation();

        switch (action)
        {
            case PendingConfirmation.Hire:
                ConfirmHire();
                break;
            case PendingConfirmation.Dismiss:
                ConfirmDismiss();
                break;
        }
    }

    public void CancelConfirmation()
    {
        pendingConfirmation = PendingConfirmation.None;
        SetConfirmationVisible(false);
    }

    public void ToggleSelectedAvailability()
    {
        BistroBuilderStaffPlayerEmployeeRow employee = FindSelectedEmployee();
        if (employee == null)
        {
            ShowFeedback("Selecciona primero un empleado.");
            return;
        }

        BistroBuilderEmployeeAvailability target =
            employee.availability == BistroBuilderEmployeeAvailability.Available
                ? BistroBuilderEmployeeAvailability.Unavailable
                : BistroBuilderEmployeeAvailability.Available;

        if (!facade.TrySetAvailability(
                employee.employeeId,
                target,
                out _,
                out string error))
        {
            ShowFeedback(error);
            return;
        }

        Refresh();
    }

    public void RefreshCandidateMarket()
    {
        if (!facade.TryRefreshCandidates(out string error))
        {
            ShowFeedback(error);
            return;
        }

        selectedCandidateId = string.Empty;
        Refresh();
    }

    private void ConfirmHire()
    {
        BistroBuilderStaffPlayerCandidateRow candidate = FindSelectedCandidate();
        if (candidate == null)
        {
            ShowFeedback("El candidato seleccionado ya no está disponible.");
            Refresh();
            return;
        }

        if (!facade.TryHireCandidate(
                candidate.candidateId,
                out BistroBuilderEmployeeRecord employee,
                out string error))
        {
            ShowFeedback(error);
            Refresh();
            return;
        }

        selectedCandidateId = string.Empty;
        selectedEmployeeId = employee != null ? employee.employeeId : string.Empty;
        viewMode = ViewMode.Staff;
        Refresh();
    }

    private void ConfirmDismiss()
    {
        BistroBuilderStaffPlayerEmployeeRow employee = FindSelectedEmployee();
        if (employee == null)
        {
            ShowFeedback("El empleado seleccionado ya no existe.");
            Refresh();
            return;
        }

        if (!facade.TryDismissEmployee(
                employee.employeeId,
                out _,
                out string error))
        {
            ShowFeedback(error);
            Refresh();
            return;
        }

        Refresh();
    }

    private void HandleEmployeeSelected(string employeeId)
    {
        selectedEmployeeId = employeeId ?? string.Empty;
        RenderSelectedEmployee();
    }

    private void HandleCandidateSelected(string candidateId)
    {
        selectedCandidateId = candidateId ?? string.Empty;
        RenderSelectedCandidate();
    }

    private void HandleViewInvalidated()
    {
        if (isActiveAndEnabled && IsVisible)
        {
            Refresh();
        }
    }

    private void RebuildRows()
    {
        ClearRows(employeeRows);
        ClearRows(candidateRows);

        if (currentSnapshot == null)
        {
            return;
        }

        for (int index = 0; index < currentSnapshot.employees.Count; index++)
        {
            BistroBuilderStaffPlayerEmployeeRowView row = Instantiate(
                employeeRowPrefab,
                employeeListContent);
            row.gameObject.SetActive(true);
            row.Bind(currentSnapshot.employees[index], HandleEmployeeSelected);
            employeeRows.Add(row);
        }

        for (int index = 0; index < currentSnapshot.candidates.Count; index++)
        {
            BistroBuilderStaffPlayerCandidateRowView row = Instantiate(
                candidateRowPrefab,
                candidateListContent);
            row.gameObject.SetActive(true);
            row.Bind(currentSnapshot.candidates[index], HandleCandidateSelected);
            candidateRows.Add(row);
        }
    }

    private void RenderHeader()
    {
        if (currentSnapshot == null)
        {
            headerSummaryText.text = "Personal";
            return;
        }

        int activeEmployees = 0;
        for (int index = 0; index < currentSnapshot.employees.Count; index++)
        {
            if (currentSnapshot.employees[index].employmentStatus ==
                BistroBuilderEmploymentStatus.Active)
            {
                activeEmployees++;
            }
        }

        headerSummaryText.text =
            activeEmployees + " empleados activos · " +
            currentSnapshot.candidates.Count + " candidatos · " +
            currentSnapshot.activeServiceBindings + " asignados al servicio";
    }

    private void RenderSelectedEmployee()
    {
        BistroBuilderStaffPlayerEmployeeRow employee = FindSelectedEmployee();
        bool hasEmployee = employee != null;
        dismissButton.interactable = hasEmployee &&
            employee.employmentStatus == BistroBuilderEmploymentStatus.Active;
        toggleAvailabilityButton.interactable = dismissButton.interactable;

        if (!hasEmployee)
        {
            employeeNameText.text = "Selecciona un empleado";
            employeeRoleText.text = string.Empty;
            employeeContractText.text = string.Empty;
            employeeProgressText.text = string.Empty;
            employeeSkillsText.text = string.Empty;
            employeePerformanceText.text = string.Empty;
            employeeSessionText.text = string.Empty;
            toggleAvailabilityButtonText.text = "Disponibilidad";
            return;
        }

        employeeNameText.text = employee.fullName;
        employeeRoleText.text = employee.roleDisplayName;
        employeeContractText.text =
            FormatMoney(employee.salaryCentsPerService) + " / servicio · Alta día " +
            employee.hiredDayIndex;
        employeeProgressText.text =
            "Nivel " + employee.level + " · XP " + employee.experiencePoints +
            " / " + Math.Max(employee.experiencePoints, employee.nextLevelExperience);
        employeeSkillsText.text = BuildSkills(employee.skills);
        employeePerformanceText.text = BuildPerformance(employee.performance);
        employeeSessionText.text = employee.hasServiceAssignment
            ? "Servicio: " + employee.sessionStatus + " · Agente " + employee.waiterId
            : "Sin asignación de servicio";
        toggleAvailabilityButtonText.text =
            employee.availability == BistroBuilderEmployeeAvailability.Available
                ? "Marcar no disponible"
                : "Marcar disponible";
    }

    private void RenderSelectedCandidate()
    {
        BistroBuilderStaffPlayerCandidateRow candidate = FindSelectedCandidate();
        hireButton.interactable = candidate != null;

        if (candidate == null)
        {
            candidateNameText.text = "Selecciona un candidato";
            candidateRoleText.text = string.Empty;
            candidateProfileText.text = string.Empty;
            candidateSkillsText.text = string.Empty;
            candidateSalaryText.text = string.Empty;
            return;
        }

        candidateNameText.text = candidate.fullName;
        candidateRoleText.text = candidate.roleDisplayName;
        candidateProfileText.text =
            "Perfil: " + FormatCandidateProfile(candidate.profile);
        candidateSkillsText.text = BuildSkills(candidate.skills);
        candidateSalaryText.text =
            "Salario esperado: " +
            FormatMoney(candidate.expectedSalaryCentsPerService) + " / servicio";
    }

    private void PreserveValidSelection()
    {
        if (FindEmployee(selectedEmployeeId) == null)
        {
            selectedEmployeeId = currentSnapshot != null &&
                                 currentSnapshot.employees.Count > 0
                ? currentSnapshot.employees[0].employeeId
                : string.Empty;
        }

        if (FindCandidate(selectedCandidateId) == null)
        {
            selectedCandidateId = currentSnapshot != null &&
                                  currentSnapshot.candidates.Count > 0
                ? currentSnapshot.candidates[0].candidateId
                : string.Empty;
        }
    }

    private BistroBuilderStaffPlayerEmployeeRow FindSelectedEmployee()
    {
        return FindEmployee(selectedEmployeeId);
    }

    private BistroBuilderStaffPlayerEmployeeRow FindEmployee(string employeeId)
    {
        if (currentSnapshot == null || string.IsNullOrWhiteSpace(employeeId))
        {
            return null;
        }

        for (int index = 0; index < currentSnapshot.employees.Count; index++)
        {
            BistroBuilderStaffPlayerEmployeeRow row = currentSnapshot.employees[index];
            if (row != null && string.Equals(
                    row.employeeId,
                    employeeId,
                    StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    private BistroBuilderStaffPlayerCandidateRow FindSelectedCandidate()
    {
        return FindCandidate(selectedCandidateId);
    }

    private BistroBuilderStaffPlayerCandidateRow FindCandidate(string candidateId)
    {
        if (currentSnapshot == null || string.IsNullOrWhiteSpace(candidateId))
        {
            return null;
        }

        for (int index = 0; index < currentSnapshot.candidates.Count; index++)
        {
            BistroBuilderStaffPlayerCandidateRow row = currentSnapshot.candidates[index];
            if (row != null && string.Equals(
                    row.candidateId,
                    candidateId,
                    StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    private void ApplyViewMode()
    {
        if (staffPanel != null)
        {
            staffPanel.SetActive(viewMode == ViewMode.Staff);
        }
        if (candidatesPanel != null)
        {
            candidatesPanel.SetActive(viewMode == ViewMode.Candidates);
        }
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(visible);
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message ?? string.Empty;
        }
    }

    private void BindButtons()
    {
        UnbindButtons();
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (staffTabButton != null) staffTabButton.onClick.AddListener(ShowStaff);
        if (candidatesTabButton != null)
            candidatesTabButton.onClick.AddListener(ShowCandidates);
        if (toggleAvailabilityButton != null)
            toggleAvailabilityButton.onClick.AddListener(ToggleSelectedAvailability);
        if (dismissButton != null)
            dismissButton.onClick.AddListener(RequestDismissSelectedEmployee);
        if (hireButton != null)
            hireButton.onClick.AddListener(RequestHireSelectedCandidate);
        if (refreshCandidatesButton != null)
            refreshCandidatesButton.onClick.AddListener(RefreshCandidateMarket);
        if (confirmationAcceptButton != null)
            confirmationAcceptButton.onClick.AddListener(ConfirmPendingAction);
        if (confirmationCancelButton != null)
            confirmationCancelButton.onClick.AddListener(CancelConfirmation);
    }

    private void UnbindButtons()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
        if (staffTabButton != null) staffTabButton.onClick.RemoveListener(ShowStaff);
        if (candidatesTabButton != null)
            candidatesTabButton.onClick.RemoveListener(ShowCandidates);
        if (toggleAvailabilityButton != null)
            toggleAvailabilityButton.onClick.RemoveListener(ToggleSelectedAvailability);
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(RequestDismissSelectedEmployee);
        if (hireButton != null)
            hireButton.onClick.RemoveListener(RequestHireSelectedCandidate);
        if (refreshCandidatesButton != null)
            refreshCandidatesButton.onClick.RemoveListener(RefreshCandidateMarket);
        if (confirmationAcceptButton != null)
            confirmationAcceptButton.onClick.RemoveListener(ConfirmPendingAction);
        if (confirmationCancelButton != null)
            confirmationCancelButton.onClick.RemoveListener(CancelConfirmation);
    }

    private void CacheDependencies()
    {
        if (facade == null)
        {
            TryGetComponent(out facade);
        }
    }

    private static string FormatCandidateProfile(
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

    private static string BuildSkills(BistroBuilderEmployeeSkillSet skills)
    {
        if (skills == null)
        {
            return "Habilidades no disponibles";
        }

        return "Velocidad " + skills.speed +
               " · Atención " + skills.attentiveness +
               " · Organización " + skills.organization +
               " · Trato " + skills.hospitality;
    }

    private static string BuildPerformance(
        BistroBuilderEmployeePerformanceSummary performance)
    {
        if (performance == null || !performance.hasData)
        {
            return "Sin historial de rendimiento todavía";
        }

        int whole = Math.Max(0, performance.completionRateBasisPoints) / 100;
        int fraction = Math.Max(0, performance.completionRateBasisPoints) % 100;
        return performance.completedServices + " servicios · " +
               performance.completedTasks + " tareas · " +
               whole + "," + fraction.ToString("00") + "% completadas";
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0L ? "-" : string.Empty) +
               (absolute / 100L) + "," +
               (absolute % 100L).ToString("00") + " €";
    }

    private static void ClearRows<T>(List<T> rows) where T : Component
    {
        for (int index = 0; index < rows.Count; index++)
        {
            if (rows[index] != null)
            {
                Destroy(rows[index].gameObject);
            }
        }
        rows.Clear();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
