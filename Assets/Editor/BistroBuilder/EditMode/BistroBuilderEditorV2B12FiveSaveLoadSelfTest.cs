using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>B12 acceptance: actual five-object template through the real save/load orchestrator.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B12FiveSaveLoadSelfTest
{
    private const string Stage = "BB.EditorV2.B12.FiveSaveLoad.Stage";
    private const string Success = "BB.EditorV2.B12.FiveSaveLoad.Success";
    private const string Report = "EditorV2_B12_FiveSaveLoad_Report.txt";
    static BistroBuilderEditorV2B12FiveSaveLoadSelfTest()
    {
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.playModeStateChanged += Changed;
    }
    [MenuItem("Bistro Builder/QA/Editor V2/B12 Five Piece Save Load")]
    public static void RunFromMenu() => Start(false);
    public static void RunFromCommandLine() => Start(true);
    private static void Start(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Already entering Play Mode");
        File.WriteAllText(Path.GetFullPath(Report), "B12 Five SaveLoad starting\n");
        SessionState.SetBool(Success, false);
        SessionState.SetString(Stage, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__B12_FiveSaveLoad_QA")
                .AddComponent<BistroBuilderEditorV2B12FiveSaveLoadDriver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Success, false) ? 0 : 1);
        }
    }
    public static void Complete(bool passed, string message)
    {
        string stage = SessionState.GetString(Stage, "");
        string report = "B12 FIVE-PIECE ACTUAL SAVE/LOAD\n" +
            (passed ? "PASS " : "FAIL ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(Report), report);
        if (passed) Debug.Log("[B12 5 SAVELOAD] " + report);
        else Debug.LogError("[B12 5 SAVELOAD] " + report);
        SessionState.SetBool(Success, passed);
        SessionState.SetString(Stage, stage.EndsWith("cli", StringComparison.Ordinal)
            ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
public sealed class BistroBuilderEditorV2B12FiveSaveLoadDriver : MonoBehaviour
{
    private BistroBuilderSaveGameService save;
    private int slot = -1;
    private string temporaryLibrary;
    private bool operationFinished;
    private bool operationSucceeded;
    private string operationError;
    private bool completed;

    private IEnumerator Start()
    {
        for (int i = 0; i < 12; i++) yield return null;
        save = Object.FindFirstObjectByType<BistroBuilderSaveGameService>(
            FindObjectsInactive.Include);
        var library = Object.FindFirstObjectByType<BistroBuilderEditorV2TemplateLibrary>(
            FindObjectsInactive.Include);
        var mode = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var group = Object.FindFirstObjectByType<BistroBuilderEditorV2GroupOperationService>(
            FindObjectsInactive.Include);
        var selection = Object.FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>(
            FindObjectsInactive.Include);
        var seating = Object.FindFirstObjectByType<RestaurantSeatingTopologyService>(
            FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var provider = Object.FindFirstObjectByType<RestaurantStructureSaveSectionProvider>(
            FindObjectsInactive.Include);
        if (save == null || library == null || mode == null || group == null ||
            selection == null || seating == null || registry == null || provider == null)
        {
            Finish(false, "Missing required runtime authorities");
            yield break;
        }
        save.RefreshExtensions();
        string providerError = "";
        if (!save.HasProvider(RestaurantStructureSaveSectionProvider.StableSectionId) ||
            !provider.ValidateConfiguration(out providerError))
        {
            Finish(false, "Structure provider unavailable: " + providerError);
            yield break;
        }
        for (int id = 970; id <= 989; id++)
            if (!save.SlotExists(id)) { slot = id; break; }
        if (slot < 0)
        {
            Finish(false, "No vacant isolated QA slot between 970 and 989");
            yield break;
        }
        temporaryLibrary = Path.Combine(Path.GetTempPath(),
            "bb_b12_5_save_" + Guid.NewGuid().ToString("N") + ".json");
        library.UseTemporaryStorageForTest(temporaryLibrary);
        save.OperationCompleted += OperationCompleted;
        if (!mode.IsEditModeActive &&
            !mode.TryEnterEditMode(out _, out string editError))
        {
            Finish(false, "Cannot enter edit mode: " + editError);
            yield break;
        }
        int initialCount = registry.RegisteredPlaceables.Count;
        seating.RebuildImmediately();
        var objects = new List<RestaurantPlaceableObject>();
        foreach (var item in registry.RegisteredPlaceables)
            if (item != null && item.ItemDefinition != null)
                objects.Add(item);
        objects.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));
        var fixture = new List<RestaurantPlaceableObject>(5);
        foreach (var table in objects)
        {
            var config = table.GetComponent<RestaurantTableSeatingConfiguration>();
            if (config == null) continue;
            fixture.Clear();
            fixture.Add(table);
            foreach (var chair in objects)
            {
                var seat = chair.GetComponent<RestaurantSeat>();
                if (seat != null && seat.IsAssociated && seat.AssociatedTable == config)
                    fixture.Add(chair);
                if (fixture.Count == 5) break;
            }
            if (fixture.Count == 5) break;
        }
        if (fixture.Count != 5)
        {
            Finish(false, "No real table with 4 associated chairs found");
            yield break;
        }
        Vector3 center = Vector3.zero;
        var selected = new List<BistroBuilderEditorV2Selection>(5);
        var originalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in fixture)
        {
            originalIds.Add(item.InstanceId);
            center += item.PlacementAnchor.position;
            selected.Add(new BistroBuilderEditorV2Selection {
                family = BistroBuilderEditorV2ToolFamily.Furniture,
                kind = BistroBuilderEditorV2SelectionKind.Furniture,
                stableId = item.InstanceId, displayName = item.DisplayName,
                persistentIdentity = true,
                capabilities = BistroBuilderEditorV2SelectionCapability.Inspect |
                    BistroBuilderEditorV2SelectionCapability.Delete
            });
        }
        center /= fixture.Count;
        if (!selection.ReplaceSelectionSet(selected, fixture[0].InstanceId,
                out string selectError) ||
            !library.TryCaptureSelection("B12 Five SaveLoad",
                out string templateId, out string captureError) ||
            !library.TryReload(out _) ||
            !library.TryFind(templateId, out var savedTemplate) ||
            savedTemplate.members.Count != 5)
        {
            Finish(false, "Template capture/reload failed: " + selectError);
            yield break;
        }
        if (!group.TryDeleteSelection(out string deleteError))
        {
            Finish(false, "Cannot free original five-piece fixture: " + deleteError);
            yield break;
        }
        if (!library.TryPlace(templateId, center, Quaternion.identity,
            out IReadOnlyList<RestaurantPlaceableObject> generated,
            out string placeError) || generated.Count != 5)
        {
            Finish(false, "Cannot recreate five-piece template: " + placeError);
            yield break;
        }
        seating.RebuildImmediately();
        var expectedIds = new HashSet<string>(StringComparer.Ordinal);
        var newTable = default(RestaurantTableSeatingConfiguration);
        foreach (var item in generated)
        {
            if (originalIds.Contains(item.InstanceId))
            {
                Finish(false, "Recreated item reused original instance ID");
                yield break;
            }
            expectedIds.Add(item.InstanceId);
            var t = item.GetComponent<RestaurantTableSeatingConfiguration>();
            if (t != null) newTable = t;
        }
        int newLinkedSeats = 0;
        foreach (var item in generated)
        {
            var seat = item.GetComponent<RestaurantSeat>();
            if (seat != null && seat.IsAssociated &&
                seat.AssociatedTable == newTable)
                newLinkedSeats++;
        }
        if (newTable == null || newLinkedSeats != 4 ||
            registry.RegisteredPlaceables.Count != initialCount)
        {
            Finish(false, "Template created but 4 seating links are missing");
            yield break;
        }
        var beforeCapture = new BistroBuilderSaveCaptureContext(slot);
        IEnumerator before = provider.CaptureState(beforeCapture);
        while (before.MoveNext()) yield return before.Current;
        var beforeData = beforeCapture.State as RestaurantStructureSaveData;
        var expectedLinks = LinksForIds(beforeData, expectedIds);
        if (beforeCapture.HasFailed || beforeData == null ||
            expectedLinks.Count != 4)
        {
            Finish(false, "Pre-save structure does not contain all four links");
            yield break;
        }
        int originalSavedCount = beforeData.placeables.Count;
        var renovation = Object.FindFirstObjectByType<BistroBuilderEditorV2RenovationSession>(
            FindObjectsInactive.Include);
        if (renovation != null && renovation.IsActive && renovation.HasPendingChanges &&
            !renovation.TryApplyChanges(out string applyError))
        {
            Finish(false, "Renovation commit rejected: " + applyError);
            yield break;
        }
        if (mode.IsEditModeActive && !mode.TryExitEditMode(false, out var exitReason))
        {
            Finish(false, "Exit edit rejected: " + exitReason);
            yield break;
        }
        operationFinished = false;
        if (!save.TrySaveSlot(slot, "B12 FIVE PIECE QA", out string saveError))
        {
            Finish(false, "Save rejected: " + saveError);
            yield break;
        }
        yield return WaitForOperation();
        if (!operationSucceeded)
        {
            Finish(false, "Save failed: " + operationError);
            yield break;
        }
        operationFinished = false;
        if (!save.TryLoadSlot(slot, out string loadError))
        {
            Finish(false, "Load rejected: " + loadError);
            yield break;
        }
        yield return WaitForOperation();
        if (!operationSucceeded)
        {
            Finish(false, "Load failed: " + operationError);
            yield break;
        }
        var loadedRegistry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var loadedProvider = Object.FindFirstObjectByType<
            RestaurantStructureSaveSectionProvider>(FindObjectsInactive.Include);
        var loadedTopology = Object.FindFirstObjectByType<
            RestaurantSeatingTopologyService>(FindObjectsInactive.Include);
        loadedTopology?.RebuildImmediately();
        var live = new HashSet<string>(StringComparer.Ordinal);
        if (loadedRegistry != null)
            foreach (var item in loadedRegistry.RegisteredPlaceables)
                if (item != null) live.Add(item.InstanceId);
        var afterCapture = new BistroBuilderSaveCaptureContext(slot);
        IEnumerator after = loadedProvider.CaptureState(afterCapture);
        while (after.MoveNext()) yield return after.Current;
        var loadedData = afterCapture.State as RestaurantStructureSaveData;
        var afterLinks = LinksForIds(loadedData, expectedIds);
        bool idsPersist = expectedIds.IsSubsetOf(live);
        bool oldAbsent = !originalIds.Overlaps(live);
        bool countMatch = loadedData != null &&
            loadedData.placeables.Count == originalSavedCount;
        bool linksMatch = expectedLinks.Count == afterLinks.Count;
        for (int i = 0; i < expectedLinks.Count && i < afterLinks.Count; i++)
            linksMatch &= expectedLinks[i] == afterLinks[i];
        bool pass = idsPersist && oldAbsent && countMatch && linksMatch &&
            !afterCapture.HasFailed && live.Count == initialCount;
        operationFinished = false;
        if (!save.TryDeleteSlot(slot, out string clearError))
        {
            Finish(false, "Could not delete reserved QA save slot: " + clearError);
            yield break;
        }
        yield return WaitForOperation();
        if (!operationSucceeded)
        {
            Finish(false, "QA slot cleanup failed: " + operationError);
            yield break;
        }
        Finish(pass, "savedFive=True loadedFive=" + idsPersist +
            " originalAbsent=" + oldAbsent + " seatsBefore=" + expectedLinks.Count +
            " seatsAfter=" + afterLinks.Count + " exactLinks=" + linksMatch +
            " objectsBefore=" + originalSavedCount + " objectsAfter=" +
            (loadedData == null ? -1 : loadedData.placeables.Count) +
            " saveSlotDeleted=True");
    }
    private static List<string> LinksForIds(RestaurantStructureSaveData data,
        HashSet<string> ids)
    {
        var links = new List<string>();
        if (data == null) return links;
        foreach (var x in data.seatLinks)
            if (ids.Contains(x.tableInstanceId) && ids.Contains(x.seatInstanceId))
                links.Add(x.tableInstanceId + "|" + x.seatInstanceId + "|" + x.slotIndex);
        links.Sort(StringComparer.Ordinal);
        return links;
    }
    private IEnumerator WaitForOperation()
    {
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!operationFinished && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!operationFinished)
        {
            operationSucceeded = false;
            operationError = "Operation timed out after 90 seconds";
        }
    }
    private void OperationCompleted(BistroBuilderSaveOperationResult result)
    {
        operationSucceeded = result != null && result.Succeeded;
        operationError = result != null ? result.Message : "null result";
        operationFinished = true;
    }
    private void Finish(bool passed, string detail)
    {
        if (completed) return;
        completed = true;
        if (save != null)
        {
            save.OperationCompleted -= OperationCompleted;
            if (slot >= 0 && save.SlotExists(slot) && !save.IsBusy)
                save.TryDeleteSlot(slot, out _);
        }
        if (temporaryLibrary != null)
        {
            try
            {
                if (File.Exists(temporaryLibrary)) File.Delete(temporaryLibrary);
                if (File.Exists(temporaryLibrary + ".bak"))
                    File.Delete(temporaryLibrary + ".bak");
            }
            catch (IOException) { }
        }
        BistroBuilderEditorV2B12FiveSaveLoadSelfTest.Complete(passed, detail);
    }
}
