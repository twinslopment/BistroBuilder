using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reloj principal de la simulación.
///
/// Distingue entre:
/// - Pausa solicitada por el jugador.
/// - Bloqueos temporales de sistemas, por ejemplo durante guardado/carga.
///
/// Los bloqueos temporales permiten restaurar hora, velocidad y pausa sin
/// reactivar la simulación a mitad de una carga compleja.
/// </summary>
public sealed class GameClock : MonoBehaviour
{
    [Header("Hora inicial")]
    [SerializeField, Range(0, 23)]
    private int startHour = 8;

    [SerializeField, Range(0, 59)]
    private int startMinute = 0;

    [Header("Velocidad base")]
    [Tooltip("Minutos de juego que avanzan por segundo real a velocidad x1.")]
    [SerializeField, Min(0.01f)]
    private float gameMinutesPerRealSecond = 1f;

    public event Action<int, int> TimeChanged;
    public event Action<float> SpeedChanged;
    public event Action<bool> PauseChanged;
    public event Action<bool> RuntimeSuspensionChanged;
    public event Action DayElapsed;

    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public bool IsPaused { get; private set; }
    public float SpeedMultiplier { get; private set; } = 1f;

    /// <summary>
    /// Fracción pendiente de minuto. Se persiste para que una carga no
    /// adelante ni retrase de forma visible el siguiente cambio de minuto.
    /// </summary>
    public float AccumulatedMinutes => accumulatedMinutes;

    /// <summary>
    /// Indica si uno o más sistemas mantienen detenido temporalmente el
    /// reloj, aunque el jugador no lo haya pausado.
    /// </summary>
    public bool IsRuntimeSuspended => runtimePauseLocks.Count > 0;

    public bool IsEffectivelyPaused => IsPaused || IsRuntimeSuspended;

    private readonly HashSet<int> runtimePauseLocks =
        new HashSet<int>();

    private float accumulatedMinutes;
    private int nextRuntimePauseLockId = 1;

    private void Awake()
    {
        Hour = startHour;
        Minute = startMinute;
        accumulatedMinutes = 0f;

        ApplySimulationSpeed();
        NotifyTimeChanged();
    }

    private void Update()
    {
        if (IsEffectivelyPaused)
        {
            return;
        }

        accumulatedMinutes +=
            Time.deltaTime *
            gameMinutesPerRealSecond;

        while (accumulatedMinutes >= 1f)
        {
            accumulatedMinutes -= 1f;
            AdvanceOneMinute();
        }
    }

    private void OnDestroy()
    {
        runtimePauseLocks.Clear();

        // Evita que Unity permanezca acelerado o pausado
        // después de salir del modo Play.
        Time.timeScale = 1f;
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused)
        {
            return;
        }

        IsPaused = paused;

        ApplySimulationSpeed();
        PauseChanged?.Invoke(IsPaused);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (!IsValidSpeed(multiplier))
        {
            Debug.LogWarning(
                "La velocidad de simulación debe ser finita y superior " +
                "a cero.",
                this
            );

            return;
        }

        if (Mathf.Approximately(SpeedMultiplier, multiplier))
        {
            return;
        }

        SpeedMultiplier = multiplier;

        ApplySimulationSpeed();
        SpeedChanged?.Invoke(SpeedMultiplier);
    }

    /// <summary>
    /// Adquiere un bloqueo temporal del reloj.
    ///
    /// El llamador debe liberar el IDisposable devuelto. Varios sistemas
    /// pueden bloquear simultáneamente el reloj sin pisarse entre sí.
    /// </summary>
    public IDisposable AcquireSimulationLock(string ownerName)
    {
        int lockId = nextRuntimePauseLockId++;

        if (nextRuntimePauseLockId <= 0)
        {
            nextRuntimePauseLockId = 1;
        }

        bool wasSuspended = IsRuntimeSuspended;
        runtimePauseLocks.Add(lockId);

        ApplySimulationSpeed();

        if (!wasSuspended)
        {
            RuntimeSuspensionChanged?.Invoke(true);
        }

        return new SimulationLockToken(this, lockId, ownerName);
    }

    /// <summary>
    /// Restaura de forma atómica el estado persistente del reloj.
    ///
    /// Si existe un bloqueo temporal activo, los valores quedan restaurados
    /// pero el tiempo no vuelve a avanzar hasta liberar dicho bloqueo.
    /// </summary>
    public bool TryRestoreState(
        int hour,
        int minute,
        float speedMultiplier,
        bool isPaused,
        float pendingAccumulatedMinutes,
        bool publishEvents = true
    )
    {
        if (hour < 0 || hour > 23 ||
            minute < 0 || minute > 59 ||
            !IsValidSpeed(speedMultiplier) ||
            float.IsNaN(pendingAccumulatedMinutes) ||
            float.IsInfinity(pendingAccumulatedMinutes))
        {
            return false;
        }

        bool timeChanged = Hour != hour || Minute != minute;
        bool speedChanged =
            !Mathf.Approximately(SpeedMultiplier, speedMultiplier);
        bool pauseChanged = IsPaused != isPaused;

        Hour = hour;
        Minute = minute;
        SpeedMultiplier = speedMultiplier;
        IsPaused = isPaused;
        accumulatedMinutes = Mathf.Clamp(
            pendingAccumulatedMinutes,
            0f,
            0.999999f
        );

        ApplySimulationSpeed();

        if (!publishEvents)
        {
            return true;
        }

        if (timeChanged)
        {
            NotifyTimeChanged();
        }

        if (speedChanged)
        {
            SpeedChanged?.Invoke(SpeedMultiplier);
        }

        if (pauseChanged)
        {
            PauseChanged?.Invoke(IsPaused);
        }

        return true;
    }

    private void ReleaseSimulationLock(int lockId)
    {
        bool wasSuspended = IsRuntimeSuspended;

        if (!runtimePauseLocks.Remove(lockId))
        {
            return;
        }

        ApplySimulationSpeed();

        if (wasSuspended && !IsRuntimeSuspended)
        {
            RuntimeSuspensionChanged?.Invoke(false);
        }
    }

    private void ApplySimulationSpeed()
    {
        Time.timeScale = IsEffectivelyPaused
            ? 0f
            : SpeedMultiplier;
    }

    private void AdvanceOneMinute()
    {
        Minute++;

        if (Minute >= 60)
        {
            Minute = 0;
            Hour++;

            if (Hour >= 24)
            {
                Hour = 0;
                DayElapsed?.Invoke();
            }
        }

        NotifyTimeChanged();
    }

    private void NotifyTimeChanged()
    {
        Debug.Log($"Hora del juego: {Hour:00}:{Minute:00}");
        TimeChanged?.Invoke(Hour, Minute);
    }

    private static bool IsValidSpeed(float value)
    {
        return value > 0f &&
               !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private sealed class SimulationLockToken : IDisposable
    {
        private GameClock owner;
        private readonly int lockId;
        private readonly string ownerName;

        public SimulationLockToken(
            GameClock owner,
            int lockId,
            string ownerName
        )
        {
            this.owner = owner;
            this.lockId = lockId;
            this.ownerName = ownerName ?? string.Empty;
        }

        public void Dispose()
        {
            GameClock currentOwner = owner;
            owner = null;

            if (currentOwner != null)
            {
                currentOwner.ReleaseSimulationLock(lockId);
            }
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(ownerName)
                ? "GameClock simulation lock"
                : ownerName;
        }
    }
}
