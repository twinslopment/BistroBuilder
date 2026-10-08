using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class BistroBuilderB10ProbeCommand : IRestaurantEditHistoryCommand
{
    public RestaurantEditHistoryCommandType CommandType { get; }
    public string Description => "B10 probe";
    public Object PrimaryTarget => null;
    public bool IsValid => true;
    public bool FailUndo { get; set; }
    public bool FailRedo { get; set; }
    public bool Active { get; private set; }
    public int Releases { get; private set; }

    public BistroBuilderB10ProbeCommand(
        RestaurantEditHistoryCommandType type, bool active)
    {
        CommandType = type;
        Active = active;
    }

    public bool TryUndo(out RestaurantEditHistoryCommandResult result)
    {
        if (FailUndo)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandUnavailable,
                null, null, default, "Injected failure.");
            return false;
        }
        Active = CommandType == RestaurantEditHistoryCommandType.Delete;
        result = RestaurantEditHistoryCommandResult.Success(null, null);
        return true;
    }

    public bool TryRedo(out RestaurantEditHistoryCommandResult result)
    {
        if (FailRedo)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandUnavailable,
                null, null, default, "Injected failure.");
            return false;
        }
        Active = CommandType == RestaurantEditHistoryCommandType.Create;
        result = RestaurantEditHistoryCommandResult.Success(null, null);
        return true;
    }

    public void ReleaseResources() { Releases++; }
}

public static class BistroBuilderEditorV2B10ReplacementSelfTest
{
    private static readonly List<string> Lines = new List<string>(128);
    private static int pass;
    private static int fail;
    private const string Scene = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string Report = "EditorV2_B10_Replacement_Report.txt";

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B10 Replacement Self Test")]
    public static void RunFromMenu() { Run(); }

    private static bool Run()
    {
        Lines.Clear(); pass = 0; fail = 0;
        Lines.Add("EDITOR V2 B10 - REPLACEMENT SELF TEST");
        try
        {
            TestHistory();
            TestRealScene();
        }
        catch (Exception ex)
        {
            Fail("Excepción: " + ex);
        }
        finally
        {
            Lines.Add("Resultado: " + pass + " OK / " + fail + " fallos.");
            File.WriteAllLines(Path.Combine(
                Directory.GetCurrentDirectory(), Report), Lines);
            for (int i = 0; i < Lines.Count; i++)
                Debug.Log("[B10] " + Lines[i]);
        }
        return fail == 0;
    }

    private static void TestHistory()
    {
        var old1 = new BistroBuilderB10ProbeCommand(
            RestaurantEditHistoryCommandType.Delete, false);
        var new1 = new BistroBuilderB10ProbeCommand(
            RestaurantEditHistoryCommandType.Create, true);
        var old2 = new BistroBuilderB10ProbeCommand(
            RestaurantEditHistoryCommandType.Delete, false);
        var new2 = new BistroBuilderB10ProbeCommand(
            RestaurantEditHistoryCommandType.Create, true);
        var cmd = new BistroBuilderEditorV2ReplaceHistoryCommand(
            new IRestaurantEditHistoryCommand[] { old1, old2 },
            new IRestaurantEditHistoryCommand[] { new1, new2 });
        Check(cmd.IsValid &&
              cmd.CommandType == RestaurantEditHistoryCommandType.Replace &&
              cmd.ChildCommands.Count == 4,
            "comando B10 admite operación mixta con un único historial");

        Check(cmd.TryUndo(out _) &&
              old1.Active && old2.Active && !new1.Active && !new2.Active,
            "Undo retira nuevos antes de restaurar los originales");
        Check(cmd.TryRedo(out _) &&
              !old1.Active && !old2.Active && new1.Active && new2.Active,
            "Redo retira originales antes de reponer sustitutos");

        old2.FailUndo = true;
        Check(!cmd.TryUndo(out _) &&
              !old1.Active && !old2.Active && new1.Active && new2.Active,
            "fault injection Undo conserva estado completo");
        old2.FailUndo = false;

        new2.FailRedo = true;
        Check(cmd.TryUndo(out _) && !cmd.TryRedo(out _) &&
              old1.Active && old2.Active && !new1.Active && !new2.Active,
            "fault injection Redo conserva estado completo");
        new2.FailRedo = false;

        Check(cmd.TryRedo(out _), "historial permite repetir tras error");
        cmd.ReleaseResources();
        Check(old1.Releases == 1 && old2.Releases == 1 &&
              new1.Releases == 1 && new2.Releases == 1,
            "comando libera cada recurso exactamente una vez");
    }

    private static void TestRealScene()
    {
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        Bootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
        Bootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));

        var registry = Find<RestaurantPlaceableRegistry>();
        var transaction = Find<RestaurantPlacementTransactionService>();
        var history = Find<RestaurantPlacementHistoryService>();
        var creation = Find<RestaurantPlaceableCreationService>();
        var deletion = Find<RestaurantPlaceableDeletionService>();
        var edit = Find<RestaurantEditModeService>();
        var finance = Find<BistroBuilderPlaceableFinanceBridge>();
        var global = Find<BistroBuilderEditorV2GlobalHistory>();

        Check(registry != null && creation != null && history != null &&
              transaction != null && edit != null && global != null,
            "escena real contiene autoridades B10");
        if (registry == null || creation == null || history == null ||
            transaction == null || edit == null || global == null) return;

        MethodInfo initializer =
            typeof(BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest)
                .GetMethod("InitializeRuntimeAuthorities",
                    BindingFlags.NonPublic | BindingFlags.Static);
        initializer.Invoke(null, new object[] {
            registry, transaction, history, creation, deletion
        });
        var financeAuthority = Find<BistroBuilderFinanceService>();
        Check(financeAuthority != null &&
              (financeAuthority.IsInitialized ||
               financeAuthority.TryInitializeFresh(out _)),
            "finanzas reales inicializadas para B10");

        var purchaseOrder = Find<BistroBuilderSupplierPurchaseOrderService>();
        if (purchaseOrder == null)
        {
            var host = new GameObject("__B10_RealPurchaseOrderFixture");
            purchaseOrder = host.AddComponent<BistroBuilderSupplierPurchaseOrderService>();
        }
        FieldInfo purchaseInstance =
            typeof(BistroBuilderSupplierPurchaseOrderService).GetField(
                "instance", BindingFlags.Static | BindingFlags.NonPublic);
        purchaseInstance?.SetValue(null, purchaseOrder);
        Check(purchaseOrder != null &&
              (purchaseOrder.IsInitialized ||
               purchaseOrder.TryInitializeFresh()),
            "purchase order 2.3E inicializado sin simuladores");
        Invoke(Find<BistroBuilderSupplierPurchaseFinanceBridge>(), "OnEnable");

        if (finance != null)
            Invoke(finance, "OnEnable");
        Check(finance != null && finance.IsBound,
            "bridge de compra/retirada conectado a Finanzas");

        bool entered = edit.IsEditModeActive ||
            edit.TryEnterEditMode(out _, out _);
        Check(entered, "edición activa con reglas de servicio reales");
        if (!entered) return;

        MethodInfo finder =
            typeof(BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest)
                .GetMethod("FindMovablePlaceables",
                    BindingFlags.NonPublic | BindingFlags.Static);
        var sources = (List<RestaurantPlaceableObject>)finder.Invoke(
            null, new object[] { registry });
        Check(sources.Count > 1, "escena contiene artículos editables reales");
        if (sources.Count == 0) return;

        RestaurantPlaceableObject first = sources[0];
        int baselineHistory = history.UndoCount;
        RestaurantPlaceableItemDefinition wrong = null;
        string[] ids = AssetDatabase.FindAssets(
            "t:RestaurantPlaceableItemDefinition");
        var definitions = new List<RestaurantPlaceableItemDefinition>(ids.Length);
        foreach (string guid in ids)
        {
            var definition = AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableItemDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.HasValidPrefab)
            {
                definitions.Add(definition);
                if (definition.Category != first.ItemDefinition.Category)
                    wrong = definition;
            }
        }

        if (wrong != null)
        {
            Check(!creation.TryReplaceBatch(
                      new[] { first }, wrong, out _,
                      out RestaurantPlaceableReplacementResult rejected) &&
                  !rejected.Succeeded && registry.ContainsPlaceable(first) &&
                  history.UndoCount == baselineHistory,
                "sustitución de categoría incompatible no muta mundo ni historial");
        }

        definitions.Sort((a, b) => b.PurchasePriceCents.CompareTo(a.PurchasePriceCents));
        long initialBalance = financeAuthority.CurrentBalanceCents;
        bool succeeded = false;
        RestaurantPlaceableObject original = null;
        RestaurantPlaceableObject replacement = null;
        RestaurantPlaceableItemDefinition appliedDefinition = null;
        RestaurantPlacementStateSnapshot before = default;
        long quote = 0L;
        int rejectedAttempts = 0;
        int totalAttempts = 0;
        var rejectedReasons = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int s = 0; s < sources.Count && s < 12 && !succeeded; s++)
        {
            RestaurantPlaceableObject source = sources[s];
            if (source == null || !registry.ContainsPlaceable(source))
                continue;
            for (int t = 0; t < definitions.Count && t < 40 && !succeeded; t++)
            {
                RestaurantPlaceableItemDefinition target = definitions[t];
                if (source.ItemDefinition == target ||
                    source.ItemDefinition.Category != target.Category)
                    continue;

                totalAttempts++;
                source.TryGetComponent(out RestaurantAreaMember member);
                before = RestaurantPlacementStateSnapshot.Capture(member);
                bool quoteOk = creation.TryQuoteReplacement(
                    new[] { source }, target, out quote, out _);
                bool executed = creation.TryReplaceBatch(
                    new[] { source }, target,
                    out IReadOnlyList<RestaurantPlaceableObject> created,
                    out RestaurantPlaceableReplacementResult result);
                if (executed && result.Succeeded && created.Count == 1)
                {
                    original = source;
                    replacement = created[0];
                    appliedDefinition = target;
                    succeeded = true;
                    Lines.Add("METRIC - APPLIED_SOURCE=" + source.ItemDefinition.ItemId);
                    Lines.Add("METRIC - APPLIED_TARGET=" + target.ItemId);
                    Lines.Add("METRIC - PURCHASE_CENTS=" + target.PurchasePriceCents);
                    Lines.Add("METRIC - QUOTE_AVAILABLE=" + quoteOk);
                    Lines.Add("METRIC - NET_COST_CENTS=" + quote);
                }
                else
                {
                    rejectedAttempts++;
                    string reason = result.Message ?? string.Empty;
                    if (rejectedReasons.TryGetValue(reason, out int count))
                        rejectedReasons[reason] = count + 1;
                    else
                        rejectedReasons.Add(reason, 1);
                    if (!registry.ContainsPlaceable(source) ||
                        history.UndoCount != baselineHistory ||
                        !before.IsValid)
                    {
                        Fail("rechazo alteró registro o historial en intento " +
                            totalAttempts + ": " + result.Message);
                        return;
                    }
                }
            }
        }

        Lines.Add("METRIC - TRIED=" + totalAttempts);
        Lines.Add("METRIC - REJECTED=" + rejectedAttempts);
        foreach (var reason in rejectedReasons)
            Lines.Add("REJECTION - " + reason.Value + " x " + reason.Key);
        Check(succeeded, "sustitución válida realmente ejecutada en escena");
        if (!succeeded) return;

        Check(history.UndoCount == baselineHistory + 1 &&
              history.PeekUndoCommand().CommandType ==
                  RestaurantEditHistoryCommandType.Replace,
            "sustitución real registra un solo comando global");
        Check(!registry.ContainsPlaceable(original) &&
              registry.ContainsPlaceable(replacement) &&
              !string.Equals(original.InstanceId,
                  replacement.InstanceId, StringComparison.Ordinal),
            "retirada y compra publican identidades sin colisión");
        before.GetWorldPose(out Vector3 expectedPos, out _);
        Check(Vector3.Distance(
                  original.PlacementAnchor.position,
                  replacement.PlacementAnchor.position) < 0.002f,
            "anclaje del sustituto coincide con el anterior");
        long afterReplaceBalance = financeAuthority.CurrentBalanceCents;
        Check(afterReplaceBalance == initialBalance - quote,
            "efecto financiero real coincide con presupuesto neto");
        var structure = Find<RestaurantStructureSaveSectionProvider>();
        if (structure != null)
        {
            bool captureOk = TryCaptureStructure(
                structure, out RestaurantStructureSaveData saved,
                out string captureError);
            bool containsNew = false;
            bool containsOld = false;
            if (saved != null)
            {
                foreach (RestaurantPlaceableSaveRecord record in saved.placeables)
                {
                    containsNew |= record.instanceId == replacement.InstanceId &&
                                   record.itemId == appliedDefinition.ItemId;
                    containsOld |= record.instanceId == original.InstanceId;
                }
            }
            Check(captureOk && containsNew && !containsOld,
                "Save estructura captura reemplazo con ID y definición nuevos: " +
                captureError);
            if (captureOk)
            {
                string json = JsonUtility.ToJson(saved);
                var restored = JsonUtility.FromJson<RestaurantStructureSaveData>(json);
                Check(restored != null &&
                      restored.placeables.Count == saved.placeables.Count &&
                      restored.placeables.Exists(x =>
                          x.instanceId == replacement.InstanceId &&
                          x.itemId == appliedDefinition.ItemId),
                    "serialización Save JSON conserva identidad del reemplazo");
            }
        }
        Check(global.TryUndo(out _) &&
              registry.ContainsPlaceable(original) &&
              !registry.ContainsPlaceable(replacement),
            "Undo global restaura original en escena");
        Check(financeAuthority.CurrentBalanceCents == initialBalance,
            "Undo real restituye saldo financiero exacto");
        Check(global.TryRedo(out _) &&
              !registry.ContainsPlaceable(original) &&
              registry.ContainsPlaceable(replacement),
            "Redo global aplica sustituto en escena");
        Check(financeAuthority.CurrentBalanceCents == afterReplaceBalance,
            "Redo real reproduce saldo financiero");
        Check(global.TryUndo(out _) &&
              registry.ContainsPlaceable(original) &&
              financeAuthority.CurrentBalanceCents == initialBalance,
            "Undo final recupera escena y dinero originales");
        TestMultiReplacement(
            sources, definitions, creation, registry, history,
            global, financeAuthority);
        TestPaidReplacement(
            sources, definitions, creation, registry, history,
            global, financeAuthority);
    }

    private static void TestPaidReplacement(
        List<RestaurantPlaceableObject> sources,
        List<RestaurantPlaceableItemDefinition> definitions,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableRegistry registry,
        RestaurantPlacementHistoryService history,
        BistroBuilderEditorV2GlobalHistory global,
        BistroBuilderFinanceService finance)
    {
        RestaurantPlaceableObject source = sources.Find(x =>
            x != null && registry.ContainsPlaceable(x) &&
            x.ItemDefinition.ItemId == "table_basic");
        RestaurantPlaceableItemDefinition template =
            definitions.Find(x => x != null && x.ItemId == "table_basic_4");
        if (source == null || template == null)
        {
            Fail("No hay fixture real de mesa para sustitución con coste.");
            return;
        }

        // Temporary in-memory test definition: no source asset is edited.
        RestaurantPlaceableItemDefinition paid =
            UnityEngine.Object.Instantiate(template);
        paid.name = "__B10_PaidTemporary_250EUR";
        FieldInfo priceField = typeof(RestaurantPlaceableItemDefinition)
            .GetField("purchasePrice",
                BindingFlags.NonPublic | BindingFlags.Instance);
        priceField.SetValue(paid, 250);

        long initialCash = finance.CurrentBalanceCents;
        bool quoteOk = creation.TryQuoteReplacement(
            new[] { source }, paid, out long purchaseQuote,
            out string quoteError);
        bool firstExecuted = creation.TryReplaceBatch(
            new[] { source }, paid,
            out IReadOnlyList<RestaurantPlaceableObject> firstCreated,
            out RestaurantPlaceableReplacementResult firstResult);

        Check(quoteOk && purchaseQuote == 25000L &&
              firstExecuted && firstCreated.Count == 1 &&
              finance.CurrentBalanceCents == initialCash - 25000L,
            "fixture temporal 250 euros compra real y descuenta exactamente 25000 céntimos: " +
            quoteError + " / " + firstResult.Message);

        if (!firstExecuted)
        {
            UnityEngine.Object.DestroyImmediate(paid);
            return;
        }

        RestaurantPlaceableObject purchased = firstCreated[0];
        bool disposalQuote = creation.TryQuoteReplacement(
            new[] { purchased }, source.ItemDefinition,
            out long resaleQuote, out string disposalError);

        bool secondExecuted = creation.TryReplaceBatch(
            new[] { purchased }, source.ItemDefinition,
            out IReadOnlyList<RestaurantPlaceableObject> secondCreated,
            out RestaurantPlaceableReplacementResult secondResult);
        Check(disposalQuote && resaleQuote == -12500L &&
              secondExecuted && secondCreated.Count == 1 &&
              finance.CurrentBalanceCents == initialCash - 12500L,
            "reventa 50 % recupera 12500 céntimos en operación encadenada: " +
            disposalError + " / " + secondResult.Message);

        if (secondExecuted)
        {
            Check(global.TryUndo(out _) &&
                  registry.ContainsPlaceable(purchased) &&
                  finance.CurrentBalanceCents == initialCash - 25000L,
                "Undo de reventa restaura artículo pagado y saldo");
            Check(global.TryUndo(out _) &&
                  registry.ContainsPlaceable(source) &&
                  finance.CurrentBalanceCents == initialCash,
                "Undo de compra devuelve caja inicial sin duplicar cobros");
            Check(global.TryRedo(out _) &&
                  registry.ContainsPlaceable(purchased) &&
                  finance.CurrentBalanceCents == initialCash - 25000L,
                "Redo de compra vuelve a cobrar exactamente el precio");
            Check(global.TryRedo(out _) &&
                  registry.ContainsPlaceable(secondCreated[0]) &&
                  finance.CurrentBalanceCents == initialCash - 12500L,
                "Redo de reventa vuelve a abonar 50 %");
            Check(global.TryUndo(out _) &&
                  global.TryUndo(out _) &&
                  registry.ContainsPlaceable(source) &&
                  finance.CurrentBalanceCents == initialCash,
                "recuperación final tras seis inversiones conserva mundo y caja");
        }
        else
        {
            global.TryUndo(out _);
        }

        // No retained history object may refer to a destroyed test definition.
        history.ClearHistory();
        global.ClearGlobalOrdering();
        UnityEngine.Object.DestroyImmediate(paid);
    }

    private static bool TryCaptureStructure(
        RestaurantStructureSaveSectionProvider provider,
        out RestaurantStructureSaveData data,
        out string error)
    {
        data = null;
        error = string.Empty;
        Invoke(provider, "Awake");
        if (!provider.ValidateConfiguration(out error))
            return false;

        var context = new BistroBuilderSaveCaptureContext(100);
        IEnumerator capture = provider.CaptureState(context);
        int steps = 0;
        while (capture.MoveNext())
        {
            if (++steps > 100000)
            {
                error = "La captura de estructura superó el límite de iteraciones.";
                return false;
            }
        }

        if (context.HasFailed)
        {
            error = context.ErrorMessage;
            return false;
        }
        data = context.State as RestaurantStructureSaveData;
        if (data == null)
        {
            error = "El proveedor no devolvió una estructura persistente.";
            return false;
        }
        return true;
    }

    private static void TestMultiReplacement(
        List<RestaurantPlaceableObject> sources,
        List<RestaurantPlaceableItemDefinition> definitions,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableRegistry registry,
        RestaurantPlacementHistoryService history,
        BistroBuilderEditorV2GlobalHistory global,
        BistroBuilderFinanceService finance)
    {
        int attempts = 0;
        int failedAttempts = 0;
        int beforeUndoCount = history.UndoCount;
        long initialCash = finance.CurrentBalanceCents;
        bool success = false;
        RestaurantPlaceableObject[] originals = null;
        IReadOnlyList<RestaurantPlaceableObject> newObjects = null;
        long netCost = 0L;
        string lastRejection = string.Empty;

        for (int i = 0; i < sources.Count && i < 20 && !success; i++)
        {
            RestaurantPlaceableObject first = sources[i];
            if (first == null || !registry.ContainsPlaceable(first))
                continue;
            for (int j = i + 1; j < sources.Count && j < 24 && !success; j++)
            {
                RestaurantPlaceableObject second = sources[j];
                if (second == null || !registry.ContainsPlaceable(second) ||
                    first.ItemDefinition.Category != second.ItemDefinition.Category ||
                    first.transform.IsChildOf(second.transform) ||
                    second.transform.IsChildOf(first.transform))
                    continue;

                for (int t = 0; t < definitions.Count && !success; t++)
                {
                    RestaurantPlaceableItemDefinition target = definitions[t];
                    if (target.Category != first.ItemDefinition.Category ||
                        target == first.ItemDefinition ||
                        target == second.ItemDefinition)
                        continue;

                    attempts++;
                    originals = new[] { first, second };
                    bool quote = creation.TryQuoteReplacement(
                        originals, target, out netCost, out _);
                    bool worked = creation.TryReplaceBatch(
                        originals, target, out newObjects,
                        out RestaurantPlaceableReplacementResult result);
                    if (worked && result.Succeeded && newObjects.Count == 2)
                    {
                        success = true;
                        Check(quote, "presupuesto económico multiartículo disponible");
                        break;
                    }

                    failedAttempts++;
                    lastRejection = result.Message;
                    if (!registry.ContainsPlaceable(first) ||
                        !registry.ContainsPlaceable(second) ||
                        history.UndoCount != beforeUndoCount ||
                        finance.CurrentBalanceCents != initialCash)
                    {
                        Fail("falló rollback de sustitución grupal: " +
                             lastRejection);
                        return;
                    }

                    if (attempts >= 120)
                        break;
                }
                if (attempts >= 120)
                    break;
            }
        }

        Lines.Add("METRIC - MULTI_ATTEMPTS=" + attempts);
        Lines.Add("METRIC - MULTI_REJECTED=" + failedAttempts);
        if (!success)
            Lines.Add("MULTI_LAST_REJECTION - " + lastRejection);

        Check(success, "dos artículos reales sustituidos simultáneamente");
        if (!success) return;

        Check(history.UndoCount == beforeUndoCount + 1 &&
              history.PeekUndoCommand().CommandType ==
                  RestaurantEditHistoryCommandType.Replace,
            "B10 multiselección conserva una única operación");
        bool afterState = !registry.ContainsPlaceable(originals[0]) &&
                          !registry.ContainsPlaceable(originals[1]) &&
                          registry.ContainsPlaceable(newObjects[0]) &&
                          registry.ContainsPlaceable(newObjects[1]) &&
                          finance.CurrentBalanceCents == initialCash - netCost;
        Check(afterState, "B10 grupo confirma registro y dinero exactos");
        history.CommandRejected += (command, rejection) =>
            Lines.Add("MULTI_COMMAND_REJECT - " + rejection.Message);
        Lines.Add("MULTI_INSTANCE_IDS - " + originals[0].InstanceId + "," +
            originals[1].InstanceId + "," +
            newObjects[0].InstanceId + "," + newObjects[1].InstanceId);
        var peek = history.PeekUndoCommand();
        Lines.Add("MULTI_HISTORY_VALID=" + peek.IsValid);
        if (peek is IRestaurantEditHistoryCommandGroup diagnosticGroup)
            for (int k = 0; k < diagnosticGroup.ChildCommands.Count; k++)
                Lines.Add("MULTI_CHILD_" + k + " " +
                    diagnosticGroup.ChildCommands[k].CommandType + " valid=" +
                    diagnosticGroup.ChildCommands[k].IsValid + " targetNull=" +
                    (diagnosticGroup.ChildCommands[k].PrimaryTarget == null));
        bool undoOk = global.TryUndo(out string undoError);
        if (!undoOk)
        {
            Lines.Add("MULTI_UNDO_ERROR - " + undoError);
            Lines.Add("MULTI_UNDO_STATE - globalUndo=" + global.UndoCount +
                ", placementUndo=" + history.UndoCount +
                ", initialCash=" + initialCash +
                ", actualCash=" + finance.CurrentBalanceCents);
        }
        Check(undoOk &&
              registry.ContainsPlaceable(originals[0]) &&
              registry.ContainsPlaceable(originals[1]) &&
              !registry.ContainsPlaceable(newObjects[0]) &&
              !registry.ContainsPlaceable(newObjects[1]) &&
              finance.CurrentBalanceCents == initialCash,
            "B10 Undo global grupal revierte dos artículos y finanzas");
        if (!undoOk) return;
        bool redoOk = global.TryRedo(out string redoError);
        if (!redoOk) Lines.Add("MULTI_REDO_ERROR - " + redoError);
        Check(redoOk &&
              !registry.ContainsPlaceable(originals[0]) &&
              !registry.ContainsPlaceable(originals[1]) &&
              registry.ContainsPlaceable(newObjects[0]) &&
              registry.ContainsPlaceable(newObjects[1]) &&
              finance.CurrentBalanceCents == initialCash - netCost,
            "B10 Redo global grupal restaura dos artículos y dinero");
        if (!redoOk) return;
        Check(global.TryUndo(out _) &&
              registry.ContainsPlaceable(originals[0]) &&
              registry.ContainsPlaceable(originals[1]) &&
              finance.CurrentBalanceCents == initialCash,
            "B10 Undo final multiselección restaura saldo");
    }

    private static void Bootstrap(Type type)
    {
        MethodInfo method = type.GetMethod("Install",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Invoke(null, null);
    }

    private static T Find<T>() where T : Object =>
        Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    private static void Invoke(object target, string method)
    {
        target?.GetType().GetMethod(method,
            BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, null);
    }

    private static void Check(bool condition, string label)
    {
        if (condition) { pass++; Lines.Add("OK - " + label); }
        else Fail(label);
    }

    private static void Fail(string text)
    {
        fail++; Lines.Add("FAIL - " + text);
    }
}
