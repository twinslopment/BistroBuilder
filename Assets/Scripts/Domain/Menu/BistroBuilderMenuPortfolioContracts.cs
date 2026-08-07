using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipo visible de una regla de selección de carta. Las condiciones se
/// almacenan de forma explícita y pueden combinarse; el tipo evita que la UI
/// tenga que inferir la intención del jugador a partir de campos sueltos.
/// </summary>
public enum BistroBuilderMenuActivationRuleType
{
    Schedule = 0,
    Season = 1,
    Event = 2,
    Promotion = 3,
    Composite = 4
}

/// <summary>
/// Contexto inmutable utilizado para resolver la carta efectiva.
/// </summary>
public readonly struct BistroBuilderMenuActivationContext
{
    public int DateKey { get; }
    public DayOfWeek DayOfWeek { get; }
    public int MinuteOfDay { get; }
    public BistroBuilderMealServiceAvailability MealService { get; }
    public IReadOnlyCollection<string> ActiveEventIds { get; }
    public IReadOnlyCollection<string> ActivePromotionIds { get; }

    public BistroBuilderMenuActivationContext(
        int dateKey,
        DayOfWeek dayOfWeek,
        int minuteOfDay,
        BistroBuilderMealServiceAvailability mealService,
        IReadOnlyCollection<string> activeEventIds,
        IReadOnlyCollection<string> activePromotionIds
    )
    {
        DateKey = dateKey;
        DayOfWeek = dayOfWeek;
        MinuteOfDay = minuteOfDay;
        MealService = mealService;
        ActiveEventIds = activeEventIds;
        ActivePromotionIds = activePromotionIds;
    }
}

/// <summary>
/// Regla serializable y clonable de selección de carta.
///
/// Resolución determinista:
/// 1. prioridad mayor;
/// 2. mayor especificidad;
/// 3. RuleId ordinal ascendente.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuActivationRuleRuntimeState
{
    public const int MinimumPriority = -1000;
    public const int MaximumPriority = 1000;
    public const int AnyMinute = -1;

    [SerializeField]
    private string ruleId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [SerializeField]
    private bool enabled = true;

    [SerializeField]
    private string targetMenuId = string.Empty;

    [SerializeField]
    private int priority;

    [SerializeField]
    private BistroBuilderMenuActivationRuleType ruleType =
        BistroBuilderMenuActivationRuleType.Schedule;

    // yyyyMMdd. 0 significa sin límite.
    [SerializeField]
    private int startDateKey;

    [SerializeField]
    private int endDateKey;

    // Sunday = bit 0 ... Saturday = bit 6. 0 significa cualquier día.
    [SerializeField]
    private int weekdayMask;

    // None significa cualquier servicio concreto.
    [SerializeField]
    private BistroBuilderMealServiceAvailability mealServices =
        BistroBuilderMealServiceAvailability.None;

    // Ambos -1 significan cualquier hora. Se admite un tramo nocturno donde
    // startMinute > endMinute.
    [SerializeField]
    private int startMinute = AnyMinute;

    [SerializeField]
    private int endMinute = AnyMinute;

    [SerializeField]
    private string requiredEventId = string.Empty;

    [SerializeField]
    private string requiredPromotionId = string.Empty;

    public string RuleId => ruleId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public bool Enabled => enabled;
    public string TargetMenuId => targetMenuId ?? string.Empty;
    public int Priority => priority;
    public BistroBuilderMenuActivationRuleType RuleType => ruleType;
    public int StartDateKey => startDateKey;
    public int EndDateKey => endDateKey;
    public int WeekdayMask => weekdayMask;
    public BistroBuilderMealServiceAvailability MealServices => mealServices;
    public int StartMinute => startMinute;
    public int EndMinute => endMinute;
    public string RequiredEventId => requiredEventId ?? string.Empty;
    public string RequiredPromotionId => requiredPromotionId ?? string.Empty;

    public int Specificity
    {
        get
        {
            int value = 0;
            if (startDateKey != 0 || endDateKey != 0) value++;
            if (weekdayMask != 0) value++;
            if (mealServices != BistroBuilderMealServiceAvailability.None) value++;
            if (startMinute != AnyMinute || endMinute != AnyMinute) value++;
            if (!string.IsNullOrEmpty(RequiredEventId)) value++;
            if (!string.IsNullOrEmpty(RequiredPromotionId)) value++;
            return value;
        }
    }

    public BistroBuilderMenuActivationRuleRuntimeState()
    {
    }

    public BistroBuilderMenuActivationRuleRuntimeState(
        string ruleId,
        string displayName,
        bool enabled,
        string targetMenuId,
        int priority,
        BistroBuilderMenuActivationRuleType ruleType,
        int startDateKey,
        int endDateKey,
        int weekdayMask,
        BistroBuilderMealServiceAvailability mealServices,
        int startMinute,
        int endMinute,
        string requiredEventId,
        string requiredPromotionId
    )
    {
        this.ruleId = BistroBuilderMenuIdUtility.NormalizeStableId(ruleId);
        this.displayName = NormalizeDisplayName(displayName);
        this.enabled = enabled;
        this.targetMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(
            targetMenuId
        );
        this.priority = priority;
        this.ruleType = ruleType;
        this.startDateKey = startDateKey;
        this.endDateKey = endDateKey;
        this.weekdayMask = weekdayMask;
        this.mealServices = mealServices;
        this.startMinute = startMinute;
        this.endMinute = endMinute;
        this.requiredEventId = BistroBuilderMenuIdUtility.NormalizeStableId(
            requiredEventId
        );
        this.requiredPromotionId = BistroBuilderMenuIdUtility.NormalizeStableId(
            requiredPromotionId
        );
    }

    public BistroBuilderMenuActivationRuleRuntimeState Clone()
    {
        return new BistroBuilderMenuActivationRuleRuntimeState(
            RuleId,
            DisplayName,
            enabled,
            TargetMenuId,
            priority,
            ruleType,
            startDateKey,
            endDateKey,
            weekdayMask,
            mealServices,
            startMinute,
            endMinute,
            RequiredEventId,
            RequiredPromotionId
        );
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(RuleId))
        {
            error = "La regla contiene un RuleId inválido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 80)
        {
            error = "La regla " + RuleId + " no tiene un nombre válido.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(TargetMenuId))
        {
            error = "La regla " + RuleId + " apunta a un MenuId inválido.";
            return false;
        }

        if (priority < MinimumPriority || priority > MaximumPriority)
        {
            error = "La prioridad de " + RuleId + " está fuera de rango.";
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderMenuActivationRuleType), ruleType))
        {
            error = "La regla " + RuleId + " tiene un tipo desconocido.";
            return false;
        }

        if (!TryValidateDateRange(startDateKey, endDateKey))
        {
            error = "La regla " + RuleId + " contiene un rango de fechas inválido.";
            return false;
        }

        if (weekdayMask < 0 || weekdayMask > 0x7F)
        {
            error = "La regla " + RuleId + " contiene días de semana inválidos.";
            return false;
        }

        if (mealServices != BistroBuilderMealServiceAvailability.None &&
            !BistroBuilderMenuIdUtility.IsValidServiceMask(mealServices, false))
        {
            error = "La regla " + RuleId + " contiene servicios inválidos.";
            return false;
        }

        bool anyTime = startMinute == AnyMinute && endMinute == AnyMinute;
        bool concreteTime = startMinute >= 0 && startMinute <= 1439 &&
                            endMinute >= 0 && endMinute <= 1439;
        if (!anyTime && !concreteTime)
        {
            error = "La regla " + RuleId + " contiene una franja horaria incompleta.";
            return false;
        }

        if (!string.IsNullOrEmpty(RequiredEventId) &&
            !BistroBuilderMenuIdUtility.IsValidStableId(RequiredEventId))
        {
            error = "La regla " + RuleId + " contiene un EventId inválido.";
            return false;
        }

        if (!string.IsNullOrEmpty(RequiredPromotionId) &&
            !BistroBuilderMenuIdUtility.IsValidStableId(RequiredPromotionId))
        {
            error = "La regla " + RuleId + " contiene un PromotionId inválido.";
            return false;
        }

        if (ruleType == BistroBuilderMenuActivationRuleType.Season &&
            startDateKey == 0 && endDateKey == 0)
        {
            error = "Una regla de temporada necesita al menos un límite de fecha.";
            return false;
        }

        if (ruleType == BistroBuilderMenuActivationRuleType.Event &&
            string.IsNullOrEmpty(RequiredEventId))
        {
            error = "Una regla de evento necesita un EventId.";
            return false;
        }

        if (ruleType == BistroBuilderMenuActivationRuleType.Promotion &&
            string.IsNullOrEmpty(RequiredPromotionId))
        {
            error = "Una regla de promoción necesita un PromotionId.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool Matches(BistroBuilderMenuActivationContext context)
    {
        if (!enabled)
        {
            return false;
        }

        if (startDateKey != 0 && context.DateKey < startDateKey)
        {
            return false;
        }

        if (endDateKey != 0 && context.DateKey > endDateKey)
        {
            return false;
        }

        if (weekdayMask != 0)
        {
            int bit = 1 << (int)context.DayOfWeek;
            if ((weekdayMask & bit) == 0)
            {
                return false;
            }
        }

        if (mealServices != BistroBuilderMealServiceAvailability.None &&
            (mealServices & context.MealService) == 0)
        {
            return false;
        }

        if (startMinute != AnyMinute)
        {
            bool inTimeRange = startMinute <= endMinute
                ? context.MinuteOfDay >= startMinute &&
                  context.MinuteOfDay <= endMinute
                : context.MinuteOfDay >= startMinute ||
                  context.MinuteOfDay <= endMinute;

            if (!inTimeRange)
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(RequiredEventId) &&
            !Contains(context.ActiveEventIds, RequiredEventId))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(RequiredPromotionId) &&
            !Contains(context.ActivePromotionIds, RequiredPromotionId))
        {
            return false;
        }

        return true;
    }

    public static bool IsHigherPrecedence(
        BistroBuilderMenuActivationRuleRuntimeState candidate,
        BistroBuilderMenuActivationRuleRuntimeState current
    )
    {
        if (candidate == null)
        {
            return false;
        }

        if (current == null)
        {
            return true;
        }

        if (candidate.Priority != current.Priority)
        {
            return candidate.Priority > current.Priority;
        }

        if (candidate.Specificity != current.Specificity)
        {
            return candidate.Specificity > current.Specificity;
        }

        return string.CompareOrdinal(candidate.RuleId, current.RuleId) < 0;
    }

    private static bool Contains(
        IReadOnlyCollection<string> source,
        string value
    )
    {
        if (source == null)
        {
            return false;
        }

        foreach (string item in source)
        {
            if (string.Equals(item, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryValidateDateRange(int start, int end)
    {
        if (start == 0 && end == 0)
        {
            return true;
        }

        if ((start != 0 && !TryDateKeyToDate(start, out _)) ||
            (end != 0 && !TryDateKeyToDate(end, out _)))
        {
            return false;
        }

        return start == 0 || end == 0 || start <= end;
    }

    public static bool TryDateKeyToDate(int dateKey, out DateTime date)
    {
        date = default(DateTime);
        int year = dateKey / 10000;
        int month = dateKey / 100 % 100;
        int day = dateKey % 100;

        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string NormalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim();
        return normalized.Length <= 80
            ? normalized
            : normalized.Substring(0, 80);
    }
}

/// <summary>
/// Carta nombrada e independiente dentro de un restaurante.
/// </summary>
[Serializable]
public sealed class BistroBuilderNamedMenuRuntimeState
{
    [SerializeField]
    private string menuId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [SerializeField]
    private int revision;

    [SerializeField]
    private List<BistroBuilderMenuItemRuntimeState> items =
        new List<BistroBuilderMenuItemRuntimeState>();

    [SerializeField]
    private List<BistroBuilderMenuItemRuntimeState> unresolvedItems =
        new List<BistroBuilderMenuItemRuntimeState>();

    public string MenuId => menuId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public int Revision => revision;
    public IReadOnlyList<BistroBuilderMenuItemRuntimeState> Items => items;
    public IReadOnlyList<BistroBuilderMenuItemRuntimeState> UnresolvedItems => unresolvedItems;
    public int ItemCount => items != null ? items.Count : 0;
    public int UnresolvedItemCount => unresolvedItems != null ? unresolvedItems.Count : 0;

    public BistroBuilderNamedMenuRuntimeState()
    {
    }

    public BistroBuilderNamedMenuRuntimeState(
        string menuId,
        string displayName,
        int revision,
        IList<BistroBuilderMenuItemRuntimeState> items,
        IList<BistroBuilderMenuItemRuntimeState> unresolvedItems
    )
    {
        this.menuId = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        this.displayName = NormalizeDisplayName(displayName);
        this.revision = Math.Max(0, revision);
        CopyItems(items, this.items);
        CopyItems(unresolvedItems, this.unresolvedItems);
    }

    public BistroBuilderNamedMenuRuntimeState Clone()
    {
        return new BistroBuilderNamedMenuRuntimeState(
            MenuId,
            DisplayName,
            revision,
            items,
            unresolvedItems
        );
    }

    public bool TryValidate(
        BistroBuilderDishCatalogService catalogService,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(MenuId))
        {
            error = "La carta nombrada contiene un MenuId inválido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 80)
        {
            error = "La carta " + MenuId + " no tiene un nombre válido.";
            return false;
        }

        if (revision < 0 || items == null || unresolvedItems == null)
        {
            error = "La carta " + MenuId + " contiene estado inválido.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta el catálogo para validar la carta " + MenuId + ".";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        if (!ValidateItems(items, true, catalogService, ids, out error) ||
            !ValidateItems(unresolvedItems, false, catalogService, ids, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void Rename(string nextName)
    {
        displayName = NormalizeDisplayName(nextName);
    }

    internal void ReplaceItems(
        IList<BistroBuilderMenuItemRuntimeState> resolved,
        IList<BistroBuilderMenuItemRuntimeState> unresolved,
        bool incrementRevision
    )
    {
        CopyItems(resolved, items);
        CopyItems(unresolved, unresolvedItems);
        if (incrementRevision)
        {
            revision++;
        }
    }

    private static bool ValidateItems(
        IList<BistroBuilderMenuItemRuntimeState> source,
        bool mustResolve,
        BistroBuilderDishCatalogService catalogService,
        HashSet<string> ids,
        out string error
    )
    {
        error = string.Empty;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            if (item == null || !item.TryValidateStructure(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "La carta nombrada contiene una entrada nula.";
                }
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La carta contiene el DishId duplicado " + item.DishId + ".";
                return false;
            }

            bool resolves = catalogService.TryGetDefinition(item.DishId, out _);
            if (resolves != mustResolve)
            {
                error = "La clasificación resuelta/no resuelta de " + item.DishId + " es incoherente.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static void CopyItems(
        IList<BistroBuilderMenuItemRuntimeState> source,
        List<BistroBuilderMenuItemRuntimeState> destination
    )
    {
        destination.Clear();
        if (source == null) return;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            destination.Add(item != null ? item.Clone() : null);
        }
    }

    private static string NormalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Trim();
        return normalized.Length <= 80 ? normalized : normalized.Substring(0, 80);
    }
}

/// <summary>
/// Portfolio completo de cartas y reglas de un restaurante.
/// </summary>
[Serializable]
public sealed class BistroBuilderRestaurantMenuPortfolioRuntimeState
{
    [SerializeField]
    private string restaurantId = string.Empty;

    [SerializeField]
    private int revision;

    [SerializeField]
    private string fallbackMenuId = string.Empty;

    [SerializeField]
    private string activeMenuId = string.Empty;

    [SerializeField]
    private string manualOverrideMenuId = string.Empty;

    [SerializeField]
    private List<BistroBuilderNamedMenuRuntimeState> menus =
        new List<BistroBuilderNamedMenuRuntimeState>();

    [SerializeField]
    private List<BistroBuilderMenuActivationRuleRuntimeState> rules =
        new List<BistroBuilderMenuActivationRuleRuntimeState>();

    public string RestaurantId => restaurantId ?? string.Empty;
    public int Revision => revision;
    public string FallbackMenuId => fallbackMenuId ?? string.Empty;
    public string ActiveMenuId => activeMenuId ?? string.Empty;
    public string ManualOverrideMenuId => manualOverrideMenuId ?? string.Empty;
    public IReadOnlyList<BistroBuilderNamedMenuRuntimeState> Menus => menus;
    public IReadOnlyList<BistroBuilderMenuActivationRuleRuntimeState> Rules => rules;
    public int MenuCount => menus != null ? menus.Count : 0;
    public int RuleCount => rules != null ? rules.Count : 0;

    public BistroBuilderRestaurantMenuPortfolioRuntimeState()
    {
    }

    public BistroBuilderRestaurantMenuPortfolioRuntimeState(
        string restaurantId,
        int revision,
        string fallbackMenuId,
        string activeMenuId,
        string manualOverrideMenuId,
        IList<BistroBuilderNamedMenuRuntimeState> menus,
        IList<BistroBuilderMenuActivationRuleRuntimeState> rules
    )
    {
        this.restaurantId = BistroBuilderMenuIdUtility.NormalizeStableId(restaurantId);
        this.revision = Math.Max(0, revision);
        this.fallbackMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(fallbackMenuId);
        this.activeMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(activeMenuId);
        this.manualOverrideMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(manualOverrideMenuId);
        CopyMenus(menus, this.menus);
        CopyRules(rules, this.rules);
    }

    public BistroBuilderRestaurantMenuPortfolioRuntimeState Clone()
    {
        return new BistroBuilderRestaurantMenuPortfolioRuntimeState(
            RestaurantId,
            revision,
            FallbackMenuId,
            ActiveMenuId,
            ManualOverrideMenuId,
            menus,
            rules
        );
    }

    public bool TryValidate(
        BistroBuilderDishCatalogService catalogService,
        out string error
    )
    {
        error = string.Empty;
        if (!BistroBuilderMenuIdUtility.IsValidStableId(RestaurantId))
        {
            error = "El portfolio contiene un RestaurantId inválido.";
            return false;
        }

        if (revision < 0 || menus == null || menus.Count == 0 || rules == null)
        {
            error = "El portfolio de " + RestaurantId + " está vacío o es inválido.";
            return false;
        }

        Dictionary<string, BistroBuilderNamedMenuRuntimeState> byMenuId =
            new Dictionary<string, BistroBuilderNamedMenuRuntimeState>(StringComparer.Ordinal);
        HashSet<string> displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < menus.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState menu = menus[index];
            if (menu == null || !menu.TryValidate(catalogService, out error))
            {
                return false;
            }

            if (byMenuId.ContainsKey(menu.MenuId))
            {
                error = "El portfolio contiene el MenuId duplicado " + menu.MenuId + ".";
                return false;
            }
            byMenuId.Add(menu.MenuId, menu);

            if (!displayNames.Add(menu.DisplayName))
            {
                error = "El portfolio contiene dos cartas con el mismo nombre.";
                return false;
            }
        }

        if (!byMenuId.ContainsKey(FallbackMenuId) ||
            !byMenuId.ContainsKey(ActiveMenuId))
        {
            error = "El portfolio no contiene su carta base o activa.";
            return false;
        }

        if (!string.IsNullOrEmpty(ManualOverrideMenuId) &&
            !byMenuId.ContainsKey(ManualOverrideMenuId))
        {
            error = "La anulación manual apunta a una carta inexistente.";
            return false;
        }

        HashSet<string> ruleIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < rules.Count; index++)
        {
            BistroBuilderMenuActivationRuleRuntimeState rule = rules[index];
            if (rule == null || !rule.TryValidate(out error))
            {
                return false;
            }

            if (!ruleIds.Add(rule.RuleId))
            {
                error = "El portfolio contiene el RuleId duplicado " + rule.RuleId + ".";
                return false;
            }

            if (!byMenuId.ContainsKey(rule.TargetMenuId))
            {
                error = "La regla " + rule.RuleId + " apunta a una carta inexistente.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetMenu(string menuId, out BistroBuilderNamedMenuRuntimeState menu)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        for (int index = 0; index < menus.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState candidate = menus[index];
            if (candidate != null && string.Equals(candidate.MenuId, normalized, StringComparison.Ordinal))
            {
                menu = candidate;
                return true;
            }
        }

        menu = null;
        return false;
    }

    public bool TryGetRule(string ruleId, out BistroBuilderMenuActivationRuleRuntimeState rule)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(ruleId);
        for (int index = 0; index < rules.Count; index++)
        {
            BistroBuilderMenuActivationRuleRuntimeState candidate = rules[index];
            if (candidate != null && string.Equals(candidate.RuleId, normalized, StringComparison.Ordinal))
            {
                rule = candidate;
                return true;
            }
        }

        rule = null;
        return false;
    }

    internal void SetFallback(string menuId)
    {
        fallbackMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        revision++;
    }

    internal void SetActive(string menuId)
    {
        activeMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
    }

    internal void SetManualOverride(string menuId)
    {
        manualOverrideMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        revision++;
    }

    internal void ClearManualOverride()
    {
        manualOverrideMenuId = string.Empty;
        revision++;
    }

    internal void AddMenu(BistroBuilderNamedMenuRuntimeState menu)
    {
        menus.Add(menu);
        revision++;
    }

    internal bool RemoveMenu(string menuId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(menuId);
        for (int index = 0; index < menus.Count; index++)
        {
            if (menus[index] != null && string.Equals(menus[index].MenuId, normalized, StringComparison.Ordinal))
            {
                menus.RemoveAt(index);
                revision++;
                return true;
            }
        }
        return false;
    }

    internal void UpsertRule(BistroBuilderMenuActivationRuleRuntimeState rule)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            if (rules[index] != null && string.Equals(rules[index].RuleId, rule.RuleId, StringComparison.Ordinal))
            {
                rules[index] = rule.Clone();
                revision++;
                return;
            }
        }
        rules.Add(rule.Clone());
        revision++;
    }

    internal bool RemoveRule(string ruleId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(ruleId);
        for (int index = 0; index < rules.Count; index++)
        {
            if (rules[index] != null && string.Equals(rules[index].RuleId, normalized, StringComparison.Ordinal))
            {
                rules.RemoveAt(index);
                revision++;
                return true;
            }
        }
        return false;
    }

    internal void SortStable()
    {
        menus.Sort((left, right) => string.CompareOrdinal(left != null ? left.MenuId : string.Empty, right != null ? right.MenuId : string.Empty));
        rules.Sort((left, right) => string.CompareOrdinal(left != null ? left.RuleId : string.Empty, right != null ? right.RuleId : string.Empty));
    }

    private static void CopyMenus(
        IList<BistroBuilderNamedMenuRuntimeState> source,
        List<BistroBuilderNamedMenuRuntimeState> destination
    )
    {
        destination.Clear();
        if (source == null) return;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState menu = source[index];
            destination.Add(menu != null ? menu.Clone() : null);
        }
    }

    private static void CopyRules(
        IList<BistroBuilderMenuActivationRuleRuntimeState> source,
        List<BistroBuilderMenuActivationRuleRuntimeState> destination
    )
    {
        destination.Clear();
        if (source == null) return;
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuActivationRuleRuntimeState rule = source[index];
            destination.Add(rule != null ? rule.Clone() : null);
        }
    }
}

/// <summary>
/// Resultado observable de la resolución de una carta.
/// </summary>
public readonly struct BistroBuilderMenuResolutionResult
{
    public string RestaurantId { get; }
    public string MenuId { get; }
    public string RuleId { get; }
    public bool UsedManualOverride { get; }
    public string Description { get; }

    public BistroBuilderMenuResolutionResult(
        string restaurantId,
        string menuId,
        string ruleId,
        bool usedManualOverride,
        string description
    )
    {
        RestaurantId = restaurantId ?? string.Empty;
        MenuId = menuId ?? string.Empty;
        RuleId = ruleId ?? string.Empty;
        UsedManualOverride = usedManualOverride;
        Description = description ?? string.Empty;
    }
}
