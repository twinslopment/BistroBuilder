using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Doble de pruebas para verificar la política de despido durante un binding
/// activo sin depender todavía del Waiter real. 4D sustituirá este contrato.
/// </summary>
public sealed class BistroBuilderStaff4BTestAssignmentQuery :
    MonoBehaviour,
    IBistroBuilderStaffSessionAssignmentQuery
{
    public string blockedEmployeeId = string.Empty;

    public bool TryGetActiveAssignment(
        string employeeId,
        out string assignmentReference)
    {
        if (BistroBuilderEmployeeIdUtility.Normalize(employeeId) ==
            BistroBuilderEmployeeIdUtility.Normalize(blockedEmployeeId) &&
            BistroBuilderEmployeeIdUtility.IsValid(blockedEmployeeId))
        {
            assignmentReference = "test.waiter.1";
            return true;
        }

        assignmentReference = string.Empty;
        return false;
    }
}

/// <summary>
/// Autotest 4B: mercado determinista, variedad, contratación atómica, despido
/// histórico y bloqueo mediante contrato de sesión.
/// </summary>
public static class BistroBuilderStaff4BSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4B - Autotest contratación y despido",
        false,
        3212)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4B Personal",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4B de Personal ha fallado.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4B PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog catalog = null;
        BistroBuilderStaffRecruitmentProfile profile = null;
        GameObject host = null;

        try
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderStaffRoleCatalog>();
            catalog.InitializeV1DefaultsIfEmpty();
            profile = ScriptableObject.CreateInstance<
                BistroBuilderStaffRecruitmentProfile>();

            Check(catalog.TryValidate(out string catalogError),
                "Catálogo de roles válido. " + catalogError,
                ref passed, ref failed, log);
            Check(profile.TryValidate(catalog, out string profileError),
                "Perfil de contratación válido. " + profileError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                    profile, catalog, 5,
                    out BistroBuilderStaffRecruitmentSnapshot marketA,
                    out string generationError),
                "Mercado inicial generado. " + generationError,
                ref passed, ref failed, log);
            Check(marketA != null &&
                  marketA.candidates.Count == profile.CandidateCount,
                "Mercado contiene el número configurado de candidatos.",
                ref passed, ref failed, log);
            Check(BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                    marketA, profile, catalog, false, out string marketError),
                "Mercado generado es íntegro. " + marketError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                    profile, catalog, 5,
                    out BistroBuilderStaffRecruitmentSnapshot marketSame,
                    out _) && MarketsEquivalent(marketA, marketSame),
                "Mismo día + perfil produce candidatos deterministas.",
                ref passed, ref failed, log);

            Check(BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                    profile, catalog, 6,
                    out BistroBuilderStaffRecruitmentSnapshot marketOtherDay,
                    out _) && !MarketsEquivalent(marketA, marketOtherDay),
                "Otro día produce un mercado distinto.",
                ref passed, ref failed, log);

            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            var decisionSignatures = new HashSet<string>(StringComparer.Ordinal);
            bool allCandidatesValid = true;
            bool salaryInRange = true;
            bool skillsInRange = true;
            for (int index = 0; index < marketA.candidates.Count; index++)
            {
                BistroBuilderStaffCandidateRecord candidate = marketA.candidates[index];
                allCandidatesValid &= candidate != null &&
                    BistroBuilderStaffRecruitmentEngine.IsValidCandidateId(
                        candidate.candidateId) &&
                    candidateIds.Add(candidate.candidateId);
                salaryInRange &= candidate.expectedSalaryCentsPerService >=
                        profile.MinimumSalaryCentsPerService &&
                    candidate.expectedSalaryCentsPerService <=
                        profile.MaximumSalaryCentsPerService;
                skillsInRange &= candidate.skills != null &&
                    InRange(candidate.skills.speed, profile) &&
                    InRange(candidate.skills.attentiveness, profile) &&
                    InRange(candidate.skills.organization, profile) &&
                    InRange(candidate.skills.hospitality, profile);
                decisionSignatures.Add(BuildSignature(candidate));
            }

            Check(allCandidatesValid,
                "CandidateId son válidos y únicos.",
                ref passed, ref failed, log);
            Check(decisionSignatures.Count == marketA.candidates.Count,
                "No hay candidatos idénticos con distinto nombre.",
                ref passed, ref failed, log);
            Check(salaryInRange,
                "Todos los salarios esperados permanecen en rango V1.",
                ref passed, ref failed, log);
            Check(skillsInRange,
                "Todas las habilidades permanecen en rango V1.",
                ref passed, ref failed, log);

            Check(!BistroBuilderStaffRecruitmentEngine.TryRefreshMarket(
                    marketA, profile, catalog, 5, false,
                    out _, out _),
                "No se permite reroll del mercado el mismo día.",
                ref passed, ref failed, log);
            Check(BistroBuilderStaffRecruitmentEngine.TryRefreshMarket(
                    marketA, profile, catalog, 6, false,
                    out BistroBuilderStaffRecruitmentSnapshot refreshed,
                    out string refreshError),
                "El día siguiente permite refrescar. " + refreshError,
                ref passed, ref failed, log);
            Check(refreshed.generationSequence == marketA.generationSequence + 1 &&
                  refreshed.revision == marketA.revision + 1,
                "Refresco incrementa secuencia y revisión una sola vez.",
                ref passed, ref failed, log);

            BistroBuilderStaffCandidateRecord selected = marketA.candidates[0];
            Check(BistroBuilderStaffRecruitmentEngine.TryRemoveCandidate(
                    marketA,
                    selected.candidateId,
                    profile,
                    catalog,
                    out BistroBuilderStaffRecruitmentSnapshot afterRemoval,
                    out BistroBuilderStaffCandidateRecord removed,
                    out string removalError),
                "Candidato puede retirarse transaccionalmente. " + removalError,
                ref passed, ref failed, log);
            Check(afterRemoval.candidates.Count == marketA.candidates.Count - 1 &&
                  removed.candidateId == selected.candidateId &&
                  marketA.candidates.Count == profile.CandidateCount,
                "Retirada no muta el snapshot anterior.",
                ref passed, ref failed, log);
            Check(!BistroBuilderStaffRecruitmentEngine.TryRemoveCandidate(
                    afterRemoval,
                    selected.candidateId,
                    profile,
                    catalog,
                    out _, out _, out _),
                "Un candidato retirado no puede contratarse dos veces.",
                ref passed, ref failed, log);

            host = new GameObject("BB_4B_SELFTEST_TEMP");
            BistroBuilderGeneralGameStateService general =
                host.AddComponent<BistroBuilderGeneralGameStateService>();
            BistroBuilderStaffService staff =
                host.AddComponent<BistroBuilderStaffService>();
            BistroBuilderStaffRecruitmentService recruitment =
                host.AddComponent<BistroBuilderStaffRecruitmentService>();
            BistroBuilderStaff4BTestAssignmentQuery assignment =
                host.AddComponent<BistroBuilderStaff4BTestAssignmentQuery>();

            AssignObject(staff, "roleCatalog", catalog);
            AssignObject(recruitment, "staffService", staff);
            AssignObject(recruitment, "generalGameStateService", general);
            AssignObject(recruitment, "recruitmentProfile", profile);

            Check(staff.TryInitializeFresh(out string initError),
                "StaffService inicializado para 4B. " + initError,
                ref passed, ref failed, log);
            Check(recruitment.EnsureMarketReady(out string serviceMarketError),
                "RecruitmentService inicializa mercado. " + serviceMarketError,
                ref passed, ref failed, log);

            var serviceCandidates = new List<BistroBuilderStaffCandidateRecord>();
            recruitment.CopyCandidates(serviceCandidates);
            Check(serviceCandidates.Count == profile.CandidateCount,
                "Application expone candidatos mediante copias.",
                ref passed, ref failed, log);

            string hiredCandidateId = serviceCandidates[0].candidateId;
            int hiredEvents = 0;
            recruitment.EmployeeHired += (_, __) => hiredEvents++;
            Check(recruitment.TryHireCandidate(
                    hiredCandidateId,
                    out BistroBuilderEmployeeRecord hired,
                    out string hireError),
                "Contratación convierte candidato en empleado. " + hireError,
                ref passed, ref failed, log);
            Check(hired != null &&
                  BistroBuilderEmployeeIdUtility.IsValid(hired.employeeId) &&
                  !string.Equals(
                      hired.employeeId,
                      hiredCandidateId,
                      StringComparison.Ordinal),
                "EmployeeId nuevo e independiente del CandidateId.",
                ref passed, ref failed, log);
            Check(hiredEvents == 1 && staff.ActiveEmployeeCount == 1,
                "Contratación publica un evento y entra en plantilla activa.",
                ref passed, ref failed, log);
            Check(!recruitment.TryGetCandidate(hiredCandidateId, out _),
                "Candidato contratado desaparece del mercado.",
                ref passed, ref failed, log);
            Check(!recruitment.TryHireCandidate(
                    hiredCandidateId, out _, out _),
                "Repetir contratación no duplica empleado.",
                ref passed, ref failed, log);
            Check(staff.TotalActiveSalaryCentsPerService ==
                    hired.salaryCentsPerService,
                "Coste salarial de plantilla se deriva del contrato del empleado.",
                ref passed, ref failed, log);

            AssignObject(recruitment, "sessionAssignmentQuerySource", assignment);
            assignment.blockedEmployeeId = hired.employeeId;
            Check(!recruitment.TryDismissEmployee(
                    hired.employeeId, out _, out string blockedError) &&
                  !string.IsNullOrWhiteSpace(blockedError),
                "Empleado con binding activo no puede despedirse.",
                ref passed, ref failed, log);
            Check(staff.TryGetEmployee(hired.employeeId, out BistroBuilderEmployeeRecord stillActive) &&
                  stillActive.employmentStatus == BistroBuilderEmploymentStatus.Active,
                "Despido bloqueado no modifica staff.state.",
                ref passed, ref failed, log);

            assignment.blockedEmployeeId = string.Empty;
            int dismissedEvents = 0;
            recruitment.EmployeeDismissed += _ => dismissedEvents++;
            Check(recruitment.TryDismissEmployee(
                    hired.employeeId,
                    out BistroBuilderEmployeeRecord dismissed,
                    out string dismissError),
                "Empleado libre puede despedirse. " + dismissError,
                ref passed, ref failed, log);
            Check(dismissed != null &&
                  dismissed.employeeId == hired.employeeId &&
                  dismissed.employmentStatus == BistroBuilderEmploymentStatus.Dismissed &&
                  dismissed.availability == BistroBuilderEmployeeAvailability.Unavailable,
                "Despido conserva identidad y fuerza estado no disponible.",
                ref passed, ref failed, log);
            Check(staff.ActiveEmployeeCount == 0 && dismissedEvents == 1,
                "Despido retira de plantilla activa sin borrar historial.",
                ref passed, ref failed, log);
            Check(staff.EmployeeCount == 1 &&
                  staff.TryGetEmployee(hired.employeeId, out BistroBuilderEmployeeRecord history) &&
                  history.employmentStatus == BistroBuilderEmploymentStatus.Dismissed,
                "Registro despedido permanece trazable por EmployeeId.",
                ref passed, ref failed, log);
            Check(!recruitment.TryDismissEmployee(
                    hired.employeeId, out _, out _),
                "Un empleado ya despedido no puede despedirse otra vez.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción no controlada: " + exception);
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        log.AppendLine();
        log.AppendLine("Autotest: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static bool MarketsEquivalent(
        BistroBuilderStaffRecruitmentSnapshot left,
        BistroBuilderStaffRecruitmentSnapshot right)
    {
        if (left == null || right == null ||
            left.candidates == null || right.candidates == null ||
            left.candidates.Count != right.candidates.Count)
        {
            return false;
        }

        for (int index = 0; index < left.candidates.Count; index++)
        {
            BistroBuilderStaffCandidateRecord a = left.candidates[index];
            BistroBuilderStaffCandidateRecord b = right.candidates[index];
            if (a == null || b == null ||
                a.candidateId != b.candidateId ||
                a.FullName != b.FullName ||
                BuildSignature(a) != BuildSignature(b))
            {
                return false;
            }
        }
        return true;
    }

    private static bool InRange(
        int value,
        BistroBuilderStaffRecruitmentProfile profile)
    {
        return value >= profile.MinimumSkill && value <= profile.MaximumSkill;
    }

    private static string BuildSignature(BistroBuilderStaffCandidateRecord candidate)
    {
        return candidate.roleId + "|" +
               candidate.expectedSalaryCentsPerService + "|" +
               candidate.experiencePoints + "|" +
               candidate.skills.speed + "|" +
               candidate.skills.attentiveness + "|" +
               candidate.skills.organization + "|" +
               candidate.skills.hospitality;
    }

    private static void AssignObject(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            log.AppendLine("[FALLO] " + message);
        }
    }
}
