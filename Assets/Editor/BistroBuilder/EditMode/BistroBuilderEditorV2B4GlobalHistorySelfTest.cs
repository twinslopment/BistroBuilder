using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BistroBuilderEditorV2B4GlobalHistorySelfTest
{
    private static readonly List<string> Lines = new List<string>(128);
    private static int pass;
    private static int fail;

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B4 Global History Self Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static bool Run()
    {
        Lines.Clear();
        pass = 0;
        fail = 0;
        Lines.Add("EDITOR V2 - B4 GLOBAL HISTORY SELF TEST");

        try
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Prototype_Restaurant.unity",
                OpenSceneMode.Single);

            InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
            InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));

            var editMode = Object.FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
            var global = Object.FindFirstObjectByType<BistroBuilderEditorV2GlobalHistory>(FindObjectsInactive.Include);
            var placementHistory = Object.FindFirstObjectByType<RestaurantPlacementHistoryService>(FindObjectsInactive.Include);
            var transaction = Object.FindFirstObjectByType<RestaurantPlacementTransactionService>(FindObjectsInactive.Include);
            var creation = Object.FindFirstObjectByType<RestaurantPlaceableCreationService>(FindObjectsInactive.Include);
            var deletion = Object.FindFirstObjectByType<RestaurantPlaceableDeletionService>(FindObjectsInactive.Include);
            var registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>(FindObjectsInactive.Include);
            var architecture = Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>(FindObjectsInactive.Include);
            var finance = Object.FindFirstObjectByType<BistroBuilderFinanceService>(FindObjectsInactive.Include);
            var financeBridge = Object.FindFirstObjectByType<BistroBuilderPlaceableFinanceBridge>(FindObjectsInactive.Include);

            bool dependencies =
                editMode != null &&
                global != null &&
                placementHistory != null &&
                transaction != null &&
                creation != null &&
                deletion != null &&
                registry != null &&
                architecture != null &&
                finance != null &&
                financeBridge != null;

            Check(dependencies, "B4 dispone de todas las autoridades reales necesarias");
            if (!dependencies)
                return Finish();

            // En batch EditMode Unity no ejecuta el ciclo MonoBehaviour de Play Mode.
            // Inicializamos las mismas autoridades reales, en su orden runtime,
            // para probar B4 sin mocks ni estados inventados.
            var areaRegistry = Object.FindFirstObjectByType<RestaurantAreaRegistry>(FindObjectsInactive.Include);
            var areaAssignment = Object.FindFirstObjectByType<RestaurantAreaAssignmentService>(FindObjectsInactive.Include);
            var placementRegistry = Object.FindFirstObjectByType<RestaurantPlacementRegistry>(FindObjectsInactive.Include);
            var obstacleRegistry = Object.FindFirstObjectByType<RestaurantPlacementObstacleRegistry>(FindObjectsInactive.Include);
            var constraintService = Object.FindFirstObjectByType<RestaurantPlacementConstraintService>(FindObjectsInactive.Include);
            var validationService = Object.FindFirstObjectByType<RestaurantPlacementValidationService>(FindObjectsInactive.Include);
            var linkedGroups = Object.FindFirstObjectByType<RestaurantPlacementLinkedGroupService>(FindObjectsInactive.Include);
            var tableRegistry = Object.FindFirstObjectByType<RestaurantTableRegistry>(FindObjectsInactive.Include);
            var seatRegistry = Object.FindFirstObjectByType<RestaurantSeatRegistry>(FindObjectsInactive.Include);
            var seatingTopology = Object.FindFirstObjectByType<RestaurantSeatingTopologyService>(FindObjectsInactive.Include);
            var seatingRule = Object.FindFirstObjectByType<RestaurantSeatingPlacementConstraintRule>(FindObjectsInactive.Include);
            var seatingLinkedProvider = Object.FindFirstObjectByType<RestaurantSeatingLinkedGroupProvider>(FindObjectsInactive.Include);
            var seatingSnapProvider = Object.FindFirstObjectByType<RestaurantSeatingSnapProvider>(FindObjectsInactive.Include);

            InvokeInstanceLifecycle(areaRegistry, "OnEnable");
            InvokeInstanceLifecycle(areaRegistry, "Start");
            InvokeInstanceLifecycle(areaAssignment, "Awake");
            InvokeInstanceLifecycle(areaAssignment, "Start");

            InvokeInstanceLifecycle(registry, "Start");
            InvokeInstanceLifecycle(tableRegistry, "Awake");
            InvokeInstanceLifecycle(tableRegistry, "Start");
            InvokeInstanceLifecycle(seatRegistry, "Awake");
            InvokeInstanceLifecycle(seatRegistry, "OnEnable");
            InvokeInstanceLifecycle(seatRegistry, "Start");
            InvokeInstanceLifecycle(seatingTopology, "Awake");
            InvokeInstanceLifecycle(seatingTopology, "OnEnable");
            seatingTopology?.RebuildImmediately();
            InvokeInstanceLifecycle(seatingRule, "Awake");
            InvokeInstanceLifecycle(seatingLinkedProvider, "Awake");
            InvokeInstanceLifecycle(seatingSnapProvider, "Awake");

            InvokeInstanceLifecycle(placementRegistry, "Awake");
            InvokeInstanceLifecycle(placementRegistry, "OnEnable");
            InvokeInstanceLifecycle(placementRegistry, "Start");
            InvokeInstanceLifecycle(obstacleRegistry, "Start");
            InvokeInstanceLifecycle(validationService, "Awake");
            InvokeInstanceLifecycle(linkedGroups, "Awake");
            InvokeInstanceLifecycle(linkedGroups, "OnEnable");
            InvokeInstanceLifecycle(linkedGroups, "Start");
            InvokeInstanceLifecycle(constraintService, "Awake");
            InvokeInstanceLifecycle(constraintService, "Start");
            InvokeInstanceLifecycle(transaction, "Awake");
            InvokeInstanceLifecycle(placementHistory, "Awake");
            InvokeInstanceLifecycle(placementHistory, "OnEnable");
            InvokeInstanceLifecycle(
                Object.FindFirstObjectByType<RestaurantPlaceableLifecycleService>(FindObjectsInactive.Include),
                "Awake");
            InvokeInstanceLifecycle(creation, "Awake");
            InvokeInstanceLifecycle(creation, "OnEnable");
            InvokeInstanceLifecycle(deletion, "Awake");

            Check(finance.TryInitializeFresh(out string financeInitError),
                "B4 inicializa la autoridad financiera real" +
                (string.IsNullOrEmpty(financeInitError) ? string.Empty : ": " + financeInitError));

            BistroBuilderSupplierPurchaseOrderService purchaseOrderAuthority =
                Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>(
                    FindObjectsInactive.Include);
            if (purchaseOrderAuthority == null)
            {
                GameObject purchaseOrderHost =
                    new GameObject("__B4_PurchaseOrderAuthority");
                purchaseOrderAuthority =
                    purchaseOrderHost.AddComponent<BistroBuilderSupplierPurchaseOrderService>();
            }

            FieldInfo purchaseOrderInstanceField =
                typeof(BistroBuilderSupplierPurchaseOrderService).GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.NonPublic);
            purchaseOrderInstanceField?.SetValue(null, purchaseOrderAuthority);

            bool purchaseOrderReady =
                purchaseOrderAuthority != null &&
                purchaseOrderAuthority.TryInitializeFresh() &&
                purchaseOrderAuthority.IsInitialized;
            Check(purchaseOrderReady,
                "B4 inicializa PurchaseOrder 2.3E para autorización de caja");

            BistroBuilderSupplierPurchaseFinanceBridge supplierFinanceBridge =
                Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseFinanceBridge>(
                    FindObjectsInactive.Include);
            InvokeInstanceLifecycle(supplierFinanceBridge, "OnEnable");

            InvokeInstanceLifecycle(financeBridge, "OnEnable");

            bool entered = editMode.IsEditModeActive ||
                editMode.TryEnterEditMode(out _, out _);
            Check(entered, "B4 entra en modo edición real");
            if (!entered)
                return Finish();

            placementHistory.ClearHistory();
            global.ClearGlobalOrdering();

            if (architecture.HasSession)
                architecture.CancelSession();

            bool sessionReady = architecture.TryBeginSession(out string sessionError);
            Check(sessionReady, "B4 abre una sesión arquitectónica limpia" +
                (sessionReady ? string.Empty : ": " + sessionError));
            if (!sessionReady)
                return Finish();

            Check(financeBridge.IsBound,
                "B4 mantiene enlazado el puente financiero de colocables");

            RestaurantPlaceableItemDefinition pricedProbeDefinition =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                    "Assets/Data/Restaurant/EditMode/PlaceableItems/PlaceableItemDefinition_FactoryTestPlant.asset");
            Check(pricedProbeDefinition != null &&
                  pricedProbeDefinition.PurchasePriceCents > 0L &&
                  pricedProbeDefinition.HasValidPrefab,
                "B4 dispone de un colocable canónico con coste real para verificar ledger");

            RestaurantPlaceableObject pricedProbe = null;
            string pricedProbeError = string.Empty;
            bool pricedProbeCreated =
                pricedProbeDefinition != null &&
                TryCreatePricedProbe(
                    creation,
                    transaction,
                    registry,
                    pricedProbeDefinition,
                    out pricedProbe,
                    out pricedProbeError);
            Check(pricedProbeCreated,
                "B4 prepara fixture económico real antes del gate" +
                (pricedProbeCreated ? string.Empty : ": " + pricedProbeError));
            if (!pricedProbeCreated)
                return Finish();

            // El fixture pasa a ser parte del baseline; su compra queda en el ledger,
            // pero su comando de creación no forma parte de las 5 operaciones B4.
            placementHistory.ClearHistory();
            global.ClearGlobalOrdering();

            string baselineArchitecture = architecture.Session.Draft.ComputeFingerprint();
            string baselineWorld = ComputeRegisteredWorldFingerprint(registry);
            long baselineBalance = finance.CurrentBalanceCents;
            int baselineTransactions = finance.TransactionCount;

            Check(ValidateFinance(finance, out string baselineFinanceError),
                "B4 ledger financiero válido antes de la secuencia" +
                (string.IsNullOrEmpty(baselineFinanceError) ? string.Empty : ": " + baselineFinanceError));
            Check(HasUniquePlaceableIds(out string baselineIdError),
                "B4 IDs de colocables únicos antes de la secuencia" +
                (string.IsNullOrEmpty(baselineIdError) ? string.Empty : ": " + baselineIdError));
            Check(NoArchitectureOrphans(architecture.Session.Draft, out string baselineOrphanError),
                "B4 sin referencias arquitectónicas huérfanas al inicio" +
                (string.IsNullOrEmpty(baselineOrphanError) ? string.Empty : ": " + baselineOrphanError));

            var chronology = new List<string>(5);

            RestaurantPlaceableObject chair;
            string moveError;
            bool moved = TryMoveAnyChair(transaction, registry, out chair, out moveError);
            Check(moved, "B4 paso 1/5 mueve una silla con Placement real" +
                (moved ? string.Empty : ": " + moveError));
            if (!moved)
                return Finish();
            RecordChronology(global, chronology, 1, "movimiento de silla");

            var probeWall = new BistroBuilderWallRecord
            {
                wallId = new BistroBuilderEditId("b4_global_history_probe_wall"),
                buildPlaneId = "default",
                axisStart = new Vector2(100f, 100f),
                axisEnd = new Vector2(102f, 100f),
                baseElevation = 0f,
                height = 2.5f,
                thickness = 0.12f,
                wallDefinitionId = "wall.default"
            };
            bool wallCreated = architecture.TryExecute(
                new BistroBuilderCreateWallCommand(probeWall),
                out _,
                out string wallError);
            Check(wallCreated, "B4 paso 2/5 crea pared mediante autoridad arquitectónica" +
                (wallCreated ? string.Empty : ": " + wallError));
            if (!wallCreated)
                return Finish();
            RecordChronology(global, chronology, 2, "creación de pared");

            var probeSurface = new BistroBuilderSurfaceFinishPatchRecord
            {
                surfacePatchId = new BistroBuilderEditId("b4_global_history_probe_surface"),
                buildPlaneId = "default",
                surfaceRole = "floor",
                finishDefinitionId = "surface.b4_probe"
            };
            probeSurface.fallbackBoundary.Add(new Vector2(110f, 110f));
            probeSurface.fallbackBoundary.Add(new Vector2(111f, 110f));
            probeSurface.fallbackBoundary.Add(new Vector2(111f, 111f));
            probeSurface.fallbackBoundary.Add(new Vector2(110f, 111f));

            bool surfaceChanged = architecture.TryExecute(
                new BistroBuilderApplySurfaceFinishCommand(probeSurface),
                out _,
                out string surfaceError);
            Check(surfaceChanged, "B4 paso 3/5 cambia una superficie mediante autoridad arquitectónica" +
                (surfaceChanged ? string.Empty : ": " + surfaceError));
            if (!surfaceChanged)
                return Finish();
            RecordChronology(global, chronology, 3, "cambio de superficie");

            RestaurantPlaceableObject table = FindTable(registry);
            Check(table != null, "B4 encuentra una mesa real registrada para la prueba");
            if (table == null)
                return Finish();

            Vector3 tablePosition = table.transform.position;
            Quaternion tableRotation = table.transform.rotation;
            Transform duplicateParent = chair != null ? chair.transform.parent : table.transform.parent;

            bool tableDeleted = deletion.TryDelete(
                table,
                out RestaurantPlaceableDeletionResult deleteResult);
            Check(tableDeleted, "B4 paso 4/5 elimina una mesa mediante lifecycle real" +
                (tableDeleted ? string.Empty : ": " + deleteResult.Message));
            if (!tableDeleted)
                return Finish();
            RecordChronology(global, chronology, 4, "eliminación de mesa");

            RestaurantPlaceableObject duplicateSource;
            RestaurantPlaceableObject duplicate;
            string duplicateError;
            bool duplicated = TryDuplicateAnyPlaceable(
                creation,
                transaction,
                registry,
                pricedProbe,
                tablePosition,
                duplicateParent,
                out duplicateSource,
                out duplicate,
                out duplicateError);
            Check(duplicated, "B4 paso 5/5 duplica un objeto mediante creación real" +
                (duplicated ? string.Empty : ": " + duplicateError));
            if (!duplicated)
                return Finish();
            RecordChronology(global, chronology, 5, "duplicado de objeto");

            string finalArchitecture = architecture.Session.Draft.ComputeFingerprint();
            string finalWorld = ComputeRegisteredWorldFingerprint(registry);
            long finalBalance = finance.CurrentBalanceCents;
            int finalTransactions = finance.TransactionCount;

            Check(!string.Equals(finalArchitecture, baselineArchitecture, StringComparison.Ordinal),
                "B4 la secuencia modifica realmente el Draft arquitectónico");
            Check(!string.Equals(finalWorld, baselineWorld, StringComparison.Ordinal),
                "B4 la secuencia modifica realmente el mundo de colocables");
            Check(finalTransactions > baselineTransactions,
                "B4 creación/eliminación publican movimientos en el ledger");
            Check(ValidateFinance(finance, out string finalFinanceError),
                "B4 ledger válido tras la secuencia inicial" +
                (string.IsNullOrEmpty(finalFinanceError) ? string.Empty : ": " + finalFinanceError));
            Check(HasUniquePlaceableIds(out string finalIdError),
                "B4 sin IDs duplicados tras la secuencia inicial" +
                (string.IsNullOrEmpty(finalIdError) ? string.Empty : ": " + finalIdError));
            Check(NoArchitectureOrphans(architecture.Session.Draft, out string finalOrphanError),
                "B4 sin referencias huérfanas tras la secuencia inicial" +
                (string.IsNullOrEmpty(finalOrphanError) ? string.Empty : ": " + finalOrphanError));

            Check(global.UndoCount == 5 && global.RedoCount == 0,
                "B4 historial global contiene exactamente las 5 operaciones mezcladas");

            bool undoAll = true;
            for (int i = chronology.Count - 1; i >= 0; i--)
            {
                string expected = chronology[i];
                bool correctTop = string.Equals(
                    global.NextUndoDescription,
                    expected,
                    StringComparison.Ordinal);
                Check(correctTop,
                    "B4 Undo respeta orden global: " + expected);

                if (!global.TryUndo(out string undoError))
                {
                    Check(false, "B4 Undo ejecuta " + expected + ": " + undoError);
                    undoAll = false;
                    break;
                }
            }

            Check(undoAll, "B4 completa Undo de las 5 operaciones");
            Check(global.UndoCount == 0 && global.RedoCount == 5,
                "B4 tras Undo completo conserva una pila Redo global coherente");

            string undoArchitecture = architecture.Session.Draft.ComputeFingerprint();
            string undoWorld = ComputeRegisteredWorldFingerprint(registry);
            long undoBalance = finance.CurrentBalanceCents;
            int undoTransactions = finance.TransactionCount;

            Check(string.Equals(undoArchitecture, baselineArchitecture, StringComparison.Ordinal),
                "B4 Undo restaura exactamente el fingerprint arquitectónico inicial");
            Check(string.Equals(undoWorld, baselineWorld, StringComparison.Ordinal),
                "B4 Undo restaura exactamente mobiliario, poses, jerarquía y áreas");
            Check(undoBalance == baselineBalance,
                "B4 Undo restaura exactamente la Caja inicial");
            Check(undoTransactions > finalTransactions,
                "B4 Undo financiero usa asientos compensatorios, no borra el ledger");
            Check(ValidateFinance(finance, out string undoFinanceError),
                "B4 ledger válido tras Undo completo" +
                (string.IsNullOrEmpty(undoFinanceError) ? string.Empty : ": " + undoFinanceError));
            Check(HasUniquePlaceableIds(out string undoIdError),
                "B4 sin IDs duplicados tras Undo completo" +
                (string.IsNullOrEmpty(undoIdError) ? string.Empty : ": " + undoIdError));
            Check(NoArchitectureOrphans(architecture.Session.Draft, out string undoOrphanError),
                "B4 sin referencias huérfanas tras Undo completo" +
                (string.IsNullOrEmpty(undoOrphanError) ? string.Empty : ": " + undoOrphanError));

            bool redoAll = true;
            for (int i = 0; i < chronology.Count; i++)
            {
                string expected = chronology[i];
                bool correctTop = string.Equals(
                    global.NextRedoDescription,
                    expected,
                    StringComparison.Ordinal);
                Check(correctTop,
                    "B4 Redo respeta orden global: " + expected);

                if (!global.TryRedo(out string redoError))
                {
                    Check(false, "B4 Redo ejecuta " + expected + ": " + redoError);
                    redoAll = false;
                    break;
                }
            }

            Check(redoAll, "B4 completa Redo de las 5 operaciones");
            Check(global.UndoCount == 5 && global.RedoCount == 0,
                "B4 tras Redo completo recupera la pila Undo global");

            string redoArchitecture = architecture.Session.Draft.ComputeFingerprint();
            string redoWorld = ComputeRegisteredWorldFingerprint(registry);
            long redoBalance = finance.CurrentBalanceCents;
            int redoTransactions = finance.TransactionCount;

            Check(string.Equals(redoArchitecture, finalArchitecture, StringComparison.Ordinal),
                "B4 Redo reproduce exactamente el fingerprint arquitectónico final");
            Check(string.Equals(redoWorld, finalWorld, StringComparison.Ordinal),
                "B4 Redo reproduce exactamente mobiliario, poses, jerarquía y áreas");
            Check(redoBalance == finalBalance,
                "B4 Redo reproduce exactamente la Caja final");
            Check(redoTransactions > undoTransactions,
                "B4 Redo financiero añade la compensación correcta al ledger");
            Check(ValidateFinance(finance, out string redoFinanceError),
                "B4 ledger válido tras Redo completo" +
                (string.IsNullOrEmpty(redoFinanceError) ? string.Empty : ": " + redoFinanceError));
            Check(HasUniquePlaceableIds(out string redoIdError),
                "B4 sin IDs duplicados tras Redo completo" +
                (string.IsNullOrEmpty(redoIdError) ? string.Empty : ": " + redoIdError));
            Check(NoArchitectureOrphans(architecture.Session.Draft, out string redoOrphanError),
                "B4 sin referencias huérfanas tras Redo completo" +
                (string.IsNullOrEmpty(redoOrphanError) ? string.Empty : ": " + redoOrphanError));

            Check(duplicate != null &&
                  duplicateSource != null &&
                  !string.IsNullOrWhiteSpace(duplicate.InstanceId) &&
                  !string.Equals(duplicate.InstanceId, duplicateSource.InstanceId, StringComparison.Ordinal),
                "B4 el duplicado conserva identidad persistente propia");
        }
        catch (Exception ex)
        {
            fail++;
            Lines.Add("EXCEPTION - " + ex);
        }

        return Finish();
    }

    private static void RecordChronology(
        BistroBuilderEditorV2GlobalHistory global,
        List<string> chronology,
        int expectedUndoCount,
        string label)
    {
        Check(global.UndoCount == expectedUndoCount,
            "B4 registra en historial global " + label);
        string description = global.NextUndoDescription;
        Check(!string.IsNullOrWhiteSpace(description),
            "B4 expone descripción global para " + label);
        chronology.Add(description);
    }

    private static bool TryMoveAnyChair(
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        out RestaurantPlaceableObject chair,
        out string error)
    {
        chair = null;
        error = string.Empty;
        var candidates = new List<RestaurantPlaceableObject>();
        foreach (RestaurantPlaceableObject p in registry.RegisteredPlaceables)
        {
            if (p == null || p.ItemDefinition == null)
                continue;
            if (p.ItemDefinition.Category == RestaurantPlaceableItemCategory.Seating ||
                Contains(p.ItemDefinition.ItemId, "chair") ||
                Contains(p.ItemDefinition.DisplayName, "silla"))
            {
                candidates.Add(p);
            }
        }

        candidates.Sort((a, b) =>
            string.CompareOrdinal(a.InstanceId, b.InstanceId));

        Vector3[] offsets =
        {
            new Vector3(0.05f,0f,0f), new Vector3(-0.05f,0f,0f),
            new Vector3(0f,0f,0.05f), new Vector3(0f,0f,-0.05f),
            new Vector3(0.10f,0f,0f), new Vector3(-0.10f,0f,0f),
            new Vector3(0f,0f,0.10f), new Vector3(0f,0f,-0.10f),
            new Vector3(0.20f,0f,0f), new Vector3(-0.20f,0f,0f),
            new Vector3(0f,0f,0.20f), new Vector3(0f,0f,-0.20f),
            new Vector3(0.35f,0f,0f), new Vector3(-0.35f,0f,0f),
            new Vector3(0f,0f,0.35f), new Vector3(0f,0f,-0.35f),
            new Vector3(0.20f,0f,0.20f), new Vector3(-0.20f,0f,0.20f),
            new Vector3(0.20f,0f,-0.20f), new Vector3(-0.20f,0f,-0.20f)
        };

        for (int c = 0; c < candidates.Count; c++)
        {
            RestaurantPlaceableObject candidate = candidates[c];
            if (!candidate.TryGetComponent(out RestaurantAreaMember member))
                continue;

            Vector3 originalPosition = candidate.transform.position;
            Quaternion originalRotation = candidate.transform.rotation;

            if (!transaction.TryBeginPlacement(member, out RestaurantPlacementTransactionFailureReason beginFailure))
            {
                error = "No se pudo iniciar movimiento: " + beginFailure;
                continue;
            }

            bool validCandidate = false;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (transaction.TryPreviewPlacement(
                        originalPosition + offsets[i],
                        originalRotation,
                        out RestaurantPlacementValidationResult validation,
                        out _) &&
                    validation.IsValid &&
                    validation.CandidateArea != null)
                {
                    validCandidate = true;
                    break;
                }
            }

            if (validCandidate &&
                transaction.TryCommitPlacement(out _, out RestaurantPlacementTransactionFailureReason commitFailure))
            {
                chair = candidate;
                return true;
            }

            if (transaction.HasActiveTransaction)
                transaction.CancelPlacement();

            error = validCandidate
                ? "La confirmación del movimiento fue rechazada."
                : "No se encontró una posición vecina válida para " + candidate.DisplayName + ".";
        }

        if (candidates.Count == 0)
            error = "No hay ninguna silla registrada.";
        return false;
    }

    private static RestaurantPlaceableObject FindTable(RestaurantPlaceableRegistry registry)
    {
        RestaurantPlaceableObject fallback = null;
        foreach (RestaurantPlaceableObject p in registry.RegisteredPlaceables)
        {
            if (p == null || p.ItemDefinition == null)
                continue;

            bool namedTable =
                Contains(p.ItemDefinition.ItemId, "table") ||
                Contains(p.ItemDefinition.ItemId, "mesa") ||
                Contains(p.ItemDefinition.DisplayName, "mesa") ||
                Contains(p.ItemDefinition.DisplayName, "table") ||
                Contains(p.name, "mesa") ||
                Contains(p.name, "table");

            if (namedTable && p.ItemDefinition.PurchasePrice > 0)
                return p;
            if (namedTable)
                fallback = p;
        }
        return fallback;
    }

    private static bool TryCreatePricedProbe(
        RestaurantPlaceableCreationService creation,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        RestaurantPlaceableItemDefinition definition,
        out RestaurantPlaceableObject created,
        out string error)
    {
        created = null;
        error = string.Empty;

        var anchors = new List<RestaurantPlaceableObject>();
        foreach (RestaurantPlaceableObject p in registry.RegisteredPlaceables)
            if (p != null)
                anchors.Add(p);

        anchors.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));
        if (anchors.Count == 0)
        {
            error = "No hay anclas espaciales registradas.";
            return false;
        }

        RestaurantPlaceableObject anchor = anchors[0];
        Vector3 start = anchor.transform.position;
        Transform parent = anchor.transform.parent;

        if (!creation.TryBeginCreation(
                definition,
                start,
                Quaternion.identity,
                parent,
                out RestaurantPlaceableObject provisional,
                out RestaurantPlaceableCreationResult beginResult))
        {
            error = beginResult.Message;
            return false;
        }

        float[] distances = { 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f };
        RestaurantPlacementValidationResult lastValidation = default;
        bool valid = false;

        for (int a = 0; a < anchors.Count && !valid; a++)
        {
            Vector3 origin = anchors[a].transform.position;
            for (int d = 0; d < distances.Length && !valid; d++)
            {
                float radius = distances[d];
                Vector3[] offsets =
                {
                    new Vector3(radius,0f,0f), new Vector3(-radius,0f,0f),
                    new Vector3(0f,0f,radius), new Vector3(0f,0f,-radius),
                    new Vector3(radius,0f,radius), new Vector3(-radius,0f,radius),
                    new Vector3(radius,0f,-radius), new Vector3(-radius,0f,-radius)
                };

                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 candidate = origin + offsets[i];
                    if (transaction.TryPreviewPlacement(
                            candidate,
                            Quaternion.identity,
                            out lastValidation,
                            out _) &&
                        lastValidation.IsValid &&
                        lastValidation.CandidateArea != null)
                    {
                        valid = true;
                        break;
                    }
                }
            }
        }

        if (!valid)
        {
            creation.TryCancelActiveCreation(out _);
            error = "No se encontró hueco válido para el fixture económico. Último estado: " +
                lastValidation.Status + " / " + lastValidation.UserMessage;
            return false;
        }

        if (!creation.TryCommitActiveCreation(
                out RestaurantPlaceableCreationResult commitResult))
        {
            if (creation.HasActiveCreation)
                creation.TryCancelActiveCreation(out _);
            error = commitResult.Message;
            return false;
        }

        created = provisional;
        return created != null;
    }

    private static bool TryDuplicateAnyPlaceable(
        RestaurantPlaceableCreationService creation,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlaceableRegistry registry,
        RestaurantPlaceableObject preferredSource,
        Vector3 preferredPosition,
        Transform fallbackParent,
        out RestaurantPlaceableObject source,
        out RestaurantPlaceableObject duplicate,
        out string error)
    {
        source = null;
        duplicate = null;
        error = string.Empty;

        var candidates = new List<RestaurantPlaceableObject>();
        if (preferredSource != null &&
            preferredSource.ItemDefinition != null &&
            preferredSource.ItemDefinition.HasValidPrefab &&
            !preferredSource.TryGetComponent(out RestaurantSeat _))
        {
            candidates.Add(preferredSource);
        }

        foreach (RestaurantPlaceableObject p in registry.RegisteredPlaceables)
        {
            if (p == null ||
                ReferenceEquals(p, preferredSource) ||
                p.ItemDefinition == null ||
                !p.ItemDefinition.HasValidPrefab ||
                p.TryGetComponent(out RestaurantSeat _) ||
                p.TryGetComponent(out RestaurantTableSeatingConfiguration _))
            {
                continue;
            }

            candidates.Add(p);
        }

        candidates.Sort((a, b) =>
        {
            int categoryA = DuplicationRank(a.ItemDefinition.Category);
            int categoryB = DuplicationRank(b.ItemDefinition.Category);
            int categoryComparison = categoryA.CompareTo(categoryB);
            return categoryComparison != 0
                ? categoryComparison
                : string.CompareOrdinal(a.InstanceId, b.InstanceId);
        });

        Vector3[] offsets =
        {
            Vector3.zero,
            new Vector3(0.50f,0f,0f), new Vector3(-0.50f,0f,0f),
            new Vector3(0f,0f,0.50f), new Vector3(0f,0f,-0.50f),
            new Vector3(1.0f,0f,0f), new Vector3(-1.0f,0f,0f),
            new Vector3(0f,0f,1.0f), new Vector3(0f,0f,-1.0f),
            new Vector3(1.5f,0f,0f), new Vector3(-1.5f,0f,0f),
            new Vector3(0f,0f,1.5f), new Vector3(0f,0f,-1.5f),
            new Vector3(2.0f,0f,0f), new Vector3(-2.0f,0f,0f),
            new Vector3(0f,0f,2.0f), new Vector3(0f,0f,-2.0f),
            new Vector3(2.0f,0f,2.0f), new Vector3(-2.0f,0f,2.0f),
            new Vector3(2.0f,0f,-2.0f), new Vector3(-2.0f,0f,-2.0f),
            new Vector3(3.0f,0f,0f), new Vector3(-3.0f,0f,0f),
            new Vector3(0f,0f,3.0f), new Vector3(0f,0f,-3.0f),
            new Vector3(4.0f,0f,0f), new Vector3(-4.0f,0f,0f),
            new Vector3(0f,0f,4.0f), new Vector3(0f,0f,-4.0f)
        };

        string lastError = string.Empty;
        for (int c = 0; c < candidates.Count; c++)
        {
            RestaurantPlaceableObject candidate = candidates[c];
            Vector3 basePosition = new Vector3(
                preferredPosition.x,
                candidate.transform.position.y,
                preferredPosition.z);
            Quaternion rotation = candidate.transform.rotation;
            Transform parent = candidate.transform.parent != null
                ? candidate.transform.parent
                : fallbackParent;

            if (!creation.TryBeginCreation(
                    candidate.ItemDefinition,
                    basePosition,
                    rotation,
                    parent,
                    out RestaurantPlaceableObject provisional,
                    out RestaurantPlaceableCreationResult beginResult))
            {
                lastError = candidate.DisplayName + ": " + beginResult.Message;
                continue;
            }

            bool validCandidate = false;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (transaction.TryPreviewPlacement(
                        basePosition + offsets[i],
                        rotation,
                        out RestaurantPlacementValidationResult validation,
                        out _) &&
                    validation.IsValid &&
                    validation.CandidateArea != null)
                {
                    validCandidate = true;
                    break;
                }

                lastError = candidate.DisplayName + ": " +
                    validation.Status + " / " + validation.UserMessage;
            }

            if (!validCandidate)
            {
                creation.TryCancelActiveCreation(out _);
                continue;
            }

            if (!creation.TryCommitActiveCreation(
                    out RestaurantPlaceableCreationResult commitResult))
            {
                if (creation.HasActiveCreation)
                    creation.TryCancelActiveCreation(out _);
                lastError = candidate.DisplayName + ": " + commitResult.Message;
                continue;
            }

            source = candidate;
            duplicate = provisional;
            return duplicate != null;
        }

        error = candidates.Count == 0
            ? "No hay colocables no-seating duplicables."
            : "Ningún colocable normal encontró una posición válida. Último diagnóstico: " + lastError;
        return false;
    }

    private static int DuplicationRank(RestaurantPlaceableItemCategory category)
    {
        switch (category)
        {
            case RestaurantPlaceableItemCategory.Decoration: return 0;
            case RestaurantPlaceableItemCategory.Furniture: return 1;
            case RestaurantPlaceableItemCategory.ServiceEquipment: return 2;
            case RestaurantPlaceableItemCategory.Other: return 3;
            case RestaurantPlaceableItemCategory.Lighting: return 4;
            case RestaurantPlaceableItemCategory.KitchenEquipment: return 5;
            case RestaurantPlaceableItemCategory.Structural: return 6;
            default: return 10;
        }
    }

    private static bool ValidateFinance(
        BistroBuilderFinanceService finance,
        out string error)
    {
        error = string.Empty;
        BistroBuilderFinanceSnapshot snapshot = finance.CreateSnapshot();
        if (snapshot == null)
        {
            error = "Snapshot financiero nulo.";
            return false;
        }

        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(snapshot, out error))
            return false;

        if (snapshot.currentBalanceCents != finance.CurrentBalanceCents ||
            snapshot.transactions == null ||
            snapshot.transactions.Count != finance.TransactionCount)
        {
            error = "Snapshot, Caja y contador del ledger no coinciden.";
            return false;
        }

        return true;
    }

    private static bool HasUniquePlaceableIds(out string error)
    {
        error = string.Empty;
        RestaurantPlaceableObject[] all =
            Object.FindObjectsByType<RestaurantPlaceableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < all.Length; i++)
        {
            RestaurantPlaceableObject p = all[i];
            if (p == null || string.IsNullOrWhiteSpace(p.InstanceId))
                continue;
            if (!ids.Add(p.InstanceId))
            {
                error = "InstanceId duplicado: " + p.InstanceId;
                return false;
            }
        }

        return true;
    }

    private static bool NoArchitectureOrphans(
        BistroBuilderEditDocument document,
        out string error)
    {
        error = string.Empty;
        if (document == null)
        {
            error = "Draft nulo.";
            return false;
        }

        for (int i = 0; i < document.openings.Count; i++)
        {
            BistroBuilderOpeningRecord opening = document.openings[i];
            if (opening != null && document.FindWall(opening.hostWallId) == null)
            {
                error = "Opening huérfano: " + opening.openingId.Value;
                return false;
            }
        }

        var roomIds = new HashSet<BistroBuilderEditId>();
        for (int i = 0; i < document.rooms.Count; i++)
            if (document.rooms[i] != null)
                roomIds.Add(document.rooms[i].roomId);

        for (int i = 0; i < document.zones.Count; i++)
        {
            BistroBuilderFunctionalZoneRecord zone = document.zones[i];
            if (zone == null)
                continue;
            for (int r = 0; r < zone.roomIds.Count; r++)
            {
                if (!roomIds.Contains(zone.roomIds[r]))
                {
                    error = "Zona con RoomId huérfano: " + zone.roomIds[r].Value;
                    return false;
                }
            }
        }

        return true;
    }

    private static string ComputeRegisteredWorldFingerprint(
        RestaurantPlaceableRegistry registry)
    {
        var items = new List<RestaurantPlaceableObject>();
        foreach (RestaurantPlaceableObject p in registry.RegisteredPlaceables)
            if (p != null)
                items.Add(p);

        items.Sort((a, b) =>
            string.CompareOrdinal(a.InstanceId, b.InstanceId));

        var sb = new StringBuilder(items.Count * 128);
        for (int i = 0; i < items.Count; i++)
        {
            RestaurantPlaceableObject p = items[i];
            Transform t = p.transform;
            RestaurantAreaMember member = p.GetComponent<RestaurantAreaMember>();
            sb.Append(p.InstanceId).Append('|');
            sb.Append(p.ItemDefinition != null ? p.ItemDefinition.ItemId : "").Append('|');
            AppendVector(sb, t.position);
            AppendQuaternion(sb, t.rotation);
            AppendVector(sb, t.localScale);
            sb.Append(BuildTransformPath(t.parent)).Append('|');
            sb.Append(member != null && member.AssignedArea != null
                ? member.AssignedArea.name
                : "").Append(';');
        }

        return sb.ToString();
    }

    private static void AppendVector(StringBuilder sb, Vector3 value)
    {
        sb.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
    }

    private static void AppendQuaternion(StringBuilder sb, Quaternion value)
    {
        sb.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(value.w.ToString("R", CultureInfo.InvariantCulture)).Append('|');
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var names = new List<string>(8);
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static bool Contains(string text, string token)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void InvokeInstanceLifecycle(object target, string methodName)
    {
        if (target == null)
            return;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
            method.Invoke(target, null);
    }

    private static void InvokeBootstrap(Type bootstrapType)
    {
        MethodInfo install = bootstrapType.GetMethod(
            "Install",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (install == null)
            throw new MissingMethodException(bootstrapType.FullName, "Install");
        install.Invoke(null, null);
    }

    private static bool Finish()
    {
        Lines.Add("Resultado: " + pass + " OK / " + fail + " fallos.");
        string reportPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EditorV2_B4_GlobalHistory_Report.txt");
        File.WriteAllLines(reportPath, Lines, Encoding.UTF8);

        Debug.Log(string.Join(Environment.NewLine, Lines));
        return fail == 0;
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
}
