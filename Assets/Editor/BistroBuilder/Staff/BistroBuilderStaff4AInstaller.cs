using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente y transaccional de 4A. Añade únicamente la
/// autoridad persistente de Personal y su catálogo de roles. No toca Waiter,
/// tareas, service.runtime, Finanzas ni proveedores Save.
/// </summary>
public static class BistroBuilderStaff4AInstaller
{
    public const string RoleCatalogAssetPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4A - Instalar + validar + autotest",
        false,
        3200)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4A Personal",
                "Sal de Play Mode antes de instalar 4A.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4A Personal",
                "Abre y guarda la escena principal antes de instalar 4A.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4A Personal",
                "Guarda la escena antes de instalar 4A.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);
        bool createdCatalog = false;
        var createdFolders = new List<string>();

        Undo.SetCurrentGroupName("Instalar 4A Personal");
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
                    RoleCatalogAssetPath);

            if (roleCatalog == null)
            {
                EnsureFolder("Assets/Resources", createdFolders);
                EnsureFolder("Assets/Resources/BistroBuilder", createdFolders);
                EnsureFolder("Assets/Resources/BistroBuilder/Staff", createdFolders);

                roleCatalog = ScriptableObject.CreateInstance<
                    BistroBuilderStaffRoleCatalog>();
                roleCatalog.InitializeV1DefaultsIfEmpty();
                if (!roleCatalog.TryValidate(out string newCatalogError))
                {
                    throw new InvalidOperationException(newCatalogError);
                }

                AssetDatabase.CreateAsset(roleCatalog, RoleCatalogAssetPath);
                createdCatalog = true;
            }
            else if (!roleCatalog.TryValidate(out string catalogError))
            {
                throw new InvalidOperationException(
                    "El catálogo existente de Personal es inválido y no se " +
                    "modificará automáticamente. " + catalogError);
            }

            BistroBuilderStaffService[] existing =
                FindSceneComponents<BistroBuilderStaffService>(scene);
            if (existing.Length > 1)
            {
                throw new InvalidOperationException(
                    "La escena contiene varios BistroBuilderStaffService.");
            }

            BistroBuilderStaffService staff = existing.Length == 1
                ? existing[0]
                : Undo.AddComponent<BistroBuilderStaffService>(gameSystems);
            if (staff.gameObject != gameSystems)
            {
                throw new InvalidOperationException(
                    "El StaffService existente no vive en GameSystems.");
            }

            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty roleProperty = serialized.FindProperty("roleCatalog");
            if (roleProperty == null)
            {
                throw new InvalidOperationException(
                    "BistroBuilderStaffService no expone roleCatalog serializado.");
            }
            roleProperty.objectReferenceValue = roleCatalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!staff.ValidateConfiguration(out string staffError))
            {
                throw new InvalidOperationException(staffError);
            }

            EditorUtility.SetDirty(roleCatalog);
            EditorUtility.SetDirty(staff);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4A.");
            }

            AssetDatabase.Refresh();

            BistroBuilderStaff4AValidationResult validation =
                BistroBuilderStaff4AValidator.ValidateCurrentScene();
            bool selfTestOk = BistroBuilderStaff4ASelfTest.Run(
                out int testPassed,
                out int testFailed,
                out string testReport);

            Debug.Log(validation.BuildReport());
            Debug.Log(testReport);

            if (validation.ErrorCount > 0 || !selfTestOk)
            {
                throw new InvalidOperationException(
                    "Los gates automáticos 4A no fueron limpios. " +
                    "Validación: " + validation.CorrectCount + " OK / " +
                    validation.WarningCount + " avisos / " +
                    validation.ErrorCount + " errores. Autotest: " +
                    testPassed + " OK / " + testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4A Personal",
                "4A instalado correctamente." +
                "\n\nValidación: " + validation.CorrectCount + " OK / " +
                validation.WarningCount + " avisos / 0 errores" +
                "\nAutotest: " + testPassed + " OK / 0 fallos" +
                "\n\nNo se ha modificado ningún sistema operativo de camareros.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, sceneBackup);

            if (createdCatalog)
            {
                AssetDatabase.DeleteAsset(RoleCatalogAssetPath);
            }
            CleanupCreatedFolders(createdFolders);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4A Personal",
                "La instalación 4A falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void EnsureFolder(
        string path,
        List<string> createdFolders)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int separator = path.LastIndexOf('/');
        if (separator <= 0)
        {
            throw new InvalidOperationException(
                "Ruta de carpeta Asset inválida: " + path);
        }

        string parent = path.Substring(0, separator);
        string name = path.Substring(separator + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent, createdFolders);
        }

        string guid = AssetDatabase.CreateFolder(parent, name);
        if (string.IsNullOrWhiteSpace(guid))
        {
            throw new InvalidOperationException(
                "No se pudo crear la carpeta " + path + ".");
        }
        createdFolders.Add(path);
    }

    private static void CleanupCreatedFolders(List<string> createdFolders)
    {
        for (int index = createdFolders.Count - 1; index >= 0; index--)
        {
            string path = createdFolders[index];
            if (!AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            string[] assets = AssetDatabase.FindAssets(string.Empty, new[] { path });
            if (assets.Length == 0)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
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
