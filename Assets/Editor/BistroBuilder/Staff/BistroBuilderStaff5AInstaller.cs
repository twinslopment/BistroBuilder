using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 5A. Añade únicamente la autoridad de planificación
/// y su perfil; reutiliza Staff, calendario y estado de servicio existentes.
/// </summary>
public static class BistroBuilderStaff5AInstaller
{
    private const string ProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffScheduleProfile.asset";

    [MenuItem("Tools/Bistro Builder/Personal/5A - Instalar horarios + validar", false, 3271)]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5A Horarios",
                "Sal de Play Mode antes de instalar 5A.", "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5A Horarios",
                "Abre la escena principal, guárdala y vuelve a ejecutar.", "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        bool profileCreated = false;

        try
        {
            bool preOk = BistroBuilderStaff5AFoundationSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport);
            Debug.Log(preReport);
            if (!preOk)
                throw new InvalidOperationException(
                    "Gate 5A previo: " + preFailed + " fallos / " + prePassed + " OK.");

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException("No existe exactamente un GameSystems canónico.");

            BistroBuilderStaffService staff = RequireUnique<BistroBuilderStaffService>(scene);
            BistroBuilderGeneralGameStateService calendar =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            RestaurantServiceStateService serviceState =
                RequireUnique<RestaurantServiceStateService>(scene);

            BistroBuilderStaffScheduleProfile profile =
                AssetDatabase.LoadAssetAtPath<BistroBuilderStaffScheduleProfile>(ProfilePath);
            if (profile == null)
            {
                EnsureFolder("Assets/Resources/BistroBuilder/Staff");
                profile = ScriptableObject.CreateInstance<BistroBuilderStaffScheduleProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                profileCreated = true;
            }
            if (!profile.TryValidate(out string profileError))
                throw new InvalidOperationException(profileError);

            BistroBuilderStaffScheduleService schedule =
                EnsureUnique<BistroBuilderStaffScheduleService>(scene, gameSystems);
            Assign(schedule, "staffService", staff);
            Assign(schedule, "generalGameStateService", calendar);
            Assign(schedule, "serviceStateService", serviceState);
            Assign(schedule, "scheduleProfile", profile);
            if (!schedule.ValidateConfiguration(out string error))
                throw new InvalidOperationException(error);

            EditorUtility.SetDirty(schedule);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena 5A.");

            BistroBuilderStaff5AValidationResult validation =
                BistroBuilderStaff5AValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderStaff5AFoundationSelfTest.Run(
                out int passed, out int failed, out string report);
            Debug.Log(validation.BuildReport());
            Debug.Log(report);
            if (validation.errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "5A no superó gates: " + validation.errors +
                    " errores / " + failed + " fallos.");

            EditorUtility.DisplayDialog("Bistro Builder — 5A Horarios",
                "5A instalado: " + validation.correct + " OK / " +
                validation.warnings + " avisos / 0 errores; " + passed +
                " autotests OK.\n\nPendiente Play Mode real.", "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (profileCreated)
            {
                AssetDatabase.DeleteAsset(ProfilePath);
                AssetDatabase.SaveAssets();
            }
            EditorUtility.DisplayDialog("Bistro Builder — 5A Horarios",
                "La instalación falló y la escena fue restaurada.\n\n" +
                exception.Message, "Aceptar");
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash <= 0) return;
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && transform.name == "GameSystems")
            {
                found = transform.gameObject;
                count++;
            }
        }
        return count == 1 ? found : null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException("Se esperaba exactamente un " +
                typeof(T).Name + " y hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUnique<T>(Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length > 1)
            throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T item in all)
            if (item != null && item.gameObject.scene == scene) list.Add(item);
        return list.ToArray();
    }

    private static void Assign(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException("No existe la propiedad " + name + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

public sealed class BistroBuilderStaff5AValidationResult
{
    public int correct;
    public int warnings;
    public int errors;
    public readonly List<string> lines = new List<string>();

    public string BuildReport()
    {
        return "5A — Validación Horarios\n" + string.Join("\n", lines) +
               "\nResultado: " + correct + " OK / " + warnings +
               " avisos / " + errors + " errores";
    }
}

public static class BistroBuilderStaff5AValidator
{
    [MenuItem("Tools/Bistro Builder/Personal/5A - Validar horarios", false, 3272)]
    private static void RunMenu()
    {
        BistroBuilderStaff5AValidationResult result = ValidateCurrentScene();
        if (result.errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderStaff5AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff5AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Fail(result, "No hay escena activa.");
            return result;
        }

        BistroBuilderStaffScheduleService[] schedules = FindScene<BistroBuilderStaffScheduleService>(scene);
        if (schedules.Length != 1)
        {
            Fail(result, "Debe existir una única StaffScheduleService; hay " + schedules.Length + ".");
            return result;
        }
        Pass(result, "Existe una única autoridad StaffScheduleService.");
        BistroBuilderStaffScheduleService schedule = schedules[0];

        if (schedule.ValidateConfiguration(out string error)) Pass(result, "Servicio configurado.");
        else Fail(result, error);

        string profileError = string.Empty;
        if (schedule.ScheduleProfile != null &&
            schedule.ScheduleProfile.TryValidate(out profileError))
            Pass(result, "Perfil canónico de turnos válido.");
        else
            Fail(result, "Perfil de turnos inválido: " + profileError);

        BistroBuilderStaffScheduleSnapshot snapshot = schedule.CreateSnapshot();
        if (snapshot != null && snapshot.schemaId == BistroBuilderStaffScheduleSnapshot.CurrentSchemaId)
            Pass(result, "Snapshot staff.schedule V1 disponible.");
        else
            Fail(result, "No se expone staff.schedule V1.");
        return result;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T item in all)
            if (item != null && item.gameObject.scene == scene) list.Add(item);
        return list.ToArray();
    }

    private static void Pass(BistroBuilderStaff5AValidationResult r, string text)
    { r.correct++; r.lines.Add("[OK] " + text); }
    private static void Fail(BistroBuilderStaff5AValidationResult r, string text)
    { r.errors++; r.lines.Add("[ERROR] " + text); }
}
