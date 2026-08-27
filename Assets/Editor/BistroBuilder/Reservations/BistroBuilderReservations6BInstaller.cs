using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional de 6B — disponibilidad y asignación de mesas.
/// </summary>
public static class BistroBuilderReservations6BInstaller
{
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    public const string ProfilePath =
        "Assets/Resources/BistroBuilder/Reservations/ReservationAvailabilityProfile.asset";

    [MenuItem(
        "Tools/Bistro Builder/Reservations/6B - Instalar + validar",
        false,
        622)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — 6B Reservas", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — 6B Reservas", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar 6B.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 6B.";
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
            bool selfOk = BistroBuilderReservations6BAvailabilitySelfTest.Run(
                out int prePassed,
                out int preFailed,
                out string preReport);
            Debug.Log(preReport);
            if (!selfOk)
                throw new InvalidOperationException(
                    "Gate 6B previo: " + prePassed + " OK / " + preFailed + " fallos.");

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderReservationService reservationService =
                RequireUnique<BistroBuilderReservationService>(scene);
            RestaurantTableRegistry tableRegistry =
                RequireUnique<RestaurantTableRegistry>(scene);
            RestaurantSeatRegistry seatRegistry =
                RequireUnique<RestaurantSeatRegistry>(scene);
            BistroBuilderGeneralGameStateService gameState =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderReservationAvailabilityProfile profile =
                EnsureProfile();

            BistroBuilderReservationAvailabilityService service =
                EnsureUniqueOnHost<BistroBuilderReservationAvailabilityService>(
                    scene,
                    gameSystems);
            Wire(service, reservationService, tableRegistry, seatRegistry, gameState, profile);
            if (!service.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException(serviceError);

            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 6B.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderReservations6BValidationResult validation =
                BistroBuilderReservations6BValidator.ValidateCurrentScene();
            bool finalSelfOk = BistroBuilderReservations6BAvailabilitySelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !finalSelfOk)
                throw new InvalidOperationException(
                    "6B no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "6B instalado correctamente.\n" +
                     validation.BuildReport() + "\n" +
                     "Autotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absoluteScene, sceneBackup);
            RestoreProfile(profileExisted, profileBackup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            report = "La instalación 6B falló y fue restaurada. " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }
    private static BistroBuilderReservationAvailabilityProfile EnsureProfile()
    {
        EnsureAssetFolder("Assets/Resources");
        EnsureAssetFolder("Assets/Resources/BistroBuilder");
        EnsureAssetFolder("Assets/Resources/BistroBuilder/Reservations");

        BistroBuilderReservationAvailabilityProfile profile =
            AssetDatabase.LoadAssetAtPath<BistroBuilderReservationAvailabilityProfile>(
                ProfilePath);
        if (profile != null)
            return profile;

        profile = ScriptableObject.CreateInstance<
            BistroBuilderReservationAvailabilityProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);
        return profile;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        if (slash <= 0)
            throw new InvalidOperationException("Ruta de carpeta inválida: " + path);
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
    private static void Wire(
        BistroBuilderReservationAvailabilityService service,
        BistroBuilderReservationService reservationService,
        RestaurantTableRegistry tableRegistry,
        RestaurantSeatRegistry seatRegistry,
        BistroBuilderGeneralGameStateService gameState,
        BistroBuilderReservationAvailabilityProfile profile)
    {
        SerializedObject serialized = new SerializedObject(service);
        SetObject(serialized, "reservationService", reservationService);
        SetObject(serialized, "tableRegistry", tableRegistry);
        SetObject(serialized, "seatRegistry", seatRegistry);
        SetObject(serialized, "generalGameStateService", gameState);
        SetObject(serialized, "availabilityProfile", profile);
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

    private static T RequireUnique<T>(Scene scene)
        where T : Component
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
    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
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
