using System;
using System.Collections.Generic;

/// <summary>
/// Fila de empleado preparada para Presentation. Es una proyección de lectura:
/// nunca se persiste ni se utiliza como autoridad de Personal.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffPlayerEmployeeRow
{
    public string employeeId = string.Empty;
    public string fullName = string.Empty;
    public string roleId = string.Empty;
    public string roleDisplayName = string.Empty;
    public BistroBuilderEmploymentStatus employmentStatus;
    public BistroBuilderEmployeeAvailability availability;
    public long salaryCentsPerService;
    public int hiredDayIndex;
    public long experiencePoints;
    public int level;
    public long nextLevelExperience;
    public BistroBuilderEmployeeSkillSet skills =
        new BistroBuilderEmployeeSkillSet();
    public BistroBuilderEmployeePerformanceSummary performance =
        new BistroBuilderEmployeePerformanceSummary();
    public bool hasServiceAssignment;
    public BistroBuilderEmployeeSessionStatus sessionStatus;
    public int waiterId;
}

/// <summary>
/// Fila de candidato de mercado. CandidateId identifica la oferta; contratarla
/// genera un EmployeeId nuevo en 4B.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffPlayerCandidateRow
{
    public string candidateId = string.Empty;
    public string fullName = string.Empty;
    public string roleId = string.Empty;
    public string roleDisplayName = string.Empty;
    public long expectedSalaryCentsPerService;
    public long experiencePoints;
    public BistroBuilderEmployeeSkillSet skills =
        new BistroBuilderEmployeeSkillSet();
    public BistroBuilderStaffCandidateProfile profile;
    public int generatedDayIndex;
}

/// <summary>
/// Formación visible para un empleado seleccionado. La posibilidad de ejecutar
/// la operación vuelve a validarse en 4C al confirmar; este booleano es solo UX.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffPlayerTrainingRow
{
    public string trainingId = string.Empty;
    public string displayName = string.Empty;
    public BistroBuilderEmployeeSkillKind skillKind;
    public int skillGain;
    public int minimumLevel;
    public int maximumCompletions;
    public int completedCount;
    public long financialCostCents;
    public bool canTrain;
    public string blockedReason = string.Empty;
}

/// <summary>
/// Snapshot completo de lectura para la pantalla de Personal.
/// Se reconstruye desde autoridades canónicas cuando Presentation lo solicita.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffPlayerUiSnapshot
{
    public long staffRevision;
    public long marketRevision;
    public bool hasActiveServiceSession;
    public int activeServiceBindings;
    public List<BistroBuilderStaffPlayerEmployeeRow> employees =
        new List<BistroBuilderStaffPlayerEmployeeRow>();
    public List<BistroBuilderStaffPlayerCandidateRow> candidates =
        new List<BistroBuilderStaffPlayerCandidateRow>();
}
