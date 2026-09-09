using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAdvancedWaiters13Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Waiters/13 - Instalar + validar", false, 13000)]
    private static void InstallFromMenu()
    {
        bool ok = TryInstall(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder - Camareros 13", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            report = "No esta abierta la escena canonica.";
            return false;
        }

        string absolute = Path.GetFullPath(ScenePath);
        byte[] backup = File.ReadAllBytes(absolute);
        try
        {
            GameObject host = GameObject.Find("GameSystems");
            if (host == null) throw new InvalidOperationException("Falta GameSystems.");

            BistroBuilderWaiterRoutingService routing =
                Ensure<BistroBuilderWaiterRoutingService>(host);
            BistroBuilderAdvancedWaiterService advanced =
                Ensure<BistroBuilderAdvancedWaiterService>(host);
            BistroBuilderAdvancedWaiterPlayerScreen screen =
                Ensure<BistroBuilderAdvancedWaiterPlayerScreen>(host);
            WaiterTaskCoordinator coordinator =
                UnityEngine.Object.FindFirstObjectByType<WaiterTaskCoordinator>();
            BistroBuilderCustomerExperienceTrackingService experience =
                UnityEngine.Object.FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>();
            if (coordinator == null) throw new InvalidOperationException("Falta WaiterTaskCoordinator.");
            if (experience == null) throw new InvalidOperationException("Falta Customer Experience Tracking.");

            Assign(advanced, "routingService", routing);
            Assign(advanced, "experienceTrackingService", experience);
            Assign(screen, "service", advanced);
            Assign(coordinator, "advancedWaiterService", advanced);

            Waiter[] waiters = UnityEngine.Object.FindObjectsByType<Waiter>(FindObjectsSortMode.None);
            if (waiters.Length == 0) throw new InvalidOperationException("No hay camareros instalados.");
            for (int i = 0; i < waiters.Length; i++)
            {
                Ensure<BistroBuilderAdvancedWaiterProfile>(waiters[i].gameObject);
                WaiterMovementView movement = waiters[i].GetComponent<WaiterMovementView>();
                if (movement != null) Assign(movement, "routingService", routing);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalacion 13.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedWaiters13ValidationResult validation =
                BistroBuilderAdvancedWaiters13Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedWaiters13SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(validation.BuildReport() + "\n" + selfReport);

            report = "Bloque 13 instalado correctamente.\n" +
                validation.BuildReport() + "\nAutotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absolute, backup);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report = "Instalacion 13 revertida: " + exception.Message;
            return false;
        }
    }

    private static T Ensure<T>(GameObject host) where T : Component
    {
        T value = host.GetComponent<T>();
        if (value == null) value = host.AddComponent<T>();
        EditorUtility.SetDirty(value);
        return value;
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("No existe campo " + field + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
