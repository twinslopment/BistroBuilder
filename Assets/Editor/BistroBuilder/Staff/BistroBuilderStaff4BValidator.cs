using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4BValidationResult
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
        builder.AppendLine("=== BISTRO BUILDER — 4B PERSONAL / VALIDACIÓN ===");
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
/// Gate estructural 4B. Verifica que contratación no haya absorbido dinero,
/// agentes operativos ni persistencia antes de sus hitos correspondientes.
/// </summary>
public static class BistroBuilderStaff4BValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4B - Validar contratación y despido",
        false,
        3211)]
    private static void ValidateMenu()
    {
        BistroBuilderStaff4BValidationResult result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4B Personal",
            "Correctos: " + result.CorrectCount +
            "\nAdvertencias: " + result.WarningCount +
            "\nErrores: " + result.ErrorCount,
            "Aceptar");
    }

    public static BistroBuilderStaff4BValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4BValidationResult();
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
            result.Error("Debe existir exactamente un GameSystems.");
            return result;
        }
        result.Correct("GameSystems canónico único.");

        BistroBuilderStaffService[] staffServices =
            FindSceneComponents<BistroBuilderStaffService>(scene);
        if (staffServices.Length != 1)
        {
            result.Error("4B necesita exactamente un StaffService canónico.");
            return result;
        }
        BistroBuilderStaffService staff = staffServices[0];
        if (staff.gameObject == gameSystems)
        {
            result.Correct("StaffService único permanece en GameSystems.");
        }
        else
        {
            result.Error("StaffService debe permanecer en GameSystems.");
        }

        BistroBuilderStaffRecruitmentService[] recruitmentServices =
            FindSceneComponents<BistroBuilderStaffRecruitmentService>(scene);
        if (recruitmentServices.Length != 1)
        {
            result.Error(
                "Debe existir exactamente un StaffRecruitmentService.");
            return result;
        }

        BistroBuilderStaffRecruitmentService recruitment =
            recruitmentServices[0];
        if (recruitment.gameObject == gameSystems)
        {
            result.Correct("RecruitmentService vive en GameSystems.");
        }
        else
        {
            result.Error("RecruitmentService debe vivir en GameSystems.");
        }

        if (staff.ValidateConfiguration(out string staffError))
        {
            result.Correct("Autoridad de plantilla válida.");
        }
        else
        {
            result.Error("StaffService inválido: " + staffError);
        }

        if (recruitment.ValidateConfiguration(out string recruitmentError))
        {
            result.Correct("Configuración de contratación válida.");
        }
        else
        {
            result.Error("RecruitmentService inválido: " + recruitmentError);
        }

        BistroBuilderStaffRecruitmentProfile profile =
            recruitment.RecruitmentProfile;
        if (profile != null &&
            profile.TryValidate(staff.RoleCatalog, out string profileError))
        {
            result.Correct("Perfil de candidatos dirigido por datos válido.");
        }
        else
        {
            result.Error(
                "Perfil de candidatos inválido: " +
                (profile == null ? "nulo" : profileError));
        }

        if (!ContainsForbiddenReference(
                typeof(BistroBuilderStaffRecruitmentService)))
        {
            result.Correct(
                "Contratación no referencia Waiter, tareas ni autoridades financieras.");
        }
        else
        {
            result.Error(
                "RecruitmentService contiene una dependencia operativa/financiera prohibida.");
        }

        FieldInfo assignmentSource = typeof(BistroBuilderStaffRecruitmentService)
            .GetField(
                "sessionAssignmentQuerySource",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (assignmentSource != null &&
            assignmentSource.FieldType == typeof(MonoBehaviour))
        {
            result.Correct(
                "4D se integra mediante contrato de consulta, no mediante Waiter directo.");
        }
        else
        {
            result.Error("No existe el punto de extensión de binding previsto para 4D.");
        }

        if (Application.isPlaying)
        {
            if (recruitment.EnsureMarketReady(out string marketError) &&
                recruitment.CreateMarketSnapshot() is
                    BistroBuilderStaffRecruitmentSnapshot market &&
                BistroBuilderStaffRecruitmentEngine.TryValidateSnapshot(
                    market,
                    profile,
                    staff.RoleCatalog,
                    false,
                    out marketError))
            {
                result.Correct(
                    "Mercado runtime íntegro con " +
                    market.candidates.Count + " candidato(s).");
            }
            else
            {
                result.Error("Mercado runtime inválido: " + marketError);
            }
        }
        else
        {
            result.Correct(
                "Mercado se genera en runtime; no se serializan candidatos en escena.");
        }

        WaiterTaskCoordinator[] coordinators =
            FindSceneComponents<WaiterTaskCoordinator>(scene);
        if (coordinators.Length == 1)
        {
            result.Correct(
                "WaiterTaskCoordinator sigue siendo la única autoridad de tareas.");
        }
        else
        {
            result.Error(
                "La escena debe conservar un único WaiterTaskCoordinator.");
        }

        int recruitmentSaveProviders = CountSectionProviders(
            scene,
            BistroBuilderStaffRecruitmentSnapshot.CurrentSchemaId);
        if (recruitmentSaveProviders == 0)
        {
            result.Correct(
                "4B no adelanta persistencia de mercado; queda reservada para 4E.");
        }
        else
        {
            result.Warning(
                "Existe ya un provider de staff.recruitment.state; revisar en 4E.");
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
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }

            if (behaviour is IBistroBuilderSaveSectionProvider provider &&
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
