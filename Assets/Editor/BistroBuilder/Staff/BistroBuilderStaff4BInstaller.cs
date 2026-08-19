using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 4A+4B. Si 4A todavía no fue instalado en la escena,
/// añade StaffService y lo cablea antes de instalar RecruitmentService.
/// Es idempotente y restaura la escena byte a byte si cualquier gate falla.
/// </summary>
public static class BistroBuilderStaff4BInstaller
{
    private const string RoleCatalogPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4B - Instalar + validar + autotest",
        false,
        3210)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4B Personal",
                "Sal de Play Mode antes de instalar 4B.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4B Personal",
                "Abre y guarda la escena principal antes de instalar 4B.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4B Personal",
                "Guarda la escena antes de instalar 4B.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4B Personal");
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
            if (roleCatalog == null ||
                !roleCatalog.TryValidate(out string roleError))
            {
                throw new InvalidOperationException(
                    "El catálogo canónico de roles 4A falta o es inválido. " +
                    roleError);
            }

            BistroBuilderStaffRecruitmentProfile recruitmentProfile =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderStaffRecruitmentProfile>(
                    RecruitmentProfilePath);
            if (recruitmentProfile == null ||
                !recruitmentProfile.TryValidate(roleCatalog, out string profileError))
            {
                throw new InvalidOperationException(
                    "El perfil canónico de contratación 4B falta o es inválido. " +
                    profileError);
            }

            BistroBuilderGeneralGameStateService[] generalStates =
                FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
            if (generalStates.Length != 1)
            {
                throw new InvalidOperationException(
                    "4B necesita exactamente un GeneralGameStateService.");
            }

            BistroBuilderStaffService[] staffServices =
                FindSceneComponents<BistroBuilderStaffService>(scene);
            if (staffServices.Length > 1)
            {
                throw new InvalidOperationException(
                    "La escena contiene varios StaffService.");
            }

            BistroBuilderStaffService staff = staffServices.Length == 1
                ? staffServices[0]
                : Undo.AddComponent<BistroBuilderStaffService>(gameSystems);
            if (staff.gameObject != gameSystems)
            {
                throw new InvalidOperationException(
                    "StaffService existente no vive en GameSystems.");
            }
            AssignObject(staff, "roleCatalog", roleCatalog);

            BistroBuilderStaffRecruitmentService[] recruitmentServices =
                FindSceneComponents<BistroBuilderStaffRecruitmentService>(scene);
            if (recruitmentServices.Length > 1)
            {
                throw new InvalidOperationException(
                    "La escena contiene varios StaffRecruitmentService.");
            }

            BistroBuilderStaffRecruitmentService recruitment =
                recruitmentServices.Length == 1
                    ? recruitmentServices[0]
                    : Undo.AddComponent<BistroBuilderStaffRecruitmentService>(
                        gameSystems);
            if (recruitment.gameObject != gameSystems)
            {
                throw new InvalidOperationException(
                    "StaffRecruitmentService existente no vive en GameSystems.");
            }

            AssignObject(recruitment, "staffService", staff);
            AssignObject(
                recruitment,
                "generalGameStateService",
                generalStates[0]);
            AssignObject(
                recruitment,
                "recruitmentProfile",
                recruitmentProfile);

            if (!staff.ValidateConfiguration(out string staffError))
            {
                throw new InvalidOperationException(staffError);
            }
            if (!recruitment.ValidateConfiguration(out string recruitmentError))
            {
                throw new InvalidOperationException(recruitmentError);
            }

            EditorUtility.SetDirty(staff);
            EditorUtility.SetDirty(recruitment);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4B.");
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

            Debug.Log(validation4A.BuildReport());
            Debug.Log(report4A);
            Debug.Log(validation4B.BuildReport());
            Debug.Log(report4B);

            if (validation4A.ErrorCount > 0 || !self4A ||
                validation4B.ErrorCount > 0 || !self4B)
            {
                throw new InvalidOperationException(
                    "Los gates acumulativos 4A–4B no fueron limpios. " +
                    "4A: " + validation4A.ErrorCount + " errores, " +
                    failed4A + " fallos. 4B: " +
                    validation4B.ErrorCount + " errores, " +
                    failed4B + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4B Personal",
                "4A + 4B instalados correctamente." +
                "\n\n4A validación: " + validation4A.CorrectCount +
                " OK / " + validation4A.WarningCount + " avisos / 0 errores" +
                "\n4A autotest: " + passed4A + " OK / 0 fallos" +
                "\n4B validación: " + validation4B.CorrectCount +
                " OK / " + validation4B.WarningCount + " avisos / 0 errores" +
                "\n4B autotest: " + passed4B + " OK / 0 fallos" +
                "\n\nNo se han modificado Waiter, tareas, Finanzas ni Save.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, sceneBackup);
            AssetDatabase.ImportAsset(
                scenePath,
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4B Personal",
                "La instalación 4B falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
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
                "No existe la propiedad serializada " + propertyName + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        int count = 0;
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
