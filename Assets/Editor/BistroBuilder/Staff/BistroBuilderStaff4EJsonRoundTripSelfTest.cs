using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate 4E sobre el serializador real del Save universal.
///
/// No escribe archivos ni modifica escena. Serializa y deserializa los tres
/// snapshots de Personal con unity-json-v1 y vuelve a ejecutar las validaciones
/// canónicas de dominio para detectar campos perdidos, nombres incompatibles o
/// regresiones de JsonUtility antes de una prueba real de Save/Load.
/// </summary>
public static class BistroBuilderStaff4EJsonRoundTripSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E v2 - Autotest JSON round-trip",
        false,
        3243)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E JSON",
            "Round-trip: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El round-trip JSON 4E de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4E PERSONAL / JSON ROUND-TRIP ===");

        BistroBuilderStaffRoleCatalog roleCatalog = null;
        BistroBuilderStaffRecruitmentProfile recruitmentProfile = null;

        try
        {
            roleCatalog = ScriptableObject.CreateInstance<
                BistroBuilderStaffRoleCatalog>();
            roleCatalog.InitializeV1DefaultsIfEmpty();
            recruitmentProfile = ScriptableObject.CreateInstance<
                BistroBuilderStaffRecruitmentProfile>();

            Check(
                roleCatalog.TryValidate(out string roleError),
                "Catálogo de roles válido. " + roleError,
                ref passed,
                ref failed,
                log);
            Check(
                recruitmentProfile.TryValidate(
                    roleCatalog,
                    out string profileError),
                "Perfil de contratación válido. " + profileError,
                ref passed,
                ref failed,
                log);

            var serializer = new BistroBuilderJsonSaveSerializer();
            Check(
                string.Equals(
                    serializer.SerializerId,
                    BistroBuilderJsonSaveSerializer.StableSerializerId,
                    StringComparison.Ordinal) &&
                string.Equals(serializer.FileExtension, ".json", StringComparison.Ordinal),
                "Se usa el serializador canónico unity-json-v1.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSnapshot staff =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var request = new BistroBuilderEmployeeCreateRequest
            {
                firstName = "Lucía",
                lastName = "Santos",
                roleId = "waiter",
                salaryCentsPerService = 8450L,
                hiredDayIndex = 7,
                initialExperiencePoints = 321L,
                initialSkills = new BistroBuilderEmployeeSkillSet
                {
                    speed = 61,
                    attentiveness = 58,
                    organization = 64,
                    hospitality = 67
                },
                availability = BistroBuilderEmployeeAvailability.Available,
                responsibilities = new BistroBuilderEmployeeResponsibilitySettings
                {
                    primaryResponsibilityId = "dining-room",
                    primaryZoneId = "main",
                    canSupportOtherZones = true
                }
            };

            BistroBuilderEmployeeRecord employee = null;
            BistroBuilderStaffSnapshot populatedStaff = null;
            string buildError = string.Empty;
            string appendError = string.Empty;
            bool employeeBuilt = BistroBuilderStaffEngine.TryBuildEmployee(
                employeeId,
                request,
                roleCatalog,
                out employee,
                out buildError);
            bool employeeAppended = employeeBuilt &&
                BistroBuilderStaffEngine.TryAppendEmployee(
                    staff,
                    employee,
                    roleCatalog,
                    out populatedStaff,
                    out appendError);
            Check(
                employeeBuilt && employeeAppended && populatedStaff != null,
                "staff.state construido. " + buildError + appendError,
                ref passed,
                ref failed,
                log);
            if (!employeeBuilt || !employeeAppended || populatedStaff == null)
            {
                throw new InvalidOperationException(
                    "No puede continuar el round-trip sin staff.state válido.");
            }

            byte[] staffBytes = serializer.Serialize(populatedStaff, false);
            var staffRestored = (BistroBuilderStaffSnapshot)serializer.Deserialize(
                staffBytes,
                typeof(BistroBuilderStaffSnapshot));
            Check(
                BistroBuilderStaffEngine.TryValidateSnapshot(
                    staffRestored,
                    roleCatalog,
                    out string restoredStaffError),
                "staff.state valida después de JSON. " + restoredStaffError,
                ref passed,
                ref failed,
                log);
            Check(
                staffRestored.employees.Count == 1 &&
                string.Equals(
                    staffRestored.employees[0].employeeId,
                    employeeId,
                    StringComparison.Ordinal) &&
                staffRestored.employees[0].salaryCentsPerService == 8450L &&
                staffRestored.employees[0].skills.organization == 64 &&
                string.Equals(
                    staffRestored.employees[0].responsibilities.primaryZoneId,
                    "main",
                    StringComparison.Ordinal),
                "staff.state conserva identidad, contrato, skills y responsabilidades.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffRecruitmentSnapshot market = null;
            bool marketGenerated =
                BistroBuilderStaffRecruitmentEngine.TryGenerateInitialMarket(
                    recruitmentProfile,
                    roleCatalog,
                    7,
                    out market,
                    out string marketError);
            Check(
                marketGenerated && market != null,
                "staff.recruitment generado. " + marketError,
                ref passed,
                ref failed,
                log);
            if (!marketGenerated || market == null ||
                market.candidates == null || market.candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "No puede continuar el round-trip sin mercado válido.");
            }

            string candidateId = market.candidates[0].candidateId;
            long candidateSalary = market.candidates[0].expectedSalaryCentsPerService;
            byte[] marketBytes = serializer.Serialize(market, false);
            var marketRestored =
                (BistroBuilderStaffRecruitmentSnapshot)serializer.Deserialize(
                    marketBytes,
                    typeof(BistroBuilderStaffRecruitmentSnapshot));
            Check(
                BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                    marketRestored,
                    recruitmentProfile,
                    roleCatalog,
                    false,
                    out string restoredMarketError),
                "staff.recruitment valida después de JSON. " + restoredMarketError,
                ref passed,
                ref failed,
                log);
            Check(
                marketRestored.generationSequence == market.generationSequence &&
                marketRestored.lastRefreshDayIndex == market.lastRefreshDayIndex &&
                marketRestored.candidates.Count == market.candidates.Count &&
                string.Equals(
                    marketRestored.candidates[0].candidateId,
                    candidateId,
                    StringComparison.Ordinal) &&
                marketRestored.candidates[0].expectedSalaryCentsPerService ==
                    candidateSalary,
                "staff.recruitment conserva generación, refresh y candidatos.",
                ref passed,
                ref failed,
                log);

            string sessionId = BistroBuilderStaffSessionIdUtility.CreateNew();
            var session = new BistroBuilderStaffSessionSnapshot
            {
                schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
                revision = 9L,
                active = true,
                sessionId = sessionId,
                dayIndex = 7,
                bindings = new List<BistroBuilderStaffSessionBindingRecord>
                {
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = employeeId,
                        waiterId = 3,
                        completedTasks = 11,
                        failedTasks = 1,
                        totalTaskDurationMilliseconds = 183500L,
                        handledTableIds = new List<int> { 2, 4, 9 }
                    }
                }
            };

            byte[] sessionBytes = serializer.Serialize(session, false);
            var sessionRestored =
                (BistroBuilderStaffSessionSnapshot)serializer.Deserialize(
                    sessionBytes,
                    typeof(BistroBuilderStaffSessionSnapshot));
            Check(
                BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    sessionRestored,
                    staffRestored,
                    out string restoredSessionError),
                "staff.session.runtime valida después de JSON. " +
                restoredSessionError,
                ref passed,
                ref failed,
                log);
            Check(
                sessionRestored.active &&
                sessionRestored.revision == 9L &&
                string.Equals(
                    sessionRestored.sessionId,
                    sessionId,
                    StringComparison.Ordinal) &&
                sessionRestored.dayIndex == 7 &&
                sessionRestored.bindings.Count == 1 &&
                sessionRestored.bindings[0].waiterId == 3 &&
                sessionRestored.bindings[0].completedTasks == 11 &&
                sessionRestored.bindings[0].failedTasks == 1 &&
                sessionRestored.bindings[0].totalTaskDurationMilliseconds == 183500L &&
                sessionRestored.bindings[0].handledTableIds.Count == 3,
                "staff.session.runtime conserva binding y métricas reales.",
                ref passed,
                ref failed,
                log);

            string beforeOperationId =
                BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    session.sessionId,
                    employeeId);
            string afterOperationId =
                BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    sessionRestored.sessionId,
                    sessionRestored.bindings[0].employeeId);
            Check(
                string.Equals(
                    beforeOperationId,
                    afterOperationId,
                    StringComparison.Ordinal) &&
                BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(
                    afterOperationId),
                "El operationId idempotente de rendimiento es estable tras JSON.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot inactive =
                BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            byte[] inactiveBytes = serializer.Serialize(inactive, false);
            var inactiveRestored =
                (BistroBuilderStaffSessionSnapshot)serializer.Deserialize(
                    inactiveBytes,
                    typeof(BistroBuilderStaffSessionSnapshot));
            Check(
                BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    inactiveRestored,
                    staffRestored,
                    out string inactiveError) &&
                !inactiveRestored.active &&
                string.IsNullOrEmpty(inactiveRestored.sessionId) &&
                inactiveRestored.bindings.Count == 0,
                "La sesión inactiva también sobrevive sin residuos. " + inactiveError,
                ref passed,
                ref failed,
                log);
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

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
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
