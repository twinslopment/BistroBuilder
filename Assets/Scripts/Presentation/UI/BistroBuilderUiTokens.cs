using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tokens canónicos del BB UI Design System. Son la única fuente de verdad visual
/// para las primitivas reutilizables de Bistro Builder.
/// </summary>
public static class BistroBuilderUiTokens
{
    public const string Revision = "21A-UIUX-PRIMITIVES-V2.0";
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    public const float Space4 = 4f;
    public const float Space8 = 8f;
    public const float Space12 = 12f;
    public const float Space16 = 16f;
    public const float Space24 = 24f;
    public const float Space32 = 32f;

    public const float ControlSmall = 32f;
    public const float ControlStandard = 40f;
    public const float ControlPrimary = 46f;
    public const float TableRow = 42f;
    public const float TabUnderline = 2f;
    public const float ProgressHeight = 6f;
    public const float PanelOutline = 1f;

    // Jerarquía tipográfica fijada en la guía visual.
    public const float FontH1 = 28f;
    public const float FontH2 = 20f;
    public const float FontH3 = 16f;
    public const float FontBody = 14f;
    public const float FontSecondary = 13f;
    public const float FontLabel = 12f;
    public const float FontCaption = 12f;
    public const float FontKpi = 24f;

    public const float MotionPressSeconds = 0.10f;
    public const float MotionMicroSeconds = 0.14f;
    public const float MotionDropdownSeconds = 0.16f;
    public const float MotionPanelSeconds = 0.18f;
    public const float MotionScreenSeconds = 0.20f;
    public const float PressScale = 0.975f;
    public const float DropdownOffset = 6f;
    public const float ValuePulseOffset = 4f;

    // Paleta fijada en la guía de componentes.
    public static readonly Color32 Background = new Color32(30, 34, 38, 255);       // #1E2226
    public static readonly Color32 Surface1 = new Color32(42, 47, 53, 248);          // #2A2F35
    public static readonly Color32 Surface2 = new Color32(58, 64, 71, 248);          // #3A4047
    public static readonly Color32 SurfaceElevated = new Color32(66, 73, 80, 252);   // elevated
    public static readonly Color32 ContentLight = new Color32(244, 239, 230, 255);   // #F4EFE6
    public static readonly Color32 TextPrimary = new Color32(230, 225, 214, 255);    // #E6E1D6
    public static readonly Color32 TextSecondary = new Color32(122, 125, 133, 255);  // #7A7D85
    public static readonly Color32 TextMuted = new Color32(104, 110, 117, 255);
    public static readonly Color32 TextOnLight = new Color32(30, 34, 38, 255);

    public static readonly Color32 Brand = new Color32(184, 149, 91, 255);           // #B8955B
    public static readonly Color32 Primary = new Color32(46, 125, 104, 255);         // #2E7D68
    public static readonly Color32 PrimaryHover = new Color32(58, 143, 119, 255);
    public static readonly Color32 PrimaryPressed = new Color32(36, 100, 83, 255);
    public static readonly Color32 SecondaryAction = new Color32(62, 71, 80, 255);   // #3E4750
    public static readonly Color32 ContextualAction = new Color32(42, 47, 53, 0);
    public static readonly Color32 WarmAccent = Brand;
    public static readonly Color32 Terracotta = new Color32(185, 100, 79, 255);
    public static readonly Color32 Success = new Color32(61, 166, 101, 255);         // #3DA665
    public static readonly Color32 Attention = new Color32(240, 171, 58, 255);       // #F0AB3A
    public static readonly Color32 Critical = new Color32(217, 83, 79, 255);         // #D9534F
    public static readonly Color32 Info = new Color32(74, 144, 226, 255);            // #4A90E2
    public static readonly Color32 Disabled = new Color32(107, 115, 123, 150);       // #6B737B
    public static readonly Color32 BorderSubtle = new Color32(90, 98, 106, 105);
    public static readonly Color32 Shadow = new Color32(0, 0, 0, 38);
    public static readonly Color32 Overlay = new Color32(8, 10, 12, 178);

    public static ColorBlock ButtonColors(Color normal, Color hover, Color pressed)
    {
        ColorBlock result = ColorBlock.defaultColorBlock;
        result.normalColor = normal;
        result.highlightedColor = hover;
        result.pressedColor = pressed;
        result.selectedColor = hover;
        result.disabledColor = Disabled;
        result.colorMultiplier = 1f;
        result.fadeDuration = 0.08f;
        return result;
    }
}
