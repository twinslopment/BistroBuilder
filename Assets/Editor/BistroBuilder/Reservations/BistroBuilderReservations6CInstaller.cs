using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional de 6C — integración con servicio real.
/// </summary>
public static class BistroBuilderReservations6CInstaller
{
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    public const string ProfilePath =
        "Assets/Resources/BistroBuilder/Reservations/ReservationRuntimeProfile.asset";

    [MenuItem("Tools/Bistro Builder/Reservations/6C - Instalar + validar", false, 632)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — 6C Reservas", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — 6C Reservas", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
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
            report = "Sal de Play Mode antes de instalar 6C.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 6C.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        bool profileExisted = File.Exists(Path.GetFullPath(ProfilePath));
        byte[] profileBackup = profileExisted
            ? File.ReadAllBytes(Path.GetFullPath(ProfilePath))
            : null;

        try
        {
            BistroBuilderReservations6AValidationResult gate6A =
                BistroBuilderReservations6AValidator.ValidateCurrentScene();
            BistroBuilderReservations6BValidationResult gate6B =
                BistroBuilderReservations6BValidator.ValidateCurrentScene();
            if (gate6A.Errors > 0 || gate6B.Errors > 0)
                throw new InvalidOperationException(
                    "6C requiere 6A y 6B verdes antes de instalarse.");

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderReservationRuntimeProfile profile = EnsureProfile();
            BistroBuilderReservationServiceIntegration integration =
                EnsureUniqueOnHost<BistroBuilderReservationServiceIntegration>(
                    scene,
                    gameSystems);

            Wire(
                integration,
                RequireUnique<BistroBuilderReservationService>(scene),
                RequireUnique<BistroBuilderReservationAvailabilityService>(scene),
                profile,
                RequireUnique<BistroBuilderGeneralGameStateService>(scene),
                RequireUnique<GameClock>(scene),
                RequireUnique<RestaurantServiceStateService>(scene),
                RequireUnique<RestaurantTableRegistry>(scene),
                RequireUnique<TableAssignmentSystem>(scene),
                RequireUnique<CustomerGroupSpawner>(scene));

            if (!integration.ValidateConfiguration(out string integrationError))
                throw new InvalidOperationException(integrationError);

            EditorUtility.SetDirty(integration);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 6C.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderReservations6CValidationResult validation =
                BistroBuilderReservations6CValidator.ValidateCurrentScene();
            Debug.Log(validation.BuildReport());
            if (validation.Errors > 0)
                throw new InvalidOperationException(
                    "6C no superó validación: " + validation.Errors + " errores.");

            report = "6C instalado correctamente.\n" + validation.BuildReport();
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absoluteScene, sceneBackup);
            RestoreProfile(profileExisted, profileBackup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            report = "La instalación 6C falló y fue restaurada. " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private static BistroBuilderReservationRuntimeProfile EnsureProfile()
    {
        BistroBuilderReservationRuntimeProfile profile =
            AssetDatabase.LoadAssetAtPath<BistroBuilderReservationRuntimeProfile>(
                ProfilePath);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<BistroBuilderReservationRuntimeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        return profile;
    }

    private static void Wire(
        BistroBuilderReservationServiceIntegration integration,
        BistroBuilderReservationService reservationService,
        BistroBuilderReservationAvailabilityService availabilityService,
        BistroBuilderReservationRuntimeProfile runtimeProfile,
        BistroBuilderGeneralGameStateService gameState,
        GameClock gameClock,
        RestaurantServiceStateService serviceState,
        RestaurantTableRegistry tableRegistry,
        TableAssignmentSystem tableAssignment,
        CustomerGroupSpawner spawner)
    {
        SerializedObject serialized = new SerializedObject(integration);
        SetObject(serialized, "reservationService", reservationService);
        SetObject(serialized, "availabilityService", availabilityService);
        SetObject(serialized, "runtimeProfile", runtimeProfile);
        SetObject(serialized, "generalGameStateService", gameState);
        SetObject(serialized, "gameClock", gameClock);
        SetObject(serialized, "serviceStateService", serviceState);
        SetObject(serialized, "tableRegistry", tableRegistry);
        SetObject(serialized, "tableAssignmentSystem", tableAssignment);
        SetObject(serialized, "customerGroupSpawner", spawner);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                serialized.targetObject.name + " no expone " + propertyName + ".");
        property.objectReferenceValue = value;
    }

    private static void RestoreProfile(bool existed, byte[] backup)
    {
        if (existed)
        {
            File.WriteAllBytes(Path.GetFullPath(ProfilePath), backup);
            AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceSynchronousImport);
        }
        else
        {
            AssetDatabase.DeleteAsset(ProfilePath);
        }
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
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null)
                    result.Add(values[index]);
            }
        }
        return result.ToArray();
    }
}
