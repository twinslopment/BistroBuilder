using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedOrders11PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Orders11.Play.Stage";
    private const string SuccessKey = "BB.Orders11.Play.Success";
    private const string ReportPath = "AdvancedOrders11PlayModeReport.txt";

    static BistroBuilderAdvancedOrders11PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Orders/11 - PlayMode real", false, 9103)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 11 ya está ejecutándose.");
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
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        SessionState.SetString(StageKey, cli ? "running_cli" : "running_menu");
        RunRuntime(cli);
    }

    private static void RunRuntime(bool cli)
    {
        BistroBuilderCanonicalOrderRuntimeSnapshot originalOrders = null;
        BistroBuilderInventoryRuntimeSnapshot originalInventory = null;
        BistroBuilderFinanceSnapshot originalFinance = null;
        GameObject tableGo = null, groupGo = null, waiterGo = null;
        RestaurantOrder legacy = null;
        List<RestaurantOrder> activeOrders = null;
        try
        {
            var advanced = Find<BistroBuilderAdvancedOrderService>();
            var canonical = Find<BistroBuilderCanonicalOrderService>();
            var orderSystem = Find<OrderSystem>();
            var lifecycle = Find<BistroBuilderOrderInventoryLifecycleService>();
            var inventory = Find<BistroBuilderInventoryService>();
            var menu = Find<BistroBuilderRestaurantMenuService>();
            var recipes = Find<BistroBuilderRecipeCatalogService>();
            var facade = Find<BistroBuilderAdvancedOrderPlayerFacade>();
            var screen = Find<BistroBuilderAdvancedOrderPlayerScreen>();
            var finance = Find<BistroBuilderFinanceService>();
            var salesBridge = Find<BistroBuilderSalesRevenueBridge>();
            if (advanced == null || canonical == null || orderSystem == null || lifecycle == null ||
                inventory == null || menu == null || recipes == null || facade == null || screen == null ||
                finance == null || salesBridge == null)
                throw new InvalidOperationException("Faltan autoridades runtime del bloque 11.");
            string cfg = string.Empty, facadeCfg = string.Empty, screenCfg = string.Empty;
            bool advancedOk = advanced.ValidateConfiguration(out cfg);
            bool facadeOk = facade.ValidateConfiguration(out facadeCfg);
            bool screenOk = screen.ValidateConfiguration(out screenCfg);
            if (!advancedOk || !facadeOk || !screenOk)
                throw new InvalidOperationException("Configuración 11 inválida: " + cfg + " " + facadeCfg + " " + screenCfg);
            if (orderSystem.ActiveOrders.Count != 0)
                throw new InvalidOperationException("El fixture 11 exige iniciar sin comandas activas.");
            if (!canonical.TryCaptureRuntimeSnapshot(out originalOrders, out string orderSnapError))
                throw new InvalidOperationException(orderSnapError);
            if (!inventory.TryCaptureRuntimeSnapshot(out originalInventory, out string invSnapError))
                throw new InvalidOperationException(invSnapError);
            originalFinance = finance.CreateSnapshot();
            string salesCfg = string.Empty;
            bool salesOk = salesBridge.ValidateConfiguration(out salesCfg);
            if (originalFinance == null || !salesOk)
                throw new InvalidOperationException("Finanzas 3B no están disponibles para el cierre 11. " + salesCfg);

            var stocks = new List<BistroBuilderInventoryStockSnapshot>();
            inventory.CopyStockSnapshotsTo(stocks);
            for (int i = 0; i < stocks.Count; i++)
            {
                long target = Math.Max(stocks[i].OnHandCanonicalMilliUnits, 1000000000L);
                if (!inventory.TryCorrectOnHand(
                        "block11_play_topup_" + i,
                        "block11_play",
                        stocks[i].IngredientId,
                        target,
                        "Fixture temporal de PlayMode 11.",
                        out string topupError))
                    throw new InvalidOperationException("No pudo preparar stock: " + topupError);
            }

            BistroBuilderMenuItemRuntimeState item = FindOrderableDish(menu, recipes, canonical.OfferService);
            if (item == null)
                throw new InvalidOperationException("No existe un plato orderable con receta para el fixture 11.");

            var request = new BistroBuilderCanonicalOrderCreationRequest
            {
                externalReferenceId = "legacy_order_11_play",
                tableReferenceId = "table_11_play",
                customerGroupReferenceId = "group_11_play",
                serviceMode = BistroBuilderServiceMode.TableService,
                mealService = BistroBuilderMealServiceAvailability.Lunch
            };
            request.lines.Add(new BistroBuilderCanonicalOrderLineRequest(
                item.DishId, "customer_11_play", new[] { "customer_11_play" }, 0));
            var resolver = new FixedResolver(item);
            if (!BistroBuilderCanonicalOrderFactory.TryCreate(
                    request, resolver, originalOrders.NextSequenceNumber,
                    out BistroBuilderCanonicalOrder fixtureOrder,
                    out BistroBuilderCanonicalOrderOperationResult factoryResult) ||
                fixtureOrder == null || !factoryResult.Succeeded)
                throw new InvalidOperationException("No se pudo crear la comanda fixture 11: " + factoryResult.Message);

            var combined = new List<BistroBuilderCanonicalOrder>();
            for (int i = 0; i < originalOrders.Orders.Count; i++) combined.Add(originalOrders.Orders[i]);
            combined.Add(fixtureOrder);
            var fixtureSnapshot = new BistroBuilderCanonicalOrderRuntimeSnapshot(
                originalOrders.NextSequenceNumber + 1L, combined);
            if (!canonical.TryReplaceFromRuntimeSnapshot(fixtureSnapshot, true, out string replaceError))
                throw new InvalidOperationException("No se pudo instalar fixture canónico: " + replaceError);

            tableGo = new GameObject("__BB11_PLAY_TABLE__");
            groupGo = new GameObject("__BB11_PLAY_GROUP__");
            waiterGo = new GameObject("__BB11_PLAY_WAITER__");
            var table = tableGo.AddComponent<RestaurantTable>();
            table.AssignTableId(9911);
            var group = groupGo.AddComponent<CustomerGroup>();
            if (!group.Initialize(9911, 1)) throw new InvalidOperationException("No se pudo crear CustomerGroup 11.");
            var waiter = waiterGo.AddComponent<Waiter>();
            legacy = new RestaurantOrder(
                9911, table, group, waiter, fixtureOrder.OrderId,
                orderSystem.CanonicalIntegrationService);

            FieldInfo activeField = typeof(OrderSystem).GetField(
                "activeOrders", BindingFlags.Instance | BindingFlags.NonPublic);
            activeOrders = activeField != null
                ? activeField.GetValue(orderSystem) as List<RestaurantOrder> : null;
            if (activeOrders == null) throw new InvalidOperationException("No se pudo acceder a activeOrders para el fixture 11.");
            activeOrders.Add(legacy);

            MethodInfo createdHandler = typeof(BistroBuilderOrderInventoryLifecycleService).GetMethod(
                "HandleOrderCreated", BindingFlags.Instance | BindingFlags.NonPublic);
            if (createdHandler == null) throw new InvalidOperationException("No existe HandleOrderCreated 368CD.");
            createdHandler.Invoke(lifecycle, new object[] { legacy });

            string originalLineId = fixtureOrder.Lines[0].LineId;
            if (!lifecycle.TryGetCanonicalLineReservation(
                    legacy, originalLineId, out BistroBuilderInventoryReservationSnapshot originalReservation) ||
                originalReservation == null || originalReservation.Status != BistroBuilderInventoryReservationStatus.Active)
                throw new InvalidOperationException("La línea original no reservó inventario real.");

            int basePrice = fixtureOrder.Lines[0].PriceCentsAtOrder;
            BistroBuilderAdvancedOrderMutationResult corrected = advanced.TryCorrectLine(
                fixtureOrder.OrderId, originalLineId, item.DishId,
                BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
                "El cliente corrige el plato antes de cocina.", "play11");
            if (!corrected.succeeded || string.IsNullOrEmpty(corrected.newLineId))
                throw new InvalidOperationException("Corrección real falló: " + corrected.message);
            string correctedId = corrected.newLineId;
            if (!lifecycle.TryGetCanonicalLineReservation(legacy, originalLineId, out originalReservation) ||
                originalReservation.Status != BistroBuilderInventoryReservationStatus.Released)
                throw new InvalidOperationException("La corrección no liberó la reserva original.");
            AssertReservation(lifecycle, legacy, correctedId, BistroBuilderInventoryReservationStatus.Active,
                "La corrección no creó una reserva nueva activa.");

            Advance(canonical, correctedId, BistroBuilderCanonicalOrderLineState.Submitted);
            Advance(canonical, correctedId, BistroBuilderCanonicalOrderLineState.Queued);
            Advance(canonical, correctedId, BistroBuilderCanonicalOrderLineState.Preparing);
            if (!lifecycle.TryConsumeLine(legacy, correctedId, out string consumeError))
                throw new InvalidOperationException("No se consumió la reserva al preparar: " + consumeError);
            AssertReservation(lifecycle, legacy, correctedId, BistroBuilderInventoryReservationStatus.Consumed,
                "Preparing no dejó la reserva consumida.");

            BistroBuilderAdvancedOrderMutationResult blockedCancel = advanced.TryCancelLine(
                fixtureOrder.OrderId, correctedId,
                BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
                "Intento de cancelación tardía.", "play11");
            if (blockedCancel.succeeded || blockedCancel.restriction != BistroBuilderAdvancedOrderRestriction.PreparationStarted)
                throw new InvalidOperationException("Una línea en preparación aceptó una cancelación simple.");

            BistroBuilderAdvancedOrderMutationResult courtesy = advanced.TryReplaceAfterIncident(
                fixtureOrder.OrderId, correctedId, item.DishId,
                BistroBuilderAdvancedOrderIncidentKind.KitchenError,
                "Cocina detecta una elaboración incorrecta.", true, "play11");
            if (!courtesy.succeeded || string.IsNullOrEmpty(courtesy.newLineId))
                throw new InvalidOperationException("Reposición de cortesía falló: " + courtesy.message);
            string courtesyId = courtesy.newLineId;
            AssertReservation(lifecycle, legacy, correctedId, BistroBuilderInventoryReservationStatus.Consumed,
                "La incidencia repuso stock ya consumido.");
            AssertReservation(lifecycle, legacy, courtesyId, BistroBuilderInventoryReservationStatus.Active,
                "La reposición no creó su propia reserva.");

            canonical.TryGetOrderSnapshot(fixtureOrder.OrderId, out BistroBuilderCanonicalOrder afterCourtesy);
            if (afterCourtesy == null || afterCourtesy.CalculateTotalPriceCents() != 0)
                throw new InvalidOperationException("La cortesía no retiró correctamente la línea defectuosa de la cuenta.");

            Advance(canonical, courtesyId, BistroBuilderCanonicalOrderLineState.Preparing);
            if (!lifecycle.TryConsumeLine(legacy, courtesyId, out consumeError))
                throw new InvalidOperationException("No se consumió inventario de cortesía: " + consumeError);
            Advance(canonical, courtesyId, BistroBuilderCanonicalOrderLineState.ReadyForPickup);
            Advance(canonical, courtesyId, BistroBuilderCanonicalOrderLineState.AssignedForDelivery);
            Advance(canonical, courtesyId, BistroBuilderCanonicalOrderLineState.InTransit);
            Advance(canonical, courtesyId, BistroBuilderCanonicalOrderLineState.Served);

            BistroBuilderAdvancedOrderMutationResult repeat = advanced.TryRepeatLine(
                fixtureOrder.OrderId, courtesyId, "play11");
            if (!repeat.succeeded || string.IsNullOrEmpty(repeat.newLineId))
                throw new InvalidOperationException("Repetición real falló: " + repeat.message);
            AssertReservation(lifecycle, legacy, repeat.newLineId, BistroBuilderInventoryReservationStatus.Active,
                "La repetición no reservó inventario.");
            canonical.TryGetOrderSnapshot(fixtureOrder.OrderId, out BistroBuilderCanonicalOrder afterRepeat);
            if (afterRepeat == null || afterRepeat.CalculateTotalPriceCents() != basePrice)
                throw new InvalidOperationException("La repetición no incrementó la cuenta exactamente una vez.");

            BistroBuilderAdvancedOrderMutationResult partialCancel = advanced.TryCancelLine(
                fixtureOrder.OrderId, repeat.newLineId,
                BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
                "El cliente retira la repetición antes de cocina.", "play11");
            if (!partialCancel.succeeded)
                throw new InvalidOperationException("Cancelación parcial falló: " + partialCancel.message);
            AssertReservation(lifecycle, legacy, repeat.newLineId, BistroBuilderInventoryReservationStatus.Released,
                "La cancelación parcial no liberó inventario.");

            BistroBuilderAdvancedOrderMutationResult repeat2 = advanced.TryRepeatLine(
                fixtureOrder.OrderId, courtesyId, "play11");
            if (!repeat2.succeeded) throw new InvalidOperationException("Segunda repetición falló.");
            Advance(canonical, repeat2.newLineId, BistroBuilderCanonicalOrderLineState.Preparing);
            if (!lifecycle.TryConsumeLine(legacy, repeat2.newLineId, out consumeError))
                throw new InvalidOperationException(consumeError);
            Advance(canonical, repeat2.newLineId, BistroBuilderCanonicalOrderLineState.ReadyForPickup);
            Advance(canonical, repeat2.newLineId, BistroBuilderCanonicalOrderLineState.AssignedForDelivery);
            Advance(canonical, repeat2.newLineId, BistroBuilderCanonicalOrderLineState.InTransit);
            Advance(canonical, repeat2.newLineId, BistroBuilderCanonicalOrderLineState.Served);
            BistroBuilderAdvancedOrderMutationResult returned = advanced.TryReturnWithoutReplacement(
                fixtureOrder.OrderId, repeat2.newLineId,
                BistroBuilderAdvancedOrderIncidentKind.QualityIssue,
                "El cliente devuelve el plato servido.", "play11");
            if (!returned.succeeded) throw new InvalidOperationException("Devolución real falló: " + returned.message);
            AssertReservation(lifecycle, legacy, repeat2.newLineId, BistroBuilderInventoryReservationStatus.Consumed,
                "La devolución repuso inventario ya consumido.");

            BistroBuilderAdvancedOrderMutationResult repeat3 = advanced.TryRepeatLine(
                fixtureOrder.OrderId, courtesyId, "play11");
            if (!repeat3.succeeded || string.IsNullOrEmpty(repeat3.newLineId))
                throw new InvalidOperationException("Repetición facturable final falló: " + repeat3.message);
            Advance(canonical, repeat3.newLineId, BistroBuilderCanonicalOrderLineState.Preparing);
            if (!lifecycle.TryConsumeLine(legacy, repeat3.newLineId, out consumeError))
                throw new InvalidOperationException("No se consumió inventario de la repetición final: " + consumeError);
            Advance(canonical, repeat3.newLineId, BistroBuilderCanonicalOrderLineState.ReadyForPickup);
            Advance(canonical, repeat3.newLineId, BistroBuilderCanonicalOrderLineState.AssignedForDelivery);
            Advance(canonical, repeat3.newLineId, BistroBuilderCanonicalOrderLineState.InTransit);
            Advance(canonical, repeat3.newLineId, BistroBuilderCanonicalOrderLineState.Served);
            BistroBuilderAdvancedOrderMutationResult wholeCancel = advanced.TryCancelWholeOrderSafely(
                fixtureOrder.OrderId, "Cancelación total tardía.", "play11");
            if (wholeCancel.succeeded || wholeCancel.restriction != BistroBuilderAdvancedOrderRestriction.PreparationStarted)
                throw new InvalidOperationException("La cancelación total ignoró producto ya preparado.");

            if (!facade.TryBuildSnapshot(out BistroBuilderAdvancedOrderPlayerSnapshot player, out string playerError))
                throw new InvalidOperationException(playerError);
            bool foundCourtesy = false, foundReturned = false;
            for (int i = 0; i < player.lines.Count; i++)
            {
                BistroBuilderAdvancedOrderPlayerLine line = player.lines[i];
                if (line.lineId == courtesyId && line.origin == BistroBuilderAdvancedOrderLineOriginKind.Courtesy)
                    foundCourtesy = true;
                if (line.lineId == repeat2.newLineId && line.state == BistroBuilderCanonicalOrderLineState.Failed &&
                    line.incidentKind == BistroBuilderAdvancedOrderIncidentKind.QualityIssue)
                    foundReturned = true;
            }
            if (!foundCourtesy || !foundReturned || player.changedLineCount < 3 || player.incidentCount < 2)
                throw new InvalidOperationException("La UI no proyecta claramente cambios e incidencias reales.");

            if (!canonical.TryCaptureRuntimeSnapshot(out BistroBuilderCanonicalOrderRuntimeSnapshot liveSnapshot,
                    out string liveSnapError))
                throw new InvalidOperationException(liveSnapError);
            string json = JsonUtility.ToJson(liveSnapshot);
            BistroBuilderCanonicalOrderRuntimeSnapshot roundTrip =
                JsonUtility.FromJson<BistroBuilderCanonicalOrderRuntimeSnapshot>(json);
            string rtError = string.Empty;
            bool roundTripOk = roundTrip != null && roundTrip.TryValidate(out rtError);
            if (!json.Contains("advancedOriginKind") || !json.Contains("advancedIncidentKind") || !roundTripOk)
                throw new InvalidOperationException("Persistencia service.runtime perdió metadata 11. " + rtError);

            BistroBuilderCanonicalOrderOperationResult completed =
                canonical.TryCompleteServedOrder(fixtureOrder.OrderId, "play11");
            if (!completed.Succeeded)
                throw new InvalidOperationException("El cierre canónico con incidencias fue rechazado: " + completed.Message);
            if (!canonical.TryGetOrderSnapshot(
                    fixtureOrder.OrderId, out BistroBuilderCanonicalOrder closedOrder) ||
                closedOrder == null || closedOrder.State != BistroBuilderCanonicalOrderState.Completed)
                throw new InvalidOperationException("La comanda con líneas Failed resueltas no alcanzó Completed.");
            if (closedOrder.CalculateTotalPriceCents() != basePrice)
                throw new InvalidOperationException("El cierre económico no conserva exactamente la repetición facturable final.");

            long balanceBeforePayment = finance.CurrentBalanceCents;
            int transactionsBeforePayment = finance.TransactionCount;
            MethodInfo recordPayment = typeof(BistroBuilderSalesRevenueBridge).GetMethod(
                "TryRecordCompletedOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            if (recordPayment == null)
                throw new InvalidOperationException("No se encontró el puente financiero 3B para validar el cobro final.");
            object[] paymentArgs = { legacy, string.Empty };
            bool paymentRecorded = (bool)recordPayment.Invoke(salesBridge, paymentArgs);
            string paymentError = paymentArgs[1] as string ?? string.Empty;
            if (!paymentRecorded)
                throw new InvalidOperationException("El cobro final 3B fue rechazado: " + paymentError);
            if (salesBridge.LastBaseAmountCents != basePrice)
                throw new InvalidOperationException("Finanzas recibió un importe base distinto del total canónico 11.");
            if (finance.CurrentBalanceCents != balanceBeforePayment + salesBridge.LastFinalAmountCents)
                throw new InvalidOperationException("El saldo financiero no refleja exactamente el cobro final 11.");
            int expectedTransactionDelta = salesBridge.LastFinalAmountCents > 0L ? 1 : 0;
            if (finance.TransactionCount != transactionsBeforePayment + expectedTransactionDelta)
                throw new InvalidOperationException("El ledger financiero no registró el cierre 11 exactamente una vez.");
            Finish(true,
                "PASS — corrección, restricción por preparación, reposición de cortesía, repetición, " +
                "cancelación parcial, devolución, inventario, cuenta, UI, service.runtime y cierre económico 3B son coherentes.", cli);
        }
        catch (Exception exception)
        {
            Finish(false, "11 PlayMode: " + Unwrap(exception).Message, cli);
        }
        finally
        {
            try
            {
                var lifecycle = Find<BistroBuilderOrderInventoryLifecycleService>();
                var canonical = Find<BistroBuilderCanonicalOrderService>();
                var inventory = Find<BistroBuilderInventoryService>();
                if (lifecycle != null) lifecycle.ClearRuntimeForLoad();
                if (activeOrders != null && legacy != null) activeOrders.Remove(legacy);
                if (canonical != null && originalOrders != null)
                    canonical.TryReplaceFromRuntimeSnapshot(originalOrders, true, out _);
                if (inventory != null && originalInventory != null)
                    inventory.TryReplaceFromRuntimeSnapshot(originalInventory, true, out _);
                var finance = Find<BistroBuilderFinanceService>();
                if (finance != null && originalFinance != null)
                    finance.TryRestoreSnapshot(originalFinance, out _);
                if (tableGo != null) UnityEngine.Object.Destroy(tableGo);
                if (groupGo != null) UnityEngine.Object.Destroy(groupGo);
                if (waiterGo != null) UnityEngine.Object.Destroy(waiterGo);
            }
            catch (Exception cleanup) { Debug.LogException(cleanup); }
        }
    }

    private static BistroBuilderMenuItemRuntimeState FindOrderableDish(
        BistroBuilderRestaurantMenuService menu,
        BistroBuilderRecipeCatalogService recipes,
        BistroBuilderMenuOfferService offer)
    {
        var items = new List<BistroBuilderMenuItemRuntimeState>();
        if (!menu.TryGetSnapshot(items, out _)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            BistroBuilderMenuItemRuntimeState item = items[i];
            if (item == null || item.CurrentPriceCents <= 0 || !item.Enabled || !item.Unlocked || item.ManuallySoldOut ||
                (item.AvailableServices & BistroBuilderMealServiceAvailability.Lunch) == 0)
                continue;
            if (!recipes.TryGetRecipeByDishId(item.DishId, out BistroBuilderRecipeDefinition recipe) || recipe == null)
                continue;
            if (offer == null || offer.IsDishOrderable(
                    item.DishId,
                    BistroBuilderMealServiceAvailability.Lunch,
                    BistroBuilderServiceMode.TableService,
                    out _, out _))
                return item;
        }
        return null;
    }

    private static void Advance(
        BistroBuilderCanonicalOrderService canonical,
        string lineId,
        BistroBuilderCanonicalOrderLineState target)
    {
        BistroBuilderCanonicalOrderOperationResult result =
            canonical.TryTransitionLine(lineId, target, "play11");
        if (!result.Succeeded)
            throw new InvalidOperationException("Transición " + target + " rechazada: " + result.Message);
    }

    private static void AssertReservation(
        BistroBuilderOrderInventoryLifecycleService lifecycle,
        RestaurantOrder order,
        string lineId,
        BistroBuilderInventoryReservationStatus expected,
        string message)
    {
        if (!lifecycle.TryGetCanonicalLineReservation(
                order, lineId, out BistroBuilderInventoryReservationSnapshot snapshot) ||
            snapshot == null || snapshot.Status != expected)
            throw new InvalidOperationException(message);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException tie && tie.InnerException != null)
            exception = tie.InnerException;
        return exception;
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER — BLOQUE 11 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

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
                item.DishId, item.CurrentPriceCents, item.DisplayOrder,
                item.SignatureDish, string.Empty, 0);
            rejectionReason = string.Empty;
            return string.Equals(dishId, item.DishId, StringComparison.Ordinal);
        }
    }
}
