using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4CValidationResult
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
        builder.AppendLine("=== BISTRO BUILDER — 4C PERSONAL / VALIDACIÓN ===");
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

public static class BistroBuilderStaff4CValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4C - Validar desarrollo y rendimiento",
        false,
        3221)]
    private static void ValidateMenu()
    {
        BistroBuilderStaff4CValidationResult result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4C Personal",
            "Correctos: " + result.CorrectCount +
            "\nAdvertencias: " + result.WarningCount +
            "\nErrores: " + result.ErrorCount,
            "Aceptar");
    }

    public static BistroBuilderStaff4CValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4CValidationResult();
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
        BistroBuilderStaffDevelopmentService[] developmentServices =
            FindSceneComponents<BistroBuilderStaffDevelopmentService>(scene);

        if (staffServices.Length != 1)
        {
            result.Error("4C necesita exactamente un StaffService.");
            return result;
        }
        if (developmentServices.Length != 1)
        {
            result.Error("4C necesita exactamente un StaffDevelopmentService.");
            return result;
        }

        BistroBuilderStaffService staff = staffServices[0];
        BistroBuilderStaffDevelopmentService development =
            developmentServices[0];

        if (staff.gameObject == gameSystems && development.gameObject == gameSystems)
        {
            result.Correct("Autoridades de Personal 4A–4C viven en GameSystems.");
        }
        else
        {
            result.Error("StaffService y DevelopmentService deben vivir en GameSystems.");
        }

        if (staff.ValidateConfiguration(out string staffError))
        {
            result.Correct("staff.state y desarrollo de empleados son íntegros.");
        }
        else
        {
            result.Error("StaffService inválido: " + staffError);
        }

        if (development.ValidateConfiguration(out string developmentError))
        {
            result.Correct("Configuración de DevelopmentService válida.");
        }
        else
        {
            result.Error("DevelopmentService inválido: " + developmentError);
        }

        BistroBuilderStaffDevelopmentProfile profile = development.DevelopmentProfile;
        if (profile != null && profile.TryValidate(out string profileError))
        {
            result.Correct("Perfil de progresión/formación dirigido por datos válido.");
            bool anyPaid = false;
            for (int index = 0; index < profile.Trainings.Count; index++)
            {
                if (profile.Trainings[index] != null &&
                    profile.Trainings[index].financialCostCents > 0L)
                {
                    anyPaid = true;
                    break;
                }
            }
            if (anyPaid)
            {
                result.Warning(
                    "Hay formación con coste: 4C la bloqueará hasta integrar Finanzas de forma atómica.");
            }
            else
            {
                result.Correct(
                    "Formación V1 no crea movimientos monetarios mientras Bloque 3 sigue pendiente.");
            }
        }
        else
        {
            result.Error("Perfil 4C ausente o inválido.");
        }

        if (!ContainsForbiddenReference(typeof(BistroBuilderStaffDevelopmentService)))
        {
            result.Correct(
                "4C no referencia Waiter, tareas ni autoridades de Finanzas.");
        }
        else
        {
            result.Error("4C contiene una referencia operativa/financiera prohibida.");
        }

        MethodInfo update = typeof(BistroBuilderStaffDevelopmentService).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (update == null)
        {
            result.Correct("DevelopmentService no utiliza Update/polling por frame.");
        }
        else
        {
            result.Error("DevelopmentService no debe reconstruir progreso mediante Update.");
        }

        if (Application.isPlaying)
        {
            var employees = new List<BistroBuilderEmployeeRecord>();
            staff.CopyEmployees(employees, true);
            bool valid = true;
            for (int index = 0; index < employees.Count; index++)
            {
                BistroBuilderEmployeeRecord employee = employees[index];
                valid &= employee != null &&
                    BistroBuilderStaffDevelopmentEngine.TryValidateDevelopmentData(
                        employee.development,
                        out _);
                if (profile != null && employee != null)
                {
                    int level = BistroBuilderStaffDevelopmentEngine.GetLevelForExperience(
                        employee.experiencePoints,
                        profile);
                    valid &= level >= 1 && level <= profile.MaximumLevel;
                }
            }
            if (valid)
            {
                result.Correct(
                    "Todos los empleados runtime tienen progreso/desarrollo coherente.");
            }
            else
            {
                result.Error("Existe un empleado runtime con desarrollo inválido.");
            }
        }
        else
        {
            result.Correct("4C se validará también sobre roster runtime tras instalación.");
        }

        int developmentSaveProviders = CountSectionProviders(
            scene,
            "staff.development.state");
        if (developmentSaveProviders == 0)
        {
            result.Correct(
                "4C no adelanta un segundo estado Save; desarrollo vive dentro de Employee/staff.state.");
        }
        else
        {
            result.Error(
                "No debe existir una segunda sección staff.development.state separada.");
        }

        return result;
    }

    private static bool ContainsForbiddenReference(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int index = 0; index < fields.Length; index++)
        {
            Type fieldType = fields[index].FieldType;
            if (typeof(Waiter).IsAssignableFrom(fieldType) ||
                typeof(WaiterTaskCoordinator).IsAssignableFrom(fieldType) ||
                typeof(BistroBuilderFinanceService).IsAssignableFrom(fieldType) ||
                typeof(BistroBuilderOperatingExpenseService).IsAssignableFrom(fieldType))
            {
                return true;
            }
        }
        return false;
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
