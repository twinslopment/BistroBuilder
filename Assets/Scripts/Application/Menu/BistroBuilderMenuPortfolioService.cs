using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de múltiples cartas y reglas de selección.
///
/// La carta operativa continúa en BistroBuilderRestaurantMenuService para no
/// romper comandas, cocina ni oferta. Este servicio conserva los perfiles
/// nombrados, resuelve cuál corresponde al contexto y proyecta únicamente el
/// perfil efectivo sobre la autoridad histórica.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Portfolio Service")]
public sealed class BistroBuilderMenuPortfolioService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1HI";
    public const string DefaultMenuId = "menu_default";
    public const string DefaultMenuName = "Carta principal";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuCollectionService collectionService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [SerializeField]
    private BistroBuilderMenuActivationContextService contextService;

    [SerializeField]
    private BistroBuilderMenuEditSessionService editSessionService;

    [Header("Portfolios por restaurante")]

    [SerializeField]
    private List<BistroBuilderRestaurantMenuPortfolioRuntimeState> portfolios =
        new List<BistroBuilderRestaurantMenuPortfolioRuntimeState>();

    [Header("Depuración")]

    [SerializeField]
    private bool logResolutionChanges = true;

    private readonly Dictionary<string, BistroBuilderRestaurantMenuPortfolioRuntimeState>
        byRestaurantId =
            new Dictionary<string, BistroBuilderRestaurantMenuPortfolioRuntimeState>(
                StringComparer.Ordinal
            );

    private readonly List<BistroBuilderRestaurantMenuRuntimeState>
        restaurantBuffer = new List<BistroBuilderRestaurantMenuRuntimeState>(4);

    private readonly List<BistroBuilderMenuItemRuntimeState> menuItemBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(64);

    private bool initialized;
    private bool subscribed;
    private bool suppressOperationalCapture;
    private int externalSynchronizationDepth;
    private bool resolutionPending;

    public event Action<BistroBuilderMenuResolutionResult> ActiveMenuChanged;
    public event Action PortfolioChanged;

    public BistroBuilderRestaurantMenuCollectionService CollectionService =>
        collectionService;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    public BistroBuilderMenuActivationContextService ContextService =>
        contextService;

    public BistroBuilderMenuEditSessionService EditSessionService =>
        editSessionService;

    public int PortfolioCount => portfolios != null ? portfolios.Count : 0;

    public string ActiveMenuId
    {
        get
        {
            if (collectionService != null &&
                byRestaurantId.TryGetValue(
                    collectionService.ActiveRestaurantId,
                    out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio
                ))
            {
                return portfolio.ActiveMenuId;
            }

            return string.Empty;
        }
    }

    public string ActiveMenuName
    {
        get
        {
            if (TryGetActiveMenuSnapshot(out BistroBuilderNamedMenuRuntimeState menu, out _))
            {
                return menu.DisplayName;
            }

            return string.Empty;
        }
    }

    public bool IsAutomaticResolutionSuspended =>
        externalSynchronizationDepth > 0;

    private void Awake()
    {
        if (!RebuildRuntimeIndexAndEnsureDefaults(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void Start()
    {
        if (!TryApplyCurrentResolution(false, out string error))
        {
            Debug.LogError(error, this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (collectionService == null || menuService == null ||
            catalogService == null || contextService == null)
        {
            error = "Faltan dependencias del portfolio de cartas.";
            return false;
        }

        if (!collectionService.ValidateConfiguration(out error) ||
            !menuService.ValidateConfiguration(out error) ||
            !catalogService.ValidateConfiguration(out error) ||
            !contextService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(collectionService.MenuService, menuService) ||
            !ReferenceEquals(collectionService.CatalogService, catalogService))
        {
            error = "El portfolio no comparte la carta y el catálogo canónicos.";
            return false;
        }

        if (portfolios == null || portfolios.Count == 0)
        {
            error = "No existe ningún portfolio de cartas.";
            return false;
        }

        HashSet<string> restaurantIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < portfolios.Count; index++)
        {
            BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio = portfolios[index];
            if (portfolio == null || !portfolio.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (!restaurantIds.Add(portfolio.RestaurantId))
            {
                error = "El portfolio repite el RestaurantId " + portfolio.RestaurantId + ".";
                return false;
            }
        }

        if (!restaurantIds.Contains(collectionService.ActiveRestaurantId))
        {
            error = "El portfolio no contiene el restaurante activo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Crea un portfolio base para cada restaurante histórico. La migración es
    /// no destructiva: la carta existente se convierte en Carta principal.
    /// </summary>
    public bool RebuildRuntimeIndexAndEnsureDefaults(out string error)
    {
        CacheDependenciesIfNeeded();
        initialized = false;

        if (collectionService == null || menuService == null ||
            catalogService == null || contextService == null)
        {
            error = "Faltan dependencias para inicializar cartas y reglas.";
            return false;
        }

        if (!collectionService.TryGetAllRestaurantSnapshots(
                restaurantBuffer,
                out error
            ))
        {
            return false;
        }

        if (portfolios == null)
        {
            portfolios = new List<BistroBuilderRestaurantMenuPortfolioRuntimeState>();
        }

        byRestaurantId.Clear();
        for (int index = 0; index < portfolios.Count; index++)
        {
            BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio = portfolios[index];
            if (portfolio == null ||
                !TryReconcilePortfolio(portfolio, out error) ||
                !portfolio.TryValidate(catalogService, out error) ||
                byRestaurantId.ContainsKey(portfolio.RestaurantId))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "La colección de portfolios es inválida.";
                }
                return false;
            }

            byRestaurantId.Add(portfolio.RestaurantId, portfolio);
        }

        for (int index = 0; index < restaurantBuffer.Count; index++)
        {
            BistroBuilderRestaurantMenuRuntimeState restaurant = restaurantBuffer[index];
            if (byRestaurantId.ContainsKey(restaurant.RestaurantId))
            {
                continue;
            }

            BistroBuilderNamedMenuRuntimeState defaultMenu =
                new BistroBuilderNamedMenuRuntimeState(
                    DefaultMenuId,
                    DefaultMenuName,
                    restaurant.Revision,
                    ToList(restaurant.Items),
                    ToList(restaurant.UnresolvedItems)
                );
            BistroBuilderRestaurantMenuPortfolioRuntimeState created =
                new BistroBuilderRestaurantMenuPortfolioRuntimeState(
                    restaurant.RestaurantId,
                    0,
                    DefaultMenuId,
                    DefaultMenuId,
                    string.Empty,
                    new[] { defaultMenu },
                    Array.Empty<BistroBuilderMenuActivationRuleRuntimeState>()
                );
            portfolios.Add(created);
            byRestaurantId.Add(created.RestaurantId, created);
        }

        portfolios.Sort(ComparePortfolios);
        initialized = true;
        Subscribe();
        error = string.Empty;
        return true;
    }

    public bool TryGetPortfolioSnapshot(
        string restaurantId,
        out BistroBuilderRestaurantMenuPortfolioRuntimeState snapshot,
        out string error
    )
    {
        snapshot = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(restaurantId);
        if (!byRestaurantId.TryGetValue(normalized, out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio))
        {
            error = "No existe portfolio para " + normalized + ".";
            return false;
        }

        snapshot = portfolio.Clone();
        error = string.Empty;
        return true;
    }

    public bool TryGetActivePortfolioSnapshot(
        out BistroBuilderRestaurantMenuPortfolioRuntimeState snapshot,
        out string error
    )
    {
        return TryGetPortfolioSnapshot(
            collectionService != null
                ? collectionService.ActiveRestaurantId
                : BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId,
            out snapshot,
            out error
        );
    }

    public bool TryGetAllPortfolioSnapshots(
        List<BistroBuilderRestaurantMenuPortfolioRuntimeState> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino de portfolios es nulo.";
            return false;
        }

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        destination.Clear();
        for (int index = 0; index < portfolios.Count; index++)
        {
            destination.Add(portfolios[index].Clone());
        }
        destination.Sort(ComparePortfolios);
        error = string.Empty;
        return true;
    }

    public bool TryGetActiveMenuSnapshot(
        out BistroBuilderNamedMenuRuntimeState snapshot,
        out string error
    )
    {
        snapshot = null;
        if (!TryGetActivePortfolioSnapshot(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ) ||
            !portfolio.TryGetMenu(portfolio.ActiveMenuId, out BistroBuilderNamedMenuRuntimeState menu))
        {
            if (string.IsNullOrEmpty(error))
            {
                error = "No existe la carta activa del portfolio.";
            }
            return false;
        }

        snapshot = menu.Clone();
        error = string.Empty;
        return true;
    }

    public bool TryResolveCurrent(
        out BistroBuilderMenuResolutionResult result,
        out BistroBuilderNamedMenuRuntimeState menu,
        out string error
    )
    {
        result = default(BistroBuilderMenuResolutionResult);
        menu = null;

        if (!EnsureInitialized(out error) ||
            !contextService.TryGetCurrentContext(
                out BistroBuilderMenuActivationContext context,
                out error
            ))
        {
            return false;
        }

        return TryResolve(
            collectionService.ActiveRestaurantId,
            context,
            out result,
            out menu,
            out error
        );
    }

    public bool TryResolve(
        string restaurantId,
        BistroBuilderMenuActivationContext context,
        out BistroBuilderMenuResolutionResult result,
        out BistroBuilderNamedMenuRuntimeState menu,
        out string error
    )
    {
        result = default(BistroBuilderMenuResolutionResult);
        menu = null;

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(restaurantId);
        if (!EnsureInitialized(out error) ||
            !byRestaurantId.TryGetValue(normalized, out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio))
        {
            if (string.IsNullOrEmpty(error))
            {
                error = "No existe el portfolio de " + normalized + ".";
            }
            return false;
        }

        string selectedMenuId = portfolio.FallbackMenuId;
        string selectedRuleId = string.Empty;
        bool manual = false;
        string description = "Carta base.";

        if (!string.IsNullOrEmpty(portfolio.ManualOverrideMenuId))
        {
            selectedMenuId = portfolio.ManualOverrideMenuId;
            manual = true;
            description = "Anulación manual.";
        }
        else
        {
            BistroBuilderMenuActivationRuleRuntimeState winner = null;
            for (int index = 0; index < portfolio.Rules.Count; index++)
            {
                BistroBuilderMenuActivationRuleRuntimeState candidate = portfolio.Rules[index];
                if (candidate != null && candidate.Matches(context) &&
                    BistroBuilderMenuActivationRuleRuntimeState.IsHigherPrecedence(candidate, winner))
                {
                    winner = candidate;
                }
            }

            if (winner != null)
            {
                selectedMenuId = winner.TargetMenuId;
                selectedRuleId = winner.RuleId;
                description = "Regla " + winner.DisplayName + ".";
            }
        }

        if (!portfolio.TryGetMenu(selectedMenuId, out menu))
        {
            error = "La resolución apunta a la carta inexistente " + selectedMenuId + ".";
            return false;
        }

        result = new BistroBuilderMenuResolutionResult(
            normalized,
            menu.MenuId,
            selectedRuleId,
            manual,
            description
        );
        error = string.Empty;
        return true;
    }

    public bool TryApplyCurrentResolution(bool force, out string error)
    {
        if (IsAutomaticResolutionSuspended)
        {
            resolutionPending = true;
            error = string.Empty;
            return true;
        }

        if (editSessionService != null && editSessionService.HasOpenSession)
        {
            resolutionPending = true;
            error = string.Empty;
            return true;
        }

        if (!TryResolveCurrent(
                out BistroBuilderMenuResolutionResult result,
                out BistroBuilderNamedMenuRuntimeState menu,
                out error
            ))
        {
            return false;
        }

        if (!byRestaurantId.TryGetValue(
                result.RestaurantId,
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio
            ))
        {
            error = "No existe el portfolio activo.";
            return false;
        }

        if (!force && string.Equals(
                portfolio.ActiveMenuId,
                result.MenuId,
                StringComparison.Ordinal
            ))
        {
            resolutionPending = false;
            error = string.Empty;
            return true;
        }

        if (!ApplyOperationalMenu(portfolio, menu, out error))
        {
            return false;
        }

        string previousMenuId = portfolio.ActiveMenuId;
        portfolio.SetActive(menu.MenuId);
        resolutionPending = false;

        if (logResolutionChanges)
        {
            Debug.Log(
                "Carta efectiva de " + result.RestaurantId + ": " +
                previousMenuId + " -> " + menu.MenuId + " (" +
                result.Description + ")",
                this
            );
        }

        ActiveMenuChanged?.Invoke(result);
        return true;
    }

    public bool TryCreateMenuFromActive(
        string displayName,
        bool activateManualOverride,
        out string menuId,
        out string error
    )
    {
        menuId = string.Empty;
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        if (!portfolio.TryGetMenu(
                portfolio.ActiveMenuId,
                out BistroBuilderNamedMenuRuntimeState sourceMenu
            ))
        {
            error = "No existe la carta activa que debe utilizarse como origen.";
            return false;
        }

        string normalizedName = NormalizeDisplayName(displayName);
        if (!TryValidateUniqueDisplayName(portfolio, normalizedName, string.Empty, out error))
        {
            return false;
        }

        menuId = GenerateStableMenuId();
        BistroBuilderNamedMenuRuntimeState created =
            new BistroBuilderNamedMenuRuntimeState(
                menuId,
                normalizedName,
                0,
                ToList(sourceMenu.Items),
                ToList(sourceMenu.UnresolvedItems)
            );

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.AddMenu(created);
        if (activateManualOverride)
        {
            candidate.SetManualOverride(menuId);
        }
        candidate.SortStable();

        if (!CommitPortfolioCandidate(candidate, true, out error))
        {
            menuId = string.Empty;
            return false;
        }

        return true;
    }

    public bool TryDuplicateMenu(
        string sourceMenuId,
        string displayName,
        bool activateManualOverride,
        out string menuId,
        out string error
    )
    {
        menuId = string.Empty;
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ) ||
            !portfolio.TryGetMenu(sourceMenuId, out BistroBuilderNamedMenuRuntimeState source))
        {
            if (string.IsNullOrEmpty(error)) error = "No existe la carta origen.";
            return false;
        }

        string normalizedName = NormalizeDisplayName(displayName);
        if (!TryValidateUniqueDisplayName(portfolio, normalizedName, string.Empty, out error))
        {
            return false;
        }

        menuId = GenerateStableMenuId();
        BistroBuilderNamedMenuRuntimeState duplicate =
            new BistroBuilderNamedMenuRuntimeState(
                menuId,
                normalizedName,
                0,
                ToList(source.Items),
                ToList(source.UnresolvedItems)
            );
        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.AddMenu(duplicate);
        if (activateManualOverride) candidate.SetManualOverride(menuId);
        candidate.SortStable();

        if (!CommitPortfolioCandidate(candidate, true, out error))
        {
            menuId = string.Empty;
            return false;
        }
        return true;
    }

    public bool TryRenameMenu(string menuId, string displayName, out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        string normalizedMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        string normalizedName = NormalizeDisplayName(displayName);
        if (!TryValidateUniqueDisplayName(portfolio, normalizedName, normalizedMenuId, out error))
        {
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        if (!candidate.TryGetMenu(normalizedMenuId, out BistroBuilderNamedMenuRuntimeState menu))
        {
            error = "No existe la carta indicada.";
            return false;
        }
        menu.Rename(normalizedName);
        return CommitPortfolioCandidate(candidate, false, out error);
    }

    public bool TryDeleteMenu(string menuId, out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        if (portfolio.MenuCount <= 1)
        {
            error = "Un restaurante debe conservar al menos una carta.";
            return false;
        }
        if (string.Equals(normalized, portfolio.FallbackMenuId, StringComparison.Ordinal) ||
            string.Equals(normalized, portfolio.ActiveMenuId, StringComparison.Ordinal) ||
            string.Equals(normalized, portfolio.ManualOverrideMenuId, StringComparison.Ordinal))
        {
            error = "No se puede eliminar una carta base, activa o forzada.";
            return false;
        }
        for (int index = 0; index < portfolio.Rules.Count; index++)
        {
            if (string.Equals(portfolio.Rules[index].TargetMenuId, normalized, StringComparison.Ordinal))
            {
                error = "No se puede eliminar una carta utilizada por reglas.";
                return false;
            }
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        if (!candidate.RemoveMenu(normalized))
        {
            error = "No existe la carta indicada.";
            return false;
        }
        return CommitPortfolioCandidate(candidate, false, out error);
    }

    public bool TrySetFallbackMenu(string menuId, out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ) || !portfolio.TryGetMenu(menuId, out _))
        {
            if (string.IsNullOrEmpty(error)) error = "No existe la carta indicada.";
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.SetFallback(menuId);
        return CommitPortfolioCandidate(candidate, true, out error);
    }

    public bool TrySetManualOverride(string menuId, out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ) || !portfolio.TryGetMenu(menuId, out _))
        {
            if (string.IsNullOrEmpty(error)) error = "No existe la carta indicada.";
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.SetManualOverride(menuId);
        return CommitPortfolioCandidate(candidate, true, out error);
    }

    public bool TryClearManualOverride(out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.ClearManualOverride();
        return CommitPortfolioCandidate(candidate, true, out error);
    }

    public bool TryUpsertRule(
        BistroBuilderMenuActivationRuleRuntimeState rule,
        out string error
    )
    {
        error = string.Empty;
        if (rule == null || !rule.TryValidate(out error) ||
            !TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        if (!portfolio.TryGetMenu(rule.TargetMenuId, out _))
        {
            error = "La regla apunta a una carta inexistente.";
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        candidate.UpsertRule(rule);
        candidate.SortStable();
        return CommitPortfolioCandidate(candidate, true, out error);
    }

    public bool TryDeleteRule(string ruleId, out string error)
    {
        if (!TryRequireEditableActivePortfolio(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error
            ))
        {
            return false;
        }

        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = portfolio.Clone();
        if (!candidate.RemoveRule(ruleId))
        {
            error = "No existe la regla indicada.";
            return false;
        }
        return CommitPortfolioCandidate(candidate, true, out error);
    }

    /// <summary>
    /// Sustitución atómica utilizada por menu.state v5. Se validan todos los
    /// portfolios y señales antes de tocar el estado operativo.
    /// </summary>
    public bool TryReplaceAllPortfolios(
        IList<BistroBuilderRestaurantMenuPortfolioRuntimeState> replacement,
        IList<string> activeEventIds,
        IList<string> activePromotionIds,
        bool notify,
        out string error
    )
    {
        error = string.Empty;
        if (replacement == null || replacement.Count == 0)
        {
            error = "El reemplazo de portfolios está vacío.";
            return false;
        }

        List<BistroBuilderRestaurantMenuPortfolioRuntimeState> candidates =
            new List<BistroBuilderRestaurantMenuPortfolioRuntimeState>(replacement.Count);
        Dictionary<string, BistroBuilderRestaurantMenuPortfolioRuntimeState> candidateIndex =
            new Dictionary<string, BistroBuilderRestaurantMenuPortfolioRuntimeState>(StringComparer.Ordinal);

        for (int index = 0; index < replacement.Count; index++)
        {
            BistroBuilderRestaurantMenuPortfolioRuntimeState candidate = replacement[index] != null
                ? replacement[index].Clone()
                : null;
            if (candidate == null ||
                !TryReconcilePortfolio(candidate, out error) ||
                !candidate.TryValidate(catalogService, out error) ||
                candidateIndex.ContainsKey(candidate.RestaurantId))
            {
                if (string.IsNullOrEmpty(error)) error = "El reemplazo contiene portfolios inválidos o duplicados.";
                return false;
            }
            candidates.Add(candidate);
            candidateIndex.Add(candidate.RestaurantId, candidate);
        }

        if (!candidateIndex.ContainsKey(collectionService.ActiveRestaurantId))
        {
            error = "El reemplazo no contiene el restaurante activo.";
            return false;
        }

        List<string> previousEvents = new List<string>();
        List<string> previousPromotions = new List<string>();
        contextService.CopySignalsTo(previousEvents, previousPromotions);
        List<BistroBuilderRestaurantMenuPortfolioRuntimeState> previous =
            new List<BistroBuilderRestaurantMenuPortfolioRuntimeState>();
        for (int index = 0; index < portfolios.Count; index++) previous.Add(portfolios[index].Clone());

        BeginExternalSynchronization();
        bool signalsApplied = contextService.TryReplaceSignals(
            activeEventIds,
            activePromotionIds,
            false,
            out error
        );
        if (!signalsApplied)
        {
            EndExternalSynchronization(false);
            return false;
        }

        portfolios.Clear();
        portfolios.AddRange(candidates);
        portfolios.Sort(ComparePortfolios);
        byRestaurantId.Clear();
        foreach (KeyValuePair<string, BistroBuilderRestaurantMenuPortfolioRuntimeState> pair in candidateIndex)
        {
            byRestaurantId.Add(pair.Key, pair.Value);
        }
        initialized = true;

        EndExternalSynchronization(false);
        if (!TryApplyCurrentResolution(true, out error))
        {
            BeginExternalSynchronization();
            portfolios.Clear();
            portfolios.AddRange(previous);
            byRestaurantId.Clear();
            for (int index = 0; index < previous.Count; index++)
            {
                byRestaurantId.Add(previous[index].RestaurantId, previous[index]);
            }
            contextService.TryReplaceSignals(previousEvents, previousPromotions, false, out _);
            EndExternalSynchronization(false);
            TryApplyCurrentResolution(true, out _);
            return false;
        }

        if (notify)
        {
            PortfolioChanged?.Invoke();
        }
        error = string.Empty;
        return true;
    }

    public void BeginExternalSynchronization()
    {
        externalSynchronizationDepth++;
    }

    public void EndExternalSynchronization(bool applyPending)
    {
        externalSynchronizationDepth = Math.Max(0, externalSynchronizationDepth - 1);
        if (applyPending && externalSynchronizationDepth == 0 && resolutionPending)
        {
            TryApplyCurrentResolution(false, out _);
        }
    }

    private bool CommitPortfolioCandidate(
        BistroBuilderRestaurantMenuPortfolioRuntimeState candidate,
        bool reResolve,
        out string error
    )
    {
        error = string.Empty;
        if (candidate == null ||
            !TryReconcilePortfolio(candidate, out error) ||
            !candidate.TryValidate(catalogService, out error))
        {
            return false;
        }

        if (!byRestaurantId.TryGetValue(candidate.RestaurantId, out BistroBuilderRestaurantMenuPortfolioRuntimeState previous))
        {
            error = "No existe el portfolio que se intenta sustituir.";
            return false;
        }

        int listIndex = portfolios.IndexOf(previous);
        if (listIndex < 0)
        {
            error = "El índice serializado del portfolio es incoherente.";
            return false;
        }

        portfolios[listIndex] = candidate;
        byRestaurantId[candidate.RestaurantId] = candidate;

        if (reResolve && string.Equals(candidate.RestaurantId, collectionService.ActiveRestaurantId, StringComparison.Ordinal) &&
            !TryApplyCurrentResolution(true, out error))
        {
            portfolios[listIndex] = previous;
            byRestaurantId[previous.RestaurantId] = previous;
            TryApplyCurrentResolution(true, out _);
            return false;
        }

        PortfolioChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    private bool ApplyOperationalMenu(
        BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        BistroBuilderNamedMenuRuntimeState menu,
        out string error
    )
    {
        if (!collectionService.TryGetAllRestaurantSnapshots(restaurantBuffer, out error))
        {
            return false;
        }

        bool replaced = false;
        for (int index = 0; index < restaurantBuffer.Count; index++)
        {
            BistroBuilderRestaurantMenuRuntimeState state = restaurantBuffer[index];
            if (!string.Equals(state.RestaurantId, portfolio.RestaurantId, StringComparison.Ordinal))
            {
                continue;
            }

            restaurantBuffer[index] = new BistroBuilderRestaurantMenuRuntimeState(
                state.RestaurantId,
                state.Revision + 1,
                ToList(menu.Items),
                ToList(menu.UnresolvedItems)
            );
            replaced = true;
            break;
        }

        if (!replaced)
        {
            error = "No se encontró el restaurante activo al aplicar la carta.";
            return false;
        }

        suppressOperationalCapture = true;
        try
        {
            return collectionService.TryReplaceAllRestaurantStates(
                restaurantBuffer,
                portfolio.RestaurantId,
                true,
                out error
            );
        }
        finally
        {
            suppressOperationalCapture = false;
        }
    }

    private void CaptureOperationalMenu()
    {
        if (suppressOperationalCapture || IsAutomaticResolutionSuspended ||
            collectionService == null || menuService == null ||
            !byRestaurantId.TryGetValue(
                collectionService.ActiveRestaurantId,
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio
            ) ||
            !portfolio.TryGetMenu(portfolio.ActiveMenuId, out BistroBuilderNamedMenuRuntimeState activeMenu) ||
            !menuService.TryGetSnapshot(menuItemBuffer, out _))
        {
            return;
        }

        activeMenu.ReplaceItems(
            menuItemBuffer,
            ToList(activeMenu.UnresolvedItems),
            true
        );
        PortfolioChanged?.Invoke();
    }

    private bool TryRequireEditableActivePortfolio(
        out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        out string error
    )
    {
        portfolio = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (editSessionService != null && editSessionService.HasOpenSession)
        {
            error = "Aplica, descarta o cierra el editor de carta antes de gestionar cartas y reglas.";
            return false;
        }

        if (!byRestaurantId.TryGetValue(collectionService.ActiveRestaurantId, out portfolio))
        {
            error = "No existe el portfolio del restaurante activo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateUniqueDisplayName(
        BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        string displayName,
        string exceptMenuId,
        out string error
    )
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "La carta necesita un nombre.";
            return false;
        }

        for (int index = 0; index < portfolio.Menus.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState menu = portfolio.Menus[index];
            if (!string.Equals(menu.MenuId, exceptMenuId, StringComparison.Ordinal) &&
                string.Equals(menu.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Ya existe una carta con ese nombre.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reclasifica todas las entradas según el catálogo efectivo actual. Esto
    /// permite cargar cartas antiguas o contenido temporalmente ausente sin
    /// perder datos ni confiar en la clasificación persistida.
    /// </summary>
    private bool TryReconcilePortfolio(
        BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        out string error
    )
    {
        error = string.Empty;
        if (portfolio == null || catalogService == null)
        {
            error = "No se puede reconciliar un portfolio sin catálogo.";
            return false;
        }

        for (int menuIndex = 0; menuIndex < portfolio.Menus.Count; menuIndex++)
        {
            BistroBuilderNamedMenuRuntimeState menu = portfolio.Menus[menuIndex];
            if (menu == null)
            {
                error = "El portfolio contiene una carta nula.";
                return false;
            }

            List<BistroBuilderMenuItemRuntimeState> resolved =
                new List<BistroBuilderMenuItemRuntimeState>();
            List<BistroBuilderMenuItemRuntimeState> unresolved =
                new List<BistroBuilderMenuItemRuntimeState>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            if (!ClassifyPortfolioItems(
                    menu.Items,
                    ids,
                    resolved,
                    unresolved,
                    out error
                ) ||
                !ClassifyPortfolioItems(
                    menu.UnresolvedItems,
                    ids,
                    resolved,
                    unresolved,
                    out error
                ))
            {
                return false;
            }

            menu.ReplaceItems(resolved, unresolved, false);
        }

        return true;
    }

    private bool ClassifyPortfolioItems(
        IReadOnlyList<BistroBuilderMenuItemRuntimeState> source,
        HashSet<string> ids,
        List<BistroBuilderMenuItemRuntimeState> resolved,
        List<BistroBuilderMenuItemRuntimeState> unresolved,
        out string error
    )
    {
        error = string.Empty;
        if (source == null)
        {
            error = "La colección de entradas de una carta es nula.";
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            if (item == null || !item.TryValidateStructure(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "La carta contiene una entrada nula.";
                }
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La carta contiene el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (catalogService.TryGetDefinition(item.DishId, out _))
            {
                resolved.Add(item.Clone());
            }
            else
            {
                unresolved.Add(item.Clone());
            }
        }

        return true;
    }

    private bool EnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }
        return RebuildRuntimeIndexAndEnsureDefaults(out error);
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        if (contextService != null)
        {
            contextService.ContextChanged += HandleContextChanged;
        }
        if (collectionService != null)
        {
            collectionService.ActiveRestaurantChanged += HandleActiveRestaurantChanged;
        }
        if (menuService != null)
        {
            menuService.MenuChanged += HandleMenuChanged;
        }
        if (editSessionService != null)
        {
            editSessionService.SessionChanged += HandleEditSessionChanged;
        }
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (contextService != null) contextService.ContextChanged -= HandleContextChanged;
        if (collectionService != null) collectionService.ActiveRestaurantChanged -= HandleActiveRestaurantChanged;
        if (menuService != null) menuService.MenuChanged -= HandleMenuChanged;
        if (editSessionService != null) editSessionService.SessionChanged -= HandleEditSessionChanged;
        subscribed = false;
    }

    private void HandleContextChanged()
    {
        if (!TryApplyCurrentResolution(false, out string error) &&
            !string.IsNullOrWhiteSpace(error))
        {
            Debug.LogError(error, this);
        }
    }

    private void HandleActiveRestaurantChanged(string previous, string current)
    {
        if (!byRestaurantId.ContainsKey(current))
        {
            RebuildRuntimeIndexAndEnsureDefaults(out _);
        }
        TryApplyCurrentResolution(true, out _);
    }

    private void HandleMenuChanged(BistroBuilderMenuChangedEvent change)
    {
        CaptureOperationalMenu();
    }

    private void HandleEditSessionChanged(BistroBuilderMenuEditSessionChangedEvent change)
    {
        if (editSessionService != null && !editSessionService.HasOpenSession && resolutionPending)
        {
            TryApplyCurrentResolution(false, out _);
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (collectionService == null) TryGetComponent(out collectionService);
        if (menuService == null) TryGetComponent(out menuService);
        if (catalogService == null) TryGetComponent(out catalogService);
        if (contextService == null) TryGetComponent(out contextService);
        if (editSessionService == null) TryGetComponent(out editSessionService);
    }

    private static List<BistroBuilderMenuItemRuntimeState> ToList(
        IReadOnlyList<BistroBuilderMenuItemRuntimeState> source
    )
    {
        List<BistroBuilderMenuItemRuntimeState> result =
            new List<BistroBuilderMenuItemRuntimeState>(source != null ? source.Count : 0);
        if (source == null) return result;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            result.Add(item != null ? item.Clone() : null);
        }
        return result;
    }

    private static string NormalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Trim();
        return normalized.Length <= 80 ? normalized : normalized.Substring(0, 80);
    }

    private static string GenerateStableMenuId()
    {
        return "menu_" + Guid.NewGuid().ToString("N").Substring(0, 16);
    }

    private static int ComparePortfolios(
        BistroBuilderRestaurantMenuPortfolioRuntimeState left,
        BistroBuilderRestaurantMenuPortfolioRuntimeState right
    )
    {
        return string.CompareOrdinal(
            left != null ? left.RestaurantId : string.Empty,
            right != null ? right.RestaurantId : string.Empty
        );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
