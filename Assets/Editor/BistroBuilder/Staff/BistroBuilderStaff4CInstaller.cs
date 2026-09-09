using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 4A+4B+4C. Añade únicamente las autoridades de
/// Personal necesarias hasta desarrollo/rendimiento y ejecuta todos los gates
/// previos. No modifica Waiter, tareas, Finanzas ni proveedores Save.
/// </summary>
public static class BistroBuilderStaff4CInstaller
{
    private const string RoleCatalogPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";
    private const string DevelopmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffDevelopmentProfile.asset";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4C - Instalar + validar + autotest",
        false,
        3220)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4C Personal",
                "Sal de Play Mode antes de instalar 4C.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4C Personal",
                "Abre y guarda la escena principal antes de instalar 4C.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4C Personal",
                "Guarda la escena antes de instalar 4C.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4C Personal");
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
            if (generalStates.Length != 1)
            {
                throw new InvalidOperationException(
                    "4C necesita exactamente un GeneralGameStateService.");
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
            // sessionAssignmentQuerySource se conserva si ya existe. 4C nunca
            // lo limpia ni lo sustituye; 4D será su propietario.

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

            EditorUtility.SetDirty(staff);
            EditorUtility.SetDirty(recruitment);
            EditorUtility.SetDirty(development);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4C.");
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

            Debug.Log(validation4A.BuildReport());
            Debug.Log(report4A);
            Debug.Log(validation4B.BuildReport());
            Debug.Log(report4B);
            Debug.Log(validation4C.BuildReport());
            Debug.Log(report4C);

            if (validation4A.ErrorCount > 0 || !self4A ||
                validation4B.ErrorCount > 0 || !self4B ||
                validation4C.ErrorCount > 0 || !self4C)
            {
                throw new InvalidOperationException(
                    "Los gates acumulativos 4A–4C no fueron limpios. " +
                    "4A: " + validation4A.ErrorCount + " errores, " +
                    failed4A + " fallos. 4B: " +
                    validation4B.ErrorCount + " errores, " +
                    failed4B + " fallos. 4C: " +
                    validation4C.ErrorCount + " errores, " +
                    failed4C + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4C Personal",
                "4A + 4B + 4C instalados correctamente." +
                "\n\n4A validación: " + validation4A.CorrectCount +
                " OK / " + validation4A.WarningCount + " avisos / 0 errores" +
                "\n4A autotest: " + passed4A + " OK / 0 fallos" +
                "\n4B validación: " + validation4B.CorrectCount +
                " OK / " + validation4B.WarningCount + " avisos / 0 errores" +
                "\n4B autotest: " + passed4B + " OK / 0 fallos" +
                "\n4C validación: " + validation4C.CorrectCount +
                " OK / " + validation4C.WarningCount + " avisos / 0 errores" +
                "\n4C autotest: " + passed4C + " OK / 0 fallos" +
                "\n\nWaiter, tareas, Finanzas y Save permanecen intactos.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4C Personal",
                "La instalación 4C falló y la escena fue restaurada.\n\n" +
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
