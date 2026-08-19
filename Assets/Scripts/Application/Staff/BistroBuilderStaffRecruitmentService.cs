using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador 4B de mercado, contratación y despido.
///
/// No mueve dinero, no crea camareros y no decide tareas. Convierte una oferta
/// de candidato en un Employee persistente y consulta mediante interfaz si un
/// EmployeeId está ligado a una sesión antes de permitir su despido.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Recruitment Service")]
public sealed class BistroBuilderStaffRecruitmentService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderStaffService staffService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private BistroBuilderStaffRecruitmentProfile recruitmentProfile;

    [Tooltip(
        "Opcional hasta 4D. Debe implementar " +
        "IBistroBuilderStaffSessionAssignmentQuery.")]
    [SerializeField]
    private MonoBehaviour sessionAssignmentQuerySource;

    private BistroBuilderStaffRecruitmentSnapshot marketState;

    public event Action<long> CandidateMarketChanged;
    public event Action<
        BistroBuilderStaffCandidateRecord,
        BistroBuilderEmployeeRecord> EmployeeHired;
    public event Action<BistroBuilderEmployeeRecord> EmployeeDismissed;

    public BistroBuilderStaffRecruitmentProfile RecruitmentProfile =>
        recruitmentProfile;
    public bool IsMarketInitialized =>
        marketState != null && marketState.generationSequence > 0;
    public long MarketRevision => marketState != null ? marketState.revision : 0L;
    public int CandidateCount =>
        marketState != null && marketState.candidates != null
            ? marketState.candidates.Count
            : 0;

    private IBistroBuilderStaffSessionAssignmentQuery SessionAssignmentQuery =>
        sessionAssignmentQuerySource as IBistroBuilderStaffSessionAssignmentQuery;

    private void Awake()
    {
        CacheDependencies();
    }

    private void Start()
    {
        if (!EnsureMarketReady(out string error))
        {
            Debug.LogError(
                "4B no pudo inicializar el mercado de Personal. " + error,
                this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();

        if (staffService == null || generalGameStateService == null ||
            recruitmentProfile == null)
        {
            error =
                "4B necesita StaffService, calendario y perfil de contratación.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !recruitmentProfile.TryValidate(staffService.RoleCatalog, out error))
        {
            return false;
        }

        if (sessionAssignmentQuerySource != null &&
            !(sessionAssignmentQuerySource is
                IBistroBuilderStaffSessionAssignmentQuery))
        {
            error =
                "La fuente de asignación de Personal no implementa el contrato 4D.";
            return false;
        }

        if (marketState != null &&
            !BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                marketState,
                recruitmentProfile,
                staffService.RoleCatalog,
                marketState.generationSequence == 0,
                out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool EnsureMarketReady(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (marketState != null && marketState.generationSequence > 0)
        {
            error = string.Empty;
            return true;
        }

        if (!BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                recruitmentProfile,
                staffService.RoleCatalog,
                Math.Max(1, generalGameStateService.DayIndex),
                out BistroBuilderStaffRecruitmentSnapshot generated,
                out error))
        {
            return false;
        }

        marketState = generated;
        CandidateMarketChanged?.Invoke(marketState.revision);
        return true;
    }

    /// <summary>
    /// Refresco manual V1: como máximo una vez por día. No tiene coste
    /// financiero en 4B y nunca altera la plantilla.
    /// </summary>
    public bool TryRefreshCandidates(out string error)
    {
        if (!EnsureMarketReady(out error))
        {
            return false;
        }

        if (!BistroBuilderStaffRecruitmentEngine.TryRefreshMarket(
                marketState,
                recruitmentProfile,
                staffService.RoleCatalog,
                Math.Max(1, generalGameStateService.DayIndex),
                false,
                out BistroBuilderStaffRecruitmentSnapshot refreshed,
                out error))
        {
            return false;
        }

        marketState = refreshed;
        CandidateMarketChanged?.Invoke(marketState.revision);
        error = string.Empty;
        return true;
    }

    public bool TryGetCandidate(
        string candidateId,
        out BistroBuilderStaffCandidateRecord candidate)
    {
        candidate = null;
        return marketState != null &&
               BistroBuilderStaffRecruitmentEngine.TryFindCandidate(
                   marketState,
                   candidateId,
                   out candidate);
    }

    public void CopyCandidates(
        List<BistroBuilderStaffCandidateRecord> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        if (marketState == null || marketState.candidates == null)
        {
            return;
        }

        for (int index = 0; index < marketState.candidates.Count; index++)
        {
            BistroBuilderStaffCandidateRecord candidate =
                marketState.candidates[index];
            if (candidate != null)
            {
                destination.Add(candidate.DeepClone());
            }
        }
    }

    /// <summary>
    /// Contratación transaccional a nivel de dominio. Primero prepara la
    /// versión del mercado sin el candidato; solo si StaffService crea el
    /// Employee se publica ese nuevo mercado. No existe CandidateId ->
    /// EmployeeId por reutilización de identidad.
    /// </summary>
    public bool TryHireCandidate(
        string candidateId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (!EnsureMarketReady(out error))
        {
            return false;
        }

        if (!BistroBuilderStaffRecruitmentEngine.TryFindCandidate(
                marketState,
                candidateId,
                out BistroBuilderStaffCandidateRecord selected))
        {
            error = "El candidato ya no pertenece al mercado actual.";
            return false;
        }

        if (!BistroBuilderStaffRecruitmentEngine.TryRemoveCandidate(
                marketState,
                candidateId,
                recruitmentProfile,
                staffService.RoleCatalog,
                out BistroBuilderStaffRecruitmentSnapshot marketAfterHire,
                out BistroBuilderStaffCandidateRecord removed,
                out error))
        {
            return false;
        }

        var request = new BistroBuilderEmployeeCreateRequest
        {
            firstName = selected.firstName,
            lastName = selected.lastName,
            roleId = selected.roleId,
            salaryCentsPerService = selected.expectedSalaryCentsPerService,
            hiredDayIndex = Math.Max(1, generalGameStateService.DayIndex),
            initialExperiencePoints = selected.experiencePoints,
            initialSkills = selected.skills != null
                ? selected.skills.DeepClone()
                : null,
            availability = BistroBuilderEmployeeAvailability.Available,
            responsibilities =
                new BistroBuilderEmployeeResponsibilitySettings()
        };

        if (!staffService.TryCreateEmployee(
                request,
                out BistroBuilderEmployeeRecord hired,
                out error))
        {
            return false;
        }

        marketState = marketAfterHire;
        employee = hired.DeepClone();
        CandidateMarketChanged?.Invoke(marketState.revision);
        EmployeeHired?.Invoke(removed.DeepClone(), employee.DeepClone());
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Política V1: un empleado actualmente ligado a un agente de servicio no
    /// puede despedirse. Debe terminar/liberarse primero. Sin 4D instalado no
    /// existe binding de sesión y la consulta es deliberadamente opcional.
    /// </summary>
    public bool TryDismissEmployee(
        string employeeId,
        out BistroBuilderEmployeeRecord dismissed,
        out string error)
    {
        dismissed = null;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!staffService.TryGetEmployee(
                employeeId,
                out BistroBuilderEmployeeRecord existing) ||
            existing == null)
        {
            error = "No existe el empleado solicitado.";
            return false;
        }

        if (existing.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            error = "Solo puede despedirse un empleado activo.";
            return false;
        }

        IBistroBuilderStaffSessionAssignmentQuery assignmentQuery =
            SessionAssignmentQuery;
        if (assignmentQuery != null &&
            assignmentQuery.TryGetActiveAssignment(
                existing.employeeId,
                out string assignmentReference))
        {
            error =
                "El empleado está trabajando/asignado actualmente (" +
                assignmentReference + "). Libera su servicio antes de despedirlo.";
            return false;
        }

        if (!staffService.TryDismissEmployee(
                existing.employeeId,
                out BistroBuilderEmployeeRecord result,
                out error))
        {
            return false;
        }

        dismissed = result.DeepClone();
        EmployeeDismissed?.Invoke(dismissed.DeepClone());
        error = string.Empty;
        return true;
    }

    public BistroBuilderStaffRecruitmentSnapshot CreateMarketSnapshot()
    {
        return marketState != null ? marketState.DeepClone() : null;
    }

    /// <summary>
    /// Contrato preparado para 4E. Valida antes de sustituir el mercado.
    /// </summary>
    public bool TryRestoreMarketSnapshot(
        BistroBuilderStaffRecruitmentSnapshot candidate,
        out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                candidate,
                recruitmentProfile,
                staffService.RoleCatalog,
                candidate != null && candidate.generationSequence == 0,
                out error))
        {
            return false;
        }

        marketState = candidate.DeepClone();
        CandidateMarketChanged?.Invoke(marketState.revision);
        error = string.Empty;
        return true;
    }

    private void CacheDependencies()
    {
        if (staffService == null)
        {
            TryGetComponent(out staffService);
        }
        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }
        if (recruitmentProfile == null)
        {
            recruitmentProfile = Resources.Load<
                BistroBuilderStaffRecruitmentProfile>(
                "BistroBuilder/Staff/StaffRecruitmentProfile");
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
