using System;
using UnityEngine;

/// <summary>
/// Propietario único de la identidad y calendario globales de la partida.
///
/// No almacena dinero ni datos de sistemas especializados para evitar
/// duplicar futuras autoridades de finanzas, inventario o progresión.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/General Game State Service"
)]
public sealed class BistroBuilderGeneralGameStateService :
    MonoBehaviour
{
    [Header("Identidad inicial")]

    [SerializeField]
    private string initialRestaurantName = "Mi restaurante";

    [Header("Calendario inicial")]

    [SerializeField]
    [Min(1)]
    private int initialDayIndex = 1;

    [SerializeField]
    [Range(1, 9999)]
    private int initialCalendarYear = 1;

    [SerializeField]
    [Range(1, 12)]
    private int initialCalendarMonth = 1;

    [SerializeField]
    [Range(1, 31)]
    private int initialCalendarDay = 1;

    [Header("Progresión inicial")]

    [SerializeField]
    private string initialProgressionStageId = "new_restaurant";

    [SerializeField]
    [Min(1)]
    private int initialProgressionLevel = 1;

    [Header("Dependencias")]

    [SerializeField]
    private GameClock gameClock;

    public event Action IdentityChanged;
    public event Action CalendarChanged;
    public event Action ProgressionChanged;

    public string GameId { get; private set; } = string.Empty;
    public string RestaurantName { get; private set; } = string.Empty;
    public string CreatedUtc { get; private set; } = string.Empty;

    public int DayIndex { get; private set; } = 1;
    public int CalendarYear { get; private set; } = 1;
    public int CalendarMonth { get; private set; } = 1;
    public int CalendarDay { get; private set; } = 1;

    public string ProgressionStageId { get; private set; } =
        "new_restaurant";

    public int ProgressionLevel { get; private set; } = 1;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        InitializeNewRuntimeGameIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();

        if (gameClock != null)
        {
            gameClock.DayElapsed -= HandleDayElapsed;
            gameClock.DayElapsed += HandleDayElapsed;
        }
    }

    private void OnDisable()
    {
        if (gameClock != null)
        {
            gameClock.DayElapsed -= HandleDayElapsed;
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (gameClock == null)
        {
            error = "Falta GameClock.";
            return false;
        }

        if (!IsValidDate(
                initialCalendarYear,
                initialCalendarMonth,
                initialCalendarDay
            ))
        {
            error = "La fecha inicial no es válida.";
            return false;
        }

        if (initialDayIndex < 1 ||
            initialProgressionLevel < 1 ||
            string.IsNullOrWhiteSpace(initialProgressionStageId))
        {
            error = "El día o la progresión inicial no son válidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TrySetRestaurantName(string restaurantName)
    {
        string normalized = NormalizeDisplayName(restaurantName);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (string.Equals(
                RestaurantName,
                normalized,
                StringComparison.Ordinal
            ))
        {
            return true;
        }

        RestaurantName = normalized;
        IdentityChanged?.Invoke();
        return true;
    }

    public bool TrySetCalendar(
        int dayIndex,
        int year,
        int month,
        int day
    )
    {
        if (dayIndex < 1 || !IsValidDate(year, month, day))
        {
            return false;
        }

        DayIndex = dayIndex;
        CalendarYear = year;
        CalendarMonth = month;
        CalendarDay = day;
        CalendarChanged?.Invoke();
        return true;
    }

    public bool TrySetProgression(
        string stageId,
        int level
    )
    {
        string normalizedStageId = NormalizeStableId(stageId);

        if (!IsSafeStableId(normalizedStageId) || level < 1)
        {
            return false;
        }

        ProgressionStageId = normalizedStageId;
        ProgressionLevel = level;
        ProgressionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Restaura identidad, calendario y progresión en una sola operación.
    /// </summary>
    public bool TryRestoreState(
        string gameId,
        string restaurantName,
        string createdUtc,
        int dayIndex,
        int calendarYear,
        int calendarMonth,
        int calendarDay,
        string progressionStageId,
        int progressionLevel,
        bool publishEvents = true
    )
    {
        string normalizedGameId = NormalizeStableId(gameId);
        string normalizedName = NormalizeDisplayName(restaurantName);
        string normalizedStage = NormalizeStableId(progressionStageId);

        if (!IsSafeStableId(normalizedGameId) ||
            string.IsNullOrWhiteSpace(normalizedName) ||
            !DateTime.TryParse(
                createdUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _
            ) ||
            dayIndex < 1 ||
            !IsValidDate(
                calendarYear,
                calendarMonth,
                calendarDay
            ) ||
            !IsSafeStableId(normalizedStage) ||
            progressionLevel < 1)
        {
            return false;
        }

        GameId = normalizedGameId;
        RestaurantName = normalizedName;
        CreatedUtc = createdUtc;
        DayIndex = dayIndex;
        CalendarYear = calendarYear;
        CalendarMonth = calendarMonth;
        CalendarDay = calendarDay;
        ProgressionStageId = normalizedStage;
        ProgressionLevel = progressionLevel;

        if (publishEvents)
        {
            IdentityChanged?.Invoke();
            CalendarChanged?.Invoke();
            ProgressionChanged?.Invoke();
        }

        return true;
    }

    private void HandleDayElapsed()
    {
        DateTime date = new DateTime(
            CalendarYear,
            CalendarMonth,
            CalendarDay
        ).AddDays(1d);

        DayIndex++;
        CalendarYear = date.Year;
        CalendarMonth = date.Month;
        CalendarDay = date.Day;
        CalendarChanged?.Invoke();
    }

    private void InitializeNewRuntimeGameIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(GameId))
        {
            GameId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(CreatedUtc))
        {
            CreatedUtc = DateTime.UtcNow.ToString("O");
        }

        if (string.IsNullOrWhiteSpace(RestaurantName))
        {
            RestaurantName = NormalizeDisplayName(initialRestaurantName);

            if (string.IsNullOrWhiteSpace(RestaurantName))
            {
                RestaurantName = "Mi restaurante";
            }
        }

        DayIndex = Mathf.Max(1, initialDayIndex);

        if (IsValidDate(
                initialCalendarYear,
                initialCalendarMonth,
                initialCalendarDay
            ))
        {
            CalendarYear = initialCalendarYear;
            CalendarMonth = initialCalendarMonth;
            CalendarDay = initialCalendarDay;
        }

        string normalizedStage =
            NormalizeStableId(initialProgressionStageId);
        ProgressionStageId = string.IsNullOrWhiteSpace(normalizedStage)
            ? "new_restaurant"
            : normalizedStage;
        ProgressionLevel = Mathf.Max(1, initialProgressionLevel);
    }

    private void CacheDependenciesIfNeeded()
    {
        if (gameClock == null)
        {
            TryGetComponent(out gameClock);
        }
    }

    private static bool IsValidDate(
        int year,
        int month,
        int day
    )
    {
        if (year < 1 || year > 9999 ||
            month < 1 || month > 12)
        {
            return false;
        }

        return day >= 1 &&
               day <= DateTime.DaysInMonth(year, month);
    }

    private static bool IsSafeStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= '0' && character <= '9' ||
                character == '_' ||
                character == '-' ||
                character == '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeStableId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
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

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        initialDayIndex = Mathf.Max(1, initialDayIndex);
        initialCalendarYear = Mathf.Clamp(
            initialCalendarYear,
            1,
            9999
        );
        initialCalendarMonth = Mathf.Clamp(
            initialCalendarMonth,
            1,
            12
        );
        initialCalendarDay = Mathf.Clamp(
            initialCalendarDay,
            1,
            DateTime.DaysInMonth(
                initialCalendarYear,
                initialCalendarMonth
            )
        );
        initialProgressionLevel = Mathf.Max(
            1,
            initialProgressionLevel
        );
        CacheDependenciesIfNeeded();
    }
#endif
}
