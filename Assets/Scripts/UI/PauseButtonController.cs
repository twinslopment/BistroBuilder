using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control de pausa con estado visual sincronizado con GameClock.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class PauseButtonController : MonoBehaviour
{
    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private TMP_Text buttonText;

    [Header("Apariencia 368B")]

    [SerializeField]
    private Color normalBackground = new Color32(48, 53, 59, 255);

    [SerializeField]
    private Color pausedBackground = new Color32(188, 139, 62, 255);

    [SerializeField]
    private Color normalText = new Color32(236, 238, 240, 255);

    [SerializeField]
    private Color pausedText = new Color32(25, 27, 30, 255);

    private Button button;

    public GameClock GameClock => gameClock;

    public TMP_Text ButtonText => buttonText;

    public Button Button => button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.RemoveListener(TogglePause);
        button.onClick.AddListener(TogglePause);

        if (gameClock != null)
        {
            gameClock.PauseChanged -= HandlePauseChanged;
            gameClock.PauseChanged += HandlePauseChanged;
        }

        RefreshVisualState();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(TogglePause);
        }

        if (gameClock != null)
        {
            gameClock.PauseChanged -= HandlePauseChanged;
        }
    }

    private void TogglePause()
    {
        if (gameClock == null)
        {
            Debug.LogError(
                "PauseButtonController necesita una referencia a GameClock.",
                this
            );

            return;
        }

        gameClock.SetPaused(!gameClock.IsPaused);
        RefreshVisualState();
    }

    private void HandlePauseChanged(bool isPaused)
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (buttonText == null || gameClock == null)
        {
            return;
        }

        bool isPaused = gameClock.IsPaused;
        buttonText.text = isPaused ? "REANUDAR" : "PAUSA";
        buttonText.color = isPaused ? pausedText : normalText;

        if (button == null)
        {
            return;
        }

        Color baseColor = isPaused ? pausedBackground : normalBackground;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            0.45f
        );
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }
}
