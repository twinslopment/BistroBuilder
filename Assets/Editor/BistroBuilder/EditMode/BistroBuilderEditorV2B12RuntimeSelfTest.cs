using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Failure injection: third purchase rejects; rollback must refund
/// exactly the first two and leave no placed or history state.</summary>
public sealed class BistroBuilderB12FailThirdPurchase : IRestaurantPlaceableEconomyGate
{
    public int Attempted;
    public int Charged;
    public int Reversed;
    public bool TryAuthorizeCreation(RestaurantPlaceableObject p, out string error)
    {
        error = "";
        return true;
    }
    public bool TryCommitCreation(RestaurantPlaceableObject p, out string error)
    {
        Attempted++;
        if (Attempted == 3)
        {
            error = "Injected insufficient funds at item three.";
            return false;
        }
        Charged++;
        error = "";
        return true;
    }
    public bool TryRollbackCreation(RestaurantPlaceableObject p, out string error)
    {
        Reversed++;
        error = "";
        return true;
    }
    public bool TryAuthorizeDeletion(RestaurantPlaceableObject p, out string error)
    {
        error = "";
        return true;
    }
    public bool TryCommitDeletion(RestaurantPlaceableObject p, out string error)
    {
        error = "";
        return true;
    }
    public bool TryRollbackDeletion(RestaurantPlaceableObject p, out string error)
    {
        error = "";
        return true;
    }
}

/// <summary>B12: real scene capture/reload/placement, isolated temporary library.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B12RuntimeSelfTest
{
    private const string Stage = "BB.B12.Stage";
    private const string Result = "BB.B12.Result";
    private const string Report = "EditorV2_B12_Runtime_Report.txt";
    static BistroBuilderEditorV2B12RuntimeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnChanged;
        EditorApplication.playModeStateChanged += OnChanged;
    }
    [MenuItem("Bistro Builder/QA/Editor V2/B12 Templates Runtime")]
    public static void RunFromMenu() => Launch(false);
    public static void RunFromCommandLine() => Launch(true);
    private static void Launch(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode ya está activo.");
        File.WriteAllText(Path.GetFullPath(Report), "B12 Starting\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage, cli ? "enter_cli" : "enter_manual");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void OnChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__B12_QA").AddComponent<BistroBuilderEditorV2B12Driver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }
    public static void Finish(bool passed, string details)
    {
        string stage = SessionState.GetString(Stage, "");
        File.WriteAllText(Path.GetFullPath(Report),
            "B12 real Play Mode\n" + (passed ? "PASS " : "FAIL ") + details + "\n");
        if (passed) Debug.Log("[B12] PASS " + details);
        else Debug.LogError("[B12] FAIL " + details);
        SessionState.SetBool(Result, passed);
        SessionState.SetString(Stage, stage.EndsWith("cli", StringComparison.Ordinal)
            ? "exit_cli" : "exit_manual");
        EditorApplication.ExitPlaymode();
    }
}
public sealed class BistroBuilderEditorV2B12Driver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int i = 0; i < 12; i++) yield return null;
        var library = Object.FindFirstObjectByType<
            BistroBuilderEditorV2TemplateLibrary>(FindObjectsInactive.Include);
        var mode = Object.FindFirstObjectByType<
            RestaurantEditModeService>(FindObjectsInactive.Include);
        var catalog = Object.FindFirstObjectByType<
            RestaurantPlaceableCatalogService>(FindObjectsInactive.Include);
        var registry = Object.FindFirstObjectByType<
            RestaurantPlaceableRegistry>(FindObjectsInactive.Include);
        var controller = Object.FindFirstObjectByType<
            RestaurantEditInteractionController>(FindObjectsInactive.Include);
        var history = Object.FindFirstObjectByType<
            RestaurantPlacementHistoryService>(FindObjectsInactive.Include);
        if (library == null || mode == null || catalog == null ||
            registry == null || controller == null || history == null)
        {
            BistroBuilderEditorV2B12RuntimeSelfTest.Finish(
                false, "Faltan autoridades de escena");
            yield break;
        }
        string path = Path.Combine(Path.GetTempPath(),
            "bb_b12_qa_" + Guid.NewGuid().ToString("N") + ".json");
        library.UseTemporaryStorageForTest(path);
        int initialItems = registry.RegisteredPlaceables.Count;
        int initialUndo = history.UndoCount;

        bool beforeGuard = !library.TryCaptureSelection(
            "fuera modo", out _, out _);
        if (!mode.IsEditModeActive &&
            !mode.TryEnterEditMode(out _, out string modeError))
        {
            BistroBuilderEditorV2B12RuntimeSelfTest.Finish(
                false, "No entró en edición: " + modeError);
            yield break;
        }
        var candidates = new List<RestaurantPlaceableObject>();
        foreach (var item in registry.RegisteredPlaceables)
        {
            if (item == null || item.ItemDefinition == null ||
                !catalog.TryGetItem(item.ItemDefinition.ItemId, out var canonical) ||
                canonical == null || !canonical.HasValidPrefab) continue;
            candidates.Add(item);
        }
        candidates.Sort((a,b) => a.ItemDefinition.PurchasePrice
            .CompareTo(b.ItemDefinition.PurchasePrice));
        if (candidates.Count == 0)
        {
            BistroBuilderEditorV2B12RuntimeSelfTest.Finish(
                false, "Ningún mueble canónico para la prueba");
            yield break;
        }

        // Use the B8 authority directly: the 38 demo scene objects are
        // registered placeables but their legacy EditableObject components
        // do not necessarily permit the older UI controller's selection.
        RestaurantPlaceableObject source = candidates.Find(x =>
            x.ItemDefinition.ItemId == "table_basic_4") ??
            candidates.Find(x =>
            x.ItemDefinition.Category == RestaurantPlaceableItemCategory.Decoration)
            ?? candidates.Find(x =>
                x.ItemDefinition.Category == RestaurantPlaceableItemCategory.Lighting)
            ?? candidates.Find(x =>
                x.ItemDefinition.Category == RestaurantPlaceableItemCategory.Furniture)
            ?? candidates[0];
        var selection = Object.FindFirstObjectByType<
            BistroBuilderEditorV2SelectionCoordinator>(FindObjectsInactive.Include);
        controller.ClearSelection();
        bool selectionOk = selection != null &&
            selection.ReplaceSelectionSet(new [] {
                new BistroBuilderEditorV2Selection {
                    family = BistroBuilderEditorV2ToolFamily.Furniture,
                    kind = BistroBuilderEditorV2SelectionKind.Furniture,
                    stableId = source.InstanceId,
                    displayName = source.DisplayName,
                    capabilities = BistroBuilderEditorV2SelectionCapability.Inspect |
                        BistroBuilderEditorV2SelectionCapability.Delete |
                        BistroBuilderEditorV2SelectionCapability.Duplicate,
                    persistentIdentity = true
                }
            }, source.InstanceId, out _);
        if (!selectionOk)
        {
            BistroBuilderEditorV2B12RuntimeSelfTest.Finish(false,
                "No pudo inicializarse la selección B8 de prueba");
            yield break;
        }
        bool stored = library.TryCaptureSelection("Composición QA",
            out string id, out string captureError);
        if (!selectionOk || !stored)
        {
            BistroBuilderEditorV2B12RuntimeSelfTest.Finish(false,
                "Captura fallida: " + captureError +
                " select=" + selectionOk);
            yield break;
        }
        bool saved = File.Exists(path) && library.TemplateCount == 1;
        bool got = library.TryFind(id, out var original) &&
            original.members.Count == 1 &&
            original.members[0].itemId == source.ItemDefinition.ItemId;
        bool loaded = library.TryReload(out string loadError) &&
            library.TryFind(id, out var restored) &&
            restored.members[0].itemId == source.ItemDefinition.ItemId;
        bool quote = library.TryQuote(id, out long cents, out string costError) &&
            cents == source.ItemDefinition.PurchasePriceCents;
        Vector3 origin = source.PlacementAnchor.position;
        bool noBadId = !library.TryPlace("id-no-existe", origin,
            Quaternion.identity, out _, out _);

        // The original stays in the scene: this also verifies that a
        // recovered template creates independent new instance identities.
        // Check multiple actual world positions via the normal validator;
        // never bypass an accessibility / service-envelope constraint.
        bool placed = false;
        IReadOnlyList<RestaurantPlaceableObject> created = null;
        string placeError = "";
        Vector3 target = Vector3.zero;
        int attempts = 0;
        for (int radius = 1; radius <= 6 && !placed; radius++)
        {
            for (int x = -radius; x <= radius && !placed; x++)
                for (int z = -radius; z <= radius && !placed; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius)
                        continue;
                    target = origin + new Vector3(x * 1.8f, 0f, z * 1.8f);
                    attempts++;
                    placed = library.TryPlace(id, target, Quaternion.identity,
                        out created, out placeError);
                    if (!placed) yield return null;
                }
        }
        bool identity = placed && created.Count == 1 &&
            created[0].ItemDefinition.ItemId == source.ItemDefinition.ItemId &&
            Vector3.Distance(created[0].PlacementAnchor.position, target) < 0.002f &&
            created[0].InstanceId != source.InstanceId;
        bool undo = placed && history.TryUndo(out _, out _, out _);
        bool redo = undo && history.TryRedo(out _, out _, out _);
        bool rewindNew = redo && history.TryUndo(out _, out _, out _);
        bool restoredWorld = rewindNew &&
            registry.RegisteredPlaceables.Count == initialItems &&
            history.UndoCount == initialUndo;

        var creator = Object.FindFirstObjectByType<
            RestaurantPlaceableCreationService>(FindObjectsInactive.Include);
        var duplicateSpec = new BistroBuilderEditorV2TemplateSpawn(
            source.ItemDefinition, target, Quaternion.identity);
        bool sameCellRejected = false;
        if (creator != null)
        {
            bool allowedOverlap = creator.TryCreateTemplateBatch(
                new [] { duplicateSpec, duplicateSpec },
                out var overlapping, out var overlapResult);
            sameCellRejected = !allowedOverlap &&
                overlapResult.CreatedCount == 0 &&
                overlapping.Count == 0 &&
                !string.IsNullOrEmpty(overlapResult.Message) &&
                registry.RegisteredPlaceables.Count == initialItems &&
                history.UndoCount == initialUndo;
        }
        var beyondLimit = new BistroBuilderEditorV2TemplateSpawn[65];
        for (int i = 0; i < beyondLimit.Length; i++)
            beyondLimit[i] = duplicateSpec;
        bool limitRejected = creator != null &&
            !creator.TryCreateTemplateBatch(beyondLimit, out var excess,
                out var limitResult) &&
            limitResult.CreatedCount == 0 && excess.Count == 0 &&
            registry.RegisteredPlaceables.Count == initialItems &&
            history.UndoCount == initialUndo;

        bool stress64 = false;
        long stress64Ms = -1;
        if (creator != null)
        {
            var sixtyFour = new BistroBuilderEditorV2TemplateSpawn[64];
            for (int i = 0; i < sixtyFour.Length; i++)
                sixtyFour[i] = duplicateSpec;
            var timer64 = System.Diagnostics.Stopwatch.StartNew();
            bool accepted64 = creator.TryCreateTemplateBatch(
                sixtyFour, out var rejected64, out var status64);
            timer64.Stop();
            stress64Ms = timer64.ElapsedMilliseconds;
            stress64 = !accepted64 && rejected64.Count == 0 &&
                status64.CreatedCount == 0 &&
                registry.RegisteredPlaceables.Count == initialItems &&
                history.UndoCount == initialUndo;
        }
        bool removed = library.TryRemove(id, out _) &&
            library.TryReload(out _) && library.TemplateCount == 0;
        bool multiRoundTrip = false;
        if (candidates.Count > 1 && selection != null)
        {
            var second = candidates.Find(x => x.InstanceId != source.InstanceId);
            if (second != null)
            {
                var two = new [] {
                    new BistroBuilderEditorV2Selection {
                        family = BistroBuilderEditorV2ToolFamily.Furniture,
                        kind = BistroBuilderEditorV2SelectionKind.Furniture,
                        stableId = source.InstanceId,
                        displayName = source.DisplayName,
                        capabilities = BistroBuilderEditorV2SelectionCapability.Inspect,
                        persistentIdentity = true
                    },
                    new BistroBuilderEditorV2Selection {
                        family = BistroBuilderEditorV2ToolFamily.Furniture,
                        kind = BistroBuilderEditorV2SelectionKind.Furniture,
                        stableId = second.InstanceId,
                        displayName = second.DisplayName,
                        capabilities = BistroBuilderEditorV2SelectionCapability.Inspect,
                        persistentIdentity = true
                    }
                };
                bool selectedPair = selection.ReplaceSelectionSet(two,
                    source.InstanceId, out _);
                string pairId = string.Empty;
                bool capturedPair = selectedPair && library.TryCaptureSelection(
                    "Mesa y complemento QA", out pairId, out _);
                if (capturedPair)
                {
                    BistroBuilderEditorV2Template pair = null;
                    bool pairReloaded = library.TryReload(out _) &&
                        library.TryFind(pairId, out pair) &&
                        pair.members.Count == 2;
                    bool pairGeometry = pairReloaded &&
                        Mathf.Abs(Vector3.Distance(
                            pair.members[0].relativeAnchor,
                            pair.members[1].relativeAnchor) -
                            Vector3.Distance(source.PlacementAnchor.position,
                                second.PlacementAnchor.position)) < 0.003f;
                    bool pairQuote = library.TryQuote(pairId,
                        out long quotedPair, out _) &&
                        quotedPair == source.ItemDefinition.PurchasePriceCents +
                            second.ItemDefinition.PurchasePriceCents;
                    bool untouched = registry.RegisteredPlaceables.Count == initialItems;
                    bool pairRemoved = library.TryRemove(pairId, out _);
                    multiRoundTrip = pairReloaded && pairGeometry &&
                        pairQuote && untouched && pairRemoved;
                }
            }
        }
        // Five-piece functional composition: table and four actual seats
        // associated by the live Seating authority, never guessed by distance.
        bool seating5 = false;
        bool financeRollback = false;
        bool seatingFixture = false;
        string seatingFailure = "";
        int seatingAttempts = 0;
        var seating = Object.FindFirstObjectByType<
            RestaurantSeatingTopologyService>(FindObjectsInactive.Include);
        if (seating != null && selection != null)
        {
            seating.RebuildImmediately();
            foreach (var tablePlaceable in candidates)
            {
                var config = tablePlaceable.GetComponent<
                    RestaurantTableSeatingConfiguration>();
                if (config == null) continue;
                var five = new List<RestaurantPlaceableObject> { tablePlaceable };
                foreach (var chair in candidates)
                {
                    if (chair == tablePlaceable) continue;
                    var seat = chair.GetComponent<RestaurantSeat>();
                    if (seat != null && seat.IsAssociated &&
                        seat.AssociatedTable == config)
                        five.Add(chair);
                    if (five.Count == 5) break;
                }
                if (five.Count != 5) continue;
                seatingFixture = true;
                var references = new List<BistroBuilderEditorV2Selection>();
                Vector3 center = Vector3.zero;
                foreach (var item in five)
                {
                    center += item.PlacementAnchor.position;
                    references.Add(new BistroBuilderEditorV2Selection
                    {
                        family = BistroBuilderEditorV2ToolFamily.Furniture,
                        kind = BistroBuilderEditorV2SelectionKind.Furniture,
                        stableId = item.InstanceId,
                        displayName = item.DisplayName,
                        capabilities = BistroBuilderEditorV2SelectionCapability.Inspect,
                        persistentIdentity = true
                    });
                }
                center /= five.Count;
                string groupId = null;
                bool groupCaptured = selection.ReplaceSelectionSet(
                    references, tablePlaceable.InstanceId, out _) &&
                    library.TryCaptureSelection("Mesa 4 sillas QA", out groupId, out _);
                if (!groupCaptured)
                {
                    seatingFailure = "No se pudo capturar la selección de 5.";
                    break;
                }
                bool groupLoaded = library.TryReload(out _) &&
                    library.TryFind(groupId, out var fiveData) &&
                    fiveData.members.Count == 5;
                if (!groupLoaded)
                {
                    seatingFailure = "No se recuperaron los 5 componentes.";
                    break;
                }
                // Make the exact original 5-piece area free via the B8
                // deletion authority; recreate it from the saved library.
                // Undo both operations to restore the source restaurant.
                for (int k = 0; k < references.Count; k++)
                {
                    var selectable = references[k];
                    selectable.capabilities |=
                        BistroBuilderEditorV2SelectionCapability.Delete;
                    references[k] = selectable;
                }
                var groupAuthority = Object.FindFirstObjectByType<
                    BistroBuilderEditorV2GroupOperationService>(
                        FindObjectsInactive.Include);
                bool deletedFive = groupAuthority != null &&
                    selection.ReplaceSelectionSet(references,
                        tablePlaceable.InstanceId, out _) &&
                    groupAuthority.TryDeleteSelection(out seatingFailure);
                // Destructive finance probe: reject the third of five
                // after two charge events, and verify exact refund and
                // rollback before allowing the real creation.
                bool injectedRollback = false;
                if (deletedFive && creator != null)
                {
                    var field = typeof(RestaurantPlaceableCreationService)
                        .GetField("economyGate",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        object realGate = field.GetValue(creator);
                        var failingGate = new BistroBuilderB12FailThirdPurchase();
                        field.SetValue(creator, failingGate);
                        try
                        {
                            bool unexpectedlyCreated = library.TryPlace(
                                groupId, center, Quaternion.identity,
                                out var blocked, out string injectedError);
                            injectedRollback = !unexpectedlyCreated &&
                                failingGate.Attempted == 3 &&
                                failingGate.Charged == 2 &&
                                failingGate.Reversed == 2 &&
                                blocked.Count == 0 &&
                                registry.RegisteredPlaceables.Count ==
                                    initialItems - five.Count &&
                                history.UndoCount == initialUndo + 1 &&
                                injectedError.Contains("insufficient funds");
                            if (unexpectedlyCreated)
                                history.TryUndo(out _, out _, out _);
                        }
                        finally
                        {
                            field.SetValue(creator, realGate);
                        }
                    }
                }
                financeRollback = injectedRollback;
                bool placedFive = false;
                IReadOnlyList<RestaurantPlaceableObject> fiveCreated = null;
                if (deletedFive)
                {
                    seatingAttempts++;
                    placedFive = library.TryPlace(groupId, center,
                        Quaternion.identity, out fiveCreated, out seatingFailure);
                }
                int newSeats = 0;
                bool undoFive = false, redoFive = false, rewindFive = false;
                if (placedFive)
                {
                    seating.RebuildImmediately();
                    RestaurantTableSeatingConfiguration newTable = null;
                    foreach (var item in fiveCreated)
                    {
                        var table = item.GetComponent<
                            RestaurantTableSeatingConfiguration>();
                        if (table != null) newTable = table;
                    }
                    foreach (var item in fiveCreated)
                    {
                        var seat = item.GetComponent<RestaurantSeat>();
                        if (seat != null && seat.IsAssociated &&
                            seat.AssociatedTable == newTable)
                            newSeats++;
                    }
                    undoFive = history.TryUndo(out _, out _, out _);
                    redoFive = undoFive &&
                        history.TryRedo(out _, out _, out _);
                    rewindFive = redoFive &&
                        history.TryUndo(out _, out _, out _);
                }
                bool restoredFiveOriginal = deletedFive &&
                    history.TryUndo(out _, out _, out _) &&
                    registry.RegisteredPlaceables.Count == initialItems &&
                    history.UndoCount == initialUndo;
                seating5 = placedFive && fiveCreated.Count == 5 &&
                    newSeats == 4 && undoFive && redoFive && rewindFive &&
                    restoredFiveOriginal && financeRollback;
                if (!seating5)
                    seatingFailure = "deleted=" + deletedFive +
                        " placed=" + placedFive + " associated=" + newSeats +
                        " undo=" + undoFive + " redo=" + redoFive +
                        " rewind=" + rewindFive +
                        " originalRestored=" + restoredFiveOriginal +
                        " cause=" + seatingFailure;
                library.TryRemove(groupId, out _);
                break;
            }
        }
        string badFile = path + ".corrupt";
        File.WriteAllText(badFile, "{invalid");
        library.UseTemporaryStorageForTest(badFile);
        bool corruptSafe = library.LoadingError.Length > 0 &&
            !library.TryCaptureSelection("should not overwrite",out _,out _) &&
            File.ReadAllText(badFile) == "{invalid";
        library.UseTemporaryStorageForTest(path);
        try { File.Delete(path); File.Delete(path + ".bak"); File.Delete(badFile); }
        catch (IOException) { }

        bool passed = beforeGuard && saved && got && loaded && quote &&
            noBadId && identity && undo && redo && rewindNew &&
            restoredWorld && removed && multiRoundTrip && corruptSafe &&
            sameCellRejected && limitRejected && stress64 &&
            seatingFixture && seating5;
        BistroBuilderEditorV2B12RuntimeSelfTest.Finish(passed,
            "guard=" + beforeGuard + " capture=" + saved +
            " canonical=" + got + " reload=" + loaded +
            " quote=" + quote + " invalidId=" + noBadId +
            " tried=" + attempts + " placed=" + placed +
            " pose=" + identity + " undo=" + undo + " redo=" + redo +
            " worldRestored=" + restoredWorld + " multi=" + multiRoundTrip +
            " overlapRejected=" + sameCellRejected +
            " limit65Rejected=" + limitRejected +
            " stress64=" + stress64 +
            " stress64Ms=" + stress64Ms +
            " seatingFixture=" + seatingFixture +
            " seatingFive=" + seating5 +
            " financeRollback=" + financeRollback +
            " seatingAttempts=" + seatingAttempts +
            " seatingFailure=" + seatingFailure +
            " remove=" + removed +
            " corruptSafe=" + corruptSafe + " items=" + initialItems +
            " costCents=" + cents +
            " source=" + source.ItemDefinition.ItemId +
            " category=" + source.ItemDefinition.Category +
            " placementError=" + placeError);
    }
}
