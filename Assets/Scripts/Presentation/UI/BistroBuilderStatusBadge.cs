using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BistroBuilderSemanticState { Correct, Attention, Critical, Information, Disabled }

public static class BistroBuilderStatusBadge
{
    public static string Label(BistroBuilderSemanticState state) => state == BistroBuilderSemanticState.Correct ? "Correcto" : state == BistroBuilderSemanticState.Attention ? "Atención" : state == BistroBuilderSemanticState.Critical ? "Crítico" : state == BistroBuilderSemanticState.Information ? "Información" : "Desactivado";
    public static Color Tint(BistroBuilderSemanticState state)
    {
        switch(state)
        {
            case BistroBuilderSemanticState.Correct:return new Color32(207,224,194,255);
            case BistroBuilderSemanticState.Attention:return new Color32(255,227,163,255);
            case BistroBuilderSemanticState.Critical:return new Color32(255,198,190,255);
            case BistroBuilderSemanticState.Information:return new Color32(191,224,242,255);
            default:return new Color32(210,209,205,255);
        }
    }
    public static void Apply(Image background,TMP_Text label,BistroBuilderSemanticState state,string text=null)
    {
        background.color=Tint(state);BistroBuilderSurface.Apply(background,BistroBuilderSurfaceLevel.Base);
        label.text="●  "+(text??Label(state));label.color=new Color32(38,45,32,255);
        BistroBuilderTypography.Apply(label,BistroBuilderUiStyleRole.Label);
        label.alignment=TextAlignmentOptions.Center;
    }
}
