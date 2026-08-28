using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional e idempotente de 7A — Fundación de Marketing.
/// Publica el catálogo 7x5 y conecta Marketing con autoridades ya existentes.
/// </summary>
public static class BistroBuilderMarketing7AInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/7A - Instalar + validar + autotest",
        false,
        702)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 7A Marketing", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 7A Marketing", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar 7A.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 7A.";
            return false;
        }

        bool preOk = BistroBuilderMarketing7ASelfTest.Run(
            out int prePassed,
            out int preFailed,
            out string preReport);
        Debug.Log(preReport);
        if (!preOk)
        {
            report = "Gate 7A previo falló: " + prePassed +
                     " OK / " + preFailed + " fallos.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        BistroBuilderMarketingCampaignCatalog catalog = null;
        bool catalogWasCreated = false;
        string catalogBackupJson = string.Empty;

        try
        {
            EnsureDataFolder();
            AssetDatabase.Refresh();

            catalog = AssetDatabase.LoadAssetAtPath<
                BistroBuilderMarketingCampaignCatalog>(
                BistroBuilderMarketing7APaths.CatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    BistroBuilderMarketingCampaignCatalog>();
                AssetDatabase.CreateAsset(
                    catalog,
                    BistroBuilderMarketing7APaths.CatalogAsset);
                catalogWasCreated = true;
            }
            else
            {
                catalogBackupJson = EditorJsonUtility.ToJson(catalog);
                Undo.RecordObject(catalog, "Publicar catálogo Marketing 7A");
            }

            catalog.EditorReplaceAll(
                BistroBuilderMarketing7ASeedFactory.CreateSeed());
            EditorUtility.SetDirty(catalog);

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderDiscretionaryFinanceService finance =
                RequireUnique<BistroBuilderDiscretionaryFinanceService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderMarketingService marketing =
                EnsureUniqueOnHost<BistroBuilderMarketingService>(
                    scene,
                    gameSystems);

            SerializedObject serialized = new SerializedObject(marketing);
            SetObject(serialized, "campaignCatalog", catalog);
            SetObject(serialized, "discretionaryFinanceService", finance);
            SetObject(serialized, "generalGameStateService", general);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!marketing.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException(serviceError);

            EditorUtility.SetDirty(marketing);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 7A.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMarketing7AValidationResult validation =
                BistroBuilderMarketing7AValidator.ValidateCurrentScene();
            bool finalOk = BistroBuilderMarketing7ASelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !finalOk)
                throw new InvalidOperationException(
                    "7A no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report =
                "7A instalado correctamente.\n" +
                "Catálogo: 35 campañas (7 familias x 5).\n" +
                validation.BuildReport() + "\n" +
                "Autotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Rollback(
                scene.path,
                absoluteScene,
                sceneBackup,
                catalog,
                catalogWasCreated,
                catalogBackupJson);
            report =
                "La instalación 7A falló y se restauró el estado previo. " +
                exception.Message;
            return false;
        }
    }

    private static void EnsureDataFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(BistroBuilderMarketing7APaths.DataFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Marketing");
    }

    private static void SetObject(
        SerializedObject serialized,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(
                serialized.targetObject.name + " no expone " + fieldName + ".");
        property.objectReferenceValue = value;
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
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(
        Scene scene,
        GameObject host)
        where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1)
            throw new InvalidOperationException(
                "Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1
            ? matches[0]
            : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(
                typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < values.Length; i++)
                if (values[i] != null) result.Add(values[i]);
        }
        return result.ToArray();
    }

    private static void Rollback(
        string scenePath,
        string absoluteScene,
        byte[] sceneBackup,
        BistroBuilderMarketingCampaignCatalog catalog,
        bool catalogWasCreated,
        string catalogBackupJson)
    {
        try
        {
            File.WriteAllBytes(absoluteScene, sceneBackup);
            if (catalogWasCreated)
            {
                AssetDatabase.DeleteAsset(
                    BistroBuilderMarketing7APaths.CatalogAsset);
            }
            else if (catalog != null &&
                     !string.IsNullOrWhiteSpace(catalogBackupJson))
            {
                EditorJsonUtility.FromJsonOverwrite(
                    catalogBackupJson,
                    catalog);
                EditorUtility.SetDirty(catalog);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                scenePath,
                ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception rollbackError)
        {
            Debug.LogException(rollbackError);
        }
    }
}
