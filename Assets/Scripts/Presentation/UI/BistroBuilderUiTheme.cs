using TMPro;
using UnityEngine;

public enum BistroBuilderUiStyleRole
{
    Auto = 0,
    Background = 1,
    Surface = 2,
    SurfaceElevated = 3,
    Overlay = 4,
    Panel = 5,
    Card = 6,
    PrimaryButton = 10,
    SecondaryButton = 11,
    ContextualButton = 12,
    TertiaryButton = 12,
    DestructiveButton = 13,
    NavButton = 14,
    Tab = 15,
    Field = 20,
    Row = 21,
    BadgeNeutral = 22,
    BadgeInfo = 23,
    BadgeSuccess = 24,
    BadgeAttention = 25,
    BadgeCritical = 26,
    ProgressTrack = 27,
    ProgressSuccess = 28,
    ProgressAttention = 29,

    // Valores 30-34 preservados: existen tags serializados de 21A V1.
    Title = 30,
    Heading = 31,
    Body = 32,
    Caption = 33,
    Kpi = 34,
    ProgressInfo = 35,
    ProgressCritical = 36,

    // Valores 40-43 preservados por compatibilidad de escenas/prefabs V1.
    StatusSuccess = 40,
    StatusAttention = 41,
    StatusCritical = 42,
    StatusInfo = 43,
    BottomDock = 50,
    Toast = 51
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Style Tag")]
public sealed class BistroBuilderUiStyleTag : MonoBehaviour
{
    [SerializeField] private BistroBuilderUiStyleRole role = BistroBuilderUiStyleRole.Auto;
    [SerializeField] private bool preserveGraphicColor;
    [SerializeField] private bool preserveFontSize;
    [SerializeField] private bool preserveFont;

    public BistroBuilderUiStyleRole Role => role;
    public bool PreserveGraphicColor => preserveGraphicColor;
    public bool PreserveFontSize => preserveFontSize;
    public bool PreserveFont => preserveFont;

    public void Configure(
        BistroBuilderUiStyleRole newRole,
        bool keepGraphicColor = false,
        bool keepFontSize = false,
        bool keepFont = false)
    {
        role = newRole;
        preserveGraphicColor = keepGraphicColor;
        preserveFontSize = keepFontSize;
        preserveFont = keepFont;
    }
}

/// <summary>
/// Tema intercambiable. Los assets de fuente solo se asignan cuando existen y están licenciados.
/// Si no hay fuente de títulos, el sistema usa la fuente de cuerpo sin crear dependencias ocultas.
/// </summary>
[CreateAssetMenu(fileName = "BistroBuilderUiTheme", menuName = "Bistro Builder/UI/Theme")]
public sealed class BistroBuilderUiTheme : ScriptableObject
{
    [Header("Tipografía")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;

    [Header("Paleta")]
    [SerializeField] private Color background = new Color32(30, 34, 38, 255);
    [SerializeField] private Color surface1 = new Color32(42, 47, 53, 248);
    [SerializeField] private Color surface2 = new Color32(58, 64, 71, 248);
    [SerializeField] private Color surfaceElevated = new Color32(66, 73, 80, 252);
    [SerializeField] private Color textPrimary = new Color32(230, 225, 214, 255);
    [SerializeField] private Color textSecondary = new Color32(122, 125, 133, 255);
    [SerializeField] private Color textMuted = new Color32(104, 110, 117, 255);
    [SerializeField] private Color brand = new Color32(184, 149, 91, 255);
    [SerializeField] private Color primary = new Color32(46, 125, 104, 255);
    [SerializeField] private Color secondaryAction = new Color32(62, 71, 80, 255);
    [SerializeField] private Color success = new Color32(61, 166, 101, 255);
    [SerializeField] private Color attention = new Color32(240, 171, 58, 255);
    [SerializeField] private Color critical = new Color32(217, 83, 79, 255);
    [SerializeField] private Color info = new Color32(74, 144, 226, 255);
    [SerializeField] private Color borderSubtle = new Color32(90, 98, 106, 105);

    public TMP_FontAsset TitleFont => titleFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public Color Background => background;
    public Color Surface1 => surface1;
    public Color Surface2 => surface2;
    public Color SurfaceElevated => surfaceElevated;
    public Color TextPrimary => textPrimary;
    public Color TextSecondary => textSecondary;
    public Color TextMuted => textMuted;
    public Color Brand => brand;
    public Color Primary => primary;
    public Color SecondaryAction => secondaryAction;
    public Color WarmAccent => brand;
    public Color Success => success;
    public Color Attention => attention;
    public Color Critical => critical;
    public Color Info => info;
    public Color BorderSubtle => borderSubtle;
}
