using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tokens canónicos del BB UI Design System v1.0.
/// Solo Presentation: no contiene decisiones de gameplay.
/// </summary>
public static class BistroBuilderUiTokens
{
    public const string Revision = "21C-TYPOGRAPHY-DEPTH";
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

    public const float FontH1 = 34f;
    public const float FontH2 = 24f;
    public const float FontH3 = 18f;
    public const float FontBody = 15f;
    public const float FontLabel = 14f;
    public const float FontCaption = 12f;
    public const float FontKpi = 27f;
    public const float MotionPressSeconds = 0.10f;
    public const float MotionMicroSeconds = 0.14f;
    public const float MotionDropdownSeconds = 0.16f;
    public const float MotionPanelSeconds = 0.18f;
    public const float MotionScreenSeconds = 0.20f;
    public const float ScrollWheelStepPixels = 84f;
    public const float ScrollSmoothingRate = 16f;
    public const float ScrollDecelerationRate = 0.08f;
    public const float ScrollMaximumNotchesPerEvent = 3f;
    public const float PressScale = 0.975f;
    public const float DropdownOffset = 6f;
    public const float ValuePulseOffset = 4f;

    // Base aprobada: cálida, viva y neutra respecto al nivel del restaurante.
    public static readonly Color32 Background = new Color32(29, 27, 23, 255);
    public static readonly Color32 Surface1 = new Color32(48, 42, 34, 255);
    public static readonly Color32 Surface2 = new Color32(83, 72, 58, 255);
    public static readonly Color32 SurfaceElevated = new Color32(218, 200, 177, 255);
    public static readonly Color32 ContentLight = new Color32(244, 240, 231, 255);  // #F4F0E7
    public static readonly Color32 TextPrimary = new Color32(244, 240, 231, 255);
    public static readonly Color32 TextSecondary = new Color32(213, 203, 188, 255);
    public static readonly Color32 TextMuted = new Color32(165, 155, 140, 255);
    public static readonly Color32 TextOnLight = new Color32(23, 35, 33, 255);

    public static readonly Color32 Primary = new Color32(74, 99, 53, 255);
    public static readonly Color32 PrimaryHover = new Color32(91, 118, 67, 255);
    public static readonly Color32 PrimaryPressed = new Color32(55, 77, 38, 255);
    public static readonly Color32 WarmAccent = new Color32(199, 148, 88, 255);     // #C79458
    public static readonly Color32 Terracotta = new Color32(185, 100, 79, 255);     // #B9644F
    public static readonly Color32 Success = new Color32(135, 178, 104, 255);
    public static readonly Color32 Attention = new Color32(235, 175, 46, 255);
    public static readonly Color32 Critical = new Color32(239, 105, 92, 255);
    public static readonly Color32 Info = new Color32(107, 187, 226, 255);
    public static readonly Color32 Disabled = new Color32(145, 142, 136, 180);
    public static readonly Color32 Overlay = new Color32(8, 12, 11, 178);

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
