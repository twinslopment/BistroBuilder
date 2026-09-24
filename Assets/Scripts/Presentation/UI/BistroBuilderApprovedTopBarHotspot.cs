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
    private Outline outline;
    private Button button;
    private bool selected;
    private bool hovered;
    private bool pressed;

    private static readonly Color Clear = new Color(1f, 0.89f, 0.62f, 0f);
    private static readonly Color Hover = new Color(1f, 0.86f, 0.48f, 0.10f);
    private static readonly Color Selected = new Color(1f, 0.80f, 0.30f, 0.07f);
    private static readonly Color Pressed = new Color(1f, 0.76f, 0.22f, 0.14f);

    public void Configure(Button target)
    {
        button = target;
        surface = target != null ? target.GetComponent<Image>() : null;
        if (surface == null && target != null) surface = target.gameObject.AddComponent<Image>();        if (surface != null) surface.raycastTarget = true;

        outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        Refresh();
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
        Color color = !enabled
            ? Clear
            : pressed
                ? Pressed
                : selected
                    ? Selected
                    : hovered ? Hover : Clear;
        surface.color = color;

        if (outline != null)
        {
            float alpha = !enabled ? 0f : selected ? 0.42f : hovered ? 0.28f : 0f;
            outline.effectColor = new Color(0.87f, 0.63f, 0.23f, alpha);
        }
    }
}