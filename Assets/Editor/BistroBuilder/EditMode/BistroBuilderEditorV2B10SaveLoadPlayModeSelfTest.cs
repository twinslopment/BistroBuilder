using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// B10 acceptance: real Play Mode -> runtime replacement -> SaveGame slot
/// -> orchestrated Load -> rebuilt registry, structure and seat links.
/// Never uses a player's existing slot or writes a source scene.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest
{
    private const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.EditorV2.B10.SaveLoad.Stage";
    private const string SuccessKey = "BB.EditorV2.B10.SaveLoad.Passed";
    private const string Report = "EditorV2_B10_SaveLoadPlay_Report.txt";

    static BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayChanged;
        EditorApplication.playModeStateChanged += OnPlayChanged;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B10 Save Load Real Play Test")]
    public static void RunFromMenu() => Start(false);

    public static void RunFromCommandLine() => Start(true);

    private static void Start(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode already active");
        File.WriteAllText(Path.GetFullPath(Report),
            "B10 Play Mode Save/Load started.\n");
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            GameObject host = new GameObject("__B10_SaveLoadPlay_TestRunner");
            host.AddComponent<BistroBuilderEditorV2B10PlayDriver>();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(StageKey);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
        }
    }

    public static void End(bool passed, string summary)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        string report = "EDITOR V2 B10 - RUNTIME SAVE/LOAD\n" +
            (passed ? "PASS - " : "FAIL - ") + summary + "\n";
        File.WriteAllText(Path.GetFullPath(Report), report);
        if (passed) Debug.Log("[B10 SAVELOAD] " + report);
        else Debug.LogError("[B10 SAVELOAD] " + report);
        SessionState.SetBool(SuccessKey, passed);
        SessionState.SetString(StageKey,
            stage.EndsWith("cli", StringComparison.Ordinal)
                ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2B10PlayDriver : MonoBehaviour
{
    private BistroBuilderSaveGameService save;
    private int slot = -1;
    private bool completed;
    private bool success;
    private string completionMessage;
    private string savedId;
    private string removedId;
    private string savedDefinitionId;
    private List<string> expectedLinks;
    private int expectedCount;

    private void Start() { StartCoroutine(Run()); }

    private IEnumerator Run()
    {
        for (int i = 0; i < 8; i++) yield return null;
        save = Object.FindFirstObjectByType<BistroBuilderSaveGameService>(
            FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var edit = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var creation = Object.FindFirstObjectByType<RestaurantPlaceableCreationService>(
            FindObjectsInactive.Include);
        var provider = Object.FindFirstObjectByType<RestaurantStructureSaveSectionProvider>(
            FindObjectsInactive.Include);
        if (save == null || registry == null || edit == null ||
            creation == null || provider == null)
        {
            Fail("Missing real runtime providers");
            yield break;
        }

        save.RefreshExtensions();
        string configureError = string.Empty;
        if (!save.HasProvider(RestaurantStructureSaveSectionProvider.StableSectionId) ||
            !provider.ValidateConfiguration(out configureError))
        {
            Fail("Save provider unavailable: " + configureError);
            yield break;
        }

        for (int candidate = 950; candidate <= 969; candidate++)
            if (!save.SlotExists(candidate)) { slot = candidate; break; }
        if (slot < 0) { Fail("No free isolated QA slot 950-969"); yield break; }
        save.OperationCompleted += OnOperationCompleted;

        if (!edit.IsEditModeActive &&
            !edit.TryEnterEditMode(out _, out string enterError))
        {
            Fail("Cannot enter Edit Mode: " + enterError); yield break;
        }

        string[] guids = AssetDatabase.FindAssets("t:RestaurantPlaceableItemDefinition");
        var defs = new List<RestaurantPlaceableItemDefinition>(guids.Length);
        foreach (string guid in guids)
        {
            var def = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (def != null && def.HasValidPrefab) defs.Add(def);
        }

        var sources = new List<RestaurantPlaceableObject>();
        foreach (var placed in registry.RegisteredPlaceables)
            if (placed != null && placed.ItemDefinition != null)
                sources.Add(placed);
        sources.Sort((a, b) =>
            string.CompareOrdinal(a.InstanceId, b.InstanceId));
        bool replaced = false;
        string lastError = string.Empty;
        for (int i = 0; i < sources.Count && i < 60 && !replaced; i++)
        {
            var source = sources[i];
            if (!registry.ContainsPlaceable(source)) continue;
            for (int j = 0; j < defs.Count && !replaced; j++)
            {
                var target = defs[j];
                if (source.ItemDefinition.Category != target.Category ||
                    source.ItemDefinition == target) continue;
                bool worked = creation.TryReplaceBatch(
                    new[] { source }, target, out var created,
                    out RestaurantPlaceableReplacementResult result);
                if (worked && created.Count == 1)
                {
                    replaced = true;
                    savedId = created[0].InstanceId;
                    removedId = source.InstanceId;
                    savedDefinitionId = target.ItemId;
                }
                else lastError = result.Message;
            }
        }
        if (!replaced)
        {
            Fail("No runtime-compatible replacement: " + lastError);
            yield break;
        }
        if (string.IsNullOrWhiteSpace(savedId) ||
            string.Equals(removedId, savedId, StringComparison.Ordinal))
        {
            Fail("Replacement identity collision"); yield break;
        }

        // Give the canonical seating reconciliation a chance to finish
        // before capturing persistent seat links. The replacement itself
        // remains synchronous, but seating topology may rebuild on next frame.
        for (int settle = 0; settle < 3; settle++) yield return null;
        var seating = Object.FindFirstObjectByType<RestaurantSeatingTopologyService>(
            FindObjectsInactive.Include);
        seating?.RebuildImmediately();
        yield return null;

        var snapshotContext = new BistroBuilderSaveCaptureContext(slot);
        IEnumerator capture = provider.CaptureState(snapshotContext);
        while (capture.MoveNext()) yield return capture.Current;
        var snapshot = snapshotContext.State as RestaurantStructureSaveData;
        if (snapshotContext.HasFailed || snapshot == null)
        {
            Fail("Before-save structure capture failed: " +
                 snapshotContext.ErrorMessage); yield break;
        }
        expectedCount = snapshot.placeables.Count;
        expectedLinks = MakeLinks(snapshot);
        bool hadNew = false;
        foreach (var p in snapshot.placeables)
            if (p.instanceId == savedId && p.itemId == savedDefinitionId)
                hadNew = true;
        if (!hadNew || snapshot.placeables.Exists(x => x.instanceId == removedId))
        {
            Fail("Capture still includes old object / missing new");
            yield break;
        }
        var renovation = Object.FindFirstObjectByType<BistroBuilderEditorV2RenovationSession>(
            FindObjectsInactive.Include);
        if (renovation != null && renovation.IsActive &&
            renovation.HasPendingChanges &&
            !renovation.TryApplyChanges(out string applyError))
        {
            Fail("Could not commit edit renovation: " + applyError);
            yield break;
        }
        if (edit.IsEditModeActive && !edit.TryExitEditMode(false, out var exitReason))
        {
            Fail("Cannot leave edit mode before save: " +
                exitReason + " / " + edit.LastExitRejectionMessage);
            yield break;
        }

        completed = false;
        if (!save.TrySaveSlot(slot, "B10 REPLACEMENT QA", out string saveError))
        {
            Fail("Real Save rejected: " + saveError); yield break;
        }
        yield return WaitForOperation("Save");
        if (!success) yield break;

        completed = false;
        if (!save.TryLoadSlot(slot, out string loadError))
        {
            Fail("Real Load rejected: " + loadError); yield break;
        }
        yield return WaitForOperation("Load");
        if (!success) yield break;

        var registryAfter = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(
            FindObjectsInactive.Include);
        var providerAfter =
            Object.FindFirstObjectByType<RestaurantStructureSaveSectionProvider>(
                FindObjectsInactive.Include);
        if (registryAfter == null || providerAfter == null)
        {
            Fail("Missing rebuilt runtime authorities after Load");
            yield break;
        }
        bool found = false;
        bool oldFound = false;
        foreach (var obj in registryAfter.RegisteredPlaceables)
        {
            if (obj == null) continue;
            found |= obj.InstanceId == savedId &&
                obj.ItemDefinition != null &&
                obj.ItemDefinition.ItemId == savedDefinitionId;
            oldFound |= obj.InstanceId == removedId;
        }
        var loadedContext = new BistroBuilderSaveCaptureContext(slot);
        IEnumerator loadedCapture = providerAfter.CaptureState(loadedContext);
        while (loadedCapture.MoveNext()) yield return loadedCapture.Current;
        var loaded = loadedContext.State as RestaurantStructureSaveData;
        bool valid = found && !oldFound && !loadedContext.HasFailed &&
            loaded != null && loaded.placeables.Count == expectedCount;
        List<string> links = loaded != null
            ? MakeLinks(loaded) : new List<string>();
        bool linkMatch = links.Count == expectedLinks.Count;
        for (int i = 0; i < links.Count && i < expectedLinks.Count; i++)
            linkMatch &= links[i] == expectedLinks[i];
        valid &= linkMatch;
        string diagnosis = "found=" + found + " oldFound=" + oldFound +
            " captureFailed=" + loadedContext.HasFailed +
            " loadedCount=" + (loaded != null ? loaded.placeables.Count : -1) +
            " expectedCount=" + expectedCount +
            " linkedSeats=" + links.Count +
            " expectedSeats=" + expectedLinks.Count +
            " linksMatch=" + linkMatch +
            " captureError=" + loadedContext.ErrorMessage;
        if (!linkMatch)
        {
            string expectedFirst = expectedLinks.Count > 0 ?
                expectedLinks[0] : "(none)";
            string actualFirst = links.Count > 0 ? links[0] : "(none)";
            diagnosis += " expectedFirst=" + expectedFirst +
                " actualFirst=" + actualFirst;
            foreach (string link in links)
                if (!expectedLinks.Contains(link))
                    diagnosis += " extraLink=" + link;
            foreach (string link in expectedLinks)
                if (!links.Contains(link))
                    diagnosis += " missingLink=" + link;
        }

        completed = false;
        if (!save.TryDeleteSlot(slot, out string deleteError))
        {
            Fail("QA slot deletion rejected: " + deleteError); yield break;
        }
        yield return WaitForOperation("Delete");
        if (!success) yield break;

        if (!valid)
        {
            Fail("Post-load identity, object count or seating links differ: " +
                diagnosis);
            yield break;
        }
        Pass("real Play Mode, orchestrated Save->Load->Delete; replaced " +
             savedId + " (" + savedDefinitionId + "), objects=" +
             expectedCount + ", seat links=" + expectedLinks.Count +
             ", original ID absent; runtime references reconstructed.");
    }

    private static List<string> MakeLinks(RestaurantStructureSaveData data)
    {
        var result = new List<string>();
        foreach (var link in data.seatLinks)
            result.Add(link.tableInstanceId + "|" +
                link.seatInstanceId + "|" + link.slotIndex);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private IEnumerator WaitForOperation(string label)
    {
        float end = Time.realtimeSinceStartup + 90f;
        while (!completed && Time.realtimeSinceStartup < end)
            yield return null;
        if (!completed) { Fail(label + " timed out"); yield break; }
        if (!success) { Fail(label + " failed: " + completionMessage); yield break; }
    }

    private void OnOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        completionMessage = result != null ? result.Message : "null result";
        success = result != null && result.Succeeded;
        completed = true;
    }

    private void Pass(string message)
    {
        if (save != null) save.OperationCompleted -= OnOperationCompleted;
        BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest.End(true, message);
    }

    private void Fail(string message)
    {
        if (save != null)
        {
            save.OperationCompleted -= OnOperationCompleted;
            if (slot >= 0 && save.SlotExists(slot) && !save.IsBusy)
                save.TryDeleteSlot(slot, out _);
        }
        BistroBuilderEditorV2B10SaveLoadPlayModeSelfTest.End(false, message);
    }
}
