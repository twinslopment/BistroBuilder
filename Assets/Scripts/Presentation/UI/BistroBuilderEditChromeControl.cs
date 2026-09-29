using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared lightweight hover, selection, availability and keyboard focus for the edit bars.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditChromeControl : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    Button button;
    Image background;
    Outline outline;
    BistroBuilderEditChromeIcon icon;
    BistroBuilderTopBarPlate topBarPlate;
    TMP_Text label, hint;
    string explanation;
    bool hovered, pressed, focused, selected, lastEnabled;
    bool action;
    Color tint;
    static readonly Color Olive = new Color32(103,128,70,255);
    public void Configure(Button target, BistroBuilderEditChromeIcon glyph, TMP_Text text, TMP_Text tooltip, string help, Color iconColor, bool outlined)
    {
        button=target;background=target.GetComponent<Image>(); icon=glyph;label=text;hint=tooltip;explanation=help;tint=iconColor;action=outlined;
        topBarPlate=target.GetComponentInChildren<BistroBuilderTopBarPlate>(true);
        outline=gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(.7f,-.7f);outline.useGraphicAlpha=false;
        lastEnabled=button.interactable;Refresh();
    }
    public void SetSelected(bool value) {if(selected==value)return;selected=value;Refresh();}
    void OnEnable(){Refresh();}
    void LateUpdate(){if(button!=null && lastEnabled!=button.IsInteractable()){lastEnabled=button.IsInteractable();Refresh();}}
    void Refresh()
    {
        if(button==null)return;

        bool available=button.IsInteractable();
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        Color actionInk=available?new Color32(48,70,33,255):new Color32(99,101,94,255);

        if(topBarPlate!=null)
        {
            // La barra de edición comparte exactamente el lenguaje de superficie
            // de la navegación normal. El botón transparente solo conserva el
            // hit target; BistroBuilderTopBarPlate dibuja hover/selección/foco.
            background.color=Color.clear;
            topBarPlate.State(
                available&&hovered?1f:0f,
                available&&selected?1f:0f,
                available&&keyboard?1f:0f);

            Color topInk=
                available
                    ? selected
                        ? Olive
                        : new Color32(73,62,47,255)
                    : new Color32(135,132,124,190);

            if(icon!=null)icon.color=topInk;
            if(label!=null)label.color=topInk;

            outline.enabled=keyboard;
            outline.effectColor=new Color32(50,158,220,255);

            if(icon!=null)
            {
                float scale=1f;
                if(available&&!BistroBuilderOptionsScreen.ReducedMotion)
                {
                    if(pressed)scale=.94f;
                    else if(hovered)scale=1.035f;
                }
                icon.rectTransform.localScale=new Vector3(scale,scale,1f);
            }

            return;
        }

        background.color=selected?Olive:hovered&&available?new Color32(234,231,225,255):action?(available?new Color32(231,239,220,255):new Color32(228,225,218,255)):Color.clear;
        if(icon!=null)icon.color=selected?Color.white:action?actionInk:new Color(tint.r,tint.g,tint.b,available?1f:.65f);
        if(label!=null)label.color=selected?Color.white:action?actionInk:new Color(.14f,.15f,.14f,available?1f:.65f);
        outline.enabled=keyboard||action;outline.effectColor=keyboard?new Color32(50,158,220,255):available?new Color32(107,130,77,255):new Color32(156,156,145,255);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();if(hint!=null){hint.text=explanation;hint.transform.parent.gameObject.SetActive(true);}}
    public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;Refresh();HideHint();}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left){pressed=true;Refresh();}}
    public void OnPointerUp(PointerEventData e){pressed=false;Refresh();}
    public void OnSelect(BaseEventData e){focused=true;Refresh();}
    public void OnDeselect(BaseEventData e){focused=false;Refresh();}
    void OnDisable()
    {
        hovered=pressed=focused=false;
        if(topBarPlate!=null)topBarPlate.State(0f,0f,0f);
        if(icon!=null)icon.rectTransform.localScale=Vector3.one;
        HideHint();
    }
    void HideHint(){if(hint!=null && hint.text==explanation)hint.transform.parent.gameObject.SetActive(false);}
}
