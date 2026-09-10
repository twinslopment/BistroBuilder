using TMPro;
using UnityEngine;

public enum BistroBuilderUiStyleRole
{
    Auto = 0,
    Background = 1,
    Surface = 2,
    SurfaceElevated = 3,
    Overlay = 4,
    PrimaryButton = 10,
    SecondaryButton = 11,
    TertiaryButton = 12,
    DestructiveButton = 13,
    NavButton = 14,
    Tab = 15,
    Field = 20,
    Row = 21,
    Title = 30,
    Heading = 31,
    Body = 32,
    Caption = 33,
    Kpi = 34,
    StatusSuccess = 40,
    StatusAttention = 41,
    StatusCritical = 42,
    StatusInfo = 43,
    BottomDock = 50
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Style Tag")]
public sealed class BistroBuilderUiStyleTag : MonoBehaviour
{    [SerializeField] private BistroBuilderUiStyleRole role = BistroBuilderUiStyleRole.Auto;
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
/// Tema intercambiable. Recoleta se inyectará aquí cuando exista el asset/licencia.
/// </summary>
[CreateAssetMenu(fileName = "BistroBuilderUiTheme", menuName = "Bistro Builder/UI/Theme")]
public sealed class BistroBuilderUiTheme : ScriptableObject
{    [Header("Tipografía")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;

    [Header("Paleta")]
    [SerializeField] private Color background = new Color32(23, 34, 32, 255);
    [SerializeField] private Color surface1 = new Color32(34, 48, 45, 248);
    [SerializeField] private Color surface2 = new Color32(45, 59, 55, 248);
    [SerializeField] private Color surfaceElevated = new Color32(56, 70, 64, 250);
    [SerializeField] private Color textPrimary = new Color32(244, 240, 231, 255);
    [SerializeField] private Color textSecondary = new Color32(189, 198, 194, 255);
    [SerializeField] private Color textMuted = new Color32(127, 137, 133, 255);
    [SerializeField] private Color primary = new Color32(23, 100, 71, 255);
    [SerializeField] private Color warmAccent = new Color32(199, 148, 88, 255);
    [SerializeField] private Color success = new Color32(63, 154, 105, 255);
    [SerializeField] private Color attention = new Color32(224, 168, 68, 255);
    [SerializeField] private Color critical = new Color32(201, 79, 72, 255);
    [SerializeField] private Color info = new Color32(87, 144, 186, 255);

    public TMP_FontAsset TitleFont => titleFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public Color Background => background;
    public Color Surface1 => surface1;
    public Color Surface2 => surface2;
    public Color SurfaceElevated => surfaceElevated;    public Color TextPrimary => textPrimary;
    public Color TextSecondary => textSecondary;
    public Color TextMuted => textMuted;
    public Color Primary => primary;
    public Color WarmAccent => warmAccent;
    public Color Success => success;
    public Color Attention => attention;
    public Color Critical => critical;
    public Color Info => info;
}