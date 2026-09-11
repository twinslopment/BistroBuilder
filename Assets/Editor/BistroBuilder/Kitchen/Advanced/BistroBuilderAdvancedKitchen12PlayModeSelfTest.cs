using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedKitchen12PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Kitchen12.Play.Stage";
    private const string SuccessKey = "BB.Kitchen12.Play.Success";
    private const string ReportPath = "AdvancedKitchen12PlayModeReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;

    private static BistroBuilderAdvancedKitchenService advanced;
    private static KitchenSystem kitchen;
    private static BistroBuilderCanonicalOrderService canonical;
    private static OrderSystem orderSystem;
    private static BistroBuilderOrderInventoryLifecycleService lifecycle;
    private static BistroBuilderInventoryService inventory;
    private static BistroBuilderStaffService staff;
    private static BistroBuilderAdvancedKitchenPlayerFacade facade;
    private static BistroBuilderAdvancedKitchenPlayerScreen screen;
    private static BistroBuilderCanonicalOrderRuntimeSnapshot originalOrders;
    private static BistroBuilderInventoryRuntimeSnapshot originalInventory;
    private static BistroBuilderStaffSnapshot originalStaff;
    private static BistroBuilderKitchenRuntimeSnapshot originalKitchen;
    private static GameObject tableGo;
    private static GameObject groupGo;
    private static GameObject waiterGo;
    private static RestaurantOrder legacy;
    private static List<RestaurantOrder> activeOrders;
    private static readonly List<string> lineIds = new List<string>(4);
    private static string stationId = string.Empty;
    private static double phaseStarted;

    static BistroBuilderAdvancedKitchen12PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Kitchen/12 - PlayMode real", false, 9122)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 12 ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        if (cli)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
            EditorApplication.Step();
        }
        if (playReadyAt <= 0d) playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt) return;
        try
        {
            if (stage.StartsWith("run_", StringComparison.Ordinal))
            {
                SetupFixture();
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "blocked_cli" : "blocked_menu");
                return;
            }
            if (stage.StartsWith("blocked_", StringComparison.Ordinal))
            {
                if (EditorApplication.timeSinceStartup - phaseStarted < 0.25d) return;
                VerifyBlockedState();
                if (!facade.SetIntakeMode(BistroBuilderKitchenIntakeMode.Normal, out string error))
                    throw new InvalidOperationException("No pudo reanudarse la cocina: " + error);
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "active_cli" : "active_menu");
                return;
            }
            if (stage.StartsWith("active_", StringComparison.Ordinal))
            {
                if (advanced.ActiveCount == 0)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 6d)
                        throw new TimeoutException("La cocina no inició trabajo tras reparar la estación.");
                    return;
                }
                VerifyActiveState();
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "complete_cli" : "complete_menu");
                return;
            }
            if (stage.StartsWith("complete_", StringComparison.Ordinal))
            {
                if (advanced.CompletedLineCount == 0)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 10d)
                        throw new TimeoutException("Ninguna preparación completó el flujo de estaciones.");
                    return;
                }
                VerifyCompletedState();
                Finish(true,
                    "PASS — estaciones, capacidad, colas, prioridad limitada, bloqueo por avería, " +
                    "cocinero, calidad, UI y flujo real comanda-inventario-cocina son coherentes.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "12 PlayMode: " + Unwrap(exception).Message, cli);
        }
    }
    private static void SetupFixture()
    {
        advanced = Find<BistroBuilderAdvancedKitchenService>();
        kitchen = Find<KitchenSystem>();
        canonical = Find<BistroBuilderCanonicalOrderService>();
        orderSystem = Find<OrderSystem>();
        lifecycle = Find<BistroBuilderOrderInventoryLifecycleService>();
        inventory = Find<BistroBuilderInventoryService>();
        staff = Find<BistroBuilderStaffService>();
        facade = Find<BistroBuilderAdvancedKitchenPlayerFacade>();
        screen = Find<BistroBuilderAdvancedKitchenPlayerScreen>();
        var menu = Find<BistroBuilderRestaurantMenuService>();
        var recipes = Find<BistroBuilderRecipeCatalogService>();
        var catalog = AssetDatabase.LoadAssetAtPath<BistroBuilderKitchenStationCatalog>(
            "Assets/Data/Kitchen/BB_Kitchen_Station_Catalog.asset");

        if (advanced == null || kitchen == null || canonical == null || orderSystem == null ||
            lifecycle == null || inventory == null || staff == null || facade == null ||
            screen == null || menu == null || recipes == null || catalog == null)
            throw new InvalidOperationException("Faltan autoridades runtime del Bloque 12.");
        string cfg = string.Empty;
        string facadeCfg = string.Empty;
        string screenCfg = string.Empty;
        bool advancedOk = advanced.ValidateConfiguration(out cfg);
        bool facadeOk = facade.ValidateConfiguration(out facadeCfg);
        bool screenOk = screen.ValidateConfiguration(out screenCfg);
        if (!advancedOk || !facadeOk || !screenOk)
            throw new InvalidOperationException("Configuración 12 inválida: " + cfg + " " + facadeCfg + " " + screenCfg);
        if (orderSystem.ActiveOrders.Count != 0)
            throw new InvalidOperationException("El fixture 12 exige iniciar sin comandas activas.");

        if (!canonical.TryCaptureRuntimeSnapshot(out originalOrders, out string orderError))
            throw new InvalidOperationException(orderError);
        if (!inventory.TryCaptureRuntimeSnapshot(out originalInventory, out string inventoryError))
            throw new InvalidOperationException(inventoryError);
        originalStaff = staff.CreateSnapshot();
        if (!kitchen.TryCaptureRuntimeSnapshot(out originalKitchen, out string kitchenError))
            throw new InvalidOperationException(kitchenError);

        SetPrivate(kitchen, "preparationDurationScale", 1f);
        SetPrivate(kitchen, "minimumPreparationDuration", 4f);
        SetPrivate(kitchen, "maximumPreparationDuration", 4f);
        SetPrivate(advanced, "automaticIncidents", false);
        SetPrivate(advanced, "equipmentRepairSeconds", 2f);

        if (!staff.TryCreateEmployee(
                new BistroBuilderEmployeeCreateRequest
                {
                    firstName = "Carmen",
                    lastName = "Test Cocina",
                    roleId = "cook",
                    salaryCentsPerService = 5000,
                    hiredDayIndex = 1,
                    initialExperiencePoints = 5000,
                    initialSkills = new BistroBuilderEmployeeSkillSet
                    {
                        speed = 80,
                        attentiveness = 85,
                        organization = 90,
                        hospitality = 50
                    }
                },
                out _,
                out string cookError))
            throw new InvalidOperationException("No pudo crear el cocinero temporal: " + cookError);

        var stocks = new List<BistroBuilderInventoryStockSnapshot>();
        inventory.CopyStockSnapshotsTo(stocks);
        for (int i = 0; i < stocks.Count; i++)
        {
            long target = Math.Max(stocks[i].OnHandCanonicalMilliUnits, 1000000000L);
            if (!inventory.TryCorrectOnHand(
                    "block12_play_topup_" + i, "block12_play", stocks[i].IngredientId,
                    target, "Fixture temporal Cocina 12.", out string topupError))
                throw new InvalidOperationException("No pudo preparar stock: " + topupError);
        }
        BistroBuilderMenuItemRuntimeState item = FindSingleStageDish(
            menu, recipes, canonical.OfferService, catalog);
        if (item == null)
            throw new InvalidOperationException("No existe un plato orderable de una estación para el fixture 12.");

        var request = new BistroBuilderCanonicalOrderCreationRequest
        {
            externalReferenceId =
                BistroBuilderServiceOrderIdentityUtility.BuildLegacyOrderReference(9912),
            tableReferenceId =
                BistroBuilderServiceOrderIdentityUtility.BuildTableReference(9912),
            customerGroupReferenceId =
                BistroBuilderServiceOrderIdentityUtility.BuildGroupReference(9912),
            serviceMode = BistroBuilderServiceMode.TableService,
            mealService = BistroBuilderMealServiceAvailability.Lunch
        };
        for (int i = 0; i < 4; i++)
        {
            string customer =
                BistroBuilderServiceOrderIdentityUtility.BuildCustomerReference(9912, i + 1);
            request.lines.Add(new BistroBuilderCanonicalOrderLineRequest(
                item.DishId, customer, new[] { customer }, 0));
        }
        if (!BistroBuilderCanonicalOrderFactory.TryCreate(
                request, new FixedResolver(item), originalOrders.NextSequenceNumber,
                out BistroBuilderCanonicalOrder fixtureOrder,
                out BistroBuilderCanonicalOrderOperationResult factoryResult) ||
            fixtureOrder == null || !factoryResult.Succeeded)
            throw new InvalidOperationException("No se pudo crear la comanda fixture 12: " + factoryResult.Message);

        var combined = new List<BistroBuilderCanonicalOrder>();
        for (int i = 0; i < originalOrders.Orders.Count; i++) combined.Add(originalOrders.Orders[i]);
        combined.Add(fixtureOrder);
        if (!canonical.TryReplaceFromRuntimeSnapshot(
                new BistroBuilderCanonicalOrderRuntimeSnapshot(
                    originalOrders.NextSequenceNumber + 1L, combined),
                true, out string replaceError))
            throw new InvalidOperationException("No pudo instalar la comanda 12: " + replaceError);
        tableGo = new GameObject("__BB12_PLAY_TABLE__");
        groupGo = new GameObject("__BB12_PLAY_GROUP__");
        waiterGo = new GameObject("__BB12_PLAY_WAITER__");
        var table = tableGo.AddComponent<RestaurantTable>();
        table.AssignTableId(9912);
        var group = groupGo.AddComponent<CustomerGroup>();
        if (!group.Initialize(9912, 4))
            throw new InvalidOperationException("No pudo crear CustomerGroup 12.");
        var waiter = waiterGo.AddComponent<Waiter>();
        legacy = new RestaurantOrder(
            9912, table, group, waiter, fixtureOrder.OrderId,
            orderSystem.CanonicalIntegrationService);

        FieldInfo activeField = typeof(OrderSystem).GetField(
            "activeOrders", BindingFlags.Instance | BindingFlags.NonPublic);
        activeOrders = activeField?.GetValue(orderSystem) as List<RestaurantOrder>;
        if (activeOrders == null)
            throw new InvalidOperationException("No pudo accederse a activeOrders para el fixture 12.");
        activeOrders.Add(legacy);

        if (!orderSystem.CanonicalIntegrationService.TryRegisterLegacyOrder(
                legacy,
                out string linkError))
            throw new InvalidOperationException(
                "No pudo registrar el enlace legacy-canónico 12: " + linkError);
        InvokePrivate(lifecycle, "HandleOrderCreated", legacy);
        InvokePrivate(kitchen, "HandleOrderCreated", legacy);
        if (!facade.SetIntakeMode(BistroBuilderKitchenIntakeMode.Paused, out string pauseError))
            throw new InvalidOperationException("No pudo pausarse la entrada: " + pauseError);

        lineIds.Clear();
        for (int i = 0; i < fixtureOrder.Lines.Count; i++)
        {
            string lineId = fixtureOrder.Lines[i].LineId;
            lineIds.Add(lineId);
            Advance(lineId, BistroBuilderCanonicalOrderLineState.Submitted);
            Advance(lineId, BistroBuilderCanonicalOrderLineState.Queued);
        }
        if (!legacy.TrySetState(OrderState.SentToKitchen))
            throw new InvalidOperationException("La comanda legacy no entró en cocina: " + legacy.LastTransitionError);
        if (!facade.TryBuildSnapshot(out BistroBuilderAdvancedKitchenSnapshot queued, out string queuedError))
            throw new InvalidOperationException(queuedError);
        if (queued.activeCount != 0 || queued.queuedCount != 4)
            throw new InvalidOperationException(
                "La cola pausada no conserva las cuatro preparaciones: " +
                queued.activeCount + " activas / " + queued.queuedCount + " en espera.");

        stationId = FindStationForLine(queued, lineIds[0]);
        if (string.IsNullOrWhiteSpace(stationId))
            throw new InvalidOperationException("La línea no aparece en una estación real.");
        if (!facade.Prioritize(lineIds[0], out string priorityError))
            throw new InvalidOperationException("No pudo priorizarse una línea en espera: " + priorityError);
        if (facade.Prioritize(lineIds[1], out _))
            throw new InvalidOperationException("La estación aceptó dos prioridades manuales simultáneas.");
        if (!facade.SetIntakeMode(BistroBuilderKitchenIntakeMode.Reduced, out string reducedError) ||
            advanced.IntakeMode != BistroBuilderKitchenIntakeMode.Reduced)
            throw new InvalidOperationException("El modo reducido no quedó operativo: " + reducedError);
        if (!facade.SetIntakeMode(BistroBuilderKitchenIntakeMode.Paused, out pauseError))
            throw new InvalidOperationException(pauseError);
        if (!advanced.TryForceIncident(
                stationId, BistroBuilderKitchenIncidentKind.EquipmentFailure,
                out string incidentError))
            throw new InvalidOperationException("No pudo forzarse la avería diagnóstica: " + incidentError);

        screen.Show();
        if (!screen.IsOpen)
            throw new InvalidOperationException("La pantalla operativa de cocina no se abre.");
    }

    private static void VerifyBlockedState()
    {
        if (!facade.TryBuildSnapshot(out BistroBuilderAdvancedKitchenSnapshot snapshot, out string error))
            throw new InvalidOperationException(error);
        BistroBuilderKitchenStationSnapshot station = FindStation(snapshot, stationId);
        if (snapshot.loadState != BistroBuilderKitchenLoadState.Blocked ||
            station == null || station.blockedSeconds <= 0f || station.queuedCount == 0)
            throw new InvalidOperationException("La avería con cola pendiente no produjo estado Bloqueada.");
    }
    private static void VerifyActiveState()
    {
        if (!facade.TryBuildSnapshot(out BistroBuilderAdvancedKitchenSnapshot snapshot, out string error))
            throw new InvalidOperationException(error);
        BistroBuilderKitchenTaskSnapshot activeTask = null;
        for (int i = 0; i < snapshot.stations.Count && activeTask == null; i++)
            for (int j = 0; j < snapshot.stations[i].tasks.Count; j++)
                if (snapshot.stations[i].tasks[j].active)
                {
                    activeTask = snapshot.stations[i].tasks[j];
                    break;
                }
        if (activeTask == null || activeTask.remainingSeconds <= 0f)
            throw new InvalidOperationException("No existe una preparación activa con tiempo restante.");
        if (!canonical.TryGetOrderSnapshot(
                legacy.CanonicalOrderId, out BistroBuilderCanonicalOrder order) || order == null ||
            !TryFindLine(order, activeTask.lineId, out BistroBuilderCanonicalOrderLine line) ||
            line.State != BistroBuilderCanonicalOrderLineState.Preparing)
            throw new InvalidOperationException("La preparación activa no está sincronizada con la comanda canónica.");
        if (!lifecycle.TryGetCanonicalLineReservation(
                legacy, activeTask.lineId, out BistroBuilderInventoryReservationSnapshot reservation) ||
            reservation == null || reservation.Status != BistroBuilderInventoryReservationStatus.Consumed)
            throw new InvalidOperationException("La preparación activa no consumió su reserva de inventario.");
        if (string.IsNullOrWhiteSpace(activeTask.cookEmployeeId) ||
            !staff.TryGetEmployee(activeTask.cookEmployeeId, out BistroBuilderEmployeeRecord cook) ||
            cook == null || !string.Equals(cook.roleId, "cook", StringComparison.Ordinal))
            throw new InvalidOperationException("La tarea activa no tiene un cocinero persistente válido asignado.");

        if (!kitchen.TryCaptureRuntimeSnapshot(out BistroBuilderKitchenRuntimeSnapshot runtime, out string runtimeError))
            throw new InvalidOperationException(runtimeError);
        bool hasActiveAdvanced = false;
        for (int i = 0; i < runtime.workItems.Count; i++)
        {
            BistroBuilderKitchenLineWorkSaveData item = runtime.workItems[i];
            if (item != null && item.advanced && item.wasActive &&
                !string.IsNullOrWhiteSpace(item.stationId)) hasActiveAdvanced = true;
        }
        if (!runtime.advancedEnabled || !hasActiveAdvanced || runtime.stationStates.Count == 0)
            throw new InvalidOperationException("service.runtime no captura la preparación avanzada activa.");
    }
    private static void VerifyCompletedState()
    {
        int completedWithQuality = 0;
        for (int i = 0; i < lineIds.Count; i++)
        {
            if (!advanced.TryGetLineQualityBasisPoints(lineIds[i], out int quality)) continue;
            if (quality < 2500 || quality > 10000)
                throw new InvalidOperationException("La calidad final quedó fuera de rango.");
            completedWithQuality++;
        }
        if (completedWithQuality == 0)
            throw new InvalidOperationException("Una preparación completada no publicó calidad final.");
        if (!facade.TryBuildSnapshot(out BistroBuilderAdvancedKitchenSnapshot snapshot, out string error))
            throw new InvalidOperationException(error);
        if (snapshot.completedLineCount < 1 ||
            snapshot.averageRecentQualityBasisPoints < 2500 ||
            snapshot.averageRecentQualityBasisPoints > 10000)
            throw new InvalidOperationException("El resumen operativo no refleja calidad/completados reales.");
        screen.Refresh();
        if (!screen.IsOpen)
            throw new InvalidOperationException("La UI dejó de estar operativa durante la producción.");
    }

    private static BistroBuilderMenuItemRuntimeState FindSingleStageDish(
        BistroBuilderRestaurantMenuService menu,
        BistroBuilderRecipeCatalogService recipes,
        BistroBuilderMenuOfferService offer,
        BistroBuilderKitchenStationCatalog catalog)
    {
        var items = new List<BistroBuilderMenuItemRuntimeState>();
        var route = new List<string>();
        if (!menu.TryGetSnapshot(items, out _)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            BistroBuilderMenuItemRuntimeState item = items[i];
            if (item == null || item.CurrentPriceCents <= 0 || !item.Enabled || !item.Unlocked ||
                item.ManuallySoldOut ||
                (item.AvailableServices & BistroBuilderMealServiceAvailability.Lunch) == 0)
                continue;
            if (!recipes.TryGetRecipeByDishId(item.DishId, out BistroBuilderRecipeDefinition recipe) || recipe == null)
                continue;
            if (offer != null && !offer.IsDishOrderable(
                    item.DishId, BistroBuilderMealServiceAvailability.Lunch,
                    BistroBuilderServiceMode.TableService, out _, out _))
                continue;
            if (catalog.TryResolveRoute(item.DishId, route) && route.Count == 1)
                return item;
        }
        return null;
    }
    private static string FindStationForLine(
        BistroBuilderAdvancedKitchenSnapshot snapshot,
        string lineId)
    {
        for (int i = 0; i < snapshot.stations.Count; i++)
            for (int j = 0; j < snapshot.stations[i].tasks.Count; j++)
                if (string.Equals(
                        snapshot.stations[i].tasks[j].lineId,
                        lineId,
                        StringComparison.Ordinal))
                    return snapshot.stations[i].stationId;
        return string.Empty;
    }

    private static BistroBuilderKitchenStationSnapshot FindStation(
        BistroBuilderAdvancedKitchenSnapshot snapshot,
        string id)
    {
        for (int i = 0; i < snapshot.stations.Count; i++)
            if (string.Equals(snapshot.stations[i].stationId, id, StringComparison.Ordinal))
                return snapshot.stations[i];
        return null;
    }

    private static bool TryFindLine(
        BistroBuilderCanonicalOrder order,
        string lineId,
        out BistroBuilderCanonicalOrderLine line)
    {
        line = null;
        if (order == null) return false;
        for (int i = 0; i < order.Lines.Count; i++)
            if (order.Lines[i] != null &&
                string.Equals(order.Lines[i].LineId, lineId, StringComparison.Ordinal))
            {
                line = order.Lines[i];
                return true;
            }
        return false;
    }
    private static void Advance(
        string lineId,
        BistroBuilderCanonicalOrderLineState target)
    {
        BistroBuilderCanonicalOrderOperationResult result =
            canonical.TryTransitionLine(lineId, target, "play12");
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Transición " + target + " rechazada: " + result.Message);
    }

    private static void InvokePrivate(object target, string name, object argument)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, name);
        method.Invoke(target, new[] { argument });
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException tie && tie.InnerException != null)
            exception = tie.InnerException;
        return exception;
    }

    private static T Find<T>() where T : UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    private static void Finish(bool success, string message, bool cli)
    {
        try { CleanupFixture(); }
        catch (Exception cleanup) { Debug.LogException(cleanup); }
        string report = "=== BISTRO BUILDER — BLOQUE 12 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void CleanupFixture()
    {
        screen?.Hide();
        advanced?.ClearRuntimeForLoad();
        lifecycle?.ClearRuntimeForLoad();
        if (activeOrders != null && legacy != null) activeOrders.Remove(legacy);
        if (canonical != null && originalOrders != null)
            canonical.TryReplaceFromRuntimeSnapshot(originalOrders, true, out _);
        if (inventory != null && originalInventory != null)
            inventory.TryReplaceFromRuntimeSnapshot(originalInventory, true, out _);
        if (staff != null && originalStaff != null)
            staff.TryRestoreSnapshot(originalStaff, out _);
        if (kitchen != null && originalKitchen != null)
            kitchen.TryReplaceFromRuntimeSnapshot(
                originalKitchen,
                new Dictionary<string, RestaurantOrder>(StringComparer.Ordinal),
                out _);
        advanced?.TrySetIntakeMode(BistroBuilderKitchenIntakeMode.Normal, out _);
        if (tableGo != null) UnityEngine.Object.Destroy(tableGo);
        if (groupGo != null) UnityEngine.Object.Destroy(groupGo);
        if (waiterGo != null) UnityEngine.Object.Destroy(waiterGo);
        lineIds.Clear();
    }
    private sealed class FixedResolver : IBistroBuilderOrderDishResolver
    {
        private readonly BistroBuilderMenuItemRuntimeState item;
        public FixedResolver(BistroBuilderMenuItemRuntimeState item) => this.item = item;

        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish dish,
            out string rejectionReason)
        {
            dish = new BistroBuilderResolvedOrderDish(
                item.DishId,
                item.CurrentPriceCents,
                item.DisplayOrder,
                item.SignatureDish,
                string.Empty,
                0);
            rejectionReason = string.Empty;
            return string.Equals(dishId, item.DishId, StringComparison.Ordinal);
        }
    }
}
