using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real B12 full 64-slot positive placement, in an isolated QA dining area.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B12Valid64SelfTest
{
    private const string Key = "BB.EditorV2.B12.Valid64";
    private const string Result = "BB.EditorV2.B12.Valid64.Result";
    private const string Report = "EditorV2_B12_Valid64_Report.txt";
    static BistroBuilderEditorV2B12Valid64SelfTest()
    {
        EditorApplication.playModeStateChanged -= OnChanged;
        EditorApplication.playModeStateChanged += OnChanged;
    }
    [MenuItem("Bistro Builder/QA/Editor V2/B12 Valid 64 Bench")]
    public static void RunFromMenu() => Start(false);
    public static void RunFromCommandLine() => Start(true);
    private static void Start(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode active");
        File.WriteAllText(Path.GetFullPath(Report), "B12 Valid64 starting\n");
        SessionState.SetString(Key, cli ? "enter_cli" : "enter_menu");
        SessionState.SetBool(Result, false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void OnChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Key, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__B12_Valid64_QA")
                .AddComponent<BistroBuilderEditorV2B12Valid64Driver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Key);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }
    public static void End(bool success, string detail)
    {
        string stage = SessionState.GetString(Key, "");
        string output = "B12 VALID 64 FULL PLACEMENT BENCH\n" +
            (success ? "PASS " : "FAIL ") + detail + "\n";
        File.WriteAllText(Path.GetFullPath(Report), output);
        if (success) Debug.Log("[B12 VALID64] " + output);
        else Debug.LogError("[B12 VALID64] " + output);
        SessionState.SetBool(Result, success);
        SessionState.SetString(Key, stage.EndsWith("cli", StringComparison.Ordinal)
            ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
public sealed class BistroBuilderEditorV2B12Valid64Driver : MonoBehaviour
{
    private GameObject qaArea;
    private RestaurantAreaRegistry areaRegistry;
    private RestaurantArea qaComponent;
    private string tempLibrary;
    private IEnumerator Start()
    {
        for (int i = 0; i < 14; i++) yield return null;
        var edit = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var library = Object.FindFirstObjectByType<BistroBuilderEditorV2TemplateLibrary>(
            FindObjectsInactive.Include);
        var creation = Object.FindFirstObjectByType<RestaurantPlaceableCreationService>(
            FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>(
            FindObjectsInactive.Include);
        areaRegistry = Object.FindFirstObjectByType<RestaurantAreaRegistry>(
            FindObjectsInactive.Include);
        var history = Object.FindFirstObjectByType<RestaurantPlacementHistoryService>(
            FindObjectsInactive.Include);
        var selection = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        if (edit == null || library == null || creation == null ||
            registry == null || catalog == null || areaRegistry == null ||
            history == null || selection == null)
        {
            Complete(false, "Required runtime services unavailable");
            yield break;
        }
        if (!edit.IsEditModeActive && !edit.TryEnterEditMode(out _, out string editError))
        {
            Complete(false, "Edit mode unavailable: " + editError);
            yield break;
        }
        int originalCount = registry.RegisteredPlaceables.Count;
        int undoBefore = history.UndoCount;
        RestaurantPlaceableItemDefinition definition = null;
        RestaurantAreaDefinition dining = null;
        foreach (var obj in registry.RegisteredPlaceables)
        {
            if (obj == null || obj.ItemDefinition == null) continue;
            if (obj.ItemDefinition.ItemId != "table_basic_4") continue;
            var areaMember = obj.GetComponent<RestaurantAreaMember>();
            if (areaMember == null || areaMember.AssignedArea == null) continue;
            dining = areaMember.AssignedArea.Definition;
            if (!catalog.TryGetItem(obj.ItemDefinition.ItemId, out definition) ||
                definition == null || !definition.HasValidPrefab)
                continue;
            break;
        }
        if (definition == null || dining == null)
        {
            Complete(false, "No canonical 4-person table and dining capabilities");
            yield break;
        }
        // Temporary isolated simulation fixture: never included in scene assets.
        qaArea = new GameObject("__B12_Valid64_Only_Area");
        qaArea.SetActive(false);
        qaArea.transform.position = new Vector3(500f, 0f, 500f);
        var box = qaArea.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(155f, 6f, 155f);
        qaComponent = qaArea.AddComponent<RestaurantArea>();
        SetField(qaComponent, "areaId",
            "b12_qa_large_dining_" + Guid.NewGuid().ToString("N"));
        SetField(qaComponent, "definition", dining);
        SetField(qaComponent, "boundaryColliders", new Collider[] { box });
        qaArea.SetActive(true);
        if (!areaRegistry.RegisterArea(qaComponent) ||
            !areaRegistry.TryFindAreaContainingPosition(
                new Vector3(500f, 0f, 500f), out var candidateArea) ||
            candidateArea != qaComponent)
        {
            Complete(false, "Temporary isolated dining area not registered");
            yield break;
        }
        tempLibrary = Path.Combine(Path.GetTempPath(),
            "bb_b12_64_" + Guid.NewGuid().ToString("N") + ".json");
        library.UseTemporaryStorageForTest(tempLibrary);
        var slots = new BistroBuilderEditorV2TemplateSpawn[64];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new BistroBuilderEditorV2TemplateSpawn(
                definition,
                new Vector3(500f + (i % 8 - 3.5f) * 9f, 0f,
                    500f + (i / 8 - 3.5f) * 9f),
                Quaternion.identity);
        yield return null;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        bool created = creation.TryCreateTemplateBatch(slots,
            out IReadOnlyList<RestaurantPlaceableObject> createdItems,
            out RestaurantPlaceableBatchCreationResult creationResult);
        clock.Stop();
        long directMs = clock.ElapsedMilliseconds;
        long directBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        if (!created || createdItems.Count != 64 ||
            registry.RegisteredPlaceables.Count != originalCount + 64 ||
            history.UndoCount != undoBefore + 1)
        {
            Complete(false, "Valid 64 creation rejected: " + creationResult.Message +
                " created=" + createdItems.Count + " ms=" + directMs +
                " allocations=" + directBytes);
            yield break;
        }
        var chosen = new BistroBuilderEditorV2Selection[64];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < createdItems.Count; i++)
        {
            var obj = createdItems[i];
            ids.Add(obj.InstanceId);
            centroid += obj.PlacementAnchor.position;
            chosen[i] = new BistroBuilderEditorV2Selection {
                family = BistroBuilderEditorV2ToolFamily.Furniture,
                kind = BistroBuilderEditorV2SelectionKind.Furniture,
                stableId = obj.InstanceId,
                displayName = obj.DisplayName,
                capabilities = BistroBuilderEditorV2SelectionCapability.Inspect |
                    BistroBuilderEditorV2SelectionCapability.Delete,
                persistentIdentity = true
            };
        }
        centroid /= 64;
        string id = string.Empty;
        bool captured = selection.ReplaceSelectionSet(chosen,
                chosen[0].stableId, out string selectionError) &&
            library.TryCaptureSelection("64 Tables QA", out id,
                out string captureError);
        if (!captured)
        {
            Complete(false, "64 valid placed; capture failed: " + selectionError +
                " placedMs=" + directMs);
            yield break;
        }
        long totalCents = 0L;
        if (!library.TryReload(out string reloadError) ||
            !library.TryFind(id, out var snapshot) || snapshot.members.Count != 64 ||
            !library.TryQuote(id, out totalCents, out string quoteError))
        {
            Complete(false, "64-component template reload failed: " + reloadError);
            yield break;
        }
        if (!history.TryUndo(out _, out _, out _) ||
            registry.RegisteredPlaceables.Count != originalCount)
        {
            Complete(false, "64 members were not atomically undone");
            yield break;
        }
        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        clock.Restart();
        bool loaded = library.TryPlace(id, centroid, Quaternion.identity,
            out IReadOnlyList<RestaurantPlaceableObject> loadedItems,
            out string placeError);
        clock.Stop();
        long loadedMs = clock.ElapsedMilliseconds;
        long loadedBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        bool newIds = true;
        foreach (var obj in loadedItems)
            newIds &= !ids.Contains(obj.InstanceId);
        bool placed64 = loaded && loadedItems.Count == 64 && newIds &&
            registry.RegisteredPlaceables.Count == originalCount + 64;
        bool reversed = placed64 && history.TryUndo(out _, out _, out _) &&
            registry.RegisteredPlaceables.Count == originalCount;
        bool redone = reversed && history.TryRedo(out _, out _, out _) &&
            registry.RegisteredPlaceables.Count == originalCount + 64;
        bool reset = redone && history.TryUndo(out _, out _, out _) &&
            registry.RegisteredPlaceables.Count == originalCount &&
            history.UndoCount == undoBefore;
        Complete(placed64 && reversed && redone && reset,
            "created64=" + created + " library64=" + captured +
            " reloaded64=" + loaded + " identitiesUnique=" + newIds +
            " undoRedo=" + reset + " directMs=" + directMs +
            " reloadMs=" + loadedMs + " directAllocBytes=" + directBytes +
            " reloadAllocBytes=" + loadedBytes + " totalCostCents=" + totalCents +
            " countOriginal=" + originalCount + " error=" + placeError);
    }
    private static void SetField(object owner, string key, object value)
        => typeof(RestaurantArea).GetField(key, BindingFlags.NonPublic |
            BindingFlags.Instance).SetValue(owner, value);
    private void Complete(bool passed, string details)
    {
        if (qaComponent != null && areaRegistry != null)
            areaRegistry.UnregisterArea(qaComponent);
        if (qaArea != null) Destroy(qaArea);
        if (!string.IsNullOrEmpty(tempLibrary))
        {
            try
            {
                if (File.Exists(tempLibrary)) File.Delete(tempLibrary);
                if (File.Exists(tempLibrary + ".bak"))
                    File.Delete(tempLibrary + ".bak");
            }
            catch (IOException) {}
        }
        BistroBuilderEditorV2B12Valid64SelfTest.End(passed, details);
    }
}
