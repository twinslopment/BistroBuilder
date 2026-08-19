using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro/de aplicación de 4A. No necesita Play Mode ni modifica la
/// escena. Crea objetos temporales y los destruye al finalizar.
/// </summary>
public static class BistroBuilderStaff4ASelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4A - Autotest fundación canónica",
        false,
        3202)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4A Personal",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4A de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4A PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog catalog = null;
        GameObject host = null;
        try
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderStaffRoleCatalog>();
            catalog.InitializeV1DefaultsIfEmpty();

            Check(catalog.TryValidate(out string catalogError),
                "Catálogo V1 válido. " + catalogError, ref passed, ref failed, log);
            Check(catalog.TryGetRole("waiter", out BistroBuilderStaffRoleDefinition waiterRole) &&
                  waiterRole != null && waiterRole.active,
                "Rol de camarero V1 disponible.", ref passed, ref failed, log);
            Check(waiterRole != null &&
                  waiterRole.operationalAdapterId ==
                      BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                "Camarero usa adaptador waiter.agent sin referencia a Waiter.",
                ref passed, ref failed, log);

            string idA = BistroBuilderEmployeeIdUtility.CreateNew();
            string idB = BistroBuilderEmployeeIdUtility.CreateNew();
            Check(BistroBuilderEmployeeIdUtility.IsValid(idA),
                "EmployeeId generado es válido.", ref passed, ref failed, log);
            Check(BistroBuilderEmployeeIdUtility.IsValid(idB) && idA != idB,
                "Dos empleados reciben identidades independientes.", ref passed, ref failed, log);
            Check(BistroBuilderEmployeeIdUtility.Normalize("  " + idA.ToUpperInvariant() + "  ") == idA,
                "EmployeeId normaliza sin cambiar identidad lógica.", ref passed, ref failed, log);
            Check(!BistroBuilderEmployeeIdUtility.IsValid("emp_miguel"),
                "Un nombre no puede utilizarse como EmployeeId.", ref passed, ref failed, log);
            Check(!BistroBuilderEmployeeIdUtility.IsValid("emp_00000000000000000000000000000000"),
                "EmployeeId vacío/sentinel queda rechazado.", ref passed, ref failed, log);
            Check(BistroBuilderStaffStableIdUtility.IsValid("waiter.agent") &&
                  !BistroBuilderStaffStableIdUtility.IsValid("Waiter Agent"),
                "IDs de rol/adaptador usan formato estable técnico.", ref passed, ref failed, log);

            BistroBuilderStaffSnapshot empty = BistroBuilderStaffEngine.CreateEmptySnapshot();
            Check(BistroBuilderStaffEngine.TryValidateSnapshot(empty, catalog, out string emptyError),
                "staff.state vacío inicial es válido. " + emptyError,
                ref passed, ref failed, log);
            Check(empty.revision == 1L && empty.employees.Count == 0,
                "staff.state inicia sin empleados hardcodeados.", ref passed, ref failed, log);

            BistroBuilderEmployeeCreateRequest request = MakeRequest(
                "Lucía", "Santos", 8450L, 3, 125L);
            Check(BistroBuilderStaffEngine.TryBuildEmployee(
                    idA, request, catalog,
                    out BistroBuilderEmployeeRecord employeeA,
                    out string buildError),
                "Se construye un empleado válido. " + buildError,
                ref passed, ref failed, log);
            Check(employeeA != null && employeeA.employeeId == idA,
                "El EmployeeId se conserva exactamente.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.FullName == "Lucía Santos",
                "Nombre visible se deriva de datos personales, no de identidad.",
                ref passed, ref failed, log);
            Check(employeeA != null && employeeA.roleId == "waiter",
                "Rol del empleado queda conservado.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.salaryCentsPerService == 8450L,
                "Salario contractual se conserva en céntimos.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.hiredDayIndex == 3,
                "Día de contratación se conserva.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.experiencePoints == 125L,
                "Experiencia inicial se conserva.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.skills.speed == 58 &&
                  employeeA.skills.attentiveness == 61 &&
                  employeeA.skills.organization == 55 &&
                  employeeA.skills.hospitality == 63,
                "Las cuatro habilidades V1 se conservan.", ref passed, ref failed, log);
            Check(employeeA != null && employeeA.performance.completedTasks == 0,
                "Rendimiento empieza vacío; no se inventan métricas.", ref passed, ref failed, log);

            Check(BistroBuilderStaffEngine.TryAppendEmployee(
                    empty, employeeA, catalog,
                    out BistroBuilderStaffSnapshot oneEmployee,
                    out string appendError),
                "Empleado entra en el roster canónico. " + appendError,
                ref passed, ref failed, log);
            Check(empty.employees.Count == 0 && oneEmployee.employees.Count == 1,
                "Mutación del engine no altera el snapshot anterior.", ref passed, ref failed, log);
            Check(oneEmployee.revision == empty.revision + 1L,
                "La revisión de staff.state aumenta una sola vez.", ref passed, ref failed, log);
            Check(!BistroBuilderStaffEngine.TryAppendEmployee(
                    oneEmployee, employeeA, catalog,
                    out _, out _),
                "No se permite EmployeeId duplicado.", ref passed, ref failed, log);

            BistroBuilderStaffSnapshot duplicate = oneEmployee.DeepClone();
            duplicate.employees.Add(employeeA.DeepClone());
            Check(!BistroBuilderStaffEngine.TryValidateSnapshot(duplicate, catalog, out _),
                "Validador detecta IDs duplicados incluso en snapshot externo.",
                ref passed, ref failed, log);

            BistroBuilderEmployeeRecord invalidSalary = employeeA.DeepClone();
            invalidSalary.employeeId = idB;
            invalidSalary.salaryCentsPerService = -1L;
            Check(!BistroBuilderStaffEngine.TryValidateEmployee(
                    invalidSalary, catalog, true, out _),
                "Salario negativo queda rechazado.", ref passed, ref failed, log);

            BistroBuilderEmployeeRecord invalidSkill = employeeA.DeepClone();
            invalidSkill.employeeId = idB;
            invalidSkill.skills.speed = 101;
            Check(!BistroBuilderStaffEngine.TryValidateEmployee(
                    invalidSkill, catalog, true, out _),
                "Habilidad fuera de 0..100 queda rechazada.", ref passed, ref failed, log);

            BistroBuilderEmployeeRecord invalidRole = employeeA.DeepClone();
            invalidRole.employeeId = idB;
            invalidRole.roleId = "unknown_role";
            Check(!BistroBuilderStaffEngine.TryValidateEmployee(
                    invalidRole, catalog, true, out _),
                "Rol inexistente queda rechazado.", ref passed, ref failed, log);

            BistroBuilderEmployeeRecord invalidName = employeeA.DeepClone();
            invalidName.employeeId = idB;
            invalidName.firstName = " ";
            Check(!BistroBuilderStaffEngine.TryValidateEmployee(
                    invalidName, catalog, true, out _),
                "Empleado sin nombre visible queda rechazado.", ref passed, ref failed, log);

            Check(BistroBuilderStaffEngine.TrySetAvailability(
                    oneEmployee, idA,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    catalog,
                    out BistroBuilderStaffSnapshot unavailable,
                    out BistroBuilderEmployeeRecord updated,
                    out bool changed,
                    out string availabilityError),
                "Disponibilidad cambia por API canónica. " + availabilityError,
                ref passed, ref failed, log);
            Check(changed && updated.availability ==
                    BistroBuilderEmployeeAvailability.Unavailable,
                "Cambio de disponibilidad queda reflejado.", ref passed, ref failed, log);
            Check(unavailable.revision == oneEmployee.revision + 1L &&
                  updated.revision == employeeA.revision + 1L,
                "Cambio incrementa revisiones global e individual.", ref passed, ref failed, log);
            Check(BistroBuilderStaffEngine.TrySetAvailability(
                    unavailable, idA,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    catalog,
                    out BistroBuilderStaffSnapshot replayAvailability,
                    out _, out bool replayChanged, out _) &&
                  !replayChanged &&
                  replayAvailability.revision == unavailable.revision,
                "Repetir la misma disponibilidad es idempotente.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot clone = unavailable.DeepClone();
            clone.employees[0].firstName = "MUTADO";
            clone.employees[0].skills.speed = 0;
            Check(unavailable.employees[0].firstName == "Lucía" &&
                  unavailable.employees[0].skills.speed == 58,
                "DeepClone aísla empleado y habilidades.", ref passed, ref failed, log);

            host = new GameObject("BB_4A_SELFTEST_TEMP");
            BistroBuilderStaffService service = host.AddComponent<BistroBuilderStaffService>();
            SerializedObject serialized = new SerializedObject(service);
            SerializedProperty roleProperty = serialized.FindProperty("roleCatalog");
            Check(roleProperty != null,
                "StaffService expone solo el catálogo como dependencia serializada.",
                ref passed, ref failed, log);
            if (roleProperty != null)
            {
                roleProperty.objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Check(service.ValidateConfiguration(out string serviceConfigError),
                "StaffService configura sin Waiter ni Finanzas. " + serviceConfigError,
                ref passed, ref failed, log);
            Check(service.TryInitializeFresh(out string serviceInitError),
                "StaffService inicializa roster vacío. " + serviceInitError,
                ref passed, ref failed, log);
            Check(service.EmployeeCount == 0 && service.Revision == 1L,
                "Servicio comienza sin empleados inventados.", ref passed, ref failed, log);

            int createdEvents = 0;
            int updatedEvents = 0;
            int availabilityEvents = 0;
            int staffEvents = 0;
            service.EmployeeCreated += _ => createdEvents++;
            service.EmployeeUpdated += _ => updatedEvents++;
            service.AvailabilityChanged += _ => availabilityEvents++;
            service.StaffChanged += _ => staffEvents++;

            Check(service.TryCreateEmployee(
                    MakeRequest("Lucía", "Santos", 8450L, 3, 125L),
                    out BistroBuilderEmployeeRecord serviceEmployee,
                    out string createError),
                "StaffService crea empleado por comando. " + createError,
                ref passed, ref failed, log);
            Check(serviceEmployee != null &&
                  BistroBuilderEmployeeIdUtility.IsValid(serviceEmployee.employeeId),
                "Servicio genera EmployeeId estable automáticamente.",
                ref passed, ref failed, log);
            Check(createdEvents == 1 && staffEvents == 1,
                "Creación publica exactamente un evento específico y uno global.",
                ref passed, ref failed, log);

            Check(service.TryCreateEmployee(
                    MakeRequest("Lucía", "Santos", 8450L, 3, 125L),
                    out BistroBuilderEmployeeRecord sameNameSecond,
                    out _) &&
                  sameNameSecond.employeeId != serviceEmployee.employeeId,
                "Dos personas con mismo nombre siguen teniendo EmployeeId distintos.",
                ref passed, ref failed, log);
            Check(service.EmployeeCount == 2,
                "Roster admite personas con el mismo nombre sin colisión.",
                ref passed, ref failed, log);

            Check(service.TryGetEmployee(
                    serviceEmployee.employeeId,
                    out BistroBuilderEmployeeRecord queried) && queried != null,
                "Consulta por EmployeeId devuelve el empleado.", ref passed, ref failed, log);
            queried.firstName = "ALTERADO";
            Check(service.TryGetEmployee(serviceEmployee.employeeId, out queried) &&
                  queried.firstName == "Lucía",
                "Consulta devuelve copia y no permite mutar autoridad.",
                ref passed, ref failed, log);

            int beforeStaffEvents = staffEvents;
            Check(service.TrySetAvailability(
                    serviceEmployee.employeeId,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    out BistroBuilderEmployeeRecord serviceUnavailable,
                    out string setError),
                "Servicio cambia disponibilidad. " + setError,
                ref passed, ref failed, log);
            Check(serviceUnavailable.availability ==
                    BistroBuilderEmployeeAvailability.Unavailable &&
                  updatedEvents == 1 && availabilityEvents == 1 &&
                  staffEvents == beforeStaffEvents + 1,
                "Disponibilidad publica eventos exactamente una vez.",
                ref passed, ref failed, log);

            beforeStaffEvents = staffEvents;
            Check(service.TrySetAvailability(
                    serviceEmployee.employeeId,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    out _, out _) &&
                  staffEvents == beforeStaffEvents &&
                  availabilityEvents == 1,
                "Repetición idempotente no duplica listeners/eventos.",
                ref passed, ref failed, log);

            var copied = new List<BistroBuilderEmployeeRecord>();
            service.CopyEmployees(copied);
            Check(copied.Count == 2,
                "CopyEmployees devuelve roster completo.", ref passed, ref failed, log);
            copied[0].skills.organization = 0;
            service.CopyEmployees(copied);
            Check(copied[0].skills.organization == 55,
                "CopyEmployees no filtra referencias mutables internas.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot beforeRestore = service.CreateSnapshot();
            BistroBuilderStaffSnapshot invalidRestore = beforeRestore.DeepClone();
            invalidRestore.employees.Add(beforeRestore.employees[0].DeepClone());
            Check(!service.TryRestoreSnapshot(invalidRestore, out _),
                "Restore rechaza snapshot corrupto.", ref passed, ref failed, log);
            Check(service.CreateSnapshot().employees.Count == beforeRestore.employees.Count,
                "Restore fallido no muta la plantilla actual.", ref passed, ref failed, log);

            int restoredEvents = 0;
            service.StateRestored += () => restoredEvents++;
            Check(service.TryRestoreSnapshot(beforeRestore, out string restoreError),
                "Restore válido reconstruye staff.state. " + restoreError,
                ref passed, ref failed, log);
            Check(restoredEvents == 1,
                "Restore publica un único evento de restauración.",
                ref passed, ref failed, log);

            Check(service.TryGetRoleDefinition("waiter", out BistroBuilderStaffRoleDefinition serviceRole) &&
                  serviceRole.operationalAdapterId ==
                      BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                "La aplicación resuelve roles desde catálogo, no desde enum rígido.",
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

    private static BistroBuilderEmployeeCreateRequest MakeRequest(
        string firstName,
        string lastName,
        long salary,
        int hiredDay,
        long experience)
    {
        return new BistroBuilderEmployeeCreateRequest
        {
            firstName = firstName,
            lastName = lastName,
            roleId = "waiter",
            salaryCentsPerService = salary,
            hiredDayIndex = hiredDay,
            initialExperiencePoints = experience,
            availability = BistroBuilderEmployeeAvailability.Available,
            initialSkills = new BistroBuilderEmployeeSkillSet
            {
                speed = 58,
                attentiveness = 61,
                organization = 55,
                hospitality = 63
            },
            responsibilities = new BistroBuilderEmployeeResponsibilitySettings
            {
                canSupportOtherZones = true
            }
        };
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
