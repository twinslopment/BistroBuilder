using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3EInstaller
{
    public const string ProfileAssetPath =
        "Assets/Data/BistroBuilder/Finance/operating.expenses.asset";

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3E - Instalar + validar + autotest",
        false,
        3040)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 3E.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant antes de instalar 3E.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de instalar 3E.",
                "Aceptar");
            return;
        }

        GameObject gameSystems = FindGameSystems(scene);
        if (gameSystems == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se encontró GameSystems en la escena activa.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        bool profileCreated = false;

        Undo.SetCurrentGroupName(
            "Instalar 3E Gastos operativos y nóminas");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderFinanceService finance =
                FindSingleSceneComponent<BistroBuilderFinanceService>(scene);
            BistroBuilderGeneralGameStateService generalState =
                FindSingleSceneComponent<
                    BistroBuilderGeneralGameStateService>(scene);
            GameClock clock =
                FindSingleSceneComponent<GameClock>(scene);
            BistroBuilderSaveGameService save =
                FindSingleSceneComponent<BistroBuilderSaveGameService>(scene);

            BistroBuilderOperatingExpenseProfile profile =
                LoadOrCreateProfile(out profileCreated);

            BistroBuilderOperatingExpenseService service =
                GetOrAdd<BistroBuilderOperatingExpenseService>(gameSystems);

            SetReference(service, "financeService", finance);
            SetReference(
                service,
                "generalGameStateService",
                generalState);
            SetReference(service, "gameClock", clock);
            SetReference(service, "saveGameService", save);
            SetReference(service, "expenseProfile", profile);

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 3E.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool validationOk =
                BistroBuilderFinance3EValidator.ValidateCurrentScene(
                    out int validationPassed,
                    out int validationFailed,
                    out string validationReport);

            bool testOk =
                BistroBuilderFinance3ESelfTest.Run(
                    out int testPassed,
                    out int testFailed,
                    out string testReport);

            Debug.Log(validationReport);
            Debug.Log(testReport);

            if (!validationOk || !testOk)
            {
                throw new InvalidOperationException(
                    "La validación automática de 3E no fue limpia. " +
                    "Validación: " + validationPassed + " OK / " +
                    validationFailed + " errores. Autotest: " +
                    testPassed + " OK / " + testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 3E",
                "3E instalado y probado correctamente." +
                "\n\nValidación: " + validationPassed +
                " OK / 0 errores" +
                "\nAutotest: " + testPassed +
                " OK / 0 fallos",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);

            if (profileCreated)
            {
                AssetDatabase.DeleteAsset(ProfileAssetPath);
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 3E falló y la escena fue restaurada." +
                "\n\n" + exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static BistroBuilderOperatingExpenseProfile
        LoadOrCreateProfile(out bool created)
    {
        created = false;

        BistroBuilderOperatingExpenseProfile existing =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderOperatingExpenseProfile>(
                ProfileAssetPath);

        if (existing != null)
        {
            if (!existing.TryValidate(out string existingError))
            {
                throw new InvalidOperationException(
                    "El perfil 3E existente no es válido. " +
                    existingError);
            }

            return existing;
        }

        EnsureFolder("Assets/Data/BistroBuilder/Finance");

        var profile =
            ScriptableObject.CreateInstance<
                BistroBuilderOperatingExpenseProfile>();

        profile.ConfigureForEditor(
            "operating_expenses_v1",
            new List<BistroBuilderRecurringExpenseDefinition>
            {
                new BistroBuilderRecurringExpenseDefinition(
                    "utilities",
                    "Suministros y servicios",
                    "expense.operating.utilities",
                    15000L,
                    7,
                    7),
                new BistroBuilderRecurringExpenseDefinition(
                    "rent",
                    "Alquiler del local",
                    "expense.operating.rent",
                    150000L,
                    30,
                    30)
            });

        if (!profile.TryValidate(out string error))
        {
            UnityEngine.Object.DestroyImmediate(profile);
            throw new InvalidOperationException(
                "No se pudo crear el perfil 3E. " + error);
        }

        AssetDatabase.CreateAsset(profile, ProfileAssetPath);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        created = true;
        return profile;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal))
            {
                return roots[index];
            }
        }

        return null;
    }

    private static T FindSingleSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        T found = null;

        for (int index = 0; index < components.Length; index++)
        {
            T candidate = components[index];

            if (candidate == null ||
                candidate.gameObject.scene != scene)
            {
                continue;
            }

            if (found != null)
            {
                throw new InvalidOperationException(
                    "Existe más de un " + typeof(T).Name +
                    " en la escena.");
            }

            found = candidate;
        }

        if (found == null)
        {
            throw new InvalidOperationException(
                "No se encontró " + typeof(T).Name +
                " en la escena.");
        }

        return found;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null
            ? existing
            : Undo.AddComponent<T>(target);
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property =
            serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe el campo " + fieldName +
                " en " + target.GetType().Name + ".");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup)
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
        }
        catch (Exception rollbackError)
        {
            Debug.LogException(rollbackError);
        }
    }
}
