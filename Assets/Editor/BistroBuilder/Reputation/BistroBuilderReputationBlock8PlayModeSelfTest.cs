using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 8G — Gate funcional real: grupo -> esperas -> comanda canónica ->
/// cobro financiero -> experiencia -> reseña -> boca a boca -> UI.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderReputationBlock8PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Reputation.8G.Play.Stage";
    private const string SuccessKey = "BB.Reputation.8G.Play.Success";
    private const string ReportPath = "ReputationBlock8PlayModeReport.txt";

    private static BistroBuilderReputationSnapshot originalReputation;
    private static BistroBuilderReputationRuntimeSnapshot originalRuntime;
    private static BistroBuilderGuestRelationsSnapshot originalRelations;
    private static BistroBuilderFinanceSnapshot originalFinance;
    private static BistroBuilderMarketingSnapshot originalMarketing;
    private static string originalGameId;
    private static string originalRestaurantName;
    private static string originalCreatedUtc;
    private static int originalDayIndex;
    private static int originalYear;
    private static int originalMonth;
    private static int originalDay;
    private static string originalProgressionStage;
    private static int originalProgressionLevel;

    private static BistroBuilderReputationService reputation;
    private static BistroBuilderCustomerExperienceTrackingService tracking;
    private static BistroBuilderGuestRelationsService relations;
    private static BistroBuilderFinanceService finance;
    private static BistroBuilderMarketingService marketing;
    private static BistroBuilderCanonicalOrderService canonical;
    private static BistroBuilderOrderLineExecutionService lineExecution;
    private static BistroBuilderReputationPlayerFacade facade;
    private static BistroBuilderReputationPlayerScreen screen;
    private static BistroBuilderGeneralGameStateService general;
    private static TableAssignmentSystem tables;
    private static OrderSystem orders;
    private static CustomerGroup testGroup;
    private static RestaurantTable testTable;
    private static Waiter testWaiter;
    private static RestaurantOrder testOrder;
    private static double deadline;

    static BistroBuilderReputationBlock8PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Reputation/8G - PlayMode funcional", false, 8194)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 8G ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "init_cli" : "init_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        if (stage.StartsWith("init_", StringComparison.Ordinal) && Time.frameCount >= 4)
        {
            Initialize(cli);
            return;
        }
        if (stage.StartsWith("wait_waiter_", StringComparison.Ordinal) &&
            EditorApplication.timeSinceStartup >= deadline)
        {
            CreateOrderAndStartFoodWait(cli);
            return;
        }
        if (stage.StartsWith("wait_food_", StringComparison.Ordinal) &&
            EditorApplication.timeSinceStartup >= deadline)
        {
            StartBillWait(cli);
            return;
        }
        if (stage.StartsWith("wait_bill_", StringComparison.Ordinal) &&
            EditorApplication.timeSinceStartup >= deadline)
        {
            CompletePaidVisit(cli);
        }
    }

    private static void Initialize(bool commandLine)
    {
        reputation = Find<BistroBuilderReputationService>();
        tracking = Find<BistroBuilderCustomerExperienceTrackingService>();
        relations = Find<BistroBuilderGuestRelationsService>();
        finance = Find<BistroBuilderFinanceService>();
        marketing = Find<BistroBuilderMarketingService>();
        canonical = Find<BistroBuilderCanonicalOrderService>();
        lineExecution = Find<BistroBuilderOrderLineExecutionService>();
        facade = Find<BistroBuilderReputationPlayerFacade>();
        screen = Find<BistroBuilderReputationPlayerScreen>();
        general = Find<BistroBuilderGeneralGameStateService>();
        tables = Find<TableAssignmentSystem>();
        orders = Find<OrderSystem>();

        if (reputation == null || tracking == null || relations == null ||
            finance == null || marketing == null || canonical == null ||
            lineExecution == null || facade == null || screen == null || general == null ||
            tables == null || orders == null)
        {
            Finish(false, "8G: faltan autoridades runtime.", commandLine);
            return;
        }

        string trackingError = string.Empty;
        string facadeError = string.Empty;
        string screenError = string.Empty;
        bool trackingValid = tracking.ValidateConfiguration(out trackingError);
        bool facadeValid = facade.ValidateConfiguration(out facadeError);
        bool screenValid = screen.ValidateConfiguration(out screenError);
        if (!trackingValid || !facadeValid || !screenValid)
        {
            Finish(false, "8G: configuración inválida. Tracking=" + trackingError +
                " | Facade=" + facadeError + " | Screen=" + screenError, commandLine);
            return;
        }

        CaptureOriginalState();
        string repError = string.Empty;
        string runtimeError = string.Empty;
        string marketingError = string.Empty;
        bool reputationReset = reputation.TryRestoreSnapshot(
            BistroBuilderReputationEngine.CreateInitialSnapshot(), out repError);
        bool runtimeReset = tracking.TryResetRuntimeForLegacyLoad(out runtimeError);
        bool marketingReset = marketing.TryRestoreSnapshot(
            BistroBuilderMarketingEngine.CreateEmptySnapshot(), out marketingError);
        if (!reputationReset || !runtimeReset || !marketingReset)
        {
            Finish(false, "8G: no pudo aislar el fixture. Reputation=" + repError +
                " | Runtime=" + runtimeError + " | Marketing=" + marketingError,
                commandLine);
            return;
        }

        testTable = CreateFixtureTable();
        testWaiter = CreateFixtureWaiter();
        if (testTable == null || testWaiter == null || !testWaiter.IsAvailable)
        {
            Finish(false, "8G: no pudo preparar mesa/camarero aislados del fixture.",
                commandLine);
            return;
        }

        int groupId = ResolveDiagnosticGroupId();
        GameObject groupObject = new GameObject("BB_Reputation_8G_TestGroup");
        testGroup = groupObject.AddComponent<CustomerGroup>();
        BistroBuilderCustomerAcquisitionTag tag =
            groupObject.AddComponent<BistroBuilderCustomerAcquisitionTag>();
        var acquisition = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        acquisition.sourceSystemId = "reputation.word_of_mouth";
        acquisition.sourceReferenceId = "reputation.8g.functional";
        acquisition.discoverySourceId = "word_of_mouth";
        string tagError = string.Empty;
        bool groupInitialized = testGroup.Initialize(groupId, 1);
        bool tagConfigured = groupInitialized && tag.TryConfigure(acquisition, out tagError);
        bool groupRegistered = tagConfigured && tables.RegisterCustomerGroup(testGroup);
        if (!groupInitialized || !tagConfigured || !groupRegistered)
        {
            Finish(false, "8G: no pudo crear el CustomerGroup funcional. " + tagError,
                commandLine);
            return;
        }

        testGroup.SetState(CustomerGroupState.WaitingForWaiter);
        deadline = EditorApplication.timeSinceStartup + 0.18d;
        SessionState.SetString(StageKey,
            commandLine ? "wait_waiter_cli" : "wait_waiter_menu");
    }

    private static void CreateOrderAndStartFoodWait(bool commandLine)
    {
        if (testGroup == null || testTable == null || testWaiter == null ||
            !testGroup.AssignTable(testTable))
        {
            Finish(false, "8G: la mesa real no pudo asignarse al grupo.", commandLine);
            return;
        }
        testTable.SetState(TableState.WaitingForWaiter);
        if (!testWaiter.AssignTable(testTable))
        {
            Finish(false, "8G: el camarero real no pudo asignarse a la mesa.", commandLine);
            return;
        }

        testGroup.SetState(CustomerGroupState.Ordering);
        testOrder = orders.CreateOrder(testTable, testWaiter);
        if (testOrder == null || !testOrder.HasCanonicalOrder)
        {
            Finish(false, "8G: OrderSystem no creó una comanda canónica real.", commandLine);
            return;
        }

        BistroBuilderReputationRuntimeSnapshot runtime = tracking.CreateRuntimeSnapshot();
        if (runtime == null || runtime.visits.Count != 1 ||
            runtime.visits[0].waiterWaitSeconds <= 0f ||
            !string.Equals(runtime.visits[0].canonicalOrderId,
                testOrder.CanonicalOrderId, StringComparison.Ordinal))
        {
            Finish(false, "8G: Experience Tracking no capturó espera y comanda reales.",
                commandLine);
            return;
        }

        BistroBuilderReputationRuntimeSnapshot roundTrip = runtime.DeepClone();
        if (!tracking.TryRestoreRuntimeSnapshot(roundTrip, out string restoreError))
        {
            Finish(false, "8G: reputation.runtime no restauró la visita activa. " +
                restoreError, commandLine);
            return;
        }

        testGroup.SetState(CustomerGroupState.WaitingForFood);
        deadline = EditorApplication.timeSinceStartup + 0.22d;
        SessionState.SetString(StageKey,
            commandLine ? "wait_food_cli" : "wait_food_menu");
    }

    private static void StartBillWait(bool commandLine)
    {
        if (testGroup == null || testOrder == null)
        {
            Finish(false, "8G: el fixture perdió grupo o comanda.", commandLine);
            return;
        }
        testGroup.SetState(CustomerGroupState.WaitingForBill);
        deadline = EditorApplication.timeSinceStartup + 0.14d;
        SessionState.SetString(StageKey,
            commandLine ? "wait_bill_cli" : "wait_bill_menu");
    }

    private static void CompletePaidVisit(bool commandLine)
    {
        if (!testOrder.TrySetState(OrderState.SentToKitchen))
        {
            Finish(false, "8G: la comanda no pudo enviarse a cocina. " +
                testOrder.LastTransitionError, commandLine);
            return;
        }

        BistroBuilderCanonicalOrderOperationResult advance =
            canonical.TryAdvanceAllLinesToState(
                testOrder.CanonicalOrderId,
                BistroBuilderCanonicalOrderLineState.Served,
                "reputation_8g_test");
        string synchronizationError = string.Empty;
        bool synchronized = advance.Succeeded &&
            lineExecution.TrySynchronizeLegacyOrder(
                testOrder, out _, out _, out synchronizationError);
        if (!synchronized || testOrder.CurrentState != OrderState.Served)
        {
            Finish(false, "8G: la comanda real no alcanzó Served. " +
                advance.Message + " " + synchronizationError, commandLine);
            return;
        }

        int financeBefore = finance.TransactionCount;
        if (!orders.CompleteOrder(testOrder))
        {
            Finish(false, "8G: OrderSystem no completó la comanda real.", commandLine);
            return;
        }

        if (finance.TransactionCount != financeBefore + 1 ||
            reputation.TotalExperiences != 1 || reputation.ReviewCount != 1 ||
            reputation.WordOfMouthBasisPoints <= 0 ||
            reputation.WordOfMouthDiscoveries != 1 ||
            tracking.ActiveVisitCount != 0 ||
            tracking.LastRecordedSatisfactionBasisPoints <= 5000)
        {
            Finish(false,
                "8G: el cobro real no produjo exactamente una experiencia, reseña " +
                "y boca a boca positivos. Finance=" + finance.TransactionCount +
                " | Exp=" + reputation.TotalExperiences +
                " | Reviews=" + reputation.ReviewCount +
                " | WOM=" + reputation.WordOfMouthBasisPoints +
                " | Discovery=" + reputation.WordOfMouthDiscoveries +
                " | Active=" + tracking.ActiveVisitCount +
                " | Satisfaction=" + tracking.LastRecordedSatisfactionBasisPoints + ".",
                commandLine);
            return;
        }

        if (!facade.TryBuildSnapshot(
                out BistroBuilderReputationPlayerUiSnapshot ui,
                out string uiError) || ui == null || ui.reviewCount != 1 ||
            ui.totalExperiences != 1 || ui.wordOfMouthDiscoveries != 1)
        {
            Finish(false, "8G: la UI no refleja la experiencia real. " + uiError,
                commandLine);
            return;
        }

        screen.Show();
        if (!screen.IsVisible)
        {
            Finish(false, "8G: la pantalla jugable no puede abrirse.", commandLine);
            return;
        }
        screen.Hide();

        Finish(true,
            "PASS — visita real medida: esperas -> comanda canónica -> cobro Finance -> " +
            "satisfacción -> reputación por aspectos -> reseña -> boca a boca -> UI.",
            commandLine);
    }

    private static void CaptureOriginalState()
    {
        originalReputation = reputation.CreateSnapshot();
        originalRuntime = tracking.CreateRuntimeSnapshot();
        originalRelations = relations.CreateSnapshot();
        originalFinance = finance.CreateSnapshot();
        originalMarketing = marketing.CreateSnapshot();
        originalGameId = general.GameId;
        originalRestaurantName = general.RestaurantName;
        originalCreatedUtc = general.CreatedUtc;
        originalDayIndex = general.DayIndex;
        originalYear = general.CalendarYear;
        originalMonth = general.CalendarMonth;
        originalDay = general.CalendarDay;
        originalProgressionStage = general.ProgressionStageId;
        originalProgressionLevel = general.ProgressionLevel;
    }

    private static bool RestoreOriginalState(out string error)
    {
        error = string.Empty;
        CleanupFixtureObjects();
        if (originalMarketing != null &&
            !marketing.TryRestoreSnapshot(originalMarketing, out error)) return false;
        if (originalFinance != null &&
            !finance.TryRestoreSnapshot(originalFinance, out error)) return false;
        if (originalRelations != null &&
            !relations.TryRestoreSnapshot(originalRelations, out error)) return false;
        if (originalReputation != null &&
            !reputation.TryRestoreSnapshot(originalReputation, out error)) return false;
        if (originalRuntime != null &&
            !tracking.TryRestoreRuntimeSnapshot(originalRuntime, out error)) return false;
        if (!general.TryRestoreState(
                originalGameId, originalRestaurantName, originalCreatedUtc,
                originalDayIndex, originalYear, originalMonth, originalDay,
                originalProgressionStage, Math.Max(1, originalProgressionLevel)))
        {
            error = "No pudo restaurarse el estado general.";
            return false;
        }
        return true;
    }

    private static void CleanupFixtureObjects()
    {
        if (testWaiter != null) testWaiter.ClearAssignment();
        if (testGroup != null)
        {
            tables?.UnregisterCustomerGroup(testGroup);
            if (testGroup.HasAssignedTable) testGroup.ClearAssignedTable();
            UnityEngine.Object.DestroyImmediate(testGroup.gameObject);
        }
        if (testWaiter != null)
            UnityEngine.Object.DestroyImmediate(testWaiter.gameObject);
        if (testTable != null)
            UnityEngine.Object.DestroyImmediate(testTable.gameObject);
        testGroup = null; testTable = null; testWaiter = null; testOrder = null;
    }

    private static RestaurantTable CreateFixtureTable()
    {
        GameObject owner = new GameObject("BB_Reputation_8G_TestTable");
        RestaurantTable table = owner.AddComponent<RestaurantTable>();
        RestaurantTable[] existing = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int maxId = 900000;
        for (int i = 0; i < existing.Length; i++)
            if (existing[i] != null && !ReferenceEquals(existing[i], table))
                maxId = Math.Max(maxId, existing[i].TableId);
        table.AssignTableId(maxId + 1);
        table.SetState(TableState.Free);
        return table;
    }

    private static Waiter CreateFixtureWaiter()
    {
        GameObject owner = new GameObject("BB_Reputation_8G_TestWaiter");
        Waiter waiter = owner.AddComponent<Waiter>();
        Waiter[] existing = UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int maxId = 900000;
        for (int i = 0; i < existing.Length; i++)
            if (existing[i] != null && !ReferenceEquals(existing[i], waiter))
                maxId = Math.Max(maxId, existing[i].WaiterId);
        var serialized = new SerializedObject(waiter);
        SerializedProperty waiterId = serialized.FindProperty("waiterId");
        if (waiterId != null)
        {
            waiterId.intValue = maxId + 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        waiter.TrySetStaffServiceEligibility(true);
        waiter.ClearAssignment();
        return waiter;
    }

    private static RestaurantTable FindFreeTable()
    {
        RestaurantTable[] values = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        RestaurantTable best = null;
        for (int i = 0; i < values.Length; i++)
        {
            RestaurantTable table = values[i];
            if (table == null || table.CurrentState != TableState.Free ||
                table.AssignedCustomerGroup != null || table.Capacity < 1) continue;
            if (best == null || table.TableId < best.TableId) best = table;
        }
        return best;
    }

    private static Waiter FindAvailableWaiter()
    {
        Waiter[] values = UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < values.Length; i++)
            if (values[i] != null && values[i].isActiveAndEnabled && values[i].IsAvailable)
                return values[i];
        return null;
    }

    private static int ResolveDiagnosticGroupId()
    {
        int max = 900000;
        if (tables != null)
            foreach (CustomerGroup group in tables.RegisteredGroups)
                if (group != null) max = Math.Max(max, group.GroupId);
        return max + 1;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (originalReputation != null &&
            !RestoreOriginalState(out string restoreError))
        {
            success = false;
            message += " Rollback funcional falló: " + restoreError;
        }

        string report = "=== BISTRO BUILDER — REPUTACIÓN BLOQUE 8 PLAY MODE ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        ClearReferences();
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void ClearReferences()
    {
        originalReputation = null; originalRuntime = null; originalRelations = null;
        originalFinance = null; originalMarketing = null;
        reputation = null; tracking = null; relations = null; finance = null;
        marketing = null; canonical = null; facade = null; screen = null;
        general = null; tables = null; orders = null;
        testGroup = null; testTable = null; testWaiter = null; testOrder = null;
    }

    private static T Find<T>() where T : UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
}
