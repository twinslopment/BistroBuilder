using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de 2.3E para pedidos de compra.
///
/// Responsabilidades:
/// - crear y editar borradores;
/// - cotizar contra 2.3D en el instante de confirmación;
/// - congelar precios/condiciones comerciales;
/// - gobernar el ciclo de estados del PurchaseOrder;
/// - exponer snapshot para 2.3J.
///
/// No planifica retrasos (2.3G), no representa la entrega física (2.3H),
/// no recibe mercancía (2.2B) y no escribe Inventario ni Recepciones.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class BistroBuilderSupplierPurchaseOrderService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath =
        BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath;
    public const string IngredientAuthoringResourcePath =
        BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath;
    public const string SettingsResourcePath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierPurchaseOrderSettings";

    private static BistroBuilderSupplierPurchaseOrderService instance;

    private readonly Dictionary<string, BistroBuilderPurchaseOrderRecord> orderById =
        new Dictionary<string, BistroBuilderPurchaseOrderRecord>(StringComparer.Ordinal);

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private BistroBuilderSupplierPurchaseOrderSettings settings;
    private BistroBuilderSupplierMarketService marketService;
    private BistroBuilderSupplierCommercialIntelligenceService commercialService;
    private BistroBuilderSupplierPurchaseOrdersSnapshot state;
    private string lastInitializationError;

    public static BistroBuilderSupplierPurchaseOrderService Instance => instance;
    public bool IsInitialized => state != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => ResolveCurrentGameDay();
    public long OrdersRevision => state != null ? state.ordersRevision : 0L;
    public ulong SourceMarketSeed => state != null ? state.sourceMarketSeed : 0UL;
    public ulong SourceCommercialSeed => state != null ? state.sourceCommercialSeed : 0UL;
    public int OrderCount => state != null && state.orders != null ? state.orders.Count : 0;

    public event Action<BistroBuilderPurchaseOrderRecord> OrderCreated;
    public event Action<BistroBuilderPurchaseOrderRecord> OrderChanged;
    public event Action<BistroBuilderPurchaseOrderConfirmationReceipt> OrderConfirmed;
    public event Action<BistroBuilderPurchaseOrderRecord> OrderStateChanged;
    public event Action<BistroBuilderPurchaseOrderRecord> OrderCancelled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>() != null)
        {
            return;
        }

        GameObject host = new GameObject("BistroBuilderSupplierPurchaseOrderService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierPurchaseOrderService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        TryInitializeFresh();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool TryInitializeFresh()
    {
        lastInitializationError = null;
        LoadDependencies();
        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring en Resources.";
            return false;
        }
        if (ingredientDatabase == null)
        {
            lastInitializationError = "Falta ingredient.authoring en Resources.";
            return false;
        }
        if (settings == null)
        {
            lastInitializationError = "Falta supplier.orders.settings. Ejecuta el instalador 2.3E.";
            return false;
        }

        ulong marketSeed = marketService != null && marketService.IsInitialized
            ? marketService.MarketSeed
            : 0UL;
        ulong commercialSeed = commercialService != null && commercialService.IsInitialized
            ? commercialService.CommercialSeed
            : 0UL;
        state = BistroBuilderSupplierPurchaseOrderEngine.CreateInitialSnapshot(
            ResolveCurrentGameDay(),
            marketSeed,
            commercialSeed);
        RebuildIndex();
        return true;
    }

    public bool TryCreateDraft(
        string supplierId,
        out BistroBuilderPurchaseOrderRecord created,
        out string error)
    {
        created = null;
        error = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            error = "El SupplierId no corresponde a un proveedor activo.";
            return false;
        }

        BistroBuilderPurchaseOrderRecord stored;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryCreateDraft(
                state,
                supplier.SupplierId,
                ResolveCurrentGameDay(),
                settings,
                out stored,
                out error))
        {
            return false;
        }

        RebuildIndex();
        created = stored.DeepClone();
        OrderCreated?.Invoke(created.DeepClone());
        return true;
    }

    public bool TrySetDraftLine(
        string purchaseOrderId,
        string supplierOfferId,
        int packageCount,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        BistroBuilderSupplierAuthoringRecord supplier;
        BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        if (!TryResolveEditableOrderAndOffer(
                purchaseOrderId,
                supplierOfferId,
                out order,
                out supplier,
                out offer,
                out error))
        {
            return false;
        }

        BistroBuilderIngredientAuthoringRecord ingredient;
        BistroBuilderCommercialPackageAuthoringRecord package;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActivePackage(
                ingredientDatabase,
                offer.ingredientId,
                offer.packageFormatId,
                out ingredient,
                out package))
        {
            error = "La oferta referencia un formato comercial no activo o inexistente.";
            return false;
        }

        if (!BistroBuilderSupplierPurchaseOrderEngine.TrySetDraftLine(
                state,
                order,
                offer,
                packageCount,
                ResolveCurrentGameDay(),
                settings,
                out error))
        {
            return false;
        }

        updated = order.DeepClone();
        OrderChanged?.Invoke(updated.DeepClone());
        return true;
    }

    public bool TryRemoveDraftLine(
        string purchaseOrderId,
        string supplierOfferId,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }

        if (!BistroBuilderSupplierPurchaseOrderEngine.TryRemoveDraftLine(
                state,
                order,
                supplierOfferId,
                ResolveCurrentGameDay(),
                out error))
        {
            return false;
        }

        updated = order.DeepClone();
        OrderChanged?.Invoke(updated.DeepClone());
        return true;
    }

    public bool TryBuildConfirmationPreview(
        string purchaseOrderId,
        out BistroBuilderPurchaseOrderConfirmationPreview preview,
        out string error)
    {
        preview = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo los pedidos Draft tienen cotización dinámica previa a confirmación.";
            return false;
        }

        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(order.supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            error = "El proveedor del pedido ya no está activo.";
            return false;
        }

        List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs;
        if (!TryBuildConfirmationInputs(order, supplier, out inputs, out error))
        {
            return false;
        }

        return BistroBuilderSupplierPurchaseOrderEngine.TryBuildConfirmationPreview(
            order,
            supplier,
            inputs,
            settings,
            out preview,
            out error);
    }

    public bool TryConfirmOrder(
        string purchaseOrderId,
        out BistroBuilderPurchaseOrderConfirmationReceipt receipt,
        out string error)
    {
        receipt = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        if (order.status == BistroBuilderPurchaseOrderStatus.Confirmed)
        {
            receipt = BistroBuilderSupplierPurchaseOrderEngine.BuildReceipt(order);
            return true;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "El pedido ya no está en Draft y no puede confirmarse.";
            return false;
        }

        BistroBuilderPurchaseOrderConfirmationPreview preview;
        if (!TryBuildConfirmationPreview(purchaseOrderId, out preview, out error))
        {
            return false;
        }
        if (!preview.canConfirm)
        {
            error = preview.blockers != null && preview.blockers.Count > 0
                ? string.Join(" | ", preview.blockers.ToArray())
                : "La cotización actual bloquea la confirmación.";
            return false;
        }

        BistroBuilderSupplierAuthoringRecord supplier;
        if (!supplierDatabase.TryGetSupplier(order.supplierId, out supplier) || supplier == null)
        {
            error = "No se pudo resolver el proveedor del pedido.";
            return false;
        }

        long marketRevision = marketService != null ? marketService.MarketRevision : 0L;
        long commercialRevision = commercialService != null ? commercialService.CommercialRevision : 0L;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryConfirm(
                state,
                order,
                supplier,
                preview,
                ResolveCurrentGameDay(),
                marketRevision,
                commercialRevision,
                out receipt,
                out error))
        {
            return false;
        }

        RebuildIndex();
        OrderConfirmed?.Invoke(receipt != null ? receipt.DeepClone() : null);
        OrderStateChanged?.Invoke(order.DeepClone());
        return true;
    }

    public bool TryCancelOrder(
        string purchaseOrderId,
        string reason,
        out BistroBuilderPurchaseOrderRecord cancelled,
        out string error)
    {
        cancelled = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }

        long revisionBefore = order.stateRevision;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryCancel(
                state,
                order,
                reason,
                ResolveCurrentGameDay(),
                settings,
                out error))
        {
            return false;
        }

        cancelled = order.DeepClone();
        if (order.stateRevision != revisionBefore)
        {
            OrderCancelled?.Invoke(cancelled.DeepClone());
            OrderStateChanged?.Invoke(cancelled.DeepClone());
        }
        return true;
    }

    /// <summary>
    /// Contrato para 2.3G. Adjuntar un plan logístico hace que Confirmed pase a PendingDelivery.
    /// </summary>
    public bool TryMarkPendingDelivery(
        string purchaseOrderId,
        string logisticsPlanId,
        int plannedDeliveryGameDay,
        int windowStartMinuteOfDay,
        int windowEndMinuteOfDay,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        long revisionBefore = order.stateRevision;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryMarkPendingDelivery(
                state,
                order,
                logisticsPlanId,
                plannedDeliveryGameDay,
                windowStartMinuteOfDay,
                windowEndMinuteOfDay,
                ResolveCurrentGameDay(),
                out error))
        {
            return false;
        }
        updated = order.DeepClone();
        if (order.stateRevision != revisionBefore)
        {
            OrderStateChanged?.Invoke(updated.DeepClone());
        }
        return true;
    }


    /// <summary>
    /// Contrato de 2.3G para retrasos/replanificación antes de la expedición.
    /// Conserva LogisticsPlanId y solo modifica el plan mientras el pedido sigue PendingDelivery.
    /// </summary>
    public bool TryUpdatePendingDeliveryPlan(
        string purchaseOrderId,
        string logisticsPlanId,
        int plannedDeliveryGameDay,
        int windowStartMinuteOfDay,
        int windowEndMinuteOfDay,
        int plannedDelayGameMinutes,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        long revisionBefore = order.stateRevision;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryUpdatePendingDeliveryPlan(
                state,
                order,
                logisticsPlanId,
                plannedDeliveryGameDay,
                windowStartMinuteOfDay,
                windowEndMinuteOfDay,
                plannedDelayGameMinutes,
                ResolveCurrentGameDay(),
                out error))
        {
            return false;
        }
        updated = order.DeepClone();
        if (order.stateRevision != revisionBefore)
        {
            OrderChanged?.Invoke(updated.DeepClone());
        }
        return true;
    }

    /// <summary>
    /// Contrato para 2.3G/2.3H. Una vez despachado, el pedido ya no es cancelable.
    /// </summary>
    public bool TryMarkInDelivery(
        string purchaseOrderId,
        int actualDeliveryStartGameDay,
        int appliedDelayGameMinutes,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        long revisionBefore = order.stateRevision;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryMarkInDelivery(
                state,
                order,
                actualDeliveryStartGameDay,
                appliedDelayGameMinutes,
                ResolveCurrentGameDay(),
                out error))
        {
            return false;
        }
        updated = order.DeepClone();
        if (order.stateRevision != revisionBefore)
        {
            OrderStateChanged?.Invoke(updated.DeepClone());
        }
        return true;
    }

    /// <summary>
    /// Contrato para el puente de 2.2B. Delivered exige ReceiptId estable.
    /// El alta física de stock se realiza en Receiving, nunca aquí.
    /// </summary>
    public bool TryMarkDelivered(
        string purchaseOrderId,
        string deliveryReceiptId,
        int deliveredGameDay,
        out BistroBuilderPurchaseOrderRecord updated,
        out string error)
    {
        updated = null;
        error = null;
        BistroBuilderPurchaseOrderRecord order;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        long revisionBefore = order.stateRevision;
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryMarkDelivered(
                state,
                order,
                deliveryReceiptId,
                deliveredGameDay,
                out error))
        {
            return false;
        }
        updated = order.DeepClone();
        if (order.stateRevision != revisionBefore)
        {
            OrderStateChanged?.Invoke(updated.DeepClone());
        }
        return true;
    }

    public bool TryGetOrder(string purchaseOrderId, out BistroBuilderPurchaseOrderRecord order)
    {
        order = null;
        if (!IsInitialized || string.IsNullOrWhiteSpace(purchaseOrderId))
        {
            return false;
        }
        BistroBuilderPurchaseOrderRecord stored;
        if (!orderById.TryGetValue(purchaseOrderId, out stored) || stored == null)
        {
            return false;
        }
        order = stored.DeepClone();
        return true;
    }

    public int CopyOrders(List<BistroBuilderPurchaseOrderRecord> buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (!IsInitialized || state.orders == null)
        {
            return 0;
        }
        for (int index = 0; index < state.orders.Count; index++)
        {
            if (state.orders[index] != null)
            {
                buffer.Add(state.orders[index].DeepClone());
            }
        }
        return buffer.Count;
    }

    public BistroBuilderSupplierPurchaseOrdersSnapshot CreateSnapshot()
    {
        if (state == null)
        {
            return null;
        }
        BistroBuilderSupplierPurchaseOrdersSnapshot clone = state.DeepClone();
        clone.currentGameDay = Math.Max(clone.currentGameDay, ResolveCurrentGameDay());
        return clone;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderSupplierPurchaseOrdersSnapshot candidate,
        out string error)
    {
        error = null;
        LoadDependencies();
        if (candidate == null)
        {
            error = "Snapshot de pedidos nulo.";
            return false;
        }
        BistroBuilderSupplierPurchaseOrdersSnapshot owned = candidate.DeepClone();
        if (!BistroBuilderSupplierPurchaseOrderEngine.ValidateSnapshotAgainstAuthoring(
                owned,
                supplierDatabase,
                ingredientDatabase,
                settings,
                out error))
        {
            return false;
        }

        BindRuntimeDependencies();
        if (marketService != null && marketService.IsInitialized &&
            owned.sourceMarketSeed != 0UL &&
            owned.sourceMarketSeed != marketService.MarketSeed)
        {
            error = "supplier.orders.runtime pertenece a otra sesión de mercado 2.3C; restaura primero el snapshot de mercado correspondiente.";
            return false;
        }
        if (marketService != null && marketService.IsInitialized &&
            owned.sourceMarketSeed != 0UL &&
            owned.currentGameDay != marketService.CurrentGameDay)
        {
            error = "supplier.orders.runtime pertenece a otro día de la sesión 2.3C; restaura mercado, comercial y pedidos del mismo snapshot.";
            return false;
        }
        if (commercialService != null && commercialService.IsInitialized &&
            owned.sourceCommercialSeed != 0UL &&
            owned.sourceCommercialSeed != commercialService.CommercialSeed)
        {
            error = "supplier.orders.runtime pertenece a otra sesión comercial 2.3D; restaura primero el snapshot comercial correspondiente.";
            return false;
        }

        state = owned;
        lastInitializationError = null;
        RebuildIndex();
        return true;
    }

    private bool TryBuildConfirmationInputs(
        BistroBuilderPurchaseOrderRecord order,
        BistroBuilderSupplierAuthoringRecord supplier,
        out List<BistroBuilderPurchaseOrderConfirmationLineInput> inputs,
        out string error)
    {
        inputs = new List<BistroBuilderPurchaseOrderConfirmationLineInput>();
        error = null;
        BindRuntimeDependencies();
        if (marketService == null || !marketService.IsInitialized)
        {
            error = "El mercado 2.3C no está inicializado; no se puede cotizar el pedido.";
            return false;
        }
        if (commercialService == null || !commercialService.IsInitialized)
        {
            error = "El Motor Comercial 2.3D no está inicializado; no se puede cotizar el pedido.";
            return false;
        }
        if (state != null && state.sourceMarketSeed != 0UL &&
            state.sourceMarketSeed != marketService.MarketSeed)
        {
            error = "supplier.orders.runtime pertenece a otra sesión de mercado 2.3C; debe restaurarse o reinicializarse antes de cotizar.";
            return false;
        }

        // Primero alinear 2.3D con el mercado actual. Esto es esencial para estados de pedidos
        // aún no vinculados: 2.3D puede regenerar su semilla al detectar una nueva sesión 2.3C.
        string synchronizationError;
        if (!commercialService.TrySynchronizeCurrentMarketState(out synchronizationError))
        {
            error = "2.3D no pudo sincronizar la cotización actual: " + synchronizationError;
            return false;
        }
        if (!TryEnsureCommercialSessionBinding(out error))
        {
            return false;
        }

        if (order.draftLines == null)
        {
            return true;
        }

        for (int index = 0; index < order.draftLines.Count; index++)
        {
            BistroBuilderPurchaseOrderDraftLine draftLine = order.draftLines[index];
            if (draftLine == null)
            {
                error = "El borrador contiene una línea nula.";
                return false;
            }

            BistroBuilderSupplierBaseOfferAuthoringRecord offer;
            if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActiveOffer(
                    supplier,
                    draftLine.supplierOfferId,
                    out offer) || offer == null)
            {
                error = draftLine.supplierOfferId + ": la oferta ya no está activa para este proveedor.";
                return false;
            }

            BistroBuilderIngredientAuthoringRecord ingredient;
            BistroBuilderCommercialPackageAuthoringRecord package;
            if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActivePackage(
                    ingredientDatabase,
                    offer.ingredientId,
                    offer.packageFormatId,
                    out ingredient,
                    out package))
            {
                error = offer.SupplierOfferId + ": formato comercial inexistente o inactivo.";
                return false;
            }

            BistroBuilderSupplierCommercialQuote quote;
            if (!commercialService.TryGetCommercialQuote(offer.SupplierOfferId, out quote) || quote == null)
            {
                error = offer.SupplierOfferId + ": 2.3D no devuelve cotización comercial.";
                return false;
            }
            if (!string.Equals(quote.supplierId, supplier.SupplierId, StringComparison.Ordinal) ||
                !string.Equals(quote.ingredientId, offer.ingredientId, StringComparison.Ordinal) ||
                !string.Equals(quote.packageFormatId, offer.packageFormatId, StringComparison.Ordinal))
            {
                error = offer.SupplierOfferId + ": la cotización 2.3D no converge con la oferta base.";
                return false;
            }

            int promotionStartGameDay = 0;
            if (quote.hasActivePromotion && !string.IsNullOrWhiteSpace(quote.promotionId))
            {
                BistroBuilderSupplierPromotionRecord activePromotion;
                if (commercialService.TryGetActivePromotion(offer.SupplierOfferId, out activePromotion) &&
                    activePromotion != null &&
                    string.Equals(activePromotion.promotionId, quote.promotionId, StringComparison.Ordinal))
                {
                    promotionStartGameDay = activePromotion.startGameDay;
                }
            }

            inputs.Add(new BistroBuilderPurchaseOrderConfirmationLineInput
            {
                purchaseOrderLineId = draftLine.purchaseOrderLineId,
                supplierOfferId = offer.SupplierOfferId,
                supplierId = supplier.SupplierId,
                ingredientId = offer.ingredientId,
                ingredientDisplayName = ingredient.displayNameSnapshot,
                canonicalUnit = ingredient.canonicalUnitSnapshot,
                packageFormatId = offer.packageFormatId,
                packageDisplayName = package.displayName,
                packageType = package.packageType,
                logisticSize = package.logisticSize,
                packageNetQuantityMicrounits = package.netQuantityMicrounits,
                packageCount = draftLine.packageCount,
                minimumPackageCount = Math.Max(1, offer.minimumPackageCount),
                orderIncrement = Math.Max(1, offer.orderIncrement),
                basePriceCents = offer.basePriceCents,
                marketPriceCents = quote.marketPriceCents,
                effectiveUnitPriceCents = quote.effectivePriceCents,
                availability = quote.availability,
                availableForNewOrders = quote.availableForNewOrders,
                hasActivePromotion = quote.hasActivePromotion,
                promotionId = quote.promotionId,
                promotionStartGameDay = promotionStartGameDay,
                promotionEndGameDayExclusive = quote.promotionEndGameDayExclusive,
                discountBasisPoints = quote.discountBasisPoints,
                promotionReasonCode = quote.reasonCode,
                promotionReasonText = quote.reasonText,
                quotedLeadTimeGameHours = offer.overrideLeadTime
                    ? Math.Max(0.1f, offer.leadTimeOverrideGameHours)
                    : Math.Max(0.1f, supplier.defaultLeadTimeGameHours),
                sourceMarketRevision = marketService.MarketRevision,
                sourceCommercialRevision = commercialService.CommercialRevision
            });
        }
        return true;
    }

    /// <summary>
    /// Vincula el estado de pedidos a la sesión concreta de 2.3C/2.3D usada para cotizar.
    /// Un snapshot antiguo no puede confirmar silenciosamente contra otro mercado/comercial.
    /// Los estados creados antes de que 2.3C/2.3D estuvieran listos se vinculan una sola vez.
    /// </summary>
    private bool TryEnsureCommercialSessionBinding(out string error)
    {
        error = null;
        if (state == null)
        {
            error = "supplier.orders.runtime no está inicializado.";
            return false;
        }
        if (marketService == null || !marketService.IsInitialized ||
            commercialService == null || !commercialService.IsInitialized)
        {
            error = "2.3C/2.3D deben estar inicializados para vincular la sesión comercial del pedido.";
            return false;
        }

        ulong marketSeed = marketService.MarketSeed;
        ulong commercialSeed = commercialService.CommercialSeed;
        if (state.sourceMarketSeed != 0UL && state.sourceMarketSeed != marketSeed)
        {
            error = "supplier.orders.runtime pertenece a otra sesión de mercado 2.3C; debe restaurarse o reinicializarse antes de cotizar.";
            return false;
        }
        if (state.sourceCommercialSeed != 0UL && state.sourceCommercialSeed != commercialSeed)
        {
            error = "supplier.orders.runtime pertenece a otra sesión comercial 2.3D; debe restaurarse o reinicializarse antes de cotizar.";
            return false;
        }

        bool changed = false;
        if (state.sourceMarketSeed == 0UL)
        {
            state.sourceMarketSeed = marketSeed;
            changed = true;
        }
        if (state.sourceCommercialSeed == 0UL)
        {
            state.sourceCommercialSeed = commercialSeed;
            changed = true;
        }
        if (changed)
        {
            state.ordersRevision++;
            state.currentGameDay = Math.Max(state.currentGameDay, ResolveCurrentGameDay());
        }
        return true;
    }

    private bool TryResolveEditableOrderAndOffer(
        string purchaseOrderId,
        string supplierOfferId,
        out BistroBuilderPurchaseOrderRecord order,
        out BistroBuilderSupplierAuthoringRecord supplier,
        out BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        out string error)
    {
        supplier = null;
        offer = null;
        if (!TryResolveOrder(purchaseOrderId, out order, out error))
        {
            return false;
        }
        if (order.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            error = "Solo un pedido Draft se puede editar.";
            return false;
        }
        if (!supplierDatabase.TryGetSupplier(order.supplierId, out supplier) || supplier == null || !supplier.isActive)
        {
            error = "El proveedor del pedido no existe o ya no está activo.";
            return false;
        }
        if (!BistroBuilderSupplierPurchaseOrderEngine.TryFindActiveOffer(supplier, supplierOfferId, out offer) ||
            offer == null)
        {
            error = "La oferta no pertenece al proveedor del pedido o está inactiva.";
            return false;
        }
        return true;
    }

    private bool TryResolveOrder(
        string purchaseOrderId,
        out BistroBuilderPurchaseOrderRecord order,
        out string error)
    {
        order = null;
        error = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(purchaseOrderId) ||
            !orderById.TryGetValue(purchaseOrderId, out order) || order == null)
        {
            error = "PurchaseOrderId no localizado.";
            return false;
        }
        return true;
    }

    private bool EnsureInitialized(out string error)
    {
        error = null;
        if (!IsInitialized && !TryInitializeFresh())
        {
            error = lastInitializationError ?? "Servicio de pedidos 2.3E no inicializado.";
            return false;
        }
        return true;
    }

    private void LoadDependencies()
    {
        supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(SupplierAuthoringResourcePath);
        ingredientDatabase = Resources.Load<BistroBuilderIngredientAuthoringDatabase>(IngredientAuthoringResourcePath);
        settings = Resources.Load<BistroBuilderSupplierPurchaseOrderSettings>(SettingsResourcePath);
        BindRuntimeDependencies();
    }

    private void BindRuntimeDependencies()
    {
        marketService = BistroBuilderSupplierMarketService.Instance;
        if (marketService == null)
        {
            marketService = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        }
        commercialService = BistroBuilderSupplierCommercialIntelligenceService.Instance;
        if (commercialService == null)
        {
            commercialService = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        }
    }

    private int ResolveCurrentGameDay()
    {
        BindRuntimeDependencies();
        return marketService != null && marketService.IsInitialized
            ? Math.Max(1, marketService.CurrentGameDay)
            : state != null ? Math.Max(1, state.currentGameDay) : 1;
    }

    private void RebuildIndex()
    {
        orderById.Clear();
        if (state == null || state.orders == null)
        {
            return;
        }
        for (int index = 0; index < state.orders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = state.orders[index];
            if (order != null && !string.IsNullOrWhiteSpace(order.purchaseOrderId))
            {
                orderById[order.purchaseOrderId] = order;
            }
        }
    }
}
