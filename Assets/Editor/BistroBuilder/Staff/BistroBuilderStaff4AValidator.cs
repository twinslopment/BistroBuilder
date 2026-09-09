using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4AValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;

    public void Correct(string message) => correct.Add(message);
    public void Warning(string message) => warnings.Add(message);
    public void Error(string message) => errors.Add(message);

    public string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — 4A PERSONAL / VALIDACIÓN ===");
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
        List<string> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            builder.AppendLine("[" + prefix + "] " + lines[index]);
        }
    }
}

/// <summary>
/// Gate estructural de 4A. Comprueba además que Personal no haya absorbido
/// responsabilidades de Waiter, tareas, Finanzas o Save antes de sus hitos.
/// </summary>
public static class BistroBuilderStaff4AValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4A - Validar fundación canónica",
        false,
        3201)]
    private static void ValidateMenu()
    {
        BistroBuilderStaff4AValidationResult result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4A Personal",
            "Correctos: " + result.CorrectCount +
            "\nAdvertencias: " + result.WarningCount +
            "\nErrores: " + result.ErrorCount,
            "Aceptar");
    }

    public static BistroBuilderStaff4AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No hay una escena activa válida.");
            return result;
        }
        result.Correct("Escena activa válida.");

        GameObject gameSystems = FindUniqueGameSystems(scene, out int gameSystemsCount);
        if (gameSystemsCount != 1 || gameSystems == null)
        {
            result.Error("Debe existir exactamente un GameSystems en la escena.");
            return result;
        }
        result.Correct("GameSystems canónico único.");

        BistroBuilderStaffService[] staffServices = FindSceneComponents<
            BistroBuilderStaffService>(scene);
        if (staffServices.Length != 1)
        {
            result.Error("Debe existir exactamente un BistroBuilderStaffService.");
            return result;
        }

        BistroBuilderStaffService staff = staffServices[0];
        result.Correct("BistroBuilderStaffService único.");
        if (staff.gameObject == gameSystems)
        {
            result.Correct("Personal vive en GameSystems.");
        }
        else
        {
            result.Error("BistroBuilderStaffService debe vivir en GameSystems.");
        }

        if (staff.RoleCatalog == null)
        {
            result.Error("Personal no tiene catálogo de roles asignado.");
        }
        else if (!staff.RoleCatalog.TryValidate(out string roleError))
        {
            result.Error("Catálogo de roles inválido: " + roleError);
        }
        else
        {
            result.Correct("Catálogo de roles dirigido por datos válido.");
            if (ContainsOperationalAdapter(
                    staff.RoleCatalog,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent))
            {
                result.Correct(
                    "Existe un rol V1 compatible con el adaptador waiter.agent.");
            }
            else
            {
                result.Error(
                    "El catálogo no expone ningún rol para el camarero operativo V1.");
            }
        }

        if (staff.ValidateConfiguration(out string staffError))
        {
            result.Correct("Configuración canónica de Personal válida.");
        }
        else
        {
            result.Error("Configuración de Personal inválida: " + staffError);
        }

        if (Application.isPlaying)
        {
            BistroBuilderStaffSnapshot snapshot = staff.CreateSnapshot();
            string snapshotError = string.Empty;
            bool validSnapshot =
                staff.IsInitialized &&
                snapshot != null &&
                BistroBuilderStaffEngine.TryValidateSnapshot(
                    snapshot,
                    staff.RoleCatalog,
                    out snapshotError);

            if (validSnapshot)
            {
                result.Correct("staff.state runtime íntegro y sin IDs duplicados.");
            }
            else
            {
                result.Error(
                    "staff.state runtime no es válido: " + snapshotError);
            }
        }
        else
        {
            result.Correct(
                "staff.state se inicializa en runtime; 4A no serializa roster en escena.");
        }

        Waiter[] waiters = FindSceneComponents<Waiter>(scene);
        if (waiters.Length == 0)
        {
            result.Warning(
                "La escena no contiene agentes Waiter; 4D no podrá probar binding aquí.");
        }
        else if (HaveUniqueWaiterIds(waiters, out string waiterError))
        {
            result.Correct(
                "Agentes Waiter existentes conservan WaiterId únicos (" +
                waiters.Length + ").");
        }
        else
        {
            result.Error(waiterError);
        }

        WaiterTaskCoordinator[] coordinators = FindSceneComponents<
            WaiterTaskCoordinator>(scene);
        if (coordinators.Length == 1)
        {
            result.Correct(
                "WaiterTaskCoordinator existente permanece como autoridad de tareas.");
        }
        else
        {
            result.Error(
                "La escena debe conservar exactamente un WaiterTaskCoordinator.");
        }

        if (!TypeContainsForbiddenOperationalReference(typeof(BistroBuilderStaffService)))
        {
            result.Correct(
                "StaffService no serializa Waiter, tareas ni autoridades financieras.");
        }
        else
        {
            result.Error(
                "StaffService contiene una referencia operativa/financiera prohibida.");
        }

        if (!ModelContainsUnityObjectReferences())
        {
            result.Correct(
                "Employee/staff.state no contienen referencias directas a GameObjects o UnityEngine.Object.");
        }
        else
        {
            result.Error(
                "El modelo persistente de Personal contiene una referencia Unity prohibida.");
        }

        int staffSaveProviders = CountStaffSaveProviders(scene);
        if (staffSaveProviders <= 1)
        {
            result.Correct(
                staffSaveProviders == 0
                    ? "4A reserva staff.state sin adelantar el Save provider de 4E."
                    : "Existe un único Save provider de staff.state (extensión posterior compatible)."
            );
        }
        else
        {
            result.Error("Existen varios Save providers para staff.state.");
        }

        return result;
    }

    private static bool ContainsOperationalAdapter(
        BistroBuilderStaffRoleCatalog catalog,
        string adapterId)
    {
        var roles = new List<BistroBuilderStaffRoleDefinition>();
        catalog.CopyRoles(roles);
        string expected = BistroBuilderStaffStableIdUtility.Normalize(adapterId);
        for (int index = 0; index < roles.Count; index++)
        {
            BistroBuilderStaffRoleDefinition role = roles[index];
            if (role != null && role.active &&
                string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(
                        role.operationalAdapterId),
                    expected,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HaveUniqueWaiterIds(
        Waiter[] waiters,
        out string error)
    {
        var ids = new HashSet<int>();
        for (int index = 0; index < waiters.Length; index++)
        {
            Waiter waiter = waiters[index];
            if (waiter == null || waiter.WaiterId < 1 || !ids.Add(waiter.WaiterId))
            {
                error = "La escena contiene WaiterId inválidos o duplicados.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static bool TypeContainsForbiddenOperationalReference(Type type)
    {
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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

    private static bool ModelContainsUnityObjectReferences()
    {
        Type[] modelTypes =
        {
            typeof(BistroBuilderEmployeeRecord),
            typeof(BistroBuilderEmployeeSkillSet),
            typeof(BistroBuilderEmployeeResponsibilitySettings),
            typeof(BistroBuilderEmployeePerformanceData),
            typeof(BistroBuilderStaffSnapshot)
        };

        for (int typeIndex = 0; typeIndex < modelTypes.Length; typeIndex++)
        {
            FieldInfo[] fields = modelTypes[typeIndex].GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(fields[fieldIndex].FieldType))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int CountStaffSaveProviders(Scene scene)
    {
        int count = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }
            if (behaviour is IBistroBuilderSaveSectionProvider provider &&
                string.Equals(
                    provider.SectionId,
                    BistroBuilderStaffSnapshot.CurrentSchemaId,
                    StringComparison.Ordinal))
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
            for (int t = 0; t < transforms.Length; t++)
            {
                if (transforms[t] != null &&
                    string.Equals(transforms[t].name, "GameSystems", StringComparison.Ordinal))
                {
                    found = transforms[t].gameObject;
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
