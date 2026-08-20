using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador 4E v2 no destructivo. Añade únicamente las tres extensiones de
/// persistencia de Personal al GameSystems existente y las cablea contra las
/// autoridades ya instaladas. No sustituye SaveGame, service.runtime ni 4D.
///
/// Si cualquier gate falla, restaura la escena original desde copia binaria.
/// </summary>
public static class BistroBuilderStaff4EInstallerV2
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E v2 - Instalar + validar + autotest",
        false,
        3240)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E v2 Personal",
                "Sal de Play Mode antes de instalar 4E v2.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E v2 Personal",
                "Abre y guarda la escena principal antes de instalar 4E v2.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E v2 Personal",
                "Guarda la escena antes de instalar 4E v2.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4E v2 Personal");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            // 4E no puede instalarse sobre un 4D con gates pendientes. Este
            // autotest incluye comprobación de wiring real en StaffSessionService.
            bool hardeningOk = BistroBuilderStaff4DHardeningSelfTest.Run(
                out int hardeningPassed,
                out int hardeningFailed,
                out string hardeningReport);
            Debug.Log(hardeningReport);
            if (!hardeningOk)
            {
                throw new InvalidOperationException(
                    "4D todavía no supera su gate de endurecimiento: " +
                    hardeningFailed + " fallos / " + hardeningPassed +
                    " correctos. 4E no modificará la escena.");
            }

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems en la escena.");
            }

            BistroBuilderSaveGameService save =
                RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderStaffService staff =
                RequireUnique<BistroBuilderStaffService>(scene);
            BistroBuilderStaffRecruitmentService recruitment =
                RequireUnique<BistroBuilderStaffRecruitmentService>(scene);
            BistroBuilderStaffSessionService session =
                RequireUnique<BistroBuilderStaffSessionService>(scene);
            RestaurantServiceStateService serviceState =
                RequireUnique<RestaurantServiceStateService>(scene);
            BistroBuilderActiveServiceSaveSectionProvider activeService =
                RequireUnique<BistroBuilderActiveServiceSaveSectionProvider>(scene);

            if (save.gameObject != gameSystems ||
                staff.gameObject != gameSystems ||
                recruitment.gameObject != gameSystems ||
                session.gameObject != gameSystems)
            {
                throw new InvalidOperationException(
                    "SaveGame y Personal deben vivir en el GameSystems canónico.");
            }

            BistroBuilderStaffStateSaveSectionProvider stateProvider =
                EnsureUniqueComponent<BistroBuilderStaffStateSaveSectionProvider>(
                    scene,
                    gameSystems);
            BistroBuilderStaffRecruitmentSaveSectionProvider recruitmentProvider =
                EnsureUniqueComponent<
                    BistroBuilderStaffRecruitmentSaveSectionProvider>(
                    scene,
                    gameSystems);
            BistroBuilderStaffSessionSaveSectionProvider sessionProvider =
                EnsureUniqueComponent<BistroBuilderStaffSessionSaveSectionProvider>(
                    scene,
                    gameSystems);

            AssignObject(stateProvider, "saveGameService", save);
            AssignObject(stateProvider, "staffService", staff);

            AssignObject(recruitmentProvider, "saveGameService", save);
            AssignObject(recruitmentProvider, "staffService", staff);
            AssignObject(
                recruitmentProvider,
                "recruitmentService",
                recruitment);

            AssignObject(sessionProvider, "saveGameService", save);
            AssignObject(sessionProvider, "staffService", staff);
            AssignObject(sessionProvider, "staffSessionService", session);
            AssignObject(sessionProvider, "serviceStateService", serviceState);

            if (!stateProvider.ValidateConfiguration(out string stateError))
            {
                throw new InvalidOperationException(
                    "staff.state inválido tras instalación: " + stateError);
            }
            if (!recruitmentProvider.ValidateConfiguration(
                    out string recruitmentError))
            {
                throw new InvalidOperationException(
                    "staff.recruitment inválido tras instalación: " +
                    recruitmentError);
            }
            if (!sessionProvider.ValidateConfiguration(out string sessionError))
            {
                throw new InvalidOperationException(
                    "staff.session.runtime inválido tras instalación: " +
                    sessionError);
            }

            if (!(stateProvider.ApplyOrder < recruitmentProvider.ApplyOrder &&
                  recruitmentProvider.ApplyOrder < activeService.ApplyOrder &&
                  activeService.ApplyOrder < sessionProvider.ApplyOrder))
            {
                throw new InvalidOperationException(
                    "El orden Apply 4E no conserva Staff -> mercado -> " +
                    "service.runtime -> binding.");
            }

            // SaveGameService ordena Prepare de mayor a menor. El limpiador
            // operativo service.runtime debe ejecutarse antes que Personal.
            if (!(activeService.PrepareOrder > sessionProvider.PrepareOrder &&
                  sessionProvider.PrepareOrder > recruitmentProvider.PrepareOrder &&
                  recruitmentProvider.PrepareOrder > stateProvider.PrepareOrder))
            {
                throw new InvalidOperationException(
                    "El orden Prepare 4E no conserva service.runtime -> " +
                    "binding -> mercado -> Staff con sort descendente.");
            }

            if (!(stateProvider.FinalizeOrder < recruitmentProvider.FinalizeOrder &&
                  recruitmentProvider.FinalizeOrder < activeService.FinalizeOrder &&
                  activeService.FinalizeOrder < sessionProvider.FinalizeOrder))
            {
                throw new InvalidOperationException(
                    "El orden Finalize 4E no conserva Staff -> mercado -> " +
                    "service.runtime -> binding.");
            }

            EditorUtility.SetDirty(stateProvider);
            EditorUtility.SetDirty(recruitmentProvider);
            EditorUtility.SetDirty(sessionProvider);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4E v2.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            save.RefreshExtensions();

            BistroBuilderStaff4EValidatorV2.Result validation =
                BistroBuilderStaff4EValidatorV2.ValidateCurrentScene();
            bool selfTestOk = BistroBuilderStaff4ESelfTestV2.Run(
                out int passed,
                out int failed,
                out string selfTestReport);

            Debug.Log(validation.BuildReport());
            Debug.Log(selfTestReport);

            if (validation.errors > 0 || !selfTestOk)
            {
                throw new InvalidOperationException(
                    "Los gates 4E v2 no fueron limpios. Validación: " +
                    validation.errors + " errores. Autotest: " +
                    failed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4E v2 Personal",
                "Persistencia de Personal instalada.\n\n" +
                "4D hardening: " + hardeningPassed + " OK / 0 fallos\n" +
                "Validación: " + validation.correct + " OK / " +
                validation.warnings + " avisos / 0 errores\n" +
                "Autotest: " + passed + " OK / 0 fallos\n\n" +
                "Pendiente todavía: compilación/gates reales 4D–4E en Unity " +
                "y prueba Save/Load en servicio activo.",
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
                "Bistro Builder — 4E v2 Personal",
                "La instalación 4E v2 falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                " y existen " + matches.Length + ".");
        }
        return matches[0];
    }

    private static T EnsureUniqueComponent<T>(
        Scene scene,
        GameObject host) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "La escena contiene varios " + typeof(T).Name + ".");
        }

        T component = matches.Length == 1
            ? matches[0]
            : Undo.AddComponent<T>(host);

        if (component.gameObject != host)
        {
            throw new InvalidOperationException(
                typeof(T).Name + " existente no vive en GameSystems.");
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
        for (int root = 0; root < roots.Length; root++)
        {
            Transform[] transforms =
                roots[root].GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform transform = transforms[index];
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
            T component = all[index];
            if (component != null && component.gameObject.scene == scene)
            {
                result.Add(component);
            }
        }
        return result.ToArray();
    }
}
