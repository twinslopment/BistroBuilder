using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional de reputación persistente y visitas recurrentes.
/// </summary>
public static class BistroBuilderMarketingGuestRelationsInstaller
{
    [MenuItem("Tools/Bistro Builder/Marketing/Guest Relations - Instalar + validar", false, 7252)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Marketing", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Marketing", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        if (!TryInstall(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar GuestRelations.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar.";
            return false;
        }

        bool selfOk = BistroBuilderMarketingGuestRelationsSelfTest.Run(
            out int prePassed,
            out int preFailed,
            out string preReport);
        Debug.Log(preReport);
        if (!selfOk)
        {
            report = "El autotest previo falló: " + prePassed +
                     " OK / " + preFailed + " fallos.";
            return false;
        }


        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);

        try
        {
            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            TableAssignmentSystem tables =
                RequireUnique<TableAssignmentSystem>(scene);
            BistroBuilderMarketingService marketing =
                RequireUnique<BistroBuilderMarketingService>(scene);
            BistroBuilderMarketingDemandIntegrationService demand =
                RequireUnique<BistroBuilderMarketingDemandIntegrationService>(scene);
            BistroBuilderSaveGameService save =
                RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderReputationService reputation =
                RequireUnique<BistroBuilderReputationService>(scene);

            BistroBuilderGuestRelationsService relations =
                EnsureUniqueOnHost<BistroBuilderGuestRelationsService>(
                    scene, gameSystems);
            BistroBuilderMarketingGuestRelationsBridge bridge =
                EnsureUniqueOnHost<BistroBuilderMarketingGuestRelationsBridge>(
                    scene, gameSystems);
            BistroBuilderGuestRelationsSaveSectionProvider provider =
                EnsureUniqueOnHost<BistroBuilderGuestRelationsSaveSectionProvider>(
                    scene, gameSystems);

            SerializedObject relationsSo = new SerializedObject(relations);
            SetObject(relationsSo, "generalGameStateService", general);
            SetObject(relationsSo, "tableAssignmentSystem", tables);
            SetObject(relationsSo, "reputationService", reputation);
            relationsSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bridgeSo = new SerializedObject(bridge);
            SetObject(bridgeSo, "marketingService", marketing);
            SetObject(bridgeSo, "reputationService", reputation);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject providerSo = new SerializedObject(provider);
            SetObject(providerSo, "saveGameService", save);
            SetObject(providerSo, "guestRelationsService", relations);
            SetObject(providerSo, "marketingBridge", bridge);
            providerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject demandSo = new SerializedObject(demand);
            SetObject(demandSo, "guestRelationsService", relations);
            SetObject(demandSo, "reputationService", reputation);
            demandSo.ApplyModifiedPropertiesWithoutUndo();

            save.RefreshExtensions();
            if (!relations.ValidateConfiguration(out string relationsError))
                throw new InvalidOperationException(relationsError);
            if (!bridge.ValidateConfiguration(out string bridgeError))
                throw new InvalidOperationException(bridgeError);
            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException(providerError);
            if (!demand.ValidateConfiguration(out string demandError))
                throw new InvalidOperationException(demandError);
            if (!save.HasProvider(
                    BistroBuilderGuestRelationsSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException(
                    "SaveGame no descubrió guest_relations.state.");

            EditorUtility.SetDirty(relations);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(demand);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar GuestRelations.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMarketingGuestRelationsValidationResult validation =
                BistroBuilderMarketingGuestRelationsValidator.ValidateCurrentScene();
            bool finalOk = BistroBuilderMarketingGuestRelationsSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !finalOk)
                throw new InvalidOperationException(
                    "GuestRelations no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "GuestRelations instalado correctamente.\n" +
                     validation.BuildReport() + "\nAutotest: " +
                     passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(
                    scene.path,
                    ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError)
            {
                Debug.LogException(rollbackError);
            }

            report = "La instalación GuestRelations falló y la escena fue " +
                     "restaurada. " + exception.Message;
            return false;
        }
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

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host)
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
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < found.Length; index++)
                if (found[index] != null) result.Add(found[index]);
        }
        return result.ToArray();
    }
}
