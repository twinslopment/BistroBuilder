using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderEditBlock18Installer
{
    public const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    public const string WallContractPath =
        "Assets/Resources/BistroBuilder/Spatial/Contracts/" +
        "BB_SpatialContract_Architecture_Wall.asset";

    public static int LastCreatedCount { get; private set; }
    public static int LastAssignedCount { get; private set; }

    [MenuItem("Bistro Builder/18 Modo Edicion/Install Canonical Scene")]
    public static void InstallFromMenu()
    {
        InstallCanonicalScene();
        Debug.Log(BistroBuilderEditBlock18SceneValidator.LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            InstallCanonicalScene();
            Debug.Log(BistroBuilderEditBlock18SceneValidator.LastReport);
            Debug.Log("Block 18 installation: " + LastCreatedCount +
                " components/objects created, " + LastAssignedCount + " references assigned.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // This remains a resource-only entry point for callers that do not own a scene.
    public static BistroBuilderSpatialContractDefinition Install()
    {
        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(WallContractPath);
        if (contract == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(WallContractPath) != null)
                throw new InvalidOperationException("The wall contract path is occupied by another asset.");
            EnsureFolder("Assets/Resources/BistroBuilder/Spatial/Contracts");
            contract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            contract.ConfigureForEditor("architecture.wall.runtime", "generic",
                BistroBuilderAdaptiveSpatialProxyMode.Simple,
                new[] { "architecture.wall", "static.obstacle" });
            AssetDatabase.CreateAsset(contract, WallContractPath);
            AssetDatabase.SaveAssetIfDirty(contract);
        }

        // Existing traits, semantic geometry and version belong to the author.
        if (!IsWallContractValid(contract, out string error))
            throw new InvalidOperationException("Block 18 wall contract: " + error);
        return contract;
    }

    public static void InstallCanonicalScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before installing Block 18.");
        LastCreatedCount = 0;
        LastAssignedCount = 0;
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (!openedHere && scene.isDirty)
            throw new InvalidOperationException(
                "Prototype_Restaurant has unsaved changes. Save it before running the installer; no changes were discarded.");
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Block 18 scene integration");
        try
        {
            InstallScene(scene);
            if (!BistroBuilderEditBlock18SceneValidator.ValidateScene(scene))
                throw new InvalidOperationException(BistroBuilderEditBlock18SceneValidator.LastReport);
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the Block 18 scene installation.");
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void InstallScene(Scene scene)
    {
        // Resolve and check all existing singletons before adding anything.
        var spatial = RequireSingle<BistroBuilderSpatialInteractionService>(scene);
        var navigation = RequireSingle<BistroBuilderNavigationService>(scene);
        var operational = RequireSingle<BistroBuilderOperationalSpatialCoordinator>(scene);
        var finance = RequireSingle<BistroBuilderFinanceService>(scene);
        var discretionary = RequireSingle<BistroBuilderDiscretionaryFinanceService>(scene);
        var gameState = RequireSingle<BistroBuilderGeneralGameStateService>(scene);
        var clock = RequireSingle<GameClock>(scene);
        var save = RequireSingle<BistroBuilderSaveGameService>(scene);
        var editMode = RequireSingle<RestaurantEditModeService>(scene);
        var availability = RequireSingle<RestaurantServiceEditModeAvailabilityRule>(scene);
        RequireAtMostOne<BistroBuilderEditDocumentRuntimeService>(scene);
        RequireAtMostOne<BistroBuilderArchitectureRuntimeMaterializer>(scene);
        RequireAtMostOne<BistroBuilderEditDocumentMaterializationBridge>(scene);
        RequireAtMostOne<BistroBuilderEditRuntimeCoordinator>(scene);
        RequireAtMostOne<BistroBuilderEditPlayerFacade>(scene);
        RequireAtMostOne<BistroBuilderArchitecturePlayerTool>(scene);
        RequireAtMostOne<BistroBuilderEditDocumentSaveSectionProvider>(scene);
        RequireAtMostOne<BistroBuilderEditFinanceGateway>(scene);
        RequireAtMostOne<BistroBuilderEditFinanceGatewayBinder>(scene);
        RequireAtMostOne<BistroBuilderEditNavigationValidationProvider>(scene);
        RequireAtMostOne<BistroBuilderEditSessionSaveGuard>(scene);
        var existingSave = SingleOrNull<BistroBuilderEditDocumentSaveSectionProvider>(scene);
        if (existingSave != null && existingSave.gameObject != save.gameObject)
            throw new InvalidOperationException(
                "The existing architecture save provider must be on the SaveGameService GameObject. It was preserved without duplication.");

        var contract = Install();
        var document = EnsureComponent<BistroBuilderEditDocumentRuntimeService>(scene, save.gameObject);
        var materializer = EnsureComponent<BistroBuilderArchitectureRuntimeMaterializer>(scene, document.gameObject);
        var projection = EnsureComponent<BistroBuilderEditDocumentMaterializationBridge>(scene, document.gameObject);
        var coordinator = EnsureComponent<BistroBuilderEditRuntimeCoordinator>(scene, document.gameObject);
        var facade = EnsureComponent<BistroBuilderEditPlayerFacade>(scene, document.gameObject);
        EnsureComponent<BistroBuilderArchitecturePlayerTool>(scene, document.gameObject);
        var saveProvider = EnsureComponent<BistroBuilderEditDocumentSaveSectionProvider>(scene, save.gameObject);
        var gateway = EnsureComponent<BistroBuilderEditFinanceGateway>(scene, finance.gameObject);
        var binder = EnsureComponent<BistroBuilderEditFinanceGatewayBinder>(scene, gateway.gameObject);
        var navValidator = EnsureComponent<BistroBuilderEditNavigationValidationProvider>(scene, navigation.gameObject);
        var editSaveGuard = EnsureComponent<BistroBuilderEditSessionSaveGuard>(scene, save.gameObject);

        AssignMissingReference(materializer, "wallSpatialContract", contract);
        var materializerState = new SerializedObject(materializer);
        var rootProperty = materializerState.FindProperty("generatedRoot");
        if (rootProperty.objectReferenceValue == null)
        {
            var generated = new GameObject("BB18_GeneratedArchitecture");
            SceneManager.MoveGameObjectToScene(generated, scene);
            Undo.RegisterCreatedObjectUndo(generated, "Create architecture projection root");
            Undo.SetTransformParent(generated.transform, materializer.transform, "Parent architecture projection root");
            generated.transform.localPosition = Vector3.zero;
            generated.transform.localRotation = Quaternion.identity;
            generated.transform.localScale = Vector3.one;
            LastCreatedCount++;
            AssignMissingReference(materializer, "generatedRoot", generated.transform);
        }
        AssignMissingReference(projection, "runtimeService", document);
        AssignMissingReference(projection, "materializer", materializer);
        AssignMissingReference(projection, "spatialService", spatial);
        AssignMissingReference(projection, "operationalSpatialCoordinator", operational);
        AssignMissingReference(projection, "navigationService", navigation);
        AssignMissingReference(coordinator, "documentService", document);
        AssignMissingReference(coordinator, "editModeService", editMode);
        AssignMissingReference(coordinator, "availabilityRuleSource", availability);
        AssignMissingReference(facade, "coordinator", coordinator);
        AssignMissingReference(saveProvider, "runtimeService", document);
        AssignMissingReference(saveProvider, "materializer", materializer);
        AssignMissingReference(gateway, "financeService", finance);
        AssignMissingReference(gateway, "discretionaryFinanceService", discretionary);
        AssignMissingReference(gateway, "generalGameStateService", gameState);
        AssignMissingReference(gateway, "gameClock", clock);
        AssignMissingReference(binder, "gateway", gateway);
        AssignMissingReference(binder, "editCoordinator", coordinator);
        AssignMissingReference(navValidator, "editDocumentService", document);
        AssignMissingReference(editSaveGuard, "coordinator", coordinator);
        BindAuthoredTariffTable(gateway);
        save.RefreshExtensions();
    }

    private static void BindAuthoredTariffTable(BistroBuilderEditFinanceGateway gateway)
    {
        var state = new SerializedObject(gateway);
        var table = state.FindProperty("tariffTable");
        if (table == null || table.objectReferenceValue != null) return;
        string[] candidates = AssetDatabase.FindAssets("t:BistroBuilderEditFinanceTariffTable");
        // Multiple authored tables require an explicit selection by Finance.
        if (candidates.Length != 1) return;
        var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(candidates[0]));
        if (asset != null) AssignMissingReference(gateway, "tariffTable", asset);
    }

    private static T EnsureComponent<T>(Scene scene, GameObject host) where T : MonoBehaviour
    {
        T component = SingleOrNull<T>(scene);
        if (component != null) return component;
        component = Undo.AddComponent<T>(host);
        LastCreatedCount++;
        return component;
    }

    private static void AssignMissingReference(UnityEngine.Object target, string field, UnityEngine.Object expected)
    {
        if (expected == null) throw new InvalidOperationException("Missing dependency: " + field);
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + field + " was not found.");
        if (property.objectReferenceValue == expected) return;
        if (property.objectReferenceValue != null)
            throw new InvalidOperationException(target.GetType().Name + "." + field +
                " already points to another object; the authored reference was preserved.");
        property.objectReferenceValue = expected;
        serialized.ApplyModifiedProperties();
        LastAssignedCount++;
    }

    public static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return results;
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results;
    }

    private static T RequireSingle<T>(Scene scene) where T : MonoBehaviour
    {
        T component = SingleOrNull<T>(scene);
        if (component == null || !component.enabled || !component.gameObject.activeInHierarchy)
            throw new InvalidOperationException("An active " + typeof(T).Name + " is required in " + ScenePath + ".");
        return component;
    }

    private static void RequireAtMostOne<T>(Scene scene) where T : MonoBehaviour
    {
        T component = SingleOrNull<T>(scene);
        if (component != null && (!component.enabled || !component.gameObject.activeInHierarchy))
            throw new InvalidOperationException("Existing " + typeof(T).Name + " is disabled; its authored state was preserved.");
    }

    private static T SingleOrNull<T>(Scene scene) where T : Component
    {
        List<T> results = FindInScene<T>(scene);
        if (results.Count > 1)
            throw new InvalidOperationException(typeof(T).Name + " has multiple instances in the canonical scene.");
        return results.Count == 1 ? results[0] : null;
    }

    public static bool IsWallContractValid(BistroBuilderSpatialContractDefinition contract, out string error)
    {
        error = string.Empty;
        if (contract == null) { error = "Missing wall Spatial Contract."; return false; }
        if (!contract.ValidateDefinition(out error)) return false;
        if (contract.ContractId != "architecture.wall.runtime" ||
            !contract.HasTrait("architecture.wall") || !contract.HasTrait("static.obstacle"))
        {
            error = "The wall contract must retain its stable identity and architecture.wall/static.obstacle traits.";
            return false;
        }
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(leaf))
            throw new InvalidOperationException("Invalid asset folder: " + path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}