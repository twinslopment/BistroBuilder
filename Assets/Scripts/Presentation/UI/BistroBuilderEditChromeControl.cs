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
    static readonly Color Honey = new Color32(236,183,92,255);
    static readonly Color Brass = new Color32(165,111,45,255);
    static readonly Color Destructive = new Color32(164,47,39,255);
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
        // Premium ivory/brass treatment: honey is selection, red only destructive.
        bool destructive = action;
        Color disabled = new Color32(220,210,191,255);
        background.color = !available ? disabled
            : selected ? Honey
            : destructive ? (hovered ? new Color32(181,59,48,255) : Destructive)
            : hovered ? new Color32(244,224,186,255)
            : new Color32(252,244,232,255);
        Color contentColor = !available ? new Color32(138,126,111,255)
            : destructive ? Color.white : new Color32(74,46,25,255);
        if (icon != null) icon.color = contentColor;
        if (label != null) label.color = contentColor;
        bool keyboard = focused && BistroBuilderPointerFeedback.KeyboardFocus;
        outline.enabled = keyboard || selected || hovered || destructive;
        outline.effectColor = keyboard ? new Color32(69,132,170,255)
            : selected ? Brass : destructive ? new Color32(135,38,30,255)
            : new Color32(203,162,97,255);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();if(hint!=null){hint.text=explanation;hint.transform.parent.gameObject.SetActive(true);}}
    public void OnPointerExit(PointerEventData e){hovered=false;Refresh();HideHint();}
    public void OnSelect(BaseEventData e){focused=true;Refresh();}
    public void OnDeselect(BaseEventData e){focused=false;Refresh();}
    void OnDisable(){hovered=focused=false;HideHint();}
    void HideHint(){if(hint!=null && hint.text==explanation)hint.transform.parent.gameObject.SetActive(false);}
}
