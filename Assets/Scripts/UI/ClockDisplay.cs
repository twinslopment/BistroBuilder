using TMPro;
using UnityEngine;

public sealed class ClockDisplay : MonoBehaviour
{
    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private TMP_Text clockText;

    private void Awake()
    {
        if (clockText == null)
            clockText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (gameClock == null)
        {
            Debug.LogError(
                "ClockDisplay necesita una referencia a GameClock.",
                this
            );

            enabled = false;
            return;
        }

        gameClock.TimeChanged += HandleTimeChanged;
        UpdateDisplay(gameClock.Hour, gameClock.Minute);
    }

    private void OnDisable()
    {
        if (gameClock != null)
            gameClock.TimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        UpdateDisplay(hour, minute);
    }

    private void UpdateDisplay(int hour, int minute)
    {
        if (clockText != null)
            clockText.text = $"{hour:00}:{minute:00}";
    }
}