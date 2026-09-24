using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActivityRowHoverFeedback :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float DefaultLiftPixels = 2f;
    private const float DefaultBobDuration = 0.55f;
    private const float DefaultHighlightStrength = 0.055f;
    private const float ColorResponse = 18f;
    private const float ReturnResponse = 20f;

    [SerializeField] private Image background;

    private RectTransform rect;
    private Color baseColor;
    private Color hoverColor;
    private Vector2 baseAnchoredPosition;
    private bool initialized;
    private bool pointerInside;
    private bool hasBasePosition;
    private float hoverStartedAt;
    private float currentOffset;
    private float appliedOffset;

    private void Awake()
    {
        rect = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (rect == null)
            rect = transform as RectTransform;

        CaptureBasePosition();
    }

    private void OnDisable()
    {
        pointerInside = false;
        RestoreBasePosition();

        if (initialized && background != null)
            background.color = baseColor;
    }
    public void Configure(Image target)
    {
        if (target == null)
            return;

        if (initialized && ReferenceEquals(background, target))
            return;

        background = target;
        baseColor = background.color;
        hoverColor = Color.Lerp(
            baseColor,
            Color.white,
            DefaultHighlightStrength);

        initialized = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!initialized || background == null)
            return;

        pointerInside = true;
        hoverStartedAt = Time.unscaledTime;
        CaptureBasePosition();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void Update()
    {
        if (!initialized || background == null)
            return;

        float colorT = 1f - Mathf.Exp(
            -ColorResponse * Time.unscaledDeltaTime);
        Color targetColor = pointerInside ? hoverColor : baseColor;
        background.color = Color.Lerp(
            background.color,
            targetColor,
            colorT);

        float targetOffset = pointerInside
            ? EvaluateBobOffset(Time.unscaledTime - hoverStartedAt)
            : 0f;
        if (pointerInside)
        {
            currentOffset = targetOffset;
        }
        else
        {
            float motionT = 1f - Mathf.Exp(
                -ReturnResponse * Time.unscaledDeltaTime);
            currentOffset = Mathf.Lerp(
                currentOffset,
                0f,
                motionT);
        }
    }

    private void LateUpdate()
    {
        if (rect == null)
            return;

        Vector2 current = rect.anchoredPosition;
        if (!hasBasePosition)
        {
            baseAnchoredPosition = current;
            hasBasePosition = true;
        }
        else
        {
            Vector2 expected =
                baseAnchoredPosition + Vector2.up * appliedOffset;
            if ((current - expected).sqrMagnitude > 0.01f)
                baseAnchoredPosition = current;
        }

        rect.anchoredPosition =
            baseAnchoredPosition + Vector2.up * currentOffset;
        appliedOffset = currentOffset;
    }

    private float EvaluateBobOffset(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / DefaultBobDuration);
        if (t >= 1f)
            return 0f;

        if (t < 0.35f)
        {
            float phase = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            return Mathf.Lerp(0f, DefaultLiftPixels, phase);
        }
        if (t < 0.68f)
        {
            float phase = Mathf.SmoothStep(
                0f,
                1f,
                (t - 0.35f) / 0.33f);
            return Mathf.Lerp(
                DefaultLiftPixels,
                -DefaultLiftPixels * 0.5f,
                phase);
        }

        float finalPhase = Mathf.SmoothStep(
            0f,
            1f,
            (t - 0.68f) / 0.32f);
        return Mathf.Lerp(
            -DefaultLiftPixels * 0.5f,
            0f,
            finalPhase);
    }

    private void CaptureBasePosition()
    {
        if (rect == null)
            return;

        Vector2 current = rect.anchoredPosition;
        baseAnchoredPosition =
            current - Vector2.up * appliedOffset;
        hasBasePosition = true;
    }

    private void RestoreBasePosition()
    {
        if (rect != null && hasBasePosition)
            rect.anchoredPosition = baseAnchoredPosition;

        currentOffset = 0f;
        appliedOffset = 0f;
    }
}
