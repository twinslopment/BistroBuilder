using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada 4F para la UI jugable de Personal.
///
/// Presentation no es autoridad: todas las lecturas se reconstruyen desde
/// Staff/Recruitment/Development/Session y todos los comandos se delegan en
/// esos servicios. No persiste empleados, candidatos, XP ni bindings.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Player Facade")]
public sealed class BistroBuilderStaffPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffRecruitmentService recruitmentService;
    [SerializeField] private BistroBuilderStaffDevelopmentService developmentService;
    [SerializeField] private BistroBuilderStaffSessionService sessionService;

    private readonly List<BistroBuilderEmployeeRecord> employeeBuffer =
        new List<BistroBuilderEmployeeRecord>();
    private readonly List<BistroBuilderStaffCandidateRecord> candidateBuffer =
        new List<BistroBuilderStaffCandidateRecord>();

    public event Action ViewInvalidated;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (staffService == null || recruitmentService == null ||
            developmentService == null || sessionService == null)
        {
            error =
                "4F necesita Staff, Recruitment, Development y SessionService.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !recruitmentService.ValidateConfiguration(out error) ||
            !developmentService.ValidateConfiguration(out error) ||
            !sessionService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Construye una proyección completa de lectura. Ningún objeto del snapshot
    /// se reutiliza como fuente de verdad del dominio.
    /// </summary>
    public bool TryBuildSnapshot(
        out BistroBuilderStaffPlayerUiSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error) ||
            !recruitmentService.EnsureMarketReady(out error))
        {
            return false;
        }

        var built = new BistroBuilderStaffPlayerUiSnapshot
        {
            staffRevision = staffService.Revision,
            marketRevision = recruitmentService.MarketRevision,
            hasActiveServiceSession = sessionService.HasActiveSession,
            activeServiceBindings = sessionService.BindingCount
        };

        // Role metadata is the source for playable departments, not sample
        // names from the approved mock-up or an invented hiring cap.
        BistroBuilderStaffRoleCatalog roleCatalog = staffService.RoleCatalog;
        if (roleCatalog != null && roleCatalog.Roles != null)
        {
            foreach (BistroBuilderStaffRoleDefinition role in roleCatalog.Roles)
            {
                if (role == null || !role.active ||
                    string.IsNullOrWhiteSpace(role.operationalAdapterId))
                    continue;
                string id = string.IsNullOrWhiteSpace(role.departmentId)
                    ? role.roleId : role.departmentId;
                BistroBuilderStaffPlayerDepartmentRow department = null;
                for (int i = 0; i < built.departments.Count; i++)
                    if (string.Equals(built.departments[i].departmentId, id,
                        StringComparison.Ordinal))
                    {
                        department = built.departments[i];
                        break;
                    }
                if (department == null)
                {
                    department = new BistroBuilderStaffPlayerDepartmentRow
                    {
                        departmentId = id,
                        displayName = string.IsNullOrWhiteSpace(role.departmentDisplayName)
                            ? role.displayName : role.departmentDisplayName,
                        sortOrder = role.departmentSortOrder > 0
                            ? role.departmentSortOrder : 100
                    };
                    built.departments.Add(department);
                }
                if (!department.roleIds.Contains(role.roleId))
                    department.roleIds.Add(role.roleId);
            }
        }
        built.departments.Sort((a, b) =>
        {
            int order = a.sortOrder.CompareTo(b.sortOrder);
            return order != 0 ? order
                : string.Compare(a.displayName, b.displayName,
                    StringComparison.OrdinalIgnoreCase);
        });

        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, true);
        employeeBuffer.Sort(CompareEmployees);
        for (int index = 0; index < employeeBuffer.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employeeBuffer[index];
            // Dismissed records remain in staff.state for history/Save,
            // but must not be offered as current members of Plantilla.
            if (employee != null &&
                employee.employmentStatus != BistroBuilderEmploymentStatus.Dismissed)
            {
                built.employees.Add(BuildEmployeeRow(employee));
            }
        }

        candidateBuffer.Clear();
        recruitmentService.CopyCandidates(candidateBuffer);
        candidateBuffer.Sort(CompareCandidates);
        for (int index = 0; index < candidateBuffer.Count; index++)
        {
            BistroBuilderStaffCandidateRecord candidate = candidateBuffer[index];
            if (candidate != null)
            {
                built.candidates.Add(BuildCandidateRow(candidate));
            }
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryHireCandidate(
        string candidateId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (recruitmentService == null)
        {
            error = "RecruitmentService no está disponible.";
            return false;
        }
        return recruitmentService.TryHireCandidate(
            candidateId,
            out employee,
            out error);
    }

    public bool TryDismissEmployee(
        string employeeId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (recruitmentService == null)
        {
            error = "RecruitmentService no está disponible.";
            return false;
        }
        return recruitmentService.TryDismissEmployee(
            employeeId,
            out employee,
            out error);
    }

    public bool TrySetAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability availability,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (staffService == null)
        {
            error = "StaffService no está disponible.";
            return false;
        }
        return staffService.TrySetAvailability(
            employeeId,
            availability,
            out employee,
            out error);
    }

    public bool TryRefreshCandidates(out string error)
    {
        if (recruitmentService == null)
        {
            error = "RecruitmentService no está disponible.";
            return false;
        }
        return recruitmentService.TryRefreshCandidates(out error);
    }

    public bool TryTrainEmployee(
        string employeeId,
        string trainingId,
        out BistroBuilderEmployeeRecord employee,
        out BistroBuilderEmployeeTrainingResult result,
        out string error)
    {
        employee = null;
        result = null;
        if (developmentService == null)
        {
            error = "DevelopmentService no está disponible.";
            return false;
        }

        string operationId = "staff.ui.training:" +
            Guid.NewGuid().ToString("N").ToLowerInvariant();
        return developmentService.TryTrainEmployee(
            employeeId,
            trainingId,
            operationId,
            out employee,
            out result,
            out error);
    }

    private BistroBuilderStaffPlayerEmployeeRow BuildEmployeeRow(
        BistroBuilderEmployeeRecord employee)
    {
        string roleDisplayName = employee.roleId;
        string departmentId = employee.roleId;
        string departmentDisplayName = roleDisplayName;
        int departmentSortOrder = 100;
        if (staffService.TryGetRoleDefinition(
                employee.roleId,
                out BistroBuilderStaffRoleDefinition role) && role != null)
        {
            roleDisplayName = role.displayName;
            departmentId = string.IsNullOrWhiteSpace(role.departmentId)
                ? role.roleId : role.departmentId;
            departmentDisplayName = string.IsNullOrWhiteSpace(role.departmentDisplayName)
                ? role.displayName : role.departmentDisplayName;
            departmentSortOrder = role.departmentSortOrder > 0
                ? role.departmentSortOrder : 100;
        }

        int level = 1;
        long nextLevelExperience = employee.experiencePoints;
        developmentService.TryGetLevel(
            employee.employeeId,
            out level,
            out _,
            out nextLevelExperience,
            out _);

        developmentService.TryGetPerformanceSummary(
            employee.employeeId,
            out BistroBuilderEmployeePerformanceSummary performance,
            out _);
        performance ??= new BistroBuilderEmployeePerformanceSummary();

        bool hasAssignment = sessionService.TryGetAssignmentView(
            employee.employeeId,
            out BistroBuilderEmployeeSessionAssignmentView assignment);

        return new BistroBuilderStaffPlayerEmployeeRow
        {
            employeeId = employee.employeeId,
            fullName = employee.FullName,
            roleId = employee.roleId,
            roleDisplayName = roleDisplayName,
            departmentId = departmentId,
            departmentDisplayName = departmentDisplayName,
            departmentSortOrder = departmentSortOrder,
            employmentStatus = employee.employmentStatus,
            availability = employee.availability,
            salaryCentsPerService = employee.salaryCentsPerService,
            hiredDayIndex = employee.hiredDayIndex,
            experiencePoints = employee.experiencePoints,
            level = level,
            nextLevelExperience = nextLevelExperience,
            experienceAtLevel = BistroBuilderStaffDevelopmentEngine.GetExperienceRequiredForLevel(
                level, developmentService.DevelopmentProfile),
            skills = employee.skills != null
                ? employee.skills.DeepClone()
                : new BistroBuilderEmployeeSkillSet(),
            performance = performance,
            hasServiceAssignment = hasAssignment,
            sessionStatus = hasAssignment
                ? assignment.status
                : BistroBuilderEmployeeSessionStatus.Unassigned,
            waiterId = hasAssignment ? assignment.waiterId : 0
        };
    }

    private BistroBuilderStaffPlayerCandidateRow BuildCandidateRow(
        BistroBuilderStaffCandidateRecord candidate)
    {
        string roleDisplayName = candidate.roleId;
        string departmentId = candidate.roleId;
        string departmentDisplayName = roleDisplayName;
        int departmentSortOrder = 100;
        if (staffService.TryGetRoleDefinition(
                candidate.roleId,
                out BistroBuilderStaffRoleDefinition role) && role != null)
        {
            roleDisplayName = role.displayName;
            departmentId = string.IsNullOrWhiteSpace(role.departmentId)
                ? role.roleId : role.departmentId;
            departmentDisplayName = string.IsNullOrWhiteSpace(role.departmentDisplayName)
                ? role.displayName : role.departmentDisplayName;
            departmentSortOrder = role.departmentSortOrder > 0
                ? role.departmentSortOrder : 100;
        }

        return new BistroBuilderStaffPlayerCandidateRow
        {
            candidateId = candidate.candidateId,
            fullName = candidate.FullName,
            roleId = candidate.roleId,
            roleDisplayName = roleDisplayName,
            departmentId = departmentId,
            departmentDisplayName = departmentDisplayName,
            departmentSortOrder = departmentSortOrder,
            expectedSalaryCentsPerService =
                candidate.expectedSalaryCentsPerService,
            experiencePoints = candidate.experiencePoints,
            skills = candidate.skills != null
                ? candidate.skills.DeepClone()
                : new BistroBuilderEmployeeSkillSet(),
            profile = candidate.profile,
            generatedDayIndex = candidate.generatedDayIndex
        };
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (staffService != null)
        {
            staffService.StaffChanged += HandleStaffChanged;
        }
        if (recruitmentService != null)
        {
            recruitmentService.CandidateMarketChanged += HandleMarketChanged;
        }
        if (sessionService != null)
        {
            sessionService.AssignmentChanged += HandleAssignmentChanged;
            sessionService.SessionStarted += HandleSessionChanged;
            sessionService.SessionEnded += HandleSessionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (staffService != null)
        {
            staffService.StaffChanged -= HandleStaffChanged;
        }
        if (recruitmentService != null)
        {
            recruitmentService.CandidateMarketChanged -= HandleMarketChanged;
        }
        if (sessionService != null)
        {
            sessionService.AssignmentChanged -= HandleAssignmentChanged;
            sessionService.SessionStarted -= HandleSessionChanged;
            sessionService.SessionEnded -= HandleSessionChanged;
        }
    }

    private void HandleStaffChanged(long revision) => ViewInvalidated?.Invoke();
    private void HandleMarketChanged(long revision) => ViewInvalidated?.Invoke();
    private void HandleAssignmentChanged(string employeeId) => ViewInvalidated?.Invoke();
    private void HandleSessionChanged(string sessionId) => ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (staffService == null) TryGetComponent(out staffService);
        if (recruitmentService == null) TryGetComponent(out recruitmentService);
        if (developmentService == null) TryGetComponent(out developmentService);
        if (sessionService == null) TryGetComponent(out sessionService);
    }

    private static int CompareEmployees(
        BistroBuilderEmployeeRecord a,
        BistroBuilderEmployeeRecord b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int status = a.employmentStatus.CompareTo(b.employmentStatus);
        return status != 0
            ? status
            : string.Compare(
                a.FullName,
                b.FullName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareCandidates(
        BistroBuilderStaffCandidateRecord a,
        BistroBuilderStaffCandidateRecord b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int salary = a.expectedSalaryCentsPerService.CompareTo(
            b.expectedSalaryCentsPerService);
        return salary != 0
            ? salary
            : string.Compare(
                a.FullName,
                b.FullName,
                StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
