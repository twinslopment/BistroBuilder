using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest 4C: XP trazable, niveles derivados, rendimiento real agregado,
/// idempotencia por servicio y formación V1 sin economía paralela.
/// </summary>
public static class BistroBuilderStaff4CSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4C - Autotest desarrollo y rendimiento",
        false,
        3222)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4C Personal",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4C de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4C PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog catalog = null;
        BistroBuilderStaffDevelopmentProfile profile = null;
        GameObject host = null;

        try
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderStaffRoleCatalog>();
            catalog.InitializeV1DefaultsIfEmpty();
            profile = ScriptableObject.CreateInstance<BistroBuilderStaffDevelopmentProfile>();

            Check(profile.TryValidate(out string profileError),
                "Perfil 4C válido. " + profileError,
                ref passed, ref failed, log);
            Check(BistroBuilderStaffDevelopmentEngine.GetLevelForExperience(
                    0L, profile) == 1,
                "XP 0 corresponde a nivel profesional 1.",
                ref passed, ref failed, log);
            Check(BistroBuilderStaffDevelopmentEngine.GetExperienceRequiredForLevel(
                    2, profile) == profile.BaseExperiencePerLevel,
                "Umbral del nivel 2 deriva del perfil.",
                ref passed, ref failed, log);
            Check(BistroBuilderStaffDevelopmentEngine.GetExperienceRequiredForLevel(
                    3, profile) >
                  BistroBuilderStaffDevelopmentEngine.GetExperienceRequiredForLevel(
                    2, profile),
                "La progresión requiere cada vez más experiencia acumulada.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot snapshot =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var request = new BistroBuilderEmployeeCreateRequest
            {
                firstName = "Lucía",
                lastName = "Santos",
                roleId = "waiter",
                salaryCentsPerService = 8500L,
                hiredDayIndex = 1,
                initialExperiencePoints = 0L,
                initialSkills = new BistroBuilderEmployeeSkillSet
                {
                    speed = 50,
                    attentiveness = 50,
                    organization = 50,
                    hospitality = 50
                }
            };

            Check(BistroBuilderStaffEngine.TryBuildEmployee(
                    employeeId,
                    request,
                    catalog,
                    out BistroBuilderEmployeeRecord built,
                    out string buildError) &&
                  BistroBuilderStaffEngine.TryAppendEmployee(
                    snapshot,
                    built,
                    catalog,
                    out BistroBuilderStaffSnapshot roster,
                    out string appendError),
                "Empleado base preparado. " + buildError + appendError,
                ref passed, ref failed, log);
            Check(BistroBuilderStaffDevelopmentEngine.TryValidateDevelopmentData(
                    roster.employees[0].development,
                    out string developmentError),
                "Estado de desarrollo inicial válido. " + developmentError,
                ref passed, ref failed, log);

            var serviceReport = new BistroBuilderEmployeeServicePerformanceReport
            {
                operationId = "staff.service.day1.lunch.emp1",
                serviceCompleted = true,
                completedTasks = 5,
                failedTasks = 1,
                tablesHandled = 3,
                totalTaskDurationMilliseconds = 180000L
            };

            Check(BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(
                    roster,
                    employeeId,
                    serviceReport,
                    profile,
                    catalog,
                    out BistroBuilderStaffSnapshot afterService,
                    out BistroBuilderEmployeeRecord afterServiceEmployee,
                    out BistroBuilderEmployeeProgressionResult progression,
                    out string serviceError),
                "Resultado real de servicio aplicado. " + serviceError,
                ref passed, ref failed, log);
            long expectedXp = profile.BaseCompletedServiceExperience +
                Math.Min(
                    profile.MaximumTaskExperiencePerService,
                    5L * profile.ExperiencePerCompletedTask);
            Check(progression.experienceGained == expectedXp &&
                  afterServiceEmployee.experiencePoints == expectedXp,
                "XP deriva únicamente de servicio completado + tareas agregadas.",
                ref passed, ref failed, log);
            Check(afterServiceEmployee.performance.completedServices == 1 &&
                  afterServiceEmployee.performance.completedTasks == 5 &&
                  afterServiceEmployee.performance.failedTasks == 1 &&
                  afterServiceEmployee.performance.tablesHandled == 3,
                "Rendimiento conserva hechos reales del servicio.",
                ref passed, ref failed, log);
            Check(afterServiceEmployee.performance.totalTaskDurationMilliseconds == 180000L,
                "Tiempo de tareas queda agregado sin temporizadores de XP por frame.",
                ref passed, ref failed, log);
            Check(progression.levelBefore == 1 && progression.levelAfter == 1,
                "Un solo servicio no provoca una subida rápida de nivel.",
                ref passed, ref failed, log);

            long revisionAfterService = afterService.revision;
            Check(BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(
                    afterService,
                    employeeId,
                    serviceReport,
                    profile,
                    catalog,
                    out BistroBuilderStaffSnapshot replayService,
                    out BistroBuilderEmployeeRecord replayEmployee,
                    out BistroBuilderEmployeeProgressionResult replayProgression,
                    out _) &&
                  replayProgression.wasReplayed &&
                  replayService.revision == revisionAfterService &&
                  replayEmployee.experiencePoints == expectedXp,
                "Repetir el mismo operationId no duplica XP ni rendimiento.",
                ref passed, ref failed, log);

            BistroBuilderEmployeePerformanceSummary summary =
                BistroBuilderStaffDevelopmentEngine.BuildPerformanceSummary(
                    afterServiceEmployee);
            Check(summary.hasData && summary.completedTasks == 5 &&
                  summary.failedTasks == 1 &&
                  summary.completionRateBasisPoints == 8333,
                "Resumen de rendimiento se deriva de contadores reales.",
                ref passed, ref failed, log);
            Check(summary.averageTaskDurationMilliseconds == 30000L,
                "Duración media se calcula sobre las tareas registradas.",
                ref passed, ref failed, log);

            var invalidReport = new BistroBuilderEmployeeServicePerformanceReport
            {
                operationId = "staff.service.invalid",
                serviceCompleted = true,
                completedTasks = -1
            };
            Check(!BistroBuilderStaffDevelopmentEngine.TryApplyServicePerformance(
                    afterService,
                    employeeId,
                    invalidReport,
                    profile,
                    catalog,
                    out _, out _, out _, out _),
                "Un informe negativo/corrupto queda rechazado.",
                ref passed, ref failed, log);

            var trainingRequest = new BistroBuilderEmployeeTrainingRequest
            {
                operationId = "staff.training.day1.pace.001",
                employeeId = employeeId,
                trainingId = "service_pace",
                dayIndex = 1
            };
            Check(BistroBuilderStaffDevelopmentEngine.TryApplyTraining(
                    afterService,
                    trainingRequest,
                    profile,
                    catalog,
                    out BistroBuilderStaffSnapshot afterTraining,
                    out BistroBuilderEmployeeRecord trained,
                    out BistroBuilderEmployeeTrainingResult training,
                    out string trainingError),
                "Formación V1 aplicada. " + trainingError,
                ref passed, ref failed, log);
            Check(training.skillKind == BistroBuilderEmployeeSkillKind.Speed &&
                  training.skillBefore == 50 &&
                  training.skillAfter == 52 &&
                  training.skillGained == 2,
                "Formación modifica solo la habilidad objetivo.",
                ref passed, ref failed, log);
            Check(trained.skills.attentiveness == 50 &&
                  trained.skills.organization == 50 &&
                  trained.skills.hospitality == 50,
                "Formación no altera habilidades ajenas.",
                ref passed, ref failed, log);
            Check(trained.experiencePoints == expectedXp,
                "Formación no inventa XP adicional.",
                ref passed, ref failed, log);
            Check(training.financialCostCents == 0L,
                "Formación V1 actual no crea un cargo financiero alternativo.",
                ref passed, ref failed, log);

            long trainingRevision = afterTraining.revision;
            Check(BistroBuilderStaffDevelopmentEngine.TryApplyTraining(
                    afterTraining,
                    trainingRequest,
                    profile,
                    catalog,
                    out BistroBuilderStaffSnapshot replayTraining,
                    out _,
                    out BistroBuilderEmployeeTrainingResult trainingReplay,
                    out _) &&
                  trainingReplay.wasReplayed &&
                  replayTraining.revision == trainingRevision,
                "Repetir operationId de formación es idempotente.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot deepClone = afterTraining.DeepClone();
            deepClone.employees[0].development.trainingHistory[0].trainingId = "mutated";
            Check(afterTraining.employees[0].development.trainingHistory[0].trainingId ==
                    "service_pace",
                "DeepClone aísla el historial de formación.",
                ref passed, ref failed, log);

            host = new GameObject("BB_4C_SELFTEST_TEMP");
            BistroBuilderGeneralGameStateService general =
                host.AddComponent<BistroBuilderGeneralGameStateService>();
            BistroBuilderStaffService staff =
                host.AddComponent<BistroBuilderStaffService>();
            BistroBuilderStaffDevelopmentService development =
                host.AddComponent<BistroBuilderStaffDevelopmentService>();
            AssignObject(staff, "roleCatalog", catalog);
            AssignObject(development, "staffService", staff);
            AssignObject(development, "generalGameStateService", general);
            AssignObject(development, "developmentProfile", profile);

            Check(staff.TryInitializeFresh(out string initError),
                "StaffService inicializado para 4C. " + initError,
                ref passed, ref failed, log);
            Check(staff.TryCreateEmployee(
                    request,
                    out BistroBuilderEmployeeRecord applicationEmployee,
                    out string createError),
                "Empleado creado en aplicación 4C. " + createError,
                ref passed, ref failed, log);
            Check(development.ValidateConfiguration(out string configError),
                "DevelopmentService configurado. " + configError,
                ref passed, ref failed, log);

            int experienceEvents = 0;
            int performanceEvents = 0;
            int skillEvents = 0;
            development.ExperienceChanged += (_, __) => experienceEvents++;
            development.PerformanceChanged += (_, __) => performanceEvents++;
            development.SkillChanged += (_, __) => skillEvents++;

            var appReport = new BistroBuilderEmployeeServicePerformanceReport
            {
                operationId = "staff.service.app.001",
                serviceCompleted = true,
                completedTasks = 4,
                failedTasks = 0,
                tablesHandled = 2,
                totalTaskDurationMilliseconds = 100000L
            };
            Check(development.TryApplyServiceResult(
                    applicationEmployee.employeeId,
                    appReport,
                    out BistroBuilderEmployeeRecord appUpdated,
                    out BistroBuilderEmployeeProgressionResult appProgression,
                    out string appError),
                "Application aplica resultado de servicio. " + appError,
                ref passed, ref failed, log);
            Check(!appProgression.wasReplayed && experienceEvents == 1 &&
                  performanceEvents == 1,
                "Servicio publica eventos de XP/rendimiento exactamente una vez.",
                ref passed, ref failed, log);
            long appRevision = staff.Revision;
            Check(development.TryApplyServiceResult(
                    applicationEmployee.employeeId,
                    appReport,
                    out _, out BistroBuilderEmployeeProgressionResult appReplay,
                    out _) &&
                  appReplay.wasReplayed && staff.Revision == appRevision &&
                  experienceEvents == 1 && performanceEvents == 1,
                "Replay de servicio no vuelve a publicar eventos ni muta roster.",
                ref passed, ref failed, log);

            Check(development.TryTrainEmployee(
                    applicationEmployee.employeeId,
                    "guest_care",
                    "staff.training.app.001",
                    out BistroBuilderEmployeeRecord appTrained,
                    out BistroBuilderEmployeeTrainingResult appTraining,
                    out string appTrainingError),
                "Application ejecuta formación sin monedero paralelo. " +
                    appTrainingError,
                ref passed, ref failed, log);
            Check(appTraining.skillGained == 2 &&
                  appTrained.skills.hospitality == 52 && skillEvents == 1,
                "Formación publica SkillChanged una vez.",
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
