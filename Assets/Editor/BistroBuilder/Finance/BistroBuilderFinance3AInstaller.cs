using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3AInstaller
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3A - Instalar + validar + autotest", false, 3000)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 3A.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant antes de instalar 3A.",
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

        BistroBuilderSaveGameService save = gameSystems.GetComponent<BistroBuilderSaveGameService>();
        if (save == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "GameSystems no contiene BistroBuilderSaveGameService.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 3A Núcleo financiero");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            GetOrAdd<BistroBuilderFinanceService>(gameSystems);
            GetOrAdd<BistroBuilderFinanceSaveSectionProvider>(gameSystems);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 3A.");
            }

            bool validationOk = BistroBuilderFinance3AValidator.ValidateCurrentScene(
                out int validationPassed,
                out int validationFailed,
                out string validationReport);
            bool testOk = BistroBuilderFinance3ASelfTest.Run(
                out int testPassed,
                out int testFailed,
                out string testReport);

            Debug.Log(validationReport);
            Debug.Log(testReport);

            bool ok = validationOk && testOk;
            EditorUtility.DisplayDialog(
                "Bistro Builder — 3A",
                (ok
                    ? "3A instalado y probado correctamente."
                    : "3A necesita revisión.") +
                "\n\nValidación: " + validationPassed + " OK / " + validationFailed + " errores" +
                "\nAutotest: " + testPassed + " OK / " + testFailed + " fallos",
                "Aceptar");

            if (!ok)
            {
                Debug.LogError(
                    "3A — Instalación automática terminada con fallos. " +
                    "Revisa los informes de validación y autotest.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 3A falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            GameObject root = roots[index];
            if (root != null &&
                string.Equals(root.name, "GameSystems", StringComparison.Ordinal))
            {
                return root;
            }
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
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
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception rollbackError)
        {
            Debug.LogException(rollbackError);
        }
    }
}
