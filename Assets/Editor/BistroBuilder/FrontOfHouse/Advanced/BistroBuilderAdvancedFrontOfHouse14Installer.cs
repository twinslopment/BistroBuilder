using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderAdvancedFrontOfHouse14Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Front Of House/14 - Instalar + validar", false, 14000)]
    private static void InstallFromMenu()
    {
        bool ok = TryInstall(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder - Sala 14", report, "Aceptar");
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
            BistroBuilderAdvancedFrontOfHouseService advanced = Ensure<BistroBuilderAdvancedFrontOfHouseService>(host);
            BistroBuilderAdvancedFrontOfHousePlayerScreen screen = Ensure<BistroBuilderAdvancedFrontOfHousePlayerScreen>(host);

            TableAssignmentSystem tables = Find<TableAssignmentSystem>();
            CustomerWaitingAreaSystem waiting = Find<CustomerWaitingAreaSystem>();
            RestaurantTableRegistry tableRegistry = Find<RestaurantTableRegistry>();
            BistroBuilderBarServiceRegistry barRegistry = Find<BistroBuilderBarServiceRegistry>();
            BistroBuilderBarServiceSystem bar = Find<BistroBuilderBarServiceSystem>();
            BistroBuilderReservationService reservations = Find<BistroBuilderReservationService>();
            BistroBuilderAdvancedCustomerProfileService profiles = Find<BistroBuilderAdvancedCustomerProfileService>();
            BistroBuilderAdvancedCustomerSeatingPreferenceService preferences = Find<BistroBuilderAdvancedCustomerSeatingPreferenceService>();
            BistroBuilderAdvancedWaiterService waiters = Find<BistroBuilderAdvancedWaiterService>();
            BistroBuilderGeneralGameStateService general = Find<BistroBuilderGeneralGameStateService>();
            GameClock clock = Find<GameClock>();
            BistroBuilderActiveServiceSaveSectionProvider persistence = Find<BistroBuilderActiveServiceSaveSectionProvider>();

            Assign(advanced, "tableAssignmentSystem", tables);
            Assign(advanced, "waitingAreaSystem", waiting);
            Assign(advanced, "tableRegistry", tableRegistry);
            Assign(advanced, "barRegistry", barRegistry);
            Assign(advanced, "barServiceSystem", bar);
            Assign(advanced, "reservationService", reservations);
            Assign(advanced, "profileService", profiles);
            Assign(advanced, "seatingPreferenceService", preferences);
            Assign(advanced, "advancedWaiterService", waiters);
            Assign(advanced, "generalGameStateService", general);
            Assign(advanced, "gameClock", clock);
            Assign(screen, "service", advanced);
            Assign(tables, "advancedFrontOfHouseService", advanced);
            Assign(waiting, "advancedFrontOfHouseService", advanced);
            Assign(persistence, "advancedFrontOfHouseService", advanced);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalacion 14.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedFrontOfHouse14ValidationResult validation =
                BistroBuilderAdvancedFrontOfHouse14Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedFrontOfHouse14SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(validation.BuildReport() + "\n" + selfReport);

            report = "Bloque 14 instalado correctamente.\n" + validation.BuildReport() +
                     "\nAutotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absolute, backup);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report = "Instalacion 14 revertida: " + exception.Message;
            return false;
        }
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        T value = UnityEngine.Object.FindFirstObjectByType<T>();
        if (value == null) throw new InvalidOperationException("Falta " + typeof(T).Name + ".");
        return value;
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
        if (target == null) throw new InvalidOperationException("Destino nulo para " + field + ".");
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("No existe campo " + field + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
