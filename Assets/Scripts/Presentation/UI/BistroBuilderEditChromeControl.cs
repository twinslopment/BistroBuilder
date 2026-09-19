using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared lightweight hover, selection, availability and keyboard focus for the edit bars.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditChromeControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Button button;
    Image background;
    Outline outline;
    BistroBuilderEditChromeIcon icon;
    TMP_Text label, hint;
    string explanation;
    bool hovered, focused, selected, lastEnabled;
    bool action;
    Color tint;
    static readonly Color Olive = new Color32(103,128,70,255);
    public void Configure(Button target, BistroBuilderEditChromeIcon glyph, TMP_Text text, TMP_Text tooltip, string help, Color iconColor, bool outlined)
    {
        button=target;background=target.GetComponent<Image>(); icon=glyph;label=text;hint=tooltip;explanation=help;tint=iconColor;action=outlined;
        outline=gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(.7f,-.7f);outline.useGraphicAlpha=false;
        lastEnabled=button.interactable;Refresh();
    }
    public void SetSelected(bool value) {if(selected==value)return;selected=value;Refresh();}
    void LateUpdate(){if(button!=null && lastEnabled!=button.IsInteractable()){lastEnabled=button.IsInteractable();Refresh();}}
    void Refresh()
    {
        if(button==null)return;
        bool available=button.IsInteractable();
        background.color=selected?Olive:hovered&&available?new Color32(234,231,225,255):action?new Color32(255,253,249,255):Color.clear;
        if(icon!=null)icon.color=selected?Color.white:new Color(tint.r,tint.g,tint.b,available?1f:.4f);
        if(label!=null)label.color=selected?Color.white:new Color(.14f,.15f,.14f,available?1f:.4f);
        bool keyboard=focused&&BistroBuilderPointerFeedback.KeyboardFocus;
        outline.enabled=keyboard||action;outline.effectColor=keyboard?new Color32(50,158,220,255):new Color32(226,221,212,255);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();if(hint!=null){hint.text=explanation;hint.transform.parent.gameObject.SetActive(true);}}
    public void OnPointerExit(PointerEventData e){hovered=false;Refresh();HideHint();}
    public void OnSelect(BaseEventData e){focused=true;Refresh();}
    public void OnDeselect(BaseEventData e){focused=false;Refresh();}
    void OnDisable(){hovered=focused=false;HideHint();}
    void HideHint(){if(hint!=null && hint.text==explanation)hint.transform.parent.gameObject.SetActive(false);}
}
