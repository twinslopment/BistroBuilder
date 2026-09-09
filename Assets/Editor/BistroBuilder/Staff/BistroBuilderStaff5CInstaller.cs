using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador aditivo 5C. Añade solo el bridge entre horarios y 4D.
/// </summary>
public static class BistroBuilderStaff5CInstaller
{
    [MenuItem("Tools/Bistro Builder/Personal/5C - Instalar binding horario", false, 3275)]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5C", "Sal de Play Mode antes de instalar.", "Aceptar");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5C", "Abre y guarda la escena principal.", "Aceptar");
            return;
        }

        string scenePath = scene.path;
        byte[] backup = File.ReadAllBytes(Path.GetFullPath(scenePath));
        try
        {
            if (!BistroBuilderStaff5CBindingSelfTest.Run(out _, out int staticFailed, out string staticReport))
            {
                Debug.LogError(staticReport);
                throw new InvalidOperationException("Gate estático 5C: " + staticFailed + " fallos.");
            }
            Debug.Log(staticReport);

            GameObject host = RequireUnique<BistroBuilderStaffScheduleService>(scene).gameObject;
            BistroBuilderStaffScheduleSessionBridge bridge =
                EnsureUnique<BistroBuilderStaffScheduleSessionBridge>(scene, host);

            Assign(bridge, "scheduleService", RequireUnique<BistroBuilderStaffScheduleService>(scene));
            Assign(bridge, "staffService", RequireUnique<BistroBuilderStaffService>(scene));
            Assign(bridge, "sessionService", RequireUnique<BistroBuilderStaffSessionService>(scene));
            Assign(bridge, "generalGameStateService", RequireUnique<BistroBuilderGeneralGameStateService>(scene));
            Assign(bridge, "orderIntegration", RequireUnique<BistroBuilderCanonicalOrderIntegrationService>(scene));
            Assign(bridge, "serviceStateService", RequireUnique<RestaurantServiceStateService>(scene));

            if (!bridge.ValidateConfiguration(out string error))
                throw new InvalidOperationException(error);

            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena 5C.");
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Bistro Builder — 5C",
                "Bridge horario -> 4D instalado y gate estático correcto.\n\nPendiente Play Mode real.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(Path.GetFullPath(scenePath), backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Bistro Builder — 5C",
                "La instalación falló y la escena fue restaurada.\n\n" + exception.Message,
                "Aceptar");
        }
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException("Se esperaba exactamente un " + typeof(T).Name +
                " y hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUnique<T>(Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length > 1)
            throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T value = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (value.gameObject != host)
            throw new InvalidOperationException(typeof(T).Name + " no vive junto a StaffScheduleService.");
        return value;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T value in all)
            if (value != null && value.gameObject.scene == scene) list.Add(value);
        return list.ToArray();
    }

    private static void Assign(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("No existe " + name + " en " + target.GetType().Name + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
