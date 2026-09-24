using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Local presentation feedback: never sends commands to game systems.
public sealed class BistroBuilderOpeningButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public RectTransform Icon;
    public bool IsBell;
    private bool hover, focused, pressed, captured;
    private Vector2 iconOrigin;
    private float pulseUntil;
    private Button button;
    private Outline outline;
    private void Awake() { button = GetComponent<Button>(); outline = GetComponent<Outline>(); }
    private void Update()
    {
        bool enabledAction = button != null && button.interactable;
        if (Icon != null)
        {
            if (!captured) { iconOrigin = Icon.anchoredPosition; captured = true; }
            bool push = enabledAction && (pressed || Time.unscaledTime < pulseUntil);
            Vector2 offset = !enabledAction || BistroBuilderOptionsScreen.ReducedMotion ? Vector2.zero : push ? new Vector2(IsBell ? 0 : -9, -2) :
                (hover || focused) ? new Vector2(IsBell ? 0 : -5, IsBell ? 5 : 0) : Vector2.zero;
            Icon.anchoredPosition = Vector2.Lerp(Icon.anchoredPosition, iconOrigin + offset, Mathf.Min(1, Time.unscaledDeltaTime * 20));
            Icon.localRotation = Quaternion.Euler(0, 0, push && IsBell && !BistroBuilderOptionsScreen.ReducedMotion ? Mathf.Sin(Time.unscaledTime * 55) * 6 : 0);
        }
        if (outline != null) outline.effectDistance = focused ? new Vector2(3, -3) : new Vector2(1, -1);
    }
    private void OnDisable()
    { hover = focused = pressed = false; pulseUntil = 0; if (captured && Icon != null) { Icon.anchoredPosition = iconOrigin; Icon.localRotation = Quaternion.identity; } }
    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; pulseUntil = Time.unscaledTime + .25f; }
    public void OnSelect(BaseEventData e) => focused = true;
    public void OnDeselect(BaseEventData e) => focused = false;
    public void OnSubmit(BaseEventData e) => pulseUntil = Time.unscaledTime + .25f;
}
