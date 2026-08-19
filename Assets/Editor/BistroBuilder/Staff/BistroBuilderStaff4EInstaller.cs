using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 4A–4E.
///
/// Puede ejecutarse sobre una escena que todavía no tenga Personal: crea y
/// cablea Staff, Recruitment, Development, Session y los dos providers 4E.
/// Reutiliza Waiter, WaiterTaskCoordinator, service.runtime y SaveGameService
/// existentes; nunca crea autoridades paralelas.
///
/// La escena se respalda byte a byte y se restaura si cualquier gate falla.
/// </summary>
public static class BistroBuilderStaff4EInstaller
{
    private const string RoleCatalogPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";
    private const string DevelopmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffDevelopmentProfile.asset";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4E - Instalar 4A-4E + validar + autotest",
        false,
        3240)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E Personal",
                "Sal de Play Mode antes de instalar 4E.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E Personal",
                "Abre y guarda la escena principal antes de instalar 4E.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E Personal",
                "Guarda la escena antes de instalar 4E.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4A-4E Personal");
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
                    "El perfil de contratación 4B falta o es inválido. " +
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
                    "El perfil de desarrollo 4C falta o es inválido. " +
                    developmentProfileError);
            }

            BistroBuilderSaveGameService[] saves =
                FindSceneComponents<BistroBuilderSaveGameService>(scene);
            BistroBuilderGeneralGameStateService[] generalStates =
                FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
            RestaurantServiceStateService[] serviceStates =
                FindSceneComponents<RestaurantServiceStateService>(scene);
            WaiterTaskCoordinator[] coordinators =
                FindSceneComponents<WaiterTaskCoordinator>(scene);
            BistroBuilderActiveServiceSaveSectionProvider[] serviceProviders =
                FindSceneComponents<BistroBuilderActiveServiceSaveSectionProvider>(scene);
            Waiter[] waiters = FindSceneComponents<Waiter>(scene);

            RequireUniqueExisting(saves, "BistroBuilderSaveGameService");
            RequireUniqueExisting(generalStates, "GeneralGameStateService");
            RequireUniqueExisting(serviceStates, "RestaurantServiceStateService");
            RequireUniqueExisting(coordinators, "WaiterTaskCoordinator");
            RequireUniqueExisting(
                serviceProviders,
                "BistroBuilderActiveServiceSaveSectionProvider");
            if (waiters.Length == 0)
            {
                throw new InvalidOperationException(
                    "4D/4E necesitan al menos un Waiter existente; " +
                    "Personal no crea agentes operativos.");
            }
            ValidateUniqueWaiterIds(waiters);

            if (saves[0].gameObject != gameSystems)
            {
                throw new InvalidOperationException(
                    "BistroBuilderSaveGameService debe vivir en GameSystems.");
            }

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
            AssignObject(
                recruitment,
                "sessionAssignmentQuerySource",
                session);

            BistroBuilderStaffStateSaveSectionProvider stateProvider =
                GetOrAddUnique<BistroBuilderStaffStateSaveSectionProvider>(
                    scene,
                    gameSystems);
            AssignObject(stateProvider, "saveGameService", saves[0]);
            AssignObject(stateProvider, "staffService", staff);

            BistroBuilderStaffSessionSaveSectionProvider sessionProvider =
                GetOrAddUnique<BistroBuilderStaffSessionSaveSectionProvider>(
                    scene,
                    gameSystems);
            AssignObject(sessionProvider, "saveGameService", saves[0]);
            AssignObject(sessionProvider, "staffService", staff);
            AssignObject(sessionProvider, "staffSessionService", session);
            AssignObject(
                sessionProvider,
                "serviceStateService",
                serviceStates[0]);

            if (!staff.ValidateConfiguration(out string staffError))
                throw new InvalidOperationException(staffError);
            if (!recruitment.ValidateConfiguration(out string recruitmentError))
                throw new InvalidOperationException(recruitmentError);
            if (!development.ValidateConfiguration(out string developmentError))
                throw new InvalidOperationException(developmentError);
            if (!session.ValidateConfiguration(out string sessionError))
                throw new InvalidOperationException(sessionError);
            if (!stateProvider.ValidateConfiguration(out string stateProviderError))
                throw new InvalidOperationException(stateProviderError);
            if (!sessionProvider.ValidateConfiguration(out string sessionProviderError))
                throw new InvalidOperationException(sessionProviderError);

            saves[0].RefreshExtensions();
            if (!saves[0].HasProvider(
                    BistroBuilderStaffStateSaveSectionProvider.StableSectionId) ||
                !saves[0].HasProvider(
                    BistroBuilderStaffSessionSaveSectionProvider.StableSectionId))
            {
                throw new InvalidOperationException(
                    "SaveGameService no registró las dos secciones 4E.");
            }
            if (!saves[0].ValidateConfiguration(out string saveError))
            {
                throw new InvalidOperationException(
                    "SaveGameService quedó inválido: " + saveError);
            }

            EditorUtility.SetDirty(staff);
            EditorUtility.SetDirty(recruitment);
            EditorUtility.SetDirty(development);
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(stateProvider);
            EditorUtility.SetDirty(sessionProvider);
            EditorUtility.SetDirty(saves[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4E.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            saves[0].RefreshExtensions();

            BistroBuilderStaff4AValidationResult validation4A =
                BistroBuilderStaff4AValidator.ValidateCurrentScene();
            bool self4A = BistroBuilderStaff4ASelfTest.Run(
                out int passed4A, out int failed4A, out string report4A);
            BistroBuilderStaff4BValidationResult validation4B =
                BistroBuilderStaff4BValidator.ValidateCurrentScene();
            bool self4B = BistroBuilderStaff4BSelfTest.Run(
                out int passed4B, out int failed4B, out string report4B);
            BistroBuilderStaff4CValidationResult validation4C =
                BistroBuilderStaff4CValidator.ValidateCurrentScene();
            bool self4C = BistroBuilderStaff4CSelfTest.Run(
                out int passed4C, out int failed4C, out string report4C);
            BistroBuilderStaff4DValidationResult validation4D =
                BistroBuilderStaff4DValidator.ValidateCurrentScene();
            bool self4D = BistroBuilderStaff4DSelfTest.Run(
                out int passed4D, out int failed4D, out string report4D);
            BistroBuilderStaff4EValidationResult validation4E =
                BistroBuilderStaff4EValidator.ValidateCurrentScene();
            bool self4E = BistroBuilderStaff4ESelfTest.Run(
                out int passed4E, out int failed4E, out string report4E);

            Debug.Log(validation4A.BuildReport());
            Debug.Log(report4A);
            Debug.Log(validation4B.BuildReport());
            Debug.Log(report4B);
            Debug.Log(validation4C.BuildReport());
            Debug.Log(report4C);
            Debug.Log(validation4D.BuildReport());
            Debug.Log(report4D);
            Debug.Log(validation4E.BuildReport());
            Debug.Log(report4E);

            if (validation4A.ErrorCount > 0 || !self4A ||
                validation4B.ErrorCount > 0 || !self4B ||
                validation4C.ErrorCount > 0 || !self4C ||
                validation4D.ErrorCount > 0 || !self4D ||
                validation4E.ErrorCount > 0 || !self4E)
            {
                throw new InvalidOperationException(
                    "Los gates acumulativos 4A–4E no fueron limpios. " +
                    "4A=" + validation4A.ErrorCount + "/" + failed4A +
                    ", 4B=" + validation4B.ErrorCount + "/" + failed4B +
                    ", 4C=" + validation4C.ErrorCount + "/" + failed4C +
                    ", 4D=" + validation4D.ErrorCount + "/" + failed4D +
                    ", 4E=" + validation4E.ErrorCount + "/" + failed4E +
                    " (errores/fallos)." );
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E Personal",
                "4A–4E instalados correctamente." +
                "\n\nAutotests: " +
                "4A " + passed4A + ", 4B " + passed4B +
                ", 4C " + passed4C + ", 4D " + passed4D +
                ", 4E " + passed4E + " OK; 0 fallos." +
                "\nValidaciones 4A–4E: 0 errores." +
                "\n\nSaveGameService y service.runtime siguen siendo autoritativos.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E Personal",
                "La instalación 4A–4E falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void RequireUniqueExisting<T>(T[] values, string name)
        where T : Component
    {
        if (values == null || values.Length != 1)
        {
            throw new InvalidOperationException(
                "4E necesita exactamente un " + name + " existente.");
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
