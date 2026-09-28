using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BistroBuilderApprovedTopBarHotspot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private const float ResponseSeconds = .09f;
    private Button button;
    private BistroBuilderTopBarPlate plate;
    private RectTransform icon;
    private Canvas canvas;
    private BistroBuilderTopBarArtEntry motion;
    private bool selected, hovered, pressed, focused;
    private float amount, clock;
    public float HoverAmount => amount;

    public void Configure(Button target, RectTransform artwork, BistroBuilderTopBarArtEntry animation)
    {
        button=target; icon=artwork; motion=animation; canvas=GetComponentInParent<Canvas>();
        var surface=new GameObject("ApprovedCell",typeof(RectTransform),typeof(CanvasRenderer));surface.transform.SetParent(transform,false);surface.transform.SetAsFirstSibling();
        plate=surface.AddComponent<BistroBuilderTopBarPlate>();var rect=plate.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        plate.Cell=true; plate.raycastTarget=false;
    }
    public void SetSelected(bool value) => selected=value;
    public void SetInteractable(bool value) { if(button!=null)button.interactable=value; }
    public void OnPointerEnter(PointerEventData e) { hovered=true; BeginResponse(); }
    public void OnPointerExit(PointerEventData e) { hovered=false; pressed=false; }
    public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left){pressed=true; ApplyState();} }
    public void OnPointerUp(PointerEventData e) { pressed=false; ApplyState(); }
    public void OnSelect(BaseEventData e) { focused=true; if(BistroBuilderPointerFeedback.KeyboardFocus)BeginResponse(); }
    public void OnDeselect(BaseEventData e) => focused=false;

    private void BeginResponse()
    {
        if(button==null||!button.IsInteractable())return;
        clock=0;
        // Visible feedback in the pointer event's frame, before the settling animation.
        amount=Mathf.Max(amount,.4f);
        ApplyState();
    }
    private void Update()
    {
        bool enabled=button!=null&&button.IsInteractable();
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        amount=Mathf.MoveTowards(amount,enabled&&(hovered||keyboard)?1:0,Time.unscaledDeltaTime/ResponseSeconds);
        clock+=Time.unscaledDeltaTime;
        ApplyState();
    }
    private void ApplyState()
    {
        bool enabled=button!=null&&button.IsInteractable();
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        if(plate!=null)plate.State(enabled?amount:0,enabled&&selected?1:0,enabled&&keyboard?1:0);
        if(icon==null||motion==null)return;
        // The main lift responds immediately; only a small idle motion follows its cadence.
        float wave=enabled?amount*(.85f+.15f*Mathf.Sin(clock*2*Mathf.PI/Mathf.Max(.1f,motion.duration))):0;
        bool reduced=BistroBuilderOptionsScreen.ReducedMotion;
        if(reduced)wave=0;
        float scale=1+(motion.zoom-1)*wave;
        if(pressed&&enabled&&!reduced)scale=.96f;
        icon.localScale=new Vector3(scale,scale,1);
        icon.localRotation=Quaternion.Euler(0,0,-motion.rotation*wave);
        float pixelScale=canvas!=null?Mathf.Max(.01f,canvas.scaleFactor):1;
        icon.anchoredPosition=new Vector2(motion.dx,-motion.dy)*wave*.65f/pixelScale;
    }
    private void OnDisable()
    {
        hovered=pressed=focused=false; amount=0;
        if(plate!=null)plate.State(0,0,0);
        if(icon!=null){icon.anchoredPosition=Vector2.zero;icon.localRotation=Quaternion.identity;icon.localScale=Vector3.one;}
    }
}