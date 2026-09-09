using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador 4C de experiencia, rendimiento y formación.
///
/// No escucha tareas todavía: 4D entregará informes agregados al cierre de un
/// servicio real. No usa Update ni mueve dinero. Las formaciones V1 actuales
/// cuestan 0 hasta que exista una integración financiera atómica validada.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Development Service")]
public sealed class BistroBuilderStaffDevelopmentService : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderStaffDevelopmentProfile developmentProfile;

    public event Action<
        BistroBuilderEmployeeRecord,
        BistroBuilderEmployeeProgressionResult> ExperienceChanged;
    public event Action<
        BistroBuilderEmployeeRecord,
        BistroBuilderEmployeePerformanceSummary> PerformanceChanged;
    public event Action<
        BistroBuilderEmployeeRecord,
        BistroBuilderEmployeeTrainingResult> SkillChanged;
    public event Action<
        BistroBuilderEmployeeRecord,
        BistroBuilderEmployeeTrainingResult> TrainingCompleted;

    public BistroBuilderStaffDevelopmentProfile DevelopmentProfile =>
        developmentProfile;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (staffService == null || generalGameStateService == null ||
            developmentProfile == null)
        {
            error =
                "4C necesita StaffService, calendario y perfil de desarrollo.";
            return false;
        }

        if (generalGameStateService.DayIndex < 1)
        {
            error = "El calendario global no expone un DayIndex válido.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !developmentProfile.TryValidate(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Aplica un resultado final de servicio una sola vez. El operationId debe
    /// provenir de 4D y ser estable para el EmployeeId y sesión concretos.
    /// </summary>
    public bool TryApplyServiceResult(
        string employeeId,
        BistroBuilderEmployeeServicePerformanceReport report,
        out BistroBuilderEmployeeRecord employee,
        out BistroBuilderEmployeeProgressionResult progression,
        out string error)
    {
        employee = null;
        progression = null;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        BistroBuilderStaffSnapshot snapshot = staffService.CreateSnapshot();
        if (!BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(
                snapshot,
                employeeId,
                report,
                developmentProfile,
                staffService.RoleCatalog,
                out BistroBuilderStaffSnapshot candidate,
                out BistroBuilderEmployeeRecord updated,
                out BistroBuilderEmployeeProgressionResult result,
                out error))
        {
            return false;
        }

        if (result.wasReplayed)
        {
            employee = updated;
            progression = result;
            error = string.Empty;
            return true;
        }

        if (!staffService.TryCommitDomainMutation(candidate, updated, out error))
        {
            return false;
        }

        employee = updated.DeepClone();
        progression = result;
        ExperienceChanged?.Invoke(employee.DeepClone(), result);
        PerformanceChanged?.Invoke(
            employee.DeepClone(),
            BistroBuilderStaffDevelopmentEngine.BuildPerformanceSummary(employee));
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Formación inmediata y simple V1. Si un curso adquiere coste financiero
    /// distinto de cero, 4C se niega a ejecutarlo hasta disponer de un gateway
    /// atómico contra Finanzas; nunca crea un monedero alternativo.
    /// </summary>
    public bool TryTrainEmployee(
        string employeeId,
        string trainingId,
        string operationId,
        out BistroBuilderEmployeeRecord employee,
        out BistroBuilderEmployeeTrainingResult trainingResult,
        out string error)
    {
        employee = null;
        trainingResult = null;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!developmentProfile.TryGetTraining(
                trainingId,
                out BistroBuilderStaffTrainingDefinition training) ||
            training == null)
        {
            error = "La formación solicitada no existe.";
            return false;
        }

        if (training.financialCostCents > 0L)
        {
            error =
                "Esta formación requiere integración financiera atómica, " +
                "todavía no activada mientras el Bloque 3 está pendiente de cierre.";
            return false;
        }

        var request = new BistroBuilderEmployeeTrainingRequest
        {
            operationId = operationId,
            employeeId = employeeId,
            trainingId = trainingId,
            dayIndex = Math.Max(1, generalGameStateService.DayIndex)
        };

        BistroBuilderStaffSnapshot snapshot = staffService.CreateSnapshot();
        if (!BistroBuilderStaffDevelopmentEngine.TryApplyTraining(
                snapshot,
                request,
                developmentProfile,
                staffService.RoleCatalog,
                out BistroBuilderStaffSnapshot candidate,
                out BistroBuilderEmployeeRecord updated,
                out BistroBuilderEmployeeTrainingResult result,
                out error))
        {
            return false;
        }

        if (result.wasReplayed)
        {
            employee = updated;
            trainingResult = result;
            error = string.Empty;
            return true;
        }

        if (!staffService.TryCommitDomainMutation(candidate, updated, out error))
        {
            return false;
        }

        employee = updated.DeepClone();
        trainingResult = result;
        SkillChanged?.Invoke(employee.DeepClone(), result);
        TrainingCompleted?.Invoke(employee.DeepClone(), result);
        error = string.Empty;
        return true;
    }

    public bool TryGetLevel(
        string employeeId,
        out int level,
        out long currentExperience,
        out long nextLevelExperience,
        out string error)
    {
        level = 0;
        currentExperience = 0L;
        nextLevelExperience = 0L;
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

        currentExperience = employee.experiencePoints;
        level = BistroBuilderStaffDevelopmentEngine.GetLevelForExperience(
            currentExperience,
            developmentProfile);
        nextLevelExperience = level >= developmentProfile.MaximumLevel
            ? currentExperience
            : BistroBuilderStaffDevelopmentEngine.GetExperienceRequiredForLevel(
                level + 1,
                developmentProfile);
        error = string.Empty;
        return true;
    }

    public bool TryGetPerformanceSummary(
        string employeeId,
        out BistroBuilderEmployeePerformanceSummary summary,
        out string error)
    {
        summary = null;
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

        summary = BistroBuilderStaffDevelopmentEngine.BuildPerformanceSummary(
            employee);
        error = string.Empty;
        return true;
    }

    public void CopyTrainingDefinitions(
        List<BistroBuilderStaffTrainingDefinition> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        if (developmentProfile == null || developmentProfile.Trainings == null)
        {
            return;
        }

        for (int index = 0; index < developmentProfile.Trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition definition =
                developmentProfile.Trainings[index];
            if (definition != null)
            {
                destination.Add(definition.DeepClone());
            }
        }
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
        if (developmentProfile == null)
        {
            developmentProfile = Resources.Load<
                BistroBuilderStaffDevelopmentProfile>(
                "BistroBuilder/Staff/StaffDevelopmentProfile");
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
