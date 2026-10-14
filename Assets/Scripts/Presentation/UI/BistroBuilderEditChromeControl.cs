using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Selection, availability, pointer press and keyboard focus for native edit controls.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditChromeControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    Button button;
    Image background;
    Outline outline;
    BistroBuilderEditChromeIcon icon;
    TMP_Text label,hint;
    string explanation;
    bool hovered,focused,selected,pressed,lastEnabled,lastKeyboard,action,toolbar;
    Vector2 iconRest,labelRest;
    public bool IsToolbarControl=>toolbar;
    public BistroBuilderEditorV2ToolbarArt.State ToolbarState { get; private set; }
    public void Configure(Button target,BistroBuilderEditChromeIcon glyph,TMP_Text text,TMP_Text tooltip,
        string help,Color iconColor,bool outlined)
    {
        button=target;background=target.GetComponent<Image>();icon=glyph;label=text;hint=tooltip;
        explanation=help;action=outlined;
        outline=gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(.7f,-.7f);
        outline.useGraphicAlpha=false;lastEnabled=button.IsInteractable();Refresh();
    }
    public void ConfigureToolbar()
    {
        toolbar=true;iconRest=icon.rectTransform.anchoredPosition;labelRest=label.rectTransform.anchoredPosition;
        icon.ConfigureToolbarGlyph();Refresh();
    }
    public void SetSelected(bool value){if(selected==value)return;selected=value;Refresh();}
    void LateUpdate()
    {
        if(button==null)return;
        bool available=button.IsInteractable();bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        if(lastEnabled!=available || lastKeyboard!=keyboard) {
            lastEnabled=available;lastKeyboard=keyboard;if(!available)pressed=false;Refresh();
        }
    }
    void Refresh()
    {
        if(button==null)return;
        bool available=button.IsInteractable();
        if(toolbar) {
            var state=!available?BistroBuilderEditorV2ToolbarArt.State.Disabled :
                selected ? pressed&&hovered?BistroBuilderEditorV2ToolbarArt.State.SelectedPressed :
                    hovered?BistroBuilderEditorV2ToolbarArt.State.SelectedHovered:BistroBuilderEditorV2ToolbarArt.State.Selected :
                pressed&&hovered?BistroBuilderEditorV2ToolbarArt.State.Pressed :
                    hovered?BistroBuilderEditorV2ToolbarArt.State.Hovered:BistroBuilderEditorV2ToolbarArt.State.Available;
            ToolbarState=state;
            BistroBuilderEditorV2ToolbarArt.Decorate(background,action,state);
            SetContent(BistroBuilderEditorV2ToolbarArt.Colors(action,state).Ink,true);
            var offset=available&&pressed&&hovered?Vector2.down:Vector2.zero;
            icon.rectTransform.anchoredPosition=iconRest+offset;
            label.rectTransform.anchoredPosition=labelRest+offset;
            // Hover is already in the surface. Only keyboard navigation adds a focus ring.
            outline.enabled=available&&focused&&BistroBuilderPointerFeedback.KeyboardFocus;
            outline.effectDistance=new Vector2(1.2f,-1.2f);outline.effectColor=new Color32(69,132,170,255);
            return;
        }
        BistroBuilderEditorV2BrassArt.Decorate(background,action?BistroBuilderEditorV2BrassArt.Style.Destructive:
            selected?BistroBuilderEditorV2BrassArt.Style.Selected:BistroBuilderEditorV2BrassArt.Style.Button);
        background.color=available?Color.white:new Color32(242,237,224,245);
        SetContent(!available?new Color32(138,126,111,255):action?Color.white:new Color32(74,46,25,255),selected||action);
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        outline.enabled=keyboard||hovered;
        outline.effectColor=keyboard?new Color32(69,132,170,255):selected?new Color32(165,111,45,255):
            action?new Color32(135,38,30,255):new Color32(203,162,97,255);
    }
    void SetContent(Color ink,bool strong)
    {
        if(icon!=null)icon.color=ink;
        if(label==null)return;
        label.color=ink;label.font=strong?BistroBuilderEditorV2ReferenceLayout.BodyStrong:BistroBuilderEditorV2ReferenceLayout.Body;
        label.fontStyle=FontStyles.Normal;label.characterSpacing=0;
    }
    public void OnPointerEnter(PointerEventData e)
    {hovered=true;Refresh();if(hint!=null){hint.text=explanation;hint.transform.parent.gameObject.SetActive(true);}}
    public void OnPointerExit(PointerEventData e){hovered=false;Refresh();HideHint();}
    public void OnPointerDown(PointerEventData e)
    {if(e.button!=PointerEventData.InputButton.Left || button==null || !button.IsInteractable())return;pressed=true;Refresh();}
    public void OnPointerUp(PointerEventData e)
    {if(e.button!=PointerEventData.InputButton.Left)return;pressed=false;Refresh();}
    public void OnSelect(BaseEventData e){focused=true;Refresh();}
    public void OnDeselect(BaseEventData e){focused=false;Refresh();}
    void OnEnable(){Refresh();}
    void OnDisable(){hovered=focused=pressed=false;Refresh();HideHint();}
    void HideHint(){if(hint!=null&&hint.text==explanation)hint.transform.parent.gameObject.SetActive(false);}
}
