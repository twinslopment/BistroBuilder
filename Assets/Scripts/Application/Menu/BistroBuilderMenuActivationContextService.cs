using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fuente única del contexto que utilizan las reglas de carta.
///
/// Combina calendario, reloj, servicio del día y señales explícitas de evento
/// o promoción. No decide qué carta se activa; solo publica un contexto
/// determinista y persistible.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Activation Context Service")]
public sealed class BistroBuilderMenuActivationContextService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1HI-CONTEXT";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private BistroBuilderMenuOfferService offerService;

    [Header("Señales activas")]

    [SerializeField]
    private List<string> activeEventIds = new List<string>();

    [SerializeField]
    private List<string> activePromotionIds = new List<string>();

    private readonly HashSet<string> eventIdSet =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly HashSet<string> promotionIdSet =
        new HashSet<string>(StringComparer.Ordinal);

    private bool subscribed;

    public event Action ContextChanged;

    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;

    public GameClock GameClock => gameClock;

    public BistroBuilderMenuOfferService OfferService => offerService;

    public IReadOnlyList<string> ActiveEventIds => activeEventIds;

    public IReadOnlyList<string> ActivePromotionIds => activePromotionIds;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        RebuildSignalIndexes();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        RebuildSignalIndexes();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (generalGameStateService == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService.";
            return false;
        }

        if (!generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (gameClock == null)
        {
            error = "Falta GameClock.";
            return false;
        }

        if (offerService == null)
        {
            error = "Falta BistroBuilderMenuOfferService.";
            return false;
        }

        if (!offerService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!TryValidateSignalList(activeEventIds, "evento", out error) ||
            !TryValidateSignalList(
                activePromotionIds,
                "promoción",
                out error
            ))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetCurrentContext(
        out BistroBuilderMenuActivationContext context,
        out string error
    )
    {
        context = default(BistroBuilderMenuActivationContext);

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        DateTime date;
        try
        {
            date = new DateTime(
                generalGameStateService.CalendarYear,
                generalGameStateService.CalendarMonth,
                generalGameStateService.CalendarDay
            );
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "El calendario actual no representa una fecha válida.";
            return false;
        }

        int dateKey = date.Year * 10000 + date.Month * 100 + date.Day;
        int minuteOfDay = gameClock.Hour * 60 + gameClock.Minute;
        BistroBuilderMealServiceAvailability mealService =
            offerService.CurrentMealService;

        if (!BistroBuilderMenuOfferContext.IsConcreteMealService(mealService))
        {
            error = "El contexto de cartas necesita un servicio concreto.";
            return false;
        }

        context = new BistroBuilderMenuActivationContext(
            dateKey,
            date.DayOfWeek,
            minuteOfDay,
            mealService,
            eventIdSet,
            promotionIdSet
        );
        error = string.Empty;
        return true;
    }

    public bool IsEventActive(string eventId)
    {
        return eventIdSet.Contains(
            BistroBuilderMenuIdUtility.NormalizeStableId(eventId)
        );
    }

    public bool IsPromotionActive(string promotionId)
    {
        return promotionIdSet.Contains(
            BistroBuilderMenuIdUtility.NormalizeStableId(promotionId)
        );
    }

    public bool TrySetEventActive(
        string eventId,
        bool active,
        out string error
    )
    {
        return TrySetSignal(
            eventId,
            active,
            activeEventIds,
            eventIdSet,
            "EventId",
            out error
        );
    }

    public bool TrySetPromotionActive(
        string promotionId,
        bool active,
        out string error
    )
    {
        return TrySetSignal(
            promotionId,
            active,
            activePromotionIds,
            promotionIdSet,
            "PromotionId",
            out error
        );
    }

    /// <summary>
    /// Sustituye las señales activas de forma atómica. Se usa durante carga y
    /// en pruebas funcionales para no publicar estados intermedios.
    /// </summary>
    public bool TryReplaceSignals(
        IList<string> eventIds,
        IList<string> promotionIds,
        bool notify,
        out string error
    )
    {
        List<string> candidateEvents = CloneNormalized(eventIds);
        List<string> candidatePromotions = CloneNormalized(promotionIds);

        if (!TryValidateSignalList(candidateEvents, "evento", out error) ||
            !TryValidateSignalList(
                candidatePromotions,
                "promoción",
                out error
            ))
        {
            return false;
        }

        activeEventIds.Clear();
        activeEventIds.AddRange(candidateEvents);
        activePromotionIds.Clear();
        activePromotionIds.AddRange(candidatePromotions);
        RebuildSignalIndexes();

        if (notify)
        {
            ContextChanged?.Invoke();
        }

        error = string.Empty;
        return true;
    }

    public void CopySignalsTo(
        List<string> eventDestination,
        List<string> promotionDestination
    )
    {
        if (eventDestination != null)
        {
            eventDestination.Clear();
            eventDestination.AddRange(activeEventIds);
        }

        if (promotionDestination != null)
        {
            promotionDestination.Clear();
            promotionDestination.AddRange(activePromotionIds);
        }
    }

    private bool TrySetSignal(
        string value,
        bool active,
        List<string> serialized,
        HashSet<string> index,
        string label,
        out string error
    )
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
            value
        );

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized))
        {
            error = label + " inválido.";
            return false;
        }

        bool changed;
        if (active)
        {
            changed = index.Add(normalized);
            if (changed)
            {
                serialized.Add(normalized);
                serialized.Sort(StringComparer.Ordinal);
            }
        }
        else
        {
            changed = index.Remove(normalized);
            if (changed)
            {
                serialized.Remove(normalized);
            }
        }

        if (changed)
        {
            ContextChanged?.Invoke();
        }

        error = string.Empty;
        return true;
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged += HandleContextChanged;
        }

        if (gameClock != null)
        {
            gameClock.TimeChanged += HandleTimeChanged;
        }

        if (offerService != null)
        {
            offerService.OfferChanged += HandleOfferChanged;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleContextChanged;
        }

        if (gameClock != null)
        {
            gameClock.TimeChanged -= HandleTimeChanged;
        }

        if (offerService != null)
        {
            offerService.OfferChanged -= HandleOfferChanged;
        }

        subscribed = false;
    }

    private void HandleContextChanged()
    {
        ContextChanged?.Invoke();
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        ContextChanged?.Invoke();
    }

    private void HandleOfferChanged(BistroBuilderMenuOfferChangedEvent change)
    {
        if (change.ChangeType ==
            BistroBuilderMenuOfferChangeType.MealServiceChanged)
        {
            ContextChanged?.Invoke();
        }
    }

    private void RebuildSignalIndexes()
    {
        eventIdSet.Clear();
        promotionIdSet.Clear();
        NormalizeInPlace(activeEventIds, eventIdSet);
        NormalizeInPlace(activePromotionIds, promotionIdSet);
    }

    private static void NormalizeInPlace(
        List<string> values,
        HashSet<string> index
    )
    {
        if (values == null)
        {
            return;
        }

        for (int position = values.Count - 1; position >= 0; position--)
        {
            string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(
                values[position]
            );
            if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized) ||
                !index.Add(normalized))
            {
                values.RemoveAt(position);
            }
            else
            {
                values[position] = normalized;
            }
        }

        values.Sort(StringComparer.Ordinal);
    }

    private static bool TryValidateSignalList(
        IList<string> values,
        string label,
        out string error
    )
    {
        if (values == null)
        {
            error = "La lista de " + label + " es nula.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (!BistroBuilderMenuIdUtility.IsValidStableId(value) ||
                !ids.Add(value))
            {
                error = "La lista de " + label + " contiene identidades inválidas o duplicadas.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static List<string> CloneNormalized(IList<string> source)
    {
        List<string> result = new List<string>();
        if (source == null)
        {
            return result;
        }

        for (int index = 0; index < source.Count; index++)
        {
            result.Add(
                BistroBuilderMenuIdUtility.NormalizeStableId(source[index])
            );
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }

        if (gameClock == null)
        {
            TryGetComponent(out gameClock);
        }

        if (offerService == null)
        {
            TryGetComponent(out offerService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
