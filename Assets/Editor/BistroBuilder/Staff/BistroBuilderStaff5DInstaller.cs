using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador acumulativo 5D de staff.schedule sobre el SaveGame universal.
/// </summary>
public static class BistroBuilderStaff5DInstaller
{
    [MenuItem("Tools/Bistro Builder/Personal/5D - Instalar persistencia horarios", false, 3277)]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5D", "Sal de Play Mode antes de instalar.", "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5D", "Abre y guarda la escena principal.", "Aceptar");
            return;
        }

        string scenePath = scene.path;
        byte[] backup = File.ReadAllBytes(Path.GetFullPath(scenePath));
        try
        {
            RunGate("5A", () => BistroBuilderStaff5AFoundationSelfTest.Run(out _, out _, out _));
            RunGate("5B", () => BistroBuilderStaff5BPlanningSelfTest.Run(out _, out _, out _));
            RunGate("5C", () => BistroBuilderStaff5CBindingSelfTest.Run(out _, out _, out _));
            RunGate("5D JSON", () => BistroBuilderStaff5DJsonRoundTripSelfTest.Run(out _, out _, out _));
            RunGate("5D Save cruzado", () => BistroBuilderStaff5DCrossSaveSelfTest.Run(out _, out _, out _));

            BistroBuilderSaveGameService save = RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderStaffService staff = RequireUnique<BistroBuilderStaffService>(scene);
            BistroBuilderStaffScheduleService schedule = RequireUnique<BistroBuilderStaffScheduleService>(scene);
            GameObject host = save.gameObject;
            if (staff.gameObject != host || schedule.gameObject != host)
                throw new InvalidOperationException("SaveGame, Staff y Schedule deben vivir en GameSystems.");

            BistroBuilderStaffScheduleSaveSectionProvider provider =
                EnsureUnique<BistroBuilderStaffScheduleSaveSectionProvider>(scene, host);
            Assign(provider, "saveGameService", save);
            Assign(provider, "staffService", staff);
            Assign(provider, "scheduleService", schedule);
            if (!provider.ValidateConfiguration(out string error))
                throw new InvalidOperationException(error);

            BistroBuilderStaffStateSaveSectionProvider state =
                RequireUnique<BistroBuilderStaffStateSaveSectionProvider>(scene);
            BistroBuilderStaffRecruitmentSaveSectionProvider recruitment =
                RequireUnique<BistroBuilderStaffRecruitmentSaveSectionProvider>(scene);
            BistroBuilderStaffSessionSaveSectionProvider session =
                RequireUnique<BistroBuilderStaffSessionSaveSectionProvider>(scene);
            BistroBuilderActiveServiceSaveSectionProvider active =
                RequireUnique<BistroBuilderActiveServiceSaveSectionProvider>(scene);

            if (!(state.ApplyOrder < provider.ApplyOrder && provider.ApplyOrder < active.ApplyOrder &&
                  active.ApplyOrder < session.ApplyOrder))
                throw new InvalidOperationException("Orden Apply inseguro para staff.schedule.");
            if (!(active.PrepareOrder > session.PrepareOrder && session.PrepareOrder > recruitment.PrepareOrder &&
                  recruitment.PrepareOrder > provider.PrepareOrder && provider.PrepareOrder > state.PrepareOrder))
                throw new InvalidOperationException("Orden Prepare inseguro para staff.schedule.");
            if (!(state.FinalizeOrder < recruitment.FinalizeOrder && recruitment.FinalizeOrder < provider.FinalizeOrder &&
                  provider.FinalizeOrder < session.FinalizeOrder && session.FinalizeOrder < active.FinalizeOrder))
                throw new InvalidOperationException("Orden Finalize inseguro para staff.schedule.");

            EditorUtility.SetDirty(provider);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena 5D.");
            AssetDatabase.SaveAssets();
            save.RefreshExtensions();
            if (!save.HasProvider(BistroBuilderStaffScheduleSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException("SaveGame no registra staff.schedule tras instalación.");

            EditorUtility.DisplayDialog("Bistro Builder — 5D",
                "staff.schedule instalado sobre SaveGame universal.\n\nPendiente Save/Load real en Unity.", "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(Path.GetFullPath(scenePath), backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Bistro Builder — 5D",
                "La instalación falló y la escena fue restaurada.\n\n" + exception.Message, "Aceptar");
        }
    }

    private static void RunGate(string name, Func<bool> gate)
    {
        if (!gate()) throw new InvalidOperationException("Gate " + name + " no superado.");
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException("Se esperaba un " + typeof(T).Name + " y hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUnique<T>(Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length > 1) throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T value = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (value.gameObject != host) throw new InvalidOperationException(typeof(T).Name + " no vive en GameSystems.");
        return value;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T value in all) if (value != null && value.gameObject.scene == scene) list.Add(value);
        return list.ToArray();
    }

    private static void Assign(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("No existe " + name + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
