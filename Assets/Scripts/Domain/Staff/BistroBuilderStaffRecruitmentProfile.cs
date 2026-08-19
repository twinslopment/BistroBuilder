using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Datos de authoring para generar candidatos V1. No contiene empleados
/// concretos ni estado de partida. Los nombres son pools de generación.
/// </summary>
[CreateAssetMenu(
    fileName = "StaffRecruitmentProfile",
    menuName = "Bistro Builder/Staff/Recruitment Profile")]
public sealed class BistroBuilderStaffRecruitmentProfile : ScriptableObject
{
    public const string StableSchemaId = "staff.recruitment.profile";
    public const int StableSchemaVersion = 1;

    [SerializeField] private string schemaId = StableSchemaId;
    [SerializeField] private int schemaVersion = StableSchemaVersion;

    [Header("Mercado")]
    [SerializeField, Range(3, 12)] private int candidateCount = 5;
    [SerializeField] private int deterministicSalt = 4100201;

    [Header("Rangos V1")]
    [SerializeField, Min(0)] private long minimumSalaryCentsPerService = 6800L;
    [SerializeField, Min(0)] private long maximumSalaryCentsPerService = 11800L;
    [SerializeField, Min(0)] private long minimumExperiencePoints = 0L;
    [SerializeField, Min(0)] private long maximumExperiencePoints = 900L;
    [SerializeField, Range(0, 100)] private int minimumSkill = 42;
    [SerializeField, Range(0, 100)] private int maximumSkill = 72;

    [Header("Roles candidatos")]
    [SerializeField] private List<string> enabledRoleIds = new List<string>
    {
        "waiter"
    };

    [Header("Nombres")]
    [SerializeField] private List<string> firstNames = new List<string>
    {
        "Lucía", "Marta", "Claudia", "Irene", "Paula", "Sara",
        "Daniel", "Álvaro", "Marcos", "Hugo", "Javier", "Diego",
        "Nerea", "Aitana", "Carmen", "Adrián", "Pablo", "Bruno"
    };

    [SerializeField] private List<string> lastNames = new List<string>
    {
        "Álvarez", "Santos", "Fernández", "García", "Martín", "Suárez",
        "Iglesias", "Vega", "Díaz", "Rubio", "Prieto", "Méndez",
        "López", "Blanco", "Pérez", "Ramos", "Ortega", "Castro"
    };

    public int CandidateCount => Mathf.Clamp(candidateCount, 3, 12);
    public int DeterministicSalt => deterministicSalt;
    public long MinimumSalaryCentsPerService => minimumSalaryCentsPerService;
    public long MaximumSalaryCentsPerService => maximumSalaryCentsPerService;
    public long MinimumExperiencePoints => minimumExperiencePoints;
    public long MaximumExperiencePoints => maximumExperiencePoints;
    public int MinimumSkill => minimumSkill;
    public int MaximumSkill => maximumSkill;
    public IReadOnlyList<string> EnabledRoleIds => enabledRoleIds;
    public IReadOnlyList<string> FirstNames => firstNames;
    public IReadOnlyList<string> LastNames => lastNames;

    public bool TryValidate(
        BistroBuilderStaffRoleCatalog roleCatalog,
        out string error)
    {
        if (!string.Equals(schemaId, StableSchemaId, StringComparison.Ordinal) ||
            schemaVersion != StableSchemaVersion ||
            candidateCount < 3 || candidateCount > 12 ||
            minimumSalaryCentsPerService < 0L ||
            maximumSalaryCentsPerService < minimumSalaryCentsPerService ||
            minimumExperiencePoints < 0L ||
            maximumExperiencePoints < minimumExperiencePoints ||
            minimumSkill < 0 || maximumSkill > 100 ||
            maximumSkill < minimumSkill ||
            enabledRoleIds == null || enabledRoleIds.Count == 0 ||
            firstNames == null || firstNames.Count < candidateCount ||
            lastNames == null || lastNames.Count < candidateCount)
        {
            error = "El perfil de contratación contiene rangos o colecciones inválidos.";
            return false;
        }

        if (roleCatalog == null)
        {
            error = "El perfil de contratación necesita un catálogo de roles.";
            return false;
        }

        if (!roleCatalog.TryValidate(out error))
        {
            return false;
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < enabledRoleIds.Count; index++)
        {
            string roleId = BistroBuilderStaffStableIdUtility.Normalize(
                enabledRoleIds[index]);
            if (!BistroBuilderStaffStableIdUtility.IsValid(roleId) ||
                !roles.Add(roleId) ||
                !roleCatalog.TryGetRole(roleId, out BistroBuilderStaffRoleDefinition role) ||
                role == null || !role.active)
            {
                error = "El perfil referencia un rol candidato inválido o duplicado.";
                return false;
            }
        }

        if (!ValidateNames(firstNames) || !ValidateNames(lastNames))
        {
            error = "El perfil contiene nombres vacíos, duplicados o excesivamente largos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateNames(List<string> values)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index] != null ? values[index].Trim() : string.Empty;
            if (value.Length < 1 || value.Length > 64 || !unique.Add(value))
            {
                return false;
            }
        }
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        schemaId = StableSchemaId;
        schemaVersion = StableSchemaVersion;
        candidateCount = Mathf.Clamp(candidateCount, 3, 12);
        minimumSkill = Mathf.Clamp(minimumSkill, 0, 100);
        maximumSkill = Mathf.Clamp(maximumSkill, minimumSkill, 100);
        minimumSalaryCentsPerService = Math.Max(0L, minimumSalaryCentsPerService);
        maximumSalaryCentsPerService = Math.Max(
            minimumSalaryCentsPerService,
            maximumSalaryCentsPerService);
        minimumExperiencePoints = Math.Max(0L, minimumExperiencePoints);
        maximumExperiencePoints = Math.Max(
            minimumExperiencePoints,
            maximumExperiencePoints);
    }
#endif
}
