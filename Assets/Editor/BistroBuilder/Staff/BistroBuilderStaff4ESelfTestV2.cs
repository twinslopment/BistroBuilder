using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro 4E v2. Comprueba contratos de persistencia de Personal sin
/// escribir saves ni depender de Play Mode: IDs/versiones, DeepClone,
/// validación cruzada staff.state ↔ staff.session.runtime y orden relativo de
/// las secciones frente a service.runtime.
/// </summary>
public static class BistroBuilderStaff4ESelfTestV2
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E v2 - Autotest persistencia",
        false,
        3242)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E v2",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4E v2 de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4E v2 PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog roleCatalog = null;
        BistroBuilderStaffRecruitmentProfile recruitmentProfile = null;

        try
        {
            roleCatalog = ScriptableObject.CreateInstance<
                BistroBuilderStaffRoleCatalog>();
            roleCatalog.InitializeV1DefaultsIfEmpty();
            recruitmentProfile = ScriptableObject.CreateInstance<
                BistroBuilderStaffRecruitmentProfile>();

            Check(roleCatalog.TryValidate(out string roleError),
                "Catálogo de roles válido. " + roleError,
                ref passed, ref failed, log);
            Check(recruitmentProfile.TryValidate(
                    roleCatalog,
                    out string recruitmentError),
                "Perfil de contratación válido. " + recruitmentError,
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffStateSaveSectionProvider.StableSectionId ==
                    "staff.state" &&
                BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId ==
                    "staff.recruitment" &&
                BistroBuilderStaffSessionSaveSectionProvider.StableSectionId ==
                    "staff.session.runtime" &&
                BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId ==
                    "staff.recruitment.state",
                "Los SectionId públicos son estables y el schema interno de mercado permanece separado.",
                ref passed, ref failed, log);

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal)
            {
                BistroBuilderStaffStateSaveSectionProvider.StableSectionId,
                BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId,
                BistroBuilderStaffSessionSaveSectionProvider.StableSectionId
            };
            Check(uniqueIds.Count == 3,
                "staff.state, staff.recruitment y staff.session.runtime no colisionan.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffStateSaveSectionProvider.StableSectionVersion ==
                    BistroBuilderStaffSnapshot.CurrentSchemaVersion &&
                BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionVersion ==
                    BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaVersion &&
                BistroBuilderStaffSessionSaveSectionProvider.StableSectionVersion ==
                    BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
                "Las versiones de provider siguen las versiones de dominio.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot staff =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var createRequest = new BistroBuilderEmployeeCreateRequest
            {
                firstName = "Marta",
                lastName = "Vega",
                roleId = "waiter",
                salaryCentsPerService = 8200L,
                hiredDayIndex = 3,
                initialExperiencePoints = 120L,
                initialSkills = new BistroBuilderEmployeeSkillSet
                {
                    speed = 53,
                    attentiveness = 57,
                    organization = 51,
                    hospitality = 59
                },
                availability = BistroBuilderEmployeeAvailability.Available,
                responsibilities =
                    new BistroBuilderEmployeeResponsibilitySettings()
            };

            BistroBuilderEmployeeRecord employee = null;
            string buildError = string.Empty;
            BistroBuilderStaffSnapshot populatedStaff = null;
            string appendError = string.Empty;
            Check(BistroBuilderStaffEngine.TryBuildEmployee(
                    employeeId,
                    createRequest,
                    roleCatalog,
                    out employee,
                    out buildError) &&
                  BistroBuilderStaffEngine.TryAppendEmployee(
                    staff,
                    employee,
                    roleCatalog,
                    out populatedStaff,
                    out appendError),
                "staff.state de prueba construido. " + buildError + appendError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffEngine.TryValidateSnapshot(
                    populatedStaff,
                    roleCatalog,
                    out string staffError),
                "staff.state válido antes de persistir. " + staffError,
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot staffClone = populatedStaff.DeepClone();
            staffClone.employees[0].firstName = "Mutado";
            staffClone.employees[0].skills.speed = 99;
            Check(populatedStaff.employees[0].firstName == "Marta" &&
                  populatedStaff.employees[0].skills.speed == 53,
                "DeepClone de staff.state no comparte identidad ni Skills.",
                ref passed, ref failed, log);

            Check(BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                    recruitmentProfile,
                    roleCatalog,
                    3,
                    out BistroBuilderStaffRecruitmentSnapshot market,
                    out string marketError),
                "staff.recruitment generado de forma válida. " + marketError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                    market,
                    recruitmentProfile,
                    roleCatalog,
                    false,
                    out string marketValidationError),
                "staff.recruitment válido antes de persistir. " +
                marketValidationError,
                ref passed, ref failed, log);

            BistroBuilderStaffRecruitmentSnapshot marketClone = market.DeepClone();
            string originalCandidateName = market.candidates[0].firstName;
            marketClone.candidates[0].firstName = "Mutado";
            Check(market.candidates[0].firstName == originalCandidateName,
                "DeepClone de staff.recruitment aísla candidatos.",
                ref passed, ref failed, log);

            string sessionId = BistroBuilderStaffSessionIdUtility.CreateNew();
            var session = new BistroBuilderStaffSessionSnapshot
            {
                schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
                revision = 1L,
                active = true,
                sessionId = sessionId,
                dayIndex = 3,
                bindings = new List<BistroBuilderStaffSessionBindingRecord>
                {
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = employeeId,
                        waiterId = 1,
                        completedTasks = 4,
                        failedTasks = 0,
                        totalTaskDurationMilliseconds = 95000L,
                        handledTableIds = new List<int> { 2, 5 }
                    }
                }
            };

            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    session,
                    populatedStaff,
                    out string sessionError),
                "staff.session.runtime válido contra staff.state. " + sessionError,
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot sessionClone = session.DeepClone();
            sessionClone.bindings[0].handledTableIds.Add(9);
            sessionClone.bindings[0].completedTasks = 99;
            Check(session.bindings[0].handledTableIds.Count == 2 &&
                  session.bindings[0].completedTasks == 4,
                "DeepClone de staff.session.runtime aísla métricas y mesas.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot missingEmployee =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    session,
                    missingEmployee,
                    out _),
                "Un binding no puede restaurarse sin su EmployeeId autoritativo.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot duplicatedWaiter = session.DeepClone();
            duplicatedWaiter.bindings.Add(
                new BistroBuilderStaffSessionBindingRecord
                {
                    employeeId = BistroBuilderEmployeeIdUtility.CreateNew(),
                    waiterId = 1,
                    handledTableIds = new List<int>()
                });
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    duplicatedWaiter,
                    populatedStaff,
                    out _),
                "staff.session.runtime rechaza WaiterId duplicado.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot inactive =
                BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    inactive,
                    populatedStaff,
                    out string inactiveError),
                "Snapshot inactivo es válido para partidas sin servicio. " +
                inactiveError,
                ref passed, ref failed, log);
            Check(!inactive.active &&
                  string.IsNullOrEmpty(inactive.sessionId) &&
                  inactive.dayIndex == 0 &&
                  inactive.bindings.Count == 0,
                "Snapshot inactivo no conserva bindings de otra partida.",
                ref passed, ref failed, log);

            string operationId =
                BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    sessionId,
                    employeeId);
            Check(BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                    operationId),
                "El operationId de rendimiento sobrevive a Save/Load de la sesión.",
                ref passed, ref failed, log);

            Check(
                typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                    typeof(BistroBuilderStaffStateSaveSectionProvider)) &&
                typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                    typeof(BistroBuilderStaffRecruitmentSaveSectionProvider)) &&
                typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                    typeof(BistroBuilderStaffSessionSaveSectionProvider)),
                "Los providers extienden el SaveGame universal; no crean otro Save.",
                ref passed, ref failed, log);

            Check(
                typeof(IBistroBuilderSaveSectionPhaseOrdering).IsAssignableFrom(
                    typeof(BistroBuilderStaffStateSaveSectionProvider)) &&
                typeof(IBistroBuilderSaveSectionPhaseOrdering).IsAssignableFrom(
                    typeof(BistroBuilderStaffRecruitmentSaveSectionProvider)) &&
                typeof(IBistroBuilderSaveSectionPhaseOrdering).IsAssignableFrom(
                    typeof(BistroBuilderStaffSessionSaveSectionProvider)),
                "Las tres secciones declaran orden de fases explícito.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        finally
        {
            if (roleCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(roleCatalog);
            }
            if (recruitmentProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(recruitmentProfile);
            }
        }

        log.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + text);
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
