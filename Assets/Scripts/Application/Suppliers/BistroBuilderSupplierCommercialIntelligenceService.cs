using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de 2.3D.
///
/// Se apoya en BistroBuilderSupplierMarketService como única autoridad de precio
/// y disponibilidad. 2.3D añade campañas/promociones temporales y una cotización
/// comercial efectiva, pero nunca reescribe supplier.catalog, supplier.authoring,
/// Inventory ni Recepciones.
/// </summary>
public sealed class BistroBuilderSupplierCommercialIntelligenceService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath =
        BistroBuilderSupplierMarketService.SupplierAuthoringResourcePath;
    public const string IngredientAuthoringResourcePath =
        "BistroBuilder/Suppliers/Authoring/BistroBuilderIngredientAuthoringDatabase";
    public const string SettingsResourcePath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierCommercialIntelligenceSettings";

    private static BistroBuilderSupplierCommercialIntelligenceService instance;

    private readonly Dictionary<string, BistroBuilderSupplierPromotionRecord> activeByOfferId =
        new Dictionary<string, BistroBuilderSupplierPromotionRecord>(StringComparer.Ordinal);

    private BistroBuilderSupplierMarketService marketService;
    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private BistroBuilderSupplierCommercialIntelligenceSettings settings;
    private BistroBuilderSupplierCommercialIntelligenceSnapshot state;
    private string lastInitializationError;

    public static BistroBuilderSupplierCommercialIntelligenceService Instance => instance;
    public bool IsInitialized => state != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => state != null ? state.currentGameDay : 0;
    public long CommercialRevision => state != null ? state.commercialRevision : 0L;
    public int ActivePromotionCount => state != null && state.activePromotions != null
        ? state.activePromotions.Count
        : 0;
    public int PromotionHistoryCount => state != null && state.promotionHistory != null
        ? state.promotionHistory.Count
        : 0;
    public ulong CommercialSeed => state != null ? state.commercialSeed : 0UL;
    public ulong SourceMarketSeed => state != null ? state.sourceMarketSeed : 0UL;

    public event Action<BistroBuilderSupplierCommercialReviewOutcome> CommercialReviewProcessed;
    public event Action<BistroBuilderSupplierPromotionRecord> PromotionStarted;
    public event Action<BistroBuilderSupplierPromotionRecord> PromotionEnded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>() != null)
        {
            return;
        }

        GameObject host = new GameObject("BistroBuilderSupplierCommercialIntelligenceService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierCommercialIntelligenceService>();
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
        LoadStaticDependencies();
        TryBindAndInitializeIfPossible();
    }

    private void OnDestroy()
    {
        UnbindMarket();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (marketService == null || !marketService.IsInitialized)
        {
            TryBindAndInitializeIfPossible();
            return;
        }

        if (!IsInitialized || state.sourceMarketSeed != marketService.MarketSeed)
        {
            TryInitializeFresh(null);
            return;
        }

        string synchronizationError;
        if (!TrySynchronizeCurrentMarketState(out synchronizationError) &&
            !string.IsNullOrWhiteSpace(synchronizationError))
        {
            lastInitializationError = synchronizationError;
        }
    }

    public bool TryInitializeFresh(ulong? explicitCommercialSeed)
    {
        lastInitializationError = null;
        LoadStaticDependencies();
        BindMarketIfNeeded();

        if (marketService == null || !marketService.IsInitialized)
        {
            lastInitializationError = "2.3D espera a que BistroBuilderSupplierMarketService esté inicializado.";
            return false;
        }
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
            lastInitializationError = "Falta supplier.commercial.settings. Ejecuta el instalador 2.3D.";
            return false;
        }

        BistroBuilderSupplierMarketSnapshot marketSnapshot = marketService.CreateSnapshot();
        if (marketSnapshot == null)
        {
            lastInitializationError = "2.3D no puede capturar el snapshot de mercado 2.3C.";
            return false;
        }

        state = BistroBuilderSupplierCommercialIntelligenceEngine.CreateInitialSnapshot(
            marketSnapshot,
            settings,
            explicitCommercialSeed);
        RebuildIndex();
        return true;
    }

    public bool TryProcessCurrentMarketReview(out string error)
    {
        error = null;
        if (!IsInitialized || marketService == null || !marketService.IsInitialized)
        {
            error = lastInitializationError ?? "Motor Comercial Inteligente no inicializado.";
            return false;
        }

        BistroBuilderSupplierMarketSnapshot marketSnapshot = marketService.CreateSnapshot();
        if (marketSnapshot == null || marketSnapshot.lastReviewGameDay <= 0)
        {
            return true;
        }

        return ProcessReviewSnapshot(marketSnapshot, marketSnapshot.lastReviewGameDay, out error);
    }


    public bool TrySynchronizeCurrentMarketState(out string error)
    {
        error = null;
        if (marketService == null || !marketService.IsInitialized)
        {
            BindMarketIfNeeded();
        }
        if (marketService == null || !marketService.IsInitialized)
        {
            error = "El mercado 2.3C no está inicializado.";
            return false;
        }
        if (!IsInitialized || state.sourceMarketSeed != marketService.MarketSeed)
        {
            if (!TryInitializeFresh(null))
            {
                error = lastInitializationError;
                return false;
            }
        }

        List<BistroBuilderSupplierPromotionRecord> expired;
        if (!BistroBuilderSupplierCommercialIntelligenceEngine.TryAdvanceToGameDay(
                state,
                settings,
                marketService.CurrentGameDay,
                out expired,
                out error))
        {
            return false;
        }
        RebuildIndex();
        PublishEnded(expired);

        BistroBuilderSupplierMarketSnapshot marketSnapshot = marketService.CreateSnapshot();
        if (marketSnapshot != null &&
            marketSnapshot.lastReviewGameDay > state.lastProcessedMarketReviewDay)
        {
            List<int> pendingReviewDays = new List<int>();
            if (marketSnapshot.reviews != null)
            {
                for (int index = 0; index < marketSnapshot.reviews.Count; index++)
                {
                    BistroBuilderSupplierMarketReviewRecord review = marketSnapshot.reviews[index];
                    if (review != null && review.gameDay > state.lastProcessedMarketReviewDay)
                    {
                        pendingReviewDays.Add(review.gameDay);
                    }
                }
            }
            if (pendingReviewDays.Count == 0)
            {
                pendingReviewDays.Add(marketSnapshot.lastReviewGameDay);
            }
            pendingReviewDays.Sort();

            for (int index = 0; index < pendingReviewDays.Count; index++)
            {
                int reviewDay = pendingReviewDays[index];
                BistroBuilderSupplierMarketSnapshot reviewSnapshot =
                    BistroBuilderSupplierCommercialIntelligenceEngine.CreateReviewScopedMarketSnapshot(
                        marketSnapshot,
                        reviewDay);
                if (!ProcessReviewSnapshot(reviewSnapshot, reviewDay, out error))
                {
                    return false;
                }
            }
        }

        lastInitializationError = null;
        return true;
    }

    public bool TryGetActivePromotion(
        string supplierOfferId,
        out BistroBuilderSupplierPromotionRecord promotion)
    {
        promotion = null;
        if (!IsInitialized || string.IsNullOrWhiteSpace(supplierOfferId))
        {
            return false;
        }

        BistroBuilderSupplierPromotionRecord stored;
        if (!activeByOfferId.TryGetValue(supplierOfferId, out stored) || stored == null ||
            !stored.IsActiveOnDay(state.currentGameDay))
        {
            return false;
        }

        promotion = stored.DeepClone();
        return true;
    }

    public bool TryGetCommercialQuote(
        string supplierOfferId,
        out BistroBuilderSupplierCommercialQuote quote)
    {
        quote = null;
        if (!IsInitialized || marketService == null || !marketService.IsInitialized)
        {
            return false;
        }

        BistroBuilderSupplierMarketOfferState market;
        if (!marketService.TryGetOfferState(supplierOfferId, out market) || market == null)
        {
            return false;
        }

        BistroBuilderSupplierPromotionRecord promotion;
        bool hasPromotion = TryGetActivePromotion(supplierOfferId, out promotion);
        long effectivePrice = market.currentPriceCents;
        if (hasPromotion && promotion != null)
        {
            effectivePrice = Math.Min(market.currentPriceCents, promotion.promotionalPriceCents);
        }

        quote = new BistroBuilderSupplierCommercialQuote
        {
            supplierOfferId = market.supplierOfferId,
            supplierId = market.supplierId,
            ingredientId = market.ingredientId,
            packageFormatId = market.packageFormatId,
            marketPriceCents = market.currentPriceCents,
            effectivePriceCents = Math.Max(1L, effectivePrice),
            availability = market.availability,
            availableForNewOrders =
                market.availability != BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado,
            hasActivePromotion = hasPromotion,
            promotionId = hasPromotion ? promotion.promotionId : null,
            promotionEndGameDayExclusive = hasPromotion ? promotion.endGameDayExclusive : 0,
            discountBasisPoints = hasPromotion ? promotion.discountBasisPoints : 0,
            reasonCode = hasPromotion ? promotion.reasonCode : null,
            reasonText = hasPromotion ? promotion.reasonText : null
        };
        return true;
    }

    public long GetEffectivePriceCents(string supplierOfferId, long fallbackCents = 0L)
    {
        BistroBuilderSupplierCommercialQuote quote;
        return TryGetCommercialQuote(supplierOfferId, out quote)
            ? quote.effectivePriceCents
            : fallbackCents;
    }

    public int CopyActivePromotions(List<BistroBuilderSupplierPromotionRecord> buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (!IsInitialized)
        {
            return 0;
        }
        for (int index = 0; index < state.activePromotions.Count; index++)
        {
            if (state.activePromotions[index] != null)
            {
                buffer.Add(state.activePromotions[index].DeepClone());
            }
        }
        return buffer.Count;
    }

    public int CopyPromotionHistory(List<BistroBuilderSupplierPromotionRecord> buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (!IsInitialized)
        {
            return 0;
        }
        for (int index = 0; index < state.promotionHistory.Count; index++)
        {
            if (state.promotionHistory[index] != null)
            {
                buffer.Add(state.promotionHistory[index].DeepClone());
            }
        }
        return buffer.Count;
    }

    public BistroBuilderSupplierCommercialIntelligenceSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderSupplierCommercialIntelligenceSnapshot candidate,
        out string error)
    {
        error = null;
        if (candidate == null)
        {
            error = "Snapshot comercial nulo.";
            return false;
        }

        LoadStaticDependencies();
        BindMarketIfNeeded();
        if (marketService == null || !marketService.IsInitialized)
        {
            error = "No existe mercado 2.3C inicializado para restaurar 2.3D.";
            return false;
        }

        BistroBuilderSupplierMarketSnapshot marketSnapshot = marketService.CreateSnapshot();
        BistroBuilderSupplierCommercialIntelligenceSnapshot owned = candidate.DeepClone();
        if (!BistroBuilderSupplierCommercialIntelligenceEngine.ValidateSnapshotAgainstAuthoringAndMarket(
                owned,
                marketSnapshot,
                supplierDatabase,
                out error))
        {
            return false;
        }

        state = owned;
        lastInitializationError = null;
        RebuildIndex();
        return true;
    }

    private void HandleMarketReviewed(BistroBuilderSupplierMarketReviewOutcome marketOutcome)
    {
        if (!marketOutcome.reviewed || marketService == null || !marketService.IsInitialized)
        {
            return;
        }

        if (!IsInitialized || state.sourceMarketSeed != marketService.MarketSeed)
        {
            if (!TryInitializeFresh(null))
            {
                return;
            }
        }

        BistroBuilderSupplierMarketSnapshot marketSnapshot = marketService.CreateSnapshot();
        BistroBuilderSupplierMarketSnapshot reviewSnapshot =
            BistroBuilderSupplierCommercialIntelligenceEngine.CreateReviewScopedMarketSnapshot(
                marketSnapshot,
                marketOutcome.reviewDay);
        string error;
        if (!ProcessReviewSnapshot(reviewSnapshot, marketOutcome.reviewDay, out error))
        {
            lastInitializationError = error;
            Debug.LogError("2.3D no pudo procesar la revisión comercial: " + error);
        }
    }

    private bool ProcessReviewSnapshot(
        BistroBuilderSupplierMarketSnapshot marketSnapshot,
        int reviewDay,
        out string error)
    {
        error = null;
        BistroBuilderSupplierCommercialReviewOutcome outcome;
        List<BistroBuilderSupplierPromotionRecord> started;
        List<BistroBuilderSupplierPromotionRecord> expired;

        if (!BistroBuilderSupplierCommercialIntelligenceEngine.TryProcessMarketReview(
                state,
                marketSnapshot,
                supplierDatabase,
                ingredientDatabase,
                settings,
                reviewDay,
                out outcome,
                out started,
                out expired,
                out error))
        {
            return false;
        }

        if (!outcome.processed)
        {
            return true;
        }

        lastInitializationError = null;
        RebuildIndex();
        PublishEnded(expired);
        for (int index = 0; index < started.Count; index++)
        {
            PromotionStarted?.Invoke(started[index].DeepClone());
        }
        CommercialReviewProcessed?.Invoke(outcome);
        return true;
    }

    private void PublishEnded(List<BistroBuilderSupplierPromotionRecord> expired)
    {
        if (expired == null)
        {
            return;
        }
        for (int index = 0; index < expired.Count; index++)
        {
            if (expired[index] != null)
            {
                PromotionEnded?.Invoke(expired[index].DeepClone());
            }
        }
    }

    private void LoadStaticDependencies()
    {
        if (supplierDatabase == null)
        {
            supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                SupplierAuthoringResourcePath);
        }
        if (ingredientDatabase == null)
        {
            ingredientDatabase = Resources.Load<BistroBuilderIngredientAuthoringDatabase>(
                IngredientAuthoringResourcePath);
        }
        if (settings == null)
        {
            settings = Resources.Load<BistroBuilderSupplierCommercialIntelligenceSettings>(
                SettingsResourcePath);
        }
    }

    private void TryBindAndInitializeIfPossible()
    {
        LoadStaticDependencies();
        BindMarketIfNeeded();
        if (state == null && marketService != null && marketService.IsInitialized)
        {
            TryInitializeFresh(null);
        }
    }

    private void BindMarketIfNeeded()
    {
        BistroBuilderSupplierMarketService current = BistroBuilderSupplierMarketService.Instance;
        if (current == marketService)
        {
            return;
        }

        UnbindMarket();
        marketService = current;
        if (marketService != null)
        {
            marketService.MarketReviewed += HandleMarketReviewed;
        }
    }

    private void UnbindMarket()
    {
        if (marketService != null)
        {
            marketService.MarketReviewed -= HandleMarketReviewed;
            marketService = null;
        }
    }

    private void RebuildIndex()
    {
        activeByOfferId.Clear();
        if (state == null || state.activePromotions == null)
        {
            return;
        }
        for (int index = 0; index < state.activePromotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = state.activePromotions[index];
            if (promotion != null && !string.IsNullOrWhiteSpace(promotion.supplierOfferId) &&
                promotion.IsActiveOnDay(state.currentGameDay))
            {
                activeByOfferId[promotion.supplierOfferId] = promotion;
            }
        }
    }
}
