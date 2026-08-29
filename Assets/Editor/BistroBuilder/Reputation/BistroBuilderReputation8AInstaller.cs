using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional 8A. Separa la autoridad de reputación de
/// GuestRelations, conserva compatibilidad con Marketing y registra persistencia.
/// </summary>
public static class BistroBuilderReputation8AInstaller
{
    [MenuItem("Tools/Bistro Builder/Reputation/8A - Instalar + validar", false, 8103)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Reputación 8A", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Reputación 8A", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar Reputación 8A.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar Reputación 8A.";
            return false;
        }

        if (!BistroBuilderReputation8ASelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 8A falló: " + prePassed + " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        try
        {
            GameObject host = FindUniqueGameSystems(scene);
            if (host == null) throw new InvalidOperationException("No existe exactamente un GameSystems canónico.");

            BistroBuilderSaveGameService save = RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderGuestRelationsService relations = RequireUnique<BistroBuilderGuestRelationsService>(scene);
            BistroBuilderMarketingGuestRelationsBridge bridge = RequireUnique<BistroBuilderMarketingGuestRelationsBridge>(scene);
            BistroBuilderMarketingDemandIntegrationService demand = RequireUnique<BistroBuilderMarketingDemandIntegrationService>(scene);
            BistroBuilderMarketingPlayerFacade facade = RequireUnique<BistroBuilderMarketingPlayerFacade>(scene);

            BistroBuilderReputationService reputation =
                EnsureUniqueOnHost<BistroBuilderReputationService>(scene, host);
            BistroBuilderReputationSaveSectionProvider provider =
                EnsureUniqueOnHost<BistroBuilderReputationSaveSectionProvider>(scene, host);

            SetObject(relations, "reputationService", reputation);
            SetObject(bridge, "reputationService", reputation);
            SetObject(demand, "reputationService", reputation);
            SetObject(facade, "reputationService", reputation);
            SetObject(provider, "saveGameService", save);
            SetObject(provider, "reputationService", reputation);
            SetObject(provider, "guestRelationsService", relations);

            int legacyPoints = relations.LegacyStoredReputationPoints;
            if (legacyPoints > 0 && !reputation.TryApplyExternalReputationCredit(
                    "migration.guest_relations.v1", legacyPoints, out _, out string migrationError))
                throw new InvalidOperationException("No pudo migrarse reputación legacy: " + migrationError);

            save.RefreshExtensions();
            if (!reputation.ValidateConfiguration(out string reputationError))
                throw new InvalidOperationException(reputationError);
            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException(providerError);
            if (!relations.ValidateConfiguration(out string relationsError))
                throw new InvalidOperationException(relationsError);
            if (!bridge.ValidateConfiguration(out string bridgeError))
                throw new InvalidOperationException(bridgeError);
            if (!demand.ValidateConfiguration(out string demandError))
                throw new InvalidOperationException(demandError);
            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);
            if (!save.HasProvider(BistroBuilderReputationSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException("SaveGame no descubrió reputation.state.");

            EditorUtility.SetDirty(reputation);
            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(relations);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(demand);
            EditorUtility.SetDirty(facade);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 8A.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderReputation8AValidationResult validation =
                BistroBuilderReputation8AValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderReputation8ASelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("8A no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "Reputación 8A instalada correctamente.\n" +
                     validation.BuildReport() + "\nAutotest: " + passed +
                     " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 8A falló y la escena fue restaurada. " + exception.Message;
            return false;
        }
    }

    private static void SetObject(Component component, string fieldName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null) throw new InvalidOperationException(
            component.GetType().Name + " no expone " + fieldName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == "GameSystems")
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1) throw new InvalidOperationException(
            "Se esperaba exactamente un " + typeof(T).Name + "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1) throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host) throw new InvalidOperationException(
            typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < found.Length; index++) if (found[index] != null) result.Add(found[index]);
        }
        return result.ToArray();
    }
}
