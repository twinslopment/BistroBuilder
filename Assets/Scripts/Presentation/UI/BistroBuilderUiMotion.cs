using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Microinteracción de press sin desplazar elementos en hover.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderUiSelectableMotion : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler
{
    private Coroutine scaleRoutine;
    private Vector3 restScale;

    private void Awake() => restScale = transform.localScale;
    private void OnEnable() => restScale = transform.localScale;

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(restScale * BistroBuilderUiTokens.PressScale,
            BistroBuilderUiTokens.MotionPressSeconds * 0.55f);
    }

    public void OnPointerUp(PointerEventData eventData) => AnimateTo(restScale,
        BistroBuilderUiTokens.MotionPressSeconds);

    public void OnPointerExit(PointerEventData eventData) => AnimateTo(restScale,
        BistroBuilderUiTokens.MotionPressSeconds);

    public void OnSubmit(BaseEventData eventData)
    {
        if (isActiveAndEnabled) StartCoroutine(SubmitPulse());
    }
    private IEnumerator SubmitPulse()
    {
        AnimateTo(restScale * BistroBuilderUiTokens.PressScale,
            BistroBuilderUiTokens.MotionPressSeconds * 0.45f);
        yield return new WaitForSecondsRealtime(BistroBuilderUiTokens.MotionPressSeconds * 0.45f);
        AnimateTo(restScale, BistroBuilderUiTokens.MotionPressSeconds);
    }

    private void AnimateTo(Vector3 target, float duration)
    {
        if (!isActiveAndEnabled)
        {
            transform.localScale = target;
            return;
        }
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(target, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 from = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            transform.localScale = Vector3.LerpUnclamped(from, target, t);
            yield return null;
        }
        transform.localScale = target;
        scaleRoutine = null;
    }

    private void OnDisable()
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        transform.localScale = restScale;
    }
}

/// <summary>Fade + desplazamiento de 6 px para listas de Dropdown.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderUiDropdownMotion : MonoBehaviour, IPointerClickHandler
{    public void OnPointerClick(PointerEventData eventData)
    {
        if (isActiveAndEnabled) StartCoroutine(AnimateNextFrame());
    }

    private IEnumerator AnimateNextFrame()
    {
        yield return null;
        RectTransform list = FindNewestDropdownList();
        if (list == null) yield break;

        CanvasGroup group = list.GetComponent<CanvasGroup>();
        if (group == null) group = list.gameObject.AddComponent<CanvasGroup>();
        Vector2 target = list.anchoredPosition;
        Vector2 start = target + new Vector2(0f, BistroBuilderUiTokens.DropdownOffset);
        list.anchoredPosition = start;
        group.alpha = 0f;

        float elapsed = 0f;
        float duration = BistroBuilderUiTokens.MotionDropdownSeconds;
        while (elapsed < duration && list != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            list.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            group.alpha = t;
            yield return null;
        }
        if (list != null)
        {
            list.anchoredPosition = target;
            group.alpha = 1f;
        }
    }

    private static RectTransform FindNewestDropdownList()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int c = canvases.Length - 1; c >= 0; c--)
        {
            RectTransform[] rects = canvases[c].GetComponentsInChildren<RectTransform>(true);
            for (int i = rects.Length - 1; i >= 0; i--)
                if (rects[i] != null && rects[i].gameObject.activeInHierarchy &&
                    rects[i].name.IndexOf("Dropdown List", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return rects[i];
        }
        return null;
    }
}

/// <summary>Feedback breve para selectores numéricos +/−.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderUiValuePulse : MonoBehaviour
{
    private TMP_Text text;
    private RectTransform rect;
    private Coroutine pulse;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        rect = transform as RectTransform;
    }

    public static void Pulse(TMP_Text target, int direction)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;
        BistroBuilderUiValuePulse component = target.GetComponent<BistroBuilderUiValuePulse>();
        if (component == null) component = target.gameObject.AddComponent<BistroBuilderUiValuePulse>();
        component.Begin(direction);
    }

    private void Begin(int direction)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (rect == null) rect = transform as RectTransform;
        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(PulseRoutine(direction >= 0 ? 1 : -1));
    }

    private IEnumerator PulseRoutine(int direction)
    {
        Vector2 basePos = rect != null ? rect.anchoredPosition : Vector2.zero;
        Color baseColor = text != null ? text.color : Color.white;
        Vector2 offset = new Vector2(0f, -direction * BistroBuilderUiTokens.ValuePulseOffset);
        if (rect != null) rect.anchoredPosition = basePos + offset;
        if (text != null) text.color = Color.Lerp(baseColor, BistroBuilderUiTokens.WarmAccent, 0.42f);

        float elapsed = 0f;
        float duration = BistroBuilderUiTokens.MotionMicroSeconds;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
            if (rect != null) rect.anchoredPosition = Vector2.LerpUnclamped(basePos + offset, basePos, t);
            if (text != null) text.color = Color.Lerp(Color.Lerp(baseColor,
                BistroBuilderUiTokens.WarmAccent, 0.42f), baseColor, t);
            yield return null;
        }

        if (rect != null) rect.anchoredPosition = basePos;
        if (text != null) text.color = baseColor;
        pulse = null;
    }
}
