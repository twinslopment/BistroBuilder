using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// V21/V30 quick press feedback. Presentation only: never invokes gameplay actions.
/// The complete animation runs when a button is pressed, independent of Time.timeScale.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderV30ButtonMotion : MonoBehaviour, IPointerDownHandler, ISubmitHandler
{
    public const float DurationSeconds = 0.28f;
    public const float PressScale = 0.94f;
    public const float ReboundScale = 1.03f;

    private Button button;
    private Vector3 restingScale = Vector3.one;
    private Coroutine running;
    private Shadow highlight;
    private bool highlightCreated;

    private void Awake()
    {
        button = GetComponent<Button>();
        restingScale = transform.localScale;
    }

    private void OnEnable()
    {
        restingScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data == null || data.button != PointerEventData.InputButton.Left) return;
        Play();
    }

    public void OnSubmit(BaseEventData data) => Play();

    public void Play()
    {
        if (!isActiveAndEnabled || button == null || !button.IsInteractable()) return;
        if (IsEditModeActive()) return; // Editor V2 is deliberately pending a separate decision.
        if (running != null) StopCoroutine(running);
        transform.localScale = restingScale;
        if (BistroBuilderOptionsScreen.ReducedMotion)
        {
            ResetVisuals();
            return;
        }
        EnsureHighlight();
        running = StartCoroutine(Animate());
    }

    private static bool IsEditModeActive()
    {
        var mode = Object.FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        return mode != null && mode.IsEditModeActive;
    }

    private IEnumerator Animate()
    {
        // Resolve once per click, not every rendered frame.
        var editMode = Object.FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        float elapsed = 0f;
        while (elapsed < DurationSeconds)
        {
            if (BistroBuilderOptionsScreen.ReducedMotion || (editMode != null && editMode.IsEditModeActive) || !button.IsInteractable()) break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / DurationSeconds);
            float scale;
            if (t < 0.25f)
                scale = Mathf.LerpUnclamped(1f, PressScale, Smooth(t / 0.25f));
            else if (t < 0.58f)
                scale = Mathf.LerpUnclamped(PressScale, ReboundScale, Smooth((t - 0.25f) / 0.33f));
            else
                scale = Mathf.LerpUnclamped(ReboundScale, 1f, Smooth((t - 0.58f) / 0.42f));

            transform.localScale = restingScale * scale;
            if (highlight != null)
            {
                // The flash is brief; selected/hover styles remain owned by their respective systems.
                float flash = Mathf.Sin(Mathf.PI * t);
                highlight.effectColor = new Color(1f, 1f, 1f, 0.22f * flash);
            }
            yield return null;
        }
        ResetVisuals();
        running = null;
    }

    private void EnsureHighlight()
    {
        if (highlightCreated) return;
        Image[] images = GetComponentsInChildren<Image>(true);
        Image target = null;
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].transform == transform || images[i].sprite == null) continue;
            if (images[i].name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = images[i];
                break;
            }
            if (target == null) target = images[i];
        }
        if (target == null) return; // Text-only buttons retain the tactile scale, without a halo.
        highlightCreated = true;
        highlight = target.gameObject.AddComponent<Shadow>();
        highlight.effectDistance = new Vector2(0f, -0.65f);
        highlight.useGraphicAlpha = true;
        highlight.effectColor = new Color(1f, 1f, 1f, 0f);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ResetVisuals()
    {
        transform.localScale = restingScale;
        if (highlight != null) highlight.effectColor = new Color(1f, 1f, 1f, 0f);
    }

    private void OnDisable()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        ResetVisuals();
    }

    private void OnDestroy()
    {
        // The optional highlight belongs to the icon and is destroyed with it.
        running = null;
    }
}
