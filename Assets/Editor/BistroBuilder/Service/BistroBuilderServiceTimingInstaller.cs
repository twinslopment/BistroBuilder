using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderServiceTimingInstaller
{
    public const string AssetPath =
        "Assets/Resources/BistroBuilder/Service/BB_ServiceTimingCatalog.asset";
    public const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Bistro Builder/Servicio/Timing contextual/Instalar o actualizar")]
    public static void InstallFromMenu()
    {
        Install(true);
    }

    public static void RunBatch()
    {
        if (!Install(true))
            throw new System.InvalidOperationException(
                "No se pudo instalar Service Timing contextual."
            );
    }

    public static bool Install(bool logResult)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (logResult)
                Debug.LogError("La instalación de Service Timing requiere Edit Mode.");
            return false;
        }

        EnsureFolders();

        BistroBuilderServiceTimingCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderServiceTimingCatalog>(
                AssetPath
            );

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderServiceTimingCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        ConfigureCatalog(catalog);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        WaiterTaskCoordinator coordinator =
            Object.FindFirstObjectByType<WaiterTaskCoordinator>(
                FindObjectsInactive.Include
            );
        BistroBuilderCustomerExperienceTrackingService experience =
            Object.FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>(
                FindObjectsInactive.Include
            );

        if (coordinator == null || experience == null)
        {
            if (logResult)
            {
                Debug.LogError(
                    "Service Timing necesita WaiterTaskCoordinator y Customer Experience Tracking."
                );
            }
            return false;
        }

        BistroBuilderTableContextActionService context =
            Object.FindFirstObjectByType<BistroBuilderTableContextActionService>(
                FindObjectsInactive.Include
            );

        if (context == null)
            context = Undo.AddComponent<BistroBuilderTableContextActionService>(
                coordinator.gameObject
            );

        SerializedObject contextSo = new SerializedObject(context);
        contextSo.FindProperty("timingCatalog").objectReferenceValue = catalog;
        contextSo.FindProperty("taskCoordinator").objectReferenceValue = coordinator;
        contextSo.FindProperty("experienceTrackingService").objectReferenceValue =
            experience;
        contextSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(context);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool valid =
            BistroBuilderServiceTimingValidator.Validate(logResult) &&
            BistroBuilderServiceTimingSelfTest.Run(logResult);

        if (logResult)
        {
            Debug.Log(
                valid
                    ? "SERVICE TIMING CONTEXTUAL: INSTALL + VALIDATION + SELFTEST PASS"
                    : "SERVICE TIMING CONTEXTUAL: FAIL"
            );
        }

        return valid;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources", "BistroBuilder");
        EnsureFolder("Assets/Resources/BistroBuilder", "Service");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void ConfigureCatalog(BistroBuilderServiceTimingCatalog catalog)
    {
        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty profiles = so.FindProperty("profiles");
        profiles.arraySize = 1;

        SerializedProperty profile = profiles.GetArrayElementAtIndex(0);
        profile.FindPropertyRelative("phase").enumValueIndex =
            (int)BistroBuilderServiceTimingPhase.BillDelivery;
        profile.FindPropertyRelative("targetSeconds").floatValue = 90f;
        profile.FindPropertyRelative("attentionSeconds").floatValue = 120f;
        profile.FindPropertyRelative("delaySeconds").floatValue = 210f;
        profile.FindPropertyRelative("incidentSeconds").floatValue = 300f;
        profile.FindPropertyRelative("criticalSeconds").floatValue = 420f;

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
