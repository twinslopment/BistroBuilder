using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BistroBuilderEditorV2B5RenovationSelfTest
{
    private static readonly List<string> Lines = new List<string>(128);
    private static int pass;
    private static int fail;

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B5 Renovation Self Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static bool Run()
    {
        Lines.Clear();
        pass = 0;
        fail = 0;
        Lines.Add("EDITOR V2 - B5 TRANSACTIONAL RENOVATION SELF TEST");

        try
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Prototype_Restaurant.unity",
                OpenSceneMode.Single);

            InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
            InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));

            var editMode = Find<RestaurantEditModeService>();
            var editor = Find<BistroBuilderEditorV2Coordinator>();
            var renovation = Find<BistroBuilderEditorV2RenovationSession>();
            var global = Find<BistroBuilderEditorV2GlobalHistory>();
            var placementHistory = Find<RestaurantPlacementHistoryService>();
            var transaction = Find<RestaurantPlacementTransactionService>();
            var creation = Find<RestaurantPlaceableCreationService>();
            var deletion = Find<RestaurantPlaceableDeletionService>();
            var registry = Find<RestaurantPlaceableRegistry>();
            var architecture = Find<BistroBuilderEditRuntimeCoordinator>();
            var document = Find<BistroBuilderEditDocumentRuntimeService>();
            var finance = Find<BistroBuilderFinanceService>();
            var financeBridge = Find<BistroBuilderPlaceableFinanceBridge>();
            var editFinance = Find<BistroBuilderEditFinanceGateway>();

            bool deps = editMode != null && editor != null && renovation != null &&
                        global != null && placementHistory != null &&
                        transaction != null && creation != null && deletion != null &&
                        registry != null && architecture != null && document != null &&
                        finance != null && financeBridge != null && editFinance != null;
            Check(deps, "B5 dispone de todas las autoridades reales necesarias");
            if (!deps)
                return Finish();

            renovation.enabled = false;
            InvokeLifecycle(renovation, "OnDisable");
            InitializeRuntimeAuthorities(
                registry, transaction, placementHistory, creation, deletion);

            Check(finance.TryInitializeFresh(out string financeInitError),
                "B5 inicializa Finanzas real" +
                Suffix(financeInitError));

            BistroBuilderSupplierPurchaseOrderService purchaseOrder =
                Find<BistroBuilderSupplierPurchaseOrderService>();
            if (purchaseOrder == null)
            {
                GameObject host = new GameObject("__B5_PurchaseOrderAuthority");
                purchaseOrder =
                    host.AddComponent<BistroBuilderSupplierPurchaseOrderService>();
            }

            FieldInfo instanceField =
                typeof(BistroBuilderSupplierPurchaseOrderService).GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, purchaseOrder);

            bool purchaseReady =
                purchaseOrder != null &&
                purchaseOrder.TryInitializeFresh() &&
                purchaseOrder.IsInitialized;
            Check(purchaseReady, "B5 inicializa PurchaseOrder 2.3E");

            InvokeLifecycle(Find<BistroBuilderSupplierPurchaseFinanceBridge>(), "OnEnable");
            InvokeLifecycle(financeBridge, "OnEnable");
            BistroBuilderEditFinanceGatewayBinder editFinanceBinder =
                Find<BistroBuilderEditFinanceGatewayBinder>();
            bool editFinanceBound =
                editFinanceBinder != null && editFinanceBinder.TryBind();
            Check(editFinanceBound,
                "B5 enlaza la autoridad financiera de reformas" +
                Suffix(editFinanceBinder != null ? editFinanceBinder.LastError : "binder ausente"));
            Check(financeBridge.IsBound, "B5 mantiene conectado el bridge financiero de colocables");

            bool entered = editMode.IsEditModeActive ||
                           editMode.TryEnterEditMode(out _, out _);
            Check(entered, "B5 entra en modo edición real");
            if (!entered)
                return Finish();

            RestaurantPlaceableItemDefinition pricedDefinition =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                    "Assets/Data/Restaurant/EditMode/PlaceableItems/" +
                    "PlaceableItemDefinition_FactoryTestPlant.asset");
            Check(pricedDefinition != null &&
                  pricedDefinition.HasValidPrefab &&
                  pricedDefinition.PurchasePriceCents > 0L,
                "B5 dispone de fixture canónico con coste real");

            RestaurantPlaceableObject pricedProbe = null;
            string probeError = string.Empty;
            bool probeCreated =
                pricedDefinition != null &&
                InvokeB4TryCreatePricedProbe(
                    creation,
                    transaction,
                    registry,
                    pricedDefinition,
                    out pricedProbe,
                    out probeError);
            Check(probeCreated,
                "B5 prepara el fixture económico previo a la baseline" +
                Suffix(probeError));
            if (!probeCreated)
                return Finish();

            placementHistory.ClearHistory();
            global.ClearGlobalOrdering();

            renovation.Configure(
                editMode,
                editor,
                global,
                placementHistory,
                architecture,
                finance);
            bool began = renovation.TryBeginSession(out string beginError);
            renovation.enabled = true;
            renovation.TryGetSnapshot(
                out BistroBuilderEditorV2RenovationSnapshot initialRenovation,
                out string initialSnapshotError);
            Check(began && renovation.IsActive && !renovation.HasPendingChanges,
                "B5 abre una baseline común limpia" + Suffix(beginError) +
                " [global=" + global.UndoCount +
                ", placement=" + placementHistory.UndoCount +
                ", archDirty=" + architecture.IsDirty +
                ", baseTx=" + initialRenovation.baselineTransactionCount +
                ", currentTx=" + initialRenovation.currentTransactionCount +
                ", snapshot=" + initialSnapshotError + "]");
            if (!began)
                return Finish();

            // -------------------------------------------------------------
            // GATE 1: reforma grande + Discard exacto.
            // -------------------------------------------------------------
            string discardArchBaseline =
                document.GetCommittedSnapshot().ComputeFingerprint();
            string discardWorldBaseline = WorldFingerprint(registry);
            string discardSeatBaseline = SeatingFingerprint();
            string discardFinanceBaseline = FinanceFingerprint(finance);
            long discardBalanceBaseline = finance.CurrentBalanceCents;
            int discardTransactionBaseline = finance.TransactionCount;

            bool mixedDiscard = ExecuteFiveStepRenovation(
                "b5_discard",
                transaction,
                creation,
                deletion,
                registry,
                architecture,
                pricedProbe,
                out string mixedDiscardError);
            Check(mixedDiscard,
                "B5 ejecuta reforma grande mezclando mobiliario, arquitectura y economía" +
                Suffix(mixedDiscardError));
            if (!mixedDiscard)
                return Finish();

            Check(global.UndoCount == 5,
                "B5 registra las 5 operaciones en una única cronología");
            Check(renovation.HasPendingChanges,
                "B5 marca la reforma como pendiente");

            bool quoted = renovation.TryGetSnapshot(
                out BistroBuilderEditorV2RenovationSnapshot pendingSnapshot,
                out string quoteError);
            Check(quoted &&
                  pendingSnapshot.operationCount == 5 &&
                  pendingSnapshot.totalSignedCostCents != 0L,
                "B5 resume operaciones y coste total real" + Suffix(quoteError));

            bool exitBlocked =
                !editMode.TryExitEditMode(
                    true,
                    out RestaurantEditModeFailureReason exitReason) &&
                exitReason == RestaurantEditModeFailureReason.BlockedByExitGuard &&
                !string.IsNullOrWhiteSpace(editMode.LastExitRejectionMessage);
            Check(exitBlocked,
                "B5 impide salir con cambios pendientes sin Aplicar/Descartar");

            bool discarded = renovation.TryDiscardChanges(out string discardError);
            Check(discarded,
                "B5 Descartar completa la reversión transaccional" +
                Suffix(discardError));
            if (!discarded)
                return Finish();

            Check(
                string.Equals(
                    document.GetCommittedSnapshot().ComputeFingerprint(),
                    discardArchBaseline,
                    StringComparison.Ordinal),
                "B5 Discard conserva exactamente la estructura canónica inicial");
            Check(
                architecture.HasSession &&
                architecture.Session.State == BistroBuilderEditSessionState.ActiveClean &&
                architecture.Session.DraftRevision == architecture.Session.BaselineRevision &&
                architecture.Session.UndoCount == 0 &&
                architecture.Session.RedoCount == 0,
                "B5 Discard reabre una sesión arquitectónica limpia sobre la baseline canónica");
            Check(
                string.Equals(
                    WorldFingerprint(registry),
                    discardWorldBaseline,
                    StringComparison.Ordinal),
                "B5 Discard restaura exactamente mobiliario, poses, jerarquía y áreas");
            Check(
                string.Equals(
                    SeatingFingerprint(),
                    discardSeatBaseline,
                    StringComparison.Ordinal),
                "B5 Discard restaura exactamente relaciones mesa-silla");
            Check(
                finance.CurrentBalanceCents == discardBalanceBaseline &&
                finance.TransactionCount == discardTransactionBaseline &&
                string.Equals(
                    FinanceFingerprint(finance),
                    discardFinanceBaseline,
                    StringComparison.Ordinal),
                "B5 Discard restaura exactamente Caja y ledger, sin asientos residuales");
            Check(UniqueIds(),
                "B5 Discard no deja IDs persistentes duplicados");
            Check(NoArchitectureOrphans(architecture.Session.Draft),
                "B5 Discard no deja referencias arquitectónicas huérfanas");
            Check(global.UndoCount == 0 && global.RedoCount == 0 &&
                  placementHistory.UndoCount == 0 && placementHistory.RedoCount == 0,
                "B5 Discard limpia por completo ambos historiales");
            Check(renovation.IsActive && !renovation.HasPendingChanges &&
                  renovation.CanExitEditMode(out _),
                "B5 queda limpio y permite salir después de Descartar");

            // -------------------------------------------------------------
            // GATE 2: Apply publica una sola versión coherente.
            // -------------------------------------------------------------
            BistroBuilderEditDocument applyBaselineDocument =
                document.GetCommittedSnapshot();
            long applyBaselineRevision = applyBaselineDocument.revision;
            int applyFinanceTransactions = finance.TransactionCount;
            string applyWorldBaseline = WorldFingerprint(registry);

            RestaurantPlaceableObject movedChair;
            bool moved = InvokeB4TryMoveAnyChair(
                transaction,
                registry,
                out movedChair,
                out string applyMoveError);
            Check(moved,
                "B5 Apply: mueve mobiliario real" + Suffix(applyMoveError));

            BistroBuilderEditId applyWallId =
                new BistroBuilderEditId("b5_apply_wall");
            bool applyWallCreated = architecture.TryExecute(
                BuildWall(applyWallId, 130f),
                out _,
                out string applyWallError);
            Check(applyWallCreated,
                "B5 Apply: añade arquitectura al Draft" +
                Suffix(applyWallError));

            RestaurantPlaceableObject targetTable = InvokeB4FindTable(registry);
            RestaurantPlaceableObject duplicateSource = null;
            RestaurantPlaceableObject duplicate = null;
            string duplicateError = string.Empty;
            bool duplicated =
                targetTable != null &&
                InvokeB4TryDuplicate(
                    creation,
                    transaction,
                    registry,
                    pricedProbe,
                    targetTable.transform.position,
                    movedChair != null
                        ? movedChair.transform.parent
                        : targetTable.transform.parent,
                    out duplicateSource,
                    out duplicate,
                    out duplicateError);
            Check(duplicated,
                "B5 Apply: duplica un colocable con coste real" +
                Suffix(duplicateError));

            Check(moved && applyWallCreated && duplicated &&
                  renovation.HasPendingChanges,
                "B5 Apply: la reforma mixta queda pendiente como una unidad");

            BistroBuilderEditDocument expectedApplied =
                architecture.Session.Draft.DeepClone();
            expectedApplied.revision =
                architecture.Session.BaselineRevision + 1;
            string expectedAppliedFingerprint =
                expectedApplied.ComputeFingerprint();

            bool applied = renovation.TryApplyChanges(out string applyError);
            Check(applied,
                "B5 Aplicar completa el commit común" + Suffix(applyError));
            if (!applied)
                return Finish();

            BistroBuilderEditDocument appliedDocument =
                document.GetCommittedSnapshot();
            Check(appliedDocument.revision == applyBaselineRevision + 1,
                "B5 Apply publica exactamente una nueva revisión canónica");
            Check(
                string.Equals(
                    appliedDocument.ComputeFingerprint(),
                    expectedAppliedFingerprint,
                    StringComparison.Ordinal) &&
                appliedDocument.FindWall(applyWallId) != null,
                "B5 Apply publica exactamente el Draft arquitectónico esperado");
            Check(
                !string.Equals(
                    WorldFingerprint(registry),
                    applyWorldBaseline,
                    StringComparison.Ordinal) &&
                duplicate != null &&
                duplicate.gameObject.activeInHierarchy,
                "B5 Apply conserva el mobiliario confirmado");
            Check(finance.TransactionCount > applyFinanceTransactions,
                "B5 Apply conserva los movimientos económicos confirmados");
            Check(renovation.IsActive && !renovation.HasPendingChanges,
                "B5 Apply rebasa una nueva sesión limpia");
            Check(global.UndoCount == 0 && global.RedoCount == 0 &&
                  placementHistory.UndoCount == 0 && placementHistory.RedoCount == 0,
                "B5 Apply cierra el historial de la reforma anterior");
            Check(renovation.CanExitEditMode(out _),
                "B5 permite salir después de Aplicar");
            Check(UniqueIds(),
                "B5 Apply mantiene IDs persistentes únicos");

            // -------------------------------------------------------------
            // GATE 3: fallo de commit recuperable y sin corrupción.
            // -------------------------------------------------------------
            string failureArchBaseline =
                document.GetCommittedSnapshot().ComputeFingerprint();
            string failureWorldBaseline = WorldFingerprint(registry);
            string failureSeatBaseline = SeatingFingerprint();
            string failureFinanceBaseline = FinanceFingerprint(finance);

            BistroBuilderEditId failureWallId =
                new BistroBuilderEditId("b5_failure_wall");
            bool failureDraftReady = architecture.TryExecute(
                BuildWall(failureWallId, 150f),
                out _,
                out string failureDraftError);
            Check(failureDraftReady,
                "B5 Recovery: prepara un Draft pendiente" +
                Suffix(failureDraftError));

            bool unbound = architecture.UnbindEconomyGateway(editFinance);
            Check(unbound,
                "B5 Recovery: simula pérdida controlada de la autoridad económica");

            bool rejected = !renovation.TryApplyChanges(out string expectedFailure);
            Check(rejected && renovation.HasPendingChanges,
                "B5 Recovery: el commit defectuoso se rechaza y conserva un Draft recuperable" +
                Suffix(expectedFailure));

            bool rebound =
                architecture.TryBindEconomyGateway(
                    editFinance,
                    out string rebindError);
            Check(rebound,
                "B5 Recovery: restaura la autoridad económica" +
                Suffix(rebindError));

            Check(
                string.Equals(
                    document.GetCommittedSnapshot().ComputeFingerprint(),
                    failureArchBaseline,
                    StringComparison.Ordinal),
                "B5 Recovery: el fallo no publica arquitectura parcial");
            Check(
                string.Equals(
                    WorldFingerprint(registry),
                    failureWorldBaseline,
                    StringComparison.Ordinal) &&
                string.Equals(
                    SeatingFingerprint(),
                    failureSeatBaseline,
                    StringComparison.Ordinal),
                "B5 Recovery: el fallo no altera mobiliario ni relaciones");
            Check(
                string.Equals(
                    FinanceFingerprint(finance),
                    failureFinanceBaseline,
                    StringComparison.Ordinal),
                "B5 Recovery: el fallo no altera Caja ni ledger");
            Check(
                architecture.HasSession &&
                architecture.Session.Draft.FindWall(failureWallId) != null,
                "B5 Recovery: el Draft sigue disponible para corregir o descartar");

            bool recoveryDiscard =
                renovation.TryDiscardChanges(out string recoveryDiscardError);
            Check(recoveryDiscard,
                "B5 Recovery: Descartar recupera después del commit fallido" +
                Suffix(recoveryDiscardError));
            Check(
                string.Equals(
                    document.GetCommittedSnapshot().ComputeFingerprint(),
                    failureArchBaseline,
                    StringComparison.Ordinal) &&
                string.Equals(
                    WorldFingerprint(registry),
                    failureWorldBaseline,
                    StringComparison.Ordinal) &&
                string.Equals(
                    SeatingFingerprint(),
                    failureSeatBaseline,
                    StringComparison.Ordinal) &&
                string.Equals(
                    FinanceFingerprint(finance),
                    failureFinanceBaseline,
                    StringComparison.Ordinal),
                "B5 Recovery: estado completo vuelve exactamente a la baseline");
            Check(!renovation.HasPendingChanges &&
                  global.UndoCount == 0 &&
                  placementHistory.UndoCount == 0,
                "B5 Recovery: sesión final limpia y convergente");
        }
        catch (Exception ex)
        {
            fail++;
            Lines.Add("EXCEPTION - " + ex);
        }

        return Finish();
    }

    private static bool ExecuteFiveStepRenovation(
        string prefix,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion,
        RestaurantPlaceableRegistry registry,
        BistroBuilderEditRuntimeCoordinator architecture,
        RestaurantPlaceableObject pricedProbe,
        out string error)
    {
        error = string.Empty;

        if (!InvokeB4TryMoveAnyChair(
                transaction,
                registry,
                out RestaurantPlaceableObject chair,
                out error))
            return false;

        if (!architecture.TryExecute(
                BuildWall(new BistroBuilderEditId(prefix + "_wall"), 100f),
                out _,
                out error))
            return false;

        var surface = new BistroBuilderSurfaceFinishPatchRecord
        {
            surfacePatchId = new BistroBuilderEditId(prefix + "_surface"),
            buildPlaneId = "default",
            surfaceRole = "floor",
            finishDefinitionId = "finish.floor.default"
        };
        surface.fallbackBoundary.Add(new Vector2(110f, 110f));
        surface.fallbackBoundary.Add(new Vector2(111f, 110f));
        surface.fallbackBoundary.Add(new Vector2(111f, 111f));
        surface.fallbackBoundary.Add(new Vector2(110f, 111f));

        if (!architecture.TryExecute(
                new BistroBuilderApplySurfaceFinishCommand(surface),
                out _,
                out error))
            return false;

        RestaurantPlaceableObject table = InvokeB4FindTable(registry);
        if (table == null)
        {
            error = "No se encontró una mesa real para eliminar.";
            return false;
        }

        Vector3 tablePosition = table.transform.position;
        Transform parent =
            chair != null ? chair.transform.parent : table.transform.parent;

        if (!deletion.TryDelete(
                table,
                out RestaurantPlaceableDeletionResult deletionResult))
        {
            error = deletionResult.Message;
            return false;
        }

        if (!InvokeB4TryDuplicate(
                creation,
                transaction,
                registry,
                pricedProbe,
                tablePosition,
                parent,
                out _,
                out _,
                out error))
            return false;

        return true;
    }

    private static BistroBuilderCreateWallCommand BuildWall(
        BistroBuilderEditId id,
        float origin)
    {
        return new BistroBuilderCreateWallCommand(
            new BistroBuilderWallRecord
            {
                wallId = id,
                buildPlaneId = "default",
                axisStart = new Vector2(origin, origin),
                axisEnd = new Vector2(origin + 2f, origin),
                baseElevation = 0f,
                height = 2.5f,
                thickness = 0.12f,
                wallDefinitionId = "wall.default"
            });
    }

    private static void InitializeRuntimeAuthorities(
        RestaurantPlaceableRegistry registry,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlacementHistoryService placementHistory,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion)
    {
        var areaRegistry = Find<RestaurantAreaRegistry>();
        var areaAssignment = Find<RestaurantAreaAssignmentService>();
        var placementRegistry = Find<RestaurantPlacementRegistry>();
        var obstacleRegistry = Find<RestaurantPlacementObstacleRegistry>();
        var constraintService = Find<RestaurantPlacementConstraintService>();
        var validationService = Find<RestaurantPlacementValidationService>();
        var linkedGroups = Find<RestaurantPlacementLinkedGroupService>();
        var tableRegistry = Find<RestaurantTableRegistry>();
        var seatRegistry = Find<RestaurantSeatRegistry>();
        var topology = Find<RestaurantSeatingTopologyService>();

        InvokeLifecycle(areaRegistry, "OnEnable");
        InvokeLifecycle(areaRegistry, "Start");
        InvokeLifecycle(areaAssignment, "Awake");
        InvokeLifecycle(areaAssignment, "Start");
        InvokeLifecycle(registry, "Start");
        InvokeLifecycle(tableRegistry, "Awake");
        InvokeLifecycle(tableRegistry, "Start");
        InvokeLifecycle(seatRegistry, "Awake");
        InvokeLifecycle(seatRegistry, "OnEnable");
        InvokeLifecycle(seatRegistry, "Start");
        InvokeLifecycle(topology, "Awake");
        InvokeLifecycle(topology, "OnEnable");
        topology?.RebuildImmediately();
        InvokeLifecycle(Find<RestaurantSeatingPlacementConstraintRule>(), "Awake");
        InvokeLifecycle(Find<RestaurantSeatingLinkedGroupProvider>(), "Awake");
        InvokeLifecycle(Find<RestaurantSeatingSnapProvider>(), "Awake");
        InvokeLifecycle(placementRegistry, "Awake");
        InvokeLifecycle(placementRegistry, "OnEnable");
        InvokeLifecycle(placementRegistry, "Start");
        InvokeLifecycle(obstacleRegistry, "Start");
        InvokeLifecycle(validationService, "Awake");
        InvokeLifecycle(linkedGroups, "Awake");
        InvokeLifecycle(linkedGroups, "OnEnable");
        InvokeLifecycle(linkedGroups, "Start");
        InvokeLifecycle(constraintService, "Awake");
        InvokeLifecycle(constraintService, "Start");
        InvokeLifecycle(transaction, "Awake");
        InvokeLifecycle(placementHistory, "Awake");
        InvokeLifecycle(placementHistory, "OnEnable");
        InvokeLifecycle(Find<RestaurantPlaceableLifecycleService>(), "Awake");
        InvokeLifecycle(creation, "Awake");
        InvokeLifecycle(creation, "OnEnable");
        InvokeLifecycle(deletion, "Awake");
    }

    private static string SeatingFingerprint()
    {
        RestaurantSeatRegistry seatRegistry = Find<RestaurantSeatRegistry>();
        if (seatRegistry == null)
            return string.Empty;

        var rows = new List<string>();
        foreach (RestaurantSeat seat in seatRegistry.RegisteredSeats)
        {
            if (seat == null)
                continue;

            RestaurantPlaceableObject seatPlaceable =
                seat.GetComponent<RestaurantPlaceableObject>();
            RestaurantPlaceableObject tablePlaceable =
                seat.AssociatedTable != null
                    ? seat.AssociatedTable.GetComponent<RestaurantPlaceableObject>()
                    : null;

            rows.Add(
                (seatPlaceable != null ? seatPlaceable.InstanceId : seat.name) +
                "|" +
                (tablePlaceable != null ? tablePlaceable.InstanceId : string.Empty) +
                "|" +
                seat.AssociatedSlotIndex);
        }

        rows.Sort(StringComparer.Ordinal);
        return string.Join(";", rows);
    }

    private static string FinanceFingerprint(BistroBuilderFinanceService finance)
    {
        BistroBuilderFinanceSnapshot snapshot = finance.CreateSnapshot();
        return snapshot != null ? JsonUtility.ToJson(snapshot) : string.Empty;
    }

    private static string WorldFingerprint(RestaurantPlaceableRegistry registry)
    {
        return (string)InvokeB4("ComputeRegisteredWorldFingerprint", registry);
    }

    private static bool UniqueIds()
    {
        object[] args = { null };
        bool ok = (bool)InvokeB4("HasUniquePlaceableIds", args);
        return ok;
    }

    private static bool NoArchitectureOrphans(BistroBuilderEditDocument document)
    {
        object[] args = { document, null };
        return (bool)InvokeB4("NoArchitectureOrphans", args);
    }

    private static bool InvokeB4TryMoveAnyChair(
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        out RestaurantPlaceableObject chair,
        out string error)
    {
        object[] args = { transaction, registry, null, null };
        bool ok = (bool)InvokeB4("TryMoveAnyChair", args);
        chair = args[2] as RestaurantPlaceableObject;
        error = args[3] as string ?? string.Empty;
        return ok;
    }

    private static RestaurantPlaceableObject InvokeB4FindTable(
        RestaurantPlaceableRegistry registry)
    {
        return InvokeB4("FindTable", registry) as RestaurantPlaceableObject;
    }

    private static bool InvokeB4TryCreatePricedProbe(
        RestaurantPlaceableCreationService creation,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        RestaurantPlaceableItemDefinition definition,
        out RestaurantPlaceableObject created,
        out string error)
    {
        object[] args =
            { creation, transaction, registry, definition, null, null };
        bool ok = (bool)InvokeB4("TryCreatePricedProbe", args);
        created = args[4] as RestaurantPlaceableObject;
        error = args[5] as string ?? string.Empty;
        return ok;
    }

    private static bool InvokeB4TryDuplicate(
        RestaurantPlaceableCreationService creation,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        RestaurantPlaceableObject source,
        Vector3 position,
        Transform parent,
        out RestaurantPlaceableObject duplicateSource,
        out RestaurantPlaceableObject duplicate,
        out string error)
    {
        object[] args =
        {
            creation, transaction, registry, source, position, parent,
            null, null, null
        };
        bool ok = (bool)InvokeB4("TryDuplicateAnyPlaceable", args);
        duplicateSource = args[6] as RestaurantPlaceableObject;
        duplicate = args[7] as RestaurantPlaceableObject;
        error = args[8] as string ?? string.Empty;
        return ok;
    }

    private static object InvokeB4(string methodName, params object[] args)
    {
        MethodInfo method =
            typeof(BistroBuilderEditorV2B4GlobalHistorySelfTest).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(
                typeof(BistroBuilderEditorV2B4GlobalHistorySelfTest).FullName,
                methodName);
        return method.Invoke(null, args);
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static void InvokeLifecycle(object target, string methodName)
    {
        if (target == null)
            return;
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        method?.Invoke(target, null);
    }

    private static void InvokeBootstrap(Type type)
    {
        MethodInfo method = type.GetMethod(
            "Install",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(type.FullName, "Install");
        method.Invoke(null, null);
    }

    private static string Suffix(string error)
    {
        return string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : ": " + error;
    }

    private static void Check(bool condition, string label)
    {
        if (condition)
        {
            pass++;
            Lines.Add("OK - " + label);
        }
        else
        {
            fail++;
            Lines.Add("FAIL - " + label);
        }
    }

    private static bool Finish()
    {
        Lines.Add("Resultado: " + pass + " OK / " + fail + " fallos.");
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EditorV2_B5_Renovation_Report.txt");
        File.WriteAllLines(path, Lines, Encoding.UTF8);
        Debug.Log(string.Join(Environment.NewLine, Lines));
        return fail == 0;
    }
}
