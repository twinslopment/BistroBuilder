using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de perfiles individuales de cliente. La generación es
/// determinista: un mismo cliente/grupo restaurado conserva su personalidad.
/// </summary>
public static class BistroBuilderAdvancedCustomerProfileEngine
{
    public const int MaximumArchetypes = 32;
    public const int MaximumMembersPerGroup = 32;
    public const int MinimumFoodWaitToleranceBasisPoints = 7000;
    public const int MaximumFoodWaitToleranceBasisPoints = 40000;
    private const int PersonalVarianceBasisPoints = 1200;
    private static readonly string[] FirstNames =
    {
        "Lucía", "Carlos", "Elena", "Diego", "Marta", "Pablo",
        "Sara", "Álvaro", "Irene", "Javier", "Claudia", "Hugo",
        "Paula", "Daniel", "Nuria", "Marcos", "Laura", "Adrián"
    };
    private static readonly string[] Surnames =
    {
        "Martín", "García", "Alonso", "Pérez", "Fernández", "Suárez",
        "López", "Ramos", "Santos", "Vega", "Romero", "Castro"
    };
    private const BistroBuilderCustomerSpecialNeed AllSpecialNeeds =
        BistroBuilderCustomerSpecialNeed.AccessibleSeating |
        BistroBuilderCustomerSpecialNeed.QuietSeating |
        BistroBuilderCustomerSpecialNeed.DietaryAwareness;

    public static bool TryValidateCatalog(
        IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> definitions,
        out string error)
    {
        if (definitions == null || definitions.Count < 2 ||
            definitions.Count > MaximumArchetypes)
        {
            error = "El catálogo avanzado necesita entre 2 y 32 arquetipos.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (!TryValidateDefinition(definitions[i], out error) ||
                !ids.Add(NormalizeId(definitions[i].archetypeId)))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "El catálogo contiene un arquetipo duplicado.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryBuildGroupProfile(
        int groupId,
        int groupSize,
        int dayIndex,
        BistroBuilderCustomerAcquisitionProfile acquisition,
        IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> definitions,
        out BistroBuilderAdvancedCustomerGroupProfile profile,
        out string error)
    {
        profile = null;
        error = string.Empty;
        if (groupId < 1 || groupSize < 1 || groupSize > MaximumMembersPerGroup ||
            dayIndex < 1 || acquisition == null ||
            !acquisition.TryValidate(out error) ||
            !TryValidateCatalog(definitions, out error))
            return false;

        string segment = NormalizeId(acquisition.segmentId);
        string returningReference = acquisition.returningVisit
            ? NormalizeId(acquisition.guestRelationsReferenceId)
            : string.Empty;
        string identitySeed = acquisition.returningVisit
            ? returningReference
            : "visit.day" + dayIndex.ToString("D4") +
              ".group" + groupId.ToString("D6");

        profile = new BistroBuilderAdvancedCustomerGroupProfile
        {
            groupId = groupId,
            dayIndex = dayIndex,
            segmentId = segment,
            returningVisit = acquisition.returningVisit,
            returningReferenceId = returningReference
        };
        for (int memberIndex = 1; memberIndex <= groupSize; memberIndex++)
        {
            uint seed = StableHash(identitySeed + "|" + segment + "|" + memberIndex);
            BistroBuilderAdvancedCustomerArchetypeDefinition definition =
                SelectArchetype(definitions, segment, seed);
            if (definition == null)
            {
                error = "No pudo resolverse un arquetipo para el cliente.";
                profile = null;
                return false;
            }

            string customerId = acquisition.returningVisit
                ? returningReference + ".member" + memberIndex.ToString("D2")
                : identitySeed + ".member" + memberIndex.ToString("D2");
            profile.members.Add(BuildMember(
                customerId,
                memberIndex,
                definition,
                seed));
        }

        return TryValidateGroupProfile(profile, groupSize, out error);
    }

    public static bool TryValidateGroupProfile(
        BistroBuilderAdvancedCustomerGroupProfile profile,
        int expectedGroupSize,
        out string error)
    {
        if (profile == null ||
            profile.schemaVersion != BistroBuilderAdvancedCustomerGroupProfile.CurrentSchemaVersion ||
            profile.groupId < 1 || profile.dayIndex < 1 ||
            expectedGroupSize < 1 || expectedGroupSize > MaximumMembersPerGroup ||
            !IsSafeId(NormalizeId(profile.segmentId)) ||
            profile.members == null || profile.members.Count != expectedGroupSize)
        {
            error = "El perfil avanzado del grupo contiene una cabecera inválida.";
            return false;
        }

        if (profile.returningVisit != !string.IsNullOrWhiteSpace(profile.returningReferenceId) ||
            (profile.returningVisit && !IsSafeId(NormalizeId(profile.returningReferenceId))))
        {
            error = "La identidad de retorno del grupo es incoherente.";
            return false;
        }

        var memberIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < profile.members.Count; i++)
        {
            BistroBuilderAdvancedCustomerMemberProfile member = profile.members[i];
            if (!TryValidateMember(member, out error) ||
                member.memberIndex != i + 1 ||
                !memberIds.Add(NormalizeId(member.customerId)))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "El grupo contiene miembros duplicados o fuera de orden.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static int ComputePatiencePressureBasisPoints(
        float elapsedSeconds,
        float toleranceSeconds)
    {
        if (!FiniteNonNegative(elapsedSeconds) ||
            !FinitePositive(toleranceSeconds))
            return 10000;
        if (elapsedSeconds <= toleranceSeconds * 0.60f)
            return 0;
        double normalized = (elapsedSeconds - toleranceSeconds * 0.60d) /
            Math.Max(0.001d, toleranceSeconds * 0.90d);
        return ClampBasisPoints((int)Math.Round(
            normalized * 10000d, MidpointRounding.AwayFromZero));
    }

    public static int ComputeCategoryPreferenceBasisPoints(
        BistroBuilderAdvancedCustomerMemberProfile member,
        string categoryId)
    {
        if (member == null) return 0;
        string normalized = NormalizeId(categoryId);
        if (!IsSafeId(normalized)) return 0;
        if (ContainsId(member.avoidedDishCategoryIds, normalized)) return -2500;
        if (ContainsId(member.preferredDishCategoryIds, normalized)) return 1500;
        return 0;
    }

    private static bool TryValidateDefinition(
        BistroBuilderAdvancedCustomerArchetypeDefinition definition,
        out string error)
    {
        if (definition == null ||
            !IsSafeId(NormalizeId(definition.archetypeId)) ||
            string.IsNullOrWhiteSpace(definition.displayName) ||
            definition.baseWeight < 1 || definition.baseWeight > 10000 ||
            definition.segmentAffinityBonusWeight < 0 ||
            definition.segmentAffinityBonusWeight > 20000 ||
            !FinitePositive(definition.tableWaitToleranceSeconds) ||
            !FinitePositive(definition.waiterWaitToleranceSeconds) ||
            !FinitePositive(definition.billWaitToleranceSeconds) ||
            definition.tableWaitToleranceSeconds > 900f ||
            definition.waiterWaitToleranceSeconds > 900f ||
            definition.billWaitToleranceSeconds > 900f ||
            definition.foodWaitToleranceBasisPoints < MinimumFoodWaitToleranceBasisPoints ||
            definition.foodWaitToleranceBasisPoints > MaximumFoodWaitToleranceBasisPoints ||
            !ValidSensitivity(definition.serviceSensitivityBasisPoints) ||
            !ValidSensitivity(definition.qualitySensitivityBasisPoints) ||
            !ValidSensitivity(definition.priceSensitivityBasisPoints) ||
            !ValidSensitivity(definition.ambienceSensitivityBasisPoints) ||
            HasInvalidSpecialNeeds(definition.specialNeeds))
        {
            error = "Existe un arquetipo con identidad o parámetros fuera de rango.";
            return false;
        }

        if (!ValidateIdList(definition.segmentAffinityIds, false, out error) ||
            !ValidateIdList(definition.preferredDishCategoryIds, true, out error) ||
            !ValidateIdList(definition.avoidedDishCategoryIds, true, out error))
            return false;

        if (!string.IsNullOrWhiteSpace(definition.preferredZoneTagId) &&
            !IsSafeId(NormalizeId(definition.preferredZoneTagId)))
        {
            error = "El arquetipo contiene una zona preferida inválida.";
            return false;
        }

        if (definition.preferredDishCategoryIds != null &&
            definition.avoidedDishCategoryIds != null)
        {
            for (int i = 0; i < definition.preferredDishCategoryIds.Count; i++)
            {
                string id = NormalizeId(definition.preferredDishCategoryIds[i]);
                if (ContainsId(definition.avoidedDishCategoryIds, id))
                {
                    error = "Una categoría no puede ser preferida y evitada a la vez.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateMember(
        BistroBuilderAdvancedCustomerMemberProfile member,
        out string error)
    {
        if (member == null || member.memberIndex < 1 ||
            !IsSafeId(NormalizeId(member.customerId)) ||
            !IsSafeId(NormalizeId(member.archetypeId)) ||
            !FinitePositive(member.tableWaitToleranceSeconds) ||
            !FinitePositive(member.waiterWaitToleranceSeconds) ||
            !FinitePositive(member.billWaitToleranceSeconds) ||
            member.foodWaitToleranceBasisPoints < MinimumFoodWaitToleranceBasisPoints ||
            member.foodWaitToleranceBasisPoints > MaximumFoodWaitToleranceBasisPoints ||
            !ValidSensitivity(member.serviceSensitivityBasisPoints) ||
            !ValidSensitivity(member.qualitySensitivityBasisPoints) ||
            !ValidSensitivity(member.priceSensitivityBasisPoints) ||
            !ValidSensitivity(member.ambienceSensitivityBasisPoints) ||
            HasInvalidSpecialNeeds(member.specialNeeds))
        {
            error = "Existe un perfil individual inválido.";
            return false;
        }

        if (!ValidateIdList(member.preferredDishCategoryIds, true, out error) ||
            !ValidateIdList(member.avoidedDishCategoryIds, true, out error))
            return false;

        if (!string.IsNullOrWhiteSpace(member.preferredZoneTagId) &&
            !IsSafeId(NormalizeId(member.preferredZoneTagId)))
        {
            error = "El perfil individual contiene una zona preferida inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderAdvancedCustomerArchetypeDefinition SelectArchetype(
        IReadOnlyList<BistroBuilderAdvancedCustomerArchetypeDefinition> definitions,
        string segmentId,
        uint seed)
    {
        long total = 0L;
        for (int i = 0; i < definitions.Count; i++)
            total += ResolveWeight(definitions[i], segmentId);
        if (total <= 0L) return null;

        long roll = seed % (uint)Math.Min(uint.MaxValue, total);
        long cursor = 0L;
        for (int i = 0; i < definitions.Count; i++)
        {
            cursor += ResolveWeight(definitions[i], segmentId);
            if (roll < cursor) return definitions[i];
        }
        return definitions[definitions.Count - 1];
    }

    private static int ResolveWeight(
        BistroBuilderAdvancedCustomerArchetypeDefinition definition,
        string segmentId)
    {
        int weight = definition != null ? definition.baseWeight : 0;
        if (definition?.segmentAffinityIds != null &&
            ContainsId(definition.segmentAffinityIds, segmentId))
            weight += definition.segmentAffinityBonusWeight;
        return Math.Max(0, weight);
    }

    private static BistroBuilderAdvancedCustomerMemberProfile BuildMember(
        string customerId,
        int memberIndex,
        BistroBuilderAdvancedCustomerArchetypeDefinition definition,
        uint seed)
    {
        int variance = SignedVariance(seed ^ 0x9E3779B9u);
        return new BistroBuilderAdvancedCustomerMemberProfile
        {
            customerId = NormalizeId(customerId),
            displayName = BuildDisplayName(seed),
            memberIndex = memberIndex,
            archetypeId = NormalizeId(definition.archetypeId),
            tableWaitToleranceSeconds = ScaleFloat(
                definition.tableWaitToleranceSeconds, variance),
            waiterWaitToleranceSeconds = ScaleFloat(
                definition.waiterWaitToleranceSeconds,
                SignedVariance(seed ^ 0x85EBCA6Bu)),
            foodWaitToleranceBasisPoints = ScaleInt(
                definition.foodWaitToleranceBasisPoints,
                SignedVariance(seed ^ 0xC2B2AE35u),
                MinimumFoodWaitToleranceBasisPoints,
                MaximumFoodWaitToleranceBasisPoints),
            billWaitToleranceSeconds = ScaleFloat(
                definition.billWaitToleranceSeconds,
                SignedVariance(seed ^ 0x27D4EB2Fu)),
            serviceSensitivityBasisPoints = ScaleInt(
                definition.serviceSensitivityBasisPoints,
                SignedVariance(seed ^ 0x165667B1u), 0, 10000),
            qualitySensitivityBasisPoints = ScaleInt(
                definition.qualitySensitivityBasisPoints,
                SignedVariance(seed ^ 0xD3A2646Cu), 0, 10000),
            priceSensitivityBasisPoints = ScaleInt(
                definition.priceSensitivityBasisPoints,
                SignedVariance(seed ^ 0xFD7046C5u), 0, 10000),
            ambienceSensitivityBasisPoints = ScaleInt(
                definition.ambienceSensitivityBasisPoints,
                SignedVariance(seed ^ 0xB55A4F09u), 0, 10000),
            preferredDishCategoryIds = definition.preferredDishCategoryIds != null
                ? new List<string>(definition.preferredDishCategoryIds)
                : new List<string>(),
            avoidedDishCategoryIds = definition.avoidedDishCategoryIds != null
                ? new List<string>(definition.avoidedDishCategoryIds)
                : new List<string>(),
            preferredZoneTagId = NormalizeId(definition.preferredZoneTagId),
            specialNeeds = definition.specialNeeds | ResolveIncidentalSpecialNeeds(seed)
        };
    }

    private static string BuildDisplayName(uint seed)
    {
        string first = FirstNames[seed % (uint)FirstNames.Length];
        uint surnameSeed = seed ^ 0x7F4A7C15u;
        string surname = Surnames[surnameSeed % (uint)Surnames.Length];
        return first + " " + surname;
    }
    private static BistroBuilderCustomerSpecialNeed ResolveIncidentalSpecialNeeds(uint seed)
    {
        BistroBuilderCustomerSpecialNeed result = BistroBuilderCustomerSpecialNeed.None;
        if (((seed ^ 0xA24BAED5u) % 10000u) < 300u)
            result |= BistroBuilderCustomerSpecialNeed.AccessibleSeating;
        if (((seed ^ 0x9FB21C65u) % 10000u) < 700u)
            result |= BistroBuilderCustomerSpecialNeed.QuietSeating;
        if (((seed ^ 0xC13FA9A9u) % 10000u) < 600u)
            result |= BistroBuilderCustomerSpecialNeed.DietaryAwareness;
        return result;
    }

    private static bool HasInvalidSpecialNeeds(BistroBuilderCustomerSpecialNeed value) =>
        (value & ~AllSpecialNeeds) != 0;

    private static int SignedVariance(uint seed)
    {
        int span = PersonalVarianceBasisPoints * 2 + 1;
        return (int)(seed % (uint)span) - PersonalVarianceBasisPoints;
    }

    private static float ScaleFloat(float value, int varianceBasisPoints)
    {
        return (float)Math.Max(1d,
            value * (1d + varianceBasisPoints / 10000d));
    }

    private static int ScaleInt(
        int value,
        int varianceBasisPoints,
        int min,
        int max)
    {
        int scaled = (int)Math.Round(
            value * (1d + varianceBasisPoints / 10000d),
            MidpointRounding.AwayFromZero);
        return Math.Max(min, Math.Min(max, scaled));
    }

    private static bool ValidateIdList(
        IReadOnlyList<string> values,
        bool menuCategory,
        out string error)
    {
        if (values == null)
        {
            error = "Una lista de afinidades/preferencias es nula.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string normalized = NormalizeId(values[i]);
            bool valid = menuCategory
                ? BistroBuilderMenuIdUtility.IsValidStableId(normalized)
                : IsSafeId(normalized);
            if (!valid || !ids.Add(normalized))
            {
                error = "Existe una afinidad/preferencia inválida o duplicada.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static bool ContainsId(IReadOnlyList<string> values, string id)
    {
        if (values == null || string.IsNullOrWhiteSpace(id)) return false;
        for (int i = 0; i < values.Count; i++)
            if (string.Equals(NormalizeId(values[i]), id, StringComparison.Ordinal))
                return true;
        return false;
    }

    public static string NormalizeId(string value) =>
        BistroBuilderGuestRelationsEngine.NormalizeId(value);

    public static bool IsSafeId(string value) =>
        BistroBuilderGuestRelationsEngine.IsSafeId(value);

    private static bool ValidSensitivity(int value) => value >= 0 && value <= 10000;
    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    private static int ClampBasisPoints(int value) => Math.Max(0, Math.Min(10000, value));

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string safe = value ?? string.Empty;
            for (int i = 0; i < safe.Length; i++)
            {
                hash ^= safe[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
