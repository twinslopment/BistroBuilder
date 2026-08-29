using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador de aplicación de la demanda base dinámica. Lee únicamente
/// autoridades canónicas y entrega una proyección consultiva al compositor
/// de demanda; CustomerGroupSpawner conserva la materialización de clientes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Dynamic Base Demand Service")]
public sealed class BistroBuilderDynamicDemandService : MonoBehaviour
{
    [SerializeField] private BistroBuilderDynamicDemandSettings settings =
        new BistroBuilderDynamicDemandSettings();
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private BistroBuilderBarServiceRegistry barRegistry;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderRestaurantMenuService menuService;
    [SerializeField] private BistroBuilderDishAvailabilityService dishAvailabilityService;

    private readonly List<BistroBuilderCustomerExperienceRecord> experiences =
        new List<BistroBuilderCustomerExperienceRecord>(16);
    private readonly List<BistroBuilderReservationRecord> reservations =
        new List<BistroBuilderReservationRecord>(32);
    private readonly List<BistroBuilderMenuItemRuntimeState> menuItems =
        new List<BistroBuilderMenuItemRuntimeState>(32);
    private BistroBuilderDynamicDemandProjection lastProjection;

    public BistroBuilderDynamicDemandProjection LastProjection =>
        lastProjection != null ? lastProjection.DeepClone() : null;
    public BistroBuilderDynamicDemandSettings Settings =>
        settings != null ? settings.DeepClone() : null;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (settings == null || !settings.TryValidate(out error)) return false;
        if (generalGameStateService == null || gameClock == null ||
            tableRegistry == null || barRegistry == null || reputationService == null ||
            reservationService == null || menuService == null ||
            dishAvailabilityService == null)
        {
            error = "Demanda dinámica necesita calendario, capacidad, reputación, reservas y carta.";
            return false;
        }
        if (!generalGameStateService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !reservationService.ValidateConfiguration(out error) ||
            !menuService.ValidateConfiguration(out error)) return false;
        if (Application.isPlaying && tableRegistry.RegisteredTableCount < 1)
        {
            error = "La demanda dinámica necesita al menos una mesa operativa.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Construye la demanda orgánica del próximo servicio con datos actuales.
    /// No incluye modificadores de Marketing ni crea clientes.
    /// </summary>
    public bool TryBuildProjection(
        out BistroBuilderDynamicDemandProjection projection,
        out string error)
    {
        projection = null;
        if (!ValidateConfiguration(out error) ||
            !TryBuildContext(out BistroBuilderDynamicDemandContext context, out error))
            return false;
        if (!BistroBuilderDynamicDemandEngine.TryEvaluate(
                settings, context, out projection, out error)) return false;
        lastProjection = projection.DeepClone();
        return true;
    }

    public bool TryBuildArrivalDelays(
        int groupCount,
        List<float> destination,
        out string error)
    {
        if (destination == null || groupCount < 1 || groupCount > 100)
        {
            error = "La curva de llegadas recibió una cardinalidad inválida.";
            return false;
        }
        if (!ValidateConfiguration(out error)) return false;
        BistroBuilderDynamicDemandEngine.BuildArrivalDelays(
            settings, gameClock.Hour * 60 + gameClock.Minute,
            groupCount, destination);
        error = destination.Count == groupCount
            ? string.Empty
            : "La curva de llegadas no conserva la cardinalidad.";
        return error.Length == 0;
    }

    private bool TryBuildContext(
        out BistroBuilderDynamicDemandContext context,
        out string error)
    {
        context = null;
        int tableSeats = 0;
        int tableCount = 0;
        if (Application.isPlaying && tableRegistry.RegisteredTableCount > 0)
        {
            foreach (RestaurantTable table in tableRegistry.RegisteredTables)
            {
                if (table == null) continue;
                tableSeats += Math.Max(0, table.Capacity);
                tableCount++;
            }
        }
        else
        {
            RestaurantTable[] sceneTables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < sceneTables.Length; index++)
            {
                RestaurantTable table = sceneTables[index];
                if (table == null || table.gameObject.scene != gameObject.scene) continue;
                tableSeats += Math.Max(0, table.Capacity);
                tableCount++;
            }
        }

        int barSeats = 0;
        if (Application.isPlaying && barRegistry.RegisteredSpotCount > 0)
        {
            foreach (BistroBuilderBarServiceSpot spot in barRegistry.RegisteredSpots)
                if (spot != null) barSeats += Math.Max(0, spot.Capacity);
        }
        else
        {
            BistroBuilderBarServiceSpot[] sceneSpots =
                UnityEngine.Object.FindObjectsByType<BistroBuilderBarServiceSpot>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < sceneSpots.Length; index++)
            {
                BistroBuilderBarServiceSpot spot = sceneSpots[index];
                if (spot != null && spot.gameObject.scene == gameObject.scene)
                    barSeats += Math.Max(0, spot.Capacity);
            }
        }

        if (tableSeats + barSeats < 1)
        {
            error = "El restaurante no expone capacidad operativa.";
            return false;
        }

        int satisfaction = ResolveRecentSatisfaction();
        if (!TryCountAvailableDishes(out int availableDishes, out error)) return false;
        ResolveReservationPressure(
            generalGameStateService.DayIndex,
            gameClock.Hour * 60 + gameClock.Minute,
            out int reservationGroups,
            out int reservedPartySize);

        DateTime date;
        try
        {
            date = new DateTime(
                generalGameStateService.CalendarYear,
                generalGameStateService.CalendarMonth,
                generalGameStateService.CalendarDay);
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "El calendario global no puede convertirse en una fecha válida.";
            return false;
        }

        context = new BistroBuilderDynamicDemandContext
        {
            progressionLevel = generalGameStateService.ProgressionLevel,
            tableCount = tableCount,
            tableSeatCapacity = tableSeats,
            barSeatCapacity = barSeats,
            globalReputationBasisPoints = reputationService.GlobalScoreBasisPoints,
            recentSatisfactionBasisPoints = satisfaction,
            dayOfWeek = date.DayOfWeek,
            minuteOfDay = gameClock.Hour * 60 + gameClock.Minute,
            availableDishCount = availableDishes,
            reservationGroupCount = reservationGroups,
            reservedPartySize = reservedPartySize
        };
        error = string.Empty;
        return true;
    }

    private int ResolveRecentSatisfaction()
    {
        experiences.Clear();
        reputationService.CopyRecentExperiences(experiences);
        if (experiences.Count == 0) return 5000;
        long total = 0L;
        int count = 0;
        int first = Math.Max(0, experiences.Count - 12);
        for (int index = first; index < experiences.Count; index++)
        {
            BistroBuilderCustomerExperienceRecord record = experiences[index];
            if (record == null) continue;
            total += Math.Max(0, Math.Min(10000, record.overallSatisfactionBasisPoints));
            count++;
        }
        return count == 0 ? 5000 : (int)Math.Round(total / (double)count);
    }

    private bool TryCountAvailableDishes(out int count, out string error)
    {
        count = 0;
        menuItems.Clear();
        if (!menuService.TryGetSnapshot(menuItems, out error)) return false;
        for (int index = 0; index < menuItems.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuItems[index];
            if (item == null || !item.Unlocked || !item.Enabled || item.ManuallySoldOut)
                continue;
            if (!Application.isPlaying)
            {
                count++;
                continue;
            }
            if (dishAvailabilityService.TryGetSnapshot(
                    item.DishId, out BistroBuilderDishAvailabilitySnapshot availability) &&
                availability.IsOrderable)
                count++;
        }
        error = string.Empty;
        return true;
    }

    private void ResolveReservationPressure(
        int dayIndex,
        int minuteOfDay,
        out int groupCount,
        out int partySize)
    {
        groupCount = 0;
        partySize = 0;
        reservations.Clear();
        reservationService.CopyReservationsForDay(dayIndex, reservations);
        int windowStart = Math.Max(0, minuteOfDay - 30);
        int windowEnd = Math.Min(1439, minuteOfDay + 240);
        for (int index = 0; index < reservations.Count; index++)
        {
            BistroBuilderReservationRecord record = reservations[index];
            if (record == null || record.IsTerminal ||
                record.EndMinute < windowStart || record.arrivalMinute > windowEnd)
                continue;
            groupCount++;
            partySize += Math.Max(1, record.partySize);
        }
    }

    private void CacheDependencies()
    {
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (gameClock == null) gameClock = FindFirstObjectByType<GameClock>();
        if (tableRegistry == null) TryGetComponent(out tableRegistry);
        if (barRegistry == null) TryGetComponent(out barRegistry);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (reservationService == null) TryGetComponent(out reservationService);
        if (menuService == null) TryGetComponent(out menuService);
        if (dishAvailabilityService == null) TryGetComponent(out dishAvailabilityService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
