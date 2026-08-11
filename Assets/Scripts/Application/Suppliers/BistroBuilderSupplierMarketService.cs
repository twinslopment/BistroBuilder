using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime del estado de mercado 2.3C.
///
/// supplier.catalog conserva producto/precio base. Este servicio conserva
/// únicamente el estado dinámico de mercado: precio actual, disponibilidad,
/// revisiones e historial. No modifica inventario, recepciones ni pedidos.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-120)]
public sealed class BistroBuilderSupplierMarketService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath =
        "BistroBuilder/Suppliers/Authoring/BistroBuilderSupplierAuthoringDatabase";
    public const string MarketSettingsResourcePath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierMarketSettings";

    private static BistroBuilderSupplierMarketService instance;

    private readonly Dictionary<string, BistroBuilderSupplierMarketOfferState> stateByOfferId =
        new Dictionary<string, BistroBuilderSupplierMarketOfferState>(StringComparer.Ordinal);
    private readonly BistroBuilderSupplierMarketGameDayResolver dayResolver =
        new BistroBuilderSupplierMarketGameDayResolver();

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderSupplierMarketSettings settings;
    private BistroBuilderSupplierMarketSnapshot state;
    private float nextClockPollTime;
    private string lastInitializationError;

    public static BistroBuilderSupplierMarketService Instance => instance;
    public bool IsInitialized => state != null && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;
    public int CurrentGameDay => state != null ? state.currentGameDay : 0;
    public int LastReviewGameDay => state != null ? state.lastReviewGameDay : 0;
    public int NextReviewGameDay => state != null ? state.nextReviewGameDay : 0;
    public long MarketRevision => state != null ? state.marketRevision : 0L;
    public ulong MarketSeed => state != null ? state.marketSeed : 0UL;
    public int OfferCount => state != null && state.offerStates != null ? state.offerStates.Count : 0;
    public string ClockDiagnostic => dayResolver.Diagnostic;

    public event Action<BistroBuilderSupplierMarketReviewOutcome> MarketReviewed;
    public event Action<BistroBuilderSupplierMarketChangeRecord> MarketOfferChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>() != null)
        {
            return;
        }

        GameObject host = new GameObject("BistroBuilderSupplierMarketService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierMarketService>();
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
        TryInitializeFresh(null);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (!IsInitialized || Time.unscaledTime < nextClockPollTime)
        {
            return;
        }

        nextClockPollTime = Time.unscaledTime + 0.25f;
        int gameDay;
        if (dayResolver.TryGetGameDay(out gameDay) && gameDay >= 1)
        {
            string error;
            TryAdvanceToGameDay(gameDay, out error);
        }
    }

    public bool TryInitializeFresh(ulong? explicitSeed)
    {
        lastInitializationError = null;
        supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
            SupplierAuthoringResourcePath);
        settings = Resources.Load<BistroBuilderSupplierMarketSettings>(
            MarketSettingsResourcePath);

        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring en Resources.";
            return false;
        }

        if (settings == null)
        {
            lastInitializationError = "Falta supplier.market.settings en Resources. Ejecuta el instalador 2.3C.";
            return false;
        }

        ulong seed = explicitSeed.HasValue && explicitSeed.Value != 0UL
            ? explicitSeed.Value
            : BistroBuilderSupplierMarketEngine.StableSeedFromText(
                Guid.NewGuid().ToString("N"),
                settings.DeterministicSalt);

        state = BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(
            supplierDatabase,
            settings,
            seed,
            1);
        RebuildIndex();
        dayResolver.Reset(1);
        return true;
    }

    public bool TryAdvanceToGameDay(int gameDay, out string error)
    {
        error = null;
        if (!IsInitialized)
        {
            error = lastInitializationError ?? "El mercado no está inicializado.";
            return false;
        }

        if (gameDay < state.currentGameDay)
        {
            // Una carga/restauración real usará TryRestoreSnapshot en 2.3J.
            // El reloj no hace retroceder silenciosamente el mercado.
            return true;
        }

        List<BistroBuilderSupplierMarketReviewOutcome> outcomes;
        if (!BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                state,
                supplierDatabase,
                settings,
                gameDay,
                out outcomes,
                out error))
        {
            return false;
        }

        RebuildIndex();

        for (int outcomeIndex = 0; outcomeIndex < outcomes.Count; outcomeIndex++)
        {
            BistroBuilderSupplierMarketReviewOutcome outcome = outcomes[outcomeIndex];
            PublishReviewEvents(outcome);
            MarketReviewed?.Invoke(outcome);
        }

        return true;
    }

    public bool TryGetOfferState(
        string supplierOfferId,
        out BistroBuilderSupplierMarketOfferState marketState)
    {
        marketState = null;
        if (!IsInitialized || string.IsNullOrWhiteSpace(supplierOfferId))
        {
            return false;
        }

        BistroBuilderSupplierMarketOfferState stored;
        if (!stateByOfferId.TryGetValue(supplierOfferId, out stored) || stored == null)
        {
            return false;
        }

        marketState = stored.DeepClone();
        return true;
    }

    public long GetCurrentPriceCents(string supplierOfferId, long fallbackBasePriceCents = 0L)
    {
        BistroBuilderSupplierMarketOfferState marketState;
        return TryGetOfferState(supplierOfferId, out marketState)
            ? marketState.currentPriceCents
            : fallbackBasePriceCents;
    }

    public bool IsAvailableForNewOrders(string supplierOfferId)
    {
        BistroBuilderSupplierMarketOfferState marketState;
        return TryGetOfferState(supplierOfferId, out marketState) &&
               marketState.availability !=
                   BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado;
    }

    public int CopyOfferStates(List<BistroBuilderSupplierMarketOfferState> buffer)
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

        for (int index = 0; index < state.offerStates.Count; index++)
        {
            if (state.offerStates[index] != null)
            {
                buffer.Add(state.offerStates[index].DeepClone());
            }
        }

        return buffer.Count;
    }

    public int CopyRecentChanges(List<BistroBuilderSupplierMarketChangeRecord> buffer)
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

        for (int index = 0; index < state.changes.Count; index++)
        {
            if (state.changes[index] != null)
            {
                buffer.Add(state.changes[index].DeepClone());
            }
        }

        return buffer.Count;
    }

    public BistroBuilderSupplierMarketSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderSupplierMarketSnapshot candidate,
        out string error)
    {
        error = null;
        if (candidate == null)
        {
            error = "Snapshot de mercado nulo.";
            return false;
        }

        if (supplierDatabase == null)
        {
            supplierDatabase = Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                SupplierAuthoringResourcePath);
        }

        if (settings == null)
        {
            settings = Resources.Load<BistroBuilderSupplierMarketSettings>(
                MarketSettingsResourcePath);
        }

        BistroBuilderSupplierMarketSnapshot owned = candidate.DeepClone();
        if (!BistroBuilderSupplierMarketEngine.ValidateSnapshotAgainstAuthoring(
                owned,
                supplierDatabase,
                out error))
        {
            return false;
        }

        state = owned;
        lastInitializationError = null;
        RebuildIndex();
        dayResolver.ForceSyntheticDayForRestore(state.currentGameDay);
        return true;
    }

    private void RebuildIndex()
    {
        stateByOfferId.Clear();
        if (state == null || state.offerStates == null)
        {
            return;
        }

        for (int index = 0; index < state.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState entry = state.offerStates[index];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.supplierOfferId))
            {
                stateByOfferId[entry.supplierOfferId] = entry;
            }
        }
    }

    private void PublishReviewEvents(BistroBuilderSupplierMarketReviewOutcome outcome)
    {
        if (state == null || state.changes == null || outcome.reviewDay <= 0)
        {
            return;
        }

        for (int index = 0; index < state.changes.Count; index++)
        {
            BistroBuilderSupplierMarketChangeRecord change = state.changes[index];
            if (change != null && change.gameDay == outcome.reviewDay)
            {
                MarketOfferChanged?.Invoke(change.DeepClone());
            }
        }
    }
}
