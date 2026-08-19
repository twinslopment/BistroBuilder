using System;
using System.Collections.Generic;

public enum BistroBuilderEmployeeSkillKind
{
    Speed = 0,
    Attentiveness = 1,
    Organization = 2,
    Hospitality = 3
}

/// <summary>
/// Formación ya completada. El historial es pequeño y canónico; no contiene
/// árboles de talentos ni referencias a Presentation.
/// </summary>
[Serializable]
public sealed class BistroBuilderEmployeeTrainingRecord
{
    public string operationId = string.Empty;
    public string trainingId = string.Empty;
    public BistroBuilderEmployeeSkillKind skillKind;
    public int skillGain;
    public int completedDayIndex = 1;
    public long financialCostCents;

    public BistroBuilderEmployeeTrainingRecord DeepClone()
    {
        return (BistroBuilderEmployeeTrainingRecord)MemberwiseClone();
    }
}

/// <summary>
/// Estado mínimo de desarrollo que pertenece al empleado persistente.
/// lastServiceResultOperationId evita re-aplicar el mismo cierre de servicio
/// tras reentradas/Load. El historial de formación es pequeño y acotado.
/// </summary>
[Serializable]
public sealed class BistroBuilderEmployeeDevelopmentData
{
    public string lastServiceResultOperationId = string.Empty;
    public List<BistroBuilderEmployeeTrainingRecord> trainingHistory =
        new List<BistroBuilderEmployeeTrainingRecord>();

    public BistroBuilderEmployeeDevelopmentData DeepClone()
    {
        var clone = new BistroBuilderEmployeeDevelopmentData
        {
            lastServiceResultOperationId = lastServiceResultOperationId,
            trainingHistory = new List<BistroBuilderEmployeeTrainingRecord>()
        };

        if (trainingHistory != null)
        {
            for (int index = 0; index < trainingHistory.Count; index++)
            {
                clone.trainingHistory.Add(
                    trainingHistory[index] != null
                        ? trainingHistory[index].DeepClone()
                        : null);
            }
        }
        return clone;
    }
}

/// <summary>
/// Resultado agregado de un empleado en un servicio real. 4D lo construirá
/// desde hechos del runtime; 4C solo define y valida el contrato.
/// </summary>
public sealed class BistroBuilderEmployeeServicePerformanceReport
{
    public string operationId = string.Empty;
    public bool serviceCompleted = true;
    public int completedTasks;
    public int failedTasks;
    public int tablesHandled;
    public long totalTaskDurationMilliseconds;
}

public sealed class BistroBuilderEmployeePerformanceSummary
{
    public bool hasData;
    public int completedServices;
    public int completedTasks;
    public int failedTasks;
    public int tablesHandled;
    public int completionRateBasisPoints;
    public long averageTaskDurationMilliseconds;
    public int averageTasksPerServiceTimes100;
}

public sealed class BistroBuilderEmployeeProgressionResult
{
    public bool wasReplayed;
    public long experienceBefore;
    public long experienceAfter;
    public long experienceGained;
    public int levelBefore;
    public int levelAfter;
}

public sealed class BistroBuilderEmployeeTrainingRequest
{
    public string operationId = string.Empty;
    public string employeeId = string.Empty;
    public string trainingId = string.Empty;
    public int dayIndex = 1;
}

public sealed class BistroBuilderEmployeeTrainingResult
{
    public bool wasReplayed;
    public BistroBuilderEmployeeSkillKind skillKind;
    public int skillBefore;
    public int skillAfter;
    public int skillGained;
    public int completionCount;
    public long financialCostCents;
}

/// <summary>
/// IDs de operaciones de desarrollo. No son EmployeeId ni IDs visuales.
/// </summary>
public static class BistroBuilderStaffDevelopmentOperationIdUtility
{
    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Length < 4 || normalized.Length > 128)
        {
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char c = normalized[index];
            bool valid =
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '.' || c == '_' || c == '-' || c == ':';
            if (!valid)
            {
                return false;
            }
        }
        return true;
    }
}
