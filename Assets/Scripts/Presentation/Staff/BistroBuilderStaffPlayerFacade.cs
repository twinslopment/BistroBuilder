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

    /// <summary>
    /// Señal única para Presentation. La vista decide cuándo reconstruirse;
    /// esta clase no mantiene una copia autoritativa del estado.
    /// </summary>
    public event Action ViewInvalidated;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

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
    /// Construye una fotografía de lectura completa para la pantalla.
    /// No expone referencias mutables pertenecientes al dominio.
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

        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, true);
        employeeBuffer.Sort(CompareEmployees);
        for (int index = 0; index < employeeBuffer.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employeeBuffer[index];
            if (employee == null)
            {
                continue;
            }
            built.employees.Add(BuildEmployeeRow(employee));
        }

        candidateBuffer.Clear();
        recruitmentService.CopyCandidates(candidateBuffer);
        candidateBuffer.Sort(CompareCandidates);
        for (int index = 0; index < candidateBuffer.Count; index++)
        {
            BistroBuilderStaffCandidateRecord candidate = candidateBuffer[index];
            if (candidate == null)
            {
                continue;
            }
            built.candidates.Add(BuildCandidateRow(candidate));
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryBuildTrainingRows(
        string employeeId,
        List<BistroBuilderStaffPlayerTrainingRow> destination,
        out string error)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();

        if (!ValidateConfiguration(out error) ||
            !staffService.TryGetEmployee(
                employeeId,
                out BistroBuilderEmployeeRecord employee) ||
            employee == null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "No existe el empleado solicitado.";
            }
            return false;
        }

        if (!developmentService.TryGetLevel(
                employee.employeeId,
                out int level,
                out _,
                out _,
                out error))
        {
            return false;
        }

        IReadOnlyList<BistroBuilderStaffTrainingDefinition> trainings =
            developmentService.DevelopmentProfile.Trainings;
        for (int index = 0; index < trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition training = trainings[index];
            if (training == null)
            {
                continue;
            }

            int completed = CountTrainingCompletions(employee, training.trainingId);
            int currentSkill = GetSkill(employee.skills, training.skillKind);
            bool levelAllowed = level >= training.minimumLevel;
            bool repetitionsAllowed = completed < training.maximumCompletions;
            bool skillAllowed = currentSkill < 100;
            bool financeAllowed = training.financialCostCents == 0L;

            destination.Add(new BistroBuilderStaffPlayerTrainingRow
            {
                trainingId = training.trainingId,
                displayName = training.displayName,
                skillKind = training.skillKind,
                skillGain = training.skillGain,
                minimumLevel = training.minimumLevel,
                maximumCompletions = training.maximumCompletions,
                completedCount = completed,
                financialCostCents = training.financialCostCents,
                canTrain = levelAllowed && repetitionsAllowed &&
                           skillAllowed && financeAllowed,
                blockedReason = BuildTrainingBlockedReason(
                    levelAllowed,
                    repetitionsAllowed,
                    skillAllowed,
                    financeAllowed)
            });
        }

        error = string.Empty;
        return true;
    }

    public bool TryHireCandidate(
        string candidateId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        return recruitmentService != null &&
               recruitmentService.TryHireCandidate(candidateId, out employee, out error);
    }

    public bool TryDismissEmployee(
        string employeeId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        return recruitmentService != null &&
               recruitmentService.TryDismissEmployee(employeeId, out employee, out error);
    }

    public bool TrySetAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability availability,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        if (staffService == null)
        {
            employee = null;
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
        string roleDisplay = employee.roleId;
        if (staffService.TryGetRoleDefinition(
                employee.roleId,
                out BistroBuilderStaffRoleDefinition role) &&
            role != null)
        {
            roleDisplay = role.displayName;
        }

        int level = 1;
        long nextLevelExperience = employee.experiencePoints;
        developmentService.TryGetLevel(
            employee.employeeId,
            out level,
            out _,
            out nextLevelExperience,
            out _);

        BistroBuilderEmployeePerformanceSummary performance =
            new BistroBuilderEmployeePerformanceSummary();
        developmentService.TryGetPerformanceSummary(
            employee.employeeId,
            out performance,
            out _);

        bool hasAssignment = sessionService.TryGetAssignmentView(
            employee.employeeId,
            out BistroBuilderEmployeeSessionAssignmentView assignment);

        return new BistroBuilderStaffPlayerEmployeeRow
        {
            employeeId = employee.employeeId,
            fullName = employee.FullName,
            roleId = employee.roleId,
            roleDisplayName = roleDisplay,
            employmentStatus = employee.employmentStatus,
            availability = employee.availability,
            salaryCentsPerService = employee.salaryCentsPerService,
            hiredDayIndex = employee.hiredDayIndex,
            experiencePoints = employee.experiencePoints,
            level = level,
            nextLevelExperience = nextLevelExperience,
            skills = employee.skills != null
                ? employee.skills.DeepClone()
                : new BistroBuilderEmployeeSkillSet(),
            performance = performance ?? new BistroBuilderEmployeePerformanceSummary(),
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
        string roleDisplay = candidate.roleId;
        if (staffService.TryGetRoleDefinition(
                candidate.roleId,
                out BistroBuilderStaffRoleDefinition role) &&
            role != null)
        {
            roleDisplay = role.displayName;
        }

        return new BistroBuilderStaffPlayerCandidateRow
        {
            candidateId = candidate.candidateId,
            fullName = candidate.FullName,
            roleId = candidate.roleId,
            roleDisplayName = roleDisplay,
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
        if (staffService == null)
        {
            TryGetComponent(out staffService);
        }
        if (recruitmentService == null)
        {
            TryGetComponent(out recruitmentService);
        }
        if (developmentService == null)
        {
            TryGetComponent(out developmentService);
        }
        if (sessionService == null)
        {
            TryGetComponent(out sessionService);
        }
    }

    private static int CompareEmployees(
        BistroBuilderEmployeeRecord a,
        BistroBuilderEmployeeRecord b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int status = a.employmentStatus.CompareTo(b.employmentStatus);
        if (status != 0) return status;
        return string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase);
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
            : string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountTrainingCompletions(
        BistroBuilderEmployeeRecord employee,
        string trainingId)
    {
        if (employee?.development?.trainingHistory == null)
        {
            return 0;
        }

        string normalized = BistroBuilderStaffStableIdUtility.Normalize(trainingId);
        int count = 0;
        for (int index = 0;
             index < employee.development.trainingHistory.Count;
             index++)
        {
            BistroBuilderEmployeeTrainingRecord record =
                employee.development.trainingHistory[index];
            if (record != null && string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(record.trainingId),
                    normalized,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static int GetSkill(
        BistroBuilderEmployeeSkillSet skills,
        BistroBuilderEmployeeSkillKind kind)
    {
        if (skills == null) return 0;
        switch (kind)
        {
            case BistroBuilderEmployeeSkillKind.Speed:
                return skills.speed;
            case BistroBuilderEmployeeSkillKind.Attentiveness:
                return skills.attentiveness;
            case BistroBuilderEmployeeSkillKind.Organization:
                return skills.organization;
            case BistroBuilderEmployeeSkillKind.Hospitality:
                return skills.hospitality;
            default:
                return 0;
        }
    }

    private static string BuildTrainingBlockedReason(
        bool levelAllowed,
        bool repetitionsAllowed,
        bool skillAllowed,
        bool financeAllowed)
    {
        if (!levelAllowed) return "Nivel insuficiente";
        if (!repetitionsAllowed) return "Formación completada al máximo";
        if (!skillAllowed) return "Habilidad al máximo";
        if (!financeAllowed) return "Pendiente de integración financiera";
        return string.Empty;
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
