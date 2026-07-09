using System;
using UnityEngine;

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

    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public bool IsPaused { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1f;

    private float accumulatedMinutes;

    private void Awake()
    {
        Hour = startHour;
        Minute = startMinute;

        ApplySimulationSpeed();
        NotifyTimeChanged();
    }

    private void Update()
    {
        if (IsPaused)
            return;

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
        // Evita que Unity permanezca acelerado o pausado
        // después de salir del modo Play.
        Time.timeScale = 1f;
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused)
            return;

        IsPaused = paused;

        ApplySimulationSpeed();
        PauseChanged?.Invoke(IsPaused);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (multiplier <= 0f)
        {
            Debug.LogWarning(
                "La velocidad de simulación debe ser superior a cero.",
                this
            );

            return;
        }

        if (Mathf.Approximately(SpeedMultiplier, multiplier))
            return;

        SpeedMultiplier = multiplier;

        ApplySimulationSpeed();
        SpeedChanged?.Invoke(SpeedMultiplier);
    }

    private void ApplySimulationSpeed()
    {
        Time.timeScale = IsPaused
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
                Hour = 0;
        }

        NotifyTimeChanged();
    }

    private void NotifyTimeChanged()
    {
        Debug.Log($"Hora del juego: {Hour:00}:{Minute:00}");
        TimeChanged?.Invoke(Hour, Minute);
    }
}