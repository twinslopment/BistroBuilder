using BistroBuilder.UI.Iconography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public enum BistroBuilderSurfaceState { Normal, Hover, Selected, Disabled }

/// <summary>Keyboard focus is independent from persistent game selection.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderInteractionSurface : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Selectable control;
    private BBIconButton icon;
    private Toggle toggle;
    private BistroBuilderStateGraphic graphic;
    private bool hovered, selected;
    private bool applied;
    private GameObject unavailable;
    private Image[] cardImages;
    private Material[] originalMaterials;
    private Material disabledMaterial;
    private TMP_Text[] cardTexts;
    private Color[] originalTextColors;
    public BistroBuilderSurfaceState State { get; private set; }
    public bool HasKeyboardFocus { get; private set; }
    public bool IsCard { get; set; }
    public static BistroBuilderInteractionSurface Attach(Selectable target)
    {
        if (target == null) return null;
        return target.GetComponent<BistroBuilderInteractionSurface>() ?? target.gameObject.AddComponent<BistroBuilderInteractionSurface>();
    }
    public void SetSelected(bool value) { selected = value; Refresh(); }
    private void Awake()
    {
        control = GetComponent<Selectable>(); icon = GetComponent<BBIconButton>(); toggle = GetComponent<Toggle>();
        var go = new GameObject("Interaction state", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false); go.transform.SetAsFirstSibling();
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        graphic = go.AddComponent<BistroBuilderStateGraphic>(); graphic.raycastTarget = false;
        var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-3,-3); rect.offsetMax = new Vector2(3,3);
        Refresh();
    }
    private void OnEnable() { hovered = false; applied = false; }
    private void OnDisable() { hovered = false; if (graphic != null) graphic.enabled = false; }
    private void LateUpdate() => Refresh();
    public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
    public void Refresh()
    {
        if (control == null || graphic == null) return;
        var next = !control.IsInteractable() ? BistroBuilderSurfaceState.Disabled :
            selected || (toggle != null && toggle.isOn) || (icon != null && icon.IsSelected) ? BistroBuilderSurfaceState.Selected :
            hovered ? BistroBuilderSurfaceState.Hover : BistroBuilderSurfaceState.Normal;
        bool focus = control.IsInteractable() && BistroBuilderPointerFeedback.KeyboardFocus && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        if (applied && State == next && HasKeyboardFocus == focus && graphic.Card == IsCard) return;
        applied = true; State = next; HasKeyboardFocus = focus;
        graphic.enabled = true; graphic.State = next; graphic.Focus = focus; graphic.Card = IsCard;
        // Keep the established top-bar gold selection; use the common keyboard focus ring there.
        graphic.Navigation = icon != null;
        graphic.SetVerticesDirty();
        if (IsCard) RefreshCard(next == BistroBuilderSurfaceState.Disabled);
    }
    private void RefreshCard(bool disabled)
    {
        if (unavailable == null)
        {
            cardImages = GetComponentsInChildren<Image>(true);
            cardTexts = GetComponentsInChildren<TMP_Text>(true);
            originalTextColors = new Color[cardTexts.Length];
            for(int i=0;i<cardTexts.Length;i++) originalTextColors[i]=cardTexts[i].color;
            originalMaterials = new Material[cardImages.Length];
            for (int i=0;i<cardImages.Length;i++) originalMaterials[i]=cardImages[i].material;
            var shader = Resources.Load<Shader>("BistroBuilder/UI/DisabledCard");
            if (shader != null) disabledMaterial = new Material(shader);
            unavailable = new GameObject("Unavailable badge", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(Image));
            unavailable.transform.SetParent(transform,false); unavailable.GetComponent<LayoutElement>().ignoreLayout=true;
            var background=unavailable.GetComponent<Image>();background.color=new Color(0.30f,0.31f,0.29f,0.96f); background.raycastTarget=false;
            var rect=(RectTransform)unavailable.transform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.offsetMin=new Vector2(6,6);rect.offsetMax=new Vector2(-6,27);
            var labelObject=new GameObject("Label",typeof(RectTransform));labelObject.transform.SetParent(unavailable.transform,false);
            var label=labelObject.AddComponent<TextMeshProUGUI>();label.text="× No disponible";label.fontSize=12;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            var labelRect=(RectTransform)label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        }
        unavailable.SetActive(disabled);
        for(int i=0;i<cardImages.Length;i++) if(cardImages[i]!=null) cardImages[i].material=disabled && disabledMaterial!=null ? disabledMaterial : originalMaterials[i];
        for(int i=0;i<cardTexts.Length;i++) if(cardTexts[i]!=null) cardTexts[i].color=State==BistroBuilderSurfaceState.Selected ? Color.white : disabled ? new Color(0.48f,0.48f,0.46f) : originalTextColors[i];
    }
    private void OnDestroy() { if(disabledMaterial!=null) Destroy(disabledMaterial); }
}
