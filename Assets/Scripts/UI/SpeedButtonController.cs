using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class SpeedButtonController : MonoBehaviour
{
    [SerializeField]
    private GameClock gameClock;

    [SerializeField, Min(0.1f)]
    private float speedMultiplier = 1f;

    [SerializeField]
    private TMP_Text buttonText;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (buttonText == null)
            buttonText = GetComponentInChildren<TMP_Text>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(ApplySpeed);
        UpdateButtonText();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(ApplySpeed);
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
    }

    private void UpdateButtonText()
    {
        if (buttonText != null)
            buttonText.text = $"x{speedMultiplier:0.#}";
    }
}