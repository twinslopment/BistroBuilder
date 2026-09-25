using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BistroBuilderApprovedTopBarHotspot :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private Image surface;
    private Image glow;
    private Outline outline;
    private Button button;
    private RectTransform visualRoot;
    private Vector2 visualBasePosition;
    private bool selected;
    private bool hovered;
    private bool pressed;
    private Color targetSurface;
    private Color targetOutline;
    private float targetGlowAlpha;

    private static Sprite softGlowSprite;

    private static readonly Color Clear = new Color(1f, 0.89f, 0.62f, 0f);
    private static readonly Color Hover = new Color(1f, 0.84f, 0.38f, 0.11f);
    private static readonly Color Selected = new Color(1f, 0.79f, 0.28f, 0.05f);
    private static readonly Color Pressed = new Color(1f, 0.75f, 0.20f, 0.15f);

    public void Configure(Button target, RectTransform visualTarget = null)
    {
        button = target;
        visualRoot = visualTarget;
        if (visualRoot != null)
        {
            visualBasePosition = visualRoot.anchoredPosition;
            visualRoot.localScale = Vector3.one;
        }
        surface = target != null ? target.GetComponent<Image>() : null;
        if (surface == null && target != null) surface = target.gameObject.AddComponent<Image>();        if (surface != null) surface.raycastTarget = true;

        outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;

        Transform existingGlow = transform.Find("ApprovedHoverLight");
        if (existingGlow == null)
        {
            GameObject glowGo = new GameObject("ApprovedHoverLight", typeof(RectTransform));
            glowGo.transform.SetParent(transform, false);
            existingGlow = glowGo.transform;
        }

        glow = existingGlow.GetComponent<Image>();
        if (glow == null) glow = existingGlow.gameObject.AddComponent<Image>();
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(3f, 3f);
        glowRect.offsetMax = new Vector2(-3f, -3f);
        glow.sprite = SoftGlowSprite();
        glow.preserveAspect = false;
        glow.raycastTarget = false;
        glow.color = new Color(1f, 0.79f, 0.28f, 0f);
        glow.transform.SetAsLastSibling();

        Refresh();
        ApplyImmediate();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        Refresh();
    }

    public void SetInteractable(bool value)
    {
        if (button != null) button.interactable = value;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        Refresh();
    }    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        Refresh();
    }

    private void Refresh()
    {
        if (surface == null) return;

        bool enabled = button == null || button.interactable;
        targetSurface = !enabled
            ? Clear
            : pressed
                ? Pressed
                : hovered
                    ? Hover
                    : selected ? Selected : Clear;

        float outlineAlpha = !enabled
            ? 0f
            : pressed ? 0.48f
            : hovered ? 0.36f
            : selected ? 0.24f
            : 0f;
        targetOutline = new Color(0.91f, 0.66f, 0.20f, outlineAlpha);
        targetGlowAlpha = !enabled
            ? 0f
            : pressed ? 0.18f
            : hovered ? 0.14f
            : selected ? 0.05f
            : 0f;

        if (!Application.isPlaying) ApplyImmediate();
    }

    private void Update()
    {
        if (!Application.isPlaying || surface == null) return;

        float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        surface.color = Color.Lerp(surface.color, targetSurface, blend);
        if (outline != null)
            outline.effectColor = Color.Lerp(outline.effectColor, targetOutline, blend);

        if (glow != null)
        {
            Color current = glow.color;
            Color target = new Color(1f, 0.80f, 0.30f, targetGlowAlpha);
            glow.color = Color.Lerp(current, target, blend);
        }

        if (visualRoot != null)
        {
            float scale = pressed ? 0.98f : hovered ? 1.045f : 1f;
            float lift = hovered && !pressed ? 2f : 0f;
            visualRoot.localScale = Vector3.Lerp(
                visualRoot.localScale,
                Vector3.one * scale,
                blend);
            visualRoot.anchoredPosition = Vector2.Lerp(
                visualRoot.anchoredPosition,
                visualBasePosition + new Vector2(0f, lift),
                blend);
        }
    }

    private void ApplyImmediate()
    {
        if (surface != null) surface.color = targetSurface;
        if (outline != null) outline.effectColor = targetOutline;
        if (glow != null)
            glow.color = new Color(1f, 0.80f, 0.30f, targetGlowAlpha);
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one;
            visualRoot.anchoredPosition = visualBasePosition;
        }
    }

    private static Sprite SoftGlowSprite()
    {
        if (softGlowSprite != null) return softGlowSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float ny = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float falloff = Mathf.Clamp01(1f - (nx * 0.58f + ny * 0.42f));
                float alpha = Mathf.SmoothStep(0f, 1f, falloff);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        softGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        softGlowSprite.name = "BB_ApprovedTopBar_SoftGlow";
        return softGlowSprite;
    }
}