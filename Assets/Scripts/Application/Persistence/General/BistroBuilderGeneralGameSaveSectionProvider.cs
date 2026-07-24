using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persiste identidad, calendario, reloj y estado operativo general.
///
/// La fase de servicio se aplica en la finalización tardía. De este modo,
/// una futura carga con el restaurante abierto no reactivará llegadas,
/// IA ni flujos hasta haber restaurado clientes, comandas y cocina.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/General Game Save Provider"
)]
public sealed class BistroBuilderGeneralGameSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "game.general";
    public const int StableSectionVersion = 1;
    public const string FutureActiveServiceSectionId = "service.runtime";

    public const string SharedStateKey = "game.general.loaded_state";
    public const string SharedCheckpointKey =
        "game.general.active_service_checkpoint";
    public const string SharedCapturedUtcKey =
        "game.general.captured_utc";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameState;

    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private RestaurantServiceStateService serviceStateService;

    [Header("Depuración")]

    [SerializeField]
    private bool logLoadSummary = true;

    private RestaurantServiceState pendingServiceState =
        RestaurantServiceState.Closed;

    private BistroBuilderSaveSnapshotMode pendingSnapshotMode =
        BistroBuilderSaveSnapshotMode.ClosedRestaurant;

    private bool hasPendingServiceState;

    public string SectionId => StableSectionId;

    public int SectionVersion => StableSectionVersion;

    public int LoadOrder => 10;

    // Opcional para poder cargar generaciones 366 creadas antes de
    // existir game.general. Las nuevas generaciones siempre la incluyen.
    public bool IsRequired => false;

    public Type StateType => typeof(BistroBuilderGeneralGameSaveData);

    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    /// <summary>
    /// Cierra el servicio antes de que otros proveedores retiren o
    /// reconstruyan entidades.
    /// </summary>
    public int PrepareOrder => 10000;

    /// <summary>
    /// Identidad, calendario y reloj deben existir pronto durante la carga.
    /// </summary>
    public int ApplyOrder => 10;

    /// <summary>
    /// El estado Open/Preparing/Closing se restaura al final absoluto.
    /// </summary>
    public int FinalizeOrder => 10000;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (generalGameState == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService.";
            return false;
        }

        if (!generalGameState.ValidateConfiguration(out error))
        {
            return false;
        }

        if (gameClock == null)
        {
            error = "Falta GameClock.";
            return false;
        }

        if (serviceStateService == null)
        {
            error = "Falta RestaurantServiceStateService.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(
        BistroBuilderSaveCaptureContext context
    )
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        RestaurantServiceState serviceState =
            serviceStateService.CurrentState;

        BistroBuilderSaveSnapshotMode snapshotMode =
            serviceState == RestaurantServiceState.Closed
                ? BistroBuilderSaveSnapshotMode.ClosedRestaurant
                : BistroBuilderSaveSnapshotMode.ActiveService;

        string capturedUtc = DateTime.UtcNow.ToString("O");

        BistroBuilderGeneralGameSaveData data =
            new BistroBuilderGeneralGameSaveData
            {
                gameId = generalGameState.GameId,
                restaurantName = generalGameState.RestaurantName,
                createdUtc = generalGameState.CreatedUtc,
                capturedUtc = capturedUtc,
                dayIndex = generalGameState.DayIndex,
                calendarYear = generalGameState.CalendarYear,
                calendarMonth = generalGameState.CalendarMonth,
                calendarDay = generalGameState.CalendarDay,
                progressionStageId =
                    generalGameState.ProgressionStageId,
                progressionLevel = generalGameState.ProgressionLevel,
                clockHour = gameClock.Hour,
                clockMinute = gameClock.Minute,
                clockAccumulatedMinutes =
                    gameClock.AccumulatedMinutes,
                clockSpeedMultiplier = gameClock.SpeedMultiplier,
                clockIsPaused = gameClock.IsPaused,
                serviceState = (int)serviceState,
                snapshotMode = (int)snapshotMode
            };

        if (snapshotMode ==
            BistroBuilderSaveSnapshotMode.ActiveService)
        {
            data.activeServiceCheckpointId =
                Guid.NewGuid().ToString("N");
            data.requiredRuntimeSectionIds.Add(
                FutureActiveServiceSectionId
            );
            context.SharedData.Set(
                SharedCheckpointKey,
                data.activeServiceCheckpointId
            );
        }

        context.SharedData.Set(SharedCapturedUtcKey, capturedUtc);
        context.Complete(data);
        yield break;
    }

    public bool ValidateState(
        object state,
        out string error
    )
    {
        if (!(state is BistroBuilderGeneralGameSaveData data))
        {
            error = "El estado general no tiene el tipo esperado.";
            return false;
        }

        if (data.schemaVersion != StableSectionVersion)
        {
            error = "La versión interna de game.general no coincide.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(data.gameId) ||
            string.IsNullOrWhiteSpace(data.restaurantName) ||
            !DateTime.TryParse(
                data.createdUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _
            ) ||
            data.dayIndex < 1 ||
            !IsValidDate(
                data.calendarYear,
                data.calendarMonth,
                data.calendarDay
            ) ||
            string.IsNullOrWhiteSpace(data.progressionStageId) ||
            data.progressionLevel < 1)
        {
            error = "La identidad, calendario o progresión son inválidos.";
            return false;
        }

        if (data.clockHour < 0 || data.clockHour > 23 ||
            data.clockMinute < 0 || data.clockMinute > 59 ||
            data.clockSpeedMultiplier <= 0f ||
            float.IsNaN(data.clockSpeedMultiplier) ||
            float.IsInfinity(data.clockSpeedMultiplier) ||
            data.clockAccumulatedMinutes < 0f ||
            data.clockAccumulatedMinutes >= 1f ||
            float.IsNaN(data.clockAccumulatedMinutes) ||
            float.IsInfinity(data.clockAccumulatedMinutes))
        {
            error = "El estado persistente del reloj es inválido.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(RestaurantServiceState),
                data.serviceState
            ) ||
            !Enum.IsDefined(
                typeof(BistroBuilderSaveSnapshotMode),
                data.snapshotMode
            ))
        {
            error = "El estado de servicio o modo de snapshot es inválido.";
            return false;
        }

        RestaurantServiceState serviceState =
            (RestaurantServiceState)data.serviceState;
        BistroBuilderSaveSnapshotMode snapshotMode =
            (BistroBuilderSaveSnapshotMode)data.snapshotMode;

        if (snapshotMode ==
                BistroBuilderSaveSnapshotMode.ClosedRestaurant &&
            serviceState != RestaurantServiceState.Closed)
        {
            error = "Un snapshot cerrado no puede declarar servicio activo.";
            return false;
        }

        if (snapshotMode ==
            BistroBuilderSaveSnapshotMode.ActiveService)
        {
            if (serviceState == RestaurantServiceState.Closed)
            {
                error = "Un snapshot activo no puede declarar Closed.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    data.activeServiceCheckpointId
                ) ||
                data.requiredRuntimeSectionIds == null ||
                data.requiredRuntimeSectionIds.Count == 0)
            {
                error = "El snapshot activo no declara su checkpoint " +
                        "ni secciones de runtime.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator PrepareForLoad(
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        hasPendingServiceState = false;
        pendingServiceState = RestaurantServiceState.Closed;
        pendingSnapshotMode =
            BistroBuilderSaveSnapshotMode.ClosedRestaurant;

        if (!serviceStateService.TryRestoreState(
                RestaurantServiceState.Closed,
                false
            ))
        {
            context.Fail(
                "No se pudo cerrar de forma segura el servicio antes " +
                "de reconstruir la partida."
            );
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!(state is BistroBuilderGeneralGameSaveData data))
        {
            context.Fail("No se puede aplicar game.general.");
            yield break;
        }

        if (!ValidateState(data, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        BistroBuilderSaveSnapshotMode snapshotMode =
            (BistroBuilderSaveSnapshotMode)data.snapshotMode;

        if (snapshotMode ==
            BistroBuilderSaveSnapshotMode.ActiveService)
        {
            for (int index = 0;
                 index < data.requiredRuntimeSectionIds.Count;
                 index++)
            {
                string requiredSectionId =
                    data.requiredRuntimeSectionIds[index];

                if (!saveGameService.HasProvider(requiredSectionId))
                {
                    context.Fail(
                        "La partida contiene un servicio activo y falta " +
                        "la sección obligatoria " + requiredSectionId +
                        "."
                    );
                    yield break;
                }
            }
        }

        if (!generalGameState.TryRestoreState(
                data.gameId,
                data.restaurantName,
                data.createdUtc,
                data.dayIndex,
                data.calendarYear,
                data.calendarMonth,
                data.calendarDay,
                data.progressionStageId,
                data.progressionLevel,
                true
            ))
        {
            context.Fail("No se pudo restaurar el estado general.");
            yield break;
        }

        if (!gameClock.TryRestoreState(
                data.clockHour,
                data.clockMinute,
                data.clockSpeedMultiplier,
                data.clockIsPaused,
                data.clockAccumulatedMinutes,
                true
            ))
        {
            context.Fail("No se pudo restaurar el reloj.");
            yield break;
        }

        pendingServiceState =
            (RestaurantServiceState)data.serviceState;
        pendingSnapshotMode = snapshotMode;
        hasPendingServiceState = true;

        context.SharedData.Set(SharedStateKey, data);
        context.References.TryRegister(
            BistroBuilderSaveReferenceDomains.GameState,
            "general",
            generalGameState,
            true
        );
        context.References.TryRegister(
            BistroBuilderSaveReferenceDomains.GameClock,
            "main",
            gameClock,
            true
        );

        yield break;
    }

    public void FinalizeLoad(
        BistroBuilderSaveLoadContext context
    )
    {
        if (context.HasFailed || !hasPendingServiceState)
        {
            return;
        }

        if (!serviceStateService.TryRestoreState(
                pendingServiceState,
                true
            ))
        {
            context.Fail(
                "No se pudo restaurar el estado operativo final."
            );
            return;
        }

        context.References.TryRegister(
            BistroBuilderSaveReferenceDomains.ServiceState,
            "main",
            serviceStateService,
            true
        );

        if (logLoadSummary)
        {
            Debug.Log(
                nameof(BistroBuilderGeneralGameSaveSectionProvider) +
                " restauró día " + generalGameState.DayIndex +
                ", " + gameClock.Hour.ToString("00") + ":" +
                gameClock.Minute.ToString("00") +
                ", servicio " + pendingServiceState +
                ", modo " + pendingSnapshotMode +
                ". Rollback: " + context.IsRollback + ".",
                this
            );
        }

        hasPendingServiceState = false;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (generalGameState == null)
        {
            TryGetComponent(out generalGameState);
        }

        if (gameClock == null)
        {
            TryGetComponent(out gameClock);
        }

        if (serviceStateService == null)
        {
            TryGetComponent(out serviceStateService);
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

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
