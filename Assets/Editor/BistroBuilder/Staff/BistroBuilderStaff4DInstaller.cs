using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 4A+4B+4C+4D.
///
/// Añade/cablea únicamente las autoridades de Personal necesarias hasta el
/// binding EmployeeId ↔ WaiterId. No crea camareros, no sustituye el reparto
/// de tareas, no toca Finanzas y no instala todavía providers de Save 4E.
///
/// La operación es idempotente y conserva una copia byte a byte de la escena.
/// Si cualquier gate acumulativo falla, restaura la escena anterior.
/// </summary>
public static class BistroBuilderStaff4DInstaller
{
    private const string RoleCatalogPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";
    private const string DevelopmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffDevelopmentProfile.asset";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Instalar + validar + autotest",
        false,
        3230)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4D Personal",
                "Sal de Play Mode antes de instalar 4D.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4D Personal",
                "Abre y guarda la escena principal antes de instalar 4D.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4D Personal",
                "Guarda la escena antes de instalar 4D.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4D Personal");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems en la escena.");
            }

            BistroBuilderStaffRoleCatalog roleCatalog =
                AssetDatabase.LoadAssetAtPath<BistroBuilderStaffRoleCatalog>(
                    RoleCatalogPath);
            string roleError = string.Empty;
            if (roleCatalog == null || !roleCatalog.TryValidate(out roleError))
            {
                throw new InvalidOperationException(
                    "El catálogo canónico de roles 4A falta o es inválido. " +
                    roleError);
            }

            BistroBuilderStaffRecruitmentProfile recruitmentProfile =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderStaffRecruitmentProfile>(
                    RecruitmentProfilePath);
            string recruitmentProfileError = string.Empty;
            if (recruitmentProfile == null ||
                !recruitmentProfile.TryValidate(
                    roleCatalog,
                    out recruitmentProfileError))
            {
                throw new InvalidOperationException(
                    "El perfil canónico de contratación 4B falta o es inválido. " +
                    recruitmentProfileError);
            }

            BistroBuilderStaffDevelopmentProfile developmentProfile =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderStaffDevelopmentProfile>(
                    DevelopmentProfilePath);
            string developmentProfileError = string.Empty;
            if (developmentProfile == null ||
                !developmentProfile.TryValidate(out developmentProfileError))
            {
                throw new InvalidOperationException(
                    "El perfil canónico de desarrollo 4C falta o es inválido. " +
                    developmentProfileError);
            }

            BistroBuilderGeneralGameStateService[] generalStates =
                FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
            RestaurantServiceStateService[] serviceStates =
                FindSceneComponents<RestaurantServiceStateService>(scene);
            WaiterTaskCoordinator[] coordinators =
                FindSceneComponents<WaiterTaskCoordinator>(scene);
            Waiter[] waiters = FindSceneComponents<Waiter>(scene);

            if (generalStates.Length != 1)
            {
                throw new InvalidOperationException(
                    "4D necesita exactamente un GeneralGameStateService.");
            }
            if (serviceStates.Length != 1)
            {
                throw new InvalidOperationException(
                    "4D necesita exactamente un RestaurantServiceStateService.");
            }
            if (coordinators.Length != 1)
            {
                throw new InvalidOperationException(
                    "4D necesita exactamente un WaiterTaskCoordinator existente.");
            }
            if (waiters.Length == 0)
            {
                throw new InvalidOperationException(
                    "4D necesita al menos un agente Waiter existente; " +
                    "el instalador no crea camareros operativos.");
            }
            ValidateUniqueWaiterIds(waiters);

            BistroBuilderStaffService staff = GetOrAddUnique<
                BistroBuilderStaffService>(scene, gameSystems);
            AssignObject(staff, "roleCatalog", roleCatalog);

            BistroBuilderStaffRecruitmentService recruitment = GetOrAddUnique<
                BistroBuilderStaffRecruitmentService>(scene, gameSystems);
            AssignObject(recruitment, "staffService", staff);
            AssignObject(
                recruitment,
                "generalGameStateService",
                generalStates[0]);
            AssignObject(
                recruitment,
                "recruitmentProfile",
                recruitmentProfile);

            BistroBuilderStaffDevelopmentService development = GetOrAddUnique<
                BistroBuilderStaffDevelopmentService>(scene, gameSystems);
            AssignObject(development, "staffService", staff);
            AssignObject(
                development,
                "generalGameStateService",
                generalStates[0]);
            AssignObject(
                development,
                "developmentProfile",
                developmentProfile);

            BistroBuilderStaffSessionService session = GetOrAddUnique<
                BistroBuilderStaffSessionService>(scene, gameSystems);
            AssignObject(session, "staffService", staff);
            AssignObject(session, "developmentService", development);
            AssignObject(
                session,
                "generalGameStateService",
                generalStates[0]);
            AssignObject(
                session,
                "restaurantServiceStateService",
                serviceStates[0]);
            AssignObject(
                session,
                "waiterTaskCoordinator",
                coordinators[0]);
            AssignObject(
                session,
                "recruitmentProfile",
                recruitmentProfile);

            // 4B consulta 4D mediante su contrato inverso; no conoce la clase
            // concreta desde el dominio de contratación.
            AssignObject(
                recruitment,
                "sessionAssignmentQuerySource",
                session);

            if (!staff.ValidateConfiguration(out string staffError))
            {
                throw new InvalidOperationException(staffError);
            }
            if (!recruitment.ValidateConfiguration(out string recruitmentError))
            {
                throw new InvalidOperationException(recruitmentError);
            }
            if (!development.ValidateConfiguration(out string developmentError))
            {
                throw new InvalidOperationException(developmentError);
            }
            if (!session.ValidateConfiguration(out string sessionError))
            {
                throw new InvalidOperationException(sessionError);
            }

            EditorUtility.SetDirty(staff);
            EditorUtility.SetDirty(recruitment);
            EditorUtility.SetDirty(development);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4D.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderStaff4AValidationResult validation4A =
                BistroBuilderStaff4AValidator.ValidateCurrentScene();
            bool self4A = BistroBuilderStaff4ASelfTest.Run(
                out int passed4A,
                out int failed4A,
                out string report4A);

            BistroBuilderStaff4BValidationResult validation4B =
                BistroBuilderStaff4BValidator.ValidateCurrentScene();
            bool self4B = BistroBuilderStaff4BSelfTest.Run(
                out int passed4B,
                out int failed4B,
                out string report4B);

            BistroBuilderStaff4CValidationResult validation4C =
                BistroBuilderStaff4CValidator.ValidateCurrentScene();
            bool self4C = BistroBuilderStaff4CSelfTest.Run(
                out int passed4C,
                out int failed4C,
                out string report4C);

            BistroBuilderStaff4DValidationResult validation4D =
                BistroBuilderStaff4DValidator.ValidateCurrentScene();
            bool self4D = BistroBuilderStaff4DSelfTest.Run(
                out int passed4D,
                out int failed4D,
                out string report4D);

            Debug.Log(validation4A.BuildReport());
            Debug.Log(report4A);
            Debug.Log(validation4B.BuildReport());
            Debug.Log(report4B);
            Debug.Log(validation4C.BuildReport());
            Debug.Log(report4C);
            Debug.Log(validation4D.BuildReport());
            Debug.Log(report4D);

            if (validation4A.ErrorCount > 0 || !self4A ||
                validation4B.ErrorCount > 0 || !self4B ||
                validation4C.ErrorCount > 0 || !self4C ||
                validation4D.ErrorCount > 0 || !self4D)
            {
                throw new InvalidOperationException(
                    "Los gates acumulativos 4A–4D no fueron limpios. " +
                    "4A: " + validation4A.ErrorCount + " errores, " +
                    failed4A + " fallos. 4B: " +
                    validation4B.ErrorCount + " errores, " +
                    failed4B + " fallos. 4C: " +
                    validation4C.ErrorCount + " errores, " +
                    failed4C + " fallos. 4D: " +
                    validation4D.ErrorCount + " errores, " +
                    failed4D + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4D Personal",
                "4A + 4B + 4C + 4D instalados correctamente." +
                "\n\n4A validación: " + validation4A.CorrectCount +
                " OK / " + validation4A.WarningCount + " avisos / 0 errores" +
                "\n4A autotest: " + passed4A + " OK / 0 fallos" +
                "\n4B validación: " + validation4B.CorrectCount +
                " OK / " + validation4B.WarningCount + " avisos / 0 errores" +
                "\n4B autotest: " + passed4B + " OK / 0 fallos" +
                "\n4C validación: " + validation4C.CorrectCount +
                " OK / " + validation4C.WarningCount + " avisos / 0 errores" +
                "\n4C autotest: " + passed4C + " OK / 0 fallos" +
                "\n4D validación: " + validation4D.CorrectCount +
                " OK / " + validation4D.WarningCount + " avisos / 0 errores" +
                "\n4D autotest: " + passed4D + " OK / 0 fallos" +
                "\n\nWaiterTaskCoordinator, Finanzas y Save permanecen autoritativos.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4D Personal",
                "La instalación 4D falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static T GetOrAddUnique<T>(
        Scene scene,
        GameObject gameSystems)
        where T : Component
    {
        T[] existing = FindSceneComponents<T>(scene);
        if (existing.Length > 1)
        {
            throw new InvalidOperationException(
                "La escena contiene varios " + typeof(T).Name + ".");
        }

        T component = existing.Length == 1
            ? existing[0]
            : Undo.AddComponent<T>(gameSystems);
        if (component.gameObject != gameSystems)
        {
            throw new InvalidOperationException(
                typeof(T).Name + " debe vivir en GameSystems.");
        }
        return component;
    }

    private static void ValidateUniqueWaiterIds(Waiter[] waiters)
    {
        var ids = new HashSet<int>();
        for (int index = 0; index < waiters.Length; index++)
        {
            Waiter waiter = waiters[index];
            if (waiter == null || waiter.WaiterId < 1 || !ids.Add(waiter.WaiterId))
            {
                throw new InvalidOperationException(
                    "La escena contiene WaiterId inválidos o duplicados.");
            }
        }
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
                "No existe la propiedad serializada " + propertyName +
                " en " + target.GetType().Name + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
            for (int child = 0; child < transforms.Length; child++)
            {
                Transform transform = transforms[child];
                if (transform != null &&
                    string.Equals(
                        transform.name,
                        "GameSystems",
                        StringComparison.Ordinal))
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

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup)
    {
        File.WriteAllBytes(absoluteScenePath, backup);
        AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }
}
