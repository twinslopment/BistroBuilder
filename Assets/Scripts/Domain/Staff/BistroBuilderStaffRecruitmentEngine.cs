using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Reglas puras del mercado de contratación 4B. La generación es determinista
/// para un mismo día, secuencia y perfil; no depende de UnityEngine.Random.
/// </summary>
public static class BistroBuilderStaffRecruitmentEngine
{
    private const string CandidatePrefix = "cand_";

    public static BistroBuilderStaffRecruitmentSnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderStaffRecruitmentSnapshot
        {
            schemaId = BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaVersion,
            revision = 1L,
            generationSequence = 0,
            lastRefreshDayIndex = 0,
            candidates = new List<BistroBuilderStaffCandidateRecord>()
        };
    }

    public static bool TryGenerateInitialMarket(
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        int dayIndex,
        out BistroBuilderStaffRecruitmentSnapshot result,
        out string error)
    {
        BistroBuilderStaffRecruitmentSnapshot empty = CreateEmptySnapshot();
        return TryRefreshMarket(
            empty,
            profile,
            roleCatalog,
            dayIndex,
            true,
            out result,
            out error);
    }

    /// <summary>
    /// Genera una nueva tanda. Tras la inicial, solo permite un refresco por
    /// día de juego para impedir rerolls ilimitados sin coste ni significado.
    /// </summary>
    public static bool TryRefreshMarket(
        BistroBuilderStaffRecruitmentSnapshot snapshot,
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        int dayIndex,
        bool allowInitialGeneration,
        out BistroBuilderStaffRecruitmentSnapshot result,
        out string error)
    {
        result = null;
        if (dayIndex < 1)
        {
            error = "El día de generación de candidatos no es válido.";
            return false;
        }

        if (!TryValidateSnapshot(snapshot, profile, roleCatalog, true, out error))
        {
            return false;
        }

        bool isInitial = snapshot.generationSequence == 0;
        if (isInitial && !allowInitialGeneration)
        {
            error = "El mercado todavía no ha sido inicializado.";
            return false;
        }

        if (!isInitial && snapshot.lastRefreshDayIndex >= dayIndex)
        {
            error = "El mercado de Personal ya se ha refrescado en este día.";
            return false;
        }

        int nextSequence;
        long nextRevision;
        try
        {
            nextSequence = checked(snapshot.generationSequence + 1);
            nextRevision = checked(snapshot.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La secuencia o revisión del mercado ha desbordado su rango.";
            return false;
        }

        if (!TryGenerateCandidates(
                profile,
                roleCatalog,
                dayIndex,
                nextSequence,
                out List<BistroBuilderStaffCandidateRecord> candidates,
                out error))
        {
            return false;
        }

        result = new BistroBuilderStaffRecruitmentSnapshot
        {
            schemaId = BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaVersion,
            revision = nextRevision,
            generationSequence = nextSequence,
            lastRefreshDayIndex = dayIndex,
            candidates = candidates
        };

        return TryValidateSnapshot(result, profile, roleCatalog, false, out error);
    }

    public static bool TryRemoveCandidate(
        BistroBuilderStaffRecruitmentSnapshot snapshot,
        string candidateId,
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffRecruitmentSnapshot result,
        out BistroBuilderStaffCandidateRecord removed,
        out string error)
    {
        result = null;
        removed = null;
        if (!TryValidateSnapshot(snapshot, profile, roleCatalog, false, out error))
        {
            return false;
        }

        string normalized = NormalizeCandidateId(candidateId);
        if (!IsValidCandidateId(normalized))
        {
            error = "CandidateId no es válido.";
            return false;
        }

        int index = -1;
        for (int current = 0; current < snapshot.candidates.Count; current++)
        {
            BistroBuilderStaffCandidateRecord candidate = snapshot.candidates[current];
            if (candidate != null && string.Equals(
                    NormalizeCandidateId(candidate.candidateId),
                    normalized,
                    StringComparison.Ordinal))
            {
                index = current;
                break;
            }
        }

        if (index < 0)
        {
            error = "El candidato ya no pertenece al mercado actual.";
            return false;
        }

        try
        {
            result = snapshot.DeepClone();
            removed = result.candidates[index].DeepClone();
            result.candidates.RemoveAt(index);
            result.revision = checked(result.revision + 1L);
        }
        catch (OverflowException)
        {
            result = null;
            removed = null;
            error = "La revisión del mercado ha desbordado su rango.";
            return false;
        }

        return TryValidateSnapshot(result, profile, roleCatalog, false, out error);
    }

    public static bool TryFindCandidate(
        BistroBuilderStaffRecruitmentSnapshot snapshot,
        string candidateId,
        out BistroBuilderStaffCandidateRecord candidate)
    {
        candidate = null;
        if (snapshot == null || snapshot.candidates == null)
        {
            return false;
        }

        string normalized = NormalizeCandidateId(candidateId);
        if (!IsValidCandidateId(normalized))
        {
            return false;
        }

        for (int index = 0; index < snapshot.candidates.Count; index++)
        {
            BistroBuilderStaffCandidateRecord current = snapshot.candidates[index];
            if (current != null && string.Equals(
                    NormalizeCandidateId(current.candidateId),
                    normalized,
                    StringComparison.Ordinal))
            {
                candidate = current.DeepClone();
                return true;
            }
        }

        return false;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderStaffRecruitmentSnapshot snapshot,
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        bool allowNeverGenerated,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(
                snapshot.schemaId,
                BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion !=
                BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L ||
            snapshot.generationSequence < 0 ||
            snapshot.lastRefreshDayIndex < 0 ||
            snapshot.candidates == null)
        {
            error = "staff.recruitment.state contiene cabecera o colección inválida.";
            return false;
        }

        if (profile == null)
        {
            error = "Falta el perfil de contratación.";
            return false;
        }

        if (!profile.TryValidate(roleCatalog, out error))
        {
            return false;
        }

        if (snapshot.generationSequence == 0)
        {
            if (!allowNeverGenerated ||
                snapshot.lastRefreshDayIndex != 0 ||
                snapshot.candidates.Count != 0)
            {
                error = "Un mercado no generado contiene estado de candidatos.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (snapshot.lastRefreshDayIndex < 1 ||
            snapshot.candidates.Count > profile.CandidateCount)
        {
            error = "El mercado generado contiene día o número de candidatos inválido.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.candidates.Count; index++)
        {
            BistroBuilderStaffCandidateRecord candidate = snapshot.candidates[index];
            if (!TryValidateCandidate(candidate, profile, roleCatalog, out error))
            {
                return false;
            }

            string id = NormalizeCandidateId(candidate.candidateId);
            if (!ids.Add(id))
            {
                error = "El mercado repite CandidateId.";
                return false;
            }

            string signature = BuildDecisionSignature(candidate);
            if (!signatures.Add(signature))
            {
                error = "El mercado contiene dos candidatos decisionalmente idénticos.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateCandidate(
        BistroBuilderStaffCandidateRecord candidate,
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out string error)
    {
        if (candidate == null ||
            !IsValidCandidateId(candidate.candidateId) ||
            string.IsNullOrWhiteSpace(candidate.firstName) ||
            candidate.firstName.Trim().Length > 48 ||
            candidate.lastName == null || candidate.lastName.Trim().Length > 64 ||
            candidate.expectedSalaryCentsPerService <
                profile.MinimumSalaryCentsPerService ||
            candidate.expectedSalaryCentsPerService >
                profile.MaximumSalaryCentsPerService ||
            candidate.experiencePoints < profile.MinimumExperiencePoints ||
            candidate.experiencePoints > profile.MaximumExperiencePoints ||
            candidate.generatedDayIndex < 1 ||
            candidate.revision < 1L ||
            candidate.skills == null ||
            !Enum.IsDefined(
                typeof(BistroBuilderStaffCandidateProfile),
                candidate.profile))
        {
            error = "El mercado contiene un candidato con datos básicos inválidos.";
            return false;
        }

        if (!IsSkillInProfile(candidate.skills.speed, profile) ||
            !IsSkillInProfile(candidate.skills.attentiveness, profile) ||
            !IsSkillInProfile(candidate.skills.organization, profile) ||
            !IsSkillInProfile(candidate.skills.hospitality, profile))
        {
            error = "El candidato contiene habilidades fuera del rango del mercado.";
            return false;
        }

        string roleId = BistroBuilderStaffStableIdUtility.Normalize(candidate.roleId);
        if (roleCatalog == null ||
            !roleCatalog.TryGetRole(roleId, out BistroBuilderStaffRoleDefinition role) ||
            role == null || !role.active ||
            !ProfileAllowsRole(profile, roleId))
        {
            error = "El candidato referencia un rol no contrat-able por este mercado.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string NormalizeCandidateId(string candidateId)
    {
        return string.IsNullOrWhiteSpace(candidateId)
            ? string.Empty
            : candidateId.Trim().ToLowerInvariant();
    }

    public static bool IsValidCandidateId(string candidateId)
    {
        string normalized = NormalizeCandidateId(candidateId);
        if (!normalized.StartsWith(CandidatePrefix, StringComparison.Ordinal) ||
            normalized.Length != CandidatePrefix.Length + 32)
        {
            return false;
        }

        for (int index = CandidatePrefix.Length; index < normalized.Length; index++)
        {
            char value = normalized[index];
            bool hex = (value >= '0' && value <= '9') ||
                       (value >= 'a' && value <= 'f');
            if (!hex)
            {
                return false;
            }
        }

        return !string.Equals(
            normalized,
            CandidatePrefix + "00000000000000000000000000000000",
            StringComparison.Ordinal);
    }

    private static bool TryGenerateCandidates(
        BistroBuilderStaffRecruitmentProfile profile,
        BistroBuilderStaffRoleCatalog roleCatalog,
        int dayIndex,
        int generationSequence,
        out List<BistroBuilderStaffCandidateRecord> candidates,
        out string error)
    {
        candidates = new List<BistroBuilderStaffCandidateRecord>(
            profile.CandidateCount);

        ulong seed = BuildSeed(
            dayIndex,
            generationSequence,
            profile.DeterministicSalt);
        var random = new DeterministicRandom(seed);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < profile.CandidateCount; index++)
        {
            bool generated = false;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string firstName = profile.FirstNames[
                    random.NextInt(profile.FirstNames.Count)].Trim();
                string lastName = profile.LastNames[
                    random.NextInt(profile.LastNames.Count)].Trim();
                string nameKey = firstName + "|" + lastName;
                if (!names.Add(nameKey))
                {
                    continue;
                }

                string roleId = BistroBuilderStaffStableIdUtility.Normalize(
                    profile.EnabledRoleIds[
                        random.NextInt(profile.EnabledRoleIds.Count)]);

                int speed = random.NextIntInclusive(
                    profile.MinimumSkill,
                    profile.MaximumSkill);
                int attentiveness = random.NextIntInclusive(
                    profile.MinimumSkill,
                    profile.MaximumSkill);
                int organization = random.NextIntInclusive(
                    profile.MinimumSkill,
                    profile.MaximumSkill);
                int hospitality = random.NextIntInclusive(
                    profile.MinimumSkill,
                    profile.MaximumSkill);

                long experience = random.NextLongInclusive(
                    profile.MinimumExperiencePoints,
                    profile.MaximumExperiencePoints);
                var skills = new BistroBuilderEmployeeSkillSet
                {
                    speed = speed,
                    attentiveness = attentiveness,
                    organization = organization,
                    hospitality = hospitality
                };

                int skillAverage =
                    (speed + attentiveness + organization + hospitality) / 4;
                long salarySpan =
                    profile.MaximumSalaryCentsPerService -
                    profile.MinimumSalaryCentsPerService;
                long skillRange = Math.Max(
                    1,
                    profile.MaximumSkill - profile.MinimumSkill);
                long experienceRange = Math.Max(
                    1L,
                    profile.MaximumExperiencePoints -
                    profile.MinimumExperiencePoints);
                long normalizedSkill = skillAverage - profile.MinimumSkill;
                long normalizedExperience = experience -
                    profile.MinimumExperiencePoints;

                // 70 % del rango salarial procede de habilidades, 25 % de XP
                // y 5 % de variación acotada para evitar clones perfectos.
                long skillPart = salarySpan * 70L * normalizedSkill /
                    (100L * skillRange);
                long experiencePart = salarySpan * 25L * normalizedExperience /
                    (100L * experienceRange);
                long varianceBand = Math.Max(1L, salarySpan * 5L / 100L);
                long variance = random.NextLongInclusive(
                    -varianceBand,
                    varianceBand);
                long salary = ClampLong(
                    profile.MinimumSalaryCentsPerService +
                    skillPart + experiencePart + variance,
                    profile.MinimumSalaryCentsPerService,
                    profile.MaximumSalaryCentsPerService);

                string candidateId = CreateCandidateId(ref random);
                if (!ids.Add(candidateId))
                {
                    names.Remove(nameKey);
                    continue;
                }

                var candidate = new BistroBuilderStaffCandidateRecord
                {
                    candidateId = candidateId,
                    firstName = firstName,
                    lastName = lastName,
                    roleId = roleId,
                    expectedSalaryCentsPerService = salary,
                    experiencePoints = experience,
                    skills = skills,
                    profile = ResolveProfile(skills),
                    generatedDayIndex = dayIndex,
                    revision = 1L
                };

                string signature = BuildDecisionSignature(candidate);
                if (!signatures.Add(signature))
                {
                    ids.Remove(candidateId);
                    names.Remove(nameKey);
                    continue;
                }

                if (!TryValidateCandidate(
                        candidate,
                        profile,
                        roleCatalog,
                        out error))
                {
                    return false;
                }

                candidates.Add(candidate);
                generated = true;
                break;
            }

            if (!generated)
            {
                error = "No se pudo generar suficiente variedad de candidatos V1.";
                candidates.Clear();
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static BistroBuilderStaffCandidateProfile ResolveProfile(
        BistroBuilderEmployeeSkillSet skills)
    {
        int[] values =
        {
            skills.speed,
            skills.attentiveness,
            skills.organization,
            skills.hospitality
        };

        int bestIndex = 0;
        int best = values[0];
        int second = int.MinValue;
        for (int index = 1; index < values.Length; index++)
        {
            if (values[index] > best)
            {
                second = best;
                best = values[index];
                bestIndex = index;
            }
            else if (values[index] > second)
            {
                second = values[index];
            }
        }

        if (second == int.MinValue)
        {
            second = best;
        }

        if (best - second < 4)
        {
            return BistroBuilderStaffCandidateProfile.Balanced;
        }

        switch (bestIndex)
        {
            case 0: return BistroBuilderStaffCandidateProfile.Fast;
            case 1: return BistroBuilderStaffCandidateProfile.Attentive;
            case 2: return BistroBuilderStaffCandidateProfile.Organized;
            case 3: return BistroBuilderStaffCandidateProfile.Hospitable;
            default: return BistroBuilderStaffCandidateProfile.Balanced;
        }
    }

    private static bool ProfileAllowsRole(
        BistroBuilderStaffRecruitmentProfile profile,
        string roleId)
    {
        for (int index = 0; index < profile.EnabledRoleIds.Count; index++)
        {
            if (string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(
                        profile.EnabledRoleIds[index]),
                    roleId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsSkillInProfile(
        int value,
        BistroBuilderStaffRecruitmentProfile profile)
    {
        return value >= profile.MinimumSkill && value <= profile.MaximumSkill;
    }

    private static string BuildDecisionSignature(
        BistroBuilderStaffCandidateRecord candidate)
    {
        return string.Concat(
            BistroBuilderStaffStableIdUtility.Normalize(candidate.roleId), "|",
            candidate.expectedSalaryCentsPerService.ToString(CultureInfo.InvariantCulture), "|",
            candidate.experiencePoints.ToString(CultureInfo.InvariantCulture), "|",
            candidate.skills.speed.ToString(CultureInfo.InvariantCulture), "|",
            candidate.skills.attentiveness.ToString(CultureInfo.InvariantCulture), "|",
            candidate.skills.organization.ToString(CultureInfo.InvariantCulture), "|",
            candidate.skills.hospitality.ToString(CultureInfo.InvariantCulture));
    }

    private static ulong BuildSeed(
        int dayIndex,
        int generationSequence,
        int salt)
    {
        unchecked
        {
            ulong value = 1469598103934665603UL;
            MixInt(ref value, dayIndex);
            MixInt(ref value, generationSequence);
            MixInt(ref value, salt);
            return value == 0UL ? 0x9E3779B97F4A7C15UL : value;
        }
    }

    private static void MixInt(ref ulong hash, int value)
    {
        unchecked
        {
            uint unsigned = (uint)value;
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(unsigned >> shift);
                hash *= 1099511628211UL;
            }
        }
    }

    private static string CreateCandidateId(ref DeterministicRandom random)
    {
        ulong left = random.NextUInt64();
        ulong right = random.NextUInt64();
        if (left == 0UL && right == 0UL)
        {
            right = 1UL;
        }

        return CandidatePrefix +
               left.ToString("x16", CultureInfo.InvariantCulture) +
               right.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static long ClampLong(long value, long minimum, long maximum)
    {
        if (value < minimum) return minimum;
        if (value > maximum) return maximum;
        return value;
    }

    /// <summary>
    /// PRNG SplitMix64 mínimo y estable para authoring determinista. No se usa
    /// para seguridad ni para IDs persistentes de Employee.
    /// </summary>
    private struct DeterministicRandom
    {
        private ulong state;

        public DeterministicRandom(ulong seed)
        {
            state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            }
            return (int)(NextUInt64() % (ulong)exclusiveMaximum);
        }

        public int NextIntInclusive(int minimum, int maximum)
        {
            if (maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }
            ulong span = (ulong)((long)maximum - minimum + 1L);
            return minimum + (int)(NextUInt64() % span);
        }

        public long NextLongInclusive(long minimum, long maximum)
        {
            if (maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            unchecked
            {
                ulong span = (ulong)(maximum - minimum) + 1UL;
                if (span == 0UL)
                {
                    return (long)NextUInt64();
                }
                return minimum + (long)(NextUInt64() % span);
            }
        }
    }
}
