using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderStaffTrainingDefinition
{
    public string trainingId = string.Empty;
    public string displayName = string.Empty;
    public BistroBuilderEmployeeSkillKind skillKind;
    [Range(1, 10)] public int skillGain = 2;
    [Range(1, 20)] public int minimumLevel = 1;
    [Range(1, 20)] public int maximumCompletions = 5;
    [Min(0)] public long financialCostCents;

    public BistroBuilderStaffTrainingDefinition DeepClone()
    {
        return (BistroBuilderStaffTrainingDefinition)MemberwiseClone();
    }
}

/// <summary>
/// Balance V1 de experiencia y formación. Los niveles se derivan de XP y no
/// se persisten para evitar dos fuentes de verdad.
/// </summary>
[CreateAssetMenu(
    fileName = "StaffDevelopmentProfile",
    menuName = "Bistro Builder/Staff/Development Profile")]
public sealed class BistroBuilderStaffDevelopmentProfile : ScriptableObject
{
    public const string StableSchemaId = "staff.development.profile";
    public const int StableSchemaVersion = 1;

    [SerializeField] private string schemaId = StableSchemaId;
    [SerializeField] private int schemaVersion = StableSchemaVersion;

    [Header("Progresión")]
    [SerializeField, Range(2, 20)] private int maximumLevel = 10;
    [SerializeField, Min(1)] private long baseExperiencePerLevel = 400L;
    [SerializeField, Min(0)] private long growthExperiencePerLevel = 120L;

    [Header("XP por servicio")]
    [SerializeField, Min(0)] private long baseCompletedServiceExperience = 18L;
    [SerializeField, Min(0)] private long experiencePerCompletedTask = 2L;
    [SerializeField, Min(0)] private long maximumTaskExperiencePerService = 30L;

    [Header("Formación V1")]
    [SerializeField] private List<BistroBuilderStaffTrainingDefinition> trainings =
        new List<BistroBuilderStaffTrainingDefinition>
        {
            new BistroBuilderStaffTrainingDefinition
            {
                trainingId = "service_pace",
                displayName = "Ritmo de servicio",
                skillKind = BistroBuilderEmployeeSkillKind.Speed,
                skillGain = 2,
                minimumLevel = 1,
                maximumCompletions = 5,
                financialCostCents = 0L
            },
            new BistroBuilderStaffTrainingDefinition
            {
                trainingId = "attention_detail",
                displayName = "Atención al detalle",
                skillKind = BistroBuilderEmployeeSkillKind.Attentiveness,
                skillGain = 2,
                minimumLevel = 1,
                maximumCompletions = 5,
                financialCostCents = 0L
            },
            new BistroBuilderStaffTrainingDefinition
            {
                trainingId = "service_organization",
                displayName = "Organización de sala",
                skillKind = BistroBuilderEmployeeSkillKind.Organization,
                skillGain = 2,
                minimumLevel = 1,
                maximumCompletions = 5,
                financialCostCents = 0L
            },
            new BistroBuilderStaffTrainingDefinition
            {
                trainingId = "guest_care",
                displayName = "Trato al cliente",
                skillKind = BistroBuilderEmployeeSkillKind.Hospitality,
                skillGain = 2,
                minimumLevel = 1,
                maximumCompletions = 5,
                financialCostCents = 0L
            }
        };

    public int MaximumLevel => Mathf.Clamp(maximumLevel, 2, 20);
    public long BaseExperiencePerLevel => Math.Max(1L, baseExperiencePerLevel);
    public long GrowthExperiencePerLevel => Math.Max(0L, growthExperiencePerLevel);
    public long BaseCompletedServiceExperience =>
        Math.Max(0L, baseCompletedServiceExperience);
    public long ExperiencePerCompletedTask =>
        Math.Max(0L, experiencePerCompletedTask);
    public long MaximumTaskExperiencePerService =>
        Math.Max(0L, maximumTaskExperiencePerService);
    public IReadOnlyList<BistroBuilderStaffTrainingDefinition> Trainings => trainings;

    public bool TryGetTraining(
        string trainingId,
        out BistroBuilderStaffTrainingDefinition training)
    {
        training = null;
        string normalized = BistroBuilderStaffStableIdUtility.Normalize(trainingId);
        if (!BistroBuilderStaffStableIdUtility.IsValid(normalized) || trainings == null)
        {
            return false;
        }

        for (int index = 0; index < trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition current = trainings[index];
            if (current != null && string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(current.trainingId),
                    normalized,
                    StringComparison.Ordinal))
            {
                training = current.DeepClone();
                return true;
            }
        }
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (!string.Equals(schemaId, StableSchemaId, StringComparison.Ordinal) ||
            schemaVersion != StableSchemaVersion ||
            maximumLevel < 2 || maximumLevel > 20 ||
            baseExperiencePerLevel < 1L || growthExperiencePerLevel < 0L ||
            baseCompletedServiceExperience < 0L ||
            experiencePerCompletedTask < 0L ||
            maximumTaskExperiencePerService < 0L ||
            trainings == null || trainings.Count == 0 || trainings.Count > 8)
        {
            error = "El perfil de desarrollo contiene configuración básica inválida.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition training = trainings[index];
            if (training == null ||
                !BistroBuilderStaffStableIdUtility.IsValid(training.trainingId) ||
                !ids.Add(BistroBuilderStaffStableIdUtility.Normalize(training.trainingId)) ||
                string.IsNullOrWhiteSpace(training.displayName) ||
                !Enum.IsDefined(typeof(BistroBuilderEmployeeSkillKind), training.skillKind) ||
                training.skillGain < 1 || training.skillGain > 10 ||
                training.minimumLevel < 1 || training.minimumLevel > maximumLevel ||
                training.maximumCompletions < 1 || training.maximumCompletions > 20 ||
                training.financialCostCents < 0L)
            {
                error = "El perfil contiene una formación inválida o duplicada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        schemaId = StableSchemaId;
        schemaVersion = StableSchemaVersion;
        maximumLevel = Mathf.Clamp(maximumLevel, 2, 20);
        baseExperiencePerLevel = Math.Max(1L, baseExperiencePerLevel);
        growthExperiencePerLevel = Math.Max(0L, growthExperiencePerLevel);
        baseCompletedServiceExperience = Math.Max(
            0L,
            baseCompletedServiceExperience);
        experiencePerCompletedTask = Math.Max(0L, experiencePerCompletedTask);
        maximumTaskExperiencePerService = Math.Max(
            0L,
            maximumTaskExperiencePerService);
    }
#endif
}
