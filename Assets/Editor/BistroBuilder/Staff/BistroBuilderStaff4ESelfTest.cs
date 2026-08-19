using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest 4E sin tocar disco ni escena real. Verifica round-trip JSON de
/// staff.state y staff.session.runtime, identidad estable y órdenes de fase
/// respecto a service.runtime.
/// </summary>
public static class BistroBuilderStaff4ESelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E - Autotest persistencia",
        false,
        3242)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E Personal",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4E de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4E PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog catalog = null;
        GameObject providerHost = null;

        try
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderStaffRoleCatalog>();
            catalog.InitializeV1DefaultsIfEmpty();

            BistroBuilderStaffSnapshot staff =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var request = new BistroBuilderEmployeeCreateRequest
            {
                firstName = "Lucía",
                lastName = "Persistencia",
                roleId = "waiter",
                salaryCentsPerService = 8450L,
                hiredDayIndex = 12,
                initialExperiencePoints = 640L,
                initialSkills = new BistroBuilderEmployeeSkillSet
                {
                    speed = 58,
                    attentiveness = 61,
                    organization = 55,
                    hospitality = 67
                },
                availability = BistroBuilderEmployeeAvailability.Available,
                responsibilities = new BistroBuilderEmployeeResponsibilitySettings
                {
                    primaryResponsibilityId = "service.floor",
                    primaryZoneId = "zone.main",
                    canSupportOtherZones = true
                }
            };

            Check(BistroBuilderStaffEngine.TryBuildEmployee(
                    employeeId,
                    request,
                    catalog,
                    out BistroBuilderEmployeeRecord employee,
                    out string buildError),
                "Employee construido para round-trip. " + buildError,
                ref passed, ref failed, log);
            Check(BistroBuilderStaffEngine.TryAppendEmployee(
                    staff,
                    employee,
                    catalog,
                    out staff,
                    out string appendError),
                "Employee añadido a staff.state. " + appendError,
                ref passed, ref failed, log);
            staff.operationalBootstrapCompleted = true;

            var session = new BistroBuilderStaffSessionSnapshot
            {
                schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
                revision = 3L,
                active = true,
                sessionId = BistroBuilderStaffSessionIdUtility.CreateNew(),
                dayIndex = 12,
                bindings = new List<BistroBuilderStaffSessionBindingRecord>
                {
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = employeeId,
                        waiterId = 1,
                        completedTasks = 9,
                        failedTasks = 1,
                        totalTaskDurationMilliseconds = 182000L,
                        handledTableIds = new List<int> { 1, 2, 4 }
                    }
                }
            };

            Check(BistroBuilderStaffEngine.TryValidateSnapshot(
                    staff,
                    catalog,
                    out string staffError),
                "staff.state origen es válido. " + staffError,
                ref passed, ref failed, log);
            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    session,
                    staff,
                    out string sessionError),
                "staff.session.runtime origen es válido. " + sessionError,
                ref passed, ref failed, log);

            var serializer = new BistroBuilderJsonSaveSerializer();
            byte[] staffBytes = serializer.Serialize(staff, true);
            byte[] sessionBytes = serializer.Serialize(session, true);
            Check(staffBytes != null && staffBytes.Length > 0,
                "staff.state produce payload JSON no vacío.",
                ref passed, ref failed, log);
            Check(sessionBytes != null && sessionBytes.Length > 0,
                "staff.session.runtime produce payload JSON no vacío.",
                ref passed, ref failed, log);

            var restoredStaff = (BistroBuilderStaffSnapshot)serializer.Deserialize(
                staffBytes,
                typeof(BistroBuilderStaffSnapshot));
            var restoredSession =
                (BistroBuilderStaffSessionSnapshot)serializer.Deserialize(
                    sessionBytes,
                    typeof(BistroBuilderStaffSessionSnapshot));

            Check(BistroBuilderStaffEngine.TryValidateSnapshot(
                    restoredStaff,
                    catalog,
                    out string restoredStaffError),
                "staff.state deserializado conserva invariantes. " +
                restoredStaffError,
                ref passed, ref failed, log);
            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    restoredSession,
                    restoredStaff,
                    out string restoredSessionError),
                "staff.session.runtime deserializado conserva invariantes. " +
                restoredSessionError,
                ref passed, ref failed, log);

            Check(restoredStaff.operationalBootstrapCompleted,
                "La marca anti-rebootstrap sobrevive al Save/Load.",
                ref passed, ref failed, log);
            Check(restoredStaff.employees.Count == 1 &&
                  restoredStaff.employees[0].employeeId == employeeId &&
                  restoredStaff.employees[0].salaryCentsPerService == 8450L &&
                  restoredStaff.employees[0].skills.hospitality == 67,
                "Identidad, contrato y habilidades sobreviven al round-trip.",
                ref passed, ref failed, log);
            Check(restoredSession.bindings.Count == 1 &&
                  restoredSession.bindings[0].employeeId == employeeId &&
                  restoredSession.bindings[0].waiterId == 1 &&
                  restoredSession.bindings[0].completedTasks == 9 &&
                  restoredSession.bindings[0].handledTableIds.Count == 3,
                "Binding y métricas de sesión sobreviven al round-trip.",
                ref passed, ref failed, log);

            // Corrupciones básicas deben ser detectadas después de deserializar.
            restoredSession.bindings[0].waiterId = 0;
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    restoredSession,
                    restoredStaff,
                    out _),
                "WaiterId corrupto se rechaza tras deserialización.",
                ref passed, ref failed, log);

            providerHost = new GameObject("BB_4E_PROVIDER_SELFTEST_TEMP");
            BistroBuilderStaffStateSaveSectionProvider stateProvider =
                providerHost.AddComponent<BistroBuilderStaffStateSaveSectionProvider>();
            BistroBuilderStaffSessionSaveSectionProvider sessionProvider =
                providerHost.AddComponent<BistroBuilderStaffSessionSaveSectionProvider>();

            Check(stateProvider.SectionId == "staff.state" &&
                  sessionProvider.SectionId == "staff.session.runtime" &&
                  stateProvider.SectionId != sessionProvider.SectionId,
                "Las dos secciones de Personal tienen IDs únicos y estables.",
                ref passed, ref failed, log);
            Check(stateProvider.SerializerId ==
                    BistroBuilderJsonSaveSerializer.StableSerializerId &&
                  sessionProvider.SerializerId ==
                    BistroBuilderJsonSaveSerializer.StableSerializerId,
                "Ambas secciones usan el serializador universal registrado.",
                ref passed, ref failed, log);
            Check(!stateProvider.IsRequired && !sessionProvider.IsRequired,
                "4E conserva compatibilidad con saves anteriores mediante secciones opcionales.",
                ref passed, ref failed, log);
            Check(stateProvider.PrepareOrder > 9000 &&
                  sessionProvider.PrepareOrder > stateProvider.PrepareOrder,
                "Prepare: service.runtime limpia primero y Personal después.",
                ref passed, ref failed, log);
            Check(stateProvider.ApplyOrder < sessionProvider.ApplyOrder &&
                  sessionProvider.ApplyOrder < 500,
                "Apply: staff.state → binding → service.runtime.",
                ref passed, ref failed, log);
            Check(sessionProvider.FinalizeOrder > 11000,
                "Finalize: Personal reanuda después de service.runtime.",
                ref passed, ref failed, log);
            Check(stateProvider.StateType == typeof(BistroBuilderStaffSnapshot) &&
                  sessionProvider.StateType ==
                    typeof(BistroBuilderStaffSessionSnapshot),
                "Cada provider declara el tipo autoritativo correcto.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción no controlada: " + exception);
        }
        finally
        {
            if (providerHost != null)
            {
                UnityEngine.Object.DestroyImmediate(providerHost);
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
