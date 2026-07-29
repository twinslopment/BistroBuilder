using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control de velocidad con selección visual sincronizada con GameClock.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SpeedButtonController : MonoBehaviour
{
    [SerializeField]
    private GameClock gameClock;

    [SerializeField, Min(0.1f)]
    private float speedMultiplier = 1f;

    [SerializeField]
    private TMP_Text buttonText;

    [Header("Apariencia 368B")]

    [SerializeField]
    private Color normalBackground = new Color32(48, 53, 59, 255);

    [SerializeField]
    private Color activeBackground = new Color32(72, 113, 94, 255);

    [SerializeField]
    private Color normalText = new Color32(190, 195, 199, 255);

    [SerializeField]
    private Color activeText = new Color32(247, 248, 246, 255);

    private Button button;

    public GameClock GameClock => gameClock;

    public float SpeedMultiplier => speedMultiplier;

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

        button.onClick.RemoveListener(ApplySpeed);
        button.onClick.AddListener(ApplySpeed);

        if (gameClock != null)
        {
            gameClock.SpeedChanged -= HandleSpeedChanged;
            gameClock.SpeedChanged += HandleSpeedChanged;
        }

        RefreshVisualState();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ApplySpeed);
        }

        if (gameClock != null)
        {
            gameClock.SpeedChanged -= HandleSpeedChanged;
        }
    }

    private void ApplySpeed()
    {
        if (gameClock == null)
        {
            Debug.LogError(
                "SpeedButtonController necesita una referencia a GameClock.",
                this
            );

            return;
        }

        gameClock.SetSpeedMultiplier(speedMultiplier);
        RefreshVisualState();
    }

    private void HandleSpeedChanged(float currentSpeed)
    {
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (buttonText != null)
        {
            buttonText.text = $"{speedMultiplier:0.#}×";
        }

        if (button == null || gameClock == null)
        {
            return;
        }

        bool isActive = Mathf.Approximately(
            gameClock.SpeedMultiplier,
            speedMultiplier
        );
        Color baseColor = isActive ? activeBackground : normalBackground;

        if (buttonText != null)
        {
            buttonText.color = isActive ? activeText : normalText;
        }

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
