using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderNewGame16Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Opening/16 - Instalar + validar", false, 16000)]
    private static void InstallFromMenu()
    {
        bool ok = TryInstall(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder - Bloque 16", report, "Aceptar");
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

            BistroBuilderNewGameOpeningService service = Ensure<BistroBuilderNewGameOpeningService>(host);
            BistroBuilderNewGameOpeningSaveSectionProvider persistence =
                Ensure<BistroBuilderNewGameOpeningSaveSectionProvider>(host);
            BistroBuilderNewGameOpeningPlayerScreen screen =
                Ensure<BistroBuilderNewGameOpeningPlayerScreen>(host);

            Assign(service, "generalGameStateService", Need<BistroBuilderGeneralGameStateService>());
            Assign(service, "gameClock", Need<GameClock>());
            Assign(service, "serviceStateService", Need<RestaurantServiceStateService>());
            Assign(service, "inventoryService", Need<BistroBuilderInventoryService>());
            Assign(service, "menuService", Need<BistroBuilderRestaurantMenuService>());
            Assign(service, "staffService", Need<BistroBuilderStaffService>());
            Assign(service, "recruitmentService", Need<BistroBuilderStaffRecruitmentService>());
            Assign(service, "scheduleService", Need<BistroBuilderStaffScheduleService>());
            Assign(service, "financeService", Need<BistroBuilderFinanceService>());
            Assign(service, "reputationService", Need<BistroBuilderReputationService>());
            Assign(service, "customerHistoryService", Need<BistroBuilderAdvancedCustomerHistoryService>());
            Assign(service, "endOfDayService", Need<BistroBuilderEndOfDayService>());
            Assign(service, "advancedKitchenService", Need<BistroBuilderAdvancedKitchenService>());
            Assign(service, "tableRegistry", Need<RestaurantTableRegistry>());
            Assign(service, "placementValidationService", Need<RestaurantPlacementValidationService>());
            Assign(service, "saveGameService", Need<BistroBuilderSaveGameService>());

            Assign(persistence, "saveGameService", Need<BistroBuilderSaveGameService>());
            Assign(persistence, "openingService", service);
            Assign(screen, "openingService", service);

            BistroBuilderSaveGameService save = Need<BistroBuilderSaveGameService>();
            save.RefreshExtensions();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalacion 16.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderNewGame16ValidationResult validation = BistroBuilderNewGame16Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderNewGame16SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            if (validation.Errors > 0 || !selfOk || failed > 0)
                throw new InvalidOperationException(validation.BuildReport() + "\n" + selfReport);

            report = "Bloque 16 instalado correctamente.\n" + validation.BuildReport() +
                     "\nAutotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absolute, backup);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            report = "Instalacion 16 revertida: " + exception.Message;
            return false;
        }
    }

    private static T Need<T>() where T : UnityEngine.Object
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
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("No existe campo " + field + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
