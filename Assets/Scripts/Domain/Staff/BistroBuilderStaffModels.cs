using System;
using System.Collections.Generic;

public enum BistroBuilderEmploymentStatus
{
    Active = 0,
    Inactive = 1,
    Dismissed = 2
}

/// <summary>
/// Disponibilidad persistente declarada por Personal. No equivale a estar
/// asignado/trabajando: esos estados pertenecen al binding de sesión 4D.
/// </summary>
public enum BistroBuilderEmployeeAvailability
{
    Available = 0,
    Unavailable = 1
}

[Serializable]
public sealed class BistroBuilderEmployeeSkillSet
{
    public int speed = 50;
    public int attentiveness = 50;
    public int organization = 50;
    public int hospitality = 50;

    public BistroBuilderEmployeeSkillSet DeepClone()
    {
        return (BistroBuilderEmployeeSkillSet)MemberwiseClone();
    }
}

/// <summary>
/// Preferencias/configuración persistente. No contiene lógica de reparto de
/// tareas ni navegación. primaryZoneId puede quedar vacío hasta que exista un
/// sistema de zonas autoritativo.
/// </summary>
[Serializable]
public sealed class BistroBuilderEmployeeResponsibilitySettings
{
    public string primaryResponsibilityId = string.Empty;
    public string primaryZoneId = string.Empty;
    public bool canSupportOtherZones = true;

    public BistroBuilderEmployeeResponsibilitySettings DeepClone()
    {
        return (BistroBuilderEmployeeResponsibilitySettings)MemberwiseClone();
    }
}

/// <summary>
/// Contadores históricos alimentados exclusivamente desde hechos reales del
/// servicio. No contienen puntuaciones inventadas ni estado visual.
/// </summary>
[Serializable]
public sealed class BistroBuilderEmployeePerformanceData
{
    public int completedServices;
    public int completedTasks;
    public int failedTasks;
    public int tablesHandled;
    public long totalTaskDurationMilliseconds;

    public BistroBuilderEmployeePerformanceData DeepClone()
    {
        return (BistroBuilderEmployeePerformanceData)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderEmployeeRecord
{
    public string employeeId = string.Empty;
    public string firstName = string.Empty;
    public string lastName = string.Empty;
    public string roleId = string.Empty;
    public BistroBuilderEmploymentStatus employmentStatus =
        BistroBuilderEmploymentStatus.Active;
    public BistroBuilderEmployeeAvailability availability =
        BistroBuilderEmployeeAvailability.Available;

    /// <summary>
    /// Coste contractual base por servicio programado, en céntimos. Personal
    /// no mueve caja: 3E consumirá más adelante una proyección de nómina.
    /// </summary>
    public long salaryCentsPerService;
    public int hiredDayIndex = 1;
    public long experiencePoints;

    public BistroBuilderEmployeeSkillSet skills =
        new BistroBuilderEmployeeSkillSet();
    public BistroBuilderEmployeeResponsibilitySettings responsibilities =
        new BistroBuilderEmployeeResponsibilitySettings();
    public BistroBuilderEmployeePerformanceData performance =
        new BistroBuilderEmployeePerformanceData();
    public BistroBuilderEmployeeDevelopmentData development =
        new BistroBuilderEmployeeDevelopmentData();

    public long revision = 1L;

    public string FullName
    {
        get
        {
            string first = (firstName ?? string.Empty).Trim();
            string last = (lastName ?? string.Empty).Trim();
            return string.IsNullOrEmpty(last) ? first : first + " " + last;
        }
    }

    public bool IsActiveEmployee =>
        employmentStatus == BistroBuilderEmploymentStatus.Active;

    public BistroBuilderEmployeeRecord DeepClone()
    {
        var clone = (BistroBuilderEmployeeRecord)MemberwiseClone();
        clone.skills = skills != null ? skills.DeepClone() : null;
        clone.responsibilities = responsibilities != null
            ? responsibilities.DeepClone()
            : null;
        clone.performance = performance != null
            ? performance.DeepClone()
            : null;
        clone.development = development != null
            ? development.DeepClone()
            : null;
        return clone;
    }
}

/// <summary>
/// Petición de creación canónica. Contratación 4B la consume sin exponer
/// EmployeeId arbitrarios.
/// </summary>
public sealed class BistroBuilderEmployeeCreateRequest
{
    public string firstName = string.Empty;
    public string lastName = string.Empty;
    public string roleId = string.Empty;
    public long salaryCentsPerService;
    public int hiredDayIndex = 1;
    public long initialExperiencePoints;
    public BistroBuilderEmployeeSkillSet initialSkills =
        new BistroBuilderEmployeeSkillSet();
    public BistroBuilderEmployeeAvailability availability =
        BistroBuilderEmployeeAvailability.Available;
    public BistroBuilderEmployeeResponsibilitySettings responsibilities =
        new BistroBuilderEmployeeResponsibilitySettings();
}

[Serializable]
public sealed class BistroBuilderStaffSnapshot
{
    public const string CurrentSchemaId = "staff.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision = 1L;

    /// <summary>
    /// Marca persistente de compatibilidad. 4D puede convertir una única vez
    /// los agentes Waiter legacy de una partida antigua en empleados generados.
    /// Una vez completado, despedir a toda la plantilla nunca vuelve a crearla.
    /// </summary>
    public bool operationalBootstrapCompleted;

    public List<BistroBuilderEmployeeRecord> employees =
        new List<BistroBuilderEmployeeRecord>();

    public BistroBuilderStaffSnapshot DeepClone()
    {
        var clone = new BistroBuilderStaffSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            operationalBootstrapCompleted = operationalBootstrapCompleted,
            employees = new List<BistroBuilderEmployeeRecord>()
        };

        if (employees != null)
        {
            for (int index = 0; index < employees.Count; index++)
            {
                clone.employees.Add(
                    employees[index] != null
                        ? employees[index].DeepClone()
                        : null);
            }
        }
        return clone;
    }
}
