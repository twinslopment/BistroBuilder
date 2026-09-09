using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras 4C de experiencia, rendimiento y formación. No conoce Waiter,
/// GameObjects ni Finanzas. Los resultados de servicio llegan agregados desde
/// 4D y se aplican una sola vez por operationId.
/// </summary>
public static class BistroBuilderStaffDevelopmentEngine
{
    public static bool TryValidateDevelopmentData(
        BistroBuilderEmployeeDevelopmentData development,
        out string error)
    {
        if (development == null || development.trainingHistory == null)
        {
            error = "El estado de desarrollo del empleado es nulo.";
            return false;
        }

        if (!string.IsNullOrEmpty(development.lastServiceResultOperationId) &&
            !BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                development.lastServiceResultOperationId))
        {
            error = "El último operationId de servicio no es válido.";
            return false;
        }

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < development.trainingHistory.Count; index++)
        {
            BistroBuilderEmployeeTrainingRecord record =
                development.trainingHistory[index];
            if (record == null ||
                !BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                    record.operationId) ||
                !BistroBuilderStaffStableIdUtility.IsValid(record.trainingId) ||
                !Enum.IsDefined(
                    typeof(BistroBuilderEmployeeSkillKind),
                    record.skillKind) ||
                record.skillGain < 1 || record.skillGain > 10 ||
                record.completedDayIndex < 1 ||
                record.financialCostCents < 0L ||
                !operationIds.Add(
                    BistroBuilderStaffDevelopmentOperationIdUtility.Normalize(
                        record.operationId)))
            {
                error =
                    "El historial de formación contiene un registro inválido o duplicado.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static int GetLevelForExperience(
        long experiencePoints,
        BistroBuilderStaffDevelopmentProfile profile)
    {
        if (profile == null || experiencePoints <= 0L)
        {
            return 1;
        }

        int level = 1;
        long threshold = 0L;
        while (level < profile.MaximumLevel)
        {
            try
            {
                long increment = checked(
                    profile.BaseExperiencePerLevel +
                    profile.GrowthExperiencePerLevel * (level - 1L));
                threshold = checked(threshold + increment);
            }
            catch (OverflowException)
            {
                return level;
            }

            if (experiencePoints < threshold)
            {
                break;
            }
            level++;
        }
        return level;
    }

    public static long GetExperienceRequiredForLevel(
        int level,
        BistroBuilderStaffDevelopmentProfile profile)
    {
        if (profile == null || level <= 1)
        {
            return 0L;
        }

        int target = Math.Min(level, profile.MaximumLevel);
        long total = 0L;
        try
        {
            for (int current = 1; current < target; current++)
            {
                total = checked(
                    total + profile.BaseExperiencePerLevel +
                    profile.GrowthExperiencePerLevel * (current - 1L));
            }
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
        return total;
    }

    public static bool TryApplyServicePerformance(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId,
        BistroBuilderEmployeeServicePerformanceReport report,
        BistroBuilderStaffDevelopmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffSnapshot result,
        out BistroBuilderEmployeeRecord updatedEmployee,
        out BistroBuilderEmployeeProgressionResult progression,
        out string error)
    {
        result = null;
        updatedEmployee = null;
        progression = null;

        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                snapshot,
                roleCatalog,
                out error))
        {
            return false;
        }
        if (profile == null)
        {
            error = "Falta el perfil de desarrollo 4C.";
            return false;
        }
        if (!profile.TryValidate(out error) ||
            !TryValidateServiceReport(report, out error))
        {
            return false;
        }

        if (!BistroBuilderStaffEngine.TryFindEmployee(
                snapshot,
                employeeId,
                out BistroBuilderEmployeeRecord current) ||
            current == null)
        {
            error = "No existe el EmployeeId del resultado de servicio.";
            return false;
        }

        if (current.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            error = "No puede aplicarse rendimiento a un empleado no activo.";
            return false;
        }
        if (!TryValidateDevelopmentData(current.development, out error))
        {
            return false;
        }

        string operationId =
            BistroBuilderStaffDevelopmentOperationIdUtility.Normalize(
                report.operationId);
        int levelBefore = GetLevelForExperience(
            current.experiencePoints,
            profile);

        if (string.Equals(
                BistroBuilderStaffDevelopmentOperationIdUtility.Normalize(
                    current.development.lastServiceResultOperationId),
                operationId,
                StringComparison.Ordinal))
        {
            result = snapshot.DeepClone();
            updatedEmployee = current.DeepClone();
            progression = new BistroBuilderEmployeeProgressionResult
            {
                wasReplayed = true,
                experienceBefore = current.experiencePoints,
                experienceAfter = current.experiencePoints,
                experienceGained = 0L,
                levelBefore = levelBefore,
                levelAfter = levelBefore
            };
            error = string.Empty;
            return true;
        }

        long experienceGain;
        try
        {
            long taskExperience = checked(
                (long)report.completedTasks * profile.ExperiencePerCompletedTask);
            taskExperience = Math.Min(
                taskExperience,
                profile.MaximumTaskExperiencePerService);
            experienceGain = checked(
                profile.BaseCompletedServiceExperience + taskExperience);

            result = snapshot.DeepClone();
            int employeeIndex = FindEmployeeIndex(result, current.employeeId);
            if (employeeIndex < 0)
            {
                error = "La copia de staff.state perdió el empleado objetivo.";
                result = null;
                return false;
            }

            BistroBuilderEmployeeRecord updated = result.employees[employeeIndex];
            if (updated.performance == null || updated.development == null)
            {
                error = "La copia del empleado perdió rendimiento o desarrollo.";
                result = null;
                return false;
            }

            updated.performance.completedServices = checked(
                updated.performance.completedServices + 1);
            updated.performance.completedTasks = checked(
                updated.performance.completedTasks + report.completedTasks);
            updated.performance.failedTasks = checked(
                updated.performance.failedTasks + report.failedTasks);
            updated.performance.tablesHandled = checked(
                updated.performance.tablesHandled + report.tablesHandled);
            updated.performance.totalTaskDurationMilliseconds = checked(
                updated.performance.totalTaskDurationMilliseconds +
                report.totalTaskDurationMilliseconds);
            updated.experiencePoints = checked(
                updated.experiencePoints + experienceGain);
            updated.development.lastServiceResultOperationId = operationId;
            updated.revision = checked(updated.revision + 1L);
            result.revision = checked(result.revision + 1L);
            updatedEmployee = updated.DeepClone();
        }
        catch (OverflowException)
        {
            result = null;
            updatedEmployee = null;
            error = "Los contadores o XP del empleado desbordaron su rango.";
            return false;
        }

        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                result,
                roleCatalog,
                out error) ||
            !TryValidateDevelopmentData(
                updatedEmployee.development,
                out error))
        {
            result = null;
            updatedEmployee = null;
            return false;
        }

        int levelAfter = GetLevelForExperience(
            updatedEmployee.experiencePoints,
            profile);
        progression = new BistroBuilderEmployeeProgressionResult
        {
            wasReplayed = false,
            experienceBefore = current.experiencePoints,
            experienceAfter = updatedEmployee.experiencePoints,
            experienceGained = experienceGain,
            levelBefore = levelBefore,
            levelAfter = levelAfter
        };
        error = string.Empty;
        return true;
    }

    public static bool TryApplyTraining(
        BistroBuilderStaffSnapshot snapshot,
        BistroBuilderEmployeeTrainingRequest request,
        BistroBuilderStaffDevelopmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffSnapshot result,
        out BistroBuilderEmployeeRecord updatedEmployee,
        out BistroBuilderEmployeeTrainingResult trainingResult,
        out string error)
    {
        result = null;
        updatedEmployee = null;
        trainingResult = null;

        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                snapshot,
                roleCatalog,
                out error))
        {
            return false;
        }
        if (profile == null)
        {
            error = "Falta el perfil de desarrollo 4C.";
            return false;
        }
        if (!profile.TryValidate(out error))
        {
            return false;
        }
        if (request == null ||
            !BistroBuilderEmployeeIdUtility.IsValid(request.employeeId) ||
            !BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                request.operationId) ||
            !BistroBuilderStaffStableIdUtility.IsValid(request.trainingId) ||
            request.dayIndex < 1)
        {
            error = "La petición de formación no es válida.";
            return false;
        }
        if (!profile.TryGetTraining(
                request.trainingId,
                out BistroBuilderStaffTrainingDefinition training) ||
            training == null)
        {
            error = "La formación solicitada no existe.";
            return false;
        }
        if (!BistroBuilderStaffEngine.TryFindEmployee(
                snapshot,
                request.employeeId,
                out BistroBuilderEmployeeRecord current) ||
            current == null ||
            current.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            error = "La formación necesita un empleado activo existente.";
            return false;
        }
        if (!TryValidateDevelopmentData(current.development, out error))
        {
            return false;
        }

        string operationId =
            BistroBuilderStaffDevelopmentOperationIdUtility.Normalize(
                request.operationId);
        int previousCompletionCount = 0;
        for (int index = 0; index < current.development.trainingHistory.Count; index++)
        {
            BistroBuilderEmployeeTrainingRecord record =
                current.development.trainingHistory[index];
            if (record == null)
            {
                continue;
            }

            if (string.Equals(
                    BistroBuilderStaffDevelopmentOperationIdUtility.Normalize(
                        record.operationId),
                    operationId,
                    StringComparison.Ordinal))
            {
                int currentSkill = GetSkill(current.skills, training.skillKind);
                result = snapshot.DeepClone();
                updatedEmployee = current.DeepClone();
                trainingResult = new BistroBuilderEmployeeTrainingResult
                {
                    wasReplayed = true,
                    skillKind = training.skillKind,
                    skillBefore = currentSkill,
                    skillAfter = currentSkill,
                    skillGained = 0,
                    completionCount = CountTrainingCompletions(
                        current,
                        training.trainingId),
                    financialCostCents = record.financialCostCents
                };
                error = string.Empty;
                return true;
            }

            if (string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(record.trainingId),
                    BistroBuilderStaffStableIdUtility.Normalize(training.trainingId),
                    StringComparison.Ordinal))
            {
                previousCompletionCount++;
            }
        }

        int level = GetLevelForExperience(current.experiencePoints, profile);
        if (level < training.minimumLevel)
        {
            error = "El empleado todavía no cumple el nivel mínimo de formación.";
            return false;
        }
        if (previousCompletionCount >= training.maximumCompletions)
        {
            error = "El empleado ya completó el máximo de esta formación.";
            return false;
        }

        int skillBefore = GetSkill(current.skills, training.skillKind);
        if (skillBefore >= 100)
        {
            error = "La habilidad objetivo ya está en su máximo V1.";
            return false;
        }

        try
        {
            result = snapshot.DeepClone();
            int employeeIndex = FindEmployeeIndex(result, current.employeeId);
            if (employeeIndex < 0)
            {
                error = "La copia de staff.state perdió el empleado objetivo.";
                result = null;
                return false;
            }

            BistroBuilderEmployeeRecord updated = result.employees[employeeIndex];
            if (updated.skills == null || updated.development == null ||
                updated.development.trainingHistory == null)
            {
                error = "La copia del empleado perdió habilidades o desarrollo.";
                result = null;
                return false;
            }

            int skillAfter = Math.Min(100, skillBefore + training.skillGain);
            SetSkill(updated.skills, training.skillKind, skillAfter);
            updated.development.trainingHistory.Add(
                new BistroBuilderEmployeeTrainingRecord
                {
                    operationId = operationId,
                    trainingId = BistroBuilderStaffStableIdUtility.Normalize(
                        training.trainingId),
                    skillKind = training.skillKind,
                    skillGain = skillAfter - skillBefore,
                    completedDayIndex = request.dayIndex,
                    financialCostCents = training.financialCostCents
                });
            updated.revision = checked(updated.revision + 1L);
            result.revision = checked(result.revision + 1L);
            updatedEmployee = updated.DeepClone();

            trainingResult = new BistroBuilderEmployeeTrainingResult
            {
                wasReplayed = false,
                skillKind = training.skillKind,
                skillBefore = skillBefore,
                skillAfter = skillAfter,
                skillGained = skillAfter - skillBefore,
                completionCount = previousCompletionCount + 1,
                financialCostCents = training.financialCostCents
            };
        }
        catch (OverflowException)
        {
            result = null;
            updatedEmployee = null;
            trainingResult = null;
            error = "La revisión de Personal desbordó el rango soportado.";
            return false;
        }

        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                result,
                roleCatalog,
                out error) ||
            !TryValidateDevelopmentData(
                updatedEmployee.development,
                out error))
        {
            result = null;
            updatedEmployee = null;
            trainingResult = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static BistroBuilderEmployeePerformanceSummary BuildPerformanceSummary(
        BistroBuilderEmployeeRecord employee)
    {
        var summary = new BistroBuilderEmployeePerformanceSummary();
        if (employee == null || employee.performance == null)
        {
            return summary;
        }

        BistroBuilderEmployeePerformanceData performance = employee.performance;
        summary.completedServices = Math.Max(0, performance.completedServices);
        summary.completedTasks = Math.Max(0, performance.completedTasks);
        summary.failedTasks = Math.Max(0, performance.failedTasks);
        summary.tablesHandled = Math.Max(0, performance.tablesHandled);
        summary.hasData = summary.completedServices > 0 ||
                          summary.completedTasks > 0 ||
                          summary.failedTasks > 0;

        long totalTasks = (long)summary.completedTasks + summary.failedTasks;
        if (totalTasks > 0L)
        {
            summary.completionRateBasisPoints = (int)Math.Min(
                10000L,
                (long)summary.completedTasks * 10000L / totalTasks);
            summary.averageTaskDurationMilliseconds = Math.Max(
                0L,
                performance.totalTaskDurationMilliseconds) / totalTasks;
        }

        if (summary.completedServices > 0)
        {
            summary.averageTasksPerServiceTimes100 = (int)Math.Min(
                int.MaxValue,
                (long)summary.completedTasks * 100L /
                summary.completedServices);
        }

        return summary;
    }

    private static bool TryValidateServiceReport(
        BistroBuilderEmployeeServicePerformanceReport report,
        out string error)
    {
        if (report == null ||
            !report.serviceCompleted ||
            !BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                report.operationId) ||
            report.completedTasks < 0 || report.failedTasks < 0 ||
            report.tablesHandled < 0 ||
            report.totalTaskDurationMilliseconds < 0L)
        {
            error =
                "El resultado de servicio debe ser final, trazable y no negativo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int FindEmployeeIndex(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId)
    {
        if (snapshot == null || snapshot.employees == null)
        {
            return -1;
        }

        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            if (snapshot.employees[index] != null &&
                string.Equals(
                    BistroBuilderEmployeeIdUtility.Normalize(
                        snapshot.employees[index].employeeId),
                    normalized,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static int CountTrainingCompletions(
        BistroBuilderEmployeeRecord employee,
        string trainingId)
    {
        if (employee == null || employee.development == null ||
            employee.development.trainingHistory == null)
        {
            return 0;
        }

        int count = 0;
        string normalized = BistroBuilderStaffStableIdUtility.Normalize(trainingId);
        for (int index = 0; index < employee.development.trainingHistory.Count; index++)
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

    public static int GetSkill(
        BistroBuilderEmployeeSkillSet skills,
        BistroBuilderEmployeeSkillKind kind)
    {
        if (skills == null)
        {
            return 0;
        }
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

    private static void SetSkill(
        BistroBuilderEmployeeSkillSet skills,
        BistroBuilderEmployeeSkillKind kind,
        int value)
    {
        switch (kind)
        {
            case BistroBuilderEmployeeSkillKind.Speed:
                skills.speed = value;
                break;
            case BistroBuilderEmployeeSkillKind.Attentiveness:
                skills.attentiveness = value;
                break;
            case BistroBuilderEmployeeSkillKind.Organization:
                skills.organization = value;
                break;
            case BistroBuilderEmployeeSkillKind.Hospitality:
                skills.hospitality = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
