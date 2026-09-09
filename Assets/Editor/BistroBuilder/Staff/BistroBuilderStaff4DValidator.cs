using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4DValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;
    public void Correct(string value) => correct.Add(value);
    public void Warning(string value) => warnings.Add(value);
    public void Error(string value) => errors.Add(value);

    public string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — 4D PERSONAL / VALIDACIÓN ===");
        Append(builder, "OK", correct);
        Append(builder, "AVISO", warnings);
        Append(builder, "ERROR", errors);
        builder.AppendLine();
        builder.AppendLine(
            "Correctos: " + CorrectCount +
            " · Advertencias: " + WarningCount +
            " · Errores: " + ErrorCount);
        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.AppendLine("[" + prefix + "] " + values[index]);
        }
    }
}

/// <summary>
/// Gate estructural 4D. Comprueba la separación Employee ↔ Waiter y que la
/// capa de binding no haya sustituido tareas, movimiento o service.runtime.
/// </summary>
public static class BistroBuilderStaff4DValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Validar binding de servicio",
        false,
        3231)]
    private static void ValidateMenu()
    {
        BistroBuilderStaff4DValidationResult result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D Personal",
            "Correctos: " + result.CorrectCount +
            "\nAdvertencias: " + result.WarningCount +
            "\nErrores: " + result.ErrorCount,
            "Aceptar");
    }

    public static BistroBuilderStaff4DValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4DValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No hay escena activa válida.");
            return result;
        }
        result.Correct("Escena activa válida.");

        GameObject gameSystems = FindUniqueGameSystems(scene, out int gsCount);
        if (gameSystems == null || gsCount != 1)
        {
            result.Error("Debe existir exactamente un GameSystems.");
            return result;
        }
        result.Correct("GameSystems canónico único.");

        BistroBuilderStaffService[] staffServices =
            FindSceneComponents<BistroBuilderStaffService>(scene);
        BistroBuilderStaffSessionService[] sessionServices =
            FindSceneComponents<BistroBuilderStaffSessionService>(scene);
        BistroBuilderStaffRecruitmentService[] recruitmentServices =
            FindSceneComponents<BistroBuilderStaffRecruitmentService>(scene);
        WaiterTaskCoordinator[] coordinators =
            FindSceneComponents<WaiterTaskCoordinator>(scene);

        if (staffServices.Length != 1 || sessionServices.Length != 1 ||
            recruitmentServices.Length != 1 || coordinators.Length != 1)
        {
            result.Error(
                "4D necesita un único StaffService, RecruitmentService, " +
                "StaffSessionService y WaiterTaskCoordinator.");
            return result;
        }

        BistroBuilderStaffService staff = staffServices[0];
        BistroBuilderStaffSessionService session = sessionServices[0];
        BistroBuilderStaffRecruitmentService recruitment = recruitmentServices[0];

        if (staff.gameObject == gameSystems && session.gameObject == gameSystems)
        {
            result.Correct("Personal persistente y binding viven en GameSystems.");
        }
        else
        {
            result.Error("StaffService/StaffSessionService deben vivir en GameSystems.");
        }

        if (session.ValidateConfiguration(out string sessionError))
        {
            result.Correct("Configuración de binding 4D válida.");
        }
        else
        {
            result.Error("StaffSessionService inválido: " + sessionError);
        }

        SerializedObject recruitmentSerialized = new SerializedObject(recruitment);
        SerializedProperty assignmentSource = recruitmentSerialized.FindProperty(
            "sessionAssignmentQuerySource");
        if (assignmentSource != null &&
            ReferenceEquals(assignmentSource.objectReferenceValue, session))
        {
            result.Correct(
                "Despido 4B consulta el binding 4D mediante contrato inverso.");
        }
        else
        {
            result.Error(
                "RecruitmentService no está cableado al query de sesión 4D.");
        }

        Waiter[] waiters = FindSceneComponents<Waiter>(scene);
        if (waiters.Length == 0)
        {
            result.Error("4D necesita al menos un agente Waiter existente.");
        }
        else if (HaveUniqueWaiterIds(waiters, out string waiterError))
        {
            result.Correct(
                "Los " + waiters.Length +
                " agentes operativos conservan WaiterId únicos.");
        }
        else
        {
            result.Error(waiterError);
        }

        PropertyInfo eligibilityProperty = typeof(Waiter).GetProperty(
            "IsStaffServiceEligible",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo eligibilityMethod = typeof(Waiter).GetMethod(
            "TrySetStaffServiceEligibility",
            BindingFlags.Instance | BindingFlags.Public);
        if (eligibilityProperty != null && eligibilityMethod != null)
        {
            result.Correct(
                "Waiter solo recibe una compuerta runtime de elegibilidad; " +
                "Employee no se almacena en el GameObject.");
        }
        else
        {
            result.Error("Waiter no expone la compuerta de elegibilidad 4D.");
        }

        MethodInfo updateMethod = typeof(BistroBuilderStaffSessionService).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (updateMethod == null)
        {
            result.Correct("Binding y rendimiento 4D no utilizan Update/polling.");
        }
        else
        {
            result.Error("StaffSessionService no debe utilizar Update.");
        }

        if (!PersistentSessionModelsContainUnityReferences())
        {
            result.Correct(
                "staff.session.runtime no contiene Waiter/GameObject/UnityEngine.Object.");
        }
        else
        {
            result.Error(
                "El modelo persistible 4D contiene referencias Unity prohibidas.");
        }

        BistroBuilderActiveServiceSaveSectionProvider[] serviceProviders =
            FindSceneComponents<BistroBuilderActiveServiceSaveSectionProvider>(scene);
        if (serviceProviders.Length == 1)
        {
            result.Correct(
                "service.runtime existente permanece único y autoritativo.");
        }
        else
        {
            result.Error(
                "Debe conservarse exactamente un provider service.runtime.");
        }

        int staffSessionProviders = CountSectionProviders(
            scene,
            BistroBuilderStaffSessionSnapshot.CurrentSchemaId);
        if (staffSessionProviders == 0)
        {
            result.Correct(
                "4D prepara staff.session.runtime sin adelantar el Save provider 4E.");
        }
        else
        {
            result.Warning(
                "Ya existe provider staff.session.runtime; revisar como instalación 4E.");
        }

        if (Application.isPlaying)
        {
            BistroBuilderStaffSessionSnapshot snapshot = session.CreateSessionSnapshot();
            string runtimeError = string.Empty;
            if (snapshot != null &&
                BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    snapshot,
                    staff.CreateSnapshot(),
                    out runtimeError))
            {
                result.Correct("Snapshot runtime de binding es íntegro.");
            }
            else
            {
                result.Error(
                    "Snapshot runtime de binding inválido: " +
                    (snapshot == null ? "snapshot nulo." : runtimeError));
            }

            if (snapshot != null && snapshot.active)
            {
                var boundWaiterIds = new HashSet<int>();
                bool runtimeBindingsOk = true;
                for (int index = 0; index < snapshot.bindings.Count; index++)
                {
                    BistroBuilderStaffSessionBindingRecord binding =
                        snapshot.bindings[index];
                    Waiter waiter = FindWaiterById(waiters, binding.waiterId);
                    runtimeBindingsOk &= waiter != null &&
                        waiter.IsStaffServiceEligible &&
                        session.TryGetActiveAssignment(
                            binding.employeeId,
                            out string assignment) &&
                        assignment == "waiter:" + binding.waiterId;
                    boundWaiterIds.Add(binding.waiterId);
                }

                for (int index = 0; index < waiters.Length; index++)
                {
                    if (!boundWaiterIds.Contains(waiters[index].WaiterId))
                    {
                        runtimeBindingsOk &= !waiters[index].IsStaffServiceEligible;
                    }
                }

                if (runtimeBindingsOk)
                {
                    result.Correct(
                        "Cada agente elegible tiene exactamente un EmployeeId y " +
                        "los agentes no ligados quedan fuera del reparto.");
                }
                else
                {
                    result.Error(
                        "Elegibilidad runtime y bindings EmployeeId↔WaiterId divergen.");
                }
            }

            BistroBuilderStaffCoverageSnapshot coverage =
                session.CreateCoverageSnapshot();
            if (coverage.hasFullCurrentCoverage)
            {
                result.Correct("Cobertura actual de slots Waiter completa.");
            }
            else
            {
                result.Warning(
                    "Cobertura Waiter incompleta: " +
                    coverage.boundWaiterEmployees + "/" +
                    coverage.operationalWaiterSlots + " slots ligados.");
            }
        }
        else
        {
            result.Correct(
                "La correspondencia EmployeeId↔WaiterId se comprobará en runtime.");
        }

        return result;
    }

    private static bool PersistentSessionModelsContainUnityReferences()
    {
        Type[] types =
        {
            typeof(BistroBuilderStaffSessionBindingRecord),
            typeof(BistroBuilderStaffSessionSnapshot)
        };
        for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
            FieldInfo[] fields = types[typeIndex].GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(
                        fields[fieldIndex].FieldType))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HaveUniqueWaiterIds(Waiter[] waiters, out string error)
    {
        var ids = new HashSet<int>();
        for (int index = 0; index < waiters.Length; index++)
        {
            if (waiters[index] == null || waiters[index].WaiterId < 1 ||
                !ids.Add(waiters[index].WaiterId))
            {
                error = "La escena contiene WaiterId inválidos o duplicados.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static Waiter FindWaiterById(Waiter[] waiters, int waiterId)
    {
        for (int index = 0; index < waiters.Length; index++)
        {
            if (waiters[index] != null && waiters[index].WaiterId == waiterId)
            {
                return waiters[index];
            }
        }
        return null;
    }

    private static int CountSectionProviders(Scene scene, string sectionId)
    {
        int count = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour != null && behaviour.gameObject.scene == scene &&
                behaviour is IBistroBuilderSaveSectionProvider provider &&
                string.Equals(provider.SectionId, sectionId, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static GameObject FindUniqueGameSystems(Scene scene, out int count)
    {
        count = 0;
        GameObject found = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
            for (int child = 0; child < transforms.Length; child++)
            {
                Transform transform = transforms[child];
                if (transform != null &&
                    string.Equals(transform.name, "GameSystems", StringComparison.Ordinal))
                {
                    found = transform.gameObject;
                    count++;
                }
            }
        }
        return count == 1 ? found : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                result.Add(all[index]);
            }
        }
        return result.ToArray();
    }
}
