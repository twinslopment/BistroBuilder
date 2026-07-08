using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class PauseButtonController : MonoBehaviour
{
    [SerializeField]
    private GameClock gameClock;

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
        button.onClick.AddListener(TogglePause);
        UpdateButtonText();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(TogglePause);
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
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (buttonText == null || gameClock == null)
            return;

        buttonText.text = gameClock.IsPaused
            ? "Reanudar"
            : "Pausar";
    }
}