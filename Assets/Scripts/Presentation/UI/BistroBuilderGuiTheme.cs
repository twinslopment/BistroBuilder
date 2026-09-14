using UnityEngine;

public static class BistroBuilderGuiTheme
{
    private static GUISkin skin;
    private static Texture2D Texture(Color color)
    {var texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();return texture;}
    public static void Apply()
    {
        if(skin==null)
        {
            skin=Object.Instantiate(GUI.skin);skin.font=BistroBuilderTypography.LegacyBody;
            skin.label.fontSize=15;skin.label.normal.textColor=BistroBuilderUiTokens.TextPrimary;
            skin.box.normal.background=Texture(BistroBuilderUiTokens.Surface1);
            skin.box.border=new RectOffset(1,1,1,1);skin.box.padding=new RectOffset(18,18,18,18);
            foreach(var style in new[]{skin.button,skin.textField,skin.textArea,skin.toggle})
            {
                style.font=BistroBuilderTypography.LegacyBody;style.fontSize=14;
                style.normal.background=Texture(BistroBuilderUiTokens.Surface2);style.normal.textColor=BistroBuilderUiTokens.TextPrimary;
                style.hover.background=Texture(BistroBuilderUiTokens.PrimaryHover);style.hover.textColor=BistroBuilderUiTokens.TextPrimary;
                style.active.background=Texture(BistroBuilderUiTokens.Primary);style.active.textColor=Color.white;
                style.focused.background=Texture(BistroBuilderUiTokens.Surface2);style.focused.textColor=Color.white;
            }
        }
        GUI.skin=skin;
    }
}
