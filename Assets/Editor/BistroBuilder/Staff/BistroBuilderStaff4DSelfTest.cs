using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderStaff4DTestMutationGuard :
    IBistroBuilderStaffRuntimeMutationGuard
{
    public string blockedEmployeeId = string.Empty;

    public bool CanDismissEmployee(string employeeId, out string error)
    {
        if (BistroBuilderEmployeeIdUtility.Normalize(employeeId) ==
            BistroBuilderEmployeeIdUtility.Normalize(blockedEmployeeId))
        {
            error = "binding activo de prueba";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool CanChangeAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability requestedAvailability,
        out string error)
    {
        if (BistroBuilderEmployeeIdUtility.Normalize(employeeId) ==
            BistroBuilderEmployeeIdUtility.Normalize(blockedEmployeeId))
        {
            error = "binding activo de prueba";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Autotest 4D centrado en invariantes de identidad, binding y protección de
/// la autoridad persistente. La interacción multimesa real queda para el gate
/// Play Mode 4G.
/// </summary>
public static class BistroBuilderStaff4DSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest binding de servicio",
        false,
        3232)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D Personal",
            "Autotest: " + passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest 4D de Personal ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4D PERSONAL / AUTOTEST ===");

        BistroBuilderStaffRoleCatalog catalog = null;
        GameObject staffHost = null;
        GameObject waiterHost = null;

        try
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderStaffRoleCatalog>();
            catalog.InitializeV1DefaultsIfEmpty();

            string sessionIdA = BistroBuilderStaffSessionIdUtility.CreateNew();
            string sessionIdB = BistroBuilderStaffSessionIdUtility.CreateNew();
            Check(BistroBuilderStaffSessionIdUtility.IsValid(sessionIdA),
                "SessionId generado es estable y válido.",
                ref passed, ref failed, log);
            Check(BistroBuilderStaffSessionIdUtility.IsValid(sessionIdB) &&
                  sessionIdA != sessionIdB,
                "Dos sesiones obtienen identidades independientes.",
                ref passed, ref failed, log);
            Check(!BistroBuilderStaffSessionIdUtility.IsValid("staffsession_waiter1"),
                "SessionId no puede derivarse de WaiterId/nombre.",
                ref passed, ref failed, log);

            BistroBuilderStaffSnapshot roster =
                BistroBuilderStaffEngine.CreateEmptySnapshot();
            string employeeA = BistroBuilderEmployeeIdUtility.CreateNew();
            string employeeB = BistroBuilderEmployeeIdUtility.CreateNew();
            Check(TryAppendEmployee(roster, catalog, employeeA, "Lucía", out roster),
                "Primer Employee preparado para binding.",
                ref passed, ref failed, log);
            Check(TryAppendEmployee(roster, catalog, employeeB, "Marta", out roster),
                "Segundo Employee preparado para binding.",
                ref passed, ref failed, log);

            var active = new BistroBuilderStaffSessionSnapshot
            {
                revision = 1L,
                active = true,
                sessionId = sessionIdA,
                dayIndex = 8,
                bindings = new List<BistroBuilderStaffSessionBindingRecord>
                {
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = employeeA,
                        waiterId = 1,
                        completedTasks = 7,
                        failedTasks = 0,
                        totalTaskDurationMilliseconds = 120000L,
                        handledTableIds = new List<int> { 1, 3 }
                    },
                    new BistroBuilderStaffSessionBindingRecord
                    {
                        employeeId = employeeB,
                        waiterId = 2,
                        completedTasks = 5,
                        failedTasks = 0,
                        totalTaskDurationMilliseconds = 90000L,
                        handledTableIds = new List<int> { 2 }
                    }
                }
            };

            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    active, roster, out string activeError),
                "Dos empleados pueden ligar dos agentes distintos. " + activeError,
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot duplicateEmployee = active.DeepClone();
            duplicateEmployee.bindings[1].employeeId = employeeA;
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    duplicateEmployee, roster, out _),
                "Un EmployeeId no puede controlar dos Waiter simultáneos.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot duplicateWaiter = active.DeepClone();
            duplicateWaiter.bindings[1].waiterId = 1;
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    duplicateWaiter, roster, out _),
                "Un WaiterId no puede representar dos Employee simultáneos.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot duplicateTable = active.DeepClone();
            duplicateTable.bindings[0].handledTableIds.Add(1);
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    duplicateTable, roster, out _),
                "Rendimiento de sesión rechaza TableId duplicados.",
                ref passed, ref failed, log);

            string operationId =
                BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    sessionIdA,
                    employeeA);
            Check(BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(operationId),
                "Binding produce operationId compatible con idempotencia 4C.",
                ref passed, ref failed, log);
            Check(operationId ==
                  BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    sessionIdA, employeeA),
                "OperationId de cierre es determinista por sesión + EmployeeId.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot inactive =
                BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            Check(BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    inactive, roster, out string inactiveError),
                "Sesión inactiva canónica es válida. " + inactiveError,
                ref passed, ref failed, log);
            inactive.bindings.Add(active.bindings[0].DeepClone());
            Check(!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    inactive, roster, out _),
                "Sesión inactiva no puede conservar bindings huérfanos.",
                ref passed, ref failed, log);

            BistroBuilderStaffSessionSnapshot isolated = active.DeepClone();
            isolated.bindings[0].handledTableIds.Add(99);
            Check(!active.bindings[0].handledTableIds.Contains(99),
                "DeepClone de sesión aísla métricas y listas internas.",
                ref passed, ref failed, log);

            staffHost = new GameObject("BB_4D_STAFF_SELFTEST_TEMP");
            BistroBuilderStaffService staff =
                staffHost.AddComponent<BistroBuilderStaffService>();
            AssignObject(staff, "roleCatalog", catalog);
            Check(staff.TryInitializeFresh(out string initError),
                "StaffService inicializado. " + initError,
                ref passed, ref failed, log);
            Check(staff.TryCreateEmployee(
                    MakeRequest("Irene"),
                    out BistroBuilderEmployeeRecord employee,
                    out string createError),
                "Empleado creado para guardia runtime. " + createError,
                ref passed, ref failed, log);

            var guard = new BistroBuilderStaff4DTestMutationGuard
            {
                blockedEmployeeId = employee.employeeId
            };
            Check(staff.TryRegisterRuntimeMutationGuard(guard, out string guardError),
                "StaffService acepta una única guardia 4D. " + guardError,
                ref passed, ref failed, log);
            Check(!staff.TrySetAvailability(
                    employee.employeeId,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    out _, out _),
                "Binding activo bloquea cambios persistentes de disponibilidad.",
                ref passed, ref failed, log);
            Check(!staff.TryDismissEmployee(employee.employeeId, out _, out _),
                "Binding activo bloquea despido en la autoridad canónica.",
                ref passed, ref failed, log);
            Check(staff.TryGetEmployee(
                    employee.employeeId,
                    out BistroBuilderEmployeeRecord protectedEmployee) &&
                  protectedEmployee.employmentStatus ==
                    BistroBuilderEmploymentStatus.Active &&
                  protectedEmployee.availability ==
                    BistroBuilderEmployeeAvailability.Available,
                "Mutaciones bloqueadas no alteran staff.state.",
                ref passed, ref failed, log);

            staff.UnregisterRuntimeMutationGuard(guard);
            Check(staff.TrySetAvailability(
                    employee.employeeId,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    out _, out _),
                "Al liberar binding, disponibilidad vuelve a ser gestionable.",
                ref passed, ref failed, log);

            waiterHost = new GameObject("BB_4D_WAITER_SELFTEST_TEMP");
            Waiter waiter = waiterHost.AddComponent<Waiter>();
            Check(waiter.IsStaffServiceEligible && waiter.IsAvailable,
                "Waiter conserva elegibilidad legacy por defecto.",
                ref passed, ref failed, log);
            Check(waiter.TrySetStaffServiceEligibility(false) &&
                  !waiter.IsStaffServiceEligible && !waiter.IsAvailable,
                "Agente sin Employee binding deja de aceptar tareas.",
                ref passed, ref failed, log);
            Check(waiter.TrySetStaffServiceEligibility(true) && waiter.IsAvailable,
                "Reactivar elegibilidad no altera el estado operativo.",
                ref passed, ref failed, log);
            waiter.SetState(WaiterState.TakingOrder);
            Check(!waiter.TrySetStaffServiceEligibility(false),
                "4D no puede desactivar un agente mientras está ocupado.",
                ref passed, ref failed, log);
            waiter.SetState(WaiterState.Idle);
            Check(waiter.TrySetStaffServiceEligibility(false),
                "Agente vuelve a poder liberarse al quedar Idle.",
                ref passed, ref failed, log);

            MethodInfo update = typeof(BistroBuilderStaffSessionService).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(update == null,
                "StaffSessionService no utiliza Update para binding/rendimiento.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción no controlada: " + exception);
        }
        finally
        {
            if (waiterHost != null)
            {
                UnityEngine.Object.DestroyImmediate(waiterHost);
            }
            if (staffHost != null)
            {
                UnityEngine.Object.DestroyImmediate(staffHost);
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

    private static bool TryAppendEmployee(
        BistroBuilderStaffSnapshot source,
        BistroBuilderStaffRoleCatalog catalog,
        string employeeId,
        string firstName,
        out BistroBuilderStaffSnapshot result)
    {
        result = null;
        if (!BistroBuilderStaffEngine.TryBuildEmployee(
                employeeId,
                MakeRequest(firstName),
                catalog,
                out BistroBuilderEmployeeRecord employee,
                out _) ||
            !BistroBuilderStaffEngine.TryAppendEmployee(
                source,
                employee,
                catalog,
                out result,
                out _))
        {
            return false;
        }
        return true;
    }

    private static BistroBuilderEmployeeCreateRequest MakeRequest(string firstName)
    {
        return new BistroBuilderEmployeeCreateRequest
        {
            firstName = firstName,
            lastName = "Prueba",
            roleId = "waiter",
            salaryCentsPerService = 8000L,
            hiredDayIndex = 1,
            initialExperiencePoints = 0L,
            initialSkills = new BistroBuilderEmployeeSkillSet
            {
                speed = 50,
                attentiveness = 50,
                organization = 50,
                hospitality = 50
            },
            availability = BistroBuilderEmployeeAvailability.Available
        };
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
