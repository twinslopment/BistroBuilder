using System;
using System.Collections.Generic;

/// <summary>
/// Perfil legible derivado de los atributos del candidato. No es una clase
/// RPG ni una autoridad de gameplay: resume qué capacidad destaca para la UI.
/// </summary>
public enum BistroBuilderStaffCandidateProfile
{
    Balanced = 0,
    Fast = 1,
    Attentive = 2,
    Organized = 3,
    Hospitable = 4
}

/// <summary>
/// Propuesta de contratación. CandidateId identifica la oferta del mercado,
/// nunca al futuro empleado. Al contratar se genera un EmployeeId nuevo.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffCandidateRecord
{
    public string candidateId = string.Empty;
    public string firstName = string.Empty;
    public string lastName = string.Empty;
    public string roleId = string.Empty;
    public long expectedSalaryCentsPerService;
    public long experiencePoints;
    public BistroBuilderEmployeeSkillSet skills =
        new BistroBuilderEmployeeSkillSet();
    public BistroBuilderStaffCandidateProfile profile =
        BistroBuilderStaffCandidateProfile.Balanced;
    public int generatedDayIndex = 1;
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

    public BistroBuilderStaffCandidateRecord DeepClone()
    {
        var clone = (BistroBuilderStaffCandidateRecord)MemberwiseClone();
        clone.skills = skills != null ? skills.DeepClone() : null;
        return clone;
    }
}

/// <summary>
/// Estado del mercado de contratación. 4E podrá persistirlo sin mezclarlo con
/// service.runtime. 4B lo mantiene únicamente en memoria.
/// </summary>
[Serializable]
public sealed class BistroBuilderStaffRecruitmentSnapshot
{
    public const string CurrentSchemaId = "staff.recruitment.state";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision = 1L;
    public int generationSequence = 0;
    public int lastRefreshDayIndex = 0;
    public List<BistroBuilderStaffCandidateRecord> candidates =
        new List<BistroBuilderStaffCandidateRecord>();

    public BistroBuilderStaffRecruitmentSnapshot DeepClone()
    {
        var clone = new BistroBuilderStaffRecruitmentSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            generationSequence = generationSequence,
            lastRefreshDayIndex = lastRefreshDayIndex,
            candidates = new List<BistroBuilderStaffCandidateRecord>()
        };

        if (candidates != null)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                clone.candidates.Add(
                    candidates[index] != null
                        ? candidates[index].DeepClone()
                        : null);
            }
        }

        return clone;
    }
}

/// <summary>
/// Contrato de consulta que 4D implementará. Personal 4B no conoce Waiter ni
/// GameObjects: solo pregunta si el EmployeeId tiene un binding de sesión vivo.
/// </summary>
public interface IBistroBuilderStaffSessionAssignmentQuery
{
    bool TryGetActiveAssignment(
        string employeeId,
        out string assignmentReference);
}
