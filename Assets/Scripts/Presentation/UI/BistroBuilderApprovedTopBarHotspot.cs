using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BistroBuilderApprovedTopBarHotspot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private BistroBuilderTopBarPlate plate;
    private RectTransform icon;
    private BistroBuilderTopBarArtEntry motion;
    private bool selected, hovered, pressed, focused;
    private float amount, clock;
    public float HoverAmount => amount;
    public void Configure(Button target, RectTransform artwork, BistroBuilderTopBarArtEntry animation)
    {
        button=target; icon=artwork; motion=animation;
        var surface=new GameObject("ApprovedCell",typeof(RectTransform),typeof(CanvasRenderer));surface.transform.SetParent(transform,false);surface.transform.SetAsFirstSibling();
        plate=surface.AddComponent<BistroBuilderTopBarPlate>();var rect=plate.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        plate.Cell=true; plate.raycastTarget=false;
    }
    public void SetSelected(bool value) => selected=value;
    public void SetInteractable(bool value) { if(button!=null)button.interactable=value; }
    public void OnPointerEnter(PointerEventData e) { hovered=true; clock=0; }
    public void OnPointerExit(PointerEventData e) { hovered=false; pressed=false; }
    public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left)pressed=true; }
    public void OnPointerUp(PointerEventData e) => pressed=false;
    public void OnSelect(BaseEventData e) => focused=true;
    public void OnDeselect(BaseEventData e) => focused=false;
    private void Update()
    {
        bool enabled=button!=null&&button.IsInteractable();
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        amount=Mathf.MoveTowards(amount,enabled&&(hovered||keyboard)?1:0,Time.unscaledDeltaTime/.17f);
        clock+=Time.unscaledDeltaTime;
        if(plate!=null)plate.State(amount,enabled&&selected?1:0,enabled&&keyboard?1:0);
        if(icon==null||motion==null)return;
        float wave=(1-Mathf.Cos(clock*Mathf.PI/Mathf.Max(.1f,motion.duration)))*.5f*amount;
        if(BistroBuilderOptionsScreen.ReducedMotion)wave=0;
        float scale=1+(motion.zoom-1)*wave;
        if(pressed&&enabled&&!BistroBuilderOptionsScreen.ReducedMotion)scale=.96f;
        icon.localScale=new Vector3(scale,scale,1);
        icon.localRotation=Quaternion.Euler(0,0,-motion.rotation*wave);
        // Artwork is anchored at the centre of its own fixed-size holder.
        icon.anchoredPosition=new Vector2(motion.dx,-motion.dy)*wave*.45f;
    }
    private void OnDisable()
    {
        hovered=pressed=focused=false; amount=0;
        if(icon!=null){icon.anchoredPosition=Vector2.zero;icon.localRotation=Quaternion.identity;icon.localScale=Vector3.one;}
    }
}
